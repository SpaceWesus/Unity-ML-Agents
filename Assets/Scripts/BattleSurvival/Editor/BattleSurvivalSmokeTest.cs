using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Turtle.BattleSurvival.Editor
{
    [InitializeOnLoad]
    public static class BattleSurvivalSmokeTest
    {
        private const string ActiveKey = "Turtle.BattleSurvival.Smoke.Active";
        private const string FinishedKey = "Turtle.BattleSurvival.Smoke.Finished";
        private const string ExitCodeKey = "Turtle.BattleSurvival.Smoke.ExitCode";
        private const string StressAppliedKey = "Turtle.BattleSurvival.Smoke.StressApplied";
        private const string SawTwoHundredMonstersKey = "Turtle.BattleSurvival.Smoke.SawTwoHundred";
        private const string SawAttacksKey = "Turtle.BattleSurvival.Smoke.SawAttacks";
        private const string SawHitsKey = "Turtle.BattleSurvival.Smoke.SawHits";
        private const string SawAbilitiesKey = "Turtle.BattleSurvival.Smoke.SawAbilities";
        private const string SawAreaAbilitiesKey = "Turtle.BattleSurvival.Smoke.SawAreaAbilities";
        private const string SawStatusesKey = "Turtle.BattleSurvival.Smoke.SawStatuses";
        private const string ScreenshotQueuedKey = "Turtle.BattleSurvival.Smoke.ScreenshotQueued";
        private const string RequestRelativePath =
            "Temp/CodexValidation/battle-test-smoke.request";
        private const string ResultRelativePath =
            "Temp/CodexValidation/battle-test-smoke.result";
        private const string ScreenshotRelativePath =
            "Temp/CodexValidation/battle-test-survival.png";
        private const float MaximumCombatSeconds = 90f;
        private static double readyToStartAt;
        private static double screenshotQueuedAt;

        private static string ProjectRoot => Directory.GetParent(Application.dataPath)?.FullName
                                             ?? Directory.GetCurrentDirectory();
        private static string RequestPath => Path.Combine(
            ProjectRoot,
            RequestRelativePath.Replace('/', Path.DirectorySeparatorChar));
        private static string ResultPath => Path.Combine(
            ProjectRoot,
            ResultRelativePath.Replace('/', Path.DirectorySeparatorChar));
        private static string ScreenshotPath => Path.Combine(
            ProjectRoot,
            ScreenshotRelativePath.Replace('/', Path.DirectorySeparatorChar));

        static BattleSurvivalSmokeTest()
        {
            readyToStartAt = EditorApplication.timeSinceStartup + 2d;
            EditorApplication.update -= Poll;
            EditorApplication.update += Poll;
        }

        [MenuItem("Turtle/Battle Survival/Run Survival Smoke Test")]
        public static void Run()
        {
            Application.runInBackground = true;
            SetBool(StressAppliedKey, false);
            SetBool(SawTwoHundredMonstersKey, false);
            SetBool(SawAttacksKey, false);
            SetBool(SawHitsKey, false);
            SetBool(SawAbilitiesKey, false);
            SetBool(SawAreaAbilitiesKey, false);
            SetBool(SawStatusesKey, false);
            SetBool(ScreenshotQueuedKey, false);
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetBool(FinishedKey, false);
            SessionState.SetInt(ExitCodeKey, 1);
            if (File.Exists(ScreenshotPath)) File.Delete(ScreenshotPath);
            EditorSceneManager.OpenScene(BattleSurvivalSceneBuilder.ScenePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        private static void Poll()
        {
            if (!SessionState.GetBool(ActiveKey, false))
            {
                TryStartRequestedRun();
                return;
            }
            if (SessionState.GetBool(FinishedKey, false))
            {
                if (EditorApplication.isPlaying) return;
                var exitCode = SessionState.GetInt(ExitCodeKey, 1);
                ClearState();
                if (Application.isBatchMode) EditorApplication.Exit(exitCode);
                return;
            }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;

            Application.runInBackground = true;
            Time.timeScale = 4f;
            var survival = Object.FindFirstObjectByType<BattleSurvivalDirector2D>();
            if (survival == null) return;
            if (!survival.IsConfigured)
            {
                Complete(1, "Battle Test entered Play Mode without all required references.");
                return;
            }
            if (survival.Phase is BattleSurvivalPhase.Prewarming or BattleSurvivalPhase.Preparing)
            {
                return;
            }
            if (survival.HunterCount != BattleSurvivalHunterCatalog.HunterCount ||
                survival.ActiveSergeantCount != BattleSurvivalHunterCatalog.SquadCount)
            {
                Complete(1,
                    $"Expected {BattleSurvivalHunterCatalog.HunterCount} unique hunters and " +
                    $"{BattleSurvivalHunterCatalog.SquadCount} active sergeants; observed " +
                    $"{survival.HunterCount} and {survival.ActiveSergeantCount}.");
                return;
            }
            if (!SessionState.GetBool(StressAppliedKey, false) &&
                survival.Phase == BattleSurvivalPhase.Wave)
            {
                survival.AddStressEnemies(220);
                SessionState.SetBool(StressAppliedKey, true);
            }

            Observe(SawTwoHundredMonstersKey, survival.ActiveMonsterCount >= 200);
            Observe(SawAttacksKey, survival.BasicAttackAttempts > 0);
            Observe(SawHitsKey, survival.ConfirmedBasicHits > 0);
            Observe(SawAbilitiesKey, survival.AbilityCasts > 0);
            Observe(SawAreaAbilitiesKey, survival.AreaAbilityCasts > 0);
            Observe(SawStatusesKey, survival.ActiveStatusEffects > 0);

            if (survival.ActiveMonsterCount >= 200 &&
                !SessionState.GetBool(ScreenshotQueuedKey, false))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ScreenshotPath) ?? ProjectRoot);
                ScreenCapture.CaptureScreenshot(ScreenshotPath);
                screenshotQueuedAt = EditorApplication.timeSinceStartup;
                SessionState.SetBool(ScreenshotQueuedKey, true);
            }

            var allObserved = SessionState.GetBool(SawTwoHundredMonstersKey, false) &&
                              SessionState.GetBool(SawAttacksKey, false) &&
                              SessionState.GetBool(SawHitsKey, false) &&
                              SessionState.GetBool(SawAbilitiesKey, false) &&
                              SessionState.GetBool(SawAreaAbilitiesKey, false) &&
                              SessionState.GetBool(SawStatusesKey, false) &&
                              survival.CommanderDecisions >= 4;
            if (allObserved)
            {
                if (!SessionState.GetBool(ScreenshotQueuedKey, false))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(ScreenshotPath) ?? ProjectRoot);
                    ScreenCapture.CaptureScreenshot(ScreenshotPath);
                    screenshotQueuedAt = EditorApplication.timeSinceStartup;
                    SessionState.SetBool(ScreenshotQueuedKey, true);
                    return;
                }
                if (!File.Exists(ScreenshotPath) &&
                    EditorApplication.timeSinceStartup - screenshotQueuedAt < 3d)
                {
                    return;
                }
                Complete(0,
                    $"Battle Test survival smoke passed on round {survival.CurrentRound} at " +
                    $"{survival.CombatTime:0.0}s: {survival.HunterCount} hunters, peak observed horde " +
                    $"{survival.PeakConcurrentMonsterCount}, {survival.BasicAttackAttempts} attacks, " +
                    $"{survival.ConfirmedBasicHits} hits, {survival.AbilityCasts} abilities, " +
                    $"{survival.AreaAbilityCasts} AOE casts, and live status effects.");
                return;
            }

            if (survival.Phase == BattleSurvivalPhase.Defeat ||
                survival.CombatTime >= MaximumCombatSeconds)
            {
                Complete(1,
                    $"Battle Test survival smoke ended before all representative systems were observed. " +
                    $"Round={survival.CurrentRound}, phase={survival.Phase}, " +
                    $"hunters={survival.LivingHunterCount}, monsters={survival.ActiveMonsterCount}, " +
                    $"attacks={survival.BasicAttackAttempts}, hits={survival.ConfirmedBasicHits}, " +
                    $"abilities={survival.AbilityCasts}, AOE={survival.AreaAbilityCasts}, " +
                    $"statuses={survival.ActiveStatusEffects}, commands={survival.CommanderDecisions}.");
            }
        }

        private static void Observe(string key, bool condition)
        {
            if (condition) SessionState.SetBool(key, true);
        }

        private static void SetBool(string key, bool value)
        {
            SessionState.SetBool(key, value);
        }

        private static void Complete(int exitCode, string message)
        {
            if (exitCode == 0) Debug.Log(message);
            else Debug.LogError(message);
            Directory.CreateDirectory(Path.GetDirectoryName(ResultPath) ?? ProjectRoot);
            File.WriteAllLines(ResultPath, new[]
            {
                exitCode == 0 ? "PASS" : "FAIL",
                message,
                $"Screenshot: {ScreenshotRelativePath}"
            });
            SessionState.SetInt(ExitCodeKey, exitCode);
            SessionState.SetBool(FinishedKey, true);
            Time.timeScale = 1f;
            EditorApplication.isPlaying = false;
        }

        private static void TryStartRequestedRun()
        {
            if (EditorApplication.timeSinceStartup < readyToStartAt ||
                !File.Exists(RequestPath) || EditorApplication.isCompiling ||
                EditorApplication.isUpdating)
            {
                return;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
                return;
            }
            File.Delete(RequestPath);
            if (File.Exists(ResultPath)) File.Delete(ResultPath);
            Run();
        }

        private static void ClearState()
        {
            SessionState.EraseBool(ActiveKey);
            SessionState.EraseBool(FinishedKey);
            SessionState.EraseBool(StressAppliedKey);
            SessionState.EraseBool(SawTwoHundredMonstersKey);
            SessionState.EraseBool(SawAttacksKey);
            SessionState.EraseBool(SawHitsKey);
            SessionState.EraseBool(SawAbilitiesKey);
            SessionState.EraseBool(SawAreaAbilitiesKey);
            SessionState.EraseBool(SawStatusesKey);
            SessionState.EraseBool(ScreenshotQueuedKey);
            SessionState.EraseInt(ExitCodeKey);
        }
    }
}
