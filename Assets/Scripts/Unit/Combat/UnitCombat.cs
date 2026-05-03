using Gameplay;
using System;
using System.Collections.Generic;
using Unit.Command;
using Unit.Movement;
using UnityEngine;
namespace Unit.Combat
{
    [DisallowMultipleComponent]
    public class UnitCombat : MonoBehaviour
    {
        public enum AttackPattern
        {
            MeleeSphere = 0,
            Hitscan = 1
        }

        [Header("Primary Attack")]
        public AttackPattern pattern = AttackPattern.MeleeSphere;
        [Min(0f)] public float damage = 10f;
        [Min(0.05f)] public float attackCooldown = 0.4f;
        [Min(0.1f)] public float attackRange = 2f;
        public LayerMask targetMask = ~0;
        public bool consumeCooldownOnMiss = true;

        [Header("Targeting")]
        [Min(0.1f)] public float detectionRange = 8f;

        [Header("Melee Sphere")]
        [Min(0.1f)] public float meleeRadius = 0.9f;

        [Header("Origin")]
        public Transform attackOrigin;
        public float defaultOriginHeight = 1.0f;

        [Header("Team")]
        [SerializeField] TeamAffiliation teamAffiliation;

        [Header("Auto Targeting")]
        public bool autoChaseTargets = true;
        [Min(1f)] public float loseTargetRangeMultiplier = 2f;
        [Min(0.05f)] public float targetScanInterval = 0.15f;
        [Min(0.05f)] public float chaseRepathInterval = 0.2f;
        [Min(0.05f)] public float chaseRepathDistance = 0.5f;

        readonly Collider[] _overlapBuffer = new Collider[32];
        readonly RaycastHit[] _raycastBuffer = new RaycastHit[32];
        readonly List<ICombatSkill> _skills = new List<ICombatSkill>(8);
        readonly Dictionary<string, ICombatSkill> _skillMap =
            new Dictionary<string, ICombatSkill>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<object, float> _damageMultipliers = new Dictionary<object, float>(4);

        float _nextAttackTime;
        float _cachedDamageMultiplier = 1f;
        UnitBase _motor;
        CommandExecutor _commandExecutor;
        Transform _lockedTarget;
        Vector3 _lastChasePosition;
        float _scanTimer;
        float _chaseTimer;
        bool _hasChasePosition;
        bool _isAutoChasing;

        public float AttackRange => attackRange;
        public float DetectionRange => detectionRange;
        public bool IsReady => Time.time >= _nextAttackTime;
        public IReadOnlyList<ICombatSkill> Skills => _skills;
        public TeamAffiliation TeamAffiliation => teamAffiliation;

        public event Action<Health, float> OnDamageApplied;
        public event Action<string> OnSkillUsed;

        void Awake()
        {
            CacheReferences();
            RefreshSkills();
        }

        void OnEnable()
        {
            CacheReferences();
            RefreshSkills();
        }

        void OnValidate()
        {
            CacheReferences();
        }

        void Update()
        {
            TickAutoCombat(Time.deltaTime);
        }

        public void RefreshSkills()
        {
            _skills.Clear();
            _skillMap.Clear();

            var mbs = GetComponents<MonoBehaviour>();
            for (int i = 0; i < mbs.Length; i++)
            {
                if (mbs[i] is not ICombatSkill skill)
                    continue;

                if (string.IsNullOrWhiteSpace(skill.SkillId))
                    continue;

                if (_skillMap.ContainsKey(skill.SkillId))
                {
                    Debug.LogWarning(
                        $"Duplicate combat skill id '{skill.SkillId}' on '{name}'. Keeping first registration.",
                        this);
                    continue;
                }

                _skills.Add(skill);
                _skillMap.Add(skill.SkillId, skill);
            }
        }

        public bool HasSkill(string skillId)
        {
            if (string.IsNullOrWhiteSpace(skillId)) return false;
            return _skillMap.ContainsKey(skillId);
        }

        public bool TryUseSkill(string skillId, CombatSkillRequest request)
        {
            if (string.IsNullOrWhiteSpace(skillId))
                return false;

            if (!_skillMap.TryGetValue(skillId, out ICombatSkill skill))
                return false;

            bool used = skill.TryUseSkill(this, request);
            if (used)
                OnSkillUsed?.Invoke(skillId);

            return used;
        }

        public bool TryUsePrimaryOnTarget(Transform target)
        {
            return TryAttackTarget(target);
        }

        public bool TryUsePrimaryInDirection(Vector3 direction)
        {
            return TryAttackDirection(direction);
        }

        public void SetDamageMultiplier(object source, float multiplier)
        {
            if (source == null)
                return;

            if (multiplier <= 1f)
            {
                ClearDamageMultiplier(source);
                return;
            }

            _damageMultipliers[source] = multiplier;
            RecalculateDamageMultiplier();
        }

        public void ClearDamageMultiplier(object source)
        {
            if (source == null || !_damageMultipliers.Remove(source))
                return;

            RecalculateDamageMultiplier();
        }

        public bool IsTargetInRange(Transform target, float extraRange = 0f)
        {
            return IsTargetWithinRange(target, attackRange + Mathf.Max(0f, extraRange));
        }

        public bool IsTargetInDetectionRange(Transform target, float extraRange = 0f)
        {
            return IsTargetWithinRange(target, detectionRange + Mathf.Max(0f, extraRange));
        }

        public bool CanKeepTargetLocked(Transform target)
        {
            return IsTargetWithinRange(target, detectionRange * Mathf.Max(1f, loseTargetRangeMultiplier));
        }

        public Transform FindNearestTargetInDetectionRange()
        {
            return FindNearestTarget(detectionRange);
        }

        public Transform FindNearestTargetInAttackRange()
        {
            return FindNearestTarget(attackRange);
        }

        Transform FindNearestTarget(float searchRange)
        {
            Vector3 origin = transform.position;
            int count = Physics.OverlapSphereNonAlloc(
                origin,
                Mathf.Max(0.1f, searchRange),
                _overlapBuffer,
                targetMask,
                QueryTriggerInteraction.Ignore);

            Health best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                Health h = ResolveHealth(_overlapBuffer[i]);
                if (h == null) continue;

                Vector3 d = h.transform.position - origin;
                d.y = 0f;
                float sqr = d.sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = h;
                }
            }

            return best != null ? best.transform : null;
        }

        public bool TryAttackTarget(Transform target)
        {
            if (target == null) return false;

            Vector3 dir = target.position - GetAttackOrigin();
            if (dir.sqrMagnitude < 1e-6f)
                dir = transform.forward;

            return TryAttackInternal(dir.normalized, target);
        }

        public bool TryAttackDirection(Vector3 direction)
        {
            if (direction.sqrMagnitude < 1e-6f)
                direction = transform.forward;

            return TryAttackInternal(direction.normalized, null);
        }

        public bool TryApplyDamage(Health targetHealth, float amount)
        {
            if (targetHealth == null) return false;
            if (amount <= 0f) return false;

            targetHealth.TakeDamage(amount);
            OnDamageApplied?.Invoke(targetHealth, amount);
            return true;
        }

        bool TryAttackInternal(Vector3 direction, Transform preferredTarget)
        {
            if (!IsReady)
                return false;

            Health hit = pattern == AttackPattern.Hitscan
                ? DoHitscan(direction, preferredTarget)
                : DoMeleeSphere(direction, preferredTarget);

            bool didHit = hit != null;
            if (didHit)
                TryApplyDamage(hit, damage * _cachedDamageMultiplier);

            if (didHit || consumeCooldownOnMiss)
                _nextAttackTime = Time.time + attackCooldown;

            return didHit;
        }

        Health DoMeleeSphere(Vector3 direction, Transform preferredTarget)
        {
            Vector3 origin = GetAttackOrigin();
            float centerOffset = Mathf.Max(0f, attackRange - meleeRadius * 0.5f);
            Vector3 center = origin + direction * centerOffset;

            int count = Physics.OverlapSphereNonAlloc(
                center,
                meleeRadius,
                _overlapBuffer,
                targetMask,
                QueryTriggerInteraction.Ignore);

            return FindBestHealthFromOverlap(count, center, preferredTarget);
        }

        Health DoHitscan(Vector3 direction, Transform preferredTarget)
        {
            Vector3 origin = GetAttackOrigin();
            Ray ray = new Ray(origin, direction);

            int count = Physics.RaycastNonAlloc(
                ray,
                _raycastBuffer,
                attackRange,
                targetMask,
                QueryTriggerInteraction.Ignore);

            return FindBestHealthFromRaycast(count, preferredTarget);
        }

        Health FindBestHealthFromOverlap(int count, Vector3 center, Transform preferredTarget)
        {
            Health best = null;
            float bestSqr = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                Collider c = _overlapBuffer[i];
                Health h = ResolveHealth(c);
                if (h == null) continue;

                if (preferredTarget != null && h.transform == preferredTarget)
                    return h;

                float sqr = (h.transform.position - center).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    best = h;
                    bestSqr = sqr;
                }
            }

            return best;
        }

        Health FindBestHealthFromRaycast(int count, Transform preferredTarget)
        {
            Health best = null;
            float bestDist = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _raycastBuffer[i];
                Health h = ResolveHealth(hit.collider);
                if (h == null) continue;

                if (preferredTarget != null && h.transform == preferredTarget)
                    return h;

                if (hit.distance < bestDist)
                {
                    best = h;
                    bestDist = hit.distance;
                }
            }

            return best;
        }

        Health ResolveHealth(Component hitComponent)
        {
            if (hitComponent == null) return null;

            if (!Health.TryResolve(hitComponent, out Health h))
                return null;

            if (h == null) return null;
            if (h.transform.root == transform.root) return null;
            if (!CanAttackHealth(h)) return null;

            return h;
        }

        Vector3 GetAttackOrigin()
        {
            if (attackOrigin != null)
                return attackOrigin.position;

            return transform.position + Vector3.up * defaultOriginHeight;
        }

        void CacheReferences()
        {
            teamAffiliation = ResolveNearbyComponent(teamAffiliation);
            _motor = ResolveNearbyComponent(_motor);
            _commandExecutor = ResolveNearbyComponent(_commandExecutor);
        }

        bool CanAttackHealth(Health targetHealth)
        {
            if (targetHealth == null)
                return false;

            if (teamAffiliation == null)
                return false;

            TeamAffiliation targetTeam = targetHealth.TeamAffiliation;

            if (targetTeam == null)
                return false;

            return teamAffiliation.IsHostileTo(targetTeam);
        }

        void RecalculateDamageMultiplier()
        {
            // Damage buffs also use the strongest source to avoid runaway scaling from overlapping support units.
            _cachedDamageMultiplier = 1f;
            foreach (KeyValuePair<object, float> modifier in _damageMultipliers)
                _cachedDamageMultiplier = Mathf.Max(_cachedDamageMultiplier, modifier.Value);
        }

        void TickAutoCombat(float dt)
        {
            if (!CanRunAutoCombat())
            {
                ClearLockedTarget();
                StopAutoChase();
                return;
            }

            if (!CanKeepTargetLocked(_lockedTarget))
                ClearLockedTarget();

            TryAcquireTarget(dt);
            if (_lockedTarget == null)
            {
                StopAutoChase();
                return;
            }

            FaceTarget(_lockedTarget.position);
            if (IsTargetInRange(_lockedTarget))
            {
                StopAutoChase();
                TryUsePrimaryOnTarget(_lockedTarget);
                return;
            }

            _chaseTimer -= dt;
            Vector3 chasePosition = _lockedTarget.position;
            bool targetMoved = !_hasChasePosition
                               || FlattenedSqr(chasePosition - _lastChasePosition)
                               >= chaseRepathDistance * chaseRepathDistance;
            if (_chaseTimer > 0f && !targetMoved)
                return;

            _motor.SetDestination(chasePosition);
            _lastChasePosition = chasePosition;
            _chaseTimer = chaseRepathInterval;
            _hasChasePosition = true;
            _isAutoChasing = true;
        }

        bool CanRunAutoCombat()
        {
            return autoChaseTargets
                   && teamAffiliation != null
                   && _motor != null
                   && (_commandExecutor == null || _commandExecutor.IsIdle);
        }

        void TryAcquireTarget(float dt)
        {
            if (_lockedTarget != null)
                return;

            _scanTimer -= dt;
            if (_scanTimer > 0f)
                return;

            _scanTimer = targetScanInterval;
            _lockedTarget = FindNearestTargetInDetectionRange();
            if (_lockedTarget == null)
                return;

            _chaseTimer = 0f;
            _hasChasePosition = false;
        }

        void ClearLockedTarget()
        {
            _lockedTarget = null;
            _scanTimer = 0f;
        }

        void StopAutoChase()
        {
            if (_isAutoChasing && _motor != null && (_commandExecutor == null || _commandExecutor.IsIdle))
                _motor.CancelPathing();

            _isAutoChasing = false;
            _hasChasePosition = false;
        }

        void FaceTarget(Vector3 targetPosition)
        {
            if (_motor == null)
                return;

            Vector3 direction = targetPosition - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 1e-6f)
                return;

            float yaw = Quaternion.LookRotation(direction, Vector3.up).eulerAngles.y;
            _motor.SetYaw(yaw);
        }

        bool IsTargetWithinRange(Transform target, float range)
        {
            if (target == null)
                return false;

            Vector3 delta = target.position - transform.position;
            delta.y = 0f;
            return delta.sqrMagnitude <= range * range;
        }

        T ResolveNearbyComponent<T>(T current) where T : Component
        {
            if (current != null)
                return current;

            T resolved = GetComponent<T>();
            if (resolved != null)
                return resolved;

            return GetComponentInParent<T>();
        }

        static float FlattenedSqr(Vector3 delta)
        {
            delta.y = 0f;
            return delta.sqrMagnitude;
        }
    }
}
