using System.IO;
using System.Collections.Generic;
using System.Linq;
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
        private const string SeedAppliedKey = "Turtle.DungeonRaid.Smoke.SeedApplied";
        private const string PlaybackValidatedKey = "Turtle.DungeonRaid.Smoke.PlaybackValidated";
        private const string TemporaryShieldObservedKey =
            "Turtle.DungeonRaid.Smoke.TemporaryShieldObserved";
        private const string ShieldVisualObservedKey =
            "Turtle.DungeonRaid.Smoke.ShieldVisualObserved";
        private const string BossEngagedKey = "Turtle.DungeonRaid.Smoke.BossEngaged";
        private const string BossDealtDamageKey = "Turtle.DungeonRaid.Smoke.BossDealtDamage";
        private const string CooldownObservedKey = "Turtle.DungeonRaid.Smoke.CooldownObserved";
        private const string StatusObservedKey = "Turtle.DungeonRaid.Smoke.StatusObserved";
        private const string ScreenshotQueuedKey = "Turtle.DungeonRaid.Smoke.ScreenshotQueued";
        private const string RequestRelativePath =
            "Temp/CodexValidation/demo-dungeon-smoke.request";
        private const string ResultRelativePath =
            "Temp/CodexValidation/demo-dungeon-smoke.result";
        private const string ScreenshotRelativePath =
            "Temp/CodexValidation/demo-dungeon-indicators.png";
        private const float MaximumRaidSeconds = 180f;
        // Regression seed containing a large room obstacle between the party and
        // its next route waypoint. This keeps the smoke test reproducible and
        // protects local wall-following instead of relying on a lucky random map.
        private const int SmokeSeed = 1311095377;
        private static double readyToStartAt;
        private static RaidAgent2D observedBoss;
        private static readonly HashSet<int> SubscribedHunterIds = new();
        private static readonly List<RaidStatusEffectSnapshot> StatusSnapshots = new(8);

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

        static DemoDungeonRaidSmokeTest()
        {
            readyToStartAt = EditorApplication.timeSinceStartup + 2d;
            EditorApplication.update -= Poll;
            EditorApplication.update += Poll;
        }

        [MenuItem("Turtle/Dungeon Raid/Run Autonomous Smoke Test")]
        public static void Run()
        {
            Application.runInBackground = true;
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetBool(FinishedKey, false);
            SessionState.SetBool(SeedAppliedKey, false);
            SessionState.SetBool(PlaybackValidatedKey, false);
            SessionState.SetBool(TemporaryShieldObservedKey, false);
            SessionState.SetBool(ShieldVisualObservedKey, false);
            SessionState.SetBool(BossEngagedKey, false);
            SessionState.SetBool(BossDealtDamageKey, false);
            SessionState.SetBool(CooldownObservedKey, false);
            SessionState.SetBool(StatusObservedKey, false);
            SessionState.SetBool(ScreenshotQueuedKey, false);
            SessionState.SetInt(ExitCodeKey, 1);
            observedBoss = null;
            SubscribedHunterIds.Clear();
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
                SessionState.EraseBool(SeedAppliedKey);
                SessionState.EraseBool(PlaybackValidatedKey);
                SessionState.EraseBool(TemporaryShieldObservedKey);
                SessionState.EraseBool(ShieldVisualObservedKey);
                SessionState.EraseBool(BossEngagedKey);
                SessionState.EraseBool(BossDealtDamageKey);
                SessionState.EraseBool(CooldownObservedKey);
                SessionState.EraseBool(StatusObservedKey);
                SessionState.EraseBool(ScreenshotQueuedKey);
                SessionState.EraseInt(ExitCodeKey);
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(exitCode);
                }
                return;
            }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;

            Application.runInBackground = true;
            var playback = Object.FindFirstObjectByType<RaidPlaybackController2D>();
            var raid = Object.FindFirstObjectByType<DungeonRaidDirector2D>();
            if (playback == null || raid == null) return;
            if (!SessionState.GetBool(PlaybackValidatedKey, false))
            {
                if (!Mathf.Approximately(playback.PlaybackSpeed, 0.25f) ||
                    !Mathf.Approximately(Time.timeScale, 0.25f))
                {
                    Complete(1,
                        $"Demo Dungeon did not start at the authored 0.25x playback speed " +
                        $"(controller={playback.PlaybackSpeed:0.00}, global={Time.timeScale:0.00}).");
                    return;
                }
                if (raid.Hunters.Any(hunter =>
                        hunter.CurrentShield > 0f || hunter.IsShieldVisualVisible))
                {
                    Complete(1,
                        "One or more hunters spawned with innate shield HP or a visible shield renderer.");
                    return;
                }
                SessionState.SetBool(PlaybackValidatedKey, true);
            }
            playback.SetPlaybackSpeed(4f);
            var generator = Object.FindFirstObjectByType<DungeonRoomFirstGenerator2D>();
            if (!SessionState.GetBool(SeedAppliedKey, false))
            {
                if (generator == null) return;
                generator.GenerateFromStoredGateSeed(SmokeSeed);
                SessionState.SetBool(SeedAppliedKey, true);
            }
            if (raid.Hunters.Any(hunter =>
                    hunter.IsShieldVisualVisible && !hunter.HasTemporaryShield))
            {
                Complete(1,
                    "A hunter displayed the shield renderer without an active temporary shield.");
                return;
            }
            if (raid.Hunters.Any(hunter => hunter.HasTemporaryShield))
            {
                SessionState.SetBool(TemporaryShieldObservedKey, true);
            }
            if (raid.Hunters.Any(hunter =>
                    hunter.HasTemporaryShield && hunter.IsShieldVisualVisible))
            {
                SessionState.SetBool(ShieldVisualObservedKey, true);
            }
            ObserveIndicatorData(raid);
            var bossPod = raid.EnemyPods.FirstOrDefault(pod =>
                pod != null && pod.DisplayName == "Goblin Warlord");
            ObserveBossCombat(raid, bossPod);
            if (raid.ResultMessage.StartsWith("RAID COMPLETE"))
            {
                if (!SessionState.GetBool(TemporaryShieldObservedKey, false) ||
                    !SessionState.GetBool(ShieldVisualObservedKey, false))
                {
                    Complete(1,
                        "The raid completed without observing both the Tanker's temporary Bulwark state and its conditional shield renderer.");
                    return;
                }
                var boss = bossPod?.Members.FirstOrDefault();
                if (bossPod == null || boss == null || !bossPod.IsDefeated ||
                    boss.LifeState != RaidLifeState.Dead || boss.CurrentHealth > 0f)
                {
                    Complete(1,
                        $"The raid completed before the boss was actually killed. " +
                        $"Pod={bossPod?.Phase.ToString() ?? "missing"}, " +
                        $"life={boss?.LifeState.ToString() ?? "missing"}, " +
                        $"health={boss?.CurrentHealth.ToString("0.0") ?? "missing"}.");
                    return;
                }
                if (!SessionState.GetBool(BossEngagedKey, false) ||
                    !SessionState.GetBool(BossDealtDamageKey, false))
                {
                    Complete(1,
                        "The boss did not both enter combat and land an attack before raid completion.");
                    return;
                }
                if (!SessionState.GetBool(CooldownObservedKey, false) ||
                    !SessionState.GetBool(StatusObservedKey, false))
                {
                    Complete(1,
                        "The raid completed without exposing both a live ability cooldown and an active status effect to the HUD projection.");
                    return;
                }
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
                var centroid = raid.PartyCentroid();
                var pod = raid.Party?.CurrentPod;
                var currentRoom = raid.FindRoom(centroid);
                var target = pod?.ActivationCenter ?? centroid;
                var waypoint = pod != null ? raid.GetAdvanceWaypoint(centroid, pod) : target;
                var navigation = generator?.Navigation;
                var navigationPath = new List<Vector2>();
                var hasNavigationPath = navigation != null &&
                                        navigation.TryFindPath(centroid, waypoint, navigationPath);
                var hunterStates = string.Join("; ", raid.Hunters.Select(hunter =>
                    DescribeHunter(hunter, navigation, waypoint)));
                Complete(1,
                    $"Demo Dungeon Play Mode smoke test timed out after {raid.RaidTime:0.0} simulated seconds. " +
                    $"Seed: {generator?.CurrentSeed.ToString() ?? "unknown"}. " +
                    $"Party: {centroid}; room: {currentRoom?.RoomId ?? "corridor"}; " +
                    $"waypoint: {waypoint}; target: {target}; " +
                    $"distance: {Vector2.Distance(centroid, target):0.00}. " +
                    $"Navigation ready: {navigation?.IsReady.ToString() ?? "missing"}; " +
                    $"centroid walkable: {navigation?.IsWalkable(centroid).ToString() ?? "unknown"}; " +
                    $"waypoint walkable: {navigation?.IsWalkable(waypoint).ToString() ?? "unknown"}; " +
                    $"path to waypoint: {hasNavigationPath} ({navigationPath.Count} points). " +
                    $"Hunters: [{hunterStates}]. " +
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
            var raid = Object.FindFirstObjectByType<DungeonRaidDirector2D>();
            if (raid != null)
            {
                for (var index = 0; index < raid.Hunters.Count; index++)
                {
                    raid.Hunters[index].Damaged -= OnHunterDamaged;
                }
            }
            var playback = Object.FindFirstObjectByType<RaidPlaybackController2D>();
            if (playback != null) playback.SetPlaybackSpeed(1f);
            else Time.timeScale = 1f;
            EditorApplication.isPlaying = false;
        }

        private static void ObserveBossCombat(
            DungeonRaidDirector2D raid,
            RaidEnemyPodBrain2D bossPod)
        {
            observedBoss = bossPod?.Members.FirstOrDefault();
            if (bossPod?.Phase == RaidPodPhase.Engaging)
            {
                SessionState.SetBool(BossEngagedKey, true);
            }
            for (var index = 0; index < raid.Hunters.Count; index++)
            {
                var hunter = raid.Hunters[index];
                if (hunter != null && SubscribedHunterIds.Add(hunter.GetInstanceID()))
                {
                    hunter.Damaged += OnHunterDamaged;
                }
            }
        }

        private static void OnHunterDamaged(
            RaidAgent2D target,
            RaidAgent2D source,
            float amount)
        {
            if (source == observedBoss && amount > 0f)
            {
                SessionState.SetBool(BossDealtDamageKey, true);
            }
        }

        private static void ObserveIndicatorData(DungeonRaidDirector2D raid)
        {
            var cooldownObserved = SessionState.GetBool(CooldownObservedKey, false);
            var statusObserved = SessionState.GetBool(StatusObservedKey, false);
            var agents = raid.Hunters.Concat(
                raid.EnemyPods
                    .Where(pod => pod != null)
                    .SelectMany(pod => pod.Members));
            foreach (var agent in agents)
            {
                if (agent == null || agent.LifeState == RaidLifeState.Dead) continue;
                if (!statusObserved)
                {
                    agent.CollectActiveStatusEffects(raid.RaidTime, StatusSnapshots);
                    statusObserved = StatusSnapshots.Count > 0;
                }
                if (!cooldownObserved)
                {
                    cooldownObserved = agent.Abilities.Any(ability =>
                        agent.GetAbilityAvailability(ability, raid.RaidTime) ==
                        RaidAbilityAvailability.Cooldown);
                }
                if (cooldownObserved && statusObserved) break;
            }
            SessionState.SetBool(CooldownObservedKey, cooldownObserved);
            SessionState.SetBool(StatusObservedKey, statusObserved);
            if (!cooldownObserved || !statusObserved ||
                SessionState.GetBool(ScreenshotQueuedKey, false)) return;

            Directory.CreateDirectory(Path.GetDirectoryName(ScreenshotPath) ?? ProjectRoot);
            ScreenCapture.CaptureScreenshot(ScreenshotPath);
            SessionState.SetBool(ScreenshotQueuedKey, true);
        }

        private static string DescribeHunter(
            RaidAgent2D hunter,
            DungeonNavigationGrid2D navigation,
            Vector2 waypoint)
        {
            if (hunter == null) return "missing";
            var path = new List<Vector2>();
            var hasPath = navigation != null &&
                          navigation.TryFindPath(hunter.Position, waypoint, path);
            var steeringPoint = path.Count > 1 ? path[1] : waypoint;
            var blocker = DescribeForwardBlocker(hunter, navigation, steeringPoint);
            return $"{hunter.DisplayName}@{hunter.Position}/{hunter.LifeState}/" +
                   $"nav:{hunter.Navigation == navigation}/" +
                   $"walk:{navigation?.IsWalkable(hunter.Position).ToString() ?? "unknown"}/" +
                   $"path:{hasPath}:{path.Count}:next:{steeringPoint}/block:{blocker}";
        }

        private static string DescribeForwardBlocker(
            RaidAgent2D hunter,
            DungeonNavigationGrid2D navigation,
            Vector2 waypoint)
        {
            var offset = waypoint - hunter.Position;
            if (offset.sqrMagnitude <= 0.001f) return "at-target";
            var hits = Physics2D.CircleCastAll(
                hunter.Position,
                hunter.CollisionRadius,
                offset.normalized,
                1.25f);
            for (var index = 0; index < hits.Length; index++)
            {
                var collider = hits[index].collider;
                if (collider == null || collider.isTrigger ||
                    collider.GetComponentInParent<RaidAgent2D>() != null) continue;
                var center = (Vector2)collider.transform.TransformPoint(
                    collider is BoxCollider2D box ? box.offset : Vector2.zero);
                var belongsToBake = navigation?.GeometryRoot != null &&
                                    collider.transform.IsChildOf(navigation.GeometryRoot);
                return $"{collider.name}@{hits[index].distance:0.00}/" +
                       $"center:{center}/centerWalk:{navigation?.IsWalkable(center).ToString() ?? "unknown"}/" +
                       $"bakedRoot:{belongsToBake}";
            }
            return "none";
        }

        private static void TryStartRequestedRun()
        {
            if (EditorApplication.timeSinceStartup < readyToStartAt ||
                !File.Exists(RequestPath) || EditorApplication.isCompiling ||
                EditorApplication.isUpdating)
            {
                return;
            }
            // A domain reload can strand an earlier requested run in Play Mode
            // before its SessionState flags are restored. Retain the request,
            // exit that stale run, and start cleanly on the next editor update.
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
