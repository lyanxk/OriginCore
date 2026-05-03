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
            combat.TryUsePrimaryOnTarget(_target);
        }

        public void ProcessWeaponInput(InputIntent intent)
        {
            if (intent.CommandQ)
                SwitchWeapon(-1);
            else if (intent.CommandE)
                SwitchWeapon(1);
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
            if (weapon == null || weapon.RangeType == HeroWeaponRangeType.Melee)
                return combat.TryUsePrimaryInDirection(direction);

            if (weapon is RevolverWeapon revolver)
                return TryFireRevolver(revolver, direction, origin);

            return false;
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

                activeWeaponSlot = slot;
                ClearTarget();
                RefreshWeaponVisual();
                return;
            }

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

            Vector3 shotDirection = ResolveShotDirection(direction);
            Vector3 spawnPosition = origin + shotDirection * 0.25f;
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

            if (!IsValidWeaponSlot(activeWeaponSlot))
                activeWeaponSlot = 0;
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
