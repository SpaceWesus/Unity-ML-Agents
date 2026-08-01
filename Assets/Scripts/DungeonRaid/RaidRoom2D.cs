using System;
using System.Collections.Generic;
using UnityEngine;

namespace Turtle.DungeonRaid
{
    [DisallowMultipleComponent]
    public sealed class RaidRoom2D : MonoBehaviour
    {
        [SerializeField] private string roomId = "room";
        [SerializeField, Min(0)] private int sequence;
        [SerializeField] private RaidRoomPurpose purpose = RaidRoomPurpose.Encounter;
        [SerializeField] private Vector2 size = new(20f, 20f);

        public string RoomId => roomId;
        public int Sequence => sequence;
        public RaidRoomPurpose Purpose => purpose;
        public Vector2 Size => size;
        public Vector2 Center => transform.position;
        public Rect Bounds => new(
            Center - size * 0.5f,
            new Vector2(Mathf.Max(0.1f, size.x), Mathf.Max(0.1f, size.y)));

        public bool Contains(Vector2 point) => Bounds.Contains(point);

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.15f, 0.85f, 1f, 0.65f);
            Gizmos.DrawWireCube(transform.position, new Vector3(size.x, size.y, 0f));
        }

#if UNITY_EDITOR
        public void ConfigureEditor(
            string id,
            int order,
            Vector2 roomSize,
            RaidRoomPurpose roomPurpose = RaidRoomPurpose.Encounter)
        {
            roomId = id;
            sequence = Mathf.Max(0, order);
            purpose = roomPurpose;
            size = new Vector2(Mathf.Max(0.1f, roomSize.x), Mathf.Max(0.1f, roomSize.y));
        }
#endif
    }
}
