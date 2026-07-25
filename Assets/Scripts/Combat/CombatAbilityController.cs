using System;
using UnityEngine;

namespace Turtle.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Combatant))]
    public sealed class CombatAbilityController : MonoBehaviour
    {
        [SerializeField] private Combatant owner;
        [SerializeField] private CombatAbilityLoadoutDefinition loadout;

        private readonly float[] readyAt = new float[CombatAbilityLoadoutDefinition.SlotCount];
        private float ultimateCharge;
        private float cooldownDurationMultiplier = 1f;
        private float ultimateGainMultiplier = 1f;
        private float abilityDamageMultiplier = 1f;
        private float currentMana;
        private float barrierHealth;
        private float barrierMaximum;
        private float barrierEndsAt;
        private Color barrierColor;

        public CombatAbilityLoadoutDefinition Loadout => loadout;
        public float CurrentMana => currentMana;
        public float MaximumMana => loadout != null ? loadout.MaximumMana : 0f;
        public float ManaRatio => MaximumMana > 0f
            ? Mathf.Clamp01(currentMana / MaximumMana)
            : 0f;
        public float UltimateCharge => ultimateCharge;
        public float UltimateChargeRequired =>
            loadout != null ? loadout.UltimateChargeRequired : 1f;
        public float UltimateRatio => Mathf.Clamp01(UltimateCharge / UltimateChargeRequired);
        public float BarrierHealth
        {
            get
            {
                ExpireBarrierIfNeeded();
                return barrierHealth;
            }
        }
        public float BarrierRatio
        {
            get
            {
                ExpireBarrierIfNeeded();
                return barrierMaximum > 0f ? barrierHealth / barrierMaximum : 0f;
            }
        }

        public event Action<int, CombatAbilityDefinition> AbilityCommitted;
        public event Action ManaChanged;
        public event Action UltimateChargeChanged;
        public event Action BarrierChanged;

        private void Awake()
        {
            owner ??= GetComponent<Combatant>();
            RebuildPassiveModifiers();
            ResetAbilityState();
        }

        private void Update()
        {
            ExpireBarrierIfNeeded();
            RegenerateMana();
        }

        public CombatAbilityDefinition GetAbility(int slot)
        {
            return loadout != null ? loadout.GetAbility(slot) : null;
        }

        public bool IsReady(int slot)
        {
            if (slot < 0 || slot >= readyAt.Length)
            {
                return false;
            }
            var ability = GetAbility(slot);
            if (ability == null || Time.time < readyAt[slot])
            {
                return false;
            }

            return CanAfford(ability) &&
                   (slot != CombatAbilityLoadoutDefinition.UltimateSlot ||
                    ultimateCharge >= UltimateChargeRequired);
        }

        public bool CanAfford(CombatAbilityDefinition ability)
        {
            return ability != null && currentMana + 0.001f >= ability.ManaCost;
        }

        public float GetCooldownRemaining(int slot)
        {
            if (slot < 0 || slot >= readyAt.Length)
            {
                return 0f;
            }
            return Mathf.Max(0f, readyAt[slot] - Time.time);
        }

        public bool TryPrepare(int slot, out CombatAbilityDefinition ability)
        {
            ability = slot >= 0 && slot < CombatAbilityLoadoutDefinition.SlotCount
                ? GetAbility(slot)
                : null;
            return ability != null && IsReady(slot);
        }

        public bool TryCommit(
            int slot,
            CombatAbilityDefinition expectedAbility,
            Vector3 direction)
        {
            if (!TryPrepare(slot, out var currentAbility) ||
                currentAbility != expectedAbility ||
                owner == null ||
                !owner.IsAlive)
            {
                return false;
            }

            if (slot == CombatAbilityLoadoutDefinition.UltimateSlot)
            {
                ultimateCharge = Mathf.Max(0f, ultimateCharge - UltimateChargeRequired);
                UltimateChargeChanged?.Invoke();
            }
            else
            {
                readyAt[slot] = Time.time +
                                currentAbility.Cooldown * cooldownDurationMultiplier;
            }
            SpendMana(currentAbility.ManaCost);

            var context = new CombatAbilityContext(this, owner, direction);
            currentAbility.Activate(context);
            AbilityCommitted?.Invoke(slot, currentAbility);
            return true;
        }

        public void NotifyDamageDealt(float damage)
        {
            if (loadout == null || damage <= 0f)
            {
                return;
            }
            AddUltimateCharge(
                damage * loadout.UltimateChargePerDamageDealt * ultimateGainMultiplier);
        }

        public void NotifyDamageReceived(float damage)
        {
            if (loadout == null || damage <= 0f)
            {
                return;
            }
            AddUltimateCharge(
                damage * loadout.UltimateChargePerDamageReceived * ultimateGainMultiplier);
        }

        public float ModifyAbilityDamage(float baseDamage)
        {
            return Mathf.Max(0f, baseDamage * abilityDamageMultiplier);
        }

        public void GrantBarrier(float capacity, float duration, Color color)
        {
            barrierMaximum = Mathf.Max(barrierHealth, capacity);
            barrierHealth = Mathf.Max(barrierHealth, capacity);
            barrierEndsAt = Time.time + Mathf.Max(0.1f, duration);
            barrierColor = color;
            CombatFeedbackPool.SpawnMagicPulse(
                owner.transform.position + Vector3.up,
                color,
                0.4f,
                2.8f,
                0.45f);
            BarrierChanged?.Invoke();
        }

        public float AbsorbDamage(float incomingDamage)
        {
            ExpireBarrierIfNeeded();
            if (barrierHealth <= 0f || incomingDamage <= 0f)
            {
                return incomingDamage;
            }

            var absorbed = Mathf.Min(barrierHealth, incomingDamage);
            barrierHealth -= absorbed;
            CombatFeedbackPool.SpawnMagicPulse(
                owner.transform.position + Vector3.up,
                barrierColor,
                1.8f,
                2.5f,
                0.18f);
            if (barrierHealth <= 0f)
            {
                ClearBarrier();
            }
            else
            {
                BarrierChanged?.Invoke();
            }
            return incomingDamage - absorbed;
        }

        public void ResetAbilityState()
        {
            Array.Clear(readyAt, 0, readyAt.Length);
            currentMana = loadout != null ? loadout.StartingMana : 0f;
            ultimateCharge = loadout != null
                ? Mathf.Clamp(
                    loadout.StartingUltimateCharge,
                    0f,
                    loadout.UltimateChargeRequired)
                : 0f;
            ClearBarrier();
            ManaChanged?.Invoke();
            UltimateChargeChanged?.Invoke();
        }

        private void RegenerateMana()
        {
            if (loadout == null ||
                owner == null ||
                !owner.IsAlive ||
                currentMana >= loadout.MaximumMana)
            {
                return;
            }

            var previous = currentMana;
            currentMana = Mathf.Min(
                loadout.MaximumMana,
                currentMana + loadout.ManaRegenerationPerSecond * Time.deltaTime);
            if (!Mathf.Approximately(previous, currentMana))
            {
                ManaChanged?.Invoke();
            }
        }

        private void SpendMana(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            currentMana = Mathf.Max(0f, currentMana - amount);
            ManaChanged?.Invoke();
        }

        private void AddUltimateCharge(float amount)
        {
            var previous = ultimateCharge;
            ultimateCharge = Mathf.Clamp(
                ultimateCharge + amount,
                0f,
                UltimateChargeRequired);
            if (!Mathf.Approximately(previous, ultimateCharge))
            {
                UltimateChargeChanged?.Invoke();
            }
        }

        private void RebuildPassiveModifiers()
        {
            cooldownDurationMultiplier = 1f;
            ultimateGainMultiplier = 1f;
            abilityDamageMultiplier = 1f;
            if (loadout == null || loadout.Passives == null)
            {
                return;
            }

            var passives = loadout.Passives;
            for (var index = 0; index < passives.Length; index++)
            {
                var passive = passives[index];
                if (passive == null)
                {
                    continue;
                }
                cooldownDurationMultiplier *= passive.CooldownDurationMultiplier;
                ultimateGainMultiplier *= passive.UltimateGainMultiplier;
                abilityDamageMultiplier *= passive.AbilityDamageMultiplier;
            }
            cooldownDurationMultiplier = Mathf.Clamp(cooldownDurationMultiplier, 0.1f, 10f);
            ultimateGainMultiplier = Mathf.Clamp(ultimateGainMultiplier, 0f, 10f);
            abilityDamageMultiplier = Mathf.Clamp(abilityDamageMultiplier, 0f, 10f);
        }

        private void ExpireBarrierIfNeeded()
        {
            if (barrierHealth > 0f && Time.time >= barrierEndsAt)
            {
                ClearBarrier();
            }
        }

        private void ClearBarrier()
        {
            var changed = barrierHealth > 0f || barrierMaximum > 0f;
            barrierHealth = 0f;
            barrierMaximum = 0f;
            barrierEndsAt = 0f;
            if (changed)
            {
                BarrierChanged?.Invoke();
            }
        }

#if UNITY_EDITOR
        public void ConfigureEditor(
            Combatant assignedOwner,
            CombatAbilityLoadoutDefinition assignedLoadout)
        {
            owner = assignedOwner;
            loadout = assignedLoadout;
            RebuildPassiveModifiers();
        }
#endif
    }
}
