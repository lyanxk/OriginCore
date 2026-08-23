using System;
using System.Collections;
using OriginCore.Core;
using OriginCore.Gameplay;
using OriginCore.Visibility;
using UnityEngine;

namespace OriginCore.Combat
{
    public enum AttackAttemptResult
    {
        Succeeded = 0,
        Cooldown = 1,
        InvalidTarget = 2,
        OutOfRange = 3,
        NoLineOfSight = 4,
        NoDamageApplied = 5
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(EntityIdentity), typeof(FactionMember))]
    public sealed class AttackCapability : MonoBehaviour
    {
        private const int LineOfSightBufferSize = 32;
        private const float DefaultAttackOriginHeight = 1f;
        private const float AttackMoveLeashExtraRange = 2f;

        [SerializeField] private EntityIdentity _identity;
        [SerializeField] private FactionMember _faction;
        [SerializeField] private VitalsComponent _vitals;
        [SerializeField] private RuntimeStatBlock _statBlock;
        [SerializeField] private FactionRelationService _factionRelations;
        [SerializeField] private Transform _attackOrigin;
        [SerializeField] private LayerMask _lineOfSightMask = ~0;
        [SerializeField] private bool _requiresLineOfSight = true;
        [Min(1), SerializeField] private int _hitCount = 1;
        [Min(0f), SerializeField] private float _hitInterval;
        [SerializeField] private DamageType _damageType = DamageType.Physical;
        [SerializeField] private DamageFlags _damageFlags;
        [SerializeField] private bool _canAttackGround = true;
        [SerializeField] private bool _canAttackAir = true;
        [Min(0f), SerializeField] private float _bonusDamageToAir;
        [Min(0f), SerializeField] private float _splashRadius;

        private readonly RaycastHit[] _lineOfSightHits =
            new RaycastHit[LineOfSightBufferSize];
        private readonly Collider[] _splashHits = new Collider[64];
        private readonly System.Collections.Generic.HashSet<EntityIdentity> _splashTargets =
            new System.Collections.Generic.HashSet<EntityIdentity>();
        private float _cooldownRemaining;

        public event Action<AttackCapability, EntityIdentity, DamageResult> AttackPerformed;

        public EntityIdentity Identity => _identity;
        public FactionMember Faction => _faction;
        public VitalsComponent Vitals => _vitals;
        public FactionRelationService FactionRelations => _factionRelations;
        public Transform AttackOrigin => _attackOrigin;
        public LayerMask LineOfSightMask => _lineOfSightMask;
        public bool RequiresLineOfSight => _requiresLineOfSight;
        public float AttackRange => ResolveStat(RuntimeStatId.AttackRange, Definition != null ? Definition.AttackRange : 0f);
        public float AttackDamage => ResolveStat(RuntimeStatId.AttackDamage, Definition != null ? Definition.AttackDamage : 0f);
        public float AttackCooldown => ResolveStat(RuntimeStatId.AttackInterval, Definition != null ? Definition.AttackCooldown : 0f);
        public float VisionRange => ResolveStat(RuntimeStatId.VisionRange, Definition != null ? Definition.VisionRange : 0f);
        public float AggroRadius => Mathf.Max(
            AttackRange,
            Mathf.Min(VisionRange, AttackRange + AttackMoveLeashExtraRange));
        public float CooldownRemaining => _cooldownRemaining;
        public bool IsReady => _cooldownRemaining <= 0f;
        public int SuccessfulAttackCount { get; private set; }

        private UnitDefinition Definition => _identity != null ? _identity.Definition : null;

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
            _hitCount = Mathf.Max(1, _hitCount);
            _hitInterval = Mathf.Max(0f, _hitInterval);
            _bonusDamageToAir = Mathf.Max(0f, _bonusDamageToAir);
            _splashRadius = Mathf.Max(0f, _splashRadius);
        }

        private void OnDisable()
        {
            StopAllCoroutines();
        }

        private void Update()
        {
            AdvanceCooldown(Time.deltaTime);
        }

        public void Configure(
            EntityIdentity identity,
            FactionMember faction,
            VitalsComponent vitals,
            FactionRelationService factionRelations,
            Transform attackOrigin,
            LayerMask lineOfSightMask,
            bool requiresLineOfSight)
        {
            _identity = identity != null ? identity : GetComponent<EntityIdentity>();
            _faction = faction != null ? faction : GetComponent<FactionMember>();
            _vitals = vitals != null ? vitals : GetComponent<VitalsComponent>();
            _factionRelations = factionRelations;
            _attackOrigin = attackOrigin;
            _lineOfSightMask = lineOfSightMask;
            _requiresLineOfSight = requiresLineOfSight;
        }

        public void AdvanceCooldown(float deltaTime)
        {
            if (_cooldownRemaining <= 0f)
            {
                _cooldownRemaining = 0f;
                return;
            }

            float cooldownRate = ResolveStat(
                RuntimeStatId.CooldownRate,
                Definition != null ? Definition.CooldownRate : 1f);
            _cooldownRemaining = Mathf.Max(
                0f,
                _cooldownRemaining - Mathf.Max(0f, deltaTime) * cooldownRate);
        }

        public void ConfigurePattern(
            int hitCount,
            float hitInterval,
            DamageType damageType = DamageType.Physical,
            DamageFlags damageFlags = DamageFlags.None)
        {
            _hitCount = Mathf.Max(1, hitCount);
            _hitInterval = Mathf.Max(0f, hitInterval);
            _damageType = damageType;
            _damageFlags = damageFlags;
        }

        public void ConfigureTargeting(
            bool canAttackGround,
            bool canAttackAir,
            float bonusDamageToAir = 0f,
            float splashRadius = 0f)
        {
            _canAttackGround = canAttackGround;
            _canAttackAir = canAttackAir;
            _bonusDamageToAir = Mathf.Max(0f, bonusDamageToAir);
            _splashRadius = Mathf.Max(0f, splashRadius);
        }

        public bool IsValidHostileTarget(EntityIdentity target)
        {
            CacheComponents();
            TryBindFactionRelations();
            if (_identity == null || _faction == null || _factionRelations == null ||
                target == null || target == _identity || !target.isActiveAndEnabled ||
                (_vitals != null && !_vitals.IsAlive) ||
                AttackRange <= 0f || AttackDamage <= 0f)
            {
                return false;
            }

            bool targetIsAir = target.Roles.HasAny(UnitRole.Air);
            if (targetIsAir ? !_canAttackAir : !_canAttackGround)
            {
                return false;
            }

            FactionMember targetFaction = target.GetComponent<FactionMember>();
            VitalsComponent targetVitals = target.GetComponent<VitalsComponent>();
            DamageReceiver targetReceiver = target.GetComponent<DamageReceiver>();
            VisibilityTarget visibilityTarget = target.GetComponent<VisibilityTarget>();
            return targetFaction != null && targetVitals != null && targetVitals.IsAlive &&
                   targetReceiver != null &&
                   (visibilityTarget == null ||
                    visibilityTarget.IsVisibleTo(_faction.Faction)) &&
                   _factionRelations.AreHostile(_faction.Faction, targetFaction.Faction);
        }

        public bool IsInRange(EntityIdentity target, float extraRange = 0f)
        {
            if (target == null)
            {
                return false;
            }

            float range = Mathf.Max(0f, AttackRange + extraRange);
            Vector3 delta = target.transform.position - transform.position;
            delta.y = 0f;
            return delta.sqrMagnitude <= range * range;
        }

        public float GetPlanarDistance(EntityIdentity target)
        {
            if (target == null)
            {
                return float.PositiveInfinity;
            }

            Vector3 delta = target.transform.position - transform.position;
            delta.y = 0f;
            return delta.magnitude;
        }

        public bool HasLineOfSight(EntityIdentity target)
        {
            if (!_requiresLineOfSight)
            {
                return true;
            }

            if (target == null || !target.isActiveAndEnabled)
            {
                return false;
            }

            Vector3 origin = GetAttackOriginPosition();
            Vector3 targetPoint = GetTargetPoint(target);
            Vector3 delta = targetPoint - origin;
            float distance = delta.magnitude;
            if (distance <= 0.001f)
            {
                return true;
            }

            int hitCount = Physics.RaycastNonAlloc(
                new Ray(origin, delta / distance),
                _lineOfSightHits,
                distance + 0.05f,
                _lineOfSightMask,
                QueryTriggerInteraction.Ignore);
            Collider nearestBlocker = null;
            float nearestDistance = float.PositiveInfinity;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _lineOfSightHits[i];
                if (hit.collider == null || hit.distance >= nearestDistance ||
                    IsOwnedCollider(hit.collider))
                {
                    continue;
                }

                nearestBlocker = hit.collider;
                nearestDistance = hit.distance;
            }

            if (nearestBlocker == null)
            {
                return true;
            }

            EntityIdentity hitIdentity = nearestBlocker.GetComponentInParent<EntityIdentity>();
            return hitIdentity == target;
        }

        public AttackAttemptResult TryAttack(
            EntityIdentity target,
            out DamageResult damageResult)
        {
            damageResult = DamageResult.None;
            if (!IsValidHostileTarget(target))
            {
                return AttackAttemptResult.InvalidTarget;
            }

            if (!IsInRange(target))
            {
                return AttackAttemptResult.OutOfRange;
            }

            if (!HasLineOfSight(target))
            {
                return AttackAttemptResult.NoLineOfSight;
            }

            if (!IsReady)
            {
                return AttackAttemptResult.Cooldown;
            }

            if (!TryApplyHit(target, out damageResult))
            {
                return AttackAttemptResult.NoDamageApplied;
            }

            _cooldownRemaining = Mathf.Max(0f, AttackCooldown);
            SuccessfulAttackCount++;
            if (_hitCount > 1)
            {
                StartCoroutine(ContinueAttackBurst(target, _hitCount - 1));
            }

            return AttackAttemptResult.Succeeded;
        }

        private IEnumerator ContinueAttackBurst(EntityIdentity target, int remainingHits)
        {
            for (int i = 0; i < remainingHits; i++)
            {
                if (_hitInterval > 0f)
                {
                    yield return new WaitForSeconds(_hitInterval);
                }
                else
                {
                    yield return null;
                }

                if (!IsValidHostileTarget(target) || !IsInRange(target) ||
                    !HasLineOfSight(target))
                {
                    yield break;
                }

                TryApplyHit(target, out _);
            }
        }

        private bool TryApplyHit(EntityIdentity target, out DamageResult damageResult)
        {
            bool changed = TryApplyDamage(target, out damageResult);
            if (!changed)
            {
                return false;
            }

            AttackPerformed?.Invoke(this, target, damageResult);
            ApplySplash(target);
            return true;
        }

        private bool TryApplyDamage(EntityIdentity target, out DamageResult damageResult)
        {
            DamageReceiver receiver = target != null ? target.GetComponent<DamageReceiver>() : null;
            float damage = AttackDamage +
                           (target != null && target.Roles.HasAny(UnitRole.Air)
                               ? _bonusDamageToAir
                               : 0f);
            if (receiver == null || _faction == null ||
                !receiver.TryReceiveDamage(
                    new DamageInfo(
                        damage,
                        _identity,
                        _faction.Faction,
                        _damageType,
                        _damageFlags,
                        GetTargetPoint(target),
                        true),
                    out damageResult))
            {
                damageResult = DamageResult.None;
                return false;
            }

            return true;
        }

        private void ApplySplash(EntityIdentity primaryTarget)
        {
            if (_splashRadius <= 0f || primaryTarget == null || _factionRelations == null ||
                _faction == null)
            {
                return;
            }

            _splashTargets.Clear();
            int count = Physics.OverlapSphereNonAlloc(
                primaryTarget.transform.position,
                _splashRadius,
                _splashHits,
                ~0,
                QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                EntityIdentity target = _splashHits[i] != null
                    ? _splashHits[i].GetComponentInParent<EntityIdentity>()
                    : null;
                if (target == null || target == primaryTarget || !_splashTargets.Add(target) ||
                    !IsValidHostileTarget(target))
                {
                    continue;
                }

                if (TryApplyDamage(target, out DamageResult result))
                {
                    AttackPerformed?.Invoke(this, target, result);
                }
            }
        }

        public void FaceTarget(EntityIdentity target)
        {
            if (target == null)
            {
                return;
            }

            Vector3 direction = target.transform.position - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }
        }

        public Vector3 GetTargetPoint(EntityIdentity target)
        {
            if (target == null)
            {
                return Vector3.zero;
            }

            Selectable selectable = target.GetComponent<Selectable>();
            return selectable != null
                ? selectable.GetSelectionWorldPoint()
                : target.transform.position + Vector3.up * DefaultAttackOriginHeight;
        }

        private Vector3 GetAttackOriginPosition()
        {
            return _attackOrigin != null
                ? _attackOrigin.position
                : transform.position + Vector3.up * DefaultAttackOriginHeight;
        }

        private bool IsOwnedCollider(Collider collider)
        {
            if (collider == null)
            {
                return false;
            }

            EntityIdentity hitIdentity = collider.GetComponentInParent<EntityIdentity>();
            return hitIdentity != null && hitIdentity == _identity;
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

            if (_vitals == null)
            {
                _vitals = GetComponent<VitalsComponent>();
            }

            if (_statBlock == null)
            {
                _statBlock = GetComponent<RuntimeStatBlock>();
            }
        }

        private float ResolveStat(RuntimeStatId statId, float fallback)
        {
            return _statBlock != null ? _statBlock.GetStat(statId) : fallback;
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
