using Core;
using System;
using Input;
using Unit.Command;
using Unit.Movement;
using UnityEngine;

namespace Unit.Combat.Hero
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnitBase))]
    [RequireComponent(typeof(UnitCombat))]
    public sealed class HeroBase : MonoBehaviour
    {
        const float AttackFallReductionDuration = 0.45f;
        const float AttackFallReductionMaxFallSpeed = 2f;
        const float AttackFallReductionGravityMultiplier = 0.15f;

        public enum PerspectiveOption
        {
            None = 0,
            ThirdPersonOnly = 1,
            FirstPersonOnly = 2,
            FirstAndThirdPerson = 3
        }

        [Header("Perspective")]
        [SerializeField] PerspectiveOption perspectiveOption = PerspectiveOption.FirstAndThirdPerson;
        [SerializeField] Transform thirdPersonPivot;
        [SerializeField] Transform firstPersonPivot;

        [Header("Auto Attack")]
        [SerializeField] UnitCombat combat;
        [SerializeField] UnitBase motor;
        [SerializeField] CommandExecutor commandExecutor;
        [Min(0.05f)] public float scanInterval = 0.15f;

        [Header("Weapons")]
        [SerializeField] Transform weaponSocket;
        [SerializeField] bool spawnWeaponVisuals = true;
        [SerializeReference] HeroWeapon[] weaponQueue = Array.Empty<HeroWeapon>();
        [SerializeField, HideInInspector] int activeWeaponSlot;

        Transform _target;
        GameObject _weaponVisualInstance;
        float _scanTimer;
        Vector3 _weaponAimOrigin;
        Vector3 _weaponAimDirection = Vector3.forward;
        bool _hasWeaponAimOrigin;
        bool _hasWeaponAimDirection;

        public bool HasThirdPersonView =>
            perspectiveOption == PerspectiveOption.ThirdPersonOnly ||
            perspectiveOption == PerspectiveOption.FirstAndThirdPerson;
        public bool HasFirstPersonView =>
            perspectiveOption == PerspectiveOption.FirstPersonOnly ||
            perspectiveOption == PerspectiveOption.FirstAndThirdPerson;
        public Transform ThirdPersonPivot => thirdPersonPivot;
        public Transform FirstPersonPivot => firstPersonPivot;
        public int ActiveWeaponSlot => activeWeaponSlot;
        public HeroWeapon CurrentWeapon => GetWeaponAtSlot(activeWeaponSlot);
        public bool IsUnarmed => activeWeaponSlot == 0;

        void Awake()
        {
            CacheReferences();
            DisableCombatAutoChase();
            NormalizeWeaponState();
            RefreshWeaponVisual();
        }

        void OnEnable()
        {
            CacheReferences();
            DisableCombatAutoChase();
            NormalizeWeaponState();
            RefreshWeaponVisual();
        }

        void OnDisable()
        {
            ResetCurrentWeaponAbilityState();
            ClearWeaponVisual();
        }

        void OnValidate()
        {
            CacheReferences();
            DisableCombatAutoChase();
            NormalizeWeaponState();
        }

        void Update()
        {
            if (!CanAutoAttack())
            {
                ClearTarget();
                return;
            }

            if (!combat.IsTargetInRange(_target))
            {
                _target = null;
                _scanTimer = 0f;
            }

            _scanTimer -= Time.deltaTime;
            if (_target == null && _scanTimer <= 0f)
            {
                _scanTimer = scanInterval;
                _target = combat.FindNearestTargetInAttackRange();
            }

            if (_target == null)
                return;

            FaceTarget(_target.position);
            TryUseCurrentWeaponPrimaryOnTarget(_target);
        }

        public void ProcessWeaponSwitchInput(InputIntent intent)
        {
            if (intent.CommandQ)
                SwitchWeapon(-1);
            else if (intent.CommandE)
                SwitchWeapon(1);
        }

        public void ProcessActiveWeaponInput(InputIntent intent)
        {
            CurrentWeapon?.ProcessInput(this, combat, intent);
        }

        public bool ProcessPriorityWeaponInput(
            InputIntent intent,
            out bool blocksModeAbilities,
            out bool blocksMovement,
            out bool blocksPrimaryAttack)
        {
            blocksModeAbilities = false;
            blocksMovement = false;
            blocksPrimaryAttack = false;

            HeroWeapon weapon = CurrentWeapon;
            return weapon != null && weapon.ProcessPriorityInput(
                this,
                combat,
                intent,
                out blocksModeAbilities,
                out blocksMovement,
                out blocksPrimaryAttack);
        }

        public void SetWeaponAimContext(Vector3 origin, Vector3 direction)
        {
            _weaponAimOrigin = origin;
            _hasWeaponAimOrigin = true;

            if (direction.sqrMagnitude <= 1e-6f)
                return;

            _weaponAimDirection = direction.normalized;
            _hasWeaponAimDirection = true;
        }

        public bool CurrentWeaponSupportsAbility(string abilityId)
        {
            HeroWeapon weapon = CurrentWeapon;
            return weapon != null && weapon.SupportsAbility(abilityId);
        }

        public bool TryGetCurrentWeapon<TWeapon>(out TWeapon weapon) where TWeapon : HeroWeapon
        {
            weapon = CurrentWeapon as TWeapon;
            return weapon != null;
        }

        public bool TryUseCurrentWeaponPrimary(Vector3 direction)
        {
            return TryUseCurrentWeaponPrimary(direction, GetAttackOrigin());
        }

        public bool TryUseCurrentWeaponPrimary(Vector3 direction, Vector3 origin)
        {
            if (combat == null)
                return false;

            HeroWeapon weapon = CurrentWeapon;
            if (weapon == null)
                return TryUseCombatPrimaryInDirectionWithFallReduction(direction, null);

            if (weapon.RangeType == HeroWeaponRangeType.Melee)
                return TryUseCombatPrimaryInDirectionWithFallReduction(direction, weapon.BaseDamage);

            if (weapon is RevolverWeapon revolver)
                return ApplyPrimaryAttackFallReductionIfUsed(TryFireRevolver(revolver, direction, origin));

            return false;
        }

        public bool TryUseCurrentWeaponPrimaryOnTarget(Transform target)
        {
            if (combat == null)
                return false;

            HeroWeapon weapon = CurrentWeapon;
            if (weapon == null)
                return TryUseCombatPrimaryOnTargetWithFallReduction(target, null);

            if (weapon.RangeType == HeroWeaponRangeType.Melee)
                return TryUseCombatPrimaryOnTargetWithFallReduction(target, weapon.BaseDamage);

            if (target == null)
                return false;

            Vector3 origin = GetAttackOrigin();
            Vector3 direction = target.position - origin;
            if (direction.sqrMagnitude <= 1e-6f)
                direction = transform.forward;

            if (weapon is RevolverWeapon revolver)
                return ApplyPrimaryAttackFallReductionIfUsed(TryFireRevolver(revolver, direction, origin));

            return false;
        }

        public void GetCurrentWeaponShotPose(out Vector3 origin, out Vector3 direction)
        {
            Vector3 fallbackOrigin = _hasWeaponAimOrigin ? _weaponAimOrigin : GetAttackOrigin();
            Vector3 fallbackDirection = _hasWeaponAimDirection ? _weaponAimDirection : transform.forward;
            ResolveCurrentWeaponShotPose(fallbackOrigin, fallbackDirection, out origin, out direction);
        }

        public bool TryGetRawWeaponAimDirection(out Vector3 direction)
        {
            direction = _hasWeaponAimDirection ? _weaponAimDirection : transform.forward;
            return direction.sqrMagnitude > 1e-6f;
        }

        public bool TryGetActLockedTargetAimPoint(out Vector3 targetPoint)
        {
            targetPoint = Vector3.zero;
            Transform target = motor != null && motor.AbilityRouter != null
                ? motor.AbilityRouter.ActLockedTarget
                : null;

            if (target == null)
                return false;

            targetPoint = ResolveTargetAimPoint(target);
            return true;
        }

        public void SwitchWeapon(int direction)
        {
            NormalizeWeaponState();

            int slotCount = GetWeaponSlotCount();
            if (slotCount <= 1)
            {
                activeWeaponSlot = 0;
                return;
            }

            int step = direction < 0 ? -1 : 1;
            int slot = activeWeaponSlot;
            for (int i = 0; i < slotCount; i++)
            {
                slot = WrapSlot(slot + step, slotCount);
                if (!IsValidWeaponSlot(slot))
                    continue;

                ResetCurrentWeaponAbilityState();
                activeWeaponSlot = slot;
                ClearTarget();
                RefreshWeaponVisual();
                return;
            }

            ResetCurrentWeaponAbilityState();
            activeWeaponSlot = 0;
            RefreshWeaponVisual();
        }

        bool CanAutoAttack()
        {
            if (combat == null || motor == null)
                return false;

            if (commandExecutor != null && !commandExecutor.IsIdle)
                return false;

            ControlModeManager manager = ControlModeManager.Instance;
            return manager == null || manager.unit != motor || manager.CurrentModeName == "RTS";
        }

        void CacheReferences()
        {
            combat = ResolveNearbyComponent(combat);
            motor = ResolveNearbyComponent(motor);
            commandExecutor = ResolveNearbyComponent(commandExecutor);
        }

        void DisableCombatAutoChase()
        {
            if (combat != null)
                combat.autoChaseTargets = false;
        }

        void ClearTarget()
        {
            _target = null;
            _scanTimer = 0f;
        }

        void RefreshWeaponVisual()
        {
            ClearWeaponVisual();

            if (!spawnWeaponVisuals || weaponSocket == null)
                return;

            HeroWeapon weapon = CurrentWeapon;
            if (weapon == null || weapon.ViewPrefab == null)
                return;

            _weaponVisualInstance = Instantiate(weapon.ViewPrefab, weaponSocket);
            _weaponVisualInstance.transform.localPosition = Vector3.zero;
            _weaponVisualInstance.transform.localRotation = Quaternion.identity;
            _weaponVisualInstance.transform.localScale = weapon.ViewPrefab.transform.localScale;
        }

        bool TryFireRevolver(RevolverWeapon weapon, Vector3 direction, Vector3 origin)
        {
            if (weapon == null || combat == null || weapon.ProjectilePrefab == null)
                return false;

            if (!combat.TryConsumePrimaryCooldown())
                return false;

            ResolveCurrentWeaponShotPose(origin, direction, out Vector3 shotOrigin, out Vector3 shotDirection);

            Vector3 spawnPosition = shotOrigin + shotDirection * 0.05f;
            GameObject projectileObject = Instantiate(
                weapon.ProjectilePrefab,
                spawnPosition,
                Quaternion.LookRotation(shotDirection, Vector3.up));

            if (!projectileObject.TryGetComponent(out BulletProjectile projectile))
                projectile = projectileObject.AddComponent<BulletProjectile>();

            if (weapon.HitEffectPrefab != null)
                projectile.Initialize(combat, shotDirection, weapon.BaseDamage, weapon.HitEffectPrefab);
            else
                projectile.Initialize(combat, shotDirection, weapon.BaseDamage);

            return true;
        }

        bool TryUseCombatPrimaryInDirectionWithFallReduction(Vector3 direction, float? damageOverride)
        {
            bool wasReady = combat != null && combat.IsReady;
            bool didHit = damageOverride.HasValue
                ? combat.TryUsePrimaryInDirection(direction, damageOverride.Value)
                : combat.TryUsePrimaryInDirection(direction);

            return ApplyPrimaryAttackFallReductionIfUsed(didHit || (wasReady && combat.consumeCooldownOnMiss));
        }

        bool TryUseCombatPrimaryOnTargetWithFallReduction(Transform target, float? damageOverride)
        {
            bool wasReady = combat != null && combat.IsReady;
            bool didHit = damageOverride.HasValue
                ? combat.TryUsePrimaryOnTarget(target, damageOverride.Value)
                : combat.TryUsePrimaryOnTarget(target);

            return ApplyPrimaryAttackFallReductionIfUsed(didHit || (wasReady && combat.consumeCooldownOnMiss));
        }

        bool ApplyPrimaryAttackFallReductionIfUsed(bool used)
        {
            if (used && motor != null)
            {
                motor.ApplyFallSpeedReduction(
                    AttackFallReductionDuration,
                    AttackFallReductionMaxFallSpeed,
                    AttackFallReductionGravityMultiplier);
            }

            return used;
        }

        void ResolveCurrentWeaponShotPose(
            Vector3 fallbackOrigin,
            Vector3 fallbackDirection,
            out Vector3 origin,
            out Vector3 direction)
        {
            string modeName = ControlModeManager.Instance != null
                ? ControlModeManager.Instance.CurrentModeName
                : string.Empty;

            if (string.Equals(modeName, "FPS", StringComparison.OrdinalIgnoreCase))
            {
                origin = fallbackOrigin;
                direction = ResolveShotDirection(fallbackDirection);
                return;
            }

            if (string.Equals(modeName, "ACT", StringComparison.OrdinalIgnoreCase))
            {
                Transform fireTransform = ResolveCurrentWeaponFireTransform();
                origin = fireTransform != null ? fireTransform.position : fallbackOrigin;
                Transform lockedTarget = motor != null && motor.AbilityRouter != null
                    ? motor.AbilityRouter.ActLockedTarget
                    : null;

                if (lockedTarget != null)
                {
                    Vector3 toTarget = ResolveTargetAimPoint(lockedTarget) - origin;
                    if (toTarget.sqrMagnitude > 1e-6f)
                    {
                        direction = toTarget.normalized;
                        return;
                    }
                }

                direction = ResolveShotDirection(fallbackDirection);
                return;
            }

            Transform defaultFireTransform = ResolveCurrentWeaponFireTransform();
            origin = defaultFireTransform != null ? defaultFireTransform.position : fallbackOrigin;
            direction = ResolveShotDirection(fallbackDirection);
        }

        Transform ResolveCurrentWeaponFireTransform()
        {
            if (_weaponVisualInstance != null)
            {
                Transform muzzle = FindNamedMuzzleTransform(_weaponVisualInstance.transform);
                return muzzle != null ? muzzle : _weaponVisualInstance.transform;
            }

            return weaponSocket;
        }

        static Transform FindNamedMuzzleTransform(Transform root)
        {
            if (root == null)
                return null;

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate == null)
                    continue;

                string candidateName = candidate.name;
                if (string.IsNullOrWhiteSpace(candidateName))
                    continue;

                if (candidateName.IndexOf("muzzle", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    candidateName.IndexOf("firepoint", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    candidateName.IndexOf("barrel", StringComparison.OrdinalIgnoreCase) >= 0)
                    return candidate;
            }

            return null;
        }

        static Vector3 ResolveTargetAimPoint(Transform target)
        {
            Collider[] colliders = target.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider candidate = colliders[i];
                if (candidate.enabled)
                    return candidate.bounds.center;
            }

            return target.position;
        }

        Vector3 ResolveShotDirection(Vector3 direction)
        {
            if (direction.sqrMagnitude <= 1e-6f)
                direction = transform.forward;
            if (direction.sqrMagnitude <= 1e-6f)
                direction = Vector3.forward;

            return direction.normalized;
        }

        Vector3 GetAttackOrigin()
        {
            if (combat != null && combat.attackOrigin != null)
                return combat.attackOrigin.position;

            float height = combat != null ? combat.defaultOriginHeight : 1f;
            return transform.position + Vector3.up * height;
        }

        void ClearWeaponVisual()
        {
            if (_weaponVisualInstance == null)
                return;

            if (Application.isPlaying)
                Destroy(_weaponVisualInstance);
            else
                DestroyImmediate(_weaponVisualInstance);

            _weaponVisualInstance = null;
        }

        void NormalizeWeaponState()
        {
            weaponQueue ??= Array.Empty<HeroWeapon>();

            for (int i = 0; i < weaponQueue.Length; i++)
                weaponQueue[i]?.Normalize();

            if (!IsValidWeaponSlot(activeWeaponSlot))
                activeWeaponSlot = 0;
        }

        void ResetCurrentWeaponAbilityState()
        {
            CurrentWeapon?.ResetAbilityState();
        }

        int GetWeaponSlotCount()
        {
            return 1 + (weaponQueue != null ? weaponQueue.Length : 0);
        }

        bool IsValidWeaponSlot(int slot)
        {
            if (slot == 0)
                return true;

            return weaponQueue != null &&
                   slot > 0 &&
                   slot <= weaponQueue.Length &&
                   weaponQueue[slot - 1] != null;
        }

        HeroWeapon GetWeaponAtSlot(int slot)
        {
            if (slot == 0 || weaponQueue == null || slot < 0 || slot > weaponQueue.Length)
                return null;

            return weaponQueue[slot - 1];
        }

        void FaceTarget(Vector3 targetPosition)
        {
            Vector3 direction = targetPosition - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 1e-6f)
                return;

            motor.SetYaw(Quaternion.LookRotation(direction, Vector3.up).eulerAngles.y);
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

        static int WrapSlot(int slot, int slotCount)
        {
            if (slotCount <= 0)
                return 0;

            slot %= slotCount;
            if (slot < 0)
                slot += slotCount;

            return slot;
        }
    }
}
