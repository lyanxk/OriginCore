using OriginCore.Gameplay;
using UnityEngine;

namespace OriginCore.Economy
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EntityIdentity))]
    public sealed class Gatherer : MonoBehaviour
    {
        [Min(1), SerializeField] private int _carryCapacity = 10;
        [Min(0.05f), SerializeField] private float _unitsPerSecond = 2f;
        [Min(0.1f), SerializeField] private float _interactionRange = 1.5f;
        [SerializeField] private ResourceType _carriedType = ResourceType.Crystal;
        [Min(0), SerializeField] private int _carriedAmount;

        private float _harvestAccumulator;

        public int CarryCapacity => Mathf.Max(1, _carryCapacity);
        public float UnitsPerSecond => Mathf.Max(0.05f, _unitsPerSecond);
        public float InteractionRange => Mathf.Max(0.1f, _interactionRange);
        public ResourceType CarriedType => _carriedType;
        public int CarriedAmount => Mathf.Clamp(_carriedAmount, 0, CarryCapacity);
        public bool IsFull => CarriedAmount >= CarryCapacity;

        private void OnValidate()
        {
            _carryCapacity = Mathf.Max(1, _carryCapacity);
            _unitsPerSecond = Mathf.Max(0.05f, _unitsPerSecond);
            _interactionRange = Mathf.Max(0.1f, _interactionRange);
            _carriedAmount = Mathf.Clamp(_carriedAmount, 0, _carryCapacity);
        }

        public void Configure(int carryCapacity, float unitsPerSecond, float interactionRange)
        {
            _carryCapacity = Mathf.Max(1, carryCapacity);
            _unitsPerSecond = Mathf.Max(0.05f, unitsPerSecond);
            _interactionRange = Mathf.Max(0.1f, interactionRange);
            _carriedAmount = Mathf.Clamp(_carriedAmount, 0, _carryCapacity);
        }

        public int Harvest(ResourceNode node, float deltaTime)
        {
            if (node == null || node.IsDepleted || IsFull || deltaTime <= 0f)
            {
                return 0;
            }

            if (_carriedAmount > 0 && _carriedType != node.ResourceType)
            {
                return 0;
            }

            _carriedType = node.ResourceType;
            _harvestAccumulator += UnitsPerSecond * deltaTime;
            int requested = Mathf.Min(
                CarryCapacity - _carriedAmount,
                Mathf.FloorToInt(_harvestAccumulator));
            if (requested <= 0)
            {
                return 0;
            }

            int extracted = node.Extract(requested);
            _harvestAccumulator -= requested;
            _carriedAmount += extracted;
            return extracted;
        }

        public int DepositTo(ResourceDropoff dropoff, FactionId faction)
        {
            int amount = CarriedAmount;
            if (amount <= 0 || dropoff == null ||
                !dropoff.Deposit(_carriedType, faction, amount))
            {
                return 0;
            }

            _carriedAmount = 0;
            _harvestAccumulator = 0f;
            return amount;
        }

        public void RestoreCargo(ResourceType resourceType, int amount)
        {
            _carriedType = resourceType;
            _carriedAmount = Mathf.Clamp(amount, 0, CarryCapacity);
            _harvestAccumulator = 0f;
        }
    }
}
