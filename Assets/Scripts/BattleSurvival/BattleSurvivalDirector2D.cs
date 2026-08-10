using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using Turtle.DungeonRaid;

namespace Turtle.BattleSurvival
{
    public enum BattleSurvivalPhase
    {
        Prewarming,
        Preparing,
        Wave,
        Intermission,
        Defeat
    }

    public enum BattleSquadOrder
    {
        FormUp,
        DefendWest,
        DefendEast,
        DefendNorth,
        DefendSouth,
        Rescue,
        Regroup
    }

    /// <summary>
    /// Three-squad, round-based survival scenario. It owns wave progression and
    /// hierarchical AI while RaidAgent2D and DungeonRaidDirector2D remain the
    /// shared combat, hurtbox, ability, status, and VFX authority.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleSurvivalDirector2D : MonoBehaviour
    {
        private sealed class SquadState
        {
            public readonly List<BattleSurvivalUnit2D> Members =
                new(BattleSurvivalHunterCatalog.HuntersPerSquad);
            public string Name;
            public int Index;
            public Vector2 HomeAnchor;
            public Vector2 CommandAnchor;
            public BattleSquadOrder Order;
            public BattleSurvivalUnit2D Sergeant;
            public RaidAgent2D FocusTarget;
            public int Promotions;
        }

        private enum MonsterArchetype
        {
            Ravager,
            Brute,
            Spitter,
            Hexer,
            Charger,
            Elite
        }

        private static readonly string[] PortalNames =
            { "West", "East", "North", "South" };

        [Header("Authored Scene References")]
        [SerializeField] private RaidAgent2D[] hunters = Array.Empty<RaidAgent2D>();
        [SerializeField] private GameObject monsterTemplate;
        [SerializeField] private Transform monsterPoolRoot;
        [SerializeField] private DungeonRaidDirector2D combatResolver;
        [SerializeField] private RaidFxPool2D effects;
        [SerializeField] private Camera battleCamera;

        [Header("Arena and Horde")]
        [SerializeField] private Vector2 arenaSize = new(120f, 72f);
        [SerializeField, Range(100, 800)] private int maximumConcurrentMonsters = 600;
        [SerializeField, Range(4, 64)] private int poolCreationsPerFrame = 18;
        [SerializeField, Min(0.02f)] private float spawnInterval = 0.12f;
        [SerializeField, Min(0f)] private float preparationSeconds = 2.5f;
        [SerializeField, Min(0f)] private float intermissionSeconds = 5f;

        [Header("Round Scaling")]
        [SerializeField, Min(1)] private int baseEnemiesPerRound = 24;
        [SerializeField, Min(0)] private int linearEnemiesPerRound = 11;
        [SerializeField, Min(0)] private int quadraticEnemiesPerRound = 2;
        [SerializeField, Min(1f)] private float healthGrowthPerRound = 1.17f;
        [SerializeField, Min(1f)] private float damageGrowthPerRound = 1.075f;
        [SerializeField, Min(0f)] private float speedGrowthPerRound = 0.009f;
        [SerializeField, Min(1f)] private float maximumSpeedMultiplier = 1.28f;

        [Header("AI Budgets")]
        [SerializeField, Min(0.1f)] private float commanderDecisionInterval = 0.85f;
        [SerializeField, Range(16, 512)] private int maximumMonsterDecisionsPerFrame = 160;
        [SerializeField, Min(0.05f)] private float hunterDecisionInterval = 0.13f;
        [SerializeField, Min(0.05f)] private float monsterDecisionInterval = 0.18f;

        private readonly List<BattleSurvivalUnit2D> hunterUnits =
            new(BattleSurvivalHunterCatalog.HunterCount);
        private readonly List<BattleSurvivalUnit2D> allMonsterUnits = new(600);
        private readonly List<BattleSurvivalUnit2D> activeMonsters = new(600);
        private readonly Queue<BattleSurvivalUnit2D> availableMonsters = new(600);
        private readonly Dictionary<RaidAgent2D, BattleSurvivalUnit2D> unitByAgent = new(640);
        private readonly List<RaidAgent2D> allMonsterAgents = new(600);
        private readonly SquadState[] squads = { new(), new(), new() };
        private readonly Vector2[] portalPositions = new Vector2[4];
        private readonly float[] portalPressure = new float[4];
        private readonly int[] sortedPortals = { 0, 1, 2, 3 };
        private readonly int[] sortedSquads = { 0, 1, 2 };
        private readonly List<RaidStatusEffectSnapshot> statusScratch = new(8);
        private readonly List<RaidAbilitySpec>[] monsterAbilitySets = new List<RaidAbilitySpec>[6];

        private BattleSurvivalPhase phase = BattleSurvivalPhase.Prewarming;
        private BattleSurvivalUnit2D selectedHunter;
        private float combatTime;
        private float phaseCountdown;
        private float spawnAccumulator;
        private float commanderAccumulator;
        private int monsterDecisionCursor;
        private int currentRound;
        private int roundEnemyTarget;
        private int spawnedThisRound;
        private int bonusEnemiesThisRound;
        private int bonusConcurrentThisRound;
        private int stressBurstRemaining;
        private int totalMonstersDefeated;
        private int peakConcurrentMonsters;
        private int peakTotalCombatants;
        private long commanderDecisions;
        private long basicAttackAttempts;
        private long confirmedBasicHits;
        private long abilityCasts;
        private long areaAbilityCasts;
        private int activeStatusEffects;
        private string latestEvent = "Preparing the arena pool.";

        private float sampleElapsed;
        private int sampleFrames;
        private int decisionsThisSample;
        private int attacksThisSample;
        private int hitsThisSample;
        private int abilitiesThisSample;
        private int displayedDecisionsPerSecond;
        private int displayedAttacksPerSecond;
        private int displayedHitsPerSecond;
        private int displayedAbilitiesPerSecond;
        private float displayedFps;
        private float displayedFrameMilliseconds;
        private float roundLowestFps;
        private float roundHighestFrameMilliseconds;

        private GUIStyle panelStyle;
        private GUIStyle titleStyle;
        private GUIStyle labelStyle;
        private GUIStyle smallStyle;
        private GUIStyle deadStyle;
        private float guiWidth;
        private float guiHeight;

        public bool IsConfigured => hunters is { Length: BattleSurvivalHunterCatalog.HunterCount } &&
                                    monsterTemplate != null && monsterPoolRoot != null &&
                                    combatResolver != null && effects != null && battleCamera != null;
        public BattleSurvivalPhase Phase => phase;
        public int CurrentRound => currentRound;
        public int HunterCount => hunterUnits.Count;
        public int LivingHunterCount => CountLivingHunters();
        public int ActiveMonsterCount => activeMonsters.Count;
        public int PeakConcurrentMonsterCount => peakConcurrentMonsters;
        public int RoundEnemyTarget => roundEnemyTarget;
        public int SpawnedThisRound => spawnedThisRound;
        public int ActiveSergeantCount => CountActiveSergeants();
        public long CommanderDecisions => commanderDecisions;
        public long BasicAttackAttempts => basicAttackAttempts;
        public long ConfirmedBasicHits => confirmedBasicHits;
        public long AbilityCasts => abilityCasts;
        public long AreaAbilityCasts => areaAbilityCasts;
        public int ActiveStatusEffects => activeStatusEffects;
        public float CombatTime => combatTime;

        private void Start()
        {
            if (!IsConfigured)
            {
                enabled = false;
                Debug.LogError("Battle Survival Director is missing authored scene references.", this);
                return;
            }
            monsterTemplate.SetActive(false);
            ConfigurePortalPositions();
            BuildMonsterAbilitySets();
            ConfigureAuthoredHunters();
            phase = BattleSurvivalPhase.Prewarming;
            latestEvent = $"Prewarming a {maximumConcurrentMonsters}-monster pool without wave-time churn.";
        }

        private void OnDestroy()
        {
            if (combatResolver != null)
            {
                combatResolver.AbilityResolved -= OnAbilityResolved;
                combatResolver.EndExternalCombat();
            }
            foreach (var pair in unitByAgent)
            {
                if (pair.Key != null) pair.Key.Damaged -= OnAgentDamaged;
            }
        }

        private void Update()
        {
            SamplePerformance();
            if (phase == BattleSurvivalPhase.Prewarming)
            {
                PrewarmMonsterPool();
                return;
            }

            var deltaTime = Mathf.Min(0.05f, Time.deltaTime);
            if (phase != BattleSurvivalPhase.Defeat) combatTime += deltaTime;
            combatResolver.StepExternalCombat(combatTime);
            StepAgents(deltaTime);
            RemoveDefeatedMonsters();
            if (CountLivingHunters() == 0 && phase != BattleSurvivalPhase.Defeat)
            {
                EnterDefeat();
            }

            switch (phase)
            {
                case BattleSurvivalPhase.Preparing:
                    phaseCountdown -= deltaTime;
                    MoveHuntersToSquadAnchors();
                    if (phaseCountdown <= 0f) StartNextRound();
                    break;
                case BattleSurvivalPhase.Wave:
                    SpawnWaveMonsters(deltaTime);
                    RunCommandAndDecisionLoops(deltaTime);
                    if (spawnedThisRound >= roundEnemyTarget && activeMonsters.Count == 0)
                    {
                        CompleteRound();
                    }
                    break;
                case BattleSurvivalPhase.Intermission:
                    phaseCountdown -= deltaTime;
                    RunHunterDecisions(true);
                    MoveHuntersToSquadAnchors();
                    if (phaseCountdown <= 0f) StartNextRound();
                    break;
            }

            peakConcurrentMonsters = Mathf.Max(peakConcurrentMonsters, activeMonsters.Count);
            peakTotalCombatants = Mathf.Max(
                peakTotalCombatants,
                activeMonsters.Count + CountLivingHunters());
        }

        public void RestartSurvival()
        {
            ReturnAllMonstersToPool();
            combatTime = 0f;
            currentRound = 0;
            roundEnemyTarget = 0;
            spawnedThisRound = 0;
            bonusEnemiesThisRound = 0;
            bonusConcurrentThisRound = 0;
            stressBurstRemaining = 0;
            totalMonstersDefeated = 0;
            peakConcurrentMonsters = 0;
            peakTotalCombatants = 0;
            commanderDecisions = 0;
            basicAttackAttempts = 0;
            confirmedBasicHits = 0;
            abilityCasts = 0;
            areaAbilityCasts = 0;
            activeStatusEffects = 0;
            ResetHunters();
            combatResolver.BeginExternalCombat(hunters, allMonsterAgents, combatTime);
            phase = BattleSurvivalPhase.Preparing;
            phaseCountdown = preparationSeconds;
            latestEvent = "The three squads reformed for another survival run.";
        }

        public void AddStressEnemies(int additionalEnemies)
        {
            if (phase != BattleSurvivalPhase.Wave) return;
            var addition = Mathf.Max(0, additionalEnemies);
            bonusEnemiesThisRound += addition;
            bonusConcurrentThisRound = Mathf.Min(
                maximumConcurrentMonsters,
                bonusConcurrentThisRound + addition);
            stressBurstRemaining = Mathf.Min(
                maximumConcurrentMonsters,
                stressBurstRemaining + addition);
            roundEnemyTarget += addition;
            latestEvent = $"Stress injection: bursting {addition} additional pooled monsters into this round.";
        }

        public void EscalateFiveRounds()
        {
            if (phase is BattleSurvivalPhase.Prewarming or BattleSurvivalPhase.Defeat) return;
            currentRound = Mathf.Max(1, currentRound + 5);
            var previousTarget = roundEnemyTarget;
            roundEnemyTarget = Mathf.Max(
                roundEnemyTarget,
                spawnedThisRound + CalculateRoundEnemyTarget(currentRound));
            bonusConcurrentThisRound = Mathf.Min(
                maximumConcurrentMonsters,
                bonusConcurrentThisRound + 50);
            latestEvent =
                $"Threat tier jumped to round {currentRound}; " +
                $"wave quota increased by {roundEnemyTarget - previousTarget}.";
            if (phase == BattleSurvivalPhase.Intermission)
            {
                phaseCountdown = Mathf.Min(phaseCountdown, 1f);
            }
        }

        private void PrewarmMonsterPool()
        {
            var created = 0;
            while (allMonsterUnits.Count < maximumConcurrentMonsters &&
                   created++ < poolCreationsPerFrame)
            {
                var clone = Instantiate(monsterTemplate, monsterPoolRoot);
                clone.name = $"Pooled Horde Monster {allMonsterUnits.Count + 1:000}";
                clone.SetActive(false);
                var unit = clone.GetComponent<BattleSurvivalUnit2D>();
                if (unit == null) unit = clone.AddComponent<BattleSurvivalUnit2D>();
                allMonsterUnits.Add(unit);
                allMonsterAgents.Add(unit.Agent);
                RegisterAgent(unit);

                // Force the first activation during the explicit prewarm phase.
                // RaidAgent2D creates its compact health presentation in Awake;
                // deferring that work until a wave would hide GameObject and
                // renderer creation inside the measurement window.
                clone.SetActive(true);
                unit.Agent.CaptureSpawnPosition();
                unit.Agent.ResetForRaid();
                clone.SetActive(false);
                availableMonsters.Enqueue(unit);
            }
            if (allMonsterUnits.Count < maximumConcurrentMonsters) return;

            combatResolver.AbilityResolved -= OnAbilityResolved;
            combatResolver.AbilityResolved += OnAbilityResolved;
            combatResolver.BeginExternalCombat(hunters, allMonsterAgents, combatTime);
            phase = BattleSurvivalPhase.Preparing;
            phaseCountdown = preparationSeconds;
            latestEvent = "Monster pool ready. Squads are taking their opening sectors.";
        }

        private void ConfigureAuthoredHunters()
        {
            hunterUnits.Clear();
            unitByAgent.Clear();
            for (var squadIndex = 0; squadIndex < squads.Length; squadIndex++)
            {
                var squad = squads[squadIndex];
                squad.Index = squadIndex;
                squad.Name = $"Squad {squadIndex + 1}";
                squad.HomeAnchor = SquadHomeAnchor(squadIndex);
                squad.CommandAnchor = squad.HomeAnchor;
                squad.Order = BattleSquadOrder.FormUp;
                squad.Members.Clear();
                squad.FocusTarget = null;
                squad.Promotions = 0;
                squad.Sergeant = null;
            }

            for (var index = 0; index < hunters.Length; index++)
            {
                var agent = hunters[index];
                if (agent == null) continue;
                var unit = agent.GetComponent<BattleSurvivalUnit2D>();
                if (unit == null) unit = agent.gameObject.AddComponent<BattleSurvivalUnit2D>();
                var squadIndex = Mathf.Clamp(
                    unit.SquadIndex >= 0
                        ? unit.SquadIndex
                        : index / BattleSurvivalHunterCatalog.HuntersPerSquad,
                    0,
                    squads.Length - 1);
                unit.ResetRuntimeState(
                    unit.MemberIndex * 0.017f + squadIndex * 0.031f);
                agent.CaptureSpawnPosition();
                agent.ResetForRaid();
                hunterUnits.Add(unit);
                squads[squadIndex].Members.Add(unit);
                squads[squadIndex].Name = unit.SquadName;
                if (unit.IsSergeant) squads[squadIndex].Sergeant = unit;
                RegisterAgent(unit);
            }
            for (var squadIndex = 0; squadIndex < squads.Length; squadIndex++)
            {
                var squad = squads[squadIndex];
                if (squad.Sergeant != null || squad.Members.Count == 0) continue;
                squad.Sergeant = squad.Members[0];
                squad.Sergeant.PromoteToSergeant();
            }
            selectedHunter = hunterUnits.Count > 0 ? hunterUnits[0] : null;
        }

        private void ResetHunters()
        {
            for (var index = 0; index < hunterUnits.Count; index++)
            {
                var unit = hunterUnits[index];
                if (unit.MemberIndex == 0) unit.PromoteToSergeant();
                else unit.DemoteSergeant();
                unit.ResetRuntimeState(
                    unit.MemberIndex * 0.017f + unit.SquadIndex * 0.031f);
                unit.Agent.ResetForRaid();
            }
            for (var index = 0; index < squads.Length; index++)
            {
                squads[index].Sergeant = squads[index].Members[0];
                squads[index].Promotions = 0;
                squads[index].Order = BattleSquadOrder.FormUp;
                squads[index].CommandAnchor = squads[index].HomeAnchor;
            }
        }

        private void RegisterAgent(BattleSurvivalUnit2D unit)
        {
            if (unit?.Agent == null) return;
            unitByAgent[unit.Agent] = unit;
            unit.Agent.Damaged -= OnAgentDamaged;
            unit.Agent.Damaged += OnAgentDamaged;
        }

        private void StartNextRound()
        {
            currentRound++;
            spawnedThisRound = 0;
            bonusEnemiesThisRound = 0;
            bonusConcurrentThisRound = 0;
            stressBurstRemaining = 0;
            roundEnemyTarget = CalculateRoundEnemyTarget(currentRound);
            spawnAccumulator = spawnInterval;
            commanderAccumulator = commanderDecisionInterval;
            phase = BattleSurvivalPhase.Wave;
            roundLowestFps = float.MaxValue;
            roundHighestFrameMilliseconds = 0f;
            for (var index = 0; index < hunterUnits.Count; index++)
            {
                var hunter = hunterUnits[index].Agent;
                if (hunter.CanReceiveDamage)
                {
                    hunter.Heal(hunter.MaximumHealth * 0.12f, combatTime, combatResolver);
                }
            }
            latestEvent =
                $"ROUND {currentRound}: {roundEnemyTarget} monsters. " +
                $"Health x{RoundHealthMultiplier:0.00}, damage x{RoundDamageMultiplier:0.00}.";
            effects.EmitBurst(Vector2.zero, new Color(1f, 0.32f, 0.08f), 7f, 0.8f);
        }

        private void CompleteRound()
        {
            phase = BattleSurvivalPhase.Intermission;
            phaseCountdown = intermissionSeconds;
            latestEvent =
                $"Round {currentRound} cleared. {LivingHunterCount} hunters remain; " +
                $"low FPS {ResolveRoundLowFps():0.0}, worst frame " +
                $"{roundHighestFrameMilliseconds:0.0} ms; next horde in {intermissionSeconds:0.0}s.";
            effects.EmitBurst(Vector2.zero, new Color(0.2f, 1f, 0.62f), 8f, 0.9f);
        }

        private void EnterDefeat()
        {
            phase = BattleSurvivalPhase.Defeat;
            for (var index = 0; index < activeMonsters.Count; index++)
            {
                activeMonsters[index].Agent.StopMoving();
            }
            latestEvent =
                $"ALL SQUADS LOST - survived through round {currentRound}, " +
                $"defeated {totalMonstersDefeated} monsters, peak battle {peakTotalCombatants} agents.";
        }

        private void SpawnWaveMonsters(float deltaTime)
        {
            if (spawnedThisRound >= roundEnemyTarget || availableMonsters.Count == 0) return;
            var allowedConcurrent = Mathf.Min(
                maximumConcurrentMonsters,
                CalculateRoundConcurrentLimit(currentRound) + bonusConcurrentThisRound);
            if (activeMonsters.Count >= allowedConcurrent) return;

            var burstBudget = Mathf.Min(24, stressBurstRemaining);
            while (burstBudget-- > 0 &&
                   stressBurstRemaining > 0 &&
                   spawnedThisRound < roundEnemyTarget &&
                   activeMonsters.Count < allowedConcurrent &&
                   availableMonsters.Count > 0)
            {
                SpawnMonster(spawnedThisRound);
                spawnedThisRound++;
                stressBurstRemaining--;
            }

            spawnAccumulator += deltaTime;
            var spawnedThisFrame = 0;
            while (spawnAccumulator >= spawnInterval &&
                   spawnedThisRound < roundEnemyTarget &&
                   activeMonsters.Count < allowedConcurrent &&
                   availableMonsters.Count > 0 && spawnedThisFrame++ < 12)
            {
                spawnAccumulator -= spawnInterval;
                SpawnMonster(spawnedThisRound);
                spawnedThisRound++;
            }
        }

        private void SpawnMonster(int roundSerial)
        {
            var unit = availableMonsters.Dequeue();
            var archetype = SelectMonsterArchetype(currentRound, roundSerial);
            ResolveMonsterStats(archetype, out var health, out var mana, out var regeneration,
                out var speed, out var damage, out var range, out var preferredRange,
                out var cooldown, out var ranged, out var color);
            var portalIndex = StableHash(currentRound, roundSerial) % portalPositions.Length;
            var tangent = portalIndex < 2 ? Vector2.up : Vector2.right;
            var spread = ((StableHash(roundSerial, currentRound + 71) % 1000) / 999f - 0.5f) * 24f;
            var position = portalPositions[portalIndex] + tangent * spread;
            position.x = Mathf.Clamp(position.x, -arenaSize.x * 0.48f, arenaSize.x * 0.48f);
            position.y = Mathf.Clamp(position.y, -arenaSize.y * 0.48f, arenaSize.y * 0.48f);

            unit.gameObject.SetActive(false);
            unit.transform.position = new Vector3(position.x, position.y, 0f);
            unit.transform.localScale = Vector3.one * (archetype == MonsterArchetype.Elite
                ? 0.82f
                : archetype == MonsterArchetype.Brute ? 0.68f : 0.5f);
            var serial = totalMonstersDefeated + spawnedThisRound;
            unit.Agent.ConfigureRuntime(
                $"horde-r{currentRound}-{serial:0000}",
                $"{archetype} R{currentRound}-{roundSerial + 1:000}",
                RaidFaction.Monsters,
                MonsterRole(archetype),
                health * RoundHealthMultiplier,
                mana,
                regeneration,
                speed * RoundSpeedMultiplier,
                damage * RoundDamageMultiplier,
                range,
                preferredRange,
                cooldown,
                ranged,
                color,
                monsterAbilitySets[(int)archetype],
                archetype is MonsterArchetype.Elite or MonsterArchetype.Brute ? 0.5f : 0.36f,
                true,
                true,
                true,
                false);
            unit.ConfigureMonster(archetype.ToString(), serial,
                (serial % 19) * 0.009f);
            unit.gameObject.name = unit.Agent.DisplayName;
            unit.gameObject.SetActive(true);
            unit.Agent.CaptureSpawnPosition();
            unit.Agent.ResetForRaid();
            activeMonsters.Add(unit);
        }

        private void RemoveDefeatedMonsters()
        {
            for (var index = activeMonsters.Count - 1; index >= 0; index--)
            {
                var unit = activeMonsters[index];
                if (unit.Agent.LifeState != RaidLifeState.Dead) continue;
                totalMonstersDefeated++;
                var last = activeMonsters.Count - 1;
                activeMonsters[index] = activeMonsters[last];
                activeMonsters.RemoveAt(last);
                unit.gameObject.SetActive(false);
                availableMonsters.Enqueue(unit);
                if (monsterDecisionCursor > index) monsterDecisionCursor--;
            }
            if (monsterDecisionCursor >= activeMonsters.Count) monsterDecisionCursor = 0;
        }

        private void ReturnAllMonstersToPool()
        {
            activeMonsters.Clear();
            availableMonsters.Clear();
            for (var index = 0; index < allMonsterUnits.Count; index++)
            {
                var unit = allMonsterUnits[index];
                unit.gameObject.SetActive(false);
                availableMonsters.Enqueue(unit);
            }
            monsterDecisionCursor = 0;
        }

        private void RunCommandAndDecisionLoops(float deltaTime)
        {
            commanderAccumulator += deltaTime;
            if (commanderAccumulator >= commanderDecisionInterval)
            {
                commanderAccumulator -= commanderDecisionInterval;
                UpdateCoalitionCommand();
            }
            RunHunterDecisions(false);
            RunMonsterDecisionBudget();
        }

        private void RunHunterDecisions(bool supportOnly)
        {
            for (var index = 0; index < hunterUnits.Count; index++)
            {
                var unit = hunterUnits[index];
                if (!unit.Agent.CanAct || combatTime < unit.NextDecisionAt) continue;
                DecideHunter(unit, supportOnly);
                unit.NextDecisionAt = combatTime + hunterDecisionInterval *
                    Mathf.Lerp(1.2f, 0.8f, unit.Aggression);
                decisionsThisSample++;
            }
        }

        private void DecideHunter(BattleSurvivalUnit2D unit, bool supportOnly)
        {
            var agent = unit.Agent;
            var squad = squads[unit.SquadIndex];
            var localTarget = FindNearestMonster(agent.Position, 15f);
            var target = squad.FocusTarget != null && squad.FocusTarget.CanReceiveDamage
                ? squad.FocusTarget
                : localTarget?.Agent;
            if (target != null && localTarget != null)
            {
                var focusDistance = Vector2.SqrMagnitude(target.Position - agent.Position);
                var localDistance = Vector2.SqrMagnitude(localTarget.Agent.Position - agent.Position);
                if (localDistance < focusDistance * Mathf.Lerp(0.5f, 0.88f, unit.Cohesion))
                {
                    target = localTarget.Agent;
                }
            }
            unit.CurrentTarget = target;

            if (combatResolver.TryUseBestAbility(agent, target))
            {
                unit.CurrentObjective = agent.Role == RaidCombatRole.Healer
                    ? "Supporting wounded allies"
                    : $"Casting against {target?.DisplayName ?? "the horde"}";
                return;
            }
            if (supportOnly)
            {
                unit.CurrentObjective = "Recovering and regrouping";
                return;
            }
            if (target != null && agent.CanBasicAttack(target, combatTime))
            {
                basicAttackAttempts++;
                attacksThisSample++;
                if (agent.TryBasicAttack(target, combatTime, combatResolver))
                {
                    confirmedBasicHits++;
                    hitsThisSample++;
                }
                unit.CurrentObjective = $"Engaging {target.DisplayName}";
                return;
            }

            if (target != null)
            {
                var distanceFromCommand = Vector2.Distance(agent.Position, squad.CommandAnchor);
                var chaseLimit = Mathf.Lerp(7f, 17f, unit.Aggression) *
                                 Mathf.Lerp(0.8f, 1.15f, 1f - unit.Cohesion);
                if (distanceFromCommand < chaseLimit ||
                    Vector2.Distance(target.Position, squad.CommandAnchor) < chaseLimit)
                {
                    agent.MoveToward(target.Position,
                        Mathf.Max(0.2f, agent.PreferredCombatRange * 0.82f));
                    unit.CurrentObjective = $"Intercepting {target.DisplayName}";
                    return;
                }
            }

            var formation = squad.CommandAnchor + FormationOffset(unit.MemberIndex);
            agent.MoveToward(formation, 0.35f);
            unit.CurrentObjective = OrderLabel(squad.Order);
        }

        private void RunMonsterDecisionBudget()
        {
            if (activeMonsters.Count == 0) return;
            var inspected = 0;
            var decisions = 0;
            while (inspected < activeMonsters.Count &&
                   decisions < maximumMonsterDecisionsPerFrame)
            {
                if (monsterDecisionCursor >= activeMonsters.Count) monsterDecisionCursor = 0;
                var unit = activeMonsters[monsterDecisionCursor++];
                inspected++;
                if (!unit.Agent.CanAct || combatTime < unit.NextDecisionAt) continue;
                DecideMonster(unit);
                unit.NextDecisionAt = combatTime + monsterDecisionInterval +
                                      (unit.MemberIndex % 7) * 0.008f;
                decisions++;
            }
            decisionsThisSample += decisions;
        }

        private void DecideMonster(BattleSurvivalUnit2D unit)
        {
            var agent = unit.Agent;
            var target = agent.ResolveForcedTarget(combatTime) ??
                         FindNearestHunter(agent.Position)?.Agent;
            unit.CurrentTarget = target;
            if (target == null)
            {
                agent.MoveToward(Vector2.zero, 1f);
                return;
            }
            if (combatResolver.TryUseBestAbility(agent, target)) return;
            if (agent.CanBasicAttack(target, combatTime))
            {
                basicAttackAttempts++;
                attacksThisSample++;
                if (agent.TryBasicAttack(target, combatTime, combatResolver))
                {
                    confirmedBasicHits++;
                    hitsThisSample++;
                }
                return;
            }
            agent.MoveToward(target.Position,
                Mathf.Max(0.18f, agent.PreferredCombatRange * 0.82f));
        }

        private void UpdateCoalitionCommand()
        {
            CalculatePortalPressure();
            SortPortalPressure();
            SortSquadCapability();
            var rescuedSquad = FindSquadNeedingRescue();
            for (var rank = 0; rank < sortedSquads.Length; rank++)
            {
                var squad = squads[sortedSquads[rank]];
                PromoteSergeantIfNeeded(squad);
                var living = CountLiving(squad);
                var averageHealth = AverageHealth(squad);
                if (living <= 2 || averageHealth < 0.3f)
                {
                    squad.Order = BattleSquadOrder.Regroup;
                    squad.CommandAnchor = squad.HomeAnchor * 0.3f;
                }
                else if (rescuedSquad != null && rescuedSquad != squad && rank == 0)
                {
                    squad.Order = BattleSquadOrder.Rescue;
                    squad.CommandAnchor = SquadCentroid(rescuedSquad);
                }
                else
                {
                    var portal = sortedPortals[Mathf.Min(rank, 2)];
                    squad.Order = PortalOrder(portal);
                    squad.CommandAnchor = Vector2.Lerp(Vector2.zero, portalPositions[portal], 0.52f);
                }
                squad.FocusTarget = SelectSquadFocus(squad);
                ApplySergeantCohesion(squad);
            }
            commanderDecisions++;
        }

        private void PromoteSergeantIfNeeded(SquadState squad)
        {
            if (squad.Sergeant?.Agent != null && squad.Sergeant.Agent.CanReceiveDamage) return;
            squad.Sergeant?.DemoteSergeant();
            squad.Sergeant = null;
            for (var index = 0; index < squad.Members.Count; index++)
            {
                var candidate = squad.Members[index];
                if (!candidate.Agent.CanReceiveDamage) continue;
                candidate.PromoteToSergeant();
                squad.Sergeant = candidate;
                squad.Promotions++;
                latestEvent = $"{candidate.Agent.DisplayName} assumed command of {squad.Name} squad.";
                break;
            }
        }

        private void ApplySergeantCohesion(SquadState squad)
        {
            if (squad.Sergeant?.Agent == null || !squad.Sergeant.Agent.CanAct) return;
            var center = squad.Sergeant.Agent.Position;
            for (var index = 0; index < squad.Members.Count; index++)
            {
                var agent = squad.Members[index].Agent;
                if (!agent.CanAct || Vector2.SqrMagnitude(agent.Position - center) > 36f) continue;
                agent.GrantDamageBuff(1.05f, combatTime + commanderDecisionInterval + 0.1f);
            }
        }

        private RaidAgent2D SelectSquadFocus(SquadState squad)
        {
            var origin = squad.Sergeant?.Agent != null && squad.Sergeant.Agent.CanAct
                ? squad.Sergeant.Agent.Position
                : squad.CommandAnchor;
            RaidAgent2D best = null;
            var bestScore = float.MinValue;
            for (var index = 0; index < activeMonsters.Count; index++)
            {
                var candidate = activeMonsters[index].Agent;
                if (!candidate.CanReceiveDamage) continue;
                var distance = Vector2.Distance(origin, candidate.Position);
                if (distance > 24f) continue;
                var roleWeight = candidate.Role switch
                {
                    RaidCombatRole.Elite => 15f,
                    RaidCombatRole.Mage => 8f,
                    RaidCombatRole.Archer => 4f,
                    _ => 0f
                };
                var score = roleWeight - distance + (1f - candidate.HealthRatio) * 3f;
                if (score <= bestScore) continue;
                bestScore = score;
                best = candidate;
            }
            return best;
        }

        private void CalculatePortalPressure()
        {
            Array.Clear(portalPressure, 0, portalPressure.Length);
            for (var index = 0; index < activeMonsters.Count; index++)
            {
                var monster = activeMonsters[index].Agent;
                if (!monster.CanReceiveDamage) continue;
                var portal = NearestPortal(monster.Position);
                var weight = monster.Role switch
                {
                    RaidCombatRole.Elite => 3f,
                    RaidCombatRole.Mage => 1.7f,
                    RaidCombatRole.Archer => 1.35f,
                    _ => 1f
                };
                portalPressure[portal] += weight;
            }
        }

        private void SortPortalPressure()
        {
            for (var index = 0; index < sortedPortals.Length; index++) sortedPortals[index] = index;
            for (var left = 0; left < sortedPortals.Length - 1; left++)
            {
                for (var right = left + 1; right < sortedPortals.Length; right++)
                {
                    if (portalPressure[sortedPortals[right]] <= portalPressure[sortedPortals[left]])
                    {
                        continue;
                    }
                    (sortedPortals[left], sortedPortals[right]) =
                        (sortedPortals[right], sortedPortals[left]);
                }
            }
        }

        private void SortSquadCapability()
        {
            for (var index = 0; index < sortedSquads.Length; index++) sortedSquads[index] = index;
            for (var left = 0; left < sortedSquads.Length - 1; left++)
            {
                for (var right = left + 1; right < sortedSquads.Length; right++)
                {
                    var leftSquad = squads[sortedSquads[left]];
                    var rightSquad = squads[sortedSquads[right]];
                    var leftScore = CountLiving(leftSquad) * 10f + AverageHealth(leftSquad) * 6f;
                    var rightScore = CountLiving(rightSquad) * 10f + AverageHealth(rightSquad) * 6f;
                    if (rightScore <= leftScore) continue;
                    (sortedSquads[left], sortedSquads[right]) =
                        (sortedSquads[right], sortedSquads[left]);
                }
            }
        }

        private SquadState FindSquadNeedingRescue()
        {
            SquadState best = null;
            var mostDowned = 0;
            for (var squadIndex = 0; squadIndex < squads.Length; squadIndex++)
            {
                var downed = 0;
                var squad = squads[squadIndex];
                for (var memberIndex = 0; memberIndex < squad.Members.Count; memberIndex++)
                {
                    if (squad.Members[memberIndex].Agent.CanBeRescued) downed++;
                }
                if (downed <= mostDowned) continue;
                mostDowned = downed;
                best = squad;
            }
            return mostDowned > 0 ? best : null;
        }

        private BattleSurvivalUnit2D FindNearestMonster(Vector2 position, float maximumRange)
        {
            BattleSurvivalUnit2D best = null;
            var bestDistance = maximumRange * maximumRange;
            for (var index = 0; index < activeMonsters.Count; index++)
            {
                var candidate = activeMonsters[index];
                if (!candidate.Agent.CanReceiveDamage) continue;
                var distance = Vector2.SqrMagnitude(candidate.Agent.Position - position);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = candidate;
            }
            return best;
        }

        private BattleSurvivalUnit2D FindNearestHunter(Vector2 position)
        {
            BattleSurvivalUnit2D best = null;
            var bestDistance = float.MaxValue;
            for (var index = 0; index < hunterUnits.Count; index++)
            {
                var candidate = hunterUnits[index];
                if (!candidate.Agent.CanReceiveDamage) continue;
                var distance = Vector2.SqrMagnitude(candidate.Agent.Position - position);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = candidate;
            }
            return best;
        }

        private void StepAgents(float deltaTime)
        {
            for (var index = 0; index < hunterUnits.Count; index++)
            {
                hunterUnits[index].Agent.Step(deltaTime, combatTime, combatResolver);
            }
            for (var index = 0; index < activeMonsters.Count; index++)
            {
                activeMonsters[index].Agent.Step(deltaTime, combatTime, combatResolver);
            }
        }

        private void MoveHuntersToSquadAnchors()
        {
            for (var squadIndex = 0; squadIndex < squads.Length; squadIndex++)
            {
                var squad = squads[squadIndex];
                for (var memberIndex = 0; memberIndex < squad.Members.Count; memberIndex++)
                {
                    var unit = squad.Members[memberIndex];
                    if (!unit.Agent.CanAct) continue;
                    unit.Agent.MoveToward(
                        squad.CommandAnchor + FormationOffset(memberIndex), 0.35f);
                }
            }
        }

        private void OnAgentDamaged(RaidAgent2D target, RaidAgent2D source, float amount)
        {
            if (source != null && unitByAgent.TryGetValue(source, out var sourceUnit))
            {
                sourceUnit.RecordDamage(amount);
                if (target != null && target.CurrentHealth <= 0f &&
                    unitByAgent.TryGetValue(target, out var targetUnit) &&
                    !targetUnit.DeathCredited)
                {
                    targetUnit.DeathCredited = true;
                    sourceUnit.RecordKill();
                }
            }
        }

        private void OnAbilityResolved(RaidAgent2D caster, RaidAbilitySpec ability)
        {
            abilityCasts++;
            abilitiesThisSample++;
            if (ability != null && (ability.shape != RaidAttackShape.Single ||
                                    ability.effect is RaidAbilityEffect.AreaDamage or
                                        RaidAbilityEffect.AreaHeal or
                                        RaidAbilityEffect.PersistentAreaHeal or
                                        RaidAbilityEffect.ProjectileAreaDamage or
                                        RaidAbilityEffect.ChainDamage or
                                        RaidAbilityEffect.PiercingDamage or
                                        RaidAbilityEffect.Taunt or RaidAbilityEffect.Shield))
            {
                areaAbilityCasts++;
            }
            if (caster != null && unitByAgent.TryGetValue(caster, out var unit))
            {
                unit.RecordAbilityCast();
            }
        }

        private int CalculateRoundEnemyTarget(int round)
        {
            return baseEnemiesPerRound +
                   round * linearEnemiesPerRound +
                   round * round * quadraticEnemiesPerRound;
        }

        private int CalculateRoundConcurrentLimit(int round)
        {
            return Mathf.Min(maximumConcurrentMonsters, 20 + round * 11);
        }

        private float RoundHealthMultiplier =>
            Mathf.Pow(healthGrowthPerRound, Mathf.Max(0, currentRound - 1));
        private float RoundDamageMultiplier =>
            Mathf.Pow(damageGrowthPerRound, Mathf.Max(0, currentRound - 1));
        private float RoundSpeedMultiplier => Mathf.Min(
            maximumSpeedMultiplier,
            1f + Mathf.Max(0, currentRound - 1) * speedGrowthPerRound);

        private void BuildMonsterAbilitySets()
        {
            monsterAbilitySets[(int)MonsterArchetype.Ravager] = new List<RaidAbilitySpec>();
            monsterAbilitySets[(int)MonsterArchetype.Brute] = new List<RaidAbilitySpec>
            {
                MonsterArea("horde-brute-slam", "Brute Slam", 2.4f, 13f, 5.5f)
            };
            monsterAbilitySets[(int)MonsterArchetype.Spitter] = new List<RaidAbilitySpec>
            {
                MonsterDot("horde-acid", "Acid Spit", 7f, 7f, 3f, 5.2f)
            };
            monsterAbilitySets[(int)MonsterArchetype.Hexer] = new List<RaidAbilitySpec>
            {
                MonsterChain("horde-chain", "Grave Arc", 7f, 9f, 5f, 3, 6.5f),
                MonsterFreeze("horde-bind", "Grave Bind", 6.5f, 6f, 8f, 0.8f)
            };
            monsterAbilitySets[(int)MonsterArchetype.Charger] = new List<RaidAbilitySpec>
            {
                MonsterDash("horde-charge", "Rending Charge", 6f, 14f, 5.4f)
            };
            monsterAbilitySets[(int)MonsterArchetype.Elite] = new List<RaidAbilitySpec>
            {
                MonsterArea("horde-elite-pulse", "Overlord Pulse", 3.4f, 18f, 5.8f),
                MonsterChain("horde-elite-chain", "Black Lightning", 7.5f, 12f, 7f, 5, 7.2f)
            };
        }

        private static MonsterArchetype SelectMonsterArchetype(int round, int serial)
        {
            var roll = StableHash(round * 13 + serial, serial * 7 + 19) % 100;
            var eliteChance = Mathf.Min(22, 2 + round);
            if (roll < eliteChance) return MonsterArchetype.Elite;
            return (serial % 12) switch
            {
                0 or 7 => MonsterArchetype.Brute,
                2 or 9 => MonsterArchetype.Spitter,
                4 => MonsterArchetype.Hexer,
                6 or 11 => MonsterArchetype.Charger,
                _ => MonsterArchetype.Ravager
            };
        }

        private static void ResolveMonsterStats(
            MonsterArchetype archetype, out float health, out float mana,
            out float regeneration, out float speed, out float damage,
            out float range, out float preferredRange, out float cooldown,
            out bool ranged, out Color color)
        {
            health = 48f;
            mana = 0f;
            regeneration = 0f;
            speed = 3.7f;
            damage = 6.5f;
            range = 1.25f;
            preferredRange = 0.95f;
            cooldown = 1f;
            ranged = false;
            color = new Color(0.5f, 0.88f, 0.15f);
            switch (archetype)
            {
                case MonsterArchetype.Brute:
                    health = 108f;
                    mana = 60f;
                    regeneration = 5f;
                    speed = 2.9f;
                    damage = 10.5f;
                    cooldown = 1.2f;
                    color = new Color(0.65f, 0.28f, 0.08f);
                    break;
                case MonsterArchetype.Spitter:
                    health = 56f;
                    mana = 65f;
                    regeneration = 6f;
                    speed = 3.45f;
                    damage = 5.5f;
                    range = 6.8f;
                    preferredRange = 5.5f;
                    cooldown = 1.25f;
                    ranged = true;
                    color = new Color(0.62f, 1f, 0.12f);
                    break;
                case MonsterArchetype.Hexer:
                    health = 72f;
                    mana = 110f;
                    regeneration = 9f;
                    speed = 3.25f;
                    damage = 5f;
                    range = 6.2f;
                    preferredRange = 5f;
                    cooldown = 1.35f;
                    ranged = true;
                    color = new Color(0.7f, 0.18f, 0.95f);
                    break;
                case MonsterArchetype.Charger:
                    health = 66f;
                    mana = 60f;
                    regeneration = 6f;
                    speed = 4.75f;
                    damage = 8f;
                    cooldown = 0.82f;
                    color = new Color(1f, 0.28f, 0.08f);
                    break;
                case MonsterArchetype.Elite:
                    health = 178f;
                    mana = 140f;
                    regeneration = 10f;
                    speed = 3.55f;
                    damage = 12f;
                    range = 1.6f;
                    preferredRange = 1.2f;
                    cooldown = 0.9f;
                    color = new Color(0.95f, 0.08f, 0.12f);
                    break;
            }
        }

        private static RaidCombatRole MonsterRole(MonsterArchetype archetype)
        {
            return archetype switch
            {
                MonsterArchetype.Brute => RaidCombatRole.Melee,
                MonsterArchetype.Spitter => RaidCombatRole.Archer,
                MonsterArchetype.Hexer => RaidCombatRole.Mage,
                MonsterArchetype.Charger => RaidCombatRole.Assassin,
                MonsterArchetype.Elite => RaidCombatRole.Elite,
                _ => RaidCombatRole.Melee
            };
        }

        private static RaidAbilitySpec MonsterArea(
            string id, string name, float radius, float power, float cooldown)
        {
            var ability = RaidAbilitySpec.Create(id, name, RaidAbilityEffect.AreaDamage,
                radius, radius, power, cooldown, 18f, new Color(1f, 0.18f, 0.08f));
            ability.shape = RaidAttackShape.Circle;
            ability.maximumTargets = 18;
            return ability;
        }

        private static RaidAbilitySpec MonsterDot(
            string id, string name, float range, float power, float tick, float cooldown)
        {
            var ability = RaidAbilitySpec.Create(id, name, RaidAbilityEffect.DamageOverTime,
                range, 0f, power, cooldown, 16f, new Color(0.55f, 1f, 0.1f));
            ability.secondaryPower = tick;
            ability.duration = 4f;
            ability.width = 0.25f;
            return ability;
        }

        private static RaidAbilitySpec MonsterChain(
            string id, string name, float range, float power, float chainPower,
            int targets, float cooldown)
        {
            var ability = RaidAbilitySpec.Create(id, name, RaidAbilityEffect.ChainDamage,
                range, 2.5f, power, cooldown, 24f, new Color(0.68f, 0.25f, 1f));
            ability.secondaryPower = chainPower;
            ability.maximumTargets = targets;
            ability.width = 0.2f;
            return ability;
        }

        private static RaidAbilitySpec MonsterFreeze(
            string id, string name, float range, float power, float cooldown, float duration)
        {
            var ability = RaidAbilitySpec.Create(id, name, RaidAbilityEffect.Freeze,
                range, 0f, power, cooldown, 22f, new Color(0.35f, 0.72f, 1f));
            ability.duration = duration;
            ability.width = 0.24f;
            return ability;
        }

        private static RaidAbilitySpec MonsterDash(
            string id, string name, float range, float power, float cooldown)
        {
            var ability = RaidAbilitySpec.Create(id, name, RaidAbilityEffect.DashStrike,
                range, 0f, power, cooldown, 17f, new Color(1f, 0.25f, 0.08f));
            ability.width = 0.4f;
            return ability;
        }

        private void ConfigurePortalPositions()
        {
            var half = arenaSize * 0.5f;
            portalPositions[0] = new Vector2(-half.x + 2.5f, 0f);
            portalPositions[1] = new Vector2(half.x - 2.5f, 0f);
            portalPositions[2] = new Vector2(0f, half.y - 2.5f);
            portalPositions[3] = new Vector2(0f, -half.y + 2.5f);
        }

        private static Vector2 SquadHomeAnchor(int squadIndex)
        {
            return squadIndex switch
            {
                0 => new Vector2(-11f, 4f),
                1 => new Vector2(11f, 4f),
                _ => new Vector2(0f, -9f)
            };
        }

        private static Vector2 FormationOffset(int memberIndex)
        {
            return memberIndex switch
            {
                0 => new Vector2(0f, 1.8f),
                1 => new Vector2(-1.4f, 0.8f),
                2 => new Vector2(1.4f, 0.8f),
                3 => new Vector2(-2.8f, -0.2f),
                4 => new Vector2(0f, -0.2f),
                5 => new Vector2(2.8f, -0.2f),
                6 => new Vector2(-3.6f, -1.5f),
                7 => new Vector2(-1.2f, -1.5f),
                8 => new Vector2(1.2f, -1.5f),
                _ => new Vector2(3.6f, -1.5f)
            };
        }

        private static BattleSquadOrder PortalOrder(int portal)
        {
            return portal switch
            {
                0 => BattleSquadOrder.DefendWest,
                1 => BattleSquadOrder.DefendEast,
                2 => BattleSquadOrder.DefendNorth,
                _ => BattleSquadOrder.DefendSouth
            };
        }

        private static string OrderLabel(BattleSquadOrder order)
        {
            return order switch
            {
                BattleSquadOrder.Rescue => "Reinforcing a squad in danger",
                BattleSquadOrder.Regroup => "Falling back to regroup",
                BattleSquadOrder.FormUp => "Holding opening formation",
                _ => $"{order.ToString().Replace("Defend", "Defending ")} sector"
            };
        }

        private int NearestPortal(Vector2 position)
        {
            var best = 0;
            var bestDistance = float.MaxValue;
            for (var index = 0; index < portalPositions.Length; index++)
            {
                var distance = Vector2.SqrMagnitude(position - portalPositions[index]);
                if (distance >= bestDistance) continue;
                best = index;
                bestDistance = distance;
            }
            return best;
        }

        private static int StableHash(int left, int right)
        {
            var hash = unchecked(17 + left * 73856093 + right * 19349663);
            hash ^= hash >> 13;
            return hash & int.MaxValue;
        }

        private static int CountLiving(SquadState squad)
        {
            var count = 0;
            for (var index = 0; index < squad.Members.Count; index++)
            {
                if (squad.Members[index].Agent.CanReceiveDamage) count++;
            }
            return count;
        }

        private int CountLivingHunters()
        {
            var count = 0;
            for (var index = 0; index < hunterUnits.Count; index++)
            {
                if (hunterUnits[index].Agent.CanReceiveDamage) count++;
            }
            return count;
        }

        private int CountActiveSergeants()
        {
            var count = 0;
            for (var index = 0; index < squads.Length; index++)
            {
                if (squads[index].Sergeant?.Agent != null &&
                    squads[index].Sergeant.Agent.CanReceiveDamage)
                {
                    count++;
                }
            }
            return count;
        }

        private static float AverageHealth(SquadState squad)
        {
            var total = 0f;
            var count = 0;
            for (var index = 0; index < squad.Members.Count; index++)
            {
                var agent = squad.Members[index].Agent;
                if (agent.LifeState == RaidLifeState.Dead) continue;
                total += agent.HealthRatio;
                count++;
            }
            return count > 0 ? total / count : 0f;
        }

        private static Vector2 SquadCentroid(SquadState squad)
        {
            var sum = Vector2.zero;
            var count = 0;
            for (var index = 0; index < squad.Members.Count; index++)
            {
                var agent = squad.Members[index].Agent;
                if (agent.LifeState == RaidLifeState.Dead) continue;
                sum += agent.Position;
                count++;
            }
            return count > 0 ? sum / count : squad.HomeAnchor;
        }

        private void SamplePerformance()
        {
            var delta = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
            sampleElapsed += delta;
            sampleFrames++;
            if (sampleElapsed < 1f) return;
            displayedFps = sampleFrames / sampleElapsed;
            displayedFrameMilliseconds = sampleElapsed * 1000f / sampleFrames;
            displayedDecisionsPerSecond = Mathf.RoundToInt(decisionsThisSample / sampleElapsed);
            displayedAttacksPerSecond = Mathf.RoundToInt(attacksThisSample / sampleElapsed);
            displayedHitsPerSecond = Mathf.RoundToInt(hitsThisSample / sampleElapsed);
            displayedAbilitiesPerSecond = Mathf.RoundToInt(abilitiesThisSample / sampleElapsed);
            if (phase == BattleSurvivalPhase.Wave)
            {
                roundLowestFps = Mathf.Min(roundLowestFps, displayedFps);
                roundHighestFrameMilliseconds = Mathf.Max(
                    roundHighestFrameMilliseconds,
                    displayedFrameMilliseconds);
            }
            activeStatusEffects = CountStatuses();
            sampleElapsed = 0f;
            sampleFrames = 0;
            decisionsThisSample = 0;
            attacksThisSample = 0;
            hitsThisSample = 0;
            abilitiesThisSample = 0;
        }

        private int CountStatuses()
        {
            var count = 0;
            for (var index = 0; index < hunterUnits.Count; index++)
            {
                hunterUnits[index].Agent.CollectActiveStatusEffects(combatTime, statusScratch);
                count += statusScratch.Count;
            }
            for (var index = 0; index < activeMonsters.Count; index++)
            {
                activeMonsters[index].Agent.CollectActiveStatusEffects(combatTime, statusScratch);
                count += statusScratch.Count;
            }
            return count;
        }

        private void OnGUI()
        {
            var guiScale = Mathf.Clamp(
                Mathf.Min(Screen.width / 1180f, Screen.height / 560f),
                0.5f,
                1f);
            var previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(guiScale, guiScale, 1f));
            guiWidth = Screen.width / guiScale;
            guiHeight = Screen.height / guiScale;
            EnsureStyles();
            DrawBattlePanel();
            DrawSelectedHunterPanel();
            DrawSquadPanels();
            GUI.matrix = previousMatrix;
        }

        private void DrawBattlePanel()
        {
            var panel = new Rect(12f, 12f, 500f, 300f);
            DrawPanel(panel);
            GUI.Label(new Rect(26f, 20f, 470f, 24f), "BATTLE TEST - THREE SQUAD SURVIVAL", titleStyle);
            GUI.Label(new Rect(26f, 46f, 470f, 20f),
                phase == BattleSurvivalPhase.Prewarming
                    ? $"PREWARMING HORDE POOL: {allMonsterUnits.Count}/{maximumConcurrentMonsters}"
                    : $"ROUND {currentRound} - {phase.ToString().ToUpperInvariant()}", labelStyle);

            if (GUI.Button(new Rect(26f, 72f, 82f, 25f), "0.25x")) Time.timeScale = 0.25f;
            if (GUI.Button(new Rect(112f, 72f, 82f, 25f), "0.5x")) Time.timeScale = 0.5f;
            if (GUI.Button(new Rect(198f, 72f, 82f, 25f), "1x")) Time.timeScale = 1f;
            if (GUI.Button(new Rect(284f, 72f, 82f, 25f), "2x")) Time.timeScale = 2f;
            if (GUI.Button(new Rect(370f, 72f, 126f, 25f), "RESTART")) RestartSurvival();
            if (GUI.Button(new Rect(26f, 102f, 150f, 25f), "STRESS +220")) AddStressEnemies(220);
            if (GUI.Button(new Rect(182f, 102f, 150f, 25f), "THREAT +5 ROUNDS")) EscalateFiveRounds();

            GUI.Label(new Rect(26f, 134f, 470f, 19f),
                $"Hunters: {LivingHunterCount}/{hunterUnits.Count}   Horde: {activeMonsters.Count} active   " +
                $"Spawned: {spawnedThisRound}/{roundEnemyTarget}", labelStyle);
            GUI.Label(new Rect(26f, 154f, 470f, 19f),
                $"Enemy scaling: HP x{RoundHealthMultiplier:0.00}   Damage x{RoundDamageMultiplier:0.00}   " +
                $"Speed x{RoundSpeedMultiplier:0.00}", labelStyle);
            GUI.Label(new Rect(26f, 174f, 470f, 19f),
                $"FPS: {displayedFps:0.0} ({displayedFrameMilliseconds:0.0} ms)   " +
                $"Memory: {Profiler.GetTotalAllocatedMemoryLong() / (1024f * 1024f):0} MB", labelStyle);
            GUI.Label(new Rect(26f, 194f, 470f, 19f),
                $"AI/s: {displayedDecisionsPerSecond}   Attacks/s: {displayedAttacksPerSecond}   " +
                $"Hits/s: {displayedHitsPerSecond}   Abilities/s: {displayedAbilitiesPerSecond}", labelStyle);
            GUI.Label(new Rect(26f, 214f, 470f, 19f),
                $"AOE casts: {areaAbilityCasts}   Active statuses: {activeStatusEffects}   " +
                $"Peak: {peakTotalCombatants} agents / {peakConcurrentMonsters} monsters", labelStyle);
            GUI.Label(new Rect(26f, 236f, 470f, 20f),
                $"Round low: {ResolveRoundLowFps():0.0} FPS / " +
                $"{roundHighestFrameMilliseconds:0.0} ms   Threat - W:{portalPressure[0]:0} E:{portalPressure[1]:0} " +
                $"N:{portalPressure[2]:0} S:{portalPressure[3]:0}", labelStyle);
            GUI.Label(new Rect(26f, 258f, 470f, 38f), latestEvent, smallStyle);
        }

        private void DrawSelectedHunterPanel()
        {
            var width = 355f;
            var panel = new Rect(guiWidth - width - 12f, 12f, width, 300f);
            DrawPanel(panel);
            GUI.Label(new Rect(panel.x + 14f, panel.y + 8f, width - 28f, 24f),
                "INDIVIDUAL HUNTER INSPECTOR", titleStyle);
            if (selectedHunter?.Agent == null)
            {
                GUI.Label(new Rect(panel.x + 14f, panel.y + 42f, width - 28f, 20f),
                    "Select a hunter from a squad card.", labelStyle);
                return;
            }
            var agent = selectedHunter.Agent;
            var x = panel.x + 14f;
            var y = panel.y + 38f;
            GUI.Label(new Rect(x, y, width - 28f, 22f),
                $"{agent.DisplayName} {(selectedHunter.IsSergeant ? "[S] SERGEANT" : string.Empty)}",
                labelStyle);
            y += 21f;
            GUI.Label(new Rect(x, y, width - 28f, 19f),
                $"{selectedHunter.SquadName} Squad - {selectedHunter.BuildLabel}", smallStyle);
            y += 19f;
            GUI.Label(new Rect(x, y, width - 28f, 32f), selectedHunter.TraitLabel, smallStyle);
            y += 32f;
            DrawBar(new Rect(x, y, width - 28f, 12f), agent.HealthRatio,
                new Color(0.86f, 0.08f, 0.06f), $"HP {agent.CurrentHealth:0}/{agent.MaximumHealth:0}");
            y += 18f;
            DrawBar(new Rect(x, y, width - 28f, 12f),
                agent.MaximumMana <= 0f ? 0f : agent.CurrentMana / agent.MaximumMana,
                new Color(0.08f, 0.42f, 1f), $"Mana {agent.CurrentMana:0}/{agent.MaximumMana:0}");
            y += 21f;
            GUI.Label(new Rect(x, y, width - 28f, 18f),
                $"Goal: {selectedHunter.CurrentObjective}", smallStyle);
            y += 19f;
            GUI.Label(new Rect(x, y, width - 28f, 18f),
                $"Kills {selectedHunter.Kills}   Damage {selectedHunter.DamageDealt:0}   " +
                $"Casts {selectedHunter.AbilityCasts}", smallStyle);
            y += 21f;
            for (var index = 0; index < agent.Abilities.Count; index++)
            {
                var ability = agent.Abilities[index];
                var remaining = agent.GetAbilityCooldownRemaining(ability, combatTime);
                var state = agent.GetAbilityAvailability(ability, combatTime);
                GUI.Label(new Rect(x, y, width - 28f, 18f),
                    $"{index + 1}. {ability.displayName} - " +
                    (remaining > 0f ? $"{remaining:0.0}s" : state.ToString()), smallStyle);
                y += 18f;
            }
            agent.CollectActiveStatusEffects(combatTime, statusScratch);
            var statuses = statusScratch.Count == 0 ? "None" : string.Empty;
            for (var index = 0; index < statusScratch.Count; index++)
            {
                if (index > 0) statuses += ", ";
                statuses += $"{statusScratch[index].Kind} {statusScratch[index].RemainingSeconds:0.0}s";
            }
            GUI.Label(new Rect(x, panel.yMax - 33f, width - 28f, 26f),
                $"Statuses: {statuses}", smallStyle);
        }

        private void DrawSquadPanels()
        {
            var gap = 8f;
            var width = Mathf.Min(360f, (guiWidth - 24f - gap * 2f) / 3f);
            const float height = 230f;
            var startX = 12f;
            var y = guiHeight - height - 12f;
            for (var squadIndex = 0; squadIndex < squads.Length; squadIndex++)
            {
                var panel = new Rect(startX + squadIndex * (width + gap), y, width, height);
                DrawSquadPanel(squads[squadIndex], panel);
            }
        }

        private void DrawSquadPanel(SquadState squad, Rect panel)
        {
            DrawPanel(panel);
            GUI.Label(new Rect(panel.x + 10f, panel.y + 5f, panel.width - 20f, 20f),
                $"{squad.Name.ToUpperInvariant()} - {OrderLabel(squad.Order)}", labelStyle);
            var rowY = panel.y + 28f;
            for (var index = 0; index < squad.Members.Count; index++)
            {
                var unit = squad.Members[index];
                var agent = unit.Agent;
                var row = new Rect(panel.x + 8f, rowY, panel.width - 16f, 19f);
                if (GUI.Button(row, GUIContent.none)) selectedHunter = unit;
                var style = agent.LifeState == RaidLifeState.Dead ? deadStyle : smallStyle;
                GUI.Label(new Rect(row.x + 4f, row.y + 1f, 108f, 17f),
                    $"{(unit.IsSergeant ? "[S] " : string.Empty)}{agent.DisplayName}", style);
                DrawBar(new Rect(row.x + 112f, row.y + 3f, 76f, 6f), agent.HealthRatio,
                    new Color(0.85f, 0.08f, 0.06f), string.Empty);
                DrawBar(new Rect(row.x + 112f, row.y + 12f, 76f, 4f),
                    agent.MaximumMana <= 0f ? 0f : agent.CurrentMana / agent.MaximumMana,
                    new Color(0.08f, 0.42f, 1f), string.Empty);
                var abilityX = row.x + 194f;
                for (var abilityIndex = 0; abilityIndex < agent.Abilities.Count; abilityIndex++)
                {
                    var ability = agent.Abilities[abilityIndex];
                    var availability = agent.GetAbilityAvailability(ability, combatTime);
                    var color = availability switch
                    {
                        RaidAbilityAvailability.Ready => new Color(0.2f, 0.88f, 0.35f),
                        RaidAbilityAvailability.Cooldown => new Color(0.48f, 0.5f, 0.54f),
                        RaidAbilityAvailability.InsufficientMana => new Color(0.12f, 0.32f, 0.85f),
                        _ => new Color(0.18f, 0.18f, 0.2f)
                    };
                    DrawSolid(new Rect(abilityX + abilityIndex * 18f, row.y + 3f, 14f, 14f), color);
                }
                rowY += 20f;
            }
        }

        private void DrawPanel(Rect rect)
        {
            var previous = GUI.color;
            GUI.color = new Color(0.018f, 0.028f, 0.045f, 0.94f);
            GUI.Box(rect, GUIContent.none, panelStyle);
            GUI.color = previous;
        }

        private static void DrawBar(Rect rect, float ratio, Color fill, string label)
        {
            DrawSolid(rect, new Color(0.08f, 0.09f, 0.11f, 0.95f));
            DrawSolid(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(ratio), rect.height), fill);
            if (!string.IsNullOrEmpty(label))
            {
                GUI.Label(new Rect(rect.x + 3f, rect.y - 3f, rect.width - 6f, rect.height + 7f), label);
            }
        }

        private static void DrawSolid(Rect rect, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private void EnsureStyles()
        {
            if (panelStyle != null) return;
            panelStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = Texture2D.whiteTexture }
            };
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.75f, 0.92f, 1f) }
            };
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            smallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                wordWrap = true,
                clipping = TextClipping.Clip,
                normal = { textColor = new Color(0.78f, 0.84f, 0.9f) }
            };
            deadStyle = new GUIStyle(smallStyle)
            {
                normal = { textColor = new Color(0.48f, 0.48f, 0.5f) }
            };
        }

        private float ResolveRoundLowFps()
        {
            return float.IsInfinity(roundLowestFps) || roundLowestFps == float.MaxValue
                ? 0f
                : roundLowestFps;
        }

#if UNITY_EDITOR
        public void ConfigureEditor(
            RaidAgent2D[] authoredHunters,
            GameObject authoredMonsterTemplate,
            Transform authoredMonsterRoot,
            DungeonRaidDirector2D resolver,
            RaidFxPool2D fxPool,
            Camera sceneCamera,
            Vector2 configuredArenaSize,
            int configuredMaximumMonsters)
        {
            hunters = authoredHunters ?? Array.Empty<RaidAgent2D>();
            monsterTemplate = authoredMonsterTemplate;
            monsterPoolRoot = authoredMonsterRoot;
            combatResolver = resolver;
            effects = fxPool;
            battleCamera = sceneCamera;
            arenaSize = configuredArenaSize;
            maximumConcurrentMonsters = Mathf.Clamp(configuredMaximumMonsters, 100, 800);
        }
#endif
    }
}
