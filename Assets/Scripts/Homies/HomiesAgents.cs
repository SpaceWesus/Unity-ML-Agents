using UnityEngine;

namespace Turtle.Homies
{
    [DisallowMultipleComponent]
    public sealed class HomiesEnemyAgent : MonoBehaviour
    {
        private HomiesArenaDirector director;
        private HomiesCombatant combatant;
        private HomiesCombatant target;
        private float moveSpeed;
        private float attackReadyAt;

        public void Initialize(HomiesArenaDirector arenaDirector, float speed)
        {
            director = arenaDirector;
            combatant = GetComponent<HomiesCombatant>();
            moveSpeed = speed;
        }

        private void Update()
        {
            if (!combatant.IsAlive)
            {
                return;
            }

            if (target == null || !target.IsAlive || target.Faction == HomiesFaction.Enemy)
            {
                target = director.FindNearestHunter(transform.position);
            }

            if (target == null)
            {
                return;
            }

            var offset = Vector3.ProjectOnPlane(target.transform.position - transform.position, Vector3.up);
            if (offset.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(offset),
                    9f * Time.deltaTime);
            }

            if (offset.magnitude > 1.65f)
            {
                transform.position += offset.normalized * (moveSpeed * Time.deltaTime);
            }
            else if (Time.time >= attackReadyAt)
            {
                attackReadyAt = Time.time + (combatant.IsBoss ? 0.72f : 1.05f);
                combatant.PlayAttack();
                target.TakeDamage(
                    combatant.AttackDamage,
                    combatant,
                    offset.normalized,
                    combatant.IsBoss ? 2.2f : 1.05f);
            }
        }
    }

    [DisallowMultipleComponent]
    public sealed class HomiesShadowAgent : MonoBehaviour
    {
        private HomiesArenaDirector director;
        private HomiesCombatant combatant;
        private HomiesCombatant target;
        private Transform owner;
        private float attackReadyAt;
        private Vector3 formationOffset;

        public void Initialize(HomiesArenaDirector arenaDirector, Transform shadowOwner, int formationIndex)
        {
            director = arenaDirector;
            owner = shadowOwner;
            combatant = GetComponent<HomiesCombatant>();
            var angle = formationIndex * 137.5f * Mathf.Deg2Rad;
            formationOffset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) *
                              (2.1f + formationIndex * 0.18f);
        }

        public void CommandAttack(HomiesCombatant commandedTarget)
        {
            target = commandedTarget;
        }

        private void Update()
        {
            if (!combatant.IsAlive)
            {
                return;
            }

            if (target == null || !target.IsAlive)
            {
                target = director.FindNearestEnemy(transform.position, 11f);
            }

            if (target != null)
            {
                FightTarget();
                return;
            }

            var desired = owner.position + owner.TransformDirection(formationOffset);
            MoveTowards(desired, 7.5f, 1.1f);
        }

        private void FightTarget()
        {
            var offset = Vector3.ProjectOnPlane(target.transform.position - transform.position, Vector3.up);
            if (offset.magnitude > 1.6f)
            {
                MoveTowards(target.transform.position, 8.5f, 1.4f);
            }
            else if (Time.time >= attackReadyAt)
            {
                attackReadyAt = Time.time + 0.85f;
                combatant.PlayAttack();
                target.TakeDamage(combatant.AttackDamage, combatant, offset.normalized, 1.25f);
            }
        }

        private void MoveTowards(Vector3 destination, float speed, float stopDistance)
        {
            var offset = Vector3.ProjectOnPlane(destination - transform.position, Vector3.up);
            if (offset.magnitude <= stopDistance)
            {
                return;
            }

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(offset),
                10f * Time.deltaTime);
            transform.position += offset.normalized * (speed * Time.deltaTime);
        }
    }
}
