using System.IO;
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
        private const string RequestRelativePath =
            "Temp/CodexValidation/demo-dungeon-smoke.request";
        private const string ResultRelativePath =
            "Temp/CodexValidation/demo-dungeon-smoke.result";
        private const float MaximumRaidSeconds = 180f;

        private static string ProjectRoot => Directory.GetParent(Application.dataPath)?.FullName
                                             ?? Directory.GetCurrentDirectory();
        private static string RequestPath => Path.Combine(
            ProjectRoot,
            RequestRelativePath.Replace('/', Path.DirectorySeparatorChar));
        private static string ResultPath => Path.Combine(
            ProjectRoot,
            ResultRelativePath.Replace('/', Path.DirectorySeparatorChar));

        static DemoDungeonRaidSmokeTest()
        {
            EditorApplication.update -= Poll;
            EditorApplication.update += Poll;
        }

        [MenuItem("Turtle/Dungeon Raid/Run Autonomous Smoke Test")]
        public static void Run()
        {
            Application.runInBackground = true;
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
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(exitCode);
                }
                return;
            }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;

            Application.runInBackground = true;
            Time.timeScale = 4f;
            var raid = Object.FindFirstObjectByType<DungeonRaidDirector2D>();
            if (raid == null) return;
            if (raid.ResultMessage.StartsWith("RAID COMPLETE"))
            {
                var generator = Object.FindFirstObjectByType<DungeonRoomFirstGenerator2D>();
                Complete(0,
                    $"Demo Dungeon Play Mode smoke test passed in {raid.RaidTime:0.0} simulated seconds " +
                    $"on generated seed {generator?.CurrentSeed.ToString() ?? "unknown"}.");
            }
            else if (raid.ResultMessage.StartsWith("RAID FAILED"))
            {
                Complete(1,
                    $"Demo Dungeon Play Mode smoke test failed: {raid.ResultMessage}");
            }
            else if (raid.RaidTime >= MaximumRaidSeconds)
            {
                var generator = Object.FindFirstObjectByType<DungeonRoomFirstGenerator2D>();
                var centroid = raid.PartyCentroid();
                var pod = raid.Party?.CurrentPod;
                var currentRoom = raid.FindRoom(centroid);
                var target = pod?.ActivationCenter ?? centroid;
                var waypoint = pod != null ? raid.GetAdvanceWaypoint(centroid, pod) : target;
                Complete(1,
                    $"Demo Dungeon Play Mode smoke test timed out after {raid.RaidTime:0.0} simulated seconds. " +
                    $"Seed: {generator?.CurrentSeed.ToString() ?? "unknown"}. " +
                    $"Party: {centroid}; room: {currentRoom?.RoomId ?? "corridor"}; " +
                    $"waypoint: {waypoint}; target: {target}; " +
                    $"distance: {Vector2.Distance(centroid, target):0.00}. " +
                    $"Last event: {raid.LatestEvent}");
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
            if (!File.Exists(RequestPath) || EditorApplication.isCompiling ||
                EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }
            File.Delete(RequestPath);
            if (File.Exists(ResultPath)) File.Delete(ResultPath);
            Run();
        }
    }
}
