using UnityEngine;
using UnityEngine.InputSystem;

namespace Turtle.Homies
{
    [DisallowMultipleComponent]
    public sealed class HomiesCameraRig : MonoBehaviour
    {
        private Transform target;
        private HomiesCombatant playerCombatant;
        private float yaw = 210f;
        private float pitch = 23f;
        private const float Distance = 8.5f;
        private float trauma;
        private float redFlash;

        public void Initialize(Transform followTarget, HomiesCombatant followedCombatant)
        {
            target = followTarget;
            playerCombatant = followedCombatant;
            playerCombatant.Damaged += OnPlayerDamaged;
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            var mouse = Mouse.current;
            if (mouse != null)
            {
                var delta = mouse.delta.ReadValue();
                yaw += delta.x * 0.14f;
                pitch = Mathf.Clamp(pitch - delta.y * 0.1f, 10f, 55f);
            }

            var rotation = Quaternion.Euler(pitch, yaw, 0f);
            var focus = target.position + Vector3.up * 1.1f;
            var shakeOffset = trauma > 0f
                ? Random.insideUnitSphere * (0.3f * trauma * trauma)
                : Vector3.zero;
            var desired = focus - rotation * Vector3.forward * Distance + shakeOffset;
            transform.position = Vector3.Lerp(transform.position, desired, 12f * Time.deltaTime);
            transform.rotation = Quaternion.LookRotation(focus - transform.position);
            trauma = Mathf.MoveTowards(trauma, 0f, 2.8f * Time.unscaledDeltaTime);
            redFlash = Mathf.MoveTowards(redFlash, 0f, 1.8f * Time.unscaledDeltaTime);
        }

        private void OnGUI()
        {
            if (redFlash <= 0f)
            {
                return;
            }

            var previousColor = GUI.color;
            GUI.color = new Color(0.75f, 0f, 0f, redFlash * 0.42f);
            GUI.DrawTexture(
                new Rect(0f, 0f, Screen.width, Screen.height),
                Texture2D.whiteTexture);
            GUI.color = previousColor;
        }

        private void OnPlayerDamaged(HomiesCombatant injured, HomiesCombatant attacker)
        {
            trauma = Mathf.Clamp01(trauma + 0.72f);
            redFlash = Mathf.Clamp01(redFlash + 0.9f);
        }

        private void OnDestroy()
        {
            if (playerCombatant != null)
            {
                playerCombatant.Damaged -= OnPlayerDamaged;
            }
        }
    }
}
