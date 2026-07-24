using UnityEngine;

namespace Turtle.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class CombatHurtbox : MonoBehaviour
    {
        [SerializeField] private Combatant owner;
        [SerializeField] private Collider hurtboxCollider;

        public Combatant Owner => owner;

        private void Awake()
        {
            hurtboxCollider ??= GetComponent<Collider>();
            owner ??= GetComponentInParent<Combatant>();
            if (hurtboxCollider != null)
            {
                hurtboxCollider.isTrigger = true;
            }
        }

        public bool TryReceiveHit(in CombatHit hit)
        {
            return owner != null &&
                   !owner.IsIntangible &&
                   hit.Attacker != null &&
                   hit.Attacker.CanTarget(owner) &&
                   owner.TryReceiveHit(hit);
        }

#if UNITY_EDITOR
        public void ConfigureEditor(Combatant assignedOwner, Collider assignedCollider)
        {
            owner = assignedOwner;
            hurtboxCollider = assignedCollider;
            hurtboxCollider.isTrigger = true;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.15f, 0.85f, 1f, 0.35f);
            if (hurtboxCollider is CapsuleCollider capsule)
            {
                Gizmos.matrix = capsule.transform.localToWorldMatrix;
                Gizmos.DrawWireSphere(capsule.center + Vector3.up * Mathf.Max(0f, capsule.height * 0.5f - capsule.radius), capsule.radius);
                Gizmos.DrawWireSphere(capsule.center - Vector3.up * Mathf.Max(0f, capsule.height * 0.5f - capsule.radius), capsule.radius);
            }
        }
#endif
    }
}
