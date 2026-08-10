using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using Turtle.BattleSurvival;
using Turtle.DungeonRaid;

namespace Turtle.BattleArena3D
{
    [DisallowMultipleComponent]
    public sealed class BattleArena3DDirector : MonoBehaviour
    {
        [Serializable]
        private sealed class SquadState
        {
            public string Name;
            public Color Color;
            public readonly List<BattleArena3DUnit> Members = new(10);
            public BattleArena3DUnit Sergeant;
            public BattleArenaSquadOrder3D Order;
            public Vector3 Anchor;
            public float AverageHealth;
        }

        private readonly struct FallenMonster
        {
            public FallenMonster(BattleArena3DUnit unit, float returnAt)
            {
                Unit = unit;
                ReturnAt = returnAt;
            }

            public BattleArena3DUnit Unit { get; }
            public float ReturnAt { get; }
        }

        private sealed class PersistentField
        {
            public BattleArena3DUnit Source;
            public Vector3 Position;
            public float Radius;
            public float Power;
            public float NextTick;
            public float ExpiresAt;
            public bool Healing;
            public Color Color;
        }

        [Header("Authored Scene References")]
        [SerializeField] private BattleArena3DUnit[] hunters = Array.Empty<BattleArena3DUnit>();
        [SerializeField] private BattleArena3DUnit monsterTemplate;
        [SerializeField] private Transform activeMonsterRoot;
        [SerializeField] private Transform[] hordePortals = Array.Empty<Transform>();
        [SerializeField] private Transform[] squadRallyPoints = Array.Empty<Transform>();
        [SerializeField] private BattleArena3DVfxPool vfxPool;
        [SerializeField] private Camera battleCamera;
        [SerializeField] private BattleArena3DPresentationController presentationController;
        [SerializeField] private BattleArena3DCombatFeedback combatFeedback;

        [Header("Population")]
        [SerializeField, Range(220, 500)] private int monsterPoolCapacity = 360;
        [SerializeField, Range(200, 320)] private int firstRoundQuota = 220;
        [SerializeField, Range(200, 320)] private int firstRoundConcurrentLimit = 220;
        [SerializeField, Range(10, 160)] private int roundQuotaGrowth = 70;
        [SerializeField, Range(0, 40)] private int roundConcurrentGrowth = 20;
        [SerializeField, Range(1, 32)] private int prewarmBatchSize = 20;
        [SerializeField, Range(1, 24)] private int spawnBatchSize = 10;
        [SerializeField, Range(0.01f, 0.25f)] private float spawnInterval = 0.04f;

        [Header("Round Scaling")]
        [SerializeField, Min(1f)] private float healthMultiplierPerRound = 1.16f;
        [SerializeField, Min(1f)] private float damageMultiplierPerRound = 1.075f;
        [SerializeField, Range(0f, 0.2f)] private float speedGrowthPerRound = 0.025f;
        [SerializeField, Range(0.5f, 10f)] private float intermissionSeconds = 3f;
        [SerializeField, Range(0.03f, 0.5f)] private float decisionInterval = 0.16f;
        [SerializeField, Range(8, 96)] private int decisionsPerFrame = 42;
        [SerializeField, Range(0.2f, 3f)] private float commanderInterval = 0.85f;

        [Header("Presentation")]
        [SerializeField] private bool beginAutomatically = true;
        [SerializeField] private bool showHud = true;
        [SerializeField] private float startingTimeScale = 1f;

        private readonly SquadState[] squads =
        {
            new() { Name = "Aegis", Color = new Color(0.1f, 0.6f, 1f) },
            new() { Name = "Ember", Color = new Color(1f, 0.28f, 0.08f) },
            new() { Name = "Vanguard", Color = new Color(0.66f, 0.25f, 1f) }
        };
        private readonly List<BattleArena3DUnit> allMonsterUnits = new(384);
        private readonly Queue<BattleArena3DUnit> availableMonsters = new(384);
        private readonly List<BattleArena3DUnit> activeMonsters = new(384);
        private readonly List<FallenMonster> fallenMonsters = new(256);
        private readonly List<PersistentField> persistentFields = new(64);
        private readonly Dictionary<BattleArenaMonsterArchetype3D, IReadOnlyList<RaidAbilitySpec>>
            monsterAbilitySets = new();
        private readonly Collider[] contactBuffer = new Collider[256];
        private readonly RaycastHit[] lineHitBuffer = new RaycastHit[192];
        private Vector3[] hunterSpawnPositions = Array.Empty<Vector3>();
        private Quaternion[] hunterSpawnRotations = Array.Empty<Quaternion>();
        private readonly float[] portalPressure = new float[4];
        private readonly bool[] assignedPortals = new bool[4];
        private BattleArenaPhase3D phase = BattleArenaPhase3D.Prewarming;
        private BattleArena3DUnit selectedUnit;
        private int currentRound;
        private int spawnedThisRound;
        private int defeatedThisRound;
        private int roundQuota;
        private int concurrentLimit;
        private int stressQuotaBonus;
        private int stressConcurrentBonus;
        private int pendingStressSpawns;
        private int spawnSerial;
        private int decisionCursor;
        private float combatTime;
        private float phaseEndsAt;
        private float spawnAccumulator;
        private float decisionAccumulator;
        private float commanderAccumulator;
        private float barFacingAccumulator;
        private string latestEvent = "Preparing the 3D battle simulation.";
        private int attackAttempts;
        private int confirmedHits;
        private int abilityCasts;
        private int areaAbilityCasts;
        private int statusApplications;
        private int commanderDecisions;
        private int peakConcurrentMonsters;
        private int peakCombatants;
        private int decisionsThisSample;
        private int attacksThisSample;
        private int hitsThisSample;
        private int abilitiesThisSample;
        private int displayedDecisionsPerSecond;
        private int displayedAttacksPerSecond;
        private int displayedHitsPerSecond;
        private int displayedAbilitiesPerSecond;
        private int sampleFrames;
        private float sampleUnscaledElapsed;
        private float displayedFps;
        private float displayedFrameMilliseconds;
        private float guiWidth;
        private float guiHeight;
        private float currentHudScale = 1f;
        private bool compactHud;
        private int appliedPresentationRevision = -1;
        private GUIStyle titleStyle;
        private GUIStyle labelStyle;
        private GUIStyle smallStyle;
        private GUIStyle deadStyle;
        private Texture2D panelTexture;
        private Texture2D barBackgroundTexture;
        private Texture2D pixelTexture;

        public bool IsConfigured => hunters is { Length: BattleSurvivalHunterCatalog.HunterCount } &&
                                    monsterTemplate != null && activeMonsterRoot != null &&
                                    hordePortals is { Length: 4 } && squadRallyPoints is { Length: 3 } &&
                                    vfxPool != null && vfxPool.IsConfigured && battleCamera != null &&
                                    presentationController != null && presentationController.IsConfigured &&
                                    combatFeedback != null && combatFeedback.IsConfigured;
        public BattleArenaPhase3D Phase => phase;
        public int CurrentRound => currentRound;
        public float CombatTime => combatTime;
        public int HunterCount => hunters.Length;
        public int LivingHunterCount => CountLivingHunters();
        public int ActiveMonsterCount => activeMonsters.Count;
        public int PeakConcurrentMonsterCount => peakConcurrentMonsters;
        public int PeakCombatantCount => peakCombatants;
        public int AttackAttempts => attackAttempts;
        public int ConfirmedHits => confirmedHits;
        public int AbilityCasts => abilityCasts;
        public int AreaAbilityCasts => areaAbilityCasts;
        public int StatusApplications => statusApplications;
        public int CommanderDecisions => commanderDecisions;
        public int TelegraphEmissions => vfxPool != null ? vfxPool.TelegraphEmissionCount : 0;
        public int DroppedTelegraphs => vfxPool != null ? vfxPool.DroppedTelegraphCount : 0;
        public int ActiveProjectileCount => vfxPool != null ? vfxPool.ActiveProjectileCount : 0;
        public int ActiveTelegraphCount => vfxPool != null ? vfxPool.ActiveTelegraphCount : 0;
        public int FeedbackEventCount => combatFeedback != null ? combatFeedback.FeedbackEventCount : 0;
        public int CameraImpulseCount => combatFeedback != null ? combatFeedback.CameraImpulseCount : 0;
        public int DamageLabelEmissionCount => combatFeedback != null ? combatFeedback.DamageLabelEmissionCount : 0;
        public int DeathBurstCount => combatFeedback != null ? combatFeedback.DeathBurstCount : 0;
        public int ShieldContactCount => combatFeedback != null ? combatFeedback.ShieldContactCount : 0;
        public BattleArena3DUnit SelectedUnit => selectedUnit;
        public BattleArena3DPresentationController PresentationController => presentationController;
        public BattleArenaPresentationOptions3D PresentationOptions => presentationController != null
            ? presentationController.Options
            : BattleArenaPresentationOptions3D.Default;
        public float RoundHealthMultiplier => Mathf.Pow(healthMultiplierPerRound, Mathf.Max(0, currentRound - 1));
        public float RoundDamageMultiplier => Mathf.Pow(damageMultiplierPerRound, Mathf.Max(0, currentRound - 1));
        public float RoundSpeedMultiplier => 1f + Mathf.Min(0.4f, speedGrowthPerRound * Mathf.Max(0, currentRound - 1));

        private void Start()
        {
            Time.timeScale = Mathf.Clamp(startingTimeScale, 0.25f, 2f);
            if (monsterTemplate != null) monsterTemplate.gameObject.SetActive(false);
            CaptureHunterSpawns();
            BuildSquads();
            BuildMonsterAbilitySets();
            vfxPool?.Initialize(this);
            ApplyPresentationOptions(true);
            if (beginAutomatically) StartCoroutine(PrewarmAndBegin());
        }

        private void OnDisable()
        {
            Time.timeScale = 1f;
        }

        private void OnDestroy()
        {
            if (panelTexture != null) Destroy(panelTexture);
            if (barBackgroundTexture != null) Destroy(barBackgroundTexture);
            if (pixelTexture != null) Destroy(pixelTexture);
        }

        private void Update()
        {
            ApplyPresentationOptions(false);
            var deltaTime = Mathf.Min(0.05f, Time.deltaTime);
            combatTime += deltaTime;
            SamplePerformance();
            vfxPool?.TickProjectiles(deltaTime, combatTime);
            ProcessPersistentFields();
            ProcessHunters(deltaTime);
            ProcessMonsters(deltaTime);
            ProcessFallenMonsters();

            if (phase == BattleArenaPhase3D.Preparing && combatTime >= phaseEndsAt)
            {
                BeginRound(Mathf.Max(1, currentRound));
            }
            else if (phase == BattleArenaPhase3D.Intermission && combatTime >= phaseEndsAt)
            {
                BeginRound(currentRound + 1);
            }
            else if (phase == BattleArenaPhase3D.Wave)
            {
                TickWaveSpawning(deltaTime);
                TickDecisionBudget(deltaTime);
                commanderAccumulator += deltaTime;
                if (commanderAccumulator >= commanderInterval)
                {
                    commanderAccumulator -= commanderInterval;
                    UpdateCoalitionOrders();
                }
                if (spawnedThisRound >= roundQuota && activeMonsters.Count == 0 && fallenMonsters.Count == 0)
                {
                    CompleteRound();
                }
                if (CountLivingHunters() == 0) EndDefeat();
            }

            barFacingAccumulator += deltaTime;
            if (barFacingAccumulator >= 0.12f)
            {
                barFacingAccumulator = 0f;
                FaceVisibleBars();
            }
        }

        public void RestartBattle()
        {
            if (phase == BattleArenaPhase3D.Prewarming && allMonsterUnits.Count < monsterPoolCapacity)
            {
                latestEvent = $"Prewarming horde pool: {allMonsterUnits.Count}/{monsterPoolCapacity}.";
                return;
            }
            StopAllCoroutines();
            vfxPool?.ResetBattleEffects();
            combatFeedback?.ResetFeedback();
            ReturnAllMonsters();
            persistentFields.Clear();
            currentRound = 1;
            combatTime = 0f;
            stressQuotaBonus = 0;
            stressConcurrentBonus = 0;
            pendingStressSpawns = 0;
            spawnSerial = 0;
            decisionCursor = 0;
            spawnAccumulator = decisionAccumulator = commanderAccumulator = barFacingAccumulator = 0f;
            attackAttempts = confirmedHits = abilityCasts = areaAbilityCasts = statusApplications = 0;
            commanderDecisions = 0;
            peakConcurrentMonsters = peakCombatants = 0;
            decisionsThisSample = attacksThisSample = hitsThisSample = abilitiesThisSample = 0;
            displayedDecisionsPerSecond = displayedAttacksPerSecond = displayedHitsPerSecond =
                displayedAbilitiesPerSecond = 0;
            sampleFrames = 0;
            sampleUnscaledElapsed = 0f;
            selectedUnit = null;
            Array.Clear(portalPressure, 0, portalPressure.Length);
            Array.Clear(assignedPortals, 0, assignedPortals.Length);
            for (var index = 0; index < hunters.Length; index++)
            {
                if (hunters[index] != null)
                {
                    hunters[index].ResetForBattle(
                        hunterSpawnPositions[index],
                        hunterSpawnRotations[index],
                        combatTime);
                    hunters[index].ApplyPresentationOptions(PresentationOptions);
                }
            }
            BuildSquads();
            phase = BattleArenaPhase3D.Preparing;
            phaseEndsAt = combatTime + 0.8f;
            latestEvent = "Three hunter squads are taking their positions.";
            UpdateCoalitionOrders();
        }

        public void AddStressEnemies(int amount = 220)
        {
            amount = Mathf.Clamp(amount, 1, monsterPoolCapacity);
            if (phase is BattleArenaPhase3D.Defeat or BattleArenaPhase3D.Victory)
            {
                RestartBattle();
            }
            if (phase != BattleArenaPhase3D.Wave)
            {
                currentRound = Mathf.Max(1, currentRound);
                BeginRound(currentRound);
            }
            stressQuotaBonus += amount;
            stressConcurrentBonus += amount;
            pendingStressSpawns += amount;
            roundQuota += amount;
            concurrentLimit = Mathf.Min(monsterPoolCapacity, concurrentLimit + amount);
            latestEvent = $"Stress command added {amount} monsters to the live wave.";
        }

        public void EscalateFiveRounds()
        {
            currentRound = Mathf.Max(1, currentRound + 5);
            latestEvent = $"Threat curve jumped to round {currentRound} scaling.";
        }

        public void SelectUnit(BattleArena3DUnit unit)
        {
            selectedUnit = unit;
        }

        public void SelectNextHunter(int direction)
        {
            if (hunters == null || hunters.Length == 0) return;
            direction = direction < 0 ? -1 : 1;
            var currentIndex = Array.IndexOf(hunters, selectedUnit);
            for (var step = 1; step <= hunters.Length; step++)
            {
                var index = (currentIndex + direction * step) % hunters.Length;
                if (index < 0) index += hunters.Length;
                var candidate = hunters[index];
                if (candidate == null || candidate.LifeState == BattleArenaLifeState3D.Dead) continue;
                selectedUnit = candidate;
                return;
            }
        }

        public void ResolveProjectileContact(
            BattleArena3DUnit source,
            BattleArena3DUnit target,
            RaidProjectilePayload payload,
            Vector3 position)
        {
            if (!AreHostile(source, target) || !target.CanReceiveDamage) return;
            if (payload.SplashRadius > 0.2f)
            {
                ResolveAreaDamage(source, position, payload.SplashRadius, payload.Damage,
                    payload.Effect, payload.StatusDuration, payload.Color);
                return;
            }
            ApplyDamage(source, target, payload.Damage, position, payload.Effect,
                payload.StatusDuration, payload.BasicAttack, payload.HeavyImpact ? 0.58f : 0.35f,
                payload.HeavyImpact);
        }

        private IEnumerator PrewarmAndBegin()
        {
            phase = BattleArenaPhase3D.Prewarming;
            latestEvent = "Prewarming the 3D horde and projectile pools.";
            while (allMonsterUnits.Count < monsterPoolCapacity)
            {
                var count = Mathf.Min(prewarmBatchSize, monsterPoolCapacity - allMonsterUnits.Count);
                for (var index = 0; index < count; index++)
                {
                    var clone = Instantiate(monsterTemplate, activeMonsterRoot);
                    clone.name = $"Pooled Monster {allMonsterUnits.Count + 1:000}";
                    clone.ApplyPresentationOptions(PresentationOptions);
                    clone.gameObject.SetActive(false);
                    allMonsterUnits.Add(clone);
                    availableMonsters.Enqueue(clone);
                }
                yield return null;
            }
            RestartBattle();
        }

        private void CaptureHunterSpawns()
        {
            hunterSpawnPositions = new Vector3[hunters.Length];
            hunterSpawnRotations = new Quaternion[hunters.Length];
            for (var index = 0; index < hunters.Length; index++)
            {
                if (hunters[index] == null) continue;
                hunterSpawnPositions[index] = hunters[index].transform.position;
                hunterSpawnRotations[index] = hunters[index].transform.rotation;
            }
        }

        private void BuildSquads()
        {
            for (var index = 0; index < squads.Length; index++)
            {
                squads[index].Members.Clear();
                squads[index].Sergeant = null;
                squads[index].Order = BattleArenaSquadOrder3D.HoldCenter;
                squads[index].Anchor = squadRallyPoints.Length > index && squadRallyPoints[index] != null
                    ? squadRallyPoints[index].position
                    : Vector3.zero;
            }
            for (var index = 0; index < hunters.Length; index++)
            {
                var hunter = hunters[index];
                if (hunter == null || hunter.SquadIndex < 0 || hunter.SquadIndex >= squads.Length) continue;
                var squad = squads[hunter.SquadIndex];
                squad.Members.Add(hunter);
                if (hunter.IsSergeant) squad.Sergeant = hunter;
            }
        }

        private void BeginRound(int round)
        {
            currentRound = Mathf.Max(1, round);
            spawnedThisRound = 0;
            defeatedThisRound = 0;
            stressQuotaBonus = 0;
            stressConcurrentBonus = 0;
            pendingStressSpawns = 0;
            roundQuota = Mathf.Min(monsterPoolCapacity * 3,
                firstRoundQuota + (currentRound - 1) * roundQuotaGrowth +
                (currentRound - 1) * (currentRound - 1) * 8);
            concurrentLimit = Mathf.Min(monsterPoolCapacity,
                firstRoundConcurrentLimit + (currentRound - 1) * roundConcurrentGrowth);
            spawnAccumulator = spawnInterval;
            decisionAccumulator = decisionInterval;
            commanderAccumulator = commanderInterval;
            phase = BattleArenaPhase3D.Wave;
            latestEvent = $"Round {currentRound}: {roundQuota} monsters are surging through four gates.";
            UpdateCoalitionOrders();
        }

        private void CompleteRound()
        {
            phase = BattleArenaPhase3D.Intermission;
            phaseEndsAt = combatTime + intermissionSeconds;
            latestEvent = $"Round {currentRound} cleared. {CountLivingHunters()} hunters remain combat-capable.";
            for (var index = 0; index < hunters.Length; index++)
            {
                var hunter = hunters[index];
                if (hunter != null && hunter.LifeState == BattleArenaLifeState3D.Active)
                {
                    hunter.ReceiveHealing(hunter.MaximumHealth * 0.18f, combatTime, false);
                }
            }
        }

        private void EndDefeat()
        {
            phase = BattleArenaPhase3D.Defeat;
            latestEvent = $"The strike force fell during round {currentRound}. Restart to deploy again.";
        }

        private void TickWaveSpawning(float deltaTime)
        {
            spawnAccumulator += deltaTime;
            if (pendingStressSpawns > 0)
            {
                var burst = Mathf.Min(spawnBatchSize * 2, pendingStressSpawns);
                burst = Mathf.Min(burst, concurrentLimit - activeMonsters.Count);
                burst = Mathf.Min(burst, availableMonsters.Count);
                for (var index = 0; index < burst; index++) SpawnMonster();
                pendingStressSpawns -= burst;
            }
            if (spawnAccumulator < spawnInterval || spawnedThisRound >= roundQuota ||
                activeMonsters.Count >= concurrentLimit || availableMonsters.Count == 0)
            {
                return;
            }
            spawnAccumulator -= spawnInterval;
            var count = Mathf.Min(spawnBatchSize, roundQuota - spawnedThisRound);
            count = Mathf.Min(count, concurrentLimit - activeMonsters.Count);
            count = Mathf.Min(count, availableMonsters.Count);
            for (var index = 0; index < count; index++) SpawnMonster();
        }

        private void SpawnMonster()
        {
            if (availableMonsters.Count == 0 || hordePortals.Length == 0) return;
            var portalIndex = spawnSerial % hordePortals.Length;
            var portal = hordePortals[portalIndex];
            if (portal == null) return;
            var archetype = SelectMonsterArchetype(spawnSerial, currentRound);
            var color = MonsterColor(archetype);
            var lateral = portal.right * UnityEngine.Random.Range(-4.5f, 4.5f);
            var forward = portal.forward * UnityEngine.Random.Range(0.3f, 3.2f);
            var position = portal.position + lateral + forward;
            var unit = availableMonsters.Dequeue();
            unit.PrepareMonster(
                archetype,
                spawnSerial,
                currentRound,
                RoundHealthMultiplier,
                RoundDamageMultiplier,
                RoundSpeedMultiplier,
                color,
                monsterAbilitySets[archetype],
                position,
                Quaternion.LookRotation(portal.forward),
                combatTime);
            unit.ApplyPresentationOptions(PresentationOptions);
            activeMonsters.Add(unit);
            spawnedThisRound++;
            spawnSerial++;
            peakConcurrentMonsters = Mathf.Max(peakConcurrentMonsters, activeMonsters.Count);
            peakCombatants = Mathf.Max(peakCombatants, activeMonsters.Count + CountLivingHunters());
        }

        private void ProcessHunters(float deltaTime)
        {
            for (var index = 0; index < hunters.Length; index++)
            {
                var hunter = hunters[index];
                if (hunter == null || !hunter.isActiveAndEnabled) continue;
                hunter.Tick(deltaTime, combatTime, ShouldShowWorldBar(hunter));
                if (hunter.TryConsumeBurnTick(combatTime, out var burnDamage))
                {
                    ApplyDamage(null, hunter, burnDamage, hunter.transform.position,
                        RaidAbilityEffect.Damage, 0f, false, 0f);
                }
                hunter.ExpireDownedState(combatTime);
                if (hunter.TryConsumePendingAttack(combatTime, out var target, out var heavy))
                {
                    ResolveBasicAttack(hunter, target, heavy);
                }
            }
        }

        private void ProcessMonsters(float deltaTime)
        {
            for (var index = activeMonsters.Count - 1; index >= 0; index--)
            {
                var monster = activeMonsters[index];
                if (monster == null || !monster.isActiveAndEnabled)
                {
                    activeMonsters.RemoveAt(index);
                    continue;
                }
                monster.Tick(deltaTime, combatTime, ShouldShowWorldBar(monster));
                if (monster.TryConsumeBurnTick(combatTime, out var burnDamage))
                {
                    ApplyDamage(null, monster, burnDamage, monster.transform.position,
                        RaidAbilityEffect.Damage, 0f, false, 0f);
                }
                if (monster.TryConsumePendingAttack(combatTime, out var target, out var heavy))
                {
                    ResolveBasicAttack(monster, target, heavy);
                }
            }
        }

        private void ApplyPresentationOptions(bool force)
        {
            var revision = presentationController != null ? presentationController.Revision : 0;
            if (!force && revision == appliedPresentationRevision) return;
            appliedPresentationRevision = revision;
            var options = PresentationOptions;
            for (var index = 0; index < hunters.Length; index++)
            {
                hunters[index]?.ApplyPresentationOptions(options);
            }
            for (var index = 0; index < allMonsterUnits.Count; index++)
            {
                allMonsterUnits[index]?.ApplyPresentationOptions(options);
            }
        }

        private bool ShouldShowWorldBar(BattleArena3DUnit unit)
        {
            if (unit == null) return false;
            return PresentationOptions.WorldBars switch
            {
                BattleArenaWorldBars3D.SelectedOnly => unit == selectedUnit,
                BattleArenaWorldBars3D.All => true,
                _ => unit == selectedUnit || unit.Faction == BattleArenaFaction3D.Hunters ||
                     unit.HealthRatio < 0.995f || unit.Shield > 0.01f ||
                     unit.MonsterArchetype == BattleArenaMonsterArchetype3D.Elite
            };
        }

        private void ProcessFallenMonsters()
        {
            for (var index = fallenMonsters.Count - 1; index >= 0; index--)
            {
                if (combatTime < fallenMonsters[index].ReturnAt) continue;
                var unit = fallenMonsters[index].Unit;
                fallenMonsters.RemoveAt(index);
                if (unit == null) continue;
                unit.ReturnToPool();
                availableMonsters.Enqueue(unit);
            }
        }

        private void TickDecisionBudget(float deltaTime)
        {
            decisionAccumulator += deltaTime;
            if (decisionAccumulator < decisionInterval) return;
            decisionAccumulator -= decisionInterval;
            var population = hunters.Length + activeMonsters.Count;
            if (population == 0) return;
            var count = Mathf.Min(decisionsPerFrame, population);
            for (var step = 0; step < count; step++)
            {
                var index = decisionCursor++ % population;
                if (index < hunters.Length)
                {
                    DecideHunter(hunters[index]);
                }
                else
                {
                    var monsterIndex = index - hunters.Length;
                    if (monsterIndex < activeMonsters.Count) DecideMonster(activeMonsters[monsterIndex]);
                }
                decisionsThisSample++;
            }
        }

        private void DecideHunter(BattleArena3DUnit hunter)
        {
            if (hunter == null || !hunter.CanAct(combatTime)) return;
            var target = FindNearestMonster(hunter.transform.position, 30f);
            var squad = hunter.SquadIndex >= 0 && hunter.SquadIndex < squads.Length
                ? squads[hunter.SquadIndex]
                : null;
            if (TryUseHunterAbility(hunter, target)) return;
            if (target == null)
            {
                var anchor = squad?.Anchor ?? Vector3.zero;
                hunter.SetDestination(anchor + FormationOffset(squad, hunter));
                hunter.SetObjective("Holding assigned sector");
                return;
            }
            var distance = FlatDistance(hunter.transform.position, target.transform.position);
            if (distance <= hunter.AttackRange + (hunter.IsRanged ? 0.4f : 0.15f))
            {
                hunter.StopMoving();
                hunter.Face(target.transform.position);
                hunter.QueueBasicAttack(target, combatTime, false);
                hunter.SetObjective($"Engaging {target.DisplayName}");
                return;
            }
            if (hunter.IsRanged && distance < hunter.PreferredRange * 0.58f)
            {
                var away = (hunter.transform.position - target.transform.position).normalized;
                hunter.SetDestination(hunter.transform.position + away * 3.5f);
                hunter.SetObjective("Repositioning to casting range");
                return;
            }
            var personalityChase = Mathf.Lerp(4f, 13f, hunter.Aggression);
            var anchorDistance = squad == null ? 0f : FlatDistance(hunter.transform.position, squad.Anchor);
            if (distance <= personalityChase || anchorDistance < Mathf.Lerp(4f, 11f, 1f - hunter.Cohesion))
            {
                var approach = target.transform.position -
                               (target.transform.position - hunter.transform.position).normalized *
                               hunter.PreferredRange * 0.72f;
                hunter.SetDestination(approach);
                hunter.SetObjective($"Pressing {target.DisplayName}");
            }
            else
            {
                hunter.SetDestination((squad?.Anchor ?? Vector3.zero) + FormationOffset(squad, hunter));
                hunter.SetObjective("Maintaining squad cohesion");
            }
        }

        private void DecideMonster(BattleArena3DUnit monster)
        {
            if (monster == null || !monster.CanAct(combatTime)) return;
            var target = monster.ForcedTarget != null && monster.ForcedTarget.CanReceiveDamage
                ? monster.ForcedTarget
                : FindMonsterTarget(monster);
            if (target == null)
            {
                monster.SetDestination(Vector3.zero);
                return;
            }
            if (TryUseMonsterAbility(monster, target)) return;
            var distance = FlatDistance(monster.transform.position, target.transform.position);
            if (distance <= monster.AttackRange + 0.2f)
            {
                monster.StopMoving();
                monster.Face(target.transform.position);
                var heavyChance = monster.MonsterArchetype switch
                {
                    BattleArenaMonsterArchetype3D.Brute => 0.7f,
                    BattleArenaMonsterArchetype3D.Elite => 0.45f,
                    _ => 0f
                };
                var heavy = heavyChance > 0f && UnityEngine.Random.value < heavyChance;
                if (monster.QueueBasicAttack(target, combatTime, heavy) && heavy)
                {
                    vfxPool.EmitTelegraph(target.transform.position,
                        Mathf.Max(1.1f, monster.AttackRange * 0.72f),
                        new Color(1f, 0.18f, 0.06f, 1f), combatTime, 0.42f);
                }
                monster.SetObjective($"Attacking {target.DisplayName}");
                return;
            }
            if (monster.IsRanged && distance < monster.PreferredRange * 0.55f)
            {
                var away = (monster.transform.position - target.transform.position).normalized;
                monster.SetDestination(monster.transform.position + away * 2.8f);
                monster.SetObjective("Kiting the strike team");
            }
            else
            {
                monster.SetDestination(target.transform.position);
                monster.SetObjective($"Hunting {target.DisplayName}");
            }
        }

        private bool TryUseHunterAbility(BattleArena3DUnit hunter, BattleArena3DUnit target)
        {
            var count = hunter.Abilities.Count;
            if (count == 0) return false;
            for (var step = 0; step < count; step++)
            {
                var index = (hunter.AbilityCursor + step) % count;
                if (!hunter.IsAbilityReady(index, combatTime)) continue;
                var ability = hunter.Abilities[index];
                BattleArena3DUnit friendlyTarget = null;
                var valid = ability.effect switch
                {
                    RaidAbilityEffect.Heal =>
                        (friendlyTarget = FindLowestHealthHunter(hunter.transform.position, ability.range, true)) != null &&
                        friendlyTarget.HealthRatio <= ability.preferredHealthThreshold,
                    RaidAbilityEffect.AreaHeal or RaidAbilityEffect.PersistentAreaHeal =>
                        CountWoundedHunters(hunter.transform.position, ability.radius) >= 2,
                    RaidAbilityEffect.Shield => !hunter.HasShield(combatTime) &&
                                                CountNearbyHunters(hunter.transform.position, ability.radius) >= 2,
                    RaidAbilityEffect.Taunt => target != null &&
                                               FlatDistance(hunter.transform.position, target.transform.position) <=
                                               ability.range,
                    _ => target != null &&
                         FlatDistance(hunter.transform.position, target.transform.position) <= ability.range + 0.6f
                };
                if (!valid || !hunter.CommitAbility(index, combatTime)) continue;
                abilityCasts++;
                abilitiesThisSample++;
                ResolveAbility(hunter, target, friendlyTarget, ability);
                return true;
            }
            return false;
        }

        private bool TryUseMonsterAbility(BattleArena3DUnit monster, BattleArena3DUnit target)
        {
            if (monster.Abilities.Count == 0 || target == null) return false;
            for (var index = 0; index < monster.Abilities.Count; index++)
            {
                if (!monster.IsAbilityReady(index, combatTime)) continue;
                var ability = monster.Abilities[index];
                if (FlatDistance(monster.transform.position, target.transform.position) > ability.range) continue;
                if (!monster.CommitAbility(index, combatTime)) continue;
                abilityCasts++;
                abilitiesThisSample++;
                ResolveAbility(monster, target, null, ability);
                return true;
            }
            return false;
        }

        private void ResolveAbility(
            BattleArena3DUnit source,
            BattleArena3DUnit hostileTarget,
            BattleArena3DUnit friendlyTarget,
            RaidAbilitySpec ability)
        {
            var sourcePosition = source.transform.position;
            switch (ability.effect)
            {
                case RaidAbilityEffect.Heal:
                    if (friendlyTarget != null)
                    {
                        var healed = friendlyTarget.ReceiveHealing(ability.power, combatTime, true);
                        if (healed > 0f) vfxPool.EmitHealing(friendlyTarget.transform.position + Vector3.up, ability.color);
                    }
                    break;
                case RaidAbilityEffect.AreaHeal:
                    ResolveAreaHealing(source, sourcePosition, ability.radius, ability.power, ability.color, true);
                    break;
                case RaidAbilityEffect.PersistentAreaHeal:
                    persistentFields.Add(new PersistentField
                    {
                        Source = source,
                        Position = sourcePosition,
                        Radius = ability.radius,
                        Power = ability.power,
                        NextTick = combatTime,
                        ExpiresAt = combatTime + Mathf.Max(2f, ability.duration),
                        Healing = true,
                        Color = ability.color
                    });
                    areaAbilityCasts++;
                    vfxPool.EmitHealing(sourcePosition, ability.color, 42);
                    break;
                case RaidAbilityEffect.Shield:
                    ResolveAreaShield(source, sourcePosition, ability.radius, ability.power, ability.duration, ability.color);
                    break;
                case RaidAbilityEffect.Taunt:
                    ResolveTaunt(source, sourcePosition, ability.radius, ability.duration);
                    break;
                case RaidAbilityEffect.DashStrike:
                case RaidAbilityEffect.ShadowStep:
                    if (hostileTarget != null)
                    {
                        var direction = (sourcePosition - hostileTarget.transform.position).normalized;
                        source.Warp(hostileTarget.transform.position + direction * 1.1f);
                        source.Face(hostileTarget.transform.position);
                        ResolveMeleeVolume(source, hostileTarget, ability.power, 1.2f,
                            ability.effect, ability.duration, ability.color, true, true);
                    }
                    break;
                case RaidAbilityEffect.AreaDamage:
                    ResolveAreaDamage(source,
                        hostileTarget != null && ability.range > 3f ? hostileTarget.transform.position : sourcePosition,
                        ability.radius, ability.power, ability.effect, ability.duration, ability.color);
                    break;
                case RaidAbilityEffect.DamageAndBuffAllies:
                    ResolveAreaDamage(source, sourcePosition, ability.radius, ability.power,
                        ability.effect, ability.duration, ability.color);
                    EmpowerNearbyHunters(source, sourcePosition, ability.radius, ability.duration);
                    break;
                case RaidAbilityEffect.ChainDamage:
                    ResolveChainDamage(source, hostileTarget, ability);
                    break;
                case RaidAbilityEffect.PiercingDamage:
                    ResolvePiercingDamage(source, hostileTarget, ability);
                    break;
                case RaidAbilityEffect.Execute:
                    if (hostileTarget != null)
                    {
                        var power = hostileTarget.HealthRatio <= 0.3f ? ability.power * 2f : ability.power;
                        ResolveMeleeVolume(source, hostileTarget, power, 1.05f,
                            ability.effect, ability.duration, ability.color, true, true);
                    }
                    break;
                default:
                    if (hostileTarget != null)
                    {
                        var payload = new RaidProjectilePayload(
                            ability.power,
                            ability.effect == RaidAbilityEffect.ProjectileAreaDamage ? ability.radius : 0f,
                            ability.duration,
                            ability.effect,
                            ability.color,
                            false);
                        if (!vfxPool.LaunchProjectile(source, hostileTarget,
                                ability.element == RaidElement.Lightning ? 20f : 13f,
                                ability.effect == RaidAbilityEffect.ProjectileAreaDamage ? 0.24f : 0.16f,
                                ability.color,
                                payload,
                                combatTime))
                        {
                            ApplyDamage(source, hostileTarget, ability.power, hostileTarget.transform.position,
                                ability.effect, ability.duration, false, 0.25f);
                        }
                    }
                    break;
            }
        }

        private void ResolveBasicAttack(BattleArena3DUnit source, BattleArena3DUnit target, bool heavy)
        {
            if (!AreHostile(source, target) || !source.CanAct(combatTime)) return;
            attackAttempts++;
            attacksThisSample++;
            if (source.IsRanged)
            {
                var payload = new RaidProjectilePayload(
                    source.ResolveOutgoingDamage(source.BasicDamage * (heavy ? 1.5f : 1f), combatTime),
                    0f,
                    0f,
                    RaidAbilityEffect.Damage,
                    source.ThemeColor,
                    true,
                    heavy);
                vfxPool.LaunchProjectile(source, target, 18f, 0.12f, source.ThemeColor, payload, combatTime);
                return;
            }
            ResolveMeleeVolume(source, target,
                source.ResolveOutgoingDamage(source.BasicDamage * (heavy ? 1.65f : 1f), combatTime),
                heavy ? 1.25f : 0.88f,
                RaidAbilityEffect.Damage,
                0f,
                source.ThemeColor,
                false,
                heavy);
        }

        private void ResolveMeleeVolume(
            BattleArena3DUnit source,
            BattleArena3DUnit intendedTarget,
            float damage,
            float halfWidth,
            RaidAbilityEffect effect,
            float statusDuration,
            Color color,
            bool ability,
            bool heavyImpact = false)
        {
            if (source == null) return;
            var center = source.transform.position + source.transform.forward * (source.AttackRange * 0.62f) +
                         Vector3.up * 0.9f;
            var halfExtents = new Vector3(halfWidth, 0.9f, Mathf.Max(0.55f, source.AttackRange * 0.55f));
            var count = Physics.OverlapBoxNonAlloc(center, halfExtents, contactBuffer,
                source.transform.rotation, Physics.AllLayers, QueryTriggerInteraction.Collide);
            BattleArena3DUnit best = null;
            var bestSqr = float.MaxValue;
            for (var index = 0; index < count; index++)
            {
                var collider = contactBuffer[index];
                contactBuffer[index] = null;
                if (collider == null) continue;
                var unit = collider.GetComponentInParent<BattleArena3DUnit>();
                if (!AreHostile(source, unit) || !unit.CanReceiveDamage) continue;
                var sqr = (unit.transform.position - intendedTarget.transform.position).sqrMagnitude;
                if (sqr >= bestSqr) continue;
                best = unit;
                bestSqr = sqr;
            }
            if (best == null) return;
            ApplyDamage(source, best, damage, best.transform.position + Vector3.up * 0.8f,
                effect, statusDuration, !ability, ability ? 0.65f : heavyImpact ? 0.58f : 0.42f,
                heavyImpact);
            if (ability)
            {
                vfxPool.EmitTelegraph(best.transform.position, Mathf.Max(0.8f, halfWidth), color,
                    combatTime, 0.38f);
            }
        }

        private void ResolveAreaDamage(
            BattleArena3DUnit source,
            Vector3 position,
            float radius,
            float power,
            RaidAbilityEffect effect,
            float statusDuration,
            Color color)
        {
            areaAbilityCasts++;
            vfxPool.EmitTelegraph(position, Mathf.Max(0.5f, radius), color, combatTime, 0.68f);
            vfxPool.EmitMagic(position + Vector3.up * 0.2f, color, 48);
            var count = Physics.OverlapSphereNonAlloc(position, Mathf.Max(0.5f, radius), contactBuffer,
                Physics.AllLayers, QueryTriggerInteraction.Collide);
            for (var index = 0; index < count; index++)
            {
                var collider = contactBuffer[index];
                contactBuffer[index] = null;
                if (collider == null) continue;
                var unit = collider.GetComponentInParent<BattleArena3DUnit>();
                if (!AreHostile(source, unit) || !unit.CanReceiveDamage) continue;
                ApplyDamage(source, unit, power, unit.transform.position + Vector3.up * 0.7f,
                    effect, statusDuration, false, 0.72f);
            }
        }

        private void ResolveAreaHealing(
            BattleArena3DUnit source,
            Vector3 position,
            float radius,
            float power,
            Color color,
            bool canRevive)
        {
            areaAbilityCasts++;
            vfxPool.EmitTelegraph(position, Mathf.Max(0.5f, radius), color, combatTime, 0.76f);
            for (var index = 0; index < hunters.Length; index++)
            {
                var hunter = hunters[index];
                if (hunter == null || hunter.Faction != source.Faction ||
                    FlatDistance(position, hunter.transform.position) > radius) continue;
                if (hunter.ReceiveHealing(power, combatTime, canRevive) > 0f)
                {
                    vfxPool.EmitHealing(hunter.transform.position + Vector3.up, color, 14);
                }
            }
        }

        private void ResolveAreaShield(
            BattleArena3DUnit source,
            Vector3 position,
            float radius,
            float power,
            float duration,
            Color color)
        {
            areaAbilityCasts++;
            vfxPool.EmitTelegraph(position, Mathf.Max(0.5f, radius), color, combatTime, 0.7f);
            for (var index = 0; index < hunters.Length; index++)
            {
                var hunter = hunters[index];
                if (hunter == null || hunter.Faction != source.Faction ||
                    FlatDistance(position, hunter.transform.position) > radius) continue;
                hunter.GrantShield(power, Mathf.Max(4f, duration), combatTime);
                statusApplications++;
                vfxPool.EmitShield(hunter.transform.position + Vector3.up * 0.8f, color, 12);
            }
        }

        private void ResolveTaunt(BattleArena3DUnit source, Vector3 position, float radius, float duration)
        {
            areaAbilityCasts++;
            vfxPool.EmitTelegraph(position, Mathf.Max(0.5f, radius), source.ThemeColor, combatTime, 0.72f);
            for (var index = 0; index < activeMonsters.Count; index++)
            {
                var monster = activeMonsters[index];
                if (monster == null || FlatDistance(position, monster.transform.position) > radius) continue;
                monster.ForceTarget(source, Mathf.Max(2f, duration), combatTime);
                statusApplications++;
            }
            vfxPool.EmitMagic(position + Vector3.up, source.ThemeColor, 36);
        }

        private void ResolveChainDamage(
            BattleArena3DUnit source,
            BattleArena3DUnit firstTarget,
            RaidAbilitySpec ability)
        {
            if (firstTarget == null) return;
            var current = firstTarget;
            var hit = new HashSet<BattleArena3DUnit>();
            var maximum = Mathf.Clamp(ability.maximumTargets, 2, 8);
            for (var index = 0; index < maximum && current != null; index++)
            {
                hit.Add(current);
                ApplyDamage(source, current, ability.power * Mathf.Pow(0.82f, index),
                    current.transform.position + Vector3.up, ability.effect, ability.duration, false, 0.28f);
                vfxPool.EmitMagic(current.transform.position + Vector3.up, ability.color, 12);
                current = FindNearestMonsterExcluding(current.transform.position, ability.radius, hit, source.Faction);
            }
        }

        private void ResolvePiercingDamage(
            BattleArena3DUnit source,
            BattleArena3DUnit target,
            RaidAbilitySpec ability)
        {
            if (target == null) return;
            source.Face(target.transform.position);
            var origin = source.transform.position + Vector3.up * 1f;
            var direction = (target.transform.position + Vector3.up * 0.8f - origin).normalized;
            var count = Physics.SphereCastNonAlloc(origin, Mathf.Max(0.12f, ability.width), direction,
                lineHitBuffer, ability.range, Physics.AllLayers, QueryTriggerInteraction.Collide);
            Array.Sort(lineHitBuffer, 0, count, RaycastHitDistanceComparer.Instance);
            var hitUnits = 0;
            for (var index = 0; index < count && hitUnits < Mathf.Max(1, ability.maximumTargets); index++)
            {
                var hit = lineHitBuffer[index];
                var collider = hit.collider;
                var unit = collider != null ? collider.GetComponentInParent<BattleArena3DUnit>() : null;
                lineHitBuffer[index] = default;
                if (!AreHostile(source, unit) || !unit.CanReceiveDamage) continue;
                ApplyDamage(source, unit, ability.power, hit.point,
                    ability.effect, ability.duration, false, 0.35f);
                hitUnits++;
            }
            areaAbilityCasts++;
            vfxPool.EmitMagic(target.transform.position + Vector3.up, ability.color, 24);
        }

        private void EmpowerNearbyHunters(
            BattleArena3DUnit source,
            Vector3 position,
            float radius,
            float duration)
        {
            for (var index = 0; index < hunters.Length; index++)
            {
                var hunter = hunters[index];
                if (hunter == null || hunter.Faction != source.Faction ||
                    FlatDistance(position, hunter.transform.position) > radius) continue;
                hunter.ApplyEmpower(duration, combatTime);
                statusApplications++;
            }
        }

        private void ProcessPersistentFields()
        {
            for (var index = persistentFields.Count - 1; index >= 0; index--)
            {
                var field = persistentFields[index];
                if (combatTime >= field.ExpiresAt)
                {
                    persistentFields.RemoveAt(index);
                    continue;
                }
                if (combatTime < field.NextTick) continue;
                field.NextTick = combatTime + 1f;
                if (field.Healing)
                {
                    ResolveAreaHealing(field.Source, field.Position, field.Radius, field.Power,
                        field.Color, false);
                }
                else
                {
                    ResolveAreaDamage(field.Source, field.Position, field.Radius, field.Power,
                        RaidAbilityEffect.DamageOverTime, 1f, field.Color);
                }
            }
        }

        private void ApplyDamage(
            BattleArena3DUnit source,
            BattleArena3DUnit target,
            float amount,
            Vector3 position,
            RaidAbilityEffect effect,
            float statusDuration,
            bool basicAttack,
            float knockbackDistance,
            bool heavyImpact = false)
        {
            if (target == null || !target.CanReceiveDamage ||
                (source != null && source.Faction == target.Faction)) return;
            var resolved = source != null ? source.ResolveOutgoingDamage(amount, combatTime) : amount;
            var result = target.ReceiveDamage(resolved, combatTime,
                target.Faction == BattleArenaFaction3D.Monsters);
            if (result.TotalResolved <= 0f) return;
            confirmedHits++;
            hitsThisSample++;
            source?.RecordDamage(result.AppliedDamage);
            var direction = source != null
                ? target.transform.position - source.transform.position
                : UnityEngine.Random.insideUnitSphere;
            target.ApplyKnockback(direction, knockbackDistance);
            combatFeedback?.HandleHit(source, target, result, position, direction, effect, basicAttack,
                heavyImpact);
            if (result.AppliedDamage > 0f) ApplyStatusFromEffect(target, effect, resolved, statusDuration);
            if (result.Died)
            {
                source?.RecordKill();
                if (target.Faction == BattleArenaFaction3D.Monsters) DefeatMonster(target);
            }
            else if (result.BecameDowned)
            {
                latestEvent = $"{target.DisplayName} is down and needs a healer.";
            }
        }

        private void ApplyStatusFromEffect(
            BattleArena3DUnit target,
            RaidAbilityEffect effect,
            float power,
            float duration)
        {
            switch (effect)
            {
                case RaidAbilityEffect.Freeze:
                    target.ApplyStun(Mathf.Max(0.5f, duration), combatTime);
                    statusApplications++;
                    break;
                case RaidAbilityEffect.DamageOverTime:
                    target.ApplyBurn(Mathf.Max(1f, power * 0.22f), Mathf.Max(2f, duration), combatTime);
                    statusApplications++;
                    break;
                case RaidAbilityEffect.DamageMark:
                    target.ApplyVulnerability(Mathf.Max(2f, duration), combatTime);
                    statusApplications++;
                    break;
            }
        }

        private void DefeatMonster(BattleArena3DUnit monster)
        {
            if (monster == null) return;
            if (activeMonsters.Remove(monster))
            {
                defeatedThisRound++;
                fallenMonsters.Add(new FallenMonster(monster, combatTime + 1.25f));
            }
        }

        private void ReturnAllMonsters()
        {
            for (var index = 0; index < activeMonsters.Count; index++)
            {
                var monster = activeMonsters[index];
                if (monster == null) continue;
                monster.ReturnToPool();
            }
            for (var index = 0; index < fallenMonsters.Count; index++)
            {
                var monster = fallenMonsters[index].Unit;
                if (monster == null) continue;
                monster.ReturnToPool();
            }
            activeMonsters.Clear();
            fallenMonsters.Clear();
            availableMonsters.Clear();
            for (var index = 0; index < allMonsterUnits.Count; index++)
            {
                var monster = allMonsterUnits[index];
                if (monster != null) availableMonsters.Enqueue(monster);
            }
        }

        private void UpdateCoalitionOrders()
        {
            Array.Clear(portalPressure, 0, portalPressure.Length);
            for (var index = 0; index < activeMonsters.Count; index++)
            {
                var monster = activeMonsters[index];
                if (monster == null) continue;
                var closestPortal = FindClosestPortalIndex(monster.transform.position);
                var weight = monster.MonsterArchetype switch
                {
                    BattleArenaMonsterArchetype3D.Elite => 4f,
                    BattleArenaMonsterArchetype3D.Brute => 2.2f,
                    BattleArenaMonsterArchetype3D.Hexer => 1.6f,
                    _ => 1f
                };
                portalPressure[closestPortal] += weight;
            }
            Array.Clear(assignedPortals, 0, assignedPortals.Length);
            for (var squadIndex = 0; squadIndex < squads.Length; squadIndex++)
            {
                var squad = squads[squadIndex];
                RefreshSquadState(squad);
                if (squad.Members.Count == 0) continue;
                if (squad.AverageHealth < 0.32f || CountActiveMembers(squad) <= 4)
                {
                    squad.Order = BattleArenaSquadOrder3D.Regroup;
                    squad.Anchor = squadRallyPoints[squadIndex].position;
                }
                else
                {
                    var portalIndex = HighestPressurePortal(assignedPortals);
                    assignedPortals[portalIndex] = true;
                    squad.Order = portalIndex switch
                    {
                        0 => BattleArenaSquadOrder3D.DefendWest,
                        1 => BattleArenaSquadOrder3D.DefendEast,
                        2 => BattleArenaSquadOrder3D.DefendNorth,
                        _ => BattleArenaSquadOrder3D.DefendSouth
                    };
                    squad.Anchor = Vector3.Lerp(Vector3.zero, hordePortals[portalIndex].position, 0.48f);
                }
                for (var memberIndex = 0; memberIndex < squad.Members.Count; memberIndex++)
                {
                    var member = squad.Members[memberIndex];
                    if (member != null && member.LifeState == BattleArenaLifeState3D.Active)
                    {
                        member.SetObjective($"{squad.Order} under {squad.Sergeant?.DisplayName ?? "field command"}");
                    }
                }
            }
            commanderDecisions++;
        }

        private void RefreshSquadState(SquadState squad)
        {
            var health = 0f;
            var active = 0;
            if (squad.Sergeant == null || squad.Sergeant.LifeState != BattleArenaLifeState3D.Active)
            {
                squad.Sergeant = null;
                for (var index = 0; index < squad.Members.Count; index++)
                {
                    var candidate = squad.Members[index];
                    if (candidate == null || candidate.LifeState != BattleArenaLifeState3D.Active) continue;
                    squad.Sergeant = candidate;
                    latestEvent = $"{candidate.DisplayName} assumed field command of {squad.Name}.";
                    break;
                }
            }
            for (var index = 0; index < squad.Members.Count; index++)
            {
                var member = squad.Members[index];
                if (member == null || member.LifeState != BattleArenaLifeState3D.Active) continue;
                active++;
                health += member.HealthRatio;
            }
            squad.AverageHealth = active == 0 ? 0f : health / active;
        }

        private BattleArena3DUnit FindMonsterTarget(BattleArena3DUnit monster)
        {
            BattleArena3DUnit best = null;
            var bestScore = float.MaxValue;
            for (var index = 0; index < hunters.Length; index++)
            {
                var hunter = hunters[index];
                if (hunter == null || !hunter.CanReceiveDamage) continue;
                var distance = FlatDistance(monster.transform.position, hunter.transform.position);
                var roleBias = monster.MonsterArchetype switch
                {
                    BattleArenaMonsterArchetype3D.Charger when hunter.Role is RaidCombatRole.Healer or RaidCombatRole.Mage => -5f,
                    BattleArenaMonsterArchetype3D.Hexer when hunter.Role == RaidCombatRole.Tank => -2f,
                    _ => 0f
                };
                var score = distance + roleBias + hunter.HealthRatio * 1.5f;
                if (score >= bestScore) continue;
                best = hunter;
                bestScore = score;
            }
            return best;
        }

        private BattleArena3DUnit FindNearestMonster(Vector3 position, float maximumRange)
        {
            BattleArena3DUnit best = null;
            var bestSqr = maximumRange * maximumRange;
            for (var index = 0; index < activeMonsters.Count; index++)
            {
                var monster = activeMonsters[index];
                if (monster == null || !monster.CanReceiveDamage) continue;
                var delta = monster.transform.position - position;
                delta.y = 0f;
                var sqr = delta.sqrMagnitude;
                if (sqr >= bestSqr) continue;
                best = monster;
                bestSqr = sqr;
            }
            return best;
        }

        private BattleArena3DUnit FindNearestMonsterExcluding(
            Vector3 position,
            float maximumRange,
            HashSet<BattleArena3DUnit> excluded,
            BattleArenaFaction3D sourceFaction)
        {
            BattleArena3DUnit best = null;
            var bestSqr = maximumRange * maximumRange;
            for (var index = 0; index < activeMonsters.Count; index++)
            {
                var unit = activeMonsters[index];
                if (unit == null || excluded.Contains(unit) || unit.Faction == sourceFaction || !unit.CanReceiveDamage)
                    continue;
                var delta = unit.transform.position - position;
                delta.y = 0f;
                var sqr = delta.sqrMagnitude;
                if (sqr >= bestSqr) continue;
                best = unit;
                bestSqr = sqr;
            }
            return best;
        }

        private BattleArena3DUnit FindLowestHealthHunter(Vector3 position, float range, bool includeDowned)
        {
            BattleArena3DUnit best = null;
            var bestRatio = 1.01f;
            for (var index = 0; index < hunters.Length; index++)
            {
                var hunter = hunters[index];
                if (hunter == null || hunter.LifeState == BattleArenaLifeState3D.Dead ||
                    (!includeDowned && hunter.LifeState == BattleArenaLifeState3D.Downed) ||
                    FlatDistance(position, hunter.transform.position) > range) continue;
                var ratio = hunter.LifeState == BattleArenaLifeState3D.Downed ? -1f : hunter.HealthRatio;
                if (ratio >= bestRatio) continue;
                best = hunter;
                bestRatio = ratio;
            }
            return best;
        }

        private int CountWoundedHunters(Vector3 position, float radius)
        {
            var count = 0;
            for (var index = 0; index < hunters.Length; index++)
            {
                var hunter = hunters[index];
                if (hunter == null || hunter.LifeState == BattleArenaLifeState3D.Dead ||
                    hunter.HealthRatio >= 0.75f || FlatDistance(position, hunter.transform.position) > radius) continue;
                count++;
            }
            return count;
        }

        private int CountNearbyHunters(Vector3 position, float radius)
        {
            var count = 0;
            for (var index = 0; index < hunters.Length; index++)
            {
                var hunter = hunters[index];
                if (hunter != null && hunter.LifeState == BattleArenaLifeState3D.Active &&
                    FlatDistance(position, hunter.transform.position) <= radius) count++;
            }
            return count;
        }

        private int CountLivingHunters()
        {
            var count = 0;
            for (var index = 0; index < hunters.Length; index++)
            {
                if (hunters[index] != null && hunters[index].LifeState != BattleArenaLifeState3D.Dead) count++;
            }
            return count;
        }

        private static int CountActiveMembers(SquadState squad)
        {
            var count = 0;
            for (var index = 0; index < squad.Members.Count; index++)
            {
                if (squad.Members[index] != null &&
                    squad.Members[index].LifeState == BattleArenaLifeState3D.Active) count++;
            }
            return count;
        }

        private Vector3 FormationOffset(SquadState squad, BattleArena3DUnit unit)
        {
            if (squad == null) return Vector3.zero;
            var index = Mathf.Max(0, squad.Members.IndexOf(unit));
            var row = index / 4;
            var column = index % 4;
            var spacing = Mathf.Lerp(1.35f, 2.15f, 1f - unit.Cohesion);
            return new Vector3((column - 1.5f) * spacing, 0f, -row * spacing);
        }

        private int FindClosestPortalIndex(Vector3 position)
        {
            var best = 0;
            var bestSqr = float.MaxValue;
            for (var index = 0; index < hordePortals.Length; index++)
            {
                if (hordePortals[index] == null) continue;
                var sqr = (hordePortals[index].position - position).sqrMagnitude;
                if (sqr >= bestSqr) continue;
                best = index;
                bestSqr = sqr;
            }
            return best;
        }

        private int HighestPressurePortal(bool[] assigned)
        {
            var best = -1;
            var bestPressure = float.MinValue;
            for (var index = 0; index < portalPressure.Length; index++)
            {
                if (assigned[index] || portalPressure[index] <= bestPressure) continue;
                best = index;
                bestPressure = portalPressure[index];
            }
            if (best >= 0) return best;
            for (var index = 0; index < assigned.Length; index++) if (!assigned[index]) return index;
            return 0;
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private static bool AreHostile(BattleArena3DUnit source, BattleArena3DUnit target)
        {
            return source != null && target != null && source.Faction != target.Faction;
        }

        private static BattleArenaMonsterArchetype3D SelectMonsterArchetype(int serial, int round)
        {
            if (serial % Mathf.Max(18, 46 - round * 2) == 0) return BattleArenaMonsterArchetype3D.Elite;
            return ((serial * 17 + round * 11) % 20) switch
            {
                <= 8 => BattleArenaMonsterArchetype3D.Ravager,
                <= 11 => BattleArenaMonsterArchetype3D.Brute,
                <= 14 => BattleArenaMonsterArchetype3D.Spitter,
                <= 16 => BattleArenaMonsterArchetype3D.Hexer,
                _ => BattleArenaMonsterArchetype3D.Charger
            };
        }

        private static Color MonsterColor(BattleArenaMonsterArchetype3D archetype)
        {
            return archetype switch
            {
                BattleArenaMonsterArchetype3D.Brute => new Color(0.58f, 0.12f, 0.07f),
                BattleArenaMonsterArchetype3D.Spitter => new Color(0.35f, 0.9f, 0.12f),
                BattleArenaMonsterArchetype3D.Hexer => new Color(0.62f, 0.12f, 0.9f),
                BattleArenaMonsterArchetype3D.Charger => new Color(1f, 0.4f, 0.04f),
                BattleArenaMonsterArchetype3D.Elite => new Color(1f, 0.03f, 0.08f),
                _ => new Color(0.45f, 0.62f, 0.16f)
            };
        }

        private void BuildMonsterAbilitySets()
        {
            monsterAbilitySets.Clear();
            monsterAbilitySets[BattleArenaMonsterArchetype3D.Ravager] = Array.Empty<RaidAbilitySpec>();
            monsterAbilitySets[BattleArenaMonsterArchetype3D.Brute] = new[]
            {
                Ability("brute-slam", "Crushing Slam", RaidAbilityEffect.AreaDamage,
                    2.4f, 2.8f, 9f, 6f, 0f, 0.4f, new Color(1f, 0.25f, 0.05f))
            };
            monsterAbilitySets[BattleArenaMonsterArchetype3D.Spitter] = new[]
            {
                Ability("spitter-acid", "Acid Glob", RaidAbilityEffect.DamageOverTime,
                    9f, 0f, 5f, 5f, 8f, 4f, new Color(0.35f, 1f, 0.08f))
            };
            monsterAbilitySets[BattleArenaMonsterArchetype3D.Hexer] = new[]
            {
                Ability("hexer-bind", "Void Bind", RaidAbilityEffect.Freeze,
                    8f, 0f, 5f, 6.5f, 12f, 1.1f, new Color(0.7f, 0.2f, 1f))
            };
            monsterAbilitySets[BattleArenaMonsterArchetype3D.Charger] = new[]
            {
                Ability("charger-rush", "Horn Rush", RaidAbilityEffect.DashStrike,
                    7f, 0f, 10f, 6f, 0f, 0f, new Color(1f, 0.45f, 0.05f))
            };
            monsterAbilitySets[BattleArenaMonsterArchetype3D.Elite] = new[]
            {
                Ability("elite-eruption", "Dread Eruption", RaidAbilityEffect.AreaDamage,
                    3f, 4f, 13f, 7f, 12f, 0.6f, new Color(1f, 0.03f, 0.1f)),
                Ability("elite-curse", "Dread Mark", RaidAbilityEffect.DamageMark,
                    8f, 0f, 7f, 8f, 10f, 4f, new Color(0.75f, 0.05f, 1f))
            };
        }

        private static RaidAbilitySpec Ability(
            string id,
            string name,
            RaidAbilityEffect effect,
            float range,
            float radius,
            float power,
            float cooldown,
            float mana,
            float duration,
            Color color)
        {
            return RaidAbilitySpec.Create(id, name, effect, range, radius, power, cooldown, mana, color, duration);
        }

        private void FaceVisibleBars()
        {
            for (var index = 0; index < hunters.Length; index++) hunters[index]?.View?.FaceHealthBar(battleCamera);
            for (var index = 0; index < activeMonsters.Count; index++)
                activeMonsters[index]?.View?.FaceHealthBar(battleCamera);
        }

        private void SamplePerformance()
        {
            sampleUnscaledElapsed += Time.unscaledDeltaTime;
            sampleFrames++;
            if (sampleUnscaledElapsed < 1f) return;
            displayedFps = sampleFrames / Mathf.Max(0.001f, sampleUnscaledElapsed);
            displayedFrameMilliseconds = 1000f / Mathf.Max(0.01f, displayedFps);
            displayedDecisionsPerSecond = Mathf.RoundToInt(decisionsThisSample / sampleUnscaledElapsed);
            displayedAttacksPerSecond = Mathf.RoundToInt(attacksThisSample / sampleUnscaledElapsed);
            displayedHitsPerSecond = Mathf.RoundToInt(hitsThisSample / sampleUnscaledElapsed);
            displayedAbilitiesPerSecond = Mathf.RoundToInt(abilitiesThisSample / sampleUnscaledElapsed);
            sampleUnscaledElapsed = 0f;
            sampleFrames = 0;
            decisionsThisSample = attacksThisSample = hitsThisSample = abilitiesThisSample = 0;
        }

        public bool IsPointerOverHud(Vector2 screenPosition)
        {
            if (!showHud) return false;
            var scale = Mathf.Max(0.01f, currentHudScale);
            var point = new Vector2(screenPosition.x / scale, (Screen.height - screenPosition.y) / scale);
            var width = Screen.width / scale;
            var height = Screen.height / scale;
            var compact = compactHud;
            if (compact)
            {
                var battle = new Rect(12f, 12f, Mathf.Min(470f, width - 24f), 190f);
                if (battle.Contains(point)) return true;
                var stacked = width < 790f;
                var selectedWidth = stacked ? Mathf.Min(470f, width - 24f) : 300f;
                var selected = new Rect(stacked ? 12f : width - selectedWidth - 12f,
                    stacked ? 208f : 12f, selectedWidth, 186f);
                return selected.Contains(point);
            }
            if (new Rect(12f, 12f, 510f, 282f).Contains(point) ||
                new Rect(width - 367f, 12f, 355f, 282f).Contains(point)) return true;
            return point.y >= height - 242f;
        }

        private void SetSimulationSpeed(float value)
        {
            if (presentationController != null) presentationController.SetSimulationSpeed(value);
            else Time.timeScale = Mathf.Clamp(value, 0.25f, 2f);
        }

        private string SimulationSpeedLabel()
        {
            if (presentationController != null)
            {
                return presentationController.IsPaused
                    ? $"PAUSED (resume {presentationController.ActiveSimulationSpeed:0.##}x)"
                    : $"{presentationController.ActiveSimulationSpeed:0.##}x";
            }
            return Time.timeScale <= 0.001f ? "PAUSED" : $"{Time.timeScale:0.##}x";
        }

        private void OnGUI()
        {
            if (!showHud) return;
            var requestedScale = PresentationOptions.UiScale;
            compactHud = Screen.height / requestedScale < 600f || Screen.width / requestedScale < 1120f;
            var fitScale = compactHud
                ? Mathf.Min(Screen.width / 820f, Screen.height / 440f)
                : Mathf.Min(Screen.width / 1180f, Screen.height / 620f);
            currentHudScale = Mathf.Clamp(Mathf.Max(0.8f, fitScale) * requestedScale, 0.8f, 1.5f);
            var previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(currentHudScale, currentHudScale, 1f));
            guiWidth = Screen.width / currentHudScale;
            guiHeight = Screen.height / currentHudScale;
            EnsureStyles();
            if (compactHud)
            {
                DrawCompactBattleHud();
                DrawCompactSelectedHud();
            }
            else
            {
                DrawBattleHud();
                DrawSelectedHud();
                DrawSquadHud();
            }
            GUI.matrix = previous;
        }

        private void DrawCompactBattleHud()
        {
            var panel = new Rect(12f, 12f, Mathf.Min(470f, guiWidth - 24f), 190f);
            DrawPanel(panel);
            GUI.Label(new Rect(24f, 18f, 445f, 22f), "3D TEST ARENA - HORDE SURVIVAL", titleStyle);
            GUI.Label(new Rect(24f, 42f, 445f, 18f),
                phase == BattleArenaPhase3D.Prewarming
                    ? $"PREWARM {allMonsterUnits.Count}/{monsterPoolCapacity}"
                    : $"ROUND {currentRound} - {phase.ToString().ToUpperInvariant()}  |  {SimulationSpeedLabel()}", labelStyle);
            if (GUI.Button(new Rect(24f, 65f, 62f, 23f), "0.25x")) SetSimulationSpeed(0.25f);
            if (GUI.Button(new Rect(90f, 65f, 62f, 23f), "0.5x")) SetSimulationSpeed(0.5f);
            if (GUI.Button(new Rect(156f, 65f, 62f, 23f), "1x")) SetSimulationSpeed(1f);
            if (GUI.Button(new Rect(222f, 65f, 62f, 23f), "2x")) SetSimulationSpeed(2f);
            if (GUI.Button(new Rect(288f, 65f, 82f, 23f), "+220")) AddStressEnemies(220);
            if (GUI.Button(new Rect(374f, 65f, 92f, 23f), "RESTART")) RestartBattle();
            GUI.Label(new Rect(24f, 94f, 445f, 18f),
                $"Hunters {LivingHunterCount}/{HunterCount}   Horde {activeMonsters.Count}   " +
                $"Wave {spawnedThisRound}/{roundQuota}   Peak {peakCombatants}", labelStyle);
            GUI.Label(new Rect(24f, 114f, 445f, 18f),
                $"FPS {displayedFps:0.0} / {displayedFrameMilliseconds:0.0}ms   AI/s {displayedDecisionsPerSecond}   " +
                $"Hit/s {displayedHitsPerSecond}   Cast/s {displayedAbilitiesPerSecond}", smallStyle);
            GUI.Label(new Rect(24f, 134f, 445f, 36f), latestEvent, smallStyle);
            GUI.Label(new Rect(24f, 170f, 445f, 16f),
                "Expand the Game view vertically for full three-squad status cards.", smallStyle);
        }

        private void DrawCompactSelectedHud()
        {
            var stacked = guiWidth < 790f;
            var width = stacked ? Mathf.Min(470f, guiWidth - 24f) : 300f;
            var panel = new Rect(stacked ? 12f : guiWidth - width - 12f, stacked ? 208f : 12f, width, 186f);
            DrawPanel(panel);
            GUI.Label(new Rect(panel.x + 12f, panel.y + 7f, width - 24f, 20f), "COMBATANT", titleStyle);
            if (selectedUnit == null)
            {
                GUI.Label(new Rect(panel.x + 12f, panel.y + 34f, width - 24f, 42f),
                    "Left-click a unit to inspect.\nWASD pan - Q/E orbit - R/F zoom.", smallStyle);
                return;
            }
            var snapshot = selectedUnit.Snapshot();
            GUI.Label(new Rect(panel.x + 12f, panel.y + 31f, width - 24f, 18f),
                $"{snapshot.DisplayName} - {snapshot.LifeState}", labelStyle);
            GUI.Label(new Rect(panel.x + 12f, panel.y + 51f, width - 24f, 30f), snapshot.Build, smallStyle);
            GUI.Label(new Rect(panel.x + 12f, panel.y + 80f, width - 24f, 18f),
                $"HP {snapshot.Health:0}/{snapshot.MaximumHealth:0}   MP {snapshot.Mana:0}/{snapshot.MaximumMana:0}   SH {snapshot.Shield:0}",
                smallStyle);
            DrawBar(new Rect(panel.x + 12f, panel.y + 99f, width - 24f, 9f),
                snapshot.MaximumHealth <= 0f ? 0f : snapshot.Health / snapshot.MaximumHealth,
                HealthBarColor());
            DrawBar(new Rect(panel.x + 12f, panel.y + 112f, width - 24f, 6f),
                snapshot.MaximumMana <= 0f ? 0f : snapshot.Mana / snapshot.MaximumMana,
                ManaBarColor());
            GUI.Label(new Rect(panel.x + 12f, panel.y + 122f, width - 24f, 17f),
                $"STATUS: {selectedUnit.StatusSummary(combatTime)}", smallStyle);
            GUI.Label(new Rect(panel.x + 12f, panel.y + 140f, width - 24f, 24f), snapshot.Objective, smallStyle);
            GUI.Label(new Rect(panel.x + 12f, panel.y + 167f, width - 24f, 16f),
                $"Kills {snapshot.Kills}   Damage {snapshot.DamageDealt:0}   Casts {snapshot.AbilityCasts}", smallStyle);
        }

        private void DrawBattleHud()
        {
            var panel = new Rect(12f, 12f, 510f, 282f);
            DrawPanel(panel);
            GUI.Label(new Rect(26f, 20f, 480f, 24f), "3D TEST ARENA - HORDE SURVIVAL", titleStyle);
            GUI.Label(new Rect(26f, 46f, 480f, 20f),
                phase == BattleArenaPhase3D.Prewarming
                    ? $"PREWARMING MONSTERS {allMonsterUnits.Count}/{monsterPoolCapacity}"
                    : $"ROUND {currentRound} - {phase.ToString().ToUpperInvariant()}  |  {SimulationSpeedLabel()}", labelStyle);
            if (GUI.Button(new Rect(26f, 72f, 76f, 25f), "0.25x")) SetSimulationSpeed(0.25f);
            if (GUI.Button(new Rect(106f, 72f, 76f, 25f), "0.5x")) SetSimulationSpeed(0.5f);
            if (GUI.Button(new Rect(186f, 72f, 76f, 25f), "1x")) SetSimulationSpeed(1f);
            if (GUI.Button(new Rect(266f, 72f, 76f, 25f), "2x")) SetSimulationSpeed(2f);
            if (GUI.Button(new Rect(346f, 72f, 160f, 25f), "RESTART BATTLE")) RestartBattle();
            if (GUI.Button(new Rect(26f, 102f, 150f, 25f), "STRESS +220")) AddStressEnemies(220);
            if (GUI.Button(new Rect(182f, 102f, 150f, 25f), "THREAT +5")) EscalateFiveRounds();
            GUI.Label(new Rect(26f, 136f, 480f, 19f),
                $"Hunters {LivingHunterCount}/{HunterCount}   Monsters {activeMonsters.Count}   " +
                $"Wave {spawnedThisRound}/{roundQuota}", labelStyle);
            GUI.Label(new Rect(26f, 156f, 480f, 19f),
                $"Scaling: HP x{RoundHealthMultiplier:0.00}   Damage x{RoundDamageMultiplier:0.00}   " +
                $"Speed x{RoundSpeedMultiplier:0.00}", labelStyle);
            GUI.Label(new Rect(26f, 176f, 480f, 19f),
                $"FPS {displayedFps:0.0} ({displayedFrameMilliseconds:0.0} ms)   " +
                $"Memory {Profiler.GetTotalAllocatedMemoryLong() / (1024f * 1024f):0} MB", labelStyle);
            GUI.Label(new Rect(26f, 196f, 480f, 19f),
                $"AI/s {displayedDecisionsPerSecond}   Attacks/s {displayedAttacksPerSecond}   " +
                $"Hits/s {displayedHitsPerSecond}   Abilities/s {displayedAbilitiesPerSecond}", labelStyle);
            GUI.Label(new Rect(26f, 216f, 480f, 19f),
                $"Peak {peakCombatants} combatants / {peakConcurrentMonsters} monsters   " +
                $"AOE {areaAbilityCasts}   Status {statusApplications}   Projectiles {vfxPool.ActiveProjectileCount}",
                labelStyle);
            GUI.Label(new Rect(26f, 238f, 480f, 34f), latestEvent, smallStyle);
        }

        private void DrawSelectedHud()
        {
            const float width = 355f;
            var panel = new Rect(guiWidth - width - 12f, 12f, width, 282f);
            DrawPanel(panel);
            GUI.Label(new Rect(panel.x + 14f, panel.y + 8f, width - 28f, 22f),
                "SELECTED COMBATANT", titleStyle);
            if (selectedUnit == null)
            {
                GUI.Label(new Rect(panel.x + 14f, panel.y + 42f, width - 28f, 38f),
                    "Left-click a hunter or monster.\nWASD pan, Q/E orbit, R/F zoom.", labelStyle);
                return;
            }
            var snapshot = selectedUnit.Snapshot();
            var x = panel.x + 14f;
            var y = panel.y + 38f;
            GUI.Label(new Rect(x, y, width - 28f, 21f),
                $"{snapshot.DisplayName} - {snapshot.LifeState}", labelStyle);
            y += 21f;
            GUI.Label(new Rect(x, y, width - 28f, 34f), snapshot.Build, smallStyle);
            y += 32f;
            GUI.Label(new Rect(x, y, width - 28f, 17f),
                $"HP {snapshot.Health:0}/{snapshot.MaximumHealth:0}", smallStyle);
            y += 17f;
            DrawBar(new Rect(x, y, width - 28f, 12f),
                snapshot.MaximumHealth <= 0f ? 0f : snapshot.Health / snapshot.MaximumHealth,
                HealthBarColor());
            y += 14f;
            GUI.Label(new Rect(x, y, width - 28f, 17f),
                $"MP {snapshot.Mana:0}/{snapshot.MaximumMana:0}   SHIELD {snapshot.Shield:0}", smallStyle);
            y += 17f;
            DrawBar(new Rect(x, y, width - 28f, 8f),
                snapshot.MaximumMana <= 0f ? 0f : snapshot.Mana / snapshot.MaximumMana,
                ManaBarColor());
            y += 11f;
            GUI.Label(new Rect(x, y, width - 28f, 17f),
                $"STATUS: {selectedUnit.StatusSummary(combatTime)}", smallStyle);
            y += 18f;
            GUI.Label(new Rect(x, y, width - 28f, 27f), snapshot.Objective, smallStyle);
            y += 28f;
            GUI.Label(new Rect(x, y, width - 28f, 18f),
                $"Kills {snapshot.Kills}   Damage {snapshot.DamageDealt:0}   Casts {snapshot.AbilityCasts}",
                smallStyle);
            y += 19f;
            for (var index = 0; index < selectedUnit.Abilities.Count && index < 3; index++)
            {
                var ability = selectedUnit.Abilities[index];
                var remaining = selectedUnit.AbilityCooldownRemaining(index, combatTime);
                GUI.Label(new Rect(x, y, width - 28f, 18f),
                    $"{ability.displayName}: {(remaining <= 0f ? "READY" : $"{remaining:0.0}s")}", smallStyle);
                y += 18f;
            }
        }

        private void DrawSquadHud()
        {
            const float gap = 8f;
            var width = Mathf.Min(360f, (guiWidth - 24f - gap * 2f) / 3f);
            const float height = 230f;
            var y = guiHeight - height - 12f;
            for (var squadIndex = 0; squadIndex < squads.Length; squadIndex++)
            {
                var panel = new Rect(12f + squadIndex * (width + gap), y, width, height);
                DrawPanel(panel);
                var squad = squads[squadIndex];
                GUI.Label(new Rect(panel.x + 9f, panel.y + 5f, panel.width - 18f, 20f),
                    $"{squad.Name.ToUpperInvariant()} - {squad.Order}", labelStyle);
                var rowY = panel.y + 28f;
                for (var index = 0; index < squad.Members.Count; index++)
                {
                    var unit = squad.Members[index];
                    if (unit == null) continue;
                    var row = new Rect(panel.x + 7f, rowY, panel.width - 14f, 19f);
                    if (GUI.Button(row, GUIContent.none)) SelectUnit(unit);
                    GUI.Label(new Rect(row.x + 3f, row.y + 1f, 118f, 17f),
                        $"{(unit == squad.Sergeant ? "[S] " : string.Empty)}{unit.DisplayName}",
                        unit.LifeState == BattleArenaLifeState3D.Dead ? deadStyle : smallStyle);
                    DrawBar(new Rect(row.x + 122f, row.y + 4f, 74f, 6f), unit.HealthRatio,
                        HealthBarColor());
                    var abilityX = row.x + 202f;
                    for (var abilityIndex = 0; abilityIndex < unit.Abilities.Count && abilityIndex < 3;
                         abilityIndex++)
                    {
                        var remaining = unit.AbilityCooldownRemaining(abilityIndex, combatTime);
                        var ready = remaining <= 0f;
                        var color = ready
                            ? new Color(0.22f, 0.9f, 0.32f)
                            : new Color(0.25f, 0.28f, 0.34f);
                        var pip = new Rect(abilityX + abilityIndex * 38f, row.y + 2f, 34f, 15f);
                        DrawSolid(pip, color);
                        var textColor = GUI.color;
                        GUI.color = Color.white;
                        GUI.Label(pip, ready ? $"{abilityIndex + 1} R" : $"{abilityIndex + 1} {Mathf.CeilToInt(remaining)}",
                            smallStyle);
                        GUI.color = textColor;
                    }
                    rowY += 20f;
                }
            }
        }

        private void EnsureStyles()
        {
            if (titleStyle != null) return;
            panelTexture = MakeTexture(new Color(0.018f, 0.025f, 0.045f, 0.9f));
            barBackgroundTexture = MakeTexture(new Color(0.035f, 0.04f, 0.055f, 0.95f));
            pixelTexture = MakeTexture(Color.white);
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.74f, 0.9f, 1f) }
            };
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                normal = { textColor = new Color(0.9f, 0.94f, 1f) }
            };
            smallStyle = new GUIStyle(labelStyle)
            {
                fontSize = 10,
                wordWrap = true,
                normal = { textColor = new Color(0.75f, 0.8f, 0.88f) }
            };
            deadStyle = new GUIStyle(smallStyle)
            {
                normal = { textColor = new Color(0.48f, 0.48f, 0.5f) }
            };
        }

        private void DrawPanel(Rect rect)
        {
            GUI.DrawTexture(rect, panelTexture, ScaleMode.StretchToFill);
            DrawOutline(rect, new Color(0.16f, 0.45f, 0.72f, 0.7f));
        }

        private void DrawBar(Rect rect, float ratio, Color color)
        {
            GUI.DrawTexture(rect, barBackgroundTexture);
            DrawSolid(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(ratio), rect.height), color);
        }

        private Color HealthBarColor()
        {
            return PresentationOptions.HighContrastFactions
                ? new Color(0.9f, 0.22f, 0.08f)
                : new Color(0.86f, 0.05f, 0.04f);
        }

        private Color ManaBarColor()
        {
            return PresentationOptions.HighContrastFactions
                ? new Color(0f, 0.65f, 0.92f)
                : new Color(0.08f, 0.4f, 1f);
        }

        private void DrawSolid(Rect rect, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, pixelTexture);
            GUI.color = previous;
        }

        private void DrawOutline(Rect rect, Color color)
        {
            DrawSolid(new Rect(rect.x, rect.y, rect.width, 1f), color);
            DrawSolid(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), color);
            DrawSolid(new Rect(rect.x, rect.y, 1f, rect.height), color);
            DrawSolid(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), color);
        }

        private static Texture2D MakeTexture(Color color)
        {
            var texture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private sealed class RaycastHitDistanceComparer : IComparer<RaycastHit>
        {
            public static readonly RaycastHitDistanceComparer Instance = new();
            public int Compare(RaycastHit x, RaycastHit y) => x.distance.CompareTo(y.distance);
        }

#if UNITY_EDITOR
        public void ConfigureEditor(
            BattleArena3DUnit[] authoredHunters,
            BattleArena3DUnit authoredMonsterTemplate,
            Transform authoredMonsterRoot,
            Transform[] authoredPortals,
            Transform[] authoredRallyPoints,
            BattleArena3DVfxPool authoredVfx,
            Camera authoredCamera,
            int poolCapacity,
            BattleArena3DPresentationController authoredPresentation,
            BattleArena3DCombatFeedback authoredFeedback)
        {
            hunters = authoredHunters ?? Array.Empty<BattleArena3DUnit>();
            monsterTemplate = authoredMonsterTemplate;
            activeMonsterRoot = authoredMonsterRoot;
            hordePortals = authoredPortals ?? Array.Empty<Transform>();
            squadRallyPoints = authoredRallyPoints ?? Array.Empty<Transform>();
            vfxPool = authoredVfx;
            battleCamera = authoredCamera;
            monsterPoolCapacity = Mathf.Clamp(poolCapacity, 220, 500);
            presentationController = authoredPresentation;
            combatFeedback = authoredFeedback;
        }
#endif
    }
}
