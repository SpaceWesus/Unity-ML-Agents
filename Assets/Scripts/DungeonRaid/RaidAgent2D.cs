using System;
using System.Collections.Generic;
using UnityEngine;

namespace Turtle.DungeonRaid
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public sealed class RaidAgent2D : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private string agentId = "agent";
        [SerializeField] private string displayName = "Agent";
        [SerializeField] private RaidFaction faction;
        [SerializeField] private RaidCombatRole role;

        [Header("Combat")]
        [SerializeField, Min(1f)] private float maximumHealth = 100f;
        [SerializeField, Min(0f)] private float maximumMana = 50f;
        [SerializeField, Min(0f)] private float manaRegenerationPerSecond = 3f;
        [SerializeField, Min(0.1f)] private float moveSpeed = 4f;
        [SerializeField, Min(0.1f)] private float collisionRadius = 0.45f;
        [SerializeField, Min(0f)] private float basicAttackDamage = 12f;
        [SerializeField, Min(0.1f)] private float basicAttackRange = 1.4f;
        [SerializeField, Min(0.1f)] private float preferredCombatRange = 1.1f;
        [SerializeField, Min(0.05f)] private float basicAttackCooldown = 0.9f;
        [SerializeField] private bool rangedBasicAttack;
        [SerializeField, Min(1f)] private float downedSeconds = 8f;
        [SerializeField] private List<RaidAbilitySpec> abilities = new();

        [Header("Presentation")]
        [SerializeField] private SpriteRenderer bodyRenderer;
        [SerializeField] private Color identityColor = Color.white;
        [SerializeField] private Rigidbody2D physicsBody;
        [SerializeField] private CircleCollider2D hurtbox;
        [SerializeField] private bool compactPresentation;
        [SerializeField] private bool useLightweightPhysics;
        [SerializeField] private bool useTriggerHurtbox;
        [SerializeField] private bool usesDownedState = true;

        [Header("Navigation")]
        [SerializeField] private DungeonNavigationGrid2D navigation;
        [SerializeField, Min(0.1f)] private float navigationRepathInterval = 0.35f;
        [SerializeField, Min(0.1f)] private float navigationDestinationTolerance = 1f;

        private readonly Dictionary<string, float> abilityReadyAt = new(StringComparer.Ordinal);
        private Vector2 spawnPosition;
        private Vector3 baseLocalScale;
        private Vector2 desiredDestination;
        private bool hasDestination;
        private float destinationStopRadius;
        private float currentHealth;
        private float currentMana;
        private float currentShield;
        private float basicAttackReadyAt;
        private float downedUntil;
        private float flashUntil;
        private float punchUntil;
        private RaidAgent2D forcedTarget;
        private float forcedTargetUntil;
        private float forcedTargetDamageMultiplier = 1f;
        private float shieldExpiresAt;
        private float stunnedUntil;
        private float damageBuffMultiplier = 1f;
        private float damageBuffExpiresAt;
        private RaidAgent2D empoweredTarget;
        private float empoweredTargetMultiplier = 1f;
        private float empoweredTargetExpiresAt;
        private float vulnerabilityMultiplier = 1f;
        private float vulnerabilityExpiresAt;
        private RaidAgent2D damageOverTimeSource;
        private float damageOverTimePerSecond;
        private float damageOverTimeExpiresAt;
        private float nextDamageOverTimeTick;
        private float currentRaidTime;
        private RaidLifeState lifeState;
        private SpriteRenderer shadowRenderer;
        private SpriteRenderer healthBackground;
        private SpriteRenderer healthFill;
        private SpriteRenderer manaFill;
        private SpriteRenderer shieldRenderer;
        private readonly RaycastHit2D[] movementCastHits = new RaycastHit2D[12];
        private float avoidanceHandedness = 1f;
        private readonly List<Vector2> navigationPath = new(64);
        private int navigationPathIndex;
        private Vector2 plannedNavigationDestination;
        private float nextNavigationRepathAt;

        public string AgentId => agentId;
        public string DisplayName => displayName;
        public RaidFaction Faction => faction;
        public RaidCombatRole Role => role;
        public RaidLifeState LifeState => lifeState;
        public float CurrentHealth => currentHealth;
        public float MaximumHealth => maximumHealth;
        public float CurrentMana => currentMana;
        public float CurrentShield => currentShield;
        public bool HasTemporaryShield => currentShield > 0f && currentRaidTime < shieldExpiresAt;
        public bool IsShieldVisualVisible => shieldRenderer != null && shieldRenderer.enabled;
        public float HealthRatio => maximumHealth <= 0f ? 0f : currentHealth / maximumHealth;
        public float MaximumMana => maximumMana;
        public float MoveSpeed => moveSpeed;
        public float CollisionRadius => hurtbox != null
            ? Mathf.Max(hurtbox.bounds.extents.x, hurtbox.bounds.extents.y)
            : collisionRadius;
        public float BasicAttackDamage => basicAttackDamage;
        public float BasicAttackRange => basicAttackRange;
        public float PreferredCombatRange => preferredCombatRange;
        public bool RangedBasicAttack => rangedBasicAttack;
        public IReadOnlyList<RaidAbilitySpec> Abilities => abilities;
        public bool CanReceiveDamage => isActiveAndEnabled &&
                                        lifeState == RaidLifeState.Active && currentHealth > 0f;
        public bool CanAct => isActiveAndEnabled &&
                              lifeState == RaidLifeState.Active && currentHealth > 0f &&
                              currentRaidTime >= stunnedUntil;
        public bool CanBeRescued => lifeState == RaidLifeState.Downed;
        public Vector2 Position => physicsBody != null ? physicsBody.position : transform.position;
        public DungeonNavigationGrid2D Navigation => navigation;

        public event Action<RaidAgent2D, RaidAgent2D, float> Damaged;
        public event Action<RaidAgent2D, RaidLifeState> LifeStateChanged;

        private void Awake()
        {
            if (bodyRenderer == null)
            {
                bodyRenderer = GetComponent<SpriteRenderer>();
            }
            EnsurePhysics();
            EnsurePresentation();
            spawnPosition = transform.position;
            baseLocalScale = transform.localScale;
            avoidanceHandedness = StableAvoidanceHandedness(agentId);
            ResetForRaid();
        }

        private void OnDisable()
        {
            StopMoving();
            forcedTarget = null;
        }

        private void FixedUpdate()
        {
            if (CanAct)
            {
                StepMovement(Time.fixedDeltaTime);
            }
        }

        public void ResetForRaid()
        {
            EnsurePhysics();
            physicsBody.position = spawnPosition;
            physicsBody.linearVelocity = Vector2.zero;
            physicsBody.angularVelocity = 0f;
            transform.localScale = baseLocalScale;
            currentHealth = maximumHealth;
            currentMana = maximumMana;
            currentShield = 0f;
            basicAttackReadyAt = 0f;
            downedUntil = 0f;
            flashUntil = 0f;
            punchUntil = 0f;
            forcedTarget = null;
            forcedTargetUntil = 0f;
            forcedTargetDamageMultiplier = 1f;
            shieldExpiresAt = 0f;
            stunnedUntil = 0f;
            damageBuffMultiplier = 1f;
            damageBuffExpiresAt = 0f;
            empoweredTarget = null;
            empoweredTargetMultiplier = 1f;
            empoweredTargetExpiresAt = 0f;
            vulnerabilityMultiplier = 1f;
            vulnerabilityExpiresAt = 0f;
            damageOverTimeSource = null;
            damageOverTimePerSecond = 0f;
            damageOverTimeExpiresAt = 0f;
            nextDamageOverTimeTick = 0f;
            currentRaidTime = 0f;
            abilityReadyAt.Clear();
            lifeState = RaidLifeState.Active;
            hasDestination = false;
            InvalidateNavigationPath();
            if (hurtbox != null) hurtbox.enabled = true;
            UpdatePresentation(0f);
        }

        public void CaptureSpawnPosition()
        {
            spawnPosition = transform.position;
            baseLocalScale = transform.localScale;
        }

        public void PlaceAt(Vector2 position, bool captureAsSpawn = true)
        {
            EnsurePhysics();
            physicsBody.position = position;
            physicsBody.linearVelocity = Vector2.zero;
            physicsBody.angularVelocity = 0f;
            transform.position = new Vector3(position.x, position.y, transform.position.z);
            if (captureAsSpawn) CaptureSpawnPosition();
        }

        public void Step(float deltaTime, float raidTime, DungeonRaidDirector2D raid)
        {
            currentRaidTime = raidTime;
            if (lifeState == RaidLifeState.Downed && raidTime >= downedUntil)
            {
                SetLifeState(RaidLifeState.Dead);
            }

            if (CanAct)
            {
                currentMana = Mathf.Min(
                    maximumMana,
                    currentMana + manaRegenerationPerSecond * Mathf.Max(0f, deltaTime));
            }
            else
            {
                hasDestination = false;
                InvalidateNavigationPath();
            }
            StepStatuses(raidTime, raid);
            UpdatePresentation(raidTime);
        }

        public void MoveToward(Vector2 destination, float stopRadius = 0.1f)
        {
            if (!CanAct) return;
            var destinationChanged = !hasDestination ||
                                     Vector2.SqrMagnitude(desiredDestination - destination) >
                                     navigationDestinationTolerance * navigationDestinationTolerance;
            desiredDestination = destination;
            destinationStopRadius = Mathf.Max(0f, stopRadius);
            hasDestination = true;
            if (destinationChanged) nextNavigationRepathAt = 0f;
        }

        public void StopMoving()
        {
            hasDestination = false;
            InvalidateNavigationPath();
        }

        public void BindNavigation(DungeonNavigationGrid2D assignedNavigation)
        {
            if (navigation == assignedNavigation) return;
            navigation = assignedNavigation;
            InvalidateNavigationPath();
        }

        public void IgnoreFriendlyCollisionWith(RaidAgent2D other)
        {
            if (other == null || other == this || other.faction != faction) return;
            EnsurePhysics();
            other.EnsurePhysics();
            if (hurtbox != null && other.hurtbox != null)
            {
                Physics2D.IgnoreCollision(hurtbox, other.hurtbox, true);
            }
        }

        public void Nudge(Vector2 displacement)
        {
            if (!CanAct || displacement.sqrMagnitude <= 0.000001f) return;
            hasDestination = false;
            InvalidateNavigationPath();
            if (physicsBody.bodyType == RigidbodyType2D.Dynamic)
            {
                physicsBody.AddForce(displacement * 10f, ForceMode2D.Impulse);
            }
            else
            {
                physicsBody.position += displacement;
            }
        }

        public void TeleportNear(Vector2 destination, Vector2 awayFromTarget)
        {
            if (!CanAct) return;
            var direction = awayFromTarget.sqrMagnitude > 0.001f
                ? awayFromTarget.normalized
                : Vector2.left;
            var next = destination + direction * Mathf.Max(0.8f, CollisionRadius * 2f);
            physicsBody.position = next;
            hasDestination = false;
            InvalidateNavigationPath();
        }

        public bool CanBasicAttack(RaidAgent2D target, float raidTime)
        {
            return CanAct && target != null && target.CanReceiveDamage &&
                   target.faction != faction && raidTime >= basicAttackReadyAt &&
                   Vector2.Distance(Position, target.Position) <= basicAttackRange;
        }

        public bool TryBasicAttack(
            RaidAgent2D target,
            float raidTime,
            DungeonRaidDirector2D raid)
        {
            if (!CanBasicAttack(target, raidTime)) return false;
            basicAttackReadyAt = raidTime + basicAttackCooldown;
            StopMoving();
            faceTarget(target.Position);
            punchUntil = raidTime + 0.16f;
            var attackColor = faction == RaidFaction.Hunters
                ? identityColor
                : new Color(1f, 0.22f, 0.08f);
            if (rangedBasicAttack)
            {
                raid?.Effects?.EmitProjectile(Position, target.Position, attackColor, 0.22f);
            }
            else
            {
                raid?.Effects?.EmitArc(Position, target.Position, attackColor, 1.25f, 0.2f);
            }
            return raid != null &&
                   raid.ResolveBasicAttack(this, target, basicAttackDamage, raidTime);
        }

        public bool IsAbilityReady(RaidAbilitySpec ability, float raidTime)
        {
            if (!CanAct || ability == null || currentMana + 0.001f < ability.manaCost)
            {
                return false;
            }
            return !abilityReadyAt.TryGetValue(ability.id ?? string.Empty, out var readyAt) ||
                   raidTime >= readyAt;
        }

        public float GetAbilityCooldownRemaining(RaidAbilitySpec ability, float raidTime)
        {
            if (ability == null ||
                !abilityReadyAt.TryGetValue(ability.id ?? string.Empty, out var readyAt))
            {
                return 0f;
            }
            return Mathf.Max(0f, readyAt - raidTime);
        }

        public RaidAbilityAvailability GetAbilityAvailability(
            RaidAbilitySpec ability,
            float raidTime)
        {
            if (!CanAct || ability == null) return RaidAbilityAvailability.Incapacitated;
            if (GetAbilityCooldownRemaining(ability, raidTime) > 0f)
            {
                return RaidAbilityAvailability.Cooldown;
            }
            return currentMana + 0.001f >= ability.manaCost
                ? RaidAbilityAvailability.Ready
                : RaidAbilityAvailability.InsufficientMana;
        }

        public void CollectActiveStatusEffects(
            float raidTime,
            List<RaidStatusEffectSnapshot> results)
        {
            if (results == null) return;
            results.Clear();
            if (lifeState == RaidLifeState.Dead) return;
            if (lifeState == RaidLifeState.Downed)
            {
                results.Add(new RaidStatusEffectSnapshot(
                    RaidStatusEffectKind.Downed,
                    downedUntil - raidTime));
            }
            if (currentShield > 0f && raidTime < shieldExpiresAt)
            {
                results.Add(new RaidStatusEffectSnapshot(
                    RaidStatusEffectKind.TemporaryShield,
                    shieldExpiresAt - raidTime));
            }
            if (forcedTarget != null && forcedTarget.CanReceiveDamage && raidTime < forcedTargetUntil)
            {
                results.Add(new RaidStatusEffectSnapshot(
                    RaidStatusEffectKind.Taunted,
                    forcedTargetUntil - raidTime));
            }
            if (raidTime < stunnedUntil)
            {
                results.Add(new RaidStatusEffectSnapshot(
                    RaidStatusEffectKind.Stunned,
                    stunnedUntil - raidTime));
            }
            if (damageBuffMultiplier > 1.0001f && raidTime < damageBuffExpiresAt)
            {
                results.Add(new RaidStatusEffectSnapshot(
                    RaidStatusEffectKind.DamageUp,
                    damageBuffExpiresAt - raidTime));
            }
            if (empoweredTarget != null && empoweredTarget.CanReceiveDamage &&
                raidTime < empoweredTargetExpiresAt)
            {
                results.Add(new RaidStatusEffectSnapshot(
                    RaidStatusEffectKind.Empowered,
                    empoweredTargetExpiresAt - raidTime));
            }
            if (vulnerabilityMultiplier > 1.0001f && raidTime < vulnerabilityExpiresAt)
            {
                results.Add(new RaidStatusEffectSnapshot(
                    RaidStatusEffectKind.Vulnerable,
                    vulnerabilityExpiresAt - raidTime));
            }
            if (damageOverTimeSource != null && raidTime < damageOverTimeExpiresAt)
            {
                results.Add(new RaidStatusEffectSnapshot(
                    RaidStatusEffectKind.Burning,
                    damageOverTimeExpiresAt - raidTime));
            }
        }

        public void CommitAbility(RaidAbilitySpec ability, float raidTime)
        {
            if (ability == null) return;
            currentMana = Mathf.Max(0f, currentMana - ability.manaCost);
            abilityReadyAt[ability.id ?? string.Empty] = raidTime + ability.cooldown;
            punchUntil = raidTime + 0.22f;
        }

        public void ForceTarget(RaidAgent2D target, float until, float damageMultiplier = 1f)
        {
            if (target == null || target.faction == faction) return;
            forcedTarget = target;
            forcedTargetUntil = until;
            forcedTargetDamageMultiplier = Mathf.Max(0f, damageMultiplier);
        }

        public RaidAgent2D ResolveForcedTarget(float raidTime)
        {
            if (forcedTarget == null || !forcedTarget.CanAct || raidTime >= forcedTargetUntil)
            {
                forcedTarget = null;
                forcedTargetUntil = 0f;
                forcedTargetDamageMultiplier = 1f;
            }
            return forcedTarget;
        }

        public void GrantDamageBuff(float multiplier, float until)
        {
            damageBuffMultiplier = Mathf.Max(damageBuffMultiplier, Mathf.Max(0f, multiplier));
            damageBuffExpiresAt = Mathf.Max(damageBuffExpiresAt, until);
        }

        public void EmpowerAgainst(RaidAgent2D target, float multiplier, float until)
        {
            if (target == null || target.faction == faction) return;
            empoweredTarget = target;
            empoweredTargetMultiplier = Mathf.Max(1f, multiplier);
            empoweredTargetExpiresAt = Mathf.Max(empoweredTargetExpiresAt, until);
        }

        public void ApplyVulnerability(float multiplier, float until)
        {
            vulnerabilityMultiplier = Mathf.Max(vulnerabilityMultiplier, Mathf.Max(1f, multiplier));
            vulnerabilityExpiresAt = Mathf.Max(vulnerabilityExpiresAt, until);
        }

        public bool IsVulnerable(float raidTime)
        {
            return raidTime < vulnerabilityExpiresAt && vulnerabilityMultiplier > 1f;
        }

        public void ApplyStun(float until)
        {
            stunnedUntil = Mathf.Max(stunnedUntil, until);
            StopMoving();
        }

        public void Ignite(
            RaidAgent2D source,
            float damagePerSecond,
            float duration,
            float raidTime)
        {
            if (!CanAct || damagePerSecond <= 0f || duration <= 0f) return;
            damageOverTimeSource = source;
            damageOverTimePerSecond = Mathf.Max(damageOverTimePerSecond, damagePerSecond);
            damageOverTimeExpiresAt = Mathf.Max(damageOverTimeExpiresAt, raidTime + duration);
            nextDamageOverTimeTick = Mathf.Min(
                nextDamageOverTimeTick <= raidTime ? raidTime + 1f : nextDamageOverTimeTick,
                raidTime + 1f);
        }

        public float ResolveOutgoingDamageMultiplier(RaidAgent2D target, float raidTime)
        {
            var result = raidTime < damageBuffExpiresAt ? damageBuffMultiplier : 1f;
            if (target != null && target == empoweredTarget && raidTime < empoweredTargetExpiresAt)
            {
                result *= empoweredTargetMultiplier;
            }
            if (target != null && target == forcedTarget && raidTime < forcedTargetUntil)
            {
                result *= forcedTargetDamageMultiplier;
            }
            return result;
        }

        public float ReceiveDamage(
            RaidAgent2D source,
            float incomingDamage,
            float raidTime,
            DungeonRaidDirector2D raid)
        {
            if (!CanReceiveDamage || incomingDamage <= 0f) return 0f;
            var sourceMultiplier = source != null
                ? source.ResolveOutgoingDamageMultiplier(this, raidTime)
                : 1f;
            var receivedMultiplier = raidTime < vulnerabilityExpiresAt
                ? vulnerabilityMultiplier
                : 1f;
            var remaining = incomingDamage * sourceMultiplier * receivedMultiplier;
            if (currentShield > 0f && raidTime >= shieldExpiresAt)
            {
                currentShield = 0f;
                shieldExpiresAt = 0f;
            }
            if (currentShield > 0f)
            {
                var absorbed = Mathf.Min(currentShield, remaining);
                currentShield -= absorbed;
                remaining -= absorbed;
                if (absorbed > 0f)
                {
                    raid?.Effects?.EmitBurst(Position, Color.white, 1.4f, 0.24f);
                }
            }
            var applied = Mathf.Min(currentHealth, remaining);
            currentHealth = Mathf.Max(0f, currentHealth - remaining);
            flashUntil = raidTime + 0.12f;
            if (source != null && remaining > 0f)
            {
                var impactDirection = Position - source.Position;
                if (impactDirection.sqrMagnitude > 0.001f)
                {
                    Nudge(impactDirection.normalized * Mathf.Clamp(remaining * 0.012f, 0.08f, 0.48f));
                }
            }
            if (remaining > 0f)
            {
                raid?.Effects?.EmitBurst(Position, new Color(0.82f, 0.03f, 0.025f), 0.9f, 0.18f);
                raid?.Effects?.EmitText(Position + Vector2.up * 0.7f, $"-{Mathf.RoundToInt(remaining)}", new Color(1f, 0.35f, 0.22f));
            }
            Damaged?.Invoke(this, source, applied);
            if (currentHealth <= 0f)
            {
                if (faction == RaidFaction.Hunters && usesDownedState)
                {
                    downedUntil = raidTime + downedSeconds;
                    SetLifeState(RaidLifeState.Downed);
                    raid?.Effects?.EmitBurst(Position, new Color(0.8f, 0.05f, 0.05f), 2f, 0.42f);
                }
                else
                {
                    SetLifeState(RaidLifeState.Dead);
                    raid?.Effects?.EmitBurst(Position, new Color(0.28f, 0.7f, 0.12f), 1.8f, 0.36f);
                }
            }
            return applied;
        }

        public float Heal(float amount, float raidTime, DungeonRaidDirector2D raid)
        {
            if (amount <= 0f || lifeState == RaidLifeState.Dead) return 0f;
            if (lifeState == RaidLifeState.Downed)
            {
                lifeState = RaidLifeState.Active;
                currentHealth = Mathf.Max(maximumHealth * 0.28f, Mathf.Min(amount, maximumHealth));
                downedUntil = 0f;
                LifeStateChanged?.Invoke(this, lifeState);
                raid?.Effects?.EmitText(Position + Vector2.up * 0.8f, "RESCUED", new Color(0.35f, 1f, 0.55f));
            }
            var before = currentHealth;
            currentHealth = Mathf.Min(maximumHealth, currentHealth + amount);
            var restored = currentHealth - before;
            if (restored > 0f)
            {
                raid?.Effects?.EmitBurst(Position, new Color(0.12f, 1f, 0.45f), 1.25f, 0.35f);
                raid?.Effects?.EmitText(Position + Vector2.up * 0.7f, $"+{Mathf.RoundToInt(restored)}", new Color(0.3f, 1f, 0.5f));
            }
            return restored;
        }

        public bool GrantTemporaryShield(
            RaidAgent2D provider,
            float amount,
            float raidTime,
            DungeonRaidDirector2D raid,
            float duration = 12f)
        {
            // Temporary HP is a Tanker ability, never an innate hunter stat.
            // Keeping the authority check here prevents another ability or future
            // AI path from silently handing shields to arbitrary characters.
            if (!CanAct || amount <= 0f || provider == null || !provider.CanAct ||
                provider.Faction != faction || provider.Faction != RaidFaction.Hunters ||
                provider.Role != RaidCombatRole.Tank)
            {
                return false;
            }
            currentShield = Mathf.Max(currentShield, amount);
            shieldExpiresAt = Mathf.Max(shieldExpiresAt, raidTime + Mathf.Max(0.1f, duration));
            var shieldColor = new Color(1f, 0.96f, 0.78f);
            raid?.Effects?.EmitBurst(Position, shieldColor, 1.5f, 0.4f);
            raid?.Effects?.EmitText(
                Position + Vector2.up * 0.9f,
                $"TEMP SHIELD +{Mathf.RoundToInt(currentShield)}",
                shieldColor);
            return true;
        }

        public void MarkAbilityCast(RaidAbilitySpec ability, RaidAgent2D target, DungeonRaidDirector2D raid)
        {
            if (ability == null) return;
            faceTarget(target != null ? target.Position : Position + Vector2.right);
            raid?.Effects?.EmitBurst(Position, ability.color, 1.1f, 0.28f);
            if (target != null && ability.effect is RaidAbilityEffect.Damage or
                RaidAbilityEffect.Execute)
            {
                raid?.Effects?.EmitProjectile(Position, target.Position, ability.color, 0.2f);
            }
        }

        private void StepMovement(float deltaTime)
        {
            if (!hasDestination) return;
            var current = Position;
            var finalOffset = desiredDestination - current;
            var finalDistance = finalOffset.magnitude;
            if (finalDistance <= destinationStopRadius)
            {
                hasDestination = false;
                InvalidateNavigationPath();
                return;
            }

            var steeringDestination = desiredDestination;
            var navigationAvailable = navigation != null && navigation.IsReady;
            var hasNavigationWaypoint = false;
            if (navigationAvailable)
            {
                EnsureNavigationPath(current);
                var waypointTolerance = Mathf.Max(0.22f, CollisionRadius * 0.55f);
                while (navigationPathIndex < navigationPath.Count &&
                       Vector2.Distance(current, navigationPath[navigationPathIndex]) <= waypointTolerance)
                {
                    navigationPathIndex++;
                }
                if (navigationPathIndex < navigationPath.Count)
                {
                    steeringDestination = navigationPath[navigationPathIndex];
                    hasNavigationWaypoint = true;
                }
            }

            var offset = steeringDestination - current;
            var distance = offset.magnitude;
            if (distance <= 0.0001f) return;
            var remainingDistance = hasNavigationWaypoint
                ? distance
                : Mathf.Max(0f, finalDistance - destinationStopRadius);
            var step = Mathf.Min(remainingDistance, moveSpeed * Mathf.Max(0f, deltaTime));
            if (step <= 0f) return;
            var direction = offset / Mathf.Max(0.0001f, distance);
            direction = ResolveMovementDirection(direction, step + CollisionRadius * 0.25f);
            if (direction.sqrMagnitude <= 0.001f) return;
            var next = current + direction * step;
            physicsBody.MovePosition(next);
            faceTarget(next + direction);
        }

        private void EnsureNavigationPath(Vector2 current)
        {
            if (navigation == null || !navigation.IsReady) return;
            var destinationMoved = Vector2.SqrMagnitude(
                desiredDestination - plannedNavigationDestination) >
                navigationDestinationTolerance * navigationDestinationTolerance;
            var pathExhausted = navigationPathIndex >= navigationPath.Count;
            if (!pathExhausted && (!destinationMoved || Time.time < nextNavigationRepathAt))
            {
                return;
            }

            navigationPath.Clear();
            navigationPathIndex = 0;
            navigation.TryFindPath(current, desiredDestination, navigationPath);
            plannedNavigationDestination = desiredDestination;
            nextNavigationRepathAt = Time.time + Mathf.Max(0.1f, navigationRepathInterval);
        }

        private void InvalidateNavigationPath()
        {
            navigationPath.Clear();
            navigationPathIndex = 0;
            plannedNavigationDestination = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            nextNavigationRepathAt = 0f;
        }

        private Vector2 ResolveMovementDirection(Vector2 desired, float castDistance)
        {
            if (!IsMovementBlocked(desired, castDistance)) return desired;
            var perpendicular = new Vector2(-desired.y, desired.x) * avoidanceHandedness;
            var firstArc = (desired * 0.45f + perpendicular).normalized;
            var secondArc = (desired * 0.45f - perpendicular).normalized;
            if (!IsMovementBlocked(firstArc, castDistance)) return firstArc;
            if (!IsMovementBlocked(secondArc, castDistance)) return secondArc;
            if (!IsMovementBlocked(perpendicular, castDistance)) return perpendicular;
            if (!IsMovementBlocked(-perpendicular, castDistance)) return -perpendicular;
            return Vector2.zero;
        }

        private bool IsMovementBlocked(Vector2 direction, float castDistance)
        {
            if (physicsBody == null || direction.sqrMagnitude <= 0.001f) return false;
            var filter = new ContactFilter2D
            {
                useTriggers = false,
                useLayerMask = false
            };
            var hitCount = physicsBody.Cast(direction, filter, movementCastHits,
                Mathf.Max(0.02f, castDistance));
            for (var index = 0; index < hitCount; index++)
            {
                var hit = movementCastHits[index];
                var collider = hit.collider;
                if (collider == null) continue;
                if (collider.GetComponentInParent<RaidAgent2D>() != null) continue;

                // Rigidbody2D.Cast can report the wall we are already touching
                // at distance zero even while the proposed movement is tangent
                // to, or away from, that wall. Treating that contact as blocked
                // made every avoidance candidate fail once an agent reached an
                // obstacle face. Only reject a zero-distance contact when this
                // direction would continue pressing into the surface.
                const float contactTolerance = 0.001f;
                const float inwardTolerance = -0.01f;
                if (hit.distance <= contactTolerance &&
                    Vector2.Dot(direction, hit.normal) >= inwardTolerance)
                {
                    continue;
                }
                return true;
            }
            return false;
        }

        private static float StableAvoidanceHandedness(string value)
        {
            var hash = 17;
            if (!string.IsNullOrEmpty(value))
            {
                for (var index = 0; index < value.Length; index++)
                {
                    hash = unchecked(hash * 31 + value[index]);
                }
            }
            return (hash & 1) == 0 ? 1f : -1f;
        }

        private void StepStatuses(float raidTime, DungeonRaidDirector2D raid)
        {
            if (currentShield > 0f && raidTime >= shieldExpiresAt)
            {
                currentShield = 0f;
                shieldExpiresAt = 0f;
            }
            if (raidTime >= damageBuffExpiresAt)
            {
                damageBuffMultiplier = 1f;
            }
            if (raidTime >= empoweredTargetExpiresAt)
            {
                empoweredTarget = null;
                empoweredTargetMultiplier = 1f;
            }
            if (raidTime >= vulnerabilityExpiresAt)
            {
                vulnerabilityMultiplier = 1f;
            }
            if (lifeState == RaidLifeState.Active &&
                damageOverTimeSource != null &&
                raidTime < damageOverTimeExpiresAt &&
                raidTime >= nextDamageOverTimeTick)
            {
                nextDamageOverTimeTick += 1f;
                ReceiveDamage(damageOverTimeSource, damageOverTimePerSecond, raidTime, raid);
            }
            if (raidTime >= damageOverTimeExpiresAt)
            {
                damageOverTimeSource = null;
                damageOverTimePerSecond = 0f;
            }
        }

        private void EnsurePhysics()
        {
            if (physicsBody == null) physicsBody = GetComponent<Rigidbody2D>();
            if (hurtbox == null) hurtbox = GetComponent<CircleCollider2D>();
            if (physicsBody != null)
            {
                physicsBody.bodyType = useLightweightPhysics
                    ? RigidbodyType2D.Kinematic
                    : RigidbodyType2D.Dynamic;
                physicsBody.gravityScale = 0f;
                physicsBody.linearDamping = 8f;
                physicsBody.freezeRotation = true;
                physicsBody.collisionDetectionMode = useLightweightPhysics
                    ? CollisionDetectionMode2D.Discrete
                    : CollisionDetectionMode2D.Continuous;
                physicsBody.interpolation = useLightweightPhysics
                    ? RigidbodyInterpolation2D.None
                    : RigidbodyInterpolation2D.Interpolate;
            }
            if (hurtbox != null)
            {
                hurtbox.isTrigger = useTriggerHurtbox;
                hurtbox.radius = Mathf.Max(0.1f, collisionRadius);
            }
        }

        private void faceTarget(Vector2 worldPoint)
        {
            if (bodyRenderer == null) return;
            var delta = worldPoint - Position;
            if (Mathf.Abs(delta.x) > 0.01f)
            {
                bodyRenderer.flipX = delta.x < 0f;
            }
        }

        private void SetLifeState(RaidLifeState next)
        {
            if (lifeState == next) return;
            lifeState = next;
            hasDestination = false;
            InvalidateNavigationPath();
            LifeStateChanged?.Invoke(this, next);
        }

        private void EnsurePresentation()
        {
            if (bodyRenderer == null || bodyRenderer.sprite == null) return;
            if (!compactPresentation)
            {
                shadowRenderer = CreateSpriteChild("Raid Shadow", bodyRenderer.sprite,
                    new Color(0f, 0f, 0f, 0.32f), new Vector3(1.18f, 0.52f, 1f),
                    new Vector3(0f, -0.22f, 0f), bodyRenderer.sortingOrder - 3);
            }
            healthBackground = CreateSpriteChild("Health Background", bodyRenderer.sprite,
                new Color(0.08f, 0.04f, 0.04f, 0.9f), new Vector3(1.25f, 0.13f, 1f),
                new Vector3(0f, 0.82f, 0f), bodyRenderer.sortingOrder + 3);
            healthFill = CreateSpriteChild("Health Fill", bodyRenderer.sprite,
                new Color(0.85f, 0.05f, 0.04f, 1f), new Vector3(1.2f, 0.09f, 1f),
                new Vector3(0f, 0.82f, 0f), bodyRenderer.sortingOrder + 4);
            if (!compactPresentation)
            {
                manaFill = CreateSpriteChild("Mana Fill", bodyRenderer.sprite,
                    new Color(0.08f, 0.42f, 1f, 1f), new Vector3(1.2f, 0.065f, 1f),
                    new Vector3(0f, 0.69f, 0f), bodyRenderer.sortingOrder + 4);
                shieldRenderer = CreateSpriteChild("Shield Ring", bodyRenderer.sprite,
                    new Color(1f, 0.96f, 0.78f, 0f), new Vector3(1.55f, 1.55f, 1f),
                    Vector3.zero, bodyRenderer.sortingOrder + 2);
                // Enabling the renderer, rather than relying on transparent alpha,
                // makes the visual contract unambiguous on every sprite material:
                // no active temporary shield means no shield draw call.
                shieldRenderer.enabled = false;
            }
        }

        private SpriteRenderer CreateSpriteChild(
            string childName,
            Sprite sprite,
            Color color,
            Vector3 scale,
            Vector3 localPosition,
            int sortingOrder)
        {
            var child = new GameObject(childName);
            child.transform.SetParent(transform, false);
            child.transform.localPosition = localPosition;
            child.transform.localScale = scale;
            var renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingLayerID = bodyRenderer.sortingLayerID;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private void UpdatePresentation(float raidTime)
        {
            if (bodyRenderer == null) return;
            var bodyColor = identityColor;
            if (lifeState == RaidLifeState.Downed)
            {
                bodyColor = Color.Lerp(identityColor, new Color(0.35f, 0.2f, 0.2f), 0.7f);
            }
            else if (lifeState == RaidLifeState.Dead)
            {
                bodyColor = new Color(0.16f, 0.16f, 0.16f, 0.48f);
            }
            if (raidTime < flashUntil)
            {
                bodyColor = Color.white;
            }
            bodyRenderer.color = bodyColor;
            transform.localScale = baseLocalScale;
            if (healthFill != null)
            {
                var ratio = Mathf.Clamp01(HealthRatio);
                healthFill.transform.localScale = new Vector3(1.2f * ratio, 0.09f, 1f);
                healthFill.transform.localPosition = new Vector3(-0.6f * (1f - ratio), 0.82f, 0f);
            }
            if (manaFill != null)
            {
                var ratio = maximumMana <= 0f ? 0f : Mathf.Clamp01(currentMana / maximumMana);
                manaFill.gameObject.SetActive(faction == RaidFaction.Hunters && maximumMana > 0f);
                manaFill.transform.localScale = new Vector3(1.2f * ratio, 0.065f, 1f);
                manaFill.transform.localPosition = new Vector3(-0.6f * (1f - ratio), 0.69f, 0f);
            }
            if (shieldRenderer != null)
            {
                var shieldIsActive = HasTemporaryShield &&
                                     lifeState == RaidLifeState.Active;
                var color = shieldRenderer.color;
                color.a = shieldIsActive ? 0.62f : 0f;
                shieldRenderer.color = color;
                shieldRenderer.enabled = shieldIsActive;
            }
            if (healthBackground != null)
            {
                healthBackground.gameObject.SetActive(lifeState != RaidLifeState.Dead);
                healthFill.gameObject.SetActive(lifeState != RaidLifeState.Dead);
            }
            if (shadowRenderer != null)
            {
                shadowRenderer.gameObject.SetActive(lifeState != RaidLifeState.Dead);
            }
            if (hurtbox != null)
            {
                hurtbox.enabled = lifeState != RaidLifeState.Dead;
            }
        }

        public void ConfigureRuntime(
            string id,
            string label,
            RaidFaction assignedFaction,
            RaidCombatRole assignedRole,
            float health,
            float mana,
            float manaRegeneration,
            float speed,
            float damage,
            float attackRange,
            float preferredRange,
            float attackCooldown,
            bool ranged,
            Color color,
            List<RaidAbilitySpec> assignedAbilities,
            float assignedCollisionRadius = 0.45f,
            bool useCompactPresentation = false,
            bool useScalePhysics = false,
            bool useNonBlockingHurtbox = false,
            bool allowDownedState = true)
        {
            agentId = id;
            displayName = label;
            faction = assignedFaction;
            role = assignedRole;
            maximumHealth = Mathf.Max(1f, health);
            maximumMana = Mathf.Max(0f, mana);
            manaRegenerationPerSecond = Mathf.Max(0f, manaRegeneration);
            moveSpeed = Mathf.Max(0.1f, speed);
            basicAttackDamage = Mathf.Max(0f, damage);
            basicAttackRange = Mathf.Max(0.1f, attackRange);
            preferredCombatRange = Mathf.Max(0.1f, preferredRange);
            basicAttackCooldown = Mathf.Max(0.05f, attackCooldown);
            rangedBasicAttack = ranged;
            identityColor = color;
            abilities = assignedAbilities ?? new List<RaidAbilitySpec>();
            collisionRadius = Mathf.Max(0.1f, assignedCollisionRadius);
            compactPresentation = useCompactPresentation;
            useLightweightPhysics = useScalePhysics;
            useTriggerHurtbox = useNonBlockingHurtbox;
            usesDownedState = allowDownedState;
            avoidanceHandedness = StableAvoidanceHandedness(agentId);
            bodyRenderer = GetComponent<SpriteRenderer>();
            physicsBody = GetComponent<Rigidbody2D>();
            hurtbox = GetComponent<CircleCollider2D>();
            EnsurePhysics();
            if (bodyRenderer != null)
            {
                bodyRenderer.color = color;
                bodyRenderer.sortingOrder = assignedFaction == RaidFaction.Hunters ? 30 : 25;
            }
            spawnPosition = transform.position;
            baseLocalScale = transform.localScale;
        }

#if UNITY_EDITOR
        public void ConfigureEditor(
            string id,
            string label,
            RaidFaction assignedFaction,
            RaidCombatRole assignedRole,
            float health,
            float mana,
            float manaRegeneration,
            float speed,
            float damage,
            float attackRange,
            float preferredRange,
            float attackCooldown,
            bool ranged,
            Color color,
            List<RaidAbilitySpec> assignedAbilities,
            float assignedCollisionRadius = 0.45f,
            bool useCompactPresentation = false,
            bool useScalePhysics = false,
            bool useNonBlockingHurtbox = false,
            bool allowDownedState = true)
        {
            ConfigureRuntime(
                id,
                label,
                assignedFaction,
                assignedRole,
                health,
                mana,
                manaRegeneration,
                speed,
                damage,
                attackRange,
                preferredRange,
                attackCooldown,
                ranged,
                color,
                assignedAbilities,
                assignedCollisionRadius,
                useCompactPresentation,
                useScalePhysics,
                useNonBlockingHurtbox,
                allowDownedState);
        }
#endif
    }
}
