using System;
using OriginCore.Combat;
using OriginCore.Economy;
using UnityEngine;

namespace OriginCore.Content
{
    [Flags]
    public enum WeaponSlotMask
    {
        None = 0,
        Primary = 1 << 0,
        Secondary = 1 << 1,
        Melee = 1 << 2
    }

    [Flags]
    public enum WeaponModeMask
    {
        None = 0,
        ACT = 1 << 0,
        FPS = 1 << 1,
        ACTAndFPS = ACT | FPS
    }

    public enum WeaponFireType
    {
        Melee = 0,
        Hitscan = 1,
        Projectile = 2,
        Thrown = 3
    }

    [CreateAssetMenu(fileName = "SO_Weapon_New", menuName = "OriginCore/Content/Weapon")]
    public sealed class WeaponDefinition : ContentDefinition
    {
        [SerializeField] private WeaponSlotMask _allowedSlots = WeaponSlotMask.Primary;
        [SerializeField] private WeaponModeMask _availableModes = WeaponModeMask.ACTAndFPS;
        [SerializeField] private WeaponFireType _fireType = WeaponFireType.Hitscan;
        [SerializeField] private ResourceCost _purchaseCost;
        [Range(0f, 1f), SerializeField] private float _refundRatio = 0.5f;
        [Min(0f), SerializeField] private float _damage = 10f;
        [SerializeField] private DamageType _damageType = DamageType.Physical;
        [SerializeField] private DamageFlags _damageFlags;
        [Min(0f), SerializeField] private float _range = 30f;
        [Min(0.01f), SerializeField] private float _secondsPerShot = 0.25f;
        [Min(1), SerializeField] private int _magazineSize = 12;
        [Min(0), SerializeField] private int _startingReserveAmmo = 48;
        [Min(0f), SerializeField] private float _reloadSeconds = 1.5f;
        [Min(0f), SerializeField] private float _hipSpreadDegrees = 1.5f;
        [Min(0f), SerializeField] private float _adsSpreadDegrees = 0.2f;
        [Range(0.1f, 2f), SerializeField] private float _moveSpeedMultiplier = 1f;
        [Range(1f, 179f), SerializeField] private float _adsFieldOfView = 55f;
        [SerializeField] private GameObject _projectilePrefab;
        [SerializeField] private GameObject _worldPrefab;
        [SerializeField] private GameObject _firstPersonPrefab;
        [SerializeField] private AbilityDefinition[] _weaponSkills = new AbilityDefinition[0];
        [SerializeField] private WeaponEquipRole _equipRole = WeaponEquipRole.MainHand;
        [SerializeField] private string _requiredFormId;
        [SerializeField] private WeaponActionSetDefinition _actActionSet;
        [SerializeField] private bool _persistsWhenUnequipped;
        [SerializeField] private Sprite _icon;

        public WeaponSlotMask AllowedSlots => _allowedSlots;
        public WeaponModeMask AvailableModes => _availableModes;
        public WeaponFireType FireType => _fireType;
        public ResourceCost PurchaseCost => _purchaseCost;
        public float RefundRatio => Mathf.Clamp01(_refundRatio);
        public float Damage => Mathf.Max(0f, _damage);
        public DamageType DamageType => _damageType;
        public DamageFlags DamageFlags => _damageFlags;
        public float Range => Mathf.Max(0f, _range);
        public float SecondsPerShot => Mathf.Max(0.01f, _secondsPerShot);
        public int MagazineSize => Mathf.Max(1, _magazineSize);
        public int StartingReserveAmmo => Mathf.Max(0, _startingReserveAmmo);
        public float ReloadSeconds => Mathf.Max(0f, _reloadSeconds);
        public float HipSpreadDegrees => Mathf.Max(0f, _hipSpreadDegrees);
        public float AdsSpreadDegrees => Mathf.Max(0f, _adsSpreadDegrees);
        public float MoveSpeedMultiplier => Mathf.Clamp(_moveSpeedMultiplier, 0.1f, 2f);
        public float AdsFieldOfView => Mathf.Clamp(_adsFieldOfView, 1f, 179f);
        public GameObject ProjectilePrefab => _projectilePrefab;
        public GameObject WorldPrefab => _worldPrefab;
        public GameObject FirstPersonPrefab => _firstPersonPrefab;
        public AbilityDefinition[] WeaponSkills => _weaponSkills;
        public WeaponEquipRole EquipRole => _equipRole;
        public string RequiredFormId => ContentIdUtility.Normalize(_requiredFormId);
        public WeaponActionSetDefinition ActActionSet => _actActionSet;
        public bool HasExplicitActActionSet => _actActionSet != null;
        public bool PersistsWhenUnequipped => _persistsWhenUnequipped;
        public bool IsAuxiliary => _equipRole == WeaponEquipRole.Auxiliary;
        public Sprite Icon => _icon;

        public void Configure(
            WeaponSlotMask allowedSlots,
            WeaponModeMask availableModes,
            WeaponFireType fireType,
            ResourceCost purchaseCost,
            float refundRatio,
            float damage,
            DamageType damageType,
            DamageFlags damageFlags,
            float range,
            float secondsPerShot,
            int magazineSize,
            int startingReserveAmmo,
            float reloadSeconds,
            float hipSpreadDegrees,
            float adsSpreadDegrees,
            float moveSpeedMultiplier,
            float adsFieldOfView,
            GameObject projectilePrefab,
            GameObject worldPrefab,
            GameObject firstPersonPrefab,
            AbilityDefinition[] weaponSkills,
            Sprite icon = null)
        {
            _allowedSlots = allowedSlots;
            _availableModes = availableModes;
            _fireType = fireType;
            _purchaseCost = purchaseCost;
            _refundRatio = Mathf.Clamp01(refundRatio);
            _damage = Mathf.Max(0f, damage);
            _damageType = damageType;
            _damageFlags = damageFlags;
            _range = Mathf.Max(0f, range);
            _secondsPerShot = Mathf.Max(0.01f, secondsPerShot);
            _magazineSize = Mathf.Max(1, magazineSize);
            _startingReserveAmmo = Mathf.Max(0, startingReserveAmmo);
            _reloadSeconds = Mathf.Max(0f, reloadSeconds);
            _hipSpreadDegrees = Mathf.Max(0f, hipSpreadDegrees);
            _adsSpreadDegrees = Mathf.Max(0f, adsSpreadDegrees);
            _moveSpeedMultiplier = Mathf.Clamp(moveSpeedMultiplier, 0.1f, 2f);
            _adsFieldOfView = Mathf.Clamp(adsFieldOfView, 1f, 179f);
            _projectilePrefab = projectilePrefab;
            _worldPrefab = worldPrefab;
            _firstPersonPrefab = firstPersonPrefab;
            _weaponSkills = weaponSkills != null
                ? (AbilityDefinition[])weaponSkills.Clone()
                : new AbilityDefinition[0];
            _icon = icon;
        }

        public void ConfigureActionProfile(
            WeaponEquipRole equipRole,
            string requiredFormId,
            WeaponActionSetDefinition actActionSet,
            bool persistsWhenUnequipped)
        {
            _equipRole = equipRole;
            _requiredFormId = ContentIdUtility.Normalize(requiredFormId);
            _actActionSet = actActionSet;
            _persistsWhenUnequipped = persistsWhenUnequipped;
        }

        public bool TryValidateActionProfile(out string error)
        {
            if (_actActionSet != null && !_actActionSet.TryValidate(out error))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(RequiredFormId) &&
                !ContentIdUtility.IsValid(RequiredFormId))
            {
                error = "Weapon '" + ContentId + "' has an invalid required form ID.";
                return false;
            }

            if (_equipRole == WeaponEquipRole.Auxiliary &&
                (_allowedSlots & WeaponSlotMask.Melee) != 0)
            {
                error = "Auxiliary weapon '" + ContentId +
                        "' cannot occupy the FPS melee slot.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            _refundRatio = Mathf.Clamp01(_refundRatio);
            _damage = Mathf.Max(0f, _damage);
            _range = Mathf.Max(0f, _range);
            _secondsPerShot = Mathf.Max(0.01f, _secondsPerShot);
            _magazineSize = Mathf.Max(1, _magazineSize);
            _startingReserveAmmo = Mathf.Max(0, _startingReserveAmmo);
            _reloadSeconds = Mathf.Max(0f, _reloadSeconds);
            _hipSpreadDegrees = Mathf.Max(0f, _hipSpreadDegrees);
            _adsSpreadDegrees = Mathf.Max(0f, _adsSpreadDegrees);
            _moveSpeedMultiplier = Mathf.Clamp(_moveSpeedMultiplier, 0.1f, 2f);
            _adsFieldOfView = Mathf.Clamp(_adsFieldOfView, 1f, 179f);
            _weaponSkills = _weaponSkills ?? new AbilityDefinition[0];
            _requiredFormId = ContentIdUtility.Normalize(_requiredFormId);
        }
    }
}
