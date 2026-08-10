using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using Turtle.DungeonRaid;

namespace Turtle.BattleScale
{
    public enum BattleScaleOrder
    {
        Advance,
        Hold,
        Flank,
        Rally
    }

    /// <summary>
    /// A rendered mass-combat laboratory that reuses RaidAgent2D hurtboxes and
    /// attacks. Commanders select intent, sergeants translate it into squad
    /// anchors and focus targets, and individual units retain local autonomy.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleScaleSimulation2D : MonoBehaviour
    {
        private sealed class SquadState
        {
            public readonly List<BattleScaleUnit2D> Members = new(12);
            public int TeamIndex;
            public int SquadIndex;
            public Vector2 StartAnchor;
            public Vector2 CommandAnchor;
            public BattleScaleOrder Order;
            public BattleScaleUnit2D Sergeant;
            public RaidAgent2D FocusTarget;
        }

        private sealed class TeamState
        {
            public readonly List<BattleScaleUnit2D> Units = new(512);
            public readonly List<SquadState> Squads = new(64);
            public readonly int TeamIndex;
            public BattleScaleOrder CommanderOrder;
            public Vector2 Centroid;
            public int Promotions;

            public TeamState(int teamIndex)
            {
                TeamIndex = teamIndex;
            }
        }

        private sealed class SpatialGrid
        {
            private List<BattleScaleUnit2D>[] buckets = Array.Empty<List<BattleScaleUnit2D>>();
            private Vector2 minimum;
            private float cellSize;
            private int columns;
            private int rows;

            public void Configure(Vector2 arenaSize, float requestedCellSize)
            {
                cellSize = Mathf.Max(1f, requestedCellSize);
                minimum = -arenaSize * 0.5f - Vector2.one * cellSize * 2f;
                var coveredSize = arenaSize + Vector2.one * cellSize * 4f;
                columns = Mathf.Max(1, Mathf.CeilToInt(coveredSize.x / cellSize));
                rows = Mathf.Max(1, Mathf.CeilToInt(coveredSize.y / cellSize));
                var required = columns * rows;
                if (buckets.Length == required) return;
                buckets = new List<BattleScaleUnit2D>[required];
                for (var index = 0; index < buckets.Length; index++)
                {
                    buckets[index] = new List<BattleScaleUnit2D>(16);
                }
            }

            public void Rebuild(IReadOnlyList<BattleScaleUnit2D> units)
            {
                for (var index = 0; index < buckets.Length; index++) buckets[index].Clear();
                for (var index = 0; index < units.Count; index++)
                {
                    var unit = units[index];
                    if (unit?.Agent == null || !unit.Agent.CanReceiveDamage) continue;
                    buckets[CellIndex(unit.Agent.Position)].Add(unit);
                }
            }

            public BattleScaleUnit2D FindNearest(Vector2 position, float radius)
            {
                BattleScaleUnit2D best = null;
                var bestDistance = radius * radius;
                ResolveCellBounds(position, radius,
                    out var minX, out var maxX, out var minY, out var maxY);
                for (var y = minY; y <= maxY; y++)
                {
                    for (var x = minX; x <= maxX; x++)
                    {
                        var bucket = buckets[x + y * columns];
                        for (var index = 0; index < bucket.Count; index++)
                        {
                            var candidate = bucket[index];
                            var distance = Vector2.SqrMagnitude(
                                candidate.Agent.Position - position);
                            if (distance >= bestDistance) continue;
                            bestDistance = distance;
                            best = candidate;
                        }
                    }
                }
                return best;
            }

            public BattleScaleUnit2D FindMostInjured(Vector2 position, float radius)
            {
                BattleScaleUnit2D best = null;
                var bestRatio = 0.78f;
                var radiusSquared = radius * radius;
                ResolveCellBounds(position, radius,
                    out var minX, out var maxX, out var minY, out var maxY);
                for (var y = minY; y <= maxY; y++)
                {
                    for (var x = minX; x <= maxX; x++)
                    {
                        var bucket = buckets[x + y * columns];
                        for (var index = 0; index < bucket.Count; index++)
                        {
                            var candidate = bucket[index];
                            if (!candidate.Agent.CanReceiveDamage ||
                                Vector2.SqrMagnitude(candidate.Agent.Position - position) >
                                radiusSquared || candidate.Agent.HealthRatio >= bestRatio)
                            {
                                continue;
                            }
                            bestRatio = candidate.Agent.HealthRatio;
                            best = candidate;
                        }
                    }
                }
                return best;
            }

            private void ResolveCellBounds(
                Vector2 position,
                float radius,
                out int minX,
                out int maxX,
                out int minY,
                out int maxY)
            {
                minX = Mathf.Clamp(
                    Mathf.FloorToInt((position.x - radius - minimum.x) / cellSize), 0, columns - 1);
                maxX = Mathf.Clamp(
                    Mathf.FloorToInt((position.x + radius - minimum.x) / cellSize), 0, columns - 1);
                minY = Mathf.Clamp(
                    Mathf.FloorToInt((position.y - radius - minimum.y) / cellSize), 0, rows - 1);
                maxY = Mathf.Clamp(
                    Mathf.FloorToInt((position.y + radius - minimum.y) / cellSize), 0, rows - 1);
            }

            private int CellIndex(Vector2 position)
            {
                var x = Mathf.Clamp(
                    Mathf.FloorToInt((position.x - minimum.x) / cellSize), 0, columns - 1);
                var y = Mathf.Clamp(
                    Mathf.FloorToInt((position.y - minimum.y) / cellSize), 0, rows - 1);
                return x + y * columns;
            }
        }

        private static readonly int[] ScalePresets = { 25, 50, 100, 200, 400, 800 };

        [Header("Scene References")]
        [SerializeField] private GameObject unitTemplate;
        [SerializeField] private Transform activeUnitRoot;
        [SerializeField] private DungeonRaidDirector2D combatResolver;
        [SerializeField] private Camera battleCamera;

        [Header("Scenario")]
        [SerializeField] private Vector2 arenaSize = new(104f, 56f);
        [SerializeField, Range(1, 800)] private int unitsPerTeam = 100;
        [SerializeField, Range(4, 20)] private int squadSize = 10;
        [SerializeField, Range(25, 800)] private int maximumUnitsPerTeam = 800;
        [SerializeField] private bool beginAutomatically = true;
        [SerializeField] private bool useScalePhysics = true;

        [Header("Decision Budgets")]
        [SerializeField, Min(0.05f)] private float spatialRebuildInterval = 0.14f;
        [SerializeField, Min(0.1f)] private float commanderDecisionInterval = 0.72f;
        [SerializeField, Range(8, 512)] private int maximumUnitDecisionsPerFrame = 128;
        [SerializeField, Min(10f)] private float targetFrameRate = 30f;

        private readonly List<BattleScaleUnit2D> pooledUnits = new(1600);
        private readonly List<BattleScaleUnit2D> activeUnits = new(1600);
        private readonly TeamState[] teams = { new(0), new(1) };
        private readonly SpatialGrid[] spatialGrids = { new(), new() };
        private readonly List<RaidAbilitySpec> noAbilities = new(0);

        private float battleTime;
        private float spatialAccumulator;
        private float commanderAccumulator;
        private int decisionCursor;
        private bool battleRunning;
        private string battleStatus = "Waiting to deploy armies.";
        private long attackAttempts;
        private long confirmedHits;
        private long supportCasts;
        private long commanderDecisions;
        private int unitDecisionsThisSample;
        private int attacksThisSample;
        private int hitsThisSample;
        private int displayedDecisionsPerSecond;
        private int displayedAttacksPerSecond;
        private int displayedHitsPerSecond;
        private float sampleElapsed;
        private int sampleFrames;
        private float displayedFps;
        private float displayedFrameMilliseconds;

        private bool automaticBenchmark;
        private int benchmarkPresetIndex;
        private int lastStableTotalUnits;
        private float benchmarkStageStartedAt;
        private float benchmarkLowestFps;
        private string benchmarkStatus = "Auto benchmark idle.";

        private GUIStyle panelStyle;
        private GUIStyle titleStyle;
        private GUIStyle labelStyle;
        private GUIStyle smallStyle;

        public int UnitsPerTeam => unitsPerTeam;
        public int ActiveUnitCount => activeUnits.Count;
        public int ActiveSquadCount => teams[0].Squads.Count + teams[1].Squads.Count;
        public int ActiveSergeantCount => CountActiveSergeants();
        public int LivingAzure => CountLiving(teams[0]);
        public int LivingCrimson => CountLiving(teams[1]);
        public long AttackAttempts => attackAttempts;
        public long ConfirmedHits => confirmedHits;
        public long CommanderDecisions => commanderDecisions;
        public float BattleTime => battleTime;
        public bool BattleRunning => battleRunning;
        public string BattleStatus => battleStatus;
        public bool IsConfigured => unitTemplate != null && activeUnitRoot != null &&
                                    combatResolver != null && battleCamera != null;
        public Vector2 ArenaSize => arenaSize;

        private void Start()
        {
            if (unitTemplate != null) unitTemplate.SetActive(false);
            for (var index = 0; index < spatialGrids.Length; index++)
            {
                spatialGrids[index].Configure(arenaSize, 4f);
            }
            if (beginAutomatically) RebuildBattle(unitsPerTeam);
        }

        private void Update()
        {
            SampleFrameRate();
            if (battleRunning)
            {
                var deltaTime = Mathf.Min(0.05f, Time.deltaTime);
                battleTime += deltaTime;
                StepCombatants(deltaTime);
                spatialAccumulator += deltaTime;
                if (spatialAccumulator >= spatialRebuildInterval)
                {
                    spatialAccumulator -= spatialRebuildInterval;
                    RebuildSpatialGrids();
                }
                commanderAccumulator += deltaTime;
                if (commanderAccumulator >= commanderDecisionInterval)
                {
                    commanderAccumulator -= commanderDecisionInterval;
                    UpdateCommanders();
                }
                RunDecisionBudget();
                EvaluateOutcome();
            }
            UpdateAutomaticBenchmark();
        }

        public void RebuildBattle(int requestedUnitsPerTeam)
        {
            unitsPerTeam = Mathf.Clamp(requestedUnitsPerTeam, 1, maximumUnitsPerTeam);
            DeactivatePool();
            EnsurePoolCapacity(unitsPerTeam * 2);
            ResetTeam(teams[0]);
            ResetTeam(teams[1]);
            activeUnits.Clear();
            SpawnTeam(teams[0], unitsPerTeam);
            SpawnTeam(teams[1], unitsPerTeam);
            battleTime = 0f;
            spatialAccumulator = spatialRebuildInterval;
            commanderAccumulator = commanderDecisionInterval;
            decisionCursor = 0;
            battleRunning = true;
            attackAttempts = 0;
            confirmedHits = 0;
            supportCasts = 0;
            commanderDecisions = 0;
            displayedDecisionsPerSecond = 0;
            displayedAttacksPerSecond = 0;
            displayedHitsPerSecond = 0;
            RebuildSpatialGrids();
            UpdateCommanders();
            battleStatus =
                $"{unitsPerTeam}v{unitsPerTeam} deployed in {ActiveSquadCount} autonomous squads.";
        }

        public void BeginAutomaticBenchmark()
        {
            automaticBenchmark = true;
            benchmarkPresetIndex = 0;
            lastStableTotalUnits = 0;
            benchmarkStatus = "Auto benchmark warming up at 25v25.";
            RebuildBattle(ScalePresets[benchmarkPresetIndex]);
            BeginBenchmarkStage();
        }

        private void StopAutomaticBenchmark(string reason)
        {
            automaticBenchmark = false;
            benchmarkStatus = reason;
        }

        private void BeginBenchmarkStage()
        {
            benchmarkStageStartedAt = Time.unscaledTime;
            benchmarkLowestFps = float.MaxValue;
        }

        private void UpdateAutomaticBenchmark()
        {
            if (!automaticBenchmark) return;
            var elapsed = Time.unscaledTime - benchmarkStageStartedAt;
            if (elapsed >= 3f && displayedFps > 0f)
            {
                benchmarkLowestFps = Mathf.Min(benchmarkLowestFps, displayedFps);
            }
            if (elapsed < 12f) return;

            var passed = benchmarkLowestFps >= targetFrameRate;
            if (passed) lastStableTotalUnits = unitsPerTeam * 2;
            if (!passed)
            {
                StopAutomaticBenchmark(
                    $"Stopped at {unitsPerTeam * 2} total units: " +
                    $"lowest sampled FPS {benchmarkLowestFps:0.0}. " +
                    $"Last stable total: {lastStableTotalUnits}.");
                return;
            }
            benchmarkPresetIndex++;
            if (benchmarkPresetIndex >= ScalePresets.Length)
            {
                StopAutomaticBenchmark(
                    $"All presets passed. Last stable total: {lastStableTotalUnits} units.");
                return;
            }
            var next = ScalePresets[benchmarkPresetIndex];
            benchmarkStatus =
                $"{unitsPerTeam * 2} total passed; warming up {next}v{next}.";
            RebuildBattle(next);
            BeginBenchmarkStage();
        }

        private void StepCombatants(float deltaTime)
        {
            for (var index = 0; index < activeUnits.Count; index++)
            {
                activeUnits[index].Agent.Step(deltaTime, battleTime, combatResolver);
            }
        }

        private void RunDecisionBudget()
        {
            if (activeUnits.Count == 0) return;
            var inspected = 0;
            var decisions = 0;
            while (inspected < activeUnits.Count &&
                   decisions < maximumUnitDecisionsPerFrame)
            {
                if (decisionCursor >= activeUnits.Count) decisionCursor = 0;
                var unit = activeUnits[decisionCursor++];
                inspected++;
                if (unit?.Agent == null || !unit.Agent.CanAct ||
                    battleTime < unit.NextDecisionAt)
                {
                    continue;
                }
                DecideUnit(unit);
                unit.NextDecisionAt = battleTime +
                                      Mathf.Lerp(0.12f, 0.22f, 1f - unit.Discipline);
                decisions++;
            }
            unitDecisionsThisSample += decisions;
        }

        private void DecideUnit(BattleScaleUnit2D unit)
        {
            var agent = unit.Agent;
            var ownTeam = teams[unit.TeamIndex];
            var opposingGrid = spatialGrids[1 - unit.TeamIndex];
            var ownGrid = spatialGrids[unit.TeamIndex];
            var squad = ownTeam.Squads[unit.SquadIndex];

            if (agent.Role == RaidCombatRole.Healer && battleTime >= unit.NextSupportAt)
            {
                var injured = ownGrid.FindMostInjured(agent.Position, 5.5f);
                if (injured != null)
                {
                    injured.Agent.Heal(14f, battleTime, combatResolver);
                    unit.NextSupportAt = battleTime + 4.5f;
                    supportCasts++;
                    return;
                }
            }

            var localTarget = opposingGrid.FindNearest(agent.Position, 14f);
            var focus = squad.FocusTarget;
            var focusValid = focus != null && focus.CanReceiveDamage &&
                             Vector2.Distance(agent.Position, focus.Position) <= 18f;
            RaidAgent2D target = focusValid ? focus : localTarget?.Agent;
            if (focusValid && localTarget != null)
            {
                var focusDistance = Vector2.SqrMagnitude(focus.Position - agent.Position);
                var localDistance = Vector2.SqrMagnitude(localTarget.Agent.Position - agent.Position);
                if (localDistance < focusDistance * Mathf.Lerp(0.45f, 0.82f, unit.Discipline))
                {
                    target = localTarget.Agent;
                }
            }
            unit.CurrentTarget = target;

            if (target != null)
            {
                var distance = Vector2.Distance(agent.Position, target.Position);
                if (agent.CanBasicAttack(target, battleTime))
                {
                    attackAttempts++;
                    attacksThisSample++;
                    if (agent.TryBasicAttack(target, battleTime, combatResolver))
                    {
                        confirmedHits++;
                        hitsThisSample++;
                    }
                    return;
                }
                var chaseLimit = Mathf.Lerp(18f, 9f, unit.Discipline);
                if (distance <= chaseLimit)
                {
                    agent.MoveToward(target.Position, agent.PreferredCombatRange * 0.82f);
                    return;
                }
            }

            var formationPosition = ResolveFormationPosition(unit, squad);
            agent.MoveToward(formationPosition, 0.28f + (1f - unit.Discipline) * 0.45f);
        }

        private static Vector2 ResolveFormationPosition(
            BattleScaleUnit2D unit,
            SquadState squad)
        {
            var orderedPosition = squad.CommandAnchor + unit.FormationOffset;
            if (unit.IsSergeant || squad.Sergeant?.Agent == null ||
                !squad.Sergeant.Agent.CanAct)
            {
                return orderedPosition;
            }
            var responsivePosition = squad.Sergeant.Agent.Position + unit.FormationOffset;
            return Vector2.Lerp(responsivePosition, orderedPosition, unit.Discipline);
        }

        private void UpdateCommanders()
        {
            RebuildSpatialGrids();
            teams[0].Centroid = CalculateCentroid(teams[0]);
            teams[1].Centroid = CalculateCentroid(teams[1]);
            UpdateCommander(teams[0], teams[1]);
            UpdateCommander(teams[1], teams[0]);
            commanderDecisions += 2;
        }

        private void UpdateCommander(TeamState team, TeamState enemy)
        {
            var living = CountLiving(team);
            var enemyLiving = CountLiving(enemy);
            var forceRatio = living / (float)Mathf.Max(1, enemyLiving);
            if (living <= Mathf.Max(2, unitsPerTeam / 5))
            {
                team.CommanderOrder = BattleScaleOrder.Rally;
            }
            else if (forceRatio < 0.67f)
            {
                team.CommanderOrder = BattleScaleOrder.Hold;
            }
            else
            {
                var phase = (Mathf.FloorToInt(battleTime / 8f) + team.TeamIndex) % 3;
                team.CommanderOrder = phase switch
                {
                    1 => BattleScaleOrder.Flank,
                    2 => BattleScaleOrder.Hold,
                    _ => BattleScaleOrder.Advance
                };
            }

            for (var index = 0; index < team.Squads.Count; index++)
            {
                var squad = team.Squads[index];
                PromoteSergeantIfNeeded(team, squad);
                squad.Order = team.CommanderOrder;
                squad.CommandAnchor = ResolveSquadAnchor(team, enemy, squad);
                squad.FocusTarget = squad.Sergeant?.Agent != null && squad.Sergeant.Agent.CanAct
                    ? spatialGrids[enemy.TeamIndex]
                        .FindNearest(squad.Sergeant.Agent.Position, 22f)?.Agent
                    : null;
                ApplySergeantAura(squad);
            }
        }

        private Vector2 ResolveSquadAnchor(
            TeamState team,
            TeamState enemy,
            SquadState squad)
        {
            var direction = team.TeamIndex == 0 ? 1f : -1f;
            var squadCount = Mathf.Max(1, team.Squads.Count);
            var lane = squad.SquadIndex - (squadCount - 1) * 0.5f;
            var laneSpacing = Mathf.Min(5.2f, (arenaSize.y - 10f) / squadCount);
            var laneY = lane * laneSpacing;
            Vector2 anchor;
            switch (team.CommanderOrder)
            {
                case BattleScaleOrder.Hold:
                    anchor = squad.StartAnchor + Vector2.right * direction * arenaSize.x * 0.16f;
                    anchor.y = laneY;
                    break;
                case BattleScaleOrder.Flank:
                    var side = (squad.SquadIndex & 1) == 0 ? 1f : -1f;
                    anchor = enemy.Centroid + new Vector2(
                        -direction * 5.5f,
                        side * (arenaSize.y * 0.28f - (squad.SquadIndex % 3) * 2f));
                    break;
                case BattleScaleOrder.Rally:
                    anchor = squad.StartAnchor - Vector2.right * direction * 3f;
                    anchor.y = laneY * 0.72f;
                    break;
                default:
                    anchor = Vector2.Lerp(team.Centroid, enemy.Centroid, 0.64f);
                    anchor.y = Mathf.Lerp(anchor.y, laneY, 0.68f);
                    break;
            }
            var half = arenaSize * 0.5f - new Vector2(2f, 2f);
            return new Vector2(
                Mathf.Clamp(anchor.x, -half.x, half.x),
                Mathf.Clamp(anchor.y, -half.y, half.y));
        }

        private void PromoteSergeantIfNeeded(TeamState team, SquadState squad)
        {
            if (squad.Sergeant?.Agent != null && squad.Sergeant.Agent.CanReceiveDamage) return;
            if (squad.Sergeant != null) squad.Sergeant.SetSergeant(false);
            squad.Sergeant = null;
            for (var index = 0; index < squad.Members.Count; index++)
            {
                var candidate = squad.Members[index];
                if (candidate?.Agent == null || !candidate.Agent.CanReceiveDamage) continue;
                candidate.SetSergeant(true);
                squad.Sergeant = candidate;
                team.Promotions++;
                break;
            }
        }

        private void ApplySergeantAura(SquadState squad)
        {
            if (squad.Sergeant?.Agent == null || !squad.Sergeant.Agent.CanAct) return;
            var center = squad.Sergeant.Agent.Position;
            for (var index = 0; index < squad.Members.Count; index++)
            {
                var member = squad.Members[index]?.Agent;
                if (member == null || !member.CanAct ||
                    Vector2.SqrMagnitude(member.Position - center) > 20.25f)
                {
                    continue;
                }
                member.GrantDamageBuff(1.06f, battleTime + commanderDecisionInterval + 0.1f);
            }
        }

        private void EvaluateOutcome()
        {
            var azure = CountLiving(teams[0]);
            var crimson = CountLiving(teams[1]);
            if (azure > 0 && crimson > 0) return;
            battleRunning = false;
            for (var index = 0; index < activeUnits.Count; index++)
            {
                activeUnits[index].Agent.StopMoving();
            }
            battleStatus = azure == crimson
                ? "Mutual destruction."
                : azure > crimson
                    ? $"Azure victory with {azure} survivors."
                    : $"Crimson victory with {crimson} survivors.";
        }

        private void RebuildSpatialGrids()
        {
            spatialGrids[0].Rebuild(teams[0].Units);
            spatialGrids[1].Rebuild(teams[1].Units);
        }

        private void SpawnTeam(TeamState team, int count)
        {
            var squadCount = Mathf.CeilToInt(count / (float)squadSize);
            var teamDirection = team.TeamIndex == 0 ? 1f : -1f;
            var deploymentX = -teamDirection * arenaSize.x * 0.38f;
            var poolOffset = team.TeamIndex == 0 ? 0 : unitsPerTeam;
            var spawned = 0;
            for (var squadIndex = 0; squadIndex < squadCount; squadIndex++)
            {
                var squad = new SquadState
                {
                    TeamIndex = team.TeamIndex,
                    SquadIndex = squadIndex,
                    Order = BattleScaleOrder.Advance
                };
                var normalizedY = squadCount <= 1
                    ? 0f
                    : squadIndex / (float)(squadCount - 1) - 0.5f;
                squad.StartAnchor = new Vector2(
                    deploymentX,
                    normalizedY * (arenaSize.y - 9f));
                squad.CommandAnchor = squad.StartAnchor;
                team.Squads.Add(squad);
                var members = Mathf.Min(squadSize, count - spawned);
                var columns = Mathf.CeilToInt(Mathf.Sqrt(members));
                for (var memberIndex = 0; memberIndex < members; memberIndex++)
                {
                    var column = memberIndex % columns;
                    var row = memberIndex / columns;
                    var offset = new Vector2(
                        (column - (columns - 1) * 0.5f) * 0.86f * teamDirection,
                        (row - 1f) * 0.82f);
                    var unit = pooledUnits[poolOffset + spawned];
                    ConfigurePooledUnit(unit, team, squad, memberIndex, offset, spawned);
                    spawned++;
                }
                squad.Sergeant = squad.Members.Count > 0 ? squad.Members[0] : null;
            }
        }

        private void ConfigurePooledUnit(
            BattleScaleUnit2D unit,
            TeamState team,
            SquadState squad,
            int memberIndex,
            Vector2 formationOffset,
            int teamUnitIndex)
        {
            var role = RoleFor(memberIndex);
            ResolveRoleStats(role, out var health, out var mana, out var speed,
                out var damage, out var range, out var preferredRange,
                out var cooldown, out var ranged);
            var isSergeant = memberIndex == 0;
            if (isSergeant)
            {
                health *= 1.28f;
                damage *= 1.14f;
            }
            var teamName = team.TeamIndex == 0 ? "Azure" : "Crimson";
            var faction = team.TeamIndex == 0 ? RaidFaction.Hunters : RaidFaction.Monsters;
            var color = UnitColor(team.TeamIndex, role, isSergeant);
            var position = squad.StartAnchor + formationOffset;
            unit.gameObject.SetActive(false);
            unit.transform.position = new Vector3(position.x, position.y, 0f);
            unit.transform.localScale = Vector3.one * (isSergeant ? 0.68f : 0.54f);
            unit.Agent.ConfigureRuntime(
                $"scale-{team.TeamIndex}-{teamUnitIndex:0000}",
                $"{teamName} S{squad.SquadIndex + 1:00}-{memberIndex + 1:00}",
                faction,
                role,
                health,
                mana,
                5f,
                speed,
                damage,
                range,
                preferredRange,
                cooldown,
                ranged,
                color,
                noAbilities,
                0.38f,
                true,
                useScalePhysics,
                useScalePhysics,
                false);
            unit.Configure(
                team.TeamIndex,
                squad.SquadIndex,
                memberIndex,
                formationOffset,
                (teamUnitIndex % 17) * 0.011f);
            unit.gameObject.name = $"{teamName} Squad {squad.SquadIndex + 1:00} Unit {memberIndex + 1:00}";
            unit.gameObject.SetActive(true);
            unit.Agent.CaptureSpawnPosition();
            unit.Agent.ResetForRaid();
            team.Units.Add(unit);
            squad.Members.Add(unit);
            activeUnits.Add(unit);
        }

        private void EnsurePoolCapacity(int required)
        {
            if (unitTemplate == null || activeUnitRoot == null) return;
            while (pooledUnits.Count < required)
            {
                var clone = Instantiate(unitTemplate, activeUnitRoot);
                clone.name = $"Pooled Battle Unit {pooledUnits.Count + 1:0000}";
                clone.SetActive(false);
                var unit = clone.GetComponent<BattleScaleUnit2D>();
                if (unit == null) unit = clone.AddComponent<BattleScaleUnit2D>();
                pooledUnits.Add(unit);
            }
        }

        private void DeactivatePool()
        {
            for (var index = 0; index < pooledUnits.Count; index++)
            {
                if (pooledUnits[index] != null) pooledUnits[index].gameObject.SetActive(false);
            }
        }

        private static void ResetTeam(TeamState team)
        {
            team.Units.Clear();
            team.Squads.Clear();
            team.Centroid = Vector2.zero;
            team.CommanderOrder = BattleScaleOrder.Advance;
            team.Promotions = 0;
        }

        private static int CountLiving(TeamState team)
        {
            var count = 0;
            for (var index = 0; index < team.Units.Count; index++)
            {
                if (team.Units[index]?.Agent != null &&
                    team.Units[index].Agent.CanReceiveDamage)
                {
                    count++;
                }
            }
            return count;
        }

        private int CountActiveSergeants()
        {
            var count = 0;
            for (var teamIndex = 0; teamIndex < teams.Length; teamIndex++)
            {
                var squads = teams[teamIndex].Squads;
                for (var squadIndex = 0; squadIndex < squads.Count; squadIndex++)
                {
                    if (squads[squadIndex].Sergeant?.Agent != null &&
                        squads[squadIndex].Sergeant.Agent.CanReceiveDamage)
                    {
                        count++;
                    }
                }
            }
            return count;
        }

        private static Vector2 CalculateCentroid(TeamState team)
        {
            var sum = Vector2.zero;
            var count = 0;
            for (var index = 0; index < team.Units.Count; index++)
            {
                var agent = team.Units[index]?.Agent;
                if (agent == null || !agent.CanReceiveDamage) continue;
                sum += agent.Position;
                count++;
            }
            return count > 0 ? sum / count : Vector2.zero;
        }

        private static RaidCombatRole RoleFor(int memberIndex)
        {
            if (memberIndex == 0) return RaidCombatRole.Fighter;
            return (memberIndex % 6) switch
            {
                1 => RaidCombatRole.Tank,
                2 => RaidCombatRole.Healer,
                3 => RaidCombatRole.Mage,
                4 => RaidCombatRole.Ranger,
                _ => RaidCombatRole.Assassin
            };
        }

        private static void ResolveRoleStats(
            RaidCombatRole role,
            out float health,
            out float mana,
            out float speed,
            out float damage,
            out float range,
            out float preferredRange,
            out float cooldown,
            out bool ranged)
        {
            health = 100f;
            mana = 50f;
            speed = 4.2f;
            damage = 11f;
            range = 1.35f;
            preferredRange = 1.05f;
            cooldown = 0.9f;
            ranged = false;
            switch (role)
            {
                case RaidCombatRole.Tank:
                    health = 138f;
                    speed = 3.55f;
                    damage = 8f;
                    cooldown = 1.05f;
                    break;
                case RaidCombatRole.Healer:
                    health = 86f;
                    mana = 100f;
                    damage = 7f;
                    speed = 4f;
                    break;
                case RaidCombatRole.Mage:
                    health = 76f;
                    mana = 120f;
                    damage = 10f;
                    range = 5.4f;
                    preferredRange = 4.5f;
                    cooldown = 1.22f;
                    ranged = true;
                    break;
                case RaidCombatRole.Ranger:
                    health = 82f;
                    damage = 9f;
                    range = 6.1f;
                    preferredRange = 5.1f;
                    cooldown = 1.02f;
                    ranged = true;
                    break;
                case RaidCombatRole.Assassin:
                    health = 84f;
                    speed = 4.9f;
                    damage = 13f;
                    cooldown = 0.72f;
                    break;
            }
        }

        private static Color UnitColor(int teamIndex, RaidCombatRole role, bool sergeant)
        {
            var teamColor = teamIndex == 0
                ? new Color(0.08f, 0.55f, 1f)
                : new Color(1f, 0.16f, 0.11f);
            var roleAccent = role switch
            {
                RaidCombatRole.Tank => new Color(0.78f, 0.9f, 1f),
                RaidCombatRole.Healer => new Color(0.2f, 1f, 0.48f),
                RaidCombatRole.Mage => new Color(0.7f, 0.28f, 1f),
                RaidCombatRole.Ranger => new Color(1f, 0.86f, 0.18f),
                RaidCombatRole.Assassin => new Color(1f, 0.18f, 0.62f),
                _ => new Color(1f, 0.62f, 0.2f)
            };
            var color = Color.Lerp(teamColor, roleAccent, 0.26f);
            return sergeant ? Color.Lerp(color, Color.white, 0.28f) : color;
        }

        private void SampleFrameRate()
        {
            var delta = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
            sampleElapsed += delta;
            sampleFrames++;
            if (sampleElapsed < 1f) return;
            displayedFps = sampleFrames / sampleElapsed;
            displayedFrameMilliseconds = sampleElapsed * 1000f / sampleFrames;
            displayedDecisionsPerSecond = Mathf.RoundToInt(unitDecisionsThisSample / sampleElapsed);
            displayedAttacksPerSecond = Mathf.RoundToInt(attacksThisSample / sampleElapsed);
            displayedHitsPerSecond = Mathf.RoundToInt(hitsThisSample / sampleElapsed);
            sampleElapsed = 0f;
            sampleFrames = 0;
            unitDecisionsThisSample = 0;
            attacksThisSample = 0;
            hitsThisSample = 0;
        }

        private void OnGUI()
        {
            EnsureStyles();
            var panel = new Rect(12f, 12f, 448f, 326f);
            var previousColor = GUI.color;
            GUI.color = new Color(0.02f, 0.035f, 0.05f, 0.95f);
            GUI.Box(panel, GUIContent.none, panelStyle);
            GUI.color = previousColor;
            GUI.Label(new Rect(26f, 20f, 420f, 25f), "BATTLE SCALE LAB", titleStyle);

            const float buttonWidth = 64f;
            const float gap = 4f;
            for (var index = 0; index < ScalePresets.Length; index++)
            {
                var preset = ScalePresets[index];
                var rect = new Rect(24f + index * (buttonWidth + gap), 50f, buttonWidth, 26f);
                if (GUI.Button(rect, $"{preset}v{preset}"))
                {
                    automaticBenchmark = false;
                    benchmarkStatus = "Manual scale selected.";
                    RebuildBattle(preset);
                }
            }

            if (GUI.Button(new Rect(24f, 82f, 132f, 27f),
                    automaticBenchmark ? "STOP AUTO TEST" : "AUTO LIMIT TEST"))
            {
                if (automaticBenchmark)
                {
                    StopAutomaticBenchmark(
                        $"Auto benchmark stopped. Last stable total: {lastStableTotalUnits}.");
                }
                else
                {
                    BeginAutomaticBenchmark();
                }
            }
            if (GUI.Button(new Rect(162f, 82f, 90f, 27f), "RESET"))
            {
                RebuildBattle(unitsPerTeam);
            }
            if (GUI.Button(new Rect(258f, 82f, 174f, 27f),
                    useScalePhysics ? "PHYSICS: SCALE LOD" : "PHYSICS: FULL"))
            {
                useScalePhysics = !useScalePhysics;
                RebuildBattle(unitsPerTeam);
            }

            var memoryMb = Profiler.GetTotalAllocatedMemoryLong() / (1024f * 1024f);
            GUI.Label(new Rect(26f, 118f, 408f, 20f),
                $"Rendered: {activeUnits.Count}   Living: {LivingAzure} Azure / {LivingCrimson} Crimson",
                labelStyle);
            GUI.Label(new Rect(26f, 139f, 408f, 20f),
                $"FPS: {displayedFps:0.0}   Frame: {displayedFrameMilliseconds:0.0} ms   Memory: {memoryMb:0} MB",
                labelStyle);
            GUI.Label(new Rect(26f, 160f, 408f, 20f),
                $"AI decisions/s: {displayedDecisionsPerSecond}   Attacks/s: {displayedAttacksPerSecond}   Hits/s: {displayedHitsPerSecond}",
                labelStyle);
            GUI.Label(new Rect(26f, 181f, 408f, 20f),
                $"Squads: {ActiveSquadCount}   Commands: {commanderDecisions}   Sergeant promotions: {teams[0].Promotions + teams[1].Promotions}",
                labelStyle);
            GUI.Label(new Rect(26f, 202f, 408f, 20f),
                $"Azure commander: {teams[0].CommanderOrder}   Crimson commander: {teams[1].CommanderOrder}",
                labelStyle);
            GUI.Label(new Rect(26f, 226f, 408f, 36f), battleStatus, smallStyle);
            GUI.Label(new Rect(26f, 260f, 408f, 44f), benchmarkStatus, smallStyle);
            GUI.Label(new Rect(26f, 303f, 408f, 18f),
                "Gold markers are squad sergeants. FPS is an Editor-local diagnostic, not a shipped-build benchmark.",
                smallStyle);
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
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.72f, 0.92f, 1f) }
            };
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                normal = { textColor = Color.white }
            };
            smallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                wordWrap = true,
                normal = { textColor = new Color(0.72f, 0.8f, 0.86f) }
            };
        }

#if UNITY_EDITOR
        public void ConfigureEditor(
            GameObject template,
            Transform unitRoot,
            DungeonRaidDirector2D resolver,
            Camera sceneCamera,
            Vector2 configuredArenaSize,
            int defaultUnitsPerTeam)
        {
            unitTemplate = template;
            activeUnitRoot = unitRoot;
            combatResolver = resolver;
            battleCamera = sceneCamera;
            arenaSize = configuredArenaSize;
            unitsPerTeam = Mathf.Clamp(defaultUnitsPerTeam, 1, maximumUnitsPerTeam);
            beginAutomatically = true;
            useScalePhysics = true;
        }
#endif
    }
}
