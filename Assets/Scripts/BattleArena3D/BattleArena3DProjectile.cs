using UnityEngine;

namespace Turtle.BattleArena3D
{
    [DisallowMultipleComponent]
    public sealed class BattleArena3DProjectile : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly Collider[] HitBuffer = new Collider[12];

        [SerializeField] private Renderer projectileRenderer;
        [SerializeField] private TrailRenderer trail;

        private MaterialPropertyBlock propertyBlock;
        private BattleArena3DVfxPool ownerPool;
        private BattleArena3DUnit source;
        private BattleArena3DUnit target;
        private RaidProjectilePayload payload;
        private Vector3 velocity;
        private float expiresAt;
        private float radius;
        private bool active;

        public bool IsActive => active;

        public void Launch(
            BattleArena3DVfxPool pool,
            BattleArena3DUnit projectileSource,
            BattleArena3DUnit projectileTarget,
            Vector3 origin,
            float speed,
            float hitRadius,
            float lifetime,
            Color color,
            RaidProjectilePayload projectilePayload,
            float time)
        {
            propertyBlock ??= new MaterialPropertyBlock();
            ownerPool = pool;
            source = projectileSource;
            target = projectileTarget;
            payload = projectilePayload;
            radius = Mathf.Max(0.08f, hitRadius);
            expiresAt = time + Mathf.Max(0.2f, lifetime);
            transform.position = origin;
            var destination = ResolveTargetPoint();
            velocity = (destination - origin).normalized * Mathf.Max(1f, speed);
            transform.localScale = Vector3.one * radius * 2f;
            if (projectileRenderer != null)
            {
                projectileRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(BaseColorId, color);
                propertyBlock.SetColor("_EmissionColor", color * 3.5f);
                projectileRenderer.SetPropertyBlock(propertyBlock);
            }
            if (trail != null)
            {
                trail.Clear();
                trail.startColor = color;
                trail.endColor = new Color(color.r, color.g, color.b, 0f);
                trail.widthMultiplier = radius * 1.7f;
                trail.emitting = true;
            }
            active = true;
            gameObject.SetActive(true);
        }

        public void Step(float deltaTime, float time)
        {
            if (!active) return;
            if (time >= expiresAt || source == null || !source.isActiveAndEnabled)
            {
                Despawn(false);
                return;
            }

            var targetPoint = ResolveTargetPoint();
            var desired = targetPoint - transform.position;
            if (desired.sqrMagnitude > 0.01f)
            {
                var desiredVelocity = desired.normalized * velocity.magnitude;
                velocity = Vector3.RotateTowards(velocity, desiredVelocity, deltaTime * 2.8f, 0f);
            }
            var displacement = velocity * deltaTime;
            var distance = displacement.magnitude;
            var direction = distance > 0.0001f ? displacement / distance : velocity.normalized;
            var hitCount = Physics.OverlapCapsuleNonAlloc(
                transform.position,
                transform.position + direction * distance,
                radius,
                HitBuffer,
                Physics.AllLayers,
                QueryTriggerInteraction.Collide);
            BattleArena3DUnit hitUnit = null;
            Collider blockingCollider = null;
            var nearestSqr = float.MaxValue;
            for (var index = 0; index < hitCount; index++)
            {
                var collider = HitBuffer[index];
                HitBuffer[index] = null;
                if (collider == null || collider.transform.IsChildOf(transform)) continue;
                var unit = collider.GetComponentInParent<BattleArena3DUnit>();
                if (unit == source || (unit != null && unit.Faction == source.Faction)) continue;
                var closest = collider.ClosestPoint(transform.position);
                var sqr = (closest - transform.position).sqrMagnitude;
                if (sqr >= nearestSqr) continue;
                nearestSqr = sqr;
                hitUnit = unit;
                blockingCollider = collider;
            }

            if (blockingCollider != null)
            {
                if (hitUnit != null && hitUnit.CanReceiveDamage)
                {
                    ownerPool?.ResolveProjectileHit(source, hitUnit, payload, transform.position);
                }
                Despawn(true);
                return;
            }

            transform.position += displacement;
            if (target != null && target.CanReceiveDamage &&
                (ResolveTargetPoint() - transform.position).sqrMagnitude <= radius * radius * 2.25f)
            {
                ownerPool?.ResolveProjectileHit(source, target, payload, transform.position);
                Despawn(true);
            }
        }

        public void Despawn(bool impact)
        {
            if (!active) return;
            active = false;
            if (trail != null) trail.emitting = false;
            var position = transform.position;
            gameObject.SetActive(false);
            ownerPool?.ReturnProjectile(this, impact, position, payload.Color);
            source = null;
            target = null;
            ownerPool = null;
        }

        private Vector3 ResolveTargetPoint()
        {
            return target != null && target.CanReceiveDamage
                ? target.transform.position + Vector3.up * 0.9f
                : transform.position + velocity.normalized * 5f;
        }

#if UNITY_EDITOR
        public void ConfigureEditor(Renderer assignedRenderer, TrailRenderer assignedTrail)
        {
            projectileRenderer = assignedRenderer;
            trail = assignedTrail;
        }
#endif
    }

    public readonly struct RaidProjectilePayload
    {
        public RaidProjectilePayload(
            float damage,
            float splashRadius,
            float statusDuration,
            Turtle.DungeonRaid.RaidAbilityEffect effect,
            Color color,
            bool basicAttack,
            bool heavyImpact = false)
        {
            Damage = damage;
            SplashRadius = splashRadius;
            StatusDuration = statusDuration;
            Effect = effect;
            Color = color;
            BasicAttack = basicAttack;
            HeavyImpact = heavyImpact;
        }

        public float Damage { get; }
        public float SplashRadius { get; }
        public float StatusDuration { get; }
        public Turtle.DungeonRaid.RaidAbilityEffect Effect { get; }
        public Color Color { get; }
        public bool BasicAttack { get; }
        public bool HeavyImpact { get; }
    }
}
