using UnityEngine;
using Turtle.DungeonRaid;

namespace Turtle.BattleArena3D
{
    [DisallowMultipleComponent]
    public sealed class BattleArena3DUnitView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int IdleState = Animator.StringToHash("Base Layer.Idle");
        private static readonly int RunState = Animator.StringToHash("Base Layer.Run");
        private static readonly int AttackLightState = Animator.StringToHash("Base Layer.Attack Light");
        private static readonly int AttackHeavyState = Animator.StringToHash("Base Layer.Attack Heavy");
        private static readonly int HitState = Animator.StringToHash("Base Layer.Hit");
        private static readonly int DeathState = Animator.StringToHash("Base Layer.Death");

        [SerializeField] private Animator animator;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Transform weaponRoot;
        [SerializeField] private Transform healthBarRoot;
        [SerializeField] private Transform healthFill;
        [SerializeField] private Transform shieldFill;
        [SerializeField] private Renderer[] tintRenderers = System.Array.Empty<Renderer>();
        [SerializeField] private Renderer roleCrystal;
        [SerializeField] private bool animateProcedurally;

        [Header("Monster Silhouette")]
        [SerializeField] private Transform monsterBody;
        [SerializeField] private Transform monsterHead;
        [SerializeField] private Transform monsterLeftHorn;
        [SerializeField] private Transform monsterRightHorn;
        [SerializeField] private Transform monsterLeftClaw;
        [SerializeField] private Transform monsterRightClaw;
        [SerializeField] private Transform monsterCore;

        private MaterialPropertyBlock propertyBlock;
        private Color baseColor = Color.white;
        private float attackPulse;
        private float castPulse;
        private float hitPulse;
        private float locomotionBlend;
        private int locomotionState;
        private Vector3 visualBaseScale = Vector3.one;
        private Quaternion weaponBaseRotation = Quaternion.identity;
        private bool reducedMotion;
        private bool rendererPaletteValid;
        private Color renderedBaseColor;
        private Color renderedEmissionColor;
        private bool vitalsCacheValid;
        private float renderedHealthRatio = -1f;
        private float renderedShieldRatio = -1f;

        public bool IsConfigured => visualRoot != null && healthBarRoot != null && healthFill != null;
        public bool HasMonsterSilhouette => monsterBody != null && monsterHead != null &&
                                            monsterLeftClaw != null && monsterRightClaw != null &&
                                            monsterCore != null;

        private void Awake()
        {
            if (visualRoot != null) visualBaseScale = visualRoot.localScale;
            if (weaponRoot != null) weaponBaseRotation = weaponRoot.localRotation;
        }

        public void ApplyPalette(Color color)
        {
            EnsurePropertyBlock();
            baseColor = color;
            rendererPaletteValid = false;
            ApplyRendererPalette(
                Color.Lerp(Color.white, color, animateProcedurally ? 0.78f : 0.36f),
                0f,
                true);
            if (roleCrystal != null)
            {
                roleCrystal.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(BaseColorId, color);
                propertyBlock.SetColor(EmissionColorId, color * 2.8f);
                roleCrystal.SetPropertyBlock(propertyBlock);
            }
        }

        public void SetReducedMotion(bool value)
        {
            if (reducedMotion == value) return;
            reducedMotion = value;
            if (!reducedMotion || visualRoot == null) return;

            visualRoot.localPosition = Vector3.zero;
            visualRoot.localScale = visualBaseScale;
        }

        public void ApplyMonsterArchetype(BattleArenaMonsterArchetype3D archetype)
        {
            if (!HasMonsterSilhouette) return;

            switch (archetype)
            {
                case BattleArenaMonsterArchetype3D.Brute:
                    SetPart(monsterBody, true, new Vector3(0f, 0.88f, 0f),
                        new Vector3(0.98f, 0.95f, 0.92f), Quaternion.identity);
                    SetPart(monsterHead, true, new Vector3(0f, 1.7f, 0.08f), Vector3.one * 0.74f,
                        Quaternion.identity);
                    SetMirroredHorns(true, 0.36f, 1.98f, 0.08f, new Vector3(0.15f, 0.28f, 0.15f),
                        new Vector3(0f, 0f, 64f));
                    SetMirroredClaws(true, 0.66f, 0.91f, 0.18f, new Vector3(0.28f, 0.72f, 0.25f),
                        new Vector3(14f, 0f, 8f));
                    SetPart(monsterCore, true, new Vector3(0f, 1.34f, 0.48f), Vector3.one * 0.18f,
                        Quaternion.identity);
                    break;
                case BattleArenaMonsterArchetype3D.Spitter:
                    SetPart(monsterBody, true, new Vector3(0f, 0.7f, 0f),
                        new Vector3(0.74f, 0.68f, 0.84f), Quaternion.Euler(8f, 0f, 0f));
                    SetPart(monsterHead, true, new Vector3(0f, 1.42f, 0.3f), Vector3.one * 0.78f,
                        Quaternion.Euler(10f, 0f, 0f));
                    SetMirroredHorns(false, 0f, 0f, 0f, Vector3.one, Vector3.zero);
                    SetMirroredClaws(true, 0.43f, 0.73f, 0.25f, new Vector3(0.1f, 0.43f, 0.1f),
                        new Vector3(35f, 0f, 12f));
                    SetPart(monsterCore, true, new Vector3(0f, 1.08f, 0.52f), Vector3.one * 0.34f,
                        Quaternion.identity);
                    break;
                case BattleArenaMonsterArchetype3D.Hexer:
                    SetPart(monsterBody, true, new Vector3(0f, 0.96f, 0f),
                        new Vector3(0.56f, 1.08f, 0.56f), Quaternion.identity);
                    SetPart(monsterHead, true, new Vector3(0f, 1.94f, 0.04f), Vector3.one * 0.64f,
                        Quaternion.identity);
                    SetMirroredHorns(true, 0.22f, 2.3f, 0.03f, new Vector3(0.08f, 0.56f, 0.08f),
                        new Vector3(0f, 0f, 10f));
                    SetMirroredClaws(true, 0.46f, 1.02f, 0.18f, new Vector3(0.08f, 0.64f, 0.08f),
                        new Vector3(2f, 0f, 5f));
                    SetPart(monsterCore, true, new Vector3(0f, 1.5f, 0.39f), Vector3.one * 0.32f,
                        Quaternion.identity);
                    break;
                case BattleArenaMonsterArchetype3D.Charger:
                    SetPart(monsterBody, true, new Vector3(0f, 0.79f, 0f),
                        new Vector3(0.82f, 0.78f, 0.86f), Quaternion.Euler(8f, 0f, 0f));
                    SetPart(monsterHead, true, new Vector3(0f, 1.45f, 0.34f), Vector3.one * 0.66f,
                        Quaternion.Euler(15f, 0f, 0f));
                    SetMirroredHorns(true, 0.25f, 1.6f, 0.5f, new Vector3(0.1f, 0.54f, 0.1f),
                        new Vector3(70f, 0f, 9f));
                    SetMirroredClaws(true, 0.56f, 0.78f, 0.28f, new Vector3(0.16f, 0.58f, 0.16f),
                        new Vector3(42f, 0f, 9f));
                    SetPart(monsterCore, true, new Vector3(0f, 1.18f, 0.45f), Vector3.one * 0.16f,
                        Quaternion.identity);
                    break;
                case BattleArenaMonsterArchetype3D.Elite:
                    SetPart(monsterBody, true, new Vector3(0f, 0.98f, 0f),
                        new Vector3(1.02f, 1.08f, 0.96f), Quaternion.identity);
                    SetPart(monsterHead, true, new Vector3(0f, 1.93f, 0.08f), Vector3.one * 0.8f,
                        Quaternion.identity);
                    SetMirroredHorns(true, 0.34f, 2.38f, 0.05f, new Vector3(0.16f, 0.58f, 0.16f),
                        new Vector3(0f, 0f, 24f));
                    SetMirroredClaws(true, 0.75f, 1.04f, 0.28f, new Vector3(0.24f, 1.02f, 0.22f),
                        new Vector3(20f, 0f, 8f));
                    SetPart(monsterCore, true, new Vector3(0f, 1.52f, 0.55f), Vector3.one * 0.36f,
                        Quaternion.identity);
                    break;
                default:
                    SetPart(monsterBody, true, new Vector3(0f, 0.86f, 0f),
                        new Vector3(0.62f, 0.92f, 0.62f), Quaternion.identity);
                    SetPart(monsterHead, true, new Vector3(0f, 1.64f, 0.1f), Vector3.one * 0.54f,
                        Quaternion.identity);
                    SetMirroredHorns(true, 0.22f, 1.93f, 0.06f, new Vector3(0.08f, 0.26f, 0.08f),
                        new Vector3(0f, 0f, 34f));
                    SetMirroredClaws(true, 0.54f, 0.91f, 0.28f, new Vector3(0.13f, 0.92f, 0.13f),
                        new Vector3(18f, 0f, 10f));
                    SetPart(monsterCore, true, new Vector3(0f, 1.33f, 0.37f), Vector3.one * 0.15f,
                        Quaternion.identity);
                    break;
            }

            if (weaponRoot != null) weaponBaseRotation = weaponRoot.localRotation;
        }

        public void TickPresentation(float deltaTime, float normalizedSpeed, bool actionLocked)
        {
            attackPulse = Mathf.MoveTowards(attackPulse, 0f, deltaTime * 5.5f);
            castPulse = Mathf.MoveTowards(castPulse, 0f, deltaTime * 4f);
            hitPulse = Mathf.MoveTowards(hitPulse, 0f, deltaTime * 6.5f);
            locomotionBlend = Mathf.MoveTowards(locomotionBlend, normalizedSpeed, deltaTime * 5f);

            if (animator != null && !actionLocked)
            {
                var desired = normalizedSpeed > 0.12f ? RunState : IdleState;
                if (desired != locomotionState)
                {
                    locomotionState = desired;
                    animator.CrossFade(desired, 0.14f);
                }
            }

            if (animateProcedurally && visualRoot != null)
            {
                var bob = reducedMotion
                    ? 0f
                    : Mathf.Sin(Time.time * (6f + normalizedSpeed * 3f) + transform.GetInstanceID()) *
                      (0.025f + normalizedSpeed * 0.035f);
                var pulse = attackPulse * 0.18f + castPulse * 0.12f - hitPulse * 0.1f;
                var motionMultiplier = reducedMotion ? 0.25f : 1f;
                visualRoot.localScale = visualBaseScale * (1f + pulse * motionMultiplier);
                visualRoot.localPosition = new Vector3(0f, bob, attackPulse * 0.12f * motionMultiplier);
                if (weaponRoot != null)
                {
                    weaponRoot.localRotation = weaponBaseRotation *
                                               Quaternion.Euler(
                                                   attackPulse * -105f * (reducedMotion ? 0.68f : 1f),
                                                   0f,
                                                   attackPulse * 25f * (reducedMotion ? 0.68f : 1f));
                }
            }

            var presentationColor = Color.Lerp(Color.white, baseColor, animateProcedurally ? 0.78f : 0.36f);
            ApplyRendererPalette(hitPulse > 0f
                    ? Color.Lerp(presentationColor, Color.white, hitPulse)
                    : presentationColor,
                castPulse);
        }

        public void UpdateVitals(float healthRatio, float shieldRatio, bool show)
        {
            if (healthBarRoot == null) return;
            if (healthBarRoot.gameObject.activeSelf != show) healthBarRoot.gameObject.SetActive(show);
            if (!show) return;
            healthRatio = Mathf.Clamp01(healthRatio);
            shieldRatio = Mathf.Clamp01(shieldRatio);
            if (healthFill != null &&
                (!vitalsCacheValid || Mathf.Abs(renderedHealthRatio - healthRatio) > 0.002f))
            {
                var scale = healthFill.localScale;
                scale.x = Mathf.Max(0.001f, healthRatio);
                healthFill.localScale = scale;
                healthFill.localPosition = new Vector3((scale.x - 1f) * 0.5f, healthFill.localPosition.y,
                    healthFill.localPosition.z);
            }
            if (shieldFill != null &&
                (!vitalsCacheValid || Mathf.Abs(renderedShieldRatio - shieldRatio) > 0.002f))
            {
                var scale = shieldFill.localScale;
                scale.x = Mathf.Max(0.001f, shieldRatio);
                shieldFill.localScale = scale;
                shieldFill.localPosition = new Vector3((scale.x - 1f) * 0.5f, shieldFill.localPosition.y,
                    shieldFill.localPosition.z);
                shieldFill.gameObject.SetActive(shieldRatio > 0.001f);
            }
            renderedHealthRatio = healthRatio;
            renderedShieldRatio = shieldRatio;
            vitalsCacheValid = true;
        }

        public void FaceHealthBar(Camera camera)
        {
            if (healthBarRoot == null || camera == null || !healthBarRoot.gameObject.activeSelf) return;
            healthBarRoot.rotation = Quaternion.LookRotation(
                healthBarRoot.position - camera.transform.position,
                camera.transform.up);
        }

        public void PlayAttack(bool heavy)
        {
            attackPulse = 1f;
            if (animator == null) return;
            locomotionState = 0;
            animator.CrossFade(heavy ? AttackHeavyState : AttackLightState, 0.07f);
        }

        public void PlayCast()
        {
            castPulse = 1f;
            if (animator == null) return;
            locomotionState = 0;
            animator.CrossFade(AttackHeavyState, 0.1f);
        }

        public void PlayHit()
        {
            hitPulse = 1f;
            if (animator == null) return;
            locomotionState = 0;
            animator.CrossFade(HitState, 0.04f);
        }

        public void PlayDeath()
        {
            if (animator != null)
            {
                locomotionState = DeathState;
                animator.CrossFade(DeathState, 0.1f);
            }
            else if (visualRoot != null)
            {
                visualRoot.localRotation = Quaternion.Euler(0f, 0f, 78f);
            }
        }

        public void PlayRevive()
        {
            locomotionState = 0;
            if (visualRoot != null) visualRoot.localRotation = Quaternion.identity;
            if (animator != null) animator.CrossFade(IdleState, 0.15f);
        }

        public void ResetView()
        {
            attackPulse = castPulse = hitPulse = locomotionBlend = 0f;
            locomotionState = IdleState;
            vitalsCacheValid = false;
            rendererPaletteValid = false;
            if (visualRoot != null)
            {
                visualRoot.localRotation = Quaternion.identity;
                visualRoot.localPosition = Vector3.zero;
                visualRoot.localScale = visualBaseScale;
            }
            if (weaponRoot != null) weaponRoot.localRotation = weaponBaseRotation;
            if (animator != null) animator.Play(IdleState, 0, 0f);
            ApplyPalette(baseColor);
        }

        private void ApplyRendererPalette(Color color, float emissionPulse, bool force = false)
        {
            EnsurePropertyBlock();
            var quantizedColor = QuantizeColor(color, 16f);
            var quantizedEmission = Mathf.Round(Mathf.Clamp01(emissionPulse) * 8f) / 8f;
            var emissionColor = quantizedEmission > 0f
                ? QuantizeColor(baseColor * (1.8f + quantizedEmission * 2.8f), 16f)
                : Color.black;
            if (!force && rendererPaletteValid && renderedBaseColor == quantizedColor &&
                renderedEmissionColor == emissionColor)
            {
                return;
            }

            for (var index = 0; index < tintRenderers.Length; index++)
            {
                var renderer = tintRenderers[index];
                if (renderer == null) continue;
                renderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(BaseColorId, quantizedColor);
                propertyBlock.SetColor(EmissionColorId, emissionColor);
                renderer.SetPropertyBlock(propertyBlock);
            }
            renderedBaseColor = quantizedColor;
            renderedEmissionColor = emissionColor;
            rendererPaletteValid = true;
        }

        private static Color QuantizeColor(Color color, float steps)
        {
            return new Color(
                Mathf.Round(color.r * steps) / steps,
                Mathf.Round(color.g * steps) / steps,
                Mathf.Round(color.b * steps) / steps,
                Mathf.Round(color.a * steps) / steps);
        }

        private void EnsurePropertyBlock()
        {
            propertyBlock ??= new MaterialPropertyBlock();
        }

        private void SetMirroredHorns(
            bool active,
            float horizontal,
            float height,
            float depth,
            Vector3 scale,
            Vector3 rotation)
        {
            SetPart(monsterLeftHorn, active, new Vector3(-horizontal, height, depth), scale,
                Quaternion.Euler(rotation.x, rotation.y, rotation.z));
            SetPart(monsterRightHorn, active, new Vector3(horizontal, height, depth), scale,
                Quaternion.Euler(rotation.x, -rotation.y, -rotation.z));
        }

        private void SetMirroredClaws(
            bool active,
            float horizontal,
            float height,
            float depth,
            Vector3 scale,
            Vector3 rotation)
        {
            SetPart(monsterLeftClaw, active, new Vector3(-horizontal, height, depth), scale,
                Quaternion.Euler(rotation.x, rotation.y, rotation.z));
            SetPart(monsterRightClaw, active, new Vector3(horizontal, height, depth), scale,
                Quaternion.Euler(rotation.x, -rotation.y, -rotation.z));
        }

        private static void SetPart(
            Transform part,
            bool active,
            Vector3 localPosition,
            Vector3 localScale,
            Quaternion localRotation)
        {
            if (part == null) return;
            part.gameObject.SetActive(active);
            if (!active) return;
            part.localPosition = localPosition;
            part.localScale = localScale;
            part.localRotation = localRotation;
        }

#if UNITY_EDITOR
        public void ConfigureEditor(
            Animator assignedAnimator,
            Transform assignedVisualRoot,
            Transform assignedWeaponRoot,
            Transform assignedHealthBar,
            Transform assignedHealthFill,
            Transform assignedShieldFill,
            Renderer[] assignedTintRenderers,
            Renderer assignedRoleCrystal,
            bool proceduralAnimation,
            Transform assignedMonsterBody = null,
            Transform assignedMonsterHead = null,
            Transform assignedMonsterLeftHorn = null,
            Transform assignedMonsterRightHorn = null,
            Transform assignedMonsterLeftClaw = null,
            Transform assignedMonsterRightClaw = null,
            Transform assignedMonsterCore = null)
        {
            animator = assignedAnimator;
            visualRoot = assignedVisualRoot;
            weaponRoot = assignedWeaponRoot;
            healthBarRoot = assignedHealthBar;
            healthFill = assignedHealthFill;
            shieldFill = assignedShieldFill;
            tintRenderers = assignedTintRenderers ?? System.Array.Empty<Renderer>();
            roleCrystal = assignedRoleCrystal;
            animateProcedurally = proceduralAnimation;
            monsterBody = assignedMonsterBody;
            monsterHead = assignedMonsterHead;
            monsterLeftHorn = assignedMonsterLeftHorn;
            monsterRightHorn = assignedMonsterRightHorn;
            monsterLeftClaw = assignedMonsterLeftClaw;
            monsterRightClaw = assignedMonsterRightClaw;
            monsterCore = assignedMonsterCore;
            visualBaseScale = visualRoot != null ? visualRoot.localScale : Vector3.one;
            weaponBaseRotation = weaponRoot != null ? weaponRoot.localRotation : Quaternion.identity;
        }
#endif
    }
}
