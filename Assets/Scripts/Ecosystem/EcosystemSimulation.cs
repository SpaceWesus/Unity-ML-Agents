using System;
using System.Collections.Generic;
using UnityEngine;

namespace Turtle.Ecosystem
{
    public sealed class EcosystemSimulation
    {
        private readonly EcosystemWorldState state;
        private readonly IReadOnlyList<EcosystemGearDefinition> gearCatalog;
        private readonly System.Random random;

        public EcosystemSimulation(
            EcosystemWorldState worldState,
            IReadOnlyList<EcosystemGearDefinition> availableGear)
        {
            state = worldState;
            gearCatalog = availableGear;
            random = new System.Random(state.day * 7919 + state.hunters.Count * 31);
        }

        public void AdvanceDay()
        {
            state.day++;
            foreach (var hunter in state.hunters)
            {
                if (hunter.id == state.playerHunterId)
                {
                    continue;
                }

                ChooseGear(hunter);
                ChooseGuild(hunter);
                ChooseActivity(hunter);
            }

            TrimEventLog();
        }

        public float ScoreRaidInvitation(
            HunterProfile candidate,
            HunterProfile inviter,
            MissionState mission)
        {
            var relationship = candidate.RelationshipWith(inviter.id);
            var woundPenalty = candidate.wounds * 0.18f;
            var danger = mission.difficulty / Mathf.Max(1f, candidate.level * 12f);
            return relationship.trust * 0.55f +
                   relationship.affinity * 0.35f +
                   candidate.courage * 0.8f +
                   candidate.loyalty * 0.25f -
                   danger * 0.75f -
                   woundPenalty;
        }

        public bool ResolvePartyRaid(
            HunterProfile leader,
            HunterProfile partner,
            MissionState mission,
            int leaderGearPower,
            int partnerGearPower)
        {
            var combinedPower =
                leader.level * 9 +
                partner.level * 9 +
                leaderGearPower +
                partnerGearPower -
                (leader.wounds + partner.wounds) * 5;
            var successChance = Mathf.Clamp01(
                0.28f + combinedPower / Mathf.Max(1f, mission.difficulty * 65f));
            var success = random.NextDouble() <= successChance;
            var relationship = partner.RelationshipWith(leader.id);
            var reciprocal = leader.RelationshipWith(partner.id);

            if (success)
            {
                leader.experience += mission.reward;
                partner.experience += mission.reward;
                LevelUp(leader);
                LevelUp(partner);
                relationship.trust = Mathf.Clamp(relationship.trust + 0.18f, -1f, 1f);
                relationship.affinity = Mathf.Clamp(relationship.affinity + 0.12f, -1f, 1f);
                reciprocal.trust = Mathf.Clamp(reciprocal.trust + 0.18f, -1f, 1f);
                reciprocal.affinity = Mathf.Clamp(reciprocal.affinity + 0.12f, -1f, 1f);
                AddMemory(partner, leader.id, "raid_victory",
                    $"Cleared {mission.displayName} with {leader.displayName}.", 0.65f);
                AddMemory(leader, partner.id, "raid_victory",
                    $"Cleared {mission.displayName} with {partner.displayName}.", 0.65f);
                RewardGuild(leader.guildId, mission.reward);
                Log($"DAY {state.day}: {leader.displayName} and {partner.displayName} cleared {mission.displayName}.");
            }
            else
            {
                leader.wounds++;
                partner.wounds++;
                relationship.trust = Mathf.Clamp(relationship.trust - 0.06f, -1f, 1f);
                reciprocal.trust = Mathf.Clamp(reciprocal.trust - 0.06f, -1f, 1f);
                AddMemory(partner, leader.id, "raid_failure",
                    $"Was wounded during {leader.displayName}'s raid on {mission.displayName}.", -0.4f);
                AddMemory(leader, partner.id, "raid_failure",
                    $"Failed to protect {partner.displayName} in {mission.displayName}.", -0.4f);
                Log($"DAY {state.day}: {leader.displayName}'s party returned wounded from {mission.displayName}.");
            }

            leader.currentActivity = success ? "Celebrating a raid victory" : "Recovering from a failed raid";
            partner.currentActivity = leader.currentActivity;
            leader.destinationId = leader.guildId;
            partner.destinationId = partner.guildId;
            TrimMemories(leader);
            TrimMemories(partner);
            return success;
        }

        private void ChooseGear(HunterProfile hunter)
        {
            if (gearCatalog.Count == 0)
            {
                return;
            }

            EcosystemGearDefinition best = gearCatalog[0];
            var bestScore = float.MinValue;
            foreach (var gear in gearCatalog)
            {
                var preference = gear.MoveSet switch
                {
                    GearMoveSet.VanguardBlade => hunter.loyalty * 4f + hunter.courage * 2f,
                    GearMoveSet.TitanGreatsword => hunter.courage * 5f + hunter.ambition * 2f,
                    GearMoveSet.RiftDaggers => hunter.greed * 4f + hunter.ambition * 3f,
                    _ => 0f
                };
                var score = gear.Power + preference + (float)random.NextDouble() * 2f;
                if (score > bestScore)
                {
                    best = gear;
                    bestScore = score;
                }
            }

            if (hunter.equippedGearId != best.GearId)
            {
                hunter.equippedGearId = best.GearId;
                Log($"DAY {state.day}: {hunter.displayName} equipped {best.DisplayName} to pursue {hunter.goal}.");
            }
        }

        private void ChooseGuild(HunterProfile hunter)
        {
            if (!string.IsNullOrEmpty(hunter.guildId) || state.guilds.Count == 0)
            {
                return;
            }

            var joinChance = 0.08f + hunter.loyalty * 0.1f + hunter.ambition * 0.08f;
            if (random.NextDouble() > joinChance)
            {
                return;
            }

            var guild = state.guilds[random.Next(state.guilds.Count)];
            hunter.guildId = guild.id;
            guild.memberIds.Add(hunter.id);
            hunter.destinationId = guild.id;
            hunter.currentActivity = $"Joined {guild.displayName}";
            Log($"DAY {state.day}: {hunter.displayName} joined {guild.displayName}.");
        }

        private void ChooseActivity(HunterProfile hunter)
        {
            if (hunter.wounds > 0 && random.NextDouble() < 0.55)
            {
                hunter.wounds--;
                hunter.currentActivity = "Recovering";
                hunter.destinationId = hunter.guildId;
                return;
            }

            var bestMission = state.missions[0];
            var bestScore = float.MinValue;
            foreach (var mission in state.missions)
            {
                var risk = mission.difficulty / Mathf.Max(1f, hunter.level * 10f);
                var traitFit = mission.favoredTrait switch
                {
                    "courage" => hunter.courage,
                    "ambition" => hunter.ambition,
                    "greed" => hunter.greed,
                    _ => hunter.loyalty
                };
                var score = mission.reward * (0.5f + hunter.greed) +
                            traitFit * 20f -
                            risk * (1.1f - hunter.courage) * 25f;
                if (score > bestScore)
                {
                    bestMission = mission;
                    bestScore = score;
                }
            }

            hunter.currentActivity = $"Attempting {bestMission.displayName}";
            hunter.destinationId = bestMission.id;
            var gearPower = FindGearPower(hunter.equippedGearId);
            var chance = Mathf.Clamp01(
                0.18f + (hunter.level * 8f + gearPower) /
                Mathf.Max(1f, bestMission.difficulty * 55f));
            if (random.NextDouble() <= chance)
            {
                hunter.experience += bestMission.reward;
                LevelUp(hunter);
                RewardGuild(hunter.guildId, bestMission.reward / 2);
                AddMemory(hunter, bestMission.id, "solo_victory",
                    $"Cleared {bestMission.displayName} alone.", 0.45f);
                Log($"DAY {state.day}: {hunter.displayName} cleared {bestMission.displayName}.");
            }
            else
            {
                hunter.wounds++;
                AddMemory(hunter, bestMission.id, "solo_failure",
                    $"Was defeated in {bestMission.displayName}.", -0.35f);
                Log($"DAY {state.day}: {hunter.displayName} was wounded in {bestMission.displayName}.");
            }

            TrimMemories(hunter);
        }

        private int FindGearPower(string gearId)
        {
            foreach (var gear in gearCatalog)
            {
                if (gear.GearId == gearId)
                {
                    return gear.Power;
                }
            }

            return 0;
        }

        private void RewardGuild(string guildId, int amount)
        {
            var guild = state.guilds.Find(item => item.id == guildId);
            if (guild != null)
            {
                guild.resources += amount;
                guild.prestige += amount * 0.1f;
            }
        }

        private void AddMemory(
            HunterProfile owner,
            string subjectId,
            string eventType,
            string summary,
            float weight)
        {
            owner.memories.Add(new HunterMemory
            {
                day = state.day,
                subjectId = subjectId,
                eventType = eventType,
                summary = summary,
                emotionalWeight = weight
            });
        }

        private static void LevelUp(HunterProfile hunter)
        {
            while (hunter.experience >= hunter.level * 30)
            {
                hunter.experience -= hunter.level * 30;
                hunter.level++;
            }
        }

        private static void TrimMemories(HunterProfile hunter)
        {
            while (hunter.memories.Count > 12)
            {
                hunter.memories.RemoveAt(0);
            }
        }

        private void Log(string entry)
        {
            state.eventLog.Add(entry);
        }

        private void TrimEventLog()
        {
            while (state.eventLog.Count > 18)
            {
                state.eventLog.RemoveAt(0);
            }
        }
    }
}
