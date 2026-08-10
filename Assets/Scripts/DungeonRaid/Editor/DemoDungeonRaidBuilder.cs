using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Turtle.DungeonRaid.Editor
{
    public static class DemoDungeonRaidBuilder
    {
        // The editor tool saves a visible seeded preview. The demo intentionally
        // rolls a new seed in Play Mode; production gates will supply their stored
        // map seed to the same deterministic generator.
        public const string ScenePath = "Assets/Scenes/Demo Dungeon.unity";
        private const int DefaultPreviewSeed = 731245;
        private const float DefaultPlaybackSpeed = 0.25f;
        private const string RequestRelativePath =
            "Temp/CodexValidation/setup-demo-dungeon.request";
        private const string ResultRelativePath =
            "Temp/CodexValidation/setup-demo-dungeon.result";

        private sealed class AgentProfile
        {
            public RaidCombatRole Role;
            public float Health;
            public float Mana;
            public float ManaRegeneration;
            public float Speed;
            public float Damage;
            public float AttackRange;
            public float PreferredRange;
            public float AttackCooldown;
            public bool Ranged;
            public Color Color;
            public List<RaidAbilitySpec> Abilities = new();
        }

        private sealed class GreyboxExpansion
        {
            public readonly List<RaidRoom2D> Rooms = new();
            public readonly List<RaidRoomConnection2D> Connections = new();
        }

        private static string ProjectRoot => Directory.GetParent(Application.dataPath)?.FullName
                                             ?? Directory.GetCurrentDirectory();
        private static string RequestPath => Path.Combine(
            ProjectRoot,
            RequestRelativePath.Replace('/', Path.DirectorySeparatorChar));
        private static string ResultPath => Path.Combine(
            ProjectRoot,
            ResultRelativePath.Replace('/', Path.DirectorySeparatorChar));

        [InitializeOnLoadMethod]
        private static void ScheduleRequestedSetup()
        {
            EditorApplication.delayCall += RunRequestedSetup;
            EditorApplication.update -= RunRequestedSetup;
            EditorApplication.update += RunRequestedSetup;
        }

        private static void RunRequestedSetup()
        {
            // Requests are intentionally one-shot so normal editor updates do no polling work.
            if (!File.Exists(RequestPath))
            {
                EditorApplication.update -= RunRequestedSetup;
                return;
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            EditorApplication.update -= RunRequestedSetup;
            File.Delete(RequestPath);
            try
            {
                SetupSceneInternal();
                var failures = ValidateSceneInternal();
                Directory.CreateDirectory(Path.GetDirectoryName(ResultPath) ?? ProjectRoot);
                File.WriteAllLines(ResultPath, failures.Count == 0
                    ? new[] { "PASS", "Demo Dungeon autonomous raid setup passed validation." }
                    : new[] { "FAIL" }.Concat(failures).ToArray());
                if (failures.Count == 0)
                {
                    Debug.Log("Demo Dungeon autonomous raid setup and validation passed.");
                }
                else
                {
                    Debug.LogError("Demo Dungeon validation failed:\n- " + string.Join("\n- ", failures));
                }
            }
            catch (Exception exception)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ResultPath) ?? ProjectRoot);
                File.WriteAllLines(ResultPath, new[] { "ERROR", exception.ToString() });
                Debug.LogException(exception);
            }
        }

        [MenuItem("Turtle/Dungeon Raid/Setup Demo Dungeon Prototype")]
        public static void SetupScene()
        {
            SetupSceneInternal();
        }

        [MenuItem("Turtle/Dungeon Raid/Validate Demo Dungeon Prototype")]
        public static void ValidateScene()
        {
            var failures = ValidateSceneInternal();
            if (failures.Count == 0)
            {
                Debug.Log("Demo Dungeon autonomous raid validation passed.");
            }
            else
            {
                Debug.LogError("Demo Dungeon validation failed:\n- " + string.Join("\n- ", failures));
            }
        }

        private static void SetupSceneInternal()
        {
            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            if (sceneAsset == null)
            {
                throw new InvalidOperationException($"Scene not found at {ScenePath}.");
            }

            var scene = SceneManager.GetSceneByPath(ScenePath);
            var openedForSetup = !scene.isLoaded;
            if (openedForSetup)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            }

            try
            {
                var dungeonTiles = FindRoot(scene, "Dungeon Tiles");
                var hunterRoot = FindRoot(scene, "Local Hunters");
                var monsterRoot = FindRoot(scene, "Monsters and Bosses");
                var cameraObject = FindRoot(scene, "Main Camera");
                if (dungeonTiles == null || hunterRoot == null || monsterRoot == null || cameraObject == null)
                {
                    throw new InvalidOperationException(
                        "Demo Dungeon must retain Dungeon Tiles, Local Hunters, Monsters and Bosses, and Main Camera roots.");
                }

                var spawnRoomObject = FindChild(dungeonTiles.transform, "Room 1 - Spawn Room");
                var mobRoomObject = FindChild(dungeonTiles.transform, "Room 2 - Mob Den #1");
                var squadObject = FindChild(monsterRoot.transform, "Squad 1");
                if (spawnRoomObject == null || mobRoomObject == null || squadObject == null)
                {
                    throw new InvalidOperationException(
                        "Demo Dungeon must retain its two authored rooms and Squad 1 hierarchy.");
                }

                var spawnRoom = GetOrAddSingle<RaidRoom2D>(spawnRoomObject);
                var mobRoom = GetOrAddSingle<RaidRoom2D>(mobRoomObject);
                spawnRoom.ConfigureEditor(
                    "spawn-room",
                    0,
                    ResolveRoomSize(spawnRoomObject),
                    RaidRoomPurpose.Entrance);
                mobRoom.ConfigureEditor(
                    "mob-den-01",
                    1,
                    ResolveRoomSize(mobRoomObject),
                    RaidRoomPurpose.Encounter);
                MarkDirty(spawnRoom, mobRoom);

                var connectionObject = FindChild(dungeonTiles.transform, "Connection - Spawn to Mob Den");
                if (connectionObject == null)
                {
                    connectionObject = new GameObject("Connection - Spawn to Mob Den");
                    Undo.RegisterCreatedObjectUndo(connectionObject, "Create dungeon room connection");
                    connectionObject.transform.SetParent(dungeonTiles.transform, false);
                }
                connectionObject.transform.position = new Vector3(10f, 0f, 0f);
                var connection = GetOrAddSingle<RaidRoomConnection2D>(connectionObject);
                connection.ConfigureEditor(spawnRoom, mobRoom, 4f);
                MarkDirty(connection);

                var floorSprite = FindRenderer(spawnRoomObject, "Floor")?.sprite;
                var wallSprite = spawnRoomObject.GetComponentsInChildren<SpriteRenderer>(true)
                    .FirstOrDefault(renderer => IsWall(renderer.name))?.sprite;
                if (floorSprite == null || wallSprite == null)
                {
                    throw new InvalidOperationException(
                        "The authored spawn room must retain reusable Floor and Wall sprites.");
                }
                ConfigureGreyboxExpansion(
                    dungeonTiles.transform,
                    mobRoom,
                    floorSprite,
                    wallSprite);
                ConfigureWorldColliders(dungeonTiles.transform);

                var authoredRooms = dungeonTiles
                    .GetComponentsInChildren<RaidRoom2D>(true)
                    .OrderBy(room => room.Sequence)
                    .ThenBy(room => room.RoomId, StringComparer.Ordinal)
                    .ToList();
                var authoredConnections = dungeonTiles
                    .GetComponentsInChildren<RaidRoomConnection2D>(true)
                    .OrderBy(candidate => candidate.FromRoom?.Sequence ?? int.MaxValue)
                    .ThenBy(candidate => candidate.ToRoom?.Sequence ?? int.MaxValue)
                    .ToList();

                var squadTwoObject = FindChild(monsterRoot.transform, "Squad 2");
                var squadThreeObject = FindChild(monsterRoot.transform, "Squad 3");
                var bossObject = FindChild(monsterRoot.transform, "Boss");
                if (squadTwoObject == null || squadThreeObject == null || bossObject == null)
                {
                    throw new InvalidOperationException(
                        "Monsters and Bosses must retain Squad 2, Squad 3, and Boss fixture roots.");
                }

                var monsterTemplate = squadObject.GetComponentsInChildren<SpriteRenderer>(true)
                    .FirstOrDefault(renderer => renderer.transform.parent == squadObject.transform);
                if (monsterTemplate == null || monsterTemplate.sprite == null)
                {
                    throw new InvalidOperationException(
                        "Squad 1 must retain at least one visible monster to seed the additional test rosters.");
                }

                EnsureMonsterRoster(squadTwoObject.transform, monsterTemplate, new[]
                {
                    "Goblin Sergeant - Squad 2",
                    "Goblin - Bow Squad 2A",
                    "Goblin - Bow Squad 2B",
                    "Goblin - Sword Squad 2A",
                    "Goblin - Sword Squad 2B",
                    "Goblin - Sword Squad 2C"
                });
                EnsureMonsterRoster(squadThreeObject.transform, monsterTemplate, new[]
                {
                    "Goblin Sergeant - Squad 3",
                    "Goblin - Bow Squad 3A",
                    "Goblin - Bow Squad 3B",
                    "Goblin - Sword Squad 3A",
                    "Goblin - Sword Squad 3B",
                    "Goblin - Sword Squad 3C",
                    "Goblin - Sword Squad 3D"
                });
                EnsureMonsterRoster(bossObject.transform, monsterTemplate, new[]
                {
                    "Goblin Warlord - Boss"
                }, 1.35f);

                var hunters = ConfigureHunters(hunterRoot.transform);
                var firstMonsters = ConfigureMonsters(squadObject.transform, "goblin-pod-01");
                var secondMonsters = ConfigureMonsters(squadTwoObject.transform, "goblin-pod-02");
                var thirdMonsters = ConfigureMonsters(squadThreeObject.transform, "goblin-pod-03");
                var bossMonsters = ConfigureMonsters(bossObject.transform, "goblin-boss");
                var monsters = firstMonsters
                    .Concat(secondMonsters)
                    .Concat(thirdMonsters)
                    .Concat(bossMonsters)
                    .ToList();
                var party = GetOrAddSingle<RaidPartyBrain2D>(hunterRoot);
                party.ConfigureEditor(hunters);
                var firstPod = GetOrAddSingle<RaidEnemyPodBrain2D>(squadObject);
                var secondPod = GetOrAddSingle<RaidEnemyPodBrain2D>(squadTwoObject);
                var thirdPod = GetOrAddSingle<RaidEnemyPodBrain2D>(squadThreeObject);
                var bossPod = GetOrAddSingle<RaidEnemyPodBrain2D>(bossObject);
                firstPod.ConfigureEditor(
                    "goblin-pod-01", "Goblin Patrol", 0, mobRoom, firstMonsters, 9.5f);
                secondPod.ConfigureEditor(
                    "goblin-pod-02", "Goblin Warband", 1, mobRoom, secondMonsters, 10.5f);
                thirdPod.ConfigureEditor(
                    "goblin-pod-03", "Goblin Honor Guard", 2, mobRoom, thirdMonsters, 11.5f);
                bossPod.ConfigureEditor(
                    "goblin-boss", "Goblin Warlord", 3, mobRoom, bossMonsters, 13f);
                var encounterPods = new[] { firstPod, secondPod, thirdPod };
                var allPods = encounterPods.Append(bossPod).ToArray();
                MarkDirty(party, firstPod, secondPod, thirdPod, bossPod);

                var chestObject = FindChild(mobRoomObject.transform, "Chest - Tier 1") ??
                                  scene.GetRootGameObjects()
                                      .SelectMany(root => root.GetComponentsInChildren<RaidChest2D>(true))
                                      .Select(candidate => candidate.gameObject)
                                      .FirstOrDefault();
                if (chestObject == null)
                {
                    throw new InvalidOperationException("The authored Tier 1 chest is missing from Room 2.");
                }
                var chest = GetOrAddSingle<RaidChest2D>(chestObject);
                var chestCollider = GetOrAdd<BoxCollider2D>(chestObject);
                chestCollider.isTrigger = true;
                chest.ConfigureEditor("mob-den-01-chest", 1, firstPod);
                MarkDirty(chest, chestCollider);

                var systemsObject = FindRoot(scene, "Raid Systems");
                if (systemsObject == null)
                {
                    systemsObject = new GameObject("Raid Systems");
                    Undo.RegisterCreatedObjectUndo(systemsObject, "Create raid systems root");
                    SceneManager.MoveGameObjectToScene(systemsObject, scene);
                }
                var director = GetOrAddSingle<DungeonRaidDirector2D>(systemsObject);
                var hud = GetOrAddSingle<RaidHud2D>(systemsObject);
                var playback = GetOrAddSingle<RaidPlaybackController2D>(systemsObject);
                playback.ConfigureEditor(DefaultPlaybackSpeed, true);
                var fxObject = FindChild(systemsObject.transform, "Raid FX Pool");
                if (fxObject == null)
                {
                    fxObject = new GameObject("Raid FX Pool");
                    Undo.RegisterCreatedObjectUndo(fxObject, "Create raid FX pool");
                    fxObject.transform.SetParent(systemsObject.transform, false);
                }
                var fx = GetOrAddSingle<RaidFxPool2D>(fxObject);
                var effectSprite = hunters.FirstOrDefault()?.GetComponent<SpriteRenderer>()?.sprite ??
                                   monsters.FirstOrDefault()?.GetComponent<SpriteRenderer>()?.sprite;
                fx.ConfigureEditor(effectSprite);

                var camera = cameraObject.GetComponent<Camera>();
                if (camera == null)
                {
                    throw new InvalidOperationException("Main Camera is missing its Camera component.");
                }
                Undo.RecordObject(camera, "Configure autonomous raid camera");
                Undo.RecordObject(cameraObject.transform, "Position autonomous raid camera");
                camera.orthographic = true;
                camera.orthographicSize = 11f;
                camera.backgroundColor = new Color(0.035f, 0.055f, 0.075f, 1f);
                cameraObject.transform.position = new Vector3(10f, 0f, -10f);
                var raidCamera = GetOrAddSingle<RaidCamera2D>(cameraObject);
                raidCamera.ConfigureEditor(director, camera);
                hud.ConfigureEditor(director);
                director.ConfigureEditor(
                    party,
                    allPods,
                    authoredRooms.ToArray(),
                    authoredConnections.ToArray(),
                    new[] { chest },
                    fx,
                    raidCamera,
                    hud);

                var interactiveObjects = EnsureChild(dungeonTiles.transform, "Interactive Objects");
                chestObject.transform.SetParent(interactiveObjects.transform, true);
                var navigation = GetOrAddSingle<DungeonNavigationGrid2D>(dungeonTiles);
                var generator = GetOrAddSingle<DungeonRoomFirstGenerator2D>(dungeonTiles);
                generator.ConfigureEditor(
                    DefaultPreviewSeed,
                    true,
                    director,
                    party,
                    encounterPods,
                    bossPod,
                    chest,
                    floorSprite,
                    wallSprite);
                generator.Generate(DefaultPreviewSeed);

                spawnRoomObject.SetActive(false);
                mobRoomObject.SetActive(false);
                connectionObject.SetActive(false);
                var legacyExpansion = FindChild(dungeonTiles.transform, "Greybox Room Kit");
                if (legacyExpansion != null) legacyExpansion.SetActive(false);
                MarkDirty(director, hud, playback, fx, raidCamera, camera, generator, navigation,
                    firstPod, secondPod, thirdPod, bossPod, chest);

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                EnsureSceneInBuildSettings();
                AssetDatabase.SaveAssets();
            }
            finally
            {
                if (openedForSetup && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static List<string> ValidateSceneInternal()
        {
            var failures = new List<string>();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                failures.Add($"Missing scene: {ScenePath}");
                return failures;
            }
            var scene = SceneManager.GetSceneByPath(ScenePath);
            var openedForValidation = !scene.isLoaded;
            if (openedForValidation)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            }
            try
            {
                var roots = scene.GetRootGameObjects();
                var director = roots.SelectMany(root =>
                    root.GetComponentsInChildren<DungeonRaidDirector2D>(true)).FirstOrDefault();
                var party = roots.SelectMany(root =>
                    root.GetComponentsInChildren<RaidPartyBrain2D>(true)).FirstOrDefault();
                var pods = roots.SelectMany(root =>
                    root.GetComponentsInChildren<RaidEnemyPodBrain2D>(true)).ToArray();
                var rooms = roots.SelectMany(root =>
                    root.GetComponentsInChildren<RaidRoom2D>(true)).ToArray();
                var connections = roots.SelectMany(root =>
                    root.GetComponentsInChildren<RaidRoomConnection2D>(true)).ToArray();
                var chests = roots.SelectMany(root =>
                    root.GetComponentsInChildren<RaidChest2D>(true)).ToArray();
                var agents = roots.SelectMany(root =>
                    root.GetComponentsInChildren<RaidAgent2D>(true)).ToArray();
                var markers = roots.SelectMany(root =>
                    root.GetComponentsInChildren<RaidSpawnMarker2D>()).ToArray();
                var camera = FindRoot(scene, "Main Camera")?.GetComponent<Camera>();
                var generator = roots.SelectMany(root =>
                    root.GetComponentsInChildren<DungeonRoomFirstGenerator2D>(true)).FirstOrDefault();
                var navigation = roots.SelectMany(root =>
                    root.GetComponentsInChildren<DungeonNavigationGrid2D>(true)).FirstOrDefault();
                var playback = roots.SelectMany(root =>
                    root.GetComponentsInChildren<RaidPlaybackController2D>(true)).FirstOrDefault();

                rooms = roots.SelectMany(root =>
                    root.GetComponentsInChildren<RaidRoom2D>()).ToArray();
                connections = roots.SelectMany(root =>
                    root.GetComponentsInChildren<RaidRoomConnection2D>()).ToArray();

                Check(director != null, "Raid Systems must contain a DungeonRaidDirector2D.", failures);
                Check(playback != null &&
                      Mathf.Approximately(playback.DefaultPlaybackSpeed, DefaultPlaybackSpeed) &&
                      playback.ShowsPlaybackControls,
                    "Raid Systems must provide visible playback controls and start the demo at 0.25x speed.",
                    failures);
                Check(generator != null && generator.RandomizeSeedOnPlay,
                    "Dungeon Tiles must own a room-first generator configured to roll a new demo seed on Play.",
                    failures);
                // The lookup arrays are runtime-only; rebuild them from the
                // serialized generated geometry when validating a reopened scene.
                Check(navigation != null && navigation.RebuildFromGeneratedGeometry() && navigation.IsReady,
                    "The generated dungeon must bake a ready 2D navigation grid from its rooms, corridors, and Collider2D blockers.",
                    failures);
                Check(party != null && party.Members.Count == 6,
                    "The raid party must reference all six authored hunters.", failures);
                Check(pods.Length == 4 &&
                      pods.OrderBy(pod => pod.Order).Select(pod => pod.Members.Count)
                          .SequenceEqual(new[] { 6, 6, 7, 1 }),
                    "The raid must contain three ordered goblin squads (6/6/7) and one final boss.",
                    failures);
                Check(pods.Select(pod => pod.Order).Distinct().Count() == pods.Length &&
                      pods.Max(pod => pod.Order) == 3 &&
                      pods.OrderBy(pod => pod.Order).Last().DisplayName == "Goblin Warlord",
                    "Enemy pod ordering must end with the distinct Goblin Warlord encounter.",
                    failures);
                Check(rooms.Length >= 8,
                    "The generated greybox must contain at least eight active rooms.",
                    failures);
                Check(rooms.Count(room => room.Purpose == RaidRoomPurpose.Entrance) == 1 &&
                      rooms.Count(room => room.Purpose == RaidRoomPurpose.Encounter) >= 2 &&
                      rooms.Count(room => room.Purpose == RaidRoomPurpose.Transition) >= 1 &&
                      rooms.Count(room => room.Purpose == RaidRoomPurpose.Boss) >= 1,
                    "The greybox route must expose the expected semantic room purposes.", failures);
                Check(connections.Length >= rooms.Length - 1 && connections.All(candidate =>
                        candidate.FromRoom != null && candidate.ToRoom != null),
                    "The generated room graph must be connected by valid room connections.", failures);
                Check(connections.All(candidate => candidate.WaypointCount >= 2),
                    "Every generated corridor must expose a multi-point traversal route.", failures);
                Check(rooms.All(room => Mathf.Min(room.Size.x, room.Size.y) >= 18f),
                    "Every active generated room must provide at least 18 world units on its narrow axis for party combat.",
                    failures);
                Check(connections.All(candidate => candidate.Width >= 7f),
                    "Every active generated corridor must be at least seven world units wide.", failures);
                Check(connections.All(candidate =>
                    {
                        var expectedWalls = Mathf.Max(0, (candidate.WaypointCount - 1) * 2);
                        var solidWalls = candidate
                            .GetComponentsInChildren<BoxCollider2D>()
                            .Count(collider => !collider.isTrigger && IsWall(collider.name));
                        return expectedWalls > 0 && solidWalls >= expectedWalls;
                    }),
                    "Every generated corridor segment must have two continuous solid wall rails.",
                    failures);
                Check(markers.Count(marker => marker.Kind == RaidSpawnMarkerKind.Party) >= 1 &&
                      markers.Count(marker => marker.Kind == RaidSpawnMarkerKind.EnemyPod) >= 2 &&
                      markers.Count(marker => marker.Kind == RaidSpawnMarkerKind.Boss) == 1 &&
                      markers.Count(marker => marker.Kind == RaidSpawnMarkerKind.Chest) >= 1 &&
                      markers.Count(marker => marker.Kind == RaidSpawnMarkerKind.ExitPortal) == 1,
                    "The room kit must expose party, encounter, boss, chest, and exit materialization sockets.",
                    failures);
                Check(chests.Length == 1 && !chests[0].IsOpened,
                    "The Tier 1 chest must begin closed.", failures);
                Check(agents.Length == 26 && agents.All(agent =>
                        agent != null && !string.IsNullOrWhiteSpace(agent.AgentId) &&
                        agent.GetComponent<SpriteRenderer>()?.sprite != null),
                    "All six hunters, nineteen goblins, and the Goblin Warlord must be visible raid agents.",
                    failures);
                Check(navigation != null && agents.All(agent => agent.Navigation == navigation),
                    "Every hunter and monster must reference the shared generated 2D navigation grid.", failures);
                var generatedObstacles = navigation?.GeometryRoot != null
                    ? navigation.GeometryRoot.GetComponentsInChildren<BoxCollider2D>()
                        .Where(collider => collider.name.StartsWith(
                            "Obstacle", StringComparison.OrdinalIgnoreCase))
                        .ToArray()
                    : Array.Empty<BoxCollider2D>();
                Check(generatedObstacles.Length > 0 && generatedObstacles.All(collider =>
                        !navigation.IsWalkable(collider.transform.TransformPoint(collider.offset))),
                    "Every generated obstacle center must be excluded from the baked navigation grid.",
                    failures);
                foreach (var collider in generatedObstacles.Where(collider =>
                             navigation.IsWalkable(collider.transform.TransformPoint(collider.offset))))
                {
                    var center = (Vector2)collider.transform.TransformPoint(collider.offset);
                    failures.Add(
                        $"Obstacle '{collider.name}' center {center} remained walkable; " +
                        $"direct geometry block={navigation.IsBlockedByCurrentGeometry(center)}, " +
                        $"size={collider.size}, scale={collider.transform.lossyScale}.");
                }
                Check(agents.All(agent =>
                        agent.GetComponent<Rigidbody2D>() is { bodyType: RigidbodyType2D.Dynamic } body &&
                        Mathf.Approximately(body.gravityScale, 0f) &&
                        agent.GetComponent<CircleCollider2D>() is { isTrigger: false }),
                    "Every raid agent must use a zero-gravity dynamic Rigidbody2D and a solid CircleCollider2D hurtbox.",
                    failures);
                var authoredWalls = roots.SelectMany(root =>
                        root.GetComponentsInChildren<SpriteRenderer>())
                    .Where(renderer => IsWall(renderer.name))
                    .ToArray();
                Check(authoredWalls.Length > 0 && authoredWalls.All(wall =>
                        wall.GetComponent<BoxCollider2D>() is { isTrigger: false }),
                    "Every authored wall sprite must have a solid BoxCollider2D.",
                    failures);
                Check(chests.All(candidate =>
                        candidate.GetComponent<BoxCollider2D>() is { isTrigger: true }),
                    "Every raid chest must expose a trigger collider for physical interaction.",
                    failures);
                Check(agents.Select(agent => agent.AgentId).Distinct(StringComparer.Ordinal).Count() == agents.Length,
                    "Raid agent IDs must be unique inside the encounter.", failures);
                var authoredAbilityIds = agents
                    .Where(agent => agent.Faction == RaidFaction.Hunters)
                    .SelectMany(agent => agent.Abilities)
                    .Where(ability => ability != null)
                    .Select(ability => ability.id)
                    .ToHashSet(StringComparer.Ordinal);
                var requiredAbilityIds = new[]
                {
                    "tank.challenge",
                    "tank.bulwark",
                    "fighter.cleave",
                    "fighter.rallying-strike",
                    "healer.mend",
                    "healer.sanctuary",
                    "mage.elemental-affliction.fire",
                    "mage.fireball",
                    "ranger.piercing-shot",
                    "ranger.hunters-mark",
                    "assassin.shadow-step",
                    "assassin.execute"
                };
                Check(requiredAbilityIds.All(authoredAbilityIds.Contains),
                    "The six-hunter fixture must expose all twelve requested prototype abilities.",
                    failures);
                var unauthorizedShieldOwners = agents
                    .Where(agent => agent.Abilities.Any(ability =>
                        ability != null && ability.effect == RaidAbilityEffect.Shield))
                    .Where(agent => agent.Faction != RaidFaction.Hunters ||
                                    agent.Role != RaidCombatRole.Tank)
                    .Select(agent => agent.DisplayName)
                    .ToArray();
                Check(unauthorizedShieldOwners.Length == 0,
                    "Only the hunter Tanker may own a temporary-shield ability. Invalid owners: " +
                    string.Join(", ", unauthorizedShieldOwners),
                    failures);
                Check(camera != null && camera.orthographic,
                    "Demo Dungeon must use an orthographic top-down camera.", failures);

                var entrance = rooms.FirstOrDefault(room => room.Purpose == RaidRoomPurpose.Entrance);
                var boss = rooms.FirstOrDefault(room => room.Purpose == RaidRoomPurpose.Boss);
                var navigationPath = new List<Vector2>();
                Check(navigation != null && entrance != null && boss != null &&
                      navigation.TryFindPath(entrance.Center, boss.Center, navigationPath) &&
                      navigationPath.Count >= 2,
                    "The baked 2D navigation grid must expose a traversable route from entrance to boss.", failures);

                var deterministicA = DungeonRoomFirstPlanner2D.Create(42817);
                var deterministicB = DungeonRoomFirstPlanner2D.Create(42817);
                Check(deterministicA.StructuralSignature() == deterministicB.StructuralSignature(),
                    "A stored dungeon seed must reproduce the same room graph and templates.", failures);
                var signatures = new HashSet<string>(StringComparer.Ordinal);
                for (var seed = 8100; seed < 8112; seed++)
                {
                    var plan = DungeonRoomFirstPlanner2D.Create(seed);
                    var isValid = DungeonRoomFirstPlanner2D.Validate(plan, out var validationReason);
                    Check(isValid,
                        $"Generated validation sample seed {seed} must be connected and valid. " +
                        validationReason,
                        failures);
                    signatures.Add(plan.StructuralSignature());
                }
                Check(signatures.Count >= 10,
                    "Sampled seeds must produce meaningfully varied room graphs and templates.", failures);
            }
            finally
            {
                if (openedForValidation && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
            return failures;
        }

        private static GreyboxExpansion ConfigureGreyboxExpansion(
            Transform dungeonRoot,
            RaidRoom2D firstEncounter,
            Sprite floorSprite,
            Sprite wallSprite)
        {
            var result = new GreyboxExpansion();
            var expansionObject = EnsureChild(dungeonRoot, "Greybox Room Kit");
            Undo.RecordObject(expansionObject.transform, "Configure greybox room kit");
            expansionObject.transform.localPosition = Vector3.zero;
            expansionObject.transform.localRotation = Quaternion.identity;
            expansionObject.transform.localScale = Vector3.one;

            var entrance = firstEncounter != null
                ? FindRoomByPurpose(dungeonRoot, RaidRoomPurpose.Entrance)
                : null;
            if (entrance != null)
            {
                EnsureSpawnMarker(
                    entrance.transform,
                    "Marker - Party Formation",
                    RaidSpawnMarkerKind.Party,
                    "party-entry",
                    6,
                    new Vector2(-3.5f, 0f),
                    1.8f,
                    floorSprite,
                    new Color(0.12f, 0.82f, 1f, 0.72f));
            }
            EnsureSpawnMarker(
                firstEncounter.transform,
                "Marker - Enemy Pod 01",
                RaidSpawnMarkerKind.EnemyPod,
                "encounter-01",
                8,
                Vector2.zero,
                2.3f,
                floorSprite,
                new Color(1f, 0.38f, 0.08f, 0.56f));
            EnsureRoomSideDoorway(firstEncounter, true, 4f, wallSprite);

            var pillarHall = EnsureGreyboxRoom(
                expansionObject.transform,
                "Room 3 - Pillar Hall Encounter",
                "pillar-hall-encounter",
                2,
                RaidRoomPurpose.Encounter,
                new Vector2(50f, 0f),
                new Vector2(20f, 16f),
                floorSprite,
                wallSprite,
                new Color(0.26f, 0.34f, 0.3f));
            EnsureObstacle(pillarHall.transform, "Obstacle - Pillar NW",
                new Vector2(-4.4f, 3.1f), new Vector2(1.8f, 1.8f), wallSprite);
            EnsureObstacle(pillarHall.transform, "Obstacle - Pillar SE",
                new Vector2(4.4f, -3.1f), new Vector2(1.8f, 1.8f), wallSprite);
            EnsureObstacle(pillarHall.transform, "Obstacle - Pillar NE",
                new Vector2(4.4f, 3.1f), new Vector2(1.8f, 1.8f), wallSprite);
            EnsureObstacle(pillarHall.transform, "Obstacle - Pillar SW",
                new Vector2(-4.4f, -3.1f), new Vector2(1.8f, 1.8f), wallSprite);
            EnsureSpawnMarker(
                pillarHall.transform,
                "Marker - Enemy Pod 02",
                RaidSpawnMarkerKind.EnemyPod,
                "encounter-02",
                12,
                Vector2.zero,
                2.6f,
                floorSprite,
                new Color(1f, 0.38f, 0.08f, 0.56f));

            var antechamber = EnsureGreyboxRoom(
                expansionObject.transform,
                "Room 4 - Boss Antechamber",
                "boss-antechamber",
                3,
                RaidRoomPurpose.Transition,
                new Vector2(72f, 0f),
                new Vector2(12f, 10f),
                floorSprite,
                wallSprite,
                new Color(0.28f, 0.24f, 0.38f));

            var bossRoom = EnsureGreyboxRoom(
                expansionObject.transform,
                "Room 5 - Boss Arena",
                "boss-arena",
                4,
                RaidRoomPurpose.Boss,
                new Vector2(100f, 0f),
                new Vector2(28f, 22f),
                floorSprite,
                wallSprite,
                new Color(0.36f, 0.16f, 0.18f));
            EnsureObstacle(bossRoom.transform, "Obstacle - Boss Pylon NW",
                new Vector2(-7.5f, 5.5f), new Vector2(2f, 2f), wallSprite);
            EnsureObstacle(bossRoom.transform, "Obstacle - Boss Pylon SW",
                new Vector2(-7.5f, -5.5f), new Vector2(2f, 2f), wallSprite);
            EnsureObstacle(bossRoom.transform, "Obstacle - Boss Pylon NE",
                new Vector2(7.5f, 5.5f), new Vector2(2f, 2f), wallSprite);
            EnsureObstacle(bossRoom.transform, "Obstacle - Boss Pylon SE",
                new Vector2(7.5f, -5.5f), new Vector2(2f, 2f), wallSprite);
            EnsureSpawnMarker(
                bossRoom.transform,
                "Marker - Boss",
                RaidSpawnMarkerKind.Boss,
                "boss-primary",
                1,
                Vector2.zero,
                3.2f,
                floorSprite,
                new Color(1f, 0.06f, 0.08f, 0.66f));
            EnsureSpawnMarker(
                bossRoom.transform,
                "Marker - Boss Chest",
                RaidSpawnMarkerKind.Chest,
                "boss-reward",
                1,
                new Vector2(8.5f, 0f),
                1.2f,
                floorSprite,
                new Color(1f, 0.78f, 0.12f, 0.72f));
            EnsureSpawnMarker(
                bossRoom.transform,
                "Marker - Exit Portal",
                RaidSpawnMarkerKind.ExitPortal,
                "dungeon-exit",
                6,
                new Vector2(11.5f, 0f),
                1.5f,
                floorSprite,
                new Color(0.25f, 1f, 0.52f, 0.66f));

            result.Rooms.Add(pillarHall);
            result.Rooms.Add(antechamber);
            result.Rooms.Add(bossRoom);
            result.Connections.Add(EnsureStraightCorridor(
                expansionObject.transform,
                "Corridor - Mob Den to Pillar Hall",
                firstEncounter,
                pillarHall,
                new Vector2(35f, 0f),
                10f,
                4f,
                floorSprite,
                wallSprite));
            result.Connections.Add(EnsureStraightCorridor(
                expansionObject.transform,
                "Corridor - Pillar Hall to Antechamber",
                pillarHall,
                antechamber,
                new Vector2(63f, 0f),
                6f,
                4f,
                floorSprite,
                wallSprite));
            result.Connections.Add(EnsureStraightCorridor(
                expansionObject.transform,
                "Corridor - Antechamber to Boss Arena",
                antechamber,
                bossRoom,
                new Vector2(82f, 0f),
                8f,
                4f,
                floorSprite,
                wallSprite));
            return result;
        }

        private static RaidRoom2D EnsureGreyboxRoom(
            Transform parent,
            string objectName,
            string roomId,
            int sequence,
            RaidRoomPurpose purpose,
            Vector2 localPosition,
            Vector2 size,
            Sprite floorSprite,
            Sprite wallSprite,
            Color floorColor)
        {
            var roomObject = EnsureChild(parent, objectName);
            ConfigureLocalTransform(roomObject.transform, localPosition, Vector2.one);
            var room = GetOrAddSingle<RaidRoom2D>(roomObject);
            room.ConfigureEditor(roomId, sequence, size, purpose);
            MarkDirty(room);

            ConfigureVisualRect(
                EnsureChild(roomObject.transform, "Floor"),
                Vector2.zero,
                size,
                floorSprite,
                floorColor,
                -20,
                false);
            const float wallThickness = 0.65f;
            const float doorwayWidth = 4f;
            ConfigureVisualRect(
                EnsureChild(roomObject.transform, "Wall North"),
                new Vector2(0f, size.y * 0.5f + wallThickness * 0.5f),
                new Vector2(size.x + wallThickness * 2f, wallThickness),
                wallSprite,
                new Color(0.11f, 0.12f, 0.16f),
                -5,
                true);
            ConfigureVisualRect(
                EnsureChild(roomObject.transform, "Wall South"),
                new Vector2(0f, -size.y * 0.5f - wallThickness * 0.5f),
                new Vector2(size.x + wallThickness * 2f, wallThickness),
                wallSprite,
                new Color(0.11f, 0.12f, 0.16f),
                -5,
                true);

            var sideSegmentHeight = Mathf.Max(0.5f, (size.y - doorwayWidth) * 0.5f);
            var sideOffset = doorwayWidth * 0.5f + sideSegmentHeight * 0.5f;
            var sideSize = new Vector2(wallThickness, sideSegmentHeight);
            ConfigureVisualRect(EnsureChild(roomObject.transform, "Wall West Upper"),
                new Vector2(-size.x * 0.5f - wallThickness * 0.5f, sideOffset),
                sideSize, wallSprite, new Color(0.11f, 0.12f, 0.16f), -5, true);
            ConfigureVisualRect(EnsureChild(roomObject.transform, "Wall West Lower"),
                new Vector2(-size.x * 0.5f - wallThickness * 0.5f, -sideOffset),
                sideSize, wallSprite, new Color(0.11f, 0.12f, 0.16f), -5, true);
            ConfigureVisualRect(EnsureChild(roomObject.transform, "Wall East Upper"),
                new Vector2(size.x * 0.5f + wallThickness * 0.5f, sideOffset),
                sideSize, wallSprite, new Color(0.11f, 0.12f, 0.16f), -5, true);
            ConfigureVisualRect(EnsureChild(roomObject.transform, "Wall East Lower"),
                new Vector2(size.x * 0.5f + wallThickness * 0.5f, -sideOffset),
                sideSize, wallSprite, new Color(0.11f, 0.12f, 0.16f), -5, true);
            return room;
        }

        private static RaidRoomConnection2D EnsureStraightCorridor(
            Transform parent,
            string objectName,
            RaidRoom2D fromRoom,
            RaidRoom2D toRoom,
            Vector2 localPosition,
            float length,
            float width,
            Sprite floorSprite,
            Sprite wallSprite)
        {
            var corridorObject = EnsureChild(parent, objectName);
            ConfigureLocalTransform(corridorObject.transform, localPosition, Vector2.one);
            var connection = GetOrAddSingle<RaidRoomConnection2D>(corridorObject);
            connection.ConfigureEditor(fromRoom, toRoom, width, length);
            MarkDirty(connection);

            ConfigureVisualRect(EnsureChild(corridorObject.transform, "Floor"),
                Vector2.zero, new Vector2(length, width), floorSprite,
                new Color(0.2f, 0.24f, 0.27f), -19, false);
            const float wallThickness = 0.55f;
            ConfigureVisualRect(EnsureChild(corridorObject.transform, "Wall North"),
                new Vector2(0f, width * 0.5f + wallThickness * 0.5f),
                new Vector2(length, wallThickness), wallSprite,
                new Color(0.09f, 0.1f, 0.13f), -5, true);
            ConfigureVisualRect(EnsureChild(corridorObject.transform, "Wall South"),
                new Vector2(0f, -width * 0.5f - wallThickness * 0.5f),
                new Vector2(length, wallThickness), wallSprite,
                new Color(0.09f, 0.1f, 0.13f), -5, true);
            return connection;
        }

        private static void EnsureObstacle(
            Transform room,
            string objectName,
            Vector2 localPosition,
            Vector2 size,
            Sprite sprite)
        {
            ConfigureVisualRect(
                EnsureChild(room, objectName),
                localPosition,
                size,
                sprite,
                new Color(0.08f, 0.09f, 0.12f),
                2,
                true);
        }

        private static void EnsureRoomSideDoorway(
            RaidRoom2D room,
            bool eastSide,
            float doorwayWidth,
            Sprite wallSprite)
        {
            if (room == null) return;
            var edgeX = room.Center.x + (eastSide ? 1f : -1f) * room.Size.x * 0.5f;
            var sideWalls = room.GetComponentsInChildren<SpriteRenderer>(true)
                .Where(renderer => IsWall(renderer.name) &&
                                   Mathf.Abs(renderer.bounds.center.x - edgeX) <= 1.1f)
                .ToArray();
            for (var index = 0; index < sideWalls.Length; index++)
            {
                Undo.DestroyObjectImmediate(sideWalls[index].gameObject);
            }

            const float wallThickness = 0.65f;
            var segmentHeight = Mathf.Max(0.5f, (room.Size.y - doorwayWidth) * 0.5f);
            var yOffset = doorwayWidth * 0.5f + segmentHeight * 0.5f;
            var xOffset = (eastSide ? 1f : -1f) *
                          (room.Size.x * 0.5f + wallThickness * 0.5f);
            var prefix = eastSide ? "Wall East" : "Wall West";
            ConfigureVisualRect(
                EnsureChild(room.transform, $"{prefix} Upper"),
                new Vector2(xOffset, yOffset),
                new Vector2(wallThickness, segmentHeight),
                wallSprite,
                new Color(0.11f, 0.12f, 0.16f),
                -5,
                true);
            ConfigureVisualRect(
                EnsureChild(room.transform, $"{prefix} Lower"),
                new Vector2(xOffset, -yOffset),
                new Vector2(wallThickness, segmentHeight),
                wallSprite,
                new Color(0.11f, 0.12f, 0.16f),
                -5,
                true);
        }

        private static RaidSpawnMarker2D EnsureSpawnMarker(
            Transform room,
            string objectName,
            RaidSpawnMarkerKind kind,
            string groupId,
            int capacity,
            Vector2 localPosition,
            float radius,
            Sprite sprite,
            Color color)
        {
            var markerObject = EnsureChild(room, objectName);
            ConfigureLocalTransform(markerObject.transform, localPosition,
                new Vector2(radius * 2f, radius * 2f));
            var renderer = GetOrAdd<SpriteRenderer>(markerObject);
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = -2;
            var marker = GetOrAddSingle<RaidSpawnMarker2D>(markerObject);
            marker.ConfigureEditor(kind, groupId, capacity, radius);
            MarkDirty(renderer, marker);
            return marker;
        }

        private static void ConfigureVisualRect(
            GameObject target,
            Vector2 localPosition,
            Vector2 worldSize,
            Sprite sprite,
            Color color,
            int sortingOrder,
            bool solidCollider)
        {
            var spriteSize = sprite != null ? sprite.bounds.size : Vector3.one;
            var safeSpriteSize = new Vector2(
                Mathf.Max(0.001f, spriteSize.x),
                Mathf.Max(0.001f, spriteSize.y));
            ConfigureLocalTransform(target.transform, localPosition,
                new Vector2(worldSize.x / safeSpriteSize.x, worldSize.y / safeSpriteSize.y));
            var renderer = GetOrAdd<SpriteRenderer>(target);
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            if (solidCollider)
            {
                var collider = GetOrAdd<BoxCollider2D>(target);
                collider.isTrigger = false;
                MarkDirty(collider);
            }
            MarkDirty(renderer);
        }

        private static void ConfigureLocalTransform(
            Transform target,
            Vector2 localPosition,
            Vector2 localScale)
        {
            Undo.RecordObject(target, "Configure dungeon greybox transform");
            target.localPosition = new Vector3(localPosition.x, localPosition.y, 0f);
            target.localRotation = Quaternion.identity;
            target.localScale = new Vector3(localScale.x, localScale.y, 1f);
        }

        private static RaidRoom2D FindRoomByPurpose(
            Transform root,
            RaidRoomPurpose purpose)
        {
            return root.GetComponentsInChildren<RaidRoom2D>(true)
                .FirstOrDefault(room => room.Purpose == purpose);
        }

        private static SpriteRenderer FindRenderer(GameObject root, string objectName)
        {
            return root.GetComponentsInChildren<SpriteRenderer>(true)
                .FirstOrDefault(renderer => NormalizeName(renderer.name)
                    .Equals(objectName, StringComparison.OrdinalIgnoreCase));
        }

        private static List<RaidAgent2D> ConfigureHunters(Transform root)
        {
            var result = new List<RaidAgent2D>();
            for (var index = 0; index < root.childCount; index++)
            {
                var child = root.GetChild(index);
                if (child.GetComponent<SpriteRenderer>() == null) continue;
                var roleName = NormalizeName(child.name);
                if (!TryParseHunterRole(roleName, out var role)) continue;
                var profile = HunterProfile(role);
                ConfigureAgentPhysics(child.gameObject);
                var agent = GetOrAddSingle<RaidAgent2D>(child.gameObject);
                agent.ConfigureEditor(
                    $"hunter-{role.ToString().ToLowerInvariant()}",
                    role.ToString(),
                    RaidFaction.Hunters,
                    role,
                    profile.Health,
                    profile.Mana,
                    profile.ManaRegeneration,
                    profile.Speed,
                    profile.Damage,
                    profile.AttackRange,
                    profile.PreferredRange,
                    profile.AttackCooldown,
                    profile.Ranged,
                    profile.Color,
                    profile.Abilities);
                MarkDirty(
                    agent,
                    child.GetComponent<SpriteRenderer>(),
                    child.GetComponent<Rigidbody2D>(),
                    child.GetComponent<CircleCollider2D>());
                result.Add(agent);
            }
            result.Sort((left, right) => RoleOrder(left.Role).CompareTo(RoleOrder(right.Role)));
            return result;
        }

        private static void EnsureMonsterRoster(
            Transform root,
            SpriteRenderer template,
            IReadOnlyList<string> actorNames,
            float scale = 0.65f)
        {
            for (var index = 0; index < actorNames.Count; index++)
            {
                var actorName = actorNames[index];
                var actor = FindChild(root, actorName);
                if (actor == null)
                {
                    actor = new GameObject(actorName);
                    Undo.RegisterCreatedObjectUndo(actor, "Populate dungeon monster roster");
                    actor.transform.SetParent(root, false);
                }

                Undo.RecordObject(actor.transform, "Configure dungeon monster roster");
                actor.transform.localPosition = Vector3.zero;
                actor.transform.localRotation = Quaternion.identity;
                actor.transform.localScale = Vector3.one * scale;

                var renderer = GetOrAdd<SpriteRenderer>(actor);
                Undo.RecordObject(renderer, "Configure dungeon monster visual");
                renderer.sprite = template.sprite;
                renderer.sharedMaterial = template.sharedMaterial;
                renderer.sortingLayerID = template.sortingLayerID;
                renderer.sortingOrder = 25;
                renderer.color = Color.white;
                MarkDirty(renderer);
            }
        }

        private static List<RaidAgent2D> ConfigureMonsters(Transform root, string podId)
        {
            var result = new List<RaidAgent2D>();
            var roleCounts = new Dictionary<RaidCombatRole, int>();
            for (var index = 0; index < root.childCount; index++)
            {
                var child = root.GetChild(index);
                if (child.GetComponent<SpriteRenderer>() == null) continue;
                var normalized = NormalizeName(child.name);
                var isBoss = normalized.Contains("Boss", StringComparison.OrdinalIgnoreCase) ||
                             normalized.Contains("Warlord", StringComparison.OrdinalIgnoreCase);
                var role = isBoss || normalized.Contains("Sergeant", StringComparison.OrdinalIgnoreCase)
                    ? RaidCombatRole.Elite
                    : normalized.Contains("Bow", StringComparison.OrdinalIgnoreCase)
                        ? RaidCombatRole.Archer
                        : RaidCombatRole.Melee;
                roleCounts.TryGetValue(role, out var roleIndex);
                roleCounts[role] = ++roleIndex;
                var profile = isBoss ? BossProfile() : MonsterProfile(role);
                ConfigureAgentPhysics(child.gameObject);
                var agent = GetOrAddSingle<RaidAgent2D>(child.gameObject);
                var label = isBoss ? "Goblin Warlord" : role switch
                {
                    RaidCombatRole.Elite => "Goblin Sergeant",
                    RaidCombatRole.Archer => $"Goblin Bowman {roleIndex}",
                    _ => $"Goblin Swordsman {roleIndex}"
                };
                agent.ConfigureEditor(
                    $"{podId}-{(isBoss ? "warlord" : role.ToString().ToLowerInvariant())}-{roleIndex:00}",
                    label,
                    RaidFaction.Monsters,
                    role,
                    profile.Health,
                    profile.Mana,
                    profile.ManaRegeneration,
                    profile.Speed,
                    profile.Damage,
                    profile.AttackRange,
                    profile.PreferredRange,
                    profile.AttackCooldown,
                    profile.Ranged,
                    profile.Color,
                    profile.Abilities,
                    isBoss ? 0.7f : 0.45f);
                MarkDirty(
                    agent,
                    child.GetComponent<SpriteRenderer>(),
                    child.GetComponent<Rigidbody2D>(),
                    child.GetComponent<CircleCollider2D>());
                result.Add(agent);
            }
            return result;
        }

        private static void ConfigureAgentPhysics(GameObject actor)
        {
            var body = GetOrAdd<Rigidbody2D>(actor);
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;
            body.linearDamping = 8f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            var hurtbox = GetOrAdd<CircleCollider2D>(actor);
            hurtbox.isTrigger = false;
            hurtbox.radius = 0.45f;
        }

        private static void ConfigureWorldColliders(Transform dungeonRoot)
        {
            var renderers = dungeonRoot.GetComponentsInChildren<SpriteRenderer>(true);
            for (var index = 0; index < renderers.Length; index++)
            {
                var renderer = renderers[index];
                if (!IsWall(renderer.name)) continue;
                var wallCollider = GetOrAdd<BoxCollider2D>(renderer.gameObject);
                wallCollider.isTrigger = false;
                MarkDirty(wallCollider);
            }
        }

        private static AgentProfile HunterProfile(RaidCombatRole role)
        {
            return role switch
            {
                RaidCombatRole.Tank => new AgentProfile
                {
                    Role = role, Health = 250f, Mana = 90f, ManaRegeneration = 5f,
                    Speed = 4.25f, Damage = 20f, AttackRange = 1.55f,
                    PreferredRange = 1.2f, AttackCooldown = 0.88f,
                    Color = new Color(0.15f, 0.75f, 1f),
                    Abilities = new List<RaidAbilitySpec>
                    {
                        Ability("tank.bulwark", "Bulwark", RaidAbilityEffect.Shield,
                            1f, 8f, 50f, 30f, 22f, new Color(1f, 0.96f, 0.78f),
                            duration: 12f, shape: RaidAttackShape.Circle, maximumTargets: 64),
                        Ability("tank.challenge", "Challenge", RaidAbilityEffect.Taunt,
                            1f, 15f, 0f, 8f, 18f, new Color(0.2f, 0.8f, 1f),
                            duration: 4f, shape: RaidAttackShape.Circle,
                            multiplier: 0.75f, maximumTargets: 64)
                    }
                },
                RaidCombatRole.Fighter => new AgentProfile
                {
                    Role = role, Health = 185f, Mana = 75f, ManaRegeneration = 4f,
                    Speed = 4.8f, Damage = 27f, AttackRange = 1.55f,
                    PreferredRange = 1.15f, AttackCooldown = 0.72f,
                    Color = new Color(1f, 0.48f, 0.12f),
                    Abilities = new List<RaidAbilitySpec>
                    {
                        Ability("fighter.cleave", "Cleave", RaidAbilityEffect.AreaDamage,
                            0.1f, 2.5f, 0f, 10f, 16f, new Color(1f, 0.58f, 0.1f),
                            shape: RaidAttackShape.Circle, multiplier: 1.5f,
                            scalesWithBasicAttack: true, maximumTargets: 64),
                        Ability("fighter.rallying-strike", "Rallying Strike",
                            RaidAbilityEffect.DamageAndBuffAllies,
                            1.75f, 10f, 0f, 30f, 24f, new Color(1f, 0.32f, 0.08f),
                            duration: 10f, multiplier: 2f, secondaryPower: 1.25f,
                            scalesWithBasicAttack: true, width: 0.34f)
                    }
                },
                RaidCombatRole.Healer => new AgentProfile
                {
                    Role = role, Health = 130f, Mana = 170f, ManaRegeneration = 12f,
                    Speed = 4.5f, Damage = 11f, AttackRange = 5.5f,
                    PreferredRange = 5f, AttackCooldown = 1.05f, Ranged = true,
                    Color = new Color(0.2f, 1f, 0.42f),
                    Abilities = new List<RaidAbilitySpec>
                    {
                        Ability("healer.mend", "Mend", RaidAbilityEffect.Heal,
                            15f, 0f, 0f, 10f, 17f, new Color(0.22f, 1f, 0.5f),
                            healthThreshold: 0.82f, multiplier: 0.6f,
                            scalesWithTargetMaximumHealth: true),
                        Ability("healer.sanctuary", "Sanctuary",
                            RaidAbilityEffect.PersistentAreaHeal,
                            1f, 5f, 50f, 30f, 38f, new Color(0.6f, 1f, 0.72f),
                            duration: 6f, healthThreshold: 0.76f,
                            shape: RaidAttackShape.Circle, maximumTargets: 64)
                    }
                },
                RaidCombatRole.Mage => new AgentProfile
                {
                    Role = role, Health = 115f, Mana = 160f, ManaRegeneration = 10f,
                    Speed = 4.35f, Damage = 18f, AttackRange = 6f,
                    PreferredRange = 5.3f, AttackCooldown = 1f, Ranged = true,
                    Color = new Color(0.68f, 0.28f, 1f),
                    Abilities = new List<RaidAbilitySpec>
                    {
                        Ability("mage.elemental-affliction.fire", "Elemental Affliction",
                            RaidAbilityEffect.DamageOverTime,
                            15f, 0f, 0f, 8f, 18f, new Color(1f, 0.28f, 0.05f),
                            duration: 5f, secondaryPower: 5f, element: RaidElement.Fire,
                            width: 0.16f),
                        Ability("mage.fireball", "Fireball",
                            RaidAbilityEffect.ProjectileAreaDamage,
                            15f, 3f, 34f, 30f, 30f, new Color(1f, 0.35f, 0.06f),
                            duration: 5f, secondaryPower: 5f, element: RaidElement.Fire,
                            shape: RaidAttackShape.Circle, maximumTargets: 64, width: 0.22f)
                    }
                },
                RaidCombatRole.Ranger => new AgentProfile
                {
                    Role = role, Health = 140f, Mana = 100f, ManaRegeneration = 6f,
                    Speed = 4.9f, Damage = 23f, AttackRange = 7f,
                    PreferredRange = 6f, AttackCooldown = 0.78f, Ranged = true,
                    Color = new Color(0.92f, 0.82f, 0.16f),
                    Abilities = new List<RaidAbilitySpec>
                    {
                        Ability("ranger.piercing-shot", "Piercing Shot",
                            RaidAbilityEffect.PiercingDamage,
                            20f, 0f, 0f, 6f, 18f, new Color(1f, 0.9f, 0.25f),
                            multiplier: 1.5f, scalesWithBasicAttack: true,
                            maximumTargets: 12, width: 0.18f),
                        Ability("ranger.hunters-mark", "Hunter's Mark",
                            RaidAbilityEffect.DamageMark,
                            20f, 0f, 0f, 20f, 22f, new Color(1f, 0.65f, 0.12f),
                            duration: 10f, multiplier: 2f, width: 0.08f)
                    }
                },
                _ => new AgentProfile
                {
                    Role = RaidCombatRole.Assassin, Health = 150f, Mana = 95f, ManaRegeneration = 6f,
                    Speed = 5.65f, Damage = 30f, AttackRange = 1.4f,
                    PreferredRange = 1.05f, AttackCooldown = 0.62f,
                    Color = new Color(1f, 0.18f, 0.65f),
                    Abilities = new List<RaidAbilitySpec>
                    {
                        Ability("assassin.shadow-step", "Shadow Step",
                            RaidAbilityEffect.ShadowStep,
                            25f, 0f, 0f, 6f, 20f, new Color(0.95f, 0.18f, 0.85f),
                            duration: 5f, multiplier: 1.25f),
                        Ability("assassin.execute", "Execution", RaidAbilityEffect.Execute,
                            1.8f, 0f, 0f, 25f, 24f, new Color(1f, 0.08f, 0.35f),
                            duration: 0.25f, multiplier: 5f,
                            scalesWithBasicAttack: true, width: 0.4f)
                    }
                }
            };
        }

        private static AgentProfile MonsterProfile(RaidCombatRole role)
        {
            return role switch
            {
                RaidCombatRole.Elite => new AgentProfile
                {
                    Role = role, Health = 230f, Mana = 60f, ManaRegeneration = 5f,
                    Speed = 3.55f, Damage = 20f, AttackRange = 1.5f,
                    PreferredRange = 1.15f, AttackCooldown = 1f,
                    Color = new Color(0.78f, 0.12f, 0.05f),
                    Abilities = new List<RaidAbilitySpec>
                    {
                        Ability("goblin.sergeant-rally", "Sergeant's Rally",
                            RaidAbilityEffect.DamageAndBuffAllies,
                            1.7f, 4f, 18f, 8f, 20f, new Color(1f, 0.42f, 0.12f),
                            duration: 5f, secondaryPower: 1.12f)
                    }
                },
                RaidCombatRole.Archer => new AgentProfile
                {
                    Role = role, Health = 82f, Mana = 30f, ManaRegeneration = 2f,
                    Speed = 3.8f, Damage = 13f, AttackRange = 6.2f,
                    PreferredRange = 5.2f, AttackCooldown = 1.2f, Ranged = true,
                    Color = new Color(0.88f, 0.2f, 0.08f),
                    Abilities = new List<RaidAbilitySpec>
                    {
                        Ability("goblin.aimed-shot", "Aimed Shot", RaidAbilityEffect.Damage,
                            7f, 0f, 23f, 5.5f, 14f, new Color(1f, 0.28f, 0.08f))
                    }
                },
                _ => new AgentProfile
                {
                    Role = role, Health = 108f, Mana = 0f, ManaRegeneration = 0f,
                    Speed = 4f, Damage = 15f, AttackRange = 1.35f,
                    PreferredRange = 1.05f, AttackCooldown = 0.95f,
                    Color = new Color(0.72f, 0.05f, 0.03f)
                }
            };
        }

        private static AgentProfile BossProfile()
        {
            return new AgentProfile
            {
                Role = RaidCombatRole.Elite,
                Health = 850f,
                Mana = 180f,
                ManaRegeneration = 9f,
                Speed = 3.75f,
                Damage = 30f,
                AttackRange = 1.9f,
                PreferredRange = 1.35f,
                AttackCooldown = 0.92f,
                Color = new Color(0.58f, 0.025f, 0.08f),
                Abilities = new List<RaidAbilitySpec>
                {
                    Ability("boss.warlord-cleave", "Warlord Cleave",
                        RaidAbilityEffect.AreaDamage,
                        2.4f, 3.4f, 44f, 5f, 22f, new Color(1f, 0.12f, 0.04f),
                        shape: RaidAttackShape.Cone, maximumTargets: 6, width: 0.7f),
                    Ability("boss.warlord-chain", "Blood Chain",
                        RaidAbilityEffect.ChainDamage,
                        8f, 5f, 30f, 8f, 30f, new Color(0.95f, 0.04f, 0.22f),
                        maximumTargets: 4, multiplier: 0.72f),
                    Ability("boss.warlord-roar", "Warlord's Roar",
                        RaidAbilityEffect.AreaDamage,
                        1f, 3.4f, 28f, 12f, 35f, new Color(1f, 0.5f, 0.18f),
                        shape: RaidAttackShape.Circle, maximumTargets: 6)
                }
            };
        }

        private static RaidAbilitySpec Ability(
            string id,
            string name,
            RaidAbilityEffect effect,
            float range,
            float radius,
            float power,
            float cooldown,
            float mana,
            Color color,
            float duration = 2f,
            float healthThreshold = 0.7f,
            RaidAttackShape shape = RaidAttackShape.Single,
            RaidElement element = RaidElement.None,
            float multiplier = 1f,
            float secondaryPower = 0f,
            bool scalesWithBasicAttack = false,
            bool scalesWithTargetMaximumHealth = false,
            int maximumTargets = 1,
            float width = 0.35f)
        {
            var ability = RaidAbilitySpec.Create(
                id,
                name,
                effect,
                range,
                radius,
                power,
                cooldown,
                mana,
                color,
                duration,
                healthThreshold);
            ability.shape = shape;
            ability.element = element;
            ability.multiplier = Mathf.Max(0f, multiplier);
            ability.secondaryPower = Mathf.Max(0f, secondaryPower);
            ability.scalesWithBasicAttack = scalesWithBasicAttack;
            ability.scalesWithTargetMaximumHealth = scalesWithTargetMaximumHealth;
            ability.maximumTargets = Mathf.Max(1, maximumTargets);
            ability.width = Mathf.Max(0.02f, width);
            return ability;
        }

        private static int RoleOrder(RaidCombatRole role) => role switch
        {
            RaidCombatRole.Tank => 0,
            RaidCombatRole.Fighter => 1,
            RaidCombatRole.Assassin => 2,
            RaidCombatRole.Mage => 3,
            RaidCombatRole.Ranger => 4,
            RaidCombatRole.Healer => 5,
            _ => 10
        };

        private static Vector2 ResolveRoomSize(GameObject room)
        {
            var renderers = room.GetComponentsInChildren<SpriteRenderer>(true);
            for (var index = 0; index < renderers.Length; index++)
            {
                if (NormalizeName(renderers[index].name).Equals("Floor", StringComparison.OrdinalIgnoreCase))
                {
                    var size = renderers[index].bounds.size;
                    return new Vector2(Mathf.Max(1f, size.x), Mathf.Max(1f, size.y));
                }
            }
            return new Vector2(20f, 20f);
        }

        private static string NormalizeName(string value)
        {
            return (value ?? string.Empty).Trim().Trim('[', ']').Trim().Trim('\'');
        }

        private static bool IsWall(string value)
        {
            return NormalizeName(value).StartsWith(
                "Wall",
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryParseHunterRole(string value, out RaidCombatRole role)
        {
            if (value.Equals("Tanker", StringComparison.OrdinalIgnoreCase))
            {
                role = RaidCombatRole.Tank;
                return true;
            }

            return Enum.TryParse(value, true, out role);
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            return scene.GetRootGameObjects().FirstOrDefault(root =>
                NormalizeName(root.name).Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        private static GameObject FindChild(Transform parent, string name)
        {
            if (parent == null) return null;
            for (var index = 0; index < parent.childCount; index++)
            {
                var child = parent.GetChild(index);
                if (NormalizeName(child.name).Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return child.gameObject;
                }
            }
            return null;
        }

        private static GameObject EnsureChild(Transform parent, string name)
        {
            var existing = FindChild(parent, name);
            if (existing != null) return existing;
            var created = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(created, $"Create {name}");
            created.transform.SetParent(parent, false);
            return created;
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(target);
            var existing = target.GetComponent<T>();
            return existing != null ? existing : Undo.AddComponent<T>(target);
        }

        private static T GetOrAddSingle<T>(GameObject target) where T : Component
        {
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(target);
            var components = target.GetComponents<T>();
            var retained = components.Length > 0 ? components[0] : Undo.AddComponent<T>(target);
            for (var index = 1; index < components.Length; index++)
            {
                if (components[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(components[index], true);
                }
            }
            return retained;
        }

        private static void MarkDirty(params UnityEngine.Object[] objects)
        {
            foreach (var item in objects)
            {
                if (item != null) EditorUtility.SetDirty(item);
            }
        }

        private static void Check(bool condition, string failure, List<string> failures)
        {
            if (!condition) failures.Add(failure);
        }

        private static void EnsureSceneInBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.All(scene => scene.path != ScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
        }
    }
}
