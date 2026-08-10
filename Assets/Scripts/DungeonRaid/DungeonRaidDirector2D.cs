using System;
using System.Collections.Generic;
using UnityEngine;

namespace Turtle.DungeonRaid
{
    [DisallowMultipleComponent]
    public sealed class DungeonRaidDirector2D : MonoBehaviour
    {
        private sealed class PersistentRaidField
        {
            public RaidAgent2D Source;
            public Vector2 Center;
            public float Radius;
            public float PowerPerTick;
            public float StatusDuration;
            public float NextTick;
            public float ExpiresAt;
            public bool HealsAllies;
            public Color Color;
        }

        [SerializeField] private RaidPartyBrain2D party;
        [SerializeField] private RaidEnemyPodBrain2D[] enemyPods =
            Array.Empty<RaidEnemyPodBrain2D>();
        [SerializeField] private RaidRoom2D[] rooms = Array.Empty<RaidRoom2D>();
        [SerializeField] private RaidRoomConnection2D[] connections =
            Array.Empty<RaidRoomConnection2D>();
        [SerializeField] private RaidChest2D[] chests = Array.Empty<RaidChest2D>();
        [SerializeField] private RaidFxPool2D effects;
        [SerializeField] private RaidCamera2D raidCamera;
        [SerializeField] private RaidHud2D hud;
        [SerializeField, Min(0.05f)] private float decisionInterval = 0.12f;
        [SerializeField, Min(0f)] private float automaticStartDelay = 1f;
        [SerializeField] private bool beginAutomatically = true;

        private readonly List<RaidAgent2D> hunters = new(12);
        private readonly List<RaidAgent2D> monsters = new(32);
        private readonly Queue<RaidRoom2D> routeQueue = new();
        private readonly HashSet<RaidRoom2D> routeVisited = new();
        private readonly Dictionary<RaidRoom2D, RaidRoom2D> routePrevious = new();
        private readonly RaidCombatPhysics2D combatPhysics = new();
        private readonly List<RaidAgent2D> contactTargets = new(32);
        private readonly List<PersistentRaidField> persistentFields = new(8);

        private RaidEnemyPodBrain2D routedPod;
        private RaidRoomConnection2D activeRouteConnection;
        private RaidRoom2D activeRouteFrom;
        private RaidRoom2D activeRouteTo;
        private int activeRouteWaypoint;

        private float decisionAccumulator;
        private float startCountdown;
        private float raidTime;
        private bool running;
        private bool externalCombatMode;
        private string latestEvent = "Waiting for the strike team.";
        private string resultMessage = string.Empty;

        public RaidPartyBrain2D Party => party;
        public IReadOnlyList<RaidEnemyPodBrain2D> EnemyPods => enemyPods;
        public IReadOnlyList<RaidAgent2D> Hunters => hunters;
        public IReadOnlyList<RaidAgent2D> Monsters => monsters;
        public IReadOnlyList<RaidChest2D> Chests => chests;
        public RaidFxPool2D Effects => effects;
        public float RaidTime => raidTime;
        public bool IsRunning => running;
        public string LatestEvent => latestEvent;
        public string ResultMessage => resultMessage;

        /// <summary>
        /// Read-only telemetry hook. Combat remains authoritative here; survival
        /// labs use this event to count and inspect casts without duplicating the
        /// ability resolver.
        /// </summary>
        public event Action<RaidAgent2D, RaidAbilitySpec> AbilityResolved;

        private void Awake()
        {
            RebuildAgentCache();
        }

        private void Start()
        {
            ResetRaid();
        }

        private void Update()
        {
            EnsureAgentCacheCurrent();
            var deltaTime = Mathf.Min(0.1f, Time.deltaTime);
            if (!running)
            {
                if (beginAutomatically && string.IsNullOrEmpty(resultMessage))
                {
                    startCountdown -= deltaTime;
                    if (startCountdown <= 0f)
                    {
                        running = true;
                        party?.BeginRaid(this, raidTime);
                        PublishEvent(
                            $"The {hunters.Count}-hunter strike team entered the dungeon.");
                    }
                }
                StepAgents(deltaTime);
                return;
            }

            raidTime += deltaTime;
            decisionAccumulator += deltaTime;
            var steps = 0;
            while (decisionAccumulator >= decisionInterval && steps++ < 4)
            {
                decisionAccumulator -= decisionInterval;
                party?.Tick(this, raidTime);
                for (var index = 0; index < enemyPods.Length; index++)
                {
                    enemyPods[index]?.Tick(this, raidTime);
                }
                EvaluateOutcome();
            }
            StepAgents(deltaTime);
            StepPersistentFields();
        }

        public void ResetRaid()
        {
            externalCombatMode = false;
            RebuildAgentCache();
            raidTime = 0f;
            decisionAccumulator = 0f;
            startCountdown = automaticStartDelay;
            running = false;
            ClearActiveRoute();
            resultMessage = string.Empty;
            latestEvent = "Strike team assembling at the entrance.";
            for (var index = 0; index < hunters.Count; index++)
            {
                hunters[index].ResetForRaid();
            }
            for (var index = 0; index < monsters.Count; index++)
            {
                monsters[index].ResetForRaid();
            }
            persistentFields.Clear();
            for (var index = 0; index < enemyPods.Length; index++)
            {
                enemyPods[index]?.ResetPod();
            }
            for (var index = 0; index < chests.Length; index++)
            {
                chests[index]?.ResetChest();
            }
            party?.ResetParty();
        }

        /// <summary>
        /// Supplies a rendered combat scenario that owns its own objective and
        /// round state. The disabled dungeon director still provides the shared
        /// contact, ability, status, and pooled-FX resolution contract.
        /// </summary>
        public void BeginExternalCombat(
            IReadOnlyList<RaidAgent2D> externalHunters,
            IReadOnlyList<RaidAgent2D> externalMonsters,
            float combatTime = 0f)
        {
            externalCombatMode = true;
            running = false;
            raidTime = Mathf.Max(0f, combatTime);
            persistentFields.Clear();
            ReplaceRoster(hunters, externalHunters);
            ReplaceRoster(monsters, externalMonsters);
        }

        /// <summary>
        /// Advances timed status fields while the dungeon objective Update loop
        /// is disabled. Call once per frame before external AI decisions.
        /// </summary>
        public void StepExternalCombat(float combatTime)
        {
            if (!externalCombatMode) return;
            raidTime = Mathf.Max(raidTime, combatTime);
            StepPersistentFields();
        }

        public void EndExternalCombat()
        {
            externalCombatMode = false;
            persistentFields.Clear();
            hunters.Clear();
            monsters.Clear();
        }

        public void PublishEvent(string message)
        {
            if (!string.IsNullOrWhiteSpace(message)) latestEvent = message;
        }

        public bool ResolveBasicAttack(
            RaidAgent2D caster,
            RaidAgent2D intendedTarget,
            float rawDamage)
        {
            return ResolveBasicAttack(caster, intendedTarget, rawDamage, raidTime);
        }

        /// <summary>
        /// Resolves the shared cast-based attack contract against an explicit
        /// combat clock. Large rendered battles can reuse the same hurtbox rules
        /// without making the dungeon objective state machine authoritative.
        /// </summary>
        public bool ResolveBasicAttack(
            RaidAgent2D caster,
            RaidAgent2D intendedTarget,
            float rawDamage,
            float combatTime)
        {
            if (caster == null || intendedTarget == null || !caster.CanAct) return false;
            var direction = intendedTarget.Position - caster.Position;
            var castRadius = caster.RangedBasicAttack
                ? 0.12f
                : caster.CollisionRadius * 0.55f;
            if (!combatPhysics.TrySingleHit(
                    caster,
                    direction,
                    caster.BasicAttackRange,
                    castRadius,
                    out var contacted,
                    out var impactPoint))
            {
                Effects?.EmitText(
                    impactPoint + Vector2.up * 0.35f,
                    "MISS",
                    new Color(0.72f, 0.76f, 0.82f));
                return false;
            }
            contacted.ReceiveDamage(caster, rawDamage, combatTime, this);
            return true;
        }

        public RaidEnemyPodBrain2D FindNextPod()
        {
            RaidEnemyPodBrain2D best = null;
            for (var index = 0; index < enemyPods.Length; index++)
            {
                var candidate = enemyPods[index];
                if (candidate == null || candidate.IsDefeated) continue;
                if (best == null || candidate.Order < best.Order) best = candidate;
            }
            return best;
        }

        public RaidChest2D FindChestForPod(RaidEnemyPodBrain2D pod)
        {
            RaidChest2D best = null;
            var bestDistance = float.MaxValue;
            for (var index = 0; index < chests.Length; index++)
            {
                var chest = chests[index];
                if (chest == null || chest.IsOpened || !chest.CanOpen) continue;
                var distance = pod == null
                    ? Vector2.Distance(chest.Position, PartyCentroid())
                    : Vector2.Distance(chest.Position, pod.ActivationCenter);
                if (distance >= bestDistance) continue;
                best = chest;
                bestDistance = distance;
            }
            return best;
        }

        public bool AllChestsOpened()
        {
            for (var index = 0; index < chests.Length; index++)
            {
                if (chests[index] != null && !chests[index].IsOpened) return false;
            }
            return true;
        }

        public Vector2 PartyCentroid()
        {
            var sum = Vector2.zero;
            var count = 0;
            for (var index = 0; index < hunters.Count; index++)
            {
                if (!hunters[index].CanAct) continue;
                sum += hunters[index].Position;
                count++;
            }
            return count > 0 ? sum / count : Vector2.zero;
        }

        public Vector2 GetAdvanceWaypoint(
            Vector2 partyPosition,
            RaidEnemyPodBrain2D pod)
        {
            if (pod?.Room == null) return pod?.ActivationCenter ?? partyPosition;
            if (routedPod != pod)
            {
                ClearActiveRoute();
                routedPod = pod;
            }

            var currentRoom = FindRoom(partyPosition);
            if (currentRoom == pod.Room)
            {
                ClearActiveRoute();
                routedPod = pod;
                return pod.ActivationCenter;
            }

            if (activeRouteConnection != null)
            {
                if (currentRoom == activeRouteTo)
                {
                    activeRouteConnection = null;
                    activeRouteFrom = null;
                    activeRouteTo = null;
                    activeRouteWaypoint = 0;
                }
                else
                {
                    var forward = activeRouteConnection.FromRoom == activeRouteFrom;
                    while (activeRouteWaypoint < activeRouteConnection.WaypointCount)
                    {
                        var point = activeRouteConnection.GetWaypoint(activeRouteWaypoint, forward);
                        var arrivalRadius = Mathf.Max(
                            1.25f,
                            activeRouteConnection.Width * 0.42f);
                        if (!HasPartyQuorumReached(point, arrivalRadius))
                        {
                            return point;
                        }
                        activeRouteWaypoint++;
                    }
                    return activeRouteTo != null ? activeRouteTo.Center : pod.ActivationCenter;
                }
            }

            if (currentRoom == null) return pod.ActivationCenter;
            var nextRoom = FindNextRoomOnPath(currentRoom, pod.Room);
            if (nextRoom == null) return pod.ActivationCenter;
            for (var index = 0; index < connections.Length; index++)
            {
                var connection = connections[index];
                if (connection == null ||
                    !connection.Connects(currentRoom, nextRoom)) continue;
                activeRouteConnection = connection;
                activeRouteFrom = currentRoom;
                activeRouteTo = nextRoom;
                activeRouteWaypoint = 0;
                var forward = connection.FromRoom == currentRoom;
                return connection.GetWaypoint(0, forward);
            }
            return pod.ActivationCenter;
        }

        public void ConfigureGeneratedLayout(
            RaidRoom2D[] generatedRooms,
            RaidRoomConnection2D[] generatedConnections)
        {
            rooms = generatedRooms ?? Array.Empty<RaidRoom2D>();
            connections = generatedConnections ?? Array.Empty<RaidRoomConnection2D>();
            ClearActiveRoute();
            RebuildAgentCache();
        }

        private void ClearActiveRoute()
        {
            routedPod = null;
            activeRouteConnection = null;
            activeRouteFrom = null;
            activeRouteTo = null;
            activeRouteWaypoint = 0;
        }

        private bool HasPartyQuorumReached(Vector2 point, float radius)
        {
            var activeCount = 0;
            var arrivedCount = 0;
            for (var index = 0; index < hunters.Count; index++)
            {
                var hunter = hunters[index];
                if (hunter == null || !hunter.CanAct) continue;
                activeCount++;
                if (Vector2.Distance(hunter.Position, point) <= radius) arrivedCount++;
            }
            if (activeCount == 0) return false;
            var requiredCount = Mathf.Max(1, Mathf.CeilToInt(activeCount * 0.6f));
            return arrivedCount >= requiredCount;
        }

        public RaidRoom2D FindRoom(Vector2 position)
        {
            RaidRoom2D closest = null;
            var closestDistance = float.MaxValue;
            for (var index = 0; index < rooms.Length; index++)
            {
                var room = rooms[index];
                if (room == null || !room.Contains(position)) continue;
                var distance = Vector2.SqrMagnitude(position - room.Center);
                if (distance >= closestDistance) continue;
                closest = room;
                closestDistance = distance;
            }
            return closest;
        }

        public RaidAgent2D FindNearestActiveEnemy(
            RaidAgent2D source,
            IReadOnlyList<RaidAgent2D> candidates)
        {
            if (source == null || candidates == null) return null;
            RaidAgent2D best = null;
            var bestDistance = float.MaxValue;
            for (var index = 0; index < candidates.Count; index++)
            {
                var candidate = candidates[index];
                if (candidate == null || !candidate.CanReceiveDamage ||
                    candidate.Faction == source.Faction) continue;
                var distance = Vector2.SqrMagnitude(
                    source.Position - candidate.Position);
                if (distance >= bestDistance) continue;
                best = candidate;
                bestDistance = distance;
            }
            return best;
        }

        public bool TryUseBestAbility(
            RaidAgent2D caster,
            RaidAgent2D preferredTarget)
        {
            if (caster == null || !caster.CanAct) return false;
            RaidAbilitySpec bestAbility = null;
            RaidAgent2D bestTarget = null;
            var bestScore = 0f;
            for (var index = 0; index < caster.Abilities.Count; index++)
            {
                var ability = caster.Abilities[index];
                if (!caster.IsAbilityReady(ability, raidTime)) continue;
                var target = preferredTarget;
                var score = ScoreAbility(caster, ability, ref target);
                if (score <= bestScore) continue;
                bestScore = score;
                bestAbility = ability;
                bestTarget = target;
            }
            if (bestAbility == null) return false;
            ResolveAbility(caster, bestTarget, bestAbility);
            return true;
        }

        public bool TryGetFocusBounds(out Bounds bounds)
        {
            var initialized = false;
            bounds = new Bounds(Vector3.zero, Vector3.zero);
            EncapsulateLiving(hunters, ref bounds, ref initialized);
            for (var index = 0; index < enemyPods.Length; index++)
            {
                var pod = enemyPods[index];
                if (pod == null || pod.Phase == RaidPodPhase.Dormant) continue;
                EncapsulateLiving(pod.Members, ref bounds, ref initialized);
            }
            return initialized;
        }

        private RaidRoom2D FindNextRoomOnPath(
            RaidRoom2D start,
            RaidRoom2D destination)
        {
            routeQueue.Clear();
            routeVisited.Clear();
            routePrevious.Clear();
            routeQueue.Enqueue(start);
            routeVisited.Add(start);
            while (routeQueue.Count > 0)
            {
                var room = routeQueue.Dequeue();
                for (var index = 0; index < connections.Length; index++)
                {
                    var connection = connections[index];
                    if (connection == null) continue;
                    RaidRoom2D neighbor = null;
                    if (connection.FromRoom == room) neighbor = connection.ToRoom;
                    else if (connection.ToRoom == room) neighbor = connection.FromRoom;
                    if (neighbor == null || !routeVisited.Add(neighbor)) continue;
                    routePrevious[neighbor] = room;
                    if (neighbor == destination)
                    {
                        var step = destination;
                        while (routePrevious.TryGetValue(step, out var previous) &&
                               previous != start)
                        {
                            step = previous;
                        }
                        return step;
                    }
                    routeQueue.Enqueue(neighbor);
                }
            }
            return null;
        }

        private float ScoreAbility(
            RaidAgent2D caster,
            RaidAbilitySpec ability,
            ref RaidAgent2D target)
        {
            switch (ability.effect)
            {
                case RaidAbilityEffect.Heal:
                    target = FindMostInjuredAlly(caster, ability.range, true);
                    if (target == null) return 0f;
                    return target.CanBeRescued
                        ? 1000f
                        : target.HealthRatio < ability.preferredHealthThreshold
                            ? (1f - target.HealthRatio) * 220f
                            : 0f;
                case RaidAbilityEffect.AreaHeal:
                case RaidAbilityEffect.PersistentAreaHeal:
                    target = caster;
                    return CountInjuredAllies(
                        caster,
                        ability.radius,
                        ability.preferredHealthThreshold) * 70f;
                case RaidAbilityEffect.Shield:
                    if (caster.Faction != RaidFaction.Hunters ||
                        caster.Role != RaidCombatRole.Tank ||
                        (!externalCombatMode &&
                         (party == null || party.Phase != RaidPartyPhase.Engaging)))
                    {
                        return 0f;
                    }
                    target = caster;
                    return CountShieldCandidates(caster, ability.radius) * 34f;
                case RaidAbilityEffect.Taunt:
                    target = caster;
                    return CountEnemiesInRadius(
                        caster,
                        caster.Position,
                        ability.radius) * 45f;
                case RaidAbilityEffect.AreaDamage:
                    target = caster;
                    return CountEnemiesInRadius(
                               caster,
                               caster.Position,
                               ability.radius) *
                           ResolveAbilityPower(caster, null, ability);
                case RaidAbilityEffect.DashStrike:
                case RaidAbilityEffect.ShadowStep:
                    if (target == null || !target.CanReceiveDamage) return 0f;
                    var dashDistance = Vector2.Distance(
                        caster.Position,
                        target.Position);
                    return dashDistance <= ability.range
                        ? ResolveAbilityPower(caster, target, ability) +
                          dashDistance * 3f + 18f
                        : 0f;
                case RaidAbilityEffect.Execute:
                    if (!IsValidHostileTarget(caster, target, ability.range))
                    {
                        return 0f;
                    }
                    return ResolveAbilityPower(caster, target, ability) *
                           Mathf.Lerp(1f, 1.8f, 1f - target.HealthRatio);
                case RaidAbilityEffect.DamageAndBuffAllies:
                case RaidAbilityEffect.DamageOverTime:
                case RaidAbilityEffect.Freeze:
                case RaidAbilityEffect.ChainDamage:
                case RaidAbilityEffect.ProjectileAreaDamage:
                case RaidAbilityEffect.DamageMark:
                case RaidAbilityEffect.PiercingDamage:
                    if (!IsValidHostileTarget(caster, target, ability.range))
                    {
                        return 0f;
                    }
                    if (ability.effect == RaidAbilityEffect.DamageMark &&
                        target.IsVulnerable(raidTime))
                    {
                        return 0f;
                    }
                    return ResolveAbilityPower(caster, target, ability) +
                           (ability.effect == RaidAbilityEffect.DamageMark
                               ? 60f
                               : 0f);
                default:
                    return IsValidHostileTarget(caster, target, ability.range)
                        ? ResolveAbilityPower(caster, target, ability)
                        : 0f;
            }
        }

        private bool IsValidHostileTarget(
            RaidAgent2D caster,
            RaidAgent2D target,
            float range)
        {
            return target != null &&
                   target.CanReceiveDamage &&
                   target.Faction != caster.Faction &&
                   Vector2.Distance(caster.Position, target.Position) <= range &&
                   combatPhysics.HasLineOfSight(caster.Position, target);
        }

        private void ResolveAbility(
            RaidAgent2D caster,
            RaidAgent2D target,
            RaidAbilitySpec ability)
        {
            caster.CommitAbility(ability, raidTime);
            AbilityResolved?.Invoke(caster, ability);
            caster.MarkAbilityCast(ability, target, this);
            Effects?.EmitText(
                caster.Position + Vector2.up,
                ability.displayName,
                ability.color);
            PublishEvent($"{caster.DisplayName} used {ability.displayName}.");
            switch (ability.effect)
            {
                case RaidAbilityEffect.Heal:
                    target?.Heal(
                        ResolveAbilityPower(caster, target, ability),
                        raidTime,
                        this);
                    break;
                case RaidAbilityEffect.AreaHeal:
                    ApplyToAlliesByPhysics(
                        caster,
                        caster.Position,
                        ability.radius,
                        true,
                        ally => ally.Heal(
                            ResolveAbilityPower(caster, ally, ability),
                            raidTime,
                            this));
                    break;
                case RaidAbilityEffect.PersistentAreaHeal:
                    AddPersistentField(caster, caster.Position, ability, true);
                    break;
                case RaidAbilityEffect.Shield:
                    if (caster.Faction != RaidFaction.Hunters ||
                        caster.Role != RaidCombatRole.Tank)
                    {
                        break;
                    }
                    ApplyToAlliesByPhysics(
                        caster,
                        caster.Position,
                        ability.radius,
                        false,
                        ally => ally.GrantTemporaryShield(
                            caster,
                            ResolveAbilityPower(caster, ally, ability),
                            raidTime,
                            this,
                            ability.duration));
                    break;
                case RaidAbilityEffect.Taunt:
                    ApplyToEnemiesByPhysics(
                        caster,
                        caster.Position,
                        Vector2.right,
                        ability,
                        enemy => enemy.ForceTarget(
                            caster,
                            raidTime + ability.duration,
                            ability.multiplier));
                    break;
                case RaidAbilityEffect.AreaDamage:
                    Effects?.EmitBurst(
                        caster.Position,
                        ability.color,
                        ability.radius * 1.6f,
                        0.4f);
                    ApplyToEnemiesByPhysics(
                        caster,
                        caster.Position,
                        Vector2.right,
                        ability,
                        enemy => enemy.ReceiveDamage(
                            caster,
                            ResolveAbilityPower(caster, enemy, ability),
                            raidTime,
                            this));
                    break;
                case RaidAbilityEffect.DashStrike:
                    if (target != null)
                    {
                        caster.TeleportNear(
                            target.Position,
                            caster.Position - target.Position);
                        ResolveContactDamage(caster, target, ability);
                    }
                    break;
                case RaidAbilityEffect.ShadowStep:
                    if (target != null)
                    {
                        caster.TeleportNear(
                            target.Position,
                            caster.Position - target.Position);
                        caster.EmpowerAgainst(
                            target,
                            ability.multiplier,
                            raidTime + ability.duration);
                    }
                    break;
                case RaidAbilityEffect.Execute:
                    ResolveContactDamage(caster, target, ability)?.ApplyStun(
                        raidTime + Mathf.Max(0.01f, ability.duration));
                    break;
                case RaidAbilityEffect.DamageAndBuffAllies:
                    if (ResolveContactDamage(caster, target, ability) != null)
                    {
                        ApplyToAlliesByPhysics(
                            caster,
                            caster.Position,
                            ability.radius,
                            false,
                            ally => ally.GrantDamageBuff(
                                ability.secondaryPower,
                                raidTime + ability.duration));
                    }
                    break;
                case RaidAbilityEffect.DamageOverTime:
                    ResolveContactDamage(caster, target, ability)?.Ignite(
                        caster,
                        ability.secondaryPower,
                        ability.duration,
                        raidTime);
                    break;
                case RaidAbilityEffect.Freeze:
                    ResolveContactDamage(caster, target, ability)?.ApplyStun(
                        raidTime + ability.duration);
                    break;
                case RaidAbilityEffect.ChainDamage:
                    ResolveChainDamage(caster, target, ability);
                    break;
                case RaidAbilityEffect.ProjectileAreaDamage:
                    ResolveFireball(caster, target, ability);
                    break;
                case RaidAbilityEffect.DamageMark:
                    target?.ApplyVulnerability(
                        ability.multiplier,
                        raidTime + ability.duration);
                    break;
                case RaidAbilityEffect.PiercingDamage:
                    ResolvePiercingDamage(caster, target, ability);
                    break;
                default:
                    ResolveContactDamage(caster, target, ability);
                    break;
            }
        }

        private float ResolveAbilityPower(
            RaidAgent2D caster,
            RaidAgent2D target,
            RaidAbilitySpec ability)
        {
            if (ability == null) return 0f;
            if (ability.scalesWithBasicAttack)
            {
                return caster != null
                    ? caster.BasicAttackDamage * ability.multiplier
                    : 0f;
            }
            if (ability.scalesWithTargetMaximumHealth)
            {
                return target != null
                    ? target.MaximumHealth * ability.multiplier
                    : 0f;
            }
            return ability.power;
        }

        private RaidAgent2D ResolveContactDamage(
            RaidAgent2D caster,
            RaidAgent2D intendedTarget,
            RaidAbilitySpec ability)
        {
            if (caster == null || intendedTarget == null || ability == null)
            {
                return null;
            }
            if (!combatPhysics.TrySingleHit(
                    caster,
                    intendedTarget.Position - caster.Position,
                    ability.range,
                    Mathf.Max(0.08f, ability.width),
                    out var contacted,
                    out _))
            {
                return null;
            }
            contacted.ReceiveDamage(
                caster,
                ResolveAbilityPower(caster, contacted, ability),
                raidTime,
                this);
            return contacted;
        }

        private void ResolvePiercingDamage(
            RaidAgent2D caster,
            RaidAgent2D intendedTarget,
            RaidAbilitySpec ability)
        {
            if (caster == null || intendedTarget == null) return;
            var impact = combatPhysics.CollectPiercingHits(
                caster,
                intendedTarget.Position - caster.Position,
                ability.range,
                Mathf.Max(0.06f, ability.width),
                contactTargets,
                ability.maximumTargets);
            Effects?.EmitArc(
                caster.Position,
                impact,
                ability.color,
                ability.width * 2f,
                0.2f);
            for (var index = 0; index < contactTargets.Count; index++)
            {
                var target = contactTargets[index];
                target.ReceiveDamage(
                    caster,
                    ResolveAbilityPower(caster, target, ability),
                    raidTime,
                    this);
            }
        }

        private void ResolveChainDamage(
            RaidAgent2D caster,
            RaidAgent2D primaryTarget,
            RaidAbilitySpec ability)
        {
            var contacted = ResolveContactDamage(caster, primaryTarget, ability);
            if (contacted == null) return;
            combatPhysics.CollectTargets(
                caster,
                RaidAttackShape.Circle,
                contacted.Position,
                Vector2.right,
                0f,
                ability.radius,
                0f,
                360f,
                contactTargets,
                ability.maximumTargets + 1);
            var chained = 0;
            for (var index = 0;
                 index < contactTargets.Count && chained < ability.maximumTargets;
                 index++)
            {
                var candidate = contactTargets[index];
                if (candidate == contacted) continue;
                candidate.ReceiveDamage(
                    caster,
                    ability.secondaryPower,
                    raidTime,
                    this);
                Effects?.EmitArc(
                    contacted.Position,
                    candidate.Position,
                    ability.color,
                    0.16f,
                    0.2f);
                chained++;
            }
        }

        private void ResolveFireball(
            RaidAgent2D caster,
            RaidAgent2D intendedTarget,
            RaidAbilitySpec ability)
        {
            if (caster == null || intendedTarget == null) return;
            combatPhysics.TrySingleHit(
                caster,
                intendedTarget.Position - caster.Position,
                ability.range,
                Mathf.Max(0.1f, ability.width),
                out var directTarget,
                out var impactPoint);
            Effects?.EmitProjectile(
                caster.Position,
                impactPoint,
                ability.color,
                0.28f);
            Effects?.EmitBurst(
                impactPoint,
                ability.color,
                ability.radius * 1.7f,
                0.45f);
            if (directTarget != null)
            {
                directTarget.ReceiveDamage(
                    caster,
                    ability.power,
                    raidTime,
                    this);
                directTarget.Ignite(caster, ability.secondaryPower, 4f, raidTime);
            }
            AddPersistentField(caster, impactPoint, ability, false);
        }

        private void ApplyToEnemiesByPhysics(
            RaidAgent2D caster,
            Vector2 center,
            Vector2 direction,
            RaidAbilitySpec ability,
            Action<RaidAgent2D> action)
        {
            var maximumTargets = ability.maximumTargets <= 1 &&
                                 ability.shape != RaidAttackShape.Single
                ? 64
                : ability.maximumTargets;
            combatPhysics.CollectTargets(
                caster,
                ability.shape,
                center,
                direction,
                ability.range,
                ability.radius,
                ability.width,
                ability.angle,
                contactTargets,
                maximumTargets);
            for (var index = 0; index < contactTargets.Count; index++)
            {
                action(contactTargets[index]);
            }
        }

        private void ApplyToAlliesByPhysics(
            RaidAgent2D caster,
            Vector2 center,
            float radius,
            bool includeDowned,
            Action<RaidAgent2D> action)
        {
            combatPhysics.CollectAlliesInCircle(
                caster,
                center,
                radius,
                contactTargets,
                includeDowned);
            for (var index = 0; index < contactTargets.Count; index++)
            {
                action(contactTargets[index]);
            }
        }

        private void AddPersistentField(
            RaidAgent2D caster,
            Vector2 center,
            RaidAbilitySpec ability,
            bool healsAllies)
        {
            persistentFields.Add(new PersistentRaidField
            {
                Source = caster,
                Center = center,
                Radius = ability.radius,
                PowerPerTick = healsAllies
                    ? ability.power
                    : ability.secondaryPower,
                StatusDuration = healsAllies ? 0f : 4f,
                NextTick = raidTime,
                ExpiresAt = raidTime + Mathf.Max(0.1f, ability.duration),
                HealsAllies = healsAllies,
                Color = ability.color
            });
        }

        private void StepPersistentFields()
        {
            for (var index = persistentFields.Count - 1; index >= 0; index--)
            {
                var field = persistentFields[index];
                if (field.Source == null || raidTime >= field.ExpiresAt)
                {
                    persistentFields.RemoveAt(index);
                    continue;
                }
                if (raidTime < field.NextTick) continue;
                field.NextTick += 1f;
                Effects?.EmitBurst(
                    field.Center,
                    field.Color,
                    field.Radius * 1.5f,
                    0.3f);
                if (field.HealsAllies)
                {
                    ApplyToAlliesByPhysics(
                        field.Source,
                        field.Center,
                        field.Radius,
                        true,
                        ally => ally.Heal(field.PowerPerTick, raidTime, this));
                    continue;
                }
                combatPhysics.CollectTargets(
                    field.Source,
                    RaidAttackShape.Circle,
                    field.Center,
                    Vector2.right,
                    0f,
                    field.Radius,
                    0f,
                    360f,
                    contactTargets,
                    64);
                for (var targetIndex = 0;
                     targetIndex < contactTargets.Count;
                     targetIndex++)
                {
                    contactTargets[targetIndex].Ignite(
                        field.Source,
                        field.PowerPerTick,
                        field.StatusDuration,
                        raidTime);
                }
            }
        }

        private RaidAgent2D FindMostInjuredAlly(
            RaidAgent2D caster,
            float range,
            bool includeDowned)
        {
            var candidates = caster.Faction == RaidFaction.Hunters
                ? hunters
                : monsters;
            RaidAgent2D best = null;
            var bestScore = 0f;
            for (var index = 0; index < candidates.Count; index++)
            {
                var candidate = candidates[index];
                if (candidate == null ||
                    candidate.LifeState == RaidLifeState.Dead) continue;
                if (!includeDowned && candidate.CanBeRescued) continue;
                if (Vector2.Distance(caster.Position, candidate.Position) > range)
                {
                    continue;
                }
                var score = candidate.CanBeRescued
                    ? 10f
                    : 1f - candidate.HealthRatio;
                if (score <= bestScore) continue;
                best = candidate;
                bestScore = score;
            }
            return best;
        }

        private int CountInjuredAllies(
            RaidAgent2D caster,
            float radius,
            float threshold)
        {
            var candidates = caster.Faction == RaidFaction.Hunters
                ? hunters
                : monsters;
            var count = 0;
            for (var index = 0; index < candidates.Count; index++)
            {
                var candidate = candidates[index];
                if (candidate == null ||
                    candidate.LifeState == RaidLifeState.Dead) continue;
                if (Vector2.Distance(caster.Position, candidate.Position) <= radius &&
                    (candidate.CanBeRescued ||
                     candidate.HealthRatio < threshold))
                {
                    count++;
                }
            }
            return count;
        }

        private int CountShieldCandidates(RaidAgent2D caster, float radius)
        {
            var candidates = caster.Faction == RaidFaction.Hunters
                ? hunters
                : monsters;
            var count = 0;
            for (var index = 0; index < candidates.Count; index++)
            {
                var candidate = candidates[index];
                if (candidate != null &&
                    candidate.CanAct &&
                    !candidate.HasTemporaryShield &&
                    Vector2.Distance(caster.Position, candidate.Position) <= radius)
                {
                    count++;
                }
            }
            return count;
        }

        private int CountEnemiesInRadius(
            RaidAgent2D caster,
            Vector2 position,
            float radius)
        {
            var candidates = caster.Faction == RaidFaction.Hunters
                ? monsters
                : hunters;
            var count = 0;
            for (var index = 0; index < candidates.Count; index++)
            {
                var candidate = candidates[index];
                if (candidate != null &&
                    candidate.CanReceiveDamage &&
                    Vector2.Distance(position, candidate.Position) <= radius)
                {
                    count++;
                }
            }
            return count;
        }

        private void StepAgents(float deltaTime)
        {
            for (var index = 0; index < hunters.Count; index++)
            {
                hunters[index]?.Step(deltaTime, raidTime, this);
            }
            for (var index = 0; index < monsters.Count; index++)
            {
                monsters[index]?.Step(deltaTime, raidTime, this);
            }
        }

        private void EvaluateOutcome()
        {
            if (party == null) return;
            if (party.Phase == RaidPartyPhase.Complete)
            {
                running = false;
                resultMessage =
                    "RAID COMPLETE - Objectives cleared and available loot secured.";
                PublishEvent(resultMessage);
                Effects?.EmitBurst(
                    PartyCentroid(),
                    new Color(0.2f, 1f, 0.65f),
                    5f,
                    0.85f);
            }
            else if (party.Phase == RaidPartyPhase.Failed)
            {
                running = false;
                resultMessage =
                    "RAID FAILED - No hunters remain able to fight.";
                PublishEvent(resultMessage);
            }
        }

        private void RebuildAgentCache()
        {
            hunters.Clear();
            monsters.Clear();
            if (party != null)
            {
                var members = party.Members;
                for (var index = 0; index < members.Count; index++)
                {
                    if (members[index] != null &&
                        !hunters.Contains(members[index]))
                    {
                        hunters.Add(members[index]);
                    }
                }
            }
            for (var podIndex = 0; podIndex < enemyPods.Length; podIndex++)
            {
                var pod = enemyPods[podIndex];
                if (pod == null) continue;
                var members = pod.Members;
                for (var memberIndex = 0;
                     memberIndex < members.Count;
                     memberIndex++)
                {
                    if (members[memberIndex] != null &&
                        !monsters.Contains(members[memberIndex]))
                    {
                        monsters.Add(members[memberIndex]);
                    }
                }
            }
            IgnoreFriendlyAgentCollisions(hunters);
            IgnoreFriendlyAgentCollisions(monsters);
        }

        private static void IgnoreFriendlyAgentCollisions(IReadOnlyList<RaidAgent2D> agents)
        {
            for (var leftIndex = 0; leftIndex < agents.Count; leftIndex++)
            {
                var left = agents[leftIndex];
                if (left == null) continue;
                for (var rightIndex = leftIndex + 1; rightIndex < agents.Count; rightIndex++)
                {
                    left.IgnoreFriendlyCollisionWith(agents[rightIndex]);
                }
            }
        }

        private static void ReplaceRoster(
            List<RaidAgent2D> destination,
            IReadOnlyList<RaidAgent2D> source)
        {
            destination.Clear();
            if (source == null) return;
            for (var index = 0; index < source.Count; index++)
            {
                var candidate = source[index];
                if (candidate != null) destination.Add(candidate);
            }
        }

        private void EnsureAgentCacheCurrent()
        {
            var expectedHunters = party?.Members.Count ?? 0;
            var expectedMonsters = 0;
            for (var podIndex = 0; podIndex < enemyPods.Length; podIndex++)
            {
                expectedMonsters += enemyPods[podIndex]?.Members.Count ?? 0;
            }
            if (hunters.Count != expectedHunters || monsters.Count != expectedMonsters)
            {
                RebuildAgentCache();
            }
        }

        private static void EncapsulateLiving(
            IReadOnlyList<RaidAgent2D> agents,
            ref Bounds bounds,
            ref bool initialized)
        {
            for (var index = 0; index < agents.Count; index++)
            {
                var agent = agents[index];
                if (agent == null || agent.LifeState == RaidLifeState.Dead) continue;
                if (!initialized)
                {
                    bounds = new Bounds(agent.transform.position, Vector3.zero);
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(agent.transform.position);
                }
            }
        }

#if UNITY_EDITOR
        public void ConfigureEditor(
            RaidPartyBrain2D assignedParty,
            RaidEnemyPodBrain2D[] pods,
            RaidRoom2D[] assignedRooms,
            RaidRoomConnection2D[] assignedConnections,
            RaidChest2D[] assignedChests,
            RaidFxPool2D fx,
            RaidCamera2D cameraController,
            RaidHud2D assignedHud)
        {
            party = assignedParty;
            enemyPods = pods ?? Array.Empty<RaidEnemyPodBrain2D>();
            rooms = assignedRooms ?? Array.Empty<RaidRoom2D>();
            connections = assignedConnections ?? Array.Empty<RaidRoomConnection2D>();
            chests = assignedChests ?? Array.Empty<RaidChest2D>();
            effects = fx;
            raidCamera = cameraController;
            hud = assignedHud;
        }
#endif
    }
}
