using OriginCore.Gameplay;
using UnityEngine;

namespace OriginCore.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(VitalsComponent))]
    public sealed class RegenerationComponent : MonoBehaviour
    {
        [SerializeField] private VitalsComponent _vitals;
        [SerializeField] private RuntimeStatBlock _statBlock;
        [SerializeField] private EntityIdentity _identity;
        [Min(0.05f), SerializeField] private float _tickInterval = 0.2f;
        [Min(0f), SerializeField] private float _fallbackHealthPerSecond;
        [Min(0f), SerializeField] private float _fallbackShieldPerSecond = 1f;
        [Min(0f), SerializeField] private float _fallbackEnergyPerSecond;

        private float _accumulator;

        public float TickInterval => _tickInterval;

        private void Awake()
        {
            CacheComponents();
        }

        private void OnValidate()
        {
            CacheComponents();
            _tickInterval = Mathf.Max(0.05f, _tickInterval);
            _fallbackHealthPerSecond = Mathf.Max(0f, _fallbackHealthPerSecond);
            _fallbackShieldPerSecond = Mathf.Max(0f, _fallbackShieldPerSecond);
            _fallbackEnergyPerSecond = Mathf.Max(0f, _fallbackEnergyPerSecond);
        }

        private void Update()
        {
            if (_vitals == null || !_vitals.IsAlive || Time.deltaTime <= 0f)
            {
                return;
            }

            _accumulator += Time.deltaTime;
            if (_accumulator < _tickInterval)
            {
                return;
            }

            float elapsed = _accumulator;
            _accumulator = 0f;
            Tick(elapsed);
        }

        public void Configure(
            VitalsComponent vitals,
            RuntimeStatBlock statBlock,
            float tickInterval = 0.2f)
        {
            _vitals = vitals != null ? vitals : GetComponent<VitalsComponent>();
            _statBlock = statBlock != null ? statBlock : GetComponent<RuntimeStatBlock>();
            _identity = GetComponent<EntityIdentity>();
            _tickInterval = Mathf.Max(0.05f, tickInterval);
            _accumulator = 0f;
        }

        public bool Tick(float elapsedSeconds)
        {
            CacheComponents();
            if (_vitals == null || !_vitals.IsAlive || elapsedSeconds <= 0f)
            {
                return false;
            }

            float healthRate = ResolveRate(RuntimeStatId.HealthRegen, _fallbackHealthPerSecond);
            float shieldRate = ResolveRate(RuntimeStatId.ShieldRegen, _fallbackShieldPerSecond);
            float energyRate = ResolveRate(RuntimeStatId.EnergyRegen, _fallbackEnergyPerSecond);
            bool changed = false;

            if (_vitals.Health < _vitals.MaxHealth && healthRate > 0f)
            {
                changed |= _vitals.SetHealth(_vitals.Health + healthRate * elapsedSeconds);
            }
            else if (_vitals.Shield < _vitals.MaxShield && shieldRate > 0f)
            {
                changed |= _vitals.SetShield(_vitals.Shield + shieldRate * elapsedSeconds);
            }

            if (_vitals.Energy < _vitals.MaxEnergy && energyRate > 0f)
            {
                changed |= _vitals.SetEnergy(_vitals.Energy + energyRate * elapsedSeconds);
            }

            return changed;
        }

        private float ResolveRate(RuntimeStatId statId, float fallback)
        {
            if (_statBlock != null)
            {
                return _statBlock.GetStat(statId);
            }

            UnitDefinition definition = _identity != null ? _identity.Definition : null;
            if (definition == null)
            {
                return fallback;
            }

            switch (statId)
            {
                case RuntimeStatId.HealthRegen:
                    return definition.HealthRegen;
                case RuntimeStatId.ShieldRegen:
                    return definition.ShieldRegen;
                case RuntimeStatId.EnergyRegen:
                    return definition.EnergyRegen;
                default:
                    return fallback;
            }
        }

        private void CacheComponents()
        {
            if (_vitals == null)
            {
                _vitals = GetComponent<VitalsComponent>();
            }

            if (_statBlock == null)
            {
                _statBlock = GetComponent<RuntimeStatBlock>();
            }

            if (_identity == null)
            {
                _identity = GetComponent<EntityIdentity>();
            }
        }
    }
}
