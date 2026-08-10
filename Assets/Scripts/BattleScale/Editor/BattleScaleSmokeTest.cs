using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Turtle.BattleScale.Editor
{
    /// <summary>
    /// Verifies that the scale lab deploys real agents, issues hierarchical
    /// commands, and produces confirmed cast-to-hurtbox combat in Play Mode.
    /// </summary>
    [InitializeOnLoad]
    public static class BattleScaleSmokeTest
    {
        private const string ActiveKey = "Turtle.BattleScale.Smoke.Active";
        private const string FinishedKey = "Turtle.BattleScale.Smoke.Finished";
        private const string ExitCodeKey = "Turtle.BattleScale.Smoke.ExitCode";
        private const string RequestRelativePath =
            "Temp/CodexValidation/battle-scale-smoke.request";
        private const string ResultRelativePath =
            "Temp/CodexValidation/battle-scale-smoke.result";
        private const float MaximumBattleSeconds = 35f;
        private static double readyToStartAt;

        private static string ProjectRoot => Directory.GetParent(Application.dataPath)?.FullName
                                             ?? Directory.GetCurrentDirectory();
        private static string RequestPath => Path.Combine(
            ProjectRoot,
            RequestRelativePath.Replace('/', Path.DirectorySeparatorChar));
        private static string ResultPath => Path.Combine(
            ProjectRoot,
            ResultRelativePath.Replace('/', Path.DirectorySeparatorChar));

        static BattleScaleSmokeTest()
        {
            readyToStartAt = EditorApplication.timeSinceStartup + 2d;
            EditorApplication.update -= Poll;
            EditorApplication.update += Poll;
        }

        [MenuItem("Turtle/Battle Scale/Run 100v100 Smoke Test")]
        public static void Run()
        {
            Application.runInBackground = true;
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetBool(FinishedKey, false);
            SessionState.SetInt(ExitCodeKey, 1);
            EditorSceneManager.OpenScene(BattleScaleTestBuilder.ScenePath, OpenSceneMode.Single);
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
                SessionState.EraseBool(ActiveKey);
                SessionState.EraseBool(FinishedKey);
                SessionState.EraseInt(ExitCodeKey);
                if (Application.isBatchMode) EditorApplication.Exit(exitCode);
                return;
            }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;

            Application.runInBackground = true;
            Time.timeScale = 4f;
            var simulation = Object.FindFirstObjectByType<BattleScaleSimulation2D>();
            if (simulation == null || simulation.BattleTime < 0.25f) return;
            if (!simulation.IsConfigured)
            {
                Complete(1, "Battle Scale Test entered Play Mode without all required references.");
                return;
            }
            if (simulation.ActiveUnitCount != 200 || simulation.ActiveSquadCount != 20 ||
                simulation.ActiveSergeantCount != 20)
            {
                Complete(1,
                    $"Expected 200 agents, 20 squads, and 20 active sergeants; observed " +
                    $"{simulation.ActiveUnitCount}, {simulation.ActiveSquadCount}, and " +
                    $"{simulation.ActiveSergeantCount}.");
                return;
            }
            if (simulation.AttackAttempts > 0 && simulation.ConfirmedHits > 0 &&
                simulation.CommanderDecisions >= 4)
            {
                Complete(0,
                    $"Battle Scale Test Play Mode smoke passed at 100v100 after " +
                    $"{simulation.BattleTime:0.0} simulated seconds: " +
                    $"{simulation.AttackAttempts} attacks, {simulation.ConfirmedHits} confirmed hits, " +
                    $"{simulation.CommanderDecisions} commander decisions.");
                return;
            }
            if (simulation.BattleTime >= MaximumBattleSeconds)
            {
                Complete(1,
                    $"Battle Scale Test timed out at {simulation.BattleTime:0.0}s with " +
                    $"{simulation.AttackAttempts} attacks, {simulation.ConfirmedHits} hits, and " +
                    $"{simulation.CommanderDecisions} commander decisions. " +
                    $"Living: {simulation.LivingAzure} Azure / {simulation.LivingCrimson} Crimson.");
            }
        }

        private static void Complete(int exitCode, string message)
        {
            if (exitCode == 0) Debug.Log(message);
            else Debug.LogError(message);
            Directory.CreateDirectory(Path.GetDirectoryName(ResultPath) ?? ProjectRoot);
            File.WriteAllLines(ResultPath, new[]
            {
                exitCode == 0 ? "PASS" : "FAIL",
                message
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
    }
}
