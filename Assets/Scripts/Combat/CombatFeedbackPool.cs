using System.Collections.Generic;
using UnityEngine;

namespace Turtle.Combat
{
    public static class CombatFeedbackPool
    {
        private static readonly Queue<PooledBloodEffect> BloodPool = new();
        private static Material bloodMaterial;

        public static void SpawnBlood(Vector3 position, Vector3 direction)
        {
            var effect = BloodPool.Count > 0 ? BloodPool.Dequeue() : CreateBloodEffect();
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

        internal static void Return(PooledBloodEffect effect)
        {
            effect.gameObject.SetActive(false);
            BloodPool.Enqueue(effect);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            BloodPool.Clear();
            bloodMaterial = null;
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
}
