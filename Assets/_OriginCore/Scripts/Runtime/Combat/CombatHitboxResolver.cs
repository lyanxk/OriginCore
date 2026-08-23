using System;
using System.Collections.Generic;
using OriginCore.Content;
using OriginCore.Core;
using OriginCore.Gameplay;
using OriginCore.Visibility;
using OriginCore.Weapons;
using UnityEngine;

namespace OriginCore.Combat
{
    public readonly struct WeaponActionHitResult
    {
        public WeaponActionHitResult(
            WeaponDefinition weapon,
            WeaponActionDefinition action,
            WeaponHitDefinition hit,
            EntityIdentity target,
            DamageResult damage)
        {
            Weapon = weapon;
            Action = action;
            Hit = hit;
            Target = target;
            Damage = damage;
        }

        public WeaponDefinition Weapon { get; }
        public WeaponActionDefinition Action { get; }
        public WeaponHitDefinition Hit { get; }
        public EntityIdentity Target { get; }
        public DamageResult Damage { get; }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(EntityIdentity))]
    public sealed class CombatHitboxResolver : MonoBehaviour
    {
        private const int HitCapacity = 96;

        [SerializeField] private EntityIdentity _identity;
        [SerializeField] private FactionMember _faction;
        [SerializeField] private WeaponInventory _inventory;
        [SerializeField] private WeaponActionController _actions;
        [SerializeField] private LayerMask _hitMask = ~0;

        private readonly Collider[] _hits = new Collider[HitCapacity];
        private readonly HashSet<EntityIdentity> _targets =
            new HashSet<EntityIdentity>();

        public event Action<CombatHitboxResolver, WeaponActionHitResult> HitApplied;

        private void Awake()
        {
            CacheComponents();
        }

        private void OnValidate()
        {
            CacheComponents();
        }

        public int Resolve(
            WeaponDefinition weapon,
            WeaponActionDefinition action,
            WeaponHitDefinition hit,
            WeaponActionIntent intent)
        {
            return Resolve(weapon, action, hit, intent, false, Vector3.zero);
        }

        public int Resolve(
            WeaponDefinition weapon,
            WeaponActionDefinition action,
            WeaponHitDefinition hit,
            WeaponActionIntent intent,
            bool hasCenterOverride,
            Vector3 centerOverride)
        {
            CacheComponents();
            if (weapon == null || action == null || hit == null || _identity == null)
            {
                return 0;
            }

            Vector3 forward = ResolveForward(intent);
            Vector3 center = hasCenterOverride
                ? centerOverride
                : hit.CenterOnTarget && intent.HasTarget
                    ? intent.Target.transform.TransformPoint(hit.LocalOffset)
                    : transform.TransformPoint(hit.LocalOffset);
            int count = Query(hit, center, forward, action.Motion);
            _targets.Clear();
            int applied = 0;
            for (int i = 0; i < count; i++)
            {
                Collider candidate = _hits[i];
                DamageReceiver receiver = candidate != null
                    ? candidate.GetComponentInParent<DamageReceiver>()
                    : null;
                EntityIdentity target = receiver != null
                    ? receiver.GetComponent<EntityIdentity>()
                    : null;
                if (target == null || target == _identity ||
                    !_targets.Add(target) || !IsHostile(target) ||
                    (hit.OnlyIntentTarget &&
                     (!intent.HasTarget || target != intent.Target)) ||
                    !MatchesArc(hit, forward, target.transform.position))
                {
                    continue;
                }

                DamageResult damage = DamageResult.None;
                float sourceMultiplier = hit.OverrideLockedTargetDamage &&
                                         intent.HasTarget && target == intent.Target
                    ? hit.LockedTargetDamageMultiplier
                    : hit.DamageMultiplier;
                float amount = weapon.Damage * sourceMultiplier;
                WeaponDefinition activeMain = _actions != null &&
                                              _actions.CurrentAction == action
                    ? _actions.MainWeaponAtStart
                    : _inventory != null ? _inventory.ActiveWeapon : null;
                if (activeMain != null && activeMain != weapon &&
                    hit.ActiveMainWeaponDamageMultiplier > 0f)
                {
                    amount += activeMain.Damage * hit.ActiveMainWeaponDamageMultiplier;
                }
                if (amount > 0f)
                {
                    receiver.TryReceiveDamage(
                        new DamageInfo(
                            amount,
                            _identity,
                            _faction != null ? _faction.Faction : FactionId.Neutral,
                            hit.DamageType,
                            hit.DamageFlags,
                            target.transform.position,
                            true,
                            action.ActionId,
                            weapon.ContentId),
                        out damage);
                }

                ApplyStatuses(hit, target);
                applied++;
                HitApplied?.Invoke(
                    this,
                    new WeaponActionHitResult(
                        weapon,
                        action,
                        hit,
                        target,
                        damage));
            }

            return applied;
        }

        private int Query(
            WeaponHitDefinition hit,
            Vector3 center,
            Vector3 forward,
            WeaponActionMotionDefinition motion)
        {
            Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up);
            switch (hit.Shape)
            {
                case WeaponHitShape.Box:
                    return Physics.OverlapBoxNonAlloc(
                        center,
                        hit.Size * 0.5f,
                        _hits,
                        rotation,
                        _hitMask,
                        QueryTriggerInteraction.Collide);
                case WeaponHitShape.Capsule:
                {
                    float radius = Mathf.Max(0.05f, hit.Radius);
                    float halfLine = Mathf.Max(0f, hit.Size.y * 0.5f - radius);
                    Vector3 first = center + Vector3.up * halfLine;
                    Vector3 second = center - Vector3.up * halfLine;
                    return Physics.OverlapCapsuleNonAlloc(
                        first,
                        second,
                        radius,
                        _hits,
                        _hitMask,
                        QueryTriggerInteraction.Collide);
                }
                case WeaponHitShape.PathSweep:
                {
                    float radius = Mathf.Max(0.05f, hit.Radius);
                    float distance = motion != null
                        ? Mathf.Max(hit.Size.z, motion.Distance)
                        : hit.Size.z;
                    Vector3 end = center + forward * Mathf.Max(0f, distance);
                    return Physics.OverlapCapsuleNonAlloc(
                        center,
                        end,
                        radius,
                        _hits,
                        _hitMask,
                        QueryTriggerInteraction.Collide);
                }
                default:
                    return Physics.OverlapSphereNonAlloc(
                        center,
                        Mathf.Max(0.05f, hit.Radius),
                        _hits,
                        _hitMask,
                        QueryTriggerInteraction.Collide);
            }
        }

        private bool MatchesArc(
            WeaponHitDefinition hit,
            Vector3 forward,
            Vector3 targetPosition)
        {
            if (hit.Shape != WeaponHitShape.ForwardArc || hit.ArcDegrees >= 359.9f)
            {
                return true;
            }

            Vector3 direction = Vector3.ProjectOnPlane(
                targetPosition - transform.position,
                Vector3.up);
            if (direction.sqrMagnitude <= 0.000001f)
            {
                return true;
            }

            float minimumDot = Mathf.Cos(hit.ArcDegrees * 0.5f * Mathf.Deg2Rad);
            return Vector3.Dot(forward, direction.normalized) >= minimumDot;
        }

        private void ApplyStatuses(WeaponHitDefinition hit, EntityIdentity target)
        {
            WeaponStatusDefinition[] statuses = hit.Statuses;
            if (statuses == null || statuses.Length == 0)
            {
                return;
            }

            CombatStatusController controller =
                target.GetComponent<CombatStatusController>();
            if (controller == null)
            {
                return;
            }

            for (int i = 0; i < statuses.Length; i++)
            {
                WeaponStatusDefinition status = statuses[i];
                if (status == null)
                {
                    continue;
                }

                Vector3 direction = ResolveStatusDirection(status.Type, target);
                controller.Apply(status, _identity, direction);
            }
        }

        private Vector3 ResolveStatusDirection(
            WeaponStatusType type,
            EntityIdentity target)
        {
            switch (type)
            {
                case WeaponStatusType.Pull:
                    return transform.position - target.transform.position;
                case WeaponStatusType.Launch:
                case WeaponStatusType.LaunchPair:
                    return Vector3.up;
                case WeaponStatusType.GroundSlam:
                    return Vector3.down;
                default:
                    return target.transform.position - transform.position;
            }
        }

        private Vector3 ResolveForward(WeaponActionIntent intent)
        {
            if (intent.HasTarget)
            {
                Vector3 toTarget = Vector3.ProjectOnPlane(
                    intent.Target.transform.position - transform.position,
                    Vector3.up);
                if (toTarget.sqrMagnitude > 0.000001f)
                {
                    return toTarget.normalized;
                }
            }

            Vector3 inputDirection =
                transform.forward * intent.Move.y + transform.right * intent.Move.x;
            inputDirection = Vector3.ProjectOnPlane(inputDirection, Vector3.up);
            return inputDirection.sqrMagnitude > 0.000001f
                ? inputDirection.normalized
                : transform.forward;
        }

        private bool IsHostile(EntityIdentity target)
        {
            if (target == null || _faction == null ||
                !target.TryGetComponent(out FactionMember targetFaction) ||
                !AppRoot.TryGetInstance(out AppRoot root) || root.Services == null ||
                root.Services.FactionRelations == null)
            {
                return false;
            }

            VitalsComponent vitals = target.GetComponent<VitalsComponent>();
            if (vitals != null && !vitals.IsAlive)
            {
                return false;
            }

            VisibilityTarget visibility = target.GetComponent<VisibilityTarget>();
            if (visibility != null && !visibility.IsVisibleTo(_faction.Faction))
            {
                return false;
            }

            return root.Services.FactionRelations.AreHostile(
                _faction.Faction,
                targetFaction.Faction);
        }

        private void CacheComponents()
        {
            if (_identity == null) _identity = GetComponent<EntityIdentity>();
            if (_faction == null) _faction = GetComponent<FactionMember>();
            if (_inventory == null) _inventory = GetComponent<WeaponInventory>();
            if (_actions == null) _actions = GetComponent<WeaponActionController>();
        }
    }
}
