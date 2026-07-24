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
        [SerializeField, Min(0.1f)] private float dodgeDuration = 0.25f;
        [SerializeField] private WeaponMoveSetDefinition moveSet;

        [Header("References")]
        [SerializeField] private CharacterController characterController;
        [SerializeField] private CombatAnimationView animationView;
        [SerializeField] private CombatAgentDriver commandDriver;

        private float currentHealth;
        private float verticalVelocity;
        private float actionEndsAt;
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
        public WeaponMoveSetDefinition MoveSet => moveSet;

        public event Action<Combatant, Combatant, float> Damaged;
        public event Action<Combatant, Combatant> Defeated;

        private void Awake()
        {
            characterController ??= GetComponent<CharacterController>();
            animationView ??= GetComponentInChildren<CombatAnimationView>(true);
            commandDriver ??= GetComponent<CombatAgentDriver>();
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
            CombatantRegistry.Unregister(this);
        }

        public void Simulate(CombatCommand command, float deltaTime)
        {
            if (!IsAlive || targetDummy)
            {
                animationView?.SetLocomotion(0f, IsBusy);
                return;
            }

            ApplyMovement(command, deltaTime);
            if (!IsBusy && command.Action != CombatAction.None)
            {
                BeginAction(command.Action, command.Facing);
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

        public void ReceiveDamage(float amount, Combatant source, Vector3 direction, float knockback)
        {
            if (!IsAlive || amount <= 0f || (source != null && !source.CanTarget(this)))
            {
                return;
            }

            currentHealth = Mathf.Max(0f, currentHealth - amount);
            animationView?.PlayHit();
            CombatFeedbackPool.SpawnBlood(
                transform.position + Vector3.up * 1.15f,
                direction);
            source?.animationView?.EmitWeaponBlood();
            Damaged?.Invoke(this, source, amount);

            if (knockbackRoutine != null)
            {
                StopCoroutine(knockbackRoutine);
            }
            knockbackRoutine = StartCoroutine(ApplyKnockback(direction, knockback));

            if (currentHealth <= 0f)
            {
                actionEndsAt = float.PositiveInfinity;
                animationView?.PlayDeath();
                Defeated?.Invoke(this, source);
            }
        }

        public void ResetCombatant()
        {
            if (actionRoutine != null)
            {
                StopCoroutine(actionRoutine);
            }
            if (knockbackRoutine != null)
            {
                StopCoroutine(knockbackRoutine);
            }
            currentHealth = maxHealth;
            actionEndsAt = 0f;
            verticalVelocity = 0f;
            characterController.enabled = true;
            animationView?.ResetView();
        }

        private void ApplyMovement(CombatCommand command, float deltaTime)
        {
            var facing = command.Facing.sqrMagnitude > 0.001f ? command.Facing : transform.forward;
            var right = Vector3.Cross(Vector3.up, facing).normalized;
            var movement = facing * command.Movement.y + right * command.Movement.x;
            movement = Vector3.ClampMagnitude(movement, 1f);

            if (!IsBusy && movement.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(movement),
                    rotationSpeed * deltaTime);
            }
            else if (!IsBusy && facing.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(facing),
                    rotationSpeed * deltaTime);
            }

            verticalVelocity = characterController.isGrounded
                ? -1f
                : verticalVelocity + Physics.gravity.y * deltaTime;
            var speed = IsBusy ? 0f : moveSpeed;
            characterController.Move((movement * speed + Vector3.up * verticalVelocity) * deltaTime);
            animationView?.SetLocomotion(movement.magnitude * speed, IsBusy);
        }

        private void BeginAction(CombatAction action, Vector3 facing)
        {
            if (actionRoutine != null)
            {
                StopCoroutine(actionRoutine);
            }

            if (facing.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(facing);
            }

            actionRoutine = action switch
            {
                CombatAction.LightAttack => StartCoroutine(Attack(moveSet.LightAttack)),
                CombatAction.HeavyAttack => StartCoroutine(Attack(moveSet.HeavyAttack)),
                CombatAction.Dodge => StartCoroutine(Dodge()),
                _ => null
            };
        }

        private IEnumerator Attack(AttackDefinition attack)
        {
            actionEndsAt = Time.time + attack.windup + attack.recovery;
            animationView?.PlayAction(attack.animationState);
            var start = transform.position;
            yield return new WaitForSeconds(attack.windup);

            var forward = transform.forward;
            var target = FindBestTarget(forward, attack.range, attack.arc);
            if (target != null)
            {
                var direction = Vector3.ProjectOnPlane(
                    target.transform.position - transform.position,
                    Vector3.up).normalized;
                target.ReceiveDamage(attack.damage, this, direction, attack.knockback);
            }

            var lungeElapsed = 0f;
            const float lungeDuration = 0.12f;
            while (lungeElapsed < lungeDuration && attack.lunge > 0f)
            {
                lungeElapsed += Time.deltaTime;
                var remaining = 1f - Mathf.Clamp01(lungeElapsed / lungeDuration);
                characterController.Move(forward * (attack.lunge * remaining * 2f / lungeDuration * Time.deltaTime));
                yield return null;
            }

            var remainingRecovery = Mathf.Max(0f, actionEndsAt - Time.time);
            if (remainingRecovery > 0f)
            {
                yield return new WaitForSeconds(remainingRecovery);
            }
            actionRoutine = null;
        }

        private IEnumerator Dodge()
        {
            actionEndsAt = Time.time + dodgeDuration;
            animationView?.PlayAction("Dodge");
            var elapsed = 0f;
            while (elapsed < dodgeDuration)
            {
                elapsed += Time.deltaTime;
                characterController.Move(transform.forward * (dodgeSpeed * Time.deltaTime));
                yield return null;
            }
            actionRoutine = null;
        }

        private Combatant FindBestTarget(Vector3 direction, float range, float arc)
        {
            Combatant best = null;
            var bestScore = float.PositiveInfinity;
            var origin = transform.position;
            var candidates = CombatantRegistry.All;
            for (var index = 0; index < candidates.Count; index++)
            {
                var candidate = candidates[index];
                if (!CanTarget(candidate))
                {
                    continue;
                }

                var offset = Vector3.ProjectOnPlane(candidate.transform.position - origin, Vector3.up);
                var distance = offset.magnitude;
                if (distance > range || Vector3.Angle(direction, offset) > arc * 0.5f)
                {
                    continue;
                }

                var score = distance + Vector3.Angle(direction, offset) * 0.04f;
                if (score < bestScore)
                {
                    best = candidate;
                    bestScore = score;
                }
            }
            return best;
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
            CombatAgentDriver driver)
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
        }
#endif
    }
}
