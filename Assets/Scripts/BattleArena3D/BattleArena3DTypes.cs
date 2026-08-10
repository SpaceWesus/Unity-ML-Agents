using System;
using UnityEngine;

namespace Turtle.BattleArena3D
{
    public enum BattleArenaFaction3D
    {
        Hunters,
        Monsters
    }

    public enum BattleArenaPhase3D
    {
        Prewarming,
        Preparing,
        Wave,
        Intermission,
        Victory,
        Defeat
    }

    public enum BattleArenaLifeState3D
    {
        Active,
        Downed,
        Dead
    }

    public enum BattleArenaMonsterArchetype3D
    {
        Ravager,
        Brute,
        Spitter,
        Hexer,
        Charger,
        Elite
    }

    public enum BattleArenaSquadOrder3D
    {
        HoldCenter,
        DefendWest,
        DefendEast,
        DefendNorth,
        DefendSouth,
        Regroup,
        Rescue
    }

    public enum BattleArenaEffectsLevel3D
    {
        Minimal,
        Reduced,
        Full
    }

    public enum BattleArenaCameraMotion3D
    {
        Off,
        Reduced,
        Full
    }

    public enum BattleArenaWorldBars3D
    {
        Contextual,
        SelectedOnly,
        All
    }

    public enum BattleArenaDamageNumbers3D
    {
        Off,
        Contextual,
        All
    }

    [Serializable]
    public struct BattleArenaPresentationOptions3D
    {
        [Range(1f, 1.5f)] public float UiScale;
        public BattleArenaEffectsLevel3D EffectsLevel;
        public BattleArenaCameraMotion3D CameraMotion;
        public BattleArenaWorldBars3D WorldBars;
        public BattleArenaDamageNumbers3D DamageNumbers;
        public bool HighContrastFactions;
        public bool ReducedMotion;

        public static BattleArenaPresentationOptions3D Default => new()
        {
            UiScale = 1f,
            EffectsLevel = BattleArenaEffectsLevel3D.Full,
            CameraMotion = BattleArenaCameraMotion3D.Reduced,
            WorldBars = BattleArenaWorldBars3D.Contextual,
            DamageNumbers = BattleArenaDamageNumbers3D.Contextual,
            HighContrastFactions = false,
            ReducedMotion = false
        };

        public BattleArenaPresentationOptions3D Sanitized()
        {
            UiScale = UiScale switch
            {
                < 1.125f => 1f,
                < 1.375f => 1.25f,
                _ => 1.5f
            };
            EffectsLevel = (BattleArenaEffectsLevel3D)Mathf.Clamp((int)EffectsLevel, 0, 2);
            CameraMotion = (BattleArenaCameraMotion3D)Mathf.Clamp((int)CameraMotion, 0, 2);
            WorldBars = (BattleArenaWorldBars3D)Mathf.Clamp((int)WorldBars, 0, 2);
            DamageNumbers = (BattleArenaDamageNumbers3D)Mathf.Clamp((int)DamageNumbers, 0, 2);
            return this;
        }
    }

    [Serializable]
    public struct BattleArenaUnitSnapshot3D
    {
        public string DisplayName;
        public string Build;
        public string Objective;
        public BattleArenaLifeState3D LifeState;
        public float Health;
        public float MaximumHealth;
        public float Mana;
        public float MaximumMana;
        public float Shield;
        public int Kills;
        public int AbilityCasts;
        public float DamageDealt;
    }

    public readonly struct BattleArenaDamageResult3D
    {
        public BattleArenaDamageResult3D(
            float appliedDamage,
            float absorbedShield,
            bool shieldBroken,
            bool becameDowned,
            bool died)
        {
            AppliedDamage = appliedDamage;
            AbsorbedShield = absorbedShield;
            ShieldBroken = shieldBroken;
            BecameDowned = becameDowned;
            Died = died;
        }

        public float AppliedDamage { get; }
        public float AbsorbedShield { get; }
        public float TotalResolved => AppliedDamage + AbsorbedShield;
        public bool ShieldBroken { get; }
        public bool BecameDowned { get; }
        public bool Died { get; }
    }
}
