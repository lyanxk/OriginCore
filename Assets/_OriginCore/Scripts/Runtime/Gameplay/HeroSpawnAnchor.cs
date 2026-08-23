using OriginCore.Buildings;
using OriginCore.Combat;
using UnityEngine;

namespace OriginCore.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EntityIdentity))]
    public sealed class HeroSpawnAnchor : MonoBehaviour
    {
        [SerializeField] private Transform _anchorPoint;
        [SerializeField] private bool _supportsInitialSpawn = true;

        public Transform AnchorPoint => _anchorPoint != null ? _anchorPoint : transform;
        public bool SupportsInitialSpawn => _supportsInitialSpawn;

        public bool IsAvailableFor(FactionId faction)
        {
            if (!isActiveAndEnabled)
            {
                return false;
            }

            FactionMember member = GetComponent<FactionMember>();
            if (member != null && member.Faction != faction)
            {
                return false;
            }

            VitalsComponent vitals = GetComponent<VitalsComponent>();
            if (vitals != null && !vitals.IsAlive)
            {
                return false;
            }

            BuildingRuntime building = GetComponent<BuildingRuntime>();
            return building == null || building.IsOperational;
        }

        public void Configure(Transform anchorPoint, bool supportsInitialSpawn)
        {
            _anchorPoint = anchorPoint;
            _supportsInitialSpawn = supportsInitialSpawn;
        }
    }
}
