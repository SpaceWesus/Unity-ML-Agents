using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Turtle.BattleArena3D.Editor
{
    public static class BattleArena3DSmokeTest
    {
        private const string ActiveKey = "Turtle.BattleArena3D.Smoke.Active";
        private const string StartedAtKey = "Turtle.BattleArena3D.Smoke.StartedAt";
        private const string ThresholdAtKey = "Turtle.BattleArena3D.Smoke.ThresholdAt";
        private const string ScreenshotKey = "Turtle.BattleArena3D.Smoke.Screenshot";
        private const string RuntimeErrorKey = "Turtle.BattleArena3D.Smoke.RuntimeError";
        private const string PresentationExercisedKey = "Turtle.BattleArena3D.Smoke.PresentationExercised";
        private const string RequestRelativePath = "Temp/CodexValidation/run-3d-test-arena-smoke.request";
        private const string ResultRelativePath = "Temp/CodexValidation/3d-test-arena-smoke.result";
        private const string ScreenshotRelativePath = "Temp/CodexValidation/3d-test-arena-smoke.png";
        private static double nextPollAt;

        private static string ProjectRoot => Directory.GetParent(Application.dataPath)?.FullName ??
                                             Directory.GetCurrentDirectory();
        private static string RequestPath => Path.Combine(ProjectRoot,
            RequestRelativePath.Replace('/', Path.DirectorySeparatorChar));
        private static string ResultPath => Path.Combine(ProjectRoot,
            ResultRelativePath.Replace('/', Path.DirectorySeparatorChar));
        private static string ScreenshotPath => Path.Combine(ProjectRoot,
            ScreenshotRelativePath.Replace('/', Path.DirectorySeparatorChar));

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            Application.logMessageReceived -= OnRuntimeLog;
            Application.logMessageReceived += OnRuntimeLog;
        }

        [MenuItem("Turtle/Battle Arena 3D/Run 3D Battle Smoke Test")]
        public static void RunFromMenu()
        {
            Begin();
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(ActiveKey, false))
            {
                if (EditorApplication.timeSinceStartup < nextPollAt) return;
                nextPollAt = EditorApplication.timeSinceStartup + 0.5d;
                if (!File.Exists(RequestPath) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
                    EditorApplication.isPlayingOrWillChangePlaymode) return;
                File.Delete(RequestPath);
                Begin();
                return;
            }

            if (!EditorApplication.isPlaying) return;
            var director = UnityEngine.Object.FindFirstObjectByType<BattleArena3DDirector>();
            if (director == null)
            {
                Complete(false, "3D Test Arena entered Play Mode without BattleArena3DDirector.");
                return;
            }
            var presentation = director.PresentationController;
            var feedback = UnityEngine.Object.FindFirstObjectByType<BattleArena3DCombatFeedback>();
            if (presentation == null || !presentation.IsConfigured)
            {
                Complete(false, "3D Test Arena has no configured accessibility/presentation controller.");
                return;
            }
            if (feedback == null || !feedback.IsConfigured)
            {
                Complete(false, "3D Test Arena has no configured bounded combat-feedback service.");
                return;
            }
            if (director.Phase is not (BattleArenaPhase3D.Prewarming or BattleArenaPhase3D.Preparing) &&
                !SessionState.GetBool(PresentationExercisedKey, false))
            {
                presentation.ApplyValidationDefaults();
                if (!presentation.RunValidationExercise(out var presentationFailure))
                {
                    Complete(false, "Accessibility control exercise failed: " + presentationFailure);
                    return;
                }
                if (!HasExpectedDefaultPresentation(presentation.Options))
                {
                    Complete(false, "Accessibility control exercise did not restore presentation defaults.");
                    return;
                }
                if (director.SelectedUnit == null)
                {
                    Complete(false, "Accessibility control exercise did not leave a hunter selected.");
                    return;
                }
                SessionState.SetBool(PresentationExercisedKey, true);
            }
            var startedAt = SessionState.GetFloat(StartedAtKey, 0f);
            var elapsed = (float)EditorApplication.timeSinceStartup - startedAt;
            var runtimeError = SessionState.GetString(RuntimeErrorKey, string.Empty);
            if (!string.IsNullOrEmpty(runtimeError))
            {
                Complete(false, "Runtime error during the 3D battle: " + runtimeError);
                return;
            }
            if (elapsed > 55f)
            {
                Complete(false,
                    $"Timed out after {elapsed:0.0}s: phase={director.Phase}, hunters={director.LivingHunterCount}, " +
                    $"monsters={director.ActiveMonsterCount}, peak={director.PeakConcurrentMonsterCount}, " +
                    $"attacks={director.AttackAttempts}, hits={director.ConfirmedHits}, abilities={director.AbilityCasts}, " +
                    $"AOE={director.AreaAbilityCasts}, statuses={director.StatusApplications}, " +
                    $"commander={director.CommanderDecisions}, feedback={director.FeedbackEventCount}, " +
                    $"impulses={director.CameraImpulseCount}, labels={director.DamageLabelEmissionCount}, " +
                    $"deathBursts={director.DeathBurstCount}.");
                return;
            }
            if (director.Phase is BattleArenaPhase3D.Prewarming or BattleArenaPhase3D.Preparing) return;
            Time.timeScale = 2f;
            if (director.HunterCount != 30)
            {
                Complete(false, $"Expected 30 persistent hunters; observed {director.HunterCount}.");
                return;
            }

            var thresholdAt = SessionState.GetFloat(ThresholdAtKey, -1f);
            var representativeSystemsObserved =
                director.PeakConcurrentMonsterCount >= 200 &&
                director.AttackAttempts >= 12 &&
                director.ConfirmedHits >= 20 &&
                director.AbilityCasts >= 20 &&
                director.AreaAbilityCasts >= 5 &&
                director.StatusApplications >= 5 &&
                director.CommanderDecisions > 0 &&
                director.TelegraphEmissions >= 10 &&
                director.FeedbackEventCount > 0 &&
                director.CameraImpulseCount > 0 &&
                director.DamageLabelEmissionCount > 0 &&
                director.DeathBurstCount > 0 &&
                feedback.SelectionMarkerActive;
            if (representativeSystemsObserved && thresholdAt < 0f)
            {
                thresholdAt = elapsed;
                SessionState.SetFloat(ThresholdAtKey, thresholdAt);
                presentation.SetPanelOpen(true);
                Directory.CreateDirectory(Path.GetDirectoryName(ScreenshotPath) ?? ProjectRoot);
                ScreenCapture.CaptureScreenshot(ScreenshotPath);
                SessionState.SetBool(ScreenshotKey, true);
            }
            if (representativeSystemsObserved && thresholdAt >= 0f && elapsed - thresholdAt >= 1f)
            {
                Complete(true,
                    $"3D Test Arena smoke passed on round {director.CurrentRound} at {elapsed:0.0}s: " +
                    $"30 hunters, peak {director.PeakConcurrentMonsterCount} monsters / " +
                    $"{director.PeakCombatantCount} total combatants, {director.AttackAttempts} attacks, " +
                    $"{director.ConfirmedHits} contact hits, {director.AbilityCasts} abilities, " +
                    $"{director.AreaAbilityCasts} AOE casts, {director.StatusApplications} statuses, and " +
                    $"{director.CommanderDecisions} commander decisions. The pooled cue system emitted " +
                    $"{director.TelegraphEmissions} combat rings with {director.DroppedTelegraphs} dropped. " +
                    $"The bounded feedback layer handled {director.FeedbackEventCount} contacts, " +
                    $"{director.CameraImpulseCount} camera impulses, {director.DamageLabelEmissionCount} " +
                    $"damage-label emissions, {director.DeathBurstCount} death bursts, and " +
                    $"{director.ShieldContactCount} shield contacts. Pause/resume, hunter cycling, focus, " +
                    $"default presentation restoration, and the shared selection marker were exercised.\n" +
                    $"Screenshot: {ScreenshotRelativePath}");
            }
        }

        private static bool HasExpectedDefaultPresentation(BattleArenaPresentationOptions3D options)
        {
            return Mathf.Approximately(options.UiScale, 1f) &&
                   options.EffectsLevel == BattleArenaEffectsLevel3D.Full &&
                   options.CameraMotion == BattleArenaCameraMotion3D.Reduced &&
                   options.WorldBars == BattleArenaWorldBars3D.Contextual &&
                   options.DamageNumbers == BattleArenaDamageNumbers3D.Contextual &&
                   !options.HighContrastFactions &&
                   !options.ReducedMotion;
        }

        private static void Begin()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
                EditorApplication.isUpdating) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BattleArena3DSceneBuilder.ScenePath) == null)
            {
                WriteResult("FAIL", $"Missing scene: {BattleArena3DSceneBuilder.ScenePath}");
                return;
            }
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetFloat(StartedAtKey, (float)EditorApplication.timeSinceStartup);
            SessionState.SetFloat(ThresholdAtKey, -1f);
            SessionState.SetBool(ScreenshotKey, false);
            SessionState.SetBool(PresentationExercisedKey, false);
            SessionState.SetString(RuntimeErrorKey, string.Empty);
            if (File.Exists(ResultPath)) File.Delete(ResultPath);
            if (File.Exists(ScreenshotPath)) File.Delete(ScreenshotPath);
            EditorSceneManager.OpenScene(BattleArena3DSceneBuilder.ScenePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(ActiveKey, false))
            {
                SessionState.SetFloat(StartedAtKey, (float)EditorApplication.timeSinceStartup);
            }
            if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(ActiveKey, false))
            {
                SessionState.SetBool(ActiveKey, false);
            }
        }

        private static void OnRuntimeLog(string condition, string stackTrace, LogType type)
        {
            if (!SessionState.GetBool(ActiveKey, false) ||
                type is not (LogType.Error or LogType.Exception or LogType.Assert)) return;
            var message = string.IsNullOrWhiteSpace(condition) ? stackTrace : condition;
            if (message.Length > 800) message = message[..800];
            SessionState.SetString(RuntimeErrorKey, message);
        }

        private static void Complete(bool passed, string message)
        {
            WriteResult(passed ? "PASS" : "FAIL", message);
            Debug.Log(passed ? message : $"3D Test Arena smoke failed: {message}");
            SessionState.SetBool(ActiveKey, false);
            Time.timeScale = 1f;
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        }

        private static void WriteResult(string status, string message)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ResultPath) ?? ProjectRoot);
            File.WriteAllText(ResultPath, status + Environment.NewLine + message + Environment.NewLine);
        }
    }
}
