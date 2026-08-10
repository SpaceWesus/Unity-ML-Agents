using UnityEngine;

namespace Turtle.BattleArena3D
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LineRenderer))]
    public sealed class BattleArena3DTelegraph : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private LineRenderer ringRenderer;
        [SerializeField, Min(0.01f)] private float baseWidth = 0.09f;

        private MaterialPropertyBlock propertyBlock;
        private BattleArena3DVfxPool ownerPool;
        private Color color;
        private float radius;
        private float startedAt;
        private float expiresAt;
        private bool reducedMotion;
        private bool active;

        public bool IsActive => active;

        private void Awake()
        {
            ringRenderer ??= GetComponent<LineRenderer>();
        }

        public void Play(
            BattleArena3DVfxPool pool,
            Vector3 position,
            float targetRadius,
            Color telegraphColor,
            float time,
            float duration,
            bool useReducedMotion)
        {
            ringRenderer ??= GetComponent<LineRenderer>();
            propertyBlock ??= new MaterialPropertyBlock();
            ownerPool = pool;
            color = telegraphColor;
            radius = Mathf.Max(0.35f, targetRadius);
            startedAt = time;
            expiresAt = time + Mathf.Max(0.18f, duration);
            reducedMotion = useReducedMotion;
            transform.SetPositionAndRotation(position + Vector3.up * 0.075f, Quaternion.identity);
            transform.localScale = Vector3.one * radius * 0.18f;
            if (ringRenderer != null)
            {
                ringRenderer.widthMultiplier = baseWidth / Mathf.Max(0.45f, radius);
                ringRenderer.startColor = color;
                ringRenderer.endColor = color;
                ringRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(BaseColorId, color);
                propertyBlock.SetColor(EmissionColorId, color * 3.5f);
                ringRenderer.SetPropertyBlock(propertyBlock);
                ringRenderer.enabled = true;
            }
            active = true;
            gameObject.SetActive(true);
        }

        public void Step(float time)
        {
            if (!active) return;
            var duration = Mathf.Max(0.001f, expiresAt - startedAt);
            var normalized = Mathf.Clamp01((time - startedAt) / duration);
            var easeOut = 1f - Mathf.Pow(1f - normalized, 3f);
            var pulse = reducedMotion ? 1f : 1f + Mathf.Sin(normalized * Mathf.PI) * 0.08f;
            var scale = radius * Mathf.Lerp(0.18f, 1f, easeOut) * pulse;
            transform.localScale = new Vector3(scale, 1f, scale);
            if (time >= expiresAt) Despawn();
        }

        public void ApplyReducedMotion(bool useReducedMotion)
        {
            reducedMotion = useReducedMotion;
        }

        public void Despawn()
        {
            if (!active) return;
            active = false;
            if (ringRenderer != null) ringRenderer.enabled = false;
            gameObject.SetActive(false);
            var pool = ownerPool;
            ownerPool = null;
            pool?.ReturnTelegraph(this);
        }

#if UNITY_EDITOR
        public void ConfigureEditor(LineRenderer assignedRenderer, float width)
        {
            ringRenderer = assignedRenderer;
            baseWidth = Mathf.Max(0.01f, width);
        }
#endif
    }
}
