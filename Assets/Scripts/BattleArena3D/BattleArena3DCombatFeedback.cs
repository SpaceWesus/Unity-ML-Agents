using Turtle.DungeonRaid;
using UnityEngine;

namespace Turtle.BattleArena3D
{
    /// <summary>
    /// Owns bounded, presentation-only combat feedback for the arena. Combat simulation
    /// calls one entry point; this component decides how much of that event to render.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleArena3DCombatFeedback : MonoBehaviour
    {
        private const int DamageLabelCapacity = 48;
        private const int CachedNumberMaximum = 2000;
        private const float AggregationWindow = 0.2f;
        private const float LabelLifetime = 0.9f;
        private const string DefeatedText = "DEFEATED";
        private const string DownText = "DOWN";
        private const string BreakText = "SHIELD BREAK";
        private static readonly string[] DamageNumberText = BuildNumberCache("-");
        private static readonly string[] ShieldNumberText = BuildNumberCache("SHIELD ");

        private sealed class DamageLabelSlot
        {
            public BattleArena3DUnit Target;
            public Vector3 WorldPosition;
            public float Damage;
            public float Shield;
            public float StartedAt;
            public float LastHitAt;
            public float ExpiresAt;
            public bool Downed;
            public bool Died;
            public bool ShieldBroken;
            public readonly GUIContent DamageContent = new();
            public readonly GUIContent ShieldContent = new();
            public readonly GUIContent StatusContent = new();
            public bool Active;
        }

        [Header("Simulation references")]
        [SerializeField] private BattleArena3DDirector director;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private BattleArena3DCameraRig cameraRig;
        [SerializeField] private BattleArena3DVfxPool vfxPool;

        [Header("One shared selection marker")]
        [SerializeField] private Transform selectionMarkerRoot;
        [SerializeField] private LineRenderer selectionOuter;
        [SerializeField] private LineRenderer selectionInner;

        private readonly DamageLabelSlot[] damageLabels = new DamageLabelSlot[DamageLabelCapacity];
        private BattleArenaPresentationOptions3D options;
        private GUIStyle damageStyle;
        private GUIStyle shadowStyle;
        private BattleArena3DUnit markedUnit;
        private Color markerColor;
        private float nextCameraImpulseAt;
        private bool presentationApplied;

        public bool IsConfigured => director != null && worldCamera != null && cameraRig != null &&
                                    vfxPool != null && selectionMarkerRoot != null &&
                                    selectionOuter != null && selectionInner != null;
        public int FeedbackEventCount { get; private set; }
        public int CameraImpulseCount { get; private set; }
        public int DamageLabelEmissionCount { get; private set; }
        public int DeathBurstCount { get; private set; }
        public int ShieldContactCount { get; private set; }
        public bool SelectionMarkerActive => selectionMarkerRoot != null &&
                                             selectionMarkerRoot.gameObject.activeSelf;

        private void Awake()
        {
            for (var index = 0; index < damageLabels.Length; index++)
            {
                damageLabels[index] = new DamageLabelSlot();
            }
            if (!presentationApplied) options = BattleArenaPresentationOptions3D.Default;
            if (selectionMarkerRoot != null) selectionMarkerRoot.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            UpdateSelectionMarker();
        }

        private void OnDisable()
        {
            if (selectionMarkerRoot != null) selectionMarkerRoot.gameObject.SetActive(false);
        }

        private void OnGUI()
        {
            if (worldCamera == null || options.DamageNumbers == BattleArenaDamageNumbers3D.Off ||
                director != null && director.PresentationController != null &&
                director.PresentationController.PanelOpen) return;
            EnsureStyles();
            var time = Time.unscaledTime;
            for (var index = 0; index < damageLabels.Length; index++)
            {
                var slot = damageLabels[index];
                if (!slot.Active) continue;
                if (time >= slot.ExpiresAt)
                {
                    ClearSlot(slot);
                    continue;
                }

                var screen = worldCamera.WorldToScreenPoint(slot.WorldPosition);
                if (screen.z <= 0f) continue;
                var normalized = Mathf.Clamp01((time - slot.StartedAt) / LabelLifetime);
                var rise = options.ReducedMotion ? 0f : normalized * 34f;
                var width = 150f * options.UiScale;
                var height = 28f * options.UiScale;
                var rect = new Rect(
                    screen.x - width * 0.5f,
                    Screen.height - screen.y - rise - height * 0.5f,
                    width,
                    height);
                var alpha = 1f - Mathf.Clamp01((normalized - 0.62f) / 0.38f);
                if (slot.Damage > 0f && slot.Shield > 0f)
                {
                    var damageRect = rect;
                    damageRect.x -= width * 0.22f;
                    var shieldRect = rect;
                    shieldRect.x += width * 0.22f;
                    DrawLabel(damageRect, slot.DamageContent,
                        new Color(1f, 0.3f, 0.22f, alpha));
                    DrawLabel(shieldRect, slot.ShieldContent,
                        new Color(0.48f, 0.9f, 1f, alpha));
                }
                else if (slot.Damage > 0f)
                {
                    DrawLabel(rect, slot.DamageContent,
                        slot.Died ? new Color(1f, 0.78f, 0.2f, alpha) :
                        slot.Downed ? new Color(1f, 0.48f, 0.18f, alpha) :
                        new Color(1f, 0.3f, 0.22f, alpha));
                }
                else
                {
                    DrawLabel(rect, slot.ShieldContent, new Color(0.48f, 0.9f, 1f, alpha));
                }
                if (!string.IsNullOrEmpty(slot.StatusContent.text))
                {
                    var statusRect = rect;
                    statusRect.y += height * 0.48f;
                    DrawLabel(statusRect, slot.StatusContent,
                        new Color(1f, 0.9f, 0.5f, alpha));
                }
            }
        }

        public void HandleHit(
            BattleArena3DUnit source,
            BattleArena3DUnit target,
            BattleArenaDamageResult3D result,
            Vector3 position,
            Vector3 direction,
            RaidAbilityEffect effect,
            bool basicAttack,
            bool heavyImpact)
        {
            if (target == null || result.TotalResolved <= 0f) return;
            FeedbackEventCount++;

            var resolvedDirection = direction.sqrMagnitude > 0.001f
                ? direction.normalized
                : Vector3.forward;
            var impactColor = source != null ? source.ThemeColor : new Color(0.9f, 0.18f, 0.12f);

            if (result.AbsorbedShield > 0f)
            {
                ShieldContactCount++;
                vfxPool?.EmitShield(position, new Color(0.52f, 0.9f, 1f, 1f),
                    result.ShieldBroken ? 32 : 18);
            }

            if (basicAttack && (source == null || !source.IsRanged))
            {
                var slashOrigin = source != null
                    ? source.transform.position + Vector3.up * 1.05f + resolvedDirection * 0.65f
                    : position;
                vfxPool?.EmitSlash(slashOrigin, resolvedDirection, impactColor, heavyImpact ? 26 : 16);
            }

            if (result.AppliedDamage > 0f)
            {
                if (basicAttack || effect is RaidAbilityEffect.Execute or RaidAbilityEffect.DashStrike or
                    RaidAbilityEffect.ShadowStep)
                {
                    vfxPool?.EmitBlood(position, resolvedDirection, heavyImpact ? 18 : 11);
                }
                else
                {
                    vfxPool?.EmitImpact(position, impactColor, heavyImpact ? 18 : 10);
                }
            }

            if (result.Died)
            {
                var elite = IsElite(target);
                vfxPool?.EmitDeath(target.transform.position + Vector3.up * 0.8f,
                    target.ThemeColor, elite);
                DeathBurstCount++;
            }

            EmitCameraImpulse(source, target, result, position, heavyImpact);
            if (ShouldShowDamageLabel(source, target, result) && CanAllocateDamageLabel(target, result))
            {
                AddOrAggregateDamageLabel(target, result, position);
            }
        }

        public void ApplyPresentationOptions(BattleArenaPresentationOptions3D presentationOptions)
        {
            options = presentationOptions.Sanitized();
            presentationApplied = true;
            damageStyle = null;
            shadowStyle = null;
            markedUnit = null;
        }

        public void ResetFeedback()
        {
            FeedbackEventCount = 0;
            CameraImpulseCount = 0;
            DamageLabelEmissionCount = 0;
            DeathBurstCount = 0;
            ShieldContactCount = 0;
            for (var index = 0; index < damageLabels.Length; index++) ClearSlot(damageLabels[index]);
            nextCameraImpulseAt = 0f;
            markedUnit = null;
            if (selectionMarkerRoot != null) selectionMarkerRoot.gameObject.SetActive(false);
        }

        private void EmitCameraImpulse(
            BattleArena3DUnit source,
            BattleArena3DUnit target,
            BattleArenaDamageResult3D result,
            Vector3 position,
            bool heavyImpact)
        {
            if (cameraRig == null || options.ReducedMotion ||
                options.CameraMotion == BattleArenaCameraMotion3D.Off) return;
            var selected = director != null ? director.SelectedUnit : null;
            var selectedEvent = selected == source || selected == target;
            var normalizedDamage = result.TotalResolved / Mathf.Max(1f, target.MaximumHealth);
            if (!heavyImpact && !result.Died && !result.ShieldBroken && !selectedEvent &&
                normalizedDamage < 0.12f) return;
            var minimumInterval = options.CameraMotion == BattleArenaCameraMotion3D.Full ? 0.06f : 0.09f;
            if (Time.unscaledTime < nextCameraImpulseAt) return;
            nextCameraImpulseAt = Time.unscaledTime + minimumInterval;

            var strength = 0.12f + Mathf.Clamp01(normalizedDamage / 0.24f) * 0.28f;
            if (selectedEvent) strength += 0.12f;
            if (result.ShieldBroken) strength += 0.12f;
            if (heavyImpact) strength += 0.24f;
            if (result.Died) strength += 0.32f;
            cameraRig.AddImpulse(position, Mathf.Min(0.9f, strength));
            CameraImpulseCount++;
        }

        private bool ShouldShowDamageLabel(
            BattleArena3DUnit source,
            BattleArena3DUnit target,
            BattleArenaDamageResult3D result)
        {
            if (options.DamageNumbers == BattleArenaDamageNumbers3D.Off) return false;
            if (options.DamageNumbers == BattleArenaDamageNumbers3D.All) return true;
            var selected = director != null ? director.SelectedUnit : null;
            if (selected == source || selected == target) return true;
            if (result.Died || result.BecameDowned || result.ShieldBroken) return true;
            var normalizedDamage = result.TotalResolved / Mathf.Max(1f, target.MaximumHealth);
            return normalizedDamage >= (IsElite(target) ? 0.08f : 0.15f);
        }

        private void AddOrAggregateDamageLabel(
            BattleArena3DUnit target,
            BattleArenaDamageResult3D result,
            Vector3 position)
        {
            var time = Time.unscaledTime;
            DamageLabelSlot slot = null;
            DamageLabelSlot replacement = null;
            var oldestExpiry = float.PositiveInfinity;
            for (var index = 0; index < damageLabels.Length; index++)
            {
                var candidate = damageLabels[index];
                if (candidate.Active && candidate.Target == target &&
                    time - candidate.LastHitAt <= AggregationWindow)
                {
                    slot = candidate;
                    break;
                }
                if (!candidate.Active)
                {
                    replacement = candidate;
                    oldestExpiry = float.NegativeInfinity;
                }
                else if (oldestExpiry != float.NegativeInfinity && candidate.ExpiresAt < oldestExpiry)
                {
                    oldestExpiry = candidate.ExpiresAt;
                    replacement = candidate;
                }
            }

            slot ??= replacement ?? damageLabels[0];
            if (!slot.Active || slot.Target != target || time - slot.LastHitAt > AggregationWindow)
            {
                slot.Target = target;
                slot.Damage = 0f;
                slot.Shield = 0f;
                slot.StartedAt = time;
                slot.Downed = false;
                slot.Died = false;
                slot.ShieldBroken = false;
                slot.Active = true;
            }
            slot.Damage += result.AppliedDamage;
            slot.Shield += result.AbsorbedShield;
            slot.WorldPosition = position + Vector3.up * 0.42f;
            slot.LastHitAt = time;
            slot.ExpiresAt = time + LabelLifetime;
            slot.Downed |= result.BecameDowned;
            slot.Died |= result.Died;
            slot.ShieldBroken |= result.ShieldBroken;
            slot.DamageContent.text = CachedNumber(DamageNumberText, Mathf.CeilToInt(slot.Damage));
            slot.ShieldContent.text = CachedNumber(ShieldNumberText, Mathf.CeilToInt(slot.Shield));
            slot.StatusContent.text = slot.Died ? DefeatedText : slot.Downed ? DownText :
                slot.ShieldBroken ? BreakText : string.Empty;
            DamageLabelEmissionCount++;
        }

        private bool CanAllocateDamageLabel(BattleArena3DUnit target, BattleArenaDamageResult3D result)
        {
            if (options.DamageNumbers != BattleArenaDamageNumbers3D.Contextual) return true;
            var activeCount = 0;
            for (var index = 0; index < damageLabels.Length; index++)
            {
                var slot = damageLabels[index];
                if (!slot.Active) continue;
                if (slot.Target == target) return true;
                activeCount++;
            }
            var priority = result.Died || result.BecameDowned || result.ShieldBroken ||
                           target == director?.SelectedUnit;
            return activeCount < (priority ? 12 : 8);
        }

        private void UpdateSelectionMarker()
        {
            if (selectionMarkerRoot == null) return;
            var selected = director != null ? director.SelectedUnit : null;
            if (selected == null || !selected.isActiveAndEnabled ||
                selected.LifeState == BattleArenaLifeState3D.Dead)
            {
                selectionMarkerRoot.gameObject.SetActive(false);
                markedUnit = null;
                return;
            }

            if (!selectionMarkerRoot.gameObject.activeSelf) selectionMarkerRoot.gameObject.SetActive(true);
            selectionMarkerRoot.position = selected.transform.position + Vector3.up * 0.07f;
            var baseScale = Mathf.Max(0.9f,
                selected.Hurtbox != null ? selected.Hurtbox.radius * 1.55f : 1f);
            var pulse = options.ReducedMotion
                ? 1f
                : 1f + Mathf.Sin(Time.unscaledTime * 5.5f) * 0.055f;
            selectionMarkerRoot.localScale = Vector3.one * baseScale * pulse;

            var resolvedColor = ResolveMarkerColor(selected);
            if (markedUnit != selected || !Approximately(markerColor, resolvedColor))
            {
                markerColor = resolvedColor;
                markedUnit = selected;
                ApplyMarkerColor(resolvedColor);
            }
        }

        private void ApplyMarkerColor(Color color)
        {
            if (selectionOuter != null)
            {
                var outerColor = new Color(color.r, color.g, color.b, 0.92f);
                selectionOuter.startColor = outerColor;
                selectionOuter.endColor = outerColor;
            }
            if (selectionInner != null)
            {
                var innerColor = Color.Lerp(color, Color.white, 0.55f);
                innerColor.a = 0.82f;
                selectionInner.startColor = innerColor;
                selectionInner.endColor = innerColor;
            }
        }

        private Color ResolveMarkerColor(BattleArena3DUnit unit)
        {
            if (!options.HighContrastFactions) return unit.ThemeColor;
            return unit.Faction == BattleArenaFaction3D.Hunters
                ? new Color(0f, 0.72f, 1f, 1f)
                : new Color(1f, 0.42f, 0f, 1f);
        }

        private void EnsureStyles()
        {
            var fontSize = Mathf.RoundToInt(17f * options.UiScale);
            if (damageStyle != null && damageStyle.fontSize == fontSize) return;
            damageStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = fontSize,
                clipping = TextClipping.Overflow
            };
            shadowStyle = new GUIStyle(damageStyle);
        }

        private void DrawLabel(Rect rect, GUIContent content, Color color)
        {
            shadowStyle.normal.textColor = new Color(0f, 0f, 0f, color.a * 0.86f);
            damageStyle.normal.textColor = color;
            var shadowRect = rect;
            shadowRect.position += Vector2.one * Mathf.Max(1f, options.UiScale * 2f);
            GUI.Label(shadowRect, content, shadowStyle);
            GUI.Label(rect, content, damageStyle);
        }

        private static bool IsElite(BattleArena3DUnit unit)
        {
            return unit != null && (unit.IsSergeant ||
                                    unit.MonsterArchetype == BattleArenaMonsterArchetype3D.Elite);
        }

        private static string[] BuildNumberCache(string prefix)
        {
            var values = new string[CachedNumberMaximum + 1];
            for (var index = 0; index < values.Length; index++) values[index] = prefix + index;
            return values;
        }

        private static string CachedNumber(string[] values, int value)
        {
            return values[Mathf.Clamp(value, 0, CachedNumberMaximum)];
        }

        private static void ClearSlot(DamageLabelSlot slot)
        {
            if (slot == null) return;
            slot.Target = null;
            slot.DamageContent.text = string.Empty;
            slot.ShieldContent.text = string.Empty;
            slot.StatusContent.text = string.Empty;
            slot.Active = false;
            slot.Damage = 0f;
            slot.Shield = 0f;
            slot.Downed = false;
            slot.Died = false;
            slot.ShieldBroken = false;
        }

        private static bool Approximately(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.001f && Mathf.Abs(a.g - b.g) < 0.001f &&
                   Mathf.Abs(a.b - b.b) < 0.001f && Mathf.Abs(a.a - b.a) < 0.001f;
        }

#if UNITY_EDITOR
        public void ConfigureEditor(
            BattleArena3DDirector assignedDirector,
            Camera assignedCamera,
            BattleArena3DCameraRig assignedCameraRig,
            BattleArena3DVfxPool assignedVfxPool,
            Transform assignedSelectionRoot,
            LineRenderer assignedOuter,
            LineRenderer assignedInner)
        {
            director = assignedDirector;
            worldCamera = assignedCamera;
            cameraRig = assignedCameraRig;
            vfxPool = assignedVfxPool;
            selectionMarkerRoot = assignedSelectionRoot;
            selectionOuter = assignedOuter;
            selectionInner = assignedInner;
            if (selectionMarkerRoot != null) selectionMarkerRoot.gameObject.SetActive(false);
        }
#endif
    }
}
