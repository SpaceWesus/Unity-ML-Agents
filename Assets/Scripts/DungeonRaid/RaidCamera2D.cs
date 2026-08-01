using System;
using System.Collections.Generic;
using UnityEngine;

namespace Turtle.DungeonRaid
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class RaidCamera2D : MonoBehaviour
    {
        [SerializeField] private DungeonRaidDirector2D raid;
        [SerializeField] private Camera controlledCamera;
        [SerializeField, Min(0.1f)] private float followSharpness = 3.5f;
        [SerializeField, Min(5f)] private float minimumSize = 8f;
        [SerializeField, Min(6f)] private float maximumSize = 14f;
        [SerializeField, Min(0f)] private float framingPadding = 4f;

        private void Awake()
        {
            if (controlledCamera == null) controlledCamera = GetComponent<Camera>();
            controlledCamera.orthographic = true;
        }

        private void LateUpdate()
        {
            if (raid == null || controlledCamera == null || !raid.TryGetFocusBounds(out var bounds)) return;
            var target = bounds.center;
            var current = transform.position;
            var blend = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
            transform.position = Vector3.Lerp(
                current,
                new Vector3(target.x, target.y, current.z),
                blend);
            var aspect = Mathf.Max(0.5f, controlledCamera.aspect);
            var required = Mathf.Max(bounds.extents.y, bounds.extents.x / aspect) + framingPadding;
            controlledCamera.orthographicSize = Mathf.Lerp(
                controlledCamera.orthographicSize,
                Mathf.Clamp(required, minimumSize, maximumSize),
                blend);
        }

#if UNITY_EDITOR
        public void ConfigureEditor(DungeonRaidDirector2D director, Camera cameraReference)
        {
            raid = director;
            controlledCamera = cameraReference;
            if (controlledCamera != null)
            {
                controlledCamera.orthographic = true;
                controlledCamera.orthographicSize = 11f;
            }
        }
#endif
    }
}
