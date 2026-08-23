using System;
using OriginCore.ACT;
using OriginCore.Abilities;
using OriginCore.Combat;
using OriginCore.Content;
using OriginCore.Gameplay;
using UnityEngine;

namespace OriginCore.Weapons
{
    [DisallowMultipleComponent]
    [RequireComponent(
        typeof(WeaponInventory),
        typeof(AutoTargetScanner),
        typeof(TargetLockController))]
    public sealed class FinalWingTargetingController : MonoBehaviour
    {
        [SerializeField] private WeaponInventory _inventory;
        [SerializeField] private AutoTargetScanner _scanner;
        [SerializeField] private TargetLockController _targetLock;
        [SerializeField] private HeroFormStateMachine _forms;

        public event Action<EntityIdentity, EntityIdentity> TargetChanged;

        public EntityIdentity CurrentTarget { get; private set; }

        public bool IsAvailable => ResolveFinalWing() != null;

        private void Awake()
        {
            CacheComponents();
        }

        private void OnEnable()
        {
            CacheComponents();
            if (_targetLock != null)
            {
                _targetLock.TargetChanged -= HandleExplicitTargetChanged;
                _targetLock.TargetChanged += HandleExplicitTargetChanged;
            }
        }

        private void OnDisable()
        {
            if (_targetLock != null)
            {
                _targetLock.TargetChanged -= HandleExplicitTargetChanged;
            }
            SetTarget(null);
        }

        private void OnValidate()
        {
            CacheComponents();
        }

        public EntityIdentity ResolveTarget(bool searchIfMissing = true)
        {
            CacheComponents();
            WeaponDefinition wing = ResolveFinalWing();
            if (wing == null)
            {
                SetTarget(null);
                return null;
            }

            EntityIdentity explicitTarget = _targetLock != null
                ? _targetLock.CurrentTarget
                : null;
            if (IsTargetValid(explicitTarget))
            {
                SetTarget(explicitTarget);
                return CurrentTarget;
            }

            if (IsTargetValid(CurrentTarget))
            {
                return CurrentTarget;
            }

            SetTarget(null);
            if (searchIfMissing && _scanner != null &&
                _scanner.FindNearestHostile(wing.Range, out EntityIdentity nearest))
            {
                SetTarget(nearest);
            }

            return CurrentTarget;
        }

        public void ClearTarget()
        {
            SetTarget(null);
        }

        public bool IsTargetValid(EntityIdentity target)
        {
            WeaponDefinition wing = ResolveFinalWing();
            return wing != null && IsValidTarget(target, wing.Range);
        }

        private void HandleExplicitTargetChanged(
            EntityIdentity previous,
            EntityIdentity current)
        {
            WeaponDefinition wing = ResolveFinalWing();
            if (wing != null && IsTargetValid(current))
            {
                SetTarget(current);
                return;
            }

            SetTarget(null);
        }

        private WeaponDefinition ResolveFinalWing()
        {
            WeaponDefinition wing = _inventory != null
                ? _inventory.ActAuxiliary
                : null;
            return wing != null && _forms != null &&
                   string.Equals(_forms.CurrentFormId, "form.y.final", StringComparison.Ordinal) &&
                   string.Equals(
                       wing.ContentId,
                       BuiltInWeaponActionLibrary.FinalWingWeaponId,
                       StringComparison.Ordinal)
                ? wing
                : null;
        }

        private bool IsValidTarget(EntityIdentity target, float range)
        {
            if (target == null || !target.isActiveAndEnabled || _scanner == null ||
                !_scanner.IsValidVisibleHostile(target))
            {
                return false;
            }

            Vector3 delta = target.transform.position - transform.position;
            delta.y = 0f;
            return delta.sqrMagnitude <= range * range + 0.0001f;
        }

        private void SetTarget(EntityIdentity next)
        {
            if (CurrentTarget == next)
            {
                return;
            }

            EntityIdentity previous = CurrentTarget;
            CurrentTarget = next;
            TargetChanged?.Invoke(previous, CurrentTarget);
        }

        private void CacheComponents()
        {
            if (_inventory == null) _inventory = GetComponent<WeaponInventory>();
            if (_scanner == null) _scanner = GetComponent<AutoTargetScanner>();
            if (_targetLock == null) _targetLock = GetComponent<TargetLockController>();
            if (_forms == null) _forms = GetComponent<HeroFormStateMachine>();
        }
    }
}
