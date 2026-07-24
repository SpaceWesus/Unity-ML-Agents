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

    [CreateAssetMenu(menuName = "Turtle/Ecosystem/Gear Definition")]
    public sealed class EcosystemGearDefinition : ScriptableObject
    {
        [SerializeField] private string gearId;
        [SerializeField] private string displayName;
        [SerializeField] private GearMoveSet moveSet;
        [SerializeField, Min(1)] private int power = 10;
        [SerializeField] private Color accent = Color.white;

        public string GearId => gearId;
        public string DisplayName => displayName;
        public GearMoveSet MoveSet => moveSet;
        public int Power => power;
        public Color Accent => accent;

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
        }
#endif
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
    }

    [Serializable]
    public sealed class HunterProfile
    {
        public string id;
        public string displayName;
        public int level;
        public int experience;
        public int wounds;
        public float courage;
        public float ambition;
        public float loyalty;
        public float greed;
        public string goal;
        public string guildId;
        public string equippedGearId;
        public string currentActivity;
        public string destinationId;
        public List<HunterRelationship> relationships = new();
        public List<HunterMemory> memories = new();

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
    public sealed class EcosystemWorldState
    {
        public int saveVersion = 1;
        public int day = 1;
        public string playerHunterId;
        public List<HunterProfile> hunters = new();
        public List<GuildState> guilds = new();
        public List<MissionState> missions = new();
        public List<string> eventLog = new();
    }
}
