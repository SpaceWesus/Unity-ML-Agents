using UnityEngine;

namespace Turtle.Combat
{
    /// <summary>
    /// Draws combatant resources through one shared HUD instead of allocating a
    /// world-space Canvas and material instance for every agent.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatWorldStatusHud : MonoBehaviour
    {
        private const float OuterWidth = 84f;
        private const float OuterHeight = 23f;
        private const float InnerWidth = 76f;
        private const float BarHeight = 6f;
        private const float BorderThickness = 2f;
        private static readonly Rect CombatLabHudArea = new(16f, 16f, 505f, 337f);

        [SerializeField] private Camera worldCamera;
        [SerializeField] private Combatant player;
        [SerializeField] private bool showPlayer;
        [SerializeField, Min(1f)] private float maximumDrawDistance = 38f;

        private void Awake()
        {
            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }
        }

        private void OnGUI()
        {
            if (worldCamera == null)
            {
                worldCamera = Camera.main;
                if (worldCamera == null)
                {
                    return;
                }
            }

            var combatants = CombatantRegistry.All;
            var cameraPosition = worldCamera.transform.position;
            var maxDistanceSquared = maximumDrawDistance * maximumDrawDistance;
            for (var index = 0; index < combatants.Count; index++)
            {
                var combatant = combatants[index];
                if (combatant == null || (!showPlayer && combatant == player))
                {
                    continue;
                }

                var offset = combatant.transform.position - cameraPosition;
                if (offset.sqrMagnitude > maxDistanceSquared)
                {
                    continue;
                }

                var worldPosition = combatant.transform.position +
                                    Vector3.up * (combatant.IsTargetDummy ? 2.8f : 2.35f);
                var screenPosition = worldCamera.WorldToScreenPoint(worldPosition);
                if (screenPosition.z <= 0f)
                {
                    continue;
                }

                var topLeft = new Vector2(
                    screenPosition.x - OuterWidth * 0.5f,
                    Screen.height - screenPosition.y - OuterHeight * 0.5f);
                var outer = new Rect(topLeft.x, topLeft.y, OuterWidth, OuterHeight);
                if (outer.xMax < 0f ||
                    outer.xMin > Screen.width ||
                    outer.yMax < 0f ||
                    outer.yMin > Screen.height ||
                    outer.Overlaps(CombatLabHudArea))
                {
                    continue;
                }

                DrawStatus(topLeft, combatant);
            }
            GUI.color = Color.white;
        }

        private static void DrawStatus(Vector2 topLeft, Combatant combatant)
        {
            var abilities = combatant.Abilities;
            var barrierRatio = abilities != null ? abilities.BarrierRatio : 0f;
            var manaRatio = abilities != null ? abilities.ManaRatio : 0f;
            var outer = new Rect(topLeft.x, topLeft.y, OuterWidth, OuterHeight);

            GUI.color = new Color(0.015f, 0.018f, 0.025f, 0.88f);
            GUI.DrawTexture(outer, Texture2D.whiteTexture);

            var frameColor = barrierRatio > 0f
                ? new Color(1f, 1f, 1f, 0.98f)
                : new Color(1f, 1f, 1f, 0.36f);
            DrawFrame(outer, frameColor);

            if (barrierRatio > 0f)
            {
                GUI.color = Color.white;
                GUI.DrawTexture(
                    new Rect(
                        outer.x + BorderThickness,
                        outer.y + BorderThickness,
                        (outer.width - BorderThickness * 2f) * barrierRatio,
                        BorderThickness),
                    Texture2D.whiteTexture);
            }

            var healthRect = new Rect(
                topLeft.x + 4f,
                topLeft.y + 4f,
                InnerWidth,
                BarHeight);
            DrawBar(
                healthRect,
                combatant.HealthRatio,
                new Color(0.78f, 0.035f, 0.035f, 1f));

            var manaRect = new Rect(
                topLeft.x + 4f,
                topLeft.y + 13f,
                InnerWidth,
                BarHeight);
            DrawBar(
                manaRect,
                manaRatio,
                new Color(0.04f, 0.36f, 0.95f, 1f));
        }

        private static void DrawBar(Rect rect, float ratio, Color color)
        {
            GUI.color = new Color(0.075f, 0.08f, 0.1f, 0.96f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = color;
            GUI.DrawTexture(
                new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(ratio), rect.height),
                Texture2D.whiteTexture);
        }

        private static void DrawFrame(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(
                new Rect(rect.x, rect.y, rect.width, BorderThickness),
                Texture2D.whiteTexture);
            GUI.DrawTexture(
                new Rect(rect.x, rect.yMax - BorderThickness, rect.width, BorderThickness),
                Texture2D.whiteTexture);
            GUI.DrawTexture(
                new Rect(rect.x, rect.y, BorderThickness, rect.height),
                Texture2D.whiteTexture);
            GUI.DrawTexture(
                new Rect(rect.xMax - BorderThickness, rect.y, BorderThickness, rect.height),
                Texture2D.whiteTexture);
        }

#if UNITY_EDITOR
        public void ConfigureEditor(Camera camera, Combatant playerCombatant)
        {
            worldCamera = camera;
            player = playerCombatant;
            showPlayer = false;
            maximumDrawDistance = 38f;
        }
#endif
    }
}
