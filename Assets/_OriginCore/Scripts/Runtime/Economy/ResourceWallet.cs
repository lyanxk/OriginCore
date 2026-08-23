using System;
using UnityEngine;

namespace OriginCore.Economy
{
    public enum ResourceChangeReason
    {
        Initialized = 0,
        Added = 1,
        Spent = 2,
        InfluenceReserved = 3,
        InfluenceReleased = 4,
        Refunded = 5,
        Restored = 6
    }

    public readonly struct ResourceSnapshot : IEquatable<ResourceSnapshot>
    {
        public ResourceSnapshot(
            int commanderResource,
            int crystal,
            int influenceUsed,
            int influenceCap)
        {
            CommanderResource = Mathf.Max(0, commanderResource);
            Crystal = Mathf.Max(0, crystal);
            InfluenceCap = Mathf.Max(0, influenceCap);
            InfluenceUsed = Mathf.Clamp(influenceUsed, 0, InfluenceCap);
        }

        public int CommanderResource { get; }
        public int Crystal { get; }
        public int InfluenceUsed { get; }
        public int InfluenceCap { get; }
        public int InfluenceAvailable => Mathf.Max(0, InfluenceCap - InfluenceUsed);

        public int GetAmount(ResourceType type)
        {
            switch (type)
            {
                case ResourceType.CommanderResource:
                    return CommanderResource;
                case ResourceType.Crystal:
                    return Crystal;
                case ResourceType.Influence:
                    return InfluenceAvailable;
                default:
                    return 0;
            }
        }

        public bool Equals(ResourceSnapshot other)
        {
            return CommanderResource == other.CommanderResource &&
                   Crystal == other.Crystal &&
                   InfluenceUsed == other.InfluenceUsed &&
                   InfluenceCap == other.InfluenceCap;
        }

        public override bool Equals(object obj)
        {
            return obj is ResourceSnapshot other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = CommanderResource;
                hash = (hash * 397) ^ Crystal;
                hash = (hash * 397) ^ InfluenceUsed;
                hash = (hash * 397) ^ InfluenceCap;
                return hash;
            }
        }
    }

    public readonly struct ResourceChanged
    {
        public ResourceChanged(
            ResourceChangeReason reason,
            ResourceSnapshot previous,
            ResourceSnapshot current)
        {
            Reason = reason;
            Previous = previous;
            Current = current;
        }

        public ResourceChangeReason Reason { get; }
        public ResourceSnapshot Previous { get; }
        public ResourceSnapshot Current { get; }
    }

    public sealed class ResourceWallet
    {
        private int _commanderResource;
        private int _crystal;
        private int _influenceUsed;
        private int _influenceCap;

        public ResourceWallet(
            int commanderResource,
            int crystal,
            int influenceUsed,
            int influenceCap)
        {
            _commanderResource = Mathf.Max(0, commanderResource);
            _crystal = Mathf.Max(0, crystal);
            _influenceCap = Mathf.Max(0, influenceCap);
            _influenceUsed = Mathf.Clamp(influenceUsed, 0, _influenceCap);
        }

        public event Action<ResourceChanged> ResourceChanged;

        public ResourceSnapshot Snapshot => new ResourceSnapshot(
            _commanderResource,
            _crystal,
            _influenceUsed,
            _influenceCap);

        public bool Add(ResourceType type, int amount)
        {
            if (amount <= 0)
            {
                return false;
            }

            ResourceSnapshot previous = Snapshot;
            switch (type)
            {
                case ResourceType.CommanderResource:
                    _commanderResource = SaturatingAdd(_commanderResource, amount);
                    break;
                case ResourceType.Crystal:
                    _crystal = SaturatingAdd(_crystal, amount);
                    break;
                case ResourceType.Influence:
                    _influenceCap = SaturatingAdd(_influenceCap, amount);
                    break;
                default:
                    return false;
            }

            Publish(ResourceChangeReason.Added, previous);
            return true;
        }

        public bool CanAfford(ResourceCost cost)
        {
            return _commanderResource >= cost.CommanderResource &&
                   _crystal >= cost.Crystal &&
                   _influenceCap - _influenceUsed >= cost.Influence;
        }

        public bool TrySpend(ResourceCost cost)
        {
            if (!CanAfford(cost))
            {
                return false;
            }

            if (cost.IsZero)
            {
                return true;
            }

            ResourceSnapshot previous = Snapshot;
            _commanderResource -= cost.CommanderResource;
            _crystal -= cost.Crystal;
            _influenceUsed += cost.Influence;
            Publish(ResourceChangeReason.Spent, previous);
            return true;
        }

        public bool ReserveInfluence(int amount)
        {
            if (amount < 0 || _influenceCap - _influenceUsed < amount)
            {
                return false;
            }

            if (amount == 0)
            {
                return true;
            }

            ResourceSnapshot previous = Snapshot;
            _influenceUsed += amount;
            Publish(ResourceChangeReason.InfluenceReserved, previous);
            return true;
        }

        public bool ReleaseInfluence(int amount)
        {
            if (amount < 0 || amount > _influenceUsed)
            {
                return false;
            }

            if (amount == 0)
            {
                return true;
            }

            ResourceSnapshot previous = Snapshot;
            _influenceUsed -= amount;
            Publish(ResourceChangeReason.InfluenceReleased, previous);
            return true;
        }

        public bool Refund(ResourceCost cost)
        {
            if (cost.Influence > _influenceUsed)
            {
                return false;
            }

            if (cost.IsZero)
            {
                return true;
            }

            ResourceSnapshot previous = Snapshot;
            _commanderResource = SaturatingAdd(
                _commanderResource,
                cost.CommanderResource);
            _crystal = SaturatingAdd(_crystal, cost.Crystal);
            _influenceUsed -= cost.Influence;
            Publish(ResourceChangeReason.Refunded, previous);
            return true;
        }

        public void Restore(ResourceSnapshot snapshot)
        {
            ResourceSnapshot previous = Snapshot;
            _commanderResource = snapshot.CommanderResource;
            _crystal = snapshot.Crystal;
            _influenceUsed = snapshot.InfluenceUsed;
            _influenceCap = snapshot.InfluenceCap;
            Publish(ResourceChangeReason.Restored, previous);
        }

        private void Publish(ResourceChangeReason reason, ResourceSnapshot previous)
        {
            ResourceChanged?.Invoke(new ResourceChanged(reason, previous, Snapshot));
        }

        private static int SaturatingAdd(int current, int amount)
        {
            long sum = (long)current + amount;
            return sum >= int.MaxValue ? int.MaxValue : (int)sum;
        }
    }
}
