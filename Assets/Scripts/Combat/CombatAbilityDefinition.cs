using UnityEngine;

namespace Turtle.Combat
{
    public enum AbilityMovementMode
    {
        Mobile,
        Anchored
    }

    public enum AbilityDodgeCancelMode
    {
        DodgeAllowed,
        Committed
    }

    public enum AbilityAiIntent
    {
        Offensive,
        Defensive,
        Mobility,
        Utility
    }

    public readonly struct CombatAbilityContext
    {
        public CombatAbilityContext(
            CombatAbilityController controller,
            Combatant caster,
            Vector3 direction)
        {
            Controller = controller;
            Caster = caster;
            Direction = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
            if (Direction.sqrMagnitude < 0.001f && caster != null)
            {
                Direction = caster.transform.forward;
            }
        }

        public CombatAbilityController Controller { get; }
        public Combatant Caster { get; }
        public Vector3 Direction { get; }
    }

    public abstract class CombatAbilityDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string abilityId = "ability";
        [SerializeField] private string displayName = "Ability";
        [SerializeField, TextArea] private string description;
        [SerializeField] private string discipline = "Unaligned";

        [Header("Action Policy")]
        [SerializeField, Min(0f)] private float cooldown = 5f;
        [SerializeField, Min(0f)] private float manaCost = 15f;
        [SerializeField, Min(0f)] private float castTime = 0.2f;
        [SerializeField, Min(0f)] private float recovery = 0.25f;
        [SerializeField] private AbilityMovementMode movementMode = AbilityMovementMode.Mobile;
        [SerializeField] private AbilityDodgeCancelMode dodgeCancelMode =
            AbilityDodgeCancelMode.DodgeAllowed;

        [Header("AI")]
        [SerializeField] private AbilityAiIntent aiIntent = AbilityAiIntent.Offensive;
        [SerializeField, Min(0f)] private float aiMinimumRange;
        [SerializeField, Min(0.1f)] private float aiMaximumRange = 12f;

        [Header("Presentation")]
        [SerializeField] private Color themeColor = new(0.2f, 0.65f, 1f);

        public string AbilityId => abilityId;
        public string DisplayName => displayName;
        public string Description => description;
        public string Discipline => discipline;
        public float Cooldown => cooldown;
        public float ManaCost => manaCost;
        public float CastTime => castTime;
        public float Recovery => recovery;
        public float ActionDuration => Mathf.Max(0.01f, castTime + recovery);
        public bool AllowsMovement => movementMode == AbilityMovementMode.Mobile;
        public bool AllowsDodgeCancel =>
            dodgeCancelMode == AbilityDodgeCancelMode.DodgeAllowed;
        public AbilityAiIntent AiIntent => aiIntent;
        public float AiMinimumRange => aiMinimumRange;
        public float AiMaximumRange => Mathf.Max(aiMinimumRange, aiMaximumRange);
        public Color ThemeColor => themeColor;

        public bool IsInAiRange(float distance)
        {
            return distance >= aiMinimumRange && distance <= AiMaximumRange;
        }

        public abstract void Activate(in CombatAbilityContext context);

#if UNITY_EDITOR
        protected void ConfigureEditor(
            string id,
            string name,
            string details,
            string sourceDiscipline,
            float cooldownSeconds,
            float abilityManaCost,
            float castSeconds,
            float recoverySeconds,
            AbilityMovementMode movement,
            AbilityDodgeCancelMode dodgeCancel,
            AbilityAiIntent intent,
            float minimumRange,
            float maximumRange,
            Color color)
        {
            abilityId = id;
            displayName = name;
            description = details;
            discipline = sourceDiscipline;
            cooldown = cooldownSeconds;
            manaCost = abilityManaCost;
            castTime = castSeconds;
            recovery = recoverySeconds;
            movementMode = movement;
            dodgeCancelMode = dodgeCancel;
            aiIntent = intent;
            aiMinimumRange = minimumRange;
            aiMaximumRange = maximumRange;
            themeColor = color;
        }
#endif
    }
}
