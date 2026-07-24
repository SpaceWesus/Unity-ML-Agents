using System;
using UnityEngine;

namespace Turtle.Combat
{
    /// <summary>
    /// Receives presentation-only events embedded in the imported animation clips.
    /// Gameplay hit authority deliberately remains in Combatant's timed action flow.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatAnimationEventRelay : MonoBehaviour
    {
        public event Action LeftFootstep;
        public event Action RightFootstep;
        public event Action Landed;
        public event Action WeaponSwitched;

        public void FootL()
        {
            LeftFootstep?.Invoke();
        }

        public void FootR()
        {
            RightFootstep?.Invoke();
        }

        public void Land()
        {
            Landed?.Invoke();
        }

        public void WeaponSwitch()
        {
            WeaponSwitched?.Invoke();
        }

        public void Hit()
        {
            // Source-pack timing marker. Offensive gameplay volumes are authored
            // on the move's normalized hitbox timeline, not animation callbacks.
        }
    }
}
