using System.Collections.Generic;
using UnityEngine;

namespace Turtle.Combat
{
    public static class CombatFeedbackPool
    {
        private static readonly Queue<PooledBloodEffect> BloodPool = new();
        private static readonly Queue<PooledHayEffect> HayPool = new();
        private static readonly Queue<PooledMagicPulse> MagicPulsePool = new();
        private static Material bloodMaterial;
        private static Material hayMaterial;
        private static Material magicMaterial;

        internal static Material MagicMaterial => GetMagicMaterial();

        public static void SpawnBlood(Vector3 position, Vector3 direction)
        {
            var effect = BloodPool.Count > 0 ? BloodPool.Dequeue() : CreateBloodEffect();
            effect.Play(position, direction);
        }

        public static void SpawnHay(Vector3 position, Vector3 direction)
        {
            var effect = HayPool.Count > 0 ? HayPool.Dequeue() : CreateHayEffect();
            effect.Play(position, direction);
        }

        public static ParticleSystem CreateWeaponDrips(Transform weapon)
        {
            var objectRoot = new GameObject("Weapon Blood Drips");
            objectRoot.transform.SetParent(weapon, false);
            objectRoot.transform.localPosition = Vector3.up * 0.55f;
            var particles = objectRoot.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.22f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.075f);
            main.startColor = new Color(0.5f, 0.005f, 0.005f);
            main.gravityModifier = 1.8f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = particles.emission;
            emission.enabled = false;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.025f;
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 1.8f;
            renderer.sharedMaterial = GetBloodMaterial();
            return particles;
        }

        public static void SpawnMagicPulse(
            Vector3 position,
            Color color,
            float startScale,
            float endScale,
            float duration)
        {
            var pulse = MagicPulsePool.Count > 0
                ? MagicPulsePool.Dequeue()
                : CreateMagicPulse();
            pulse.Play(position, color, startScale, endScale, duration);
        }

        internal static void Return(PooledBloodEffect effect)
        {
            effect.gameObject.SetActive(false);
            BloodPool.Enqueue(effect);
        }

        internal static void Return(PooledHayEffect effect)
        {
            effect.gameObject.SetActive(false);
            HayPool.Enqueue(effect);
        }

        internal static void Return(PooledMagicPulse pulse)
        {
            pulse.gameObject.SetActive(false);
            MagicPulsePool.Enqueue(pulse);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            BloodPool.Clear();
            HayPool.Clear();
            MagicPulsePool.Clear();
            bloodMaterial = null;
            hayMaterial = null;
            magicMaterial = null;
        }

        private static PooledBloodEffect CreateBloodEffect()
        {
            var effectRoot = new GameObject("Pooled Blood Impact");
            var particles = effectRoot.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.duration = 0.3f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.65f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.8f, 7f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.13f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.28f, 0f, 0f),
                new Color(0.78f, 0.01f, 0.005f));
            main.gravityModifier = 1.65f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 22, 34) });
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 34f;
            shape.radius = 0.08f;
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.1f;
            renderer.lengthScale = 2.4f;
            renderer.sharedMaterial = GetBloodMaterial();

            var effect = effectRoot.AddComponent<PooledBloodEffect>();
            effect.Initialize(particles);
            effectRoot.SetActive(false);
            return effect;
        }

        private static PooledHayEffect CreateHayEffect()
        {
            var effectRoot = new GameObject("Pooled Target Dummy Hay");
            var particles = effectRoot.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.duration = 0.3f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.85f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.8f, 5.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.11f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.55f, 0.29f, 0.055f),
                new Color(1f, 0.78f, 0.2f));
            main.gravityModifier = 1.15f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 18, 28) });
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 48f;
            shape.radius = 0.12f;
            var noise = particles.noise;
            noise.enabled = true;
            noise.strength = 0.28f;
            noise.frequency = 0.8f;
            noise.scrollSpeed = 0.25f;
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.06f;
            renderer.lengthScale = 3.8f;
            renderer.sharedMaterial = GetHayMaterial();

            var effect = effectRoot.AddComponent<PooledHayEffect>();
            effect.Initialize(particles);
            effectRoot.SetActive(false);
            return effect;
        }

        private static Material GetBloodMaterial()
        {
            if (bloodMaterial != null)
            {
                return bloodMaterial;
            }

            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                         Shader.Find("Particles/Standard Unlit");
            bloodMaterial = new Material(shader)
            {
                name = "Combat Blood Particle Material",
                hideFlags = HideFlags.DontSave
            };
            if (bloodMaterial.HasProperty("_BaseColor"))
            {
                bloodMaterial.SetColor("_BaseColor", new Color(0.52f, 0.005f, 0.005f));
            }
            return bloodMaterial;
        }

        private static Material GetHayMaterial()
        {
            if (hayMaterial != null)
            {
                return hayMaterial;
            }

            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                         Shader.Find("Particles/Standard Unlit");
            hayMaterial = new Material(shader)
            {
                name = "Combat Hay Particle Material",
                hideFlags = HideFlags.DontSave
            };
            if (hayMaterial.HasProperty("_BaseColor"))
            {
                hayMaterial.SetColor("_BaseColor", new Color(0.9f, 0.58f, 0.12f));
            }
            return hayMaterial;
        }

        private static PooledMagicPulse CreateMagicPulse()
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            root.name = "Pooled Magic Pulse";
            var collider = root.GetComponent<Collider>();
            collider.enabled = false;
            Object.Destroy(collider);
            var renderer = root.GetComponent<Renderer>();
            renderer.sharedMaterial = GetMagicMaterial();
            var pulse = root.AddComponent<PooledMagicPulse>();
            pulse.Initialize(renderer);
            root.SetActive(false);
            return pulse;
        }

        private static Material GetMagicMaterial()
        {
            if (magicMaterial != null)
            {
                return magicMaterial;
            }

            var shader = Shader.Find("Universal Render Pipeline/Unlit") ??
                         Shader.Find("Unlit/Color");
            magicMaterial = new Material(shader)
            {
                name = "Combat Magic Material",
                hideFlags = HideFlags.DontSave
            };
            if (magicMaterial.HasProperty("_BaseColor"))
            {
                magicMaterial.SetColor("_BaseColor", Color.white);
            }
            return magicMaterial;
        }
    }

    [DisallowMultipleComponent]
    public sealed class PooledBloodEffect : MonoBehaviour
    {
        private ParticleSystem particles;
        private float returnAt;

        public void Initialize(ParticleSystem system)
        {
            particles = system;
        }

        public void Play(Vector3 position, Vector3 direction)
        {
            gameObject.SetActive(true);
            transform.position = position;
            transform.rotation = Quaternion.LookRotation(
                direction.sqrMagnitude > 0.001f ? direction : Vector3.forward);
            particles.Play(true);
            returnAt = Time.time + 1.25f;
        }

        private void Update()
        {
            if (Time.time >= returnAt && !particles.IsAlive(true))
            {
                CombatFeedbackPool.Return(this);
            }
        }
    }

    [DisallowMultipleComponent]
    public sealed class PooledHayEffect : MonoBehaviour
    {
        private ParticleSystem particles;
        private float returnAt;

        public void Initialize(ParticleSystem system)
        {
            particles = system;
        }

        public void Play(Vector3 position, Vector3 direction)
        {
            gameObject.SetActive(true);
            transform.position = position;
            transform.rotation = Quaternion.LookRotation(
                direction.sqrMagnitude > 0.001f ? direction : Vector3.forward);
            particles.Play(true);
            returnAt = Time.time + 1.5f;
        }

        private void Update()
        {
            if (Time.time >= returnAt && !particles.IsAlive(true))
            {
                CombatFeedbackPool.Return(this);
            }
        }
    }

    [DisallowMultipleComponent]
    public sealed class PooledMagicPulse : MonoBehaviour
    {
        private Renderer pulseRenderer;
        private MaterialPropertyBlock propertyBlock;
        private float startedAt;
        private float duration;
        private float startScale;
        private float endScale;

        public void Initialize(Renderer assignedRenderer)
        {
            pulseRenderer = assignedRenderer;
            propertyBlock = new MaterialPropertyBlock();
        }

        public void Play(
            Vector3 position,
            Color color,
            float fromScale,
            float toScale,
            float pulseDuration)
        {
            gameObject.SetActive(true);
            transform.position = position;
            startedAt = Time.time;
            duration = Mathf.Max(0.05f, pulseDuration);
            startScale = Mathf.Max(0.01f, fromScale);
            endScale = Mathf.Max(0.01f, toScale);
            transform.localScale = Vector3.one * startScale;
            pulseRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_BaseColor", color);
            propertyBlock.SetColor("_EmissionColor", color * 3f);
            pulseRenderer.SetPropertyBlock(propertyBlock);
        }

        private void Update()
        {
            var progress = Mathf.Clamp01((Time.time - startedAt) / duration);
            transform.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, progress);
            if (progress >= 1f)
            {
                CombatFeedbackPool.Return(this);
            }
        }
    }
}
