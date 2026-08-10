using System;
using System.Collections.Generic;
using UnityEngine;

namespace Turtle.DungeonRaid
{
    [DisallowMultipleComponent]
    public sealed class RaidPartyBrain2D : MonoBehaviour
    {
        [SerializeField] private List<RaidAgent2D> members = new();
        [SerializeField, Min(0f)] private float rallySeconds = 0.8f;
        [SerializeField, Min(0f)] private float recoverySeconds = 1.25f;
        [SerializeField, Min(0.1f)] private float chestInteractionRange = 1.45f;

        private RaidPartyPhase phase = RaidPartyPhase.Waiting;
        private RaidEnemyPodBrain2D currentPod;
        private RaidChest2D currentChest;
        private float phaseEnteredAt;

        public IReadOnlyList<RaidAgent2D> Members => members;
        public RaidPartyPhase Phase => phase;
        public RaidEnemyPodBrain2D CurrentPod => currentPod;

        public void ResetParty()
        {
            phase = RaidPartyPhase.Waiting;
            currentPod = null;
            currentChest = null;
            phaseEnteredAt = 0f;
            StopAllMembers();
        }

        public void BeginRaid(DungeonRaidDirector2D raid, float raidTime)
        {
            currentPod = raid?.FindNextPod();
            Transition(RaidPartyPhase.Rallying, raid, raidTime,
                "The strike team formed up at the entrance.");
        }

        public void Tick(DungeonRaidDirector2D raid, float raidTime)
        {
            if (raid == null || phase is RaidPartyPhase.Complete or RaidPartyPhase.Failed) return;
            if (!HasMemberAbleToContinue())
            {
                Transition(RaidPartyPhase.Failed, raid, raidTime,
                    "The strike team can no longer continue the raid.");
                return;
            }

            var engagedPod = FindEngagedPod(raid);
            if (engagedPod != null)
            {
                currentPod = engagedPod;
                if (phase != RaidPartyPhase.Engaging)
                {
                    Transition(RaidPartyPhase.Engaging, raid, raidTime,
                        $"The party engaged {engagedPod.DisplayName}.");
                }
                CommandCombat(raid, engagedPod, raidTime);
                return;
            }

            if (currentPod != null && currentPod.IsDefeated)
            {
                if (phase is not RaidPartyPhase.Recovering and not RaidPartyPhase.Looting)
                {
                    Transition(RaidPartyPhase.Recovering, raid, raidTime,
                        "The room is secure. The party is stabilizing and regrouping.");
                }
                if (phase == RaidPartyPhase.Recovering)
                {
                    CommandRecovery(raid, raidTime);
                    if (raidTime - phaseEnteredAt >= recoverySeconds)
                    {
                        currentChest = raid.FindChestForPod(currentPod);
                        if (currentChest != null)
                        {
                            Transition(RaidPartyPhase.Looting, raid, raidTime,
                                "The party moved to claim the unlocked chest.");
                        }
                        else
                        {
                            AdvanceToNextObjective(raid, raidTime);
                        }
                    }
                    return;
                }
            }

            switch (phase)
            {
                case RaidPartyPhase.Waiting:
                    BeginRaid(raid, raidTime);
                    break;
                case RaidPartyPhase.Rallying:
                    CommandFormation(raid.PartyCentroid(), 1.4f);
                    if (raidTime - phaseEnteredAt >= rallySeconds)
                    {
                        Transition(RaidPartyPhase.Advancing, raid, raidTime,
                            "The strike team advanced toward its first objective.");
                    }
                    break;
                case RaidPartyPhase.Advancing:
                    if (currentPod == null || currentPod.IsDefeated)
                    {
                        currentPod = raid.FindNextPod();
                    }
                    if (currentPod == null)
                    {
                        FinishIfReady(raid, raidTime);
                        break;
                    }
                    CommandAdvance(raid, currentPod);
                    break;
                case RaidPartyPhase.Looting:
                    CommandLooting(raid, raidTime);
                    break;
                case RaidPartyPhase.Recovering:
                    CommandRecovery(raid, raidTime);
                    break;
            }
        }

        private void CommandAdvance(DungeonRaidDirector2D raid, RaidEnemyPodBrain2D pod)
        {
            var centroid = raid.PartyCentroid();
            var waypoint = raid.GetAdvanceWaypoint(centroid, pod);
            var count = Mathf.Max(1, CountActiveMembers());
            var activeIndex = 0;
            for (var index = 0; index < members.Count; index++)
            {
                var member = members[index];
                if (member == null || !member.CanAct) continue;
                var angle = activeIndex++ / (float)count * Mathf.PI * 2f;
                var offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 1.15f;
                member.MoveToward(waypoint + offset, 0.4f);
            }
            if (Vector2.Distance(centroid, pod.ActivationCenter) <= pod.AggroRadius)
            {
                pod.ForceAlert(raid, raid.RaidTime);
            }
        }

        private void CommandCombat(
            DungeonRaidDirector2D raid,
            RaidEnemyPodBrain2D pod,
            float raidTime)
        {
            var enemies = pod.Members;
            for (var index = 0; index < members.Count; index++)
            {
                var member = members[index];
                if (member == null || !member.CanAct) continue;
                var target = raid.FindNearestActiveEnemy(member, enemies);
                if (raid.TryUseBestAbility(member, target))
                {
                    member.StopMoving();
                    continue;
                }
                if (target == null)
                {
                    member.MoveToward(raid.PartyCentroid(), 1.4f);
                    continue;
                }
                var offset = target.Position - member.Position;
                var distance = offset.magnitude;
                if (member.CanBasicAttack(target, raidTime))
                {
                    member.TryBasicAttack(target, raidTime, raid);
                }
                else if (distance > member.PreferredCombatRange + 0.35f)
                {
                    member.MoveToward(target.Position, Mathf.Max(0.15f, member.BasicAttackRange * 0.82f));
                }
                else if (member.RangedBasicAttack && distance < member.PreferredCombatRange - 0.7f)
                {
                    var retreat = offset.sqrMagnitude > 0.001f
                        ? member.Position - offset.normalized * 2.4f
                        : member.Position + Vector2.left * 2.4f;
                    member.MoveToward(retreat, 0.1f);
                }
                else
                {
                    member.StopMoving();
                }
            }
        }

        private void CommandRecovery(DungeonRaidDirector2D raid, float raidTime)
        {
            var center = raid.PartyCentroid();
            CommandFormation(center, 1.1f);
            for (var index = 0; index < members.Count; index++)
            {
                var member = members[index];
                if (member != null && member.CanAct)
                {
                    raid.TryUseBestAbility(member, null);
                }
            }
        }

        private void CommandLooting(DungeonRaidDirector2D raid, float raidTime)
        {
            if (currentChest == null || currentChest.IsOpened)
            {
                AdvanceToNextObjective(raid, raidTime);
                return;
            }
            var looter = FindNearestActiveMember(currentChest.Position);
            if (looter == null)
            {
                Transition(RaidPartyPhase.Failed, raid, raidTime,
                    "No hunter remains able to secure the room's loot.");
                return;
            }
            var distance = Vector2.Distance(looter.Position, currentChest.Position);
            if (distance <= chestInteractionRange)
            {
                looter.StopMoving();
                if (currentChest.TryOpen(looter, raid))
                {
                    AdvanceToNextObjective(raid, raidTime);
                    return;
                }
            }
            else
            {
                looter.MoveToward(currentChest.Position, chestInteractionRange * 0.75f);
            }
            for (var index = 0; index < members.Count; index++)
            {
                var member = members[index];
                if (member != null && member != looter && member.CanAct)
                {
                    member.MoveToward(currentChest.Position, 2.2f + index * 0.08f);
                }
            }
        }

        private void AdvanceToNextObjective(DungeonRaidDirector2D raid, float raidTime)
        {
            currentChest = null;
            currentPod = raid.FindNextPod();
            if (currentPod == null)
            {
                FinishIfReady(raid, raidTime);
                return;
            }
            Transition(RaidPartyPhase.Advancing, raid, raidTime,
                $"The party advanced toward {currentPod.DisplayName}.");
        }

        private void FinishIfReady(DungeonRaidDirector2D raid, float raidTime)
        {
            if (raid.AllChestsOpened())
            {
                Transition(RaidPartyPhase.Complete, raid, raidTime,
                    "The dungeon was cleared and its available treasure secured.");
            }
            else
            {
                currentChest = raid.FindChestForPod(null);
                if (currentChest != null)
                {
                    Transition(RaidPartyPhase.Looting, raid, raidTime,
                        "The strike team returned for remaining treasure.");
                }
                else
                {
                    Transition(RaidPartyPhase.Complete, raid, raidTime,
                        "All available combat objectives were completed.");
                }
            }
        }

        private void CommandFormation(Vector2 center, float radius)
        {
            var activeCount = Mathf.Max(1, CountActiveMembers());
            var activeIndex = 0;
            for (var index = 0; index < members.Count; index++)
            {
                var member = members[index];
                if (member == null || !member.CanAct) continue;
                var angle = activeIndex++ / (float)activeCount * Mathf.PI * 2f;
                var slot = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                member.MoveToward(slot, 0.25f);
            }
        }

        private RaidEnemyPodBrain2D FindEngagedPod(DungeonRaidDirector2D raid)
        {
            var pods = raid.EnemyPods;
            for (var index = 0; index < pods.Count; index++)
            {
                var pod = pods[index];
                if (pod != null && !pod.IsDefeated &&
                    pod.Phase is RaidPodPhase.Alerted or RaidPodPhase.Engaging) return pod;
            }
            return null;
        }

        private RaidAgent2D FindNearestActiveMember(Vector2 position)
        {
            RaidAgent2D best = null;
            var bestDistance = float.MaxValue;
            for (var index = 0; index < members.Count; index++)
            {
                var candidate = members[index];
                if (candidate == null || !candidate.CanAct) continue;
                var distance = Vector2.SqrMagnitude(candidate.Position - position);
                if (distance < bestDistance)
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }
            return best;
        }

        private bool HasMemberAbleToContinue()
        {
            for (var index = 0; index < members.Count; index++)
            {
                // Crowd control pauses a hunter's actions without turning a
                // temporary full-party stun into a raid failure.
                if (members[index] != null && members[index].CanReceiveDamage) return true;
            }
            return false;
        }

        private int CountActiveMembers()
        {
            var count = 0;
            for (var index = 0; index < members.Count; index++)
            {
                if (members[index] != null && members[index].CanAct) count++;
            }
            return count;
        }

        private void StopAllMembers()
        {
            for (var index = 0; index < members.Count; index++) members[index]?.StopMoving();
        }

        private void Transition(
            RaidPartyPhase next,
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
        public void ConfigureEditor(List<RaidAgent2D> assignedMembers)
        {
            members = assignedMembers ?? new List<RaidAgent2D>();
        }
#endif
    }
}
