using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Turtle.DungeonRaid;

namespace Turtle.BattleArena3D
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent), typeof(CapsuleCollider), typeof(BattleArena3DUnitView))]
    public sealed class BattleArena3DUnit : MonoBehaviour
    {
        private static readonly Color HighContrastHunterColor = new(0.08f, 0.58f, 1f, 1f);
        private static readonly Color HighContrastMonsterColor = new(1f, 0.32f, 0.08f, 1f);

        [Header("Identity")]
        [SerializeField] private string stableId = "arena-unit";
        [SerializeField] private string displayName = "Arena Unit";
        [SerializeField] private string buildLabel = "Fighter";
        [SerializeField, TextArea] private string traitLabel = "Steady";
        [SerializeField] private BattleArenaFaction3D faction;
        [SerializeField] private RaidCombatRole role = RaidCombatRole.Fighter;
        [SerializeField] private BattleArenaMonsterArchetype3D monsterArchetype;
        [SerializeField, Range(-1, 2)] private int squadIndex = -1;
        [SerializeField] private bool sergeant;
        [SerializeField] private Color themeColor = Color.white;

        [Header("Combat")]
        [SerializeField, Min(1f)] private float maximumHealth = 100f;
        [SerializeField, Min(0f)] private float maximumMana = 50f;
        [SerializeField, Min(0f)] private float manaRegeneration = 5f;
        [SerializeField, Min(0.1f)] private float movementSpeed = 4f;
        [SerializeField, Min(0f)] private float basicDamage = 10f;
        [SerializeField, Min(0.2f)] private float attackRange = 1.5f;
        [SerializeField, Min(0.2f)] private float preferredRange = 1.1f;
        [SerializeField, Min(0.1f)] private float attackCooldown = 0.9f;
        [SerializeField] private bool ranged;
        [SerializeField] private List<RaidAbilitySpec> abilities = new();

        [Header("Personality")]
        [SerializeField, Range(0f, 1f)] private float aggression = 0.6f;
        [SerializeField, Range(0f, 1f)] private float cohesion = 0.7f;
        [SerializeField, Range(0f, 1f)] private float support = 0.5f;

        [Header("References")]
        [SerializeField] private NavMeshAgent navigationAgent;
        [SerializeField] private CapsuleCollider hurtbox;
        [SerializeField] private BattleArena3DUnitView view;

        private float health;
        private float mana;
        private float shield;
        private float shieldCapacity;
        private float shieldExpiresAt;
        private float stunnedUntil;
        private float vulnerableUntil;
        private float empoweredUntil;
        private float burningUntil;
        private float burningDamage;
        private float nextBurnTick;
        private float downedExpiresAt;
        private float nextAttackAt;
        private float pendingAttackAt = float.PositiveInfinity;
        private float[] abilityReadyAt = System.Array.Empty<float>();
        private BattleArena3DUnit pendingAttackTarget;
        private BattleArena3DUnit forcedTarget;
        private float forcedTargetExpiresAt;
        private Vector3 spawnPosition;
        private Quaternion spawnRotation;
        private Vector3 lastDestination;
        private bool hasDestination;
        private bool attackHeavy;
        private bool actionLocked;
        private int abilityCursor;
        private int kills;
        private int abilityCasts;
        private float damageDealt;
        private string objective = "Awaiting orders";
        private BattleArenaLifeState3D lifeState = BattleArenaLifeState3D.Active;
        private BattleArenaPresentationOptions3D presentationOptions = BattleArenaPresentationOptions3D.Default;

        public string StableId => stableId;
        public string DisplayName => displayName;
        public string BuildLabel => buildLabel;
        public string TraitLabel => traitLabel;
        public BattleArenaFaction3D Faction => faction;
        public RaidCombatRole Role => role;
        public BattleArenaMonsterArchetype3D MonsterArchetype => monsterArchetype;
        public int SquadIndex => squadIndex;
        public bool IsSergeant => sergeant;
        public bool IsRanged => ranged;
        public Color ThemeColor => themeColor;
        public float MaximumHealth => maximumHealth;
        public float Health => health;
        public float HealthRatio => maximumHealth <= 0f ? 0f : health / maximumHealth;
        public float MaximumMana => maximumMana;
        public float Mana => mana;
        public float Shield => shield;
        public float MovementSpeed => movementSpeed;
        public float BasicDamage => basicDamage;
        public float AttackRange => attackRange;
        public float PreferredRange => preferredRange;
        public float AttackCooldown => attackCooldown;
        public float Aggression => aggression;
        public float Cohesion => cohesion;
        public float Support => support;
        public IReadOnlyList<RaidAbilitySpec> Abilities => abilities;
        public BattleArenaLifeState3D LifeState => lifeState;
        public bool CanAct(float time) => lifeState == BattleArenaLifeState3D.Active && time >= stunnedUntil;
        public bool CanReceiveDamage => isActiveAndEnabled && lifeState == BattleArenaLifeState3D.Active;
        public bool HasShield(float time) => shield > 0f && time < shieldExpiresAt;
        public bool IsVulnerable(float time) => time < vulnerableUntil;
        public bool IsEmpowered(float time) => time < empoweredUntil;
        public bool IsBurning(float time) => time < burningUntil;
        public bool IsStunned(float time) => time < stunnedUntil;
        public float NextAttackAt => nextAttackAt;
        public string Objective => objective;
        public int Kills => kills;
        public int AbilityCasts => abilityCasts;
        public float DamageDealt => damageDealt;
        public NavMeshAgent NavigationAgent => navigationAgent;
        public CapsuleCollider Hurtbox => hurtbox;
        public BattleArena3DUnitView View => view;
        public BattleArena3DUnit ForcedTarget => forcedTarget;
        public int AbilityCursor => abilityCursor;

        private void Awake()
        {
            navigationAgent ??= GetComponent<NavMeshAgent>();
            hurtbox ??= GetComponent<CapsuleCollider>();
            view ??= GetComponent<BattleArena3DUnitView>();
            EnsureCooldownStorage();
        }

        public void ResetForBattle(Vector3 position, Quaternion rotation, float time)
        {
            spawnPosition = position;
            spawnRotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
            lifeState = BattleArenaLifeState3D.Active;
            health = maximumHealth;
            mana = maximumMana;
            shield = shieldCapacity = 0f;
            shieldExpiresAt = stunnedUntil = vulnerableUntil = empoweredUntil = burningUntil = 0f;
            burningDamage = 0f;
            nextBurnTick = 0f;
            downedExpiresAt = 0f;
            nextAttackAt = time + Random.Range(0.05f, attackCooldown);
            pendingAttackAt = float.PositiveInfinity;
            pendingAttackTarget = null;
            forcedTarget = null;
            forcedTargetExpiresAt = 0f;
            actionLocked = false;
            hasDestination = false;
            abilityCursor = Mathf.Abs(stableId.GetHashCode()) % Mathf.Max(1, abilities.Count);
            kills = 0;
            abilityCasts = 0;
            damageDealt = 0f;
            objective = "Forming up";
            EnsureCooldownStorage();
            for (var index = 0; index < abilityReadyAt.Length; index++)
            {
                abilityReadyAt[index] = time + Random.Range(0.15f, 1.2f);
            }
            gameObject.SetActive(true);
            PlaceOnNavMesh(position);
            if (hurtbox != null) hurtbox.enabled = true;
            if (navigationAgent != null && navigationAgent.isOnNavMesh)
            {
                navigationAgent.isStopped = false;
                navigationAgent.speed = movementSpeed;
            }
            if (view != null)
            {
                view.ResetView();
                ApplyPresentationOptions(presentationOptions);
                view.UpdateVitals(1f, 0f, faction == BattleArenaFaction3D.Hunters);
            }
        }

        public void PrepareMonster(
            BattleArenaMonsterArchetype3D archetype,
            int serial,
            int round,
            float healthMultiplier,
            float damageMultiplier,
            float speedMultiplier,
            Color color,
            IReadOnlyList<RaidAbilitySpec> monsterAbilities,
            Vector3 position,
            Quaternion rotation,
            float time)
        {
            monsterArchetype = archetype;
            faction = BattleArenaFaction3D.Monsters;
            squadIndex = -1;
            sergeant = false;
            stableId = $"monster-{round}-{serial}";
            displayName = archetype.ToString();
            buildLabel = $"Round {round} {archetype}";
            traitLabel = archetype switch
            {
                BattleArenaMonsterArchetype3D.Brute => "Slow, durable, crushing melee",
                BattleArenaMonsterArchetype3D.Spitter => "Kiting ranged acid attacker",
                BattleArenaMonsterArchetype3D.Hexer => "Backline control caster",
                BattleArenaMonsterArchetype3D.Charger => "Fast line-breaking striker",
                BattleArenaMonsterArchetype3D.Elite => "Empowered horde champion",
                _ => "Relentless close-range attacker"
            };
            role = archetype switch
            {
                BattleArenaMonsterArchetype3D.Brute => RaidCombatRole.Melee,
                BattleArenaMonsterArchetype3D.Spitter => RaidCombatRole.Archer,
                BattleArenaMonsterArchetype3D.Hexer => RaidCombatRole.Mage,
                BattleArenaMonsterArchetype3D.Elite => RaidCombatRole.Elite,
                _ => RaidCombatRole.Melee
            };
            var baseHealth = archetype switch
            {
                BattleArenaMonsterArchetype3D.Brute => 76f,
                BattleArenaMonsterArchetype3D.Spitter => 32f,
                BattleArenaMonsterArchetype3D.Hexer => 40f,
                BattleArenaMonsterArchetype3D.Charger => 44f,
                BattleArenaMonsterArchetype3D.Elite => 145f,
                _ => 36f
            };
            var baseDamage = archetype switch
            {
                BattleArenaMonsterArchetype3D.Brute => 8.4f,
                BattleArenaMonsterArchetype3D.Spitter => 4.6f,
                BattleArenaMonsterArchetype3D.Hexer => 5.2f,
                BattleArenaMonsterArchetype3D.Charger => 6.8f,
                BattleArenaMonsterArchetype3D.Elite => 11f,
                _ => 4.8f
            };
            maximumHealth = baseHealth * healthMultiplier;
            maximumMana = archetype is BattleArenaMonsterArchetype3D.Hexer or
                BattleArenaMonsterArchetype3D.Elite ? 80f : 20f;
            manaRegeneration = 5f;
            movementSpeed = (archetype switch
            {
                BattleArenaMonsterArchetype3D.Brute => 2.6f,
                BattleArenaMonsterArchetype3D.Spitter => 3.3f,
                BattleArenaMonsterArchetype3D.Hexer => 3f,
                BattleArenaMonsterArchetype3D.Charger => 5.1f,
                BattleArenaMonsterArchetype3D.Elite => 3.4f,
                _ => 3.8f
            }) * speedMultiplier;
            basicDamage = baseDamage * damageMultiplier;
            ranged = archetype is BattleArenaMonsterArchetype3D.Spitter or
                BattleArenaMonsterArchetype3D.Hexer;
            attackRange = ranged ? 8.5f : archetype == BattleArenaMonsterArchetype3D.Brute ? 2f : 1.45f;
            preferredRange = ranged ? 6.7f : attackRange * 0.72f;
            attackCooldown = archetype switch
            {
                BattleArenaMonsterArchetype3D.Brute => 1.55f,
                BattleArenaMonsterArchetype3D.Spitter => 1.3f,
                BattleArenaMonsterArchetype3D.Hexer => 1.5f,
                BattleArenaMonsterArchetype3D.Charger => 0.86f,
                BattleArenaMonsterArchetype3D.Elite => 1.15f,
                _ => 1.05f
            };
            aggression = archetype == BattleArenaMonsterArchetype3D.Spitter ? 0.62f : 0.9f;
            cohesion = 0.35f;
            support = archetype == BattleArenaMonsterArchetype3D.Hexer ? 0.55f : 0.1f;
            themeColor = color;
            abilities.Clear();
            if (monsterAbilities != null)
            {
                for (var index = 0; index < monsterAbilities.Count; index++)
                {
                    abilities.Add(monsterAbilities[index]);
                }
            }
            EnsureCooldownStorage();
            ConfigureNavigationForArchetype();
            view?.ApplyMonsterArchetype(archetype);
            ResetForBattle(position, rotation, time);
        }

        public void Tick(float deltaTime, float time, bool showWorldBar)
        {
            if (lifeState == BattleArenaLifeState3D.Dead)
            {
                view?.UpdateVitals(0f, 0f, false);
                return;
            }
            if (lifeState == BattleArenaLifeState3D.Downed)
            {
                StopMoving();
                view?.TickPresentation(deltaTime, 0f, false);
                view?.UpdateVitals(HealthRatio,
                    shieldCapacity <= 0f ? 0f : shield / shieldCapacity,
                    showWorldBar);
                return;
            }
            mana = Mathf.Min(maximumMana, mana + manaRegeneration * deltaTime);
            if (shield > 0f && time >= shieldExpiresAt)
            {
                shield = shieldCapacity = 0f;
            }
            if (forcedTarget != null && (time >= forcedTargetExpiresAt || !forcedTarget.CanReceiveDamage))
            {
                forcedTarget = null;
            }
            var velocity = navigationAgent != null && navigationAgent.enabled && navigationAgent.isOnNavMesh
                ? navigationAgent.velocity.magnitude
                : 0f;
            actionLocked = time < pendingAttackAt && pendingAttackTarget != null;
            view?.TickPresentation(deltaTime, velocity / Mathf.Max(0.1f, movementSpeed), actionLocked);
            view?.UpdateVitals(HealthRatio,
                shieldCapacity <= 0f ? 0f : shield / shieldCapacity,
                showWorldBar);
        }

        public bool TryConsumeBurnTick(float time, out float damage)
        {
            damage = 0f;
            if (lifeState != BattleArenaLifeState3D.Active || time >= burningUntil || time < nextBurnTick)
            {
                return false;
            }
            nextBurnTick = time + 1f;
            damage = burningDamage;
            return damage > 0f;
        }

        public bool ExpireDownedState(float time)
        {
            if (lifeState != BattleArenaLifeState3D.Downed || time < downedExpiresAt) return false;
            lifeState = BattleArenaLifeState3D.Dead;
            health = 0f;
            if (hurtbox != null) hurtbox.enabled = false;
            if (navigationAgent != null && navigationAgent.isOnNavMesh) navigationAgent.isStopped = true;
            view?.PlayDeath();
            return true;
        }

        public BattleArenaDamageResult3D ReceiveDamage(float amount, float time, bool lethalAgainstHunters)
        {
            if (!CanReceiveDamage || amount <= 0f)
            {
                return new BattleArenaDamageResult3D(0f, 0f, false, false, false);
            }

            if (shield > 0f && time >= shieldExpiresAt)
            {
                shield = shieldCapacity = 0f;
            }

            var modified = amount * (time < vulnerableUntil ? 1.28f : 1f);
            var remaining = modified;
            var absorbedShield = 0f;
            var shieldBeforeHit = shield;
            if (shield > 0f)
            {
                absorbedShield = Mathf.Min(shield, remaining);
                shield -= absorbedShield;
                remaining -= absorbedShield;
            }
            var shieldBroken = shieldBeforeHit > 0f && shield <= 0.001f && absorbedShield > 0f;
            var applied = Mathf.Min(health, remaining);
            health = Mathf.Max(0f, health - remaining);
            view?.PlayHit();
            if (health > 0f)
            {
                return new BattleArenaDamageResult3D(
                    applied,
                    absorbedShield,
                    shieldBroken,
                    false,
                    false);
            }

            StopMoving();
            if (faction == BattleArenaFaction3D.Hunters && !lethalAgainstHunters)
            {
                lifeState = BattleArenaLifeState3D.Downed;
                downedExpiresAt = time + Mathf.Lerp(14f, 6f, Mathf.Clamp01(modified / maximumHealth));
                objective = "Downed - awaiting rescue";
                view?.PlayDeath();
                return new BattleArenaDamageResult3D(
                    applied,
                    absorbedShield,
                    shieldBroken,
                    true,
                    false);
            }

            lifeState = BattleArenaLifeState3D.Dead;
            objective = "Defeated";
            if (hurtbox != null) hurtbox.enabled = false;
            view?.PlayDeath();
            return new BattleArenaDamageResult3D(
                applied,
                absorbedShield,
                shieldBroken,
                false,
                true);
        }

        public void ApplyPresentationOptions(BattleArenaPresentationOptions3D options)
        {
            presentationOptions = options.Sanitized();
            if (view == null) return;

            view.SetReducedMotion(presentationOptions.ReducedMotion);
            var palette = presentationOptions.HighContrastFactions
                ? faction == BattleArenaFaction3D.Hunters
                    ? HighContrastHunterColor
                    : HighContrastMonsterColor
                : themeColor;
            view.ApplyPalette(palette);
        }

        public string StatusSummary(float time)
        {
            if (lifeState == BattleArenaLifeState3D.Dead) return "DEFEATED";
            if (lifeState == BattleArenaLifeState3D.Downed)
            {
                return $"DOWNED {Mathf.Max(0f, downedExpiresAt - time):0.0}s";
            }

            var summary = string.Empty;
            AppendStatus(ref summary, HasShield(time) ? $"SHIELD {shield:0}" : null);
            AppendStatus(ref summary, IsStunned(time) ? $"STUN {stunnedUntil - time:0.0}s" : null);
            AppendStatus(ref summary, IsBurning(time) ? $"BURN {burningUntil - time:0.0}s" : null);
            AppendStatus(ref summary, IsVulnerable(time) ? $"VULNERABLE {vulnerableUntil - time:0.0}s" : null);
            AppendStatus(ref summary, IsEmpowered(time) ? $"EMPOWERED {empoweredUntil - time:0.0}s" : null);
            return summary.Length == 0 ? "STABLE" : summary;
        }

        public float ReceiveHealing(float amount, float time, bool canRevive)
        {
            if (lifeState == BattleArenaLifeState3D.Dead || amount <= 0f) return 0f;
            if (lifeState == BattleArenaLifeState3D.Downed)
            {
                if (!canRevive) return 0f;
                lifeState = BattleArenaLifeState3D.Active;
                health = Mathf.Max(maximumHealth * 0.25f, Mathf.Min(maximumHealth, amount));
                stunnedUntil = time + 0.5f;
                objective = "Recovered - regrouping";
                if (hurtbox != null) hurtbox.enabled = true;
                if (navigationAgent != null && navigationAgent.isOnNavMesh) navigationAgent.isStopped = false;
                view?.PlayRevive();
                return health;
            }
            var previous = health;
            health = Mathf.Min(maximumHealth, health + amount);
            return health - previous;
        }

        public void GrantShield(float amount, float duration, float time)
        {
            if (lifeState != BattleArenaLifeState3D.Active || amount <= 0f) return;
            shieldCapacity = Mathf.Max(shieldCapacity, amount);
            shield = Mathf.Min(shieldCapacity, shield + amount);
            shieldExpiresAt = Mathf.Max(shieldExpiresAt, time + duration);
        }

        public void ApplyStun(float duration, float time)
        {
            if (lifeState != BattleArenaLifeState3D.Active) return;
            stunnedUntil = Mathf.Max(stunnedUntil, time + duration);
            StopMoving();
        }

        public void ApplyVulnerability(float duration, float time)
        {
            vulnerableUntil = Mathf.Max(vulnerableUntil, time + duration);
        }

        public void ApplyEmpower(float duration, float time)
        {
            empoweredUntil = Mathf.Max(empoweredUntil, time + duration);
        }

        public void ApplyBurn(float damagePerTick, float duration, float time)
        {
            burningDamage = Mathf.Max(burningDamage, damagePerTick);
            burningUntil = Mathf.Max(burningUntil, time + duration);
            nextBurnTick = Mathf.Min(nextBurnTick <= 0f ? time + 1f : nextBurnTick, time + 1f);
        }

        public void ForceTarget(BattleArena3DUnit target, float duration, float time)
        {
            if (target == null) return;
            forcedTarget = target;
            forcedTargetExpiresAt = time + duration;
        }

        public bool IsAbilityReady(int index, float time)
        {
            return CanAct(time) && index >= 0 && index < abilities.Count && index < abilityReadyAt.Length &&
                   time >= abilityReadyAt[index] && mana >= abilities[index].manaCost;
        }

        public float AbilityCooldownRemaining(int index, float time)
        {
            return index < 0 || index >= abilityReadyAt.Length ? 0f : Mathf.Max(0f, abilityReadyAt[index] - time);
        }

        public bool CommitAbility(int index, float time)
        {
            if (!IsAbilityReady(index, time)) return false;
            var ability = abilities[index];
            mana = Mathf.Max(0f, mana - ability.manaCost);
            abilityReadyAt[index] = time + ability.cooldown;
            abilityCursor = (index + 1) % Mathf.Max(1, abilities.Count);
            abilityCasts++;
            view?.PlayCast();
            return true;
        }

        public bool QueueBasicAttack(BattleArena3DUnit target, float time, bool heavy)
        {
            if (target == null || !target.CanReceiveDamage || !CanAct(time) || time < nextAttackAt ||
                pendingAttackTarget != null)
            {
                return false;
            }
            pendingAttackTarget = target;
            attackHeavy = heavy;
            pendingAttackAt = time + (heavy ? 0.42f : ranged ? 0.24f : 0.2f);
            nextAttackAt = time + attackCooldown * (heavy ? 1.3f : 1f);
            view?.PlayAttack(heavy);
            return true;
        }

        public bool TryConsumePendingAttack(float time, out BattleArena3DUnit target, out bool heavy)
        {
            target = null;
            heavy = false;
            if (pendingAttackTarget == null || time < pendingAttackAt) return false;
            target = pendingAttackTarget;
            heavy = attackHeavy;
            pendingAttackTarget = null;
            pendingAttackAt = float.PositiveInfinity;
            actionLocked = false;
            return target != null;
        }

        public float ResolveOutgoingDamage(float amount, float time)
        {
            return amount * (time < empoweredUntil ? 1.24f : 1f);
        }

        public void RecordDamage(float amount)
        {
            damageDealt += Mathf.Max(0f, amount);
        }

        public void RecordKill()
        {
            kills++;
        }

        public void SetObjective(string value)
        {
            objective = string.IsNullOrWhiteSpace(value) ? "Acting independently" : value;
        }

        public void SetDestination(Vector3 destination)
        {
            if (lifeState != BattleArenaLifeState3D.Active || navigationAgent == null ||
                !navigationAgent.enabled || !navigationAgent.isOnNavMesh)
            {
                return;
            }
            if (hasDestination && (lastDestination - destination).sqrMagnitude < 0.45f) return;
            hasDestination = true;
            lastDestination = destination;
            navigationAgent.isStopped = false;
            navigationAgent.speed = movementSpeed * (IsStunned(Time.time) ? 0f : 1f);
            navigationAgent.SetDestination(destination);
        }

        public void StopMoving()
        {
            hasDestination = false;
            if (navigationAgent != null && navigationAgent.enabled && navigationAgent.isOnNavMesh)
            {
                navigationAgent.isStopped = true;
                navigationAgent.ResetPath();
            }
        }

        public bool Warp(Vector3 position)
        {
            if (navigationAgent == null || !navigationAgent.enabled) return false;
            if (!NavMesh.SamplePosition(position, out var hit, 3f, NavMesh.AllAreas)) return false;
            return navigationAgent.isOnNavMesh ? navigationAgent.Warp(hit.position) : PlaceOnNavMesh(hit.position);
        }

        public void ApplyKnockback(Vector3 direction, float distance)
        {
            if (lifeState != BattleArenaLifeState3D.Active || distance <= 0f) return;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f) direction = -transform.forward;
            Warp(transform.position + direction.normalized * distance);
        }

        public void Face(Vector3 worldPosition)
        {
            var direction = worldPosition - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(direction), 0.55f);
            }
        }

        public BattleArenaUnitSnapshot3D Snapshot()
        {
            return new BattleArenaUnitSnapshot3D
            {
                DisplayName = displayName,
                Build = buildLabel,
                Objective = objective,
                LifeState = lifeState,
                Health = health,
                MaximumHealth = maximumHealth,
                Mana = mana,
                MaximumMana = maximumMana,
                Shield = shield,
                Kills = kills,
                AbilityCasts = abilityCasts,
                DamageDealt = damageDealt
            };
        }

        public void ReturnToPool()
        {
            StopMoving();
            pendingAttackTarget = null;
            forcedTarget = null;
            if (hurtbox != null) hurtbox.enabled = false;
            gameObject.SetActive(false);
        }

        private static void AppendStatus(ref string summary, string status)
        {
            if (string.IsNullOrEmpty(status)) return;
            summary = summary.Length == 0 ? status : $"{summary}  |  {status}";
        }

        private bool PlaceOnNavMesh(Vector3 position)
        {
            if (navigationAgent == null || !navigationAgent.enabled) return false;
            if (!NavMesh.SamplePosition(position, out var hit, 4f, NavMesh.AllAreas))
            {
                transform.position = position;
                return false;
            }
            transform.position = hit.position;
            navigationAgent.Warp(hit.position);
            return true;
        }

        private void EnsureCooldownStorage()
        {
            if (abilityReadyAt.Length == abilities.Count) return;
            abilityReadyAt = new float[abilities.Count];
        }

        private void ConfigureNavigationForArchetype()
        {
            if (navigationAgent == null) navigationAgent = GetComponent<NavMeshAgent>();
            if (navigationAgent == null) return;
            var scale = monsterArchetype switch
            {
                BattleArenaMonsterArchetype3D.Brute => 1.28f,
                BattleArenaMonsterArchetype3D.Elite => 1.45f,
                BattleArenaMonsterArchetype3D.Spitter => 0.82f,
                _ => 1f
            };
            transform.localScale = Vector3.one * scale;
            navigationAgent.radius = 0.34f * scale;
            navigationAgent.height = 1.75f * scale;
            navigationAgent.speed = movementSpeed;
            navigationAgent.acceleration = 20f;
            navigationAgent.angularSpeed = 720f;
            navigationAgent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            navigationAgent.avoidancePriority = 36 + Mathf.Abs(stableId.GetHashCode()) % 45;
        }

#if UNITY_EDITOR
        public void ConfigureHunterEditor(
            string id,
            string hunterName,
            string build,
            string traits,
            RaidCombatRole assignedRole,
            int assignedSquad,
            bool assignedSergeant,
            float healthValue,
            float manaValue,
            float regeneration,
            float speed,
            float damage,
            float range,
            float preferred,
            float cooldown,
            bool isRanged,
            Color color,
            float aggressionValue,
            float cohesionValue,
            float supportValue,
            IReadOnlyList<RaidAbilitySpec> assignedAbilities,
            NavMeshAgent assignedNavigationAgent,
            CapsuleCollider assignedHurtbox,
            BattleArena3DUnitView assignedView)
        {
            stableId = id;
            displayName = hunterName;
            buildLabel = build;
            traitLabel = traits;
            faction = BattleArenaFaction3D.Hunters;
            role = assignedRole;
            squadIndex = assignedSquad;
            sergeant = assignedSergeant;
            maximumHealth = healthValue;
            maximumMana = manaValue;
            manaRegeneration = regeneration;
            movementSpeed = speed;
            basicDamage = damage;
            attackRange = range;
            preferredRange = preferred;
            attackCooldown = cooldown;
            ranged = isRanged;
            themeColor = color;
            aggression = aggressionValue;
            cohesion = cohesionValue;
            support = supportValue;
            abilities = new List<RaidAbilitySpec>();
            if (assignedAbilities != null)
            {
                for (var index = 0; index < assignedAbilities.Count; index++) abilities.Add(assignedAbilities[index]);
            }
            navigationAgent = assignedNavigationAgent;
            hurtbox = assignedHurtbox;
            view = assignedView;
            EnsureCooldownStorage();
        }

        public void ConfigureTemplateEditor(
            NavMeshAgent assignedNavigationAgent,
            CapsuleCollider assignedHurtbox,
            BattleArena3DUnitView assignedView)
        {
            navigationAgent = assignedNavigationAgent;
            hurtbox = assignedHurtbox;
            view = assignedView;
            faction = BattleArenaFaction3D.Monsters;
            stableId = "monster-template";
            displayName = "Monster Template";
            abilities = new List<RaidAbilitySpec>();
            EnsureCooldownStorage();
        }
#endif
    }
}
