using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Turtle.DungeonRaid
{
    [Serializable]
    public sealed class DungeonRoomFirstSettings2D
    {
        [Min(5)] public int minimumRooms = 8;
        [Min(5)] public int maximumRooms = 12;
        [Min(12f)] public float horizontalSpacing = 29f;
        [Min(12f)] public float verticalSpacing = 23f;
        [Range(0f, 4f)] public float positionJitter = 2.5f;
        [Min(2.5f)] public float corridorWidth = 4.5f;
        [Range(0, 3)] public int maximumLoops = 2;

        public void Sanitize()
        {
            minimumRooms = Mathf.Max(5, minimumRooms);
            maximumRooms = Mathf.Max(minimumRooms, maximumRooms);
            horizontalSpacing = Mathf.Max(12f, horizontalSpacing);
            verticalSpacing = Mathf.Max(12f, verticalSpacing);
            positionJitter = Mathf.Clamp(positionJitter, 0f, 4f);
            corridorWidth = Mathf.Max(2.5f, corridorWidth);
            maximumLoops = Mathf.Clamp(maximumLoops, 0, 3);
        }
    }

    public sealed class DungeonRoomFirstPlan2D
    {
        public sealed class Room
        {
            public int Id;
            public Vector2Int Cell;
            public Vector2 Center;
            public Vector2 Size;
            public int Sequence;
            public RaidRoomPurpose Purpose;
            public DungeonRoomTemplate2D Template;
        }

        public sealed class Connection
        {
            public int FromRoomId;
            public int ToRoomId;
            public float Width;
            public readonly List<Vector2> Waypoints = new(6);
        }

        public int Seed;
        public readonly List<Room> Rooms = new(16);
        public readonly List<Connection> Connections = new(20);
        public Room Entrance => Rooms.First(room => room.Purpose == RaidRoomPurpose.Entrance);
        public Room Boss => Rooms.First(room => room.Purpose == RaidRoomPurpose.Boss);

        public string StructuralSignature()
        {
            return string.Join("|", Rooms.OrderBy(room => room.Id).Select(room =>
                       $"R{room.Id}:{room.Cell.x},{room.Cell.y}:{room.Size.x:0.0},{room.Size.y:0.0}:{(int)room.Purpose}:{(int)room.Template}")) +
                   "#" + string.Join("|", Connections
                       .Select(connection => connection.FromRoomId < connection.ToRoomId
                           ? $"{connection.FromRoomId}-{connection.ToRoomId}"
                           : $"{connection.ToRoomId}-{connection.FromRoomId}")
                       .OrderBy(value => value, StringComparer.Ordinal));
        }
    }

    /// <summary>
    /// Pure, deterministic room-first planner. It owns no scene objects, so the
    /// same map seed can drive the visible dungeon and the off-screen raid model.
    /// </summary>
    public static class DungeonRoomFirstPlanner2D
    {
        private static readonly Vector2Int[] CardinalDirections =
        {
            Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down
        };

        private readonly struct Edge : IEquatable<Edge>
        {
            public readonly int A;
            public readonly int B;

            public Edge(int first, int second)
            {
                A = Mathf.Min(first, second);
                B = Mathf.Max(first, second);
            }

            public bool Equals(Edge other) => A == other.A && B == other.B;
            public override bool Equals(object obj) => obj is Edge other && Equals(other);
            public override int GetHashCode() => unchecked(A * 397 ^ B);
        }

        public static DungeonRoomFirstPlan2D Create(
            int seed,
            DungeonRoomFirstSettings2D sourceSettings = null)
        {
            var settings = sourceSettings ?? new DungeonRoomFirstSettings2D();
            settings.Sanitize();
            var random = new System.Random(seed);
            var plan = new DungeonRoomFirstPlan2D { Seed = seed };
            var targetCount = random.Next(settings.minimumRooms, settings.maximumRooms + 1);
            var cells = GrowConnectedCells(targetCount, random);

            for (var index = 0; index < cells.Count; index++)
            {
                var cell = cells[index];
                var width = RandomEven(random, 15, 23);
                var height = RandomEven(random, 13, 19);
                var jitter = new Vector2(
                    NextFloat(random, -settings.positionJitter, settings.positionJitter),
                    NextFloat(random, -settings.positionJitter, settings.positionJitter));
                plan.Rooms.Add(new DungeonRoomFirstPlan2D.Room
                {
                    Id = index,
                    Cell = cell,
                    Center = new Vector2(
                        cell.x * settings.horizontalSpacing,
                        cell.y * settings.verticalSpacing) + jitter,
                    Size = new Vector2(width, height),
                    Purpose = RaidRoomPurpose.Encounter,
                    Template = DungeonRoomTemplate2D.OpenArena
                });
            }

            var edges = BuildSpanningTree(plan.Rooms, random);
            AddLoops(plan.Rooms, edges, random, settings.maximumLoops);
            AssignSemantics(plan.Rooms, edges, random);
            TranslateEntranceToOrigin(plan.Rooms);
            BuildConnections(plan, edges, settings.corridorWidth, random);
            return plan;
        }

        public static bool Validate(DungeonRoomFirstPlan2D plan, out string reason)
        {
            if (plan == null || plan.Rooms.Count < 5)
            {
                reason = "Plan must contain at least five rooms.";
                return false;
            }
            if (plan.Rooms.Count(room => room.Purpose == RaidRoomPurpose.Entrance) != 1 ||
                plan.Rooms.Count(room => room.Purpose == RaidRoomPurpose.Boss) != 1)
            {
                reason = "Plan must contain exactly one entrance and one boss room.";
                return false;
            }
            var adjacency = BuildAdjacency(plan.Rooms.Count,
                plan.Connections.Select(connection =>
                    new Edge(connection.FromRoomId, connection.ToRoomId)));
            var visited = DistancesFrom(plan.Entrance.Id, adjacency);
            if (visited.Count != plan.Rooms.Count)
            {
                reason = "Every generated room must be reachable from the entrance.";
                return false;
            }
            if (plan.Connections.Any(connection => connection.Waypoints.Count < 2))
            {
                reason = "Every room connection must expose traversable waypoints.";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        private static List<Vector2Int> GrowConnectedCells(int targetCount, System.Random random)
        {
            var cells = new List<Vector2Int>(targetCount) { Vector2Int.zero };
            var occupied = new HashSet<Vector2Int> { Vector2Int.zero };
            while (cells.Count < targetCount)
            {
                var candidates = new List<Vector2Int>(cells.Count * 2);
                for (var index = 0; index < cells.Count; index++)
                {
                    for (var directionIndex = 0; directionIndex < CardinalDirections.Length; directionIndex++)
                    {
                        var candidate = cells[index] + CardinalDirections[directionIndex];
                        if (!occupied.Contains(candidate) && !candidates.Contains(candidate))
                        {
                            candidates.Add(candidate);
                        }
                    }
                }
                candidates.Sort((left, right) =>
                {
                    var leftDistance = Mathf.Abs(left.x) + Mathf.Abs(left.y);
                    var rightDistance = Mathf.Abs(right.x) + Mathf.Abs(right.y);
                    return leftDistance != rightDistance
                        ? leftDistance.CompareTo(rightDistance)
                        : left.x != right.x ? left.x.CompareTo(right.x) : left.y.CompareTo(right.y);
                });
                var picked = candidates[random.Next(candidates.Count)];
                occupied.Add(picked);
                cells.Add(picked);
            }
            return cells;
        }

        private static HashSet<Edge> BuildSpanningTree(
            IReadOnlyList<DungeonRoomFirstPlan2D.Room> rooms,
            System.Random random)
        {
            var byCell = rooms.ToDictionary(room => room.Cell, room => room.Id);
            var connected = new HashSet<int> { 0 };
            var edges = new HashSet<Edge>();
            while (connected.Count < rooms.Count)
            {
                var candidates = new List<Edge>();
                foreach (var roomId in connected.OrderBy(value => value))
                {
                    var cell = rooms[roomId].Cell;
                    for (var index = 0; index < CardinalDirections.Length; index++)
                    {
                        if (!byCell.TryGetValue(cell + CardinalDirections[index], out var neighbor) ||
                            connected.Contains(neighbor)) continue;
                        candidates.Add(new Edge(roomId, neighbor));
                    }
                }
                var edge = candidates[random.Next(candidates.Count)];
                edges.Add(edge);
                connected.Add(connected.Contains(edge.A) ? edge.B : edge.A);
            }
            return edges;
        }

        private static void AddLoops(
            IReadOnlyList<DungeonRoomFirstPlan2D.Room> rooms,
            HashSet<Edge> edges,
            System.Random random,
            int maximumLoops)
        {
            var byCell = rooms.ToDictionary(room => room.Cell, room => room.Id);
            var candidates = new List<Edge>();
            for (var roomIndex = 0; roomIndex < rooms.Count; roomIndex++)
            {
                var room = rooms[roomIndex];
                for (var directionIndex = 0; directionIndex < 2; directionIndex++)
                {
                    if (!byCell.TryGetValue(room.Cell + CardinalDirections[directionIndex], out var neighbor)) continue;
                    var edge = new Edge(room.Id, neighbor);
                    if (!edges.Contains(edge)) candidates.Add(edge);
                }
            }
            var loopCount = candidates.Count == 0 ? 0 : random.Next(0, Mathf.Min(maximumLoops, candidates.Count) + 1);
            for (var index = 0; index < loopCount; index++)
            {
                var candidateIndex = random.Next(candidates.Count);
                edges.Add(candidates[candidateIndex]);
                candidates.RemoveAt(candidateIndex);
            }
        }

        private static void AssignSemantics(
            IReadOnlyList<DungeonRoomFirstPlan2D.Room> rooms,
            HashSet<Edge> edges,
            System.Random random)
        {
            var adjacency = BuildAdjacency(rooms.Count, edges);
            var endpointA = FarthestFrom(0, adjacency);
            var endpointB = FarthestFrom(endpointA, adjacency);
            var path = FindPath(endpointA, endpointB, adjacency);
            var pathSet = path.ToHashSet();
            var distances = DistancesFrom(endpointA, adjacency);

            for (var index = 0; index < rooms.Count; index++)
            {
                rooms[index].Sequence = distances[index];
                rooms[index].Purpose = RaidRoomPurpose.Encounter;
                rooms[index].Template = RandomEncounterTemplate(random);
            }

            rooms[endpointA].Purpose = RaidRoomPurpose.Entrance;
            rooms[endpointA].Template = DungeonRoomTemplate2D.OpenArena;
            rooms[endpointA].Size = new Vector2(19f, 16f);
            rooms[endpointB].Purpose = RaidRoomPurpose.Boss;
            rooms[endpointB].Template = random.Next(2) == 0
                ? DungeonRoomTemplate2D.BossPillars
                : DungeonRoomTemplate2D.BossOpen;
            rooms[endpointB].Size = new Vector2(28f, 23f);

            if (path.Count >= 3)
            {
                var transition = rooms[path[^2]];
                transition.Purpose = RaidRoomPurpose.Transition;
                transition.Template = DungeonRoomTemplate2D.BossAntechamber;
                transition.Size = new Vector2(15f, 13f);
            }

            var branchIndex = 0;
            foreach (var room in rooms.Where(room => !pathSet.Contains(room.Id)).OrderBy(room => room.Id))
            {
                switch (branchIndex++ % 3)
                {
                    case 0:
                        room.Purpose = RaidRoomPurpose.Reward;
                        room.Template = DungeonRoomTemplate2D.RewardCache;
                        break;
                    case 1:
                        room.Purpose = RaidRoomPurpose.Resource;
                        room.Template = DungeonRoomTemplate2D.ResourcePocket;
                        break;
                    default:
                        room.Purpose = RaidRoomPurpose.Event;
                        room.Template = DungeonRoomTemplate2D.Crossroads;
                        break;
                }
            }
        }

        private static void TranslateEntranceToOrigin(IReadOnlyList<DungeonRoomFirstPlan2D.Room> rooms)
        {
            var offset = rooms.First(room => room.Purpose == RaidRoomPurpose.Entrance).Center;
            for (var index = 0; index < rooms.Count; index++) rooms[index].Center -= offset;
        }

        private static void BuildConnections(
            DungeonRoomFirstPlan2D plan,
            IEnumerable<Edge> edges,
            float width,
            System.Random random)
        {
            foreach (var edge in edges.OrderBy(edge => edge.A).ThenBy(edge => edge.B))
            {
                var from = plan.Rooms[edge.A];
                var to = plan.Rooms[edge.B];
                var connection = new DungeonRoomFirstPlan2D.Connection
                {
                    FromRoomId = from.Id,
                    ToRoomId = to.Id,
                    Width = width
                };
                BuildOrthogonalWaypoints(from, to, connection.Waypoints, random.Next(2) == 0);
                plan.Connections.Add(connection);
            }
        }

        private static void BuildOrthogonalWaypoints(
            DungeonRoomFirstPlan2D.Room from,
            DungeonRoomFirstPlan2D.Room to,
            List<Vector2> waypoints,
            bool alternateBend)
        {
            var delta = to.Center - from.Center;
            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
            {
                var sign = Mathf.Sign(delta.x);
                var start = from.Center + Vector2.right * sign * from.Size.x * 0.5f;
                var end = to.Center - Vector2.right * sign * to.Size.x * 0.5f;
                var bendX = alternateBend
                    ? Mathf.Lerp(start.x, end.x, 0.35f)
                    : Mathf.Lerp(start.x, end.x, 0.65f);
                AddDistinct(waypoints, start);
                AddDistinct(waypoints, new Vector2(bendX, start.y));
                AddDistinct(waypoints, new Vector2(bendX, end.y));
                AddDistinct(waypoints, end);
            }
            else
            {
                var sign = Mathf.Sign(delta.y);
                var start = from.Center + Vector2.up * sign * from.Size.y * 0.5f;
                var end = to.Center - Vector2.up * sign * to.Size.y * 0.5f;
                var bendY = alternateBend
                    ? Mathf.Lerp(start.y, end.y, 0.35f)
                    : Mathf.Lerp(start.y, end.y, 0.65f);
                AddDistinct(waypoints, start);
                AddDistinct(waypoints, new Vector2(start.x, bendY));
                AddDistinct(waypoints, new Vector2(end.x, bendY));
                AddDistinct(waypoints, end);
            }
        }

        private static void AddDistinct(List<Vector2> points, Vector2 point)
        {
            if (points.Count == 0 || Vector2.SqrMagnitude(points[^1] - point) > 0.01f) points.Add(point);
        }

        private static Dictionary<int, List<int>> BuildAdjacency(int roomCount, IEnumerable<Edge> edges)
        {
            var result = new Dictionary<int, List<int>>(roomCount);
            for (var index = 0; index < roomCount; index++) result[index] = new List<int>(4);
            foreach (var edge in edges)
            {
                result[edge.A].Add(edge.B);
                result[edge.B].Add(edge.A);
            }
            foreach (var neighbors in result.Values) neighbors.Sort();
            return result;
        }

        private static Dictionary<int, int> DistancesFrom(int start, Dictionary<int, List<int>> adjacency)
        {
            var result = new Dictionary<int, int> { [start] = 0 };
            var queue = new Queue<int>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var neighbor in adjacency[current])
                {
                    if (result.ContainsKey(neighbor)) continue;
                    result[neighbor] = result[current] + 1;
                    queue.Enqueue(neighbor);
                }
            }
            return result;
        }

        private static int FarthestFrom(int start, Dictionary<int, List<int>> adjacency)
        {
            return DistancesFrom(start, adjacency)
                .OrderByDescending(pair => pair.Value)
                .ThenBy(pair => pair.Key)
                .First().Key;
        }

        private static List<int> FindPath(int start, int destination, Dictionary<int, List<int>> adjacency)
        {
            var previous = new Dictionary<int, int>();
            var visited = new HashSet<int> { start };
            var queue = new Queue<int>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current == destination) break;
                foreach (var neighbor in adjacency[current])
                {
                    if (!visited.Add(neighbor)) continue;
                    previous[neighbor] = current;
                    queue.Enqueue(neighbor);
                }
            }
            var path = new List<int> { destination };
            while (path[^1] != start) path.Add(previous[path[^1]]);
            path.Reverse();
            return path;
        }

        private static DungeonRoomTemplate2D RandomEncounterTemplate(System.Random random)
        {
            var templates = new[]
            {
                DungeonRoomTemplate2D.OpenArena,
                DungeonRoomTemplate2D.PillarHall,
                DungeonRoomTemplate2D.SplitHall,
                DungeonRoomTemplate2D.Crossroads,
                DungeonRoomTemplate2D.Ring,
                DungeonRoomTemplate2D.AmbushCover
            };
            return templates[random.Next(templates.Length)];
        }

        private static int RandomEven(System.Random random, int minimum, int maximum)
        {
            var value = random.Next(minimum, maximum + 1);
            return value % 2 == 0 ? value : value + 1;
        }

        private static float NextFloat(System.Random random, float minimum, float maximum)
        {
            return minimum + (float)random.NextDouble() * (maximum - minimum);
        }
    }
}
