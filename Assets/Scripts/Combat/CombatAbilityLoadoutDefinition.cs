using UnityEngine;

namespace Turtle.Combat
{
    [CreateAssetMenu(
        menuName = "Turtle/Combat/Ability Loadout",
        fileName = "Ability Loadout")]
    public sealed class CombatAbilityLoadoutDefinition : ScriptableObject
    {
        public const int SlotCount = 4;
        public const int UltimateSlot = 3;

        [SerializeField] private string displayName = "Ability Loadout";
        [SerializeField] private CombatAbilityDefinition ability1;
        [SerializeField] private CombatAbilityDefinition ability2;
        [SerializeField] private CombatAbilityDefinition ability3;
        [SerializeField] private CombatAbilityDefinition ultimate;
        [SerializeField] private CombatPassiveDefinition[] passives;
        [Header("Resources")]
        [SerializeField, Min(1f)] private float maximumMana = 100f;
        [SerializeField, Min(0f)] private float startingMana = 100f;
        [SerializeField, Min(0f)] private float manaRegenerationPerSecond = 14f;
        [SerializeField, Min(1f)] private float ultimateChargeRequired = 100f;
        [SerializeField, Min(0f)] private float startingUltimateCharge;
        [SerializeField, Min(0f)] private float ultimateChargePerDamageDealt = 0.65f;
        [SerializeField, Min(0f)] private float ultimateChargePerDamageReceived = 0.35f;

        public string DisplayName => displayName;
        public float MaximumMana => maximumMana;
        public float StartingMana => Mathf.Clamp(startingMana, 0f, maximumMana);
        public float ManaRegenerationPerSecond => manaRegenerationPerSecond;
        public float UltimateChargeRequired => ultimateChargeRequired;
        public float StartingUltimateCharge => startingUltimateCharge;
        public float UltimateChargePerDamageDealt => ultimateChargePerDamageDealt;
        public float UltimateChargePerDamageReceived => ultimateChargePerDamageReceived;
        public CombatPassiveDefinition[] Passives => passives;

        public CombatAbilityDefinition GetAbility(int slot)
        {
            return slot switch
            {
                0 => ability1,
                1 => ability2,
                2 => ability3,
                UltimateSlot => ultimate,
                _ => null
            };
        }

#if UNITY_EDITOR
        public void ConfigureEditor(
            string name,
            CombatAbilityDefinition first,
            CombatAbilityDefinition second,
            CombatAbilityDefinition third,
            CombatAbilityDefinition ultimateAbility,
            CombatPassiveDefinition[] passiveDefinitions,
            float maxMana,
            float initialMana,
            float manaRegeneration,
            float ultimateRequired,
            float initialUltimate,
            float chargePerDamageDealt,
            float chargePerDamageReceived)
        {
            displayName = name;
            ability1 = first;
            ability2 = second;
            ability3 = third;
            ultimate = ultimateAbility;
            passives = passiveDefinitions;
            maximumMana = Mathf.Max(1f, maxMana);
            startingMana = Mathf.Clamp(initialMana, 0f, maximumMana);
            manaRegenerationPerSecond = Mathf.Max(0f, manaRegeneration);
            ultimateChargeRequired = ultimateRequired;
            startingUltimateCharge = initialUltimate;
            ultimateChargePerDamageDealt = chargePerDamageDealt;
            ultimateChargePerDamageReceived = chargePerDamageReceived;
        }
#endif
    }
}
