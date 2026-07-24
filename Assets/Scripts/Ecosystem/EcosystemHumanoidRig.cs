using UnityEngine;

namespace Turtle.Ecosystem
{
    [DisallowMultipleComponent]
    public sealed class EcosystemHumanoidRig : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private Transform visualRoot;
        private Transform torso;
        private Transform leftShoulder;
        private Transform rightShoulder;
        private Transform leftElbow;
        private Transform rightElbow;
        private Transform leftHip;
        private Transform rightHip;
        private Transform leftKnee;
        private Transform rightKnee;
        private Transform rightHand;
        private Transform weapon;
        private Material bodyMaterial;
        private Material skinMaterial;
        private Material darkMaterial;
        private Material weaponMaterial;
        private float gaitTime;
        private float gaitBlend;
        private float attackStartedAt = float.NegativeInfinity;
        private float attackDuration = 0.5f;
        private GearMoveSet attackMoveSet;
        private bool moving;
        private bool built;

        public void Initialize(Transform existingWeapon = null)
        {
            BuildIfNeeded();
            if (existingWeapon != null)
            {
                AttachWeapon(existingWeapon);
            }
        }

        public void SetBodyColor(Color color)
        {
            BuildIfNeeded();
            SetMaterialColor(bodyMaterial, color);
        }

        public void SetMoving(bool isMoving)
        {
            moving = isMoving;
        }

        public void SetGear(GearMoveSet moveSet, Color accent)
        {
            BuildIfNeeded();
            if (weapon == null)
            {
                AttachWeapon(CreatePart(
                    "Equipped Gear",
                    PrimitiveType.Cube,
                    rightHand,
                    Vector3.zero,
                    Vector3.one,
                    weaponMaterial));
            }

            SetMaterialColor(weaponMaterial, accent);
            weapon.localPosition = new Vector3(0f, -0.2f, 0.18f);
            weapon.localRotation = Quaternion.Euler(-8f, 0f, 0f);
            weapon.localScale = moveSet switch
            {
                GearMoveSet.TitanGreatsword => new Vector3(0.18f, 1.45f, 0.22f),
                GearMoveSet.RiftDaggers => new Vector3(0.12f, 0.58f, 0.16f),
                _ => new Vector3(0.15f, 0.95f, 0.18f)
            };
        }

        public void TriggerAttack(GearMoveSet moveSet)
        {
            BuildIfNeeded();
            attackMoveSet = moveSet;
            attackDuration = moveSet switch
            {
                GearMoveSet.TitanGreatsword => 0.85f,
                GearMoveSet.RiftDaggers => 0.62f,
                _ => 0.48f
            };
            attackStartedAt = Time.time;
        }

        private void LateUpdate()
        {
            if (!built)
            {
                return;
            }

            gaitBlend = Mathf.MoveTowards(
                gaitBlend,
                moving ? 1f : 0f,
                Time.deltaTime * 7f);
            if (moving)
            {
                gaitTime += Time.deltaTime * 9f;
            }

            var gait = Mathf.Sin(gaitTime) * 34f * gaitBlend;
            leftHip.localRotation = Quaternion.Euler(gait, 0f, 0f);
            rightHip.localRotation = Quaternion.Euler(-gait, 0f, 0f);
            leftKnee.localRotation = Quaternion.Euler(Mathf.Max(0f, -gait) * 0.55f, 0f, 0f);
            rightKnee.localRotation = Quaternion.Euler(Mathf.Max(0f, gait) * 0.55f, 0f, 0f);

            var armGait = gait * 0.62f;
            var leftArmRotation = Quaternion.Euler(-armGait, 0f, -5f);
            var rightArmRotation = Quaternion.Euler(armGait, 0f, 5f);
            var torsoRotation = Quaternion.identity;

            var attackTime = (Time.time - attackStartedAt) / attackDuration;
            if (attackTime is >= 0f and <= 1f)
            {
                ApplyAttackPose(
                    Mathf.SmoothStep(0f, 1f, attackTime),
                    ref leftArmRotation,
                    ref rightArmRotation,
                    ref torsoRotation);
            }

            leftShoulder.localRotation = leftArmRotation;
            rightShoulder.localRotation = rightArmRotation;
            leftElbow.localRotation = Quaternion.Euler(8f + gaitBlend * 8f, 0f, 0f);
            rightElbow.localRotation = Quaternion.Euler(12f, 0f, 0f);
            torso.localRotation = torsoRotation;
            visualRoot.localPosition = Vector3.up * (Mathf.Abs(Mathf.Sin(gaitTime)) * 0.035f * gaitBlend);
        }

        private void ApplyAttackPose(
            float normalizedTime,
            ref Quaternion leftArm,
            ref Quaternion rightArm,
            ref Quaternion torsoRotation)
        {
            switch (attackMoveSet)
            {
                case GearMoveSet.TitanGreatsword:
                {
                    var angle = Mathf.Lerp(-125f, 85f, normalizedTime);
                    rightArm = Quaternion.Euler(angle, 0f, 18f);
                    leftArm = Quaternion.Euler(angle + 12f, 0f, -20f);
                    torsoRotation = Quaternion.Euler(0f, Mathf.Lerp(-28f, 34f, normalizedTime), 0f);
                    break;
                }
                case GearMoveSet.RiftDaggers:
                {
                    var strike = Mathf.Sin(normalizedTime * Mathf.PI * 3f);
                    rightArm = Quaternion.Euler(-70f * strike, 0f, 28f);
                    leftArm = Quaternion.Euler(70f * strike, 0f, -28f);
                    torsoRotation = Quaternion.Euler(0f, strike * 18f, 0f);
                    break;
                }
                default:
                {
                    var angle = Mathf.Lerp(-85f, 82f, normalizedTime);
                    rightArm = Quaternion.Euler(angle, 0f, 30f);
                    leftArm = Quaternion.Euler(-18f, 0f, -14f);
                    torsoRotation = Quaternion.Euler(0f, Mathf.Lerp(-20f, 26f, normalizedTime), 0f);
                    break;
                }
            }
        }

        private void BuildIfNeeded()
        {
            if (built)
            {
                return;
            }

            built = true;
            var sourceRenderer = GetComponent<Renderer>();
            var sourceMaterial = sourceRenderer != null
                ? sourceRenderer.sharedMaterial
                : null;
            var shader = sourceMaterial != null
                ? sourceMaterial.shader
                : Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            bodyMaterial = sourceMaterial != null
                ? new Material(sourceMaterial)
                : new Material(shader);
            skinMaterial = new Material(shader);
            darkMaterial = new Material(shader);
            weaponMaterial = new Material(shader);
            bodyMaterial.name = $"{name} Body";
            skinMaterial.name = $"{name} Skin";
            darkMaterial.name = $"{name} Boots";
            weaponMaterial.name = $"{name} Weapon";

            var skinTone = Color.Lerp(
                new Color(0.42f, 0.24f, 0.15f),
                new Color(0.96f, 0.75f, 0.58f),
                Mathf.Abs(name.GetHashCode() % 100) / 100f);
            SetMaterialColor(skinMaterial, skinTone);
            SetMaterialColor(darkMaterial, new Color(0.035f, 0.045f, 0.065f));
            SetMaterialColor(weaponMaterial, new Color(0.25f, 0.55f, 1f));

            if (sourceRenderer != null)
            {
                sourceRenderer.enabled = false;
            }

            visualRoot = new GameObject("Humanoid Visual").transform;
            visualRoot.SetParent(transform, false);

            CreatePart("Hips", PrimitiveType.Cube, visualRoot,
                new Vector3(0f, 0.87f, 0f), new Vector3(0.55f, 0.25f, 0.34f), darkMaterial);

            torso = new GameObject("Torso Pivot").transform;
            torso.SetParent(visualRoot, false);
            torso.localPosition = new Vector3(0f, 1.18f, 0f);
            CreatePart("Torso", PrimitiveType.Cube, torso,
                new Vector3(0f, 0.25f, 0f), new Vector3(0.78f, 0.72f, 0.4f), bodyMaterial);
            CreatePart("Head", PrimitiveType.Sphere, torso,
                new Vector3(0f, 0.93f, 0f), Vector3.one * 0.43f, skinMaterial);

            leftShoulder = CreateLimb(
                "Left Arm", torso, new Vector3(-0.51f, 0.52f, 0f),
                new Vector3(0f, -0.3f, 0f), new Vector3(0.2f, 0.36f, 0.2f),
                bodyMaterial, out leftElbow);
            rightShoulder = CreateLimb(
                "Right Arm", torso, new Vector3(0.51f, 0.52f, 0f),
                new Vector3(0f, -0.3f, 0f), new Vector3(0.2f, 0.36f, 0.2f),
                bodyMaterial, out rightElbow);

            leftHip = CreateLeg("Left Leg", visualRoot, new Vector3(-0.2f, 0.78f, 0f), out leftKnee);
            rightHip = CreateLeg("Right Leg", visualRoot, new Vector3(0.2f, 0.78f, 0f), out rightKnee);

            rightHand = new GameObject("Right Hand").transform;
            rightHand.SetParent(rightElbow, false);
            rightHand.localPosition = new Vector3(0f, -0.62f, 0f);
        }

        private Transform CreateLimb(
            string limbName,
            Transform parent,
            Vector3 pivotPosition,
            Vector3 meshPosition,
            Vector3 meshScale,
            Material material,
            out Transform lowerJoint)
        {
            var pivot = new GameObject($"{limbName} Shoulder").transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = pivotPosition;
            CreatePart($"{limbName} Upper", PrimitiveType.Capsule, pivot,
                meshPosition, meshScale, material);

            lowerJoint = new GameObject($"{limbName} Elbow").transform;
            lowerJoint.SetParent(pivot, false);
            lowerJoint.localPosition = new Vector3(0f, -0.62f, 0f);
            CreatePart($"{limbName} Lower", PrimitiveType.Capsule, lowerJoint,
                new Vector3(0f, -0.28f, 0f), new Vector3(0.17f, 0.32f, 0.17f), skinMaterial);
            return pivot;
        }

        private Transform CreateLeg(
            string legName,
            Transform parent,
            Vector3 pivotPosition,
            out Transform knee)
        {
            var hip = new GameObject($"{legName} Hip").transform;
            hip.SetParent(parent, false);
            hip.localPosition = pivotPosition;
            CreatePart($"{legName} Thigh", PrimitiveType.Capsule, hip,
                new Vector3(0f, -0.32f, 0f), new Vector3(0.22f, 0.38f, 0.24f), bodyMaterial);

            knee = new GameObject($"{legName} Knee").transform;
            knee.SetParent(hip, false);
            knee.localPosition = new Vector3(0f, -0.66f, 0f);
            CreatePart($"{legName} Shin", PrimitiveType.Capsule, knee,
                new Vector3(0f, -0.31f, 0f), new Vector3(0.19f, 0.36f, 0.21f), darkMaterial);
            CreatePart($"{legName} Boot", PrimitiveType.Cube, knee,
                new Vector3(0f, -0.67f, 0.12f), new Vector3(0.3f, 0.18f, 0.5f), darkMaterial);
            return hip;
        }

        private static Transform CreatePart(
            string partName,
            PrimitiveType primitiveType,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Material material)
        {
            var part = GameObject.CreatePrimitive(primitiveType);
            part.name = partName;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            var collider = part.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part.transform;
        }

        private void AttachWeapon(Transform weaponTransform)
        {
            weapon = weaponTransform;
            weapon.SetParent(rightHand, false);
            var renderer = weapon.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = weaponMaterial;
            }
        }

        private static void SetMaterialColor(Material material, Color color)
        {
            if (material.HasProperty(BaseColorId))
            {
                material.SetColor(BaseColorId, color);
            }
            else if (material.HasProperty(ColorId))
            {
                material.SetColor(ColorId, color);
            }
        }

        private void OnDestroy()
        {
            Destroy(bodyMaterial);
            Destroy(skinMaterial);
            Destroy(darkMaterial);
            Destroy(weaponMaterial);
        }
    }
}
