using OriginCore.Buildings;
using OriginCore.Economy;
using UnityEngine;

namespace OriginCore.AI
{
    public enum EnemyFundingPolicy
    {
        Unconfigured = 0,
        Finite = 1,
        Regenerating = 2,
        FreeRecipes = 3
    }

    [DisallowMultipleComponent]
    public sealed class EnemyProductionAccount : MonoBehaviour, IProductionResourceAccount
    {
        [SerializeField] private EnemyFundingPolicy _fundingPolicy;
        [Min(0), SerializeField] private int _initialCommanderResource;
        [Min(0), SerializeField] private int _initialCrystal;
        [Min(0), SerializeField] private int _populationCap = 20;
        [Min(0f), SerializeField] private float _commanderResourcePerSecond;
        [Min(0f), SerializeField] private float _crystalPerSecond;

        private ResourceWallet _wallet;
        private float _commanderRemainder;
        private float _crystalRemainder;

        public EnemyFundingPolicy FundingPolicy => _fundingPolicy;
        public ResourceSnapshot Snapshot => Wallet.Snapshot;
        public bool IsConfigured => _fundingPolicy != EnemyFundingPolicy.Unconfigured;

        private ResourceWallet Wallet
        {
            get
            {
                if (_wallet == null) ResetAccount();
                return _wallet;
            }
        }

        private void Awake() => ResetAccount();

        private void Update()
        {
            if (_fundingPolicy != EnemyFundingPolicy.Regenerating || Time.deltaTime <= 0f)
            {
                return;
            }
            Accumulate(ResourceType.CommanderResource, _commanderResourcePerSecond,
                ref _commanderRemainder, Time.deltaTime);
            Accumulate(ResourceType.Crystal, _crystalPerSecond,
                ref _crystalRemainder, Time.deltaTime);
        }

        public bool TrySpend(ResourceCost cost)
        {
            if (!IsConfigured) return false;
            if (_fundingPolicy == EnemyFundingPolicy.FreeRecipes)
            {
                return Wallet.ReserveInfluence(cost.Influence);
            }
            return Wallet.TrySpend(cost);
        }

        public bool Refund(ResourceCost cost)
        {
            if (!IsConfigured) return false;
            if (_fundingPolicy == EnemyFundingPolicy.FreeRecipes)
            {
                return Wallet.ReleaseInfluence(cost.Influence);
            }
            return Wallet.Refund(cost);
        }

        public bool ReserveInfluence(int amount) =>
            IsConfigured && Wallet.ReserveInfluence(amount);

        public bool ReleaseInfluence(int amount) =>
            IsConfigured && Wallet.ReleaseInfluence(amount);

        public void Restore(ResourceSnapshot snapshot)
        {
            Wallet.Restore(snapshot);
        }

        public void Configure(
            EnemyFundingPolicy fundingPolicy,
            int commanderResource,
            int crystal,
            int populationCap,
            float commanderResourcePerSecond = 0f,
            float crystalPerSecond = 0f)
        {
            _fundingPolicy = fundingPolicy;
            _initialCommanderResource = Mathf.Max(0, commanderResource);
            _initialCrystal = Mathf.Max(0, crystal);
            _populationCap = Mathf.Max(0, populationCap);
            _commanderResourcePerSecond = Mathf.Max(0f, commanderResourcePerSecond);
            _crystalPerSecond = Mathf.Max(0f, crystalPerSecond);
            ResetAccount();
        }

        private void ResetAccount()
        {
            _wallet = new ResourceWallet(
                _initialCommanderResource,
                _initialCrystal,
                0,
                _populationCap);
            _commanderRemainder = 0f;
            _crystalRemainder = 0f;
        }

        private void Accumulate(
            ResourceType type,
            float perSecond,
            ref float remainder,
            float deltaTime)
        {
            remainder += Mathf.Max(0f, perSecond) * deltaTime;
            int whole = Mathf.FloorToInt(remainder);
            if (whole <= 0) return;
            remainder -= whole;
            Wallet.Add(type, whole);
        }
    }
}
