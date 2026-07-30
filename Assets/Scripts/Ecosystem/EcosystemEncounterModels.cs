using System;
using System.Collections.Generic;
using UnityEngine;

namespace Turtle.Ecosystem
{
    /// <summary>
    /// Persistent gate lifecycle. Keep serialized order append-only.
    /// </summary>
    public enum GateLifecycleState
    {
        Manifested,
        Available,
        RightsAwarded,
        InProgress,
        AwaitingReauction,
        Closed,
        Broken
    }

    /// <summary>
    /// Semantic room kinds used by both the top-down projection and a future 3D
    /// materializer. Keep serialized order append-only.
    /// </summary>
    public enum DungeonAreaType
    {
        Entrance,
        Combat,
        Treasure,
        Resource,
        Boss,
        Exit
    }

    public enum DungeonBiomeType
    {
        AshCavern,
        DrownedCrypt,
        VoidSpire,
        FrostWarrens,
        RuinedTemple,
        FungalNest
    }

    public enum DungeonLayoutStyle
    {
        Linear,
        Branching,
        HubAndSpoke,
        Winding
    }

    public enum DungeonHazardType
    {
        None,
        LavaVent,
        FloodedGround,
        VoidRift,
        FrostPatch,
        FallingDebris,
        PoisonPool
    }

    public enum DungeonMobPodStatus
    {
        Dormant,
        Engaged,
        Cleared
    }

    public enum DungeonLootStatus
    {
        Hidden,
        Available,
        Claimed
    }

    public enum DungeonEncounterStatus
    {
        Preparing,
        Active,
        Succeeded,
        Failed,
        Retreated,
        Paused
    }

    public enum EncounterParticipantKind
    {
        Hunter,
        Monster
    }

    public enum EncounterParticipantLifeState
    {
        Active,
        Incapacitated,
        Defeated,
        Extracted
    }

    public enum EncounterEventType
    {
        EncounterStarted,
        AreaEntered,
        PodEngaged,
        Attack,
        Damage,
        Incapacitated,
        Defeated,
        PodCleared,
        LootClaimed,
        ResourceExtracted,
        EncounterSucceeded,
        EncounterFailed,
        EncounterRetreated
    }

    /// <summary>
    /// Shared low-level command understood by controlled hunters and autonomous
    /// combatants. Input devices and AI are only different intent producers.
    /// </summary>
    [Serializable]
    public sealed class EncounterInputIntent
    {
        public string entityId;
        public Vector2 movement;
        public Vector2 aim = Vector2.down;
        public bool primaryAttack;
        public bool interact;
        public string targetEntityId;
        // Autonomous navigation is planned into the same intent and committed with
        // movement, so candidate generation remains read-only for every participant.
        public bool hasNavigationUpdate;
        public string navigationConnectionId;
        public string navigationDestinationAreaId;
        public int navigationWaypointIndex = -1;

        public static EncounterInputIntent Idle(string id) => new()
        {
            entityId = id
        };
    }

    public delegate bool EncounterIntentOverride(
        DungeonEncounterState encounter,
        EncounterParticipantState participant,
        out EncounterInputIntent intent);

    [Serializable]
    public sealed class DungeonAreaState
    {
        public string id;
        public string displayName;
        public DungeonAreaType areaType;
        public Vector2 center;
        public Vector2 size = new(10f, 10f);
        public bool discovered;
        public bool cleared;

        public bool Contains(Vector2 position, float inset = 0f)
        {
            var halfWidth = Mathf.Max(0f, size.x * 0.5f - inset);
            var halfHeight = Mathf.Max(0f, size.y * 0.5f - inset);
            return Mathf.Abs(position.x - center.x) <= halfWidth &&
                   Mathf.Abs(position.y - center.y) <= halfHeight;
        }
    }

    [Serializable]
    public sealed class DungeonConnectionState
    {
        public string id;
        public string fromAreaId;
        public string toAreaId;
        public List<Vector2> waypoints = new();
        public bool locked;

        public string OtherArea(string areaId)
        {
            if (areaId == fromAreaId) return toAreaId;
            if (areaId == toAreaId) return fromAreaId;
            return string.Empty;
        }
    }

    [Serializable]
    public sealed class DungeonMobPodState
    {
        public string id;
        public string areaId;
        public DungeonMobPodStatus status;
        public List<string> monsterIds = new();
    }

    [Serializable]
    public sealed class DungeonMonsterState
    {
        public string id;
        public string definitionId;
        public string displayName;
        public string podId;
        public string areaId;
        public Vector2 position;
        public Vector2 facing = Vector2.down;
        public int maximumHealth = 50;
        public int maximumMana;
        public int maximumShield;
        public float combatPower = 10f;
        public float moveSpeed = 3.2f;
        public float attackRange = 1.35f;
        public float attackDamage = 8f;
        public int attackCooldownTicks = 6;
        public bool boss;
    }

    [Serializable]
    public sealed class DungeonLootNodeState
    {
        public string id;
        public string areaId;
        public Vector2 position;
        public string lootTableId;
        public int gold;
        public int guildResources;
        public List<string> gearDefinitionIds = new();
        public DungeonLootStatus status = DungeonLootStatus.Hidden;
        public string claimedByEntityId;
    }

    [Serializable]
    public sealed class DungeonResourceNodeState
    {
        public string id;
        public string areaId;
        public Vector2 position;
        public string resourceId;
        public int initialAmount;
        public int remainingAmount;
        public string extractedByPartyId;
    }

    [Serializable]
    public sealed class DungeonHazardState
    {
        public string id;
        public string areaId;
        public DungeonHazardType hazardType;
        public Vector2 position;
        public float radius = 1.5f;
        public float damagePerTick;
        public bool active = true;
    }

    /// <summary>
    /// Generated once, before entry, and then persisted. It is the immutable
    /// starting manifest for a gate. Mutable run truth lives in the linked
    /// DungeonEncounterState, preventing observation from regenerating content.
    /// </summary>
    [Serializable]
    public sealed class GateInstanceState
    {
        public string id;
        public string displayName;
        public string entranceLocationId;
        public string currentContractId;
        public string activeEncounterId;
        public int seed;
        public int generatorVersion = 1;
        public int createdDay = 1;
        public int instabilityDeadlineDay = 3;
        public int trueDifficulty = 1;
        public int appraisedDifficulty = 1;
        public DungeonBiomeType biome;
        public DungeonLayoutStyle layoutStyle;
        public string visualStyleId;
        public GateLifecycleState lifecycle = GateLifecycleState.Manifested;
        public int runSequence;
        public string bossMonsterId;
        public List<string> visibleModifierIds = new();
        public List<string> hiddenModifierIds = new();
        public List<DungeonAreaState> areas = new();
        public List<DungeonConnectionState> connections = new();
        public List<DungeonMobPodState> mobPods = new();
        public List<DungeonMonsterState> monsters = new();
        public List<DungeonLootNodeState> lootNodes = new();
        public List<DungeonResourceNodeState> resourceNodes = new();
        public List<DungeonHazardState> hazards = new();
    }

    [Serializable]
    public sealed class EncounterCooldownState
    {
        public string actionId;
        public long readyAtTick;
    }

    /// <summary>
    /// A fidelity-neutral combatant snapshot. During an active run this record,
    /// rather than a rendered object, owns exact position, vitals, targeting and
    /// cooldown truth. sourceHunterId links hunters back to their campaign record.
    /// </summary>
    [Serializable]
    public sealed class EncounterParticipantState
    {
        public string entityId;
        public EncounterParticipantKind participantKind;
        public string sourceHunterId;
        public string definitionId;
        public string displayName;
        public string factionId;
        public string podId;
        public string areaId;
        public Vector2 position;
        public Vector2 facing = Vector2.down;
        public HunterVitalsState vitals = new();
        public EncounterParticipantLifeState lifeState = EncounterParticipantLifeState.Active;
        public string targetEntityId;
        public long actionReadyTick;
        public string navigationConnectionId;
        public string navigationDestinationAreaId;
        public int navigationWaypointIndex = -1;
        public float combatPower = 10f;
        public float moveSpeed = 3.5f;
        public float attackRange = 1.4f;
        public float attackDamage = 8f;
        public int attackCooldownTicks = 6;
        public List<EncounterCooldownState> cooldowns = new();

        public bool CanFight =>
            lifeState == EncounterParticipantLifeState.Active &&
            vitals != null && vitals.currentHealth > 0;
    }

    [Serializable]
    public sealed class EncounterEventState
    {
        public string id;
        public long sequence;
        public long tick;
        public EncounterEventType eventType;
        public string actorEntityId;
        public string targetEntityId;
        public string areaId;
        public Vector2 position;
        public float magnitude;
        public string summary;
    }

    /// <summary>
    /// Mutable, saveable truth for one gate run. All coordinates are local to the
    /// generated dungeon and map naturally to either Unity 2D x/y or 3D x/z.
    /// </summary>
    [Serializable]
    public sealed class DungeonEncounterState
    {
        public string id;
        public string gateId;
        public string contractId;
        public string partyId;
        public DungeonEncounterStatus status = DungeonEncounterStatus.Preparing;
        public int createdDay = 1;
        public long fixedTick;
        public long randomSequence;
        public long eventSequence;
        public string entranceAreaId;
        public string bossAreaId;
        public List<DungeonAreaState> areas = new();
        public List<DungeonConnectionState> connections = new();
        public List<DungeonMobPodState> mobPods = new();
        public List<EncounterParticipantState> participants = new();
        public List<DungeonLootNodeState> lootNodes = new();
        public List<DungeonResourceNodeState> resourceNodes = new();
        public List<DungeonHazardState> hazards = new();
        public List<EncounterEventState> recentEvents = new();
    }
}
