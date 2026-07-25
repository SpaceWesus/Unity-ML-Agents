using UnityEngine;

namespace Turtle.Combat
{
    [DisallowMultipleComponent]
    public sealed class AiCombatCommandSource : MonoBehaviour, ICombatCommandSource
    {
        [SerializeField, Min(0.05f)] private float decisionInterval = 0.12f;
        [SerializeField, Min(0.1f)] private float preferredRange = 2.05f;
        [SerializeField, Min(0f)] private float awarenessRange = 28f;
        [SerializeField, Range(0f, 1f)] private float heavyAttackChance = 0.22f;
        [SerializeField, Range(0f, 1f)] private float abilityUseChance = 0.48f;
        [SerializeField, Range(0f, 1f)] private float ultimateUseChance = 0.72f;
        [SerializeField, Range(0f, 1f)] private float dodgeChanceWhenThreatened = 0.72f;
        [SerializeField, Min(0.05f)] private float dodgeReactionWindow = 0.42f;
        [SerializeField, Min(0f)] private float dodgeCooldown = 1.15f;

        private Combatant target;
        private float nextDecisionAt;
        private float nextDodgeAt;
        private CombatCommand cachedCommand;

        public CombatCommand SampleCommand(Combatant self)
        {
            if (Time.time < nextDecisionAt)
            {
                return cachedCommand;
            }

            nextDecisionAt = Time.time + decisionInterval + Random.Range(-0.02f, 0.02f);
            if (self.CanDodge &&
                Time.time >= nextDodgeAt &&
                TryFindIncomingAttack(self, out var threat, out var dodgeMovement) &&
                Random.value <= dodgeChanceWhenThreatened)
            {
                var threatOffset = Vector3.ProjectOnPlane(
                    threat.transform.position - transform.position,
                    Vector3.up);
                var threatFacing = threatOffset.sqrMagnitude > 0.001f
                    ? threatOffset.normalized
                    : transform.forward;
                nextDodgeAt = Time.time + dodgeCooldown;
                cachedCommand = new CombatCommand(dodgeMovement, threatFacing, CombatAction.Dodge);
                return cachedCommand;
            }

            if (target == null || !self.CanTarget(target) ||
                Vector3.Distance(transform.position, target.transform.position) > awarenessRange)
            {
                target = FindNearestTarget(self);
            }

            if (target == null)
            {
                cachedCommand = new CombatCommand(Vector2.zero, transform.forward, CombatAction.None);
                return cachedCommand;
            }

            var offset = Vector3.ProjectOnPlane(
                target.transform.position - transform.position,
                Vector3.up);
            var distance = offset.magnitude;
            var facing = offset.sqrMagnitude > 0.001f ? offset.normalized : transform.forward;
            var movement = distance > preferredRange
                ? Vector2.up
                : distance < preferredRange * 0.7f
                    ? Vector2.down
                    : Vector2.zero;
            var action = CombatAction.None;
            if (!self.IsBusy &&
                TryChooseAbility(self, distance, out var abilityAction))
            {
                action = abilityAction;
            }
            else if (!self.IsBusy && distance <= preferredRange + 0.55f)
            {
                action = Random.value < heavyAttackChance
                    ? CombatAction.HeavyAttack
                    : CombatAction.LightAttack;
            }

            cachedCommand = new CombatCommand(movement, facing, action);
            return cachedCommand;
        }

        private Combatant FindNearestTarget(Combatant self)
        {
            Combatant nearestEnemy = null;
            Combatant nearestDummy = null;
            var nearestEnemySqrDistance = awarenessRange * awarenessRange;
            var nearestDummySqrDistance = awarenessRange * awarenessRange;
            var candidates = CombatantRegistry.All;
            for (var index = 0; index < candidates.Count; index++)
            {
                var candidate = candidates[index];
                if (!self.CanTarget(candidate))
                {
                    continue;
                }

                var sqrDistance = (candidate.transform.position - transform.position).sqrMagnitude;
                if (candidate.IsTargetDummy)
                {
                    if (sqrDistance < nearestDummySqrDistance)
                    {
                        nearestDummy = candidate;
                        nearestDummySqrDistance = sqrDistance;
                    }
                }
                else if (sqrDistance < nearestEnemySqrDistance)
                {
                    nearestEnemy = candidate;
                    nearestEnemySqrDistance = sqrDistance;
                }
            }
            return nearestEnemy != null ? nearestEnemy : nearestDummy;
        }

        private bool TryChooseAbility(
            Combatant self,
            float targetDistance,
            out CombatAction action)
        {
            action = CombatAction.None;
            var abilities = self.Abilities;
            if (abilities == null || abilities.Loadout == null)
            {
                return false;
            }

            const int ultimateSlot = CombatAbilityLoadoutDefinition.UltimateSlot;
            var ultimate = abilities.GetAbility(ultimateSlot);
            if (ultimate != null &&
                abilities.IsReady(ultimateSlot) &&
                ultimate.IsInAiRange(targetDistance) &&
                Random.value <= ultimateUseChance)
            {
                action = CombatAction.Ultimate;
                return true;
            }

            if (Random.value > abilityUseChance)
            {
                return false;
            }

            var firstSlot = Random.Range(0, ultimateSlot);
            for (var offset = 0; offset < ultimateSlot; offset++)
            {
                var slot = (firstSlot + offset) % ultimateSlot;
                var ability = abilities.GetAbility(slot);
                if (ability == null ||
                    !abilities.IsReady(slot) ||
                    !ability.IsInAiRange(targetDistance) ||
                    !ShouldUseAbility(self, ability, targetDistance))
                {
                    continue;
                }

                action = slot switch
                {
                    0 => CombatAction.Ability1,
                    1 => CombatAction.Ability2,
                    _ => CombatAction.Ability3
                };
                return true;
            }
            return false;
        }

        private bool ShouldUseAbility(
            Combatant self,
            CombatAbilityDefinition ability,
            float targetDistance)
        {
            return ability.AiIntent switch
            {
                AbilityAiIntent.Defensive => self.HealthRatio <= 0.72f &&
                                             self.Abilities.BarrierHealth <= 0f,
                AbilityAiIntent.Mobility => targetDistance > preferredRange + 1.5f,
                AbilityAiIntent.Offensive => true,
                AbilityAiIntent.Utility => Random.value <= 0.5f,
                _ => false
            };
        }

        private bool TryFindIncomingAttack(
            Combatant self,
            out Combatant threat,
            out Vector2 dodgeMovement)
        {
            threat = null;
            dodgeMovement = Vector2.zero;
            var bestTimeUntilActive = float.PositiveInfinity;
            var candidates = CombatantRegistry.All;
            for (var index = 0; index < candidates.Count; index++)
            {
                var candidate = candidates[index];
                if (!candidate.IsAttacking || !candidate.CanTarget(self))
                {
                    continue;
                }

                var offset = Vector3.ProjectOnPlane(
                    self.transform.position - candidate.transform.position,
                    Vector3.up);
                var attack = candidate.CurrentAttack;
                if (offset.sqrMagnitude > Mathf.Pow(attack.range + 0.8f, 2f) ||
                    Vector3.Angle(candidate.transform.forward, offset) > attack.arc * 0.5f + 12f)
                {
                    continue;
                }

                var timeUntilActive = candidate.AttackActiveAt - Time.time;
                if (timeUntilActive > dodgeReactionWindow ||
                    timeUntilActive < -attack.activeDuration ||
                    timeUntilActive >= bestTimeUntilActive)
                {
                    continue;
                }

                bestTimeUntilActive = timeUntilActive;
                threat = candidate;
            }

            if (threat == null)
            {
                return false;
            }

            // CombatCommand movement is relative to its facing. Side rolls preserve
            // pressure while leaving the telegraphed weapon path.
            dodgeMovement = Random.value < 0.5f ? Vector2.left : Vector2.right;
            return true;
        }
    }
}
