using System;
using System.Collections.Generic;
using UnityEngine;

namespace Turtle.Ecosystem
{
    public enum GearMoveSet
    {
        VanguardBlade,
        TitanGreatsword,
        RiftDaggers
    }

    public enum TacticalRole
    {
        Flexible,
        Vanguard,
        Bruiser,
        Skirmisher,
        Controller,
        Support
    }

    /// <summary>
    /// Association assessment derived exclusively from Ability Points invested in a hunter.
    /// Keep serialized order append-only.
    /// </summary>
    public enum HunterRank
    {
        E,
        D,
        C,
        B,
        A,
        S
    }

    /// <summary>
    /// Descriptive build families rather than mutually exclusive character classes.
    /// Keep serialized order append-only.
    /// </summary>
    public enum HunterArchetype
    {
        Fighter,
        Healer,
        Assassin,
        Ranger,
        Tank,
        Mage
    }

    /// <summary>
    /// Career ownership category used to validate learned skills and active loadouts.
    /// Keep serialized order append-only.
    /// </summary>
    public enum HunterAbilityKind
    {
        Cooldown,
        Ultimate,
        Passive
    }

    public enum LocationType
    {
        Town,
        ResourceSite,
        Dungeon,
        Marketplace,
        Hospital
    }

    public enum ContractType
    {
        DungeonClear,
        ResourceDefense,
        Escort,
        Raid,
        Bounty
    }

    public enum ContractStatus
    {
        Offered,
        Accepted,
        Active,
        Succeeded,
        Failed,
        Expired,
        Cancelled
    }

    public enum PartyStatus
    {
        Forming,
        Travelling,
        Active,
        Returning,
        Disbanded
    }

    public enum InvitationType
    {
        GuildMembership,
        PartyMembership,
        ContractParticipation,
        Alliance,
        Truce
    }

    public enum InvitationStatus
    {
        Pending,
        Accepted,
        Declined,
        Expired,
        Withdrawn
    }

    public enum HunterActionType
    {
        None,
        Wait,
        ChooseGear,
        EquipGear,
        Travel,
        Rest,
        Recover,
        JoinGuild,
        LeaveGuild,
        CreateGuild,
        RecruitHunter,
        RecruitToGuild,
        FormParty,
        JoinParty,
        InviteToParty,
        AcceptInvitation,
        RejectInvitation,
        AcceptContract,
        DeclineContract,
        StartContract,
        ResolveContract,
        EnterDungeon,
        Retreat,
        TradeGear,
        ClaimReward,
        Help,
        Betray,
        Challenge,
        Reconcile,
        ClaimLocation,
        ClaimSite,
        DefendLocation,
        DeclareWar,
        NegotiatePeace,
        LeaveParty,
        DisbandParty,
        Train,
        InvestAttribute,
        LearnAbility,
        EquipAbility,
        EquipPassive,
        SaveAbilityPoints,
        Retire
    }

    public enum DiplomaticStance
    {
        Neutral,
        Allied,
        Cooperative,
        Rival,
        Hostile,
        AtWar,
        Truce
    }

    public enum InjurySeverity
    {
        Minor,
        Moderate,
        Severe,
        Critical
    }

    public enum WorldEventType
    {
        SimulationAdvanced,
        HunterAction,
        GuildMembershipChanged,
        PartyChanged,
        ContractChanged,
        RelationshipChanged,
        LocationControlChanged,
        DiplomacyChanged,
        HunterInjured,
        HunterRecovered,
        HunterDied,
        RewardGranted,
        HunterProgressed,
        HunterRankChanged,
        HunterBuildChanged,
        HunterAwakened,
        HunterRetired,
        GateGenerated,
        EncounterChanged
    }

    [CreateAssetMenu(menuName = "Turtle/Ecosystem/Gear Definition")]
    public sealed class EcosystemGearDefinition : ScriptableObject
    {
        [SerializeField] private string gearId;
        [SerializeField] private string displayName;
        [SerializeField] private GearMoveSet moveSet;
        [SerializeField, Min(1)] private int power = 10;
        [SerializeField] private Color accent = Color.white;
        [SerializeField] private TacticalRole tacticalRole = TacticalRole.Flexible;
        [SerializeField] private string[] grantedMoves = Array.Empty<string>();
        [SerializeField, Min(0)] private int price;

        public string GearId => gearId;
        public string DisplayName => displayName;
        public GearMoveSet MoveSet => moveSet;
        public int Power => power;
        public Color Accent => accent;
        public TacticalRole TacticalRole => tacticalRole;
        public IReadOnlyList<string> GrantedMoves => grantedMoves ?? Array.Empty<string>();
        public int Price => price;

#if UNITY_EDITOR
        public void Configure(
            string id,
            string gearName,
            GearMoveSet grantedMoveSet,
            int gearPower,
            Color gearAccent)
        {
            gearId = id;
            displayName = gearName;
            moveSet = grantedMoveSet;
            power = gearPower;
            accent = gearAccent;
            tacticalRole = grantedMoveSet switch
            {
                GearMoveSet.TitanGreatsword => TacticalRole.Bruiser,
                GearMoveSet.RiftDaggers => TacticalRole.Skirmisher,
                _ => TacticalRole.Vanguard
            };
            grantedMoves = grantedMoveSet switch
            {
                GearMoveSet.TitanGreatsword => new[] { "greatsword_cleave", "greatsword_guard_break" },
                GearMoveSet.RiftDaggers => new[] { "dagger_flurry", "rift_step" },
                _ => new[] { "blade_slash", "vanguard_guard" }
            };
            price = Mathf.Max(0, gearPower * 8);
        }

        public void Configure(
            string id,
            string gearName,
            GearMoveSet grantedMoveSet,
            int gearPower,
            Color gearAccent,
            TacticalRole role,
            string[] moves,
            int purchasePrice)
        {
            gearId = id;
            displayName = gearName;
            moveSet = grantedMoveSet;
            power = gearPower;
            accent = gearAccent;
            tacticalRole = role;
            grantedMoves = moves ?? Array.Empty<string>();
            price = Mathf.Max(0, purchasePrice);
        }
#endif
    }

    [Serializable]
    public sealed class HunterInjury
    {
        public string id;
        public string displayName;
        public InjurySeverity severity;
        public int sufferedDay;
        public int recoveryDay;
        public string sourceEventId;
        public bool healed;
    }

    [Serializable]
    public sealed class HunterMemory
    {
        public int day;
        public string subjectId;
        public string eventType;
        public string summary;
        public float emotionalWeight;
    }

    [Serializable]
    public sealed class HunterRelationship
    {
        public string hunterId;
        public float affinity;
        public float trust;
        public float rivalry;
        public float debt;
        public float grudge;
    }

    /// <summary>
    /// Coarse campaign resources persisted between ecosystem and combat encounters.
    /// Real-time combat remains authoritative while an encounter is running; a later
    /// encounter adapter can copy these values in and commit the outcome back out.
    /// </summary>
    [Serializable]
    public sealed class HunterVitalsState
    {
        public bool initialized;
        public int maximumHealth = 100;
        public int currentHealth = 100;
        public int maximumMana = 100;
        public int currentMana = 100;
        public int maximumShield = 100;
        public int currentShield;

        public float HealthRatio => maximumHealth > 0
            ? Mathf.Clamp01((float)currentHealth / maximumHealth)
            : 0f;

        public float ManaRatio => maximumMana > 0
            ? Mathf.Clamp01((float)currentMana / maximumMana)
            : 0f;

        public float ShieldRatio => maximumShield > 0
            ? Mathf.Clamp01((float)currentShield / maximumShield)
            : 0f;

        public void Initialize(bool isAlive, int wounds, int startingShield)
        {
            initialized = true;
            maximumHealth = 100;
            maximumMana = 100;
            maximumShield = 100;
            currentHealth = isAlive ? Mathf.Max(1, maximumHealth - Mathf.Max(0, wounds) * 15) : 0;
            currentMana = isAlive ? maximumMana : 0;
            currentShield = isAlive ? Mathf.Clamp(startingShield, 0, maximumShield) : 0;
        }

        public void Normalize(bool isAlive, bool isIncapacitated = false)
        {
            maximumHealth = Mathf.Max(1, maximumHealth);
            maximumMana = Mathf.Max(1, maximumMana);
            maximumShield = Mathf.Max(1, maximumShield);
            currentHealth = Mathf.Clamp(currentHealth, 0, maximumHealth);
            currentMana = Mathf.Clamp(currentMana, 0, maximumMana);
            currentShield = Mathf.Clamp(currentShield, 0, maximumShield);
            if (!isAlive)
            {
                currentHealth = 0;
                currentMana = 0;
                currentShield = 0;
            }
            else if (isIncapacitated)
            {
                currentHealth = 0;
            }
            else
            {
                // isAlive remains the single campaign death authority. Loading malformed
                // resource data must not silently kill an otherwise living persistent hunter.
                currentHealth = Mathf.Max(1, currentHealth);
            }
            initialized = true;
        }

        public void ApplyDamagePreservingLife(int amount)
        {
            var remaining = Mathf.Max(0, amount);
            var absorbed = Mathf.Min(currentShield, remaining);
            currentShield -= absorbed;
            remaining -= absorbed;
            if (remaining > 0)
            {
                currentHealth = Mathf.Max(1, currentHealth - remaining);
            }
        }

        public void Restore(int health, int mana, int shield)
        {
            currentHealth = Mathf.Min(maximumHealth, currentHealth + Mathf.Max(0, health));
            currentMana = Mathf.Min(maximumMana, currentMana + Mathf.Max(0, mana));
            currentShield = Mathf.Min(maximumShield, currentShield + Mathf.Max(0, shield));
        }

        public void MarkDead()
        {
            currentHealth = 0;
            currentMana = 0;
            currentShield = 0;
        }
    }

    [Serializable]
    public sealed class HunterProfile
    {
        public string id;
        public string displayName;
        public int level;
        public int experience;
        public HunterCareerState career = new();
        public int wounds;
        public HunterVitalsState vitals = new();
        public float courage;
        public float ambition;
        public float loyalty;
        public float greed;
        public string goal;
        public string guildId;
        public string equippedGearId;
        public string currentActivity;
        public string destinationId;
        public int travelDaysRemaining;
        public List<HunterRelationship> relationships = new();
        public List<HunterMemory> memories = new();
        public bool isAlive = true;
        public int deathDay = -1;
        public string deathCause;
        public int awakeningDay = 1;
        public bool isRetired;
        public int retirementDay = -1;
        public string retirementReason;
        public int gold;
        public string locationId;
        public List<string> inventoryGearIds = new();
        public string partyId;
        public string activeContractId;
        // An active encounter owns the hunter's exact combat snapshot until the
        // encounter is committed back to campaign state. Empty outside a run.
        public string currentEncounterId;
        public bool isIncapacitated;
        public int pendingRewardGold;
        public float fame;
        public List<HunterInjury> injuries = new();

        public bool IsActive => isAlive && !isRetired;

        public HunterRelationship RelationshipWith(string otherHunterId)
        {
            var relationship = relationships.Find(item => item.hunterId == otherHunterId);
            if (relationship != null)
            {
                return relationship;
            }

            relationship = new HunterRelationship
            {
                hunterId = otherHunterId,
                affinity = 0f,
                trust = 0f,
                rivalry = 0f
            };
            relationships.Add(relationship);
            return relationship;
        }
    }

    [Serializable]
    public sealed class GuildState
    {
        public string id;
        public string displayName;
        public int resources;
        public int territory;
        public float prestige;
        public List<string> memberIds = new();
        public string homeLocationId;
        public List<string> controlledLocationIds = new();
        public List<GuildDiplomacyState> diplomacy = new();
    }

    [Serializable]
    public sealed class GuildDiplomacyState
    {
        public string guildId;
        public DiplomaticStance stance;
        public float regard;
        public float grievance;
        public int sinceDay;
        public string causeEventId;
    }

    [Serializable]
    public sealed class MissionState
    {
        public string id;
        public string displayName;
        public int difficulty;
        public int reward;
        public string favoredTrait;
    }

    [Serializable]
    public sealed class LocationState
    {
        public string id;
        public string displayName;
        public LocationType locationType;
        public string regionId;
        public string controllingGuildId;
        public Vector2 mapPosition;
        public int danger;
        public int resourceYield;
        public List<string> missionTemplateIds = new();
    }

    [Serializable]
    public sealed class WorldRouteState
    {
        public string fromLocationId;
        public string toLocationId;
        public int travelDays = 1;
        public int danger;
    }

    [Serializable]
    public sealed class EcosystemMapState
    {
        public List<LocationState> locations = new();
        public List<WorldRouteState> routes = new();
    }

    [Serializable]
    public sealed class ContractState
    {
        public string id;
        public string displayName;
        public ContractType contractType;
        public ContractStatus status;
        public string missionTemplateId;
        public string issuerGuildId;
        public string locationId;
        public string targetLocationId;
        public int offeredDay;
        public int expiresDay;
        public int startedDay = -1;
        public int resolvedDay = -1;
        public int difficulty;
        public int rewardGold;
        public int rewardResources;
        public float rewardFame;
        public string acceptedPartyId;
        public string acceptedHunterId;
        // Added in v5. A gate is generated and persisted before the contract is
        // entered; activeEncounterId links the current materializable run.
        public string gateId;
        public string activeEncounterId;
    }

    [Serializable]
    public sealed class PartyState
    {
        public string id;
        public string displayName;
        public string leaderHunterId;
        public List<string> memberIds = new();
        public PartyStatus status;
        public string locationId;
        public string destinationId;
        public int travelDaysRemaining;
        public string activeContractId;
        public int createdDay;
    }

    [Serializable]
    public sealed class InvitationState
    {
        public string id;
        public InvitationType invitationType;
        public InvitationStatus status;
        public string senderHunterId;
        public string recipientHunterId;
        public string guildId;
        public string partyId;
        public string contractId;
        public int createdDay;
        public int expiresDay;
        public float utilityScore;
    }

    [Serializable]
    public sealed class DecisionFactor
    {
        public string key;
        public float rawValue;
        public float weight;
        public float contribution;
        public string explanation;
    }

    [Serializable]
    public sealed class HunterDecisionRecord
    {
        public string decisionId;
        public long sequence;
        public int day;
        public string hunterId;
        public string category;
        public HunterActionType actionType;
        public string targetId;
        public bool executable = true;
        public bool selected;
        public float totalScore;
        public string rejectionReason;
        public string finalExplanation;
        public string tieBreakExplanation;
        public List<DecisionFactor> factors = new();
    }

    [Serializable]
    public sealed class StructuredWorldEvent
    {
        public string id;
        public long sequence;
        public int day;
        public WorldEventType eventType;
        public string actorHunterId;
        public string targetHunterId;
        public string guildId;
        public string locationId;
        public string contractId;
        public float magnitude;
        public string summary;
    }

    [Serializable]
    public sealed class EcosystemWorldState
    {
        public int saveVersion = 5;
        public int day = 1;
        public string playerHunterId;
        public List<HunterProfile> hunters = new();
        public List<GuildState> guilds = new();
        public List<MissionState> missions = new();
        public List<string> eventLog = new();
        public int worldSeed;
        public long simulationSequence;
        public int populationSequence;
        public EcosystemMapState map = new();
        public List<ContractState> contracts = new();
        public List<PartyState> parties = new();
        public List<InvitationState> invitations = new();
        public List<GateInstanceState> gates = new();
        public List<DungeonEncounterState> encounters = new();
        public List<HunterDecisionRecord> decisionRecords = new();
        public List<StructuredWorldEvent> structuredEvents = new();
    }
}
