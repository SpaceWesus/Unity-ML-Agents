using System;
using System.Collections.Generic;
using UnityEngine;

namespace Turtle.DungeonRaid
{
    [DisallowMultipleComponent]
    public sealed class RaidFxPool2D : MonoBehaviour
    {
        private sealed class SpriteFx
        {
            public GameObject GameObject;
            public SpriteRenderer Renderer;
            public float Start;
            public float End;
            public Vector2 From;
            public Vector2 To;
            public Vector3 StartScale;
            public Vector3 EndScale;
            public Color Color;
            public bool Moving;
        }

        private sealed class TextFx
        {
            public GameObject GameObject;
            public TextMesh Text;
            public float Start;
            public float End;
            public Vector2 Origin;
            public Color Color;
        }

        [SerializeField] private Sprite effectSprite;
        [SerializeField, Range(12, 96)] private int spritePoolSize = 48;
        [SerializeField, Range(6, 48)] private int textPoolSize = 20;
        private readonly List<SpriteFx> spritePool = new();
        private readonly List<TextFx> textPool = new();
        private int nextSprite;
        private int nextText;

        private void Awake()
        {
            EnsurePool();
        }

        private void Update()
        {
            var now = Time.time;
            for (var index = 0; index < spritePool.Count; index++)
            {
                var fx = spritePool[index];
                if (!fx.GameObject.activeSelf) continue;
                var ratio = Mathf.InverseLerp(fx.Start, fx.End, now);
                if (ratio >= 1f)
                {
                    fx.GameObject.SetActive(false);
                    continue;
                }
                var position = fx.Moving ? Vector2.Lerp(fx.From, fx.To, ratio) : fx.From;
                fx.GameObject.transform.position = new Vector3(position.x, position.y, -0.5f);
                fx.GameObject.transform.localScale = Vector3.Lerp(fx.StartScale, fx.EndScale, ratio);
                var color = fx.Color;
                color.a *= 1f - ratio;
                fx.Renderer.color = color;
            }
            for (var index = 0; index < textPool.Count; index++)
            {
                var fx = textPool[index];
                if (!fx.GameObject.activeSelf) continue;
                var ratio = Mathf.InverseLerp(fx.Start, fx.End, now);
                if (ratio >= 1f)
                {
                    fx.GameObject.SetActive(false);
                    continue;
                }
                var position = fx.Origin + Vector2.up * ratio * 1.2f;
                fx.GameObject.transform.position = new Vector3(position.x, position.y, -0.7f);
                var color = fx.Color;
                color.a = 1f - ratio;
                fx.Text.color = color;
            }
        }

        public void EmitBurst(Vector2 position, Color color, float size, float duration)
        {
            var fx = AcquireSprite();
            if (fx == null) return;
            ConfigureSpriteFx(fx, position, position, color,
                Vector3.one * Mathf.Max(0.05f, size * 0.25f),
                Vector3.one * Mathf.Max(0.1f, size), duration, false);
        }

        public void EmitProjectile(Vector2 from, Vector2 to, Color color, float duration)
        {
            var fx = AcquireSprite();
            if (fx == null) return;
            ConfigureSpriteFx(fx, from, to, color,
                new Vector3(0.38f, 0.38f, 1f),
                new Vector3(0.18f, 0.18f, 1f), duration, true);
        }

        public void EmitArc(Vector2 from, Vector2 to, Color color, float width, float duration)
        {
            var fx = AcquireSprite();
            if (fx == null) return;
            var middle = Vector2.Lerp(from, to, 0.5f);
            var distance = Mathf.Max(0.1f, Vector2.Distance(from, to));
            ConfigureSpriteFx(fx, middle, middle, color,
                new Vector3(distance, Mathf.Max(0.1f, width), 1f),
                new Vector3(distance * 1.08f, 0.04f, 1f), duration, false);
            var direction = to - from;
            fx.GameObject.transform.rotation = Quaternion.Euler(
                0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        }

        public void EmitText(Vector2 position, string message, Color color)
        {
            EnsurePool();
            if (textPool.Count == 0) return;
            var fx = textPool[nextText++ % textPool.Count];
            fx.Start = Time.time;
            fx.End = fx.Start + 0.8f;
            fx.Origin = position;
            fx.Color = color;
            fx.Text.text = message;
            fx.Text.color = color;
            fx.GameObject.transform.position = new Vector3(position.x, position.y, -0.7f);
            fx.GameObject.SetActive(true);
        }

        private SpriteFx AcquireSprite()
        {
            EnsurePool();
            if (spritePool.Count == 0) return null;
            return spritePool[nextSprite++ % spritePool.Count];
        }

        private void ConfigureSpriteFx(
            SpriteFx fx,
            Vector2 from,
            Vector2 to,
            Color color,
            Vector3 startScale,
            Vector3 endScale,
            float duration,
            bool moving)
        {
            fx.Start = Time.time;
            fx.End = fx.Start + Mathf.Max(0.05f, duration);
            fx.From = from;
            fx.To = to;
            fx.StartScale = startScale;
            fx.EndScale = endScale;
            fx.Color = color;
            fx.Moving = moving;
            fx.GameObject.transform.position = new Vector3(from.x, from.y, -0.5f);
            fx.GameObject.transform.localScale = startScale;
            fx.GameObject.transform.rotation = Quaternion.identity;
            fx.Renderer.color = color;
            fx.GameObject.SetActive(true);
        }

        private void EnsurePool()
        {
            if (effectSprite == null)
            {
                var renderer = GetComponentInChildren<SpriteRenderer>(true);
                effectSprite = renderer != null ? renderer.sprite : null;
            }
            while (spritePool.Count < spritePoolSize && effectSprite != null)
            {
                var child = new GameObject($"Pooled Raid FX {spritePool.Count + 1:00}");
                child.transform.SetParent(transform, false);
                var renderer = child.AddComponent<SpriteRenderer>();
                renderer.sprite = effectSprite;
                renderer.sortingOrder = 80;
                child.SetActive(false);
                spritePool.Add(new SpriteFx { GameObject = child, Renderer = renderer });
            }
            while (textPool.Count < textPoolSize)
            {
                var child = new GameObject($"Pooled Raid Text {textPool.Count + 1:00}");
                child.transform.SetParent(transform, false);
                var text = child.AddComponent<TextMesh>();
                text.anchor = TextAnchor.MiddleCenter;
                text.alignment = TextAlignment.Center;
                text.characterSize = 0.12f;
                text.fontSize = 42;
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (text.font != null)
                {
                    text.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
                }
                text.GetComponent<MeshRenderer>().sortingOrder = 100;
                child.SetActive(false);
                textPool.Add(new TextFx { GameObject = child, Text = text });
            }
        }

#if UNITY_EDITOR
        public void ConfigureEditor(Sprite sprite)
        {
            effectSprite = sprite;
        }
#endif
    }
}
