using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Turtle.Ecosystem.Editor
{
    public static class EcosystemPrototypeValidator
    {
        // File-triggered entry points let the open Unity Editor run the same complete v5 checks as the menu.
        // Resolve them from Application.dataPath so automation is independent of the process working directory.
        private static string ProjectRootPath => Directory.GetParent(Application.dataPath)?.FullName
                                                 ?? Directory.GetCurrentDirectory();
        private static string AutomationRequestPath => Path.Combine(
            ProjectRootPath,
            "Temp",
            "CodexValidation",
            "run-guild-ecosystem-validator.request");
        private static string AutomationResultPath => Path.Combine(
            ProjectRootPath,
            "Temp",
            "CodexValidation",
            "guild-ecosystem-validator.result");
        private static string AutomationLegacyFixturePath => Path.Combine(
            ProjectRootPath,
            "Temp",
            "CodexValidation",
            "actual-legacy-v1.json");
        private static string SceneBuildRequestPath => Path.Combine(
            ProjectRootPath,
            Ecosystem2DSceneBuilder.AutomationRequestPath.Replace('/', Path.DirectorySeparatorChar));

        private static readonly string[] GearPaths =
        {
            "Assets/Data/Ecosystem/Vanguard Blade.asset",
            "Assets/Data/Ecosystem/Titan Greatsword.asset",
            "Assets/Data/Ecosystem/Rift Daggers.asset"
        };

        [InitializeOnLoadMethod]
        private static void ScheduleRequestedValidation()
        {
            EditorApplication.delayCall += RunRequestedValidation;
            EditorApplication.update -= RunRequestedValidation;
            EditorApplication.update += RunRequestedValidation;
        }

        private static void RunRequestedValidation()
        {
            if (!File.Exists(AutomationRequestPath))
            {
                EditorApplication.update -= RunRequestedValidation;
                return;
            }
            if (File.Exists(SceneBuildRequestPath))
            {
                // Let the idempotent scene-wiring request serialize the active scene
                // before scene validation inspects it on the following editor update.
                return;
            }

            EditorApplication.update -= RunRequestedValidation;
            File.Delete(AutomationRequestPath);
            try
            {
                var failures = RunValidation();
                if (File.Exists(AutomationLegacyFixturePath))
                {
                    try
                    {
                        EcosystemSaveRepository.DeserializeForValidation(
                            File.ReadAllText(AutomationLegacyFixturePath),
                            LoadGear(failures),
                            out var migratedFixture);
                        if (!migratedFixture)
                        {
                            failures.Add("The supplied legacy fixture did not execute migration to the current save version.");
                        }
                    }
                    catch (Exception exception)
                    {
                        failures.Add($"Actual legacy fixture migration failed: {exception.Message}");
                    }
                }
                var lines = failures.Count == 0
                    ? new[] { "PASS", "Guild Ecosystem Prototype validation passed." }
                    : new[] { "FAIL" }.Concat(failures).ToArray();
                File.WriteAllLines(AutomationResultPath, lines);
                if (failures.Count == 0)
                {
                    Debug.Log("Guild Ecosystem Prototype requested validation passed.");
                }
                else
                {
                    Debug.LogError("Guild Ecosystem Prototype requested validation failed:\n- " +
                                   string.Join("\n- ", failures));
                }
            }
            catch (Exception exception)
            {
                File.WriteAllLines(AutomationResultPath, new[] { "ERROR", exception.ToString() });
                Debug.LogException(exception);
            }
        }

        [MenuItem("Turtle/Ecosystem/Validate Guild Ecosystem Prototype")]
        public static void ValidateFromMenu()
        {
            var failures = RunValidation();
            if (failures.Count == 0)
            {
                Debug.Log(
                    "2D Ecosystem validation passed: serious population scale, careers, guilds, facilities, " +
                    "contracts, population churn, shared commands, v5 migration, deterministic continuation, " +
                    "persisted gate manifests, fixed-step encounters, spatial scene wiring, and invariants.");
                return;
            }

            Debug.LogError("Guild Ecosystem Prototype validation failed:\n- " +
                           string.Join("\n- ", failures));
        }

        public static void RunFromCommandLine()
        {
            var failures = RunValidation();
            if (failures.Count == 0)
            {
                Debug.Log("Guild Ecosystem Prototype command-line validation passed.");
                return;
            }

            throw new InvalidOperationException(string.Join(Environment.NewLine, failures));
        }

        public static List<string> RunValidation()
        {
            var failures = new List<string>();
            var gear = LoadGear(failures);
            if (gear.Count == 0)
            {
                return failures;
            }

            ValidateInitialWorld(gear, failures);
            ValidateSeriousEcosystemScale(gear, failures);
            ValidateGateManifests(gear, failures);
            ValidateCareerDefaults(gear, failures);
            ValidateCareerExperience(gear, failures);
            ValidateCareerRank(gear, failures);
            ValidateCareerCommandParity(gear, failures);
            ValidateCareerLoadout(gear, failures);
            ValidateCareerPlanningRegressions(gear, failures);
            ValidateCareerRobustness(gear, failures);
            ValidateSharedCommands(gear, failures);
            ValidatePartyLifecycle(gear, failures);
            ValidateTravelLifecycle(gear, failures);
            ValidateDecisionInspection(gear, failures);
            ValidateContractLifecycle(gear, failures);
            ValidateMigration(gear, failures);
            ValidatePersistenceRoundTrip(gear, failures);
            ValidateDeterministicContinuation(gear, failures);
            ValidatePopulationLifecycle(gear, failures);
            ValidateMultiWeekInvariants(gear, failures);
            Validate2DSceneWiring(failures);
            return failures;
        }

        private static List<EcosystemGearDefinition> LoadGear(List<string> failures)
        {
            var gear = new List<EcosystemGearDefinition>();
            foreach (var path in GearPaths)
            {
                var definition = AssetDatabase.LoadAssetAtPath<EcosystemGearDefinition>(path);
                if (definition == null)
                {
                    failures.Add($"Missing gear definition: {path}");
                }
                else
                {
                    gear.Add(definition);
                }
            }
            return gear;
        }

        private static void ValidateInitialWorld(
            IReadOnlyList<EcosystemGearDefinition> gear,
            List<string> failures)
        {
            var state = EcosystemWorldFactory.CreateDefaultWorld(gear, 42);
            Check(state.hunters.Count == EcosystemWorldFactory.StartingHunterCount,
                $"Default world must contain exactly {EcosystemWorldFactory.StartingHunterCount} hunters.",
                failures);
            Check(state.hunters.Select(hunter => hunter.id).Distinct().Count() ==
                  EcosystemWorldFactory.StartingHunterCount,
                "Hunter IDs must be unique.", failures);
            Check(state.guilds.Count == EcosystemWorldFactory.RequiredGuildCount,
                $"Default world must contain exactly {EcosystemWorldFactory.RequiredGuildCount} guilds.",
                failures);
            Check(state.map.locations.Any(location => location.locationType == LocationType.Town),
                "Map needs at least one town.", failures);
            Check(state.map.locations.Any(location => location.locationType == LocationType.ResourceSite),
                "Map needs at least one resource site.", failures);
            Check(state.map.locations.Any(location => location.locationType == LocationType.Dungeon),
                "Map needs at least one dungeon.", failures);
            Check(state.map.locations.Count(location => location.locationType == LocationType.Marketplace) >= 3,
                "Map needs several persistent marketplaces.", failures);
            Check(state.map.locations.Count(location => location.locationType == LocationType.Hospital) >= 3,
                "Map needs several persistent hospitals.", failures);
            Check(state.contracts.Any(contract => contract.status == ContractStatus.Offered &&
                                                  contract.expiresDay > state.day),
                "Default contract board needs an expiring offer.", failures);
            Check(gear.All(item => item.GrantedMoves.Count >= 2 && item.TacticalRole != TacticalRole.Flexible),
                "Every prototype gear item must grant multiple moves and a tactical role.", failures);
            var player = state.hunters.Find(hunter => hunter.id == state.playerHunterId);
            Check(player != null && gear.All(item => player.inventoryGearIds.Contains(item.GearId)),
                "The controlled prototype hunter must own every test moveset.", failures);
            failures.AddRange(EcosystemWorldFactory.ValidateInvariants(state, gear));
        }

        private static void ValidateSeriousEcosystemScale(
            IReadOnlyList<EcosystemGearDefinition> gear,
            List<string> failures)
        {
            var state = EcosystemWorldFactory.CreateDefaultWorld(gear, 4210);
            var active = state.hunters.Count(hunter => hunter != null && hunter.IsActive);
            var concurrentContracts = state.contracts.Count(contract => contract != null &&
                contract.expiresDay >= state.day &&
                contract.status is ContractStatus.Offered or ContractStatus.Accepted or ContractStatus.Active);
            var distinctNames = state.hunters
                .Where(hunter => hunter != null)
                .Select(hunter => hunter.displayName)
                .Distinct(StringComparer.Ordinal)
                .Count();
            var representedRanks = state.hunters
                .Where(hunter => hunter != null)
                .Select(EcosystemCareerRules.RankFor)
                .Distinct()
                .Count();
            var lowRankCount = state.hunters.Count(hunter => hunter != null &&
                EcosystemCareerRules.RankFor(hunter) is HunterRank.E or HunterRank.D);

            Check(active == EcosystemWorldFactory.StartingHunterCount,
                "The serious slice must begin with eighty active hunters.", failures);
            Check(distinctNames == state.hunters.Count,
                "Every generated hunter must begin with a distinct screen identity.", failures);
            Check(representedRanks >= 3 && lowRankCount >= state.hunters.Count / 2,
                "Generated hunters must span multiple ranks while remaining weighted toward E and D.", failures);
            Check(state.guilds.Count is >= 4 and <= 6,
                "The serious slice must contain four to six active guilds.", failures);
            Check(state.map.locations.Count(location => location.locationType == LocationType.Town) is >= 2 and <= 3,
                "The serious slice must contain two to three towns in one region.", failures);
            Check(state.map.locations.Count(location => location.locationType == LocationType.Marketplace) >= 3 &&
                  state.map.locations.Count(location => location.locationType == LocationType.Hospital) >= 3,
                "Each regional town needs accessible market and hospital infrastructure.", failures);
            Check(concurrentContracts is >= 5 and <= 15,
                "The serious slice must begin with five to fifteen concurrent gates/contracts.", failures);
        }

        private static void ValidateGateManifests(
            IReadOnlyList<EcosystemGearDefinition> gear,
            List<string> failures)
        {
            var state = EcosystemWorldFactory.CreateDefaultWorld(
                gear,
                EcosystemWorldFactory.DefaultWorldSeed);
            var gates = state.gates
                .Where(gate => gate != null)
                .OrderBy(gate => gate.id, StringComparer.Ordinal)
                .ToArray();
            var linkedContracts = state.contracts.Count(contract =>
                contract != null && !string.IsNullOrEmpty(contract.gateId));

            Check(gates.Length == linkedContracts && gates.Length is >= 5 and <= 15,
                "Every live prototype contract must own one persisted gate manifest.", failures);
            Check(gates.Select(gate => gate.biome).Distinct().Count() >= 4,
                "The default gate board must visibly exercise several procedural dungeon biomes.", failures);
            Check(gates.Select(gate => gate.layoutStyle).Distinct().Count() >= 3,
                "The default gate board must exercise several procedural dungeon layouts.", failures);

            foreach (var gate in gates)
            {
                var areas = gate.areas.Where(area => area != null).ToArray();
                var areaIds = new HashSet<string>(
                    areas.Select(area => area.id),
                    StringComparer.Ordinal);
                var visited = new HashSet<string>(StringComparer.Ordinal);
                if (areas.Length > 0)
                {
                    visited.Add(areas[0].id);
                    var changed = true;
                    while (changed)
                    {
                        changed = false;
                        foreach (var connection in gate.connections.Where(item => item != null))
                        {
                            if (visited.Contains(connection.fromAreaId) &&
                                visited.Add(connection.toAreaId)) changed = true;
                            if (visited.Contains(connection.toAreaId) &&
                                visited.Add(connection.fromAreaId)) changed = true;
                        }
                    }
                }

                Check(gate.generatorVersion == EcosystemGateGenerator.CurrentGeneratorVersion &&
                      !string.IsNullOrEmpty(gate.visualStyleId),
                    $"Gate '{gate.id}' must retain its generator version and visual style.", failures);
                Check(areas.Any(area => area.areaType == DungeonAreaType.Entrance) &&
                      areas.Any(area => area.areaType == DungeonAreaType.Boss) &&
                      visited.SetEquals(areaIds),
                    $"Gate '{gate.id}' must have a connected entrance-to-boss topology.", failures);
                Check(gate.connections.All(connection => connection != null &&
                          areaIds.Contains(connection.fromAreaId) &&
                          areaIds.Contains(connection.toAreaId) &&
                          connection.waypoints != null && connection.waypoints.Count >= 2),
                    $"Gate '{gate.id}' connections must reference real areas and persisted waypoints.",
                    failures);
                Check(gate.mobPods.Count > 0 && gate.monsters.Count > 0 &&
                      gate.monsters.Any(monster => monster != null && monster.id == gate.bossMonsterId),
                    $"Gate '{gate.id}' must persist mob pods and a boss.", failures);
                Check(gate.lootNodes.Count > 0 && gate.resourceNodes.Count > 0,
                    $"Gate '{gate.id}' must persist loot and extractable resources before observation.",
                    failures);
            }

            var repeated = EcosystemWorldFactory.CreateDefaultWorld(
                gear,
                EcosystemWorldFactory.DefaultWorldSeed);
            var firstManifests = string.Join("\n", gates.Select(JsonUtility.ToJson));
            var repeatedManifests = string.Join("\n", repeated.gates
                .Where(gate => gate != null)
                .OrderBy(gate => gate.id, StringComparer.Ordinal)
                .Select(JsonUtility.ToJson));
            Check(firstManifests == repeatedManifests,
                "Gate manifests must be deterministic for the same world seed and contract IDs.",
                failures);

            var randomCursorBefore = state.simulationSequence;
            var validationContract = new ContractState
            {
                id = "contract-validator-local-seed",
                displayName = "Validator Gate",
                locationId = state.map.locations.First().id,
                targetLocationId = state.map.locations.First().id,
                difficulty = 3,
                offeredDay = state.day,
                expiresDay = state.day + 2
            };
            EcosystemGateGenerator.CreateManifest(state, validationContract);
            Check(state.simulationSequence == randomCursorBefore,
                "Gate-local generation must not consume the shared world random cursor.", failures);
        }

        private static void ValidateCareerDefaults(
            IReadOnlyList<EcosystemGearDefinition> gear,
            List<string> failures)
        {
            var state = EcosystemWorldFactory.CreateDefaultWorld(gear, 4201);
            Check(state.hunters.All(hunter => hunter.career != null && hunter.career.initialized),
                "Every default hunter must begin with an initialized career.", failures);
            Check(state.hunters.All(hunter => EcosystemCareerRules.Validate(hunter).Count == 0),
                "Every default hunter career must satisfy progression and loadout invariants.", failures);

            var inferredBuilds = state.hunters
                .Select(hunter => EcosystemCareerRules.InferBuild(hunter, gear).Label)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            Check(inferredBuilds.Length >= 3,
                "Default hunters must infer into at least three distinct pure or hybrid builds.", failures);
        }

        private static void ValidateCareerExperience(
            IReadOnlyList<EcosystemGearDefinition> gear,
            List<string> failures)
        {
            var state = EcosystemWorldFactory.CreateDefaultWorld(gear, 4202);
            var hunter = state.hunters.Find(item => item.id == state.playerHunterId);
            var originalEarned = hunter.career.earnedAbilityPoints;
            var threshold = EcosystemCareerRules.ExperienceThreshold(hunter);
            hunter.career.currentExperience = threshold - 1;
            hunter.career.lifetimeExperience = Math.Max(
                hunter.career.lifetimeExperience,
                hunter.career.currentExperience);
            var boundary = EcosystemCareerRules.GrantExperience(hunter, 1);
            Check(boundary.ExperienceGranted == 1 && boundary.AbilityPointsGranted == 1 &&
                  hunter.career.earnedAbilityPoints == originalEarned + 1 &&
                  hunter.career.currentExperience == 0,
                "Crossing the exact XP threshold must reset the bar and award one Ability Point.", failures);

            hunter.career.currentExperience = 0;
            var beforeMultiple = hunter.career.earnedAbilityPoints;
            var firstThreshold = EcosystemCareerRules.ExperienceThreshold(beforeMultiple);
            var secondThreshold = EcosystemCareerRules.ExperienceThreshold(beforeMultiple + 1);
            var multiAmount = checked((int)(firstThreshold + secondThreshold + 5));
            var multiple = EcosystemCareerRules.GrantExperience(hunter, multiAmount);
            Check(multiple.AbilityPointsGranted == 2 &&
                  hunter.career.earnedAbilityPoints == beforeMultiple + 2 &&
                  hunter.career.currentExperience == 5,
                "One large XP award must process multiple bars without losing overflow.", failures);

            hunter.isAlive = false;
            var deadCareerSnapshot = JsonUtility.ToJson(hunter.career);
            var deadGrant = EcosystemCareerRules.GrantExperience(hunter, 999);
            Check(deadGrant.ExperienceGranted == 0 && deadGrant.AbilityPointsGranted == 0 &&
                  JsonUtility.ToJson(hunter.career) == deadCareerSnapshot,
                "Dead hunters must receive no XP or Ability Points and career state must not mutate.", failures);
        }

        private static void ValidateCareerRank(
            IReadOnlyList<EcosystemGearDefinition> gear,
            List<string> failures)
        {
            var state = EcosystemWorldFactory.CreateDefaultWorld(gear, 4203);
            var hunter = state.hunters.Find(item => item.id == state.playerHunterId);
            foreach (var attribute in hunter.career.attributes)
            {
                attribute.investedAbilityPoints = 0;
            }
            hunter.career.learnedAbilities.Clear();
            hunter.career.loadout = new HunterAbilityLoadout();
            hunter.career.earnedAbilityPoints = 0;

            var baseRank = EcosystemCareerRules.RankFor(hunter);
            hunter.career.earnedAbilityPoints = 12;
            var rankWithUnspentPoints = EcosystemCareerRules.RankFor(hunter);
            var originalGear = hunter.equippedGearId;
            hunter.equippedGearId = gear.First(item => item.GearId != originalGear).GearId;
            var rankWithDifferentGear = EcosystemCareerRules.RankFor(hunter);
            Check(baseRank == HunterRank.E && rankWithUnspentPoints == baseRank &&
                  rankWithDifferentGear == baseRank,
                "Official rank must ignore earned-but-unspent AP and equipped gear.", failures);

            hunter.career.attributes[0].investedAbilityPoints =
                EcosystemCareerRules.AbilityPointsPerRankBand;
            Check(EcosystemCareerRules.RankFor(hunter) == HunterRank.D,
                "Exactly six invested Ability Points must advance a hunter from E to D rank.", failures);
            var rankBoundaries = new[]
            {
                (points: 0, rank: HunterRank.E),
                (points: 5, rank: HunterRank.E),
                (points: 6, rank: HunterRank.D),
                (points: 11, rank: HunterRank.D),
                (points: 12, rank: HunterRank.C),
                (points: 17, rank: HunterRank.C),
                (points: 18, rank: HunterRank.B),
                (points: 23, rank: HunterRank.B),
                (points: 24, rank: HunterRank.A),
                (points: 29, rank: HunterRank.A),
                (points: 30, rank: HunterRank.S),
                (points: int.MaxValue, rank: HunterRank.S)
            };
            Check(rankBoundaries.All(boundary =>
                    EcosystemCareerRules.RankForInvestedPoints(boundary.points) == boundary.rank),
                "Every exact six-point Association rank boundary must derive E through S correctly.",
                failures);
        }

        private static void ValidateCareerCommandParity(
            IReadOnlyList<EcosystemGearDefinition> gear,
            List<string> failures)
        {
            var state = EcosystemWorldFactory.CreateDefaultWorld(gear, 4204);
            var service = new EcosystemActionService(state, gear);
            var player = state.hunters.Find(item => item.id == state.playerHunterId);
            var autonomous = state.hunters.Find(item => item.id != state.playerHunterId && item.isAlive);
            var town = state.map.locations.Find(item => item.locationType == LocationType.Town);
            var actors = new[] { player, autonomous };
            foreach (var actor in actors)
            {
                actor.locationId = town.id;
                actor.destinationId = "";
                actor.travelDaysRemaining = 0;
                actor.partyId = "";
                actor.activeContractId = "";
                actor.career.lastTrainingDay = -1;
                actor.career.earnedAbilityPoints += 20;
                actor.career.learnedAbilities.Clear();
                actor.career.loadout = new HunterAbilityLoadout();
                actor.career.plannedAbilityId = "";
                EcosystemCareerRules.Normalize(actor, gear);
            }

            foreach (var actor in actors)
            {
                var strengthBefore = EcosystemCareerRules.FindAttribute(actor, "strength")
                    .investedAbilityPoints;
                var results = new[]
                {
                    service.Execute(new EcosystemActionRequest(HunterActionType.Train, actor.id)),
                    service.Execute(new EcosystemActionRequest(HunterActionType.InvestAttribute, actor.id)
                    {
                        progressionId = "strength",
                        pointAmount = 1
                    }),
                    service.Execute(new EcosystemActionRequest(HunterActionType.LearnAbility, actor.id)
                    {
                        progressionId = "fighter.power-strike"
                    }),
                    service.Execute(new EcosystemActionRequest(HunterActionType.EquipAbility, actor.id)
                    {
                        progressionId = "fighter.power-strike",
                        slotIndex = 1
                    }),
                    service.Execute(new EcosystemActionRequest(HunterActionType.LearnAbility, actor.id)
                    {
                        progressionId = "fighter.relentless"
                    }),
                    service.Execute(new EcosystemActionRequest(HunterActionType.EquipPassive, actor.id)
                    {
                        progressionId = "fighter.relentless",
                        slotIndex = 1
                    }),
                    service.Execute(new EcosystemActionRequest(HunterActionType.SaveAbilityPoints, actor.id)
                    {
                        progressionId = "mage.arcane-nova"
                    })
                };
                Check(results.All(result => result.success && result.reasonCode == "ok"),
                    $"{actor.displayName} must use Train/Invest/Learn/Equip/Save through the shared command service.",
                    failures);
                Check(actor.career.lastTrainingDay == state.day &&
                      EcosystemCareerRules.FindAttribute(actor, "strength").investedAbilityPoints ==
                          strengthBefore + 1 &&
                      EcosystemCareerRules.IsLearned(actor.career, "fighter.power-strike") &&
                      actor.career.loadout.cooldownAbilityIds[1] == "fighter.power-strike" &&
                      EcosystemCareerRules.IsLearned(actor.career, "fighter.relentless") &&
                      actor.career.loadout.passiveAbilityIds[1] == "fighter.relentless" &&
                      actor.career.plannedAbilityId == "mage.arcane-nova",
                    $"{actor.displayName}'s shared career commands must produce the requested persisted state.",
                    failures);
            }

            var beforeRejection = JsonUtility.ToJson(state);
            var invalidPlayerRequest = new EcosystemActionRequest(HunterActionType.InvestAttribute, player.id)
            {
                progressionId = "strength",
                pointAmount = 2
            };
            var invalidAutonomousRequest = new EcosystemActionRequest(
                HunterActionType.InvestAttribute,
                autonomous.id)
            {
                progressionId = "strength",
                pointAmount = 2
            };
            var firstRejection = service.Execute(invalidPlayerRequest);
            var repeatedRejection = service.Execute(invalidPlayerRequest);
            var autonomousRejection = service.Execute(invalidAutonomousRequest);
            Check(!firstRejection.success && !repeatedRejection.success && !autonomousRejection.success &&
                  firstRejection.reasonCode == repeatedRejection.reasonCode &&
                  firstRejection.reasonCode == autonomousRejection.reasonCode &&
                  firstRejection.summary == repeatedRejection.summary &&
                  firstRejection.summary == autonomousRejection.summary,
                "Equivalent invalid player/NPC career requests must return stable rejection evidence.", failures);
            Check(JsonUtility.ToJson(state) == beforeRejection,
                "Rejected career requests must not mutate world, hunter, event, or random-cursor state.", failures);
        }

        private static void ValidateCareerLoadout(
            IReadOnlyList<EcosystemGearDefinition> gear,
            List<string> failures)
        {
            var state = EcosystemWorldFactory.CreateDefaultWorld(gear, 4205);
            var hunter = state.hunters.Find(item => item.id == state.playerHunterId);
            hunter.career.earnedAbilityPoints = 100;
            hunter.career.attributes[0].investedAbilityPoints = 6;
            hunter.career.learnedAbilities.Clear();
            hunter.career.loadout = new HunterAbilityLoadout();

            var learned = new[]
            {
                "fighter.power-strike",
                "fighter.battle-rush",
                "mage.spatial-step",
                "mage.arcane-nova",
                "fighter.relentless",
                "mage.arcane-tempo"
            }.Select(abilityId => EcosystemCareerRules.TryLearnAbility(hunter, abilityId, out _)).ToArray();
            Check(hunter.career.learnedAbilities
                    .Select(item => item.abilityId)
                    .SequenceEqual(hunter.career.learnedAbilities
                        .Select(item => item.abilityId)
                        .OrderBy(id => id, StringComparer.Ordinal)),
                "Learning abilities must preserve canonical stable-ID order before save/reload.",
                failures);
            EcosystemCareerRules.OptimizeLoadout(hunter);
            var loadout = hunter.career.loadout;
            Check(learned.All(result => result),
                "Loadout validation setup must learn three cooldowns, one ultimate, and two passives.", failures);
            Check(loadout.cooldownAbilityIds.Count == 3 &&
                  loadout.cooldownAbilityIds.All(id =>
                      !string.IsNullOrEmpty(id) &&
                      EcosystemCareerCatalog.FindAbility(id)?.kind == HunterAbilityKind.Cooldown) &&
                  !string.IsNullOrEmpty(loadout.ultimateAbilityId) &&
                  EcosystemCareerCatalog.FindAbility(loadout.ultimateAbilityId)?.kind ==
                      HunterAbilityKind.Ultimate &&
                  loadout.passiveAbilityIds.Count == 2 &&
                  loadout.passiveAbilityIds.All(id =>
                      !string.IsNullOrEmpty(id) &&
                      EcosystemCareerCatalog.FindAbility(id)?.kind == HunterAbilityKind.Passive),
                "A legal loadout must contain exactly 3 cooldown, 1 ultimate, and 2 passive abilities.", failures);
            Check(EcosystemCareerRules.Validate(hunter).Count == 0,
                "A fully occupied legal career loadout must pass domain validation.", failures);

            var loadoutSnapshot = JsonUtility.ToJson(loadout);
            var passiveInCooldown = EcosystemCareerRules.TryEquipAbility(
                hunter,
                "fighter.relentless",
                0,
                out _);
            var cooldownInUltimate = EcosystemCareerRules.TryEquipAbility(
                hunter,
                "fighter.power-strike",
                EcosystemCareerCatalog.UltimateSlotIndex,
                out _);
            var ultimateInPassive = EcosystemCareerRules.TryEquipPassive(
                hunter,
                "mage.arcane-nova",
                0,
                out _);
            Check(!passiveInCooldown && !cooldownInUltimate && !ultimateInPassive &&
                  JsonUtility.ToJson(loadout) == loadoutSnapshot,
                "Wrong-kind loadout requests must be rejected without mutating legal slots.", failures);
        }

        private static void ValidateCareerPlanningRegressions(
            IReadOnlyList<EcosystemGearDefinition> gear,
            List<string> failures)
        {
            var state = EcosystemWorldFactory.CreateDefaultWorld(gear, 4206);
            var hunter = state.hunters.Find(item =>
                item.id != state.playerHunterId && item.isAlive);
            var town = state.map.locations.Find(item => item.locationType == LocationType.Town);
            hunter.locationId = town.id;
            hunter.destinationId = "";
            hunter.travelDaysRemaining = 0;
            hunter.partyId = "";
            hunter.activeContractId = "";
            hunter.goal = "";
            hunter.ambition = 0f;
            hunter.career.plannedAbilityId = "";
            hunter.career.learnedAbilities.Clear();
            hunter.career.loadout = new HunterAbilityLoadout();
            foreach (var attribute in hunter.career.attributes)
            {
                attribute.investedAbilityPoints = 0;
            }
            hunter.career.attributes[0].investedAbilityPoints = 12;
            hunter.career.earnedAbilityPoints = 100;
            foreach (var affinity in hunter.career.affinities)
            {
                affinity.value = affinity.archetype == HunterArchetype.Mage ? 1f : 0.05f;
            }

            var abilityIds = new[]
            {
                "fighter.power-strike",
                "healer.mend-wounds",
                "tank.guardian-wall",
                "mage.spatial-step",
                "fighter.limit-break",
                "mage.arcane-nova",
                "fighter.relentless",
                "tank.iron-body",
                "mage.arcane-tempo"
            };
            var learned = abilityIds
                .Select(id => EcosystemCareerRules.TryLearnAbility(hunter, id, out _))
                .ToArray();
            Check(learned.All(result => result),
                "NPC replacement setup must learn full cooldown, ultimate, and passive alternatives.", failures);

            hunter.career.loadout = new HunterAbilityLoadout
            {
                cooldownAbilityIds = new List<string>
                {
                    "fighter.power-strike",
                    "healer.mend-wounds",
                    "tank.guardian-wall"
                },
                ultimateAbilityId = "fighter.limit-break",
                passiveAbilityIds = new List<string>
                {
                    "fighter.relentless",
                    "tank.iron-body"
                }
            };
            hunter.career.currentExperience = 0;
            hunter.career.earnedAbilityPoints = hunter.career.InvestedAbilityPoints;
            EcosystemCareerRules.Normalize(hunter, gear);

            var service = new EcosystemActionService(state, gear);
            var decisions = new EcosystemDecisionSystem(state, gear, service);
            var firstChoice = decisions.ChooseCareerAction(hunter);
            var repeatedChoice = decisions.ChooseCareerAction(hunter);
            Check(firstChoice.request != null && repeatedChoice.request != null &&
                  firstChoice.request.actionType == repeatedChoice.request.actionType &&
                  firstChoice.request.progressionId == repeatedChoice.request.progressionId &&
                  firstChoice.request.slotIndex == repeatedChoice.request.slotIndex,
                "Identical full-loadout state must produce the same deterministic NPC replacement choice.", failures);

            var replacements = new List<EcosystemActionRequest>();
            var choice = firstChoice;
            for (var iteration = 0; iteration < 8 && choice.request != null; iteration++)
            {
                var result = service.Execute(choice.request);
                Check(result.success,
                    "Every selected NPC loadout replacement must pass the shared command service.", failures);
                if (!result.success) break;
                replacements.Add(choice.request);
                choice = decisions.ChooseCareerAction(hunter);
            }

            Check(replacements.Count == 3 && choice.request == null &&
                  hunter.career.loadout.cooldownAbilityIds.Contains("mage.spatial-step") &&
                  hunter.career.loadout.ultimateAbilityId == "mage.arcane-nova" &&
                  hunter.career.loadout.passiveAbilityIds.Contains("mage.arcane-tempo"),
                "A full NPC loadout must monotonically replace weaker cooldown, ultimate, and passive fits, then stop.",
                failures);
            Check(replacements.Any(request =>
                      request.actionType == HunterActionType.EquipAbility &&
                      request.progressionId == "mage.spatial-step") &&
                  replacements.Any(request =>
                      request.actionType == HunterActionType.EquipAbility &&
                      request.progressionId == "mage.arcane-nova") &&
                  replacements.Any(request =>
                      request.actionType == HunterActionType.EquipPassive &&
                      request.progressionId == "mage.arcane-tempo"),
                "NPC career planning must express all replacement kinds through shared equip commands.", failures);

            var plan = new EcosystemActionRequest(HunterActionType.SaveAbilityPoints, hunter.id)
            {
                progressionId = "mage.grave-calling"
            };
            var firstPlan = service.Execute(plan);
            var plannedSnapshot = JsonUtility.ToJson(state);
            var repeatedPlan = service.Execute(plan);
            var repeatedPlanAgain = service.Execute(plan);
            Check(firstPlan.success && !repeatedPlan.success && !repeatedPlanAgain.success &&
                  repeatedPlan.reasonCode == repeatedPlanAgain.reasonCode &&
                  repeatedPlan.summary == repeatedPlanAgain.summary,
                "Saving for an ability must succeed once, then reject repeated identical plans stably.", failures);
            Check(JsonUtility.ToJson(state) == plannedSnapshot,
                "A repeated SaveAbilityPoints no-op must not mutate career state, events, or deterministic state.",
                failures);
        }

        private static void ValidateCareerRobustness(
            IReadOnlyList<EcosystemGearDefinition> gear,
            List<string> failures)
        {
            var combatState = EcosystemWorldFactory.CreateDefaultWorld(gear, 4207);
            var combatHunter = combatState.hunters.Find(item => item.id == combatState.playerHunterId);
            combatHunter.career.earnedAbilityPoints = 100;
            foreach (var attribute in combatHunter.career.attributes)
            {
                attribute.investedAbilityPoints = 0;
            }
            combatHunter.career.attributes[0].investedAbilityPoints = 6;
            combatHunter.career.learnedAbilities.Clear();
            combatHunter.career.loadout = new HunterAbilityLoadout();
            var setupAbilities = new[]
            {
                "fighter.power-strike",
                "fighter.battle-rush",
                "mage.aegis-barrier",
                "mage.spatial-step"
            };
            Check(setupAbilities.All(id =>
                    EcosystemCareerRules.TryLearnAbility(combatHunter, id, out _)),
                "Combat-power setup must learn three active cooldowns and one inactive replacement.",
                failures);
            var rankBeforeReplacement = EcosystemCareerRules.RankFor(combatHunter);
            var inactiveReplacementPower = EcosystemCareerRules.CombatPower(combatHunter, gear);
            var equippedReplacement = EcosystemCareerRules.TryEquipAbility(
                combatHunter,
                "mage.spatial-step",
                0,
                out _);
            var activeReplacementPower = EcosystemCareerRules.CombatPower(combatHunter, gear);
            Check(equippedReplacement && activeReplacementPower > inactiveReplacementPower &&
                  EcosystemCareerRules.RankFor(combatHunter) == rankBeforeReplacement,
                "Equipping a stronger learned ability must increase abstract combat power without changing rank rules.",
                failures);

            var corruptState = EcosystemWorldFactory.CreateDefaultWorld(gear, 4208);
            var corruptHunter = corruptState.hunters.Find(item => item.id == corruptState.playerHunterId);
            foreach (var attribute in corruptHunter.career.attributes)
            {
                attribute.investedAbilityPoints = 0;
            }
            corruptHunter.career.learnedAbilities.Clear();
            corruptHunter.career.loadout = new HunterAbilityLoadout();
            corruptHunter.career.earnedAbilityPoints = 0;
            corruptHunter.career.currentExperience = long.MaxValue;
            corruptHunter.career.lifetimeExperience = long.MaxValue;
            EcosystemCareerRules.Normalize(corruptHunter, gear);
            Check(corruptHunter.career.earnedAbilityPoints == int.MaxValue &&
                  corruptHunter.career.CareerLevel == int.MaxValue &&
                  corruptHunter.career.currentExperience >= 0 &&
                  corruptHunter.career.currentExperience <
                      EcosystemCareerRules.ExperienceThreshold(corruptHunter) &&
                  EcosystemCareerRules.Validate(corruptHunter).Count == 0,
                "Extreme imported XP must normalize in bounded work to a valid saturated career.",
                failures);
        }

        private static void ValidateSharedCommands(
            IReadOnlyList<EcosystemGearDefinition> gear,
            List<string> failures)
        {
            var state = EcosystemWorldFactory.CreateDefaultWorld(gear, 7);
            var service = new EcosystemActionService(state, gear);
            var player = state.hunters.Find(hunter => hunter.id == state.playerHunterId);
            var autonomous = state.hunters.Find(hunter => hunter.id != state.playerHunterId && hunter.isAlive);
            RemoveFromGuild(state, player);
            RemoveFromGuild(state, autonomous);

            var playerJoin = service.Execute(new EcosystemActionRequest(HunterActionType.JoinGuild, player.id)
            {
                guildId = state.guilds[0].id
            });
            var autonomousJoin = service.Execute(new EcosystemActionRequest(HunterActionType.JoinGuild, autonomous.id)
            {
                guildId = state.guilds[1].id
            });
            Check(playerJoin.success && autonomousJoin.success,
                "Player and AI hunters must both execute JoinGuild through the same service.", failures);
            Check(state.guilds[0].memberIds.Count(id => id == player.id) == 1 &&
                  state.guilds[1].memberIds.Count(id => id == autonomous.id) == 1,
                "Guild membership must not duplicate IDs.", failures);

            var firstGear = gear[0].GearId;
            if (!player.inventoryGearIds.Contains(firstGear)) player.inventoryGearIds.Add(firstGear);
            if (!autonomous.inventoryGearIds.Contains(firstGear)) autonomous.inventoryGearIds.Add(firstGear);
            var playerEquip = service.Execute(new EcosystemActionRequest(HunterActionType.EquipGear, player.id)
            {
                gearId = firstGear
            });
            var autonomousEquip = service.Execute(new EcosystemActionRequest(HunterActionType.EquipGear, autonomous.id)
            {
                gearId = firstGear
            });
            Check(playerEquip.success && autonomousEquip.success,
                "Player and AI hunters must both execute EquipGear through the same service.", failures);

            player.pendingRewardGold = 12;
            var firstClaim = service.Execute(new EcosystemActionRequest(HunterActionType.ClaimReward, player.id));
            var secondClaim = service.Execute(new EcosystemActionRequest(HunterActionType.ClaimReward, player.id));
            Check(firstClaim.success && !secondClaim.success,
                "Reward claiming must be idempotent.", failures);
            Check(firstClaim.reasonCode == "ok" && !string.IsNullOrEmpty(secondClaim.reasonCode),
                "Every shared command result must provide a stable reason code.", failures);
        }

        private static void ValidatePartyLifecycle(
            IReadOnlyList<EcosystemGearDefinition> gear,
            List<string> failures)
        {
            var state = EcosystemWorldFactory.CreateDefaultWorld(gear, 71);
            var service = new EcosystemActionService(state, gear);
            var leader = state.hunters.Find(hunter => hunter.id == state.playerHunterId);
            var member = state.hunters.Find(hunter => hunter.id != leader.id && hunter.isAlive &&
                string.IsNullOrEmpty(hunter.partyId) && hunter.locationId == leader.locationId);
            var form = service.Execute(new EcosystemActionRequest(HunterActionType.FormParty, leader.id));
            var party = service.FindParty(leader.partyId);
            var join = member == null
                ? EcosystemActionResult.Failed("No same-location party member was seeded.")
                : service.Execute(new EcosystemActionRequest(HunterActionType.JoinParty, member.id)
                {
                    partyId = party?.id
                });
            var leave = member == null
                ? EcosystemActionResult.Failed("No same-location party member was seeded.")
                : service.Execute(new EcosystemActionRequest(HunterActionType.LeaveParty, member.id));
            var disband = service.Execute(new EcosystemActionRequest(HunterActionType.DisbandParty, leader.id));

            Check(form.success && join.success && leave.success && disband.success,
                "Hunters must be able to form, join, leave, and disband through shared commands.", failures);
            Check(string.IsNullOrEmpty(leader.partyId) &&
                  (member == null || string.IsNullOrEmpty(member.partyId)) &&
                  party?.status == PartyStatus.Disbanded,
                "Party cleanup must remove reciprocal hunter references.", failures);
            failures.AddRange(EcosystemWorldFactory.ValidateInvariants(state, gear));
        }

        private static void ValidateTravelLifecycle(
            IReadOnlyList<EcosystemGearDefinition> gear,
            List<string> failures)
        {
            var state = EcosystemWorldFactory.CreateDefaultWorld(gear, 72);
            var service = new EcosystemActionService(state, gear);
            var traveler = state.hunters.Find(hunter => hunter.id == state.playerHunterId);
            var originId = traveler.locationId;
            var destination = service.FindNextTravelLocation(originId, "town-river");
            var routeTravel = service.Execute(new EcosystemActionRequest(HunterActionType.Travel, traveler.id)
            {
                locationId = destination?.id
            });
            var initialRemaining = traveler.travelDaysRemaining;
            Check(routeTravel.success && traveler.locationId == originId && initialRemaining > 0,
                "Travel must persist an in-progress journey instead of teleporting.", failures);
            for (var day = 0; day < initialRemaining; day++)
            {
                service.AdvanceTravelOneDay();
            }
            Check(destination != null && traveler.locationId == destination.id &&
                  traveler.travelDaysRemaining == 0 && string.IsNullOrEmpty(traveler.destinationId),
                "Travel must update location exactly when its route duration completes.", failures);
        }

        private static void ValidateDecisionInspection(
            IReadOnlyList<EcosystemGearDefinition> gear,
            List<string> failures)
        {
            var state = EcosystemWorldFactory.CreateDefaultWorld(gear, 99);
            var service = new EcosystemActionService(state, gear);
            var decisions = new EcosystemDecisionSystem(state, gear, service);
            var hunter = state.hunters.Find(item => item.id != state.playerHunterId && item.isAlive);
            var relationshipCount = hunter.relationships.Count;
            var choice = decisions.ChooseAction(hunter);
            Check(choice?.selected != null && choice.selected.factors.Count > 0,
                "Autonomous choices need named utility factors.", failures);
            if (choice?.selected != null)
            {
                var sum = choice.selected.factors.Sum(factor => factor.contribution);
                Check(Mathf.Abs(sum - choice.selected.totalScore) < 0.0001f,
                    "Decision total must equal the sum of factor contributions.", failures);
                Check(choice.alternatives.Where(option => option.executable)
                        .All(option => option.totalScore <= choice.selected.totalScore + 0.0001f),
                    "Selected decision must be the highest-scoring valid option.", failures);
                Check(choice.selected.executable && !string.IsNullOrEmpty(choice.selected.decisionId) &&
                      !string.IsNullOrEmpty(choice.selected.category) &&
                      !string.IsNullOrEmpty(choice.selected.finalExplanation) &&
                      !string.IsNullOrEmpty(choice.selected.tieBreakExplanation),
                    "Selected decisions need stable identity, category, explanation, and tie-break evidence.", failures);
                Check(choice.alternatives.Any(option => !option.executable &&
                                                       !string.IsNullOrEmpty(option.rejectionReason)),
                    "Decision inspection must retain at least one rejected proposal and reason.", failures);
            }
            Check(hunter.relationships.Count == relationshipCount,
                "Evaluating decisions must not create relationships or mutate state.", failures);

            var simulation = new EcosystemSimulation(state, gear);
            var repeatedRequest = new EcosystemActionRequest(HunterActionType.Wait, state.playerHunterId);
            simulation.ExecutePlayerAction(repeatedRequest);
            simulation.ExecutePlayerAction(repeatedRequest);
            var repeatedRecords = state.decisionRecords.TakeLast(2).ToArray();
            Check(repeatedRecords.Length == 2 &&
                  repeatedRecords[0].decisionId != repeatedRecords[1].decisionId &&
                  repeatedRecords[0].sequence < repeatedRecords[1].sequence,
                "Repeated same-day decisions must receive unique persisted IDs and monotonic sequences.", failures);
        }

        private static void ValidateContractLifecycle(
            IReadOnlyList<EcosystemGearDefinition> gear,
            List<string> failures)
        {
            var state = EcosystemWorldFactory.CreateDefaultWorld(gear, 1337);
            var service = new EcosystemActionService(state, gear);
            var player = state.hunters.Find(hunter => hunter.id == state.playerHunterId);
            var contract = state.contracts.Find(item => item.status == ContractStatus.Offered);
            var accept = service.Execute(new EcosystemActionRequest(HunterActionType.AcceptContract, player.id)
            {
                contractId = contract.id
            });
            Check(accept.success && !string.IsNullOrEmpty(player.partyId) &&
                  contract.status == ContractStatus.Accepted,
                "Accepting a contract must create/assign a persistent party.", failures);

            var targetLocationId = string.IsNullOrEmpty(contract.targetLocationId)
                ? contract.locationId
                : contract.targetLocationId;
            var travelSucceeded = true;
            var party = service.FindParty(player.partyId);
            while (player.locationId != targetLocationId && travelSucceeded)
            {
                var next = service.FindNextTravelLocation(player.locationId, targetLocationId);
                var travel = service.Execute(new EcosystemActionRequest(HunterActionType.Travel, player.id)
                {
                    locationId = next?.id
                });
                travelSucceeded = travel.success;
                while (party != null && party.travelDaysRemaining > 0)
                {
                    state.day++;
                    service.AdvanceTravelOneDay();
                }
            }
            var enter = service.Execute(new EcosystemActionRequest(HunterActionType.EnterDungeon, player.id)
            {
                contractId = contract.id
            });
            Check(travelSucceeded && enter.success && contract.status == ContractStatus.Active,
                "A party must travel before entering its dungeon.", failures);
            var encounter = state.encounters.Find(item =>
                item != null && item.id == contract.activeEncounterId);
            var gate = state.gates.Find(item => item != null && item.id == contract.gateId);
            Check(encounter != null && gate != null &&
                  encounter.status == DungeonEncounterStatus.Active &&
                  encounter.gateId == gate.id && encounter.contractId == contract.id,
                "Dungeon entry must link the contract, persisted gate, and canonical encounter snapshot.",
                failures);
            if (encounter != null && gate != null)
            {
                Check(encounter.areas.Count == gate.areas.Count &&
                      encounter.connections.Count == gate.connections.Count &&
                      encounter.participants.Any(participant => participant != null &&
                          participant.participantKind == EncounterParticipantKind.Hunter) &&
                      encounter.participants.Any(participant => participant != null &&
                          participant.participantKind == EncounterParticipantKind.Monster),
                    "A canonical encounter must materialize the same persisted layout with hunters and mobs.",
                    failures);

                var firstContinuation = Clone(state, gear);
                var repeatedContinuation = Clone(state, gear);
                var firstEncounter = firstContinuation.encounters.Find(item =>
                    item != null && item.id == encounter.id);
                var repeatedEncounter = repeatedContinuation.encounters.Find(item =>
                    item != null && item.id == encounter.id);
                new EcosystemEncounterSimulation(firstContinuation, gear)
                    .AdvanceEncounter(firstEncounter, 12);
                new EcosystemEncounterSimulation(repeatedContinuation, gear)
                    .AdvanceEncounter(repeatedEncounter, 12);
                Check(JsonUtility.ToJson(firstEncounter) == JsonUtility.ToJson(repeatedEncounter),
                    "Rendered and offscreen combat must share deterministic fixed-step encounter outcomes.",
                    failures);
            }
            service.ResolveActiveContracts();
            Check(contract.status == ContractStatus.Active,
                "A newly entered dungeon must leave one decision window for retreat.", failures);
            state.day++;
            var fixedStepSimulation = new EcosystemSimulation(state, gear);
            for (var dayWindow = 0;
                 dayWindow < 12 && contract.status == ContractStatus.Active;
                 dayWindow++)
            {
                fixedStepSimulation.AdvanceEncounterSteps(
                    EcosystemSimulation.EncounterFixedStepsPerCampaignDay);
                if (contract.status == ContractStatus.Active)
                {
                    state.day++;
                }
            }
            Check(contract.status is ContractStatus.Succeeded or ContractStatus.Failed,
                "An offscreen dungeon must eventually resolve through the same fixed-step encounter rules.",
                failures);
        }

        private static void ValidateMigration(
            IReadOnlyList<EcosystemGearDefinition> gear,
            List<string> failures)
        {
            var versionTwo = EcosystemWorldFactory.CreateDefaultWorld(gear, 8128);
            versionTwo.saveVersion = 2;
            versionTwo.day = 23;
            versionTwo.simulationSequence = 987654;
            var established = versionTwo.hunters.Find(hunter =>
                hunter.id != versionTwo.playerHunterId && hunter.isAlive);
            established.career.earnedAbilityPoints += 9;
            established.career.currentExperience = 7;
            established.career.lifetimeExperience += 7;
            established.career.plannedAbilityId = "mage.grave-calling";
            var establishedCareerSnapshot = JsonUtility.ToJson(established.career);
            var fallen = versionTwo.hunters.Find(hunter => hunter.id == versionTwo.playerHunterId);
            fallen.career = null;
            fallen.isAlive = false;
            fallen.deathDay = 18;
            fallen.deathCause = "Legacy gate collapse";
            fallen.vitals = new HunterVitalsState
            {
                initialized = true,
                maximumHealth = 135,
                currentHealth = 0,
                maximumMana = 120,
                currentMana = 0,
                maximumShield = 80,
                currentShield = 0
            };
            fallen.injuries = new List<HunterInjury>
            {
                new()
                {
                    id = "legacy-fatal-injury",
                    displayName = "Crushed torso",
                    severity = InjurySeverity.Critical,
                    sufferedDay = 18,
                    recoveryDay = 99,
                    sourceEventId = "legacy-collapse-event",
                    healed = false
                }
            };
            var preservedHunterId = fallen.id;
            var preservedHunterName = fallen.displayName;
            EcosystemWorldFactory.UpgradeAndNormalize(versionTwo, gear, 8128);
            Check(versionTwo.saveVersion == EcosystemWorldFactory.CurrentSaveVersion &&
                  versionTwo.day == 23 && versionTwo.simulationSequence == 987654,
                "Version-two career migration must preserve the shared clock and deterministic cursor.", failures);
            Check(JsonUtility.ToJson(established.career) == establishedCareerSnapshot,
                "Version-two migration must normalize, not regenerate, an already initialized career.",
                failures);
            Check(fallen.id == preservedHunterId && fallen.displayName == preservedHunterName &&
                  !fallen.isAlive && fallen.deathDay == 18 &&
                  fallen.deathCause == "Legacy gate collapse",
                "Version-two career migration must never resurrect or replace a dead hunter.", failures);
            Check(fallen.vitals.initialized && fallen.vitals.maximumHealth == 135 &&
                  fallen.vitals.maximumMana == 120 && fallen.vitals.maximumShield == 80 &&
                  fallen.vitals.currentHealth == 0 && fallen.vitals.currentMana == 0 &&
                  fallen.vitals.currentShield == 0,
                "Version-two career migration must preserve a dead hunter's depleted vitals.", failures);
            Check(fallen.injuries.Count == 1 && fallen.injuries[0].id == "legacy-fatal-injury" &&
                  fallen.injuries[0].severity == InjurySeverity.Critical &&
                  !fallen.injuries[0].healed && fallen.career?.initialized == true,
                "Version-two career migration must preserve injuries while initializing career data.", failures);
            var firstMigrationSnapshot = JsonUtility.ToJson(versionTwo);
            EcosystemWorldFactory.UpgradeAndNormalize(versionTwo, gear, 8128);
            Check(JsonUtility.ToJson(versionTwo) == firstMigrationSnapshot,
                "Repeating legacy-to-current normalization must be idempotent.", failures);

            var legacy = new EcosystemWorldState
            {
                saveVersion = 1,
                day = 9,
                playerHunterId = "legacy-player",
                guilds = new List<GuildState>
                {
                    new()
                    {
                        id = "legacy-guild",
                        displayName = "Legacy Guild",
                        memberIds = new List<string> { null, "legacy-player", string.Empty }
                    }
                },
                hunters = new List<HunterProfile>
                {
                    new()
                    {
                        id = "legacy-player",
                        displayName = "Legacy Hunter",
                        level = 5,
                        guildId = "legacy-guild",
                        goal = "Preserve this identity"
                    }
                }
            };
            EcosystemWorldFactory.UpgradeAndNormalize(legacy, gear, 123);
            Check(legacy.saveVersion == EcosystemWorldFactory.CurrentSaveVersion && legacy.day == 9,
                "Version-one saves must migrate directly to the current format without resetting elapsed days.", failures);
            Check(legacy.hunters.Any(hunter => hunter.id == "legacy-player" &&
                                                hunter.displayName == "Legacy Hunter" &&
                                                hunter.level == 5 &&
                                                hunter.goal == "Preserve this identity" &&
                                                hunter.career != null && hunter.career.initialized),
                "Legacy migration must preserve hunter identity and progression.", failures);
            Check(legacy.guilds.Any(guild => guild.id == "legacy-guild" &&
                                            guild.memberIds.SequenceEqual(new[] { "legacy-player" })),
                "Migration must discard null roster IDs while preserving valid legacy membership.", failures);
            Check(legacy.hunters.Count >= EcosystemWorldFactory.StartingHunterCount &&
                  legacy.guilds.Count >= EcosystemWorldFactory.RequiredGuildCount,
                "Migration must add the serious slice's required ecosystem content.", failures);
            failures.AddRange(EcosystemWorldFactory.ValidateInvariants(legacy, gear));
        }

        private static void ValidateDeterministicContinuation(
            IReadOnlyList<EcosystemGearDefinition> gear,
            List<string> failures)
        {
            var uninterrupted = EcosystemWorldFactory.CreateDefaultWorld(gear, 8675309);
            var firstLeg = Clone(uninterrupted, gear);
            new EcosystemSimulation(uninterrupted, gear).AdvanceDays(12);

            new EcosystemSimulation(firstLeg, gear).AdvanceDays(6);
            var reloaded = Clone(firstLeg, gear);
            new EcosystemSimulation(reloaded, gear).AdvanceDays(6);

            var uninterruptedJson = JsonUtility.ToJson(uninterrupted);
            var reloadedJson = JsonUtility.ToJson(reloaded);
            Check(uninterruptedJson == reloadedJson,
                "A save/reload continuation must match an uninterrupted deterministic simulation. " +
                DescribeFirstDifference(uninterruptedJson, reloadedJson), failures);
        }

        private static void ValidatePopulationLifecycle(
            IReadOnlyList<EcosystemGearDefinition> gear,
            List<string> failures)
        {
            var state = EcosystemWorldFactory.CreateDefaultWorld(gear, 99017);
            new EcosystemSimulation(state, gear).AdvanceDays(35);
            var awakened = state.hunters.Where(hunter => hunter != null && hunter.awakeningDay > 1).ToArray();
            var retired = state.hunters.Where(hunter => hunter != null && hunter.isRetired).ToArray();
            var active = state.hunters.Count(hunter => hunter != null && hunter.IsActive);

            Check(awakened.Length >= 35 && state.populationSequence >= 35,
                "At least one persistent hunter must awaken on every simulated day.", failures);
            Check(retired.Length > 0,
                "Retirement must begin balancing daily awakenings when the active cap is reached.", failures);
            Check(active is >= EcosystemWorldFactory.MinimumActiveHunterCount and
                <= EcosystemWorldFactory.MaximumActiveHunterCount,
                "Population churn must remain inside the configured 60-100 active range.", failures);
            Check(awakened.Select(hunter => hunter.id).Distinct(StringComparer.Ordinal).Count() == awakened.Length &&
                  state.hunters.Select(hunter => hunter.displayName).Distinct(StringComparer.Ordinal).Count() ==
                  state.hunters.Count,
                "Awakenings must never reuse a persistent ID or screen identity.", failures);
            Check(retired.All(hunter => string.IsNullOrEmpty(hunter.guildId) &&
                                        string.IsNullOrEmpty(hunter.partyId) &&
                                        string.IsNullOrEmpty(hunter.activeContractId)),
                "Retired hunters must leave active guild, party, and contract ownership.", failures);
            Check(state.structuredEvents.Any(item => item.eventType == WorldEventType.HunterAwakened) &&
                  state.structuredEvents.Any(item => item.eventType == WorldEventType.HunterRetired),
                "Population entry and exit must remain visible in structured world history.", failures);
            failures.AddRange(EcosystemWorldFactory.ValidateInvariants(state, gear));
        }

        private static void ValidatePersistenceRoundTrip(
            IReadOnlyList<EcosystemGearDefinition> gear,
            List<string> failures)
        {
            const string path = "Temp/CodexValidation/2d-ecosystem-roundtrip-v5.json";
            var relatedPaths = new[] { path, path + ".tmp", path + ".bak" };
            try
            {
                foreach (var relatedPath in relatedPaths)
                {
                    if (File.Exists(relatedPath)) File.Delete(relatedPath);
                }

                var source = EcosystemWorldFactory.CreateDefaultWorld(gear, 5150);
                new EcosystemSimulation(source, gear).AdvanceDays(5);
                var player = source.hunters.Find(hunter => hunter.id == source.playerHunterId);
                var buyer = source.hunters.Find(hunter => hunter.id != source.playerHunterId &&
                                                         hunter.isAlive &&
                                                         string.IsNullOrEmpty(hunter.partyId) &&
                                                         string.IsNullOrEmpty(hunter.activeContractId));
                var soldGearId = player?.inventoryGearIds.Find(gearId =>
                    gearId != player.equippedGearId &&
                    buyer != null && !buyer.inventoryGearIds.Contains(gearId));
                if (player != null && buyer != null && !string.IsNullOrEmpty(soldGearId))
                {
                    buyer.locationId = player.locationId;
                    buyer.destinationId = string.Empty;
                    buyer.travelDaysRemaining = 0;
                    buyer.gold = Mathf.Max(buyer.gold, 9999);
                    var trade = new EcosystemActionService(source, gear).Execute(
                        new EcosystemActionRequest(HunterActionType.TradeGear, player.id)
                        {
                            targetHunterId = buyer.id,
                            gearId = soldGearId
                        });
                    Check(trade.success, "Persistence regression setup must complete a real gear trade.", failures);
                }
                else
                {
                    failures.Add("Persistence regression setup could not find tradable player gear and a buyer.");
                }
                var repository = new EcosystemSaveRepository(gear, path);
                var saved = repository.Save(source, out var saveError);
                var loaded = repository.LoadOrCreate(out _);
                Check(saved && string.IsNullOrEmpty(saveError),
                    $"Validated v5 persistence write failed: {saveError}", failures);
                Check(JsonUtility.ToJson(source) == JsonUtility.ToJson(loaded),
                    "Validated v5 save/load must preserve the complete deterministic snapshot.", failures);
                if (player != null && buyer != null && !string.IsNullOrEmpty(soldGearId))
                {
                    var loadedPlayer = loaded.hunters.Find(hunter => hunter.id == player.id);
                    var loadedBuyer = loaded.hunters.Find(hunter => hunter.id == buyer.id);
                    Check(loadedPlayer != null && !loadedPlayer.inventoryGearIds.Contains(soldGearId) &&
                          loadedBuyer != null && loadedBuyer.inventoryGearIds.Contains(soldGearId),
                        "Saving and loading must not resurrect sold gear or duplicate it across hunters.", failures);
                }
            }
            catch (Exception exception)
            {
                failures.Add($"Validated v5 persistence round trip failed: {exception.Message}");
            }
            finally
            {
                foreach (var relatedPath in relatedPaths)
                {
                    if (File.Exists(relatedPath)) File.Delete(relatedPath);
                }
            }
        }

        private static void ValidateMultiWeekInvariants(
            IReadOnlyList<EcosystemGearDefinition> gear,
            List<string> failures)
        {
            foreach (var seed in new[] { 1, 42 })
            {
                var state = EcosystemWorldFactory.CreateDefaultWorld(gear, seed);
                new EcosystemSimulation(state, gear).AdvanceDays(28);
                foreach (var error in EcosystemWorldFactory.ValidateInvariants(state, gear))
                {
                    failures.Add($"Seed {seed}, day {state.day}: {error}");
                }
            }
        }

        private static void Validate2DSceneWiring(List<string> failures)
        {
            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(Ecosystem2DSceneBuilder.ScenePath);
            Check(sceneAsset != null, "The authored 2D Ecosystem scene asset is missing.", failures);
            if (sceneAsset == null)
            {
                return;
            }

            var scene = SceneManager.GetSceneByPath(Ecosystem2DSceneBuilder.ScenePath);
            var openedTemporarily = !scene.IsValid() || !scene.isLoaded;
            if (openedTemporarily)
            {
                scene = EditorSceneManager.OpenScene(Ecosystem2DSceneBuilder.ScenePath, OpenSceneMode.Additive);
            }
            try
            {
                var roots = scene.GetRootGameObjects();
                var host = roots.SelectMany(root => root.GetComponentsInChildren<EcosystemWorldController>(true))
                    .FirstOrDefault();
                var legacyView = roots.SelectMany(root => root.GetComponentsInChildren<EcosystemStrategyView>(true))
                    .FirstOrDefault();
                var authoring = roots.SelectMany(root => root.GetComponentsInChildren<EcosystemSpatialAuthoring>(true))
                    .FirstOrDefault();
                var spatialView = roots.SelectMany(root => root.GetComponentsInChildren<EcosystemSpatialWorldView>(true))
                    .FirstOrDefault();
                var spatialHud = roots.SelectMany(root => root.GetComponentsInChildren<EcosystemSpatialHud>(true))
                    .FirstOrDefault();
                var playerInput = roots.SelectMany(root => root.GetComponentsInChildren<EcosystemPlayerInput2D>(true))
                    .FirstOrDefault();
                var dungeonView = roots.SelectMany(root => root.GetComponentsInChildren<EcosystemDungeonWorldView>(true))
                    .FirstOrDefault();
                var pawns = roots.SelectMany(root => root.GetComponentsInChildren<EcosystemHunterPawn2D>(true))
                    .ToArray();
                var authoredRoot = roots.FirstOrDefault(root =>
                    root.name == EcosystemSpatialSceneAuthoringBuilder.AuthoredWorldName);
                var dungeonStage = authoredRoot == null
                    ? null
                    : authoredRoot.GetComponentsInChildren<Transform>(true).FirstOrDefault(item =>
                        item.name == EcosystemSpatialSceneAuthoringBuilder.DungeonStageName);
                var camera = roots.SelectMany(root => root.GetComponentsInChildren<Camera>(true))
                    .FirstOrDefault(item => item.CompareTag("MainCamera"));
                var mapCamera = camera == null
                    ? null
                    : camera.GetComponent<EcosystemMapCameraController>();
                var spriteRenderers = authoredRoot == null
                    ? Array.Empty<SpriteRenderer>()
                    : authoredRoot.GetComponentsInChildren<SpriteRenderer>(true);

                Check(host != null && authoring != null && spatialView != null && spatialHud != null &&
                      playerInput != null && dungeonView != null && mapCamera != null,
                    "2D Ecosystem must serialize its controller, spatial view, HUD, input, camera, and dungeon materializer.",
                    failures);
                Check(host != null && host.GearCatalog.Count == 3,
                    "2D Ecosystem world controller must serialize the ecosystem gear catalog.", failures);
                Check(camera != null && camera.orthographic,
                    "2D Ecosystem must use a serialized orthographic Main Camera.", failures);
                Check(legacyView == null || !legacyView.enabled,
                    "The legacy full-screen text strategy view must remain disabled.", failures);
                Check(authoredRoot != null && authoredRoot.activeSelf && dungeonStage != null &&
                      !dungeonStage.gameObject.activeSelf,
                    "The overworld must be visible in Edit Mode while the materialized dungeon stage starts inactive.",
                    failures);
                Check(authoring != null && authoring.DynamicActorRoot != null &&
                      authoring.Locations.Count >= 15 && authoring.Routes.Count >= 8,
                    "The authored map must retain persistent locations, roads, and its dynamic pawn root.",
                    failures);
                Check(pawns.Length == EcosystemSpatialSceneAuthoringBuilder.PawnSlotCount &&
                      pawns.All(pawn => pawn != null && pawn.AuthoredCircleSprite != null &&
                          pawn.AuthoredSquareSprite != null),
                    "The scene must serialize one hundred reusable hunter pawns with authored circle/square sprites.",
                    failures);
                Check(spriteRenderers.Length > 0 && spriteRenderers.All(renderer =>
                          renderer != null && renderer.sprite != null),
                    "Every authored overworld SpriteRenderer must reference a persistent sprite asset.",
                    failures);
                Check(host != null && host.SpatialWorldView == spatialView &&
                      host.DungeonWorldView == dungeonView,
                    "The world controller must reference the serialized overworld and dungeon presentations.",
                    failures);
            }
            finally
            {
                if (openedTemporarily)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static EcosystemWorldState Clone(
            EcosystemWorldState source,
            IReadOnlyList<EcosystemGearDefinition> gear)
        {
            var clone = JsonUtility.FromJson<EcosystemWorldState>(JsonUtility.ToJson(source));
            return EcosystemWorldFactory.UpgradeAndNormalize(clone, gear, source.worldSeed);
        }

        private static void RemoveFromGuild(EcosystemWorldState state, HunterProfile hunter)
        {
            foreach (var guild in state.guilds)
            {
                guild.memberIds.RemoveAll(id => id == hunter.id);
            }
            hunter.guildId = string.Empty;
        }

        private static void Check(bool condition, string message, List<string> failures)
        {
            if (!condition)
            {
                failures.Add(message);
            }
        }

        private static string DescribeFirstDifference(string left, string right)
        {
            var length = Math.Min(left?.Length ?? 0, right?.Length ?? 0);
            var index = 0;
            while (index < length && left[index] == right[index]) index++;
            if (index == length && (left?.Length ?? 0) == (right?.Length ?? 0))
            {
                return "Snapshots are equal.";
            }

            var start = Math.Max(0, index - 90);
            var leftLength = Math.Min(180, Math.Max(0, (left?.Length ?? 0) - start));
            var rightLength = Math.Min(180, Math.Max(0, (right?.Length ?? 0) - start));
            var leftExcerpt = leftLength > 0 ? left.Substring(start, leftLength) : "<end>";
            var rightExcerpt = rightLength > 0 ? right.Substring(start, rightLength) : "<end>";
            return $"First difference at character {index}. uninterrupted='{leftExcerpt}' reloaded='{rightExcerpt}'";
        }
    }
}
