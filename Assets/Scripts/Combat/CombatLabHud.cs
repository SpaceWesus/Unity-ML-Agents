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

        private void OnGUI()
        {
            EnsureStyles();
            var panel = new Rect(22f, 22f, 410f, 182f);
            GUI.color = new Color(0.025f, 0.035f, 0.06f, 0.9f);
            GUI.Box(panel, GUIContent.none, panelStyle);
            GUI.color = Color.white;
            GUI.Label(new Rect(42f, 36f, 360f, 30f), "WEAPONS TESTING // COMBAT LAB", titleStyle);
            var health = player == null ? 0f : player.HealthRatio;
            GUI.color = new Color(0.08f, 0.08f, 0.1f, 0.9f);
            GUI.DrawTexture(new Rect(42f, 76f, 280f, 15f), Texture2D.whiteTexture);
            GUI.color = Color.Lerp(new Color(0.75f, 0.05f, 0.04f), new Color(0.15f, 0.8f, 0.35f), health);
            GUI.DrawTexture(new Rect(42f, 76f, 280f * health, 15f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            var weapon = player != null && player.MoveSet != null ? player.MoveSet.DisplayName : "Unarmed";
            GUI.Label(new Rect(42f, 100f, 360f, 80f),
                $"{weapon}\nWASD move  •  Mouse orbit  •  LMB light  •  RMB heavy  •  Space dodge\nF1 reset arena  •  F2 pause/resume AI",
                bodyStyle);

            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            GUI.color = new Color(0.75f, 0.9f, 1f, 0.9f);
            GUI.DrawTexture(new Rect(center.x - 1f, center.y - 8f, 2f, 16f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(center.x - 8f, center.y - 1f, 16f, 2f), Texture2D.whiteTexture);
            GUI.color = Color.white;
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
        }

#if UNITY_EDITOR
        public void ConfigureEditor(Combatant playerCombatant)
        {
            player = playerCombatant;
        }
#endif
    }
}
