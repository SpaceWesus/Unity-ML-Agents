using UnityEngine;

namespace Turtle.DungeonRaid
{
    /// <summary>
    /// Authored socket consumed by a future dungeon materializer. The marker is
    /// configuration only: encounter state remains authoritative elsewhere.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RaidSpawnMarker2D : MonoBehaviour
    {
        [SerializeField] private RaidSpawnMarkerKind kind;
        [SerializeField] private string groupId = "default";
        [SerializeField, Min(1)] private int capacity = 1;
        [SerializeField, Min(0.1f)] private float radius = 1f;

        public RaidSpawnMarkerKind Kind => kind;
        public string GroupId => groupId;
        public int Capacity => capacity;
        public float Radius => radius;
        public Vector2 Position => transform.position;

        private void OnDrawGizmos()
        {
            Gizmos.color = ResolveColor(kind);
            Gizmos.DrawWireSphere(transform.position, radius);
        }

        private static Color ResolveColor(RaidSpawnMarkerKind markerKind) => markerKind switch
        {
            RaidSpawnMarkerKind.Party => new Color(0.2f, 0.9f, 1f, 0.8f),
            RaidSpawnMarkerKind.EnemyPod => new Color(1f, 0.42f, 0.12f, 0.8f),
            RaidSpawnMarkerKind.Elite => new Color(1f, 0.15f, 0.5f, 0.8f),
            RaidSpawnMarkerKind.Boss => new Color(1f, 0.05f, 0.08f, 0.9f),
            RaidSpawnMarkerKind.Chest => new Color(1f, 0.78f, 0.16f, 0.8f),
            _ => new Color(0.5f, 1f, 0.45f, 0.8f)
        };

#if UNITY_EDITOR
        public void ConfigureEditor(
            RaidSpawnMarkerKind markerKind,
            string markerGroupId,
            int markerCapacity,
            float markerRadius)
        {
            kind = markerKind;
            groupId = string.IsNullOrWhiteSpace(markerGroupId) ? "default" : markerGroupId;
            capacity = Mathf.Max(1, markerCapacity);
            radius = Mathf.Max(0.1f, markerRadius);
        }
#endif
    }
}
