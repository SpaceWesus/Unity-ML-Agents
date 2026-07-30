using System;
using System.Collections.Generic;
using UnityEngine;

namespace Turtle.Ecosystem
{
    [Serializable]
    public sealed class EcosystemActionRequest
    {
        public HunterActionType actionType;
        public string actorHunterId;
        public string targetHunterId;
        public string guildId;
        public string partyId;
        public string contractId;
        public string locationId;
        public string gearId;
        public string invitationId;
        public string progressionId;
        public int slotIndex = -1;
        public int pointAmount = 1;

        public EcosystemActionRequest()
        {
        }

        public EcosystemActionRequest(HunterActionType action, string actorId)
        {
            actionType = action;
            actorHunterId = actorId;
        }
    }

    [Serializable]
    public sealed class EcosystemActionResult
    {
        public bool success;
        public string reasonCode;
        public string summary;

        public static EcosystemActionResult Succeeded(string message)
        {
            return new EcosystemActionResult
            {
                success = true,
                reasonCode = "ok",
                summary = message ?? string.Empty
            };
        }

        public static EcosystemActionResult Failed(string message, string code = "validation_failed")
        {
            return new EcosystemActionResult
            {
                success = false,
                reasonCode = string.IsNullOrEmpty(code) ? "validation_failed" : code,
                summary = message ?? string.Empty
            };
        }
    }

    /// <summary>
    /// Authoritative mutation boundary shared by player input and autonomous hunters.
    /// Validation is read-only; all accepted state changes happen through Execute.
    /// </summary>
    public sealed class EcosystemActionService
    {
        private const int MaximumPartySize = 4;
        private const int MaximumUiEvents = 64;
        private const int MaximumStructuredEvents = 256;
        private const int MaximumMemoriesPerHunter = 64;
        private const int MaximumInvitations = 128;
        private const int TrainingExperienceReward = 10;

        private readonly EcosystemWorldState state;
        private readonly IReadOnlyList<EcosystemGearDefinition> gearCatalog;
        private readonly EcosystemDeterministicRandom random;
        private readonly EcosystemEncounterSimulation encounterSimulation;

        public EcosystemActionService(
            EcosystemWorldState worldState,
            IReadOnlyList<EcosystemGearDefinition> availableGear)
        {
            state = worldState ?? throw new ArgumentNullException(nameof(worldState));
            gearCatalog = availableGear ?? Array.Empty<EcosystemGearDefinition>();
            random = new EcosystemDeterministicRandom(state);
            encounterSimulation = new EcosystemEncounterSimulation(state, gearCatalog);
            EnsureCollections();
        }

        public EcosystemWorldState State => state;

        public HunterProfile FindHunter(string hunterId)
        {
            return string.IsNullOrEmpty(hunterId)
                ? null
                : state.hunters.Find(item => item != null && item.id == hunterId);
        }

        public GuildState FindGuild(string guildId)
        {
            return string.IsNullOrEmpty(guildId)
                ? null
                : state.guilds.Find(item => item != null && item.id == guildId);
        }

        public ContractState FindContract(string contractId)
        {
            return string.IsNullOrEmpty(contractId)
                ? null
                : state.contracts.Find(item => item != null && item.id == contractId);
        }

        public PartyState FindParty(string partyId)
        {
            return string.IsNullOrEmpty(partyId)
                ? null
                : state.parties.Find(item => item != null && item.id == partyId);
        }

        public InvitationState FindInvitation(string invitationId)
        {
            return string.IsNullOrEmpty(invitationId)
                ? null
                : state.invitations.Find(item => item != null && item.id == invitationId);
        }

        public LocationState FindLocation(string locationId)
        {
            return string.IsNullOrEmpty(locationId) || state.map == null
                ? null
                : state.map.locations.Find(item => item != null && item.id == locationId);
        }

        public GateInstanceState FindGate(string gateId)
        {
            return string.IsNullOrEmpty(gateId) || state.gates == null
                ? null
                : state.gates.Find(item => item != null && item.id == gateId);
        }

        public DungeonEncounterState FindEncounter(string encounterId)
        {
            return string.IsNullOrEmpty(encounterId) || state.encounters == null
                ? null
                : state.encounters.Find(item => item != null && item.id == encounterId);
        }

        private void SetContractStatus(ContractState contract, ContractStatus status)
        {
            if (contract == null) return;
            contract.status = status;
            var gate = FindGate(contract.gateId);
            if (gate != null)
            {
                gate.lifecycle = EcosystemGateGenerator.LifecycleForContractStatus(status);
            }
        }

        public EcosystemGearDefinition FindGear(string gearId)
        {
            if (string.IsNullOrEmpty(gearId))
            {
                return null;
            }

            for (var index = 0; index < gearCatalog.Count; index++)
            {
                var gear = gearCatalog[index];
                if (gear != null && gear.GearId == gearId)
                {
                    return gear;
                }
            }

            return null;
        }

        public bool CanExecute(EcosystemActionRequest request, out string reason)
        {
            reason = string.Empty;
            if (request == null)
            {
                reason = "No action was supplied.";
                return false;
            }

            var action = NormalizeAction(request.actionType);
            var actor = FindHunter(request.actorHunterId);
            if (actor == null)
            {
                reason = "The acting hunter does not exist.";
                return false;
            }
            if (!actor.isAlive)
            {
                reason = $"{actor.displayName} is dead and cannot act.";
                return false;
            }
            if (actor.isRetired)
            {
                reason = $"{actor.displayName} is retired and no longer takes hunter actions.";
                return false;
            }
            if (IsTravelling(actor) && action != HunterActionType.Wait)
            {
                reason = $"{actor.displayName} is still travelling and cannot begin another action.";
                return false;
            }
            if (IsCareerAction(action) && !CanUseCareerSystem(actor, out reason))
            {
                return false;
            }

            switch (action)
            {
                case HunterActionType.Wait:
                    return true;

                case HunterActionType.Train:
                    if (actor.career.lastTrainingDay >= state.day)
                        return Fail($"{actor.displayName} has already trained today.", out reason);
                    return true;

                case HunterActionType.InvestAttribute:
                    return CanInvestAttribute(actor, request.progressionId, request.pointAmount, out reason);

                case HunterActionType.LearnAbility:
                    return CanLearnAbility(actor, request.progressionId, out reason);

                case HunterActionType.EquipAbility:
                    return CanEquipAbility(actor, request.progressionId, request.slotIndex, out reason);

                case HunterActionType.EquipPassive:
                    return CanEquipPassive(actor, request.progressionId, request.slotIndex, out reason);

                case HunterActionType.SaveAbilityPoints:
                    return CanPlanAbility(actor, request.progressionId, out reason);

                case HunterActionType.Retire:
                {
                    if (!string.IsNullOrEmpty(actor.activeContractId))
                        return Fail("A hunter cannot retire during an active contract.", out reason);
                    var retirementParty = FindParty(actor.partyId);
                    if (retirementParty != null && !string.IsNullOrEmpty(retirementParty.activeContractId))
                        return Fail("A hunter cannot retire while their party is under contract.", out reason);
                    if (FindLocation(actor.locationId)?.locationType == LocationType.Dungeon)
                        return Fail("A hunter must leave the gate before retiring.", out reason);
                    return true;
                }

                case HunterActionType.JoinGuild:
                {
                    var guild = FindGuild(request.guildId);
                    if (guild == null) return Fail("The selected guild does not exist.", out reason);
                    if (!string.IsNullOrEmpty(actor.guildId))
                        return Fail($"{actor.displayName} must leave their current guild first.", out reason);
                    return true;
                }

                case HunterActionType.LeaveGuild:
                    if (string.IsNullOrEmpty(actor.guildId))
                        return Fail($"{actor.displayName} is not in a guild.", out reason);
                    if (!string.IsNullOrEmpty(actor.activeContractId))
                        return Fail("A hunter cannot leave their guild during an active contract.", out reason);
                    return true;

                case HunterActionType.FormParty:
                    if (!string.IsNullOrEmpty(actor.partyId) &&
                        FindParty(actor.partyId)?.status != PartyStatus.Disbanded)
                        return Fail($"{actor.displayName} already belongs to a party.", out reason);
                    if (!string.IsNullOrEmpty(actor.activeContractId))
                        return Fail("A hunter cannot form a new party during a contract.", out reason);
                    return true;

                case HunterActionType.JoinParty:
                {
                    var party = FindParty(request.partyId);
                    if (party == null || party.status == PartyStatus.Disbanded)
                        return Fail("The selected party does not exist.", out reason);
                    if (!string.IsNullOrEmpty(actor.partyId))
                        return Fail($"{actor.displayName} already belongs to a party.", out reason);
                    if (!string.IsNullOrEmpty(party.activeContractId))
                        return Fail("Use a party invitation to join an expedition already under contract.", out reason);
                    if (party.status == PartyStatus.Travelling)
                        return Fail("A travelling party cannot be joined.", out reason);
                    if (party.memberIds.Count >= MaximumPartySize)
                        return Fail("The selected party is full.", out reason);
                    if (party.locationId != actor.locationId)
                        return Fail("The hunter must be at the same location as the party.", out reason);
                    return true;
                }

                case HunterActionType.LeaveParty:
                {
                    var party = FindParty(actor.partyId);
                    if (party == null || party.status == PartyStatus.Disbanded)
                        return Fail($"{actor.displayName} does not belong to an active party.", out reason);
                    if (party.leaderHunterId == actor.id)
                        return Fail("The leader must disband the party or transfer leadership.", out reason);
                    if (!string.IsNullOrEmpty(party.activeContractId))
                        return Fail("A hunter cannot leave while the party has an active contract.", out reason);
                    return true;
                }

                case HunterActionType.DisbandParty:
                {
                    var party = FindParty(actor.partyId);
                    if (party == null || party.status == PartyStatus.Disbanded)
                        return Fail($"{actor.displayName} does not lead an active party.", out reason);
                    if (party.leaderHunterId != actor.id)
                        return Fail("Only the party leader can disband the party.", out reason);
                    if (!string.IsNullOrEmpty(party.activeContractId))
                        return Fail("A contracted party must finish or retreat before disbanding.", out reason);
                    return true;
                }

                case HunterActionType.RecruitToGuild:
                {
                    var guildId = string.IsNullOrEmpty(request.guildId) ? actor.guildId : request.guildId;
                    var guild = FindGuild(guildId);
                    var target = FindHunter(request.targetHunterId);
                    if (guild == null || actor.guildId != guild.id)
                        return Fail("The recruiter must belong to the selected guild.", out reason);
                    if (!IsLivingOther(actor, target))
                        return Fail("Select another living hunter to recruit.", out reason);
                    if (!string.IsNullOrEmpty(target.guildId))
                        return Fail($"{target.displayName} already belongs to a guild.", out reason);
                    if (HasPendingInvitation(InvitationType.GuildMembership, target.id, guild.id, null))
                        return Fail("That hunter already has a pending invitation from this guild.", out reason);
                    return true;
                }

                case HunterActionType.AcceptContract:
                {
                    var contract = FindContract(request.contractId);
                    if (contract == null) return Fail("The selected contract does not exist.", out reason);
                    if (contract.status != ContractStatus.Offered)
                        return Fail("The contract is no longer available.", out reason);
                    if (state.day > contract.expiresDay)
                        return Fail("The contract has expired.", out reason);
                    if (actor.locationId != contract.locationId)
                        return Fail("The hunter must visit the contract's posting location before accepting it.", out reason);
                    if (!string.IsNullOrEmpty(actor.activeContractId))
                        return Fail($"{actor.displayName} is already committed to a contract.", out reason);
                    var party = FindParty(actor.partyId);
                    if (party != null && party.status != PartyStatus.Disbanded && party.leaderHunterId != actor.id)
                        return Fail("Only the party leader can accept a contract.", out reason);
                    return true;
                }

                case HunterActionType.InviteToParty:
                {
                    var party = FindParty(actor.partyId);
                    var target = FindHunter(request.targetHunterId);
                    if (party == null || party.status == PartyStatus.Disbanded)
                        return Fail("The acting hunter does not lead an active party.", out reason);
                    if (party.leaderHunterId != actor.id)
                        return Fail("Only the party leader can invite members.", out reason);
                    if (party.memberIds.Count >= MaximumPartySize)
                        return Fail("The party is full.", out reason);
                    if (!IsLivingOther(actor, target))
                        return Fail("Select another living hunter to invite.", out reason);
                    if (!string.IsNullOrEmpty(target.partyId))
                        return Fail($"{target.displayName} already belongs to a party.", out reason);
                    if (actor.locationId != target.locationId)
                        return Fail("Party invitations require both hunters to be at the same location.", out reason);
                    if (HasPendingInvitation(InvitationType.PartyMembership, target.id, null, party.id))
                        return Fail("That hunter already has a pending invitation to this party.", out reason);
                    return true;
                }

                case HunterActionType.AcceptInvitation:
                    return CanRespondToInvitation(actor, request.invitationId, true, out reason);

                case HunterActionType.RejectInvitation:
                    return CanRespondToInvitation(actor, request.invitationId, false, out reason);

                case HunterActionType.EquipGear:
                {
                    var gear = FindGear(request.gearId);
                    if (gear == null) return Fail("The selected gear definition does not exist.", out reason);
                    if (!actor.inventoryGearIds.Contains(gear.GearId))
                        return Fail($"{actor.displayName} does not own {gear.DisplayName}.", out reason);
                    return true;
                }

                case HunterActionType.Travel:
                {
                    var destination = FindLocation(request.locationId);
                    if (destination == null) return Fail("The destination does not exist.", out reason);
                    if (actor.locationId == destination.id)
                        return Fail($"{actor.displayName} is already at {destination.displayName}.", out reason);
                    if (!string.IsNullOrEmpty(actor.activeContractId))
                    {
                        var activeContract = FindContract(actor.activeContractId);
                        var contractDestination = ContractDestination(activeContract);
                        var nextStep = FindNextTravelLocation(actor.locationId, contractDestination?.id);
                        if (activeContract == null || activeContract.status != ContractStatus.Accepted ||
                            contractDestination == null || nextStep == null || destination.id != nextStep.id)
                        {
                            return Fail("A contracted party must follow the next route toward its objective.", out reason);
                        }
                    }
                    var party = FindParty(actor.partyId);
                    if (party != null && party.status != PartyStatus.Disbanded && party.leaderHunterId != actor.id)
                        return Fail("Only the party leader can move the party.", out reason);
                    if (FindRoute(actor.locationId, destination.id) == null)
                        return Fail("There is no direct route to that destination.", out reason);
                    return true;
                }

                case HunterActionType.EnterDungeon:
                    return CanEnterDungeon(actor, request.contractId, out reason);

                case HunterActionType.Retreat:
                {
                    var contract = FindContract(request.contractId);
                    if (contract == null || contract.status != ContractStatus.Active)
                        return Fail("The selected contract is not active.", out reason);
                    var party = FindParty(contract.acceptedPartyId);
                    if (party == null || party.leaderHunterId != actor.id)
                        return Fail("Only the active party leader can order a retreat.", out reason);
                    return true;
                }

                case HunterActionType.TradeGear:
                {
                    var target = FindHunter(request.targetHunterId);
                    var gear = FindGear(request.gearId);
                    if (!IsLivingOther(actor, target))
                        return Fail("Select another living hunter to trade with.", out reason);
                    if (gear == null || !actor.inventoryGearIds.Contains(request.gearId))
                        return Fail("The acting hunter does not own that gear.", out reason);
                    if (target.inventoryGearIds.Contains(request.gearId))
                        return Fail("The recipient already owns that gear.", out reason);
                    if (actor.locationId != target.locationId)
                        return Fail("Hunters must be at the same location to trade.", out reason);
                    if (!string.IsNullOrEmpty(actor.activeContractId) ||
                        !string.IsNullOrEmpty(target.activeContractId))
                        return Fail("Hunters cannot trade during an active contract.", out reason);
                    if (target.gold < TradePrice(gear))
                        return Fail($"{target.displayName} cannot afford that gear.", out reason);
                    return true;
                }

                case HunterActionType.ClaimReward:
                    if (actor.pendingRewardGold <= 0)
                        return Fail($"{actor.displayName} has no pending reward.", out reason);
                    return true;

                case HunterActionType.Help:
                {
                    var target = FindHunter(request.targetHunterId);
                    if (!IsLivingOther(actor, target))
                        return Fail("Select another living hunter.", out reason);
                    if (actor.locationId != target.locationId)
                        return Fail("Both hunters must be at the same location.", out reason);
                    return true;
                }

                case HunterActionType.Challenge:
                {
                    var target = FindHunter(request.targetHunterId);
                    if (!IsLivingOther(actor, target))
                        return Fail("Select another living hunter.", out reason);
                    if (actor.locationId != target.locationId)
                        return Fail("Both hunters must be at the same location.", out reason);
                    if (IsSettlementSafeZone(FindLocation(actor.locationId)))
                        return Fail("Hunter-versus-hunter combat is disabled in towns and their facilities.", out reason);
                    return true;
                }

                case HunterActionType.Betray:
                {
                    var target = FindHunter(request.targetHunterId);
                    if (!IsLivingOther(actor, target))
                        return Fail("Select another living hunter.", out reason);
                    if (!ShareGuild(actor, target) && !ShareParty(actor, target))
                        return Fail("There is no shared allegiance to betray.", out reason);
                    return true;
                }

                case HunterActionType.Reconcile:
                {
                    var target = FindHunter(request.targetHunterId);
                    if (!IsLivingOther(actor, target))
                        return Fail("Select another living hunter.", out reason);
                    if (actor.locationId != target.locationId)
                        return Fail("Both hunters must be at the same location.", out reason);
                    var relationship = FindRelationship(actor, target.id);
                    if (relationship == null ||
                        (relationship.grudge <= 0.01f && relationship.rivalry <= 0.01f && relationship.trust >= 0f))
                        return Fail("There is no meaningful conflict to reconcile.", out reason);
                    return true;
                }

                case HunterActionType.Recover:
                {
                    var location = FindLocation(actor.locationId);
                    if (location == null ||
                        (location.locationType != LocationType.Town &&
                         location.locationType != LocationType.Hospital))
                        return Fail("Recovery requires a town or hospital.", out reason);
                    if (!HasRecoverableInjury(actor))
                        return Fail("No injury is ready to recover.", out reason);
                    return true;
                }

                case HunterActionType.ClaimSite:
                {
                    var location = FindLocation(request.locationId);
                    if (location == null || location.locationType != LocationType.ResourceSite)
                        return Fail("Select a resource site to claim.", out reason);
                    if (actor.locationId != location.id)
                        return Fail("The hunter must be at the resource site.", out reason);
                    if (string.IsNullOrEmpty(actor.guildId) || FindGuild(actor.guildId) == null)
                        return Fail("Only a guild member can claim a resource site.", out reason);
                    if (location.controllingGuildId == actor.guildId)
                        return Fail("The hunter's guild already controls this site.", out reason);
                    return true;
                }

                default:
                    return Fail($"{request.actionType} is not implemented by the shared action service.", out reason);
            }
        }

        public EcosystemActionResult Execute(EcosystemActionRequest request)
        {
            if (!CanExecute(request, out var reason))
            {
                return EcosystemActionResult.Failed(reason);
            }

            var actor = FindHunter(request.actorHunterId);
            switch (NormalizeAction(request.actionType))
            {
                case HunterActionType.Wait:
                    return ExecuteWait(actor);
                case HunterActionType.Train:
                    return ExecuteTrain(actor);
                case HunterActionType.InvestAttribute:
                    return ExecuteInvestAttribute(actor, request.progressionId, request.pointAmount);
                case HunterActionType.LearnAbility:
                    return ExecuteLearnAbility(actor, request.progressionId);
                case HunterActionType.EquipAbility:
                    return ExecuteEquipAbility(actor, request.progressionId, request.slotIndex);
                case HunterActionType.EquipPassive:
                    return ExecuteEquipPassive(actor, request.progressionId, request.slotIndex);
                case HunterActionType.SaveAbilityPoints:
                    return ExecuteSaveAbilityPoints(actor, request.progressionId);
                case HunterActionType.Retire:
                    return ExecuteRetire(actor);
                case HunterActionType.JoinGuild:
                    return ExecuteJoinGuild(actor, FindGuild(request.guildId));
                case HunterActionType.LeaveGuild:
                    return ExecuteLeaveGuild(actor);
                case HunterActionType.FormParty:
                    return ExecuteFormParty(actor);
                case HunterActionType.JoinParty:
                    return ExecuteJoinParty(actor, FindParty(request.partyId));
                case HunterActionType.LeaveParty:
                    return ExecuteLeaveParty(actor);
                case HunterActionType.DisbandParty:
                    return ExecuteDisbandParty(actor);
                case HunterActionType.RecruitToGuild:
                    return ExecuteGuildRecruitment(actor, FindHunter(request.targetHunterId), request.guildId);
                case HunterActionType.AcceptContract:
                    return ExecuteAcceptContract(actor, FindContract(request.contractId));
                case HunterActionType.InviteToParty:
                    return ExecutePartyInvitation(actor, FindHunter(request.targetHunterId));
                case HunterActionType.AcceptInvitation:
                    return ExecuteAcceptInvitation(actor, FindInvitation(request.invitationId));
                case HunterActionType.RejectInvitation:
                    return ExecuteRejectInvitation(actor, FindInvitation(request.invitationId));
                case HunterActionType.EquipGear:
                    return ExecuteEquipGear(actor, FindGear(request.gearId));
                case HunterActionType.Travel:
                    return ExecuteTravel(actor, FindLocation(request.locationId));
                case HunterActionType.EnterDungeon:
                    return ExecuteEnterDungeon(actor, FindContract(request.contractId));
                case HunterActionType.Retreat:
                    return ExecuteRetreat(actor, FindContract(request.contractId));
                case HunterActionType.TradeGear:
                    return ExecuteTrade(actor, FindHunter(request.targetHunterId), FindGear(request.gearId));
                case HunterActionType.ClaimReward:
                    return ExecuteClaimReward(actor);
                case HunterActionType.Help:
                    return ExecuteHelp(actor, FindHunter(request.targetHunterId));
                case HunterActionType.Betray:
                    return ExecuteBetray(actor, FindHunter(request.targetHunterId));
                case HunterActionType.Challenge:
                    return ExecuteChallenge(actor, FindHunter(request.targetHunterId));
                case HunterActionType.Reconcile:
                    return ExecuteReconcile(actor, FindHunter(request.targetHunterId));
                case HunterActionType.Recover:
                    return ExecuteRecover(actor);
                case HunterActionType.ClaimSite:
                    return ExecuteClaimSite(actor, FindLocation(request.locationId));
                default:
                    return EcosystemActionResult.Failed($"{request.actionType} is not implemented.");
            }
        }

        /// <summary>
        /// Resolves all active contracts once, in stable contract-ID order.
        /// Call this from the deterministic simulation phase, never from Update.
        /// </summary>
        public void ResolveActiveContracts()
        {
            var active = new List<ContractState>();
            foreach (var contract in state.contracts)
            {
                if (contract != null &&
                    contract.status == ContractStatus.Active &&
                    state.day > contract.startedDay)
                {
                    active.Add(contract);
                }
            }
            active.Sort((left, right) => string.CompareOrdinal(left.id, right.id));

            foreach (var contract in active)
            {
                var encounter = FindEncounter(contract.activeEncounterId);
                if (encounter == null)
                {
                    var party = FindParty(contract.acceptedPartyId);
                    if (party != null)
                    {
                        encounter = encounterSimulation.BeginEncounter(contract, party);
                    }
                }

                // The exact same encounter snapshot advances in the foreground and in
                // autoresolve. A camera appearing or disappearing is never a resolution roll.
                if (encounter != null)
                {
                    if (encounter.status is DungeonEncounterStatus.Active or
                        DungeonEncounterStatus.Preparing or DungeonEncounterStatus.Paused)
                    {
                        continue;
                    }
                    ResolveEncounterOutcome(contract, encounter);
                    continue;
                }

                // Non-gate contracts retained from older content still use the coarse
                // campaign resolver until they receive their own spatial encounter type.
                ResolveActiveContract(contract);
            }
        }

        public void ExpireContract(ContractState contract)
        {
            if (contract == null ||
                (contract.status != ContractStatus.Offered && contract.status != ContractStatus.Accepted))
            {
                return;
            }

            SetContractStatus(contract, ContractStatus.Expired);
            contract.resolvedDay = state.day;
            var party = FindParty(contract.acceptedPartyId);
            if (party != null && party.activeContractId == contract.id)
            {
                party.activeContractId = string.Empty;
                party.destinationId = string.Empty;
                party.travelDaysRemaining = 0;
                party.status = PartyStatus.Forming;
            }
            foreach (var hunter in state.hunters)
            {
                if (hunter == null || hunter.activeContractId != contract.id) continue;
                hunter.activeContractId = string.Empty;
                hunter.destinationId = string.Empty;
                hunter.travelDaysRemaining = 0;
                if (hunter.IsActive) hunter.currentActivity = "Contract expired; awaiting new work";
            }
            AddWorldEvent(WorldEventType.ContractChanged, contract.acceptedHunterId, null,
                contract.issuerGuildId, contract.locationId, contract.id, -1f,
                $"{contract.displayName} expired before it could be completed.");
        }

        private EcosystemActionResult ExecuteWait(HunterProfile actor)
        {
            actor.currentActivity = "Waiting and observing the world";
            var summary = $"{actor.displayName} waits.";
            AddWorldEvent(WorldEventType.HunterAction, actor.id, null, actor.guildId,
                actor.locationId, null, 0f, summary);
            return EcosystemActionResult.Succeeded(summary);
        }

        private EcosystemActionResult ExecuteRetire(HunterProfile actor)
        {
            var formerGuildId = actor.guildId;
            var formerParty = FindParty(actor.partyId);
            RemoveGuildMembership(actor);
            RemovePartyMember(formerParty, actor);
            actor.isRetired = true;
            actor.retirementDay = state.day;
            actor.retirementReason = actor.wounds > 0
                ? "Left the profession after accumulated injuries"
                : "Chose to leave active hunter service";
            actor.currentActivity = "Retired from hunting";
            actor.destinationId = string.Empty;
            actor.travelDaysRemaining = 0;
            actor.activeContractId = string.Empty;
            foreach (var invitation in state.invitations)
            {
                if (invitation == null || invitation.status != InvitationStatus.Pending)
                {
                    continue;
                }
                if (invitation.senderHunterId == actor.id || invitation.recipientHunterId == actor.id)
                {
                    invitation.status = InvitationStatus.Withdrawn;
                }
            }
            var summary = $"{actor.displayName} retired from active hunting after " +
                          $"{Mathf.Max(0, state.day - actor.awakeningDay)} days of service.";
            AddWorldEvent(WorldEventType.HunterRetired, actor.id, null, formerGuildId,
                actor.locationId, null, 1f, summary);
            return EcosystemActionResult.Succeeded(summary);
        }

        private EcosystemActionResult ExecuteTrain(HunterProfile actor)
        {
            actor.career.lastTrainingDay = state.day;
            var experience = GrantExperience(actor, TrainingExperienceReward, "training");
            actor.currentActivity = "Training in town";
            var summary = experience.AbilityPointsGranted > 0
                ? $"{actor.displayName} trained, earned {experience.ExperienceGranted} XP, and gained " +
                  $"{experience.AbilityPointsGranted} Ability Point" +
                  (experience.AbilityPointsGranted == 1 ? "." : "s.")
                : $"{actor.displayName} trained and earned {experience.ExperienceGranted} XP.";
            AddWorldEvent(WorldEventType.HunterAction, actor.id, null, actor.guildId,
                actor.locationId, null, experience.ExperienceGranted, summary);
            return EcosystemActionResult.Succeeded(summary);
        }

        private EcosystemActionResult ExecuteInvestAttribute(
            HunterProfile actor,
            string attributeId,
            int amount)
        {
            var previousRank = EcosystemCareerRules.RankFor(actor);
            var previousBuild = EcosystemCareerRules.InferBuild(actor, gearCatalog).Label;
            if (!EcosystemCareerRules.TryInvestAttribute(actor, attributeId, amount, out var reason))
            {
                return EcosystemActionResult.Failed(reason);
            }

            var definition = EcosystemCareerCatalog.FindAttribute(attributeId);
            var summary = $"{actor.displayName} invested {amount} AP in {definition.displayName}.";
            actor.currentActivity = $"Developing {definition.displayName}";
            RecordCareerMutation(actor, previousRank, previousBuild, summary);
            return EcosystemActionResult.Succeeded(summary);
        }

        private EcosystemActionResult ExecuteLearnAbility(HunterProfile actor, string abilityId)
        {
            var previousRank = EcosystemCareerRules.RankFor(actor);
            var previousBuild = EcosystemCareerRules.InferBuild(actor, gearCatalog).Label;
            if (!EcosystemCareerRules.TryLearnAbility(actor, abilityId, out var reason))
            {
                return EcosystemActionResult.Failed(reason);
            }

            var definition = EcosystemCareerCatalog.FindAbility(abilityId);
            var summary = $"{actor.displayName} spent {definition.abilityPointCost} AP to learn " +
                          $"{definition.displayName}.";
            actor.currentActivity = $"Practicing {definition.displayName}";
            RecordCareerMutation(actor, previousRank, previousBuild, summary);
            return EcosystemActionResult.Succeeded(summary);
        }

        private EcosystemActionResult ExecuteEquipAbility(
            HunterProfile actor,
            string abilityId,
            int slotIndex)
        {
            if (!EcosystemCareerRules.TryEquipAbility(actor, abilityId, slotIndex, out var reason))
            {
                return EcosystemActionResult.Failed(reason);
            }

            var definition = EcosystemCareerCatalog.FindAbility(abilityId);
            var slotName = slotIndex == EcosystemCareerCatalog.UltimateSlotIndex
                ? "ultimate"
                : $"cooldown {slotIndex + 1}";
            var summary = $"{actor.displayName} equipped {definition.displayName} in the {slotName} slot.";
            actor.currentActivity = "Refining their active loadout";
            AddWorldEvent(WorldEventType.HunterBuildChanged, actor.id, null, actor.guildId,
                actor.locationId, null, slotIndex, summary);
            return EcosystemActionResult.Succeeded(summary);
        }

        private EcosystemActionResult ExecuteEquipPassive(
            HunterProfile actor,
            string abilityId,
            int slotIndex)
        {
            if (!EcosystemCareerRules.TryEquipPassive(actor, abilityId, slotIndex, out var reason))
            {
                return EcosystemActionResult.Failed(reason);
            }

            var definition = EcosystemCareerCatalog.FindAbility(abilityId);
            var summary = $"{actor.displayName} equipped {definition.displayName} in passive slot " +
                          $"{slotIndex + 1}.";
            actor.currentActivity = "Refining their passive loadout";
            AddWorldEvent(WorldEventType.HunterBuildChanged, actor.id, null, actor.guildId,
                actor.locationId, null, slotIndex, summary);
            return EcosystemActionResult.Succeeded(summary);
        }

        private EcosystemActionResult ExecuteSaveAbilityPoints(HunterProfile actor, string abilityId)
        {
            if (!EcosystemCareerRules.TryPlanAbility(actor, abilityId, out var reason))
            {
                return EcosystemActionResult.Failed(reason);
            }

            var definition = EcosystemCareerCatalog.FindAbility(abilityId);
            var missingPoints = Mathf.Max(0,
                definition.abilityPointCost - actor.career.UnspentAbilityPoints);
            var summary = missingPoints > 0
                ? $"{actor.displayName} reserved {actor.career.UnspentAbilityPoints} AP while saving " +
                  $"for {definition.displayName} ({missingPoints} more needed)."
                : $"{actor.displayName} reserved their AP for {definition.displayName}.";
            actor.currentActivity = $"Saving AP for {definition.displayName}";
            AddWorldEvent(WorldEventType.HunterProgressed, actor.id, null, actor.guildId,
                actor.locationId, null, actor.career.UnspentAbilityPoints, summary);
            return EcosystemActionResult.Succeeded(summary);
        }

        private EcosystemActionResult ExecuteJoinGuild(HunterProfile actor, GuildState guild)
        {
            SetGuildMembership(actor, guild);
            actor.currentActivity = $"Joined {guild.displayName}";
            var summary = $"{actor.displayName} joined {guild.displayName}.";
            AddMemory(actor, guild.id, "guild_joined", summary, 0.35f);
            AddWorldEvent(WorldEventType.GuildMembershipChanged, actor.id, null, guild.id,
                actor.locationId, null, 1f, summary);
            return EcosystemActionResult.Succeeded(summary);
        }

        private EcosystemActionResult ExecuteLeaveGuild(HunterProfile actor)
        {
            var guild = FindGuild(actor.guildId);
            RemoveGuildMembership(actor);
            actor.currentActivity = "Independent";
            var guildName = guild?.displayName ?? "their guild";
            var summary = $"{actor.displayName} left {guildName}.";
            AddMemory(actor, guild?.id, "guild_left", summary, -0.15f);
            AddWorldEvent(WorldEventType.GuildMembershipChanged, actor.id, null, guild?.id,
                actor.locationId, null, -1f, summary);
            return EcosystemActionResult.Succeeded(summary);
        }

        private EcosystemActionResult ExecuteFormParty(HunterProfile actor)
        {
            var party = CreateParty(actor);
            actor.currentActivity = $"Formed {party.displayName}";
            var summary = $"{actor.displayName} formed {party.displayName}.";
            AddWorldEvent(WorldEventType.PartyChanged, actor.id, null, actor.guildId,
                actor.locationId, null, 0f, summary);
            return EcosystemActionResult.Succeeded(summary);
        }

        private EcosystemActionResult ExecuteJoinParty(HunterProfile actor, PartyState party)
        {
            AddPartyMember(party, actor);
            var summary = $"{actor.displayName} joined {party.displayName}.";
            AddWorldEvent(WorldEventType.PartyChanged, actor.id, party.leaderHunterId,
                actor.guildId, actor.locationId, null, 0f, summary);
            return EcosystemActionResult.Succeeded(summary);
        }

        private EcosystemActionResult ExecuteLeaveParty(HunterProfile actor)
        {
            var party = FindParty(actor.partyId);
            RemovePartyMember(party, actor);
            actor.currentActivity = "Operating independently";
            var summary = $"{actor.displayName} left {party.displayName}.";
            AddWorldEvent(WorldEventType.PartyChanged, actor.id, party.leaderHunterId,
                actor.guildId, actor.locationId, null, 0f, summary);
            return EcosystemActionResult.Succeeded(summary);
        }

        private EcosystemActionResult ExecuteDisbandParty(HunterProfile actor)
        {
            var party = FindParty(actor.partyId);
            foreach (var memberId in new List<string>(party.memberIds))
            {
                var member = FindHunter(memberId);
                if (member == null) continue;
                member.partyId = string.Empty;
                member.activeContractId = string.Empty;
                member.currentEncounterId = string.Empty;
                member.destinationId = string.Empty;
                member.travelDaysRemaining = 0;
                if (member.IsActive) member.currentActivity = "Operating independently";
            }
            party.memberIds.Clear();
            party.leaderHunterId = string.Empty;
            party.activeContractId = string.Empty;
            party.destinationId = string.Empty;
            party.travelDaysRemaining = 0;
            party.status = PartyStatus.Disbanded;
            var summary = $"{actor.displayName} disbanded {party.displayName}.";
            AddWorldEvent(WorldEventType.PartyChanged, actor.id, null, actor.guildId,
                actor.locationId, null, 0f, summary);
            return EcosystemActionResult.Succeeded(summary);
        }

        private EcosystemActionResult ExecuteGuildRecruitment(
            HunterProfile actor,
            HunterProfile target,
            string requestedGuildId)
        {
            var guild = FindGuild(string.IsNullOrEmpty(requestedGuildId) ? actor.guildId : requestedGuildId);
            var invitation = new InvitationState
            {
                id = CreateId("guild-invite", actor.id, target.id, state.invitations.Count),
                invitationType = InvitationType.GuildMembership,
                status = InvitationStatus.Pending,
                senderHunterId = actor.id,
                recipientHunterId = target.id,
                guildId = guild.id,
                createdDay = state.day,
                expiresDay = state.day + 3
            };
            state.invitations.Add(invitation);
            TrimInvitations();
            var summary = $"{actor.displayName} invited {target.displayName} to {guild.displayName}.";
            AddMemory(target, actor.id, "guild_invitation_received", summary, 0.05f);
            AddWorldEvent(WorldEventType.HunterAction, actor.id, target.id, guild.id,
                actor.locationId, null, 0f, summary);
            return EcosystemActionResult.Succeeded(summary);
        }

        private EcosystemActionResult ExecuteAcceptContract(HunterProfile actor, ContractState contract)
        {
            var party = FindParty(actor.partyId);
            if (party == null || party.status == PartyStatus.Disbanded)
            {
                party = CreateParty(actor);
            }

            SetContractStatus(contract, ContractStatus.Accepted);
            contract.acceptedHunterId = actor.id;
            contract.acceptedPartyId = party.id;
            party.activeContractId = contract.id;
            party.status = PartyStatus.Forming;
            foreach (var memberId in party.memberIds)
            {
                var member = FindHunter(memberId);
                if (member == null || !member.IsActive) continue;
                member.activeContractId = contract.id;
                member.currentActivity = $"Preparing for {contract.displayName}";
            }

            var summary = $"{actor.displayName}'s party accepted {contract.displayName}.";
            AddWorldEvent(WorldEventType.ContractChanged, actor.id, null, actor.guildId,
                contract.locationId, contract.id, 1f, summary);
            return EcosystemActionResult.Succeeded(summary);
        }

        private EcosystemActionResult ExecutePartyInvitation(HunterProfile actor, HunterProfile target)
        {
            var party = FindParty(actor.partyId);
            var invitation = new InvitationState
            {
                id = CreateId("party-invite", actor.id, target.id, state.invitations.Count),
                invitationType = InvitationType.PartyMembership,
                status = InvitationStatus.Pending,
                senderHunterId = actor.id,
                recipientHunterId = target.id,
                partyId = party.id,
                contractId = party.activeContractId,
                createdDay = state.day,
                expiresDay = state.day + 2
            };
            state.invitations.Add(invitation);
            TrimInvitations();
            var summary = $"{actor.displayName} invited {target.displayName} to {party.displayName}.";
            AddWorldEvent(WorldEventType.PartyChanged, actor.id, target.id, actor.guildId,
                actor.locationId, party.activeContractId, 0f, summary);
            return EcosystemActionResult.Succeeded(summary);
        }

        private EcosystemActionResult ExecuteAcceptInvitation(
            HunterProfile actor,
            InvitationState invitation)
        {
            invitation.status = InvitationStatus.Accepted;
            string summary;
            if (invitation.invitationType == InvitationType.GuildMembership)
            {
                var guild = FindGuild(invitation.guildId);
                SetGuildMembership(actor, guild);
                actor.currentActivity = $"Joined {guild.displayName}";
                summary = $"{actor.displayName} accepted an invitation to {guild.displayName}.";
                AddWorldEvent(WorldEventType.GuildMembershipChanged, invitation.senderHunterId,
                    actor.id, guild.id, actor.locationId, null, 1f, summary);
            }
            else
            {
                var party = FindParty(invitation.partyId);
                AddPartyMember(party, actor);
                summary = $"{actor.displayName} joined {party.displayName}.";
                AddWorldEvent(WorldEventType.PartyChanged, invitation.senderHunterId, actor.id,
                    actor.guildId, actor.locationId, party.activeContractId, 1f, summary);
            }

            var sender = FindHunter(invitation.senderHunterId);
            if (sender != null)
            {
                AdjustRelationship(actor, sender, 0.08f, 0.1f, -0.03f, 0f, -0.03f);
                AddMemory(actor, sender.id, "invitation_accepted", summary, 0.2f);
            }
            return EcosystemActionResult.Succeeded(summary);
        }

        private EcosystemActionResult ExecuteRejectInvitation(
            HunterProfile actor,
            InvitationState invitation)
        {
            invitation.status = InvitationStatus.Declined;
            var sender = FindHunter(invitation.senderHunterId);
            var summary = $"{actor.displayName} rejected {sender?.displayName ?? "a hunter"}'s invitation.";
            if (sender != null)
            {
                AdjustRelationship(sender, actor, -0.03f, -0.02f, 0.03f, 0f, 0.01f);
                AddMemory(sender, actor.id, "invitation_rejected", summary, -0.12f);
            }
            AddWorldEvent(WorldEventType.RelationshipChanged, actor.id, sender?.id, actor.guildId,
                actor.locationId, invitation.contractId, -0.1f, summary);
            return EcosystemActionResult.Succeeded(summary);
        }

        private EcosystemActionResult ExecuteEquipGear(
            HunterProfile actor,
            EcosystemGearDefinition gear)
        {
            actor.equippedGearId = gear.GearId;
            if (actor.vitals != null)
            {
                var supportedShield = EcosystemWorldFactory.StartingShieldForGear(gearCatalog, gear.GearId);
                actor.vitals.currentShield = Mathf.Min(actor.vitals.currentShield, supportedShield);
                actor.vitals.Normalize(actor.isAlive);
            }
            actor.currentActivity = $"Equipped {gear.DisplayName} ({gear.TacticalRole})";
            var summary = $"{actor.displayName} equipped {gear.DisplayName}, gaining its {gear.TacticalRole} moves.";
            AddWorldEvent(WorldEventType.HunterAction, actor.id, null, actor.guildId,
                actor.locationId, actor.activeContractId, gear.Power, summary);
            return EcosystemActionResult.Succeeded(summary);
        }

        private EcosystemActionResult ExecuteTravel(HunterProfile actor, LocationState destination)
        {
            var route = FindRoute(actor.locationId, destination.id);
            var travelDays = Mathf.Max(1, route?.travelDays ?? 1);
            var party = FindParty(actor.partyId);
            if (party != null && party.status != PartyStatus.Disbanded)
            {
                foreach (var memberId in party.memberIds)
                {
                    var member = FindHunter(memberId);
                    if (member == null || !member.IsActive) continue;
                    member.destinationId = destination.id;
                    member.travelDaysRemaining = travelDays;
                    member.currentActivity = $"Travelling to {destination.displayName} ({travelDays}d)";
                }
                party.destinationId = destination.id;
                party.travelDaysRemaining = travelDays;
                party.status = PartyStatus.Travelling;
            }
            else
            {
                actor.destinationId = destination.id;
                actor.travelDaysRemaining = travelDays;
                actor.currentActivity = $"Travelling to {destination.displayName} ({travelDays}d)";
            }

            var summary = $"{actor.displayName}{(party == null ? string.Empty : "'s party")} began travelling to " +
                          $"{destination.displayName} ({travelDays} day{(travelDays == 1 ? string.Empty : "s")}).";
            AddWorldEvent(WorldEventType.HunterAction, actor.id, null, actor.guildId,
                actor.locationId, actor.activeContractId, -travelDays, summary);
            return EcosystemActionResult.Succeeded(summary);
        }

        /// <summary>
        /// Advances every persisted journey by one campaign day. Arrival is resolved before
        /// autonomous choices so player and AI parties can act at their destination that day.
        /// </summary>
        public void AdvanceTravelOneDay()
        {
            var partyMembers = new HashSet<string>(StringComparer.Ordinal);
            foreach (var party in state.parties)
            {
                if (party == null || party.status != PartyStatus.Travelling) continue;
                foreach (var memberId in party.memberIds) partyMembers.Add(memberId);

                party.travelDaysRemaining = Mathf.Max(0, party.travelDaysRemaining - 1);
                foreach (var memberId in party.memberIds)
                {
                    var member = FindHunter(memberId);
                    if (member == null || !member.IsActive) continue;
                    member.travelDaysRemaining = party.travelDaysRemaining;
                    member.destinationId = party.destinationId;
                }

                if (party.travelDaysRemaining == 0)
                {
                    CompletePartyTravel(party);
                }
            }

            foreach (var hunter in state.hunters)
            {
                if (hunter == null || !hunter.IsActive || partyMembers.Contains(hunter.id) ||
                    hunter.travelDaysRemaining <= 0)
                {
                    continue;
                }

                hunter.travelDaysRemaining--;
                if (hunter.travelDaysRemaining == 0)
                {
                    CompleteSoloTravel(hunter);
                }
            }
        }

        private EcosystemActionResult ExecuteEnterDungeon(HunterProfile actor, ContractState contract)
        {
            var party = FindParty(contract.acceptedPartyId);
            var dungeon = ContractDestination(contract);
            SetContractStatus(contract, ContractStatus.Active);
            contract.startedDay = state.day;
            party.status = PartyStatus.Active;
            party.locationId = dungeon.id;
            var encounter = encounterSimulation.BeginEncounter(contract, party);
            foreach (var memberId in party.memberIds)
            {
                var member = FindHunter(memberId);
                if (member == null || !member.IsActive) continue;
                member.activeContractId = contract.id;
                member.currentEncounterId = encounter.id;
                member.isIncapacitated = false;
                member.currentActivity = $"Inside {contract.displayName}";
            }
            var gate = FindGate(contract.gateId);
            var summary = gate == null
                ? $"{party.displayName} entered {contract.displayName}."
                : $"{party.displayName} entered {gate.displayName}: " +
                  $"{gate.biome}, {gate.layoutStyle}, {gate.areas.Count} areas.";
            AddWorldEvent(WorldEventType.ContractChanged, actor.id, null, actor.guildId,
                dungeon.id, contract.id, 1f, summary);
            return EcosystemActionResult.Succeeded(summary);
        }

        private EcosystemActionResult ExecuteRetreat(HunterProfile actor, ContractState contract)
        {
            var encounter = FindEncounter(contract.activeEncounterId);
            if (encounter != null)
            {
                encounterSimulation.MarkRetreated(encounter);
                encounterSimulation.CommitHunterSnapshotsToWorld(encounter, true);
            }
            SetContractStatus(contract, ContractStatus.Failed);
            contract.resolvedDay = state.day;
            var party = FindParty(contract.acceptedPartyId);
            var members = LivingPartyMembers(party);
            if (encounter != null)
            {
                StabilizeReleasedEncounterParticipants(
                    encounter,
                    contract,
                    members,
                    true);
            }
            foreach (var member in members)
            {
                member.fame = Mathf.Max(0f, member.fame - 0.25f);
                AddMemory(member, contract.id, "dungeon_retreat",
                    $"Retreated from {contract.displayName} under {actor.displayName}'s command.", -0.2f);
            }
            AdjustPartyRelationships(members, -0.03f, -0.04f, 0.02f, 0.01f);
            FinalizePartyContract(party);
            var summary = $"{party.displayName} retreated from {contract.displayName}.";
            AddWorldEvent(WorldEventType.ContractChanged, actor.id, null, actor.guildId,
                ContractDestination(contract)?.id, contract.id, -1f, summary);
            return EcosystemActionResult.Succeeded(summary);
        }

        private EcosystemActionResult ExecuteTrade(
            HunterProfile actor,
            HunterProfile target,
            EcosystemGearDefinition gear)
        {
            var price = TradePrice(gear);
            actor.inventoryGearIds.Remove(gear.GearId);
            if (!target.inventoryGearIds.Contains(gear.GearId))
            {
                target.inventoryGearIds.Add(gear.GearId);
            }
            target.gold -= price;
            actor.gold += price;
            if (actor.equippedGearId == gear.GearId)
            {
                actor.equippedGearId = string.Empty;
                if (actor.vitals != null)
                {
                    actor.vitals.currentShield = 0;
                }
            }
            var summary = $"{actor.displayName} sold {gear.DisplayName} to {target.displayName} for {price} gold.";
            AddWorldEvent(WorldEventType.HunterAction, actor.id, target.id, actor.guildId,
                actor.locationId, null, price, summary);
            return EcosystemActionResult.Succeeded(summary);
        }

        private EcosystemActionResult ExecuteClaimReward(HunterProfile actor)
        {
            var amount = actor.pendingRewardGold;
            actor.pendingRewardGold = 0;
            actor.gold += amount;
            var summary = $"{actor.displayName} claimed {amount} gold in completed rewards.";
            AddWorldEvent(WorldEventType.RewardGranted, actor.id, null, actor.guildId,
                actor.locationId, null, amount, summary);
            return EcosystemActionResult.Succeeded(summary);
        }

        private EcosystemActionResult ExecuteHelp(HunterProfile actor, HunterProfile target)
        {
            var recovered = HealOneEligibleInjury(target, true);
            AdjustRelationship(target, actor, 0.12f, 0.14f, -0.06f, 0.12f, -0.05f);
            AdjustRelationship(actor, target, 0.05f, 0.04f, -0.02f, 0f, -0.02f);
            actor.fame += 0.35f;
            var summary = recovered
                ? $"{actor.displayName} helped {target.displayName} recover from an injury."
                : $"{actor.displayName} helped {target.displayName} with their current troubles.";
            AddMemory(target, actor.id, "help_received", summary, 0.45f);
            AddMemory(actor, target.id, "help_given", summary, 0.25f);
            AddWorldEvent(WorldEventType.RelationshipChanged, actor.id, target.id, actor.guildId,
                actor.locationId, null, 0.4f, summary);
            return EcosystemActionResult.Succeeded(summary);
        }

        private EcosystemActionResult ExecuteBetray(HunterProfile actor, HunterProfile target)
        {
            var formerGuildId = actor.guildId;
            if (ShareParty(actor, target))
            {
                RemovePartyMember(FindParty(actor.partyId), actor);
            }
            if (ShareGuild(actor, target))
            {
                RemoveGuildMembership(actor);
            }
            AdjustRelationship(target, actor, -0.35f, -0.55f, 0.35f, -0.15f, 0.5f);
            AdjustRelationship(actor, target, -0.12f, -0.2f, 0.2f, 0f, 0.12f);
            actor.fame = Mathf.Max(0f, actor.fame - 0.5f);
            var summary = $"{actor.displayName} betrayed {target.displayName}.";
            AddMemory(target, actor.id, "betrayal", summary, -0.9f);
            AddMemory(actor, target.id, "betrayal_committed", summary, -0.45f);
            var targetGuild = FindGuild(target.guildId);
            if (!string.IsNullOrEmpty(formerGuildId) && targetGuild != null)
            {
                AdjustDiplomacy(FindGuild(formerGuildId), targetGuild, -0.2f, 0.3f, summary);
            }
            AddWorldEvent(WorldEventType.RelationshipChanged, actor.id, target.id, formerGuildId,
                actor.locationId, null, -0.8f, summary);
            return EcosystemActionResult.Succeeded(summary);
        }

        private EcosystemActionResult ExecuteChallenge(HunterProfile actor, HunterProfile target)
        {
            var actorPower = HunterPower(actor);
            var targetPower = HunterPower(target);
            var actorChance = Mathf.Clamp(
                0.5f + (actorPower - targetPower) / Mathf.Max(20f, actorPower + targetPower),
                0.15f,
                0.85f);
            var actorWon = random.Next01($"challenge:{actor.id}:{target.id}") <= actorChance;
            var winner = actorWon ? actor : target;
            var loser = actorWon ? target : actor;
            GrantExperience(winner, 8, $"challenge victory over {loser.displayName}");
            winner.fame += 0.5f;
            ApplyInjury(loser, InjurySeverity.Minor, $"challenge with {winner.displayName}", null);
            AdjustRelationship(actor, target, -0.04f, -0.03f, 0.16f, 0f, 0.05f);
            AdjustRelationship(target, actor, -0.04f, -0.03f, 0.16f, 0f, 0.05f);
            var summary = $"{winner.displayName} defeated {loser.displayName} in a challenge.";
            AddMemory(actor, target.id, actorWon ? "challenge_victory" : "challenge_defeat",
                summary, actorWon ? 0.35f : -0.35f);
            AddMemory(target, actor.id, actorWon ? "challenge_defeat" : "challenge_victory",
                summary, actorWon ? -0.35f : 0.35f);
            AddWorldEvent(WorldEventType.RelationshipChanged, actor.id, target.id, actor.guildId,
                actor.locationId, null, actorWon ? 1f : -1f, summary);
            return EcosystemActionResult.Succeeded(summary);
        }

        private EcosystemActionResult ExecuteReconcile(HunterProfile actor, HunterProfile target)
        {
            AdjustRelationship(actor, target, 0.12f, 0.14f, -0.2f, 0f, -0.25f);
            AdjustRelationship(target, actor, 0.08f, 0.1f, -0.16f, 0f, -0.18f);
            var summary = $"{actor.displayName} reconciled with {target.displayName}.";
            AddMemory(actor, target.id, "reconciliation", summary, 0.35f);
            AddMemory(target, actor.id, "reconciliation", summary, 0.3f);
            AddWorldEvent(WorldEventType.RelationshipChanged, actor.id, target.id, actor.guildId,
                actor.locationId, null, 0.45f, summary);
            return EcosystemActionResult.Succeeded(summary);
        }

        private EcosystemActionResult ExecuteRecover(HunterProfile actor)
        {
            HealOneEligibleInjury(actor, false);
            if (actor.vitals != null)
            {
                actor.vitals.currentMana = actor.vitals.maximumMana;
                actor.vitals.currentShield = Mathf.Max(
                    actor.vitals.currentShield,
                    EcosystemWorldFactory.StartingShieldForGear(gearCatalog, actor.equippedGearId));
                actor.vitals.Normalize(actor.isAlive);
            }
            actor.currentActivity = "Recovering in town";
            var summary = $"{actor.displayName} recovered from an injury.";
            AddWorldEvent(WorldEventType.HunterRecovered, actor.id, null, actor.guildId,
                actor.locationId, null, 1f, summary);
            return EcosystemActionResult.Succeeded(summary);
        }

        private EcosystemActionResult ExecuteClaimSite(HunterProfile actor, LocationState location)
        {
            var guild = FindGuild(actor.guildId);
            var previousGuild = FindGuild(location.controllingGuildId);
            if (previousGuild != null)
            {
                previousGuild.controlledLocationIds.Remove(location.id);
                RecalculateTerritory(previousGuild);
            }
            location.controllingGuildId = guild.id;
            if (!guild.controlledLocationIds.Contains(location.id))
            {
                guild.controlledLocationIds.Add(location.id);
            }
            guild.resources += Mathf.Max(0, location.resourceYield);
            guild.prestige += 1f + location.resourceYield * 0.1f;
            RecalculateTerritory(guild);
            var summary = $"{actor.displayName} claimed {location.displayName} for {guild.displayName}.";
            AddMemory(actor, location.id, "site_claimed", summary, 0.45f);
            if (previousGuild != null && previousGuild != guild)
            {
                AdjustDiplomacy(guild, previousGuild, -0.25f, 0.35f, summary);
                AdjustDiplomacy(previousGuild, guild, -0.25f, 0.45f, summary);
            }
            AddWorldEvent(WorldEventType.LocationControlChanged, actor.id, null, guild.id,
                location.id, null, location.resourceYield, summary);
            return EcosystemActionResult.Succeeded(summary);
        }

        private void ResolveEncounterOutcome(
            ContractState contract,
            DungeonEncounterState encounter)
        {
            var party = FindParty(contract.acceptedPartyId);
            encounterSimulation.CommitHunterSnapshotsToWorld(encounter, true);
            var members = LivingPartyMembers(party);
            StabilizeReleasedEncounterParticipants(
                encounter,
                contract,
                members,
                encounter.status == DungeonEncounterStatus.Succeeded);

            if (encounter.status == DungeonEncounterStatus.Succeeded &&
                party != null && members.Count > 0)
            {
                var roles = new HashSet<TacticalRole>();
                foreach (var member in members)
                {
                    var gear = FindGear(member.equippedGearId);
                    if (gear != null) roles.Add(gear.TacticalRole);
                }
                GrantEncounterSpoils(encounter, party, members);
                ResolveContractSuccess(contract, party, members, roles.Count);
                return;
            }

            if (party == null || members.Count == 0)
            {
                SetContractStatus(contract, ContractStatus.Failed);
                contract.resolvedDay = state.day;
                FinalizePartyContract(party);
                AddWorldEvent(WorldEventType.ContractChanged, null, null,
                    contract.issuerGuildId, ContractDestination(contract)?.id, contract.id, -1f,
                    $"{contract.displayName} failed because no active hunter escaped the gate.");
                return;
            }
            ResolveContractFailure(contract, party, members);
        }

        private void StabilizeReleasedEncounterParticipants(
            DungeonEncounterState encounter,
            ContractState contract,
            List<HunterProfile> currentPartyMembers,
            bool includeCurrentParty)
        {
            var currentMemberIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var member in currentPartyMembers)
            {
                if (member != null) currentMemberIds.Add(member.id);
            }

            foreach (var participant in encounter.participants)
            {
                if (participant == null ||
                    participant.participantKind != EncounterParticipantKind.Hunter ||
                    (!includeCurrentParty && currentMemberIds.Contains(participant.sourceHunterId)))
                {
                    continue;
                }
                var member = FindHunter(participant.sourceHunterId);
                if (member == null || !member.IsActive || !member.isIncapacitated)
                {
                    continue;
                }

                // Once an encounter is released, any living downed participant must either be
                // evacuated into campaign injury state or die. This also covers a hunter whose
                // party membership changed while the canonical encounter was still active.
                ApplyInjury(
                    member,
                    InjurySeverity.Severe,
                    $"evacuation from {contract.displayName}",
                    contract.id);
                if (member.IsActive)
                {
                    member.vitals.currentHealth = Mathf.Max(1, member.vitals.currentHealth);
                    member.isIncapacitated = false;
                }
            }
        }

        private void GrantEncounterSpoils(
            DungeonEncounterState encounter,
            PartyState party,
            List<HunterProfile> members)
        {
            var lootGold = 0;
            var extractedResources = 0;
            if (encounter.lootNodes != null)
            {
                foreach (var loot in encounter.lootNodes)
                {
                    if (loot == null || loot.status != DungeonLootStatus.Claimed) continue;
                    lootGold += Mathf.Max(0, loot.gold);
                    extractedResources += Mathf.Max(0, loot.guildResources);
                }
            }
            if (encounter.resourceNodes != null)
            {
                foreach (var resource in encounter.resourceNodes)
                {
                    if (resource == null) continue;
                    extractedResources += Mathf.Max(
                        0,
                        resource.initialAmount - resource.remainingAmount);
                }
            }

            if (lootGold > 0 && members.Count > 0)
            {
                var share = lootGold / members.Count;
                var remainder = lootGold - share * members.Count;
                foreach (var member in members)
                {
                    member.pendingRewardGold += share;
                    if (member.id == party.leaderHunterId)
                    {
                        member.pendingRewardGold += remainder;
                    }
                }
            }

            var leaderGuild = FindGuild(FindHunter(party.leaderHunterId)?.guildId);
            if (leaderGuild != null)
            {
                leaderGuild.resources += extractedResources;
            }
        }

        private void ResolveActiveContract(ContractState contract)
        {
            var party = FindParty(contract.acceptedPartyId);
            var members = LivingPartyMembers(party);
            if (party == null || members.Count == 0)
            {
                SetContractStatus(contract, ContractStatus.Failed);
                contract.resolvedDay = state.day;
                if (party != null) FinalizePartyContract(party);
                foreach (var hunter in state.hunters)
                {
                    if (hunter != null && hunter.activeContractId == contract.id)
                    {
                        hunter.activeContractId = string.Empty;
                    }
                }
                AddWorldEvent(WorldEventType.ContractChanged, null, null, contract.issuerGuildId,
                    ContractDestination(contract)?.id, contract.id, -1f,
                    $"{contract.displayName} failed because no living party remained.");
                return;
            }

            var totalPower = 0f;
            var roles = new HashSet<TacticalRole>();
            foreach (var member in members)
            {
                totalPower += HunterPower(member);
                var gear = FindGear(member.equippedGearId);
                if (gear != null) roles.Add(gear.TacticalRole);
            }
            totalPower += Mathf.Max(0, roles.Count - 1) * 9f;
            totalPower += PartyRelationshipSynergy(members) * 10f;

            var opposition = 28f + Mathf.Max(1, contract.difficulty) * 42f;
            var successChance = Mathf.Clamp(
                0.12f + totalPower / Mathf.Max(1f, totalPower + opposition),
                0.12f,
                0.92f);
            var roll = random.Next01($"contract:{contract.id}:resolve");
            if (roll <= successChance)
            {
                ResolveContractSuccess(contract, party, members, roles.Count);
            }
            else
            {
                ResolveContractFailure(contract, party, members);
            }
        }

        private void ResolveContractSuccess(
            ContractState contract,
            PartyState party,
            List<HunterProfile> members,
            int roleCount)
        {
            SetContractStatus(contract, ContractStatus.Succeeded);
            contract.resolvedDay = state.day;
            var share = members.Count == 0 ? 0 : contract.rewardGold / members.Count;
            var remainder = contract.rewardGold - share * members.Count;
            foreach (var member in members)
            {
                member.pendingRewardGold += share;
                if (member.id == party.leaderHunterId)
                {
                    member.pendingRewardGold += remainder;
                }
                member.fame += contract.rewardFame / Mathf.Max(1, members.Count);
                GrantExperience(member, Mathf.Max(8, contract.difficulty * 15), contract.displayName);
                AddMemory(member, contract.id, "contract_victory",
                    $"Cleared {contract.displayName} with {party.displayName}.", 0.65f);
            }

            AdjustPartyRelationships(members, 0.1f, 0.14f, -0.05f, -0.04f);
            var beneficiary = FindGuild(FindHunter(party.leaderHunterId)?.guildId);
            if (beneficiary != null)
            {
                beneficiary.resources += Mathf.Max(0, contract.rewardResources);
                beneficiary.prestige += contract.rewardFame + contract.difficulty * 0.25f;
            }

            FinalizePartyContract(party);
            var summary =
                $"{party.displayName} cleared {contract.displayName} with {roleCount} tactical role" +
                (roleCount == 1 ? "." : "s represented.");
            AddWorldEvent(WorldEventType.ContractChanged, party.leaderHunterId, null,
                beneficiary?.id, ContractDestination(contract)?.id, contract.id, contract.rewardGold, summary);
        }

        private void ResolveContractFailure(
            ContractState contract,
            PartyState party,
            List<HunterProfile> members)
        {
            SetContractStatus(contract, ContractStatus.Failed);
            contract.resolvedDay = state.day;
            foreach (var member in members)
            {
                var injuryRoll = random.Next01($"contract:{contract.id}:injury:{member.id}");
                var severity = injuryRoll < 0.42f
                    ? InjurySeverity.Minor
                    : injuryRoll < 0.72f
                        ? InjurySeverity.Moderate
                        : injuryRoll < 0.92f
                            ? InjurySeverity.Severe
                            : InjurySeverity.Critical;
                ApplyInjury(member, severity, contract.displayName, contract.id);
                if (member.IsActive && member.vitals != null &&
                    member.vitals.currentHealth > 0)
                {
                    member.isIncapacitated = false;
                }
                if (member.IsActive)
                {
                    member.fame = Mathf.Max(0f, member.fame - 0.2f);
                    AddMemory(member, contract.id, "contract_failure",
                        $"Was injured when {party.displayName} failed {contract.displayName}.", -0.55f);
                }
            }
            AdjustPartyRelationships(members, -0.06f, -0.1f, 0.05f, 0.06f);
            FinalizePartyContract(party);
            var summary = $"{party.displayName} failed {contract.displayName} and returned injured.";
            AddWorldEvent(WorldEventType.ContractChanged, party.leaderHunterId, null,
                FindHunter(party.leaderHunterId)?.guildId, ContractDestination(contract)?.id,
                contract.id, -1f, summary);
        }

        private bool CanRespondToInvitation(
            HunterProfile actor,
            string invitationId,
            bool accepting,
            out string reason)
        {
            var invitation = FindInvitation(invitationId);
            if (invitation == null) return Fail("The invitation does not exist.", out reason);
            if (invitation.recipientHunterId != actor.id)
                return Fail("Only the invited hunter can respond.", out reason);
            if (invitation.status != InvitationStatus.Pending)
                return Fail("The invitation has already been resolved.", out reason);
            if (state.day > invitation.expiresDay)
                return Fail("The invitation has expired.", out reason);
            if (!accepting)
            {
                reason = string.Empty;
                return true;
            }

            switch (invitation.invitationType)
            {
                case InvitationType.GuildMembership:
                    if (FindGuild(invitation.guildId) == null)
                        return Fail("The inviting guild no longer exists.", out reason);
                    if (!string.IsNullOrEmpty(actor.guildId))
                        return Fail("The invited hunter already belongs to a guild.", out reason);
                    break;
                case InvitationType.PartyMembership:
                case InvitationType.ContractParticipation:
                {
                    var party = FindParty(invitation.partyId);
                    if (party == null || party.status == PartyStatus.Disbanded)
                        return Fail("The inviting party no longer exists.", out reason);
                    if (!string.IsNullOrEmpty(actor.partyId))
                        return Fail("The invited hunter already belongs to a party.", out reason);
                    if (party.memberIds.Count >= MaximumPartySize)
                        return Fail("The inviting party is full.", out reason);
                    var leader = FindHunter(party.leaderHunterId);
                    if (leader == null || leader.locationId != actor.locationId)
                        return Fail("The invited hunter is no longer with the party.", out reason);
                    break;
                }
                default:
                    return Fail("That invitation type is not handled by hunter actions.", out reason);
            }

            reason = string.Empty;
            return true;
        }

        private bool CanEnterDungeon(HunterProfile actor, string contractId, out string reason)
        {
            var contract = FindContract(contractId);
            if (contract == null) return Fail("The selected contract does not exist.", out reason);
            if (contract.status != ContractStatus.Accepted)
                return Fail("The contract must be accepted before entering its dungeon.", out reason);
            var location = ContractDestination(contract);
            if (location == null || location.locationType != LocationType.Dungeon)
                return Fail("The contract is not assigned to a valid dungeon.", out reason);
            var party = FindParty(contract.acceptedPartyId);
            if (party == null || party.status == PartyStatus.Disbanded)
                return Fail("The accepted party no longer exists.", out reason);
            if (party.leaderHunterId != actor.id)
                return Fail("Only the accepted party leader can enter the dungeon.", out reason);
            if (actor.locationId != location.id)
                return Fail("The party leader must travel to the dungeon first.", out reason);
            foreach (var memberId in party.memberIds)
            {
                var member = FindHunter(memberId);
                if (member == null || !member.IsActive)
                    return Fail("The party contains an unavailable hunter.", out reason);
                if (member.locationId != location.id)
                    return Fail("Every party member must be at the dungeon.", out reason);
            }
            reason = string.Empty;
            return true;
        }

        private PartyState CreateParty(HunterProfile leader)
        {
            var party = new PartyState
            {
                id = CreateId("party", leader.id, null, state.parties.Count),
                displayName = $"{leader.displayName}'s Party",
                leaderHunterId = leader.id,
                memberIds = new List<string> { leader.id },
                status = PartyStatus.Forming,
                locationId = leader.locationId,
                createdDay = state.day
            };
            state.parties.Add(party);
            leader.partyId = party.id;
            return party;
        }

        private void AddPartyMember(PartyState party, HunterProfile hunter)
        {
            if (!party.memberIds.Contains(hunter.id))
            {
                party.memberIds.Add(hunter.id);
            }
            hunter.partyId = party.id;
            hunter.activeContractId = party.activeContractId;
            hunter.currentActivity = string.IsNullOrEmpty(party.activeContractId)
                ? $"Joined {party.displayName}"
                : $"Preparing with {party.displayName}";
        }

        private void RemovePartyMember(PartyState party, HunterProfile hunter)
        {
            if (party == null || hunter == null) return;
            party.memberIds.Remove(hunter.id);
            hunter.partyId = string.Empty;
            hunter.activeContractId = string.Empty;
            hunter.destinationId = string.Empty;
            hunter.travelDaysRemaining = 0;
            if (party.memberIds.Count == 0)
            {
                party.status = PartyStatus.Disbanded;
                party.leaderHunterId = string.Empty;
                party.activeContractId = string.Empty;
            }
            else if (party.leaderHunterId == hunter.id)
            {
                party.memberIds.Sort(StringComparer.Ordinal);
                party.leaderHunterId = party.memberIds[0];
            }
        }

        private void FinalizePartyContract(PartyState party)
        {
            if (party == null) return;
            foreach (var memberId in party.memberIds)
            {
                var member = FindHunter(memberId);
                if (member == null) continue;
                member.activeContractId = string.Empty;
                member.destinationId = string.Empty;
                member.travelDaysRemaining = 0;
                if (member.IsActive)
                {
                    member.currentActivity = "Ready for another expedition";
                }
            }
            party.activeContractId = string.Empty;
            party.destinationId = string.Empty;
            party.travelDaysRemaining = 0;
            party.status = party.memberIds.Count == 0
                ? PartyStatus.Disbanded
                : PartyStatus.Forming;
        }

        private void SetGuildMembership(HunterProfile hunter, GuildState guild)
        {
            RemoveHunterFromAllGuildRosters(hunter.id);
            hunter.guildId = guild?.id ?? string.Empty;
            if (guild != null && !guild.memberIds.Contains(hunter.id))
            {
                guild.memberIds.Add(hunter.id);
                guild.memberIds.Sort(StringComparer.Ordinal);
            }
        }

        private void RemoveGuildMembership(HunterProfile hunter)
        {
            RemoveHunterFromAllGuildRosters(hunter.id);
            hunter.guildId = string.Empty;
        }

        private void RemoveHunterFromAllGuildRosters(string hunterId)
        {
            foreach (var guild in state.guilds)
            {
                if (guild == null) continue;
                while (guild.memberIds.Remove(hunterId))
                {
                }
            }
        }

        private void ApplyInjury(
            HunterProfile hunter,
            InjurySeverity severity,
            string cause,
            string sourceEventId)
        {
            if (hunter == null || !hunter.IsActive) return;
            var recoveryDays = severity switch
            {
                InjurySeverity.Minor => 2,
                InjurySeverity.Moderate => 4,
                InjurySeverity.Severe => 7,
                InjurySeverity.Critical => 10,
                _ => 2
            };
            var injury = new HunterInjury
            {
                id = CreateId("injury", hunter.id, sourceEventId, hunter.injuries.Count),
                displayName = $"{severity} injury from {cause}",
                severity = severity,
                sufferedDay = state.day,
                recoveryDay = state.day + recoveryDays,
                sourceEventId = sourceEventId,
                healed = false
            };
            hunter.injuries.Add(injury);
            hunter.wounds++;
            hunter.vitals ??= new HunterVitalsState();
            if (!hunter.vitals.initialized)
            {
                hunter.vitals.Initialize(
                    hunter.isAlive,
                    Mathf.Max(0, hunter.wounds - 1),
                    EcosystemWorldFactory.StartingShieldForGear(gearCatalog, hunter.equippedGearId));
            }
            var campaignDamage = severity switch
            {
                InjurySeverity.Minor => 15,
                InjurySeverity.Moderate => 28,
                InjurySeverity.Severe => 45,
                InjurySeverity.Critical => 70,
                _ => 15
            };
            hunter.vitals.ApplyDamagePreservingLife(campaignDamage);
            hunter.currentActivity = $"Recovering from a {severity.ToString().ToLowerInvariant()} injury";
            AddWorldEvent(WorldEventType.HunterInjured, hunter.id, null, hunter.guildId,
                hunter.locationId, sourceEventId, (int)severity + 1, injury.displayName);

            if (severity == InjurySeverity.Critical)
            {
                var existingBurden = InjuryBurden(hunter);
                var deathChance = Mathf.Clamp(0.1f + existingBurden * 0.035f, 0.1f, 0.65f);
                if (random.Next01($"death:{hunter.id}:{sourceEventId}:{injury.id}") <= deathChance)
                {
                    KillHunter(hunter, cause, sourceEventId);
                }
            }
        }

        private void KillHunter(HunterProfile hunter, string cause, string sourceEventId)
        {
            if (!hunter.IsActive) return;
            hunter.isAlive = false;
            hunter.isRetired = false;
            hunter.retirementDay = -1;
            hunter.retirementReason = string.Empty;
            hunter.deathDay = state.day;
            hunter.deathCause = cause;
            hunter.currentActivity = "Dead";
            hunter.vitals ??= new HunterVitalsState();
            hunter.vitals.MarkDead();
            var formerGuildId = hunter.guildId;
            RemoveGuildMembership(hunter);
            RemovePartyMember(FindParty(hunter.partyId), hunter);
            hunter.activeContractId = string.Empty;
            hunter.currentEncounterId = string.Empty;
            hunter.isIncapacitated = false;
            AddWorldEvent(WorldEventType.HunterDied, hunter.id, null, formerGuildId,
                hunter.locationId, sourceEventId, -1f,
                $"{hunter.displayName} died from injuries suffered during {cause}.");
        }

        private bool HealOneEligibleInjury(HunterProfile hunter, bool allowEarlyHelp)
        {
            HunterInjury selected = null;
            foreach (var injury in hunter.injuries)
            {
                if (injury == null || injury.healed) continue;
                if (!allowEarlyHelp && injury.recoveryDay > state.day) continue;
                if (selected == null || injury.recoveryDay < selected.recoveryDay)
                {
                    selected = injury;
                }
            }
            if (selected != null)
            {
                selected.healed = true;
                hunter.wounds = Mathf.Max(0, hunter.wounds - 1);
                var restoredHealth = selected.severity switch
                {
                    InjurySeverity.Minor => 18,
                    InjurySeverity.Moderate => 32,
                    InjurySeverity.Severe => 52,
                    InjurySeverity.Critical => 75,
                    _ => 18
                };
                hunter.vitals?.Restore(restoredHealth, 0, 0);
                return true;
            }
            if (hunter.wounds > 0)
            {
                hunter.wounds--;
                hunter.vitals?.Restore(18, 0, 0);
                return true;
            }
            return false;
        }

        private bool HasRecoverableInjury(HunterProfile hunter)
        {
            if (hunter.wounds > 0 && hunter.injuries.Count == 0) return true;
            foreach (var injury in hunter.injuries)
            {
                if (injury != null && !injury.healed && injury.recoveryDay <= state.day)
                {
                    return true;
                }
            }
            return false;
        }

        private HunterExperienceResult GrantExperience(
            HunterProfile hunter,
            int amount,
            string source = "world activity")
        {
            var result = EcosystemCareerRules.GrantExperience(hunter, amount);
            if (result.ExperienceGranted <= 0)
            {
                return result;
            }

            var pointText = result.AbilityPointsGranted > 0
                ? $" and gained {result.AbilityPointsGranted} Ability Point" +
                  (result.AbilityPointsGranted == 1 ? "" : "s")
                : "";
            AddWorldEvent(WorldEventType.HunterProgressed, hunter.id, null, hunter.guildId,
                hunter.locationId, hunter.activeContractId, result.ExperienceGranted,
                $"{hunter.displayName} earned {result.ExperienceGranted} XP from {source}{pointText}.");
            return result;
        }

        private void RecordCareerMutation(
            HunterProfile hunter,
            HunterRank previousRank,
            string previousBuild,
            string summary)
        {
            var currentBuild = EcosystemCareerRules.InferBuild(hunter, gearCatalog).Label;
            var buildSummary = previousBuild == currentBuild
                ? $"{summary} Their {currentBuild} build grew stronger."
                : $"{summary} Their assessed build shifted from {previousBuild} to {currentBuild}.";
            AddWorldEvent(WorldEventType.HunterBuildChanged, hunter.id, null, hunter.guildId,
                hunter.locationId, null, hunter.career.InvestedAbilityPoints, buildSummary);

            var currentRank = EcosystemCareerRules.RankFor(hunter);
            if (currentRank != previousRank)
            {
                AddWorldEvent(WorldEventType.HunterRankChanged, hunter.id, null, hunter.guildId,
                    hunter.locationId, null, hunter.career.InvestedAbilityPoints,
                    $"The Association now assesses {hunter.displayName} as rank {currentRank}, " +
                    $"up from rank {previousRank}.");
            }
        }

        private float HunterPower(HunterProfile hunter)
        {
            return EcosystemCareerRules.CombatPower(hunter, gearCatalog);
        }

        private static int InjuryBurden(HunterProfile hunter)
        {
            var burden = 0;
            foreach (var injury in hunter.injuries)
            {
                if (injury != null && !injury.healed)
                {
                    burden += (int)injury.severity + 1;
                }
            }
            return burden;
        }

        private float PartyRelationshipSynergy(IReadOnlyList<HunterProfile> members)
        {
            var total = 0f;
            var pairs = 0;
            for (var left = 0; left < members.Count; left++)
            {
                for (var right = left + 1; right < members.Count; right++)
                {
                    var forward = FindRelationship(members[left], members[right].id);
                    var reverse = FindRelationship(members[right], members[left].id);
                    total += RelationshipSynergy(forward) + RelationshipSynergy(reverse);
                    pairs += 2;
                }
            }
            return pairs == 0 ? 0f : total / pairs;
        }

        private static float RelationshipSynergy(HunterRelationship relationship)
        {
            return relationship == null
                ? 0f
                : relationship.trust + relationship.affinity -
                  relationship.rivalry - relationship.grudge;
        }

        private void AdjustPartyRelationships(
            IReadOnlyList<HunterProfile> members,
            float affinity,
            float trust,
            float rivalry,
            float grudge)
        {
            for (var left = 0; left < members.Count; left++)
            {
                for (var right = 0; right < members.Count; right++)
                {
                    if (left == right) continue;
                    AdjustRelationship(members[left], members[right], affinity, trust,
                        rivalry, 0f, grudge);
                }
            }
        }

        private void AdjustRelationship(
            HunterProfile owner,
            HunterProfile subject,
            float affinity,
            float trust,
            float rivalry,
            float debt,
            float grudge)
        {
            if (owner == null || subject == null || owner.id == subject.id) return;
            var relationship = GetOrCreateRelationship(owner, subject.id);
            relationship.affinity = Mathf.Clamp(relationship.affinity + affinity, -1f, 1f);
            relationship.trust = Mathf.Clamp(relationship.trust + trust, -1f, 1f);
            relationship.rivalry = Mathf.Clamp01(relationship.rivalry + rivalry);
            relationship.debt = Mathf.Clamp(relationship.debt + debt, -1f, 1f);
            relationship.grudge = Mathf.Clamp01(relationship.grudge + grudge);
        }

        private static HunterRelationship FindRelationship(HunterProfile owner, string subjectId)
        {
            return owner?.relationships?.Find(item => item != null && item.hunterId == subjectId);
        }

        private static HunterRelationship GetOrCreateRelationship(HunterProfile owner, string subjectId)
        {
            var relationship = FindRelationship(owner, subjectId);
            if (relationship != null) return relationship;
            relationship = new HunterRelationship { hunterId = subjectId };
            var insertionIndex = owner.relationships.FindIndex(item =>
                item == null || string.CompareOrdinal(item.hunterId, subjectId) > 0);
            if (insertionIndex < 0) owner.relationships.Add(relationship);
            else owner.relationships.Insert(insertionIndex, relationship);
            return relationship;
        }

        private void AddMemory(
            HunterProfile owner,
            string subjectId,
            string eventType,
            string summary,
            float emotionalWeight)
        {
            if (owner == null) return;
            owner.memories.Add(new HunterMemory
            {
                day = state.day,
                subjectId = subjectId,
                eventType = eventType,
                summary = summary,
                emotionalWeight = emotionalWeight
            });
            while (owner.memories.Count > MaximumMemoriesPerHunter)
            {
                owner.memories.RemoveAt(0);
            }
        }

        private void AdjustDiplomacy(
            GuildState owner,
            GuildState other,
            float regardDelta,
            float grievanceDelta,
            string cause)
        {
            if (owner == null || other == null || owner.id == other.id) return;
            var relation = owner.diplomacy.Find(item => item != null && item.guildId == other.id);
            if (relation == null)
            {
                relation = new GuildDiplomacyState
                {
                    guildId = other.id,
                    stance = DiplomaticStance.Neutral,
                    sinceDay = state.day
                };
                owner.diplomacy.Add(relation);
            }
            relation.regard = Mathf.Clamp(relation.regard + regardDelta, -1f, 1f);
            relation.grievance = Mathf.Clamp01(relation.grievance + grievanceDelta);
            relation.sinceDay = state.day;
            relation.causeEventId = cause;
            relation.stance = relation.grievance >= 0.75f
                ? DiplomaticStance.Hostile
                : relation.grievance >= 0.35f
                    ? DiplomaticStance.Rival
                    : relation.regard >= 0.45f
                        ? DiplomaticStance.Cooperative
                        : DiplomaticStance.Neutral;
        }

        private void RecalculateTerritory(GuildState guild)
        {
            guild.controlledLocationIds.RemoveAll(id => FindLocation(id) == null);
            guild.controlledLocationIds.Sort(StringComparer.Ordinal);
            for (var index = guild.controlledLocationIds.Count - 1; index > 0; index--)
            {
                if (guild.controlledLocationIds[index] == guild.controlledLocationIds[index - 1])
                {
                    guild.controlledLocationIds.RemoveAt(index);
                }
            }
            guild.territory = guild.controlledLocationIds.Count;
        }

        private void AddWorldEvent(
            WorldEventType eventType,
            string actorHunterId,
            string targetHunterId,
            string guildId,
            string locationId,
            string contractId,
            float magnitude,
            string summary)
        {
            var sequence = NextStructuredEventSequence();
            state.structuredEvents.Add(new StructuredWorldEvent
            {
                id = $"event-{state.day}-{sequence}",
                sequence = sequence,
                day = state.day,
                eventType = eventType,
                actorHunterId = actorHunterId,
                targetHunterId = targetHunterId,
                guildId = guildId,
                locationId = locationId,
                contractId = contractId,
                magnitude = magnitude,
                summary = summary
            });
            state.eventLog.Add($"DAY {state.day}: {summary}");
            while (state.eventLog.Count > MaximumUiEvents) state.eventLog.RemoveAt(0);
            while (state.structuredEvents.Count > MaximumStructuredEvents)
                state.structuredEvents.RemoveAt(0);
        }

        private long NextStructuredEventSequence()
        {
            var greatest = 0L;
            foreach (var worldEvent in state.structuredEvents)
            {
                if (worldEvent != null && worldEvent.sequence > greatest)
                    greatest = worldEvent.sequence;
            }
            return greatest + 1L;
        }

        private string CreateId(string prefix, string first, string second, int ordinal)
        {
            var baseText = $"{prefix}:{state.worldSeed}:{state.day}:{first}:{second}:{ordinal}";
            var hash = EcosystemDeterministicRandom.StableHash(baseText);
            var candidate = $"{prefix}-{hash:x8}";
            var suffix = 1;
            while (IdExists(candidate))
            {
                candidate = $"{prefix}-{hash:x8}-{suffix++}";
            }
            return candidate;
        }

        private bool IdExists(string id)
        {
            return FindParty(id) != null || FindInvitation(id) != null ||
                   state.structuredEvents.Exists(item => item != null && item.id == id) ||
                   state.hunters.Exists(item => item != null && item.injuries != null &&
                       item.injuries.Exists(injury => injury != null && injury.id == id));
        }

        public LocationState FindNextTravelLocation(string fromLocationId, string targetLocationId)
        {
            if (string.IsNullOrEmpty(fromLocationId) || string.IsNullOrEmpty(targetLocationId) ||
                fromLocationId == targetLocationId || FindLocation(fromLocationId) == null ||
                FindLocation(targetLocationId) == null)
            {
                return null;
            }

            var distances = new Dictionary<string, int>(StringComparer.Ordinal);
            var previous = new Dictionary<string, string>(StringComparer.Ordinal);
            var unvisited = new HashSet<string>(StringComparer.Ordinal);
            foreach (var location in state.map.locations)
            {
                if (location == null || string.IsNullOrEmpty(location.id)) continue;
                distances[location.id] = location.id == fromLocationId ? 0 : int.MaxValue;
                unvisited.Add(location.id);
            }

            while (unvisited.Count > 0)
            {
                string current = null;
                var bestDistance = int.MaxValue;
                foreach (var candidate in unvisited)
                {
                    var candidateDistance = distances[candidate];
                    if (candidateDistance < bestDistance ||
                        (candidateDistance == bestDistance &&
                         string.CompareOrdinal(candidate, current) < 0))
                    {
                        current = candidate;
                        bestDistance = candidateDistance;
                    }
                }

                if (current == null || bestDistance == int.MaxValue) break;
                unvisited.Remove(current);
                if (current == targetLocationId) break;

                foreach (var route in state.map.routes)
                {
                    if (route == null) continue;
                    string neighbor = null;
                    if (route.fromLocationId == current) neighbor = route.toLocationId;
                    else if (route.toLocationId == current) neighbor = route.fromLocationId;
                    if (neighbor == null || !unvisited.Contains(neighbor)) continue;

                    var candidateDistance = bestDistance + Mathf.Max(1, route.travelDays);
                    var hasPrevious = previous.TryGetValue(neighbor, out var oldPrevious);
                    if (candidateDistance < distances[neighbor] ||
                        (candidateDistance == distances[neighbor] &&
                         (!hasPrevious || string.CompareOrdinal(current, oldPrevious) < 0)))
                    {
                        distances[neighbor] = candidateDistance;
                        previous[neighbor] = current;
                    }
                }
            }

            if (!previous.ContainsKey(targetLocationId)) return null;
            var step = targetLocationId;
            while (previous.TryGetValue(step, out var predecessor) && predecessor != fromLocationId)
            {
                step = predecessor;
            }
            return previous.TryGetValue(step, out var directPredecessor) && directPredecessor == fromLocationId
                ? FindLocation(step)
                : null;
        }

        private WorldRouteState FindRoute(string fromLocationId, string toLocationId)
        {
            if (state.map == null) return null;
            return state.map.routes.Find(route => route != null &&
                ((route.fromLocationId == fromLocationId && route.toLocationId == toLocationId) ||
                  (route.fromLocationId == toLocationId && route.toLocationId == fromLocationId)));
        }

        private bool CanUseCareerSystem(HunterProfile hunter, out string reason)
        {
            if (hunter?.career == null || !hunter.career.initialized)
            {
                return Fail("The hunter's career has not been initialized.", out reason);
            }
            var party = FindParty(hunter.partyId);
            if (!string.IsNullOrEmpty(hunter.activeContractId) ||
                (party != null && !string.IsNullOrEmpty(party.activeContractId)))
            {
                return Fail("Career and loadout changes are unavailable during an active contract.", out reason);
            }
            var location = FindLocation(hunter.locationId);
            if (location == null || location.locationType != LocationType.Town)
            {
                return Fail("Career and loadout changes require the safety of a town.", out reason);
            }
            reason = "";
            return true;
        }

        private static bool CanInvestAttribute(
            HunterProfile hunter,
            string attributeId,
            int amount,
            out string reason)
        {
            var definition = EcosystemCareerCatalog.FindAttribute(attributeId);
            if (definition == null)
                return Fail("The selected attribute does not exist.", out reason);
            if (amount != 1)
                return Fail("Attribute investment currently spends exactly one Ability Point.", out reason);
            if (hunter.career.UnspentAbilityPoints < amount)
                return Fail("The hunter does not have enough unspent Ability Points.", out reason);
            if (EcosystemCareerRules.FindAttribute(hunter, attributeId) == null)
                return Fail("The hunter is missing that attribute record.", out reason);
            reason = "";
            return true;
        }

        private static bool CanLearnAbility(
            HunterProfile hunter,
            string abilityId,
            out string reason)
        {
            var definition = EcosystemCareerCatalog.FindAbility(abilityId);
            if (definition == null)
                return Fail("The selected ability does not exist.", out reason);
            if (EcosystemCareerRules.IsLearned(hunter.career, abilityId))
                return Fail("The hunter has already learned that ability.", out reason);
            if (hunter.career.InvestedAbilityPoints < definition.requiredInvestedAbilityPoints)
                return Fail($"This ability requires {definition.requiredInvestedAbilityPoints} invested AP.", out reason);
            if (hunter.career.UnspentAbilityPoints < definition.abilityPointCost)
                return Fail($"This ability costs {definition.abilityPointCost} AP.", out reason);
            reason = "";
            return true;
        }

        private static bool CanEquipAbility(
            HunterProfile hunter,
            string abilityId,
            int slotIndex,
            out string reason)
        {
            var definition = EcosystemCareerCatalog.FindAbility(abilityId);
            if (definition == null)
                return Fail("The selected ability does not exist.", out reason);
            if (!EcosystemCareerRules.IsLearned(hunter.career, abilityId))
                return Fail("The hunter has not learned that ability.", out reason);
            var loadout = hunter.career.loadout;
            if (loadout == null)
                return Fail("The hunter's loadout has not been initialized.", out reason);
            if (slotIndex == EcosystemCareerCatalog.UltimateSlotIndex)
            {
                if (definition.kind != HunterAbilityKind.Ultimate)
                    return Fail("Only an ultimate ability can occupy the ultimate slot.", out reason);
                if (loadout.ultimateAbilityId == abilityId)
                    return Fail("That ultimate is already equipped.", out reason);
                reason = "";
                return true;
            }
            if (slotIndex < 0 || slotIndex >= EcosystemCareerCatalog.CooldownSlotCount)
                return Fail("The cooldown slot index is invalid.", out reason);
            if (definition.kind != HunterAbilityKind.Cooldown)
                return Fail("Only a cooldown ability can occupy a cooldown slot.", out reason);
            if (loadout.cooldownAbilityIds == null || loadout.cooldownAbilityIds.Count <= slotIndex)
                return Fail("The hunter's cooldown loadout is incomplete.", out reason);
            if (loadout.cooldownAbilityIds[slotIndex] == abilityId)
                return Fail("That ability is already equipped in the selected slot.", out reason);
            reason = "";
            return true;
        }

        private static bool CanEquipPassive(
            HunterProfile hunter,
            string abilityId,
            int slotIndex,
            out string reason)
        {
            var definition = EcosystemCareerCatalog.FindAbility(abilityId);
            if (definition == null || definition.kind != HunterAbilityKind.Passive)
                return Fail("The selected entry is not a passive ability.", out reason);
            if (!EcosystemCareerRules.IsLearned(hunter.career, abilityId))
                return Fail("The hunter has not learned that passive.", out reason);
            if (slotIndex < 0 || slotIndex >= EcosystemCareerCatalog.PassiveSlotCount)
                return Fail("The passive slot index is invalid.", out reason);
            var slots = hunter.career.loadout?.passiveAbilityIds;
            if (slots == null || slots.Count <= slotIndex)
                return Fail("The hunter's passive loadout is incomplete.", out reason);
            if (slots[slotIndex] == abilityId)
                return Fail("That passive is already equipped in the selected slot.", out reason);
            reason = "";
            return true;
        }

        private static bool CanPlanAbility(
            HunterProfile hunter,
            string abilityId,
            out string reason)
        {
            if (EcosystemCareerCatalog.FindAbility(abilityId) == null)
                return Fail("The planned ability does not exist.", out reason);
            if (EcosystemCareerRules.IsLearned(hunter.career, abilityId))
                return Fail("The hunter already knows that ability.", out reason);
            if (hunter.career.plannedAbilityId == abilityId)
                return Fail("The hunter is already saving Ability Points for that ability.", out reason);
            reason = "";
            return true;
        }

        private static bool IsCareerAction(HunterActionType action)
        {
            return action is HunterActionType.Train or HunterActionType.InvestAttribute or
                HunterActionType.LearnAbility or HunterActionType.EquipAbility or
                HunterActionType.EquipPassive or HunterActionType.SaveAbilityPoints;
        }

        private bool IsTravelling(HunterProfile hunter)
        {
            if (hunter == null) return false;
            if (hunter.travelDaysRemaining > 0) return true;
            var party = FindParty(hunter.partyId);
            return party != null && party.status == PartyStatus.Travelling &&
                   party.travelDaysRemaining > 0;
        }

        private void CompletePartyTravel(PartyState party)
        {
            var destination = FindLocation(party.destinationId);
            if (destination == null)
            {
                party.destinationId = string.Empty;
                party.travelDaysRemaining = 0;
                party.status = PartyStatus.Forming;
                return;
            }

            party.locationId = destination.id;
            party.destinationId = string.Empty;
            party.travelDaysRemaining = 0;
            party.status = PartyStatus.Forming;
            foreach (var memberId in party.memberIds)
            {
                var member = FindHunter(memberId);
                if (member == null || !member.IsActive) continue;
                member.locationId = destination.id;
                member.destinationId = string.Empty;
                member.travelDaysRemaining = 0;
                member.currentActivity = $"Arrived at {destination.displayName}";
            }
            var leader = FindHunter(party.leaderHunterId);
            AddWorldEvent(WorldEventType.HunterAction, party.leaderHunterId, null, leader?.guildId,
                destination.id, party.activeContractId, 0f,
                $"{party.displayName} arrived at {destination.displayName}.");
        }

        private void CompleteSoloTravel(HunterProfile hunter)
        {
            var destination = FindLocation(hunter.destinationId);
            hunter.destinationId = string.Empty;
            hunter.travelDaysRemaining = 0;
            if (destination == null) return;
            hunter.locationId = destination.id;
            hunter.currentActivity = $"Arrived at {destination.displayName}";
            AddWorldEvent(WorldEventType.HunterAction, hunter.id, null, hunter.guildId,
                destination.id, hunter.activeContractId, 0f,
                $"{hunter.displayName} arrived at {destination.displayName}.");
        }

        private LocationState ContractDestination(ContractState contract)
        {
            if (contract == null)
            {
                return null;
            }

            return FindLocation(contract.targetLocationId) ?? FindLocation(contract.locationId);
        }

        private bool HasPendingInvitation(
            InvitationType type,
            string recipientId,
            string guildId,
            string partyId)
        {
            return state.invitations.Exists(item => item != null &&
                item.status == InvitationStatus.Pending &&
                item.invitationType == type &&
                item.recipientHunterId == recipientId &&
                (guildId == null || item.guildId == guildId) &&
                (partyId == null || item.partyId == partyId));
        }

        private List<HunterProfile> LivingPartyMembers(PartyState party)
        {
            var result = new List<HunterProfile>();
            if (party == null) return result;
            foreach (var memberId in party.memberIds)
            {
                var member = FindHunter(memberId);
                if (member != null && member.IsActive) result.Add(member);
            }
            result.Sort((left, right) => string.CompareOrdinal(left.id, right.id));
            return result;
        }

        private static bool IsLivingOther(HunterProfile actor, HunterProfile target)
        {
            return target != null && target.IsActive && actor.id != target.id;
        }

        private static bool IsSettlementSafeZone(LocationState location)
        {
            return location != null && location.locationType is LocationType.Town or
                LocationType.Marketplace or LocationType.Hospital;
        }

        private static bool ShareGuild(HunterProfile first, HunterProfile second)
        {
            return !string.IsNullOrEmpty(first.guildId) && first.guildId == second.guildId;
        }

        private static bool ShareParty(HunterProfile first, HunterProfile second)
        {
            return !string.IsNullOrEmpty(first.partyId) && first.partyId == second.partyId;
        }

        private static int TradePrice(EcosystemGearDefinition gear)
        {
            return Mathf.Max(1, gear.Price > 0 ? gear.Price : gear.Power * 4);
        }

        private static bool Fail(string message, out string reason)
        {
            reason = message;
            return false;
        }

        private static HunterActionType NormalizeAction(HunterActionType action)
        {
            return action switch
            {
                HunterActionType.ChooseGear => HunterActionType.EquipGear,
                HunterActionType.Rest => HunterActionType.Recover,
                HunterActionType.RecruitHunter => HunterActionType.RecruitToGuild,
                HunterActionType.StartContract => HunterActionType.EnterDungeon,
                HunterActionType.ClaimLocation => HunterActionType.ClaimSite,
                _ => action
            };
        }

        private void TrimInvitations()
        {
            if (state.invitations.Count <= MaximumInvitations) return;
            for (var index = 0;
                 index < state.invitations.Count && state.invitations.Count > MaximumInvitations;)
            {
                if (state.invitations[index] == null ||
                    state.invitations[index].status != InvitationStatus.Pending)
                {
                    state.invitations.RemoveAt(index);
                }
                else
                {
                    index++;
                }
            }
        }

        private void EnsureCollections()
        {
            state.hunters ??= new List<HunterProfile>();
            state.guilds ??= new List<GuildState>();
            state.contracts ??= new List<ContractState>();
            state.parties ??= new List<PartyState>();
            state.invitations ??= new List<InvitationState>();
            state.eventLog ??= new List<string>();
            state.structuredEvents ??= new List<StructuredWorldEvent>();
            state.decisionRecords ??= new List<HunterDecisionRecord>();
            state.map ??= new EcosystemMapState();
            state.map.locations ??= new List<LocationState>();
            state.map.routes ??= new List<WorldRouteState>();
            foreach (var hunter in state.hunters)
            {
                if (hunter == null) continue;
                hunter.relationships ??= new List<HunterRelationship>();
                hunter.memories ??= new List<HunterMemory>();
                hunter.inventoryGearIds ??= new List<string>();
                hunter.injuries ??= new List<HunterInjury>();
            }
            foreach (var guild in state.guilds)
            {
                if (guild == null) continue;
                guild.memberIds ??= new List<string>();
                guild.controlledLocationIds ??= new List<string>();
                guild.diplomacy ??= new List<GuildDiplomacyState>();
            }
        }
    }
}
