using System.Collections.Generic;
using OriginCore.Content;
using OriginCore.Core;
using OriginCore.Gameplay;
using OriginCore.Input;
using OriginCore.Weapons;
using TMPro;
using UnityEngine;

namespace OriginCore.UI
{
    [DisallowMultipleComponent]
    public sealed class WeaponRadialMenuView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _root;
        [SerializeField] private RectTransform _itemsRoot;
        [SerializeField] private TMP_Text _statusLabel;
        [Min(40f), SerializeField] private float _radius = 145f;
        [Min(8f), SerializeField] private float _fontSize = 18f;
        [Min(0f), SerializeField] private float _selectionDeadZone = 24f;
        [Min(0), SerializeField] private int _actWeaponSwitchPriority = 10;

        private readonly List<WeaponDefinition> _weapons =
            new List<WeaponDefinition>(WeaponInventory.MaximumDisplayedWeapons);
        private readonly List<TMP_Text> _labels = new List<TMP_Text>(8);
        private InputRouter _inputRouter;
        private GameModeController _modeController;
        private WeaponInventory _inventory;
        private Vector2 _selectionVector;

        public bool IsOpen { get; private set; }
        public int SelectedIndex { get; private set; } = -1;
        public string LastError { get; private set; } = string.Empty;

        private void OnEnable()
        {
            TryBindServices();
            Close(false);
        }

        private void Start()
        {
            TryBindServices();
        }

        private void OnDisable()
        {
            UnbindServices();
            Close(false);
        }

        public void Configure(
            CanvasGroup root,
            RectTransform itemsRoot,
            TMP_Text statusLabel,
            float radius = 145f)
        {
            _root = root;
            _itemsRoot = itemsRoot;
            _statusLabel = statusLabel;
            _radius = Mathf.Max(40f, radius);
            Close(false);
        }

        private void HandleSnapshot(InputSnapshot snapshot)
        {
            if (snapshot.GameplaySuppressed)
            {
                Close(false);
                return;
            }

            InputButtonState wheel = snapshot.ActiveMap == GameplayInputMap.ACT
                ? snapshot.ACT.WeaponWheel
                : snapshot.ActiveMap == GameplayInputMap.FPS
                    ? snapshot.FPS.WeaponWheel
                    : default(InputButtonState);
            if (wheel.WasPressedThisFrame)
            {
                Open(snapshot.ActiveMap.ToGameMode());
            }

            if (IsOpen && wheel.IsPressed)
            {
                Vector2 look = snapshot.ActiveMap == GameplayInputMap.ACT
                    ? snapshot.ACT.Look
                    : snapshot.ActiveMap == GameplayInputMap.FPS
                        ? snapshot.FPS.Look
                        : Vector2.zero;
                UpdateSelection(look);
            }

            if (IsOpen && wheel.WasReleasedThisFrame)
            {
                CommitSelection();
                Close(false);
            }
            else if (IsOpen && snapshot.ActiveMap.ToGameMode() != _inventory.Mode)
            {
                Close(false);
            }
        }

        private void Open(GameMode mode)
        {
            if (!ResolveInventory())
            {
                LastError = "No possessed weapon inventory is available.";
                SetStatus(LastError);
                return;
            }

            _inventory.SetMode(mode);
            BuildEntries(mode);
            IsOpen = true;
            _selectionVector = Vector2.zero;
            SetVisible(true);
            UpdateSelection(Vector2.zero);
        }

        private void BuildEntries(GameMode mode)
        {
            ClearEntries();
            var source = mode == GameMode.ACT
                ? _inventory.ActWeapons
                : _inventory.FpsAvailable;
            for (int i = 0; i < source.Count; i++)
            {
                WeaponDefinition weapon = source[i];
                if (weapon == null)
                {
                    continue;
                }

                _weapons.Add(weapon);
                GameObject labelObject = new GameObject(
                    "Weapon_" + i,
                    typeof(RectTransform),
                    typeof(TextMeshProUGUI));
                RectTransform rect = labelObject.GetComponent<RectTransform>();
                rect.SetParent(_itemsRoot, false);
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(150f, 58f);
                float angle = Mathf.PI * 2f * i / Mathf.Max(1, source.Count) +
                              Mathf.PI * 0.5f;
                rect.anchoredPosition = new Vector2(
                    Mathf.Cos(angle) * _radius,
                    Mathf.Sin(angle) * _radius);

                TMP_Text label = labelObject.GetComponent<TMP_Text>();
                label.fontSize = _fontSize;
                label.alignment = TextAlignmentOptions.Center;
                label.raycastTarget = false;
                label.text = BuildLabel(weapon);
                _labels.Add(label);
                if (weapon == _inventory.ActiveWeapon)
                {
                    SelectedIndex = _weapons.Count - 1;
                }
            }

            if (_weapons.Count == 0)
            {
                SelectedIndex = -1;
                SetStatus("NO WEAPONS");
            }
            else if (SelectedIndex < 0)
            {
                SelectedIndex = 0;
            }
            RefreshHighlight();
        }

        private void UpdateSelection(Vector2 lookDelta)
        {
            if (_weapons.Count == 0 || _inputRouter == null)
            {
                return;
            }

            _selectionVector = Vector2.ClampMagnitude(
                _selectionVector + lookDelta * 12f,
                Mathf.Max(_radius, _selectionDeadZone + 1f));
            Vector2 direction = _selectionVector;
            Vector2 pointerDirection = _inputRouter.PointerPosition -
                                       new Vector2(
                                           Screen.width * 0.5f,
                                           Screen.height * 0.5f);
            if (Cursor.lockState != CursorLockMode.Locked &&
                pointerDirection.magnitude >= _selectionDeadZone)
            {
                direction = pointerDirection;
            }
            if (direction.magnitude < _selectionDeadZone)
            {
                return;
            }

            float normalized = Mathf.Repeat(
                Mathf.Atan2(direction.y, direction.x) - Mathf.PI * 0.5f,
                Mathf.PI * 2f);
            int clockwise = Mathf.RoundToInt(normalized / (Mathf.PI * 2f) * _weapons.Count) %
                            _weapons.Count;
            SelectedIndex = (_weapons.Count - clockwise) % _weapons.Count;
            RefreshHighlight();
        }

        private void CommitSelection()
        {
            if (_inventory == null || SelectedIndex < 0 ||
                SelectedIndex >= _weapons.Count)
            {
                return;
            }

            WeaponDefinition selected = _weapons[SelectedIndex];
            WeaponActionController actions =
                _inventory.GetComponent<WeaponActionController>();
            if (_inventory.Mode == GameMode.ACT &&
                selected != _inventory.ActiveWeapon && actions != null &&
                !actions.CanAuthorizeExternalAction(
                    _actWeaponSwitchPriority,
                    true))
            {
                LastError = "The current weapon action cannot switch weapons yet.";
                SetStatus(LastError);
                return;
            }

            if (!_inventory.TryActivateOrEquip(selected, out string error))
            {
                LastError = error;
                SetStatus(error);
                return;
            }

            _inventory.GetComponent<ThrowableController>()?.Unequip();
            LastError = string.Empty;
        }

        private bool ResolveInventory()
        {
            if (!AppRoot.TryGetInstance(out AppRoot root) || root.Services == null ||
                root.Services.PossessionService.CurrentPawn == null)
            {
                _inventory = null;
                return false;
            }

            _inventory = root.Services.PossessionService.CurrentPawn
                .GetComponent<WeaponInventory>();
            return _inventory != null;
        }

        private void RefreshHighlight()
        {
            for (int i = 0; i < _labels.Count; i++)
            {
                _labels[i].color = i == SelectedIndex
                    ? new Color(0.55f, 1f, 0.72f, 1f)
                    : Color.white;
            }
            if (SelectedIndex >= 0 && SelectedIndex < _weapons.Count)
            {
                SetStatus(_weapons[SelectedIndex].DisplayName.ToUpperInvariant());
            }
        }

        private static string BuildLabel(WeaponDefinition weapon)
        {
            int sellCrystal = Mathf.FloorToInt(
                weapon.PurchaseCost.Crystal * weapon.RefundRatio);
            int sellResource = Mathf.FloorToInt(
                weapon.PurchaseCost.CommanderResource * weapon.RefundRatio);
            int sellInfluence = Mathf.FloorToInt(
                weapon.PurchaseCost.Influence * weapon.RefundRatio);
            return weapon.DisplayName + "\nBUY " +
                   weapon.PurchaseCost.Crystal + "C/" +
                   weapon.PurchaseCost.CommanderResource + "R/" +
                   weapon.PurchaseCost.Influence + "I  SELL " +
                   sellCrystal + "C/" + sellResource + "R/" +
                   sellInfluence + "I";
        }

        private void Close(bool preserveStatus)
        {
            IsOpen = false;
            _selectionVector = Vector2.zero;
            SetVisible(false);
            ClearEntries();
            if (!preserveStatus)
            {
                SetStatus(string.Empty);
            }
        }

        private void ClearEntries()
        {
            for (int i = 0; i < _labels.Count; i++)
            {
                if (_labels[i] != null)
                {
                    Destroy(_labels[i].gameObject);
                }
            }
            _labels.Clear();
            _weapons.Clear();
            SelectedIndex = -1;
        }

        private void SetVisible(bool visible)
        {
            if (_root == null)
            {
                return;
            }
            _root.alpha = visible ? 1f : 0f;
            _root.interactable = false;
            _root.blocksRaycasts = false;
        }

        private void SetStatus(string value)
        {
            if (_statusLabel != null)
            {
                _statusLabel.text = value ?? string.Empty;
            }
        }

        private bool TryBindServices()
        {
            if (!AppRoot.TryGetInstance(out AppRoot root) || root.Services == null)
            {
                return false;
            }

            if (_inputRouter != root.Services.InputRouter)
            {
                if (_inputRouter != null)
                {
                    _inputRouter.SnapshotReady -= HandleSnapshot;
                }
                _inputRouter = root.Services.InputRouter;
                if (_inputRouter != null)
                {
                    _inputRouter.SnapshotReady += HandleSnapshot;
                }
            }

            if (_modeController != root.Services.GameModeController)
            {
                if (_modeController != null)
                {
                    _modeController.ModeChanged -= HandleModeChanged;
                }
                _modeController = root.Services.GameModeController;
                if (_modeController != null)
                {
                    _modeController.ModeChanged += HandleModeChanged;
                }
            }
            return _inputRouter != null;
        }

        private void HandleModeChanged(GameModeChange change)
        {
            Close(false);
        }

        private void UnbindServices()
        {
            if (_inputRouter != null) _inputRouter.SnapshotReady -= HandleSnapshot;
            if (_modeController != null) _modeController.ModeChanged -= HandleModeChanged;
            _inputRouter = null;
            _modeController = null;
            _inventory = null;
        }
    }
}
