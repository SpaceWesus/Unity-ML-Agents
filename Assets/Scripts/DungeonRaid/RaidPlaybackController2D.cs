using UnityEngine;

namespace Turtle.DungeonRaid
{
    /// <summary>
    /// Scene-local developer playback control for observing the autonomous raid.
    /// Time.timeScale is used deliberately so AI decisions, movement, physics,
    /// ability durations, animation, and VFX all remain synchronized.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RaidPlaybackController2D : MonoBehaviour
    {
        private static readonly float[] PlaybackPresets = { 0.25f, 0.5f, 1f, 2f };
        private static readonly string[] PlaybackLabels = { "0.25x", "0.5x", "1x", "2x" };

        [SerializeField, Range(0.05f, 2f)] private float defaultPlaybackSpeed = 0.25f;
        [SerializeField] private bool showPlaybackControls = true;

        private float originalTimeScale = 1f;
        private float originalFixedDeltaTime = 0.02f;
        private float playbackSpeed = 1f;
        private bool ownsTimeScale;
        private GUIStyle panelStyle;
        private GUIStyle labelStyle;

        public float DefaultPlaybackSpeed => defaultPlaybackSpeed;
        public float PlaybackSpeed => playbackSpeed;
        public bool ShowsPlaybackControls => showPlaybackControls;

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            originalTimeScale = Time.timeScale;
            originalFixedDeltaTime = Time.fixedDeltaTime;
            ownsTimeScale = true;
            SetPlaybackSpeed(defaultPlaybackSpeed);
        }

        private void OnDisable()
        {
            RestoreTimeSettings();
        }

        public void SetPlaybackSpeed(float speed)
        {
            if (!Application.isPlaying) return;
            playbackSpeed = Mathf.Clamp(speed, 0f, 4f);
            Time.timeScale = playbackSpeed;

            // Slow motion receives a proportionally smaller physics step so
            // actors remain smooth instead of visibly hopping between casts.
            // Fast automated tests retain the normal step to avoid excessive work.
            Time.fixedDeltaTime = originalFixedDeltaTime *
                                  Mathf.Clamp(playbackSpeed, 0.05f, 1f);
        }

        private void OnGUI()
        {
            if (!showPlaybackControls || !Application.isPlaying) return;
            EnsureStyles();

            const float panelWidth = 320f;
            const float panelHeight = 78f;
            var panel = new Rect(Screen.width - panelWidth - 12f, 12f, panelWidth, panelHeight);
            var previousColor = GUI.color;
            GUI.color = new Color(0.025f, 0.04f, 0.055f, 0.94f);
            GUI.Box(panel, GUIContent.none, panelStyle);
            GUI.color = previousColor;

            var state = $"{playbackSpeed:0.##}x";
            GUI.Label(
                new Rect(panel.x + 12f, panel.y + 7f, panel.width - 24f, 23f),
                $"RAID PLAYBACK - {state}",
                labelStyle);

            const float buttonGap = 5f;
            var buttonWidth = (panel.width - 24f - buttonGap * (PlaybackPresets.Length - 1)) /
                              PlaybackPresets.Length;
            for (var index = 0; index < PlaybackPresets.Length; index++)
            {
                var preset = PlaybackPresets[index];
                var selected = Mathf.Approximately(playbackSpeed, preset);
                var previousBackground = GUI.backgroundColor;
                if (selected) GUI.backgroundColor = new Color(0.75f, 1f, 0.9f);
                var button = new Rect(
                    panel.x + 12f + index * (buttonWidth + buttonGap),
                    panel.y + 37f,
                    buttonWidth,
                    27f);
                if (GUI.Button(button, PlaybackLabels[index])) SetPlaybackSpeed(preset);
                GUI.backgroundColor = previousBackground;
            }
        }

        private void RestoreTimeSettings()
        {
            if (!ownsTimeScale) return;
            Time.timeScale = originalTimeScale;
            Time.fixedDeltaTime = originalFixedDeltaTime;
            playbackSpeed = originalTimeScale;
            ownsTimeScale = false;
        }

        private void EnsureStyles()
        {
            if (panelStyle != null) return;
            panelStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = Texture2D.whiteTexture },
                padding = new RectOffset(10, 10, 8, 8)
            };
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.75f, 1f, 0.9f) }
            };
        }

#if UNITY_EDITOR
        public void ConfigureEditor(float playbackSpeedOnStart, bool showControls)
        {
            defaultPlaybackSpeed = Mathf.Clamp(playbackSpeedOnStart, 0.05f, 2f);
            showPlaybackControls = showControls;
        }
#endif
    }
}
