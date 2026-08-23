using UnityEngine;

namespace OriginCore.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class FactionRelationService : MonoBehaviour
    {
        [SerializeField] private FactionRelation _friendlyEnemyRelation = FactionRelation.Hostile;

        public FactionRelation FriendlyEnemyRelation => _friendlyEnemyRelation;

        public FactionRelation GetRelation(FactionId first, FactionId second)
        {
            if (first == second)
            {
                return FactionRelation.Allied;
            }

            if (first == FactionId.Neutral || second == FactionId.Neutral)
            {
                return FactionRelation.Neutral;
            }

            return _friendlyEnemyRelation;
        }

        public bool AreAllied(FactionId first, FactionId second)
        {
            return GetRelation(first, second) == FactionRelation.Allied;
        }

        public bool AreHostile(FactionId first, FactionId second)
        {
            return GetRelation(first, second) == FactionRelation.Hostile;
        }

        public bool AreHostile(FactionMember first, FactionMember second)
        {
            return first != null && second != null && AreHostile(first.Faction, second.Faction);
        }

        public void ConfigureFriendlyEnemyRelation(FactionRelation relation)
        {
            _friendlyEnemyRelation = relation == FactionRelation.Neutral
                ? FactionRelation.Neutral
                : relation;
        }
    }
}
