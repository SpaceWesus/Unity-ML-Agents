using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Turtle.Ecosystem
{
    [DisallowMultipleComponent]
    public sealed class EcosystemHunterView : MonoBehaviour
    {
        private EcosystemHumanoidRig humanoid;
        private TextMesh label;
        private Vector3 destination;
        private float nextAttackAt;
        public HunterProfile Profile { get; private set; }

        public void Initialize(HunterProfile profile)
        {
            Profile = profile;
            transform.localScale = new Vector3(0.8f, 1f, 0.8f);
            humanoid = gameObject.AddComponent<EcosystemHumanoidRig>();
            humanoid.Initialize();
            nextAttackAt = Time.time + 0.8f + Mathf.Abs(profile.id.GetHashCode() % 12) * 0.1f;

            var labelObject = new GameObject("Identity");
            labelObject.transform.SetParent(transform, false);
            labelObject.transform.localPosition = Vector3.up * 2.35f;
            label = labelObject.AddComponent<TextMesh>();
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 30;
            label.characterSize = 0.07f;
            label.color = Color.white;
            label.text = profile.displayName;
        }

        public void Refresh(Vector3 newDestination, Color color)
        {
            destination = newDestination;
            label.text = $"{Profile.displayName}\nLv {Profile.level}";
            humanoid.SetBodyColor(color);
            var moveSet = MoveSetFor(Profile.equippedGearId);
            humanoid.SetGear(moveSet, AccentFor(moveSet));
        }

        private void Update()
        {
            var offset = Vector3.ProjectOnPlane(destination - transform.position, Vector3.up);
            var isMoving = offset.magnitude > 0.12f;
            humanoid.SetMoving(isMoving);
            if (isMoving)
            {
                transform.position += offset.normalized * (3.2f * Time.deltaTime);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(offset),
                    8f * Time.deltaTime);
            }
            else if (Profile.currentActivity.StartsWith("Attempting") &&
                     Time.time >= nextAttackAt)
            {
                humanoid.TriggerAttack(MoveSetFor(Profile.equippedGearId));
                nextAttackAt = Time.time + 1.4f + Mathf.Abs(Profile.id.GetHashCode() % 8) * 0.12f;
            }

            if (Camera.main != null)
            {
                label.transform.rotation = Quaternion.LookRotation(
                    label.transform.position - Camera.main.transform.position);
            }
        }

        private static GearMoveSet MoveSetFor(string gearId)
        {
            if (gearId != null && gearId.Contains("titan"))
            {
                return GearMoveSet.TitanGreatsword;
            }
            if (gearId != null && gearId.Contains("rift"))
            {
                return GearMoveSet.RiftDaggers;
            }
            return GearMoveSet.VanguardBlade;
        }

        private static Color AccentFor(GearMoveSet moveSet)
        {
            return moveSet switch
            {
                GearMoveSet.TitanGreatsword => new Color(1f, 0.25f, 0.08f),
                GearMoveSet.RiftDaggers => new Color(0.68f, 0.12f, 1f),
                _ => new Color(0.12f, 0.55f, 1f)
            };
        }
    }

    [DisallowMultipleComponent]
    public sealed class EcosystemCameraRig : MonoBehaviour
    {
        [SerializeField] private Transform target;
        private float yaw = 205f;
        private float pitch = 28f;

#if UNITY_EDITOR
        public void ConfigureEditor(Transform followTarget)
        {
            target = followTarget;
        }
#endif

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            var mouse = Mouse.current;
            if (mouse != null)
            {
                var delta = mouse.delta.ReadValue();
                yaw += delta.x * 0.12f;
                pitch = Mathf.Clamp(pitch - delta.y * 0.09f, 12f, 55f);
            }

            var rotation = Quaternion.Euler(pitch, yaw, 0f);
            var focus = target.position + Vector3.up * 1.2f;
            var desired = focus - rotation * Vector3.forward * 9.5f;
            transform.position = Vector3.Lerp(transform.position, desired, 12f * Time.deltaTime);
            transform.rotation = Quaternion.LookRotation(focus - transform.position);
        }
    }

    [DisallowMultipleComponent]
    public sealed class EcosystemPlayerController : MonoBehaviour
    {
        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private Transform weapon;

        private CharacterController characterController;
        private EcosystemHumanoidRig humanoid;
        private EcosystemGearDefinition equippedGear;
        private Quaternion weaponRestRotation;
        private float attackReadyAt;
        private float verticalVelocity;
        private Coroutine attackRoutine;

#if UNITY_EDITOR
        public void ConfigureEditor(Camera cameraReference, Transform weaponTransform)
        {
            gameplayCamera = cameraReference;
            weapon = weaponTransform;
        }
#endif

        public void Equip(EcosystemGearDefinition gear)
        {
            equippedGear = gear;
            humanoid?.SetGear(gear.MoveSet, gear.Accent);
            if (weapon != null)
            {
                weapon.localScale = gear.MoveSet switch
                {
                    GearMoveSet.TitanGreatsword => new Vector3(0.3f, 1.8f, 0.18f),
                    GearMoveSet.RiftDaggers => new Vector3(0.15f, 0.72f, 0.12f),
                    _ => new Vector3(0.2f, 1.15f, 0.14f)
                };
            }
        }

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            humanoid = GetComponent<EcosystemHumanoidRig>() ??
                       gameObject.AddComponent<EcosystemHumanoidRig>();
            humanoid.Initialize(weapon);
            if (equippedGear != null)
            {
                humanoid.SetGear(equippedGear.MoveSet, equippedGear.Accent);
            }
            weaponRestRotation = weapon.localRotation;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            Move();
            if (Mouse.current != null &&
                Mouse.current.leftButton.wasPressedThisFrame &&
                Time.time >= attackReadyAt)
            {
                Attack();
            }
        }

        private void Move()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            var input = Vector2.zero;
            if (keyboard.wKey.isPressed) input.y += 1f;
            if (keyboard.sKey.isPressed) input.y -= 1f;
            if (keyboard.dKey.isPressed) input.x += 1f;
            if (keyboard.aKey.isPressed) input.x -= 1f;
            input = Vector2.ClampMagnitude(input, 1f);

            var forward = Vector3.ProjectOnPlane(gameplayCamera.transform.forward, Vector3.up).normalized;
            var right = Vector3.ProjectOnPlane(gameplayCamera.transform.right, Vector3.up).normalized;
            var direction = forward * input.y + right * input.x;
            if (direction.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(direction),
                    14f * Time.deltaTime);
            }
            humanoid.SetMoving(direction.sqrMagnitude > 0.01f);
            verticalVelocity = characterController.isGrounded
                ? -1f
                : verticalVelocity + Physics.gravity.y * Time.deltaTime;
            characterController.Move((direction * 6.5f + Vector3.up * verticalVelocity) * Time.deltaTime);
        }

        private void Attack()
        {
            if (equippedGear == null)
            {
                return;
            }

            var aim = Vector3.ProjectOnPlane(gameplayCamera.transform.forward, Vector3.up).normalized;
            transform.rotation = Quaternion.LookRotation(aim);
            humanoid.TriggerAttack(equippedGear.MoveSet);
            if (attackRoutine != null)
            {
                StopCoroutine(attackRoutine);
                weapon.localRotation = weaponRestRotation;
            }

            switch (equippedGear.MoveSet)
            {
                case GearMoveSet.TitanGreatsword:
                    attackReadyAt = Time.time + 1.05f;
                    attackRoutine = StartCoroutine(SwingRoutine(-100f, 120f, 0.42f, 3.2f, 28f));
                    break;
                case GearMoveSet.RiftDaggers:
                    attackReadyAt = Time.time + 0.7f;
                    attackRoutine = StartCoroutine(DaggerFlurry(aim));
                    break;
                default:
                    attackReadyAt = Time.time + 0.52f;
                    attackRoutine = StartCoroutine(SwingRoutine(-70f, 95f, 0.28f, 2.4f, 20f));
                    break;
            }
        }

        private IEnumerator DaggerFlurry(Vector3 aim)
        {
            for (var strike = 0; strike < 3; strike++)
            {
                yield return SwingRoutine(-45f, 60f, 0.13f, 2.1f, 12f);
                characterController.Move(aim * 0.35f);
            }
            attackRoutine = null;
        }

        private IEnumerator SwingRoutine(
            float startAngle,
            float endAngle,
            float duration,
            float range,
            float force)
        {
            var start = weaponRestRotation * Quaternion.Euler(0f, 0f, startAngle);
            var end = weaponRestRotation * Quaternion.Euler(0f, 0f, endAngle);
            var elapsed = 0f;
            var hit = false;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var normalizedTime = Mathf.Clamp01(elapsed / duration);
                weapon.localRotation = Quaternion.Slerp(start, end, normalizedTime);
                if (!hit && normalizedTime >= 0.4f)
                {
                    hit = true;
                    HitTrainingTargets(range, force);
                }
                yield return null;
            }
            weapon.localRotation = weaponRestRotation;
            attackRoutine = null;
        }

        private void HitTrainingTargets(float range, float force)
        {
            var center = transform.position + transform.forward * (range * 0.55f);
            foreach (var hit in Physics.OverlapSphere(center, range * 0.55f))
            {
                if (hit.TryGetComponent<EcosystemTrainingTarget>(out var target))
                {
                    target.Hit(transform.forward, equippedGear.Accent, force);
                }
            }
        }

        private void OnDisable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    public sealed class EcosystemTrainingTarget : MonoBehaviour
    {
        private Renderer cachedRenderer;
        private Color baseColor;
        private Vector3 origin;

        private void Awake()
        {
            cachedRenderer = GetComponent<Renderer>();
            origin = transform.position;
            var material = cachedRenderer.material;
            baseColor = material.HasProperty("_BaseColor")
                ? material.GetColor("_BaseColor")
                : Color.gray;
        }

        public void Hit(Vector3 direction, Color flashColor, float force)
        {
            StopAllCoroutines();
            StartCoroutine(HitRoutine(direction, flashColor, force));
        }

        private IEnumerator HitRoutine(Vector3 direction, Color flashColor, float force)
        {
            var material = cachedRenderer.material;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", flashColor);
            }
            transform.position += direction * (force * 0.025f);
            transform.localScale = new Vector3(1.2f, 0.75f, 1.2f);
            yield return new WaitForSeconds(0.12f);
            transform.localScale = Vector3.one;
            transform.position = Vector3.Lerp(transform.position, origin, 0.45f);
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", baseColor);
            }
        }
    }
}
