using UnityEngine;
using UnityEngine.InputSystem;

namespace Turtle.BattleArena3D
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class BattleArena3DCameraRig : MonoBehaviour
    {
        [SerializeField] private Camera controlledCamera;
        [SerializeField] private BattleArena3DDirector director;
        [SerializeField] private BattleArena3DPresentationController presentationController;
        [SerializeField] private Vector2 arenaExtents = new(46f, 30f);
        [SerializeField] private Vector3 focusPoint = Vector3.zero;
        [SerializeField, Range(18f, 90f)] private float distance = 58f;
        [SerializeField, Range(25f, 75f)] private float pitch = 52f;
        [SerializeField] private float yaw;
        [SerializeField] private float panSpeed = 24f;
        [SerializeField] private float rotationSpeed = 72f;
        [SerializeField] private float zoomSpeed = 26f;
        [SerializeField] private float smoothing = 10f;

        private Vector2 previousMousePosition;
        private bool dragging;
        private bool followSelected;
        private float cameraMotionScale = 0.4f;
        private float impulse;
        private Vector3 impulseDirection = Vector3.forward;

        public bool IsFollowingSelected => followSelected;

        private void Awake()
        {
            controlledCamera ??= GetComponent<Camera>();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            var deltaTime = Time.unscaledDeltaTime;
            var yawRotation = Quaternion.Euler(0f, yaw, 0f);
            var planarForward = yawRotation * Vector3.forward;
            var planarRight = yawRotation * Vector3.right;

            if (keyboard != null)
            {
                var pan = Vector3.zero;
                if (keyboard.wKey.isPressed) pan += planarForward;
                if (keyboard.sKey.isPressed) pan -= planarForward;
                if (keyboard.dKey.isPressed) pan += planarRight;
                if (keyboard.aKey.isPressed) pan -= planarRight;
                if (pan.sqrMagnitude > 0.01f)
                {
                    followSelected = false;
                    focusPoint += pan.normalized * panSpeed * deltaTime * Mathf.Lerp(0.7f, 1.35f, distance / 90f);
                }
                if (keyboard.qKey.isPressed) yaw -= rotationSpeed * deltaTime;
                if (keyboard.eKey.isPressed) yaw += rotationSpeed * deltaTime;
                if (keyboard.rKey.isPressed) distance -= zoomSpeed * deltaTime;
                if (keyboard.fKey.isPressed) distance += zoomSpeed * deltaTime;
                if (keyboard.homeKey.wasPressedThisFrame)
                {
                    focusPoint = Vector3.zero;
                    distance = 58f;
                    yaw = 0f;
                    pitch = 52f;
                }
            }

            if (mouse != null)
            {
                var position = mouse.position.ReadValue();
                if (mouse.middleButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame)
                {
                    dragging = true;
                    previousMousePosition = position;
                }
                if (dragging && (mouse.middleButton.isPressed || mouse.rightButton.isPressed))
                {
                    var delta = position - previousMousePosition;
                    previousMousePosition = position;
                    if (mouse.rightButton.isPressed)
                    {
                        yaw += delta.x * 0.16f;
                        pitch = Mathf.Clamp(pitch - delta.y * 0.12f, 30f, 72f);
                    }
                    else
                    {
                        followSelected = false;
                        focusPoint += (-planarRight * delta.x - planarForward * delta.y) *
                                      (distance * 0.0018f);
                    }
                }
                if (mouse.middleButton.wasReleasedThisFrame || mouse.rightButton.wasReleasedThisFrame)
                {
                    dragging = false;
                }
                if (mouse.leftButton.wasPressedThisFrame && controlledCamera != null)
                {
                    var pointerBlocked = director != null && director.IsPointerOverHud(position) ||
                                         presentationController != null &&
                                         presentationController.IsPointerOverUi(position);
                    if (!pointerBlocked)
                    {
                        var ray = controlledCamera.ScreenPointToRay(position);
                        if (Physics.Raycast(ray, out var hit, 300f, Physics.AllLayers,
                                QueryTriggerInteraction.Collide))
                        {
                            director?.SelectUnit(hit.collider.GetComponentInParent<BattleArena3DUnit>());
                        }
                    }
                }
            }

            distance = Mathf.Clamp(distance, 20f, 92f);
            if (followSelected && director != null && director.SelectedUnit != null)
            {
                var selectedPosition = director.SelectedUnit.transform.position;
                focusPoint = Vector3.Lerp(focusPoint, selectedPosition, 1f - Mathf.Exp(-8f * deltaTime));
            }
            focusPoint.x = Mathf.Clamp(focusPoint.x, -arenaExtents.x, arenaExtents.x);
            focusPoint.z = Mathf.Clamp(focusPoint.z, -arenaExtents.y, arenaExtents.y);
        }

        private void LateUpdate()
        {
            var rotation = Quaternion.Euler(pitch, yaw, 0f);
            var desiredPosition = focusPoint - rotation * Vector3.forward * distance;
            impulse = Mathf.MoveTowards(impulse, 0f, Time.unscaledDeltaTime * 2.8f);
            if (impulse > 0.001f && cameraMotionScale > 0f)
            {
                var time = Time.unscaledTime * 24f;
                var noiseX = Mathf.PerlinNoise(17.1f, time) * 2f - 1f;
                var noiseY = Mathf.PerlinNoise(43.7f, time * 1.07f) * 2f - 1f;
                var amplitude = impulse * cameraMotionScale;
                desiredPosition += transform.right * noiseX * amplitude * 0.22f +
                                   transform.up * noiseY * amplitude * 0.14f +
                                   impulseDirection * amplitude * 0.05f;
                rotation *= Quaternion.Euler(noiseY * amplitude * 0.32f, noiseX * amplitude * 0.38f, 0f);
            }
            var factor = 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime);
            transform.position = Vector3.Lerp(transform.position, desiredPosition, factor);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation, factor);
        }

        public void AddImpulse(Vector3 worldPosition, float strength)
        {
            if (cameraMotionScale <= 0f || strength <= 0f) return;
            var flat = worldPosition - focusPoint;
            flat.y = 0f;
            var attenuation = 1f - Mathf.Clamp01(flat.magnitude / 48f);
            if (attenuation <= 0f) return;
            impulse = Mathf.Min(2.2f, impulse + strength * attenuation);
            impulseDirection = flat.sqrMagnitude > 0.01f ? -flat.normalized : -transform.forward;
        }

        public void ApplyPresentationOptions(BattleArenaPresentationOptions3D options)
        {
            cameraMotionScale = options.ReducedMotion
                ? 0f
                : options.CameraMotion switch
                {
                    BattleArenaCameraMotion3D.Off => 0f,
                    BattleArenaCameraMotion3D.Reduced => 0.4f,
                    _ => 1f
                };
            if (cameraMotionScale <= 0f) impulse = 0f;
        }

        public void FrameSelected(bool follow)
        {
            if (director == null || director.SelectedUnit == null) return;
            focusPoint = director.SelectedUnit.transform.position;
            distance = Mathf.Min(distance, 38f);
            followSelected = follow;
        }

        public void ToggleFollowSelected()
        {
            if (director == null || director.SelectedUnit == null)
            {
                followSelected = false;
                return;
            }
            followSelected = !followSelected;
            FrameSelected(followSelected);
        }

#if UNITY_EDITOR
        public void ConfigureEditor(
            Camera assignedCamera,
            BattleArena3DDirector assignedDirector,
            Vector2 extents,
            Vector3 focus,
            float initialDistance,
            float initialPitch,
            float initialYaw,
            BattleArena3DPresentationController assignedPresentation = null)
        {
            controlledCamera = assignedCamera;
            director = assignedDirector;
            arenaExtents = extents;
            focusPoint = focus;
            distance = initialDistance;
            pitch = initialPitch;
            yaw = initialYaw;
            presentationController = assignedPresentation;
        }
#endif
    }
}
