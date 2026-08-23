using System;
using OriginCore.Combat;
using OriginCore.Gameplay;
using UnityEngine;

namespace OriginCore.Units
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EntityIdentity), typeof(VitalsComponent))]
    public sealed class LifetimeComponent : MonoBehaviour
    {
        [SerializeField] private EntityIdentity _identity;
        [SerializeField] private VitalsComponent _vitals;
        [Min(0f), SerializeField] private float _remainingSeconds;
        [SerializeField] private bool _active;

        private bool _initialized;

        public event Action<LifetimeComponent> Expired;

        public float RemainingSeconds => Mathf.Max(0f, _remainingSeconds);
        public bool IsActive => _active && _remainingSeconds > 0f;

        private void Awake()
        {
            CacheComponents();
            EnsureInitialized();
        }

        private void OnEnable()
        {
            CacheComponents();
            EnsureInitialized();
            if (_vitals != null)
            {
                _vitals.Died -= HandleDied;
                _vitals.Died += HandleDied;
            }
        }

        private void OnDisable()
        {
            if (_vitals != null)
            {
                _vitals.Died -= HandleDied;
            }
        }

        private void Update()
        {
            if (!IsActive || Time.deltaTime <= 0f || _vitals == null || !_vitals.IsAlive)
            {
                return;
            }

            _remainingSeconds = Mathf.Max(0f, _remainingSeconds - Time.deltaTime);
            if (_remainingSeconds <= 0f)
            {
                _active = false;
                _vitals.SetHealth(0f);
                Expired?.Invoke(this);
            }
        }

        private void OnValidate()
        {
            CacheComponents();
            _remainingSeconds = Mathf.Max(0f, _remainingSeconds);
        }

        public void Configure(float lifetimeSeconds, bool restart = true)
        {
            if (restart || !_initialized)
            {
                _remainingSeconds = Mathf.Max(0f, lifetimeSeconds);
            }

            _active = _remainingSeconds > 0f;
            _initialized = true;
        }

        public void Restore(float remainingSeconds, bool active)
        {
            _remainingSeconds = Mathf.Max(0f, remainingSeconds);
            _active = active && _remainingSeconds > 0f;
            _initialized = true;
        }

        public void MakePermanent()
        {
            _remainingSeconds = 0f;
            _active = false;
            _initialized = true;
        }

        private void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            float configuredLifetime = _identity != null && _identity.Definition != null
                ? _identity.Definition.LifetimeSeconds
                : 0f;
            Configure(configuredLifetime);
        }

        private void HandleDied(VitalsComponent vitals)
        {
            _active = false;
        }

        private void CacheComponents()
        {
            if (_identity == null)
            {
                _identity = GetComponent<EntityIdentity>();
            }

            if (_vitals == null)
            {
                _vitals = GetComponent<VitalsComponent>();
            }
        }
    }
}
