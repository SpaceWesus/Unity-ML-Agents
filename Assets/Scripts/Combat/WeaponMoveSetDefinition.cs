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
            windup = 0.18f,
            recovery = 0.48f,
            knockback = 1.6f,
            lunge = 1.1f,
            animationState = "Attack Light"
        };
        [SerializeField] private AttackDefinition heavyAttack = new()
        {
            damage = 38f,
            range = 2.65f,
            arc = 60f,
            windup = 0.32f,
            recovery = 0.8f,
            knockback = 2.5f,
            lunge = 1.45f,
            animationState = "Attack Heavy"
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
