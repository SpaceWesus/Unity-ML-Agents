using UnityEngine;

namespace Turtle.Combat
{
    [CreateAssetMenu(
        menuName = "Turtle/Combat/Abilities/Area",
        fileName = "Area Ability")]
    public sealed class AreaAbilityDefinition : CombatAbilityDefinition
    {
        [Header("Area Effect")]
        [SerializeField, Min(0f)] private float damage = 48f;
        [SerializeField, Min(0.1f)] private float radius = 5f;
        [SerializeField, Min(0f)] private float forwardOffset = 2f;
        [SerializeField, Min(0f)] private float knockback = 2.5f;

        public override void Activate(in CombatAbilityContext context)
        {
            if (context.Caster == null)
            {
                return;
            }

            var center = context.Caster.transform.position + context.Direction * forwardOffset;
            CombatFeedbackPool.SpawnMagicPulse(
                center + Vector3.up * 0.15f,
                ThemeColor,
                0.5f,
                radius * 2f,
                0.55f);

            var candidates = CombatantRegistry.All;
            var radiusSquared = radius * radius;
            var finalDamage = context.Controller != null
                ? context.Controller.ModifyAbilityDamage(damage)
                : damage;
            for (var index = 0; index < candidates.Count; index++)
            {
                var target = candidates[index];
                if (!context.Caster.CanTarget(target))
                {
                    continue;
                }

                var offset = Vector3.ProjectOnPlane(
                    target.transform.position - center,
                    Vector3.up);
                if (offset.sqrMagnitude > radiusSquared)
                {
                    continue;
                }

                var hitDirection = offset.sqrMagnitude > 0.001f
                    ? offset.normalized
                    : context.Direction;
                if (target.TryReceiveAbilityDamage(
                        context.Caster,
                        finalDamage,
                        knockback,
                        hitDirection))
                {
                    context.Caster.NotifyAttackConnected(target, finalDamage);
                }
            }
        }

#if UNITY_EDITOR
        public void ConfigureEditor(
            string id,
            string name,
            string details,
            float castSeconds,
            float recoverySeconds,
            float abilityDamage,
            float abilityRadius,
            float offset,
            float abilityKnockback,
            Color color)
        {
            base.ConfigureEditor(
                id,
                name,
                details,
                "Elemental Magic",
                0f,
                0f,
                castSeconds,
                recoverySeconds,
                AbilityMovementMode.Anchored,
                AbilityDodgeCancelMode.DodgeAllowed,
                AbilityAiIntent.Offensive,
                0f,
                abilityRadius + offset,
                color);
            damage = abilityDamage;
            radius = abilityRadius;
            forwardOffset = offset;
            knockback = abilityKnockback;
        }
#endif
    }
}
