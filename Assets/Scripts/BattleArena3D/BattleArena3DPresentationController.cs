using UnityEngine;
using UnityEngine.InputSystem;

namespace Turtle.BattleArena3D
{
    [DisallowMultipleComponent]
    public sealed class BattleArena3DPresentationController : MonoBehaviour
    {
        private const string PreferencePrefix = "Turtle.BattleArena3D.Accessibility.";

        [SerializeField] private BattleArena3DDirector director;
        [SerializeField] private BattleArena3DCameraRig cameraRig;
        [SerializeField] private BattleArena3DVfxPool vfxPool;
        [SerializeField] private BattleArena3DCombatFeedback combatFeedback;
        [SerializeField] private BattleArenaPresentationOptions3D options = new()
        {
            UiScale = 1f,
            EffectsLevel = BattleArenaEffectsLevel3D.Full,
            CameraMotion = BattleArenaCameraMotion3D.Reduced,
            WorldBars = BattleArenaWorldBars3D.Contextual,
            DamageNumbers = BattleArenaDamageNumbers3D.Contextual
        };

        private bool panelOpen;
        private float previousTimeScale = 1f;
        private Rect panelRect;
        private Rect accessButtonRect;

        public bool IsConfigured => director != null && cameraRig != null && vfxPool != null &&
                                    combatFeedback != null;
        public BattleArenaPresentationOptions3D Options => options;
        public float UiScale => options.UiScale;
        public int Revision { get; private set; }
        public bool PanelOpen => panelOpen;
        public bool IsPaused => Time.timeScale <= 0.001f;
        public float ActiveSimulationSpeed => IsPaused ? previousTimeScale : Time.timeScale;

        private void Awake()
        {
            if (options.UiScale < 0.99f) options = BattleArenaPresentationOptions3D.Default;
            LoadPreferences();
            ApplyOptions(options, false);
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.f1Key.wasPressedThisFrame) panelOpen = !panelOpen;
            if (keyboard.escapeKey.wasPressedThisFrame && panelOpen) panelOpen = false;
            if (keyboard.spaceKey.wasPressedThisFrame || keyboard.pKey.wasPressedThisFrame) TogglePause();
            if (keyboard.tabKey.wasPressedThisFrame)
            {
                var reverse = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
                director?.SelectNextHunter(reverse ? -1 : 1);
                if (cameraRig != null && cameraRig.IsFollowingSelected) cameraRig.FrameSelected(true);
            }
            if (keyboard.cKey.wasPressedThisFrame) cameraRig?.ToggleFollowSelected();
        }

        private void OnDisable()
        {
            if (IsPaused) Time.timeScale = Mathf.Clamp(previousTimeScale, 0.25f, 2f);
        }

        public void TogglePause()
        {
            if (IsPaused)
            {
                Time.timeScale = Mathf.Clamp(previousTimeScale, 0.25f, 2f);
            }
            else
            {
                previousTimeScale = Mathf.Clamp(Time.timeScale, 0.25f, 2f);
                Time.timeScale = 0f;
            }
        }

        public void SetSimulationSpeed(float speed)
        {
            previousTimeScale = Mathf.Clamp(speed, 0.25f, 2f);
            if (!IsPaused) Time.timeScale = previousTimeScale;
        }

        public bool IsPointerOverUi(Vector2 screenPosition)
        {
            var guiPoint = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
            return accessButtonRect.Contains(guiPoint) || panelOpen && panelRect.Contains(guiPoint);
        }

        public void ApplyValidationDefaults()
        {
            ApplyOptions(BattleArenaPresentationOptions3D.Default, false);
            panelOpen = false;
        }

        public void ApplyValidationAccessibilityPreset()
        {
            var preset = BattleArenaPresentationOptions3D.Default;
            preset.UiScale = 1.25f;
            preset.EffectsLevel = BattleArenaEffectsLevel3D.Minimal;
            preset.CameraMotion = BattleArenaCameraMotion3D.Off;
            preset.WorldBars = BattleArenaWorldBars3D.SelectedOnly;
            preset.DamageNumbers = BattleArenaDamageNumbers3D.Contextual;
            preset.HighContrastFactions = true;
            preset.ReducedMotion = true;
            ApplyOptions(preset, false);
            panelOpen = false;
        }

        public bool RunValidationExercise(out string failure)
        {
            failure = string.Empty;
            if (!IsConfigured)
            {
                failure = "Presentation controller references are incomplete.";
                return false;
            }
            var originalScale = Mathf.Clamp(Time.timeScale, 0.25f, 2f);
            previousTimeScale = originalScale;
            TogglePause();
            if (!IsPaused)
            {
                failure = "Pause did not set Time.timeScale to zero.";
                return false;
            }
            TogglePause();
            if (Time.timeScale <= 0f)
            {
                failure = "Resume did not restore a positive time scale.";
                return false;
            }
            director.SelectNextHunter(1);
            if (director.SelectedUnit == null)
            {
                failure = "Keyboard-style hunter cycling did not select a combatant.";
                return false;
            }
            cameraRig.FrameSelected(false);
            ApplyValidationDefaults();
            Time.timeScale = originalScale;
            return true;
        }

        public void SetPanelOpen(bool open)
        {
            panelOpen = open;
        }

        private void OnGUI()
        {
            accessButtonRect = new Rect(Mathf.Max(10f, Screen.width - 188f), Mathf.Max(8f, Screen.height - 38f),
                178f, 30f);
            if (!panelOpen)
            {
                if (GUI.Button(accessButtonRect, "F1  ACCESSIBILITY")) panelOpen = true;
                return;
            }

            var compact = Screen.height < 440f;
            var width = Mathf.Min(Screen.width - 20f, compact ? 720f : 540f);
            var height = Mathf.Min(Screen.height - 20f, 330f);
            panelRect = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            var previousColor = GUI.color;
            GUI.color = new Color(0.015f, 0.022f, 0.04f, 0.985f);
            GUI.DrawTexture(panelRect, Texture2D.whiteTexture, ScaleMode.StretchToFill);
            GUI.color = previousColor;
            GUI.Box(panelRect, GUIContent.none);
            GUI.Label(new Rect(panelRect.x + 14f, panelRect.y + 9f, panelRect.width - 82f, 24f),
                "ACCESSIBILITY & CONTROLS");
            if (GUI.Button(new Rect(panelRect.xMax - 66f, panelRect.y + 7f, 52f, 26f), "CLOSE"))
            {
                panelOpen = false;
                return;
            }

            var columnGap = 10f;
            var buttonWidth = (panelRect.width - 38f - columnGap) * 0.5f;
            var left = panelRect.x + 14f;
            var right = left + buttonWidth + columnGap;
            var row = panelRect.y + 42f;
            var buttonHeight = panelRect.height < 285f ? 29f : 32f;
            var rowGap = panelRect.height < 285f ? 3f : 5f;

            if (GUI.Button(new Rect(left, row, buttonWidth, buttonHeight), IsPaused ? "RESUME  [SPACE / P]" : "PAUSE  [SPACE / P]"))
                TogglePause();
            if (GUI.Button(new Rect(right, row, buttonWidth, buttonHeight),
                    cameraRig != null && cameraRig.IsFollowingSelected ? "FOLLOW SELECTED: ON  [C]" : "FOLLOW SELECTED: OFF  [C]"))
                cameraRig?.ToggleFollowSelected();
            row += buttonHeight + rowGap;

            if (GUI.Button(new Rect(left, row, buttonWidth, buttonHeight), $"HUD SIZE: {options.UiScale * 100f:0}%"))
                CycleUiScale();
            if (GUI.Button(new Rect(right, row, buttonWidth, buttonHeight), $"WORLD BARS: {options.WorldBars}"))
                CycleWorldBars();
            row += buttonHeight + rowGap;

            if (GUI.Button(new Rect(left, row, buttonWidth, buttonHeight), $"EFFECTS: {options.EffectsLevel}"))
                CycleEffects();
            if (GUI.Button(new Rect(right, row, buttonWidth, buttonHeight), $"CAMERA MOTION: {options.CameraMotion}"))
                CycleCameraMotion();
            row += buttonHeight + rowGap;

            if (GUI.Button(new Rect(left, row, buttonWidth, buttonHeight), $"DAMAGE NUMBERS: {options.DamageNumbers}"))
                CycleDamageNumbers();
            if (GUI.Button(new Rect(right, row, buttonWidth, buttonHeight),
                    $"HIGH-CONTRAST FACTIONS: {(options.HighContrastFactions ? "ON" : "OFF")}"))
            {
                options.HighContrastFactions = !options.HighContrastFactions;
                ApplyOptions(options, true);
            }
            row += buttonHeight + rowGap;

            if (GUI.Button(new Rect(left, row, buttonWidth, buttonHeight),
                    $"REDUCED MOTION: {(options.ReducedMotion ? "ON" : "OFF")}"))
            {
                options.ReducedMotion = !options.ReducedMotion;
                ApplyOptions(options, true);
            }
            if (GUI.Button(new Rect(right, row, buttonWidth, buttonHeight), "RESET ACCESSIBILITY DEFAULTS"))
            {
                ApplyOptions(BattleArenaPresentationOptions3D.Default, true);
            }

            if (panelRect.height >= 285f)
            {
                var helpY = row + buttonHeight + 12f;
                GUI.Label(new Rect(panelRect.x + 14f, helpY, panelRect.width - 28f, 34f),
                    "TAB / SHIFT+TAB: cycle hunters     C: center/follow selected     LMB: inspect     " +
                    "WASD: pan     Q/E: orbit     R/F: zoom");
            }
        }

        private void CycleUiScale()
        {
            options.UiScale = options.UiScale < 1.125f ? 1.25f : options.UiScale < 1.375f ? 1.5f : 1f;
            ApplyOptions(options, true);
        }

        private void CycleEffects()
        {
            options.EffectsLevel = options.EffectsLevel switch
            {
                BattleArenaEffectsLevel3D.Full => BattleArenaEffectsLevel3D.Reduced,
                BattleArenaEffectsLevel3D.Reduced => BattleArenaEffectsLevel3D.Minimal,
                _ => BattleArenaEffectsLevel3D.Full
            };
            ApplyOptions(options, true);
        }

        private void CycleCameraMotion()
        {
            options.CameraMotion = options.CameraMotion switch
            {
                BattleArenaCameraMotion3D.Full => BattleArenaCameraMotion3D.Reduced,
                BattleArenaCameraMotion3D.Reduced => BattleArenaCameraMotion3D.Off,
                _ => BattleArenaCameraMotion3D.Full
            };
            ApplyOptions(options, true);
        }

        private void CycleWorldBars()
        {
            options.WorldBars = options.WorldBars switch
            {
                BattleArenaWorldBars3D.Contextual => BattleArenaWorldBars3D.SelectedOnly,
                BattleArenaWorldBars3D.SelectedOnly => BattleArenaWorldBars3D.All,
                _ => BattleArenaWorldBars3D.Contextual
            };
            ApplyOptions(options, true);
        }

        private void CycleDamageNumbers()
        {
            options.DamageNumbers = options.DamageNumbers switch
            {
                BattleArenaDamageNumbers3D.Contextual => BattleArenaDamageNumbers3D.Off,
                BattleArenaDamageNumbers3D.Off => BattleArenaDamageNumbers3D.All,
                _ => BattleArenaDamageNumbers3D.Contextual
            };
            ApplyOptions(options, true);
        }

        private void ApplyOptions(BattleArenaPresentationOptions3D value, bool persist)
        {
            options = value.Sanitized();
            Revision++;
            cameraRig?.ApplyPresentationOptions(options);
            vfxPool?.ApplyPresentationOptions(options);
            combatFeedback?.ApplyPresentationOptions(options);
            if (persist) SavePreferences();
        }

        private void LoadPreferences()
        {
            if (!PlayerPrefs.HasKey(PreferencePrefix + "UiScale")) return;
            options.UiScale = PlayerPrefs.GetFloat(PreferencePrefix + "UiScale", options.UiScale);
            options.EffectsLevel = (BattleArenaEffectsLevel3D)PlayerPrefs.GetInt(
                PreferencePrefix + "Effects", (int)options.EffectsLevel);
            options.CameraMotion = (BattleArenaCameraMotion3D)PlayerPrefs.GetInt(
                PreferencePrefix + "CameraMotion", (int)options.CameraMotion);
            options.WorldBars = (BattleArenaWorldBars3D)PlayerPrefs.GetInt(
                PreferencePrefix + "WorldBars", (int)options.WorldBars);
            options.DamageNumbers = (BattleArenaDamageNumbers3D)PlayerPrefs.GetInt(
                PreferencePrefix + "DamageNumbers", (int)options.DamageNumbers);
            options.HighContrastFactions = PlayerPrefs.GetInt(PreferencePrefix + "HighContrast", 0) != 0;
            options.ReducedMotion = PlayerPrefs.GetInt(PreferencePrefix + "ReducedMotion", 0) != 0;
            options = options.Sanitized();
        }

        private void SavePreferences()
        {
            PlayerPrefs.SetFloat(PreferencePrefix + "UiScale", options.UiScale);
            PlayerPrefs.SetInt(PreferencePrefix + "Effects", (int)options.EffectsLevel);
            PlayerPrefs.SetInt(PreferencePrefix + "CameraMotion", (int)options.CameraMotion);
            PlayerPrefs.SetInt(PreferencePrefix + "WorldBars", (int)options.WorldBars);
            PlayerPrefs.SetInt(PreferencePrefix + "DamageNumbers", (int)options.DamageNumbers);
            PlayerPrefs.SetInt(PreferencePrefix + "HighContrast", options.HighContrastFactions ? 1 : 0);
            PlayerPrefs.SetInt(PreferencePrefix + "ReducedMotion", options.ReducedMotion ? 1 : 0);
            PlayerPrefs.Save();
        }

#if UNITY_EDITOR
        public void ConfigureEditor(
            BattleArena3DDirector assignedDirector,
            BattleArena3DCameraRig assignedCameraRig,
            BattleArena3DVfxPool assignedVfxPool,
            BattleArena3DCombatFeedback assignedFeedback)
        {
            director = assignedDirector;
            cameraRig = assignedCameraRig;
            vfxPool = assignedVfxPool;
            combatFeedback = assignedFeedback;
            options = BattleArenaPresentationOptions3D.Default;
        }
#endif
    }
}
