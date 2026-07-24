using System.Collections.Generic;

namespace Turtle.Combat
{
    public static class CombatantRegistry
    {
        private static readonly List<Combatant> ActiveCombatants = new(64);

        public static IReadOnlyList<Combatant> All => ActiveCombatants;

        public static void Register(Combatant combatant)
        {
            if (combatant != null && !ActiveCombatants.Contains(combatant))
            {
                ActiveCombatants.Add(combatant);
            }
        }

        public static void Unregister(Combatant combatant)
        {
            ActiveCombatants.Remove(combatant);
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(
            UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            ActiveCombatants.Clear();
        }
    }
}
