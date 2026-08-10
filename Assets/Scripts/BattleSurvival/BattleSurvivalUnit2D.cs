using UnityEngine;
using Turtle.DungeonRaid;

namespace Turtle.BattleSurvival
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RaidAgent2D))]
    public sealed class BattleSurvivalUnit2D : MonoBehaviour
    {
        [SerializeField] private RaidAgent2D agent;
        [SerializeField] private bool hunter;
        [SerializeField] private int squadIndex = -1;
        [SerializeField] private int memberIndex = -1;
        [SerializeField] private bool sergeant;
        [SerializeField] private string squadName = string.Empty;
        [SerializeField] private string buildLabel = string.Empty;
        [SerializeField] private string traitLabel = string.Empty;
        [SerializeField, Range(0f, 1f)] private float aggression = 0.6f;
        [SerializeField, Range(0f, 1f)] private float cohesion = 0.7f;
        [SerializeField, Range(0f, 1f)] private float support = 0.5f;

        private SpriteRenderer sergeantMarker;

        public RaidAgent2D Agent
        {
            get
            {
                if (agent == null) agent = GetComponent<RaidAgent2D>();
                return agent;
            }
        }
        public bool IsHunter => hunter;
        public int SquadIndex => squadIndex;
        public int MemberIndex => memberIndex;
        public bool IsSergeant => sergeant;
        public string SquadName => squadName;
        public string BuildLabel => buildLabel;
        public string TraitLabel => traitLabel;
        public float Aggression => aggression;
        public float Cohesion => cohesion;
        public float Support => support;
        public string CurrentObjective { get; set; } = "Forming up";
        public RaidAgent2D CurrentTarget { get; set; }
        public float NextDecisionAt { get; set; }
        public float DamageDealt { get; private set; }
        public int Kills { get; private set; }
        public int AbilityCasts { get; private set; }
        public bool DeathCredited { get; set; }

        private void Awake()
        {
            if (agent == null) agent = GetComponent<RaidAgent2D>();
            ApplySergeantMarker();
        }

        public void ConfigureHunter(
            BattleHunterProfile profile,
            int assignedSquadIndex,
            int assignedMemberIndex,
            bool isSergeant)
        {
            if (agent == null) agent = GetComponent<RaidAgent2D>();
            hunter = true;
            squadIndex = assignedSquadIndex;
            memberIndex = assignedMemberIndex;
            sergeant = isSergeant;
            squadName = profile?.SquadName ?? $"Squad {assignedSquadIndex + 1}";
            buildLabel = profile?.BuildLabel ?? "Hunter";
            traitLabel = profile?.TraitLabel ?? string.Empty;
            aggression = Mathf.Clamp01(profile?.Aggression ?? 0.6f);
            cohesion = Mathf.Clamp01(profile?.Cohesion ?? 0.7f);
            support = Mathf.Clamp01(profile?.Support ?? 0.5f);
            ApplySergeantMarker();
            ResetRuntimeState(assignedMemberIndex * 0.017f + assignedSquadIndex * 0.031f);
        }

        public void ConfigureMonster(string archetype, int serial, float decisionStagger)
        {
            if (agent == null) agent = GetComponent<RaidAgent2D>();
            hunter = false;
            squadIndex = -1;
            memberIndex = serial;
            sergeant = false;
            squadName = "Horde";
            buildLabel = archetype;
            traitLabel = "Escalating gate monster";
            aggression = 0.88f;
            cohesion = 0.2f;
            support = 0.1f;
            ApplySergeantMarker();
            ResetRuntimeState(decisionStagger);
        }

        public void PromoteToSergeant()
        {
            sergeant = true;
            ApplySergeantMarker();
        }

        public void DemoteSergeant()
        {
            sergeant = false;
            ApplySergeantMarker();
        }

        public void ResetRuntimeState(float decisionStagger = 0f)
        {
            CurrentObjective = hunter ? "Forming up" : "Overrun the defenders";
            CurrentTarget = null;
            NextDecisionAt = Mathf.Max(0f, decisionStagger);
            DamageDealt = 0f;
            Kills = 0;
            AbilityCasts = 0;
            DeathCredited = false;
        }

        public void RecordDamage(float amount)
        {
            DamageDealt += Mathf.Max(0f, amount);
        }

        public void RecordKill()
        {
            Kills++;
        }

        public void RecordAbilityCast()
        {
            AbilityCasts++;
        }

        private void ApplySergeantMarker()
        {
            if (sergeantMarker == null)
            {
                var marker = transform.Find("Squad Sergeant Marker");
                if (marker != null) sergeantMarker = marker.GetComponent<SpriteRenderer>();
            }
            if (sergeant && sergeantMarker == null)
            {
                var body = GetComponent<SpriteRenderer>();
                if (body != null && body.sprite != null)
                {
                    var markerObject = new GameObject("Squad Sergeant Marker");
                    markerObject.transform.SetParent(transform, false);
                    markerObject.transform.localPosition = new Vector3(0f, 1.12f, 0f);
                    markerObject.transform.localScale = new Vector3(0.32f, 0.18f, 1f);
                    sergeantMarker = markerObject.AddComponent<SpriteRenderer>();
                    sergeantMarker.sprite = body.sprite;
                    sergeantMarker.color = new Color(1f, 0.9f, 0.18f, 1f);
                    sergeantMarker.sortingOrder = body.sortingOrder + 10;
                }
            }
            if (sergeantMarker != null) sergeantMarker.gameObject.SetActive(sergeant);
        }
    }
}
