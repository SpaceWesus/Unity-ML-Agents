using UnityEngine;
using UnityEngine.InputSystem;

namespace Turtle.Combat
{
    [DisallowMultipleComponent]
    public sealed class PlayerCombatCommandSource : MonoBehaviour, ICombatCommandSource
    {
        [SerializeField] private Camera gameplayCamera;

        public CombatCommand SampleCommand(Combatant self)
        {
            var movement = Vector2.zero;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed) movement.y += 1f;
                if (keyboard.sKey.isPressed) movement.y -= 1f;
                if (keyboard.dKey.isPressed) movement.x += 1f;
                if (keyboard.aKey.isPressed) movement.x -= 1f;
            }

            var action = CombatAction.None;
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                action = CombatAction.LightAttack;
            }
            else if (mouse != null && mouse.rightButton.wasPressedThisFrame)
            {
                action = CombatAction.HeavyAttack;
            }
            else if (keyboard != null &&
                     (keyboard.spaceKey.wasPressedThisFrame ||
                      keyboard.leftShiftKey.wasPressedThisFrame))
            {
                action = CombatAction.Dodge;
            }

            var facing = gameplayCamera != null
                ? Vector3.ProjectOnPlane(gameplayCamera.transform.forward, Vector3.up).normalized
                : self.transform.forward;
            return new CombatCommand(movement, facing, action);
        }

        private void OnEnable()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void OnDisable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnApplicationFocus(bool focused)
        {
            if (focused && isActiveAndEnabled)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

#if UNITY_EDITOR
        public void ConfigureEditor(Camera camera)
        {
            gameplayCamera = camera;
        }
#endif
    }
}
