using System;
using UnityEngine;

namespace OriginCore.Combat
{
    public readonly struct VitalsSnapshot
    {
        public VitalsSnapshot(
            float health,
            float maxHealth,
            float shield,
            float maxShield,
            float energy,
            float maxEnergy)
        {
            Health = health;
            MaxHealth = maxHealth;
            Shield = shield;
            MaxShield = maxShield;
            Energy = energy;
            MaxEnergy = maxEnergy;
        }

        public float Health { get; }
        public float MaxHealth { get; }
        public float Shield { get; }
        public float MaxShield { get; }
        public float Energy { get; }
        public float MaxEnergy { get; }
    }

    [DisallowMultipleComponent]
    public sealed class VitalsComponent : MonoBehaviour
    {
        [Min(1f), SerializeField] private float _maxHealth = 100f;
        [SerializeField] private float _health = 100f;
        [Min(0f), SerializeField] private float _maxShield;
        [SerializeField] private float _shield;
        [Min(0f), SerializeField] private float _maxEnergy;
        [SerializeField] private float _energy;

        private bool _deathRaised;

        public event Action<VitalsComponent, VitalsSnapshot, VitalsSnapshot> VitalsChanged;
        public event Action<VitalsComponent, float, float> HealthChanged;
        public event Action<VitalsComponent, float, float> ShieldChanged;
        public event Action<VitalsComponent, float, float> EnergyChanged;
        public event Action<VitalsComponent, WorldHealthBarData> WorldHealthBarDataChanged;
        public event Action<VitalsComponent> Died;

        public float Health => _health;
        public float MaxHealth => _maxHealth;
        public float Shield => _shield;
        public float MaxShield => _maxShield;
        public float Energy => _energy;
        public float MaxEnergy => _maxEnergy;
        public bool IsAlive => _health > 0f;
        public VitalsSnapshot Snapshot => new VitalsSnapshot(
            _health, _maxHealth, _shield, _maxShield, _energy, _maxEnergy);
        public WorldHealthBarData HealthBarData => new WorldHealthBarData(
            _health, _maxHealth, _shield, _maxShield, IsAlive);

        private void Awake()
        {
            ClampSerializedValues();
            _deathRaised = !IsAlive;
        }

        private void OnValidate()
        {
            ClampSerializedValues();
        }

        public void Configure(float maxHealth, float maxShield, float maxEnergy, bool refill)
        {
            VitalsSnapshot previous = Snapshot;
            _maxHealth = Mathf.Max(1f, maxHealth);
            _maxShield = Mathf.Max(0f, maxShield);
            _maxEnergy = Mathf.Max(0f, maxEnergy);

            if (refill)
            {
                _health = _maxHealth;
                _shield = _maxShield;
                _energy = _maxEnergy;
            }
            else
            {
                ClampSerializedValues();
            }

            PublishChanges(previous);
        }

        public bool SetHealth(float value)
        {
            float next = Mathf.Clamp(value, 0f, _maxHealth);
            if (Mathf.Approximately(next, _health))
            {
                return false;
            }

            VitalsSnapshot previous = Snapshot;
            _health = next;
            PublishChanges(previous);
            return true;
        }

        public bool SetShield(float value)
        {
            float next = Mathf.Clamp(value, 0f, _maxShield);
            if (Mathf.Approximately(next, _shield))
            {
                return false;
            }

            VitalsSnapshot previous = Snapshot;
            _shield = next;
            PublishChanges(previous);
            return true;
        }

        public bool SetEnergy(float value)
        {
            float next = Mathf.Clamp(value, 0f, _maxEnergy);
            if (Mathf.Approximately(next, _energy))
            {
                return false;
            }

            VitalsSnapshot previous = Snapshot;
            _energy = next;
            PublishChanges(previous);
            return true;
        }

        public void ResetToMaximum()
        {
            VitalsSnapshot previous = Snapshot;
            _health = _maxHealth;
            _shield = _maxShield;
            _energy = _maxEnergy;
            PublishChanges(previous);
        }

        public void ApplyMaximums(
            float maxHealth,
            float maxShield,
            float maxEnergy,
            bool preserveRatios,
            bool fillAddedCapacity = false)
        {
            VitalsSnapshot previous = Snapshot;
            float healthRatio = _maxHealth > 0f ? _health / _maxHealth : 0f;
            float shieldRatio = _maxShield > 0f ? _shield / _maxShield : 0f;
            float energyRatio = _maxEnergy > 0f ? _energy / _maxEnergy : 0f;
            float previousMaxHealth = _maxHealth;
            float previousMaxShield = _maxShield;
            float previousMaxEnergy = _maxEnergy;

            _maxHealth = Mathf.Max(1f, maxHealth);
            _maxShield = Mathf.Max(0f, maxShield);
            _maxEnergy = Mathf.Max(0f, maxEnergy);
            if (preserveRatios)
            {
                _health = _maxHealth * Mathf.Clamp01(healthRatio);
                _shield = _maxShield * Mathf.Clamp01(shieldRatio);
                _energy = _maxEnergy * Mathf.Clamp01(energyRatio);
            }
            else if (fillAddedCapacity)
            {
                _health += Mathf.Max(0f, _maxHealth - previousMaxHealth);
                _shield += Mathf.Max(0f, _maxShield - previousMaxShield);
                _energy += Mathf.Max(0f, _maxEnergy - previousMaxEnergy);
                ClampSerializedValues();
            }
            else
            {
                ClampSerializedValues();
            }

            PublishChanges(previous);
        }

        public void Restore(VitalsSnapshot snapshot)
        {
            VitalsSnapshot previous = Snapshot;
            _maxHealth = Mathf.Max(1f, snapshot.MaxHealth);
            _maxShield = Mathf.Max(0f, snapshot.MaxShield);
            _maxEnergy = Mathf.Max(0f, snapshot.MaxEnergy);
            _health = Mathf.Clamp(snapshot.Health, 0f, _maxHealth);
            _shield = Mathf.Clamp(snapshot.Shield, 0f, _maxShield);
            _energy = Mathf.Clamp(snapshot.Energy, 0f, _maxEnergy);
            PublishChanges(previous);
        }

        internal DamageResult ApplyDamage(float requestedAmount)
        {
            return ApplyResolvedDamage(requestedAmount, requestedAmount, 0f, false);
        }

        internal DamageResult ApplyResolvedDamage(
            float requestedAmount,
            float modifiedAmount,
            float armorMitigation,
            bool ignoreShield)
        {
            if (!IsAlive || modifiedAmount <= 0f || float.IsNaN(modifiedAmount) ||
                float.IsInfinity(modifiedAmount))
            {
                return DamageResult.None;
            }

            VitalsSnapshot previous = Snapshot;
            float remaining = Mathf.Max(0f, modifiedAmount - Mathf.Max(0f, armorMitigation));
            float shieldDamage = ignoreShield ? 0f : Mathf.Min(_shield, remaining);
            if (!ignoreShield)
            {
                _shield -= shieldDamage;
                remaining -= shieldDamage;
            }

            float healthDamage = Mathf.Min(_health, remaining);
            _health -= healthDamage;
            PublishChanges(previous);

            return new DamageResult(
                requestedAmount,
                modifiedAmount,
                armorMitigation,
                shieldDamage,
                healthDamage,
                previous.Health > 0f && _health <= 0f);
        }

        private void ClampSerializedValues()
        {
            _maxHealth = Mathf.Max(1f, _maxHealth);
            _maxShield = Mathf.Max(0f, _maxShield);
            _maxEnergy = Mathf.Max(0f, _maxEnergy);
            _health = Mathf.Clamp(_health, 0f, _maxHealth);
            _shield = Mathf.Clamp(_shield, 0f, _maxShield);
            _energy = Mathf.Clamp(_energy, 0f, _maxEnergy);
        }

        private void PublishChanges(VitalsSnapshot previous)
        {
            VitalsSnapshot current = Snapshot;
            bool healthChanged = !Mathf.Approximately(previous.Health, current.Health) ||
                                 !Mathf.Approximately(previous.MaxHealth, current.MaxHealth);
            bool shieldChanged = !Mathf.Approximately(previous.Shield, current.Shield) ||
                                 !Mathf.Approximately(previous.MaxShield, current.MaxShield);
            bool energyChanged = !Mathf.Approximately(previous.Energy, current.Energy) ||
                                 !Mathf.Approximately(previous.MaxEnergy, current.MaxEnergy);

            if (!healthChanged && !shieldChanged && !energyChanged)
            {
                return;
            }

            if (healthChanged)
            {
                HealthChanged?.Invoke(this, _health, _maxHealth);
            }

            if (shieldChanged)
            {
                ShieldChanged?.Invoke(this, _shield, _maxShield);
            }

            if (energyChanged)
            {
                EnergyChanged?.Invoke(this, _energy, _maxEnergy);
            }

            VitalsChanged?.Invoke(this, previous, current);
            if (healthChanged || shieldChanged)
            {
                WorldHealthBarDataChanged?.Invoke(this, HealthBarData);
            }

            if (_health > 0f)
            {
                _deathRaised = false;
            }
            else if (previous.Health > 0f && !_deathRaised)
            {
                _deathRaised = true;
                Died?.Invoke(this);
            }
        }
    }
}
