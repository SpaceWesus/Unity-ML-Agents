using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Turtle.Ecosystem
{
    /// <summary>
    /// Creates the deterministic campaign baseline and repairs older saves without
    /// replacing their existing hunter records.
    /// </summary>
    public static class EcosystemWorldFactory
    {
        public const int CurrentSaveVersion = 5;
        public const int DefaultWorldSeed = 24071996;
        public const int StartingHunterCount = 80;
        public const int MinimumActiveHunterCount = 60;
        public const int MaximumActiveHunterCount = 100;
        public const int RequiredGuildCount = 5;

        private const string DefaultPlayerId = "hunter-player";
        private const string DefaultLocationId = "town-frontier";

        private static readonly string[] FallbackGearIds =
        {
            "gear-vanguard",
            "gear-titan",
            "gear-rift"
        };

        private static readonly string[] GeneratedFirstNames =
        {
            "Ari", "Bryn", "Cass", "Dara", "Elias", "Faye", "Galen", "Hana",
            "Iris", "Joren", "Kaia", "Leif", "Mina", "Nolan", "Orla", "Perrin",
            "Quinn", "Rhea", "Soren", "Talia", "Ulric", "Vera", "Wren", "Xara",
            "Yorin", "Zella", "Ansel", "Briar", "Cyrus", "Delia", "Evren", "Freya",
            "Gideon", "Hollis", "Isolde", "Jace", "Kira", "Lucan", "Maeve", "Niko"
        };

        private static readonly string[] GeneratedSurnames =
        {
            "Alder", "Bishop", "Crowe", "Dusk", "Ember", "Frost", "Graves", "Hale",
            "Ivory", "Jade", "Knox", "Lark", "Morrow", "North", "Orchid", "Pike",
            "Quill", "Rook", "Stone", "Thorn", "Umber", "Vale", "Ward", "Young",
            "Zephyr", "Ashfall", "Blackwell", "Calder", "Dawn", "Everhart", "Fen", "Grove",
            "Harrow", "Irons", "Juniper", "Kestrel", "Locke", "Moon", "Nyx", "Oath"
        };

        private static readonly string[] GeneratedGoals =
        {
            "Found a guild with a reputation for bringing everyone home",
            "Earn enough to retire their family from dangerous work",
            "Become the Association's most trusted strike-team leader",
            "Master a hybrid build no instructor believes can work",
            "Discover what happened to a hunter who vanished inside a gate",
            "Build a fortune by controlling the regional mana trade",
            "Protect low-rank hunters from exploitative guilds",
            "Rise far beyond the rank assigned at awakening",
            "Collect and preserve rare gear move sets",
            "Map every unstable gate in the frontier",
            "Prove they can survive without owing loyalty to any guild",
            "Turn a bitter guild rivalry into lasting peace"
        };

        private static readonly string[] GeneratedGuildIds =
        {
            "guild-azure",
            "guild-crimson",
            "guild-verdant",
            "guild-ivory",
            "guild-umbra"
        };

        private static readonly string[] GeneratedTownIds =
        {
            "town-frontier",
            "town-river",
            "town-ember"
        };

        private readonly struct HunterTemplate
        {
            public HunterTemplate(
                string id,
                string displayName,
                int level,
                float courage,
                float ambition,
                float loyalty,
                float greed,
                string goal,
                string guildId,
                int gearIndex,
                string locationId,
                int gold,
                float fame)
            {
                Id = id;
                DisplayName = displayName;
                Level = level;
                Courage = courage;
                Ambition = ambition;
                Loyalty = loyalty;
                Greed = greed;
                Goal = goal;
                GuildId = guildId;
                GearIndex = gearIndex;
                LocationId = locationId;
                Gold = gold;
                Fame = fame;
            }

            public string Id { get; }
            public string DisplayName { get; }
            public int Level { get; }
            public float Courage { get; }
            public float Ambition { get; }
            public float Loyalty { get; }
            public float Greed { get; }
            public string Goal { get; }
            public string GuildId { get; }
            public int GearIndex { get; }
            public string LocationId { get; }
            public int Gold { get; }
            public float Fame { get; }
        }

        private static readonly HunterTemplate[] HunterTemplates =
        {
            new("hunter-player", "Rowan Vale", 2, 0.72f, 0.68f, 0.61f, 0.35f,
                "Found a guild that outlives them", "guild-azure", 0, "town-frontier", 94, 4f),
            new("hunter-mara", "Mara Quill", 3, 0.83f, 0.77f, 0.42f, 0.51f,
                "Become the most feared gatebreaker", "guild-crimson", 1, "town-ember", 128, 11f),
            new("hunter-voss", "Voss Calder", 4, 0.66f, 0.88f, 0.73f, 0.29f,
                "Expand Crimson territory", "guild-crimson", 0, "town-ember", 156, 16f),
            new("hunter-iona", "Iona Reed", 2, 0.48f, 0.55f, 0.91f, 0.22f,
                "Find a guild worthy of loyalty", "", 0, "town-frontier", 63, 3f),
            new("hunter-kest", "Kest Ardyn", 3, 0.58f, 0.92f, 0.34f, 0.82f,
                "Acquire a legendary relic", "", 2, "town-river", 117, 8f),
            new("hunter-brann", "Brann Oath", 2, 0.91f, 0.44f, 0.79f, 0.18f,
                "Protect weaker hunters", "", 1, "town-frontier", 51, 5f),
            new("hunter-sable", "Sable Nyx", 4, 0.69f, 0.81f, 0.28f, 0.75f,
                "Never be controlled again", "", 2, "town-river", 142, 14f),
            new("hunter-tarin", "Tarin Moss", 1, 0.39f, 0.63f, 0.84f, 0.41f,
                "Survive long enough to become renowned", "", 0, "town-frontier", 37, 1f),
            new("hunter-lyra", "Lyra Fen", 3, 0.62f, 0.71f, 0.67f, 0.38f,
                "Map every unstable gate", "guild-azure", 2, "town-frontier", 101, 9f),
            new("hunter-dain", "Dain Rook", 5, 0.88f, 0.74f, 0.47f, 0.45f,
                "Win a war no rival can forget", "guild-crimson", 1, "town-ember", 184, 22f),
            new("hunter-neri", "Neri Sol", 2, 0.43f, 0.59f, 0.86f, 0.31f,
                "Make dangerous roads safe", "guild-verdant", 0, "town-river", 72, 4f),
            new("hunter-orren", "Orren Pike", 4, 0.78f, 0.52f, 0.82f, 0.24f,
                "Build an unbreakable company", "guild-verdant", 1, "town-river", 149, 15f),
            new("hunter-mira", "Mira Ash", 2, 0.57f, 0.76f, 0.64f, 0.46f,
                "Earn command through action", "guild-azure", 0, "town-frontier", 81, 6f),
            new("hunter-cael", "Cael Wren", 3, 0.51f, 0.85f, 0.36f, 0.69f,
                "Control the frontier mana trade", "", 2, "resource-mana-well", 133, 10f),
            new("hunter-thorne", "Thorne Beck", 4, 0.86f, 0.61f, 0.58f, 0.33f,
                "Hold Sunstone Ridge at any cost", "guild-crimson", 1, "resource-sunstone", 161, 17f),
            new("hunter-elian", "Elian Vey", 3, 0.46f, 0.73f, 0.89f, 0.27f,
                "Unite the river settlements", "guild-verdant", 2, "resource-ironwood", 96, 8f)
        };

        public static EcosystemWorldState CreateDefaultWorld(
            IReadOnlyList<EcosystemGearDefinition> gearCatalog = null,
            int worldSeed = DefaultWorldSeed)
        {
            var state = new EcosystemWorldState
            {
                saveVersion = CurrentSaveVersion,
                day = 1,
                playerHunterId = DefaultPlayerId,
                worldSeed = worldSeed == 0 ? DefaultWorldSeed : worldSeed
            };

            EnsureCollections(state);
            AddDefaultGuilds(state);
            AddDefaultLocations(state);
            AddDefaultRoutes(state);
            AddDefaultMissions(state);
            foreach (var template in HunterTemplates)
            {
                state.hunters.Add(CreateHunter(template, gearCatalog));
            }
            AddHuntersUntilRequiredCount(state, gearCatalog);
            AddDefaultContracts(state);
            AddDefaultParties(state);
            AddDefaultInvitations(state);
            AddDefaultRelationships(state);
            AddInitialWorldEvent(state);
            state.eventLog.Add("DAY 1: Five guilds began contesting the frontier.");
            state.eventLog.Add($"DAY 1: {StartingHunterCount} persistent hunters entered the simulation.");
            EnsurePlayerPrototypeGear(state, gearCatalog);
            return UpgradeAndNormalize(state, gearCatalog, state.worldSeed);
        }

        public static EcosystemWorldState UpgradeAndNormalize(
            EcosystemWorldState state,
            IReadOnlyList<EcosystemGearDefinition> gearCatalog = null,
            int fallbackWorldSeed = DefaultWorldSeed)
        {
            state ??= new EcosystemWorldState();
            var preservedSimulationSequence = state.simulationSequence;
            var sourceVersion = state.saveVersion;
            // Version one predated persistent life state and required broad prototype repair.
            // Later schema upgrades must never reuse that path: doing so would resurrect dead
            // version-two hunters and erase legitimate campaign history.
            var isVersionOneImport = sourceVersion < 2;
            var isSeriousEcosystemUpgrade = sourceVersion < CurrentSaveVersion;
            EnsureCollections(state);
            state.day = Mathf.Max(1, state.day);
            state.worldSeed = state.worldSeed == 0
                ? fallbackWorldSeed == 0 ? DefaultWorldSeed : fallbackWorldSeed
                : state.worldSeed;

            NormalizeEntityIds(state);
            if (isSeriousEcosystemUpgrade)
            {
                AddDefaultGuilds(state);
                AddDefaultLocations(state);
                AddDefaultRoutes(state);
                AddDefaultMissions(state);
                AddHuntersUntilRequiredCount(state, gearCatalog);
                AddDefaultContracts(state);
                AddDefaultParties(state);
                AddDefaultInvitations(state);
            }
            NormalizeHunters(state, gearCatalog, isVersionOneImport);
            if (string.IsNullOrWhiteSpace(state.playerHunterId) ||
                FindHunter(state, state.playerHunterId) == null)
            {
                state.playerHunterId = FindHunter(state, DefaultPlayerId)?.id ??
                                       (state.hunters.Count > 0 ? state.hunters[0].id : "");
            }
            if (isSeriousEcosystemUpgrade)
            {
                EnsurePlayerPrototypeGear(state, gearCatalog);
            }
            NormalizeGuildMemberships(state);
            NormalizeLocationControl(state);
            NormalizeDiplomacy(state);
            NormalizeParties(state);
            NormalizeContracts(state);
            if (isVersionOneImport)
            {
                EnsureActiveExpiringContract(state);
            }
            NormalizeGateAndEncounterState(state, gearCatalog);
            NormalizeInvitations(state);
            NormalizeRoutes(state);
            NormalizeDecisionHistory(state);
            NormalizeStructuredEvents(state);
            if (isVersionOneImport)
            {
                AddDefaultRelationships(state);
                AddInitialWorldEvent(state);
            }

            state.populationSequence = Mathf.Max(0, state.populationSequence);
            state.saveVersion = CurrentSaveVersion;
            // Loading and schema repair must not consume deterministic simulation entropy.
            // Only simulation/actions are allowed to advance this cursor.
            state.simulationSequence = preservedSimulationSequence;
            return state;
        }

        public static List<string> ValidateInvariants(
            EcosystemWorldState state,
            IReadOnlyList<EcosystemGearDefinition> gearCatalog = null)
        {
            var errors = new List<string>();
            if (state == null)
            {
                errors.Add("World state is null.");
                return errors;
            }

            if (state.saveVersion < CurrentSaveVersion)
            {
                errors.Add($"Save version {state.saveVersion} has not been upgraded to {CurrentSaveVersion}.");
            }
            if (state.worldSeed == 0)
            {
                errors.Add("World seed must be non-zero.");
            }
            if (state.hunters == null || state.hunters.Count < StartingHunterCount)
            {
                errors.Add($"World requires at least {StartingHunterCount} persistent hunter records.");
            }
            if (state.guilds == null || state.guilds.Count < RequiredGuildCount)
            {
                errors.Add($"World requires at least {RequiredGuildCount} guilds.");
            }
            if (state.missions == null || state.missions.Count < 3)
            {
                errors.Add("World requires at least three mission templates.");
            }
            if (state.map == null || state.map.locations == null)
            {
                errors.Add("World map and locations are required.");
                return errors;
            }
            if (state.hunters == null || state.guilds == null || state.missions == null ||
                state.map.routes == null || state.contracts == null || state.parties == null ||
                state.invitations == null || state.gates == null || state.encounters == null ||
                state.decisionRecords == null || state.structuredEvents == null)
            {
                errors.Add("One or more required world-state collections are null.");
                return errors;
            }

            ValidateUniqueIds(state.hunters, hunter => hunter?.id, "hunter", errors);
            ValidateUniqueIds(state.guilds, guild => guild?.id, "guild", errors);
            ValidateUniqueIds(state.map.locations, location => location?.id, "location", errors);
            ValidateUniqueIds(state.contracts, contract => contract?.id, "contract", errors);
            ValidateUniqueIds(state.parties, party => party?.id, "party", errors);
            ValidateUniqueIds(state.invitations, invitation => invitation?.id, "invitation", errors);
            ValidateUniqueIds(state.gates, gate => gate?.id, "gate", errors);
            ValidateUniqueIds(state.encounters, encounter => encounter?.id, "encounter", errors);
            ValidateUniqueIds(state.decisionRecords, decision => decision?.decisionId, "decision", errors);
            ValidateUniqueIds(state.structuredEvents, worldEvent => worldEvent?.id, "world event", errors);

            var hunterIds = CollectIds(state.hunters, hunter => hunter.id);
            var guildIds = CollectIds(state.guilds, guild => guild.id);
            var locationIds = CollectIds(state.map.locations, location => location.id);
            var contractIds = CollectIds(state.contracts, contract => contract.id);
            var partyIds = CollectIds(state.parties, party => party.id);
            var gateIds = CollectIds(state.gates, gate => gate.id);
            var encounterIds = CollectIds(state.encounters, encounter => encounter.id);
            var activeHunterCount = state.hunters.Count(hunter => hunter != null && hunter.IsActive);
            if (activeHunterCount < MinimumActiveHunterCount ||
                activeHunterCount > MaximumActiveHunterCount)
            {
                errors.Add($"Active hunter population {activeHunterCount} is outside the " +
                           $"{MinimumActiveHunterCount}-{MaximumActiveHunterCount} serious-slice range.");
            }

            if (!hunterIds.Contains(state.playerHunterId))
            {
                errors.Add($"Player hunter '{state.playerHunterId}' does not exist.");
            }

            var locationTypes = new HashSet<LocationType>();
            foreach (var location in state.map.locations)
            {
                if (location == null)
                {
                    continue;
                }
                locationTypes.Add(location.locationType);
                if (!string.IsNullOrEmpty(location.controllingGuildId) &&
                    !guildIds.Contains(location.controllingGuildId))
                {
                    errors.Add($"Location '{location.id}' references missing guild '{location.controllingGuildId}'.");
                }
            }
            foreach (LocationType locationType in Enum.GetValues(typeof(LocationType)))
            {
                if (!locationTypes.Contains(locationType))
                {
                    errors.Add($"World has no {locationType} location.");
                }
            }

            foreach (var hunter in state.hunters)
            {
                if (hunter == null)
                {
                    errors.Add("Hunter list contains a null entry.");
                    continue;
                }
                if (!string.IsNullOrEmpty(hunter.guildId) && !guildIds.Contains(hunter.guildId))
                {
                    errors.Add($"Hunter '{hunter.id}' references missing guild '{hunter.guildId}'.");
                }
                if (!string.IsNullOrEmpty(hunter.locationId) && !locationIds.Contains(hunter.locationId))
                {
                    errors.Add($"Hunter '{hunter.id}' references missing location '{hunter.locationId}'.");
                }
                if (hunter.travelDaysRemaining > 0 &&
                    (!locationIds.Contains(hunter.destinationId) || hunter.destinationId == hunter.locationId))
                {
                    errors.Add($"Hunter '{hunter.id}' has an invalid journey destination.");
                }
                if (!string.IsNullOrEmpty(hunter.partyId) && !partyIds.Contains(hunter.partyId))
                {
                    errors.Add($"Hunter '{hunter.id}' references missing party '{hunter.partyId}'.");
                }
                if (!string.IsNullOrEmpty(hunter.activeContractId) &&
                    !contractIds.Contains(hunter.activeContractId))
                {
                    errors.Add($"Hunter '{hunter.id}' references missing contract '{hunter.activeContractId}'.");
                }
                if (!hunter.isAlive && hunter.deathDay < 0)
                {
                    errors.Add($"Dead hunter '{hunter.id}' has no death day.");
                }
                if (hunter.awakeningDay > state.day)
                {
                    errors.Add($"Hunter '{hunter.id}' awakens in the future.");
                }
                if (hunter.isRetired && (!hunter.isAlive || hunter.retirementDay < hunter.awakeningDay ||
                                         hunter.retirementDay > state.day))
                {
                    errors.Add($"Retired hunter '{hunter.id}' has an impossible life state.");
                }
                if (!string.IsNullOrEmpty(hunter.equippedGearId) &&
                    (hunter.inventoryGearIds == null || !hunter.inventoryGearIds.Contains(hunter.equippedGearId)))
                {
                    errors.Add($"Hunter '{hunter.id}' does not own equipped gear '{hunter.equippedGearId}'.");
                }
                if (hunter.vitals == null || !hunter.vitals.initialized)
                {
                    errors.Add($"Hunter '{hunter.id}' has uninitialized campaign vitals.");
                }
                else
                {
                    if (hunter.vitals.maximumHealth <= 0 || hunter.vitals.maximumMana <= 0 ||
                        hunter.vitals.maximumShield <= 0)
                    {
                        errors.Add($"Hunter '{hunter.id}' has an invalid vital-resource maximum.");
                    }
                    if (hunter.vitals.currentHealth < 0 ||
                        hunter.vitals.currentHealth > hunter.vitals.maximumHealth ||
                        hunter.vitals.currentMana < 0 ||
                        hunter.vitals.currentMana > hunter.vitals.maximumMana ||
                        hunter.vitals.currentShield < 0 ||
                        hunter.vitals.currentShield > hunter.vitals.maximumShield)
                    {
                        errors.Add($"Hunter '{hunter.id}' has an out-of-range campaign resource.");
                    }
                    if (hunter.isAlive && !hunter.isIncapacitated &&
                        hunter.vitals.currentHealth <= 0)
                    {
                        errors.Add($"Living hunter '{hunter.id}' has no campaign health.");
                    }
                    if (hunter.isIncapacitated &&
                        (!hunter.isAlive || hunter.vitals.currentHealth != 0 ||
                         string.IsNullOrEmpty(hunter.currentEncounterId)))
                    {
                        errors.Add($"Hunter '{hunter.id}' has an inconsistent incapacitated state.");
                    }
                    if (!hunter.isAlive &&
                        (hunter.vitals.currentHealth != 0 || hunter.vitals.currentMana != 0 ||
                         hunter.vitals.currentShield != 0))
                    {
                        errors.Add($"Dead hunter '{hunter.id}' retains campaign resources.");
                    }
                }
                ValidateHunterRelationships(hunter, hunterIds, errors);
                foreach (var careerError in EcosystemCareerRules.Validate(hunter))
                {
                    errors.Add($"Hunter '{hunter.id}' {careerError}.");
                }
            }

            foreach (var guild in state.guilds)
            {
                if (guild == null)
                {
                    errors.Add("Guild list contains a null entry.");
                    continue;
                }
                if (!string.IsNullOrEmpty(guild.homeLocationId) && !locationIds.Contains(guild.homeLocationId))
                {
                    errors.Add($"Guild '{guild.id}' references missing home '{guild.homeLocationId}'.");
                }
                ValidateReferences(guild.memberIds, hunterIds, $"Guild '{guild.id}' member", errors);
                ValidateReferences(guild.controlledLocationIds, locationIds, $"Guild '{guild.id}' location", errors);
                foreach (var memberId in guild.memberIds)
                {
                    var member = FindHunter(state, memberId);
                    if (member?.guildId != guild.id)
                    {
                        errors.Add($"Guild '{guild.id}' membership disagrees with hunter '{memberId}'.");
                    }
                    else if (!member.IsActive)
                    {
                        errors.Add($"Guild '{guild.id}' retains inactive hunter '{memberId}'.");
                    }
                }
            }

            foreach (var party in state.parties)
            {
                if (party == null)
                {
                    continue;
                }
                if (party.status == PartyStatus.Disbanded)
                {
                    if (party.memberIds.Count > 0 || !string.IsNullOrEmpty(party.activeContractId))
                    {
                        errors.Add($"Disbanded party '{party.id}' still owns members or a contract.");
                    }
                    continue;
                }
                if (!hunterIds.Contains(party.leaderHunterId))
                {
                    errors.Add($"Party '{party.id}' has missing leader '{party.leaderHunterId}'.");
                }
                ValidateReferences(party.memberIds, hunterIds, $"Party '{party.id}' member", errors);
                if (!party.memberIds.Contains(party.leaderHunterId))
                {
                    errors.Add($"Party '{party.id}' does not contain its leader.");
                }
                if (party.status == PartyStatus.Travelling &&
                    (party.travelDaysRemaining <= 0 || !locationIds.Contains(party.destinationId) ||
                     party.destinationId == party.locationId))
                {
                    errors.Add($"Travelling party '{party.id}' has an invalid journey state.");
                }
                foreach (var memberId in party.memberIds)
                {
                    var member = FindHunter(state, memberId);
                    if (member?.partyId != party.id)
                    {
                        errors.Add($"Party '{party.id}' disagrees with member '{memberId}'.");
                    }
                    else if (!member.IsActive)
                    {
                        errors.Add($"Party '{party.id}' retains inactive hunter '{memberId}'.");
                    }
                }
                if (!string.IsNullOrEmpty(party.activeContractId))
                {
                    var partyContract = FindContract(state, party.activeContractId);
                    if (partyContract == null ||
                        partyContract.status is not (ContractStatus.Accepted or ContractStatus.Active) ||
                        partyContract.acceptedPartyId != party.id)
                    {
                        errors.Add($"Party '{party.id}' has an inconsistent active contract link.");
                    }
                }
            }

            var hasActiveExpiringContract = false;
            var concurrentContractCount = 0;
            foreach (var contract in state.contracts)
            {
                if (contract == null)
                {
                    continue;
                }
                if ((contract.status is ContractStatus.Offered or ContractStatus.Accepted or
                     ContractStatus.Active) && contract.expiresDay >= state.day)
                {
                    hasActiveExpiringContract = true;
                    concurrentContractCount++;
                }
                if (!locationIds.Contains(contract.locationId))
                {
                    errors.Add($"Contract '{contract.id}' references missing location '{contract.locationId}'.");
                }
                if (!string.IsNullOrEmpty(contract.acceptedPartyId) && !partyIds.Contains(contract.acceptedPartyId))
                {
                    errors.Add($"Contract '{contract.id}' references missing party '{contract.acceptedPartyId}'.");
                }
                if (!string.IsNullOrEmpty(contract.acceptedHunterId) &&
                    !hunterIds.Contains(contract.acceptedHunterId))
                {
                    errors.Add($"Contract '{contract.id}' references missing hunter '{contract.acceptedHunterId}'.");
                }
                if (contract.status is ContractStatus.Accepted or ContractStatus.Active)
                {
                    var acceptedParty = FindParty(state, contract.acceptedPartyId);
                    if (acceptedParty == null || acceptedParty.activeContractId != contract.id)
                    {
                        errors.Add($"Contract '{contract.id}' is in progress without its assigned party.");
                        continue;
                    }
                    if (string.IsNullOrEmpty(contract.acceptedHunterId) ||
                        !acceptedParty.memberIds.Contains(contract.acceptedHunterId))
                    {
                        errors.Add($"Contract '{contract.id}' has no accepted hunter in its assigned party.");
                    }
                    foreach (var memberId in acceptedParty.memberIds)
                    {
                        if (FindHunter(state, memberId)?.activeContractId != contract.id)
                        {
                            errors.Add($"Contract '{contract.id}' disagrees with party member '{memberId}'.");
                        }
                    }
                    if (contract.status == ContractStatus.Active && contract.startedDay < contract.offeredDay)
                    {
                        errors.Add($"Active contract '{contract.id}' has not entered its lifecycle.");
                    }
                    if (contract.resolvedDay >= 0)
                    {
                        errors.Add($"In-progress contract '{contract.id}' is already marked resolved.");
                    }
                }
                if ((contract.status is ContractStatus.Offered or ContractStatus.Accepted or
                     ContractStatus.Active) && !gateIds.Contains(contract.gateId))
                {
                    errors.Add($"Current contract '{contract.id}' has no persisted gate manifest.");
                }
                if (!string.IsNullOrEmpty(contract.activeEncounterId) &&
                    !encounterIds.Contains(contract.activeEncounterId))
                {
                    errors.Add($"Contract '{contract.id}' references missing encounter " +
                               $"'{contract.activeEncounterId}'.");
                }
            }

            foreach (var gate in state.gates)
            {
                if (gate == null) continue;
                var linkedContract = state.contracts.Find(contract =>
                    contract != null && contract.gateId == gate.id);
                if (linkedContract != null)
                {
                    var expectedLifecycle = EcosystemGateGenerator
                        .LifecycleForContractStatus(linkedContract.status);
                    if (gate.lifecycle != expectedLifecycle)
                    {
                        errors.Add($"Gate '{gate.id}' lifecycle '{gate.lifecycle}' disagrees " +
                                   $"with contract '{linkedContract.id}' status " +
                                   $"'{linkedContract.status}'.");
                    }
                }
                if (gate.areas == null || gate.areas.Count < 3)
                {
                    errors.Add($"Gate '{gate.id}' has no playable generated layout.");
                }
                if (gate.mobPods == null || gate.monsters == null || gate.lootNodes == null ||
                    gate.resourceNodes == null || gate.hazards == null)
                {
                    errors.Add($"Gate '{gate.id}' has incomplete generated content collections.");
                }
                if (!string.IsNullOrEmpty(gate.activeEncounterId) &&
                    !encounterIds.Contains(gate.activeEncounterId))
                {
                    errors.Add($"Gate '{gate.id}' references missing encounter " +
                               $"'{gate.activeEncounterId}'.");
                }
            }

            foreach (var encounter in state.encounters)
            {
                if (encounter == null) continue;
                if (!gateIds.Contains(encounter.gateId))
                {
                    errors.Add($"Encounter '{encounter.id}' references missing gate '{encounter.gateId}'.");
                }
                if (!contractIds.Contains(encounter.contractId))
                {
                    errors.Add($"Encounter '{encounter.id}' references missing contract " +
                               $"'{encounter.contractId}'.");
                }
                if (!partyIds.Contains(encounter.partyId))
                {
                    errors.Add($"Encounter '{encounter.id}' references missing party " +
                               $"'{encounter.partyId}'.");
                }
                if (encounter.areas == null || encounter.participants == null ||
                    encounter.mobPods == null || encounter.lootNodes == null ||
                    encounter.resourceNodes == null || encounter.hazards == null)
                {
                    errors.Add($"Encounter '{encounter.id}' has incomplete materialization state.");
                }
            }
            if (!hasActiveExpiringContract)
            {
                errors.Add("World requires at least one available, unexpired contract.");
            }
            if (concurrentContractCount < 5 || concurrentContractCount > 15)
            {
                errors.Add($"Concurrent contract count {concurrentContractCount} is outside the 5-15 target.");
            }

            if (gearCatalog != null)
            {
                var catalogIds = new HashSet<string>();
                foreach (var gear in gearCatalog)
                {
                    if (gear != null && !string.IsNullOrWhiteSpace(gear.GearId))
                    {
                        catalogIds.Add(gear.GearId);
                    }
                }
                if (catalogIds.Count > 0)
                {
                    foreach (var hunter in state.hunters)
                    {
                        if (!string.IsNullOrEmpty(hunter.equippedGearId) &&
                            !catalogIds.Contains(hunter.equippedGearId))
                        {
                            errors.Add($"Hunter '{hunter.id}' equips gear absent from the supplied catalog.");
                        }
                    }
                }
            }

            return errors;
        }

        private static void EnsureCollections(EcosystemWorldState state)
        {
            state.hunters ??= new List<HunterProfile>();
            state.guilds ??= new List<GuildState>();
            state.missions ??= new List<MissionState>();
            state.eventLog ??= new List<string>();
            state.map ??= new EcosystemMapState();
            state.map.locations ??= new List<LocationState>();
            state.map.routes ??= new List<WorldRouteState>();
            state.contracts ??= new List<ContractState>();
            state.parties ??= new List<PartyState>();
            state.invitations ??= new List<InvitationState>();
            state.gates ??= new List<GateInstanceState>();
            state.encounters ??= new List<DungeonEncounterState>();
            state.decisionRecords ??= new List<HunterDecisionRecord>();
            state.structuredEvents ??= new List<StructuredWorldEvent>();
        }

        private static void AddDefaultGuilds(EcosystemWorldState state)
        {
            EnsureGuild(state, "guild-azure", "Azure Wake", 80, 1, 12f,
                "town-frontier", new[] { "town-frontier", "resource-mana-well" });
            EnsureGuild(state, "guild-crimson", "Crimson Compact", 105, 2, 17f,
                "town-ember", new[] { "town-ember", "resource-sunstone" });
            EnsureGuild(state, "guild-verdant", "Verdant Covenant", 72, 1, 10f,
                "town-river", new[] { "town-river", "resource-ironwood" });
            EnsureGuild(state, "guild-ivory", "Ivory Bastion", 88, 1, 13f,
                "town-frontier", new[] { "hospital-frontier" });
            EnsureGuild(state, "guild-umbra", "Umbral Exchange", 96, 1, 15f,
                "town-ember", new[] { "market-ember" });
            EnsureDiplomacySeed(state, "guild-azure", "guild-crimson",
                DiplomaticStance.Rival, -0.28f, 0.34f);
            EnsureDiplomacySeed(state, "guild-crimson", "guild-azure",
                DiplomaticStance.Rival, -0.31f, 0.38f);
            EnsureDiplomacySeed(state, "guild-azure", "guild-verdant",
                DiplomaticStance.Cooperative, 0.24f, 0.04f);
            EnsureDiplomacySeed(state, "guild-verdant", "guild-azure",
                DiplomaticStance.Cooperative, 0.27f, 0.02f);
            EnsureDiplomacySeed(state, "guild-ivory", "guild-crimson",
                DiplomaticStance.Rival, -0.18f, 0.2f);
            EnsureDiplomacySeed(state, "guild-umbra", "guild-verdant",
                DiplomaticStance.Cooperative, 0.16f, 0.06f);
        }

        private static void EnsureDiplomacySeed(
            EcosystemWorldState state,
            string ownerGuildId,
            string otherGuildId,
            DiplomaticStance stance,
            float regard,
            float grievance)
        {
            var owner = FindGuild(state, ownerGuildId);
            if (owner == null) return;
            owner.diplomacy ??= new List<GuildDiplomacyState>();
            if (owner.diplomacy.Exists(item => item != null && item.guildId == otherGuildId)) return;
            owner.diplomacy.Add(new GuildDiplomacyState
            {
                guildId = otherGuildId,
                stance = stance,
                regard = regard,
                grievance = grievance,
                sinceDay = state.day
            });
        }

        private static void EnsureGuild(
            EcosystemWorldState state,
            string id,
            string displayName,
            int resources,
            int territory,
            float prestige,
            string homeLocationId,
            string[] controlledLocations)
        {
            if (FindGuild(state, id) != null)
            {
                return;
            }
            state.guilds.Add(new GuildState
            {
                id = id,
                displayName = displayName,
                resources = resources,
                territory = territory,
                prestige = prestige,
                homeLocationId = homeLocationId,
                controlledLocationIds = new List<string>(controlledLocations)
            });
        }

        private static void AddDefaultLocations(EcosystemWorldState state)
        {
            EnsureLocation(state, "town-frontier", "Frontier District", LocationType.Town,
                "region-west", "guild-azure", new Vector2(-5f, 0f), 1, 4, null);
            EnsureLocation(state, "town-river", "Rivercross", LocationType.Town,
                "region-north", "guild-verdant", new Vector2(1f, 5f), 1, 5, null);
            EnsureLocation(state, "town-ember", "Emberwatch", LocationType.Town,
                "region-east", "guild-crimson", new Vector2(6f, 0f), 2, 4, null);
            EnsureLocation(state, "market-frontier", "Frontier Exchange", LocationType.Marketplace,
                "region-west", "guild-azure", new Vector2(-3.7f, -2.5f), 0, 7, null);
            EnsureLocation(state, "hospital-frontier", "Ivory Field Hospital", LocationType.Hospital,
                "region-west", "guild-ivory", new Vector2(-6.6f, -2.2f), 0, 5, null);
            EnsureLocation(state, "market-river", "Rivercross Bazaar", LocationType.Marketplace,
                "region-north", "guild-verdant", new Vector2(1.9f, 3.1f), 0, 8, null);
            EnsureLocation(state, "hospital-river", "Northbank Recovery Hall", LocationType.Hospital,
                "region-north", "guild-verdant", new Vector2(-1.6f, 5.8f), 0, 6, null);
            EnsureLocation(state, "market-ember", "Umbral Night Market", LocationType.Marketplace,
                "region-east", "guild-umbra", new Vector2(7.4f, -2.2f), 1, 10, null);
            EnsureLocation(state, "hospital-ember", "Emberwatch Trauma Ward", LocationType.Hospital,
                "region-east", "guild-crimson", new Vector2(4.4f, -2.5f), 0, 7, null);
            EnsureLocation(state, "resource-mana-well", "Azure Mana Well", LocationType.ResourceSite,
                "region-west", "guild-azure", new Vector2(-7f, 3f), 2, 12, null);
            EnsureLocation(state, "resource-ironwood", "Ironwood Grove", LocationType.ResourceSite,
                "region-north", "guild-verdant", new Vector2(0f, 8f), 2, 10, null);
            EnsureLocation(state, "resource-sunstone", "Sunstone Ridge", LocationType.ResourceSite,
                "region-east", "guild-crimson", new Vector2(8f, 3f), 3, 14, null);
            EnsureLocation(state, "dungeon-ash-tunnel", "Ash-Tunnel Gate", LocationType.Dungeon,
                "region-west", "", new Vector2(-3f, 3f), 2, 0, new[] { "mission-goblin" });
            EnsureLocation(state, "dungeon-drowned-crypt", "Drowned Crypt", LocationType.Dungeon,
                "region-north", "", new Vector2(3f, 7f), 4, 0, new[] { "mission-crypt" });
            EnsureLocation(state, "dungeon-voidglass-spire", "Voidglass Spire", LocationType.Dungeon,
                "region-east", "", new Vector2(5f, 4f), 6, 0, new[] { "mission-spire" });
            EnsureLocation(state, "dungeon-frostline-warrens", "Frostline Warrens", LocationType.Dungeon,
                "region-north", "", new Vector2(-6.5f, 6.8f), 3, 0, new[] { "mission-frostline" });
            EnsureLocation(state, "dungeon-red-chapel", "Red Chapel Gate", LocationType.Dungeon,
                "region-east", "", new Vector2(9f, 6.2f), 5, 0, new[] { "mission-red-chapel" });
            EnsureLocation(state, "dungeon-glassfang-nest", "Glassfang Nest", LocationType.Dungeon,
                "region-west", "", new Vector2(1.5f, -5f), 2, 0, new[] { "mission-glassfang" });
        }

        private static void EnsureLocation(
            EcosystemWorldState state,
            string id,
            string displayName,
            LocationType type,
            string regionId,
            string controllingGuildId,
            Vector2 mapPosition,
            int danger,
            int resourceYield,
            string[] missionIds)
        {
            if (FindLocation(state, id) != null)
            {
                return;
            }
            state.map.locations.Add(new LocationState
            {
                id = id,
                displayName = displayName,
                locationType = type,
                regionId = regionId,
                controllingGuildId = controllingGuildId,
                mapPosition = mapPosition,
                danger = danger,
                resourceYield = resourceYield,
                missionTemplateIds = missionIds == null
                    ? new List<string>()
                    : new List<string>(missionIds)
            });
        }

        private static void AddDefaultRoutes(EcosystemWorldState state)
        {
            EnsureRoute(state, "town-frontier", "dungeon-ash-tunnel", 1, 1);
            EnsureRoute(state, "town-frontier", "resource-mana-well", 1, 1);
            EnsureRoute(state, "town-frontier", "market-frontier", 1, 0);
            EnsureRoute(state, "town-frontier", "hospital-frontier", 1, 0);
            EnsureRoute(state, "town-frontier", "dungeon-glassfang-nest", 2, 2);
            EnsureRoute(state, "town-frontier", "town-river", 2, 2);
            EnsureRoute(state, "town-river", "resource-ironwood", 1, 1);
            EnsureRoute(state, "town-river", "dungeon-drowned-crypt", 1, 3);
            EnsureRoute(state, "town-river", "market-river", 1, 0);
            EnsureRoute(state, "town-river", "hospital-river", 1, 0);
            EnsureRoute(state, "town-river", "dungeon-frostline-warrens", 2, 3);
            EnsureRoute(state, "town-river", "town-ember", 2, 2);
            EnsureRoute(state, "town-ember", "resource-sunstone", 1, 2);
            EnsureRoute(state, "town-ember", "dungeon-voidglass-spire", 2, 4);
            EnsureRoute(state, "town-ember", "market-ember", 1, 0);
            EnsureRoute(state, "town-ember", "hospital-ember", 1, 0);
            EnsureRoute(state, "town-ember", "dungeon-red-chapel", 2, 4);
            EnsureRoute(state, "dungeon-drowned-crypt", "dungeon-voidglass-spire", 2, 5);
        }

        private static void EnsureRoute(
            EcosystemWorldState state,
            string from,
            string to,
            int travelDays,
            int danger)
        {
            foreach (var route in state.map.routes)
            {
                if (route != null &&
                    ((route.fromLocationId == from && route.toLocationId == to) ||
                     (route.fromLocationId == to && route.toLocationId == from)))
                {
                    return;
                }
            }
            state.map.routes.Add(new WorldRouteState
            {
                fromLocationId = from,
                toLocationId = to,
                travelDays = travelDays,
                danger = danger
            });
        }

        private static void AddDefaultMissions(EcosystemWorldState state)
        {
            EnsureMission(state, "mission-goblin", "Ash-Tunnel Gate", 1, 18, "courage");
            EnsureMission(state, "mission-crypt", "Drowned Crypt", 3, 42, "ambition");
            EnsureMission(state, "mission-spire", "Voidglass Spire", 5, 78, "greed");
            EnsureMission(state, "mission-frostline", "Frostline Warrens", 3, 46, "loyalty");
            EnsureMission(state, "mission-red-chapel", "Red Chapel Gate", 5, 72, "ambition");
            EnsureMission(state, "mission-glassfang", "Glassfang Nest", 2, 34, "courage");
        }

        private static void EnsureMission(
            EcosystemWorldState state,
            string id,
            string displayName,
            int difficulty,
            int reward,
            string favoredTrait)
        {
            if (state.missions.Find(item => item != null && item.id == id) != null)
            {
                return;
            }
            state.missions.Add(new MissionState
            {
                id = id,
                displayName = displayName,
                difficulty = difficulty,
                reward = reward,
                favoredTrait = favoredTrait
            });
        }

        private static HunterProfile CreateHunter(
            in HunterTemplate template,
            IReadOnlyList<EcosystemGearDefinition> gearCatalog)
        {
            var gearId = GearIdAt(gearCatalog, template.GearIndex);
            var hunter = new HunterProfile
            {
                id = template.Id,
                displayName = template.DisplayName,
                level = template.Level,
                courage = template.Courage,
                ambition = template.Ambition,
                loyalty = template.Loyalty,
                greed = template.Greed,
                goal = template.Goal,
                guildId = template.GuildId,
                equippedGearId = gearId,
                currentActivity = "Observing the frontier",
                destinationId = string.IsNullOrEmpty(template.GuildId)
                    ? "Hub_Center"
                    : template.GuildId,
                isAlive = true,
                deathDay = -1,
                gold = template.Gold,
                locationId = template.LocationId,
                inventoryGearIds = new List<string> { gearId },
                fame = template.Fame
            };
            hunter.vitals.Initialize(true, 0, StartingShieldForGear(gearCatalog, gearId));
            return hunter;
        }

        private static void AddHuntersUntilRequiredCount(
            EcosystemWorldState state,
            IReadOnlyList<EcosystemGearDefinition> gearCatalog)
        {
            foreach (var template in HunterTemplates)
            {
                if (state.hunters.Count >= StartingHunterCount)
                {
                    break;
                }
                if (FindHunter(state, template.Id) == null)
                {
                    state.hunters.Add(CreateHunter(template, gearCatalog));
                }
            }

            while (state.hunters.Count < StartingHunterCount ||
                   state.hunters.Count(hunter => hunter != null && hunter.IsActive) <
                   MinimumActiveHunterCount)
            {
                var ordinal = state.hunters.Count + 1;
                var id = $"hunter-generated-{ordinal:000}";
                if (FindHunter(state, id) != null)
                {
                    continue;
                }
                state.hunters.Add(CreateGeneratedHunter(
                    state,
                    gearCatalog,
                    id,
                    ordinal,
                    1));
            }
        }

        /// <summary>
        /// Adds one deterministic new awakening without regenerating or renumbering any
        /// existing hunter. Population sequence is persisted so save/reload continuation
        /// produces the same identities and never reuses an ID.
        /// </summary>
        public static HunterProfile AddAwakenedHunter(
            EcosystemWorldState state,
            IReadOnlyList<EcosystemGearDefinition> gearCatalog,
            int awakeningDay)
        {
            if (state == null) return null;
            EnsureCollections(state);
            string id;
            do
            {
                state.populationSequence = state.populationSequence == int.MaxValue
                    ? int.MaxValue
                    : state.populationSequence + 1;
                id = $"hunter-awakened-{Mathf.Max(1, awakeningDay):D5}-{state.populationSequence:D7}";
            } while (FindHunter(state, id) != null && state.populationSequence < int.MaxValue);

            if (FindHunter(state, id) != null)
            {
                return null;
            }

            var ordinal = StartingHunterCount + state.populationSequence;
            var hunter = CreateGeneratedHunter(
                state,
                gearCatalog,
                id,
                ordinal,
                Mathf.Max(1, awakeningDay));
            state.hunters.Add(hunter);
            var guild = FindGuild(state, hunter.guildId);
            if (guild != null && !guild.memberIds.Contains(hunter.id))
            {
                guild.memberIds.Add(hunter.id);
                guild.memberIds.Sort(StringComparer.Ordinal);
            }
            return hunter;
        }

        private static HunterProfile CreateGeneratedHunter(
            EcosystemWorldState state,
            IReadOnlyList<EcosystemGearDefinition> gearCatalog,
            string id,
            int ordinal,
            int awakeningDay)
        {
            var safeOrdinal = Mathf.Max(1, ordinal);
            var first = GeneratedFirstNames[(safeOrdinal - 1) % GeneratedFirstNames.Length];
            var surnameBand = (safeOrdinal - 1) / GeneratedFirstNames.Length;
            var surnameIndex = ((safeOrdinal - 1) * 7 + surnameBand * 13) % GeneratedSurnames.Length;
            var gearIndex = (int)(EcosystemDeterministicRandom.StableHash(id + "|gear") %
                                  (uint)Mathf.Max(1, FallbackGearIds.Length));
            var gearId = GearIdAt(gearCatalog, gearIndex);
            var affiliation = StableUnit(state.worldSeed, id + "|guild");
            var guildId = affiliation < 0.28f
                ? string.Empty
                : GeneratedGuildIds[(int)(EcosystemDeterministicRandom.StableHash(id + "|guild-index") %
                                           (uint)GeneratedGuildIds.Length)];
            var townId = string.IsNullOrEmpty(guildId)
                ? GeneratedTownIds[(int)(EcosystemDeterministicRandom.StableHash(id + "|town") %
                                         (uint)GeneratedTownIds.Length)]
                : ResolveHomeLocation(state, guildId);
            var level = GeneratedLegacyLevel(state.worldSeed, id);
            var hunter = new HunterProfile
            {
                id = id,
                displayName = $"{first} {GeneratedSurnames[surnameIndex]}",
                level = level,
                courage = Mathf.Lerp(0.18f, 0.96f, StableUnit(state.worldSeed, id + "|courage")),
                ambition = Mathf.Lerp(0.18f, 0.96f, StableUnit(state.worldSeed, id + "|ambition")),
                loyalty = Mathf.Lerp(0.18f, 0.96f, StableUnit(state.worldSeed, id + "|loyalty")),
                greed = Mathf.Lerp(0.12f, 0.94f, StableUnit(state.worldSeed, id + "|greed")),
                goal = GeneratedGoals[(int)(EcosystemDeterministicRandom.StableHash(id + "|goal") %
                                             (uint)GeneratedGoals.Length)],
                guildId = guildId,
                equippedGearId = gearId,
                currentActivity = awakeningDay <= 1
                    ? "Establishing a life on the frontier"
                    : "Registering a new awakening with the Association",
                destinationId = string.Empty,
                isAlive = true,
                deathDay = -1,
                awakeningDay = awakeningDay,
                isRetired = false,
                retirementDay = -1,
                gold = 28 + (int)(StableUnit(state.worldSeed, id + "|gold") * 150f),
                locationId = townId,
                inventoryGearIds = new List<string> { gearId },
                fame = level <= 1 ? 0f : (level - 1) * 1.25f
            };
            hunter.vitals.Initialize(true, 0, StartingShieldForGear(gearCatalog, gearId));
            EcosystemCareerRules.Normalize(hunter, gearCatalog);
            return hunter;
        }

        private static int GeneratedLegacyLevel(int worldSeed, string id)
        {
            var rankRoll = StableUnit(worldSeed, id + "|starting-rank");
            var rankBand = rankRoll < 0.55f ? 0
                : rankRoll < 0.80f ? 1
                : rankRoll < 0.92f ? 2
                : rankRoll < 0.97f ? 3
                : rankRoll < 0.995f ? 4
                : 5;
            var withinBand = (int)(EcosystemDeterministicRandom.StableHash(id + "|rank-step") % 6u);
            return 1 + rankBand * 6 + withinBand;
        }

        private static void AddDefaultContracts(EcosystemWorldState state)
        {
            EnsureContract(state, new ContractState
            {
                id = "contract-ash",
                displayName = "Clear the Ash-Tunnel",
                contractType = ContractType.DungeonClear,
                status = ContractStatus.Offered,
                missionTemplateId = "mission-goblin",
                issuerGuildId = "guild-azure",
                locationId = "town-frontier",
                targetLocationId = "dungeon-ash-tunnel",
                offeredDay = state.day,
                expiresDay = state.day + 4,
                difficulty = 1,
                rewardGold = 54,
                rewardResources = 18,
                rewardFame = 2f
            });
            EnsureContract(state, new ContractState
            {
                id = "contract-crypt",
                displayName = "Seal the Drowned Crypt",
                contractType = ContractType.DungeonClear,
                status = ContractStatus.Offered,
                missionTemplateId = "mission-crypt",
                issuerGuildId = "guild-verdant",
                locationId = "town-river",
                targetLocationId = "dungeon-drowned-crypt",
                offeredDay = state.day,
                expiresDay = state.day + 6,
                difficulty = 3,
                rewardGold = 126,
                rewardResources = 42,
                rewardFame = 5f
            });
            EnsureContract(state, new ContractState
            {
                id = "contract-spire",
                displayName = "Break the Voidglass Spire",
                contractType = ContractType.Raid,
                status = ContractStatus.Active,
                missionTemplateId = "mission-spire",
                issuerGuildId = "guild-crimson",
                locationId = "town-ember",
                targetLocationId = "dungeon-voidglass-spire",
                offeredDay = state.day,
                expiresDay = state.day + 8,
                difficulty = 5,
                rewardGold = 234,
                rewardResources = 78,
                rewardFame = 9f,
                acceptedPartyId = "party-crimson-spire"
            });
            EnsureContract(state, CreateDefaultContract(
                "contract-frostline", "Survey the Frostline Warrens", ContractType.DungeonClear,
                "mission-frostline", "guild-ivory", "town-frontier",
                "dungeon-frostline-warrens", state.day, 5, 3, 118, 38, 4f));
            EnsureContract(state, CreateDefaultContract(
                "contract-red-chapel", "Purge the Red Chapel", ContractType.Raid,
                "mission-red-chapel", "guild-umbra", "town-ember",
                "dungeon-red-chapel", state.day, 7, 5, 216, 70, 8f));
            EnsureContract(state, CreateDefaultContract(
                "contract-glassfang", "Cull the Glassfang Nest", ContractType.DungeonClear,
                "mission-glassfang", "guild-azure", "town-frontier",
                "dungeon-glassfang-nest", state.day, 4, 2, 84, 28, 3f));
            EnsureContract(state, CreateDefaultContract(
                "contract-crypt-recovery", "Recover the Crypt Survey Team", ContractType.Escort,
                "mission-crypt", "guild-verdant", "town-river",
                "dungeon-drowned-crypt", state.day, 5, 4, 154, 50, 6f));
            EnsureContract(state, CreateDefaultContract(
                "contract-ash-rescue", "Rescue Miners Beyond the Ash-Tunnel", ContractType.Escort,
                "mission-goblin", "guild-ivory", "town-frontier",
                "dungeon-ash-tunnel", state.day, 3, 2, 76, 24, 3f));
            EnsureContract(state, CreateDefaultContract(
                "contract-spire-salvage", "Salvage Voidglass Samples", ContractType.DungeonClear,
                "mission-spire", "guild-umbra", "town-ember",
                "dungeon-voidglass-spire", state.day, 8, 5, 198, 64, 7f));
        }

        private static ContractState CreateDefaultContract(
            string id,
            string displayName,
            ContractType type,
            string missionId,
            string issuerGuildId,
            string postingLocationId,
            string targetLocationId,
            int offeredDay,
            int durationDays,
            int difficulty,
            int rewardGold,
            int rewardResources,
            float rewardFame)
        {
            return new ContractState
            {
                id = id,
                displayName = displayName,
                contractType = type,
                status = ContractStatus.Offered,
                missionTemplateId = missionId,
                issuerGuildId = issuerGuildId,
                locationId = postingLocationId,
                targetLocationId = targetLocationId,
                offeredDay = offeredDay,
                expiresDay = offeredDay + durationDays,
                difficulty = difficulty,
                rewardGold = rewardGold,
                rewardResources = rewardResources,
                rewardFame = rewardFame
            };
        }

        private static void EnsureContract(EcosystemWorldState state, ContractState contract)
        {
            if (FindContract(state, contract.id) == null)
            {
                state.contracts.Add(contract);
            }
        }

        private static void AddDefaultParties(EcosystemWorldState state)
        {
            if (FindParty(state, "party-crimson-spire") == null)
            {
                state.parties.Add(new PartyState
                {
                    id = "party-crimson-spire",
                    displayName = "Crimson Gatebreakers",
                    leaderHunterId = "hunter-mara",
                    memberIds = new List<string> { "hunter-mara", "hunter-voss" },
                    status = PartyStatus.Active,
                    locationId = "town-ember",
                    activeContractId = "contract-spire",
                    createdDay = state.day
                });
            }
        }

        private static void AddDefaultInvitations(EcosystemWorldState state)
        {
            if (FindInvitation(state, "invitation-iona-azure") == null)
            {
                state.invitations.Add(new InvitationState
                {
                    id = "invitation-iona-azure",
                    invitationType = InvitationType.GuildMembership,
                    status = InvitationStatus.Pending,
                    senderHunterId = DefaultPlayerId,
                    recipientHunterId = "hunter-iona",
                    guildId = "guild-azure",
                    createdDay = state.day,
                    expiresDay = state.day + 3,
                    utilityScore = 0.28f
                });
            }
            if (FindInvitation(state, "invitation-brann-party") == null)
            {
                state.invitations.Add(new InvitationState
                {
                    id = "invitation-brann-party",
                    invitationType = InvitationType.PartyMembership,
                    status = InvitationStatus.Pending,
                    senderHunterId = "hunter-mara",
                    recipientHunterId = "hunter-brann",
                    partyId = "party-crimson-spire",
                    contractId = "contract-spire",
                    createdDay = state.day,
                    expiresDay = state.day + 2,
                    utilityScore = 0.11f
                });
            }
        }

        private static void AddDefaultRelationships(EcosystemWorldState state)
        {
            if (state.hunters.Count < 2)
            {
                return;
            }
            for (var index = 0; index < state.hunters.Count; index++)
            {
                var hunter = state.hunters[index];
                var next = state.hunters[(index + 1) % state.hunters.Count];
                if (hunter == null || next == null || hunter.id == next.id)
                {
                    continue;
                }
                hunter.relationships ??= new List<HunterRelationship>();
                if (hunter.relationships.Find(item => item != null && item.hunterId == next.id) == null)
                {
                    hunter.relationships.Add(new HunterRelationship
                    {
                        hunterId = next.id,
                        affinity = StableSignedUnit(state.worldSeed, hunter.id + ":affinity") * 0.35f,
                        trust = StableSignedUnit(state.worldSeed, hunter.id + ":trust") * 0.25f,
                        rivalry = StableUnit(state.worldSeed, hunter.id + ":rivalry") * 0.18f,
                        debt = index % 5 == 0 ? 0.12f : 0f,
                        grudge = index % 7 == 0 ? 0.08f : 0f
                    });
                }
            }
            EnsureRelationship(state, "hunter-mara", "hunter-voss", 0.42f, 0.56f, 0.12f);
            EnsureRelationship(state, "hunter-voss", "hunter-mara", 0.38f, 0.61f, 0.09f);
            EnsureRelationship(state, DefaultPlayerId, "hunter-iona", 0.24f, 0.18f, 0f);
            EnsureRelationship(state, "hunter-iona", DefaultPlayerId, 0.31f, 0.22f, 0f);
        }

        private static void EnsureRelationship(
            EcosystemWorldState state,
            string ownerId,
            string otherId,
            float affinity,
            float trust,
            float rivalry)
        {
            var owner = FindHunter(state, ownerId);
            if (owner == null || FindHunter(state, otherId) == null)
            {
                return;
            }
            owner.relationships ??= new List<HunterRelationship>();
            if (owner.relationships.Find(item => item != null && item.hunterId == otherId) != null)
            {
                return;
            }
            owner.relationships.Add(new HunterRelationship
            {
                hunterId = otherId,
                affinity = affinity,
                trust = trust,
                rivalry = rivalry
            });
        }

        private static void AddInitialWorldEvent(EcosystemWorldState state)
        {
            if (state.structuredEvents.Count > 0)
            {
                return;
            }
            state.structuredEvents.Add(new StructuredWorldEvent
            {
                id = "event-world-founded",
                sequence = 0L,
                day = state.day,
                eventType = WorldEventType.SimulationAdvanced,
                locationId = DefaultLocationId,
                summary = "The frontier hunter ecosystem entered persistent simulation."
            });
        }

        private static void NormalizeEntityIds(EcosystemWorldState state)
        {
            NormalizeIds(state.hunters, hunter => hunter?.id, (hunter, id) => hunter.id = id, "hunter");
            NormalizeIds(state.guilds, guild => guild?.id, (guild, id) => guild.id = id, "guild");
            NormalizeIds(state.missions, mission => mission?.id, (mission, id) => mission.id = id, "mission");
            NormalizeIds(state.map.locations, location => location?.id, (location, id) => location.id = id, "location");
            NormalizeIds(state.contracts, contract => contract?.id, (contract, id) => contract.id = id, "contract");
            NormalizeIds(state.parties, party => party?.id, (party, id) => party.id = id, "party");
            NormalizeIds(state.invitations, invitation => invitation?.id, (invitation, id) => invitation.id = id, "invitation");
        }

        private static void NormalizeIds<T>(
            List<T> items,
            Func<T, string> getId,
            Action<T, string> setId,
            string prefix)
            where T : class
        {
            var used = new HashSet<string>();
            var generatedIndex = 1;
            for (var index = items.Count - 1; index >= 0; index--)
            {
                if (items[index] == null)
                {
                    items.RemoveAt(index);
                }
            }
            foreach (var item in items)
            {
                var id = getId(item);
                if (string.IsNullOrWhiteSpace(id) || used.Contains(id))
                {
                    do
                    {
                        id = $"{prefix}-migrated-{generatedIndex:00}";
                        generatedIndex++;
                    } while (used.Contains(id));
                    setId(item, id);
                }
                used.Add(id);
            }
        }

        private static void NormalizeHunters(
            EcosystemWorldState state,
            IReadOnlyList<EcosystemGearDefinition> gearCatalog,
            bool isVersionOneImport)
        {
            var hunterIds = CollectIds(state.hunters, hunter => hunter.id);
            var guildIds = CollectIds(state.guilds, guild => guild.id);
            var locationIds = CollectIds(state.map.locations, location => location.id);
            var partyIds = CollectIds(state.parties, party => party.id);
            var contractIds = CollectIds(state.contracts, contract => contract.id);
            foreach (var hunter in state.hunters)
            {
                hunter.displayName = string.IsNullOrWhiteSpace(hunter.displayName) ? hunter.id : hunter.displayName;
                hunter.level = Mathf.Max(1, hunter.level);
                hunter.experience = Mathf.Max(0, hunter.experience);
                hunter.wounds = Mathf.Max(0, hunter.wounds);
                hunter.gold = Mathf.Max(0, hunter.gold);
                hunter.pendingRewardGold = Mathf.Max(0, hunter.pendingRewardGold);
                if (isVersionOneImport)
                {
                    hunter.isAlive = true;
                    hunter.deathDay = -1;
                    hunter.deathCause = "";
                    hunter.isRetired = false;
                    hunter.retirementDay = -1;
                    hunter.retirementReason = "";
                }
                hunter.awakeningDay = Mathf.Clamp(hunter.awakeningDay <= 0 ? 1 : hunter.awakeningDay, 1, state.day);
                if (hunter.isAlive)
                {
                    hunter.deathDay = -1;
                }
                else
                {
                    hunter.isRetired = false;
                    hunter.retirementDay = -1;
                    hunter.retirementReason = "";
                }
                if (hunter.isRetired)
                {
                    hunter.retirementDay = Mathf.Clamp(
                        hunter.retirementDay < hunter.awakeningDay ? state.day : hunter.retirementDay,
                        hunter.awakeningDay,
                        state.day);
                    hunter.guildId = "";
                    hunter.partyId = "";
                    hunter.activeContractId = "";
                    hunter.destinationId = "";
                    hunter.travelDaysRemaining = 0;
                    hunter.currentActivity = "Retired from hunting";
                }
                else if (hunter.isAlive)
                {
                    hunter.retirementDay = -1;
                    hunter.retirementReason = "";
                }
                if (!hunter.IsActive)
                {
                    // Guild and party rosters represent current operational membership.
                    // The person's identity, former relationships, and full life history
                    // remain on the persistent hunter record.
                    hunter.guildId = "";
                    hunter.partyId = "";
                    hunter.activeContractId = "";
                    hunter.destinationId = "";
                    hunter.travelDaysRemaining = 0;
                }
                hunter.relationships ??= new List<HunterRelationship>();
                hunter.memories ??= new List<HunterMemory>();
                hunter.inventoryGearIds ??= new List<string>();
                hunter.injuries ??= new List<HunterInjury>();
                DeduplicateStrings(hunter.inventoryGearIds);
                NormalizeRelationships(hunter, hunterIds);
                NormalizeInjuries(hunter);

                if (isVersionOneImport && string.IsNullOrWhiteSpace(hunter.equippedGearId))
                {
                    hunter.equippedGearId = GearIdAt(gearCatalog, 0);
                }
                if (!string.IsNullOrWhiteSpace(hunter.equippedGearId) &&
                    !hunter.inventoryGearIds.Contains(hunter.equippedGearId))
                {
                    hunter.inventoryGearIds.Add(hunter.equippedGearId);
                }
                EcosystemCareerRules.Normalize(hunter, gearCatalog);
                hunter.vitals ??= new HunterVitalsState();
                if (!hunter.vitals.initialized)
                {
                    hunter.vitals.Initialize(
                        hunter.isAlive,
                        hunter.wounds,
                        StartingShieldForGear(gearCatalog, hunter.equippedGearId));
                }
                else
                {
                    hunter.vitals.Normalize(hunter.isAlive, hunter.isIncapacitated);
                }
                if (!guildIds.Contains(hunter.guildId))
                {
                    hunter.guildId = "";
                }
                if (!locationIds.Contains(hunter.locationId))
                {
                    hunter.locationId = ResolveHomeLocation(state, hunter.guildId);
                }
                hunter.travelDaysRemaining = Mathf.Max(0, hunter.travelDaysRemaining);
                if (hunter.travelDaysRemaining == 0 || !locationIds.Contains(hunter.destinationId) ||
                    hunter.destinationId == hunter.locationId)
                {
                    hunter.destinationId = "";
                    hunter.travelDaysRemaining = 0;
                }
                if (!partyIds.Contains(hunter.partyId))
                {
                    hunter.partyId = "";
                }
                if (!contractIds.Contains(hunter.activeContractId))
                {
                    hunter.activeContractId = "";
                }
            }
        }

        private static void NormalizeRelationships(HunterProfile hunter, HashSet<string> hunterIds)
        {
            var byHunter = new Dictionary<string, HunterRelationship>();
            foreach (var relationship in hunter.relationships)
            {
                if (relationship == null ||
                    relationship.hunterId == hunter.id ||
                    !hunterIds.Contains(relationship.hunterId))
                {
                    continue;
                }
                relationship.affinity = Mathf.Clamp(relationship.affinity, -1f, 1f);
                relationship.trust = Mathf.Clamp(relationship.trust, -1f, 1f);
                relationship.rivalry = Mathf.Clamp01(relationship.rivalry);
                relationship.debt = Mathf.Clamp(relationship.debt, -1f, 1f);
                relationship.grudge = Mathf.Clamp01(relationship.grudge);
                if (!byHunter.TryGetValue(relationship.hunterId, out var existing))
                {
                    byHunter.Add(relationship.hunterId, relationship);
                    continue;
                }
                existing.affinity = StrongerMagnitude(existing.affinity, relationship.affinity);
                existing.trust = StrongerMagnitude(existing.trust, relationship.trust);
                existing.rivalry = Mathf.Max(existing.rivalry, relationship.rivalry);
                existing.debt = StrongerMagnitude(existing.debt, relationship.debt);
                existing.grudge = Mathf.Max(existing.grudge, relationship.grudge);
            }
            hunter.relationships.Clear();
            var normalized = new List<HunterRelationship>(byHunter.Values);
            normalized.Sort((left, right) => string.CompareOrdinal(left.hunterId, right.hunterId));
            foreach (var relationship in normalized)
            {
                hunter.relationships.Add(relationship);
            }
        }

        private static void EnsurePlayerPrototypeGear(
            EcosystemWorldState state,
            IReadOnlyList<EcosystemGearDefinition> gearCatalog)
        {
            var player = FindHunter(state, state.playerHunterId);
            if (player == null || gearCatalog == null)
            {
                return;
            }

            player.inventoryGearIds ??= new List<string>();
            foreach (var gear in gearCatalog)
            {
                if (gear != null && !string.IsNullOrWhiteSpace(gear.GearId) &&
                    !player.inventoryGearIds.Contains(gear.GearId))
                {
                    player.inventoryGearIds.Add(gear.GearId);
                }
            }
        }

        private static void NormalizeInjuries(HunterProfile hunter)
        {
            var ids = new HashSet<string>();
            for (var index = hunter.injuries.Count - 1; index >= 0; index--)
            {
                var injury = hunter.injuries[index];
                if (injury == null)
                {
                    hunter.injuries.RemoveAt(index);
                    continue;
                }
                if (string.IsNullOrWhiteSpace(injury.id))
                {
                    injury.id = $"injury-{hunter.id}-{injury.sufferedDay}-{index}";
                }
                if (!ids.Add(injury.id))
                {
                    hunter.injuries.RemoveAt(index);
                }
                injury.sufferedDay = Mathf.Max(1, injury.sufferedDay);
                injury.recoveryDay = Mathf.Max(injury.sufferedDay, injury.recoveryDay);
            }
        }

        private static void NormalizeGuildMemberships(EcosystemWorldState state)
        {
            var hunters = new Dictionary<string, HunterProfile>();
            foreach (var hunter in state.hunters)
            {
                hunters[hunter.id] = hunter;
            }

            foreach (var guild in state.guilds)
            {
                guild.memberIds ??= new List<string>();
                DeduplicateStrings(guild.memberIds);
                foreach (var memberId in guild.memberIds)
                {
                    if (hunters.TryGetValue(memberId, out var hunter) && hunter.IsActive &&
                        string.IsNullOrEmpty(hunter.guildId))
                    {
                        hunter.guildId = guild.id;
                    }
                }
                guild.memberIds.Clear();
            }

            var guilds = new Dictionary<string, GuildState>();
            foreach (var guild in state.guilds)
            {
                guilds[guild.id] = guild;
            }
            foreach (var hunter in state.hunters)
            {
                if (hunter.IsActive && guilds.TryGetValue(hunter.guildId, out var guild) &&
                    !guild.memberIds.Contains(hunter.id))
                {
                    guild.memberIds.Add(hunter.id);
                }
            }
            foreach (var guild in state.guilds)
            {
                guild.memberIds.Sort(StringComparer.Ordinal);
            }
        }

        private static void NormalizeLocationControl(EcosystemWorldState state)
        {
            var guilds = new Dictionary<string, GuildState>();
            foreach (var guild in state.guilds)
            {
                guild.controlledLocationIds ??= new List<string>();
                DeduplicateStrings(guild.controlledLocationIds);
                guilds[guild.id] = guild;
            }

            var locations = new Dictionary<string, LocationState>();
            foreach (var location in state.map.locations)
            {
                location.missionTemplateIds ??= new List<string>();
                DeduplicateStrings(location.missionTemplateIds);
                locations[location.id] = location;
            }

            foreach (var guild in state.guilds)
            {
                foreach (var locationId in guild.controlledLocationIds)
                {
                    if (locations.TryGetValue(locationId, out var location) &&
                        string.IsNullOrEmpty(location.controllingGuildId))
                    {
                        location.controllingGuildId = guild.id;
                    }
                }
                guild.controlledLocationIds.Clear();
                if (string.IsNullOrWhiteSpace(guild.homeLocationId) ||
                    !locations.ContainsKey(guild.homeLocationId))
                {
                    guild.homeLocationId = DefaultLocationId;
                }
            }

            foreach (var location in state.map.locations)
            {
                if (string.IsNullOrWhiteSpace(location.controllingGuildId) ||
                    !guilds.TryGetValue(location.controllingGuildId, out var guild))
                {
                    location.controllingGuildId = "";
                    continue;
                }
                if (!guild.controlledLocationIds.Contains(location.id))
                {
                    guild.controlledLocationIds.Add(location.id);
                }
            }
            foreach (var guild in state.guilds)
            {
                guild.controlledLocationIds.Sort(StringComparer.Ordinal);
                guild.territory = guild.controlledLocationIds.Count;
            }
        }

        private static void NormalizeDiplomacy(EcosystemWorldState state)
        {
            var guildIds = CollectIds(state.guilds, guild => guild.id);
            foreach (var guild in state.guilds)
            {
                guild.diplomacy ??= new List<GuildDiplomacyState>();
                var byGuild = new Dictionary<string, GuildDiplomacyState>();
                foreach (var relation in guild.diplomacy)
                {
                    if (relation == null || relation.guildId == guild.id || !guildIds.Contains(relation.guildId))
                    {
                        continue;
                    }
                    relation.regard = Mathf.Clamp(relation.regard, -1f, 1f);
                    relation.grievance = Mathf.Clamp01(relation.grievance);
                    if (!byGuild.ContainsKey(relation.guildId))
                    {
                        byGuild.Add(relation.guildId, relation);
                    }
                }
                foreach (var otherId in guildIds)
                {
                    if (otherId == guild.id || byGuild.ContainsKey(otherId))
                    {
                        continue;
                    }
                    byGuild.Add(otherId, new GuildDiplomacyState
                    {
                        guildId = otherId,
                        stance = DiplomaticStance.Neutral,
                        sinceDay = state.day
                    });
                }
                guild.diplomacy.Clear();
                var normalized = new List<GuildDiplomacyState>(byGuild.Values);
                normalized.Sort((left, right) => string.CompareOrdinal(left.guildId, right.guildId));
                foreach (var relation in normalized)
                {
                    guild.diplomacy.Add(relation);
                }
            }
        }

        private static void NormalizeParties(EcosystemWorldState state)
        {
            var hunterIds = CollectIds(state.hunters, hunter => hunter.id);
            var activeHunterIds = new HashSet<string>();
            foreach (var hunter in state.hunters)
            {
                if (hunter != null && hunter.IsActive)
                {
                    activeHunterIds.Add(hunter.id);
                }
            }
            var locationIds = CollectIds(state.map.locations, location => location.id);
            var contractIds = CollectIds(state.contracts, contract => contract.id);
            var assignedHunters = new HashSet<string>();
            foreach (var party in state.parties)
            {
                party.memberIds ??= new List<string>();
                DeduplicateStrings(party.memberIds);
                if (party.status == PartyStatus.Disbanded)
                {
                    party.memberIds.Clear();
                    party.leaderHunterId = "";
                    party.activeContractId = "";
                    party.destinationId = "";
                    party.travelDaysRemaining = 0;
                    continue;
                }
                for (var index = party.memberIds.Count - 1; index >= 0; index--)
                {
                    var memberId = party.memberIds[index];
                    if (!hunterIds.Contains(memberId) || !activeHunterIds.Contains(memberId) ||
                        !assignedHunters.Add(memberId))
                    {
                        party.memberIds.RemoveAt(index);
                    }
                }
                if (!activeHunterIds.Contains(party.leaderHunterId))
                {
                    party.leaderHunterId = party.memberIds.Count > 0 ? party.memberIds[0] : "";
                }
                if (party.memberIds.Count == 0)
                {
                    party.status = PartyStatus.Disbanded;
                    party.leaderHunterId = "";
                    party.activeContractId = "";
                    party.destinationId = "";
                    party.travelDaysRemaining = 0;
                    continue;
                }
                if (!string.IsNullOrEmpty(party.leaderHunterId) && !party.memberIds.Contains(party.leaderHunterId))
                {
                    party.memberIds.Insert(0, party.leaderHunterId);
                    assignedHunters.Add(party.leaderHunterId);
                }
                if (!locationIds.Contains(party.locationId))
                {
                    party.locationId = DefaultLocationId;
                }
                party.travelDaysRemaining = Mathf.Max(0, party.travelDaysRemaining);
                if (party.status != PartyStatus.Travelling || party.travelDaysRemaining == 0 ||
                    !locationIds.Contains(party.destinationId) || party.destinationId == party.locationId)
                {
                    party.destinationId = "";
                    party.travelDaysRemaining = 0;
                    if (party.status == PartyStatus.Travelling)
                    {
                        party.status = party.memberIds.Count == 0
                            ? PartyStatus.Disbanded
                            : PartyStatus.Forming;
                    }
                }
                if (!contractIds.Contains(party.activeContractId))
                {
                    party.activeContractId = "";
                }
                party.createdDay = Mathf.Max(1, party.createdDay);
                foreach (var memberId in party.memberIds)
                {
                    var hunter = FindHunter(state, memberId);
                    if (hunter != null)
                    {
                        hunter.partyId = party.id;
                        if (!string.IsNullOrEmpty(party.activeContractId))
                        {
                            hunter.activeContractId = party.activeContractId;
                        }
                        if (party.status == PartyStatus.Travelling)
                        {
                            hunter.locationId = party.locationId;
                            hunter.destinationId = party.destinationId;
                            hunter.travelDaysRemaining = party.travelDaysRemaining;
                        }
                    }
                }
            }
            foreach (var hunter in state.hunters)
            {
                if (hunter != null && !string.IsNullOrEmpty(hunter.partyId) &&
                    !assignedHunters.Contains(hunter.id))
                {
                    hunter.partyId = "";
                }
            }
        }

        private static void NormalizeContracts(EcosystemWorldState state)
        {
            var missionIds = CollectIds(state.missions, mission => mission.id);
            var guildIds = CollectIds(state.guilds, guild => guild.id);
            var locationIds = CollectIds(state.map.locations, location => location.id);
            var hunterIds = CollectIds(state.hunters, hunter => hunter.id);
            var partyIds = CollectIds(state.parties, party => party.id);
            foreach (var contract in state.contracts)
            {
                contract.offeredDay = Mathf.Max(1, contract.offeredDay);
                contract.expiresDay = Mathf.Max(contract.offeredDay + 1, contract.expiresDay);
                contract.difficulty = Mathf.Max(1, contract.difficulty);
                contract.rewardGold = Mathf.Max(0, contract.rewardGold);
                contract.rewardResources = Mathf.Max(0, contract.rewardResources);
                contract.rewardFame = Mathf.Max(0f, contract.rewardFame);
                if ((contract.status == ContractStatus.Offered ||
                     contract.status == ContractStatus.Accepted ||
                     contract.status == ContractStatus.Active) &&
                    contract.expiresDay < state.day)
                {
                    contract.status = ContractStatus.Expired;
                    contract.resolvedDay = state.day;
                }
                if (!missionIds.Contains(contract.missionTemplateId)) contract.missionTemplateId = "";
                if (!guildIds.Contains(contract.issuerGuildId)) contract.issuerGuildId = "";
                if (!locationIds.Contains(contract.locationId)) contract.locationId = DefaultLocationId;
                if (!locationIds.Contains(contract.targetLocationId)) contract.targetLocationId = contract.locationId;
                if (!hunterIds.Contains(contract.acceptedHunterId)) contract.acceptedHunterId = "";
                if (!partyIds.Contains(contract.acceptedPartyId)) contract.acceptedPartyId = "";

                if (contract.status == ContractStatus.Offered)
                {
                    contract.acceptedHunterId = "";
                    contract.acceptedPartyId = "";
                    contract.startedDay = -1;
                    contract.resolvedDay = -1;
                }
                else if (IsTerminalContractStatus(contract.status))
                {
                    contract.resolvedDay = Mathf.Max(contract.offeredDay, contract.resolvedDay);
                }
            }

            // A contract assignment is represented in three places for fast queries. Rebuild
            // those derived links from the contract lifecycle so partially written or older
            // saves cannot leave parties and hunters working different contracts.
            foreach (var contract in state.contracts)
            {
                if (!IsInProgressContractStatus(contract.status) ||
                    !string.IsNullOrEmpty(contract.acceptedPartyId))
                {
                    continue;
                }

                var linkedParty = state.parties.Find(party =>
                    party != null && party.activeContractId == contract.id);
                if (linkedParty != null)
                {
                    contract.acceptedPartyId = linkedParty.id;
                    continue;
                }

                var acceptedHunter = FindHunter(state, contract.acceptedHunterId);
                var hunterParty = acceptedHunter == null
                    ? null
                    : FindParty(state, acceptedHunter.partyId);
                if (hunterParty != null)
                {
                    contract.acceptedPartyId = hunterParty.id;
                }
            }

            foreach (var party in state.parties)
            {
                party.activeContractId = "";
            }
            foreach (var hunter in state.hunters)
            {
                hunter.activeContractId = "";
            }

            var claimedPartyIds = new HashSet<string>();
            foreach (var contract in state.contracts)
            {
                if (!IsInProgressContractStatus(contract.status))
                {
                    continue;
                }

                var acceptedHunter = FindHunter(state, contract.acceptedHunterId);
                var party = FindParty(state, contract.acceptedPartyId);
                if (party == null && acceptedHunter != null && string.IsNullOrEmpty(acceptedHunter.partyId))
                {
                    party = CreateMigrationParty(state, contract, acceptedHunter, claimedPartyIds);
                    contract.acceptedPartyId = party.id;
                }

                if (party == null || party.memberIds.Count == 0 ||
                    !claimedPartyIds.Add(party.id))
                {
                    ResetContractToOffered(contract);
                    continue;
                }

                if (acceptedHunter == null || !party.memberIds.Contains(acceptedHunter.id))
                {
                    acceptedHunter = FindHunter(state, party.leaderHunterId) ??
                                     FindHunter(state, party.memberIds[0]);
                    contract.acceptedHunterId = acceptedHunter?.id ?? "";
                }
                if (acceptedHunter == null)
                {
                    ResetContractToOffered(contract);
                    claimedPartyIds.Remove(party.id);
                    continue;
                }

                contract.acceptedPartyId = party.id;
                contract.resolvedDay = -1;
                if (contract.status == ContractStatus.Active)
                {
                    contract.startedDay = Mathf.Max(contract.offeredDay, contract.startedDay);
                    party.status = PartyStatus.Active;
                    party.locationId = contract.targetLocationId;
                    party.destinationId = "";
                    party.travelDaysRemaining = 0;
                }
                else
                {
                    contract.startedDay = -1;
                    if (party.status == PartyStatus.Disbanded)
                    {
                        party.status = PartyStatus.Forming;
                    }
                }

                party.activeContractId = contract.id;
                foreach (var memberId in party.memberIds)
                {
                    var member = FindHunter(state, memberId);
                    if (member == null)
                    {
                        continue;
                    }
                    member.partyId = party.id;
                    member.activeContractId = contract.id;
                    if (contract.status == ContractStatus.Active)
                    {
                        member.locationId = contract.targetLocationId;
                        member.destinationId = "";
                        member.travelDaysRemaining = 0;
                    }
                }
            }

            foreach (var party in state.parties)
            {
                if (party.status == PartyStatus.Active && string.IsNullOrEmpty(party.activeContractId))
                {
                    party.status = party.memberIds.Count == 0
                        ? PartyStatus.Disbanded
                        : PartyStatus.Returning;
                }
            }
        }

        /// <summary>
        /// Version-five gates are generated as campaign data, not as a side effect of opening
        /// a dungeon view. That makes the manifest (biome, topology, pods, loot and hazards)
        /// stable for saving, autoresolve, direct play, and a future 3D materializer.
        /// </summary>
        private static void NormalizeGateAndEncounterState(
            EcosystemWorldState state,
            IReadOnlyList<EcosystemGearDefinition> gearCatalog)
        {
            state.gates ??= new List<GateInstanceState>();
            state.encounters ??= new List<DungeonEncounterState>();

            state.gates.RemoveAll(gate => gate == null || string.IsNullOrWhiteSpace(gate.id));
            state.encounters.RemoveAll(encounter =>
                encounter == null || string.IsNullOrWhiteSpace(encounter.id));

            var seenGateIds = new HashSet<string>(StringComparer.Ordinal);
            state.gates.RemoveAll(gate => !seenGateIds.Add(gate.id));
            var seenEncounterIds = new HashSet<string>(StringComparer.Ordinal);
            state.encounters.RemoveAll(encounter => !seenEncounterIds.Add(encounter.id));

            foreach (var contract in state.contracts)
            {
                if (contract == null)
                {
                    continue;
                }

                var isCurrent = contract.status is ContractStatus.Offered or
                    ContractStatus.Accepted or ContractStatus.Active;
                var existingGate = string.IsNullOrEmpty(contract.gateId)
                    ? null
                    : state.gates.Find(gate => gate.id == contract.gateId);
                if (isCurrent || existingGate != null)
                {
                    var gate = EcosystemGateGenerator.EnsureGateForContract(state, contract);
                    gate.lifecycle = EcosystemGateGenerator.LifecycleForContractStatus(
                        contract.status);
                }

                if (contract.status != ContractStatus.Active)
                {
                    continue;
                }

                var party = FindParty(state, contract.acceptedPartyId);
                if (party == null || party.memberIds == null || party.memberIds.Count == 0)
                {
                    continue;
                }
                EcosystemGateGenerator.EnsureEncounterForContract(
                    state,
                    contract,
                    party,
                    gearCatalog);
            }

            state.gates.Sort((left, right) => string.CompareOrdinal(left.id, right.id));
            state.encounters.Sort((left, right) => string.CompareOrdinal(left.id, right.id));

            var encounterIds = new HashSet<string>(
                state.encounters.Select(encounter => encounter.id),
                StringComparer.Ordinal);
            foreach (var hunter in state.hunters)
            {
                if (hunter == null)
                {
                    continue;
                }
                if (!string.IsNullOrEmpty(hunter.currentEncounterId) &&
                    !encounterIds.Contains(hunter.currentEncounterId))
                {
                    hunter.currentEncounterId = string.Empty;
                    hunter.isIncapacitated = false;
                }
            }
        }

        private static void EnsureActiveExpiringContract(EcosystemWorldState state)
        {
            foreach (var contract in state.contracts)
            {
                if (contract != null &&
                    contract.status == ContractStatus.Active &&
                    contract.expiresDay >= state.day)
                {
                    return;
                }
            }

            var participant = FindHunter(state, state.playerHunterId);
            if (participant == null || !participant.IsActive ||
                !string.IsNullOrEmpty(participant.activeContractId))
            {
                participant = state.hunters.Find(hunter =>
                    hunter != null && hunter.IsActive && string.IsNullOrEmpty(hunter.activeContractId));
            }
            if (participant == null)
            {
                return;
            }

            var party = FindParty(state, participant.partyId);
            if (party != null && !string.IsNullOrEmpty(party.activeContractId))
            {
                party = null;
            }
            if (party == null)
            {
                party = CreateMigrationParty(state, null, participant, null);
            }

            var id = $"contract-frontier-renewal-{state.day}";
            var renewal = FindContract(state, id);
            if (renewal == null)
            {
                renewal = new ContractState
                {
                    id = id,
                    displayName = "Emergency Ash-Tunnel Sweep",
                    contractType = ContractType.DungeonClear,
                    missionTemplateId = "mission-goblin",
                    issuerGuildId = "guild-azure",
                    locationId = DefaultLocationId,
                    targetLocationId = "dungeon-ash-tunnel",
                    difficulty = 1,
                    rewardGold = 48,
                    rewardResources = 14,
                    rewardFame = 2f
                };
                state.contracts.Add(renewal);
            }
            renewal.status = ContractStatus.Active;
            renewal.offeredDay = state.day;
            renewal.expiresDay = state.day + 4;
            renewal.startedDay = state.day;
            renewal.resolvedDay = -1;
            renewal.acceptedHunterId = participant.id;
            renewal.acceptedPartyId = party.id;
            party.status = PartyStatus.Active;
            party.activeContractId = renewal.id;
            party.locationId = renewal.targetLocationId;
            party.destinationId = "";
            party.travelDaysRemaining = 0;
            foreach (var memberId in party.memberIds)
            {
                var member = FindHunter(state, memberId);
                if (member != null)
                {
                    member.activeContractId = renewal.id;
                    member.locationId = renewal.targetLocationId;
                    member.destinationId = "";
                    member.travelDaysRemaining = 0;
                }
            }
        }

        private static PartyState CreateMigrationParty(
            EcosystemWorldState state,
            ContractState contract,
            HunterProfile leader,
            HashSet<string> reservedPartyIds)
        {
            var stem = contract == null
                ? $"party-frontier-renewal-{leader.id}"
                : $"party-contract-{contract.id}";
            var id = stem;
            var suffix = 1;
            while (FindParty(state, id) != null ||
                   (reservedPartyIds != null && reservedPartyIds.Contains(id)))
            {
                id = $"{stem}-{suffix++}";
            }

            var party = new PartyState
            {
                id = id,
                displayName = $"{leader.displayName}'s Party",
                leaderHunterId = leader.id,
                memberIds = new List<string> { leader.id },
                status = contract?.status == ContractStatus.Active
                    ? PartyStatus.Active
                    : PartyStatus.Forming,
                locationId = leader.locationId,
                createdDay = state.day
            };
            state.parties.Add(party);
            leader.partyId = party.id;
            return party;
        }

        private static void ResetContractToOffered(ContractState contract)
        {
            contract.status = ContractStatus.Offered;
            contract.acceptedHunterId = "";
            contract.acceptedPartyId = "";
            contract.startedDay = -1;
            contract.resolvedDay = -1;
        }

        private static bool IsInProgressContractStatus(ContractStatus status)
        {
            return status == ContractStatus.Accepted || status == ContractStatus.Active;
        }

        private static bool IsTerminalContractStatus(ContractStatus status)
        {
            return status == ContractStatus.Succeeded ||
                   status == ContractStatus.Failed ||
                   status == ContractStatus.Expired ||
                   status == ContractStatus.Cancelled;
        }

        private static void NormalizeInvitations(EcosystemWorldState state)
        {
            var hunterIds = CollectIds(state.hunters, hunter => hunter.id);
            var guildIds = CollectIds(state.guilds, guild => guild.id);
            var partyIds = CollectIds(state.parties, party => party.id);
            var contractIds = CollectIds(state.contracts, contract => contract.id);
            foreach (var invitation in state.invitations)
            {
                invitation.createdDay = Mathf.Max(1, invitation.createdDay);
                invitation.expiresDay = Mathf.Max(invitation.createdDay + 1, invitation.expiresDay);
                if (!hunterIds.Contains(invitation.senderHunterId)) invitation.senderHunterId = "";
                if (!hunterIds.Contains(invitation.recipientHunterId)) invitation.recipientHunterId = "";
                if (!guildIds.Contains(invitation.guildId)) invitation.guildId = "";
                if (!partyIds.Contains(invitation.partyId)) invitation.partyId = "";
                if (!contractIds.Contains(invitation.contractId)) invitation.contractId = "";
            }
        }

        private static void NormalizeRoutes(EcosystemWorldState state)
        {
            var locationIds = CollectIds(state.map.locations, location => location.id);
            var keys = new HashSet<string>();
            for (var index = state.map.routes.Count - 1; index >= 0; index--)
            {
                var route = state.map.routes[index];
                if (route == null ||
                    route.fromLocationId == route.toLocationId ||
                    !locationIds.Contains(route.fromLocationId) ||
                    !locationIds.Contains(route.toLocationId))
                {
                    state.map.routes.RemoveAt(index);
                    continue;
                }
                route.travelDays = Mathf.Max(1, route.travelDays);
                route.danger = Mathf.Max(0, route.danger);
                var key = string.CompareOrdinal(route.fromLocationId, route.toLocationId) < 0
                    ? route.fromLocationId + "|" + route.toLocationId
                    : route.toLocationId + "|" + route.fromLocationId;
                if (!keys.Add(key))
                {
                    state.map.routes.RemoveAt(index);
                }
            }
            AddDefaultRoutes(state);
        }

        private static void NormalizeDecisionHistory(EcosystemWorldState state)
        {
            var usedIds = new HashSet<string>();
            var nextSequence = 1L;
            for (var index = 0; index < state.decisionRecords.Count; index++)
            {
                var record = state.decisionRecords[index];
                if (record == null)
                {
                    state.decisionRecords.RemoveAt(index);
                    index--;
                    continue;
                }
                record.factors ??= new List<DecisionFactor>();
                record.day = Mathf.Max(1, record.day);
                if (record.sequence < nextSequence)
                {
                    record.sequence = nextSequence;
                }
                nextSequence = record.sequence + 1L;

                if (string.IsNullOrWhiteSpace(record.decisionId) || !usedIds.Add(record.decisionId))
                {
                    var baseId = $"decision-{record.day:D3}-{record.sequence:D7}";
                    var repairedId = baseId;
                    var suffix = 1;
                    while (usedIds.Contains(repairedId))
                    {
                        repairedId = $"{baseId}-{suffix++}";
                    }
                    record.decisionId = repairedId;
                    usedIds.Add(repairedId);
                }
            }
        }

        private static void NormalizeStructuredEvents(EcosystemWorldState state)
        {
            var usedIds = new HashSet<string>();
            for (var index = state.structuredEvents.Count - 1; index >= 0; index--)
            {
                var worldEvent = state.structuredEvents[index];
                if (worldEvent == null)
                {
                    state.structuredEvents.RemoveAt(index);
                    continue;
                }
                worldEvent.day = Mathf.Max(1, worldEvent.day);
                if (string.IsNullOrWhiteSpace(worldEvent.id) || !usedIds.Add(worldEvent.id))
                {
                    var ordinal = index + 1;
                    var migratedId = $"event-migrated-{ordinal:0000}";
                    var suffix = 1;
                    while (usedIds.Contains(migratedId))
                    {
                        migratedId = $"event-migrated-{ordinal:0000}-{suffix++}";
                    }
                    worldEvent.id = migratedId;
                    usedIds.Add(worldEvent.id);
                }
            }
        }

        private static string ResolveHomeLocation(EcosystemWorldState state, string guildId)
        {
            return FindGuild(state, guildId)?.homeLocationId ?? DefaultLocationId;
        }

        private static void DeduplicateStrings(List<string> values)
        {
            var seen = new HashSet<string>();
            for (var index = values.Count - 1; index >= 0; index--)
            {
                var value = values[index];
                if (string.IsNullOrWhiteSpace(value) || !seen.Add(value))
                {
                    values.RemoveAt(index);
                }
            }
        }

        private static float StrongerMagnitude(float first, float second)
        {
            return Mathf.Abs(second) > Mathf.Abs(first) ? second : first;
        }

        private static float StableUnit(int seed, string value)
        {
            unchecked
            {
                uint hash = (uint)seed;
                foreach (var character in value)
                {
                    hash ^= character;
                    hash *= 16777619u;
                }
                return (hash & 0x00ffffffu) / 16777215f;
            }
        }

        private static float StableSignedUnit(int seed, string value)
        {
            return StableUnit(seed, value) * 2f - 1f;
        }

        private static string GearIdAt(
            IReadOnlyList<EcosystemGearDefinition> gearCatalog,
            int index)
        {
            if (gearCatalog != null && index >= 0 && index < gearCatalog.Count)
            {
                var gear = gearCatalog[index];
                if (gear != null && !string.IsNullOrWhiteSpace(gear.GearId))
                {
                    return gear.GearId;
                }
            }
            return FallbackGearIds[Mathf.Clamp(index, 0, FallbackGearIds.Length - 1)];
        }

        internal static int StartingShieldForGear(
            IReadOnlyList<EcosystemGearDefinition> gearCatalog,
            string gearId)
        {
            if (gearCatalog == null || string.IsNullOrEmpty(gearId))
            {
                return 0;
            }

            foreach (var gear in gearCatalog)
            {
                if (gear == null || gear.GearId != gearId)
                {
                    continue;
                }
                foreach (var move in gear.GrantedMoves)
                {
                    if (!string.IsNullOrEmpty(move) &&
                        (move.EndsWith("_guard", StringComparison.Ordinal) ||
                         move.Contains("barrier", StringComparison.OrdinalIgnoreCase) ||
                         move.Contains("shield", StringComparison.OrdinalIgnoreCase)))
                    {
                        return 35;
                    }
                }
                return 0;
            }
            return 0;
        }

        private static HunterProfile FindHunter(EcosystemWorldState state, string id)
        {
            return string.IsNullOrEmpty(id) ? null : state.hunters.Find(item => item.id == id);
        }

        private static GuildState FindGuild(EcosystemWorldState state, string id)
        {
            return string.IsNullOrEmpty(id) ? null : state.guilds.Find(item => item.id == id);
        }

        private static LocationState FindLocation(EcosystemWorldState state, string id)
        {
            return string.IsNullOrEmpty(id) ? null : state.map.locations.Find(item => item.id == id);
        }

        private static ContractState FindContract(EcosystemWorldState state, string id)
        {
            return string.IsNullOrEmpty(id) ? null : state.contracts.Find(item => item.id == id);
        }

        private static PartyState FindParty(EcosystemWorldState state, string id)
        {
            return string.IsNullOrEmpty(id) ? null : state.parties.Find(item => item.id == id);
        }

        private static InvitationState FindInvitation(EcosystemWorldState state, string id)
        {
            return string.IsNullOrEmpty(id) ? null : state.invitations.Find(item => item.id == id);
        }

        private static HashSet<string> CollectIds<T>(List<T> items, Func<T, string> getId)
            where T : class
        {
            var ids = new HashSet<string>();
            if (items == null)
            {
                return ids;
            }
            foreach (var item in items)
            {
                if (item == null)
                {
                    continue;
                }
                var id = getId(item);
                if (!string.IsNullOrWhiteSpace(id))
                {
                    ids.Add(id);
                }
            }
            return ids;
        }

        private static void ValidateUniqueIds<T>(
            List<T> items,
            Func<T, string> getId,
            string label,
            List<string> errors)
            where T : class
        {
            if (items == null)
            {
                errors.Add($"{label} collection is null.");
                return;
            }
            var ids = new HashSet<string>();
            foreach (var item in items)
            {
                var id = item == null ? null : getId(item);
                if (string.IsNullOrWhiteSpace(id))
                {
                    errors.Add($"{label} has an empty ID.");
                }
                else if (!ids.Add(id))
                {
                    errors.Add($"Duplicate {label} ID '{id}'.");
                }
            }
        }

        private static void ValidateReferences(
            List<string> references,
            HashSet<string> validIds,
            string label,
            List<string> errors)
        {
            if (references == null)
            {
                errors.Add($"{label} collection is null.");
                return;
            }
            var seen = new HashSet<string>();
            foreach (var reference in references)
            {
                if (!validIds.Contains(reference))
                {
                    errors.Add($"{label} references missing ID '{reference}'.");
                }
                else if (!seen.Add(reference))
                {
                    errors.Add($"{label} contains duplicate ID '{reference}'.");
                }
            }
        }

        private static void ValidateHunterRelationships(
            HunterProfile hunter,
            HashSet<string> hunterIds,
            List<string> errors)
        {
            if (hunter.relationships == null)
            {
                errors.Add($"Hunter '{hunter.id}' relationship collection is null.");
                return;
            }
            var seen = new HashSet<string>();
            foreach (var relationship in hunter.relationships)
            {
                if (relationship == null || !hunterIds.Contains(relationship.hunterId))
                {
                    errors.Add($"Hunter '{hunter.id}' has a relationship with a missing hunter.");
                }
                else if (relationship.hunterId == hunter.id)
                {
                    errors.Add($"Hunter '{hunter.id}' has a self relationship.");
                }
                else if (!seen.Add(relationship.hunterId))
                {
                    errors.Add($"Hunter '{hunter.id}' has duplicate relationships with '{relationship.hunterId}'.");
                }
            }
        }
    }
}
