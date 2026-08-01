using System.Collections.Generic;
using UnityEngine;

namespace Turtle.DungeonRaid
{
    /// <summary>
    /// Reusable, allocation-free 2D contact queries for the raid prototype.
    /// Faction validation lives here so player and AI attacks obey identical rules.
    /// </summary>
    public sealed class RaidCombatPhysics2D
    {
        private const int MaximumContacts = 64;

        private readonly Collider2D[] overlapResults = new Collider2D[MaximumContacts];
        private readonly RaycastHit2D[] castResults = new RaycastHit2D[MaximumContacts];
        private readonly HashSet<int> uniqueAgentIds = new();
        private readonly ContactFilter2D contactFilter;

        public RaidCombatPhysics2D()
        {
            contactFilter = new ContactFilter2D
            {
                useLayerMask = false,
                useDepth = false,
                useNormalAngle = false,
                useTriggers = true
            };
        }

        public bool TrySingleHit(
            RaidAgent2D source,
            Vector2 direction,
            float distance,
            float castRadius,
            out RaidAgent2D target,
            out Vector2 impactPoint)
        {
            target = null;
            impactPoint = source != null ? source.Position : Vector2.zero;
            if (source == null || direction.sqrMagnitude <= 0.0001f) return false;

            var count = Physics2D.CircleCast(
                source.Position,
                Mathf.Max(0.02f, castRadius),
                direction.normalized,
                contactFilter,
                castResults,
                Mathf.Max(0f, distance));
            SortCastResults(count);

            for (var index = 0; index < count; index++)
            {
                var hit = castResults[index];
                if (hit.collider == null) continue;
                var agent = hit.collider.GetComponentInParent<RaidAgent2D>();
                if (agent == source) continue;
                if (agent == null)
                {
                    if (!hit.collider.isTrigger)
                    {
                        impactPoint = hit.point;
                        return false;
                    }
                    continue;
                }
                if (!agent.CanReceiveDamage || agent.Faction == source.Faction) continue;
                target = agent;
                impactPoint = hit.point;
                return true;
            }
            impactPoint = source.Position + direction.normalized * Mathf.Max(0f, distance);
            return false;
        }

        public Vector2 CollectPiercingHits(
            RaidAgent2D source,
            Vector2 direction,
            float distance,
            float castRadius,
            List<RaidAgent2D> targets,
            int maximumTargets)
        {
            targets.Clear();
            uniqueAgentIds.Clear();
            if (source == null || direction.sqrMagnitude <= 0.0001f) return Vector2.zero;

            var normalized = direction.normalized;
            var count = Physics2D.CircleCast(
                source.Position,
                Mathf.Max(0.02f, castRadius),
                normalized,
                contactFilter,
                castResults,
                Mathf.Max(0f, distance));
            SortCastResults(count);
            var impactPoint = source.Position + normalized * Mathf.Max(0f, distance);
            for (var index = 0; index < count; index++)
            {
                var hit = castResults[index];
                if (hit.collider == null) continue;
                var agent = hit.collider.GetComponentInParent<RaidAgent2D>();
                if (agent == source) continue;
                if (agent == null)
                {
                    if (!hit.collider.isTrigger)
                    {
                        impactPoint = hit.point;
                        break;
                    }
                    continue;
                }
                if (!agent.CanReceiveDamage || agent.Faction == source.Faction ||
                    !uniqueAgentIds.Add(agent.GetInstanceID())) continue;
                targets.Add(agent);
                impactPoint = hit.point;
                if (targets.Count >= Mathf.Max(1, maximumTargets)) break;
            }
            return impactPoint;
        }

        public void CollectTargets(
            RaidAgent2D source,
            RaidAttackShape shape,
            Vector2 center,
            Vector2 direction,
            float range,
            float radius,
            float width,
            float angle,
            List<RaidAgent2D> targets,
            int maximumTargets = MaximumContacts)
        {
            targets.Clear();
            uniqueAgentIds.Clear();
            if (source == null) return;

            var queryRadius = shape switch
            {
                RaidAttackShape.Rectangle => Mathf.Sqrt(
                    range * range + width * width * 0.25f),
                RaidAttackShape.Cone => Mathf.Max(0.1f, range),
                _ => Mathf.Max(0.1f, radius)
            };
            var count = Physics2D.OverlapCircle(center, queryRadius, contactFilter, overlapResults);
            var forward = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;

            for (var index = 0; index < count; index++)
            {
                var collider = overlapResults[index];
                if (collider == null) continue;
                var agent = collider.GetComponentInParent<RaidAgent2D>();
                if (agent == null || agent == source || !agent.CanReceiveDamage ||
                    agent.Faction == source.Faction ||
                    !uniqueAgentIds.Add(agent.GetInstanceID())) continue;

                var offset = agent.Position - center;
                if (!MatchesShape(shape, offset, forward, range, radius, width, angle)) continue;
                targets.Add(agent);
            }
            SortTargetsByDistance(targets, center);
            var retainedCount = Mathf.Max(1, maximumTargets);
            if (targets.Count > retainedCount)
            {
                targets.RemoveRange(retainedCount, targets.Count - retainedCount);
            }
        }

        public void CollectAlliesInCircle(
            RaidAgent2D source,
            Vector2 center,
            float radius,
            List<RaidAgent2D> targets,
            bool includeDowned)
        {
            targets.Clear();
            uniqueAgentIds.Clear();
            if (source == null) return;
            var count = Physics2D.OverlapCircle(
                center,
                Mathf.Max(0.1f, radius),
                contactFilter,
                overlapResults);
            for (var index = 0; index < count; index++)
            {
                var collider = overlapResults[index];
                if (collider == null) continue;
                var agent = collider.GetComponentInParent<RaidAgent2D>();
                if (agent == null || agent.Faction != source.Faction ||
                    agent.LifeState == RaidLifeState.Dead ||
                    (!includeDowned && !agent.CanAct) ||
                    !uniqueAgentIds.Add(agent.GetInstanceID())) continue;
                targets.Add(agent);
            }
        }

        public bool HasLineOfSight(Vector2 from, RaidAgent2D target)
        {
            if (target == null) return false;
            var offset = target.Position - from;
            var distance = offset.magnitude;
            if (distance <= 0.001f) return true;
            var count = Physics2D.Raycast(from, offset / distance, contactFilter, castResults, distance);
            SortCastResults(count);
            for (var index = 0; index < count; index++)
            {
                var collider = castResults[index].collider;
                if (collider == null) continue;
                var agent = collider.GetComponentInParent<RaidAgent2D>();
                if (agent == target) return true;
                if (agent == null && !collider.isTrigger) return false;
            }
            return false;
        }

        private static bool MatchesShape(
            RaidAttackShape shape,
            Vector2 offset,
            Vector2 forward,
            float range,
            float radius,
            float width,
            float angle)
        {
            switch (shape)
            {
                case RaidAttackShape.Rectangle:
                {
                    var right = new Vector2(-forward.y, forward.x);
                    var forwardDistance = Vector2.Dot(offset, forward);
                    var lateralDistance = Mathf.Abs(Vector2.Dot(offset, right));
                    return forwardDistance >= 0f && forwardDistance <= range &&
                           lateralDistance <= width * 0.5f;
                }
                case RaidAttackShape.Cone:
                {
                    if (offset.sqrMagnitude > range * range) return false;
                    return offset.sqrMagnitude <= 0.0001f ||
                           Vector2.Angle(forward, offset) <= angle * 0.5f;
                }
                default:
                    return offset.sqrMagnitude <= radius * radius;
            }
        }

        private void SortCastResults(int count)
        {
            for (var index = 1; index < count; index++)
            {
                var value = castResults[index];
                var previous = index - 1;
                while (previous >= 0 && castResults[previous].distance > value.distance)
                {
                    castResults[previous + 1] = castResults[previous];
                    previous--;
                }
                castResults[previous + 1] = value;
            }
        }

        private static void SortTargetsByDistance(
            List<RaidAgent2D> targets,
            Vector2 center)
        {
            for (var index = 1; index < targets.Count; index++)
            {
                var value = targets[index];
                var valueDistance = Vector2.SqrMagnitude(value.Position - center);
                var previous = index - 1;
                while (previous >= 0)
                {
                    var comparison = targets[previous];
                    var comparisonDistance =
                        Vector2.SqrMagnitude(comparison.Position - center);
                    var ordered = comparisonDistance < valueDistance ||
                                  (Mathf.Approximately(comparisonDistance, valueDistance) &&
                                   string.CompareOrdinal(
                                       comparison.AgentId,
                                       value.AgentId) <= 0);
                    if (ordered) break;
                    targets[previous + 1] = comparison;
                    previous--;
                }
                targets[previous + 1] = value;
            }
        }
    }
}
