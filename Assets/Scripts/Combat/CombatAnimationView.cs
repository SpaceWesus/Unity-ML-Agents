using UnityEngine;

namespace Turtle.Combat
{
    [DisallowMultipleComponent]
    public sealed class CombatAnimationView : MonoBehaviour
    {
        private static readonly int IdleState = Animator.StringToHash("Base Layer.Idle");
        private static readonly int RunState = Animator.StringToHash("Base Layer.Run");
        private static readonly int HitState = Animator.StringToHash("Base Layer.Hit");
        private static readonly int DeathState = Animator.StringToHash("Base Layer.Death");

        [SerializeField] private Animator animator;
        [SerializeField] private Transform weapon;
        private ParticleSystem weaponBlood;
        private int locomotionState;

        private void Awake()
        {
            animator ??= GetComponentInChildren<Animator>(true);
            if (animator != null &&
                animator.GetComponent<CombatAnimationEventRelay>() == null)
            {
                animator.gameObject.AddComponent<CombatAnimationEventRelay>();
            }
            if (weapon != null)
            {
                weaponBlood = CombatFeedbackPool.CreateWeaponDrips(weapon);
            }
        }

        public void SetLocomotion(float speed, bool actionLocked)
        {
            if (animator == null || actionLocked)
            {
                return;
            }

            var desiredState = speed > 0.15f ? RunState : IdleState;
            if (desiredState == locomotionState)
            {
                return;
            }

            locomotionState = desiredState;
            animator.CrossFade(desiredState, 0.14f);
        }

        public void PlayAction(string stateName)
        {
            if (animator == null || string.IsNullOrWhiteSpace(stateName))
            {
                return;
            }
            locomotionState = 0;
            animator.CrossFade(Animator.StringToHash($"Base Layer.{stateName}"), 0.08f);
        }

        public void PlayHit()
        {
            if (animator != null)
            {
                locomotionState = 0;
                animator.CrossFade(HitState, 0.04f);
            }
        }

        public void PlayDeath()
        {
            if (animator != null)
            {
                locomotionState = DeathState;
                animator.CrossFade(DeathState, 0.1f);
            }
        }

        public void EmitWeaponBlood()
        {
            if (weaponBlood != null)
            {
                weaponBlood.Emit(Random.Range(5, 10));
            }
        }

        public void ResetView()
        {
            locomotionState = IdleState;
            if (animator != null)
            {
                animator.Play(IdleState, 0, 0f);
            }
        }

#if UNITY_EDITOR
        public void ConfigureEditor(Animator assignedAnimator, Transform assignedWeapon)
        {
            animator = assignedAnimator;
            weapon = assignedWeapon;
        }
#endif
    }
}
