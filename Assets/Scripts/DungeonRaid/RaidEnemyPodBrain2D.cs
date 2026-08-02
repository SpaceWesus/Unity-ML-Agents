using System;
using System.Collections.Generic;
using UnityEngine;

namespace Turtle.DungeonRaid
{
    [DisallowMultipleComponent]
    public sealed class RaidEnemyPodBrain2D : MonoBehaviour
    {
        [SerializeField] private string podId = "pod";
        [SerializeField] private string displayName = "Enemy Pod";
        [SerializeField, Min(0)] private int order;
        [SerializeField] private RaidRoom2D room;
        [SerializeField] private List<RaidAgent2D> members = new();
        [SerializeField, Min(1f)] private float aggroRadius = 10f;
        [SerializeField, Min(0f)] private float alertDelay = 0.32f;
        [SerializeField, Min(0.5f)] private float tacticalPlanSeconds = 3.5f;

        private RaidPodPhase phase = RaidPodPhase.Dormant;
        private float phaseEnteredAt;
        private float nextTacticalPlanAt;
        private RaidAgent2D priorityTarget;

        public string PodId => podId;
        public string DisplayName => displayName;
        public int Order => order;
        public RaidRoom2D Room => room;
        public IReadOnlyList<RaidAgent2D> Members => members;
        public RaidPodPhase Phase => phase;
        public bool IsDefeated => phase == RaidPodPhase.Defeated;
        public float AggroRadius => aggroRadius;
        public Vector2 ActivationCenter => room != null ? room.Center : MemberCentroid();

        public void BindGeneratedRoom(RaidRoom2D assignedRoom, float activationRadius = 9.5f)
        {
            room = assignedRoom;
            aggroRadius = Mathf.Max(1f, activationRadius);
            ResetPod();
        }

        public void ResetPod()
        {
            phase = RaidPodPhase.Dormant;
            phaseEnteredAt = 0f;
            nextTacticalPlanAt = 0f;
            priorityTarget = null;
            for (var index = 0; index < members.Count; index++) members[index]?.StopMoving();
        }

        public void Tick(DungeonRaidDirector2D raid, float raidTime)
        {
            if (phase == RaidPodPhase.Defeated) return;
            if (!HasActiveMember())
            {
                Transition(RaidPodPhase.Defeated, raid, raidTime,
                    $"{displayName} was defeated. The room's chest is now unlocked.");
                raid?.Effects?.EmitBurst(ActivationCenter, new Color(0.3f, 1f, 0.25f), 4f, 0.7f);
                return;
            }

            switch (phase)
            {
                case RaidPodPhase.Dormant:
                    if (ShouldAlert(raid))
                    {
                        Transition(RaidPodPhase.Alerted, raid, raidTime,
                            $"{displayName} spotted the incoming hunters.");
                        raid?.Effects?.EmitText(ActivationCenter + Vector2.up * 1.5f,
                            "POD ALERTED", new Color(1f, 0.28f, 0.1f));
                    }
                    break;
                case RaidPodPhase.Alerted:
                    if (raidTime - phaseEnteredAt >= alertDelay)
                    {
                        Transition(RaidPodPhase.Engaging, raid, raidTime,
                            $"{displayName} engaged the strike team.");
                    }
                    break;
                case RaidPodPhase.Engaging:
                    CommandCombat(raid, raidTime);
                    break;
            }
        }

        public void ForceAlert(DungeonRaidDirector2D raid, float raidTime)
        {
            if (phase != RaidPodPhase.Dormant) return;
            Transition(RaidPodPhase.Alerted, raid, raidTime,
                $"{displayName} was alerted by the advancing party.");
        }

        private void CommandCombat(DungeonRaidDirector2D raid, float raidTime)
        {
            if (priorityTarget == null || !priorityTarget.CanReceiveDamage ||
                raidTime >= nextTacticalPlanAt)
            {
                priorityTarget = SelectPriorityTarget(raid);
                nextTacticalPlanAt = raidTime + tacticalPlanSeconds;
                if (priorityTarget != null)
                {
                    raid.PublishEvent($"{displayName} is focusing {priorityTarget.DisplayName}.");
                }
            }
            for (var index = 0; index < members.Count; index++)
            {
                var member = members[index];
                if (member == null || !member.CanAct) continue;
                var target = member.ResolveForcedTarget(raidTime) ??
                             priorityTarget ??
                             raid.FindNearestActiveEnemy(member, raid.Hunters);
                if (raid.TryUseBestAbility(member, target))
                {
                    member.StopMoving();
                    continue;
                }
                if (target == null)
                {
                    member.MoveToward(ActivationCenter, 2f);
                    continue;
                }
                var offset = target.Position - member.Position;
                var distance = offset.magnitude;
                if (member.CanBasicAttack(target, raidTime))
                {
                    member.TryBasicAttack(target, raidTime, raid);
                }
                else if (member.RangedBasicAttack && distance < member.PreferredCombatRange - 0.65f)
                {
                    var retreat = offset.sqrMagnitude > 0.001f
                        ? member.Position - offset.normalized * 2.2f
                        : member.Position + Vector2.right * 2.2f;
                    member.MoveToward(retreat, 0.1f);
                }
                else
                {
                    member.MoveToward(target.Position, Mathf.Max(0.15f, member.BasicAttackRange * 0.84f));
                }
            }
        }

        private RaidAgent2D SelectPriorityTarget(DungeonRaidDirector2D raid)
        {
            RaidAgent2D best = null;
            var bestScore = float.MinValue;
            var hunters = raid.Hunters;
            for (var index = 0; index < hunters.Count; index++)
            {
                var candidate = hunters[index];
                if (candidate == null || !candidate.CanReceiveDamage) continue;
                var rolePressure = candidate.Role switch
                {
                    RaidCombatRole.Healer => 34f,
                    RaidCombatRole.Mage => 20f,
                    RaidCombatRole.Ranger => 16f,
                    RaidCombatRole.Tank => -16f,
                    _ => 0f
                };
                var woundedPressure = (1f - candidate.HealthRatio) * 52f;
                var distancePressure = -Vector2.Distance(ActivationCenter, candidate.Position);
                var score = rolePressure + woundedPressure + distancePressure;
                if (score <= bestScore) continue;
                best = candidate;
                bestScore = score;
            }
            return best;
        }

        private bool ShouldAlert(DungeonRaidDirector2D raid)
        {
            if (raid == null) return false;
            var hunters = raid.Hunters;
            for (var index = 0; index < hunters.Count; index++)
            {
                var hunter = hunters[index];
                if (hunter == null || !hunter.CanAct) continue;
                if (Vector2.Distance(hunter.Position, ActivationCenter) <= aggroRadius ||
                    (room != null && room.Contains(hunter.Position))) return true;
            }
            return false;
        }

        private bool HasActiveMember()
        {
            for (var index = 0; index < members.Count; index++)
            {
                if (members[index] != null && members[index].CanAct) return true;
            }
            return false;
        }

        private Vector2 MemberCentroid()
        {
            var sum = Vector2.zero;
            var count = 0;
            for (var index = 0; index < members.Count; index++)
            {
                if (members[index] == null) continue;
                sum += members[index].Position;
                count++;
            }
            return count > 0 ? sum / count : transform.position;
        }

        private void Transition(
            RaidPodPhase next,
            DungeonRaidDirector2D raid,
            float raidTime,
            string message)
        {
            if (phase == next) return;
            phase = next;
            phaseEnteredAt = raidTime;
            raid?.PublishEvent(message);
        }

#if UNITY_EDITOR
        public void ConfigureEditor(
            string id,
            string label,
            int podOrder,
            RaidRoom2D assignedRoom,
            List<RaidAgent2D> assignedMembers,
            float activationRadius)
        {
            podId = id;
            displayName = label;
            order = Mathf.Max(0, podOrder);
            room = assignedRoom;
            members = assignedMembers ?? new List<RaidAgent2D>();
            aggroRadius = Mathf.Max(1f, activationRadius);
        }
#endif
    }
}
