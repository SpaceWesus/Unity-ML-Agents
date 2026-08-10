using System.Collections.Generic;
using UnityEngine;

namespace Turtle.BattleArena3D
{
    [DisallowMultipleComponent]
    public sealed class BattleArena3DVfxPool : MonoBehaviour
    {
        [SerializeField] private ParticleSystem impactParticles;
        [SerializeField] private ParticleSystem magicParticles;
        [SerializeField] private ParticleSystem healingParticles;
        [SerializeField] private ParticleSystem bloodParticles;
        [SerializeField] private ParticleSystem shieldParticles;
        [SerializeField] private ParticleSystem slashParticles;
        [SerializeField] private ParticleSystem deathParticles;
        [SerializeField] private BattleArena3DProjectile projectileTemplate;
        [SerializeField] private Transform projectileRoot;
        [SerializeField, Range(32, 320)] private int projectileCapacity = 160;
        [SerializeField] private BattleArena3DTelegraph telegraphTemplate;
        [SerializeField] private Transform telegraphRoot;
        [SerializeField, Range(16, 128)] private int telegraphCapacity = 64;

        private readonly List<BattleArena3DProjectile> allProjectiles = new(192);
        private readonly Queue<BattleArena3DProjectile> availableProjectiles = new(192);
        private readonly List<BattleArena3DTelegraph> allTelegraphs = new(80);
        private readonly Queue<BattleArena3DTelegraph> availableTelegraphs = new(80);
        private BattleArena3DDirector director;
        private float particleDensity = 1f;
        private bool reducedMotion;

        public bool IsConfigured => impactParticles != null && magicParticles != null &&
                                    healingParticles != null && bloodParticles != null &&
                                    shieldParticles != null && slashParticles != null &&
                                    deathParticles != null && projectileTemplate != null &&
                                    projectileRoot != null && telegraphTemplate != null &&
                                    telegraphRoot != null;
        public int ActiveProjectileCount => allProjectiles.Count - availableProjectiles.Count;
        public int ActiveTelegraphCount => allTelegraphs.Count - availableTelegraphs.Count;
        public int TelegraphEmissionCount { get; private set; }
        public int DroppedTelegraphCount { get; private set; }

        public void Initialize(BattleArena3DDirector battleDirector)
        {
            director = battleDirector;
            if (projectileTemplate != null) projectileTemplate.gameObject.SetActive(false);
            if (telegraphTemplate != null) telegraphTemplate.gameObject.SetActive(false);
            EnsureProjectileCapacity(projectileCapacity);
            EnsureTelegraphCapacity(telegraphCapacity);
        }

        public void TickProjectiles(float deltaTime, float time)
        {
            for (var index = 0; index < allProjectiles.Count; index++)
            {
                var projectile = allProjectiles[index];
                if (projectile != null && projectile.IsActive) projectile.Step(deltaTime, time);
            }
            for (var index = 0; index < allTelegraphs.Count; index++)
            {
                var telegraph = allTelegraphs[index];
                if (telegraph != null && telegraph.IsActive) telegraph.Step(time);
            }
        }

        public void ResetBattleEffects()
        {
            for (var index = 0; index < allProjectiles.Count; index++)
            {
                var projectile = allProjectiles[index];
                if (projectile != null && projectile.IsActive) projectile.Despawn(false);
            }
            for (var index = 0; index < allTelegraphs.Count; index++)
            {
                var telegraph = allTelegraphs[index];
                if (telegraph != null && telegraph.IsActive) telegraph.Despawn();
            }
            impactParticles?.Clear(true);
            magicParticles?.Clear(true);
            healingParticles?.Clear(true);
            bloodParticles?.Clear(true);
            shieldParticles?.Clear(true);
            slashParticles?.Clear(true);
            deathParticles?.Clear(true);
            TelegraphEmissionCount = 0;
            DroppedTelegraphCount = 0;
        }

        public bool LaunchProjectile(
            BattleArena3DUnit source,
            BattleArena3DUnit target,
            float speed,
            float radius,
            Color color,
            RaidProjectilePayload payload,
            float time)
        {
            if (source == null || target == null || availableProjectiles.Count == 0) return false;
            var projectile = availableProjectiles.Dequeue();
            projectile.Launch(
                this,
                source,
                target,
                source.transform.position + Vector3.up * 1.15f + source.transform.forward * 0.45f,
                speed,
                radius,
                5f,
                color,
                payload,
                time);
            return true;
        }

        public void ResolveProjectileHit(
            BattleArena3DUnit source,
            BattleArena3DUnit target,
            RaidProjectilePayload payload,
            Vector3 position)
        {
            director?.ResolveProjectileContact(source, target, payload, position);
        }

        public void ReturnProjectile(
            BattleArena3DProjectile projectile,
            bool impact,
            Vector3 position,
            Color color)
        {
            if (projectile == null) return;
            availableProjectiles.Enqueue(projectile);
            if (impact) EmitImpact(position, color, 10);
        }

        public void EmitImpact(Vector3 position, Color color, int count = 14)
        {
            Emit(impactParticles, position, color, ScaleParticleCount(count), 0.16f);
        }

        public void EmitMagic(Vector3 position, Color color, int count = 20)
        {
            Emit(magicParticles, position, color, ScaleParticleCount(count), 0.22f);
        }

        public void EmitHealing(Vector3 position, Color color, int count = 22)
        {
            Emit(healingParticles, position, color, ScaleParticleCount(count), 0.19f);
        }

        public void EmitBlood(Vector3 position, Vector3 direction, int count = 12)
        {
            if (bloodParticles == null) return;
            count = ScaleParticleCount(count);
            var parameters = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = (direction.normalized * 2.3f + Vector3.up * 1.1f) *
                           (reducedMotion ? 0.55f : 1f),
                startColor = new Color(0.65f, 0.015f, 0.01f, 1f),
                startSize = 0.12f
            };
            bloodParticles.Emit(parameters, Mathf.Clamp(count, 1, 40));
        }

        public void EmitShield(Vector3 position, Color color, int count = 28)
        {
            Emit(shieldParticles, position, color, ScaleParticleCount(count), 0.24f);
        }

        public void EmitSlash(Vector3 position, Vector3 direction, Color color, int count = 18)
        {
            if (slashParticles == null) return;
            var resolvedDirection = direction.sqrMagnitude > 0.001f
                ? direction.normalized
                : Vector3.forward;
            var parameters = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = (resolvedDirection * 1.8f + Vector3.up * 0.32f) *
                           (reducedMotion ? 0.6f : 1f),
                startColor = color,
                startSize = reducedMotion ? 0.22f : 0.32f
            };
            slashParticles.Emit(parameters, Mathf.Clamp(ScaleParticleCount(count), 1, 48));
        }

        public void EmitDeath(Vector3 position, Color color, bool elite)
        {
            if (deathParticles == null) return;
            var count = Mathf.Clamp(ScaleParticleCount(elite ? 40 : 24), 1, 64);
            const int lobeCount = 8;
            var emittedLobes = Mathf.Min(lobeCount, count);
            for (var lobe = 0; lobe < emittedLobes; lobe++)
            {
                var radians = lobe / (float)lobeCount * Mathf.PI * 2f;
                var radial = new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians));
                var parameters = new ParticleSystem.EmitParams
                {
                    position = position,
                    velocity = (radial * (elite ? 2.9f : 2.15f) +
                                Vector3.up * (elite ? 2.4f : 1.7f)) *
                               (reducedMotion ? 0.55f : 1f),
                    startColor = color,
                    startSize = elite ? 0.42f : 0.3f
                };
                deathParticles.Emit(parameters,
                    (count + lobeCount - 1 - lobe) / lobeCount);
            }
        }

        public bool EmitTelegraph(
            Vector3 position,
            float radius,
            Color color,
            float time,
            float duration = 0.62f)
        {
            if (availableTelegraphs.Count == 0)
            {
                DroppedTelegraphCount++;
                return false;
            }
            var telegraph = availableTelegraphs.Dequeue();
            telegraph.Play(this, position, radius, color, time, duration, reducedMotion);
            TelegraphEmissionCount++;
            return true;
        }

        public void ApplyPresentationOptions(BattleArenaPresentationOptions3D options)
        {
            particleDensity = options.EffectsLevel switch
            {
                BattleArenaEffectsLevel3D.Minimal => 0.28f,
                BattleArenaEffectsLevel3D.Reduced => 0.58f,
                _ => 1f
            };
            reducedMotion = options.ReducedMotion ||
                            options.EffectsLevel == BattleArenaEffectsLevel3D.Minimal;
            for (var index = 0; index < allTelegraphs.Count; index++)
            {
                var telegraph = allTelegraphs[index];
                if (telegraph != null && telegraph.IsActive)
                {
                    telegraph.ApplyReducedMotion(reducedMotion);
                }
            }
        }

        public void ReturnTelegraph(BattleArena3DTelegraph telegraph)
        {
            if (telegraph == null) return;
            availableTelegraphs.Enqueue(telegraph);
        }

        private void EnsureProjectileCapacity(int capacity)
        {
            if (projectileTemplate == null || projectileRoot == null) return;
            while (allProjectiles.Count < capacity)
            {
                var clone = Instantiate(projectileTemplate, projectileRoot);
                clone.name = $"Pooled Projectile {allProjectiles.Count + 1:000}";
                clone.gameObject.SetActive(false);
                allProjectiles.Add(clone);
                availableProjectiles.Enqueue(clone);
            }
        }

        private void EnsureTelegraphCapacity(int capacity)
        {
            if (telegraphTemplate == null || telegraphRoot == null) return;
            while (allTelegraphs.Count < capacity)
            {
                var clone = Instantiate(telegraphTemplate, telegraphRoot);
                clone.name = $"Pooled Telegraph {allTelegraphs.Count + 1:000}";
                clone.gameObject.SetActive(false);
                allTelegraphs.Add(clone);
                availableTelegraphs.Enqueue(clone);
            }
        }

        private static void Emit(
            ParticleSystem system,
            Vector3 position,
            Color color,
            int count,
            float size)
        {
            if (system == null) return;
            var parameters = new ParticleSystem.EmitParams
            {
                position = position,
                startColor = color,
                startSize = size
            };
            system.Emit(parameters, Mathf.Clamp(count, 1, 64));
        }

        private int ScaleParticleCount(int count)
        {
            return Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(1, count) * particleDensity));
        }

#if UNITY_EDITOR
        public void ConfigureEditor(
            ParticleSystem assignedImpact,
            ParticleSystem assignedMagic,
            ParticleSystem assignedHealing,
            ParticleSystem assignedBlood,
            ParticleSystem assignedShield,
            ParticleSystem assignedSlash,
            ParticleSystem assignedDeath,
            BattleArena3DProjectile assignedProjectileTemplate,
            Transform assignedProjectileRoot,
            int capacity,
            BattleArena3DTelegraph assignedTelegraphTemplate,
            Transform assignedTelegraphRoot,
            int assignedTelegraphCapacity)
        {
            impactParticles = assignedImpact;
            magicParticles = assignedMagic;
            healingParticles = assignedHealing;
            bloodParticles = assignedBlood;
            shieldParticles = assignedShield;
            slashParticles = assignedSlash;
            deathParticles = assignedDeath;
            projectileTemplate = assignedProjectileTemplate;
            projectileRoot = assignedProjectileRoot;
            projectileCapacity = Mathf.Clamp(capacity, 32, 320);
            telegraphTemplate = assignedTelegraphTemplate;
            telegraphRoot = assignedTelegraphRoot;
            telegraphCapacity = Mathf.Clamp(assignedTelegraphCapacity, 16, 128);
        }
#endif
    }
}
