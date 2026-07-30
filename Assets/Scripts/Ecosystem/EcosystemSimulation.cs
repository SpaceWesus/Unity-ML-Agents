using System;
using System.Collections.Generic;
using System.Linq;

namespace Turtle.Ecosystem
{
    /// <summary>
    /// Advances the coarse campaign clock. Human and autonomous choices both pass through
    /// the same EcosystemActionService; only command selection differs.
    /// </summary>
    public sealed class EcosystemSimulation
    {
        private const int MinimumOpenContractOffers = 5;
        private const int DesiredConcurrentContracts = 10;
        private const int MaximumConcurrentContracts = 15;
        private const int MaximumDecisionRecords = 180;
        private const int MaximumStructuredEvents = 320;
        private const int MaximumDisplayEvents = 90;
        private const int MaximumCareerActionsPerHunterPerDay = 8;
        public const int EncounterFixedStepsPerCampaignDay = 100;

        private readonly EcosystemWorldState state;
        private readonly IReadOnlyList<EcosystemGearDefinition> gearCatalog;
        private readonly EcosystemDeterministicRandom random;
        private readonly EcosystemActionService actions;
        private readonly EcosystemDecisionSystem decisions;
        private readonly EcosystemEncounterSimulation encounterSimulation;

        public EcosystemSimulation(
            EcosystemWorldState worldState,
            IReadOnlyList<EcosystemGearDefinition> availableGear)
        {
            state = worldState ?? throw new ArgumentNullException(nameof(worldState));
            gearCatalog = availableGear ?? Array.Empty<EcosystemGearDefinition>();
            random = new EcosystemDeterministicRandom(state);
            actions = new EcosystemActionService(state, gearCatalog);
            decisions = new EcosystemDecisionSystem(state, gearCatalog, actions);
            encounterSimulation = new EcosystemEncounterSimulation(state, gearCatalog);
        }

        public EcosystemActionService Actions => actions;
        public EcosystemDecisionSystem Decisions => decisions;
        public EcosystemEncounterSimulation Encounters => encounterSimulation;

        public EcosystemActionResult ExecutePlayerAction(EcosystemActionRequest request)
        {
            var result = actions.Execute(request);
            var record = decisions.Evaluate(request);
            record.selected = result.success;
            record.executable = result.success;
            if (!result.success)
            {
                record.rejectionReason = $"{result.reasonCode}: {result.summary}";
                record.finalExplanation = result.summary;
            }
            AppendDecision(record);
            return result;
        }

        public void AdvanceDays(int count, bool advanceEncounterClock = true)
        {
            for (var index = 0; index < Math.Max(0, count); index++)
            {
                AdvanceDay(advanceEncounterClock);
            }
        }

        public void AdvanceDay(bool advanceEncounterClock = true)
        {
            state.day++;
            ExpireOffersAndInvitations();
            AdvancePopulationLifecycle();
            AwardControlledResourceIncome();
            MaintainContractBoard();

            var autonomousHunters = state.hunters
                .Where(hunter => hunter != null && hunter.IsActive && hunter.id != state.playerHunterId)
                .OrderBy(hunter => hunter.id, StringComparer.Ordinal)
                .ToArray();

            foreach (var hunter in autonomousHunters)
            {
                ResolveCareerPlan(hunter);
                var choice = decisions.ChooseAction(hunter);
                var result = actions.Execute(choice.request);
                choice.selected.selected = result.success;
                foreach (var alternative in choice.alternatives)
                {
                    AppendDecision(alternative);
                }
                AppendDecision(choice.selected);
            }

            actions.AdvanceTravelOneDay();
            if (advanceEncounterClock)
            {
                encounterSimulation.AdvanceAllActive(EncounterFixedStepsPerCampaignDay);
            }
            actions.ResolveActiveContracts();
            EnsureMinimumActivePopulation();
            MaintainContractBoard();
            RecordWorldEvent(
                WorldEventType.SimulationAdvanced,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                1f,
                $"DAY {state.day}: The guild ecosystem advanced; contracts, parties, and grudges kept moving.");
            TrimHistory();
        }

        /// <summary>
        /// Advances the canonical dungeon clock without advancing the campaign day. Runtime
        /// presentation calls this fixed-step API; tests and fast-forward use the same rules.
        /// </summary>
        public void AdvanceEncounterSteps(
            int fixedSteps,
            EncounterIntentOverride intentOverride = null)
        {
            encounterSimulation.AdvanceAllActive(fixedSteps, intentOverride);
            actions.ResolveActiveContracts();
        }

        private void ResolveCareerPlan(HunterProfile hunter)
        {
            for (var iteration = 0;
                 iteration < MaximumCareerActionsPerHunterPerDay;
                 iteration++)
            {
                var choice = decisions.ChooseCareerAction(hunter);
                foreach (var alternative in choice.alternatives)
                {
                    AppendDecision(alternative);
                }
                if (choice.request == null || choice.selected == null)
                {
                    return;
                }

                var result = actions.Execute(choice.request);
                choice.selected.selected = result.success;
                choice.selected.executable = result.success;
                if (!result.success)
                {
                    choice.selected.rejectionReason = $"{result.reasonCode}: {result.summary}";
                    choice.selected.finalExplanation = result.summary;
                }
                AppendDecision(choice.selected);

                // Saving is a deliberate terminal choice for this planning phase. Failed
                // requests also stop the loop so invalid content cannot spin repeatedly.
                if (!result.success ||
                    choice.request.actionType == HunterActionType.SaveAbilityPoints)
                {
                    return;
                }
            }
        }

        private void ExpireOffersAndInvitations()
        {
            foreach (var contract in state.contracts)
            {
                if (contract == null ||
                    (contract.status != ContractStatus.Offered &&
                     contract.status != ContractStatus.Accepted) ||
                    contract.expiresDay >= state.day)
                {
                    continue;
                }

                actions.ExpireContract(contract);
            }

            foreach (var invitation in state.invitations)
            {
                if (invitation == null || invitation.status != InvitationStatus.Pending ||
                    invitation.expiresDay >= state.day)
                {
                    continue;
                }

                invitation.status = InvitationStatus.Expired;
                RecordWorldEvent(
                    WorldEventType.PartyChanged,
                    invitation.senderHunterId,
                    invitation.recipientHunterId,
                    invitation.guildId,
                    string.Empty,
                    invitation.contractId,
                    -0.1f,
                    $"DAY {state.day}: An invitation to {HunterName(invitation.recipientHunterId)} expired.");
            }
        }

        private void AwardControlledResourceIncome()
        {
            foreach (var location in state.map.locations)
            {
                if (location == null || location.locationType != LocationType.ResourceSite ||
                    string.IsNullOrEmpty(location.controllingGuildId) || location.resourceYield <= 0)
                {
                    continue;
                }

                var guild = state.guilds.Find(item => item.id == location.controllingGuildId);
                if (guild == null)
                {
                    continue;
                }

                guild.resources += location.resourceYield;
                guild.prestige += location.resourceYield * 0.02f;
            }
        }

        private void MaintainContractBoard()
        {
            var offeredCount = state.contracts.Count(contract =>
                contract != null && contract.status == ContractStatus.Offered &&
                contract.expiresDay >= state.day);
            var concurrentCount = state.contracts.Count(IsConcurrentContract);
            while (concurrentCount < MaximumConcurrentContracts &&
                   (concurrentCount < DesiredConcurrentContracts ||
                    offeredCount < MinimumOpenContractOffers) &&
                   TryPostContract())
            {
                offeredCount++;
                concurrentCount++;
            }

            var removable = state.contracts
                .Where(contract => contract != null &&
                    contract.resolvedDay > 0 && contract.resolvedDay < state.day - 21 &&
                    !state.encounters.Any(encounter =>
                        encounter != null && encounter.contractId == contract.id) &&
                    contract.status is ContractStatus.Succeeded or ContractStatus.Failed or
                        ContractStatus.Expired or ContractStatus.Cancelled)
                .OrderBy(contract => contract.resolvedDay)
                .ToArray();
            foreach (var contract in removable)
            {
                if (state.contracts.Count <= 36)
                {
                    break;
                }
                state.contracts.Remove(contract);
            }
        }

        private void AdvancePopulationLifecycle()
        {
            if (ActiveHunterCount() >= EcosystemWorldFactory.MaximumActiveHunterCount)
            {
                TryRetireOneHunter();
            }

            if (ActiveHunterCount() < EcosystemWorldFactory.MaximumActiveHunterCount)
            {
                AddAwakening("daily awakening");
            }
            EnsureMinimumActivePopulation();
        }

        private void EnsureMinimumActivePopulation()
        {
            while (ActiveHunterCount() < EcosystemWorldFactory.MinimumActiveHunterCount)
            {
                if (AddAwakening("emergency Association recruitment") == null)
                {
                    return;
                }
            }
        }

        private HunterProfile AddAwakening(string source)
        {
            if (ActiveHunterCount() >= EcosystemWorldFactory.MaximumActiveHunterCount)
            {
                return null;
            }
            var hunter = EcosystemWorldFactory.AddAwakenedHunter(
                state,
                gearCatalog,
                state.day);
            if (hunter == null)
            {
                return null;
            }
            RecordWorldEvent(
                WorldEventType.HunterAwakened,
                hunter.id,
                string.Empty,
                hunter.guildId,
                hunter.locationId,
                string.Empty,
                EcosystemCareerRules.InvestedAbilityPoints(hunter.career),
                $"DAY {state.day}: {hunter.displayName} awakened and registered through {source}.");
            return hunter;
        }

        private bool TryRetireOneHunter()
        {
            var candidates = state.hunters
                .Where(hunter => hunter != null && hunter.IsActive &&
                                 hunter.id != state.playerHunterId &&
                                 string.IsNullOrEmpty(hunter.activeContractId) &&
                                 hunter.travelDaysRemaining == 0 &&
                                 state.day - hunter.awakeningDay >= 14)
                .OrderByDescending(RetirementReadiness)
                .ThenBy(hunter => hunter.id, StringComparer.Ordinal)
                .ToArray();
            foreach (var candidate in candidates)
            {
                var request = new EcosystemActionRequest(
                    HunterActionType.Retire,
                    candidate.id);
                var record = decisions.Evaluate(request);
                var readiness = RetirementReadiness(candidate);
                record.factors.Add(new DecisionFactor
                {
                    key = "population_retirement_pressure",
                    rawValue = readiness,
                    weight = 0.1f,
                    contribution = readiness * 0.1f,
                    explanation = "Service time, injuries, low ambition, and population pressure favor retirement."
                });
                record.totalScore += readiness * 0.1f;
                var result = actions.Execute(request);
                record.selected = result.success;
                record.executable = result.success;
                record.finalExplanation = result.success
                    ? $"Retirement utility {record.totalScore:0.000}; {result.summary}"
                    : result.summary;
                AppendDecision(record);
                if (result.success)
                {
                    return true;
                }
            }
            return false;
        }

        private float RetirementReadiness(HunterProfile hunter)
        {
            var serviceDays = Math.Max(0, state.day - hunter.awakeningDay);
            var unresolvedInjuries = hunter.injuries?.Count(injury =>
                injury != null && !injury.healed) ?? 0;
            var inclination = EcosystemDeterministicRandom.StableHash(
                $"{state.worldSeed}|retirement|{state.day}|{hunter.id}") % 1001u / 1000f;
            return serviceDays * 0.015f + hunter.wounds * 0.65f +
                   unresolvedInjuries * 0.9f + (1f - hunter.ambition) * 0.8f +
                   inclination * 0.25f;
        }

        private int ActiveHunterCount() => state.hunters.Count(hunter =>
            hunter != null && hunter.IsActive);

        private bool IsConcurrentContract(ContractState contract)
        {
            return contract != null && contract.expiresDay >= state.day &&
                   (contract.status is ContractStatus.Offered or
                       ContractStatus.Accepted or ContractStatus.Active);
        }

        private bool TryPostContract()
        {
            var dungeons = state.map.locations
                .Where(location => location != null && location.locationType == LocationType.Dungeon)
                .ToArray();
            if (dungeons.Length == 0 || state.guilds.Count == 0 || state.missions.Count == 0)
            {
                return false;
            }

            var dungeon = dungeons[random.Range(0, dungeons.Length, "contract-dungeon")];
            var guild = state.guilds[random.Range(0, state.guilds.Count, "contract-guild")];
            var mission = state.missions[random.Range(0, state.missions.Count, "contract-template")];
            var postingLocation = state.map.locations.Find(location =>
                                      location != null && location.id == guild.homeLocationId &&
                                      location.locationType == LocationType.Town) ??
                                  state.map.locations.FirstOrDefault(location =>
                                      location != null && location.locationType == LocationType.Town);
            var difficulty = Math.Max(1, dungeon.danger + random.Range(-1, 2, "contract-difficulty"));
            var contract = new ContractState
            {
                id = $"contract-{state.day:D3}-{state.simulationSequence:D7}",
                displayName = $"{mission.displayName}: {dungeon.displayName}",
                contractType = ContractType.DungeonClear,
                status = ContractStatus.Offered,
                missionTemplateId = mission.id,
                issuerGuildId = guild.id,
                locationId = postingLocation?.id ?? dungeon.id,
                targetLocationId = dungeon.id,
                offeredDay = state.day,
                expiresDay = state.day + random.Range(3, 7, "contract-expiry"),
                resolvedDay = -1,
                difficulty = difficulty,
                rewardGold = 24 + difficulty * 18,
                rewardResources = 8 + difficulty * 7,
                rewardFame = 1.5f + difficulty * 1.25f
            };
            state.contracts.Add(contract);
            var gate = EcosystemGateGenerator.EnsureGateForContract(state, contract);
            RecordWorldEvent(
                WorldEventType.ContractChanged,
                string.Empty,
                string.Empty,
                guild.id,
                dungeon.id,
                contract.id,
                contract.rewardGold,
                $"DAY {state.day}: {guild.displayName} posted {contract.displayName}; " +
                $"the {gate.biome} gate uses a {gate.layoutStyle} layout and expires day " +
                $"{contract.expiresDay}.");
            return true;
        }

        private void AppendDecision(HunterDecisionRecord record)
        {
            if (record == null)
            {
                return;
            }

            var latestSequence = state.decisionRecords.Count == 0
                ? 0L
                : state.decisionRecords[^1].sequence;
            record.sequence = latestSequence + 1L;
            record.decisionId = $"decision-{state.day:D3}-{record.sequence:D7}";
            state.decisionRecords.Add(record);
            while (state.decisionRecords.Count > MaximumDecisionRecords)
            {
                state.decisionRecords.RemoveAt(0);
            }
        }

        private void RecordWorldEvent(
            WorldEventType eventType,
            string actorId,
            string targetId,
            string guildId,
            string locationId,
            string contractId,
            float magnitude,
            string summary)
        {
            var latestSequence = state.structuredEvents.Count == 0
                ? 0L
                : state.structuredEvents[^1].sequence;
            state.structuredEvents.Add(new StructuredWorldEvent
            {
                id = $"event-{state.day:D3}-{latestSequence + 1:D7}",
                sequence = latestSequence + 1L,
                day = state.day,
                eventType = eventType,
                actorHunterId = actorId,
                targetHunterId = targetId,
                guildId = guildId,
                locationId = locationId,
                contractId = contractId,
                magnitude = magnitude,
                summary = summary
            });
            state.eventLog.Add(summary);
        }

        private void TrimHistory()
        {
            while (state.eventLog.Count > MaximumDisplayEvents)
            {
                state.eventLog.RemoveAt(0);
            }
            while (state.structuredEvents.Count > MaximumStructuredEvents)
            {
                state.structuredEvents.RemoveAt(0);
            }
        }

        private string HunterName(string hunterId) =>
            state.hunters.Find(hunter => hunter.id == hunterId)?.displayName ?? "a hunter";
    }
}
