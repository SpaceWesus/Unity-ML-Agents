using System;
using UnityEngine;

namespace Turtle.Ecosystem
{
    public readonly struct EcosystemHunterPawnVisual
    {
        public EcosystemHunterPawnVisual(
            Color guildColor,
            Color bodyColor,
            string archetypeGlyph,
            float healthRatio,
            float manaRatio,
            float shieldRatio)
        {
            GuildColor = guildColor;
            BodyColor = bodyColor;
            ArchetypeGlyph = string.IsNullOrWhiteSpace(archetypeGlyph) ? "?" : archetypeGlyph;
            HealthRatio = Mathf.Clamp01(healthRatio);
            ManaRatio = Mathf.Clamp01(manaRatio);
            ShieldRatio = Mathf.Clamp01(shieldRatio);
        }

        public Color GuildColor { get; }
        public Color BodyColor { get; }
        public string ArchetypeGlyph { get; }
        public float HealthRatio { get; }
        public float ManaRatio { get; }
        public float ShieldRatio { get; }
    }

    /// <summary>
    /// World-space visual projection of one persistent hunter. This component stores only a
    /// stable hunter ID plus transient rendering state; it never owns HunterProfile, vitals,
    /// combat outcomes, navigation decisions, or random state. All movement is driven by the
    /// central EcosystemSpatialWorldView.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EcosystemHunterPawn2D : MonoBehaviour
    {
        private const float BodyDiameter = 0.86f;
        private const float BarWidth = 0.92f;

        [Header("Serialized visual assets")]
        [SerializeField] private Sprite authoredCircleSprite;
        [SerializeField] private Sprite authoredSquareSprite;

        [Header("Visual hierarchy")]
        [SerializeField] private Transform visualRoot;
        [SerializeField] private SpriteRenderer selectionHalo;
        [SerializeField] private SpriteRenderer guildRing;
        [SerializeField] private SpriteRenderer body;
        [SerializeField] private TextMesh archetypeGlyph;
        [SerializeField] private SpriteRenderer healthBackground;
        [SerializeField] private SpriteRenderer healthFill;
        [SerializeField] private SpriteRenderer manaBackground;
        [SerializeField] private SpriteRenderer manaFill;
        [SerializeField] private SpriteRenderer shieldTop;
        [SerializeField] private SpriteRenderer shieldBottom;
        [SerializeField] private SpriteRenderer shieldLeft;
        [SerializeField] private SpriteRenderer shieldRight;
        [SerializeField] private CircleCollider2D selectionCollider;

        private static Sprite runtimeCircleSprite;
        private static Sprite runtimeSquareSprite;
        private string hunterId = string.Empty;
        private EcosystemSpatialPlane spatialPlane;
        private int baseSortingOrder;

        public string HunterId => hunterId;
        public bool IsBound => !string.IsNullOrEmpty(hunterId);
        public Collider2D SelectionCollider => selectionCollider;
        public Sprite AuthoredCircleSprite => authoredCircleSprite;
        public Sprite AuthoredSquareSprite => authoredSquareSprite;

        private void Awake()
        {
            EnsureVisualHierarchy();
        }

        public void Bind(string stableHunterId, EcosystemSpatialPlane plane)
        {
            EnsureVisualHierarchy();
            hunterId = stableHunterId ?? string.Empty;
            spatialPlane = plane;
            ApplyPlane();
            gameObject.name = string.IsNullOrEmpty(hunterId)
                ? "Hunter Pawn Slot"
                : $"Hunter Pawn [{hunterId}]";
            gameObject.SetActive(!string.IsNullOrEmpty(hunterId));
        }

        public void Release()
        {
            hunterId = string.Empty;
            SetSelected(false);
            gameObject.name = "Hunter Pawn Slot";
            gameObject.SetActive(false);
        }

        public void ApplyVisual(in EcosystemHunterPawnVisual visual)
        {
            EnsureVisualHierarchy();
            guildRing.color = visual.GuildColor;
            body.color = visual.BodyColor;
            archetypeGlyph.text = visual.ArchetypeGlyph;
            SetFill(healthFill, visual.HealthRatio, BarWidth);
            SetFill(manaFill, visual.ManaRatio, BarWidth);

            var shieldAlpha = Mathf.Lerp(0.16f, 1f, visual.ShieldRatio);
            var shieldColor = new Color(0.94f, 0.97f, 1f, shieldAlpha);
            shieldTop.color = shieldColor;
            shieldBottom.color = shieldColor;
            shieldLeft.color = shieldColor;
            shieldRight.color = shieldColor;
        }

        public void SetSelected(bool selected)
        {
            EnsureVisualHierarchy();
            selectionHalo.enabled = selected;
        }

        public void SetPlanarPosition(Vector2 planarPosition, float perpendicularPosition = 0f)
        {
            transform.position = EcosystemSpatialCoordinates.ToWorld(
                planarPosition,
                spatialPlane,
                perpendicularPosition);
        }

        public void SetPlanarFacing(Vector2 planarFacing)
        {
            if (planarFacing.sqrMagnitude <= 0.0001f || archetypeGlyph == null)
            {
                return;
            }

            // A tiny glyph lean provides readable facing without rotating the circular body.
            var normalized = planarFacing.normalized;
            archetypeGlyph.transform.localPosition = new Vector3(
                normalized.x * 0.055f,
                normalized.y * 0.055f + 0.015f,
                archetypeGlyph.transform.localPosition.z);
        }

        public void SetSortingOrder(int sortingOrder)
        {
            baseSortingOrder = sortingOrder;
            if (selectionHalo == null || archetypeGlyph == null)
            {
                EnsureVisualHierarchy();
                return;
            }
            ApplySortingOrders();
        }

        private void ApplySortingOrders()
        {
            var sortingOrder = baseSortingOrder;
            selectionHalo.sortingOrder = sortingOrder;
            guildRing.sortingOrder = sortingOrder + 1;
            body.sortingOrder = sortingOrder + 2;
            healthBackground.sortingOrder = sortingOrder + 3;
            manaBackground.sortingOrder = sortingOrder + 3;
            healthFill.sortingOrder = sortingOrder + 4;
            manaFill.sortingOrder = sortingOrder + 4;
            shieldTop.sortingOrder = sortingOrder + 5;
            shieldBottom.sortingOrder = sortingOrder + 5;
            shieldLeft.sortingOrder = sortingOrder + 5;
            shieldRight.sortingOrder = sortingOrder + 5;
            archetypeGlyph.GetComponent<MeshRenderer>().sortingOrder = sortingOrder + 6;
        }

        public bool Owns(Collider2D candidate)
        {
            return candidate != null && selectionCollider == candidate;
        }

        private void EnsureVisualHierarchy()
        {
            EnsureRuntimeFallbackSprites();
            var circle = authoredCircleSprite != null ? authoredCircleSprite : runtimeCircleSprite;
            if (visualRoot == null)
            {
                visualRoot = FindOrCreateChild(transform, "Visual Root");
            }

            if (selectionHalo == null)
            {
                selectionHalo = EnsureSpriteRenderer(
                    visualRoot,
                    "Selection Halo",
                    circle,
                    new Vector3(1.18f, 1.18f, 1f),
                    new Color(1f, 0.82f, 0.18f, 0.92f));
            }
            if (guildRing == null)
            {
                guildRing = EnsureSpriteRenderer(
                    visualRoot,
                    "Guild Ring",
                    circle,
                    Vector3.one,
                    Color.gray);
            }
            if (body == null)
            {
                body = EnsureSpriteRenderer(
                    visualRoot,
                    "Class and Gear Fill",
                    circle,
                    new Vector3(BodyDiameter, BodyDiameter, 1f),
                    new Color(0.28f, 0.48f, 0.76f, 1f));
            }

            if (archetypeGlyph == null)
            {
                archetypeGlyph = EnsureTextMesh(visualRoot, "Class Glyph");
            }
            if (healthBackground == null)
            {
                healthBackground = EnsureBar(
                    visualRoot,
                    "Health Background",
                    new Vector3(0f, -0.61f, 0f),
                    new Color(0.06f, 0.035f, 0.04f, 0.96f));
            }
            if (healthFill == null)
            {
                healthFill = EnsureBar(
                    visualRoot,
                    "Health Fill",
                    new Vector3(0f, -0.61f, -0.005f),
                    new Color(0.86f, 0.055f, 0.045f, 1f));
            }
            if (manaBackground == null)
            {
                manaBackground = EnsureBar(
                    visualRoot,
                    "Mana Background",
                    new Vector3(0f, -0.72f, 0f),
                    new Color(0.025f, 0.045f, 0.09f, 0.96f));
            }
            if (manaFill == null)
            {
                manaFill = EnsureBar(
                    visualRoot,
                    "Mana Fill",
                    new Vector3(0f, -0.72f, -0.005f),
                    new Color(0.035f, 0.34f, 1f, 1f));
            }

            if (shieldTop == null)
            {
                shieldTop = EnsureFramePart(
                    visualRoot,
                    "Shield Top",
                    new Vector3(0f, -0.545f, -0.01f),
                    new Vector3(BarWidth + 0.08f, 0.025f, 1f));
            }
            if (shieldBottom == null)
            {
                shieldBottom = EnsureFramePart(
                    visualRoot,
                    "Shield Bottom",
                    new Vector3(0f, -0.785f, -0.01f),
                    new Vector3(BarWidth + 0.08f, 0.025f, 1f));
            }
            if (shieldLeft == null)
            {
                shieldLeft = EnsureFramePart(
                    visualRoot,
                    "Shield Left",
                    new Vector3(-(BarWidth + 0.055f) * 0.5f, -0.665f, -0.01f),
                    new Vector3(0.025f, 0.265f, 1f));
            }
            if (shieldRight == null)
            {
                shieldRight = EnsureFramePart(
                    visualRoot,
                    "Shield Right",
                    new Vector3((BarWidth + 0.055f) * 0.5f, -0.665f, -0.01f),
                    new Vector3(0.025f, 0.265f, 1f));
            }

            if (selectionCollider == null)
            {
                selectionCollider = GetComponent<CircleCollider2D>();
            }
            if (selectionCollider == null)
            {
                selectionCollider = gameObject.AddComponent<CircleCollider2D>();
            }
            selectionCollider.radius = 0.56f;
            selectionCollider.isTrigger = true;
            var square = authoredSquareSprite != null ? authoredSquareSprite : runtimeSquareSprite;
            selectionHalo.sprite = circle;
            guildRing.sprite = circle;
            body.sprite = circle;
            healthBackground.sprite = square;
            healthFill.sprite = square;
            manaBackground.sprite = square;
            manaFill.sprite = square;
            shieldTop.sprite = square;
            shieldBottom.sprite = square;
            shieldLeft.sprite = square;
            shieldRight.sprite = square;
            selectionHalo.enabled = false;
            ApplySortingOrders();
            ApplyPlane();
        }

        private void ApplyPlane()
        {
            if (visualRoot == null)
            {
                return;
            }
            visualRoot.localRotation = spatialPlane == EcosystemSpatialPlane.XY
                ? Quaternion.identity
                : Quaternion.Euler(90f, 0f, 0f);
        }

        private static void SetFill(SpriteRenderer renderer, float ratio, float width)
        {
            ratio = Mathf.Clamp01(ratio);
            renderer.transform.localScale = new Vector3(width * ratio, 0.075f, 1f);
            var position = renderer.transform.localPosition;
            position.x = -(width * (1f - ratio)) * 0.5f;
            renderer.transform.localPosition = position;
        }

        private SpriteRenderer EnsureBar(
            Transform parent,
            string childName,
            Vector3 localPosition,
            Color color)
        {
            var renderer = EnsureSpriteRenderer(
                parent,
                childName,
                authoredSquareSprite != null ? authoredSquareSprite : runtimeSquareSprite,
                new Vector3(BarWidth, 0.075f, 1f),
                color);
            renderer.transform.localPosition = localPosition;
            return renderer;
        }

        private SpriteRenderer EnsureFramePart(
            Transform parent,
            string childName,
            Vector3 localPosition,
            Vector3 localScale)
        {
            var renderer = EnsureSpriteRenderer(
                parent,
                childName,
                authoredSquareSprite != null ? authoredSquareSprite : runtimeSquareSprite,
                localScale,
                new Color(0.94f, 0.97f, 1f, 0.35f));
            renderer.transform.localPosition = localPosition;
            return renderer;
        }

        private static SpriteRenderer EnsureSpriteRenderer(
            Transform parent,
            string childName,
            Sprite sprite,
            Vector3 localScale,
            Color color)
        {
            var child = FindOrCreateChild(parent, childName);
            var renderer = child.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = child.gameObject.AddComponent<SpriteRenderer>();
            }
            renderer.sprite = sprite;
            renderer.color = color;
            child.localPosition = Vector3.zero;
            child.localRotation = Quaternion.identity;
            child.localScale = localScale;
            return renderer;
        }

        private static TextMesh EnsureTextMesh(Transform parent, string childName)
        {
            var child = FindOrCreateChild(parent, childName);
            var text = child.GetComponent<TextMesh>();
            if (text == null)
            {
                text = child.gameObject.AddComponent<TextMesh>();
            }
            text.text = "?";
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.characterSize = 0.13f;
            text.fontSize = 32;
            text.color = Color.white;
            child.localPosition = new Vector3(0f, 0.015f, -0.02f);
            child.localRotation = Quaternion.identity;
            child.localScale = Vector3.one;
            return text;
        }

        private static Transform FindOrCreateChild(Transform parent, string childName)
        {
            var child = parent.Find(childName);
            if (child != null)
            {
                return child;
            }

            var childObject = new GameObject(childName);
            child = childObject.transform;
            child.SetParent(parent, false);
            return child;
        }

        private static void EnsureRuntimeFallbackSprites()
        {
            if (runtimeSquareSprite == null)
            {
                var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
                {
                    name = "Ecosystem Spatial White Pixel",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };
                texture.SetPixel(0, 0, Color.white);
                texture.Apply(false, true);
                runtimeSquareSprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, 1f, 1f),
                    new Vector2(0.5f, 0.5f),
                    1f);
                runtimeSquareSprite.name = "Ecosystem Spatial Square (Runtime Fallback)";
                runtimeSquareSprite.hideFlags = HideFlags.HideAndDontSave;
            }

            if (runtimeCircleSprite == null)
            {
                const int size = 64;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    name = "Ecosystem Spatial Circle",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };
                var pixels = new Color32[size * size];
                var center = (size - 1) * 0.5f;
                var radius = size * 0.48f;
                for (var y = 0; y < size; y++)
                {
                    for (var x = 0; x < size; x++)
                    {
                        var distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                        var alpha = (byte)Mathf.RoundToInt(
                            Mathf.Clamp01(radius + 0.75f - distance) * 255f);
                        pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                    }
                }
                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                runtimeCircleSprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, size, size),
                    new Vector2(0.5f, 0.5f),
                    size);
                runtimeCircleSprite.name = "Ecosystem Spatial Circle (Runtime Fallback)";
                runtimeCircleSprite.hideFlags = HideFlags.HideAndDontSave;
            }
        }

#if UNITY_EDITOR
        public void ConfigureEditor(
            Sprite serializedCircleSprite,
            Sprite serializedSquareSprite,
            int authoredSortingOrder = 100)
        {
            authoredCircleSprite = serializedCircleSprite;
            authoredSquareSprite = serializedSquareSprite;
            baseSortingOrder = authoredSortingOrder;
            EnsureVisualHierarchy();
            Release();
        }
#endif
    }
}
