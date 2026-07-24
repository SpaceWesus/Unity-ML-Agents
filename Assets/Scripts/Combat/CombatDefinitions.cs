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

    [Serializable]
    public struct AttackDefinition
    {
        [Min(0f)] public float damage;
        [Min(0.1f)] public float range;
        [Range(1f, 180f)] public float arc;
        [Min(0f)] public float windup;
        [Min(0.05f)] public float recovery;
        [Min(0f)] public float knockback;
        [Min(0f)] public float lunge;
        public string animationState;
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
