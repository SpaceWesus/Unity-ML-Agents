using UnityEngine;
using Turtle.DungeonRaid;

namespace Turtle.BattleScale
{
    /// <summary>
    /// Scale-test metadata and command state for one real RaidAgent2D. This
    /// component owns no combat stats; the shared raid combatant remains the
    /// authoritative health, movement, cooldown, hurtbox, and life-state owner.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RaidAgent2D))]
    public sealed class BattleScaleUnit2D : MonoBehaviour
    {
        [SerializeField] private RaidAgent2D agent;

        private SpriteRenderer commandMarker;

        public RaidAgent2D Agent
        {
            get
            {
                if (agent == null) agent = GetComponent<RaidAgent2D>();
                return agent;
            }
        }
        public int TeamIndex { get; private set; }
        public int SquadIndex { get; private set; }
        public int MemberIndex { get; private set; }
        public bool IsSergeant { get; private set; }
        public float Discipline { get; private set; }
        public Vector2 FormationOffset { get; private set; }
        public RaidAgent2D CurrentTarget { get; set; }
        public float NextDecisionAt { get; set; }
        public float NextSupportAt { get; set; }

        private void Awake()
        {
            if (agent == null) agent = GetComponent<RaidAgent2D>();
        }

        public void Configure(
            int teamIndex,
            int squadIndex,
            int memberIndex,
            Vector2 formationOffset,
            float decisionStagger)
        {
            if (agent == null) agent = GetComponent<RaidAgent2D>();
            TeamIndex = teamIndex;
            SquadIndex = squadIndex;
            MemberIndex = memberIndex;
            FormationOffset = formationOffset;
            Discipline = 0.48f + StableFraction(teamIndex, squadIndex, memberIndex) * 0.48f;
            CurrentTarget = null;
            NextDecisionAt = Mathf.Max(0f, decisionStagger);
            NextSupportAt = 1.5f + decisionStagger * 4f;
            SetSergeant(memberIndex == 0);
        }

        public void SetSergeant(bool isSergeant)
        {
            IsSergeant = isSergeant;
            EnsureCommandMarker();
            if (commandMarker != null) commandMarker.gameObject.SetActive(isSergeant);
        }

        private void EnsureCommandMarker()
        {
            if (!IsSergeant || commandMarker != null) return;
            var body = GetComponent<SpriteRenderer>();
            if (body == null || body.sprite == null) return;
            var marker = new GameObject("Squad Sergeant Marker");
            marker.transform.SetParent(transform, false);
            marker.transform.localPosition = new Vector3(0f, 1.08f, 0f);
            marker.transform.localScale = new Vector3(0.28f, 0.18f, 1f);
            commandMarker = marker.AddComponent<SpriteRenderer>();
            commandMarker.sprite = body.sprite;
            commandMarker.color = new Color(1f, 0.88f, 0.22f, 0.96f);
            commandMarker.sortingLayerID = body.sortingLayerID;
            commandMarker.sortingOrder = body.sortingOrder + 8;
        }

        private static float StableFraction(int team, int squad, int member)
        {
            var hash = unchecked(17 + team * 73856093 + squad * 19349663 + member * 83492791);
            hash ^= hash >> 13;
            return (hash & 1023) / 1023f;
        }
    }
}
