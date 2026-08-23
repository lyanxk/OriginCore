using System;
using OriginCore.Debugging;
using UnityEngine;

namespace OriginCore.Economy
{
    [DefaultExecutionOrder(-11950)]
    [DisallowMultipleComponent]
    public sealed class ResourceService : MonoBehaviour, OriginCore.Buildings.IProductionResourceAccount
    {
        [Min(0), SerializeField] private int _initialCommanderResource = 500;
        [Min(0), SerializeField] private int _initialCrystal = 250;
        [Min(0), SerializeField] private int _initialInfluenceUsed;
        [Min(0), SerializeField] private int _initialInfluenceCap = 20;

        private ResourceWallet _wallet;
        private IDebugOverlayService _debugOverlay;

        public event Action<ResourceChanged> ResourceChanged;

        public ResourceWallet Wallet
        {
            get
            {
                EnsureInitialized();
                return _wallet;
            }
        }

        public ResourceSnapshot Snapshot => Wallet.Snapshot;
        public int InitialCommanderResource => _initialCommanderResource;
        public int InitialCrystal => _initialCrystal;
        public int InitialInfluenceUsed => _initialInfluenceUsed;
        public int InitialInfluenceCap => _initialInfluenceCap;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnDestroy()
        {
            UnbindWallet();
            _debugOverlay = null;
        }

        private void OnValidate()
        {
            ClampInitialValues();
        }

        public void ConfigureInitialValues(
            int commanderResource,
            int crystal,
            int influenceUsed,
            int influenceCap)
        {
            _initialCommanderResource = Mathf.Max(0, commanderResource);
            _initialCrystal = Mathf.Max(0, crystal);
            _initialInfluenceCap = Mathf.Max(0, influenceCap);
            _initialInfluenceUsed = Mathf.Clamp(
                influenceUsed,
                0,
                _initialInfluenceCap);

            if (_wallet != null)
            {
                ResetWallet();
            }
        }

        public bool Add(ResourceType type, int amount)
        {
            return Wallet.Add(type, amount);
        }

        public bool CanAfford(ResourceCost cost)
        {
            return Wallet.CanAfford(cost);
        }

        public bool TrySpend(ResourceCost cost)
        {
            return Wallet.TrySpend(cost);
        }

        public bool ReserveInfluence(int amount)
        {
            return Wallet.ReserveInfluence(amount);
        }

        public bool ReleaseInfluence(int amount)
        {
            return Wallet.ReleaseInfluence(amount);
        }

        public bool Refund(ResourceCost cost)
        {
            return Wallet.Refund(cost);
        }

        public void Restore(ResourceSnapshot snapshot)
        {
            Wallet.Restore(snapshot);
        }

        public void ResetWallet()
        {
            UnbindWallet();
            ClampInitialValues();
            _wallet = new ResourceWallet(
                _initialCommanderResource,
                _initialCrystal,
                _initialInfluenceUsed,
                _initialInfluenceCap);
            _wallet.ResourceChanged += HandleResourceChanged;
            ResourceSnapshot snapshot = _wallet.Snapshot;
            ResourceChanged?.Invoke(new ResourceChanged(
                ResourceChangeReason.Initialized,
                snapshot,
                snapshot));
            UpdateDebugOverlay(snapshot);
        }

        internal void Initialize(IDebugOverlayService debugOverlay)
        {
            EnsureInitialized();
            _debugOverlay = debugOverlay;
            UpdateDebugOverlay(_wallet.Snapshot);
        }

        internal void Shutdown()
        {
            _debugOverlay = null;
        }

        private void EnsureInitialized()
        {
            if (_wallet == null)
            {
                ResetWallet();
            }
        }

        private void UnbindWallet()
        {
            if (_wallet != null)
            {
                _wallet.ResourceChanged -= HandleResourceChanged;
            }
        }

        private void HandleResourceChanged(ResourceChanged change)
        {
            UpdateDebugOverlay(change.Current);
            ResourceChanged?.Invoke(change);
        }

        private void UpdateDebugOverlay(ResourceSnapshot snapshot)
        {
            if (_debugOverlay == null)
            {
                return;
            }

            DebugOverlaySnapshot previous = _debugOverlay.Snapshot;
            _debugOverlay.SetSnapshot(new DebugOverlaySnapshot(
                previous.Mode,
                previous.SelectedCount,
                previous.QueuedCommandCount,
                snapshot.CommanderResource));
        }

        private void ClampInitialValues()
        {
            _initialCommanderResource = Mathf.Max(0, _initialCommanderResource);
            _initialCrystal = Mathf.Max(0, _initialCrystal);
            _initialInfluenceCap = Mathf.Max(0, _initialInfluenceCap);
            _initialInfluenceUsed = Mathf.Clamp(
                _initialInfluenceUsed,
                0,
                _initialInfluenceCap);
        }
    }
}
