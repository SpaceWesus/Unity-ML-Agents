using UnityEngine;

namespace Turtle.Combat
{
    [CreateAssetMenu(menuName = "Turtle/Combat/Weapon Move Set", fileName = "Weapon Move Set")]
    public sealed class WeaponMoveSetDefinition : ScriptableObject
    {
        [SerializeField] private string displayName = "Training Greatsword";
        [SerializeField] private AttackDefinition lightAttack = new()
        {
            damage = 22f,
            range = 2.35f,
            arc = 75f,
            windup = 0.336f,
            activeDuration = 0.444f,
            recovery = 0.42f,
            knockback = 1.6f,
            lunge = 1.1f,
            animationDuration = 1.2f,
            animationState = "Attack Light",
            hitboxWindows = new[]
            {
                new AttackHitboxWindow
                {
                    startNormalized = 0.28f,
                    endNormalized = 0.4f,
                    localCenter = new Vector3(-0.177f, -0.077f, 1.05f),
                    localSize = new Vector3(0.5f, 0.5f, 1.65f)
                },
                new AttackHitboxWindow
                {
                    startNormalized = 0.38f,
                    endNormalized = 0.53f,
                    localCenter = new Vector3(-0.177f, -0.077f, 1.2f),
                    localSize = new Vector3(0.58f, 0.58f, 1.9f)
                },
                new AttackHitboxWindow
                {
                    startNormalized = 0.5f,
                    endNormalized = 0.65f,
                    localCenter = new Vector3(-0.177f, -0.077f, 1.05f),
                    localSize = new Vector3(0.5f, 0.5f, 1.7f)
                }
            }
        };
        [SerializeField] private AttackDefinition heavyAttack = new()
        {
            damage = 38f,
            range = 2.65f,
            arc = 60f,
            windup = 0.312f,
            activeDuration = 0.552f,
            recovery = 0.336f,
            knockback = 2.5f,
            lunge = 1.45f,
            animationDuration = 1.2f,
            animationState = "Attack Heavy",
            hitboxWindows = new[]
            {
                new AttackHitboxWindow
                {
                    startNormalized = 0.26f,
                    endNormalized = 0.42f,
                    localCenter = new Vector3(-0.177f, -0.077f, 1.1f),
                    localSize = new Vector3(0.62f, 0.62f, 1.9f)
                },
                new AttackHitboxWindow
                {
                    startNormalized = 0.4f,
                    endNormalized = 0.58f,
                    localCenter = new Vector3(-0.177f, -0.077f, 1.2f),
                    localSize = new Vector3(0.72f, 0.72f, 2.1f)
                },
                new AttackHitboxWindow
                {
                    startNormalized = 0.56f,
                    endNormalized = 0.72f,
                    localCenter = new Vector3(-0.177f, -0.077f, 1.1f),
                    localSize = new Vector3(0.65f, 0.65f, 1.9f)
                }
            }
        };

        public string DisplayName => displayName;
        public AttackDefinition LightAttack => lightAttack;
        public AttackDefinition HeavyAttack => heavyAttack;

#if UNITY_EDITOR
        public void ConfigureEditor(
            string name,
            AttackDefinition light,
            AttackDefinition heavy)
        {
            displayName = name;
            lightAttack = light;
            heavyAttack = heavy;
        }
#endif
    }
}
