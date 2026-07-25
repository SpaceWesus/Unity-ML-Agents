using UnityEngine;

namespace Turtle.Combat
{
    [DisallowMultipleComponent]
    public sealed class CombatLabHud : MonoBehaviour
    {
        [SerializeField] private Combatant player;
        private GUIStyle panelStyle;
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle abilityStyle;
        private float hitMarkerEndsAt;

        private void OnEnable()
        {
            SubscribeToPlayer();
        }

        private void OnDisable()
        {
            UnsubscribeFromPlayer();
        }

        private void OnGUI()
        {
            EnsureStyles();
            var panel = new Rect(22f, 22f, 490f, 325f);
            GUI.color = new Color(0.025f, 0.035f, 0.06f, 0.9f);
            GUI.Box(panel, GUIContent.none, panelStyle);
            GUI.color = Color.white;
            GUI.Label(
                new Rect(42f, 36f, 440f, 30f),
                "WEAPONS TESTING // COMBAT LAB",
                titleStyle);

            var health = player == null ? 0f : player.HealthRatio;
            GUI.color = new Color(0.08f, 0.08f, 0.1f, 0.9f);
            GUI.DrawTexture(new Rect(42f, 76f, 280f, 15f), Texture2D.whiteTexture);
            GUI.color = Color.Lerp(
                new Color(0.75f, 0.05f, 0.04f),
                new Color(0.15f, 0.8f, 0.35f),
                health);
            GUI.DrawTexture(new Rect(42f, 76f, 280f * health, 15f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            var abilities = player != null ? player.Abilities : null;
            var mana = abilities != null ? abilities.ManaRatio : 0f;
            GUI.color = new Color(0.08f, 0.08f, 0.1f, 0.9f);
            GUI.DrawTexture(new Rect(42f, 95f, 280f, 7f), Texture2D.whiteTexture);
            GUI.color = new Color(0.04f, 0.36f, 0.95f);
            GUI.DrawTexture(new Rect(42f, 95f, 280f * mana, 7f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(
                new Rect(332f, 76f, 150f, 26f),
                abilities != null
                    ? $"{abilities.CurrentMana:0} / {abilities.MaximumMana:0} MANA"
                    : "0 / 0 MANA",
                bodyStyle);

            var weapon = player != null && player.MoveSet != null
                ? player.MoveSet.DisplayName
                : "Unarmed";
            GUI.Label(
                new Rect(42f, 108f, 440f, 52f),
                $"{weapon}\nWASD move  •  Mouse orbit  •  LMB light  •  RMB heavy  •  Space dodge",
                bodyStyle);

            DrawAbilitySlot(abilities, 0, "1", 164f);
            DrawAbilitySlot(abilities, 1, "2", 189f);
            DrawAbilitySlot(abilities, 2, "3", 214f);
            DrawAbilitySlot(
                abilities,
                CombatAbilityLoadoutDefinition.UltimateSlot,
                "4 ULT",
                239f);

            var ultimateRatio = abilities != null ? abilities.UltimateRatio : 0f;
            GUI.color = new Color(0.08f, 0.08f, 0.1f, 0.9f);
            GUI.DrawTexture(new Rect(42f, 269f, 280f, 10f), Texture2D.whiteTexture);
            GUI.color = new Color(0.65f, 0.24f, 1f, 0.95f);
            GUI.DrawTexture(
                new Rect(42f, 269f, 280f * ultimateRatio, 10f),
                Texture2D.whiteTexture);
            GUI.color = Color.white;
            var barrier = abilities != null && abilities.BarrierHealth > 0f
                ? $"  •  Barrier {abilities.BarrierHealth:0}"
                : string.Empty;
            GUI.Label(
                new Rect(42f, 284f, 440f, 32f),
                $"Ultimate {ultimateRatio * 100f:0}%{barrier}  •  F1 reset  •  F2 pause AI",
                bodyStyle);

            DrawCrosshairAndHitMarker();
        }

        private void DrawCrosshairAndHitMarker()
        {
            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            GUI.color = new Color(0.75f, 0.9f, 1f, 0.9f);
            GUI.DrawTexture(
                new Rect(center.x - 1f, center.y - 8f, 2f, 16f),
                Texture2D.whiteTexture);
            GUI.DrawTexture(
                new Rect(center.x - 8f, center.y - 1f, 16f, 2f),
                Texture2D.whiteTexture);

            if (Time.time < hitMarkerEndsAt)
            {
                var fade = Mathf.Clamp01((hitMarkerEndsAt - Time.time) / 0.14f);
                GUI.color = new Color(1f, 0.94f, 0.78f, fade);
                const float offset = 11f;
                const float length = 9f;
                DrawRotatedLine(center + new Vector2(-offset, -offset), 45f, length, 3f);
                DrawRotatedLine(center + new Vector2(offset, -offset), -45f, length, 3f);
                DrawRotatedLine(center + new Vector2(-offset, offset), -45f, length, 3f);
                DrawRotatedLine(center + new Vector2(offset, offset), 45f, length, 3f);
            }
            GUI.color = Color.white;
        }

        private void DrawAbilitySlot(
            CombatAbilityController abilities,
            int slot,
            string key,
            float y)
        {
            var ability = abilities != null ? abilities.GetAbility(slot) : null;
            var ready = ability != null && abilities.IsReady(slot);
            var cooldown = abilities != null ? abilities.GetCooldownRemaining(slot) : 0f;
            var state = ability == null
                ? "EMPTY"
                : ready
                    ? "READY"
                    : !abilities.CanAfford(ability)
                        ? "MANA"
                    : slot == CombatAbilityLoadoutDefinition.UltimateSlot
                        ? $"{abilities.UltimateRatio * 100f:0}%"
                        : $"{cooldown:0.0}s";
            GUI.color = ready
                ? new Color(0.45f, 0.9f, 1f)
                : new Color(0.65f, 0.68f, 0.76f);
            GUI.Label(
                new Rect(42f, y, 440f, 24f),
                $"[{key}]  {(ability != null ? ability.DisplayName : "Unassigned")}  //  {state}",
                abilityStyle);
            GUI.color = Color.white;
        }

        private static void DrawRotatedLine(
            Vector2 center,
            float angle,
            float length,
            float thickness)
        {
            var previousMatrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, center);
            GUI.DrawTexture(
                new Rect(
                    center.x - length * 0.5f,
                    center.y - thickness * 0.5f,
                    length,
                    thickness),
                Texture2D.whiteTexture);
            GUI.matrix = previousMatrix;
        }

        private void HandleAttackConnected(
            Combatant attacker,
            Combatant target,
            float damage)
        {
            hitMarkerEndsAt = Time.time + 0.14f;
        }

        private void SubscribeToPlayer()
        {
            if (player != null)
            {
                player.AttackConnected -= HandleAttackConnected;
                player.AttackConnected += HandleAttackConnected;
            }
        }

        private void UnsubscribeFromPlayer()
        {
            if (player != null)
            {
                player.AttackConnected -= HandleAttackConnected;
            }
        }

        private void EnsureStyles()
        {
            if (panelStyle != null)
            {
                return;
            }
            panelStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = Texture2D.whiteTexture }
            };
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.55f, 0.82f, 1f) }
            };
            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                wordWrap = true,
                normal = { textColor = new Color(0.88f, 0.92f, 0.98f) }
            };
            abilityStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
        }

#if UNITY_EDITOR
        public void ConfigureEditor(Combatant playerCombatant)
        {
            UnsubscribeFromPlayer();
            player = playerCombatant;
            if (isActiveAndEnabled)
            {
                SubscribeToPlayer();
            }
        }
#endif
    }
}
