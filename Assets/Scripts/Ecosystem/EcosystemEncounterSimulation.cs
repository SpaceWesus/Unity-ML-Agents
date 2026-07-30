using System;
using System.Collections.Generic;
using UnityEngine;

namespace Turtle.Ecosystem
{
    /// <summary>
    /// Deterministic, renderer-independent top-down encounter simulation. It operates
    /// on fixed integer ticks and fidelity-neutral Vector2 snapshots; visible and
    /// offscreen encounters therefore share rules, spacing, AI and random provenance.
    /// </summary>
    public sealed class EcosystemEncounterSimulation
    {
        public const float FixedStepSeconds = 0.2f;
        public const int MaximumRecentEvents = 128;
        private const float CorridorHalfWidth = 1.5f;
        private const float InteractionRange = 1.45f;
        private const float WaypointArrivalDistance = 0.32f;

        private readonly EcosystemWorldState world;
        private readonly IReadOnlyList<EcosystemGearDefinition> gearCatalog;
        private readonly List<EncounterInputIntent> intentBuffer = new();

        public EcosystemEncounterSimulation(
            EcosystemWorldState worldState,
            IReadOnlyList<EcosystemGearDefinition> availableGear = null)
        {
            world = worldState ?? throw new ArgumentNullException(nameof(worldState));
            gearCatalog = availableGear ?? Array.Empty<EcosystemGearDefinition>();
            world.gates ??= new List<GateInstanceState>();
            world.encounters ??= new List<DungeonEncounterState>();
        }

        public GateInstanceState EnsureGate(ContractState contract) =>
            EcosystemGateGenerator.EnsureGateForContract(world, contract);

        public DungeonEncounterState BeginEncounter(ContractState contract, PartyState party) =>
            EcosystemGateGenerator.EnsureEncounterForContract(
                world,
                contract,
                party,
                gearCatalog);

        public void AdvanceAllActive(
            int fixedSteps,
            EncounterIntentOverride intentOverride = null)
        {
            if (fixedSteps <= 0) return;
            var active = new List<DungeonEncounterState>();
            foreach (var encounter in world.encounters)
            {
                if (encounter != null && encounter.status == DungeonEncounterStatus.Active)
                {
                    active.Add(encounter);
                }
            }
            active.Sort((left, right) => string.CompareOrdinal(left.id, right.id));
            foreach (var encounter in active)
            {
                AdvanceEncounter(encounter, fixedSteps, intentOverride);
            }
        }

        public void AdvanceEncounter(
            DungeonEncounterState encounter,
            int fixedSteps,
            EncounterIntentOverride intentOverride = null)
        {
            if (encounter == null) throw new ArgumentNullException(nameof(encounter));
            NormalizeCollections(encounter);
            for (var step = 0;
                 step < Mathf.Max(0, fixedSteps) &&
                 encounter.status == DungeonEncounterStatus.Active;
                 step++)
            {
                AdvanceOneFixedStep(encounter, intentOverride);
            }
        }

        /// <summary>
        /// Public for developer inspection and future behavior authoring. The same
        /// EncounterInputIntent returned here is also accepted from player input.
        /// </summary>
        public EncounterInputIntent BuildAutonomousIntent(
            DungeonEncounterState encounter,
            EncounterParticipantState participant)
        {
            var intent = EncounterInputIntent.Idle(participant?.entityId);
            if (encounter == null || participant == null || !participant.CanFight)
            {
                return intent;
            }

            if (participant.participantKind == EncounterParticipantKind.Monster &&
                !IsPodEngaged(encounter, participant.podId))
            {
                return intent;
            }

            var target = FindNearestHostileInArea(encounter, participant);
            if (target != null)
            {
                var offset = target.position - participant.position;
                var distance = offset.magnitude;
                var direction = distance > 0.001f ? offset / distance : participant.facing;
                intent.aim = direction;
                intent.targetEntityId = target.entityId;
                if (distance <= Mathf.Max(0.2f, participant.attackRange))
                {
                    intent.primaryAttack = true;
                }
                else
                {
                    intent.movement = direction;
                }
                return intent;
            }

            if (participant.participantKind == EncounterParticipantKind.Monster)
            {
                return intent;
            }

            if (FindInteractable(encounter, participant, out _, out _))
            {
                intent.interact = true;
                return intent;
            }

            if (TryFindPendingNodeInArea(encounter, participant, out var nodePosition))
            {
                var nodeOffset = nodePosition - participant.position;
                if (nodeOffset.sqrMagnitude > 0.001f)
                {
                    intent.movement = nodeOffset.normalized;
                    intent.aim = intent.movement;
                }
                return intent;
            }

            var objectiveArea = FindNextObjectiveArea(encounter, participant);
            if (objectiveArea == null || objectiveArea.id == participant.areaId)
            {
                return intent;
            }
            PrepareNavigationIntent(encounter, participant, objectiveArea.id, intent);
            var navigationTarget = CurrentNavigationTarget(encounter, participant, intent);
            var navigationOffset = navigationTarget - participant.position;
            if (navigationOffset.sqrMagnitude > 0.001f)
            {
                intent.movement = navigationOffset.normalized;
                intent.aim = intent.movement;
            }
            return intent;
        }

        /// <summary>
        /// Explicit campaign commit boundary. An active encounter snapshot is exact
        /// combat truth; callers commit it when saving a materialized runtime, ending a
        /// run, or exposing campaign vitals. No rendered object is consulted.
        /// </summary>
        public void CommitHunterSnapshotsToWorld(
            DungeonEncounterState encounter,
            bool releaseEncounterLinks)
        {
            if (encounter?.participants == null || world.hunters == null) return;
            foreach (var participant in encounter.participants)
            {
                if (participant == null ||
                    participant.participantKind != EncounterParticipantKind.Hunter ||
                    string.IsNullOrEmpty(participant.sourceHunterId))
                {
                    continue;
                }
                var hunter = world.hunters.Find(item =>
                    item != null && item.id == participant.sourceHunterId);
                if (hunter == null || participant.vitals == null) continue;
                hunter.vitals ??= new HunterVitalsState();
                CopyVitals(participant.vitals, hunter.vitals);
                hunter.isIncapacitated =
                    participant.lifeState == EncounterParticipantLifeState.Incapacitated;
                if (releaseEncounterLinks)
                {
                    hunter.currentEncounterId = string.Empty;
                }
            }
        }

        public void MarkRetreated(DungeonEncounterState encounter)
        {
            if (encounter == null || encounter.status != DungeonEncounterStatus.Active) return;
            encounter.status = DungeonEncounterStatus.Retreated;
            var gate = FindGate(encounter.gateId);
            if (gate != null)
            {
                gate.lifecycle = GateLifecycleState.AwaitingReauction;
            }
            AppendEvent(
                encounter,
                EncounterEventType.EncounterRetreated,
                string.Empty,
                string.Empty,
                encounter.entranceAreaId,
                Vector2.zero,
                0f,
                "The strike team retreated; remaining gate contents were preserved.");
        }

        private void AdvanceOneFixedStep(
            DungeonEncounterState encounter,
            EncounterIntentOverride intentOverride)
        {
            encounter.fixedTick++;
            UpdateAreaDiscoveryAndPodEngagement(encounter);
            UpdatePodAndAreaProgress(encounter);

            // Every decision reads the same pre-mutation snapshot. Movement, attacks and
            // interactions are applied only after all intents have been collected.
            intentBuffer.Clear();
            foreach (var participant in encounter.participants)
            {
                if (participant == null) continue;
                EncounterInputIntent intent;
                if (intentOverride == null ||
                    !intentOverride(encounter, participant, out intent) ||
                    intent == null)
                {
                    intent = BuildAutonomousIntent(encounter, participant);
                }
                var sanitized = SanitizeIntent(participant, intent);
                if (sanitized.primaryAttack)
                {
                    if (!participant.CanFight || encounter.fixedTick < participant.actionReadyTick)
                    {
                        sanitized.primaryAttack = false;
                        sanitized.targetEntityId = string.Empty;
                    }
                    else
                    {
                        // Resolve directional targeting before any movement or damage is
                        // applied. Every attack therefore belongs to the same tick snapshot.
                        sanitized.targetEntityId =
                            ResolveAttackTarget(encounter, participant, sanitized)?.entityId ??
                            string.Empty;
                    }
                }
                intentBuffer.Add(sanitized);
            }

            foreach (var intent in intentBuffer)
            {
                ApplyMovementIntent(encounter, intent);
            }
            foreach (var intent in intentBuffer)
            {
                ApplyInteractionIntent(encounter, intent);
            }
            foreach (var intent in intentBuffer)
            {
                ApplyAttackIntent(encounter, intent);
            }
            ApplyHazards(encounter);
            UpdateAreaDiscoveryAndPodEngagement(encounter);
            UpdatePodAndAreaProgress(encounter);
            UpdateEncounterOutcome(encounter);
        }

        private static EncounterInputIntent SanitizeIntent(
            EncounterParticipantState participant,
            EncounterInputIntent intent)
        {
            var movement = Vector2.ClampMagnitude(intent?.movement ?? Vector2.zero, 1f);
            var aim = intent?.aim ?? Vector2.zero;
            if (aim.sqrMagnitude > 0.001f) aim.Normalize();
            else aim = participant.facing.sqrMagnitude > 0.001f
                ? participant.facing.normalized
                : Vector2.down;
            return new EncounterInputIntent
            {
                entityId = participant.entityId,
                movement = movement,
                aim = aim,
                primaryAttack = intent?.primaryAttack ?? false,
                interact = intent?.interact ?? false,
                targetEntityId = intent?.targetEntityId ?? string.Empty,
                hasNavigationUpdate = intent?.hasNavigationUpdate ?? false,
                navigationConnectionId = intent?.navigationConnectionId ?? string.Empty,
                navigationDestinationAreaId = intent?.navigationDestinationAreaId ?? string.Empty,
                navigationWaypointIndex = intent?.navigationWaypointIndex ?? -1
            };
        }

        private void ApplyMovementIntent(
            DungeonEncounterState encounter,
            EncounterInputIntent intent)
        {
            var participant = FindParticipant(encounter, intent.entityId);
            if (participant == null || !participant.CanFight) return;
            if (intent.hasNavigationUpdate)
            {
                participant.navigationConnectionId = intent.navigationConnectionId;
                participant.navigationDestinationAreaId = intent.navigationDestinationAreaId;
                participant.navigationWaypointIndex = intent.navigationWaypointIndex;
            }
            if (intent.aim.sqrMagnitude > 0.001f)
            {
                participant.facing = intent.aim.normalized;
            }
            if (intent.movement.sqrMagnitude <= 0.001f) return;

            var movement = intent.movement.normalized *
                           Mathf.Max(0f, participant.moveSpeed) * FixedStepSeconds;
            var proposed = participant.position + movement;
            if (!IsWalkable(encounter, proposed))
            {
                var horizontal = participant.position + new Vector2(movement.x, 0f);
                var vertical = participant.position + new Vector2(0f, movement.y);
                proposed = IsWalkable(encounter, horizontal)
                    ? horizontal
                    : IsWalkable(encounter, vertical)
                        ? vertical
                        : participant.position;
            }
            participant.position = proposed;
            var containingArea = FindContainingArea(encounter, participant.position);
            if (containingArea != null && containingArea.id != participant.areaId)
            {
                participant.areaId = containingArea.id;
                participant.navigationConnectionId = string.Empty;
                participant.navigationDestinationAreaId = string.Empty;
                participant.navigationWaypointIndex = -1;
                AppendEvent(
                    encounter,
                    EncounterEventType.AreaEntered,
                    participant.entityId,
                    string.Empty,
                    containingArea.id,
                    participant.position,
                    0f,
                    $"{participant.displayName} entered {containingArea.displayName}.");
            }
            AdvanceNavigationWaypoint(encounter, participant);
        }

        private void ApplyInteractionIntent(
            DungeonEncounterState encounter,
            EncounterInputIntent intent)
        {
            if (!intent.interact) return;
            var participant = FindParticipant(encounter, intent.entityId);
            if (participant == null || !participant.CanFight ||
                participant.participantKind != EncounterParticipantKind.Hunter)
            {
                return;
            }
            if (!FindInteractable(encounter, participant, out var loot, out var resource))
            {
                return;
            }
            if (loot != null)
            {
                loot.status = DungeonLootStatus.Claimed;
                loot.claimedByEntityId = participant.entityId;
                AppendEvent(
                    encounter,
                    EncounterEventType.LootClaimed,
                    participant.entityId,
                    loot.id,
                    participant.areaId,
                    loot.position,
                    loot.gold,
                    $"{participant.displayName} claimed {loot.lootTableId}.");
            }
            else if (resource != null)
            {
                var amount = resource.remainingAmount;
                resource.remainingAmount = 0;
                resource.extractedByPartyId = encounter.partyId;
                AppendEvent(
                    encounter,
                    EncounterEventType.ResourceExtracted,
                    participant.entityId,
                    resource.id,
                    participant.areaId,
                    resource.position,
                    amount,
                    $"{participant.displayName} extracted {amount} {resource.resourceId}.");
            }
        }

        private void ApplyAttackIntent(
            DungeonEncounterState encounter,
            EncounterInputIntent intent)
        {
            if (!intent.primaryAttack) return;
            var attacker = FindParticipant(encounter, intent.entityId);
            if (attacker == null)
            {
                return;
            }

            attacker.actionReadyTick = encounter.fixedTick +
                                         Mathf.Max(1, attacker.attackCooldownTicks);
            var target = FindParticipant(encounter, intent.targetEntityId);
            if (!IsValidAttackTarget(attacker, target)) target = null;
            AppendEvent(
                encounter,
                EncounterEventType.Attack,
                attacker.entityId,
                target?.entityId ?? string.Empty,
                attacker.areaId,
                attacker.position,
                0f,
                target == null
                    ? $"{attacker.displayName} attacked and missed."
                    : $"{attacker.displayName} attacked {target.displayName}.");
            if (target == null) return;

            var gate = FindGate(encounter.gateId);
            var roll = NextEncounter01(
                encounter,
                gate?.seed ?? 1,
                $"attack|{attacker.entityId}|{target.entityId}");
            if (roll > 0.9f) return;
            var variance = Mathf.Lerp(0.86f, 1.14f, NextEncounter01(
                encounter,
                gate?.seed ?? 1,
                $"damage|{attacker.entityId}|{target.entityId}"));
            var damage = Mathf.Max(1, Mathf.RoundToInt(attacker.attackDamage * variance));
            ApplyDamage(encounter, attacker, target, damage, "attack");
        }

        private void ApplyHazards(DungeonEncounterState encounter)
        {
            if (encounter.fixedTick % 5 != 0 || encounter.hazards == null) return;
            foreach (var hazard in encounter.hazards)
            {
                if (hazard == null || !hazard.active) continue;
                foreach (var participant in encounter.participants)
                {
                    if (participant == null || !participant.CanFight) continue;
                    if (participant.areaId != hazard.areaId ||
                        Vector2.Distance(participant.position, hazard.position) > hazard.radius)
                    {
                        continue;
                    }
                    ApplyDamage(
                        encounter,
                        null,
                        participant,
                        Mathf.Max(1, Mathf.CeilToInt(hazard.damagePerTick)),
                        hazard.hazardType.ToString());
                }
            }
        }

        private void ApplyDamage(
            DungeonEncounterState encounter,
            EncounterParticipantState attacker,
            EncounterParticipantState target,
            int amount,
            string cause)
        {
            if (target == null || !target.CanFight || target.vitals == null || amount <= 0) return;
            var remaining = amount;
            var absorbed = Mathf.Min(target.vitals.currentShield, remaining);
            target.vitals.currentShield -= absorbed;
            remaining -= absorbed;
            if (remaining > 0)
            {
                target.vitals.currentHealth = Mathf.Max(
                    0,
                    target.vitals.currentHealth - remaining);
            }
            AppendEvent(
                encounter,
                EncounterEventType.Damage,
                attacker?.entityId ?? string.Empty,
                target.entityId,
                target.areaId,
                target.position,
                amount,
                $"{target.displayName} took {amount} damage from {cause}.");
            if (target.vitals.currentHealth > 0) return;

            target.targetEntityId = string.Empty;
            if (target.participantKind == EncounterParticipantKind.Hunter)
            {
                target.lifeState = EncounterParticipantLifeState.Incapacitated;
                AppendEvent(
                    encounter,
                    EncounterEventType.Incapacitated,
                    attacker?.entityId ?? string.Empty,
                    target.entityId,
                    target.areaId,
                    target.position,
                    0f,
                    $"{target.displayName} was incapacitated.");
            }
            else
            {
                target.lifeState = EncounterParticipantLifeState.Defeated;
                AppendEvent(
                    encounter,
                    EncounterEventType.Defeated,
                    attacker?.entityId ?? string.Empty,
                    target.entityId,
                    target.areaId,
                    target.position,
                    0f,
                    $"{target.displayName} was defeated.");
            }
        }

        private void UpdateAreaDiscoveryAndPodEngagement(DungeonEncounterState encounter)
        {
            foreach (var participant in encounter.participants)
            {
                if (participant == null || !participant.CanFight ||
                    participant.participantKind != EncounterParticipantKind.Hunter)
                {
                    continue;
                }
                var area = FindArea(encounter, participant.areaId);
                if (area != null) area.discovered = true;
            }

            foreach (var pod in encounter.mobPods)
            {
                if (pod == null || pod.status != DungeonMobPodStatus.Dormant) continue;
                var hunterEntered = encounter.participants.Exists(participant =>
                    participant != null && participant.CanFight &&
                    participant.participantKind == EncounterParticipantKind.Hunter &&
                    participant.areaId == pod.areaId);
                if (!hunterEntered) continue;
                pod.status = DungeonMobPodStatus.Engaged;
                AppendEvent(
                    encounter,
                    EncounterEventType.PodEngaged,
                    string.Empty,
                    pod.id,
                    pod.areaId,
                    FindArea(encounter, pod.areaId)?.center ?? Vector2.zero,
                    pod.monsterIds?.Count ?? 0,
                    $"Mob pod {pod.id} engaged the strike team.");
            }

            foreach (var loot in encounter.lootNodes)
            {
                if (loot == null || loot.status != DungeonLootStatus.Hidden) continue;
                var area = FindArea(encounter, loot.areaId);
                var pod = encounter.mobPods.Find(item => item != null && item.areaId == loot.areaId);
                if (area?.discovered == true &&
                    (pod == null || pod.status == DungeonMobPodStatus.Cleared))
                {
                    loot.status = DungeonLootStatus.Available;
                }
            }
        }

        private void UpdatePodAndAreaProgress(DungeonEncounterState encounter)
        {
            foreach (var pod in encounter.mobPods)
            {
                if (pod == null || pod.status == DungeonMobPodStatus.Cleared) continue;
                var livingMonster = false;
                foreach (var monsterId in pod.monsterIds)
                {
                    var monster = FindParticipant(encounter, monsterId);
                    if (monster?.CanFight == true)
                    {
                        livingMonster = true;
                        break;
                    }
                }
                if (livingMonster) continue;
                pod.status = DungeonMobPodStatus.Cleared;
                var area = FindArea(encounter, pod.areaId);
                if (area != null) area.cleared = true;
                AppendEvent(
                    encounter,
                    EncounterEventType.PodCleared,
                    string.Empty,
                    pod.id,
                    pod.areaId,
                    area?.center ?? Vector2.zero,
                    0f,
                    $"Mob pod {pod.id} was cleared.");
            }
        }

        private void UpdateEncounterOutcome(DungeonEncounterState encounter)
        {
            var activeHunter = encounter.participants.Exists(participant =>
                participant != null && participant.CanFight &&
                participant.participantKind == EncounterParticipantKind.Hunter);
            if (!activeHunter)
            {
                encounter.status = DungeonEncounterStatus.Failed;
                var gate = FindGate(encounter.gateId);
                if (gate != null) gate.lifecycle = GateLifecycleState.AwaitingReauction;
                AppendEvent(
                    encounter,
                    EncounterEventType.EncounterFailed,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    Vector2.zero,
                    0f,
                    "No active hunter remained in the gate.");
                return;
            }

            var activeMonster = encounter.participants.Exists(participant =>
                participant != null && participant.CanFight &&
                participant.participantKind == EncounterParticipantKind.Monster);
            if (activeMonster) return;
            var availableLoot = encounter.lootNodes.Exists(loot =>
                loot != null && loot.status != DungeonLootStatus.Claimed);
            var remainingResources = encounter.resourceNodes.Exists(resource =>
                resource != null && resource.remainingAmount > 0);
            if (availableLoot || remainingResources) return;

            encounter.status = DungeonEncounterStatus.Succeeded;
            var completedGate = FindGate(encounter.gateId);
            if (completedGate != null) completedGate.lifecycle = GateLifecycleState.Closed;
            AppendEvent(
                encounter,
                EncounterEventType.EncounterSucceeded,
                string.Empty,
                string.Empty,
                encounter.bossAreaId,
                FindArea(encounter, encounter.bossAreaId)?.center ?? Vector2.zero,
                1f,
                "The gate core was cleared and all recovered resources were secured.");
        }

        private EncounterParticipantState ResolveAttackTarget(
            DungeonEncounterState encounter,
            EncounterParticipantState attacker,
            EncounterInputIntent intent)
        {
            var explicitTarget = FindParticipant(encounter, intent.targetEntityId);
            if (IsValidAttackTarget(attacker, explicitTarget) &&
                explicitTarget.areaId == attacker.areaId &&
                Vector2.Distance(attacker.position, explicitTarget.position) <= attacker.attackRange)
            {
                return explicitTarget;
            }

            EncounterParticipantState best = null;
            var bestDistance = float.MaxValue;
            foreach (var candidate in encounter.participants)
            {
                if (!IsValidAttackTarget(attacker, candidate) ||
                    candidate.areaId != attacker.areaId)
                {
                    continue;
                }
                var offset = candidate.position - attacker.position;
                var distance = offset.magnitude;
                if (distance > attacker.attackRange || distance <= 0.001f) continue;
                var dot = Vector2.Dot(intent.aim, offset / distance);
                if (dot < 0.35f) continue;
                if (distance < bestDistance ||
                    (Mathf.Approximately(distance, bestDistance) &&
                     string.CompareOrdinal(candidate.entityId, best?.entityId) < 0))
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }
            return best;
        }

        private static bool IsValidAttackTarget(
            EncounterParticipantState attacker,
            EncounterParticipantState target)
        {
            return attacker != null && target != null && target != attacker && target.CanFight &&
                   attacker.participantKind != target.participantKind &&
                   attacker.factionId != target.factionId;
        }

        private static EncounterParticipantState FindNearestHostileInArea(
            DungeonEncounterState encounter,
            EncounterParticipantState participant)
        {
            EncounterParticipantState best = null;
            var bestDistance = float.MaxValue;
            foreach (var candidate in encounter.participants)
            {
                if (!IsValidAttackTarget(participant, candidate) ||
                    candidate.areaId != participant.areaId)
                {
                    continue;
                }
                if (candidate.participantKind == EncounterParticipantKind.Monster &&
                    !IsPodEngaged(encounter, candidate.podId))
                {
                    continue;
                }
                var distance = (candidate.position - participant.position).sqrMagnitude;
                if (distance < bestDistance ||
                    (Mathf.Approximately(distance, bestDistance) &&
                     string.CompareOrdinal(candidate.entityId, best?.entityId) < 0))
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }
            return best;
        }

        private static bool FindInteractable(
            DungeonEncounterState encounter,
            EncounterParticipantState participant,
            out DungeonLootNodeState loot,
            out DungeonResourceNodeState resource)
        {
            loot = null;
            resource = null;
            var bestDistance = InteractionRange * InteractionRange;
            foreach (var candidate in encounter.lootNodes)
            {
                if (candidate == null || candidate.status != DungeonLootStatus.Available ||
                    candidate.areaId != participant.areaId)
                {
                    continue;
                }
                var distance = (candidate.position - participant.position).sqrMagnitude;
                if (distance <= bestDistance)
                {
                    loot = candidate;
                    bestDistance = distance;
                }
            }
            foreach (var candidate in encounter.resourceNodes)
            {
                if (candidate == null || candidate.remainingAmount <= 0 ||
                    candidate.areaId != participant.areaId)
                {
                    continue;
                }
                var distance = (candidate.position - participant.position).sqrMagnitude;
                if (distance <= bestDistance)
                {
                    loot = null;
                    resource = candidate;
                    bestDistance = distance;
                }
            }
            return loot != null || resource != null;
        }

        private static bool TryFindPendingNodeInArea(
            DungeonEncounterState encounter,
            EncounterParticipantState participant,
            out Vector2 position)
        {
            position = participant.position;
            var found = false;
            var bestDistance = float.MaxValue;
            foreach (var candidate in encounter.lootNodes)
            {
                if (candidate == null || candidate.status != DungeonLootStatus.Available ||
                    candidate.areaId != participant.areaId)
                {
                    continue;
                }
                var distance = (candidate.position - participant.position).sqrMagnitude;
                if (distance >= bestDistance) continue;
                position = candidate.position;
                bestDistance = distance;
                found = true;
            }
            foreach (var candidate in encounter.resourceNodes)
            {
                if (candidate == null || candidate.remainingAmount <= 0 ||
                    candidate.areaId != participant.areaId)
                {
                    continue;
                }
                var distance = (candidate.position - participant.position).sqrMagnitude;
                if (distance >= bestDistance) continue;
                position = candidate.position;
                bestDistance = distance;
                found = true;
            }
            return found;
        }

        private static DungeonAreaState FindNextObjectiveArea(
            DungeonEncounterState encounter,
            EncounterParticipantState participant)
        {
            DungeonAreaState selected = null;
            var selectedPriority = int.MaxValue;
            var selectedDistance = int.MaxValue;
            foreach (var area in encounter.areas)
            {
                if (area == null || area.areaType == DungeonAreaType.Entrance) continue;
                var activeMonster = encounter.participants.Exists(candidate =>
                    candidate != null && candidate.CanFight &&
                    candidate.participantKind == EncounterParticipantKind.Monster &&
                    candidate.areaId == area.id);
                var bossMonster = activeMonster && area.areaType == DungeonAreaType.Boss;
                var loot = encounter.lootNodes.Exists(node =>
                    node != null && node.areaId == area.id &&
                    node.status != DungeonLootStatus.Claimed);
                var resource = encounter.resourceNodes.Exists(node =>
                    node != null && node.areaId == area.id && node.remainingAmount > 0);
                var priority = activeMonster && !bossMonster ? 0
                    : !area.discovered && area.areaType != DungeonAreaType.Boss ? 1
                    : loot || resource ? 2
                    : bossMonster ? 3
                    : int.MaxValue;
                if (priority == int.MaxValue) continue;
                var distance = GraphDistance(encounter, participant.areaId, area.id);
                if (distance < 0) continue;
                if (priority < selectedPriority ||
                    (priority == selectedPriority && distance < selectedDistance) ||
                    (priority == selectedPriority && distance == selectedDistance &&
                     string.CompareOrdinal(area.id, selected?.id) < 0))
                {
                    selected = area;
                    selectedPriority = priority;
                    selectedDistance = distance;
                }
            }
            return selected;
        }

        private static void PrepareNavigationIntent(
            DungeonEncounterState encounter,
            EncounterParticipantState participant,
            string objectiveAreaId,
            EncounterInputIntent intent)
        {
            var nextAreaId = FirstStepToward(encounter, participant.areaId, objectiveAreaId);
            if (participant.navigationDestinationAreaId == nextAreaId &&
                FindConnection(encounter, participant.navigationConnectionId) != null)
            {
                intent.hasNavigationUpdate = false;
                return;
            }
            var connection = FindConnectionBetween(encounter, participant.areaId, nextAreaId);
            intent.hasNavigationUpdate = true;
            if (connection == null)
            {
                intent.navigationConnectionId = string.Empty;
                intent.navigationDestinationAreaId = string.Empty;
                intent.navigationWaypointIndex = -1;
                return;
            }
            intent.navigationConnectionId = connection.id;
            intent.navigationDestinationAreaId = nextAreaId;
            intent.navigationWaypointIndex = connection.fromAreaId == participant.areaId
                ? Mathf.Min(1, connection.waypoints.Count - 1)
                : Mathf.Max(0, connection.waypoints.Count - 2);
        }

        private static Vector2 CurrentNavigationTarget(
            DungeonEncounterState encounter,
            EncounterParticipantState participant,
            EncounterInputIntent intent)
        {
            var connectionId = intent.hasNavigationUpdate
                ? intent.navigationConnectionId
                : participant.navigationConnectionId;
            var destinationAreaId = intent.hasNavigationUpdate
                ? intent.navigationDestinationAreaId
                : participant.navigationDestinationAreaId;
            var waypointIndex = intent.hasNavigationUpdate
                ? intent.navigationWaypointIndex
                : participant.navigationWaypointIndex;
            var connection = FindConnection(encounter, connectionId);
            if (connection?.waypoints == null || connection.waypoints.Count == 0)
            {
                return FindArea(encounter, destinationAreaId)?.center ??
                       participant.position;
            }
            var index = Mathf.Clamp(
                waypointIndex,
                0,
                connection.waypoints.Count - 1);
            return connection.waypoints[index];
        }

        private static void AdvanceNavigationWaypoint(
            DungeonEncounterState encounter,
            EncounterParticipantState participant)
        {
            var connection = FindConnection(encounter, participant.navigationConnectionId);
            if (connection?.waypoints == null || connection.waypoints.Count == 0) return;
            var index = Mathf.Clamp(
                participant.navigationWaypointIndex,
                0,
                connection.waypoints.Count - 1);
            if (Vector2.Distance(participant.position, connection.waypoints[index]) >
                WaypointArrivalDistance)
            {
                return;
            }
            var forward = connection.toAreaId == participant.navigationDestinationAreaId;
            var finalIndex = forward ? connection.waypoints.Count - 1 : 0;
            if (index == finalIndex)
            {
                participant.areaId = participant.navigationDestinationAreaId;
                participant.navigationConnectionId = string.Empty;
                participant.navigationDestinationAreaId = string.Empty;
                participant.navigationWaypointIndex = -1;
                return;
            }
            participant.navigationWaypointIndex = index + (forward ? 1 : -1);
        }

        private static int GraphDistance(
            DungeonEncounterState encounter,
            string fromAreaId,
            string toAreaId)
        {
            if (fromAreaId == toAreaId) return 0;
            var queue = new Queue<string>();
            var distances = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [fromAreaId] = 0
            };
            queue.Enqueue(fromAreaId);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var connection in encounter.connections)
                {
                    if (connection == null) continue;
                    if (connection.locked) continue;
                    var next = connection.OtherArea(current);
                    if (string.IsNullOrEmpty(next) || distances.ContainsKey(next)) continue;
                    var distance = distances[current] + 1;
                    if (next == toAreaId) return distance;
                    distances[next] = distance;
                    queue.Enqueue(next);
                }
            }
            return -1;
        }

        private static string FirstStepToward(
            DungeonEncounterState encounter,
            string fromAreaId,
            string toAreaId)
        {
            if (fromAreaId == toAreaId) return toAreaId;
            var queue = new Queue<string>();
            var previous = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [fromAreaId] = string.Empty
            };
            queue.Enqueue(fromAreaId);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var connection in encounter.connections)
                {
                    if (connection == null) continue;
                    if (connection.locked) continue;
                    var next = connection.OtherArea(current);
                    if (string.IsNullOrEmpty(next) || previous.ContainsKey(next)) continue;
                    previous[next] = current;
                    if (next == toAreaId)
                    {
                        var cursor = next;
                        while (previous.TryGetValue(cursor, out var parent) &&
                               !string.IsNullOrEmpty(parent) && parent != fromAreaId)
                        {
                            cursor = parent;
                        }
                        return cursor;
                    }
                    queue.Enqueue(next);
                }
            }
            return string.Empty;
        }

        private static bool IsWalkable(DungeonEncounterState encounter, Vector2 position)
        {
            if (FindContainingArea(encounter, position) != null) return true;
            foreach (var connection in encounter.connections)
            {
                if (connection?.waypoints == null || connection.locked) continue;
                for (var index = 1; index < connection.waypoints.Count; index++)
                {
                    if (DistanceToSegment(
                            position,
                            connection.waypoints[index - 1],
                            connection.waypoints[index]) <= CorridorHalfWidth)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            var segment = end - start;
            var lengthSquared = segment.sqrMagnitude;
            if (lengthSquared <= 0.0001f) return Vector2.Distance(point, start);
            var t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSquared);
            return Vector2.Distance(point, start + segment * t);
        }

        private static DungeonAreaState FindContainingArea(
            DungeonEncounterState encounter,
            Vector2 position)
        {
            foreach (var area in encounter.areas)
            {
                if (area?.Contains(position) == true) return area;
            }
            return null;
        }

        private static bool IsPodEngaged(DungeonEncounterState encounter, string podId)
        {
            if (string.IsNullOrEmpty(podId)) return true;
            var pod = encounter.mobPods.Find(item => item != null && item.id == podId);
            return pod != null && pod.status == DungeonMobPodStatus.Engaged;
        }

        private static DungeonAreaState FindArea(DungeonEncounterState encounter, string areaId) =>
            string.IsNullOrEmpty(areaId)
                ? null
                : encounter.areas.Find(item => item != null && item.id == areaId);

        private static DungeonConnectionState FindConnection(
            DungeonEncounterState encounter,
            string connectionId) =>
            string.IsNullOrEmpty(connectionId)
                ? null
                : encounter.connections.Find(item => item != null && item.id == connectionId);

        private static DungeonConnectionState FindConnectionBetween(
            DungeonEncounterState encounter,
            string firstAreaId,
            string secondAreaId) => encounter.connections.Find(connection =>
            connection != null && !connection.locked &&
            ((connection.fromAreaId == firstAreaId && connection.toAreaId == secondAreaId) ||
             (connection.fromAreaId == secondAreaId && connection.toAreaId == firstAreaId)));

        private static EncounterParticipantState FindParticipant(
            DungeonEncounterState encounter,
            string entityId) =>
            string.IsNullOrEmpty(entityId)
                ? null
                : encounter.participants.Find(item => item != null && item.entityId == entityId);

        private GateInstanceState FindGate(string gateId) =>
            string.IsNullOrEmpty(gateId)
                ? null
                : world.gates.Find(item => item != null && item.id == gateId);

        private static float NextEncounter01(
            DungeonEncounterState encounter,
            int gateSeed,
            string salt)
        {
            encounter.randomSequence++;
            var value = EcosystemDeterministicRandom.StableHash(
                $"{gateSeed}|{encounter.id}|{encounter.randomSequence}|{salt}");
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return (value & 0x00FFFFFFu) / 16777216f;
        }

        private static void AppendEvent(
            DungeonEncounterState encounter,
            EncounterEventType eventType,
            string actorEntityId,
            string targetEntityId,
            string areaId,
            Vector2 position,
            float magnitude,
            string summary)
        {
            encounter.eventSequence++;
            encounter.recentEvents.Add(new EncounterEventState
            {
                id = $"{encounter.id}:event:{encounter.eventSequence:D7}",
                sequence = encounter.eventSequence,
                tick = encounter.fixedTick,
                eventType = eventType,
                actorEntityId = actorEntityId ?? string.Empty,
                targetEntityId = targetEntityId ?? string.Empty,
                areaId = areaId ?? string.Empty,
                position = position,
                magnitude = magnitude,
                summary = summary ?? string.Empty
            });
            while (encounter.recentEvents.Count > MaximumRecentEvents)
            {
                encounter.recentEvents.RemoveAt(0);
            }
        }

        private static void CopyVitals(HunterVitalsState source, HunterVitalsState destination)
        {
            destination.initialized = true;
            destination.maximumHealth = Mathf.Max(1, source.maximumHealth);
            destination.currentHealth = Mathf.Clamp(source.currentHealth, 0, destination.maximumHealth);
            destination.maximumMana = Mathf.Max(1, source.maximumMana);
            destination.currentMana = Mathf.Clamp(source.currentMana, 0, destination.maximumMana);
            destination.maximumShield = Mathf.Max(1, source.maximumShield);
            destination.currentShield = Mathf.Clamp(source.currentShield, 0, destination.maximumShield);
        }

        private static void NormalizeCollections(DungeonEncounterState encounter)
        {
            encounter.areas ??= new List<DungeonAreaState>();
            encounter.connections ??= new List<DungeonConnectionState>();
            encounter.mobPods ??= new List<DungeonMobPodState>();
            encounter.participants ??= new List<EncounterParticipantState>();
            encounter.lootNodes ??= new List<DungeonLootNodeState>();
            encounter.resourceNodes ??= new List<DungeonResourceNodeState>();
            encounter.hazards ??= new List<DungeonHazardState>();
            encounter.recentEvents ??= new List<EncounterEventState>();
            encounter.areas.Sort((left, right) => string.CompareOrdinal(left?.id, right?.id));
            encounter.connections.Sort((left, right) =>
                string.CompareOrdinal(left?.id, right?.id));
            encounter.mobPods.Sort((left, right) => string.CompareOrdinal(left?.id, right?.id));
            encounter.participants.Sort((left, right) =>
                string.CompareOrdinal(left?.entityId, right?.entityId));
            encounter.lootNodes.Sort((left, right) => string.CompareOrdinal(left?.id, right?.id));
            encounter.resourceNodes.Sort((left, right) =>
                string.CompareOrdinal(left?.id, right?.id));
            encounter.hazards.Sort((left, right) => string.CompareOrdinal(left?.id, right?.id));
        }
    }
}
