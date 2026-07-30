using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Turtle.Ecosystem
{
    [Serializable]
    public sealed class HunterAttributeInvestment
    {
        public string attributeId;
        public int baseValue = 10;
        public int investedAbilityPoints;

        public int Value => Mathf.Max(1, baseValue) + Mathf.Max(0, investedAbilityPoints);
    }

    [Serializable]
    public sealed class HunterAffinityState
    {
        public HunterArchetype archetype;
        [Range(0f, 1f)] public float value = 0.5f;
    }

    [Serializable]
    public sealed class HunterLearnedAbility
    {
        public string abilityId;
        // Snapshot the paid cost so a later content rebalance cannot silently change rank.
        public int investedAbilityPoints;
    }

    [Serializable]
    public sealed class HunterAbilityLoadout
    {
        public List<string> cooldownAbilityIds = new() { "", "", "" };
        public string ultimateAbilityId;
        public List<string> passiveAbilityIds = new() { "", "" };
    }

    [Serializable]
    public sealed class HunterCareerState
    {
        public bool initialized;
        public long currentExperience;
        public long lifetimeExperience;
        public int earnedAbilityPoints;
        public int lastTrainingDay = -1;
        public string plannedAbilityId;
        public List<HunterAttributeInvestment> attributes = new();
        public List<HunterAffinityState> affinities = new();
        public List<HunterLearnedAbility> learnedAbilities = new();
        public HunterAbilityLoadout loadout = new();

        public int InvestedAbilityPoints => EcosystemCareerRules.InvestedAbilityPoints(this);
        public int UnspentAbilityPoints =>
            Mathf.Max(0, earnedAbilityPoints - InvestedAbilityPoints);
        public int CareerLevel => earnedAbilityPoints >= int.MaxValue
            ? int.MaxValue
            : Mathf.Max(1, earnedAbilityPoints + 1);
    }

    [Serializable]
    public sealed class HunterAttributeDefinition
    {
        public readonly string id;
        public readonly string displayName;
        [TextArea] public readonly string description;
        public readonly int defaultBaseValue = 10;
        public readonly HunterArchetype primaryArchetype;
        public readonly HunterArchetype secondaryArchetype;

        public HunterAttributeDefinition(
            string attributeId,
            string name,
            string details,
            HunterArchetype primary,
            HunterArchetype secondary)
        {
            id = attributeId;
            displayName = name;
            description = details;
            primaryArchetype = primary;
            secondaryArchetype = secondary;
        }
    }

    [Serializable]
    public sealed class HunterAbilityDefinition
    {
        public readonly string id;
        public readonly string displayName;
        [TextArea] public readonly string description;
        public readonly HunterAbilityKind kind;
        [Min(1)] public readonly int abilityPointCost = 1;
        [Min(0)] public readonly int requiredInvestedAbilityPoints;
        public readonly HunterArchetype primaryArchetype;
        public readonly HunterArchetype secondaryArchetype;

        public HunterAbilityDefinition(
            string abilityId,
            string name,
            string details,
            HunterAbilityKind abilityKind,
            int cost,
            int prerequisite,
            HunterArchetype primary,
            HunterArchetype secondary)
        {
            id = abilityId;
            displayName = name;
            description = details;
            kind = abilityKind;
            abilityPointCost = Mathf.Max(1, cost);
            requiredInvestedAbilityPoints = Mathf.Max(0, prerequisite);
            primaryArchetype = primary;
            secondaryArchetype = secondary;
        }
    }

    /// <summary>
    /// One authored progression catalog for both controlled and autonomous hunters.
    /// Stable IDs intentionally match existing combat ability assets where those exist.
    /// </summary>
    public static class EcosystemCareerCatalog
    {
        public const int CooldownSlotCount = 3;
        public const int UltimateSlotIndex = 3;
        public const int PassiveSlotCount = 2;

        private static readonly HunterAttributeDefinition[] AttributeData =
        {
            new("vitality", "Vitality", "Health, recovery, and physical staying power.",
                HunterArchetype.Tank, HunterArchetype.Healer),
            new("mana", "Mana", "Magical capacity and sustained skill use.",
                HunterArchetype.Mage, HunterArchetype.Healer),
            new("strength", "Strength", "Physical force behind attacks and carries.",
                HunterArchetype.Fighter, HunterArchetype.Tank),
            new("agility", "Agility", "Acceleration, evasion, and close-range precision.",
                HunterArchetype.Assassin, HunterArchetype.Ranger),
            new("perception", "Perception", "Ranged accuracy, awareness, and weak-point reading.",
                HunterArchetype.Ranger, HunterArchetype.Assassin),
            new("intellect", "Intellect", "Spell complexity, control, and magical efficiency.",
                HunterArchetype.Mage, HunterArchetype.Healer),
            new("resilience", "Resilience", "Armor use, poise, and resistance to disruption.",
                HunterArchetype.Tank, HunterArchetype.Fighter)
        };

        private static readonly HunterAbilityDefinition[] AbilityData =
        {
            new("fighter.power-strike", "Power Strike", "A committed physical blow.",
                HunterAbilityKind.Cooldown, 1, 0, HunterArchetype.Fighter, HunterArchetype.Tank),
            new("fighter.battle-rush", "Battle Rush", "Close distance without surrendering pressure.",
                HunterAbilityKind.Cooldown, 3, 2, HunterArchetype.Fighter, HunterArchetype.Assassin),
            new("healer.mend-wounds", "Mend Wounds", "Restore an ally's fighting condition.",
                HunterAbilityKind.Cooldown, 1, 0, HunterArchetype.Healer, HunterArchetype.Mage),
            new("healer.cleanse", "Cleanse", "Remove a harmful condition from an ally.",
                HunterAbilityKind.Cooldown, 3, 2, HunterArchetype.Healer, HunterArchetype.Tank),
            new("assassin.shadow-step", "Shadow Step", "Reposition through an opponent's blind angle.",
                HunterAbilityKind.Cooldown, 1, 0, HunterArchetype.Assassin, HunterArchetype.Mage),
            new("assassin.exploit-opening", "Exploit Opening", "Punish an exposed or distracted target.",
                HunterAbilityKind.Cooldown, 3, 2, HunterArchetype.Assassin, HunterArchetype.Ranger),
            new("ranger.hunters-mark", "Hunter's Mark", "Expose a target for coordinated pressure.",
                HunterAbilityKind.Cooldown, 1, 0, HunterArchetype.Ranger, HunterArchetype.Assassin),
            new("ranger.volley", "Volley", "Saturate a distant area with projectiles.",
                HunterAbilityKind.Cooldown, 3, 2, HunterArchetype.Ranger, HunterArchetype.Fighter),
            new("tank.guardian-wall", "Guardian Wall", "Intercept pressure aimed at nearby allies.",
                HunterAbilityKind.Cooldown, 1, 0, HunterArchetype.Tank, HunterArchetype.Fighter),
            new("tank.provoke", "Provoke", "Force enemies to account for the tanker.",
                HunterAbilityKind.Cooldown, 3, 2, HunterArchetype.Tank, HunterArchetype.Healer),
            new("mage.arcane-bolt", "Arcane Bolt", "A quick elemental projectile.",
                HunterAbilityKind.Cooldown, 1, 0, HunterArchetype.Mage, HunterArchetype.Ranger),
            new("mage.aegis-barrier", "Aegis Barrier", "Project a temporary magical barrier.",
                HunterAbilityKind.Cooldown, 3, 2, HunterArchetype.Mage, HunterArchetype.Tank),
            new("mage.spatial-step", "Spatial Step", "Teleport a short distance through folded space.",
                HunterAbilityKind.Cooldown, 4, 4, HunterArchetype.Mage, HunterArchetype.Assassin),

            new("fighter.limit-break", "Limit Break", "Spend everything on a decisive assault.",
                HunterAbilityKind.Ultimate, 6, 6, HunterArchetype.Fighter, HunterArchetype.Assassin),
            new("healer.mass-restoration", "Mass Restoration", "Stabilize an entire strike team.",
                HunterAbilityKind.Ultimate, 6, 6, HunterArchetype.Healer, HunterArchetype.Mage),
            new("assassin.execution-chain", "Execution Chain", "Chain lethal openings between targets.",
                HunterAbilityKind.Ultimate, 6, 6, HunterArchetype.Assassin, HunterArchetype.Fighter),
            new("ranger.storm-volley", "Storm Volley", "Blanket a battlefield in precision fire.",
                HunterAbilityKind.Ultimate, 6, 6, HunterArchetype.Ranger, HunterArchetype.Mage),
            new("tank.unbroken-bastion", "Unbroken Bastion", "Become the immovable center of the party.",
                HunterAbilityKind.Ultimate, 6, 6, HunterArchetype.Tank, HunterArchetype.Healer),
            new("mage.arcane-nova", "Arcane Nova", "Release stored mana in a violent radial burst.",
                HunterAbilityKind.Ultimate, 6, 6, HunterArchetype.Mage, HunterArchetype.Fighter),
            new("mage.grave-calling", "Grave Calling", "Reanimate a limited fallen servant.",
                HunterAbilityKind.Ultimate, 8, 12, HunterArchetype.Mage, HunterArchetype.Healer),

            new("fighter.relentless", "Relentless", "Maintain pressure after a successful hit.",
                HunterAbilityKind.Passive, 3, 2, HunterArchetype.Fighter, HunterArchetype.Tank),
            new("healer.renewing-aura", "Renewing Aura", "Improve nearby recovery over time.",
                HunterAbilityKind.Passive, 3, 2, HunterArchetype.Healer, HunterArchetype.Mage),
            new("assassin.predator", "Predator", "Gain leverage against isolated targets.",
                HunterAbilityKind.Passive, 3, 2, HunterArchetype.Assassin, HunterArchetype.Ranger),
            new("ranger.pathfinder", "Pathfinder", "Read terrain and approach routes more efficiently.",
                HunterAbilityKind.Passive, 3, 2, HunterArchetype.Ranger, HunterArchetype.Assassin),
            new("tank.iron-body", "Iron Body", "Convert discipline into physical stability.",
                HunterAbilityKind.Passive, 3, 2, HunterArchetype.Tank, HunterArchetype.Fighter),
            new("mage.arcane-tempo", "Arcane Tempo", "Flow between spells with less recovery.",
                HunterAbilityKind.Passive, 3, 2, HunterArchetype.Mage, HunterArchetype.Healer)
        };

        public static IReadOnlyList<HunterAttributeDefinition> Attributes => AttributeData;
        public static IReadOnlyList<HunterAbilityDefinition> Abilities => AbilityData;

        public static HunterAttributeDefinition FindAttribute(string id) =>
            string.IsNullOrEmpty(id)
                ? null
                : Array.Find(AttributeData, item => item.id == id);

        public static HunterAbilityDefinition FindAbility(string id) =>
            string.IsNullOrEmpty(id)
                ? null
                : Array.Find(AbilityData, item => item.id == id);
    }

    public readonly struct HunterBuildDescriptor
    {
        public HunterBuildDescriptor(
            HunterArchetype primary,
            HunterArchetype secondary,
            bool hybrid,
            float primaryScore,
            float secondaryScore)
        {
            Primary = primary;
            Secondary = secondary;
            IsHybrid = hybrid;
            PrimaryScore = primaryScore;
            SecondaryScore = secondaryScore;
        }

        public HunterArchetype Primary { get; }
        public HunterArchetype Secondary { get; }
        public bool IsHybrid { get; }
        public float PrimaryScore { get; }
        public float SecondaryScore { get; }
        public string Label => IsHybrid ? $"{Primary} / {Secondary}" : Primary.ToString();
    }

    public readonly struct HunterExperienceResult
    {
        public HunterExperienceResult(int experienceGranted, int abilityPointsGranted)
        {
            ExperienceGranted = experienceGranted;
            AbilityPointsGranted = abilityPointsGranted;
        }

        public int ExperienceGranted { get; }
        public int AbilityPointsGranted { get; }
    }

    /// <summary>
    /// Deterministic career rules. This is domain code: views may read it, but every mutation
    /// still enters through EcosystemActionService.
    /// </summary>
    public static class EcosystemCareerRules
    {
        public const int AbilityPointsPerRankBand = 6;

        public static int InvestedAbilityPoints(HunterCareerState career)
        {
            if (career == null) return 0;
            var total = 0L;
            if (career.attributes != null)
            {
                foreach (var attribute in career.attributes)
                {
                    if (attribute != null) total += Mathf.Max(0, attribute.investedAbilityPoints);
                }
            }
            if (career.learnedAbilities != null)
            {
                foreach (var ability in career.learnedAbilities)
                {
                    if (ability != null) total += Mathf.Max(0, ability.investedAbilityPoints);
                }
            }
            return (int)Math.Min(int.MaxValue, total);
        }

        public static HunterRank RankFor(HunterProfile hunter) =>
            RankForInvestedPoints(hunter?.career?.InvestedAbilityPoints ?? 0);

        public static HunterRank RankForInvestedPoints(int investedAbilityPoints)
        {
            return (Mathf.Max(0, investedAbilityPoints) / AbilityPointsPerRankBand) switch
            {
                <= 0 => HunterRank.E,
                1 => HunterRank.D,
                2 => HunterRank.C,
                3 => HunterRank.B,
                4 => HunterRank.A,
                _ => HunterRank.S
            };
        }

        public static long ExperienceThreshold(HunterProfile hunter) =>
            ExperienceThreshold(hunter?.career?.earnedAbilityPoints ?? 0);

        public static long ExperienceThreshold(int earnedAbilityPoints)
        {
            var points = Math.Max(0L, earnedAbilityPoints);
            return Math.Min(int.MaxValue, 30L + points * 15L);
        }

        public static void Normalize(
            HunterProfile hunter,
            IReadOnlyList<EcosystemGearDefinition> gearCatalog)
        {
            if (hunter == null) return;
            hunter.career ??= new HunterCareerState();
            // Source version alone must never overwrite a complete career. This matters for
            // experimental/forward-patched v2 saves that already contain valid v3 career data.
            // Missing or explicitly uninitialized payloads still translate from legacy fields.
            if (!hunter.career.initialized)
            {
                InitializeFromLegacy(hunter, gearCatalog);
            }

            var career = hunter.career;
            career.attributes ??= new List<HunterAttributeInvestment>();
            career.affinities ??= new List<HunterAffinityState>();
            career.learnedAbilities ??= new List<HunterLearnedAbility>();
            career.loadout ??= new HunterAbilityLoadout();
            career.currentExperience = Math.Max(0L, career.currentExperience);
            career.lifetimeExperience = Math.Max(career.currentExperience, career.lifetimeExperience);
            career.earnedAbilityPoints = Mathf.Max(0, career.earnedAbilityPoints);
            career.lastTrainingDay = Math.Max(-1, career.lastTrainingDay);

            NormalizeAttributes(hunter);
            NormalizeAffinities(hunter);
            NormalizeLearnedAbilities(career);
            var invested = career.InvestedAbilityPoints;
            if (career.earnedAbilityPoints < invested)
            {
                career.earnedAbilityPoints = invested;
            }
            ProcessExperienceOverflow(career);
            NormalizeLoadout(career);
            if (EcosystemCareerCatalog.FindAbility(career.plannedAbilityId) == null ||
                IsLearned(career, career.plannedAbilityId))
            {
                career.plannedAbilityId = "";
            }
        }

        public static HunterExperienceResult GrantExperience(HunterProfile hunter, int amount)
        {
            if (hunter == null || !hunter.IsActive || amount <= 0)
            {
                return new HunterExperienceResult(0, 0);
            }
            hunter.career ??= new HunterCareerState();
            if (!hunter.career.initialized)
            {
                Normalize(hunter, null);
            }
            var safeAmount = Math.Max(0, amount);
            hunter.career.currentExperience = Math.Min(
                long.MaxValue - safeAmount,
                hunter.career.currentExperience) + safeAmount;
            hunter.career.lifetimeExperience = Math.Min(
                long.MaxValue - safeAmount,
                hunter.career.lifetimeExperience) + safeAmount;
            var granted = ProcessExperienceOverflow(hunter.career);
            return new HunterExperienceResult(safeAmount, granted);
        }

        public static bool TryInvestAttribute(
            HunterProfile hunter,
            string attributeId,
            int amount,
            out string reason)
        {
            reason = "";
            var definition = EcosystemCareerCatalog.FindAttribute(attributeId);
            if (definition == null) return Fail("The selected attribute does not exist.", out reason);
            if (hunter?.career == null || !hunter.career.initialized)
                return Fail("The hunter's career has not been initialized.", out reason);
            if (amount != 1) return Fail("Attribute investment currently spends exactly one Ability Point.", out reason);
            if (hunter.career.UnspentAbilityPoints < amount)
                return Fail("The hunter does not have enough unspent Ability Points.", out reason);
            var state = hunter.career.attributes.Find(item => item != null && item.attributeId == attributeId);
            if (state == null) return Fail("The hunter is missing that attribute record.", out reason);
            state.investedAbilityPoints += amount;
            hunter.career.plannedAbilityId = "";
            return true;
        }

        public static bool TryLearnAbility(HunterProfile hunter, string abilityId, out string reason)
        {
            reason = "";
            var definition = EcosystemCareerCatalog.FindAbility(abilityId);
            if (definition == null) return Fail("The selected ability does not exist.", out reason);
            if (hunter?.career == null || !hunter.career.initialized)
                return Fail("The hunter's career has not been initialized.", out reason);
            if (IsLearned(hunter.career, abilityId))
                return Fail("The hunter has already learned that ability.", out reason);
            if (hunter.career.InvestedAbilityPoints < definition.requiredInvestedAbilityPoints)
                return Fail($"This ability requires {definition.requiredInvestedAbilityPoints} invested AP.", out reason);
            if (hunter.career.UnspentAbilityPoints < definition.abilityPointCost)
                return Fail($"This ability costs {definition.abilityPointCost} AP.", out reason);

            hunter.career.learnedAbilities.Add(new HunterLearnedAbility
            {
                abilityId = definition.id,
                investedAbilityPoints = definition.abilityPointCost
            });
            // Learned abilities are persisted as a list, so mutation and reload must use
            // the same canonical order. Otherwise a save/reload can change both the JSON
            // snapshot and autonomous loadout candidate evaluation order.
            hunter.career.learnedAbilities.Sort((left, right) =>
                string.CompareOrdinal(left.abilityId, right.abilityId));
            hunter.career.plannedAbilityId = "";
            AutoEquipFirstAvailable(hunter.career, definition);
            return true;
        }

        public static bool TryEquipAbility(
            HunterProfile hunter,
            string abilityId,
            int slotIndex,
            out string reason)
        {
            reason = "";
            var definition = EcosystemCareerCatalog.FindAbility(abilityId);
            if (definition == null) return Fail("The selected ability does not exist.", out reason);
            if (hunter?.career == null || !IsLearned(hunter.career, abilityId))
                return Fail("The hunter has not learned that ability.", out reason);
            NormalizeLoadout(hunter.career);

            if (slotIndex == EcosystemCareerCatalog.UltimateSlotIndex)
            {
                if (definition.kind != HunterAbilityKind.Ultimate)
                    return Fail("Only an ultimate ability can occupy the ultimate slot.", out reason);
                hunter.career.loadout.ultimateAbilityId = abilityId;
                return true;
            }
            if (slotIndex < 0 || slotIndex >= EcosystemCareerCatalog.CooldownSlotCount)
                return Fail("The cooldown slot index is invalid.", out reason);
            if (definition.kind != HunterAbilityKind.Cooldown)
                return Fail("Only a cooldown ability can occupy a cooldown slot.", out reason);

            RemoveFromSlots(hunter.career.loadout.cooldownAbilityIds, abilityId);
            hunter.career.loadout.cooldownAbilityIds[slotIndex] = abilityId;
            return true;
        }

        public static bool TryEquipPassive(
            HunterProfile hunter,
            string abilityId,
            int slotIndex,
            out string reason)
        {
            reason = "";
            var definition = EcosystemCareerCatalog.FindAbility(abilityId);
            if (definition == null || definition.kind != HunterAbilityKind.Passive)
                return Fail("The selected entry is not a passive ability.", out reason);
            if (hunter?.career == null || !IsLearned(hunter.career, abilityId))
                return Fail("The hunter has not learned that passive.", out reason);
            if (slotIndex < 0 || slotIndex >= EcosystemCareerCatalog.PassiveSlotCount)
                return Fail("The passive slot index is invalid.", out reason);
            NormalizeLoadout(hunter.career);
            RemoveFromSlots(hunter.career.loadout.passiveAbilityIds, abilityId);
            hunter.career.loadout.passiveAbilityIds[slotIndex] = abilityId;
            return true;
        }

        public static bool TryPlanAbility(HunterProfile hunter, string abilityId, out string reason)
        {
            reason = "";
            var definition = EcosystemCareerCatalog.FindAbility(abilityId);
            if (definition == null) return Fail("The planned ability does not exist.", out reason);
            if (hunter?.career == null || !hunter.career.initialized)
                return Fail("The hunter's career has not been initialized.", out reason);
            if (IsLearned(hunter.career, abilityId))
                return Fail("The hunter already knows that ability.", out reason);
            if (hunter.career.plannedAbilityId == abilityId)
                return Fail("The hunter is already saving Ability Points for that ability.", out reason);
            hunter.career.plannedAbilityId = abilityId;
            return true;
        }

        public static void OptimizeLoadout(HunterProfile hunter)
        {
            if (hunter?.career == null) return;
            NormalizeLoadout(hunter.career);
            var learned = hunter.career.learnedAbilities
                .Where(item => item != null)
                .Select(item => EcosystemCareerCatalog.FindAbility(item.abilityId))
                .Where(item => item != null)
                .OrderByDescending(item => item.abilityPointCost)
                .ThenBy(item => item.id, StringComparer.Ordinal)
                .ToArray();
            hunter.career.loadout.cooldownAbilityIds = learned
                .Where(item => item.kind == HunterAbilityKind.Cooldown)
                .Take(EcosystemCareerCatalog.CooldownSlotCount)
                .Select(item => item.id)
                .ToList();
            EnsureSlotCount(hunter.career.loadout.cooldownAbilityIds, EcosystemCareerCatalog.CooldownSlotCount);
            hunter.career.loadout.ultimateAbilityId = learned
                .FirstOrDefault(item => item.kind == HunterAbilityKind.Ultimate)?.id ?? "";
            hunter.career.loadout.passiveAbilityIds = learned
                .Where(item => item.kind == HunterAbilityKind.Passive)
                .Take(EcosystemCareerCatalog.PassiveSlotCount)
                .Select(item => item.id)
                .ToList();
            EnsureSlotCount(hunter.career.loadout.passiveAbilityIds, EcosystemCareerCatalog.PassiveSlotCount);
        }

        public static HunterBuildDescriptor InferBuild(
            HunterProfile hunter,
            IReadOnlyList<EcosystemGearDefinition> gearCatalog)
        {
            var scores = Enum.GetValues(typeof(HunterArchetype))
                .Cast<HunterArchetype>()
                .ToDictionary(item => item, _ => 0f);
            if (hunter?.career != null)
            {
                foreach (var affinity in hunter.career.affinities ?? new List<HunterAffinityState>())
                {
                    if (affinity != null) scores[affinity.archetype] += Mathf.Clamp01(affinity.value) * 1.5f;
                }
                foreach (var attribute in hunter.career.attributes ?? new List<HunterAttributeInvestment>())
                {
                    var definition = EcosystemCareerCatalog.FindAttribute(attribute?.attributeId);
                    if (definition == null || attribute == null) continue;
                    scores[definition.primaryArchetype] += attribute.Value * 0.12f + attribute.investedAbilityPoints * 0.8f;
                    scores[definition.secondaryArchetype] += attribute.Value * 0.045f + attribute.investedAbilityPoints * 0.3f;
                }
                foreach (var learned in hunter.career.learnedAbilities ?? new List<HunterLearnedAbility>())
                {
                    var definition = EcosystemCareerCatalog.FindAbility(learned?.abilityId);
                    if (definition == null || learned == null) continue;
                    scores[definition.primaryArchetype] += 2f + learned.investedAbilityPoints;
                    scores[definition.secondaryArchetype] += 0.6f + learned.investedAbilityPoints * 0.3f;
                }
            }

            AddGearScores(scores, FindGear(gearCatalog, hunter?.equippedGearId));
            if (hunter != null)
            {
                scores[HunterArchetype.Fighter] += hunter.courage * 0.8f;
                scores[HunterArchetype.Tank] += (hunter.courage + hunter.loyalty) * 0.45f;
                scores[HunterArchetype.Healer] += hunter.loyalty * 0.75f;
                scores[HunterArchetype.Assassin] += (hunter.ambition + hunter.greed) * 0.4f;
                scores[HunterArchetype.Ranger] += Mathf.Clamp01(hunter.greed) * 0.45f;
                scores[HunterArchetype.Mage] += hunter.ambition * 0.7f;
            }

            var ordered = scores.OrderByDescending(pair => pair.Value)
                .ThenBy(pair => pair.Key)
                .ToArray();
            var primary = ordered[0];
            var secondary = ordered[1];
            var hybrid = secondary.Value >= primary.Value * 0.62f;
            return new HunterBuildDescriptor(
                primary.Key,
                secondary.Key,
                hybrid,
                primary.Value,
                secondary.Value);
        }

        public static float CombatPower(
            HunterProfile hunter,
            IReadOnlyList<EcosystemGearDefinition> gearCatalog)
        {
            if (hunter == null || !hunter.IsActive) return 0f;
            var attributePower = 0f;
            if (hunter.career?.attributes != null)
            {
                foreach (var attribute in hunter.career.attributes)
                {
                    if (attribute != null) attributePower += attribute.Value * 0.7f;
                }
            }
            var gear = FindGear(gearCatalog, hunter.equippedGearId);
            var injuries = 0;
            if (hunter.injuries != null)
            {
                foreach (var injury in hunter.injuries)
                {
                    if (injury != null && !injury.healed) injuries++;
                }
            }
            return Mathf.Max(1f,
                8f + attributePower + (gear?.Power ?? 0) +
                (hunter.career?.InvestedAbilityPoints ?? 0) * 4f +
                ActiveAbilityPower(hunter) -
                hunter.wounds * 4f - injuries * 2f);
        }

        private static float ActiveAbilityPower(HunterProfile hunter)
        {
            var career = hunter?.career;
            if (career?.learnedAbilities == null || career.loadout == null)
            {
                return 0f;
            }

            var contribution = 0f;
            foreach (var learned in career.learnedAbilities)
            {
                var definition = EcosystemCareerCatalog.FindAbility(learned?.abilityId);
                if (definition == null || learned == null) continue;
                var paidPoints = Mathf.Max(1, learned.investedAbilityPoints);

                // Library knowledge has a small baseline value; active slots express most of
                // the ability's combat value. Affinity makes identity matter even at equal cost.
                contribution += paidPoints * 0.5f;
                if (!IsAbilityActive(career, definition.id)) continue;
                var activeMultiplier = definition.kind switch
                {
                    HunterAbilityKind.Ultimate => 2.75f,
                    HunterAbilityKind.Passive => 1.5f,
                    _ => 2f
                };
                contribution += paidPoints * activeMultiplier +
                                AffinityFor(hunter, definition.primaryArchetype) * 0.75f +
                                AffinityFor(hunter, definition.secondaryArchetype) * 0.25f;
            }
            return contribution;
        }

        private static bool IsAbilityActive(HunterCareerState career, string abilityId)
        {
            if (career?.loadout == null || string.IsNullOrEmpty(abilityId)) return false;
            if (career.loadout.ultimateAbilityId == abilityId) return true;
            if (career.loadout.cooldownAbilityIds?.Contains(abilityId) == true) return true;
            return career.loadout.passiveAbilityIds?.Contains(abilityId) == true;
        }

        public static float AffinityFor(HunterProfile hunter, HunterArchetype archetype)
        {
            var affinity = hunter?.career?.affinities?.Find(item => item != null && item.archetype == archetype);
            return affinity == null ? 0.5f : Mathf.Clamp01(affinity.value);
        }

        public static HunterAttributeInvestment FindAttribute(HunterProfile hunter, string attributeId) =>
            hunter?.career?.attributes?.Find(item => item != null && item.attributeId == attributeId);

        public static bool IsLearned(HunterCareerState career, string abilityId) =>
            !string.IsNullOrEmpty(abilityId) &&
            career?.learnedAbilities?.Exists(item => item != null && item.abilityId == abilityId) == true;

        public static List<string> Validate(HunterProfile hunter)
        {
            var errors = new List<string>();
            var career = hunter?.career;
            if (career == null || !career.initialized)
            {
                errors.Add("career is not initialized");
                return errors;
            }
            if (career.currentExperience < 0 ||
                career.currentExperience >= ExperienceThreshold(career.earnedAbilityPoints))
            {
                errors.Add("career XP is outside its current threshold");
            }
            if (career.earnedAbilityPoints < career.InvestedAbilityPoints)
            {
                errors.Add("career has invested more AP than it has earned");
            }

            var attributeIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var attribute in career.attributes ?? new List<HunterAttributeInvestment>())
            {
                if (attribute == null || EcosystemCareerCatalog.FindAttribute(attribute.attributeId) == null ||
                    !attributeIds.Add(attribute.attributeId) || attribute.baseValue <= 0 ||
                    attribute.investedAbilityPoints < 0)
                {
                    errors.Add("career contains an invalid or duplicate attribute allocation");
                    break;
                }
            }
            if (attributeIds.Count != EcosystemCareerCatalog.Attributes.Count)
            {
                errors.Add("career does not contain the complete attribute set");
            }

            var affinityTypes = new HashSet<HunterArchetype>();
            foreach (var affinity in career.affinities ?? new List<HunterAffinityState>())
            {
                if (affinity == null || !affinityTypes.Add(affinity.archetype) ||
                    affinity.value < 0f || affinity.value > 1f)
                {
                    errors.Add("career contains an invalid or duplicate affinity");
                    break;
                }
            }
            if (affinityTypes.Count != Enum.GetValues(typeof(HunterArchetype)).Length)
            {
                errors.Add("career does not contain the complete affinity set");
            }

            var learnedIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var learned in career.learnedAbilities ?? new List<HunterLearnedAbility>())
            {
                if (learned == null || EcosystemCareerCatalog.FindAbility(learned.abilityId) == null ||
                    !learnedIds.Add(learned.abilityId) || learned.investedAbilityPoints <= 0)
                {
                    errors.Add("career contains an invalid or duplicate learned ability");
                    break;
                }
            }

            if (career.loadout == null || career.loadout.cooldownAbilityIds == null ||
                career.loadout.passiveAbilityIds == null ||
                career.loadout.cooldownAbilityIds.Count != EcosystemCareerCatalog.CooldownSlotCount ||
                career.loadout.passiveAbilityIds.Count != EcosystemCareerCatalog.PassiveSlotCount)
            {
                errors.Add("career loadout has invalid slot capacity");
                return errors;
            }

            var equippedIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in career.loadout.cooldownAbilityIds)
            {
                var definition = EcosystemCareerCatalog.FindAbility(id);
                if (!string.IsNullOrEmpty(id) &&
                    (!learnedIds.Contains(id) || definition?.kind != HunterAbilityKind.Cooldown ||
                     !equippedIds.Add(id)))
                {
                    errors.Add("career has an invalid cooldown loadout entry");
                    break;
                }
            }
            if (!string.IsNullOrEmpty(career.loadout.ultimateAbilityId))
            {
                var ultimate = EcosystemCareerCatalog.FindAbility(career.loadout.ultimateAbilityId);
                if (!learnedIds.Contains(career.loadout.ultimateAbilityId) ||
                    ultimate?.kind != HunterAbilityKind.Ultimate)
                {
                    errors.Add("career has an invalid ultimate loadout entry");
                }
            }
            equippedIds.Clear();
            foreach (var id in career.loadout.passiveAbilityIds)
            {
                var definition = EcosystemCareerCatalog.FindAbility(id);
                if (!string.IsNullOrEmpty(id) &&
                    (!learnedIds.Contains(id) || definition?.kind != HunterAbilityKind.Passive ||
                     !equippedIds.Add(id)))
                {
                    errors.Add("career has an invalid passive loadout entry");
                    break;
                }
            }
            return errors;
        }

        private static void InitializeFromLegacy(
            HunterProfile hunter,
            IReadOnlyList<EcosystemGearDefinition> gearCatalog)
        {
            var career = new HunterCareerState
            {
                initialized = true,
                earnedAbilityPoints = Mathf.Max(0, hunter.level - 1),
                lastTrainingDay = -1
            };
            hunter.career = career;
            foreach (var definition in EcosystemCareerCatalog.Attributes)
            {
                var variation = (int)(EcosystemDeterministicRandom.StableHash(
                    $"{hunter.id}|base|{definition.id}") % 5u) - 2;
                career.attributes.Add(new HunterAttributeInvestment
                {
                    attributeId = definition.id,
                    baseValue = Mathf.Max(1, definition.defaultBaseValue + variation)
                });
            }
            foreach (HunterArchetype archetype in Enum.GetValues(typeof(HunterArchetype)))
            {
                var hash = EcosystemDeterministicRandom.StableHash($"{hunter.id}|affinity|{archetype}");
                career.affinities.Add(new HunterAffinityState
                {
                    archetype = archetype,
                    value = 0.25f + hash % 751u / 1000f
                });
            }

            // Legacy levels become fully invested personal growth. Seed one compatible
            // starter verb when points allow, then distribute the remainder into stats.
            // This deterministic generation cannot consume the simulation random cursor.
            var pointsRemaining = career.earnedAbilityPoints;
            var starter = EcosystemCareerCatalog.Abilities
                .Where(item => item.kind == HunterAbilityKind.Cooldown &&
                               item.requiredInvestedAbilityPoints == 0 &&
                               item.abilityPointCost <= pointsRemaining)
                .OrderByDescending(item => StarterAbilityPreference(hunter, item, gearCatalog))
                .ThenBy(item => item.id, StringComparer.Ordinal)
                .FirstOrDefault();
            if (starter != null)
            {
                career.learnedAbilities.Add(new HunterLearnedAbility
                {
                    abilityId = starter.id,
                    investedAbilityPoints = starter.abilityPointCost
                });
                AutoEquipFirstAvailable(career, starter);
                pointsRemaining -= starter.abilityPointCost;
            }
            for (var point = 0; point < pointsRemaining; point++)
            {
                var selected = career.attributes
                    .OrderByDescending(attribute => LegacyAttributePreference(hunter, attribute, gearCatalog))
                    .ThenBy(attribute => attribute.attributeId, StringComparer.Ordinal)
                    .First();
                selected.investedAbilityPoints++;
            }

            var legacyThreshold = Math.Max(1L, Math.Max(1, hunter.level) * 30L);
            var progress = Mathf.Clamp01((float)Math.Max(0, hunter.experience) / legacyThreshold);
            var newThreshold = ExperienceThreshold(career.earnedAbilityPoints);
            career.currentExperience = Math.Min(newThreshold - 1L, (long)Math.Round(progress * newThreshold));
            var priorPoints = (decimal)career.earnedAbilityPoints;
            var estimatedLifetime =
                30m * priorPoints + 15m * priorPoints * (priorPoints - 1m) / 2m +
                career.currentExperience;
            career.lifetimeExperience = estimatedLifetime >= long.MaxValue
                ? long.MaxValue
                : (long)estimatedLifetime;
            NormalizeLoadout(career);
        }

        private static float StarterAbilityPreference(
            HunterProfile hunter,
            HunterAbilityDefinition ability,
            IReadOnlyList<EcosystemGearDefinition> gearCatalog)
        {
            var preference = AffinityFor(hunter, ability.primaryArchetype) +
                             AffinityFor(hunter, ability.secondaryArchetype) * 0.3f;
            var gear = FindGear(gearCatalog, hunter.equippedGearId);
            if (GearArchetype(gear?.TacticalRole ?? TacticalRole.Flexible) ==
                ability.primaryArchetype)
            {
                preference += 0.65f;
            }
            preference += EcosystemDeterministicRandom.StableHash(
                $"{hunter.id}|starter|{ability.id}") % 1001u / 5000f;
            return preference;
        }

        private static float LegacyAttributePreference(
            HunterProfile hunter,
            HunterAttributeInvestment attribute,
            IReadOnlyList<EcosystemGearDefinition> gearCatalog)
        {
            var definition = EcosystemCareerCatalog.FindAttribute(attribute.attributeId);
            var affinity = AffinityFor(hunter, definition.primaryArchetype);
            var gear = FindGear(gearCatalog, hunter.equippedGearId);
            var gearFit = GearArchetype(gear?.TacticalRole ?? TacticalRole.Flexible) ==
                          definition.primaryArchetype ? 0.7f : 0f;
            var inclination = EcosystemDeterministicRandom.StableHash(
                $"{hunter.id}|legacy-invest|{attribute.attributeId}") % 1001u / 1000f;
            return affinity + gearFit + inclination * 0.25f - attribute.investedAbilityPoints * 0.3f;
        }

        private static void NormalizeAttributes(HunterProfile hunter)
        {
            var normalizedById = new Dictionary<string, HunterAttributeInvestment>(StringComparer.Ordinal);
            foreach (var attribute in hunter.career.attributes)
            {
                if (attribute == null || string.IsNullOrWhiteSpace(attribute.attributeId)) continue;
                attribute.baseValue = Mathf.Max(1, attribute.baseValue);
                attribute.investedAbilityPoints = Mathf.Max(0, attribute.investedAbilityPoints);
                if (normalizedById.TryGetValue(attribute.attributeId, out var existing))
                {
                    // Duplicate DTO rows are malformed, but preserving the largest snapshot
                    // prevents normalization from silently deleting legitimate paid progress.
                    existing.baseValue = Mathf.Max(existing.baseValue, attribute.baseValue);
                    existing.investedAbilityPoints = Mathf.Max(
                        existing.investedAbilityPoints,
                        attribute.investedAbilityPoints);
                }
                else
                {
                    normalizedById.Add(attribute.attributeId, attribute);
                }
            }

            var byId = new Dictionary<string, HunterAttributeInvestment>(StringComparer.Ordinal);
            var unresolved = new List<HunterAttributeInvestment>();
            foreach (var pair in normalizedById)
            {
                var attribute = pair.Value;
                if (EcosystemCareerCatalog.FindAttribute(attribute.attributeId) == null)
                {
                    // Never erase paid progression merely because authored content moved.
                    // Validation will surface the unresolved stable ID for an explicit migration.
                    unresolved.Add(attribute);
                    continue;
                }
                byId.Add(attribute.attributeId, attribute);
            }
            hunter.career.attributes.Clear();
            foreach (var definition in EcosystemCareerCatalog.Attributes)
            {
                if (!byId.TryGetValue(definition.id, out var attribute))
                {
                    var variation = (int)(EcosystemDeterministicRandom.StableHash(
                        $"{hunter.id}|base|{definition.id}") % 5u) - 2;
                    attribute = new HunterAttributeInvestment
                    {
                        attributeId = definition.id,
                        baseValue = Mathf.Max(1, definition.defaultBaseValue + variation)
                    };
                }
                hunter.career.attributes.Add(attribute);
            }
            unresolved.Sort((left, right) =>
                string.CompareOrdinal(left.attributeId, right.attributeId));
            hunter.career.attributes.AddRange(unresolved);
        }

        private static void NormalizeAffinities(HunterProfile hunter)
        {
            var byType = new Dictionary<HunterArchetype, HunterAffinityState>();
            foreach (var affinity in hunter.career.affinities)
            {
                if (affinity == null || byType.ContainsKey(affinity.archetype)) continue;
                affinity.value = Mathf.Clamp01(affinity.value);
                byType.Add(affinity.archetype, affinity);
            }
            hunter.career.affinities.Clear();
            foreach (HunterArchetype archetype in Enum.GetValues(typeof(HunterArchetype)))
            {
                if (!byType.TryGetValue(archetype, out var affinity))
                {
                    var hash = EcosystemDeterministicRandom.StableHash($"{hunter.id}|affinity|{archetype}");
                    affinity = new HunterAffinityState
                    {
                        archetype = archetype,
                        value = 0.25f + hash % 751u / 1000f
                    };
                }
                hunter.career.affinities.Add(affinity);
            }
        }

        private static void NormalizeLearnedAbilities(HunterCareerState career)
        {
            var byId = new Dictionary<string, HunterLearnedAbility>(StringComparer.Ordinal);
            foreach (var learned in career.learnedAbilities)
            {
                if (learned == null || string.IsNullOrWhiteSpace(learned.abilityId)) continue;
                if (learned.investedAbilityPoints <= 0)
                {
                    var definition = EcosystemCareerCatalog.FindAbility(learned.abilityId);
                    if (definition != null)
                    {
                        learned.investedAbilityPoints = definition.abilityPointCost;
                    }
                }
                learned.investedAbilityPoints = Mathf.Max(0, learned.investedAbilityPoints);
                if (byId.TryGetValue(learned.abilityId, out var existing))
                {
                    existing.investedAbilityPoints = Mathf.Max(
                        existing.investedAbilityPoints,
                        learned.investedAbilityPoints);
                }
                else
                {
                    byId.Add(learned.abilityId, learned);
                }
            }
            career.learnedAbilities.Clear();
            career.learnedAbilities.AddRange(byId.Values);
            career.learnedAbilities.Sort((left, right) =>
                string.CompareOrdinal(left.abilityId, right.abilityId));
        }

        private static void NormalizeLoadout(HunterCareerState career)
        {
            career.loadout ??= new HunterAbilityLoadout();
            career.loadout.cooldownAbilityIds ??= new List<string>();
            career.loadout.passiveAbilityIds ??= new List<string>();
            EnsureSlotCount(career.loadout.cooldownAbilityIds, EcosystemCareerCatalog.CooldownSlotCount);
            EnsureSlotCount(career.loadout.passiveAbilityIds, EcosystemCareerCatalog.PassiveSlotCount);

            var usedCooldowns = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < career.loadout.cooldownAbilityIds.Count; index++)
            {
                var id = career.loadout.cooldownAbilityIds[index];
                var definition = EcosystemCareerCatalog.FindAbility(id);
                if (!IsLearned(career, id) || definition?.kind != HunterAbilityKind.Cooldown ||
                    !usedCooldowns.Add(id))
                {
                    career.loadout.cooldownAbilityIds[index] = "";
                }
            }
            var ultimate = EcosystemCareerCatalog.FindAbility(career.loadout.ultimateAbilityId);
            if (!IsLearned(career, career.loadout.ultimateAbilityId) ||
                ultimate?.kind != HunterAbilityKind.Ultimate)
            {
                career.loadout.ultimateAbilityId = "";
            }
            var usedPassives = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < career.loadout.passiveAbilityIds.Count; index++)
            {
                var id = career.loadout.passiveAbilityIds[index];
                var definition = EcosystemCareerCatalog.FindAbility(id);
                if (!IsLearned(career, id) || definition?.kind != HunterAbilityKind.Passive ||
                    !usedPassives.Add(id))
                {
                    career.loadout.passiveAbilityIds[index] = "";
                }
            }
        }

        private static int ProcessExperienceOverflow(HunterCareerState career)
        {
            if (career == null)
            {
                return 0;
            }

            career.currentExperience = Math.Max(0L, career.currentExperience);
            career.earnedAbilityPoints = Mathf.Max(0, career.earnedAbilityPoints);
            var maximumGrant = int.MaxValue - career.earnedAbilityPoints;
            if (maximumGrant <= 0)
            {
                career.currentExperience = Math.Min(
                    career.currentExperience,
                    ExperienceThreshold(career.earnedAbilityPoints) - 1L);
                return 0;
            }

            // Binary-search the arithmetic-series cost instead of iterating once per AP.
            // This keeps corrupt or hand-edited saves near long.MaxValue bounded to 31 checks.
            long low = 0;
            long high = maximumGrant;
            while (low < high)
            {
                var middle = low + (high - low + 1L) / 2L;
                if (ExperienceCostForLevels(career.earnedAbilityPoints, middle) <=
                    career.currentExperience)
                {
                    low = middle;
                }
                else
                {
                    high = middle - 1L;
                }
            }

            var awarded = (int)low;
            career.currentExperience -= ExperienceCostForLevels(
                career.earnedAbilityPoints,
                awarded);
            career.earnedAbilityPoints += awarded;
            if (career.earnedAbilityPoints == int.MaxValue)
            {
                career.currentExperience = Math.Min(
                    career.currentExperience,
                    ExperienceThreshold(career.earnedAbilityPoints) - 1L);
            }
            return awarded;
        }

        private static long ExperienceCostForLevels(int startingEarnedPoints, long levelCount)
        {
            if (levelCount <= 0L) return 0L;

            const long baseThreshold = 30L;
            const long thresholdStep = 15L;
            const long maximumThreshold = int.MaxValue;
            var start = Math.Max(0L, startingEarnedPoints);
            var firstCappedPoint =
                (maximumThreshold - baseThreshold + thresholdStep - 1L) / thresholdStep;
            var uncappedCount = Math.Min(
                levelCount,
                Math.Max(0L, firstCappedPoint - start));
            var firstThreshold = baseThreshold + start * thresholdStep;
            var uncappedCost = uncappedCount == 0L
                ? 0L
                : uncappedCount *
                  (2L * firstThreshold + (uncappedCount - 1L) * thresholdStep) / 2L;
            var cappedCost = (levelCount - uncappedCount) * maximumThreshold;
            return uncappedCost + cappedCost;
        }

        private static void AutoEquipFirstAvailable(
            HunterCareerState career,
            HunterAbilityDefinition definition)
        {
            NormalizeLoadout(career);
            if (definition.kind == HunterAbilityKind.Ultimate)
            {
                if (string.IsNullOrEmpty(career.loadout.ultimateAbilityId))
                {
                    career.loadout.ultimateAbilityId = definition.id;
                }
                return;
            }
            var slots = definition.kind == HunterAbilityKind.Passive
                ? career.loadout.passiveAbilityIds
                : career.loadout.cooldownAbilityIds;
            for (var index = 0; index < slots.Count; index++)
            {
                if (string.IsNullOrEmpty(slots[index]))
                {
                    slots[index] = definition.id;
                    return;
                }
            }
        }

        private static void EnsureSlotCount(List<string> slots, int count)
        {
            while (slots.Count < count) slots.Add("");
            if (slots.Count > count) slots.RemoveRange(count, slots.Count - count);
        }

        private static void RemoveFromSlots(List<string> slots, string id)
        {
            for (var index = 0; index < slots.Count; index++)
            {
                if (slots[index] == id) slots[index] = "";
            }
        }

        private static EcosystemGearDefinition FindGear(
            IReadOnlyList<EcosystemGearDefinition> gearCatalog,
            string gearId)
        {
            if (gearCatalog == null || string.IsNullOrEmpty(gearId)) return null;
            for (var index = 0; index < gearCatalog.Count; index++)
            {
                if (gearCatalog[index] != null && gearCatalog[index].GearId == gearId)
                {
                    return gearCatalog[index];
                }
            }
            return null;
        }

        private static void AddGearScores(
            IDictionary<HunterArchetype, float> scores,
            EcosystemGearDefinition gear)
        {
            if (gear == null) return;
            var primary = GearArchetype(gear.TacticalRole);
            scores[primary] += 2.2f;
            var secondary = gear.TacticalRole switch
            {
                TacticalRole.Vanguard => HunterArchetype.Fighter,
                TacticalRole.Bruiser => HunterArchetype.Tank,
                TacticalRole.Skirmisher => HunterArchetype.Ranger,
                TacticalRole.Controller => HunterArchetype.Tank,
                TacticalRole.Support => HunterArchetype.Mage,
                _ => HunterArchetype.Fighter
            };
            scores[secondary] += 0.8f;
        }

        private static HunterArchetype GearArchetype(TacticalRole role)
        {
            return role switch
            {
                TacticalRole.Vanguard => HunterArchetype.Tank,
                TacticalRole.Bruiser => HunterArchetype.Fighter,
                TacticalRole.Skirmisher => HunterArchetype.Assassin,
                TacticalRole.Controller => HunterArchetype.Mage,
                TacticalRole.Support => HunterArchetype.Healer,
                _ => HunterArchetype.Fighter
            };
        }

        private static bool Fail(string message, out string reason)
        {
            reason = message;
            return false;
        }

    }
}
