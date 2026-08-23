using System;
using OriginCore.Buildings;
using OriginCore.Combat;
using OriginCore.Economy;
using UnityEngine;

namespace OriginCore.Units
{
    [DisallowMultipleComponent]
    public sealed class PopulationOwner : MonoBehaviour
    {
        [SerializeField] private VitalsComponent _vitals;

        private IProductionResourceAccount _resourceAccount;
        private int _reservedInfluence;
        private bool _released = true;

        public event Action<PopulationOwner, int> InfluenceReleased;

        public VitalsComponent Vitals => _vitals;
        public ResourceService ResourceService => _resourceAccount as ResourceService;
        public IProductionResourceAccount ResourceAccount => _resourceAccount;
        public int ReservedInfluence => _reservedInfluence;
        public bool HasReservation => !_released && _reservedInfluence > 0;
        public bool IsReleased => _released;

        private void Awake()
        {
            CacheComponents();
        }

        private void OnEnable()
        {
            CacheComponents();
            if (!Application.isPlaying && _resourceAccount == null)
            {
                _reservedInfluence = 0;
                _released = true;
            }

            BindVitals();
        }

        private void OnDisable()
        {
            UnbindVitals();
            ReleaseInfluence();
        }

        private void OnDestroy()
        {
            UnbindVitals();
            ReleaseInfluence();
        }

        private void OnValidate()
        {
            CacheComponents();
        }

        public void Configure(VitalsComponent vitals)
        {
            UnbindVitals();
            _vitals = vitals != null ? vitals : GetComponent<VitalsComponent>();
            if (isActiveAndEnabled)
            {
                BindVitals();
            }
        }

        public bool Assign(IProductionResourceAccount resourceAccount, int reservedInfluence)
        {
            if (resourceAccount == null || reservedInfluence < 0 || HasReservation)
            {
                return false;
            }

            _resourceAccount = resourceAccount;
            _reservedInfluence = reservedInfluence;
            _released = reservedInfluence == 0;
            CacheComponents();
            BindVitals();
            return true;
        }

        public bool ReleaseInfluence()
        {
            if (_released)
            {
                return false;
            }

            _released = true;
            int releasedAmount = _reservedInfluence;
            IProductionResourceAccount resourceAccount = _resourceAccount;
            _resourceAccount = null;
            if (resourceAccount == null)
            {
                // AppRoot may be destroyed before scene objects during test teardown,
                // domain reload, application quit, or an editor scene restoration.
                return false;
            }

            if (!resourceAccount.ReleaseInfluence(releasedAmount))
            {
                Debug.LogError(
                    "[OriginCore P10] Population reservation could not be released exactly once.",
                    this);
                return false;
            }

            InfluenceReleased?.Invoke(this, releasedAmount);
            return true;
        }

        public bool TryTransferTo(PopulationOwner target, int targetReservedInfluence)
        {
            if (target == null || target == this || targetReservedInfluence < 0 ||
                _resourceAccount == null || target.HasReservation)
            {
                return false;
            }

            IProductionResourceAccount resourceAccount = _resourceAccount;
            int previousReservation = _released ? 0 : _reservedInfluence;
            target.Configure(target.GetComponent<VitalsComponent>());
            if (!target.Assign(resourceAccount, targetReservedInfluence))
            {
                return false;
            }

            _released = true;
            _reservedInfluence = 0;
            _resourceAccount = null;
            if (previousReservation > targetReservedInfluence)
            {
                resourceAccount.ReleaseInfluence(previousReservation - targetReservedInfluence);
            }

            return true;
        }

        public void AbandonReservationWithoutRelease()
        {
            _released = true;
            _reservedInfluence = 0;
            _resourceAccount = null;
        }

        private void HandleDied(VitalsComponent vitals)
        {
            ReleaseInfluence();
        }

        private void CacheComponents()
        {
            if (_vitals == null)
            {
                _vitals = GetComponent<VitalsComponent>();
            }
        }

        private void BindVitals()
        {
            if (_vitals != null)
            {
                _vitals.Died -= HandleDied;
                _vitals.Died += HandleDied;
            }
        }

        private void UnbindVitals()
        {
            if (_vitals != null)
            {
                _vitals.Died -= HandleDied;
            }
        }
    }
}
