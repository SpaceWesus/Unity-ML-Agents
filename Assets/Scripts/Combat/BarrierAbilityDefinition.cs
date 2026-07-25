using UnityEngine;

namespace Turtle.Combat
{
    [CreateAssetMenu(
        menuName = "Turtle/Combat/Abilities/Barrier",
        fileName = "Barrier Ability")]
    public sealed class BarrierAbilityDefinition : CombatAbilityDefinition
    {
        [Header("Barrier")]
        [SerializeField, Min(1f)] private float capacity = 65f;
        [SerializeField, Min(0.1f)] private float duration = 6f;

        public override void Activate(in CombatAbilityContext context)
        {
            context.Controller?.GrantBarrier(capacity, duration, ThemeColor);
        }

#if UNITY_EDITOR
        public void ConfigureEditor(
            string id,
            string name,
            string details,
            float cooldownSeconds,
            float manaCost,
            float castSeconds,
            float recoverySeconds,
            float barrierCapacity,
            float barrierDuration,
            Color color)
        {
            base.ConfigureEditor(
                id,
                name,
                details,
                "Barrier Magic",
                cooldownSeconds,
                manaCost,
                castSeconds,
                recoverySeconds,
                AbilityMovementMode.Mobile,
                AbilityDodgeCancelMode.DodgeAllowed,
                AbilityAiIntent.Defensive,
                0f,
                12f,
                color);
            capacity = barrierCapacity;
            duration = barrierDuration;
        }
#endif
    }
}
