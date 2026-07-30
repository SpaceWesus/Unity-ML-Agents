using System;
using System.Collections.Generic;
using UnityEngine;

namespace Turtle.Ecosystem
{
    /// <summary>
    /// Generates fidelity-neutral dungeon content from a gate-local seed. Generation
    /// never consumes the world's simulation cursor and generated results are persisted,
    /// so opening a view cannot change contents or combat odds.
    /// </summary>
    public static class EcosystemGateGenerator
    {
        public const int CurrentGeneratorVersion = 1;
        private const float GridSpacing = 18f;

        private static readonly Vector2Int[] BranchOffsets =
        {
            new(0, 1),
            new(0, -1),
            new(1, 1),
            new(1, -1),
            new(-1, 1),
            new(-1, -1),
            new(2, 0),
            new(-2, 0)
        };

        public static GateInstanceState EnsureGateForContract(
            EcosystemWorldState world,
            ContractState contract)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (contract == null) throw new ArgumentNullException(nameof(contract));

            world.gates ??= new List<GateInstanceState>();
            if (!string.IsNullOrEmpty(contract.gateId))
            {
                var existing = world.gates.Find(gate => gate != null && gate.id == contract.gateId);
                if (existing != null)
                {
                    existing.currentContractId = contract.id;
                    NormalizeCollections(existing);
                    return existing;
                }
            }

            var gate = CreateManifest(world, contract);
            world.gates.Add(gate);
            world.gates.Sort((left, right) =>
                string.CompareOrdinal(left?.id, right?.id));
            contract.gateId = gate.id;
            return gate;
        }

        public static GateInstanceState CreateManifest(
            EcosystemWorldState world,
            ContractState contract)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (contract == null) throw new ArgumentNullException(nameof(contract));
            if (string.IsNullOrWhiteSpace(contract.id))
            {
                throw new ArgumentException("A contract needs a stable ID before gate generation.",
                    nameof(contract));
            }

            var gateId = string.IsNullOrEmpty(contract.gateId)
                ? $"gate-{contract.id}"
                : contract.gateId;
            var seedHash = EcosystemDeterministicRandom.StableHash(
                $"{world.worldSeed}|gate-manifest|{gateId}");
            var seed = (int)(seedHash & 0x7FFFFFFFu);
            if (seed == 0) seed = 1;
            var random = new GateLocalRandom(seedHash);
            var trueDifficulty = Mathf.Max(1, contract.difficulty);
            var underAppraised = trueDifficulty > 1 && random.Next01() < 0.06f;
            var biome = ResolveBiome(contract, random);
            var layout = (DungeonLayoutStyle)random.Range(
                0,
                Enum.GetValues(typeof(DungeonLayoutStyle)).Length);
            var gate = new GateInstanceState
            {
                id = gateId,
                displayName = string.IsNullOrWhiteSpace(contract.displayName)
                    ? $"Gate {gateId}"
                    : contract.displayName,
                entranceLocationId = string.IsNullOrEmpty(contract.targetLocationId)
                    ? contract.locationId
                    : contract.targetLocationId,
                currentContractId = contract.id,
                seed = seed,
                generatorVersion = CurrentGeneratorVersion,
                createdDay = Mathf.Max(1, contract.offeredDay),
                instabilityDeadlineDay = Mathf.Max(
                    Mathf.Max(1, contract.offeredDay) + 2,
                    contract.expiresDay + 2),
                trueDifficulty = trueDifficulty,
                appraisedDifficulty = underAppraised
                    ? Mathf.Max(1, trueDifficulty - random.Range(1, 3))
                    : trueDifficulty,
                biome = biome,
                layoutStyle = layout,
                visualStyleId = VisualStyleId(biome),
                lifecycle = LifecycleForContractStatus(contract.status)
            };

            AddModifiers(gate, underAppraised, random);
            GenerateTopology(gate, random);
            GeneratePodsAndMonsters(gate, contract, random);
            GenerateLootAndResources(gate, random);
            GenerateHazards(gate, random);
            SortManifest(gate);
            contract.gateId = gate.id;
            return gate;
        }

        /// <summary>
        /// Creates the exact mutable snapshot used by abstract simulation and rendered
        /// materializers. Calling this after a snapshot exists returns that snapshot;
        /// it never rerolls a gate because somebody started observing it.
        /// </summary>
        public static DungeonEncounterState EnsureEncounterForContract(
            EcosystemWorldState world,
            ContractState contract,
            PartyState party,
            IReadOnlyList<EcosystemGearDefinition> gearCatalog = null)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (contract == null) throw new ArgumentNullException(nameof(contract));
            if (party == null) throw new ArgumentNullException(nameof(party));
            world.encounters ??= new List<DungeonEncounterState>();

            if (!string.IsNullOrEmpty(contract.activeEncounterId))
            {
                var existing = world.encounters.Find(encounter =>
                    encounter != null && encounter.id == contract.activeEncounterId);
                if (existing != null)
                {
                    NormalizeCollections(existing);
                    return existing;
                }
            }

            var gate = EnsureGateForContract(world, contract);
            if (!string.IsNullOrEmpty(gate.activeEncounterId))
            {
                var existing = world.encounters.Find(encounter =>
                    encounter != null && encounter.id == gate.activeEncounterId);
                if (existing != null)
                {
                    contract.activeEncounterId = existing.id;
                    NormalizeCollections(existing);
                    return existing;
                }
            }

            var entrance = gate.areas.Find(area => area != null &&
                area.areaType == DungeonAreaType.Entrance);
            if (entrance == null)
            {
                throw new InvalidOperationException($"Gate '{gate.id}' has no entrance area.");
            }

            gate.runSequence = Mathf.Max(0, gate.runSequence) + 1;
            var encounter = new DungeonEncounterState
            {
                id = $"{gate.id}:encounter:{gate.runSequence:D3}",
                gateId = gate.id,
                contractId = contract.id,
                partyId = party.id,
                status = DungeonEncounterStatus.Active,
                createdDay = Mathf.Max(1, world.day),
                entranceAreaId = entrance.id,
                bossAreaId = gate.areas.Find(area => area != null &&
                    area.areaType == DungeonAreaType.Boss)?.id ?? string.Empty,
                areas = CloneAreas(gate.areas),
                connections = CloneConnections(gate.connections),
                mobPods = ClonePods(gate.mobPods),
                lootNodes = CloneLoot(gate.lootNodes),
                resourceNodes = CloneResources(gate.resourceNodes),
                hazards = CloneHazards(gate.hazards)
            };

            AddHunterParticipants(world, party, gearCatalog, entrance, encounter);
            AddMonsterParticipants(gate, encounter);
            encounter.participants.Sort((left, right) =>
                string.CompareOrdinal(left?.entityId, right?.entityId));
            encounter.eventSequence = 1;
            encounter.recentEvents.Add(new EncounterEventState
            {
                id = $"{encounter.id}:event:0000001",
                sequence = 1,
                tick = 0,
                eventType = EncounterEventType.EncounterStarted,
                actorEntityId = party.leaderHunterId,
                areaId = entrance.id,
                position = entrance.center,
                summary = $"{party.displayName} entered {gate.displayName}."
            });

            world.encounters.Add(encounter);
            world.encounters.Sort((left, right) =>
                string.CompareOrdinal(left?.id, right?.id));
            gate.activeEncounterId = encounter.id;
            gate.currentContractId = contract.id;
            gate.lifecycle = GateLifecycleState.InProgress;
            contract.activeEncounterId = encounter.id;
            return encounter;
        }

        private static void GenerateTopology(GateInstanceState gate, GateLocalRandom random)
        {
            var occupied = new HashSet<Vector2Int>();
            var entrance = CreateArea(
                gate,
                "entrance",
                "Gate Threshold",
                DungeonAreaType.Entrance,
                Vector2Int.zero,
                random);
            entrance.discovered = true;
            occupied.Add(Vector2Int.zero);

            var combatCount = Mathf.Clamp(2 + gate.trueDifficulty, 3, 7);
            var combatAreas = new List<DungeonAreaState>();
            var combatCells = new List<Vector2Int>();
            var mainPath = new List<DungeonAreaState> { entrance };

            switch (gate.layoutStyle)
            {
                case DungeonLayoutStyle.Linear:
                {
                    for (var index = 0; index < combatCount; index++)
                    {
                        var cell = new Vector2Int(index + 1, 0);
                        var area = AddCombatArea(gate, index, cell, random);
                        Connect(gate, mainPath[^1], area, random);
                        mainPath.Add(area);
                        combatAreas.Add(area);
                        combatCells.Add(cell);
                        occupied.Add(cell);
                    }
                    break;
                }
                case DungeonLayoutStyle.Winding:
                {
                    var pattern = new[] { 0, 1, 1, 0, -1, -1, 0 };
                    for (var index = 0; index < combatCount; index++)
                    {
                        var cell = new Vector2Int(index + 1, pattern[index % pattern.Length]);
                        var area = AddCombatArea(gate, index, cell, random);
                        Connect(gate, mainPath[^1], area, random);
                        mainPath.Add(area);
                        combatAreas.Add(area);
                        combatCells.Add(cell);
                        occupied.Add(cell);
                    }
                    break;
                }
                case DungeonLayoutStyle.HubAndSpoke:
                {
                    var hubCell = new Vector2Int(1, 0);
                    var hub = AddCombatArea(gate, 0, hubCell, random, "Central Convergence");
                    Connect(gate, entrance, hub, random);
                    mainPath.Add(hub);
                    combatAreas.Add(hub);
                    combatCells.Add(hubCell);
                    occupied.Add(hubCell);
                    var spokes = new[]
                    {
                        new Vector2Int(1, 1), new Vector2Int(2, 0), new Vector2Int(1, -1),
                        new Vector2Int(2, 1), new Vector2Int(2, -1), new Vector2Int(3, 1),
                        new Vector2Int(3, -1)
                    };
                    for (var index = 1; index < combatCount; index++)
                    {
                        var cell = spokes[(index - 1) % spokes.Length];
                        var area = AddCombatArea(gate, index, cell, random);
                        var inwardCell = new Vector2Int(Mathf.Max(1, cell.x - 1), cell.y);
                        var inwardIndex = combatCells.FindIndex(existing => existing == inwardCell);
                        var parent = inwardIndex >= 0 ? combatAreas[inwardIndex] : hub;
                        Connect(gate, parent, area, random);
                        combatAreas.Add(area);
                        combatCells.Add(cell);
                        occupied.Add(cell);
                    }
                    break;
                }
                default:
                {
                    var mainCount = Mathf.Max(2, (combatCount + 1) / 2);
                    for (var index = 0; index < mainCount; index++)
                    {
                        var cell = new Vector2Int(index + 1, 0);
                        var area = AddCombatArea(gate, index, cell, random);
                        Connect(gate, mainPath[^1], area, random);
                        mainPath.Add(area);
                        combatAreas.Add(area);
                        combatCells.Add(cell);
                        occupied.Add(cell);
                    }
                    for (var index = mainCount; index < combatCount; index++)
                    {
                        var parentIndex = 1 + (index - mainCount) % mainCount;
                        var parent = mainPath[parentIndex];
                        var parentCell = combatCells[parentIndex - 1];
                        var cell = FindOpenCell(parentCell, occupied, index);
                        var area = AddCombatArea(gate, index, cell, random);
                        Connect(gate, parent, area, random);
                        combatAreas.Add(area);
                        combatCells.Add(cell);
                        occupied.Add(cell);
                    }
                    break;
                }
            }

            var bossParent = gate.layoutStyle == DungeonLayoutStyle.HubAndSpoke
                ? combatAreas.Find(area => CellFor(area.center) == new Vector2Int(2, 0)) ??
                  combatAreas[0]
                : mainPath[^1];
            var bossParentCell = CellFor(bossParent.center);
            var bossCell = FindOpenCell(
                bossParentCell,
                occupied,
                combatCount + 3,
                preferHorizontal: true);
            var boss = CreateArea(
                gate,
                "boss",
                "Gate Core",
                DungeonAreaType.Boss,
                bossCell,
                random,
                1.35f);
            occupied.Add(bossCell);
            Connect(gate, bossParent, boss, random);

            var treasureParentIndex = random.Range(0, combatAreas.Count);
            var treasureParent = combatAreas[treasureParentIndex];
            var treasureCell = FindOpenCell(CellFor(treasureParent.center), occupied, 0);
            var treasure = CreateArea(
                gate,
                "treasure",
                "Hidden Cache",
                DungeonAreaType.Treasure,
                treasureCell,
                random,
                0.85f);
            occupied.Add(treasureCell);
            Connect(gate, treasureParent, treasure, random);

            var resourceParentIndex = (treasureParentIndex + Mathf.Max(1, combatAreas.Count / 2)) %
                                      combatAreas.Count;
            var resourceParent = combatAreas[resourceParentIndex];
            var resourceCell = FindOpenCell(CellFor(resourceParent.center), occupied, 1);
            var resource = CreateArea(
                gate,
                "resource",
                "Mana Seam",
                DungeonAreaType.Resource,
                resourceCell,
                random,
                0.9f);
            occupied.Add(resourceCell);
            Connect(gate, resourceParent, resource, random);
        }

        private static DungeonAreaState AddCombatArea(
            GateInstanceState gate,
            int index,
            Vector2Int cell,
            GateLocalRandom random,
            string displayName = null)
        {
            return CreateArea(
                gate,
                $"combat-{index + 1:D2}",
                displayName ?? $"{BiomeRoomName(gate.biome)} {index + 1}",
                DungeonAreaType.Combat,
                cell,
                random);
        }

        private static DungeonAreaState CreateArea(
            GateInstanceState gate,
            string suffix,
            string displayName,
            DungeonAreaType type,
            Vector2Int cell,
            GateLocalRandom random,
            float sizeMultiplier = 1f)
        {
            var width = random.Range(9, 14) * sizeMultiplier;
            var height = random.Range(9, 14) * sizeMultiplier;
            var area = new DungeonAreaState
            {
                id = $"{gate.id}:area:{suffix}",
                displayName = displayName,
                areaType = type,
                center = new Vector2(cell.x * GridSpacing, cell.y * GridSpacing),
                size = new Vector2(width, height)
            };
            gate.areas.Add(area);
            return area;
        }

        private static void Connect(
            GateInstanceState gate,
            DungeonAreaState from,
            DungeonAreaState to,
            GateLocalRandom random)
        {
            var waypoints = new List<Vector2> { from.center };
            if (Mathf.Abs(from.center.x - to.center.x) > 0.01f &&
                Mathf.Abs(from.center.y - to.center.y) > 0.01f)
            {
                waypoints.Add(random.Next01() < 0.5f
                    ? new Vector2(to.center.x, from.center.y)
                    : new Vector2(from.center.x, to.center.y));
            }
            waypoints.Add(to.center);
            gate.connections.Add(new DungeonConnectionState
            {
                id = $"{gate.id}:route:{gate.connections.Count + 1:D2}",
                fromAreaId = from.id,
                toAreaId = to.id,
                waypoints = waypoints
            });
        }

        private static void GeneratePodsAndMonsters(
            GateInstanceState gate,
            ContractState contract,
            GateLocalRandom random)
        {
            var combatAreas = gate.areas.FindAll(area => area != null &&
                (area.areaType == DungeonAreaType.Combat ||
                 area.areaType == DungeonAreaType.Boss));
            combatAreas.Sort((left, right) => string.CompareOrdinal(left.id, right.id));
            foreach (var area in combatAreas)
            {
                var boss = area.areaType == DungeonAreaType.Boss;
                var pod = new DungeonMobPodState
                {
                    id = $"{gate.id}:pod:{gate.mobPods.Count + 1:D2}",
                    areaId = area.id,
                    status = DungeonMobPodStatus.Dormant
                };
                var count = boss
                    ? 1 + (gate.trueDifficulty >= 5 ? 1 : 0)
                    : Mathf.Clamp(2 + gate.trueDifficulty / 2 + random.Range(0, 2), 2, 6);
                for (var index = 0; index < count; index++)
                {
                    var monsterId = $"{pod.id}:monster:{index + 1:D2}";
                    var maximumHealth = boss
                        ? 135 + gate.trueDifficulty * 42
                        : 34 + gate.trueDifficulty * 16;
                    var monster = new DungeonMonsterState
                    {
                        id = monsterId,
                        definitionId = MonsterDefinitionId(gate.biome, boss),
                        displayName = boss
                            ? BossName(gate.biome, index)
                            : MonsterName(gate.biome, index),
                        podId = pod.id,
                        areaId = area.id,
                        position = RandomPoint(area, random, 1.5f),
                        facing = RandomDirection(random),
                        maximumHealth = maximumHealth,
                        maximumMana = boss ? 40 + gate.trueDifficulty * 8 : 0,
                        maximumShield = boss ? gate.trueDifficulty * 8 : 0,
                        combatPower = boss
                            ? 24f + gate.trueDifficulty * 15f
                            : 8f + gate.trueDifficulty * 7f,
                        moveSpeed = boss ? 2.7f : 3.1f + random.Next01() * 0.8f,
                        attackRange = boss ? 2.1f : 1.25f + random.Next01() * 0.35f,
                        attackDamage = boss
                            ? 13f + gate.trueDifficulty * 5f
                            : 5f + gate.trueDifficulty * 2.4f,
                        attackCooldownTicks = boss ? 7 : random.Range(5, 8),
                        boss = boss
                    };
                    gate.monsters.Add(monster);
                    pod.monsterIds.Add(monster.id);
                    if (boss && string.IsNullOrEmpty(gate.bossMonsterId))
                    {
                        gate.bossMonsterId = monster.id;
                    }
                }
                pod.monsterIds.Sort(StringComparer.Ordinal);
                gate.mobPods.Add(pod);
            }
        }

        private static void GenerateLootAndResources(
            GateInstanceState gate,
            GateLocalRandom random)
        {
            var treasure = gate.areas.Find(area => area != null &&
                area.areaType == DungeonAreaType.Treasure);
            if (treasure != null)
            {
                gate.lootNodes.Add(new DungeonLootNodeState
                {
                    id = $"{gate.id}:loot:treasure-01",
                    areaId = treasure.id,
                    position = RandomPoint(treasure, random, 1.2f),
                    lootTableId = $"loot.{gate.visualStyleId}.treasure",
                    gold = 18 + gate.trueDifficulty * 24 + random.Range(0, 25),
                    guildResources = 4 + gate.trueDifficulty * 7,
                    status = DungeonLootStatus.Hidden
                });
            }

            var bossArea = gate.areas.Find(area => area != null &&
                area.areaType == DungeonAreaType.Boss);
            if (bossArea != null)
            {
                gate.lootNodes.Add(new DungeonLootNodeState
                {
                    id = $"{gate.id}:loot:boss-01",
                    areaId = bossArea.id,
                    position = bossArea.center + Vector2.up * 2f,
                    lootTableId = $"loot.{gate.visualStyleId}.boss",
                    gold = 35 + gate.trueDifficulty * 38 + random.Range(0, 40),
                    guildResources = 8 + gate.trueDifficulty * 11,
                    status = DungeonLootStatus.Hidden
                });
            }

            var resourceArea = gate.areas.Find(area => area != null &&
                area.areaType == DungeonAreaType.Resource);
            if (resourceArea == null) return;
            var nodeCount = random.Range(2, 4);
            for (var index = 0; index < nodeCount; index++)
            {
                var amount = 8 + gate.trueDifficulty * 6 + random.Range(0, 8);
                gate.resourceNodes.Add(new DungeonResourceNodeState
                {
                    id = $"{gate.id}:resource:{index + 1:D2}",
                    areaId = resourceArea.id,
                    position = RandomPoint(resourceArea, random, 1.1f),
                    resourceId = ResourceId(gate.biome),
                    initialAmount = amount,
                    remainingAmount = amount
                });
            }
        }

        private static void GenerateHazards(GateInstanceState gate, GateLocalRandom random)
        {
            var hazardType = HazardFor(gate.biome);
            foreach (var area in gate.areas)
            {
                if (area == null || area.areaType is DungeonAreaType.Entrance or
                    DungeonAreaType.Treasure || random.Next01() > 0.48f)
                {
                    continue;
                }
                gate.hazards.Add(new DungeonHazardState
                {
                    id = $"{gate.id}:hazard:{gate.hazards.Count + 1:D2}",
                    areaId = area.id,
                    hazardType = hazardType,
                    position = RandomPoint(area, random, 1.8f),
                    radius = 1.1f + random.Next01() * 1.2f,
                    damagePerTick = 0.5f + gate.trueDifficulty * 0.2f
                });
            }
            if (gate.hazards.Count > 0) return;
            var fallbackArea = gate.areas.Find(area => area != null &&
                area.areaType == DungeonAreaType.Boss) ??
                               gate.areas.Find(area => area != null &&
                                   area.areaType == DungeonAreaType.Combat);
            if (fallbackArea == null) return;
            gate.hazards.Add(new DungeonHazardState
            {
                id = $"{gate.id}:hazard:01",
                areaId = fallbackArea.id,
                hazardType = hazardType,
                position = RandomPoint(fallbackArea, random, 1.8f),
                radius = 1.4f,
                damagePerTick = 0.5f + gate.trueDifficulty * 0.2f
            });
        }

        private static void AddHunterParticipants(
            EcosystemWorldState world,
            PartyState party,
            IReadOnlyList<EcosystemGearDefinition> gearCatalog,
            DungeonAreaState entrance,
            DungeonEncounterState encounter)
        {
            var memberIds = party.memberIds == null
                ? new List<string>()
                : new List<string>(party.memberIds);
            memberIds.Sort(StringComparer.Ordinal);
            for (var index = 0; index < memberIds.Count; index++)
            {
                var hunter = world.hunters?.Find(item => item != null && item.id == memberIds[index]);
                if (hunter == null || !hunter.IsActive) continue;
                var power = EcosystemCareerRules.CombatPower(
                    hunter,
                    gearCatalog ?? Array.Empty<EcosystemGearDefinition>());
                var row = index / 3;
                var column = index % 3;
                var position = entrance.center + new Vector2(
                    (column - 1) * 1.1f,
                    -1.2f - row * 1.1f);
                encounter.participants.Add(new EncounterParticipantState
                {
                    entityId = hunter.id,
                    participantKind = EncounterParticipantKind.Hunter,
                    sourceHunterId = hunter.id,
                    definitionId = hunter.equippedGearId,
                    displayName = hunter.displayName,
                    factionId = $"party:{party.id}",
                    areaId = entrance.id,
                    position = position,
                    facing = Vector2.up,
                    vitals = CloneVitals(hunter.vitals, hunter.isAlive),
                    lifeState = EncounterParticipantLifeState.Active,
                    combatPower = power,
                    moveSpeed = MoveSpeedForHunter(hunter),
                    attackRange = AttackRangeForHunter(hunter),
                    attackDamage = Mathf.Max(5f, power * 0.24f),
                    attackCooldownTicks = AttackCooldownForHunter(hunter)
                });
                hunter.currentEncounterId = encounter.id;
                hunter.isIncapacitated = false;
            }
        }

        private static void AddMonsterParticipants(
            GateInstanceState gate,
            DungeonEncounterState encounter)
        {
            foreach (var monster in gate.monsters)
            {
                if (monster == null) continue;
                encounter.participants.Add(new EncounterParticipantState
                {
                    entityId = monster.id,
                    participantKind = EncounterParticipantKind.Monster,
                    definitionId = monster.definitionId,
                    displayName = monster.displayName,
                    factionId = $"gate:{gate.id}:hostile",
                    podId = monster.podId,
                    areaId = monster.areaId,
                    position = monster.position,
                    facing = monster.facing,
                    vitals = new HunterVitalsState
                    {
                        initialized = true,
                        maximumHealth = Mathf.Max(1, monster.maximumHealth),
                        currentHealth = Mathf.Max(1, monster.maximumHealth),
                        maximumMana = Mathf.Max(1, monster.maximumMana),
                        currentMana = Mathf.Max(0, monster.maximumMana),
                        maximumShield = Mathf.Max(1, monster.maximumShield),
                        currentShield = Mathf.Max(0, monster.maximumShield)
                    },
                    lifeState = EncounterParticipantLifeState.Active,
                    combatPower = monster.combatPower,
                    moveSpeed = monster.moveSpeed,
                    attackRange = monster.attackRange,
                    attackDamage = monster.attackDamage,
                    attackCooldownTicks = Mathf.Max(1, monster.attackCooldownTicks)
                });
            }
        }

        internal static List<DungeonAreaState> CloneAreas(IEnumerable<DungeonAreaState> source)
        {
            var result = new List<DungeonAreaState>();
            if (source == null) return result;
            foreach (var item in source)
            {
                if (item == null) continue;
                result.Add(new DungeonAreaState
                {
                    id = item.id,
                    displayName = item.displayName,
                    areaType = item.areaType,
                    center = item.center,
                    size = item.size,
                    discovered = item.discovered,
                    cleared = item.cleared
                });
            }
            return result;
        }

        internal static List<DungeonConnectionState> CloneConnections(
            IEnumerable<DungeonConnectionState> source)
        {
            var result = new List<DungeonConnectionState>();
            if (source == null) return result;
            foreach (var item in source)
            {
                if (item == null) continue;
                result.Add(new DungeonConnectionState
                {
                    id = item.id,
                    fromAreaId = item.fromAreaId,
                    toAreaId = item.toAreaId,
                    waypoints = item.waypoints == null
                        ? new List<Vector2>()
                        : new List<Vector2>(item.waypoints),
                    locked = item.locked
                });
            }
            return result;
        }

        internal static List<DungeonMobPodState> ClonePods(IEnumerable<DungeonMobPodState> source)
        {
            var result = new List<DungeonMobPodState>();
            if (source == null) return result;
            foreach (var item in source)
            {
                if (item == null) continue;
                result.Add(new DungeonMobPodState
                {
                    id = item.id,
                    areaId = item.areaId,
                    status = item.status,
                    monsterIds = item.monsterIds == null
                        ? new List<string>()
                        : new List<string>(item.monsterIds)
                });
            }
            return result;
        }

        internal static List<DungeonLootNodeState> CloneLoot(IEnumerable<DungeonLootNodeState> source)
        {
            var result = new List<DungeonLootNodeState>();
            if (source == null) return result;
            foreach (var item in source)
            {
                if (item == null) continue;
                result.Add(new DungeonLootNodeState
                {
                    id = item.id,
                    areaId = item.areaId,
                    position = item.position,
                    lootTableId = item.lootTableId,
                    gold = item.gold,
                    guildResources = item.guildResources,
                    gearDefinitionIds = item.gearDefinitionIds == null
                        ? new List<string>()
                        : new List<string>(item.gearDefinitionIds),
                    status = item.status,
                    claimedByEntityId = item.claimedByEntityId
                });
            }
            return result;
        }

        internal static List<DungeonResourceNodeState> CloneResources(
            IEnumerable<DungeonResourceNodeState> source)
        {
            var result = new List<DungeonResourceNodeState>();
            if (source == null) return result;
            foreach (var item in source)
            {
                if (item == null) continue;
                result.Add(new DungeonResourceNodeState
                {
                    id = item.id,
                    areaId = item.areaId,
                    position = item.position,
                    resourceId = item.resourceId,
                    initialAmount = item.initialAmount,
                    remainingAmount = item.remainingAmount,
                    extractedByPartyId = item.extractedByPartyId
                });
            }
            return result;
        }

        internal static List<DungeonHazardState> CloneHazards(IEnumerable<DungeonHazardState> source)
        {
            var result = new List<DungeonHazardState>();
            if (source == null) return result;
            foreach (var item in source)
            {
                if (item == null) continue;
                result.Add(new DungeonHazardState
                {
                    id = item.id,
                    areaId = item.areaId,
                    hazardType = item.hazardType,
                    position = item.position,
                    radius = item.radius,
                    damagePerTick = item.damagePerTick,
                    active = item.active
                });
            }
            return result;
        }

        private static HunterVitalsState CloneVitals(HunterVitalsState source, bool isAlive)
        {
            if (source == null || !source.initialized)
            {
                var initialized = new HunterVitalsState();
                initialized.Initialize(isAlive, 0, 0);
                return initialized;
            }
            return new HunterVitalsState
            {
                initialized = true,
                maximumHealth = Mathf.Max(1, source.maximumHealth),
                currentHealth = Mathf.Clamp(source.currentHealth, 0, Mathf.Max(1, source.maximumHealth)),
                maximumMana = Mathf.Max(1, source.maximumMana),
                currentMana = Mathf.Clamp(source.currentMana, 0, Mathf.Max(1, source.maximumMana)),
                maximumShield = Mathf.Max(1, source.maximumShield),
                currentShield = Mathf.Clamp(source.currentShield, 0, Mathf.Max(1, source.maximumShield))
            };
        }

        private static void AddModifiers(
            GateInstanceState gate,
            bool underAppraised,
            GateLocalRandom random)
        {
            gate.visibleModifierIds.Add($"biome.{gate.visualStyleId}");
            if (random.Next01() < 0.38f) gate.visibleModifierIds.Add("gate.dense-mana");
            if (random.Next01() < 0.28f) gate.visibleModifierIds.Add("gate.fractured-passages");
            if (random.Next01() < 0.22f) gate.hiddenModifierIds.Add("gate.ambush-pods");
            if (random.Next01() < 0.14f) gate.hiddenModifierIds.Add("gate.unstable-core");
            if (underAppraised) gate.hiddenModifierIds.Add("gate.misclassified-danger");
            gate.visibleModifierIds.Sort(StringComparer.Ordinal);
            gate.hiddenModifierIds.Sort(StringComparer.Ordinal);
        }

        private static DungeonBiomeType ResolveBiome(
            ContractState contract,
            GateLocalRandom random)
        {
            var key = $"{contract.missionTemplateId}|{contract.displayName}".ToLowerInvariant();
            if (key.Contains("crypt") || key.Contains("drown")) return DungeonBiomeType.DrownedCrypt;
            if (key.Contains("void") || key.Contains("spire")) return DungeonBiomeType.VoidSpire;
            if (key.Contains("frost") || key.Contains("ice")) return DungeonBiomeType.FrostWarrens;
            if (key.Contains("chapel") || key.Contains("temple")) return DungeonBiomeType.RuinedTemple;
            if (key.Contains("glassfang") || key.Contains("nest") || key.Contains("spider"))
                return DungeonBiomeType.FungalNest;
            if (key.Contains("ash") || key.Contains("goblin")) return DungeonBiomeType.AshCavern;
            return (DungeonBiomeType)random.Range(
                0,
                Enum.GetValues(typeof(DungeonBiomeType)).Length);
        }

        /// <summary>
        /// Canonical mapping shared by gate generation, campaign actions, and save
        /// normalization. Keeping it in one place prevents reload-only state changes.
        /// </summary>
        public static GateLifecycleState LifecycleForContractStatus(ContractStatus status)
        {
            return status switch
            {
                ContractStatus.Offered => GateLifecycleState.Available,
                ContractStatus.Accepted => GateLifecycleState.RightsAwarded,
                ContractStatus.Active => GateLifecycleState.InProgress,
                ContractStatus.Succeeded => GateLifecycleState.Closed,
                ContractStatus.Failed or ContractStatus.Expired or ContractStatus.Cancelled =>
                    GateLifecycleState.AwaitingReauction,
                _ => GateLifecycleState.Manifested
            };
        }

        private static Vector2Int FindOpenCell(
            Vector2Int origin,
            HashSet<Vector2Int> occupied,
            int salt,
            bool preferHorizontal = false)
        {
            if (preferHorizontal)
            {
                for (var distance = 1; distance <= 6; distance++)
                {
                    var horizontal = origin + new Vector2Int(distance, 0);
                    if (!occupied.Contains(horizontal)) return horizontal;
                }
            }
            for (var index = 0; index < BranchOffsets.Length; index++)
            {
                var offset = BranchOffsets[(index + salt) % BranchOffsets.Length];
                var candidate = origin + offset;
                if (!occupied.Contains(candidate)) return candidate;
            }
            return origin + new Vector2Int(0, 2 + salt % 4);
        }

        private static Vector2Int CellFor(Vector2 position) => new(
            Mathf.RoundToInt(position.x / GridSpacing),
            Mathf.RoundToInt(position.y / GridSpacing));

        private static Vector2 RandomPoint(
            DungeonAreaState area,
            GateLocalRandom random,
            float margin)
        {
            var half = area.size * 0.5f - Vector2.one * margin;
            half.x = Mathf.Max(0.5f, half.x);
            half.y = Mathf.Max(0.5f, half.y);
            return area.center + new Vector2(
                Mathf.Lerp(-half.x, half.x, random.Next01()),
                Mathf.Lerp(-half.y, half.y, random.Next01()));
        }

        private static Vector2 RandomDirection(GateLocalRandom random)
        {
            var radians = random.Next01() * Mathf.PI * 2f;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        private static string VisualStyleId(DungeonBiomeType biome) => biome switch
        {
            DungeonBiomeType.AshCavern => "ash-cavern",
            DungeonBiomeType.DrownedCrypt => "drowned-crypt",
            DungeonBiomeType.VoidSpire => "void-spire",
            DungeonBiomeType.FrostWarrens => "frost-warrens",
            DungeonBiomeType.RuinedTemple => "ruined-temple",
            _ => "fungal-nest"
        };

        private static string BiomeRoomName(DungeonBiomeType biome) => biome switch
        {
            DungeonBiomeType.AshCavern => "Ember Vault",
            DungeonBiomeType.DrownedCrypt => "Flooded Ossuary",
            DungeonBiomeType.VoidSpire => "Voidglass Gallery",
            DungeonBiomeType.FrostWarrens => "Frozen Burrow",
            DungeonBiomeType.RuinedTemple => "Broken Nave",
            _ => "Spore Chamber"
        };

        private static string MonsterDefinitionId(DungeonBiomeType biome, bool boss) =>
            $"monster.{VisualStyleId(biome)}.{(boss ? "boss" : "common")}";

        private static string MonsterName(DungeonBiomeType biome, int index) => biome switch
        {
            DungeonBiomeType.AshCavern => $"Ash Goblin {index + 1}",
            DungeonBiomeType.DrownedCrypt => $"Drowned Husk {index + 1}",
            DungeonBiomeType.VoidSpire => $"Voidling {index + 1}",
            DungeonBiomeType.FrostWarrens => $"Frostfang {index + 1}",
            DungeonBiomeType.RuinedTemple => $"Chapel Thrall {index + 1}",
            _ => $"Glassfang Crawler {index + 1}"
        };

        private static string BossName(DungeonBiomeType biome, int index) => biome switch
        {
            DungeonBiomeType.AshCavern => index == 0 ? "Cinder Warchief" : "Ashbound Guard",
            DungeonBiomeType.DrownedCrypt => index == 0 ? "The Drowned Prior" : "Crypt Sentinel",
            DungeonBiomeType.VoidSpire => index == 0 ? "Voidglass Regent" : "Rift Echo",
            DungeonBiomeType.FrostWarrens => index == 0 ? "White-Maw Matriarch" : "Frost Broodguard",
            DungeonBiomeType.RuinedTemple => index == 0 ? "Red Chapel Hierophant" : "Blood Reliquary",
            _ => index == 0 ? "Glassfang Broodmother" : "Sporebound Guardian"
        };

        private static string ResourceId(DungeonBiomeType biome) => biome switch
        {
            DungeonBiomeType.VoidSpire => "voidglass-shard",
            DungeonBiomeType.FrostWarrens => "frost-mana-crystal",
            DungeonBiomeType.FungalNest => "alchemical-spore",
            _ => "mana-crystal"
        };

        private static DungeonHazardType HazardFor(DungeonBiomeType biome) => biome switch
        {
            DungeonBiomeType.AshCavern => DungeonHazardType.LavaVent,
            DungeonBiomeType.DrownedCrypt => DungeonHazardType.FloodedGround,
            DungeonBiomeType.VoidSpire => DungeonHazardType.VoidRift,
            DungeonBiomeType.FrostWarrens => DungeonHazardType.FrostPatch,
            DungeonBiomeType.RuinedTemple => DungeonHazardType.FallingDebris,
            _ => DungeonHazardType.PoisonPool
        };

        private static float MoveSpeedForHunter(HunterProfile hunter)
        {
            var id = hunter.equippedGearId ?? string.Empty;
            if (id.Contains("titan")) return 3.4f;
            if (id.Contains("rift")) return 5.2f;
            return 4.3f;
        }

        private static float AttackRangeForHunter(HunterProfile hunter)
        {
            var id = hunter.equippedGearId ?? string.Empty;
            if (id.Contains("titan")) return 1.9f;
            if (id.Contains("rift")) return 1.2f;
            return 1.5f;
        }

        private static int AttackCooldownForHunter(HunterProfile hunter)
        {
            var id = hunter.equippedGearId ?? string.Empty;
            if (id.Contains("titan")) return 8;
            if (id.Contains("rift")) return 4;
            return 6;
        }

        private static void SortManifest(GateInstanceState gate)
        {
            gate.areas.Sort((left, right) => string.CompareOrdinal(left?.id, right?.id));
            gate.connections.Sort((left, right) => string.CompareOrdinal(left?.id, right?.id));
            gate.mobPods.Sort((left, right) => string.CompareOrdinal(left?.id, right?.id));
            gate.monsters.Sort((left, right) => string.CompareOrdinal(left?.id, right?.id));
            gate.lootNodes.Sort((left, right) => string.CompareOrdinal(left?.id, right?.id));
            gate.resourceNodes.Sort((left, right) => string.CompareOrdinal(left?.id, right?.id));
            gate.hazards.Sort((left, right) => string.CompareOrdinal(left?.id, right?.id));
        }

        private static void NormalizeCollections(GateInstanceState gate)
        {
            gate.visibleModifierIds ??= new List<string>();
            gate.hiddenModifierIds ??= new List<string>();
            gate.areas ??= new List<DungeonAreaState>();
            gate.connections ??= new List<DungeonConnectionState>();
            gate.mobPods ??= new List<DungeonMobPodState>();
            gate.monsters ??= new List<DungeonMonsterState>();
            gate.lootNodes ??= new List<DungeonLootNodeState>();
            gate.resourceNodes ??= new List<DungeonResourceNodeState>();
            gate.hazards ??= new List<DungeonHazardState>();
            gate.visibleModifierIds.Sort(StringComparer.Ordinal);
            gate.hiddenModifierIds.Sort(StringComparer.Ordinal);
            SortManifest(gate);
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

        private sealed class GateLocalRandom
        {
            private uint state;

            public GateLocalRandom(uint seed)
            {
                state = seed == 0 ? 0x6D2B79F5u : seed;
            }

            public float Next01() => (NextUInt() & 0x00FFFFFFu) / 16777216f;

            public int Range(int minimumInclusive, int maximumExclusive)
            {
                if (maximumExclusive <= minimumInclusive) return minimumInclusive;
                return minimumInclusive +
                       (int)(Next01() * (maximumExclusive - minimumInclusive));
            }

            private uint NextUInt()
            {
                var value = state;
                value ^= value << 13;
                value ^= value >> 17;
                value ^= value << 5;
                state = value == 0 ? 0x6D2B79F5u : value;
                return state;
            }
        }
    }
}
