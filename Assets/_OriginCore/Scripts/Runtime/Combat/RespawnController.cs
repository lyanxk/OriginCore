using System;
using OriginCore.Abilities;
using OriginCore.Content;
using OriginCore.Core;
using OriginCore.Economy;
using OriginCore.Gameplay;
using UnityEngine;

namespace OriginCore.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(VitalsComponent), typeof(CombatDeathHandler))]
    public sealed class RespawnController : MonoBehaviour
    {
        [SerializeField] private HeroDefinition _heroDefinition;
        [SerializeField] private VitalsComponent _vitals;
        [SerializeField] private CombatDeathHandler _deathHandler;
        [SerializeField] private EntityIdentity _identity;
        [SerializeField] private HeroFormStateMachine _forms;
        [SerializeField] private AbilityLoadout _abilities;
        [SerializeField] private Transform _respawnAnchor;
        [SerializeField] private string _immediateRespawnCommanderId = "commander.z";

        private Behaviour[] _managedBehaviours = Array.Empty<Behaviour>();
        private bool[] _managedBehaviourStates = Array.Empty<bool>();
        private Renderer[] _renderers = Array.Empty<Renderer>();
        private bool[] _rendererStates = Array.Empty<bool>();
        private Collider[] _colliders = Array.Empty<Collider>();
        private bool[] _colliderStates = Array.Empty<bool>();
        private Vector3 _spawnPosition;
        private Quaternion _spawnRotation;
        private bool _isRespawning;
        private float _remainingSeconds;

        public event Action<RespawnController> RespawnStarted;
        public event Action<RespawnController> Respawned;

        public HeroDefinition HeroDefinition => _heroDefinition;
        public bool IsRespawning => _isRespawning;
        public float RemainingSeconds => Mathf.Max(0f, _remainingSeconds);
        public float CurrentRespawnDuration => ResolveRespawnDuration();

        private void Awake()
        {
            CacheComponents();
            CacheManagedState();
            CaptureRespawnTransform();
            ConfigureDeathHandler();
        }

        private void OnEnable()
        {
            CacheComponents();
            ConfigureDeathHandler();
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

        private void OnValidate()
        {
            CacheComponents();
            _immediateRespawnCommanderId = ContentIdUtility.Normalize(
                _immediateRespawnCommanderId);
        }

        private void Update()
        {
            if (!_isRespawning || Time.deltaTime <= 0f)
            {
                return;
            }

            _remainingSeconds = Mathf.Max(0f, _remainingSeconds - Time.deltaTime);
            if (_remainingSeconds <= 0f)
            {
                CompleteRespawn();
            }
        }

        public void Configure(HeroDefinition heroDefinition, Transform respawnAnchor = null)
        {
            _heroDefinition = heroDefinition;
            _respawnAnchor = respawnAnchor;
            CacheComponents();
            CaptureRespawnTransform();
            ConfigureDeathHandler();
        }

        public void SetRespawnAnchor(Transform respawnAnchor)
        {
            _respawnAnchor = respawnAnchor;
            CaptureRespawnTransform();
        }

        public bool TryImmediateRespawn(out string error)
        {
            if (!_isRespawning)
            {
                error = "Hero is not awaiting respawn.";
                return false;
            }

            int crystalCost = _heroDefinition != null
                ? _heroDefinition.ImmediateRespawnCrystalCost
                : 0;
            if (crystalCost <= 0 || !IsImmediateRespawnCommanderActive())
            {
                error = "Immediate respawn is unavailable for this match.";
                return false;
            }

            if (!AppRoot.TryGetInstance(out AppRoot root) || root.Services == null ||
                root.Services.ResourceService == null ||
                !root.Services.ResourceService.TrySpend(
                    new ResourceCost(0, crystalCost, 0)))
            {
                error = "Not enough Crystal for immediate respawn.";
                return false;
            }

            CompleteRespawn();
            error = string.Empty;
            return true;
        }

        public bool RestoreRespawnCountdown(float remainingSeconds)
        {
            if (!_isRespawning || _vitals == null || _vitals.IsAlive)
            {
                return false;
            }

            _remainingSeconds = Mathf.Max(0f, remainingSeconds);
            if (_remainingSeconds <= 0f)
            {
                CompleteRespawn();
            }

            return true;
        }

        public void CaptureRespawnTransform()
        {
            Transform anchor = ResolveRespawnAnchor();
            if (anchor == null)
            {
                anchor = transform;
            }

            _spawnPosition = anchor.position;
            _spawnRotation = anchor.rotation;
        }

        private void HandleDied(VitalsComponent vitals)
        {
            if (_isRespawning)
            {
                return;
            }

            _isRespawning = true;
            _remainingSeconds = ResolveRespawnDuration();
            _abilities?.CancelPendingEffects();
            CaptureEnabledStates();
            SetDeadPresentation();
            RespawnStarted?.Invoke(this);
            if (_remainingSeconds <= 0f)
            {
                CompleteRespawn();
            }
        }

        private void CompleteRespawn()
        {
            if (!_isRespawning || _vitals == null)
            {
                return;
            }

            _isRespawning = false;
            _remainingSeconds = 0f;
            Transform anchor = ResolveRespawnAnchor();
            Vector3 position = anchor != null ? anchor.position : _spawnPosition;
            Quaternion rotation = anchor != null ? anchor.rotation : _spawnRotation;

            CharacterController characterController = GetComponent<CharacterController>();
            bool characterWasEnabled = characterController != null && characterController.enabled;
            if (characterController != null)
            {
                characterController.enabled = false;
            }

            transform.SetPositionAndRotation(position, rotation);
            if (characterController != null)
            {
                characterController.enabled = characterWasEnabled;
            }

            HybridPawnMotor motor = GetComponent<HybridPawnMotor>();
            motor?.ResetMotion();
            _vitals.ResetToMaximum();
            _deathHandler?.ResetForRespawn();
            RestoreManagedState();
            RegisterIdentity();
            Respawned?.Invoke(this);
        }

        private void ConfigureDeathHandler()
        {
            if (_deathHandler != null && _deathHandler.DeactivateOnDeath)
            {
                _deathHandler.Configure(
                    _vitals,
                    _identity,
                    _deathHandler.Selectable,
                    _deathHandler.CommandQueue,
                    _deathHandler.MovementDriver,
                    false);
            }
        }

        private float ResolveRespawnDuration()
        {
            float baseDuration = _heroDefinition != null
                ? _heroDefinition.RespawnSeconds
                : 0f;
            float multiplier = _forms != null ? _forms.RespawnTimeMultiplier : 1f;
            return Mathf.Max(0f, baseDuration * Mathf.Max(0f, multiplier));
        }

        private bool IsImmediateRespawnCommanderActive()
        {
            if (!AppRoot.TryGetInstance(out AppRoot root) || root.Services == null ||
                root.Services.MatchSession == null ||
                root.Services.MatchSession.CurrentConfiguration == null)
            {
                return false;
            }

            return string.Equals(
                root.Services.MatchSession.CurrentConfiguration.commanderId,
                _immediateRespawnCommanderId,
                StringComparison.Ordinal);
        }

        private Transform ResolveRespawnAnchor()
        {
            if (_respawnAnchor != null)
            {
                return _respawnAnchor;
            }

            if (!AppRoot.TryGetInstance(out AppRoot root) || root.Services == null ||
                root.Services.EntityRegistry == null)
            {
                return null;
            }

            var entities = root.Services.EntityRegistry.Entities;
            for (int i = 0; i < entities.Count; i++)
            {
                EntityIdentity candidate = entities[i];
                HeroSpawnAnchor anchor = candidate != null
                    ? candidate.GetComponent<HeroSpawnAnchor>()
                    : null;
                if (anchor != null && anchor.IsAvailableFor(FactionId.Friendly))
                {
                    return anchor.AnchorPoint;
                }
            }

            return null;
        }

        private void CacheComponents()
        {
            if (_vitals == null) _vitals = GetComponent<VitalsComponent>();
            if (_deathHandler == null) _deathHandler = GetComponent<CombatDeathHandler>();
            if (_identity == null) _identity = GetComponent<EntityIdentity>();
            if (_forms == null) _forms = GetComponent<HeroFormStateMachine>();
            if (_abilities == null) _abilities = GetComponent<AbilityLoadout>();
        }

        private void CacheManagedState()
        {
            Behaviour[] behaviours = GetComponents<Behaviour>();
            int count = 0;
            for (int i = 0; i < behaviours.Length; i++)
            {
                Behaviour behaviour = behaviours[i];
                if (behaviour != null && behaviour != this && behaviour != _vitals &&
                    behaviour != _deathHandler)
                {
                    count++;
                }
            }

            _managedBehaviours = new Behaviour[count];
            _managedBehaviourStates = new bool[count];
            int write = 0;
            for (int i = 0; i < behaviours.Length; i++)
            {
                Behaviour behaviour = behaviours[i];
                if (behaviour == null || behaviour == this || behaviour == _vitals ||
                    behaviour == _deathHandler)
                {
                    continue;
                }

                _managedBehaviours[write++] = behaviour;
            }

            _renderers = GetComponentsInChildren<Renderer>(true);
            _rendererStates = new bool[_renderers.Length];
            _colliders = GetComponentsInChildren<Collider>(true);
            _colliderStates = new bool[_colliders.Length];
        }

        private void CaptureEnabledStates()
        {
            for (int i = 0; i < _managedBehaviours.Length; i++)
            {
                _managedBehaviourStates[i] = _managedBehaviours[i] != null &&
                                             _managedBehaviours[i].enabled;
            }

            for (int i = 0; i < _renderers.Length; i++)
            {
                _rendererStates[i] = _renderers[i] != null && _renderers[i].enabled;
            }

            for (int i = 0; i < _colliders.Length; i++)
            {
                _colliderStates[i] = _colliders[i] != null && _colliders[i].enabled;
            }
        }

        private void SetDeadPresentation()
        {
            for (int i = 0; i < _managedBehaviours.Length; i++)
            {
                if (_managedBehaviours[i] != null)
                {
                    _managedBehaviours[i].enabled = false;
                }
            }

            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null) _renderers[i].enabled = false;
            }

            for (int i = 0; i < _colliders.Length; i++)
            {
                if (_colliders[i] != null) _colliders[i].enabled = false;
            }
        }

        private void RestoreManagedState()
        {
            for (int i = 0; i < _colliders.Length; i++)
            {
                if (_colliders[i] != null) _colliders[i].enabled = _colliderStates[i];
            }

            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null) _renderers[i].enabled = _rendererStates[i];
            }

            for (int i = 0; i < _managedBehaviours.Length; i++)
            {
                if (_managedBehaviours[i] != null)
                {
                    _managedBehaviours[i].enabled = _managedBehaviourStates[i];
                }
            }
        }

        private void RegisterIdentity()
        {
            if (_identity == null || !AppRoot.TryGetInstance(out AppRoot root) ||
                root.Services == null || root.Services.EntityRegistry == null)
            {
                return;
            }

            _identity.RegisterWith(root.Services.EntityRegistry);
        }
    }
}
