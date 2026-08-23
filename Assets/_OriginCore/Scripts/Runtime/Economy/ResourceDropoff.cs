using OriginCore.Core;
using OriginCore.Gameplay;
using UnityEngine;

namespace OriginCore.Economy
{
    [DisallowMultipleComponent]
    public sealed class ResourceDropoff : MonoBehaviour
    {
        [SerializeField] private ResourceType _acceptedType = ResourceType.Crystal;
        [SerializeField] private FactionMember _faction;

        public ResourceType AcceptedType => _acceptedType;
        public FactionMember Faction => _faction;

        private void Awake()
        {
            if (_faction == null) _faction = GetComponent<FactionMember>();
        }

        private void OnValidate()
        {
            if (_faction == null) _faction = GetComponent<FactionMember>();
        }

        public void Configure(ResourceType acceptedType, FactionMember faction)
        {
            _acceptedType = acceptedType;
            _faction = faction != null ? faction : GetComponent<FactionMember>();
        }

        public bool CanAccept(ResourceType type, FactionId faction)
        {
            return isActiveAndEnabled && type == _acceptedType &&
                   (_faction == null || _faction.Faction == faction);
        }

        public bool Deposit(ResourceType type, FactionId faction, int amount)
        {
            return amount > 0 && CanAccept(type, faction) &&
                   AppRoot.TryGetInstance(out AppRoot root) && root.Services != null &&
                   root.Services.ResourceService.Add(type, amount);
        }
    }
}
