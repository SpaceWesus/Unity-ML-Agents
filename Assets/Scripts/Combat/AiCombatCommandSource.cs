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
        [SerializeField, Range(0f, 1f)] private float dodgeChanceWhenThreatened = 0.08f;

        private Combatant target;
        private float nextDecisionAt;
        private CombatCommand cachedCommand;

        public CombatCommand SampleCommand(Combatant self)
        {
            if (Time.time < nextDecisionAt)
            {
                return cachedCommand;
            }

            nextDecisionAt = Time.time + decisionInterval + Random.Range(-0.02f, 0.02f);
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
            if (!self.IsBusy && distance <= preferredRange + 0.55f)
            {
                var roll = Random.value;
                action = roll < dodgeChanceWhenThreatened
                    ? CombatAction.Dodge
                    : roll < heavyAttackChance + dodgeChanceWhenThreatened
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
    }
}
