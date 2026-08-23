using System;
using UnityEngine;

namespace OriginCore.Economy
{
    [Serializable]
    public struct ResourceCost : IEquatable<ResourceCost>
    {
        [Min(0), SerializeField] private int _commanderResource;
        [Min(0), SerializeField] private int _crystal;
        [Min(0), SerializeField] private int _influence;

        public ResourceCost(int commanderResource, int crystal, int influence)
        {
            _commanderResource = Mathf.Max(0, commanderResource);
            _crystal = Mathf.Max(0, crystal);
            _influence = Mathf.Max(0, influence);
        }

        public int CommanderResource => Mathf.Max(0, _commanderResource);
        public int Crystal => Mathf.Max(0, _crystal);
        public int Influence => Mathf.Max(0, _influence);
        public bool IsZero => CommanderResource == 0 && Crystal == 0 && Influence == 0;

        public int GetAmount(ResourceType type)
        {
            switch (type)
            {
                case ResourceType.CommanderResource:
                    return CommanderResource;
                case ResourceType.Crystal:
                    return Crystal;
                case ResourceType.Influence:
                    return Influence;
                default:
                    return 0;
            }
        }

        public bool Equals(ResourceCost other)
        {
            return CommanderResource == other.CommanderResource &&
                   Crystal == other.Crystal &&
                   Influence == other.Influence;
        }

        public override bool Equals(object obj)
        {
            return obj is ResourceCost other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = CommanderResource;
                hash = (hash * 397) ^ Crystal;
                hash = (hash * 397) ^ Influence;
                return hash;
            }
        }

        public override string ToString()
        {
            return CommanderResource + " Commander, " + Crystal +
                   " Crystal, " + Influence + " Influence";
        }
    }
}
