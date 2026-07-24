using UnityEngine;
using UnityEngine.InputSystem;

namespace Turtle.Combat
{
    [DisallowMultipleComponent]
    public sealed class CombatCameraRig : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Combatant observedCombatant;
        [SerializeField, Min(1f)] private float distance = 8.5f;
        [SerializeField] private Vector2 pitchLimits = new(10f, 58f);
        [SerializeField, Min(0f)] private float sensitivity = 0.12f;
        private float yaw;
        private float pitch = 24f;
        private float trauma;
        private float redFlash;

        private void OnEnable()
        {
            if (observedCombatant != null)
            {
                observedCombatant.Damaged += OnObservedDamaged;
            }
        }

        private void OnDisable()
        {
            if (observedCombatant != null)
            {
                observedCombatant.Damaged -= OnObservedDamaged;
            }
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            if (Mouse.current != null)
            {
                var delta = Mouse.current.delta.ReadValue();
                yaw += delta.x * sensitivity;
                pitch = Mathf.Clamp(pitch - delta.y * sensitivity, pitchLimits.x, pitchLimits.y);
            }

            var rotation = Quaternion.Euler(pitch, yaw, 0f);
            var focus = target.position + Vector3.up * 1.25f;
            var shake = trauma > 0f
                ? Random.insideUnitSphere * (0.32f * trauma * trauma)
                : Vector3.zero;
            var desired = focus - rotation * Vector3.forward * distance + shake;
            transform.position = Vector3.Lerp(transform.position, desired, 14f * Time.deltaTime);
            transform.rotation = Quaternion.LookRotation(focus - transform.position);
            trauma = Mathf.MoveTowards(trauma, 0f, 2.6f * Time.unscaledDeltaTime);
            redFlash = Mathf.MoveTowards(redFlash, 0f, 2f * Time.unscaledDeltaTime);
        }

        private void OnGUI()
        {
            if (redFlash <= 0f)
            {
                return;
            }
            var previous = GUI.color;
            GUI.color = new Color(0.8f, 0f, 0f, redFlash * 0.38f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private void OnObservedDamaged(Combatant victim, Combatant source, float amount)
        {
            trauma = Mathf.Clamp01(trauma + 0.7f);
            redFlash = Mathf.Clamp01(redFlash + 0.9f);
        }

#if UNITY_EDITOR
        public void ConfigureEditor(Transform followTarget, Combatant combatant)
        {
            target = followTarget;
            observedCombatant = combatant;
        }
#endif
    }
}
