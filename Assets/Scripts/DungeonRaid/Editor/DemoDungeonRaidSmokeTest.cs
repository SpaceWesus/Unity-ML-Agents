using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Turtle.DungeonRaid.Editor
{
    /// <summary>
    /// Play Mode smoke test for the authored autonomous slice. It is intentionally
    /// separate from structural validation: physics, AI, combat, looting, and the
    /// completion state must all advance in a running scene.
    /// </summary>
    [InitializeOnLoad]
    public static class DemoDungeonRaidSmokeTest
    {
        private const string ActiveKey = "Turtle.DungeonRaid.Smoke.Active";
        private const string FinishedKey = "Turtle.DungeonRaid.Smoke.Finished";
        private const string ExitCodeKey = "Turtle.DungeonRaid.Smoke.ExitCode";
        private const float MaximumRaidSeconds = 120f;

        static DemoDungeonRaidSmokeTest()
        {
            EditorApplication.update -= Poll;
            EditorApplication.update += Poll;
        }

        [MenuItem("Turtle/Dungeon Raid/Run Autonomous Smoke Test")]
        public static void Run()
        {
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetBool(FinishedKey, false);
            SessionState.SetInt(ExitCodeKey, 1);
            EditorSceneManager.OpenScene(
                DemoDungeonRaidBuilder.ScenePath,
                OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        private static void Poll()
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            if (SessionState.GetBool(FinishedKey, false))
            {
                if (EditorApplication.isPlaying) return;
                var exitCode = SessionState.GetInt(ExitCodeKey, 1);
                SessionState.EraseBool(ActiveKey);
                SessionState.EraseBool(FinishedKey);
                SessionState.EraseInt(ExitCodeKey);
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(exitCode);
                }
                return;
            }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;

            Time.timeScale = 4f;
            var raid = Object.FindFirstObjectByType<DungeonRaidDirector2D>();
            if (raid == null) return;
            if (raid.ResultMessage.StartsWith("RAID COMPLETE"))
            {
                Complete(0,
                    $"Demo Dungeon Play Mode smoke test passed in {raid.RaidTime:0.0} simulated seconds.");
            }
            else if (raid.ResultMessage.StartsWith("RAID FAILED"))
            {
                Complete(1,
                    $"Demo Dungeon Play Mode smoke test failed: {raid.ResultMessage}");
            }
            else if (raid.RaidTime >= MaximumRaidSeconds)
            {
                Complete(1,
                    $"Demo Dungeon Play Mode smoke test timed out after {raid.RaidTime:0.0} simulated seconds. " +
                    $"Last event: {raid.LatestEvent}");
            }
        }

        private static void Complete(int exitCode, string message)
        {
            if (exitCode == 0) Debug.Log(message);
            else Debug.LogError(message);
            SessionState.SetInt(ExitCodeKey, exitCode);
            SessionState.SetBool(FinishedKey, true);
            EditorApplication.isPlaying = false;
        }
    }
}
