using OriginCore.Core;
using OriginCore.Gameplay;
using OriginCore.Visibility;
using UnityEngine;

namespace OriginCore.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EntityIdentity), typeof(FactionMember))]
    public sealed class AutoTargetScanner : MonoBehaviour
    {
        private const int OverlapBufferSize = 64;

        [SerializeField] private EntityIdentity _identity;
        [SerializeField] private FactionMember _faction;
        [SerializeField] private AttackCapability _attackCapability;
        [SerializeField] private FactionRelationService _factionRelations;
        [SerializeField] private LayerMask _targetMask = ~0;

        private readonly Collider[] _overlapBuffer = new Collider[OverlapBufferSize];

        public EntityIdentity Identity => _identity;
        public FactionMember Faction => _faction;
        public AttackCapability AttackCapability => _attackCapability;
        public FactionRelationService FactionRelations => _factionRelations;
        public LayerMask TargetMask => _targetMask;
        public int ScanCount { get; private set; }
        public int LastHitCount { get; private set; }

        private void Awake()
        {
            CacheComponents();
            TryBindFactionRelations();
        }

        private void OnEnable()
        {
            CacheComponents();
            TryBindFactionRelations();
        }

        private void OnValidate()
        {
            CacheComponents();
        }

        public void Configure(
            EntityIdentity identity,
            FactionMember faction,
            AttackCapability attackCapability,
            FactionRelationService factionRelations,
            LayerMask targetMask)
        {
            _identity = identity != null ? identity : GetComponent<EntityIdentity>();
            _faction = faction != null ? faction : GetComponent<FactionMember>();
            _attackCapability = attackCapability != null
                ? attackCapability
                : GetComponent<AttackCapability>();
            _factionRelations = factionRelations;
            _targetMask = targetMask;
        }

        public bool FindNearestHostile(float radius, out EntityIdentity target)
        {
            target = null;
            CacheComponents();
            TryBindFactionRelations();
            if (_identity == null || _faction == null || _factionRelations == null ||
                radius <= 0f)
            {
                LastHitCount = 0;
                return false;
            }

            ScanCount++;
            LastHitCount = Physics.OverlapSphereNonAlloc(
                transform.position,
                Mathf.Max(0.1f, radius),
                _overlapBuffer,
                _targetMask,
                QueryTriggerInteraction.Collide);
            float nearestSqrDistance = float.PositiveInfinity;
            int nearestInstanceId = int.MaxValue;
            for (int i = 0; i < LastHitCount; i++)
            {
                Collider hit = _overlapBuffer[i];
                EntityIdentity candidate = hit != null
                    ? hit.GetComponentInParent<EntityIdentity>()
                    : null;
                if (!IsValidVisibleHostile(candidate))
                {
                    continue;
                }

                Vector3 delta = candidate.transform.position - transform.position;
                delta.y = 0f;
                float sqrDistance = delta.sqrMagnitude;
                int instanceId = candidate.GetInstanceID();
                if (sqrDistance > nearestSqrDistance ||
                    Mathf.Approximately(sqrDistance, nearestSqrDistance) &&
                    instanceId >= nearestInstanceId)
                {
                    continue;
                }

                nearestSqrDistance = sqrDistance;
                nearestInstanceId = instanceId;
                target = candidate;
            }

            return target != null;
        }

        public bool IsValidVisibleHostile(EntityIdentity target)
        {
            if (target == null || target == _identity || !target.isActiveAndEnabled)
            {
                return false;
            }

            VisibilityTarget visibilityTarget = target.GetComponent<VisibilityTarget>();
            if (visibilityTarget != null && _faction != null &&
                !visibilityTarget.IsVisibleTo(_faction.Faction))
            {
                return false;
            }

            if (_attackCapability != null)
            {
                return _attackCapability.IsValidHostileTarget(target) &&
                       _attackCapability.HasLineOfSight(target);
            }

            FactionMember targetFaction = target.GetComponent<FactionMember>();
            VitalsComponent targetVitals = target.GetComponent<VitalsComponent>();
            return targetFaction != null && targetVitals != null && targetVitals.IsAlive &&
                   _factionRelations != null && _faction != null &&
                   _factionRelations.AreHostile(_faction.Faction, targetFaction.Faction);
        }

        private void CacheComponents()
        {
            if (_identity == null)
            {
                _identity = GetComponent<EntityIdentity>();
            }

            if (_faction == null)
            {
                _faction = GetComponent<FactionMember>();
            }

            if (_attackCapability == null)
            {
                _attackCapability = GetComponent<AttackCapability>();
            }
        }

        private bool TryBindFactionRelations()
        {
            if (_factionRelations != null)
            {
                return true;
            }

            if (!AppRoot.TryGetInstance(out AppRoot appRoot) || appRoot.Services == null)
            {
                return false;
            }

            _factionRelations = appRoot.Services.FactionRelations;
            return _factionRelations != null;
        }
    }
}
