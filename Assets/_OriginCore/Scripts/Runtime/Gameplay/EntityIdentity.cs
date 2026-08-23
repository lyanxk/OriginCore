using System;
using OriginCore.Core;
using UnityEngine;

namespace OriginCore.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class EntityIdentity : MonoBehaviour
    {
        [SerializeField] private UnitDefinition _definition;
        [SerializeField] private string _archetypeId;
        [SerializeField] private string _runtimeId;
        [SerializeField] private string _displayName = "Entity";
        [SerializeField] private UnitRole _roles;
        [SerializeField] private bool _runtimeSpawned;

        private EntityRegistry _registeredRegistry;

        public UnitDefinition Definition => _definition;
        public string ArchetypeId => _definition != null ? _definition.ArchetypeId : _archetypeId;
        public string RuntimeId => _runtimeId;
        public string DisplayName => _definition != null ? _definition.DisplayName : _displayName;
        public UnitRole Roles => _definition != null ? _definition.Roles : _roles;
        public bool IsRegistered => _registeredRegistry != null && _registeredRegistry.Contains(this);
        public bool IsRuntimeSpawned => _runtimeSpawned;

        private void Awake()
        {
            if (Application.isPlaying)
            {
                EnsureRuntimeId();
            }
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            EnsureRuntimeId();
            TryRegisterWithAppRoot();
        }

        private void Start()
        {
            if (Application.isPlaying && _registeredRegistry == null)
            {
                TryRegisterWithAppRoot();
            }
        }

        private void OnDisable()
        {
            UnregisterFromRegistry();
        }

        public void ConfigureDefinition(UnitDefinition definition)
        {
            _definition = definition;
            SyncDefinitionFallbacks();
        }

        public void ConfigureFallbackIdentity(
            string archetypeId,
            string displayName,
            UnitRole roles)
        {
            _definition = null;
            _archetypeId = string.IsNullOrWhiteSpace(archetypeId) ? string.Empty : archetypeId.Trim();
            _displayName = string.IsNullOrWhiteSpace(displayName) ? "Entity" : displayName.Trim();
            _roles = roles;
        }

        public bool AssignRuntimeId(string runtimeId, bool allowReplace = false)
        {
            if (string.IsNullOrWhiteSpace(runtimeId))
            {
                return false;
            }

            string normalized = runtimeId.Trim();
            if (!allowReplace && !string.IsNullOrEmpty(_runtimeId) &&
                !string.Equals(_runtimeId, normalized, StringComparison.Ordinal))
            {
                return false;
            }

            _runtimeId = normalized;
            return true;
        }

        public void MarkRuntimeSpawned(bool runtimeSpawned = true)
        {
            _runtimeSpawned = runtimeSpawned;
        }

        public string EnsureRuntimeId()
        {
            if (string.IsNullOrWhiteSpace(_runtimeId))
            {
                _runtimeId = Guid.NewGuid().ToString("N");
            }

            return _runtimeId;
        }

        public bool HasAnyRole(UnitRole roles)
        {
            return Roles.HasAny(roles);
        }

        public bool RegisterWith(EntityRegistry registry)
        {
            if (registry == null)
            {
                return false;
            }

            if (_registeredRegistry == registry && registry.Contains(this))
            {
                return true;
            }

            UnregisterFromRegistry();
            return registry.Register(this);
        }

        public void UnregisterFromRegistry()
        {
            if (_registeredRegistry == null)
            {
                return;
            }

            EntityRegistry registry = _registeredRegistry;
            _registeredRegistry = null;
            registry.Unregister(this);
        }

        internal void NotifyRegistered(EntityRegistry registry)
        {
            _registeredRegistry = registry;
        }

        internal void NotifyUnregistered(EntityRegistry registry)
        {
            if (_registeredRegistry == registry)
            {
                _registeredRegistry = null;
            }
        }

        private bool TryRegisterWithAppRoot()
        {
            if (!AppRoot.TryGetInstance(out AppRoot appRoot) || appRoot.Services == null)
            {
                return false;
            }

            return RegisterWith(appRoot.Services.EntityRegistry);
        }

        private void OnValidate()
        {
            SyncDefinitionFallbacks();
            _runtimeId = string.IsNullOrWhiteSpace(_runtimeId) ? string.Empty : _runtimeId.Trim();
        }

        private void SyncDefinitionFallbacks()
        {
            if (_definition == null)
            {
                _archetypeId = string.IsNullOrWhiteSpace(_archetypeId) ? string.Empty : _archetypeId.Trim();
                _displayName = string.IsNullOrWhiteSpace(_displayName) ? "Entity" : _displayName.Trim();
                return;
            }

            _archetypeId = _definition.ArchetypeId;
            _displayName = _definition.DisplayName;
            _roles = _definition.Roles;
        }
    }
}
