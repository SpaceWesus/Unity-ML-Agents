using System;
using System.Collections;
using UnityEngine;

namespace Turtle.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class Combatant : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private string displayName = "Hunter";
        [SerializeField] private CombatTeam team;
        [SerializeField] private bool targetDummy;

        [Header("Capabilities")]
        [SerializeField, Min(1f)] private float maxHealth = 100f;
        [SerializeField, Min(0f)] private float moveSpeed = 6.5f;
        [SerializeField, Min(0f)] private float rotationSpeed = 18f;
        [SerializeField, Min(0f)] private float dodgeSpeed = 16f;
        [SerializeField, Min(0.1f)] private float dodgeTravelDuration = 0.5f;
        [SerializeField, Min(0.1f)] private float dodgeActionDuration = 1.2f;
        [SerializeField, Min(0f)] private float dodgeInvulnerabilityStart = 0.04f;
        [SerializeField, Min(0.01f)] private float dodgeInvulnerabilityDuration = 0.58f;
        [SerializeField, Min(0f)] private float hitReactionDuration = 0.34f;
        [SerializeField] private WeaponMoveSetDefinition moveSet;

        [Header("References")]
        [SerializeField] private CharacterController characterController;
        [SerializeField] private CombatAnimationView animationView;
        [SerializeField] private CombatAgentDriver commandDriver;
        [SerializeField] private CombatWeaponHitbox weaponHitbox;
        [SerializeField] private CombatHurtbox hurtbox;
        [SerializeField] private CombatAbilityController abilityController;

        private float currentHealth;
        private float verticalVelocity;
        private float actionEndsAt;
        private float dodgeInvulnerableFrom;
        private float dodgeInvulnerableUntil;
        private float attackActiveAt;
        private AttackDefinition currentAttack;
        private CombatAbilityDefinition currentAbility;
        private bool isAttacking;
        private bool isUsingAbility;
        private Coroutine actionRoutine;
        private Coroutine knockbackRoutine;

        public string DisplayName => displayName;
        public CombatTeam Team => team;
        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public float HealthRatio => maxHealth <= 0f ? 0f : currentHealth / maxHealth;
        public bool IsAlive => currentHealth > 0f;
        public bool IsTargetDummy => targetDummy;
        public bool IsBusy => Time.time < actionEndsAt;
        public bool IsIntangible => Time.time >= dodgeInvulnerableFrom && Time.time < dodgeInvulnerableUntil;
        public bool IsAttacking => isAttacking;
        public bool IsUsingAbility => isUsingAbility;
        public bool CanDodge => IsAlive &&
                                !targetDummy &&
                                (!IsBusy ||
                                 (isAttacking && currentAttack.AllowsDodgeCancel) ||
                                 (isUsingAbility && currentAbility != null &&
                                  currentAbility.AllowsDodgeCancel));
        public float AttackActiveAt => attackActiveAt;
        public AttackDefinition CurrentAttack => currentAttack;
        public CombatAbilityDefinition CurrentAbility => currentAbility;
        public WeaponMoveSetDefinition MoveSet => moveSet;
        public CombatAbilityController Abilities => abilityController;

        public event Action<Combatant, Combatant, float> Damaged;
        public event Action<Combatant, Combatant> Defeated;
        public event Action<Combatant, Combatant, float> AttackConnected;

        private void Awake()
        {
            characterController ??= GetComponent<CharacterController>();
            animationView ??= GetComponentInChildren<CombatAnimationView>(true);
            commandDriver ??= GetComponent<CombatAgentDriver>();
            weaponHitbox ??= GetComponentInChildren<CombatWeaponHitbox>(true);
            hurtbox ??= GetComponentInChildren<CombatHurtbox>(true);
            abilityController ??= GetComponent<CombatAbilityController>();
            currentHealth = maxHealth;
        }

        private void OnEnable()
        {
            if (currentHealth <= 0f)
            {
                currentHealth = maxHealth;
            }
            CombatantRegistry.Register(this);
        }

        private void OnDisable()
        {
            weaponHitbox?.EndAttack();
            CombatantRegistry.Unregister(this);
        }

        public void Simulate(CombatCommand command, float deltaTime)
        {
            if (!IsAlive || targetDummy)
            {
                animationView?.SetLocomotion(0f, IsBusy);
                return;
            }

            var movementDirection = ApplyMovement(command, deltaTime);
            if (command.Action == CombatAction.Dodge && CanDodge)
            {
                BeginAction(command.Action, command.Facing, movementDirection);
            }
            else if (!IsBusy && command.Action != CombatAction.None)
            {
                BeginAction(command.Action, command.Facing, movementDirection);
            }
        }

        public bool CanTarget(Combatant other)
        {
            return other != null &&
                   other != this &&
                   other.IsAlive &&
                   (other.targetDummy ||
                    (other.team != CombatTeam.Neutral && other.team != team));
        }

        public bool TryReceiveHit(in CombatHit hit)
        {
            return TryReceiveDamage(
                hit.Attacker,
                hit.Attack.damage,
                hit.Attack.knockback,
                hit.Direction,
                true);
        }

        public bool TryReceiveAbilityDamage(
            Combatant source,
            float damage,
            float knockback,
            Vector3 direction)
        {
            return TryReceiveDamage(source, damage, knockback, direction, false);
        }

        public void NotifyAttackConnected(Combatant target, float damage)
        {
            abilityController?.NotifyDamageDealt(damage);
            AttackConnected?.Invoke(this, target, damage);
        }

        public void TeleportBy(Vector3 direction, float distance)
        {
            direction = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
            if (direction.sqrMagnitude < 0.001f || distance <= 0f)
            {
                return;
            }
            characterController.Move(direction * distance);
            transform.rotation = Quaternion.LookRotation(direction);
        }

        public void ResetCombatant()
        {
            if (actionRoutine != null)
            {
                StopCoroutine(actionRoutine);
            }
            weaponHitbox?.EndAttack();
            if (knockbackRoutine != null)
            {
                StopCoroutine(knockbackRoutine);
            }
            currentHealth = maxHealth;
            actionEndsAt = 0f;
            dodgeInvulnerableFrom = 0f;
            dodgeInvulnerableUntil = 0f;
            attackActiveAt = 0f;
            currentAttack = default;
            currentAbility = null;
            isAttacking = false;
            isUsingAbility = false;
            verticalVelocity = 0f;
            characterController.enabled = true;
            abilityController?.ResetAbilityState();
            animationView?.ResetView();
        }

        private Vector3 ApplyMovement(CombatCommand command, float deltaTime)
        {
            var facing = command.Facing.sqrMagnitude > 0.001f ? command.Facing : transform.forward;
            var right = Vector3.Cross(Vector3.up, facing).normalized;
            var movement = facing * command.Movement.y + right * command.Movement.x;
            movement = Vector3.ClampMagnitude(movement, 1f);
            var actionAllowsMovement =
                (isAttacking && currentAttack.AllowsMovement) ||
                (isUsingAbility && currentAbility != null && currentAbility.AllowsMovement);
            var movementLocked = IsBusy && !actionAllowsMovement;

            if (!movementLocked &&
                (isAttacking || isUsingAbility) &&
                facing.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(facing),
                    rotationSpeed * deltaTime);
            }
            else if (!movementLocked && movement.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(movement),
                    rotationSpeed * deltaTime);
            }
            else if (!movementLocked && facing.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(facing),
                    rotationSpeed * deltaTime);
            }

            verticalVelocity = characterController.isGrounded
                ? -1f
                : verticalVelocity + Physics.gravity.y * deltaTime;
            var speed = movementLocked ? 0f : moveSpeed;
            characterController.Move((movement * speed + Vector3.up * verticalVelocity) * deltaTime);
            animationView?.SetLocomotion(movement.magnitude * speed, IsBusy);
            return movement;
        }

        private void BeginAction(CombatAction action, Vector3 facing, Vector3 movementDirection)
        {
            if (actionRoutine != null)
            {
                StopCoroutine(actionRoutine);
                actionRoutine = null;
            }
            if (isAttacking)
            {
                weaponHitbox?.EndAttack();
                isAttacking = false;
                attackActiveAt = 0f;
                currentAttack = default;
            }
            isUsingAbility = false;
            currentAbility = null;

            if (facing.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(facing);
            }

            var abilitySlot = GetAbilitySlot(action);
            if (abilitySlot >= 0)
            {
                if (abilityController != null &&
                    abilityController.TryPrepare(abilitySlot, out var ability))
                {
                    actionRoutine = StartCoroutine(UseAbility(
                        abilitySlot,
                        ability,
                        facing));
                }
                return;
            }

            actionRoutine = action switch
            {
                CombatAction.LightAttack => StartCoroutine(Attack(moveSet.LightAttack)),
                CombatAction.HeavyAttack => StartCoroutine(Attack(moveSet.HeavyAttack)),
                CombatAction.Dodge => StartCoroutine(Dodge(
                    movementDirection.sqrMagnitude > 0.001f
                        ? movementDirection.normalized
                        : transform.forward)),
                _ => null
            };
        }

        private IEnumerator UseAbility(
            int slot,
            CombatAbilityDefinition ability,
            Vector3 direction)
        {
            currentAbility = ability;
            isUsingAbility = true;
            var startedAt = Time.time;
            var commitAt = startedAt + ability.CastTime;
            actionEndsAt = startedAt + ability.ActionDuration;

            while (Time.time < commitAt)
            {
                yield return null;
            }

            if (!abilityController.TryCommit(slot, ability, direction))
            {
                actionEndsAt = 0f;
                isUsingAbility = false;
                currentAbility = null;
                actionRoutine = null;
                yield break;
            }

            while (Time.time < actionEndsAt)
            {
                yield return null;
            }
            isUsingAbility = false;
            currentAbility = null;
            actionRoutine = null;
        }

        private IEnumerator Attack(AttackDefinition attack)
        {
            currentAttack = attack;
            isAttacking = true;
            var attackStartedAt = Time.time;
            var animationDuration = attack.animationDuration > 0.05f
                ? attack.animationDuration
                : Mathf.Max(0.05f, attack.windup + attack.activeDuration + attack.recovery);
            var firstHitboxTime = attack.FirstHitboxStartNormalized;
            var lastHitboxTime = Mathf.Max(
                firstHitboxTime,
                attack.LastHitboxEndNormalized);
            attackActiveAt = attackStartedAt + firstHitboxTime * animationDuration;
            actionEndsAt = attackStartedAt + animationDuration;
            animationView?.PlayAction(attack.animationState);
            weaponHitbox?.BeginAttack(attack);
            var forward = transform.forward;
            var activeSpanSeconds = Mathf.Max(
                0.01f,
                (lastHitboxTime - firstHitboxTime) * animationDuration);
            while (Time.time < actionEndsAt)
            {
                var normalizedProgress = Mathf.Clamp01(
                    (Time.time - attackStartedAt) / animationDuration);
                weaponHitbox?.SetNormalizedProgress(normalizedProgress);
                if (attack.lunge > 0f &&
                    normalizedProgress >= firstHitboxTime &&
                    normalizedProgress <= lastHitboxTime)
                {
                    characterController.Move(
                        forward * (attack.lunge / activeSpanSeconds * Time.deltaTime));
                }
                yield return null;
            }
            weaponHitbox?.EndAttack();
            isAttacking = false;
            attackActiveAt = 0f;
            currentAttack = default;
            actionRoutine = null;
        }

        private IEnumerator Dodge(Vector3 direction)
        {
            direction = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
            if (direction.sqrMagnitude < 0.001f)
            {
                direction = transform.forward;
            }
            transform.rotation = Quaternion.LookRotation(direction);
            actionEndsAt = Time.time + Mathf.Max(dodgeActionDuration, dodgeTravelDuration);
            dodgeInvulnerableFrom = Time.time + dodgeInvulnerabilityStart;
            dodgeInvulnerableUntil = dodgeInvulnerableFrom + dodgeInvulnerabilityDuration;
            animationView?.PlayAction("Dodge");
            var elapsed = 0f;
            while (elapsed < dodgeTravelDuration)
            {
                elapsed += Time.deltaTime;
                var normalizedTime = Mathf.Clamp01(elapsed / dodgeTravelDuration);
                var speedMultiplier = 1f - normalizedTime * 0.45f;
                characterController.Move(direction * (dodgeSpeed * speedMultiplier * Time.deltaTime));
                yield return null;
            }

            var remainingAction = Mathf.Max(0f, actionEndsAt - Time.time);
            if (remainingAction > 0f)
            {
                yield return new WaitForSeconds(remainingAction);
            }
            dodgeInvulnerableFrom = 0f;
            dodgeInvulnerableUntil = 0f;
            actionRoutine = null;
        }

        private void InterruptAction()
        {
            if (actionRoutine != null)
            {
                StopCoroutine(actionRoutine);
                actionRoutine = null;
            }
            weaponHitbox?.EndAttack();
            isAttacking = false;
            attackActiveAt = 0f;
            currentAttack = default;
            currentAbility = null;
            isUsingAbility = false;
            dodgeInvulnerableFrom = 0f;
            dodgeInvulnerableUntil = 0f;
        }

        private bool TryReceiveDamage(
            Combatant source,
            float damage,
            float knockback,
            Vector3 direction,
            bool weaponImpact)
        {
            if (!IsAlive ||
                IsIntangible ||
                damage <= 0f ||
                source == null ||
                !source.CanTarget(this))
            {
                return false;
            }

            var healthDamage = abilityController != null
                ? abilityController.AbsorbDamage(damage)
                : damage;
            if (healthDamage <= 0f)
            {
                return true;
            }

            InterruptAction();
            currentHealth = Mathf.Max(0f, currentHealth - healthDamage);
            actionEndsAt = Time.time + hitReactionDuration;
            animationView?.PlayHit();
            if (!targetDummy)
            {
                CombatFeedbackPool.SpawnBlood(
                    transform.position + Vector3.up * 1.15f,
                    direction);
                if (weaponImpact)
                {
                    source.animationView?.EmitWeaponBlood();
                }
            }
            abilityController?.NotifyDamageReceived(healthDamage);
            Damaged?.Invoke(this, source, healthDamage);

            if (!targetDummy)
            {
                if (knockbackRoutine != null)
                {
                    StopCoroutine(knockbackRoutine);
                }
                knockbackRoutine = StartCoroutine(ApplyKnockback(direction, knockback));
            }

            if (currentHealth <= 0f)
            {
                actionEndsAt = float.PositiveInfinity;
                animationView?.PlayDeath();
                Defeated?.Invoke(this, source);
            }
            return true;
        }

        private static int GetAbilitySlot(CombatAction action)
        {
            return action switch
            {
                CombatAction.Ability1 => 0,
                CombatAction.Ability2 => 1,
                CombatAction.Ability3 => 2,
                CombatAction.Ultimate => CombatAbilityLoadoutDefinition.UltimateSlot,
                _ => -1
            };
        }

        private IEnumerator ApplyKnockback(Vector3 direction, float distance)
        {
            direction = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
            var elapsed = 0f;
            const float duration = 0.18f;
            while (elapsed < duration && direction.sqrMagnitude > 0.001f)
            {
                elapsed += Time.deltaTime;
                var remaining = 1f - Mathf.Clamp01(elapsed / duration);
                characterController.Move(direction * (distance * remaining * 2f / duration * Time.deltaTime));
                yield return null;
            }
            knockbackRoutine = null;
        }

#if UNITY_EDITOR
        public void ConfigureEditor(
            string name,
            CombatTeam assignedTeam,
            bool isDummy,
            float health,
            float speed,
            WeaponMoveSetDefinition assignedMoveSet,
            CombatAnimationView view,
            CombatAgentDriver driver,
            CombatWeaponHitbox assignedWeaponHitbox,
            CombatHurtbox assignedHurtbox,
            CombatAbilityController assignedAbilityController = null)
        {
            displayName = name;
            team = assignedTeam;
            targetDummy = isDummy;
            maxHealth = health;
            moveSpeed = speed;
            moveSet = assignedMoveSet;
            characterController = GetComponent<CharacterController>();
            animationView = view;
            commandDriver = driver;
            weaponHitbox = assignedWeaponHitbox;
            hurtbox = assignedHurtbox;
            abilityController = assignedAbilityController;
        }

        public void ConfigureCombatVolumesEditor(
            CombatWeaponHitbox assignedWeaponHitbox,
            CombatHurtbox assignedHurtbox)
        {
            weaponHitbox = assignedWeaponHitbox;
            hurtbox = assignedHurtbox;
        }

        public void ConfigureAbilityControllerEditor(
            CombatAbilityController assignedAbilityController)
        {
            abilityController = assignedAbilityController;
        }
#endif
    }
}
