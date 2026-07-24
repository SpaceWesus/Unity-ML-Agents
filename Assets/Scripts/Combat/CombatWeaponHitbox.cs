using System.Collections.Generic;
using UnityEngine;

namespace Turtle.Combat
{
    [DisallowMultipleComponent]
    public sealed class CombatWeaponHitbox : MonoBehaviour
    {
        private const int QueryCapacity = 32;
        private const int MaximumTimelineWindows = 8;
        private const int RotationalSweepSamples = 3;
        private static int transformsSynchronizedFrame = -1;

        [SerializeField] private Combatant owner;
        [SerializeField] private LayerMask hitLayers = ~0;

        private readonly Collider[] overlapBuffer = new Collider[QueryCapacity];
        private readonly RaycastHit[] castBuffer = new RaycastHit[QueryCapacity];
        private readonly HashSet<Combatant> hitTargets = new();
        private readonly Vector3[] previousCenters = new Vector3[MaximumTimelineWindows];
        private readonly Quaternion[] previousRotations = new Quaternion[MaximumTimelineWindows];
        private readonly bool[] windowWasActive = new bool[MaximumTimelineWindows];

        private AttackDefinition activeAttack;
        private float normalizedProgress;
        private float previousNormalizedProgress;
        private bool attackRunning;

        public bool IsActive => attackRunning;

        public void BeginAttack(AttackDefinition attack)
        {
            activeAttack = attack;
            normalizedProgress = 0f;
            previousNormalizedProgress = 0f;
            hitTargets.Clear();
            for (var index = 0; index < windowWasActive.Length; index++)
            {
                windowWasActive[index] = false;
            }
            attackRunning = owner != null &&
                            attack.hitboxWindows != null &&
                            attack.hitboxWindows.Length > 0;
        }

        public void SetNormalizedProgress(float progress)
        {
            normalizedProgress = Mathf.Clamp01(progress);
        }

        public void EndAttack()
        {
            attackRunning = false;
            hitTargets.Clear();
            for (var index = 0; index < windowWasActive.Length; index++)
            {
                windowWasActive[index] = false;
            }
        }

        private void OnDisable()
        {
            EndAttack();
        }

        private void LateUpdate()
        {
            if (!attackRunning || owner == null || !owner.IsAlive)
            {
                EndAttack();
                return;
            }

            SynchronizeMovedHurtboxes();
            var windows = activeAttack.hitboxWindows;
            var windowCount = Mathf.Min(windows.Length, MaximumTimelineWindows);
            for (var index = 0; index < windowCount; index++)
            {
                EvaluateWindow(index, windows[index]);
            }
            for (var index = windowCount; index < MaximumTimelineWindows; index++)
            {
                windowWasActive[index] = false;
            }
            previousNormalizedProgress = normalizedProgress;
        }

        private void EvaluateWindow(int index, AttackHitboxWindow window)
        {
            var start = Mathf.Clamp01(Mathf.Min(window.startNormalized, window.endNormalized));
            var end = Mathf.Clamp01(Mathf.Max(window.startNormalized, window.endNormalized));
            var activeNow = normalizedProgress >= start && normalizedProgress <= end;
            var crossedWindowThisFrame =
                previousNormalizedProgress < start &&
                normalizedProgress > end;
            if (!activeNow && !crossedWindowThisFrame)
            {
                windowWasActive[index] = false;
                return;
            }

            GetWorldPose(window, out var center, out var halfExtents, out var rotation);
            if (!windowWasActive[index])
            {
                ResolveOverlapBox(center, halfExtents, rotation);
            }
            else
            {
                SweepBox(
                    previousCenters[index],
                    previousRotations[index],
                    center,
                    rotation,
                    halfExtents);
            }

            previousCenters[index] = center;
            previousRotations[index] = rotation;
            windowWasActive[index] = activeNow;
        }

        private void GetWorldPose(
            AttackHitboxWindow window,
            out Vector3 center,
            out Vector3 halfExtents,
            out Quaternion rotation)
        {
            center = transform.TransformPoint(window.localCenter);
            rotation = transform.rotation * Quaternion.Euler(window.localEulerAngles);
            var scale = transform.lossyScale;
            scale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            halfExtents = Vector3.Scale(
                Vector3.Max(window.localSize, Vector3.one * 0.02f) * 0.5f,
                scale);
        }

        private void SweepBox(
            Vector3 previousCenter,
            Quaternion previousRotation,
            Vector3 currentCenter,
            Quaternion currentRotation,
            Vector3 halfExtents)
        {
            var displacement = currentCenter - previousCenter;
            var distance = displacement.magnitude;
            if (distance > 0.0001f)
            {
                var count = Physics.BoxCastNonAlloc(
                    previousCenter,
                    halfExtents,
                    displacement / distance,
                    castBuffer,
                    previousRotation,
                    distance,
                    hitLayers,
                    QueryTriggerInteraction.Collide);
                ResolveCastHits(count);
            }

            // BoxCast sweeps translation but not rotation. Interpolated oriented
            // overlaps cover the angular motion of a sword between render frames.
            for (var sample = 1; sample <= RotationalSweepSamples; sample++)
            {
                var t = sample / (float)RotationalSweepSamples;
                ResolveOverlapBox(
                    Vector3.Lerp(previousCenter, currentCenter, t),
                    halfExtents,
                    Quaternion.Slerp(previousRotation, currentRotation, t));
            }
        }

        private void ResolveOverlapBox(
            Vector3 center,
            Vector3 halfExtents,
            Quaternion rotation)
        {
            var count = Physics.OverlapBoxNonAlloc(
                center,
                halfExtents,
                overlapBuffer,
                rotation,
                hitLayers,
                QueryTriggerInteraction.Collide);
            for (var index = 0; index < count; index++)
            {
                TryResolveHurtbox(overlapBuffer[index]);
            }
        }

        private void ResolveCastHits(int count)
        {
            for (var index = 0; index < count; index++)
            {
                TryResolveHurtbox(castBuffer[index].collider);
            }
        }

        private void TryResolveHurtbox(Collider candidate)
        {
            var hurtbox = candidate != null
                ? candidate.GetComponent<CombatHurtbox>()
                : null;
            if (hurtbox == null ||
                hurtbox.Owner == null ||
                hitTargets.Contains(hurtbox.Owner))
            {
                return;
            }

            var direction = hurtbox.Owner.transform.position - owner.transform.position;
            var hit = new CombatHit(owner, activeAttack, direction);
            if (hurtbox.TryReceiveHit(hit))
            {
                hitTargets.Add(hurtbox.Owner);
                owner.NotifyAttackConnected(hurtbox.Owner, activeAttack.damage);
            }
        }

        private static void SynchronizeMovedHurtboxes()
        {
            if (transformsSynchronizedFrame == Time.frameCount)
            {
                return;
            }

            Physics.SyncTransforms();
            transformsSynchronizedFrame = Time.frameCount;
        }

#if UNITY_EDITOR
        public void ConfigureEditor(Combatant assignedOwner)
        {
            owner = assignedOwner;
        }

        private void OnDrawGizmosSelected()
        {
            if (activeAttack.hitboxWindows == null)
            {
                return;
            }

            var windows = activeAttack.hitboxWindows;
            var count = Mathf.Min(windows.Length, MaximumTimelineWindows);
            for (var index = 0; index < count; index++)
            {
                var window = windows[index];
                var activeWindow =
                    normalizedProgress >= window.startNormalized &&
                    normalizedProgress <= window.endNormalized;
                Gizmos.color = activeWindow
                    ? new Color(1f, 0.05f, 0.03f, 0.8f)
                    : new Color(1f, 0.55f, 0.05f, 0.3f);
                GetWorldPose(window, out var center, out var halfExtents, out var rotation);
                var oldMatrix = Gizmos.matrix;
                Gizmos.matrix = Matrix4x4.TRS(center, rotation, Vector3.one);
                Gizmos.DrawWireCube(Vector3.zero, halfExtents * 2f);
                Gizmos.matrix = oldMatrix;
            }
        }
#endif
    }
}
