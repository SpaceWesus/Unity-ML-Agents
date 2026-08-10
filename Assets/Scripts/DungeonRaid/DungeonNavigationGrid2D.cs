using System;
using System.Collections.Generic;
using UnityEngine;

namespace Turtle.DungeonRaid
{
    /// <summary>
    /// Runtime navigation representation for a generated top-down dungeon.
    /// Unity's NavMeshSurface collects 3D geometry, so the 2D raid uses a grid
    /// baked from its room/corridor geometry and inflated Collider2D blockers.
    /// All raid agents query this shared data and keep Rigidbody2D as the final
    /// authority for movement and local collision response.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class DungeonNavigationGrid2D : MonoBehaviour
    {
        private readonly struct OpenNode
        {
            public readonly int Index;
            public readonly float Priority;

            public OpenNode(int index, float priority)
            {
                Index = index;
                Priority = priority;
            }
        }

        private static readonly Vector2Int[] NeighborOffsets =
        {
            new(1, 0), new(-1, 0), new(0, 1), new(0, -1),
            new(1, 1), new(1, -1), new(-1, 1), new(-1, -1)
        };

        [Header("2D Navigation Bake")]
        [SerializeField, Min(0.35f)] private float cellSize = 0.8f;
        [SerializeField, Min(0.1f)] private float agentClearance = 0.62f;
        [SerializeField, Min(1)] private int nearestCellSearchRadius = 14;
        [SerializeField] private bool drawNavigationGizmos = true;
        [SerializeField] private Transform geometryRoot;

        private bool[] walkable = Array.Empty<bool>();
        private int[] visitedVersion = Array.Empty<int>();
        private int[] closedVersion = Array.Empty<int>();
        private int[] cameFrom = Array.Empty<int>();
        private float[] pathCost = Array.Empty<float>();
        private int gridWidth;
        private int gridHeight;
        private int walkableCellCount;
        private int searchVersion;
        private Vector2 gridOrigin;
        private Rect gridBounds;
        private readonly List<OpenNode> openHeap = new(512);
        private readonly List<int> reversePath = new(128);
        private readonly List<Vector2> rawPath = new(128);
        private readonly List<Vector2> connectivityPath = new(128);

        public bool IsReady => walkableCellCount > 0 &&
                               walkable.Length == gridWidth * gridHeight;
        public int WalkableCellCount => walkableCellCount;
        public float CellSize => cellSize;
        public float AgentClearance => agentClearance;
        public Rect GridBounds => gridBounds;
        public Transform GeometryRoot => geometryRoot;

        private void OnEnable()
        {
            if (geometryRoot != null && !IsReady)
            {
                RebuildFromGeneratedGeometry();
            }
        }

        private void OnValidate()
        {
            cellSize = Mathf.Max(0.35f, cellSize);
            agentClearance = Mathf.Max(0.1f, agentClearance);
            nearestCellSearchRadius = Mathf.Max(1, nearestCellSearchRadius);
        }

        public bool RebuildFromGeneratedGeometry()
        {
            if (geometryRoot == null)
            {
                ClearRuntimeData();
                return false;
            }

            var rooms = geometryRoot.GetComponentsInChildren<RaidRoom2D>(false);
            var connections = geometryRoot.GetComponentsInChildren<RaidRoomConnection2D>(false);
            return Rebuild(geometryRoot, rooms, connections);
        }

        public bool Rebuild(
            Transform generatedGeometryRoot,
            IReadOnlyList<RaidRoom2D> rooms,
            IReadOnlyList<RaidRoomConnection2D> connections)
        {
            geometryRoot = generatedGeometryRoot;
            if (geometryRoot == null || rooms == null || rooms.Count == 0)
            {
                ClearRuntimeData();
                return false;
            }

            Physics2D.SyncTransforms();
            CalculateGridBounds(rooms, connections);
            gridWidth = Mathf.Max(1, Mathf.CeilToInt(gridBounds.width / cellSize));
            gridHeight = Mathf.Max(1, Mathf.CeilToInt(gridBounds.height / cellSize));
            gridOrigin = gridBounds.min;

            var cellCount = gridWidth * gridHeight;
            walkable = new bool[cellCount];
            visitedVersion = new int[cellCount];
            closedVersion = new int[cellCount];
            cameFrom = new int[cellCount];
            pathCost = new float[cellCount];
            walkableCellCount = 0;
            searchVersion = 0;

            var blockers = geometryRoot.GetComponentsInChildren<BoxCollider2D>(false);
            for (var y = 0; y < gridHeight; y++)
            {
                for (var x = 0; x < gridWidth; x++)
                {
                    var index = ToIndex(x, y);
                    var point = CellCenter(x, y);
                    var canWalk = IsOnDungeonFloor(point, rooms, connections) &&
                                  !IsBlocked(point, blockers);
                    walkable[index] = canWalk;
                    if (canWalk) walkableCellCount++;
                }
            }

            return IsReady && AreAllRoomsConnected(rooms);
        }

        public bool IsWalkable(Vector2 worldPoint)
        {
            return IsReady && TryWorldToCell(worldPoint, out var x, out var y) &&
                   walkable[ToIndex(x, y)];
        }

        public bool IsBlockedByCurrentGeometry(Vector2 worldPoint)
        {
            if (geometryRoot == null) return false;
            return IsBlocked(
                worldPoint,
                geometryRoot.GetComponentsInChildren<BoxCollider2D>(false));
        }

        public bool TryFindPath(Vector2 startWorld, Vector2 destinationWorld, List<Vector2> result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            result.Clear();
            if (!IsReady ||
                !TryFindNearestWalkable(startWorld, out var startIndex) ||
                !TryFindNearestWalkable(destinationWorld, out var destinationIndex))
            {
                return false;
            }

            var startCenter = IndexCenter(startIndex);
            var destinationCenter = IndexCenter(destinationIndex);
            if (startIndex == destinationIndex || HasLineOfSight(startCenter, destinationCenter))
            {
                result.Add(startWorld);
                var resolvedDestination = IsWalkable(destinationWorld)
                    ? destinationWorld
                    : destinationCenter;
                if (Vector2.SqrMagnitude(result[^1] - resolvedDestination) > 0.01f)
                {
                    result.Add(resolvedDestination);
                }
                return true;
            }

            BeginSearch();
            openHeap.Clear();
            reversePath.Clear();
            rawPath.Clear();

            visitedVersion[startIndex] = searchVersion;
            pathCost[startIndex] = 0f;
            cameFrom[startIndex] = -1;
            PushOpen(startIndex, Heuristic(startIndex, destinationIndex));

            var found = false;
            while (openHeap.Count > 0)
            {
                var current = PopOpen().Index;
                if (closedVersion[current] == searchVersion) continue;
                if (current == destinationIndex)
                {
                    found = true;
                    break;
                }

                closedVersion[current] = searchVersion;
                var currentX = current % gridWidth;
                var currentY = current / gridWidth;
                for (var neighborIndex = 0; neighborIndex < NeighborOffsets.Length; neighborIndex++)
                {
                    var offset = NeighborOffsets[neighborIndex];
                    var nextX = currentX + offset.x;
                    var nextY = currentY + offset.y;
                    if (!IsCellWalkable(nextX, nextY)) continue;
                    if (offset.x != 0 && offset.y != 0 &&
                        (!IsCellWalkable(currentX + offset.x, currentY) ||
                         !IsCellWalkable(currentX, currentY + offset.y)))
                    {
                        continue;
                    }

                    var next = ToIndex(nextX, nextY);
                    if (closedVersion[next] == searchVersion) continue;
                    var stepCost = offset.x == 0 || offset.y == 0 ? 1f : 1.41421356f;
                    var tentativeCost = pathCost[current] + stepCost;
                    if (visitedVersion[next] == searchVersion && tentativeCost >= pathCost[next])
                    {
                        continue;
                    }

                    visitedVersion[next] = searchVersion;
                    pathCost[next] = tentativeCost;
                    cameFrom[next] = current;
                    PushOpen(next, tentativeCost + Heuristic(next, destinationIndex));
                }
            }

            if (!found) return false;
            BuildSmoothedPath(startWorld, destinationWorld, startIndex, destinationIndex, result);
            return result.Count > 0;
        }

        private void CalculateGridBounds(
            IReadOnlyList<RaidRoom2D> rooms,
            IReadOnlyList<RaidRoomConnection2D> connections)
        {
            var minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (var index = 0; index < rooms.Count; index++)
            {
                var room = rooms[index];
                if (room == null) continue;
                minimum = Vector2.Min(minimum, room.Bounds.min);
                maximum = Vector2.Max(maximum, room.Bounds.max);
            }

            if (connections != null)
            {
                for (var connectionIndex = 0; connectionIndex < connections.Count; connectionIndex++)
                {
                    var connection = connections[connectionIndex];
                    if (connection == null) continue;
                    var padding = Vector2.one * connection.Width * 0.5f;
                    for (var pointIndex = 0; pointIndex < connection.WaypointCount; pointIndex++)
                    {
                        var point = connection.GetWaypoint(pointIndex);
                        minimum = Vector2.Min(minimum, point - padding);
                        maximum = Vector2.Max(maximum, point + padding);
                    }
                }
            }

            if (!float.IsFinite(minimum.x) || !float.IsFinite(maximum.x))
            {
                minimum = Vector2.zero;
                maximum = Vector2.one;
            }
            var margin = Vector2.one * (cellSize + agentClearance);
            minimum -= margin;
            maximum += margin;
            gridBounds = Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
        }

        private bool IsOnDungeonFloor(
            Vector2 point,
            IReadOnlyList<RaidRoom2D> rooms,
            IReadOnlyList<RaidRoomConnection2D> connections)
        {
            for (var index = 0; index < rooms.Count; index++)
            {
                if (rooms[index] != null && rooms[index].Contains(point)) return true;
            }

            if (connections == null) return false;
            for (var connectionIndex = 0; connectionIndex < connections.Count; connectionIndex++)
            {
                var connection = connections[connectionIndex];
                if (connection == null || connection.WaypointCount < 2) continue;
                var radius = Mathf.Max(cellSize * 0.35f,
                    connection.Width * 0.5f - agentClearance * 0.35f);
                var radiusSquared = radius * radius;
                for (var pointIndex = 0; pointIndex < connection.WaypointCount - 1; pointIndex++)
                {
                    if (DistanceToSegmentSquared(
                            point,
                            connection.GetWaypoint(pointIndex),
                            connection.GetWaypoint(pointIndex + 1)) <= radiusSquared)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private bool IsBlocked(Vector2 point, IReadOnlyList<BoxCollider2D> blockers)
        {
            for (var index = 0; index < blockers.Count; index++)
            {
                var blocker = blockers[index];
                if (blocker == null || !blocker.enabled || blocker.isTrigger) continue;
                var bounds = CalculateWorldBounds(blocker);
                // Structural rails still need to block paths, but inflating
                // both sides of a generated doorway can erase its discrete
                // threshold cells. Interior cover receives full clearance;
                // walls retain their authored footprint and Rigidbody2D keeps
                // the final agent-radius separation during movement.
                if (blocker.name.StartsWith("Wall", StringComparison.OrdinalIgnoreCase))
                {
                    var rasterPadding = Mathf.Min(agentClearance, cellSize * 0.55f);
                    bounds.Expand(rasterPadding * 2f);
                    if (Contains2D(bounds, point)) return true;
                    continue;
                }
                bounds.Expand(agentClearance * 2f);
                if (Contains2D(bounds, point)) return true;
            }
            return false;
        }

        private static Bounds CalculateWorldBounds(BoxCollider2D collider)
        {
            var center = collider.transform.TransformPoint(collider.offset);
            var halfRight = collider.transform.TransformVector(
                Vector3.right * (collider.size.x * 0.5f));
            var halfUp = collider.transform.TransformVector(
                Vector3.up * (collider.size.y * 0.5f));
            var extents = new Vector3(
                Mathf.Abs(halfRight.x) + Mathf.Abs(halfUp.x),
                Mathf.Abs(halfRight.y) + Mathf.Abs(halfUp.y),
                0.05f);
            return new Bounds(center, extents * 2f);
        }

        private static bool Contains2D(Bounds bounds, Vector2 point)
        {
            return point.x >= bounds.min.x && point.x <= bounds.max.x &&
                   point.y >= bounds.min.y && point.y <= bounds.max.y;
        }

        private bool AreAllRoomsConnected(IReadOnlyList<RaidRoom2D> rooms)
        {
            RaidRoom2D origin = null;
            for (var index = 0; index < rooms.Count; index++)
            {
                if (rooms[index] == null) continue;
                origin = rooms[index];
                break;
            }
            if (origin == null) return false;

            for (var index = 0; index < rooms.Count; index++)
            {
                var room = rooms[index];
                if (room == null) continue;
                connectivityPath.Clear();
                if (!TryFindPath(origin.Center, room.Center, connectivityPath)) return false;
            }
            return true;
        }

        private bool TryFindNearestWalkable(Vector2 worldPoint, out int result)
        {
            WorldToCellClamped(worldPoint, out var centerX, out var centerY);
            if (IsCellWalkable(centerX, centerY))
            {
                result = ToIndex(centerX, centerY);
                return true;
            }

            for (var radius = 1; radius <= nearestCellSearchRadius; radius++)
            {
                var minimumX = centerX - radius;
                var maximumX = centerX + radius;
                var minimumY = centerY - radius;
                var maximumY = centerY + radius;
                for (var x = minimumX; x <= maximumX; x++)
                {
                    if (TryResolveWalkable(x, minimumY, out result) ||
                        TryResolveWalkable(x, maximumY, out result)) return true;
                }
                for (var y = minimumY + 1; y < maximumY; y++)
                {
                    if (TryResolveWalkable(minimumX, y, out result) ||
                        TryResolveWalkable(maximumX, y, out result)) return true;
                }
            }

            result = -1;
            return false;
        }

        private bool TryResolveWalkable(int x, int y, out int result)
        {
            if (IsCellWalkable(x, y))
            {
                result = ToIndex(x, y);
                return true;
            }
            result = -1;
            return false;
        }

        private void BuildSmoothedPath(
            Vector2 startWorld,
            Vector2 destinationWorld,
            int startIndex,
            int destinationIndex,
            List<Vector2> result)
        {
            var cursor = destinationIndex;
            reversePath.Add(cursor);
            while (cursor != startIndex && cursor >= 0)
            {
                cursor = cameFrom[cursor];
                if (cursor >= 0) reversePath.Add(cursor);
            }

            for (var index = reversePath.Count - 1; index >= 0; index--)
            {
                rawPath.Add(IndexCenter(reversePath[index]));
            }

            result.Add(startWorld);
            var anchor = 0;
            while (anchor < rawPath.Count - 1)
            {
                var next = rawPath.Count - 1;
                while (next > anchor + 1 && !HasLineOfSight(rawPath[anchor], rawPath[next]))
                {
                    next--;
                }
                result.Add(rawPath[next]);
                anchor = next;
            }

            if (IsWalkable(destinationWorld) &&
                HasLineOfSight(result[^1], destinationWorld) &&
                Vector2.SqrMagnitude(result[^1] - destinationWorld) > 0.01f)
            {
                result.Add(destinationWorld);
            }
        }

        private bool HasLineOfSight(Vector2 from, Vector2 to)
        {
            var distance = Vector2.Distance(from, to);
            var samples = Mathf.Max(1, Mathf.CeilToInt(distance / (cellSize * 0.45f)));
            for (var index = 0; index <= samples; index++)
            {
                if (!IsWalkable(Vector2.Lerp(from, to, index / (float)samples))) return false;
            }
            return true;
        }

        private void BeginSearch()
        {
            if (searchVersion == int.MaxValue)
            {
                Array.Clear(visitedVersion, 0, visitedVersion.Length);
                Array.Clear(closedVersion, 0, closedVersion.Length);
                searchVersion = 1;
                return;
            }
            searchVersion++;
        }

        private void PushOpen(int index, float priority)
        {
            openHeap.Add(new OpenNode(index, priority));
            var child = openHeap.Count - 1;
            while (child > 0)
            {
                var parent = (child - 1) / 2;
                if (openHeap[parent].Priority <= openHeap[child].Priority) break;
                (openHeap[parent], openHeap[child]) = (openHeap[child], openHeap[parent]);
                child = parent;
            }
        }

        private OpenNode PopOpen()
        {
            var result = openHeap[0];
            var last = openHeap[^1];
            openHeap.RemoveAt(openHeap.Count - 1);
            if (openHeap.Count == 0) return result;
            openHeap[0] = last;
            var parent = 0;
            while (true)
            {
                var left = parent * 2 + 1;
                if (left >= openHeap.Count) break;
                var right = left + 1;
                var smallest = right < openHeap.Count &&
                               openHeap[right].Priority < openHeap[left].Priority
                    ? right
                    : left;
                if (openHeap[parent].Priority <= openHeap[smallest].Priority) break;
                (openHeap[parent], openHeap[smallest]) = (openHeap[smallest], openHeap[parent]);
                parent = smallest;
            }
            return result;
        }

        private float Heuristic(int from, int to)
        {
            var fromX = from % gridWidth;
            var fromY = from / gridWidth;
            var toX = to % gridWidth;
            var toY = to / gridWidth;
            var dx = Mathf.Abs(fromX - toX);
            var dy = Mathf.Abs(fromY - toY);
            return Mathf.Max(dx, dy) + 0.41421356f * Mathf.Min(dx, dy);
        }

        private bool IsCellWalkable(int x, int y)
        {
            return x >= 0 && x < gridWidth && y >= 0 && y < gridHeight &&
                   walkable[ToIndex(x, y)];
        }

        private bool TryWorldToCell(Vector2 point, out int x, out int y)
        {
            x = Mathf.FloorToInt((point.x - gridOrigin.x) / cellSize);
            y = Mathf.FloorToInt((point.y - gridOrigin.y) / cellSize);
            return x >= 0 && x < gridWidth && y >= 0 && y < gridHeight;
        }

        private void WorldToCellClamped(Vector2 point, out int x, out int y)
        {
            x = Mathf.Clamp(Mathf.FloorToInt((point.x - gridOrigin.x) / cellSize), 0, gridWidth - 1);
            y = Mathf.Clamp(Mathf.FloorToInt((point.y - gridOrigin.y) / cellSize), 0, gridHeight - 1);
        }

        private int ToIndex(int x, int y) => y * gridWidth + x;

        private Vector2 CellCenter(int x, int y)
        {
            return gridOrigin + new Vector2((x + 0.5f) * cellSize, (y + 0.5f) * cellSize);
        }

        private Vector2 IndexCenter(int index)
        {
            return CellCenter(index % gridWidth, index / gridWidth);
        }

        private static float DistanceToSegmentSquared(Vector2 point, Vector2 start, Vector2 end)
        {
            var segment = end - start;
            var lengthSquared = segment.sqrMagnitude;
            if (lengthSquared <= 0.000001f) return Vector2.SqrMagnitude(point - start);
            var t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSquared);
            return Vector2.SqrMagnitude(point - (start + segment * t));
        }

        private void ClearRuntimeData()
        {
            walkable = Array.Empty<bool>();
            visitedVersion = Array.Empty<int>();
            closedVersion = Array.Empty<int>();
            cameFrom = Array.Empty<int>();
            pathCost = Array.Empty<float>();
            gridWidth = 0;
            gridHeight = 0;
            walkableCellCount = 0;
            gridBounds = default;
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawNavigationGizmos || !IsReady) return;
            Gizmos.color = new Color(0.1f, 0.9f, 0.75f, 0.18f);
            var stride = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(walkableCellCount / 6000f)));
            for (var y = 0; y < gridHeight; y += stride)
            {
                for (var x = 0; x < gridWidth; x += stride)
                {
                    if (!walkable[ToIndex(x, y)]) continue;
                    Gizmos.DrawWireCube(CellCenter(x, y),
                        new Vector3(cellSize * stride, cellSize * stride, 0f));
                }
            }
        }
    }
}
