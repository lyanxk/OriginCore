using OriginCore.Content;
using OriginCore.Core;
using OriginCore.Gameplay;
using OriginCore.Weapons;
using TMPro;
using UnityEngine;

namespace OriginCore.UI
{
    [DisallowMultipleComponent]
    public sealed class WeaponHudView : MonoBehaviour
    {
        [SerializeField] private GameObject _contentRoot;
        [SerializeField] private TMP_Text _weaponLabel;
        [SerializeField] private TMP_Text _ammoLabel;
        [SerializeField] private TMP_Text _statusLabel;

        private WeaponInventory _inventory;
        private WeaponController _controller;
        private ThrowableController _throwables;
        private float _nextRefreshTime;

        private void OnEnable()
        {
            ResolveInventory();
            Refresh();
        }

        private void OnDisable()
        {
            BindInventory(null);
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextRefreshTime)
            {
                return;
            }

            _nextRefreshTime = Time.unscaledTime + 0.1f;
            if (_inventory == null)
            {
                ResolveInventory();
            }
            Refresh();
        }

        public void Configure(
            GameObject contentRoot,
            TMP_Text weaponLabel,
            TMP_Text ammoLabel,
            TMP_Text statusLabel)
        {
            _contentRoot = contentRoot;
            _weaponLabel = weaponLabel;
            _ammoLabel = ammoLabel;
            _statusLabel = statusLabel;
            ResolveInventory();
            Refresh();
        }

        private void ResolveInventory()
        {
            if (!AppRoot.TryGetInstance(out AppRoot root) || root.Services == null ||
                root.Services.MatchSession.ActiveHero == null)
            {
                BindInventory(null);
                return;
            }

            EntityIdentity hero = root.Services.MatchSession.ActiveHero;
            BindInventory(hero.GetComponent<WeaponInventory>());
            _controller = hero.GetComponent<WeaponController>();
            _throwables = hero.GetComponent<ThrowableController>();
        }

        private void BindInventory(WeaponInventory inventory)
        {
            if (_inventory == inventory)
            {
                return;
            }

            if (_inventory != null)
            {
                _inventory.Changed -= HandleInventoryChanged;
            }

            _inventory = inventory;
            if (_inventory != null)
            {
                _inventory.Changed += HandleInventoryChanged;
            }
        }

        private void HandleInventoryChanged(WeaponInventory inventory)
        {
            Refresh();
        }

        private void Refresh()
        {
            bool visible = _inventory != null && _inventory.Mode != GameMode.RTS;
            PositionForMode(_inventory != null ? _inventory.Mode : GameMode.RTS);
            if (_contentRoot != null && _contentRoot.activeSelf != visible)
            {
                _contentRoot.SetActive(visible);
            }

            if (!visible)
            {
                return;
            }

            WeaponDefinition weapon = _inventory.ActiveWeapon;
            WeaponRuntimeState state = _inventory.ActiveState;
            if (_weaponLabel != null)
            {
                _weaponLabel.text = weapon != null
                    ? weapon.DisplayName.ToUpperInvariant()
                    : "UNARMED";
            }

            if (_ammoLabel != null)
            {
                _ammoLabel.text = weapon != null &&
                                  weapon.FireType != WeaponFireType.Melee && state != null
                    ? state.MagazineAmmo + " / " + state.ReserveAmmo
                    : string.Empty;
            }

            if (_statusLabel != null)
            {
                _statusLabel.text = _throwables != null && _throwables.IsEquipped
                    ? "THROWABLE  " +
                      (_throwables.Selected != null
                          ? _throwables.Selected.DisplayName.ToUpperInvariant()
                          : "NONE")
                    : _controller != null && _controller.IsReloading
                        ? "RELOAD " + _controller.ReloadRemaining.ToString("0.0") + "s"
                        : string.Empty;
            }
        }

        private void PositionForMode(GameMode mode)
        {
            RectTransform panel = _contentRoot != null
                ? _contentRoot.transform as RectTransform
                : null;
            if (panel == null)
            {
                return;
            }

            bool act = mode == GameMode.ACT;
            Vector2 anchor = act ? Vector2.zero : new Vector2(1f, 0f);
            panel.anchorMin = anchor;
            panel.anchorMax = anchor;
            panel.pivot = anchor;
            panel.anchoredPosition = act
                ? new Vector2(12f, 12f)
                : new Vector2(-12f, 12f);
        }
    }
}
