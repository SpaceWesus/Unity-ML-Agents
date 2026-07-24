using System;
using UnityEngine;

namespace Turtle.Combat
{
    public enum CombatTeam
    {
        Neutral,
        Azure,
        Crimson
    }

    public enum CombatAction
    {
        None,
        LightAttack,
        HeavyAttack,
        Dodge
    }

    public enum AttackMovementMode
    {
        Mobile,
        Anchored
    }

    public enum AttackDodgeCancelMode
    {
        DodgeAllowed,
        Committed
    }

    [Serializable]
    public struct AttackHitboxWindow
    {
        [Range(0f, 1f)] public float startNormalized;
        [Range(0f, 1f)] public float endNormalized;
        public Vector3 localCenter;
        public Vector3 localSize;
        public Vector3 localEulerAngles;
    }

    [Serializable]
    public struct AttackDefinition
    {
        [Min(0f)] public float damage;
        [Tooltip("Used by AI for spacing and threat evaluation. Damage is resolved only by weapon hitbox contact.")]
        [Min(0.1f)] public float range;
        [Tooltip("Used by AI for threat evaluation. Damage is resolved only by weapon hitbox contact.")]
        [Range(1f, 180f)] public float arc;
        [Min(0f)] public float windup;
        [Min(0.01f)] public float activeDuration;
        [Min(0.05f)] public float recovery;
        [Min(0f)] public float knockback;
        [Min(0f)] public float lunge;
        [Min(0.05f)] public float animationDuration;
        public string animationState;
        [Tooltip("Mobile attacks preserve command-driven movement. Anchored attacks lock locomotion until they finish or are cancelled.")]
        public AttackMovementMode movementMode;
        [Tooltip("DodgeAllowed attacks may be interrupted by a dodge at any point. Committed attacks must finish unless another combat effect interrupts them.")]
        public AttackDodgeCancelMode dodgeCancelMode;
        [Tooltip("Ordered offensive box volumes activated over normalized animation time.")]
        public AttackHitboxWindow[] hitboxWindows;

        public bool AllowsMovement => movementMode == AttackMovementMode.Mobile;
        public bool AllowsDodgeCancel => dodgeCancelMode == AttackDodgeCancelMode.DodgeAllowed;

        public float FirstHitboxStartNormalized
        {
            get
            {
                if (hitboxWindows == null || hitboxWindows.Length == 0)
                {
                    return animationDuration > 0f
                        ? Mathf.Clamp01(windup / animationDuration)
                        : 0f;
                }

                var first = 1f;
                for (var index = 0; index < hitboxWindows.Length; index++)
                {
                    first = Mathf.Min(first, hitboxWindows[index].startNormalized);
                }
                return Mathf.Clamp01(first);
            }
        }

        public float LastHitboxEndNormalized
        {
            get
            {
                if (hitboxWindows == null || hitboxWindows.Length == 0)
                {
                    var duration = Mathf.Max(0.05f, animationDuration);
                    return Mathf.Clamp01((windup + activeDuration) / duration);
                }

                var last = 0f;
                for (var index = 0; index < hitboxWindows.Length; index++)
                {
                    last = Mathf.Max(last, hitboxWindows[index].endNormalized);
                }
                return Mathf.Clamp01(last);
            }
        }
    }

    public readonly struct CombatHit
    {
        public CombatHit(Combatant attacker, AttackDefinition attack, Vector3 direction)
        {
            Attacker = attacker;
            Attack = attack;
            Direction = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
        }

        public Combatant Attacker { get; }
        public AttackDefinition Attack { get; }
        public Vector3 Direction { get; }
    }

    public readonly struct CombatCommand
    {
        public CombatCommand(Vector2 movement, Vector3 facing, CombatAction action)
        {
            Movement = Vector2.ClampMagnitude(movement, 1f);
            Facing = Vector3.ProjectOnPlane(facing, Vector3.up).normalized;
            Action = action;
        }

        public Vector2 Movement { get; }
        public Vector3 Facing { get; }
        public CombatAction Action { get; }
    }

    public interface ICombatCommandSource
    {
        CombatCommand SampleCommand(Combatant self);
    }
}
