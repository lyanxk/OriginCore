using System;
using System.Collections.Generic;
using OriginCore.Combat;
using OriginCore.Content;
using OriginCore.Core;
using OriginCore.Economy;
using OriginCore.Matches;
using UnityEngine;

namespace OriginCore.Weapons
{
    [Serializable]
    public sealed class WeaponRuntimeState
    {
        [SerializeField] private WeaponDefinition _definition;
        [SerializeField] private int _magazineAmmo;
        [SerializeField] private int _reserveAmmo;

        public WeaponRuntimeState(WeaponDefinition definition)
        {
            _definition = definition;
            _magazineAmmo = definition != null ? definition.MagazineSize : 0;
            _reserveAmmo = definition != null ? definition.StartingReserveAmmo : 0;
        }

        public WeaponDefinition Definition => _definition;
        public int MagazineAmmo => Mathf.Max(0, _magazineAmmo);
        public int ReserveAmmo => Mathf.Max(0, _reserveAmmo);

        public bool TryConsumeRound()
        {
            if (_definition == null || _definition.FireType == WeaponFireType.Melee)
            {
                return _definition != null;
            }

            if (_magazineAmmo <= 0)
            {
                return false;
            }

            _magazineAmmo--;
            return true;
        }

        public int CompleteReload()
        {
            if (_definition == null || _definition.FireType == WeaponFireType.Melee)
            {
                return 0;
            }

            int needed = Mathf.Max(0, _definition.MagazineSize - _magazineAmmo);
            int loaded = Mathf.Min(needed, _reserveAmmo);
            _magazineAmmo += loaded;
            _reserveAmmo -= loaded;
            return loaded;
        }

        public void Restore(int magazineAmmo, int reserveAmmo)
        {
            if (_definition == null)
            {
                _magazineAmmo = 0;
                _reserveAmmo = 0;
                return;
            }

            _magazineAmmo = Mathf.Clamp(magazineAmmo, 0, _definition.MagazineSize);
            _reserveAmmo = Mathf.Max(0, reserveAmmo);
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(RuntimeStatBlock))]
    public sealed class WeaponInventory : MonoBehaviour
    {
        private const int MaximumActWeapons = 4;
        private const int MaximumFpsAvailable = 6;
        private const string MovementModifierSource = "weapon.active.move_speed";

        public const int MaximumDisplayedWeapons = MaximumFpsAvailable;

        [SerializeField] private RuntimeStatBlock _stats;
        [SerializeField] private List<WeaponRuntimeState> _states =
            new List<WeaponRuntimeState>(MaximumFpsAvailable);
        [SerializeField] private List<WeaponDefinition> _actWeapons =
            new List<WeaponDefinition>(MaximumActWeapons);
        [SerializeField] private WeaponDefinition _actAuxiliary;
        [SerializeField] private List<WeaponDefinition> _fpsAvailable =
            new List<WeaponDefinition>(MaximumFpsAvailable);
        [SerializeField] private WeaponDefinition _fpsPrimary;
        [SerializeField] private WeaponDefinition _fpsSecondary;
        [SerializeField] private WeaponDefinition _fpsMelee;
        [SerializeField] private WeaponDefinition _activeWeapon;

        private GameMode _mode = GameMode.RTS;
        private int _actIndex = -1;

        public event Action<WeaponInventory> Changed;
        public event Action<WeaponDefinition, WeaponDefinition> ActiveWeaponChanged;

        public IReadOnlyList<WeaponRuntimeState> States => _states;
        public IReadOnlyList<WeaponDefinition> ActWeapons => _actWeapons;
        public WeaponDefinition ActAuxiliary => _actAuxiliary;
        public IReadOnlyList<WeaponDefinition> FpsAvailable => _fpsAvailable;
        public WeaponDefinition FpsPrimary => _fpsPrimary;
        public WeaponDefinition FpsSecondary => _fpsSecondary;
        public WeaponDefinition FpsMelee => _fpsMelee;
        public WeaponDefinition ActiveWeapon => _activeWeapon;
        public GameMode Mode => _mode;
        public WeaponRuntimeState ActiveState => FindState(_activeWeapon);

        private void Awake()
        {
            CacheComponents();
        }

        private void OnDisable()
        {
            RemoveMovementModifier();
        }

        private void OnValidate()
        {
            CacheComponents();
            _states = _states ?? new List<WeaponRuntimeState>(MaximumFpsAvailable);
            _actWeapons = _actWeapons ?? new List<WeaponDefinition>(MaximumActWeapons);
            _fpsAvailable = _fpsAvailable ??
                            new List<WeaponDefinition>(MaximumFpsAvailable);
        }

        public void ConfigureFromMatch(
            MatchConfiguration configuration,
            ContentCatalog catalog)
        {
            _states.Clear();
            _actWeapons.Clear();
            _actAuxiliary = null;
            _fpsAvailable.Clear();
            _fpsPrimary = null;
            _fpsSecondary = null;
            _fpsMelee = null;
            _actIndex = -1;

            if (configuration != null && catalog != null)
            {
                AddConfiguredActWeapons(
                    configuration.actWeaponIds,
                    catalog);
                AddConfiguredWeapons(
                    configuration.fpsAvailableWeaponIds,
                    MaximumFpsAvailable,
                    WeaponModeMask.FPS,
                    catalog,
                    _fpsAvailable);
                _fpsPrimary = ResolveEquipped(
                    configuration.fpsEquippedPrimary,
                    WeaponSlotMask.Primary,
                    catalog);
                _fpsSecondary = ResolveEquipped(
                    configuration.fpsEquippedSecondary,
                    WeaponSlotMask.Secondary,
                    catalog);
                _fpsMelee = ResolveEquipped(
                    configuration.fpsEquippedMelee,
                    WeaponSlotMask.Melee,
                    catalog);
            }

            EnsureFpsFallbacks();
            SetMode(_mode, true);
            Changed?.Invoke(this);
        }

        public void SetMode(GameMode mode, bool force = false)
        {
            if (!force && _mode == mode)
            {
                return;
            }

            _mode = mode;
            WeaponDefinition next = null;
            if (_mode == GameMode.ACT && _actWeapons.Count > 0)
            {
                _actIndex = Mathf.Clamp(_actIndex < 0 ? 0 : _actIndex, 0, _actWeapons.Count - 1);
                next = _actWeapons[_actIndex];
            }
            else if (_mode == GameMode.FPS)
            {
                next = _fpsPrimary ?? _fpsSecondary ?? _fpsMelee;
            }

            SetActive(next);
        }

        public bool CycleAct(int direction)
        {
            if (_mode != GameMode.ACT || _actWeapons.Count == 0 || direction == 0)
            {
                return false;
            }

            _actIndex = (_actIndex + (direction > 0 ? 1 : -1) + _actWeapons.Count) %
                        _actWeapons.Count;
            SetActive(_actWeapons[_actIndex]);
            return true;
        }

        public bool SelectFpsSlot(WeaponSlotMask slot)
        {
            if (_mode != GameMode.FPS)
            {
                return false;
            }

            WeaponDefinition next = slot == WeaponSlotMask.Primary
                ? _fpsPrimary
                : slot == WeaponSlotMask.Secondary
                    ? _fpsSecondary
                    : slot == WeaponSlotMask.Melee ? _fpsMelee : null;
            if (next == null)
            {
                return false;
            }

            SetActive(next);
            return true;
        }

        public bool TryActivateOrEquip(WeaponDefinition weapon, out string error)
        {
            if (weapon == null)
            {
                error = "Weapon is unavailable.";
                return false;
            }

            if (_mode == GameMode.ACT)
            {
                if (weapon.EquipRole == WeaponEquipRole.Auxiliary)
                {
                    if (_actAuxiliary != weapon)
                    {
                        error = "Auxiliary weapon is not in the ACT loadout.";
                        return false;
                    }

                    error = string.Empty;
                    return true;
                }

                int index = _actWeapons.IndexOf(weapon);
                if (index < 0)
                {
                    error = "Weapon is not in the ACT loadout.";
                    return false;
                }

                _actIndex = index;
                SetActive(weapon);
                error = string.Empty;
                return true;
            }

            if (_mode != GameMode.FPS || !_fpsAvailable.Contains(weapon))
            {
                error = "Weapon is not available in the current mode.";
                return false;
            }

            if (weapon == _fpsPrimary || weapon == _fpsSecondary || weapon == _fpsMelee)
            {
                SetActive(weapon);
                error = string.Empty;
                return true;
            }

            return TryPurchaseAndEquip(weapon, out error);
        }

        public WeaponRuntimeState FindState(WeaponDefinition definition)
        {
            if (definition == null)
            {
                return null;
            }

            for (int i = 0; i < _states.Count; i++)
            {
                WeaponRuntimeState state = _states[i];
                if (state != null && state.Definition == definition)
                {
                    return state;
                }
            }

            return null;
        }

        public WeaponRuntimeState FindState(string weaponId)
        {
            for (int i = 0; i < _states.Count; i++)
            {
                WeaponRuntimeState state = _states[i];
                if (state != null && state.Definition != null && string.Equals(
                        state.Definition.ContentId,
                        weaponId,
                        StringComparison.Ordinal))
                {
                    return state;
                }
            }

            return null;
        }

        public bool TryPurchaseAndEquip(WeaponDefinition weapon, out string error)
        {
            if (weapon == null ||
                !AppRoot.TryGetInstance(out AppRoot root) || root.Services == null)
            {
                error = "Weapon or resource service is unavailable.";
                return false;
            }

            ResourceService resources = root.Services.ResourceService;
            if (!resources.TrySpend(weapon.PurchaseCost))
            {
                error = "Insufficient resources for this weapon.";
                return false;
            }

            WeaponDefinition replaced = SelectReplacement(weapon);
            if (!InstallWeapon(weapon))
            {
                resources.Refund(weapon.PurchaseCost);
                error = "The weapon cannot be installed in the current loadout.";
                return false;
            }

            if (replaced != null && replaced != weapon)
            {
                RefundSaleValue(resources, replaced);
            }

            if (_mode == GameMode.ACT &&
                weapon.EquipRole == WeaponEquipRole.MainHand &&
                (weapon.AvailableModes & WeaponModeMask.ACT) != 0 ||
                _mode == GameMode.FPS && (weapon.AvailableModes & WeaponModeMask.FPS) != 0)
            {
                SetActive(weapon);
            }

            Changed?.Invoke(this);
            error = string.Empty;
            return true;
        }

        public bool RestoreAmmo(string weaponId, int magazineAmmo, int reserveAmmo)
        {
            WeaponRuntimeState state = FindState(weaponId);
            if (state == null)
            {
                return false;
            }

            state.Restore(magazineAmmo, reserveAmmo);
            Changed?.Invoke(this);
            return true;
        }

        public bool RestoreEquipped(
            string primaryId,
            string secondaryId,
            string meleeId)
        {
            _fpsPrimary = ResolveOwned(primaryId, WeaponSlotMask.Primary);
            _fpsSecondary = ResolveOwned(secondaryId, WeaponSlotMask.Secondary);
            _fpsMelee = ResolveOwned(meleeId, WeaponSlotMask.Melee);
            EnsureFpsFallbacks();
            SetMode(_mode, true);
            Changed?.Invoke(this);
            return true;
        }

        public bool RestoreActState(string activeWeaponId, string auxiliaryWeaponId)
        {
            WeaponDefinition auxiliary = ResolveOwnedDefinition(auxiliaryWeaponId);
            if (auxiliary != null && auxiliary.EquipRole == WeaponEquipRole.Auxiliary)
            {
                _actAuxiliary = auxiliary;
            }
            WeaponDefinition active = ResolveOwnedDefinition(activeWeaponId);
            int index = active != null ? _actWeapons.IndexOf(active) : -1;
            if (index >= 0)
            {
                _actIndex = index;
            }
            SetMode(_mode, true);
            Changed?.Invoke(this);
            return index >= 0 || string.IsNullOrEmpty(activeWeaponId);
        }

        private WeaponDefinition ResolveOwnedDefinition(string weaponId)
        {
            WeaponRuntimeState state = FindState(weaponId);
            return state != null ? state.Definition : null;
        }

        public void NotifyRuntimeStateChanged()
        {
            Changed?.Invoke(this);
        }

        private void SetActive(WeaponDefinition next)
        {
            WeaponDefinition previous = _activeWeapon;
            if (previous == next)
            {
                ApplyMovementModifier();
                return;
            }

            _activeWeapon = next;
            ApplyMovementModifier();
            ActiveWeaponChanged?.Invoke(previous, _activeWeapon);
            Changed?.Invoke(this);
        }

        private void ApplyMovementModifier()
        {
            CacheComponents();
            RemoveMovementModifier();
            if (_stats == null || _activeWeapon == null ||
                Mathf.Approximately(_activeWeapon.MoveSpeedMultiplier, 1f))
            {
                return;
            }

            _stats.AddModifier(new StatModifierDefinition(
                MovementModifierSource,
                RuntimeStatId.MoveSpeed,
                StatModifierOperation.Multiply,
                _activeWeapon.MoveSpeedMultiplier,
                0,
                0f,
                StatStackingRule.ReplaceExisting));
        }

        private void RemoveMovementModifier()
        {
            _stats?.RemoveModifiersBySource(MovementModifierSource);
        }

        private bool InstallWeapon(WeaponDefinition weapon)
        {
            bool installed = false;
            EnsureState(weapon);
            if ((weapon.AvailableModes & WeaponModeMask.ACT) != 0)
            {
                if (weapon.EquipRole == WeaponEquipRole.Auxiliary)
                {
                    _actAuxiliary = weapon;
                }
                else
                {
                    int existing = _actWeapons.IndexOf(weapon);
                    if (existing < 0)
                    {
                        if (_actWeapons.Count < MaximumActWeapons)
                        {
                            _actWeapons.Add(weapon);
                        }
                        else
                        {
                            int replace = Mathf.Clamp(_actIndex, 0, _actWeapons.Count - 1);
                            _actWeapons[replace] = weapon;
                        }
                    }
                }

                installed = true;
            }

            if ((weapon.AvailableModes & WeaponModeMask.FPS) != 0)
            {
                if (!_fpsAvailable.Contains(weapon))
                {
                    if (_fpsAvailable.Count >= MaximumFpsAvailable)
                    {
                        _fpsAvailable.RemoveAt(0);
                    }

                    _fpsAvailable.Add(weapon);
                }

                if ((weapon.AllowedSlots & WeaponSlotMask.Melee) != 0)
                {
                    _fpsMelee = weapon;
                }
                else if ((weapon.AllowedSlots & WeaponSlotMask.Primary) != 0)
                {
                    _fpsPrimary = weapon;
                }
                else if ((weapon.AllowedSlots & WeaponSlotMask.Secondary) != 0)
                {
                    _fpsSecondary = weapon;
                }

                installed = true;
            }

            return installed;
        }

        private WeaponDefinition SelectReplacement(WeaponDefinition weapon)
        {
            if (weapon.EquipRole == WeaponEquipRole.Auxiliary &&
                (weapon.AvailableModes & WeaponModeMask.ACT) != 0)
            {
                return _actAuxiliary;
            }

            if ((weapon.AvailableModes & WeaponModeMask.FPS) != 0)
            {
                if ((weapon.AllowedSlots & WeaponSlotMask.Melee) != 0) return _fpsMelee;
                if ((weapon.AllowedSlots & WeaponSlotMask.Primary) != 0) return _fpsPrimary;
                if ((weapon.AllowedSlots & WeaponSlotMask.Secondary) != 0) return _fpsSecondary;
            }

            return _mode == GameMode.ACT ? _activeWeapon : null;
        }

        private void AddConfiguredActWeapons(
            string[] ids,
            ContentCatalog catalog)
        {
            int count = ids != null ? ids.Length : 0;
            for (int i = 0; i < count; i++)
            {
                if (!catalog.TryGetWeapon(ids[i], out WeaponDefinition weapon) ||
                    weapon == null ||
                    (weapon.AvailableModes & WeaponModeMask.ACT) == 0)
                {
                    continue;
                }

                EnsureState(weapon);
                if (weapon.EquipRole == WeaponEquipRole.Auxiliary)
                {
                    if (_actAuxiliary == null)
                    {
                        _actAuxiliary = weapon;
                    }
                    continue;
                }

                if (_actWeapons.Count < MaximumActWeapons &&
                    !_actWeapons.Contains(weapon))
                {
                    _actWeapons.Add(weapon);
                }
            }
        }

        private void AddConfiguredWeapons(
            string[] ids,
            int maximum,
            WeaponModeMask mode,
            ContentCatalog catalog,
            List<WeaponDefinition> destination)
        {
            int count = ids != null ? Mathf.Min(ids.Length, maximum) : 0;
            for (int i = 0; i < count; i++)
            {
                if (!catalog.TryGetWeapon(ids[i], out WeaponDefinition weapon) ||
                    weapon == null || (weapon.AvailableModes & mode) == 0 ||
                    destination.Contains(weapon))
                {
                    continue;
                }

                destination.Add(weapon);
                EnsureState(weapon);
            }
        }

        private WeaponDefinition ResolveEquipped(
            string id,
            WeaponSlotMask slot,
            ContentCatalog catalog)
        {
            if (!catalog.TryGetWeapon(id, out WeaponDefinition weapon) ||
                weapon == null || !_fpsAvailable.Contains(weapon) ||
                (weapon.AllowedSlots & slot) == 0)
            {
                return null;
            }

            EnsureState(weapon);
            return weapon;
        }

        private WeaponDefinition ResolveOwned(string id, WeaponSlotMask slot)
        {
            WeaponRuntimeState state = FindState(id);
            return state != null && state.Definition != null &&
                   (state.Definition.AllowedSlots & slot) != 0
                ? state.Definition
                : null;
        }

        private void EnsureFpsFallbacks()
        {
            for (int i = 0; i < _fpsAvailable.Count; i++)
            {
                WeaponDefinition weapon = _fpsAvailable[i];
                if (weapon == null) continue;
                if (_fpsPrimary == null && (weapon.AllowedSlots & WeaponSlotMask.Primary) != 0)
                {
                    _fpsPrimary = weapon;
                }
                if (_fpsSecondary == null &&
                    (weapon.AllowedSlots & WeaponSlotMask.Secondary) != 0)
                {
                    _fpsSecondary = weapon;
                }
                if (_fpsMelee == null && (weapon.AllowedSlots & WeaponSlotMask.Melee) != 0)
                {
                    _fpsMelee = weapon;
                }
            }
        }

        private void EnsureState(WeaponDefinition weapon)
        {
            if (weapon != null && FindState(weapon) == null)
            {
                _states.Add(new WeaponRuntimeState(weapon));
            }
        }

        private static void RefundSaleValue(
            ResourceService resources,
            WeaponDefinition weapon)
        {
            int commander = Mathf.FloorToInt(
                weapon.PurchaseCost.CommanderResource * weapon.RefundRatio);
            int crystal = Mathf.FloorToInt(
                weapon.PurchaseCost.Crystal * weapon.RefundRatio);
            if (commander > 0) resources.Add(ResourceType.CommanderResource, commander);
            if (crystal > 0) resources.Add(ResourceType.Crystal, crystal);
        }

        private void CacheComponents()
        {
            if (_stats == null)
            {
                _stats = GetComponent<RuntimeStatBlock>();
            }
        }
    }
}
