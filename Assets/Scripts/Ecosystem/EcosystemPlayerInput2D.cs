using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Turtle.Ecosystem
{
    public readonly struct EcosystemPlayerIntent2D
    {
        public EcosystemPlayerIntent2D(
            Vector2 movement,
            Vector2 aimPlanarPosition,
            bool lightAttackPressed,
            bool alternateActionPressed,
            bool interactPressed,
            bool dodgePressed,
            int sampledFrame)
        {
            Movement = Vector2.ClampMagnitude(movement, 1f);
            AimPlanarPosition = aimPlanarPosition;
            LightAttackPressed = lightAttackPressed;
            AlternateActionPressed = alternateActionPressed;
            InteractPressed = interactPressed;
            DodgePressed = dodgePressed;
            SampledFrame = sampledFrame;
        }

        public Vector2 Movement { get; }
        public Vector2 AimPlanarPosition { get; }
        public bool LightAttackPressed { get; }
        public bool AlternateActionPressed { get; }
        public bool InteractPressed { get; }
        public bool DodgePressed { get; }
        public int SampledFrame { get; }
    }

    /// <summary>
    /// Input-System adapter for the direct top-down player experience. It exposes intent only;
    /// a controller or canonical spatial/combat simulation must validate and execute it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EcosystemPlayerInput2D : MonoBehaviour
    {
        [SerializeField] private Camera worldCamera;
        [SerializeField] private EcosystemSpatialAuthoring spatialAuthoring;
        [SerializeField] private EcosystemSpatialHud hud;
        [SerializeField] private bool gameplayInputEnabled = true;

        public EcosystemPlayerIntent2D CurrentIntent { get; private set; }
        public bool GameplayInputEnabled
        {
            get => gameplayInputEnabled;
            set => gameplayInputEnabled = value;
        }

        public event Action<EcosystemPlayerIntent2D> IntentSampled;

        private void Awake()
        {
            if (worldCamera == null)
            {
                worldCamera = GetComponent<Camera>();
            }
        }

        private void Update()
        {
            CurrentIntent = gameplayInputEnabled
                ? SampleIntent()
                : new EcosystemPlayerIntent2D(
                    Vector2.zero,
                    Vector2.zero,
                    false,
                    false,
                    false,
                    false,
                    Time.frameCount);
            IntentSampled?.Invoke(CurrentIntent);
        }

        private void OnDisable()
        {
            CurrentIntent = new EcosystemPlayerIntent2D(
                Vector2.zero,
                CurrentIntent.AimPlanarPosition,
                false,
                false,
                false,
                false,
                Time.frameCount);
        }

        public EcosystemPlayerIntent2D SampleIntent()
        {
            var movement = Vector2.zero;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) movement.x -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) movement.x += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) movement.y -= 1f;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) movement.y += 1f;
            }

            var gamepad = Gamepad.current;
            if (gamepad != null && gamepad.leftStick.ReadValue().sqrMagnitude > movement.sqrMagnitude)
            {
                movement = gamepad.leftStick.ReadValue();
            }

            var mouse = Mouse.current;
            var pointerOverHud = false;
            var aimPlanar = CurrentIntent.AimPlanarPosition;
            if (mouse != null)
            {
                var screenPosition = mouse.position.ReadValue();
                pointerOverHud = hud != null && hud.IsPointerOverHud(screenPosition);
                if (TryScreenToPlanar(screenPosition, out var pointerPlanar))
                {
                    aimPlanar = pointerPlanar;
                }
            }

            var lightAttack = !pointerOverHud &&
                              ((mouse != null && mouse.leftButton.wasPressedThisFrame) ||
                               (gamepad != null && gamepad.buttonWest.wasPressedThisFrame));
            var alternate = !pointerOverHud &&
                            ((mouse != null && mouse.rightButton.wasPressedThisFrame) ||
                             (gamepad != null && gamepad.rightShoulder.wasPressedThisFrame));
            var interact = !pointerOverHud &&
                           ((keyboard != null && keyboard.eKey.wasPressedThisFrame) ||
                            (gamepad != null && gamepad.buttonNorth.wasPressedThisFrame));
            var dodge = (keyboard != null && keyboard.spaceKey.wasPressedThisFrame) ||
                        (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame);

            return new EcosystemPlayerIntent2D(
                movement,
                aimPlanar,
                lightAttack,
                alternate,
                interact,
                dodge,
                Time.frameCount);
        }

        public bool TryScreenToPlanar(Vector2 screenPosition, out Vector2 planarPosition)
        {
            planarPosition = Vector2.zero;
            if (worldCamera == null)
            {
                return false;
            }

            var plane = spatialAuthoring != null
                ? spatialAuthoring.SpatialPlane
                : EcosystemSpatialPlane.XY;
            if (plane == EcosystemSpatialPlane.XY)
            {
                var depth = Mathf.Abs(worldCamera.transform.position.z);
                var world = worldCamera.ScreenToWorldPoint(
                    new Vector3(screenPosition.x, screenPosition.y, depth));
                planarPosition = new Vector2(world.x, world.y);
                return true;
            }

            var ray = worldCamera.ScreenPointToRay(screenPosition);
            var groundPlane = new Plane(Vector3.up, Vector3.zero);
            if (!groundPlane.Raycast(ray, out var distance))
            {
                return false;
            }
            planarPosition = EcosystemSpatialCoordinates.ToPlanar(
                ray.GetPoint(distance),
                EcosystemSpatialPlane.XZ);
            return true;
        }

        public void Initialize(
            Camera cameraReference,
            EcosystemSpatialAuthoring authoredMap,
            EcosystemSpatialHud spatialHud)
        {
            worldCamera = cameraReference;
            spatialAuthoring = authoredMap;
            hud = spatialHud;
        }

#if UNITY_EDITOR
        public void ConfigureEditor(
            Camera cameraReference,
            EcosystemSpatialAuthoring authoredMap,
            EcosystemSpatialHud spatialHud)
        {
            Initialize(cameraReference, authoredMap, spatialHud);
        }
#endif
    }
}
