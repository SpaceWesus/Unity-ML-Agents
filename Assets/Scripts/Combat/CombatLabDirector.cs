using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Turtle.Combat
{
    [DisallowMultipleComponent]
    public sealed class CombatLabDirector : MonoBehaviour
    {
        [SerializeField] private Combatant player;
        [SerializeField] private Transform playerSpawn;
        [SerializeField] private Combatant[] arenaCombatants;
        [SerializeField] private Transform[] arenaSpawns;
        [SerializeField] private CombatAgentDriver[] aiDrivers;
        private bool aiEnabled = true;

        private void OnEnable()
        {
            if (player != null)
            {
                player.Defeated += OnDefeated;
            }
            foreach (var combatant in arenaCombatants)
            {
                if (combatant != null && !combatant.IsTargetDummy)
                {
                    combatant.Defeated += OnDefeated;
                }
            }
        }

        private void OnDisable()
        {
            if (player != null)
            {
                player.Defeated -= OnDefeated;
            }
            foreach (var combatant in arenaCombatants)
            {
                if (combatant != null && !combatant.IsTargetDummy)
                {
                    combatant.Defeated -= OnDefeated;
                }
            }
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }
            if (keyboard.f1Key.wasPressedThisFrame)
            {
                ResetArena();
            }
            if (keyboard.f2Key.wasPressedThisFrame)
            {
                aiEnabled = !aiEnabled;
                foreach (var driver in aiDrivers)
                {
                    if (driver != null)
                    {
                        driver.enabled = aiEnabled;
                    }
                }
            }
        }

        public void ResetArena()
        {
            for (var index = 0; index < arenaCombatants.Length; index++)
            {
                var combatant = arenaCombatants[index];
                if (combatant == null)
                {
                    continue;
                }
                var spawn = index < arenaSpawns.Length ? arenaSpawns[index] : null;
                Teleport(combatant, spawn);
                combatant.ResetCombatant();
            }
            Teleport(player, playerSpawn);
            player?.ResetCombatant();
        }

        private void OnDefeated(Combatant defeated, Combatant victor)
        {
            if (defeated == null || defeated.IsTargetDummy)
            {
                return;
            }
            StartCoroutine(RespawnAfterDelay(defeated, 3f));
        }

        private IEnumerator RespawnAfterDelay(Combatant combatant, float delay)
        {
            yield return new WaitForSeconds(delay);
            var index = System.Array.IndexOf(arenaCombatants, combatant);
            var spawn = combatant == player
                ? playerSpawn
                : index >= 0 && index < arenaSpawns.Length
                    ? arenaSpawns[index]
                    : null;
            Teleport(combatant, spawn);
            combatant.ResetCombatant();
        }

        private static void Teleport(Combatant combatant, Transform spawn)
        {
            if (combatant == null || spawn == null)
            {
                return;
            }
            var controller = combatant.GetComponent<CharacterController>();
            controller.enabled = false;
            combatant.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
            controller.enabled = true;
        }

#if UNITY_EDITOR
        public void ConfigureEditor(
            Combatant playerCombatant,
            Transform assignedPlayerSpawn,
            Combatant[] combatants,
            Transform[] spawns,
            CombatAgentDriver[] drivers)
        {
            player = playerCombatant;
            playerSpawn = assignedPlayerSpawn;
            arenaCombatants = combatants;
            arenaSpawns = spawns;
            aiDrivers = drivers;
        }
#endif
    }
}
