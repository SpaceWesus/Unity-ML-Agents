using UnityEngine;

namespace Turtle.Combat
{
    [CreateAssetMenu(
        menuName = "Turtle/Combat/Abilities/Teleport",
        fileName = "Teleport Ability")]
    public sealed class TeleportAbilityDefinition : CombatAbilityDefinition
    {
        [Header("Teleport")]
        [SerializeField, Min(0.1f)] private float distance = 7f;

        public override void Activate(in CombatAbilityContext context)
        {
            if (context.Caster == null)
            {
                return;
            }

            CombatFeedbackPool.SpawnMagicPulse(
                context.Caster.transform.position + Vector3.up,
                ThemeColor,
                0.4f,
                1.6f,
                0.24f);
            context.Caster.TeleportBy(context.Direction, distance);
            CombatFeedbackPool.SpawnMagicPulse(
                context.Caster.transform.position + Vector3.up,
                ThemeColor,
                1.3f,
                0.25f,
                0.3f);
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
            float teleportDistance,
            Color color)
        {
            base.ConfigureEditor(
                id,
                name,
                details,
                "Spatial Magic",
                cooldownSeconds,
                manaCost,
                castSeconds,
                recoverySeconds,
                AbilityMovementMode.Anchored,
                AbilityDodgeCancelMode.DodgeAllowed,
                AbilityAiIntent.Mobility,
                3.5f,
                20f,
                color);
            distance = teleportDistance;
        }
#endif
    }
}
