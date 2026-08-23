using System;
using UnityEngine;

namespace OriginCore.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class FactionMember : MonoBehaviour
    {
        [SerializeField] private FactionId _faction = FactionId.Friendly;

        public event Action<FactionMember, FactionId, FactionId> FactionChanged;

        public FactionId Faction => _faction;

        public bool SetFaction(FactionId faction)
        {
            if (_faction == faction)
            {
                return false;
            }

            FactionId previous = _faction;
            _faction = faction;
            FactionChanged?.Invoke(this, previous, _faction);
            return true;
        }
    }
}
