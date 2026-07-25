using System.Collections.Generic;
using UnityEngine;

namespace Turtle.Combat
{
    [DisallowMultipleComponent]
    public sealed class CombatAbilityProjectile : MonoBehaviour
    {
        private const int QueryCapacity = 24;
        private static readonly Queue<CombatAbilityProjectile> Pool = new();

        private readonly RaycastHit[] hitBuffer = new RaycastHit[QueryCapacity];
        private Combatant caster;
        private Renderer visualRenderer;
        private TrailRenderer trail;
        private MaterialPropertyBlock colorBlock;
        private Color impactColor;
        private Vector3 direction;
        private float damage;
        private float speed;
        private float radius;
        private float knockback;
        private float expiresAt;

        public static void Launch(
            Combatant source,
            Vector3 launchDirection,
            float abilityDamage,
            float projectileSpeed,
            float projectileRadius,
            float lifetime,
            float abilityKnockback,
            Color color)
        {
            if (source == null)
            {
                return;
            }

            var projectile = Pool.Count > 0 ? Pool.Dequeue() : Create();
            projectile.Play(
                source,
                launchDirection,
                abilityDamage,
                projectileSpeed,
                projectileRadius,
                lifetime,
                abilityKnockback,
                color);
        }

        private static CombatAbilityProjectile Create()
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            root.name = "Pooled Ability Projectile";
            var collider = root.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            var projectile = root.AddComponent<CombatAbilityProjectile>();
            projectile.visualRenderer = root.GetComponent<Renderer>();
            projectile.visualRenderer.sharedMaterial = CombatFeedbackPool.MagicMaterial;
            projectile.colorBlock = new MaterialPropertyBlock();
            projectile.trail = root.AddComponent<TrailRenderer>();
            projectile.trail.sharedMaterial = CombatFeedbackPool.MagicMaterial;
            projectile.trail.time = 0.22f;
            projectile.trail.startWidth = 0.18f;
            projectile.trail.endWidth = 0.01f;
            projectile.trail.minVertexDistance = 0.05f;
            root.SetActive(false);
            return projectile;
        }

        private void Play(
            Combatant source,
            Vector3 launchDirection,
            float abilityDamage,
            float projectileSpeed,
            float projectileRadius,
            float lifetime,
            float abilityKnockback,
            Color color)
        {
            caster = source;
            direction = Vector3.ProjectOnPlane(launchDirection, Vector3.up).normalized;
            if (direction.sqrMagnitude < 0.001f)
            {
                direction = caster.transform.forward;
            }
            damage = abilityDamage;
            speed = projectileSpeed;
            radius = projectileRadius;
            knockback = abilityKnockback;
            expiresAt = Time.time + lifetime;
            impactColor = color;
            transform.position = caster.transform.position + Vector3.up * 1.15f + direction * 0.7f;
            transform.localScale = Vector3.one * radius * 2f;
            ApplyColor(color);
            gameObject.SetActive(true);
            trail.Clear();
        }

        private void Update()
        {
            if (caster == null || !caster.IsAlive || Time.time >= expiresAt)
            {
                ReturnToPool();
                return;
            }

            var distance = speed * Time.deltaTime;
            var hitCount = Physics.SphereCastNonAlloc(
                transform.position,
                radius,
                direction,
                hitBuffer,
                distance,
                ~0,
                QueryTriggerInteraction.Collide);
            var nearestDistance = float.PositiveInfinity;
            Collider nearestCollider = null;
            for (var index = 0; index < hitCount; index++)
            {
                var candidate = hitBuffer[index].collider;
                if (candidate == null ||
                    candidate.transform.root == caster.transform.root ||
                    hitBuffer[index].distance >= nearestDistance)
                {
                    continue;
                }
                nearestDistance = hitBuffer[index].distance;
                nearestCollider = candidate;
            }

            if (nearestCollider != null)
            {
                var hurtbox = nearestCollider.GetComponent<CombatHurtbox>();
                var target = hurtbox != null
                    ? hurtbox.Owner
                    : nearestCollider.GetComponentInParent<Combatant>();
                if (target != null && caster.CanTarget(target))
                {
                    var controller = caster.Abilities;
                    var finalDamage = controller != null
                        ? controller.ModifyAbilityDamage(damage)
                        : damage;
                    if (target.TryReceiveAbilityDamage(
                            caster,
                            finalDamage,
                            knockback,
                            direction))
                    {
                        caster.NotifyAttackConnected(target, finalDamage);
                    }
                }
                CombatFeedbackPool.SpawnMagicPulse(
                    transform.position + direction * nearestDistance,
                    impactColor,
                    radius,
                    radius * 4f,
                    0.2f);
                ReturnToPool();
                return;
            }

            transform.position += direction * distance;
        }

        private void ApplyColor(Color color)
        {
            visualRenderer.GetPropertyBlock(colorBlock);
            colorBlock.SetColor("_BaseColor", color);
            colorBlock.SetColor("_EmissionColor", color * 3f);
            visualRenderer.SetPropertyBlock(colorBlock);
            trail.startColor = color;
            trail.endColor = new Color(color.r, color.g, color.b, 0f);
        }

        private void ReturnToPool()
        {
            trail.Clear();
            gameObject.SetActive(false);
            caster = null;
            Pool.Enqueue(this);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetPool()
        {
            Pool.Clear();
        }
    }
}
