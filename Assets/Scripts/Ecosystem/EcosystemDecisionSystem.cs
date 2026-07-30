using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Turtle.Ecosystem
{
    public sealed class EcosystemDecisionChoice
    {
        public EcosystemActionRequest request;
        public HunterDecisionRecord selected;
        public List<HunterDecisionRecord> alternatives = new();
    }

    /// <summary>
    /// Pure, inspectable utility evaluation. It chooses commands but never mutates the world;
    /// chosen commands are still validated and executed by EcosystemActionService.
    /// </summary>
    public sealed class EcosystemDecisionSystem
    {
        private readonly EcosystemWorldState state;
        private readonly IReadOnlyList<EcosystemGearDefinition> gearCatalog;
        private readonly EcosystemActionService actions;

        public EcosystemDecisionSystem(
            EcosystemWorldState worldState,
            IReadOnlyList<EcosystemGearDefinition> availableGear,
            EcosystemActionService actionService)
        {
            state = worldState;
            gearCatalog = availableGear ?? Array.Empty<EcosystemGearDefinition>();
            actions = actionService;
        }

        public EcosystemDecisionChoice ChooseAction(HunterProfile hunter)
        {
            var scored = new List<(EcosystemActionRequest request, HunterDecisionRecord record)>();
            var rejected = new List<HunterDecisionRecord>();
            var seenCandidates = new HashSet<string>(StringComparer.Ordinal);
            foreach (var candidate in BuildCandidates(hunter))
            {
                if (!seenCandidates.Add(Signature(candidate)))
                {
                    continue;
                }

                var record = Evaluate(candidate);
                if (!actions.CanExecute(candidate, out var rejectionReason))
                {
                    record.executable = false;
                    record.rejectionReason = rejectionReason;
                    record.finalExplanation = $"Rejected before selection: {rejectionReason}";
                    rejected.Add(record);
                    continue;
                }

                record.executable = true;
                scored.Add((candidate, record));
            }

            scored.Sort((left, right) =>
            {
                var byScore = right.record.totalScore.CompareTo(left.record.totalScore);
                return byScore != 0
                    ? byScore
                    : string.CompareOrdinal(Signature(left.request), Signature(right.request));
            });

            if (scored.Count == 0)
            {
                var wait = new EcosystemActionRequest(HunterActionType.Wait, hunter.id);
                scored.Add((wait, Evaluate(wait)));
            }

            var choice = new EcosystemDecisionChoice
            {
                request = scored[0].request,
                selected = scored[0].record
            };
            choice.selected.selected = true;
            choice.selected.finalExplanation =
                $"Selected as the highest executable utility at {choice.selected.totalScore:0.000}.";
            if (scored.Count > 1 &&
                Mathf.Approximately(scored[0].record.totalScore, scored[1].record.totalScore))
            {
                choice.selected.tieBreakExplanation =
                    $"The utility score tied; '{Signature(scored[0].request)}' won the stable ordinal tie-break.";
            }
            for (var index = 1; index < Mathf.Min(4, scored.Count); index++)
            {
                scored[index].record.finalExplanation =
                    $"Executable alternative scored {scored[index].record.totalScore:0.000}, " +
                    $"below the selected {choice.selected.totalScore:0.000}.";
                choice.alternatives.Add(scored[index].record);
            }
            choice.alternatives.AddRange(rejected);

            return choice;
        }

        /// <summary>
        /// Chooses a zero-time career or loadout command independently from the hunter's
        /// ordinary daily world action. A null selected record means no executable career
        /// change remains; rejected alternatives are retained for developer inspection.
        /// </summary>
        public EcosystemDecisionChoice ChooseCareerAction(HunterProfile hunter)
        {
            var choice = new EcosystemDecisionChoice();
            var scored = new List<(EcosystemActionRequest request, HunterDecisionRecord record)>();
            var seenCandidates = new HashSet<string>(StringComparer.Ordinal);
            foreach (var candidate in BuildCareerCandidates(hunter))
            {
                if (!seenCandidates.Add(Signature(candidate))) continue;
                var record = Evaluate(candidate);
                if (!actions.CanExecute(candidate, out var rejectionReason))
                {
                    record.executable = false;
                    record.rejectionReason = rejectionReason;
                    record.finalExplanation = $"Rejected before career selection: {rejectionReason}";
                    choice.alternatives.Add(record);
                    continue;
                }
                record.executable = true;
                scored.Add((candidate, record));
            }

            scored.Sort((left, right) =>
            {
                var byScore = right.record.totalScore.CompareTo(left.record.totalScore);
                return byScore != 0
                    ? byScore
                    : string.CompareOrdinal(Signature(left.request), Signature(right.request));
            });
            if (scored.Count == 0)
            {
                return choice;
            }

            choice.request = scored[0].request;
            choice.selected = scored[0].record;
            choice.selected.selected = true;
            choice.selected.finalExplanation =
                $"Selected as the highest executable career utility at {choice.selected.totalScore:0.000}.";
            if (scored.Count > 1 &&
                Mathf.Approximately(scored[0].record.totalScore, scored[1].record.totalScore))
            {
                choice.selected.tieBreakExplanation =
                    $"The career utility tied; '{Signature(scored[0].request)}' won the stable ordinal tie-break.";
            }
            for (var index = 1; index < Mathf.Min(4, scored.Count); index++)
            {
                scored[index].record.finalExplanation =
                    $"Executable career alternative scored {scored[index].record.totalScore:0.000}, " +
                    $"below the selected {choice.selected.totalScore:0.000}.";
                choice.alternatives.Add(scored[index].record);
            }
            return choice;
        }

        public HunterDecisionRecord Evaluate(EcosystemActionRequest request)
        {
            var hunter = FindHunter(request.actorHunterId);
            var record = new HunterDecisionRecord
            {
                decisionId = DecisionId(request),
                day = state.day,
                hunterId = request.actorHunterId,
                category = Category(request.actionType),
                actionType = request.actionType,
                targetId = PrimaryTarget(request),
                executable = true,
                tieBreakExplanation =
                    "Equal utility scores are ordered by the request's stable ordinal signature."
            };

            if (hunter == null)
            {
                Add(record, "invalid_actor", -10f, 1f, "The acting hunter no longer exists.");
                Finalize(record);
                return record;
            }

            Add(record, "base_desire", BaseDesire(request.actionType), 1f,
                $"Baseline desire to {Humanize(request.actionType)}.");
            AddPersonalityFactors(record, request, hunter);
            AddRelationshipFactors(record, request, hunter);
            AddContextFactors(record, request, hunter);

            var inclination =
                (EcosystemDeterministicRandom.StableHash(hunter.id + Signature(request)) % 2001u) /
                1000f - 1f;
            Add(record, "stable_inclination", inclination, 0.035f,
                "A stable personal inclination keeps equally sensible hunters distinct.");
            Finalize(record);
            record.finalExplanation =
                $"Utility {record.totalScore:0.000} from {record.factors.Count} inspectable factors.";
            return record;
        }

        private IEnumerable<EcosystemActionRequest> BuildCandidates(HunterProfile hunter)
        {
            if (hunter == null || !hunter.IsActive)
            {
                yield break;
            }

            var party = FindParty(hunter.partyId);
            if (hunter.travelDaysRemaining > 0 ||
                (party != null && party.status == PartyStatus.Travelling))
            {
                yield return Request(HunterActionType.Wait, hunter.id);
                yield break;
            }

            foreach (var invitation in state.invitations)
            {
                if (invitation.status != InvitationStatus.Pending ||
                    invitation.recipientHunterId != hunter.id ||
                    invitation.expiresDay < state.day)
                {
                    continue;
                }

                yield return Request(HunterActionType.AcceptInvitation, hunter.id,
                    invitationId: invitation.id);
                yield return Request(HunterActionType.RejectInvitation, hunter.id,
                    invitationId: invitation.id);
            }

            if (hunter.wounds > 0 || hunter.injuries.Any(injury => injury != null && !injury.healed))
            {
                yield return Request(HunterActionType.Recover, hunter.id);
            }

            if (hunter.pendingRewardGold > 0)
            {
                yield return Request(HunterActionType.ClaimReward, hunter.id);
            }

            if (FindLocation(hunter.locationId)?.locationType == LocationType.Town &&
                (hunter.career?.lastTrainingDay ?? -1) < state.day)
            {
                yield return Request(HunterActionType.Train, hunter.id);
            }

            var contract = FindContract(hunter.activeContractId) ??
                           FindContract(party?.activeContractId);
            if (party != null && party.status != PartyStatus.Disbanded)
            {
                if (string.IsNullOrEmpty(party.activeContractId) &&
                    string.IsNullOrEmpty(hunter.activeContractId))
                {
                    yield return party.leaderHunterId == hunter.id
                        ? Request(
                            HunterActionType.DisbandParty,
                            hunter.id,
                            partyId: party.id)
                        : Request(
                            HunterActionType.LeaveParty,
                            hunter.id,
                            partyId: party.id);
                }

                if (party.leaderHunterId == hunter.id && party.memberIds.Count < 4)
                {
                    foreach (var target in state.hunters)
                    {
                        if (target.id == hunter.id || !target.IsActive || !string.IsNullOrEmpty(target.partyId))
                        {
                            continue;
                        }

                        yield return Request(HunterActionType.InviteToParty, hunter.id,
                            targetHunterId: target.id);
                    }
                }

                if (contract != null && party.leaderHunterId == hunter.id)
                {
                    if (contract.status == ContractStatus.Accepted)
                    {
                        var destinationId = string.IsNullOrEmpty(contract.targetLocationId)
                            ? contract.locationId
                            : contract.targetLocationId;
                        if (hunter.locationId != destinationId)
                        {
                            var nextStep = actions.FindNextTravelLocation(
                                hunter.locationId,
                                destinationId);
                            if (nextStep != null)
                            {
                                yield return Request(HunterActionType.Travel, hunter.id,
                                    locationId: nextStep.id);
                            }
                        }
                        else
                        {
                            yield return Request(HunterActionType.EnterDungeon, hunter.id,
                                contractId: contract.id);
                        }
                    }
                    else if (contract.status == ContractStatus.Active)
                    {
                        yield return Request(HunterActionType.Retreat, hunter.id,
                            contractId: contract.id);
                    }
                }
            }
            else
            {
                yield return Request(HunterActionType.FormParty, hunter.id);
                foreach (var otherParty in state.parties)
                {
                    if (otherParty == null || otherParty.status != PartyStatus.Forming ||
                        otherParty.memberIds.Count >= 4 ||
                        otherParty.locationId != hunter.locationId ||
                        !string.IsNullOrEmpty(otherParty.activeContractId))
                    {
                        continue;
                    }

                    yield return Request(
                        HunterActionType.JoinParty,
                        hunter.id,
                        targetHunterId: otherParty.leaderHunterId,
                        partyId: otherParty.id);
                }

                foreach (var offered in state.contracts)
                {
                    if (offered.status == ContractStatus.Offered && offered.expiresDay >= state.day)
                    {
                        yield return Request(HunterActionType.AcceptContract, hunter.id,
                            contractId: offered.id);
                    }
                }
            }

            if (string.IsNullOrEmpty(hunter.guildId))
            {
                foreach (var guild in state.guilds)
                {
                    yield return Request(HunterActionType.JoinGuild, hunter.id, guildId: guild.id);
                }
            }
            else
            {
                yield return Request(HunterActionType.LeaveGuild, hunter.id);
                foreach (var target in state.hunters)
                {
                    if (target.id != hunter.id && target.IsActive && string.IsNullOrEmpty(target.guildId))
                    {
                        yield return Request(HunterActionType.RecruitToGuild, hunter.id,
                            targetHunterId: target.id);
                    }
                }
            }

            foreach (var gearId in hunter.inventoryGearIds)
            {
                if (!string.IsNullOrEmpty(gearId) && gearId != hunter.equippedGearId)
                {
                    yield return Request(HunterActionType.EquipGear, hunter.id, gearId: gearId);
                }
            }

            foreach (var location in state.map.locations)
            {
                if (location.id != hunter.locationId)
                {
                    yield return Request(HunterActionType.Travel, hunter.id, locationId: location.id);
                }
            }

            var currentLocation = FindLocation(hunter.locationId);
            if (currentLocation != null && currentLocation.locationType == LocationType.ResourceSite &&
                !string.IsNullOrEmpty(hunter.guildId))
            {
                yield return Request(HunterActionType.ClaimSite, hunter.id,
                    locationId: currentLocation.id);
            }

            foreach (var target in state.hunters)
            {
                if (target.id == hunter.id || !target.IsActive || target.locationId != hunter.locationId)
                {
                    continue;
                }

                if (target.wounds > 0 || target.injuries.Any(injury => injury != null && !injury.healed))
                {
                    yield return Request(HunterActionType.Help, hunter.id, targetHunterId: target.id);
                }
                foreach (var gearId in hunter.inventoryGearIds)
                {
                    if (!string.IsNullOrEmpty(gearId) && FindGear(gearId) != null)
                    {
                        yield return Request(
                            HunterActionType.TradeGear,
                            hunter.id,
                            targetHunterId: target.id,
                            gearId: gearId);
                    }
                }
                yield return Request(HunterActionType.Challenge, hunter.id, targetHunterId: target.id);
                yield return Request(HunterActionType.Betray, hunter.id, targetHunterId: target.id);
                yield return Request(HunterActionType.Reconcile, hunter.id, targetHunterId: target.id);
            }

            yield return Request(HunterActionType.Wait, hunter.id);
        }

        private IEnumerable<EcosystemActionRequest> BuildCareerCandidates(HunterProfile hunter)
        {
            if (hunter?.career == null || !hunter.IsActive || !hunter.career.initialized)
            {
                yield break;
            }
            var party = FindParty(hunter.partyId);
            if (hunter.travelDaysRemaining > 0 ||
                FindLocation(hunter.locationId)?.locationType != LocationType.Town ||
                !string.IsNullOrEmpty(hunter.activeContractId) ||
                (party != null && (!string.IsNullOrEmpty(party.activeContractId) ||
                                   party.status == PartyStatus.Travelling)))
            {
                yield break;
            }

            // Loadout choices cost no AP, but remain shared validated commands. Empty slots
            // are filled first; a full loadout only replaces its weakest deterministic fit
            // when the unequipped ability is strictly preferable (or wins the stable ID tie).
            foreach (var learned in hunter.career.learnedAbilities)
            {
                if (learned == null) continue;
                var ability = EcosystemCareerCatalog.FindAbility(learned.abilityId);
                if (ability == null) continue;
                switch (ability.kind)
                {
                    case HunterAbilityKind.Cooldown:
                    {
                        var slots = hunter.career.loadout?.cooldownAbilityIds;
                        if (slots == null || slots.Contains(ability.id)) break;
                        var slot = FindPreferredReplacementSlot(hunter, ability, slots);
                        if (slot >= 0)
                        {
                            yield return Request(
                                HunterActionType.EquipAbility,
                                hunter.id,
                                progressionId: ability.id,
                                slotIndex: slot);
                        }
                        break;
                    }
                    case HunterAbilityKind.Ultimate:
                    {
                        var equippedId = hunter.career.loadout?.ultimateAbilityId;
                        if (equippedId != ability.id &&
                            (string.IsNullOrEmpty(equippedId) ||
                             IsAbilityPreferred(hunter, ability, equippedId)))
                        {
                            yield return Request(
                                HunterActionType.EquipAbility,
                                hunter.id,
                                progressionId: ability.id,
                                slotIndex: EcosystemCareerCatalog.UltimateSlotIndex);
                        }
                        break;
                    }
                    case HunterAbilityKind.Passive:
                    {
                        var slots = hunter.career.loadout?.passiveAbilityIds;
                        if (slots == null || slots.Contains(ability.id)) break;
                        var slot = FindPreferredReplacementSlot(hunter, ability, slots);
                        if (slot >= 0)
                        {
                            yield return Request(
                                HunterActionType.EquipPassive,
                                hunter.id,
                                progressionId: ability.id,
                                slotIndex: slot);
                        }
                        break;
                    }
                }
            }

            if (hunter.career.UnspentAbilityPoints <= 0)
            {
                yield break;
            }

            var existingPlan = EcosystemCareerCatalog.FindAbility(hunter.career.plannedAbilityId);
            if (existingPlan != null &&
                !EcosystemCareerRules.IsLearned(hunter.career, existingPlan.id) &&
                hunter.career.InvestedAbilityPoints >= existingPlan.requiredInvestedAbilityPoints &&
                hunter.career.UnspentAbilityPoints < existingPlan.abilityPointCost)
            {
                // The saved plan is already canonical state. Do not emit the same no-op every
                // day or let zero-time attribute spending silently abandon that commitment.
                yield break;
            }

            foreach (var attribute in EcosystemCareerCatalog.Attributes)
            {
                yield return Request(
                    HunterActionType.InvestAttribute,
                    hunter.id,
                    progressionId: attribute.id,
                    pointAmount: 1);
            }

            HunterAbilityDefinition bestSavingsTarget = null;
            var bestSavingsUtility = float.MinValue;
            foreach (var ability in EcosystemCareerCatalog.Abilities)
            {
                if (EcosystemCareerRules.IsLearned(hunter.career, ability.id)) continue;
                var eligible = hunter.career.InvestedAbilityPoints >=
                               ability.requiredInvestedAbilityPoints;
                if (eligible && hunter.career.UnspentAbilityPoints >= ability.abilityPointCost)
                {
                    yield return Request(
                        HunterActionType.LearnAbility,
                        hunter.id,
                        progressionId: ability.id);
                    continue;
                }

                if (!eligible)
                {
                    // Saving cannot satisfy an invested-AP prerequisite; the hunter must
                    // develop stats or cheaper abilities before planning this unlock.
                    continue;
                }

                var utility = CareerAbilityAppeal(hunter, ability);
                if (utility > bestSavingsUtility ||
                    (Mathf.Approximately(utility, bestSavingsUtility) &&
                     string.CompareOrdinal(ability.id, bestSavingsTarget?.id) < 0))
                {
                    bestSavingsTarget = ability;
                    bestSavingsUtility = utility;
                }
            }
            if (bestSavingsTarget != null &&
                bestSavingsTarget.id != hunter.career.plannedAbilityId)
            {
                yield return Request(
                    HunterActionType.SaveAbilityPoints,
                    hunter.id,
                    progressionId: bestSavingsTarget.id);
            }
        }

        private void AddPersonalityFactors(
            HunterDecisionRecord record,
            EcosystemActionRequest request,
            HunterProfile hunter)
        {
            switch (request.actionType)
            {
                case HunterActionType.AcceptContract:
                case HunterActionType.EnterDungeon:
                case HunterActionType.Challenge:
                    Add(record, "courage", hunter.courage, 0.65f,
                        "Courage makes danger and confrontation more attractive.");
                    Add(record, "ambition", hunter.ambition, 0.35f,
                        "Ambition values fame and advancement.");
                    break;
                case HunterActionType.Retreat:
                    Add(record, "self_preservation", 1f - hunter.courage, 0.7f,
                        "Lower courage increases the appeal of retreat.");
                    break;
                case HunterActionType.JoinGuild:
                case HunterActionType.RecruitToGuild:
                case HunterActionType.FormParty:
                case HunterActionType.JoinParty:
                case HunterActionType.InviteToParty:
                case HunterActionType.Help:
                case HunterActionType.Reconcile:
                    Add(record, "loyalty", hunter.loyalty, 0.48f,
                        "Loyal hunters value stable groups and repaired bonds.");
                    break;
                case HunterActionType.LeaveGuild:
                    Add(record, "independence", 1f - hunter.loyalty, 0.48f,
                        "Less loyal hunters are more willing to abandon an organization.");
                    break;
                case HunterActionType.LeaveParty:
                case HunterActionType.DisbandParty:
                    Add(record, "independence", 1f - hunter.loyalty, 0.4f,
                        "Less loyal hunters are more willing to leave a party behind.");
                    break;
                case HunterActionType.TradeGear:
                case HunterActionType.ClaimReward:
                case HunterActionType.ClaimSite:
                case HunterActionType.Betray:
                    Add(record, "greed", hunter.greed, 0.52f,
                        "Greed raises the value of material advantage.");
                    break;
                case HunterActionType.EquipGear:
                    Add(record, "build_ambition", hunter.ambition, 0.3f,
                        "Ambitious hunters keep refining their build.");
                    break;
                case HunterActionType.Train:
                case HunterActionType.InvestAttribute:
                case HunterActionType.LearnAbility:
                    Add(record, "growth_ambition", hunter.ambition, 0.55f,
                        "Ambition increases the value of deliberate personal growth.");
                    break;
                case HunterActionType.SaveAbilityPoints:
                    Add(record, "long_term_patience", (hunter.loyalty + hunter.ambition) * 0.5f, 0.42f,
                        "Long-term commitment makes saving for a defining ability more attractive.");
                    break;
                case HunterActionType.Retire:
                    Add(record, "low_ambition", 1f - hunter.ambition, 0.55f,
                        "Hunters with less unfinished ambition are more willing to retire.");
                    Add(record, "injury_burden", hunter.wounds, 0.18f,
                        "Accumulated wounds make leaving the profession more attractive.");
                    break;
            }
        }

        private void AddRelationshipFactors(
            HunterDecisionRecord record,
            EcosystemActionRequest request,
            HunterProfile hunter)
        {
            var targetId = request.targetHunterId;
            if (string.IsNullOrEmpty(targetId) && !string.IsNullOrEmpty(request.invitationId))
            {
                var invitation = FindInvitation(request.invitationId);
                targetId = invitation?.senderHunterId;
            }

            if (string.IsNullOrEmpty(targetId))
            {
                return;
            }

            var relationship = FindRelationship(hunter, targetId);
            var trust = relationship?.trust ?? 0f;
            var affinity = relationship?.affinity ?? 0f;
            var rivalry = relationship?.rivalry ?? 0f;
            var grudge = relationship?.grudge ?? 0f;
            var debt = relationship?.debt ?? 0f;
            var cooperative = request.actionType is HunterActionType.AcceptInvitation or
                HunterActionType.JoinParty or HunterActionType.InviteToParty or HunterActionType.Help or
                HunterActionType.Reconcile or HunterActionType.TradeGear;

            Add(record, "relationship_trust", trust, cooperative ? 0.55f : -0.16f,
                "Trust changes willingness to cooperate with this hunter.");
            Add(record, "relationship_affinity", affinity, cooperative ? 0.32f : -0.12f,
                "Personal affinity influences social choices.");
            Add(record, "rivalry", rivalry,
                request.actionType is HunterActionType.Challenge or HunterActionType.Betray ? 0.52f : -0.18f,
                "Rivalry encourages conflict and discourages cooperation.");
            Add(record, "grudge", grudge,
                request.actionType is HunterActionType.Betray or HunterActionType.Challenge ? 0.62f : -0.42f,
                "Remembered harm creates a persistent grudge.");
            Add(record, "debt", debt, cooperative ? 0.38f : -0.08f,
                "Debts make reciprocal help more likely.");

            var memoryWeight = 0f;
            for (var index = Mathf.Max(0, hunter.memories.Count - 12); index < hunter.memories.Count; index++)
            {
                var memory = hunter.memories[index];
                if (memory != null && memory.subjectId == targetId)
                {
                    memoryWeight += memory.emotionalWeight;
                }
            }
            Add(record, "remembered_history", Mathf.Clamp(memoryWeight, -2f, 2f),
                cooperative ? 0.34f : -0.2f,
                "Specific victories, failures, favors, and betrayals still influence this choice.");
        }

        private void AddContextFactors(
            HunterDecisionRecord record,
            EcosystemActionRequest request,
            HunterProfile hunter)
        {
            var activeInjuries = hunter.injuries.Count(injury => injury != null && !injury.healed);
            if (request.actionType is HunterActionType.AcceptContract or HunterActionType.EnterDungeon or
                HunterActionType.Challenge or HunterActionType.Betray)
            {
                Add(record, "wounds", hunter.wounds + activeInjuries, -0.24f,
                    "Current wounds make risky action less attractive.");
            }
            else if (request.actionType is HunterActionType.Recover or HunterActionType.Retreat)
            {
                Add(record, "wounds", hunter.wounds + activeInjuries, 0.42f,
                    "Current wounds make safety and recovery more urgent.");
            }

            var contract = FindContract(request.contractId) ?? FindContract(hunter.activeContractId);
            if (contract != null)
            {
                var reward = contract.rewardGold / 100f + contract.rewardFame * 0.12f;
                var risk = contract.difficulty * 25f /
                           Mathf.Max(1f, EcosystemCareerRules.CombatPower(hunter, gearCatalog));
                if (request.actionType is HunterActionType.AcceptContract or HunterActionType.EnterDungeon)
                {
                    Add(record, "expected_reward", reward, 0.45f + hunter.greed * 0.25f,
                        "Gold, fame, and guild resources make the contract worthwhile.");
                    Add(record, "perceived_danger", risk, -(0.62f - hunter.courage * 0.36f),
                        "Difficulty is weighed against level and courage.");
                }
                else if (request.actionType == HunterActionType.Retreat)
                {
                    Add(record, "perceived_danger", risk, 0.58f,
                        "High danger makes retreat more attractive.");
                }
            }

            if (request.actionType == HunterActionType.JoinGuild)
            {
                var guild = FindGuild(request.guildId);
                if (guild != null)
                {
                    Add(record, "guild_prestige", guild.prestige / 100f, 0.42f,
                        "Prestigious guilds help ambitious hunters reach their goals.");
                    Add(record, "guild_security", guild.resources / 250f, 0.25f,
                        "Guild resources imply equipment and recovery support.");
                }
            }

            if (request.actionType == HunterActionType.EquipGear)
            {
                var gear = FindGear(request.gearId);
                if (gear != null)
                {
                    Add(record, "gear_power", gear.Power / 25f, 0.28f,
                        "Power matters, but the gear's verbs and role matter too.");
                    var roleFit = gear.TacticalRole switch
                    {
                        TacticalRole.Bruiser => hunter.courage,
                        TacticalRole.Skirmisher => (hunter.ambition + hunter.greed) * 0.5f,
                        TacticalRole.Vanguard => (hunter.loyalty + hunter.courage) * 0.5f,
                        TacticalRole.Support => hunter.loyalty,
                        TacticalRole.Controller => hunter.ambition,
                        _ => 0.5f
                    };
                    Add(record, "moveset_role_fit", roleFit, 0.52f,
                        $"{gear.TacticalRole} verbs fit this hunter's preferred playstyle.");
                }
            }

            if (request.actionType is HunterActionType.Travel or HunterActionType.ClaimSite)
            {
                var location = FindLocation(request.locationId);
                if (location != null)
                {
                    Add(record, "location_danger", location.danger / 10f,
                        request.actionType == HunterActionType.ClaimSite ? 0.15f : -0.2f,
                        "The destination's danger affects travel and conquest.");
                    Add(record, "resource_value", location.resourceYield / 20f,
                        request.actionType == HunterActionType.ClaimSite ? 0.62f : 0.08f,
                        "Resource output benefits the hunter's guild.");
                }
            }

            if (request.actionType == HunterActionType.AcceptInvitation)
            {
                var invitation = FindInvitation(request.invitationId);
                if (invitation != null)
                {
                    Add(record, "invitation_relevance",
                        invitation.invitationType == InvitationType.PartyMembership ? 1f : 0.75f,
                        0.24f,
                        "The invitation advances a concrete social or expedition opportunity.");
                }
            }

            AddCareerFactors(record, request, hunter);

            if (!string.IsNullOrEmpty(hunter.goal))
            {
                var goalFit = GoalFit(hunter.goal, request.actionType);
                Add(record, "personal_goal", goalFit, 0.48f,
                    $"This choice is compared with the goal: {hunter.goal}");
            }
        }

        private static float GoalFit(string goal, HunterActionType action)
        {
            var lower = goal.ToLowerInvariant();
            if (lower.Contains("guild") && action is HunterActionType.JoinGuild or HunterActionType.RecruitToGuild)
                return 1f;
            if ((lower.Contains("protect") || lower.Contains("heal")) && action == HunterActionType.Help)
                return 1f;
            if ((lower.Contains("relic") || lower.Contains("wealth")) &&
                action is HunterActionType.AcceptContract or HunterActionType.TradeGear or HunterActionType.ClaimReward)
                return 1f;
            if ((lower.Contains("fear") || lower.Contains("renown") || lower.Contains("strong")) &&
                action is HunterActionType.Challenge or HunterActionType.EnterDungeon)
                return 0.9f;
            if ((lower.Contains("territory") || lower.Contains("expand")) && action == HunterActionType.ClaimSite)
                return 1f;
            if (lower.Contains("survive") && action is HunterActionType.Recover or HunterActionType.Retreat)
                return 1f;
            if ((lower.Contains("strong") || lower.Contains("renown") || lower.Contains("best") ||
                 lower.Contains("legend")) &&
                action is HunterActionType.Train or HunterActionType.InvestAttribute or
                    HunterActionType.LearnAbility)
                return 1f;
            return 0.15f;
        }

        private static float BaseDesire(HunterActionType action)
        {
            return action switch
            {
                HunterActionType.Wait => 0.03f,
                HunterActionType.Recover => 0.42f,
                HunterActionType.ClaimReward => 0.9f,
                HunterActionType.AcceptInvitation => 0.18f,
                HunterActionType.RejectInvitation => 0.12f,
                HunterActionType.AcceptContract => 0.2f,
                HunterActionType.EnterDungeon => 0.58f,
                HunterActionType.Retreat => -0.05f,
                HunterActionType.JoinGuild => 0.12f,
                HunterActionType.LeaveGuild => -0.08f,
                HunterActionType.RecruitToGuild => 0.1f,
                HunterActionType.FormParty => 0.12f,
                HunterActionType.JoinParty => 0.14f,
                HunterActionType.InviteToParty => 0.2f,
                HunterActionType.LeaveParty => -0.12f,
                HunterActionType.DisbandParty => -0.16f,
                HunterActionType.EquipGear => 0.16f,
                HunterActionType.Travel => 0.05f,
                HunterActionType.TradeGear => 0.04f,
                HunterActionType.Help => 0.08f,
                HunterActionType.Betray => -0.32f,
                HunterActionType.Challenge => -0.05f,
                HunterActionType.Reconcile => 0.02f,
                HunterActionType.ClaimSite => 0.12f,
                HunterActionType.Train => 0.16f,
                HunterActionType.InvestAttribute => 0.3f,
                HunterActionType.LearnAbility => 0.38f,
                HunterActionType.EquipAbility => 0.2f,
                HunterActionType.EquipPassive => 0.2f,
                HunterActionType.SaveAbilityPoints => 0.24f,
                HunterActionType.Retire => -0.2f,
                _ => 0f
            };
        }

        private void AddCareerFactors(
            HunterDecisionRecord record,
            EcosystemActionRequest request,
            HunterProfile hunter)
        {
            if (request.actionType == HunterActionType.Train)
            {
                Add(record, "career_progress_needed", 1f /
                    Mathf.Max(1f, hunter.career?.CareerLevel ?? 1), 0.7f,
                    "Early career growth makes structured training more valuable.");
                Add(record, "training_safety", hunter.wounds, -0.18f,
                    "Injuries reduce the value of training instead of recovery.");
                return;
            }

            if (request.actionType == HunterActionType.InvestAttribute)
            {
                var definition = EcosystemCareerCatalog.FindAttribute(request.progressionId);
                var allocation = EcosystemCareerRules.FindAttribute(hunter, request.progressionId);
                if (definition == null || allocation == null) return;
                Add(record, "build_affinity",
                    EcosystemCareerRules.AffinityFor(hunter, definition.primaryArchetype), 0.72f,
                    $"Affinity favors {definition.primaryArchetype} development.");
                Add(record, "secondary_affinity",
                    EcosystemCareerRules.AffinityFor(hunter, definition.secondaryArchetype), 0.22f,
                    $"The attribute also supports {definition.secondaryArchetype} options.");
                Add(record, "diminishing_specialization", allocation.investedAbilityPoints, -0.08f,
                    "Repeated investment eventually makes another path more appealing.");
                if (request.progressionId is "vitality" or "resilience")
                {
                    Add(record, "injury_response", hunter.wounds, 0.2f,
                        "Recent wounds encourage a tougher build.");
                }
                return;
            }

            if (request.actionType is HunterActionType.LearnAbility or
                HunterActionType.SaveAbilityPoints or HunterActionType.EquipAbility or
                HunterActionType.EquipPassive)
            {
                var ability = EcosystemCareerCatalog.FindAbility(request.progressionId);
                if (ability == null) return;
                Add(record, "ability_affinity",
                    EcosystemCareerRules.AffinityFor(hunter, ability.primaryArchetype), 0.82f,
                    $"The ability supports this hunter's {ability.primaryArchetype} affinity.");
                Add(record, "hybrid_affinity",
                    EcosystemCareerRules.AffinityFor(hunter, ability.secondaryArchetype), 0.28f,
                    $"It also opens a {ability.secondaryArchetype} hybrid path.");
                if (request.actionType == HunterActionType.LearnAbility)
                {
                    Add(record, "ability_cost", ability.abilityPointCost, -0.055f,
                        "More expensive abilities compete with immediate stat investment.");
                    Add(record, "planned_target",
                        hunter.career?.plannedAbilityId == ability.id ? 1f : 0f, 0.8f,
                        "A previously chosen build target receives consistent follow-through.");
                }
                else if (request.actionType == HunterActionType.SaveAbilityPoints)
                {
                    var gap = Mathf.Max(
                        ability.abilityPointCost - (hunter.career?.UnspentAbilityPoints ?? 0),
                        ability.requiredInvestedAbilityPoints -
                        (hunter.career?.InvestedAbilityPoints ?? 0));
                    Add(record, "future_ability_value", CareerAbilityAppeal(hunter, ability), 0.42f,
                        "The planned ability is compared with spending the points now.");
                    Add(record, "remaining_gap", gap, -0.12f,
                        "A distant unlock is harder to justify saving for.");
                    Add(record, "target_commitment",
                        hunter.career?.plannedAbilityId == ability.id ? 1f : 0f, 0.5f,
                        "Hunters tend to remain committed to an existing plan.");
                }
                else
                {
                    var equippedId = EquippedAbilityAt(hunter, request.actionType, request.slotIndex);
                    if (string.IsNullOrEmpty(equippedId))
                    {
                        Add(record, "empty_loadout_slot", 1f, 0.7f,
                            "A learned ability is useful only when the active loadout can express it.");
                    }
                    else
                    {
                        var equipped = EcosystemCareerCatalog.FindAbility(equippedId);
                        var appealGain = CareerAbilityAppeal(hunter, ability) -
                                         CareerAbilityAppeal(hunter, equipped);
                        Add(record, "loadout_appeal_gain", appealGain, 0.9f,
                            $"This replaces {equipped?.displayName ?? equippedId} with a better-fit learned ability.");
                        Add(record, "replacement_ability_appeal",
                            CareerAbilityAppeal(hunter, ability), 0.18f,
                            "The same deterministic career appeal used to authorize replacement also scores it.");
                    }
                }
            }
        }

        private static float CareerAbilityAppeal(
            HunterProfile hunter,
            HunterAbilityDefinition ability)
        {
            if (hunter == null || ability == null) return 0f;
            var score = EcosystemCareerRules.AffinityFor(hunter, ability.primaryArchetype) * 1.4f +
                        EcosystemCareerRules.AffinityFor(hunter, ability.secondaryArchetype) * 0.45f;
            if (hunter.career?.plannedAbilityId == ability.id) score += 0.8f;
            if (ability.kind == HunterAbilityKind.Ultimate) score += hunter.ambition * 0.45f;
            if (!string.IsNullOrEmpty(hunter.goal))
            {
                var goal = hunter.goal.ToLowerInvariant();
                if ((goal.Contains("protect") && ability.primaryArchetype is HunterArchetype.Tank or HunterArchetype.Healer) ||
                    (goal.Contains("heal") && ability.primaryArchetype == HunterArchetype.Healer) ||
                    (goal.Contains("fear") && ability.primaryArchetype is HunterArchetype.Assassin or HunterArchetype.Fighter) ||
                    (goal.Contains("relic") && ability.primaryArchetype is HunterArchetype.Mage or HunterArchetype.Ranger))
                {
                    score += 0.75f;
                }
            }
            score += EcosystemDeterministicRandom.StableHash($"{hunter.id}|career|{ability.id}") %
                     1001u / 10000f;
            return score;
        }

        private static int FindPreferredReplacementSlot(
            HunterProfile hunter,
            HunterAbilityDefinition incoming,
            IReadOnlyList<string> equippedIds)
        {
            if (incoming == null || equippedIds == null || equippedIds.Count == 0) return -1;
            var weakestIndex = -1;
            var weakestId = "";
            var weakestAppeal = float.MaxValue;
            for (var index = 0; index < equippedIds.Count; index++)
            {
                var equippedId = equippedIds[index];
                if (string.IsNullOrEmpty(equippedId)) return index;
                var equipped = EcosystemCareerCatalog.FindAbility(equippedId);
                if (equipped == null) return index;
                var appeal = CareerAbilityAppeal(hunter, equipped);
                if (weakestIndex < 0 || appeal < weakestAppeal ||
                    (Mathf.Approximately(appeal, weakestAppeal) &&
                     string.CompareOrdinal(equippedId, weakestId) > 0))
                {
                    weakestIndex = index;
                    weakestId = equippedId;
                    weakestAppeal = appeal;
                }
            }

            return weakestIndex >= 0 && IsAbilityPreferred(hunter, incoming, weakestId)
                ? weakestIndex
                : -1;
        }

        private static bool IsAbilityPreferred(
            HunterProfile hunter,
            HunterAbilityDefinition incoming,
            string equippedId)
        {
            if (incoming == null) return false;
            var equipped = EcosystemCareerCatalog.FindAbility(equippedId);
            if (equipped == null) return true;
            var incomingAppeal = CareerAbilityAppeal(hunter, incoming);
            var equippedAppeal = CareerAbilityAppeal(hunter, equipped);
            if (!Mathf.Approximately(incomingAppeal, equippedAppeal))
            {
                return incomingAppeal > equippedAppeal;
            }
            return string.CompareOrdinal(incoming.id, equipped.id) < 0;
        }

        private static string EquippedAbilityAt(
            HunterProfile hunter,
            HunterActionType action,
            int slotIndex)
        {
            var loadout = hunter?.career?.loadout;
            if (loadout == null) return "";
            if (action == HunterActionType.EquipPassive)
            {
                return slotIndex >= 0 && loadout.passiveAbilityIds != null &&
                       slotIndex < loadout.passiveAbilityIds.Count
                    ? loadout.passiveAbilityIds[slotIndex]
                    : "";
            }
            if (slotIndex == EcosystemCareerCatalog.UltimateSlotIndex)
            {
                return loadout.ultimateAbilityId ?? "";
            }
            return slotIndex >= 0 && loadout.cooldownAbilityIds != null &&
                   slotIndex < loadout.cooldownAbilityIds.Count
                ? loadout.cooldownAbilityIds[slotIndex]
                : "";
        }

        private static void Add(
            HunterDecisionRecord record,
            string key,
            float rawValue,
            float weight,
            string explanation)
        {
            if (Mathf.Abs(rawValue) < 0.0001f || Mathf.Abs(weight) < 0.0001f)
            {
                return;
            }

            record.factors.Add(new DecisionFactor
            {
                key = key,
                rawValue = rawValue,
                weight = weight,
                contribution = rawValue * weight,
                explanation = explanation
            });
        }

        private static void Finalize(HunterDecisionRecord record)
        {
            record.totalScore = record.factors.Sum(factor => factor.contribution);
        }

        private static EcosystemActionRequest Request(
            HunterActionType action,
            string actorHunterId,
            string targetHunterId = "",
            string guildId = "",
            string contractId = "",
            string locationId = "",
            string gearId = "",
            string invitationId = "",
            string partyId = "",
            string progressionId = "",
            int slotIndex = -1,
            int pointAmount = 1)
        {
            return new EcosystemActionRequest(action, actorHunterId)
            {
                targetHunterId = targetHunterId,
                guildId = guildId,
                contractId = contractId,
                locationId = locationId,
                gearId = gearId,
                invitationId = invitationId,
                partyId = partyId,
                progressionId = progressionId,
                slotIndex = slotIndex,
                pointAmount = pointAmount
            };
        }

        private HunterProfile FindHunter(string id) =>
            string.IsNullOrEmpty(id) ? null : state.hunters.Find(item => item.id == id);

        private GuildState FindGuild(string id) =>
            string.IsNullOrEmpty(id) ? null : state.guilds.Find(item => item.id == id);

        private ContractState FindContract(string id) =>
            string.IsNullOrEmpty(id) ? null : state.contracts.Find(item => item.id == id);

        private PartyState FindParty(string id) =>
            string.IsNullOrEmpty(id) ? null : state.parties.Find(item => item.id == id);

        private InvitationState FindInvitation(string id) =>
            string.IsNullOrEmpty(id) ? null : state.invitations.Find(item => item.id == id);

        private LocationState FindLocation(string id) =>
            string.IsNullOrEmpty(id) ? null : state.map.locations.Find(item => item.id == id);

        private EcosystemGearDefinition FindGear(string id)
        {
            for (var index = 0; index < gearCatalog.Count; index++)
            {
                if (gearCatalog[index] != null && gearCatalog[index].GearId == id)
                {
                    return gearCatalog[index];
                }
            }
            return null;
        }

        private static HunterRelationship FindRelationship(HunterProfile hunter, string targetId) =>
            hunter.relationships?.Find(item => item != null && item.hunterId == targetId);

        private static string PrimaryTarget(EcosystemActionRequest request)
        {
            if (!string.IsNullOrEmpty(request.targetHunterId)) return request.targetHunterId;
            if (!string.IsNullOrEmpty(request.contractId)) return request.contractId;
            if (!string.IsNullOrEmpty(request.guildId)) return request.guildId;
            if (!string.IsNullOrEmpty(request.locationId)) return request.locationId;
            if (!string.IsNullOrEmpty(request.gearId)) return request.gearId;
            if (!string.IsNullOrEmpty(request.progressionId)) return request.progressionId;
            if (!string.IsNullOrEmpty(request.partyId)) return request.partyId;
            return request.invitationId;
        }

        private string DecisionId(EcosystemActionRequest request)
        {
            var identity = $"{state.day}|{request.actorHunterId}|{Signature(request)}";
            return $"decision-{state.day}-{EcosystemDeterministicRandom.StableHash(identity):x8}";
        }

        private static string Category(HunterActionType action)
        {
            return action switch
            {
                HunterActionType.JoinGuild or HunterActionType.LeaveGuild or
                HunterActionType.CreateGuild or HunterActionType.RecruitHunter or
                HunterActionType.RecruitToGuild or HunterActionType.FormParty or
                HunterActionType.JoinParty or HunterActionType.InviteToParty or
                HunterActionType.AcceptInvitation or HunterActionType.RejectInvitation or
                HunterActionType.LeaveParty or HunterActionType.DisbandParty => "Organization",
                HunterActionType.AcceptContract or HunterActionType.DeclineContract or
                HunterActionType.StartContract or HunterActionType.ResolveContract or
                HunterActionType.EnterDungeon or HunterActionType.Retreat => "Contract",
                HunterActionType.Travel => "Travel",
                HunterActionType.EquipGear or HunterActionType.TradeGear or
                HunterActionType.ClaimReward => "Economy",
                HunterActionType.Help or HunterActionType.Betray or
                HunterActionType.Challenge or HunterActionType.Reconcile => "Relationship",
                HunterActionType.ClaimLocation or HunterActionType.ClaimSite or
                HunterActionType.DefendLocation or HunterActionType.DeclareWar or
                HunterActionType.NegotiatePeace => "Territory",
                HunterActionType.Recover => "Recovery",
                HunterActionType.Train or HunterActionType.InvestAttribute or
                HunterActionType.LearnAbility or HunterActionType.EquipAbility or
                HunterActionType.EquipPassive or HunterActionType.SaveAbilityPoints => "Career",
                HunterActionType.Retire => "Life",
                _ => "Idle"
            };
        }

        private static string Signature(EcosystemActionRequest request) =>
            $"{request.actionType}|{request.targetHunterId}|{request.guildId}|{request.contractId}|" +
            $"{request.locationId}|{request.gearId}|{request.invitationId}|{request.partyId}|" +
            $"{request.progressionId}|{request.slotIndex}|{request.pointAmount}";

        private static string Humanize(HunterActionType action) =>
            action.ToString().Replace("To", " to ").Replace("Invitation", " invitation").ToLowerInvariant();
    }
}
