using System.Collections;
using UnityEngine;

namespace Turtle.Homies
{
    public static class HomiesCombatFeedback
    {
        private static Material bloodMaterial;

        public static void SpawnBloodBurst(Vector3 position, Vector3 impactDirection)
        {
            var effectObject = new GameObject("Blood Impact");
            effectObject.transform.position = position;
            effectObject.transform.rotation = Quaternion.LookRotation(
                impactDirection.sqrMagnitude > 0.001f ? impactDirection : Vector3.forward);

            var particles = effectObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.duration = 0.35f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 6.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.045f, 0.14f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.32f, 0f, 0f),
                new Color(0.78f, 0.015f, 0.01f));
            main.gravityModifier = 1.5f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;

            var emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, 18, 28)
            });

            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 32f;
            shape.radius = 0.08f;

            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 2.2f;
            renderer.velocityScale = 0.12f;
            renderer.material = GetBloodMaterial();
            particles.Play();
        }

        public static ParticleSystem CreateSwordDripSystem(Transform sword)
        {
            var effectObject = new GameObject("Sword Blood Drips");
            effectObject.transform.SetParent(sword, false);
            effectObject.transform.localPosition = Vector3.up * 0.45f;

            var particles = effectObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.65f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.075f);
            main.startColor = new Color(0.55f, 0.01f, 0.01f);
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
            renderer.material = GetBloodMaterial();
            return particles;
        }

        private static Material GetBloodMaterial()
        {
            if (bloodMaterial != null)
            {
                return bloodMaterial;
            }

            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Particles/Standard Unlit");
            }

            bloodMaterial = new Material(shader)
            {
                name = "Runtime Blood Particle Material"
            };
            if (bloodMaterial.HasProperty("_BaseColor"))
            {
                bloodMaterial.SetColor("_BaseColor", new Color(0.55f, 0.005f, 0.005f));
            }

            return bloodMaterial;
        }
    }

    [DisallowMultipleComponent]
    public sealed class HomiesWeaponPresentation : MonoBehaviour
    {
        private Transform sword;
        private Quaternion restRotation;
        private Vector3 restPosition;
        private ParticleSystem bloodDrips;
        private Coroutine swingRoutine;

        public void Initialize()
        {
            if (sword != null)
            {
                return;
            }

            sword = FindSword(transform);
            if (sword == null)
            {
                return;
            }

            restRotation = sword.localRotation;
            restPosition = sword.localPosition;
            bloodDrips = HomiesCombatFeedback.CreateSwordDripSystem(sword);
        }

        public void Swing()
        {
            Initialize();
            if (sword == null)
            {
                return;
            }

            if (swingRoutine != null)
            {
                StopCoroutine(swingRoutine);
                sword.SetLocalPositionAndRotation(restPosition, restRotation);
            }

            swingRoutine = StartCoroutine(SwingRoutine());
        }

        public void EmitBloodDrips()
        {
            Initialize();
            if (bloodDrips == null)
            {
                return;
            }

            bloodDrips.Emit(Random.Range(4, 8));
        }

        private IEnumerator SwingRoutine()
        {
            const float windupDuration = 0.08f;
            const float strikeDuration = 0.13f;
            const float recoveryDuration = 0.16f;
            var windup = restRotation * Quaternion.Euler(-20f, 0f, -75f);
            var strike = restRotation * Quaternion.Euler(28f, 0f, 105f);

            yield return RotateSword(restRotation, windup, windupDuration, Vector3.back * 0.08f);
            yield return RotateSword(windup, strike, strikeDuration, Vector3.forward * 0.12f);
            yield return RotateSword(strike, restRotation, recoveryDuration, Vector3.zero);
            sword.SetLocalPositionAndRotation(restPosition, restRotation);
            swingRoutine = null;
        }

        private IEnumerator RotateSword(
            Quaternion from,
            Quaternion to,
            float duration,
            Vector3 positionOffset)
        {
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var normalizedTime = Mathf.Clamp01(elapsed / duration);
                var eased = 1f - Mathf.Pow(1f - normalizedTime, 3f);
                sword.localRotation = Quaternion.Slerp(from, to, eased);
                sword.localPosition = Vector3.Lerp(
                    sword.localPosition,
                    restPosition + positionOffset,
                    eased);
                yield return null;
            }
        }

        private static Transform FindSword(Transform root)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child != root && child.name == "Sword")
                {
                    return child;
                }
            }

            return null;
        }
    }
}
