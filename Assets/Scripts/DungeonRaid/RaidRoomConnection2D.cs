using System;
using System.Collections.Generic;
using UnityEngine;

namespace Turtle.DungeonRaid
{
    [DisallowMultipleComponent]
    public sealed class RaidRoomConnection2D : MonoBehaviour
    {
        [SerializeField] private RaidRoom2D fromRoom;
        [SerializeField] private RaidRoom2D toRoom;
        [SerializeField, Min(0.1f)] private float width = 4f;
        [SerializeField, Min(0f)] private float length;

        public RaidRoom2D FromRoom => fromRoom;
        public RaidRoom2D ToRoom => toRoom;
        public float Width => width;
        public float Length => length;
        public Vector2 Position => transform.position;

        public bool Connects(RaidRoom2D first, RaidRoom2D second)
        {
            return first != null && second != null &&
                   ((fromRoom == first && toRoom == second) ||
                    (fromRoom == second && toRoom == first));
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.25f, 1f, 0.8f, 0.65f);
            var previousMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero,
                new Vector3(Mathf.Max(width, length), width, 0f));
            Gizmos.matrix = previousMatrix;
        }

#if UNITY_EDITOR
        public void ConfigureEditor(
            RaidRoom2D from,
            RaidRoom2D to,
            float connectionWidth,
            float corridorLength = 0f)
        {
            fromRoom = from;
            toRoom = to;
            width = Mathf.Max(0.1f, connectionWidth);
            length = Mathf.Max(0f, corridorLength);
        }
#endif
    }
}
