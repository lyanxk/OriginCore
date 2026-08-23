using System;
using OriginCore.Gameplay;
using OriginCore.RTS.Commands;
using OriginCore.Units;
using UnityEngine;

namespace OriginCore.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(VitalsComponent))]
    public sealed class CombatDeathHandler : MonoBehaviour
    {
        [SerializeField] private VitalsComponent _vitals;
        [SerializeField] private EntityIdentity _identity;
        [SerializeField] private Selectable _selectable;
        [SerializeField] private UnitCommandQueue _commandQueue;
        [SerializeField] private NavMeshMovementDriver _movementDriver;
        [SerializeField] private bool _deactivateOnDeath = true;

        private bool _handled;

        public event Action<CombatDeathHandler> DeathHandled;

        public VitalsComponent Vitals => _vitals;
        public EntityIdentity Identity => _identity;
        public Selectable Selectable => _selectable;
        public UnitCommandQueue CommandQueue => _commandQueue;
        public NavMeshMovementDriver MovementDriver => _movementDriver;
        public bool DeactivateOnDeath => _deactivateOnDeath;
        public bool IsHandled => _handled;

        private void Awake()
        {
            CacheComponents();
        }

        private void OnEnable()
        {
            CacheComponents();
            _handled = false;
            BindEvents();
            if (_vitals != null && !_vitals.IsAlive)
            {
                HandleDied(_vitals);
            }
        }

        private void OnDisable()
        {
            UnbindEvents();
        }

        private void OnValidate()
        {
            CacheComponents();
        }

        public void Configure(
            VitalsComponent vitals,
            EntityIdentity identity,
            Selectable selectable,
            UnitCommandQueue commandQueue,
            NavMeshMovementDriver movementDriver,
            bool deactivateOnDeath = true)
        {
            UnbindEvents();
            _vitals = vitals != null ? vitals : GetComponent<VitalsComponent>();
            _identity = identity != null ? identity : GetComponent<EntityIdentity>();
            _selectable = selectable != null ? selectable : GetComponent<Selectable>();
            _commandQueue = commandQueue != null
                ? commandQueue
                : GetComponent<UnitCommandQueue>();
            _movementDriver = movementDriver != null
                ? movementDriver
                : GetComponent<NavMeshMovementDriver>();
            _deactivateOnDeath = deactivateOnDeath;
            if (isActiveAndEnabled)
            {
                BindEvents();
            }
        }

        public void ResetForRespawn()
        {
            CacheComponents();
            _handled = false;
            BindEvents();
        }

        private void HandleDied(VitalsComponent vitals)
        {
            if (_handled)
            {
                return;
            }

            _handled = true;
            _commandQueue?.StopAll();
            _movementDriver?.ResetToIdle();
            _selectable?.SetState(SelectionVisualState.Normal);
            _identity?.UnregisterFromRegistry();
            DeathHandled?.Invoke(this);
            if (_deactivateOnDeath && gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        private void CacheComponents()
        {
            if (_vitals == null)
            {
                _vitals = GetComponent<VitalsComponent>();
            }

            if (_identity == null)
            {
                _identity = GetComponent<EntityIdentity>();
            }

            if (_selectable == null)
            {
                _selectable = GetComponent<Selectable>();
            }

            if (_commandQueue == null)
            {
                _commandQueue = GetComponent<UnitCommandQueue>();
            }

            if (_movementDriver == null)
            {
                _movementDriver = GetComponent<NavMeshMovementDriver>();
            }
        }

        private void BindEvents()
        {
            if (_vitals != null)
            {
                _vitals.Died -= HandleDied;
                _vitals.Died += HandleDied;
            }
        }

        private void UnbindEvents()
        {
            if (_vitals != null)
            {
                _vitals.Died -= HandleDied;
            }
        }
    }
}
