using UnityEngine;

namespace Turtle.Combat
{
    [CreateAssetMenu(
        menuName = "Turtle/Combat/Abilities/Projectile",
        fileName = "Projectile Ability")]
    public sealed class ProjectileAbilityDefinition : CombatAbilityDefinition
    {
        [Header("Projectile")]
        [SerializeField, Min(0f)] private float damage = 28f;
        [SerializeField, Min(0.1f)] private float speed = 18f;
        [SerializeField, Min(0.05f)] private float radius = 0.22f;
        [SerializeField, Min(0.1f)] private float lifetime = 2.5f;
        [SerializeField, Min(0f)] private float knockback = 1.2f;

        public override void Activate(in CombatAbilityContext context)
        {
            CombatAbilityProjectile.Launch(
                context.Caster,
                context.Direction,
                damage,
                speed,
                radius,
                lifetime,
                knockback,
                ThemeColor);
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
            float abilityDamage,
            float projectileSpeed,
            float projectileRadius,
            float projectileLifetime,
            float abilityKnockback,
            Color color)
        {
            base.ConfigureEditor(
                id,
                name,
                details,
                "Elemental Magic",
                cooldownSeconds,
                manaCost,
                castSeconds,
                recoverySeconds,
                AbilityMovementMode.Mobile,
                AbilityDodgeCancelMode.DodgeAllowed,
                AbilityAiIntent.Offensive,
                2f,
                projectileSpeed * projectileLifetime * 0.8f,
                color);
            damage = abilityDamage;
            speed = projectileSpeed;
            radius = projectileRadius;
            lifetime = projectileLifetime;
            knockback = abilityKnockback;
        }
#endif
    }
}
