using UnityEngine;
using UnityEngine.InputSystem;

namespace Turtle.Ecosystem
{
    /// <summary>
    /// Orthographic top-down camera with bounded developer pan/zoom and optional controlled
    /// pawn follow. Uses the Input System directly and never changes simulation state.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class EcosystemMapCameraController : MonoBehaviour
    {
        [SerializeField] private Camera controlledCamera;
        [SerializeField] private EcosystemSpatialAuthoring spatialAuthoring;
        [SerializeField] private EcosystemSpatialHud hud;
        [SerializeField, Min(0.1f)] private float keyboardPanSpeed = 12f;
        [SerializeField, Min(0.01f)] private float dragSensitivity = 1f;
        [SerializeField, Min(0.1f)] private float zoomSpeed = 1.2f;
        [SerializeField, Min(0.5f)] private float minimumOrthographicSize = 3.5f;
        [SerializeField, Min(1f)] private float maximumOrthographicSize = 24f;
        [SerializeField, Min(0f)] private float followSharpness = 10f;
        [SerializeField] private bool allowKeyboardPan = true;
        [SerializeField] private bool allowMiddleMousePan = true;
        [SerializeField] private bool allowWheelZoom = true;
        [SerializeField] private bool followControlledPawn = true;
        [SerializeField] private bool requireAltForKeyboardPanWhileFollowing = true;

        private Transform followTarget;
        private bool developerPanOverride;
        private bool middleDragActive;

        public Camera ControlledCamera => controlledCamera;
        public bool DeveloperPanOverride => developerPanOverride;
        public bool FollowControlledPawn
        {
            get => followControlledPawn;
            set => followControlledPawn = value;
        }

        private void Awake()
        {
            if (controlledCamera == null)
            {
                controlledCamera = GetComponent<Camera>();
            }
        }

        private void LateUpdate()
        {
            if (controlledCamera == null)
            {
                return;
            }

            var deltaTime = Time.unscaledDeltaTime;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.fKey.wasPressedThisFrame && followTarget != null)
            {
                ResumeFollow(true);
            }

            var planarDelta = ReadDeveloperPan(deltaTime);
            if (planarDelta.sqrMagnitude > 0.000001f)
            {
                developerPanOverride = true;
                MovePlanar(planarDelta);
            }
            else if (followControlledPawn && !developerPanOverride && followTarget != null)
            {
                FollowTarget(deltaTime);
            }

            ReadZoom();
            ClampToBounds();
        }

        public void Initialize(
            Camera cameraReference,
            EcosystemSpatialAuthoring authoredMap,
            EcosystemSpatialHud spatialHud)
        {
            controlledCamera = cameraReference != null ? cameraReference : GetComponent<Camera>();
            spatialAuthoring = authoredMap;
            hud = spatialHud;
            ClampToBounds();
        }

        public void SetFollowTarget(Transform target, bool snap = false)
        {
            followTarget = target;
            if (snap && target != null)
            {
                var plane = ResolvePlane();
                var targetPlanar = EcosystemSpatialCoordinates.ToPlanar(target.position, plane);
                controlledCamera.transform.position = EcosystemSpatialCoordinates.WithPlanar(
                    controlledCamera.transform.position,
                    targetPlanar,
                    plane);
                ClampToBounds();
            }
        }

        public void ResumeFollow(bool snap = false)
        {
            developerPanOverride = false;
            if (snap && followTarget != null)
            {
                SetFollowTarget(followTarget, true);
            }
        }

        public void CenterOn(Transform target, bool developerOverride = true)
        {
            if (target == null)
            {
                return;
            }
            CenterOnPlanar(
                EcosystemSpatialCoordinates.ToPlanar(target.position, ResolvePlane()),
                developerOverride);
        }

        public void CenterOnPlanar(Vector2 planarPosition, bool developerOverride = true)
        {
            if (controlledCamera == null)
            {
                return;
            }
            developerPanOverride = developerOverride;
            controlledCamera.transform.position = EcosystemSpatialCoordinates.WithPlanar(
                controlledCamera.transform.position,
                planarPosition,
                ResolvePlane());
            ClampToBounds();
        }

        public void EnableDeveloperPan(bool enabled)
        {
            developerPanOverride = enabled;
        }

        private Vector2 ReadDeveloperPan(float deltaTime)
        {
            var result = Vector2.zero;
            var keyboard = Keyboard.current;
            var modifierHeld = keyboard != null &&
                               (keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed);
            var keyboardPanAllowed = allowKeyboardPan && keyboard != null &&
                                     (!requireAltForKeyboardPanWhileFollowing ||
                                      !followControlledPawn ||
                                      followTarget == null ||
                                      modifierHeld);
            if (keyboardPanAllowed)
            {
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) result.x -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) result.x += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) result.y -= 1f;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) result.y += 1f;
                result = Vector2.ClampMagnitude(result, 1f) * keyboardPanSpeed * deltaTime;
            }

            var mouse = Mouse.current;
            if (!allowMiddleMousePan || mouse == null)
            {
                return result;
            }

            if (mouse.middleButton.wasPressedThisFrame)
            {
                middleDragActive = hud == null || !hud.IsPointerOverHud(mouse.position.ReadValue());
            }
            if (mouse.middleButton.wasReleasedThisFrame)
            {
                middleDragActive = false;
            }
            if (!middleDragActive || !mouse.middleButton.isPressed)
            {
                return result;
            }

            var screenDelta = mouse.delta.ReadValue();
            if (screenDelta.sqrMagnitude <= 0.001f)
            {
                return result;
            }

            var unitsPerPixel = controlledCamera.orthographic
                ? controlledCamera.orthographicSize * 2f / Mathf.Max(1f, Screen.height)
                : 0.02f;
            result += new Vector2(-screenDelta.x, -screenDelta.y) *
                      unitsPerPixel * dragSensitivity;
            return result;
        }

        private void ReadZoom()
        {
            if (!allowWheelZoom || !controlledCamera.orthographic)
            {
                return;
            }

            var mouse = Mouse.current;
            if (mouse == null || (hud != null && hud.IsPointerOverHud(mouse.position.ReadValue())))
            {
                return;
            }

            var scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) <= 0.01f)
            {
                return;
            }

            // Input System mouse scroll is conventionally reported in increments near 120.
            var normalizedScroll = Mathf.Clamp(scroll / 120f, -4f, 4f);
            controlledCamera.orthographicSize = Mathf.Clamp(
                controlledCamera.orthographicSize - normalizedScroll * zoomSpeed,
                minimumOrthographicSize,
                maximumOrthographicSize);
        }

        private void FollowTarget(float deltaTime)
        {
            var plane = ResolvePlane();
            var currentPlanar = EcosystemSpatialCoordinates.ToPlanar(
                controlledCamera.transform.position,
                plane);
            var targetPlanar = EcosystemSpatialCoordinates.ToPlanar(followTarget.position, plane);
            var interpolation = followSharpness <= 0f
                ? 1f
                : 1f - Mathf.Exp(-followSharpness * Mathf.Max(0f, deltaTime));
            var next = Vector2.Lerp(currentPlanar, targetPlanar, interpolation);
            controlledCamera.transform.position = EcosystemSpatialCoordinates.WithPlanar(
                controlledCamera.transform.position,
                next,
                plane);
        }

        private void MovePlanar(Vector2 planarDelta)
        {
            var plane = ResolvePlane();
            var current = EcosystemSpatialCoordinates.ToPlanar(
                controlledCamera.transform.position,
                plane);
            controlledCamera.transform.position = EcosystemSpatialCoordinates.WithPlanar(
                controlledCamera.transform.position,
                current + planarDelta,
                plane);
        }

        private void ClampToBounds()
        {
            if (controlledCamera == null || spatialAuthoring == null)
            {
                return;
            }

            var bounds = spatialAuthoring.PlanarBounds;
            var halfHeight = controlledCamera.orthographic
                ? controlledCamera.orthographicSize
                : 0f;
            var halfWidth = halfHeight * controlledCamera.aspect;
            var minimumX = bounds.xMin + halfWidth;
            var maximumX = bounds.xMax - halfWidth;
            var minimumY = bounds.yMin + halfHeight;
            var maximumY = bounds.yMax - halfHeight;

            var plane = ResolvePlane();
            var planar = EcosystemSpatialCoordinates.ToPlanar(
                controlledCamera.transform.position,
                plane);
            planar.x = minimumX > maximumX
                ? bounds.center.x
                : Mathf.Clamp(planar.x, minimumX, maximumX);
            planar.y = minimumY > maximumY
                ? bounds.center.y
                : Mathf.Clamp(planar.y, minimumY, maximumY);
            controlledCamera.transform.position = EcosystemSpatialCoordinates.WithPlanar(
                controlledCamera.transform.position,
                planar,
                plane);
        }

        private EcosystemSpatialPlane ResolvePlane()
        {
            return spatialAuthoring != null
                ? spatialAuthoring.SpatialPlane
                : EcosystemSpatialPlane.XY;
        }

#if UNITY_EDITOR
        public void ConfigureEditor(
            Camera cameraReference,
            EcosystemSpatialAuthoring authoredMap,
            EcosystemSpatialHud spatialHud,
            bool authoredFollowControlledPawn = true)
        {
            controlledCamera = cameraReference;
            spatialAuthoring = authoredMap;
            hud = spatialHud;
            followControlledPawn = authoredFollowControlledPawn;
        }
#endif
    }
}
