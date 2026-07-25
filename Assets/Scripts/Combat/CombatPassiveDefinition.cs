using UnityEngine;

namespace Turtle.Combat
{
    [CreateAssetMenu(
        menuName = "Turtle/Combat/Passive",
        fileName = "Combat Passive")]
    public sealed class CombatPassiveDefinition : ScriptableObject
    {
        [SerializeField] private string passiveId = "passive";
        [SerializeField] private string displayName = "Passive";
        [SerializeField, TextArea] private string description;
        [SerializeField] private string discipline = "Unaligned";
        [SerializeField, Min(0.1f)] private float cooldownDurationMultiplier = 1f;
        [SerializeField, Min(0f)] private float ultimateGainMultiplier = 1f;
        [SerializeField, Min(0f)] private float abilityDamageMultiplier = 1f;

        public string PassiveId => passiveId;
        public string DisplayName => displayName;
        public string Description => description;
        public string Discipline => discipline;
        public float CooldownDurationMultiplier => cooldownDurationMultiplier;
        public float UltimateGainMultiplier => ultimateGainMultiplier;
        public float AbilityDamageMultiplier => abilityDamageMultiplier;

#if UNITY_EDITOR
        public void ConfigureEditor(
            string id,
            string name,
            string details,
            string sourceDiscipline,
            float cooldownMultiplier,
            float ultimateMultiplier,
            float damageMultiplier)
        {
            passiveId = id;
            displayName = name;
            description = details;
            discipline = sourceDiscipline;
            cooldownDurationMultiplier = cooldownMultiplier;
            ultimateGainMultiplier = ultimateMultiplier;
            abilityDamageMultiplier = damageMultiplier;
        }
#endif
    }
}
