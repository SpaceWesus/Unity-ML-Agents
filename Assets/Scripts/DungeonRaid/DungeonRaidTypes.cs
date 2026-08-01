using System;
using UnityEngine;

namespace Turtle.DungeonRaid
{
    public enum RaidFaction
    {
        Hunters,
        Monsters
    }

    public enum RaidCombatRole
    {
        Tank,
        Fighter,
        Healer,
        Mage,
        Ranger,
        Assassin,
        Melee,
        Archer,
        Elite
    }

    public enum RaidLifeState
    {
        Active,
        Downed,
        Dead
    }

    public enum RaidPartyPhase
    {
        Waiting,
        Rallying,
        Advancing,
        Engaging,
        Recovering,
        Looting,
        Complete,
        Failed
    }

    public enum RaidPodPhase
    {
        Dormant,
        Alerted,
        Engaging,
        Defeated
    }

    /// <summary>
    /// Semantic purpose used by room-first generation, encounter placement, and
    /// presentation. Themes decide how a purpose looks; they do not change what
    /// that room contributes to the dungeon route.
    /// </summary>
    public enum RaidRoomPurpose
    {
        Entrance,
        Encounter,
        Transition,
        Boss,
        Reward,
        Resource,
        Event
    }

    public enum RaidSpawnMarkerKind
    {
        Party,
        EnemyPod,
        Elite,
        Boss,
        Chest,
        ExitPortal
    }

    public enum RaidAbilityEffect
    {
        Damage,
        AreaDamage,
        Heal,
        AreaHeal,
        Shield,
        Taunt,
        DashStrike,
        Execute,
        DamageAndBuffAllies,
        DamageOverTime,
        Freeze,
        ChainDamage,
        ProjectileAreaDamage,
        DamageMark,
        PersistentAreaHeal,
        PiercingDamage,
        ShadowStep
    }

    public enum RaidAttackShape
    {
        Single,
        Circle,
        Cone,
        Rectangle
    }

    public enum RaidElement
    {
        None,
        Fire,
        Ice,
        Lightning
    }

    /// <summary>
    /// Small, serializable prototype rule set. The AI reasons from effects, ranges,
    /// and costs rather than hunter names or a fixed party composition.
    /// </summary>
    [Serializable]
    public sealed class RaidAbilitySpec
    {
        public string id = "ability";
        public string displayName = "Ability";
        public RaidAbilityEffect effect;
        [Min(0.1f)] public float range = 5f;
        [Min(0f)] public float radius = 1f;
        [Min(0f)] public float power = 20f;
        [Min(0f)] public float cooldown = 5f;
        [Min(0f)] public float manaCost = 10f;
        [Min(0f)] public float duration = 2f;
        [Range(0f, 1f)] public float preferredHealthThreshold = 0.7f;
        public RaidAttackShape shape = RaidAttackShape.Single;
        public RaidElement element;
        [Min(0f)] public float width = 0.35f;
        [Range(1f, 360f)] public float angle = 90f;
        [Min(1)] public int maximumTargets = 1;
        [Min(0f)] public float secondaryPower;
        [Min(0f)] public float multiplier = 1f;
        public bool scalesWithBasicAttack;
        public bool scalesWithTargetMaximumHealth;
        public Color color = Color.white;

        public static RaidAbilitySpec Create(
            string abilityId,
            string name,
            RaidAbilityEffect abilityEffect,
            float abilityRange,
            float abilityRadius,
            float abilityPower,
            float cooldownSeconds,
            float mana,
            Color theme,
            float effectDuration = 2f,
            float healthThreshold = 0.7f)
        {
            return new RaidAbilitySpec
            {
                id = abilityId,
                displayName = name,
                effect = abilityEffect,
                range = Mathf.Max(0.1f, abilityRange),
                radius = Mathf.Max(0f, abilityRadius),
                power = Mathf.Max(0f, abilityPower),
                cooldown = Mathf.Max(0f, cooldownSeconds),
                manaCost = Mathf.Max(0f, mana),
                duration = Mathf.Max(0f, effectDuration),
                preferredHealthThreshold = Mathf.Clamp01(healthThreshold),
                color = theme
            };
        }
    }
}
