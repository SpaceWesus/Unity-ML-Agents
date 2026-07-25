using UnityEngine;

namespace Turtle.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Combatant))]
    public sealed class CombatTargetDummy : MonoBehaviour
    {
        [SerializeField] private Combatant combatant;
        [SerializeField] private Transform visualRoot;
        [SerializeField, Min(0.1f)] private float outOfCombatRestoreDelay = 4f;
        [SerializeField, Min(0.1f)] private float defeatedRestoreDelay = 0.9f;
        [SerializeField, Min(0.01f)] private float shakeDuration = 0.2f;
        [SerializeField, Min(0f)] private float shakeDistance = 0.085f;

        private Vector3 visualRestPosition;
        private float restoreAt = float.PositiveInfinity;
        private float shakeEndsAt;
        private int shakeSeed;

        private void Awake()
        {
            combatant ??= GetComponent<Combatant>();
            if (visualRoot != null)
            {
                visualRestPosition = visualRoot.localPosition;
            }
            DisableMovementSources();
        }

        private void OnEnable()
        {
            combatant ??= GetComponent<Combatant>();
            combatant.Damaged -= HandleDamaged;
            combatant.Damaged += HandleDamaged;
            combatant.Defeated -= HandleDefeated;
            combatant.Defeated += HandleDefeated;
            DisableMovementSources();
        }

        private void OnDisable()
        {
            if (combatant != null)
            {
                combatant.Damaged -= HandleDamaged;
                combatant.Defeated -= HandleDefeated;
            }
            ResetVisual();
        }

        private void Update()
        {
            if (combatant != null &&
                combatant.CurrentHealth < combatant.MaxHealth &&
                Time.time >= restoreAt)
            {
                combatant.ResetCombatant();
                restoreAt = float.PositiveInfinity;
            }
        }

        private void LateUpdate()
        {
            if (visualRoot == null)
            {
                return;
            }

            if (Time.time >= shakeEndsAt)
            {
                visualRoot.localPosition = visualRestPosition;
                return;
            }

            var remaining = Mathf.Clamp01(
                (shakeEndsAt - Time.time) / Mathf.Max(0.01f, shakeDuration));
            var sampleTime = Time.time * 55f;
            var offset = new Vector3(
                Mathf.PerlinNoise(shakeSeed, sampleTime) - 0.5f,
                (Mathf.PerlinNoise(shakeSeed + 19, sampleTime) - 0.5f) * 0.35f,
                Mathf.PerlinNoise(shakeSeed + 41, sampleTime) - 0.5f);
            visualRoot.localPosition =
                visualRestPosition + offset * (shakeDistance * 2f * remaining);
        }

        private void HandleDamaged(
            Combatant victim,
            Combatant source,
            float damage)
        {
            restoreAt = Time.time + outOfCombatRestoreDelay;
            shakeEndsAt = Time.time + shakeDuration;
            shakeSeed = Random.Range(1, 10000);
            var direction = source != null
                ? Vector3.ProjectOnPlane(
                    victim.transform.position - source.transform.position,
                    Vector3.up).normalized
                : victim.transform.forward;
            CombatFeedbackPool.SpawnHay(
                victim.transform.position + Vector3.up * 1.25f,
                direction);
        }

        private void HandleDefeated(Combatant victim, Combatant source)
        {
            restoreAt = Time.time + defeatedRestoreDelay;
        }

        private void DisableMovementSources()
        {
            foreach (var driver in GetComponents<CombatAgentDriver>())
            {
                driver.enabled = false;
            }
            foreach (var source in GetComponents<AiCombatCommandSource>())
            {
                source.enabled = false;
            }
            foreach (var source in GetComponents<PlayerCombatCommandSource>())
            {
                source.enabled = false;
            }
        }

        private void ResetVisual()
        {
            if (visualRoot != null)
            {
                visualRoot.localPosition = visualRestPosition;
            }
        }

#if UNITY_EDITOR
        public void ConfigureEditor(
            Combatant assignedCombatant,
            Transform assignedVisualRoot)
        {
            combatant = assignedCombatant;
            visualRoot = assignedVisualRoot;
            visualRestPosition = visualRoot != null
                ? visualRoot.localPosition
                : Vector3.zero;
            DisableMovementSources();
        }
#endif
    }
}
