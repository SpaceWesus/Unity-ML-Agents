using System;
using System.Collections.Generic;
using UnityEngine;

namespace Turtle.Ecosystem
{
    /// <summary>
    /// Selects how dimension-neutral ecosystem coordinates are projected into a Unity scene.
    /// The current 2D slice uses XY. A later 3D presentation can reuse the same logical
    /// coordinates on XZ without changing simulation data.
    /// </summary>
    public enum EcosystemSpatialPlane
    {
        XY,
        XZ
    }

    public static class EcosystemSpatialCoordinates
    {
        public static Vector3 ToWorld(
            Vector2 planarPosition,
            EcosystemSpatialPlane plane,
            float perpendicularPosition = 0f)
        {
            return plane == EcosystemSpatialPlane.XY
                ? new Vector3(planarPosition.x, planarPosition.y, perpendicularPosition)
                : new Vector3(planarPosition.x, perpendicularPosition, planarPosition.y);
        }

        public static Vector2 ToPlanar(Vector3 worldPosition, EcosystemSpatialPlane plane)
        {
            return plane == EcosystemSpatialPlane.XY
                ? new Vector2(worldPosition.x, worldPosition.y)
                : new Vector2(worldPosition.x, worldPosition.z);
        }

        public static Vector3 WithPlanar(
            Vector3 currentWorldPosition,
            Vector2 planarPosition,
            EcosystemSpatialPlane plane)
        {
            return plane == EcosystemSpatialPlane.XY
                ? new Vector3(planarPosition.x, planarPosition.y, currentWorldPosition.z)
                : new Vector3(planarPosition.x, currentWorldPosition.y, planarPosition.y);
        }
    }

    /// <summary>
    /// Read-only seam for a future canonical spatial simulation. The spatial presenter uses
    /// this source when supplied and otherwise derives a non-authoritative travel projection
    /// from the current location/destination fields.
    /// </summary>
    public interface IEcosystemSpatialPoseSource
    {
        long SpatialRevision { get; }

        bool TryGetHunterPose(string hunterId, out EcosystemSpatialPose pose);
    }

    public readonly struct EcosystemSpatialPose
    {
        public EcosystemSpatialPose(
            Vector2 planarPosition,
            Vector2 planarFacing,
            bool isMoving,
            string layerId = "overworld")
        {
            PlanarPosition = planarPosition;
            PlanarFacing = planarFacing.sqrMagnitude > 0.0001f
                ? planarFacing.normalized
                : Vector2.up;
            IsMoving = isMoving;
            LayerId = string.IsNullOrWhiteSpace(layerId) ? "overworld" : layerId;
        }

        public Vector2 PlanarPosition { get; }
        public Vector2 PlanarFacing { get; }
        public bool IsMoving { get; }
        public string LayerId { get; }
    }

    /// <summary>
    /// Optional read-only seam for a loaded encounter. It lets a future canonical combat
    /// state override the campaign-level displayed vitals without giving the view ownership
    /// of combat state or random outcomes.
    /// </summary>
    public interface IEcosystemEncounterPresentationSource
    {
        long EncounterRevision { get; }

        bool TryGetHunterPresentation(
            string hunterId,
            out EcosystemEncounterPresentation presentation);
    }

    public readonly struct EcosystemEncounterPresentation
    {
        public EcosystemEncounterPresentation(
            float healthRatio,
            float manaRatio,
            float shieldRatio,
            bool visible = true)
        {
            HealthRatio = Mathf.Clamp01(healthRatio);
            ManaRatio = Mathf.Clamp01(manaRatio);
            ShieldRatio = Mathf.Clamp01(shieldRatio);
            Visible = visible;
        }

        public float HealthRatio { get; }
        public float ManaRatio { get; }
        public float ShieldRatio { get; }
        public bool Visible { get; }
    }

    [Serializable]
    public sealed class EcosystemLocationAnchorAuthoring
    {
        [SerializeField] private string locationId = string.Empty;
        [SerializeField] private Transform anchor;
        [SerializeField] private Transform arrivalPoint;
        [SerializeField] private Transform[] occupancyPoints = Array.Empty<Transform>();
        [SerializeField] private Collider2D selectionCollider2D;
        [SerializeField] private Collider selectionCollider3D;

        public string LocationId => locationId;
        public Transform Anchor => anchor;
        public Collider2D SelectionCollider2D => selectionCollider2D;
        public Collider SelectionCollider3D => selectionCollider3D;

        public Vector2 PlanarPosition(EcosystemSpatialPlane plane)
        {
            var source = arrivalPoint != null ? arrivalPoint : anchor;
            return source != null
                ? EcosystemSpatialCoordinates.ToPlanar(source.position, plane)
                : Vector2.zero;
        }

        public Vector2 OccupancyPosition(
            string stableActorId,
            int ordinal,
            float spacing,
            EcosystemSpatialPlane plane)
        {
            if (occupancyPoints != null && occupancyPoints.Length > 0)
            {
                var index = Mathf.Abs(ordinal) % occupancyPoints.Length;
                var point = occupancyPoints[index];
                if (point != null)
                {
                    return EcosystemSpatialCoordinates.ToPlanar(point.position, plane);
                }
            }

            // Golden-angle placement is deterministic, allocation-free, and keeps a crowd
            // legible even before a location has hand-authored occupancy points.
            var hash = EcosystemDeterministicRandom.StableHash(stableActorId ?? string.Empty);
            var offsetOrdinal = Mathf.Max(0, ordinal) + (int)(hash % 7u);
            var angle = offsetOrdinal * 2.39996323f + hash % 360u * Mathf.Deg2Rad;
            var radius = spacing * (0.55f + Mathf.Sqrt(offsetOrdinal + 1f) * 0.7f);
            return PlanarPosition(plane) + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

#if UNITY_EDITOR
        public void ConfigureEditor(
            string stableLocationId,
            Transform authoredAnchor,
            Transform authoredArrivalPoint,
            Transform[] authoredOccupancyPoints,
            Collider2D collider2D,
            Collider collider3D)
        {
            locationId = stableLocationId ?? string.Empty;
            anchor = authoredAnchor;
            arrivalPoint = authoredArrivalPoint;
            occupancyPoints = authoredOccupancyPoints ?? Array.Empty<Transform>();
            selectionCollider2D = collider2D;
            selectionCollider3D = collider3D;
        }
#endif
    }

    [Serializable]
    public sealed class EcosystemRoutePathAuthoring
    {
        [SerializeField] private string fromLocationId = string.Empty;
        [SerializeField] private string toLocationId = string.Empty;
        [SerializeField] private Transform[] waypoints = Array.Empty<Transform>();
        [SerializeField] private LineRenderer routeRenderer;

        public string FromLocationId => fromLocationId;
        public string ToLocationId => toLocationId;
        public LineRenderer RouteRenderer => routeRenderer;

        public bool Connects(string firstLocationId, string secondLocationId)
        {
            return (fromLocationId == firstLocationId && toLocationId == secondLocationId) ||
                   (fromLocationId == secondLocationId && toLocationId == firstLocationId);
        }

        public Vector2 EvaluatePlanar(
            float normalizedProgress,
            string journeyOriginId,
            Vector2 fallbackStart,
            Vector2 fallbackEnd,
            EcosystemSpatialPlane plane)
        {
            normalizedProgress = Mathf.Clamp01(normalizedProgress);
            if (waypoints == null || waypoints.Length < 2)
            {
                return Vector2.Lerp(fallbackStart, fallbackEnd, normalizedProgress);
            }

            var reverse = journeyOriginId == toLocationId;
            var totalLength = 0f;
            for (var index = 1; index < waypoints.Length; index++)
            {
                if (waypoints[index - 1] == null || waypoints[index] == null)
                {
                    continue;
                }
                totalLength += Vector2.Distance(
                    EcosystemSpatialCoordinates.ToPlanar(waypoints[index - 1].position, plane),
                    EcosystemSpatialCoordinates.ToPlanar(waypoints[index].position, plane));
            }

            if (totalLength <= 0.0001f)
            {
                return Vector2.Lerp(fallbackStart, fallbackEnd, normalizedProgress);
            }

            var distance = (reverse ? 1f - normalizedProgress : normalizedProgress) * totalLength;
            for (var index = 1; index < waypoints.Length; index++)
            {
                var previous = waypoints[index - 1];
                var current = waypoints[index];
                if (previous == null || current == null)
                {
                    continue;
                }

                var previousPosition = EcosystemSpatialCoordinates.ToPlanar(previous.position, plane);
                var currentPosition = EcosystemSpatialCoordinates.ToPlanar(current.position, plane);
                var segmentLength = Vector2.Distance(previousPosition, currentPosition);
                if (distance <= segmentLength || index == waypoints.Length - 1)
                {
                    return segmentLength <= 0.0001f
                        ? currentPosition
                        : Vector2.Lerp(previousPosition, currentPosition, distance / segmentLength);
                }
                distance -= segmentLength;
            }

            return reverse
                ? EcosystemSpatialCoordinates.ToPlanar(waypoints[0].position, plane)
                : EcosystemSpatialCoordinates.ToPlanar(waypoints[^1].position, plane);
        }

#if UNITY_EDITOR
        public void ConfigureEditor(
            string authoredFromLocationId,
            string authoredToLocationId,
            Transform[] authoredWaypoints,
            LineRenderer authoredRouteRenderer)
        {
            fromLocationId = authoredFromLocationId ?? string.Empty;
            toLocationId = authoredToLocationId ?? string.Empty;
            waypoints = authoredWaypoints ?? Array.Empty<Transform>();
            routeRenderer = authoredRouteRenderer;
        }
#endif
    }

    /// <summary>
    /// Serialized, edit-safe ownership boundary for the overworld layout. Static terrain,
    /// landmarks, colliders, and path transforms remain visible in Edit Mode; runtime actors
    /// are projected beneath DynamicActorRoot from persistent save IDs.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EcosystemSpatialAuthoring : MonoBehaviour
    {
        [SerializeField] private EcosystemSpatialPlane spatialPlane = EcosystemSpatialPlane.XY;
        [SerializeField] private Rect planarBounds = new(-32f, -22f, 64f, 44f);
        [SerializeField] private Transform dynamicActorRoot;
        [SerializeField] private EcosystemLocationAnchorAuthoring[] locations =
            Array.Empty<EcosystemLocationAnchorAuthoring>();
        [SerializeField] private EcosystemRoutePathAuthoring[] routes =
            Array.Empty<EcosystemRoutePathAuthoring>();

        private readonly Dictionary<string, EcosystemLocationAnchorAuthoring> locationLookup =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, EcosystemRoutePathAuthoring> routeLookup =
            new(StringComparer.Ordinal);
        private readonly Dictionary<int, string> collider2DLocationLookup = new();
        private readonly Dictionary<int, string> collider3DLocationLookup = new();
        private bool lookupDirty = true;

        public EcosystemSpatialPlane SpatialPlane => spatialPlane;
        public Rect PlanarBounds => planarBounds;
        public Transform DynamicActorRoot => dynamicActorRoot != null ? dynamicActorRoot : transform;
        public IReadOnlyList<EcosystemLocationAnchorAuthoring> Locations => locations;
        public IReadOnlyList<EcosystemRoutePathAuthoring> Routes => routes;

        private void Awake()
        {
            RebuildLookup();
        }

        private void OnValidate()
        {
            locations ??= Array.Empty<EcosystemLocationAnchorAuthoring>();
            routes ??= Array.Empty<EcosystemRoutePathAuthoring>();
            if (planarBounds.width < 1f) planarBounds.width = 1f;
            if (planarBounds.height < 1f) planarBounds.height = 1f;
            lookupDirty = true;
        }

        public bool TryGetLocation(
            string locationId,
            out EcosystemLocationAnchorAuthoring location)
        {
            EnsureLookup();
            location = null;
            return !string.IsNullOrEmpty(locationId) && locationLookup.TryGetValue(locationId, out location);
        }

        public bool TryGetRoute(
            string fromLocationId,
            string toLocationId,
            out EcosystemRoutePathAuthoring route)
        {
            EnsureLookup();
            return routeLookup.TryGetValue(RouteKey(fromLocationId, toLocationId), out route);
        }

        public bool TryGetLocationId(Collider2D collider, out string locationId)
        {
            EnsureLookup();
            locationId = string.Empty;
            return collider != null &&
                   collider2DLocationLookup.TryGetValue(collider.GetInstanceID(), out locationId);
        }

        public bool TryGetLocationId(Collider collider, out string locationId)
        {
            EnsureLookup();
            locationId = string.Empty;
            return collider != null &&
                   collider3DLocationLookup.TryGetValue(collider.GetInstanceID(), out locationId);
        }

        public Vector2 ResolveLocationPlanarPosition(
            LocationState fallbackLocation,
            float fallbackMapScale)
        {
            if (fallbackLocation != null &&
                TryGetLocation(fallbackLocation.id, out var authoredLocation))
            {
                return authoredLocation.PlanarPosition(spatialPlane);
            }

            return fallbackLocation == null
                ? Vector2.zero
                : fallbackLocation.mapPosition * fallbackMapScale;
        }

        public void RebuildLookup()
        {
            locationLookup.Clear();
            routeLookup.Clear();
            collider2DLocationLookup.Clear();
            collider3DLocationLookup.Clear();

            if (locations != null)
            {
                foreach (var location in locations)
                {
                    if (location == null || string.IsNullOrWhiteSpace(location.LocationId) ||
                        locationLookup.ContainsKey(location.LocationId))
                    {
                        continue;
                    }
                    locationLookup.Add(location.LocationId, location);
                    if (location.SelectionCollider2D != null)
                    {
                        collider2DLocationLookup[location.SelectionCollider2D.GetInstanceID()] =
                            location.LocationId;
                    }
                    if (location.SelectionCollider3D != null)
                    {
                        collider3DLocationLookup[location.SelectionCollider3D.GetInstanceID()] =
                            location.LocationId;
                    }
                }
            }

            if (routes != null)
            {
                foreach (var route in routes)
                {
                    if (route == null || string.IsNullOrWhiteSpace(route.FromLocationId) ||
                        string.IsNullOrWhiteSpace(route.ToLocationId))
                    {
                        continue;
                    }
                    routeLookup.TryAdd(
                        RouteKey(route.FromLocationId, route.ToLocationId),
                        route);
                }
            }
            lookupDirty = false;
        }

        private void EnsureLookup()
        {
            if (lookupDirty)
            {
                RebuildLookup();
            }
        }

        private static string RouteKey(string firstLocationId, string secondLocationId)
        {
            firstLocationId ??= string.Empty;
            secondLocationId ??= string.Empty;
            return string.CompareOrdinal(firstLocationId, secondLocationId) <= 0
                ? $"{firstLocationId}\u001f{secondLocationId}"
                : $"{secondLocationId}\u001f{firstLocationId}";
        }

#if UNITY_EDITOR
        public void ConfigureEditor(
            EcosystemSpatialPlane authoredPlane,
            Rect authoredBounds,
            Transform authoredDynamicActorRoot,
            EcosystemLocationAnchorAuthoring[] authoredLocations,
            EcosystemRoutePathAuthoring[] authoredRoutes)
        {
            spatialPlane = authoredPlane;
            planarBounds = authoredBounds;
            dynamicActorRoot = authoredDynamicActorRoot;
            locations = authoredLocations ?? Array.Empty<EcosystemLocationAnchorAuthoring>();
            routes = authoredRoutes ?? Array.Empty<EcosystemRoutePathAuthoring>();
            lookupDirty = true;
            RebuildLookup();
        }
#endif

        private void OnDrawGizmosSelected()
        {
            var previousColor = Gizmos.color;
            Gizmos.color = new Color(0.08f, 0.72f, 1f, 0.3f);
            var center = EcosystemSpatialCoordinates.ToWorld(planarBounds.center, spatialPlane);
            var size = spatialPlane == EcosystemSpatialPlane.XY
                ? new Vector3(planarBounds.width, planarBounds.height, 0.05f)
                : new Vector3(planarBounds.width, 0.05f, planarBounds.height);
            Gizmos.DrawWireCube(center, size);
            Gizmos.color = previousColor;
        }
    }
}
