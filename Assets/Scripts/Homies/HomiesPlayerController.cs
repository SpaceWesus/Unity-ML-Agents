using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Turtle.Homies
{
    [DisallowMultipleComponent]
    public sealed class HomiesPlayerController : MonoBehaviour
    {
        private CharacterController characterController;
        private HomiesCombatant combatant;
        private HomiesArenaDirector director;
        private Camera gameplayCamera;
        private Transform cameraTransform;
        private float verticalVelocity;
        private float attackReadyAt;
        private float dashReadyAt;
        private bool isDashing;

        public float MoveSpeed { get; private set; } = 7f;
        public float AttackRange { get; private set; } = 2.4f;
        public int Level { get; private set; } = 1;
        public int Experience { get; private set; }
        public int ExperienceToNextLevel => 35 + Level * 25;
        public HomiesCombatant Combatant => combatant;

        public void Initialize(HomiesArenaDirector arenaDirector, Camera gameplayCamera)
        {
            director = arenaDirector;
            this.gameplayCamera = gameplayCamera;
            cameraTransform = gameplayCamera.transform;
            combatant = GetComponent<HomiesCombatant>();

            foreach (var attachedCollider in GetComponentsInChildren<Collider>(true))
            {
                attachedCollider.enabled = false;
            }

            characterController = gameObject.AddComponent<CharacterController>();
            characterController.radius = 0.48f;
            characterController.height = 2f;
            characterController.center = Vector3.zero;
            characterController.skinWidth = 0.05f;
            LockCursor();
        }

        public void AddExperience(int amount)
        {
            Experience += Mathf.Max(0, amount);
            while (Experience >= ExperienceToNextLevel)
            {
                Experience -= ExperienceToNextLevel;
                Level++;
                MoveSpeed += 0.2f;
                combatant.Configure(
                    HomiesFaction.Player,
                    combatant.MaxHealth + 20f,
                    combatant.AttackDamage + 5f,
                    new Color(0.08f, 0.38f, 0.95f));
                director.ShowMessage($"LEVEL UP  //  HUNTER LEVEL {Level}", 2.5f);
            }
        }

        private void Update()
        {
            if (combatant == null || !combatant.IsAlive)
            {
                return;
            }

            if (Cursor.lockState != CursorLockMode.Locked &&
                Mouse.current != null &&
                Mouse.current.leftButton.wasPressedThisFrame)
            {
                LockCursor();
            }

            ReadMovement();
            ReadCombat();
        }

        private void ReadMovement()
        {
            if (isDashing || Keyboard.current == null)
            {
                return;
            }

            var input = Vector2.zero;
            if (Keyboard.current.wKey.isPressed) input.y += 1f;
            if (Keyboard.current.sKey.isPressed) input.y -= 1f;
            if (Keyboard.current.dKey.isPressed) input.x += 1f;
            if (Keyboard.current.aKey.isPressed) input.x -= 1f;
            input = Vector2.ClampMagnitude(input, 1f);

            var forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
            var right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
            var motion = forward * input.y + right * input.x;
            if (motion.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(motion),
                    16f * Time.deltaTime);
            }

            verticalVelocity = characterController.isGrounded
                ? -1f
                : verticalVelocity + Physics.gravity.y * Time.deltaTime;
            characterController.Move((motion * MoveSpeed + Vector3.up * verticalVelocity) * Time.deltaTime);
        }

        private void ReadCombat()
        {
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;

            if (mouse != null && mouse.leftButton.wasPressedThisFrame && Time.time >= attackReadyAt)
            {
                Attack();
            }

            if (keyboard == null)
            {
                return;
            }

            if ((keyboard.spaceKey.wasPressedThisFrame || keyboard.leftShiftKey.wasPressedThisFrame) &&
                Time.time >= dashReadyAt)
            {
                StartCoroutine(DashRoutine());
            }

            if (keyboard.eKey.wasPressedThisFrame)
            {
                director.TryArise(gameplayCamera);
            }

            if (keyboard.rKey.wasPressedThisFrame)
            {
                director.CommandShadows(gameplayCamera);
            }
        }

        private void Attack()
        {
            attackReadyAt = Time.time + 0.38f;
            var direction = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
            if (direction.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(direction);
            }

            combatant.PlayAttack();
            StartCoroutine(AttackLunge(direction));
            var target = director.FindAimedEnemy(
                gameplayCamera,
                transform.position,
                AttackRange,
                38f);
            if (target != null)
            {
                var impactDirection = Vector3.ProjectOnPlane(
                    target.transform.position - transform.position,
                    Vector3.up).normalized;
                target.TakeDamage(combatant.AttackDamage, combatant, impactDirection, 1.6f);
            }
        }

        private IEnumerator AttackLunge(Vector3 direction)
        {
            var elapsed = 0f;
            while (elapsed < 0.12f)
            {
                elapsed += Time.deltaTime;
                characterController.Move(direction * (4.5f * Time.deltaTime));
                yield return null;
            }
        }

        private IEnumerator DashRoutine()
        {
            dashReadyAt = Time.time + 1.2f;
            isDashing = true;
            var direction = transform.forward;
            var elapsed = 0f;
            while (elapsed < 0.18f)
            {
                elapsed += Time.deltaTime;
                characterController.Move(direction * (22f * Time.deltaTime));
                yield return null;
            }

            isDashing = false;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
            {
                LockCursor();
            }
        }

        private void OnDisable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private static void LockCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}
