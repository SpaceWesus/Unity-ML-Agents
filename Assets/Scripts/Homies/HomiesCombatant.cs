using System;
using System.Collections;
using UnityEngine;

namespace Turtle.Homies
{
    public enum HomiesFaction
    {
        Player,
        Enemy,
        Shadow
    }

    [DisallowMultipleComponent]
    public sealed class HomiesCombatant : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int Color = Shader.PropertyToID("_Color");

        private Renderer[] renderers = Array.Empty<Renderer>();
        private Color[] normalColors = Array.Empty<Color>();
        private Coroutine flashRoutine;
        private Coroutine knockbackRoutine;
        private HomiesWeaponPresentation weaponPresentation;

        public HomiesFaction Faction { get; private set; }
        public float CurrentHealth { get; private set; }
        public float MaxHealth { get; private set; }
        public float AttackDamage { get; private set; }
        public bool IsAlive { get; private set; }
        public bool IsBoss { get; private set; }
        public float HealthRatio => MaxHealth <= 0f ? 0f : CurrentHealth / MaxHealth;

        public event Action<HomiesCombatant, HomiesCombatant> Defeated;
        public event Action<HomiesCombatant, HomiesCombatant> Damaged;

        public void Configure(
            HomiesFaction faction,
            float maxHealth,
            float attackDamage,
            Color tint,
            bool isBoss = false)
        {
            Faction = faction;
            MaxHealth = Mathf.Max(1f, maxHealth);
            CurrentHealth = MaxHealth;
            AttackDamage = Mathf.Max(0f, attackDamage);
            IsAlive = true;
            IsBoss = isBoss;
            CacheRenderers();
            weaponPresentation = GetComponent<HomiesWeaponPresentation>();
            if (weaponPresentation == null)
            {
                weaponPresentation = gameObject.AddComponent<HomiesWeaponPresentation>();
            }

            weaponPresentation.Initialize();
            SetTint(tint);
            transform.localScale = isBoss ? Vector3.one * 1.45f : Vector3.one;
        }

        public void RestoreAsShadow(float maxHealth, float attackDamage)
        {
            Faction = HomiesFaction.Shadow;
            MaxHealth = Mathf.Max(1f, maxHealth);
            CurrentHealth = MaxHealth;
            AttackDamage = Mathf.Max(0f, attackDamage);
            IsAlive = true;
            IsBoss = false;
            gameObject.SetActive(true);
            SetCollidersEnabled(true);
            SetTint(new Color(0.14f, 0.05f, 0.34f));
            transform.localScale = Vector3.one;
        }

        public void TakeDamage(
            float amount,
            HomiesCombatant source,
            Vector3 impactDirection,
            float knockbackDistance = 1.15f)
        {
            if (!IsAlive || amount <= 0f || (source != null && source.Faction == Faction))
            {
                return;
            }

            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            Flash(new Color(0.9f, 0.15f, 1f));
            HomiesCombatFeedback.SpawnBloodBurst(
                transform.position + Vector3.up * 0.45f,
                impactDirection);
            if (source != null)
            {
                source.MarkWeaponBloodied();
            }

            if (knockbackRoutine != null)
            {
                StopCoroutine(knockbackRoutine);
            }

            knockbackRoutine = StartCoroutine(KnockbackRoutine(impactDirection, knockbackDistance));
            Damaged?.Invoke(this, source);
            if (CurrentHealth <= 0f)
            {
                IsAlive = false;
                SetCollidersEnabled(false);
                Defeated?.Invoke(this, source);
            }
        }

        public void PlayAttack()
        {
            weaponPresentation?.Swing();
        }

        public void MarkWeaponBloodied()
        {
            weaponPresentation?.EmitBloodDrips();
        }

        public void Heal(float amount)
        {
            if (IsAlive)
            {
                CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + Mathf.Max(0f, amount));
            }
        }

        public void SetCorpseAppearance()
        {
            SetTint(new Color(0.08f, 0.08f, 0.12f));
            transform.rotation = Quaternion.Euler(75f, transform.eulerAngles.y, 0f);
        }

        public void SetTint(Color tint)
        {
            ApplyTint(tint, true);
        }

        private void ApplyTint(Color tint, bool remember)
        {
            CacheRenderers();
            for (var index = 0; index < renderers.Length; index++)
            {
                var material = renderers[index].material;
                if (material.HasProperty(BaseColor))
                {
                    material.SetColor(BaseColor, tint);
                }
                else if (material.HasProperty(Color))
                {
                    material.SetColor(Color, tint);
                }

                if (remember)
                {
                    normalColors[index] = tint;
                }
            }
        }

        private void CacheRenderers()
        {
            if (renderers.Length > 0)
            {
                return;
            }

            renderers = GetComponentsInChildren<Renderer>(true);
            normalColors = new Color[renderers.Length];
        }

        private void SetCollidersEnabled(bool enabled)
        {
            foreach (var attachedCollider in GetComponentsInChildren<Collider>(true))
            {
                attachedCollider.enabled = enabled;
            }
        }

        private void Flash(Color color)
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            if (flashRoutine != null)
            {
                StopCoroutine(flashRoutine);
            }

            flashRoutine = StartCoroutine(FlashRoutine(color));
        }

        private IEnumerator FlashRoutine(Color color)
        {
            ApplyTint(color, false);
            yield return new WaitForSeconds(0.08f);
            for (var index = 0; index < renderers.Length; index++)
            {
                var material = renderers[index].material;
                if (material.HasProperty(BaseColor))
                {
                    material.SetColor(BaseColor, normalColors[index]);
                }
                else if (material.HasProperty(Color))
                {
                    material.SetColor(Color, normalColors[index]);
                }
            }

            flashRoutine = null;
        }

        private IEnumerator KnockbackRoutine(Vector3 direction, float distance)
        {
            direction = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
            if (direction.sqrMagnitude <= 0.001f)
            {
                yield break;
            }

            var characterController = GetComponent<CharacterController>();
            var elapsed = 0f;
            const float duration = 0.16f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var normalizedTime = Mathf.Clamp01(elapsed / duration);
                var speed = distance * (1f - normalizedTime) * 2f / duration;
                var displacement = direction * (speed * Time.deltaTime);
                if (characterController != null && characterController.enabled)
                {
                    characterController.Move(displacement);
                }
                else
                {
                    transform.position += displacement;
                }

                yield return null;
            }

            knockbackRoutine = null;
        }
    }
}
