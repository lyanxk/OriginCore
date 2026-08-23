using System;
using OriginCore.Gameplay;
using UnityEngine;

namespace OriginCore.Economy
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EntityIdentity))]
    public sealed class ResourceNode : MonoBehaviour
    {
        [SerializeField] private ResourceType _resourceType = ResourceType.Crystal;
        [Min(1), SerializeField] private int _capacity = 5000;
        [Min(0), SerializeField] private int _remaining = 5000;

        public event Action<ResourceNode, int> Harvested;
        public event Action<ResourceNode> Depleted;

        public ResourceType ResourceType => _resourceType;
        public int Capacity => Mathf.Max(1, _capacity);
        public int Remaining => Mathf.Clamp(_remaining, 0, Capacity);
        public bool IsDepleted => Remaining <= 0;

        private void OnValidate()
        {
            _capacity = Mathf.Max(1, _capacity);
            _remaining = Mathf.Clamp(_remaining, 0, _capacity);
        }

        public void Configure(ResourceType resourceType, int capacity, bool refill)
        {
            _resourceType = resourceType;
            _capacity = Mathf.Max(1, capacity);
            _remaining = refill ? _capacity : Mathf.Clamp(_remaining, 0, _capacity);
        }

        public int Extract(int requestedAmount)
        {
            if (requestedAmount <= 0 || _remaining <= 0)
            {
                return 0;
            }

            int extracted = Mathf.Min(requestedAmount, _remaining);
            _remaining -= extracted;
            Harvested?.Invoke(this, extracted);
            if (_remaining <= 0)
            {
                Depleted?.Invoke(this);
            }

            return extracted;
        }

        public void RestoreRemaining(int remaining)
        {
            _remaining = Mathf.Clamp(remaining, 0, Capacity);
        }
    }
}
