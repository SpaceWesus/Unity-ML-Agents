using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Turtle.DungeonRaid
{
    /// <summary>
    /// Materializes a lightweight dungeon plan into the playable 2D scene. The
    /// planner is deterministic; only the choice of seed is random in the demo.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(DungeonNavigationGrid2D))]
    public sealed class DungeonRoomFirstGenerator2D : MonoBehaviour
    {
        [Flags]
        private enum DoorSides
        {
            None = 0,
            North = 1 << 0,
            East = 1 << 1,
            South = 1 << 2,
            West = 1 << 3
        }

        [Header("Generation")]
        [SerializeField] private DungeonRoomFirstSettings2D settings = new();
        [SerializeField] private int previewSeed = 731245;
        [SerializeField] private bool randomizeSeedOnPlay = true;
        [SerializeField] private int currentSeed;
        [SerializeField] private Transform generatedRoot;

        [Header("Scene bindings")]
        [SerializeField] private DungeonRaidDirector2D director;
        [SerializeField] private RaidPartyBrain2D party;
        [SerializeField] private RaidEnemyPodBrain2D primaryEnemyPod;
        [SerializeField] private RaidEnemyPodBrain2D[] encounterPods =
            Array.Empty<RaidEnemyPodBrain2D>();
        [SerializeField] private RaidEnemyPodBrain2D bossPod;
        [SerializeField] private RaidChest2D primaryChest;
        [SerializeField] private Sprite floorSprite;
        [SerializeField] private Sprite wallSprite;
        [SerializeField] private DungeonNavigationGrid2D navigation;

        private readonly List<RaidRoom2D> generatedRooms = new(16);
        private readonly List<RaidRoomConnection2D> generatedConnections = new(20);
        private readonly List<DungeonCorridorWallSegment2D> corridorWallSegments = new(12);
        private DungeonRoomFirstPlan2D currentPlan;
        private Coroutine pendingNavigationRefresh;

        public int PreviewSeed => previewSeed;
        public int CurrentSeed => currentSeed;
        public bool RandomizeSeedOnPlay => randomizeSeedOnPlay;
        public IReadOnlyList<RaidRoom2D> GeneratedRooms => generatedRooms;
        public IReadOnlyList<RaidRoomConnection2D> GeneratedConnections => generatedConnections;
        public DungeonNavigationGrid2D Navigation => navigation;

        private void Awake()
        {
            var seed = randomizeSeedOnPlay
                ? unchecked(Environment.TickCount ^ DateTime.UtcNow.Ticks.GetHashCode())
                : previewSeed;
            Generate(seed);
        }

        private void OnValidate()
        {
            settings ??= new DungeonRoomFirstSettings2D();
            settings.EnsurePartyScaleDefaults();
            settings.Sanitize();
        }

        [ContextMenu("Generate New Preview Layout")]
        public void GenerateNewPreviewLayout()
        {
            previewSeed = unchecked(Environment.TickCount ^ DateTime.UtcNow.Ticks.GetHashCode());
            Generate(previewSeed);
        }

        public void Generate(int seed)
        {
            if (floorSprite == null || wallSprite == null)
            {
                Debug.LogError("Dungeon generation requires floor and wall sprites.", this);
                return;
            }

            settings ??= new DungeonRoomFirstSettings2D();
            settings.EnsurePartyScaleDefaults();
            settings.Sanitize();
            currentPlan = DungeonRoomFirstPlanner2D.Create(seed, settings);
            if (!DungeonRoomFirstPlanner2D.Validate(currentPlan, out var reason))
            {
                throw new InvalidOperationException($"Generated dungeon was invalid: {reason}");
            }

            currentSeed = seed;
            RecreateGeneratedRoot();
            MaterializePlan(currentPlan);
            RebuildNavigation();
            BindRaidFixture(currentPlan);
            ScheduleRuntimeNavigationRefresh();
        }

        public void GenerateFromStoredGateSeed(int mapSeed)
        {
            Generate(mapSeed);
        }

        private void RecreateGeneratedRoot()
        {
            if (generatedRoot != null)
            {
                generatedRoot.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(generatedRoot.gameObject);
                else DestroyImmediate(generatedRoot.gameObject);
            }

            var rootObject = new GameObject($"Generated Dungeon [Seed {currentSeed}]");
            generatedRoot = rootObject.transform;
            generatedRoot.SetParent(transform, false);
            generatedRoot.localPosition = Vector3.zero;
            generatedRoot.localRotation = Quaternion.identity;
            generatedRoot.localScale = Vector3.one;
            generatedRooms.Clear();
            generatedConnections.Clear();
        }

        private void MaterializePlan(DungeonRoomFirstPlan2D plan)
        {
            var roomObjects = new Dictionary<int, RaidRoom2D>(plan.Rooms.Count);
            var doors = plan.Rooms.ToDictionary(room => room.Id, _ => DoorSides.None);

            foreach (var roomPlan in plan.Rooms.OrderBy(room => room.Sequence).ThenBy(room => room.Id))
            {
                var roomObject = new GameObject(
                    $"Room {roomPlan.Sequence:00} - {roomPlan.Purpose} - {roomPlan.Template}");
                roomObject.transform.SetParent(generatedRoot, false);
                roomObject.transform.localPosition = roomPlan.Center;
                var room = roomObject.AddComponent<RaidRoom2D>();
                room.Configure($"generated-room-{roomPlan.Id:00}", roomPlan.Sequence,
                    roomPlan.Size, roomPlan.Purpose);
                CreateRect(roomObject.transform, "Floor", Vector2.zero, roomPlan.Size,
                    floorSprite, ResolveFloorColor(roomPlan.Purpose, roomPlan.Template), -20, false);
                roomObjects.Add(roomPlan.Id, room);
                generatedRooms.Add(room);
            }

            foreach (var connectionPlan in plan.Connections)
            {
                var fromPlan = plan.Rooms[connectionPlan.FromRoomId];
                var toPlan = plan.Rooms[connectionPlan.ToRoomId];
                doors[fromPlan.Id] |= ResolveDoorSide(
                    connectionPlan.Waypoints[0] - fromPlan.Center);
                doors[toPlan.Id] |= ResolveDoorSide(
                    connectionPlan.Waypoints[^1] - toPlan.Center);
                CreateCorridor(connectionPlan, roomObjects);
            }

            foreach (var roomPlan in plan.Rooms)
            {
                var room = roomObjects[roomPlan.Id];
                CreateRoomWalls(room.transform, roomPlan.Size, doors[roomPlan.Id],
                    settings.corridorWidth);
                PopulateTemplate(room.transform, roomPlan);
                CreateSemanticMarkers(room.transform, roomPlan);
            }

            generatedRooms.Sort((left, right) => left.Sequence.CompareTo(right.Sequence));
        }

        private void CreateCorridor(
            DungeonRoomFirstPlan2D.Connection plan,
            IReadOnlyDictionary<int, RaidRoom2D> roomObjects)
        {
            var connectionObject = new GameObject(
                $"Corridor {plan.FromRoomId:00}-{plan.ToRoomId:00}");
            connectionObject.transform.SetParent(generatedRoot, false);
            connectionObject.transform.localPosition = Average(plan.Waypoints);
            var totalLength = 0f;
            for (var index = 0; index < plan.Waypoints.Count - 1; index++)
            {
                var start = plan.Waypoints[index];
                var end = plan.Waypoints[index + 1];
                var length = Vector2.Distance(start, end);
                if (length <= 0.05f) continue;
                totalLength += length;
                CreateCorridorSegment(connectionObject.transform, index, start, end, plan.Width);
            }

            if (!DungeonCorridorContainment2D.TryBuildWallSegments(
                    plan.Waypoints,
                    plan.Width,
                    DungeonCorridorContainment2D.DefaultWallThickness,
                    corridorWallSegments,
                    out var containmentReason))
            {
                throw new InvalidOperationException(
                    $"Corridor {plan.FromRoomId}-{plan.ToRoomId} could not create continuous walls: {containmentReason}");
            }
            var wallsPerSide = plan.Waypoints.Count - 1;
            for (var index = 0; index < corridorWallSegments.Count; index++)
            {
                var side = index < wallsPerSide ? "Left" : "Right";
                var sideIndex = index % wallsPerSide;
                CreateCorridorWallSegment(
                    connectionObject.transform,
                    side,
                    sideIndex,
                    corridorWallSegments[index]);
            }

            var worldWaypoints = plan.Waypoints
                .Select(point => (Vector2)generatedRoot.TransformPoint(point))
                .ToArray();
            var connection = connectionObject.AddComponent<RaidRoomConnection2D>();
            connection.Configure(roomObjects[plan.FromRoomId], roomObjects[plan.ToRoomId],
                plan.Width, totalLength, worldWaypoints);
            generatedConnections.Add(connection);
        }

        private void CreateCorridorSegment(
            Transform parent,
            int index,
            Vector2 worldStart,
            Vector2 worldEnd,
            float width)
        {
            var localCenter = (worldStart + worldEnd) * 0.5f - (Vector2)parent.localPosition;
            var delta = worldEnd - worldStart;
            var horizontal = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y);
            var length = horizontal ? Mathf.Abs(delta.x) : Mathf.Abs(delta.y);
            var size = horizontal ? new Vector2(length + 0.2f, width) : new Vector2(width, length + 0.2f);
            CreateRect(parent, $"Floor {index:00}", localCenter, size, floorSprite,
                new Color(0.18f, 0.25f, 0.27f), -19, false);
        }

        private void CreateCorridorWallSegment(
            Transform parent,
            string side,
            int index,
            DungeonCorridorWallSegment2D segment)
        {
            var localCenter = (segment.Start + segment.End) * 0.5f -
                              (Vector2)parent.localPosition;
            var delta = segment.End - segment.Start;
            var horizontal = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y);
            var thickness = DungeonCorridorContainment2D.DefaultWallThickness;
            var size = horizontal
                ? new Vector2(segment.Length + thickness, thickness)
                : new Vector2(thickness, segment.Length + thickness);
            CreateRect(parent, $"Wall {side} {index:00}", localCenter, size,
                wallSprite, WallColor, -5, true);
        }

        private void CreateRoomWalls(Transform room, Vector2 size, DoorSides doors, float doorWidth)
        {
            const float thickness = 0.65f;
            CreateHorizontalWall(room, "North", size, thickness, doorWidth,
                DoorSides.North, doors, 1f);
            CreateHorizontalWall(room, "South", size, thickness, doorWidth,
                DoorSides.South, doors, -1f);
            CreateVerticalWall(room, "East", size, thickness, doorWidth,
                DoorSides.East, doors, 1f);
            CreateVerticalWall(room, "West", size, thickness, doorWidth,
                DoorSides.West, doors, -1f);
        }

        private void CreateHorizontalWall(
            Transform room,
            string label,
            Vector2 size,
            float thickness,
            float doorWidth,
            DoorSides side,
            DoorSides doors,
            float sign)
        {
            var y = sign * (size.y * 0.5f + thickness * 0.5f);
            if ((doors & side) == 0)
            {
                CreateRect(room, $"Wall {label}", new Vector2(0f, y),
                    new Vector2(size.x + thickness * 2f, thickness), wallSprite,
                    WallColor, -5, true);
                return;
            }
            var segment = Mathf.Max(0.5f, (size.x - doorWidth) * 0.5f);
            var offset = doorWidth * 0.5f + segment * 0.5f;
            CreateRect(room, $"Wall {label} Left", new Vector2(-offset, y),
                new Vector2(segment, thickness), wallSprite, WallColor, -5, true);
            CreateRect(room, $"Wall {label} Right", new Vector2(offset, y),
                new Vector2(segment, thickness), wallSprite, WallColor, -5, true);
        }

        private void CreateVerticalWall(
            Transform room,
            string label,
            Vector2 size,
            float thickness,
            float doorWidth,
            DoorSides side,
            DoorSides doors,
            float sign)
        {
            var x = sign * (size.x * 0.5f + thickness * 0.5f);
            if ((doors & side) == 0)
            {
                CreateRect(room, $"Wall {label}", new Vector2(x, 0f),
                    new Vector2(thickness, size.y), wallSprite, WallColor, -5, true);
                return;
            }
            var segment = Mathf.Max(0.5f, (size.y - doorWidth) * 0.5f);
            var offset = doorWidth * 0.5f + segment * 0.5f;
            CreateRect(room, $"Wall {label} Upper", new Vector2(x, offset),
                new Vector2(thickness, segment), wallSprite, WallColor, -5, true);
            CreateRect(room, $"Wall {label} Lower", new Vector2(x, -offset),
                new Vector2(thickness, segment), wallSprite, WallColor, -5, true);
        }

        private void PopulateTemplate(Transform room, DungeonRoomFirstPlan2D.Room plan)
        {
            var x = plan.Size.x * 0.25f;
            var y = plan.Size.y * 0.25f;
            switch (plan.Template)
            {
                case DungeonRoomTemplate2D.PillarHall:
                case DungeonRoomTemplate2D.BossPillars:
                    CreateObstacle(room, "Pillar NW", new Vector2(-x, y), new Vector2(1.8f, 1.8f));
                    CreateObstacle(room, "Pillar NE", new Vector2(x, y), new Vector2(1.8f, 1.8f));
                    CreateObstacle(room, "Pillar SW", new Vector2(-x, -y), new Vector2(1.8f, 1.8f));
                    CreateObstacle(room, "Pillar SE", new Vector2(x, -y), new Vector2(1.8f, 1.8f));
                    break;
                case DungeonRoomTemplate2D.SplitHall:
                    CreateObstacle(room, "Divider North", new Vector2(0f, y), new Vector2(5f, 1.2f));
                    CreateObstacle(room, "Divider South", new Vector2(0f, -y), new Vector2(5f, 1.2f));
                    break;
                case DungeonRoomTemplate2D.Crossroads:
                    CreateObstacle(room, "Cover NW", new Vector2(-x, y), new Vector2(2.8f, 2.2f));
                    CreateObstacle(room, "Cover SE", new Vector2(x, -y), new Vector2(2.8f, 2.2f));
                    break;
                case DungeonRoomTemplate2D.Ring:
                    CreateObstacle(room, "Ring North", new Vector2(0f, y), new Vector2(3.8f, 1.2f));
                    CreateObstacle(room, "Ring South", new Vector2(0f, -y), new Vector2(3.8f, 1.2f));
                    CreateObstacle(room, "Ring East", new Vector2(x, 0f), new Vector2(1.2f, 3.8f));
                    CreateObstacle(room, "Ring West", new Vector2(-x, 0f), new Vector2(1.2f, 3.8f));
                    break;
                case DungeonRoomTemplate2D.AmbushCover:
                    CreateObstacle(room, "Cover A", new Vector2(-x, y * 0.6f), new Vector2(3.5f, 1.3f));
                    CreateObstacle(room, "Cover B", new Vector2(x, -y * 0.6f), new Vector2(3.5f, 1.3f));
                    break;
                case DungeonRoomTemplate2D.RewardCache:
                    CreateObstacle(room, "Cache Left", new Vector2(-x, y), new Vector2(2.4f, 1.2f));
                    CreateObstacle(room, "Cache Right", new Vector2(x, y), new Vector2(2.4f, 1.2f));
                    break;
                case DungeonRoomTemplate2D.ResourcePocket:
                    CreateObstacle(room, "Crystal Left", new Vector2(-x, y), new Vector2(1.2f, 2.6f));
                    CreateObstacle(room, "Crystal Right", new Vector2(x, -y), new Vector2(1.2f, 2.6f));
                    break;
                case DungeonRoomTemplate2D.BossAntechamber:
                    CreateObstacle(room, "Antechamber Plinth", new Vector2(0f, y), new Vector2(3f, 1.1f));
                    break;
            }
        }

        private void CreateSemanticMarkers(Transform room, DungeonRoomFirstPlan2D.Room plan)
        {
            switch (plan.Purpose)
            {
                case RaidRoomPurpose.Entrance:
                    CreateMarker(room, "Marker - Party", RaidSpawnMarkerKind.Party,
                        "party-entry", 6, Vector2.zero, 1.8f, new Color(0.1f, 0.8f, 1f, 0.58f));
                    break;
                case RaidRoomPurpose.Encounter:
                    CreateMarker(room, "Marker - Enemy Pod", RaidSpawnMarkerKind.EnemyPod,
                        $"encounter-{plan.Id:00}", 10, Vector2.zero, 2.2f,
                        new Color(1f, 0.35f, 0.08f, 0.48f));
                    break;
                case RaidRoomPurpose.Boss:
                    CreateMarker(room, "Marker - Boss", RaidSpawnMarkerKind.Boss,
                        "boss-primary", 1, Vector2.zero, 3f, new Color(1f, 0.05f, 0.08f, 0.55f));
                    CreateMarker(room, "Marker - Exit", RaidSpawnMarkerKind.ExitPortal,
                        "dungeon-exit", 6, new Vector2(plan.Size.x * 0.33f, 0f), 1.4f,
                        new Color(0.2f, 1f, 0.55f, 0.58f));
                    break;
                case RaidRoomPurpose.Reward:
                    CreateMarker(room, "Marker - Reward Chest", RaidSpawnMarkerKind.Chest,
                        $"reward-{plan.Id:00}", 1, Vector2.zero, 1.1f,
                        new Color(1f, 0.78f, 0.12f, 0.58f));
                    break;
            }
        }

        private void BindRaidFixture(DungeonRoomFirstPlan2D plan)
        {
            var entrance = generatedRooms.First(room => room.Purpose == RaidRoomPurpose.Entrance);
            var bossRoom = generatedRooms.First(room => room.Purpose == RaidRoomPurpose.Boss);
            var combatRooms = generatedRooms
                .Where(room => room.Purpose is RaidRoomPurpose.Encounter or RaidRoomPurpose.Transition)
                .OrderBy(room => room.Sequence)
                .ThenBy(room => room.RoomId, StringComparer.Ordinal)
                .ToArray();
            if (combatRooms.Length == 0) combatRooms = new[] { bossRoom };

            if (party != null)
            {
                PlaceFormation(party.Members, entrance.Center, 1.35f, -1.5f);
            }

            var activeEncounterPods = encounterPods is { Length: > 0 }
                ? encounterPods
                : primaryEnemyPod != null
                    ? new[] { primaryEnemyPod }
                    : Array.Empty<RaidEnemyPodBrain2D>();
            RaidRoom2D firstEncounterRoom = null;
            for (var index = 0; index < activeEncounterPods.Length; index++)
            {
                var pod = activeEncounterPods[index];
                if (pod == null) continue;
                var room = combatRooms[Mathf.Min(index, combatRooms.Length - 1)];
                firstEncounterRoom ??= room;
                pod.BindGeneratedRoom(room,
                    Mathf.Max(8f, Mathf.Min(room.Size.x, room.Size.y) * 0.55f));
                PlaceFormation(pod.Members, room.Center, 1.55f, 1.55f);
            }
            if (bossPod != null)
            {
                bossPod.BindGeneratedRoom(bossRoom,
                    Mathf.Max(11f, Mathf.Min(bossRoom.Size.x, bossRoom.Size.y) * 0.48f));
                PlaceFormation(bossPod.Members, bossRoom.Center, 1.4f, 1.4f);
            }
            if (primaryChest != null)
            {
                var chestRoom = firstEncounterRoom ?? bossRoom;
                primaryChest.transform.position = chestRoom.Center +
                                                  new Vector2(chestRoom.Size.x * 0.3f,
                                                      -chestRoom.Size.y * 0.3f);
            }
            director?.ConfigureGeneratedLayout(generatedRooms.ToArray(), generatedConnections.ToArray());
            BindNavigationToAgents();
        }

        private void RebuildNavigation()
        {
            if (navigation == null) navigation = GetComponent<DungeonNavigationGrid2D>();
            if (navigation == null) navigation = gameObject.AddComponent<DungeonNavigationGrid2D>();
            if (!navigation.Rebuild(generatedRoot, generatedRooms, generatedConnections))
            {
                throw new InvalidOperationException(
                    $"Generated dungeon seed {currentSeed} did not produce a traversable 2D navigation grid.");
            }
        }

        private void ScheduleRuntimeNavigationRefresh()
        {
            if (!Application.isPlaying) return;
            if (pendingNavigationRefresh != null)
            {
                StopCoroutine(pendingNavigationRefresh);
            }
            pendingNavigationRefresh = StartCoroutine(RefreshNavigationAfterPhysicsStep());
        }

        private IEnumerator RefreshNavigationAfterPhysicsStep()
        {
            yield return new WaitForFixedUpdate();
            Physics2D.SyncTransforms();
            RebuildNavigation();
            BindNavigationToAgents();
            pendingNavigationRefresh = null;
        }

        private void BindNavigationToAgents()
        {
            if (navigation == null) return;
            if (director != null)
            {
                BindNavigation(director.Hunters);
                BindNavigation(director.Monsters);
            }
            BindNavigation(party?.Members);
            BindNavigation(primaryEnemyPod?.Members);
            for (var index = 0; index < encounterPods.Length; index++)
            {
                BindNavigation(encounterPods[index]?.Members);
            }
            BindNavigation(bossPod?.Members);
        }

        private void BindNavigation(IReadOnlyList<RaidAgent2D> agents)
        {
            if (agents == null) return;
            for (var index = 0; index < agents.Count; index++)
            {
                agents[index]?.BindNavigation(navigation);
            }
        }

        private static void PlaceFormation(
            IReadOnlyList<RaidAgent2D> agents,
            Vector2 center,
            float spacing,
            float xBias)
        {
            if (agents == null) return;
            for (var index = 0; index < agents.Count; index++)
            {
                var row = index / 3;
                var column = index % 3;
                var position = center + new Vector2(
                    xBias + (column - 1) * spacing,
                    (row - 0.5f) * spacing);
                agents[index]?.PlaceAt(position);
            }
        }

        private void CreateObstacle(Transform room, string label, Vector2 position, Vector2 size)
        {
            CreateRect(room, $"Obstacle - {label}", position, size, wallSprite,
                new Color(0.075f, 0.09f, 0.11f), 2, true);
        }

        private void CreateMarker(
            Transform room,
            string label,
            RaidSpawnMarkerKind kind,
            string group,
            int capacity,
            Vector2 position,
            float radius,
            Color color)
        {
            var markerObject = CreateRect(room, label, position,
                Vector2.one * radius * 2f, floorSprite, color, -2, false);
            markerObject.AddComponent<RaidSpawnMarker2D>()
                .Configure(kind, group, capacity, radius);
        }

        private static DoorSides ResolveDoorSide(Vector2 direction)
        {
            if (Mathf.Abs(direction.x) >= Mathf.Abs(direction.y))
            {
                return direction.x >= 0f ? DoorSides.East : DoorSides.West;
            }
            return direction.y >= 0f ? DoorSides.North : DoorSides.South;
        }

        private GameObject CreateRect(
            Transform parent,
            string label,
            Vector2 localPosition,
            Vector2 worldSize,
            Sprite sprite,
            Color color,
            int sortingOrder,
            bool solid)
        {
            var target = new GameObject(label);
            target.transform.SetParent(parent, false);
            target.transform.localPosition = localPosition;
            var spriteSize = sprite != null ? sprite.bounds.size : Vector3.one;
            target.transform.localScale = new Vector3(
                worldSize.x / Mathf.Max(0.001f, spriteSize.x),
                worldSize.y / Mathf.Max(0.001f, spriteSize.y), 1f);
            var renderer = target.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            if (solid)
            {
                var collider = target.AddComponent<BoxCollider2D>();
                collider.isTrigger = false;
            }
            return target;
        }

        private static Vector2 Average(IReadOnlyList<Vector2> points)
        {
            var sum = Vector2.zero;
            for (var index = 0; index < points.Count; index++) sum += points[index];
            return points.Count > 0 ? sum / points.Count : Vector2.zero;
        }

        private static Color ResolveFloorColor(
            RaidRoomPurpose purpose,
            DungeonRoomTemplate2D template)
        {
            return purpose switch
            {
                RaidRoomPurpose.Entrance => new Color(0.18f, 0.31f, 0.34f),
                RaidRoomPurpose.Boss => new Color(0.35f, 0.14f, 0.17f),
                RaidRoomPurpose.Transition => new Color(0.28f, 0.22f, 0.34f),
                RaidRoomPurpose.Reward => new Color(0.36f, 0.31f, 0.16f),
                RaidRoomPurpose.Resource => new Color(0.16f, 0.32f, 0.28f),
                RaidRoomPurpose.Event => new Color(0.23f, 0.25f, 0.38f),
                _ => template == DungeonRoomTemplate2D.PillarHall
                    ? new Color(0.25f, 0.34f, 0.29f)
                    : new Color(0.24f, 0.29f, 0.31f)
            };
        }

        private static readonly Color WallColor = new(0.085f, 0.1f, 0.13f);

#if UNITY_EDITOR
        public void ConfigureEditor(
            int seed,
            bool rollNewSeedOnPlay,
            DungeonRaidDirector2D assignedDirector,
            RaidPartyBrain2D assignedParty,
            RaidEnemyPodBrain2D[] assignedEncounterPods,
            RaidEnemyPodBrain2D assignedBossPod,
            RaidChest2D assignedChest,
            Sprite assignedFloorSprite,
            Sprite assignedWallSprite)
        {
            previewSeed = seed;
            randomizeSeedOnPlay = rollNewSeedOnPlay;
            director = assignedDirector;
            party = assignedParty;
            encounterPods = assignedEncounterPods ?? Array.Empty<RaidEnemyPodBrain2D>();
            primaryEnemyPod = encounterPods.FirstOrDefault();
            bossPod = assignedBossPod;
            primaryChest = assignedChest;
            floorSprite = assignedFloorSprite;
            wallSprite = assignedWallSprite;
            settings ??= new DungeonRoomFirstSettings2D();
            settings.EnsurePartyScaleDefaults();
            settings.Sanitize();
            navigation = GetComponent<DungeonNavigationGrid2D>();
        }
#endif
    }
}
