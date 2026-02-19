using System;
using System.Collections.Generic;
using UnityEngine;

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

    readonly Collider[] _overlapBuffer = new Collider[32];
    readonly RaycastHit[] _raycastBuffer = new RaycastHit[32];
    readonly List<ICombatSkill> _skills = new List<ICombatSkill>(8);
    readonly Dictionary<string, ICombatSkill> _skillMap =
        new Dictionary<string, ICombatSkill>(StringComparer.OrdinalIgnoreCase);

    float _nextAttackTime;

    public float AttackRange => attackRange;
    public float DetectionRange => detectionRange;
    public bool IsReady => Time.time >= _nextAttackTime;
    public IReadOnlyList<ICombatSkill> Skills => _skills;

    public event Action<Health, float> OnDamageApplied;
    public event Action<string> OnSkillUsed;

    void Awake()
    {
        RefreshSkills();
    }

    void OnEnable()
    {
        RefreshSkills();
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

    public bool IsTargetInRange(Transform target, float extraRange = 0f)
    {
        if (target == null) return false;

        Vector3 a = transform.position;
        Vector3 b = target.position;
        a.y = 0f;
        b.y = 0f;

        return Vector3.Distance(a, b) <= attackRange + Mathf.Max(0f, extraRange);
    }

    public bool IsTargetInDetectionRange(Transform target, float extraRange = 0f)
    {
        if (target == null) return false;

        Vector3 a = transform.position;
        Vector3 b = target.position;
        a.y = 0f;
        b.y = 0f;

        return Vector3.Distance(a, b) <= detectionRange + Mathf.Max(0f, extraRange);
    }

    public Transform FindNearestTargetInDetectionRange()
    {
        Vector3 origin = transform.position;
        int count = Physics.OverlapSphereNonAlloc(
            origin,
            detectionRange,
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
            TryApplyDamage(hit, damage);

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

        Health h = hitComponent.GetComponentInParent<Health>();
        if (h == null) return null;
        if (h.gameObject == gameObject) return null;

        return h;
    }

    Vector3 GetAttackOrigin()
    {
        if (attackOrigin != null)
            return attackOrigin.position;

        return transform.position + Vector3.up * defaultOriginHeight;
    }
}
