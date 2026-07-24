using UnityEngine;

namespace Turtle.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Combatant))]
    public sealed class CombatAgentDriver : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour commandSource;
        [SerializeField] private Combatant combatant;
        private ICombatCommandSource source;

        private void Awake()
        {
            combatant ??= GetComponent<Combatant>();
            source = commandSource as ICombatCommandSource;
            if (source == null)
            {
                Debug.LogError($"{name} requires a command source implementing {nameof(ICombatCommandSource)}.", this);
                enabled = false;
            }
        }

        private void Update()
        {
            combatant.Simulate(source.SampleCommand(combatant), Time.deltaTime);
        }

#if UNITY_EDITOR
        public void ConfigureEditor(MonoBehaviour sourceBehaviour)
        {
            combatant = GetComponent<Combatant>();
            commandSource = sourceBehaviour;
        }
#endif
    }
}
