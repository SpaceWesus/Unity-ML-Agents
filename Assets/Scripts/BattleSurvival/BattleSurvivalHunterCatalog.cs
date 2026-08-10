using System.Collections.Generic;
using UnityEngine;
using Turtle.DungeonRaid;

namespace Turtle.BattleSurvival
{
    /// <summary>
    /// Prototype roster data shared by the editor scene authoring tool and the
    /// runtime survival director. Every entry has an individual stat line,
    /// personality weights, hybrid identity, and three-ability combination.
    /// </summary>
    public sealed class BattleHunterProfile
    {
        public string Id;
        public string DisplayName;
        public string SquadName;
        public string BuildLabel;
        public string TraitLabel;
        public RaidCombatRole Role;
        public float Health;
        public float Mana;
        public float ManaRegeneration;
        public float Speed;
        public float BasicDamage;
        public float AttackRange;
        public float PreferredRange;
        public float AttackCooldown;
        public bool Ranged;
        public Color Color;
        public float Aggression;
        public float Cohesion;
        public float Support;
        public List<RaidAbilitySpec> Abilities;
    }

    public static class BattleSurvivalHunterCatalog
    {
        public const int SquadCount = 3;
        public const int HuntersPerSquad = 10;
        public const int HunterCount = SquadCount * HuntersPerSquad;

        public static IReadOnlyList<BattleHunterProfile> CreateProfiles()
        {
            return new[]
            {
                Profile("aegis-celine", "Celine Rook", "Aegis", "Tanker / Field Captain",
                    "Steadfast, protective, measured", RaidCombatRole.Tank,
                    172f, 82f, 6f, 3.8f, 9.5f, 1.45f, 1.1f, 0.92f, false,
                    new Color(0.2f, 0.68f, 1f), 0.42f, 0.94f, 0.88f,
                    Shield("celine-bulwark", "Aegis Bulwark", 5.2f, 28f, 9f, 42f),
                    Taunt("celine-challenge", "Captain's Challenge", 4.4f, 7.5f, 1.15f),
                    Freeze("celine-shield-crash", "Shield Crash", 1.9f, 22f, 6f, 1.1f)),

                Profile("aegis-dax", "Dax Iron", "Aegis", "Fighter / Vanguard",
                    "Bold, loyal, momentum-driven", RaidCombatRole.Fighter,
                    132f, 66f, 5f, 4.45f, 13.5f, 1.55f, 1.15f, 0.76f, false,
                    new Color(1f, 0.55f, 0.16f), 0.9f, 0.75f, 0.42f,
                    Dash("dax-linebreaker", "Linebreaker", 6.5f, 27f, 5.5f),
                    BuffStrike("dax-rally-cleave", "Rally Cleave", 1.8f, 19f, 7f, 1.22f),
                    Area("dax-whirl", "Iron Whirl", 2.5f, 16f, 6.5f, RaidElement.None)),

                Profile("aegis-ori", "Ori Vale", "Aegis", "Healer / Warden",
                    "Patient, triage-first, risk-averse", RaidCombatRole.Healer,
                    101f, 142f, 11f, 4.05f, 7.5f, 5.4f, 4.5f, 1.05f, true,
                    new Color(0.26f, 1f, 0.55f), 0.26f, 0.86f, 1f,
                    Heal("ori-mend", "Focused Mend", 7f, 32f, 3.8f),
                    AreaHeal("ori-sanctuary", "Verdant Sanctuary", 4.4f, 13f, 8f),
                    Mark("ori-guiding-light", "Guiding Light", 7.5f, 1.22f, 7f)),

                Profile("aegis-nyra", "Nyra Flint", "Aegis", "Mage / Pyromancer",
                    "Competitive, volatile, target-fixated", RaidCombatRole.Mage,
                    88f, 155f, 10f, 4.05f, 9f, 6.2f, 5.2f, 1.1f, true,
                    new Color(1f, 0.22f, 0.08f), 0.86f, 0.56f, 0.38f,
                    Fireball("nyra-fireball", "Cinder Bomb", 8.5f, 2.8f, 24f, 7f),
                    Dot("nyra-hexflame", "Hexflame", 7.2f, 10f, 5f, 6f),
                    Area("nyra-flare-ring", "Flare Ring", 3.1f, 19f, 8f, RaidElement.Fire)),

                Profile("aegis-kestrel", "Kestrel Ward", "Aegis", "Ranger / Controller",
                    "Observant, precise, formation-minded", RaidCombatRole.Ranger,
                    96f, 91f, 7f, 4.35f, 11f, 7.1f, 5.9f, 0.86f, true,
                    new Color(1f, 0.88f, 0.22f), 0.62f, 0.9f, 0.56f,
                    Piercing("kestrel-threadshot", "Threadshot", 8f, 18f, 5, 5.8f),
                    Mark("kestrel-quarry", "Quarry Mark", 8.2f, 1.32f, 8f),
                    Freeze("kestrel-pin", "Pinning Arrow", 7.6f, 13f, 6.5f, 1.4f)),

                Profile("aegis-sable", "Sable Kain", "Aegis", "Assassin / Duelist",
                    "Independent, opportunistic, protective of Ori", RaidCombatRole.Assassin,
                    92f, 94f, 7f, 5.15f, 15f, 1.42f, 1.05f, 0.65f, false,
                    new Color(1f, 0.18f, 0.68f), 0.98f, 0.48f, 0.32f,
                    Shadow("sable-umbra", "Umbra Step", 7f, 1.48f, 6f),
                    Execute("sable-finish", "Quietus", 1.8f, 24f, 6.8f),
                    Dash("sable-crosscut", "Crosscut", 5.5f, 22f, 4.8f)),

                Profile("aegis-juno", "Juno Harrow", "Aegis", "Fighter / Guardian",
                    "Dependable, counterattacking, watches the backline", RaidCombatRole.Fighter,
                    139f, 88f, 6.5f, 4.3f, 13f, 1.62f, 1.18f, 0.82f, false,
                    new Color(1f, 0.5f, 0.18f), 0.72f, 0.9f, 0.62f,
                    BuffStrike("juno-watch-cleave", "Watch Cleave", 1.9f, 17f, 6f, 1.2f),
                    Area("juno-harrow-sweep", "Harrow Sweep", 2.6f, 16f, 6.7f, RaidElement.None),
                    Heal("juno-guard-aid", "Guard Aid", 4.5f, 20f, 5.2f)),

                Profile("aegis-mira", "Mira Solace", "Aegis", "Healer / Barrier Mage",
                    "Gentle, anticipatory, conserves mana for emergencies", RaidCombatRole.Healer,
                    99f, 158f, 12f, 4f, 7.8f, 5.8f, 4.7f, 1.05f, true,
                    new Color(0.3f, 1f, 0.7f), 0.25f, 0.92f, 1f,
                    Heal("mira-solace", "Solace", 7.2f, 30f, 3.5f),
                    AreaHeal("mira-haven", "Haven Field", 4.7f, 12f, 7.4f),
                    Freeze("mira-crystal-wall", "Crystal Wall", 7f, 10f, 6.8f, 1.5f)),

                Profile("aegis-corin", "Corin Ash", "Aegis", "Ranger / Trapper",
                    "Methodical, wary, punishes overextension", RaidCombatRole.Ranger,
                    98f, 108f, 7.5f, 4.4f, 11.2f, 7.3f, 6f, 0.86f, true,
                    new Color(0.9f, 0.85f, 0.2f), 0.58f, 0.88f, 0.5f,
                    Piercing("corin-split-shot", "Split Shot", 8.2f, 16f, 5, 5.6f),
                    Mark("corin-snare-mark", "Snare Mark", 8f, 1.27f, 7f),
                    Dot("corin-ash-trap", "Ash Trap", 6.8f, 7f, 5f, 6.5f)),

                Profile("aegis-echo", "Echo Vane", "Aegis", "Mage / Assassin",
                    "Reserved, surgical, seeks isolated elites", RaidCombatRole.Mage,
                    91f, 149f, 10f, 4.55f, 10f, 6.5f, 5.1f, 1f, true,
                    new Color(0.78f, 0.24f, 1f), 0.84f, 0.62f, 0.36f,
                    Fireball("echo-void-star", "Void Star", 8f, 2.6f, 21f, 6.6f),
                    Shadow("echo-vane-step", "Vane Step", 7.2f, 1.38f, 5.5f),
                    Execute("echo-collapse", "Collapse", 6.2f, 19f, 7.1f)),

                Profile("ember-rowan", "Rowan Pike", "Ember", "Fighter / Sergeant",
                    "Decisive, aggressive, inspirational", RaidCombatRole.Fighter,
                    143f, 78f, 6f, 4.35f, 14f, 1.62f, 1.2f, 0.8f, false,
                    new Color(1f, 0.45f, 0.1f), 0.88f, 0.84f, 0.63f,
                    BuffStrike("rowan-banner", "Banner Strike", 2f, 18f, 6.5f, 1.28f),
                    Area("rowan-sunder", "Sundering Arc", 2.7f, 18f, 6.2f, RaidElement.None),
                    Execute("rowan-no-quarter", "No Quarter", 1.9f, 22f, 7.4f)),

                Profile("ember-bram", "Bram Hollow", "Ember", "Tanker / Brawler",
                    "Stubborn, confrontational, fearless", RaidCombatRole.Tank,
                    188f, 73f, 5.5f, 3.65f, 10.5f, 1.5f, 1.1f, 0.95f, false,
                    new Color(0.4f, 0.76f, 1f), 0.76f, 0.8f, 0.62f,
                    Shield("bram-ironhide", "Ironhide Ward", 4.8f, 25f, 10f, 38f),
                    Taunt("bram-roar", "Hollow Roar", 5f, 7f, 1.05f),
                    Area("bram-groundbreak", "Groundbreak", 2.9f, 17f, 7.2f, RaidElement.None)),

                Profile("ember-luma", "Luma Voss", "Ember", "Healer / Storm Mage",
                    "Adaptable, curious, damage-conscious", RaidCombatRole.Healer,
                    98f, 150f, 11.5f, 4.2f, 8.5f, 5.8f, 4.7f, 1f, true,
                    new Color(0.35f, 1f, 0.72f), 0.52f, 0.78f, 0.93f,
                    Heal("luma-spark-mend", "Spark Mend", 7f, 27f, 3.2f),
                    Chain("luma-voltaic", "Voltaic Chain", 7.4f, 15f, 9f, 4, 6.2f),
                    AreaHeal("luma-revival-field", "Revival Field", 4.8f, 11f, 7.2f)),

                Profile("ember-ilya", "Ilya Storm", "Ember", "Mage / Cryomancer",
                    "Analytical, calm, crowd-control focused", RaidCombatRole.Mage,
                    90f, 160f, 10f, 3.95f, 9.5f, 6.4f, 5.3f, 1.15f, true,
                    new Color(0.38f, 0.75f, 1f), 0.48f, 0.82f, 0.52f,
                    Freeze("ilya-lance", "Glacial Lance", 7.8f, 17f, 5.8f, 1.8f),
                    Area("ilya-frost-nova", "Frost Nova", 3.5f, 15f, 7f, RaidElement.Ice),
                    Piercing("ilya-shardline", "Shardline", 8.2f, 16f, 4, 6.8f)),

                Profile("ember-taryn", "Taryn Cross", "Ember", "Ranger / Combat Medic",
                    "Practical, mobile, rescue-oriented", RaidCombatRole.Ranger,
                    103f, 112f, 8f, 4.65f, 10f, 6.8f, 5.6f, 0.9f, true,
                    new Color(0.86f, 0.94f, 0.26f), 0.58f, 0.77f, 0.86f,
                    Piercing("taryn-salvo", "Rescue Salvo", 7.8f, 15f, 4, 5.2f),
                    Heal("taryn-field-dress", "Field Dressing", 5.2f, 22f, 4.4f),
                    Mark("taryn-tracer", "Tracer Bolt", 7.5f, 1.2f, 6.4f)),

                Profile("ember-veil", "Veil Mercer", "Ember", "Assassin / Hexblade",
                    "Cunning, evasive, elite-hunting", RaidCombatRole.Assassin,
                    94f, 118f, 8f, 5.05f, 14.5f, 1.5f, 1.08f, 0.68f, false,
                    new Color(0.8f, 0.22f, 1f), 0.92f, 0.54f, 0.38f,
                    Shadow("veil-fold", "Fold Space", 7.4f, 1.4f, 5.4f),
                    Mark("veil-curse", "Mercer's Curse", 5.8f, 1.4f, 7.2f),
                    Execute("veil-reap", "Reaping Cut", 1.85f, 23f, 6.2f)),

                Profile("ember-garrick", "Garrick Stone", "Ember", "Tanker / Shock Trooper",
                    "Boisterous, stubborn, draws danger toward himself", RaidCombatRole.Tank,
                    182f, 84f, 6f, 3.75f, 10.2f, 1.5f, 1.08f, 0.98f, false,
                    new Color(0.38f, 0.72f, 1f), 0.78f, 0.79f, 0.66f,
                    Shield("garrick-stonewall", "Stonewall", 5f, 24f, 9.2f, 36f),
                    Taunt("garrick-dare", "Come Break Me", 5.2f, 7.3f, 1.08f),
                    Area("garrick-quake", "Quake Fist", 3f, 18f, 7f, RaidElement.None)),

                Profile("ember-sena", "Sena Bright", "Ember", "Healer / Pyromancer",
                    "Optimistic, daring, heals between volleys", RaidCombatRole.Healer,
                    100f, 153f, 11f, 4.2f, 8.2f, 5.9f, 4.8f, 1f, true,
                    new Color(0.3f, 1f, 0.52f), 0.56f, 0.76f, 0.94f,
                    Heal("sena-bright-mend", "Bright Mend", 7f, 28f, 3.4f),
                    AreaHeal("sena-warmth", "Shared Warmth", 4.5f, 11f, 7.2f),
                    Fireball("sena-sunshot", "Sunshot", 8f, 2.5f, 19f, 6.5f)),

                Profile("ember-rook", "Rook Swift", "Ember", "Ranger / Fighter",
                    "Energetic, direct, closes distance after softening targets", RaidCombatRole.Ranger,
                    110f, 100f, 7f, 4.8f, 12f, 7f, 5.6f, 0.8f, true,
                    new Color(1f, 0.78f, 0.18f), 0.82f, 0.7f, 0.48f,
                    Piercing("rook-breach-shot", "Breach Shot", 8.1f, 18f, 5, 5.7f),
                    Dash("rook-running-cut", "Running Cut", 6f, 20f, 5.1f),
                    BuffStrike("rook-battle-cry", "Battle Cry", 1.9f, 15f, 6.4f, 1.22f)),

                Profile("ember-yara", "Yara Night", "Ember", "Assassin / Blood Mage",
                    "Intense, patient, exploits wounded targets", RaidCombatRole.Assassin,
                    95f, 124f, 8f, 5.1f, 14.8f, 1.48f, 1.05f, 0.68f, false,
                    new Color(0.92f, 0.15f, 0.55f), 0.94f, 0.5f, 0.3f,
                    Shadow("yara-nightfold", "Nightfold", 7.5f, 1.45f, 5.6f),
                    Dot("yara-blood-curse", "Blood Curse", 6f, 9f, 6f, 6.2f),
                    Execute("yara-red-end", "Red End", 1.9f, 24f, 6.6f)),

                Profile("vanguard-kael", "Kael Thorn", "Vanguard", "Ranger / Sergeant",
                    "Independent thinker, patient, terrain-aware", RaidCombatRole.Ranger,
                    108f, 104f, 7.5f, 4.5f, 11.5f, 7.3f, 6f, 0.82f, true,
                    new Color(0.96f, 0.82f, 0.18f), 0.64f, 0.91f, 0.57f,
                    Mark("kael-command-mark", "Command Mark", 8.4f, 1.28f, 6.2f),
                    Piercing("kael-stormline", "Stormline", 8.6f, 17f, 6, 6.5f),
                    Chain("kael-thunderhead", "Thunderhead", 7.6f, 13f, 8f, 3, 7.4f)),

                Profile("vanguard-asha", "Asha Bulwark", "Vanguard", "Tanker / Healer",
                    "Empathetic, disciplined, refuses abandonment", RaidCombatRole.Tank,
                    166f, 115f, 8f, 3.85f, 8.8f, 1.42f, 1.08f, 1f, false,
                    new Color(0.3f, 0.82f, 1f), 0.35f, 0.96f, 0.96f,
                    Shield("asha-shared-aegis", "Shared Aegis", 5.7f, 24f, 8f, 35f),
                    AreaHeal("asha-mercy-ring", "Mercy Ring", 4.2f, 10f, 7.5f),
                    Taunt("asha-stand-with-me", "Stand With Me", 4.6f, 8f, 0.92f)),

                Profile("vanguard-fen", "Fen Mercy", "Vanguard", "Healer / Fighter",
                    "Courageous, impulsive rescuer, close-range", RaidCombatRole.Healer,
                    116f, 136f, 10f, 4.55f, 10.5f, 1.65f, 1.2f, 0.82f, false,
                    new Color(0.2f, 1f, 0.45f), 0.7f, 0.73f, 0.98f,
                    Heal("fen-combat-revive", "Combat Revive", 4.8f, 35f, 4.1f),
                    BuffStrike("fen-valiant-blow", "Valiant Blow", 1.9f, 16f, 5.6f, 1.2f),
                    AreaHeal("fen-second-wind", "Second Wind", 3.8f, 9f, 6.4f)),

                Profile("vanguard-sol", "Sol Ardent", "Vanguard", "Mage / Spellblade",
                    "Reckless, brilliant, thrives under pressure", RaidCombatRole.Mage,
                    104f, 147f, 9.5f, 4.45f, 11f, 5.8f, 4.4f, 0.92f, true,
                    new Color(1f, 0.35f, 0.12f), 0.89f, 0.6f, 0.4f,
                    Fireball("sol-starfall", "Starfall", 7.4f, 3.2f, 22f, 6.2f),
                    Dash("sol-flash-step", "Flash Step", 5.8f, 19f, 5f),
                    Area("sol-supernova", "Close Supernova", 3.2f, 21f, 8.5f, RaidElement.Fire)),

                Profile("vanguard-wren", "Wren Hawke", "Vanguard", "Ranger / Assassin",
                    "Restless, self-reliant, flanking specialist", RaidCombatRole.Ranger,
                    93f, 105f, 7f, 4.95f, 12.5f, 7f, 5.7f, 0.75f, true,
                    new Color(1f, 0.72f, 0.16f), 0.93f, 0.52f, 0.35f,
                    Piercing("wren-railshot", "Railshot", 8.2f, 19f, 5, 5.6f),
                    Shadow("wren-slip", "Slip Between", 6.6f, 1.35f, 6f),
                    Execute("wren-cull", "Cull the Weak", 6.8f, 18f, 7f)),

                Profile("vanguard-nox", "Nox Calder", "Vanguard", "Assassin / Saboteur",
                    "Patient, calculating, debuff-oriented", RaidCombatRole.Assassin,
                    97f, 126f, 8.5f, 4.9f, 14f, 1.5f, 1.08f, 0.72f, false,
                    new Color(0.82f, 0.2f, 0.82f), 0.78f, 0.68f, 0.44f,
                    Dot("nox-venom", "Calder Venom", 6.2f, 8f, 6f, 6.3f),
                    Mark("nox-expose", "Expose Fault", 6.5f, 1.45f, 7.8f),
                    Shadow("nox-backline", "Backline Step", 7.2f, 1.5f, 5.8f)),

                Profile("vanguard-thane", "Thane Holt", "Vanguard", "Tanker / Ranger",
                    "Stoic, watchful, protects mobile allies", RaidCombatRole.Tank,
                    174f, 101f, 7f, 3.9f, 9.4f, 1.5f, 1.1f, 0.96f, false,
                    new Color(0.32f, 0.76f, 1f), 0.4f, 0.94f, 0.86f,
                    Shield("thane-cover", "Holt Cover", 5.5f, 25f, 8.8f, 38f),
                    Taunt("thane-anchor", "Anchor Call", 5f, 7.6f, 0.95f),
                    Piercing("thane-harpoon", "Harpoon Line", 7.5f, 15f, 4, 6.3f)),

                Profile("vanguard-elowen", "Elowen Reed", "Vanguard", "Healer / Ranger",
                    "Perceptive, mobile, prioritizes isolated allies", RaidCombatRole.Healer,
                    102f, 148f, 11f, 4.5f, 8f, 6.2f, 5f, 0.96f, true,
                    new Color(0.25f, 1f, 0.6f), 0.42f, 0.84f, 0.98f,
                    Heal("elowen-reach", "Reaching Mend", 7.5f, 29f, 3.6f),
                    Mark("elowen-signal", "Rescue Signal", 8f, 1.24f, 6.5f),
                    Piercing("elowen-covering-shot", "Covering Shot", 8f, 14f, 4, 5.9f)),

                Profile("vanguard-cass", "Cass Forge", "Vanguard", "Fighter / Mage",
                    "Inventive, fearless, alternates steel and spell", RaidCombatRole.Fighter,
                    128f, 112f, 8f, 4.5f, 13f, 1.65f, 1.18f, 0.78f, false,
                    new Color(1f, 0.48f, 0.14f), 0.86f, 0.7f, 0.46f,
                    Area("cass-forgeburst", "Forgeburst", 2.8f, 18f, 6.4f, RaidElement.Fire),
                    Dash("cass-tempered-rush", "Tempered Rush", 6f, 22f, 5.2f),
                    Freeze("cass-quench", "Quench", 2.2f, 14f, 6.8f, 1.3f)),

                Profile("vanguard-vesper", "Vesper Quill", "Vanguard", "Mage / Controller",
                    "Detached, tactical, values battlefield control", RaidCombatRole.Mage,
                    92f, 164f, 10.5f, 4f, 9.2f, 6.6f, 5.4f, 1.1f, true,
                    new Color(0.62f, 0.35f, 1f), 0.5f, 0.86f, 0.58f,
                    Chain("vesper-ink-lightning", "Ink Lightning", 7.8f, 14f, 8f, 5, 6.8f),
                    Freeze("vesper-still-word", "Still Word", 7.2f, 12f, 6.2f, 1.7f),
                    Fireball("vesper-falling-rune", "Falling Rune", 8.2f, 3f, 20f, 7f))
            };
        }

        private static BattleHunterProfile Profile(
            string id, string name, string squad, string build, string traits,
            RaidCombatRole role, float health, float mana, float regeneration,
            float speed, float damage, float range, float preferredRange,
            float cooldown, bool ranged, Color color, float aggression,
            float cohesion, float support, params RaidAbilitySpec[] abilities)
        {
            return new BattleHunterProfile
            {
                Id = id,
                DisplayName = name,
                SquadName = squad,
                BuildLabel = build,
                TraitLabel = traits,
                Role = role,
                Health = health,
                Mana = mana,
                ManaRegeneration = regeneration,
                Speed = speed,
                BasicDamage = damage,
                AttackRange = range,
                PreferredRange = preferredRange,
                AttackCooldown = cooldown,
                Ranged = ranged,
                Color = color,
                Aggression = aggression,
                Cohesion = cohesion,
                Support = support,
                Abilities = new List<RaidAbilitySpec>(abilities)
            };
        }

        private static RaidAbilitySpec BaseAbility(
            string id, string name, RaidAbilityEffect effect, float range,
            float radius, float power, float cooldown, float mana, Color color)
        {
            return RaidAbilitySpec.Create(
                id, name, effect, range, radius, power, cooldown, mana, color);
        }

        private static RaidAbilitySpec Shield(
            string id, string name, float radius, float power, float cooldown, float mana)
        {
            var result = BaseAbility(id, name, RaidAbilityEffect.Shield, radius, radius,
                power, cooldown, mana, Color.white);
            result.duration = 10f;
            result.shape = RaidAttackShape.Circle;
            result.maximumTargets = 12;
            return result;
        }

        private static RaidAbilitySpec Taunt(
            string id, string name, float radius, float cooldown, float damageMultiplier)
        {
            var result = BaseAbility(id, name, RaidAbilityEffect.Taunt, radius, radius,
                0f, cooldown, 18f, new Color(1f, 0.72f, 0.2f));
            result.duration = 4f;
            result.multiplier = damageMultiplier;
            result.shape = RaidAttackShape.Circle;
            result.maximumTargets = 32;
            return result;
        }

        private static RaidAbilitySpec Heal(
            string id, string name, float range, float power, float cooldown)
        {
            return BaseAbility(id, name, RaidAbilityEffect.Heal, range, 0f,
                power, cooldown, 22f, new Color(0.2f, 1f, 0.48f));
        }

        private static RaidAbilitySpec AreaHeal(
            string id, string name, float radius, float power, float cooldown)
        {
            var result = BaseAbility(id, name, RaidAbilityEffect.PersistentAreaHeal,
                radius, radius, power, cooldown, 34f, new Color(0.18f, 1f, 0.55f));
            result.duration = 4f;
            result.preferredHealthThreshold = 0.82f;
            result.shape = RaidAttackShape.Circle;
            result.maximumTargets = 18;
            return result;
        }

        private static RaidAbilitySpec Area(
            string id, string name, float radius, float power, float cooldown, RaidElement element)
        {
            var result = BaseAbility(id, name, RaidAbilityEffect.AreaDamage, radius,
                radius, power, cooldown, 25f, element == RaidElement.Ice
                    ? new Color(0.25f, 0.75f, 1f)
                    : element == RaidElement.Fire
                        ? new Color(1f, 0.22f, 0.05f)
                        : new Color(1f, 0.65f, 0.18f));
            result.shape = RaidAttackShape.Circle;
            result.element = element;
            result.maximumTargets = 48;
            return result;
        }

        private static RaidAbilitySpec Dash(
            string id, string name, float range, float power, float cooldown)
        {
            var result = BaseAbility(id, name, RaidAbilityEffect.DashStrike, range,
                0f, power, cooldown, 18f, new Color(1f, 0.6f, 0.18f));
            result.width = 0.42f;
            return result;
        }

        private static RaidAbilitySpec Shadow(
            string id, string name, float range, float multiplier, float cooldown)
        {
            var result = BaseAbility(id, name, RaidAbilityEffect.ShadowStep, range,
                0f, 0f, cooldown, 24f, new Color(0.65f, 0.2f, 1f));
            result.multiplier = multiplier;
            result.duration = 3.5f;
            return result;
        }

        private static RaidAbilitySpec Execute(
            string id, string name, float range, float power, float cooldown)
        {
            var result = BaseAbility(id, name, RaidAbilityEffect.Execute, range,
                0f, power, cooldown, 22f, new Color(1f, 0.18f, 0.35f));
            result.width = 0.42f;
            result.duration = 0.45f;
            return result;
        }

        private static RaidAbilitySpec BuffStrike(
            string id, string name, float range, float power, float cooldown, float buff)
        {
            var result = BaseAbility(id, name, RaidAbilityEffect.DamageAndBuffAllies,
                range, 4f, power, cooldown, 22f, new Color(1f, 0.72f, 0.2f));
            result.secondaryPower = buff;
            result.duration = 5f;
            result.width = 0.45f;
            return result;
        }

        private static RaidAbilitySpec Dot(
            string id, string name, float range, float power, float damagePerTick, float cooldown)
        {
            var result = BaseAbility(id, name, RaidAbilityEffect.DamageOverTime,
                range, 0f, power, cooldown, 20f, new Color(1f, 0.24f, 0.05f));
            result.secondaryPower = damagePerTick;
            result.duration = 5f;
            result.width = 0.3f;
            return result;
        }

        private static RaidAbilitySpec Freeze(
            string id, string name, float range, float power, float cooldown, float duration)
        {
            var result = BaseAbility(id, name, RaidAbilityEffect.Freeze,
                range, 0f, power, cooldown, 22f, new Color(0.28f, 0.78f, 1f));
            result.duration = duration;
            result.width = 0.28f;
            return result;
        }

        private static RaidAbilitySpec Chain(
            string id, string name, float range, float power, float chainPower,
            int maximumChains, float cooldown)
        {
            var result = BaseAbility(id, name, RaidAbilityEffect.ChainDamage,
                range, 2.7f, power, cooldown, 28f, new Color(0.45f, 0.75f, 1f));
            result.secondaryPower = chainPower;
            result.maximumTargets = maximumChains;
            result.width = 0.2f;
            return result;
        }

        private static RaidAbilitySpec Fireball(
            string id, string name, float range, float radius, float power, float cooldown)
        {
            var result = BaseAbility(id, name, RaidAbilityEffect.ProjectileAreaDamage,
                range, radius, power, cooldown, 32f, new Color(1f, 0.2f, 0.04f));
            result.secondaryPower = 5f;
            result.duration = 4f;
            result.maximumTargets = 48;
            result.width = 0.22f;
            result.element = RaidElement.Fire;
            return result;
        }

        private static RaidAbilitySpec Mark(
            string id, string name, float range, float multiplier, float cooldown)
        {
            var result = BaseAbility(id, name, RaidAbilityEffect.DamageMark,
                range, 0f, 0f, cooldown, 18f, new Color(1f, 0.85f, 0.2f));
            result.multiplier = multiplier;
            result.duration = 6f;
            return result;
        }

        private static RaidAbilitySpec Piercing(
            string id, string name, float range, float power, int maximumTargets, float cooldown)
        {
            var result = BaseAbility(id, name, RaidAbilityEffect.PiercingDamage,
                range, 0f, power, cooldown, 24f, new Color(1f, 0.82f, 0.25f));
            result.maximumTargets = maximumTargets;
            result.width = 0.2f;
            return result;
        }
    }
}
