using System;
using OriginCore.Content;
using OriginCore.Core;
using OriginCore.Economy;
using OriginCore.Gameplay;
using OriginCore.Input;
using UnityEngine;

namespace OriginCore.Weapons
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EntityIdentity))]
    public sealed class ThrowableController : MonoBehaviour
    {
        [SerializeField] private EntityIdentity _identity;
        [SerializeField] private FactionMember _faction;
        [SerializeField] private Camera _aimCamera;
        [SerializeField] private Transform _throwAnchor;
        [SerializeField] private ThrowableDefinition[] _available =
            new ThrowableDefinition[0];
        [Min(0.1f), SerializeField] private float _wheelHoldSeconds = 0.35f;
        [Min(0f), SerializeField] private float _selectionDeadZone = 24f;
        [SerializeField] private LayerMask _hitMask = ~0;

        private InputRouter _inputRouter;
        private float _pressedAt;
        private int _selectedIndex = -1;
        private Vector2 _wheelSelectionVector;

        public event Action<ThrowableController> Changed;

        public ThrowableDefinition[] Available => _available;
        public int SelectedIndex => _selectedIndex;
        public ThrowableDefinition Selected =>
            _selectedIndex >= 0 && _selectedIndex < _available.Length
                ? _available[_selectedIndex]
                : null;
        public bool IsEquipped { get; private set; }
        public bool IsWheelOpen { get; private set; }
        public string LastError { get; private set; } = string.Empty;

        private void OnEnable()
        {
            CacheComponents();
            _available = _available ?? new ThrowableDefinition[0];
            ClampSelection();
            TryBindInput();
        }

        private void Start()
        {
            TryBindInput();
        }

        private void OnDisable()
        {
            if (_inputRouter != null)
            {
                _inputRouter.SnapshotReady -= HandleSnapshot;
                _inputRouter = null;
            }
            IsWheelOpen = false;
            IsEquipped = false;
        }

        private void OnValidate()
        {
            CacheComponents();
            _available = _available ?? new ThrowableDefinition[0];
            _wheelHoldSeconds = Mathf.Max(0.1f, _wheelHoldSeconds);
            _selectionDeadZone = Mathf.Max(0f, _selectionDeadZone);
            ClampSelection();
        }

        public void Configure(
            ThrowableDefinition[] available,
            float wheelHoldSeconds = 0.35f)
        {
            _available = available != null
                ? (ThrowableDefinition[])available.Clone()
                : new ThrowableDefinition[0];
            _wheelHoldSeconds = Mathf.Max(0.1f, wheelHoldSeconds);
            ClampSelection();
            Changed?.Invoke(this);
        }

        public void Unequip()
        {
            if (!IsEquipped && !IsWheelOpen)
            {
                return;
            }

            IsEquipped = false;
            IsWheelOpen = false;
            Changed?.Invoke(this);
        }

        private void HandleSnapshot(InputSnapshot snapshot)
        {
            if (snapshot.GameplaySuppressed || snapshot.ActiveMap != GameplayInputMap.FPS ||
                !IsPossessedPawn())
            {
                Unequip();
                return;
            }

            InputButtonState grenade = snapshot.FPS.Grenade;
            if (grenade.WasPressedThisFrame)
            {
                _pressedAt = Time.unscaledTime;
                LastError = string.Empty;
            }

            if (grenade.IsPressed && !IsWheelOpen &&
                Time.unscaledTime - _pressedAt >= _wheelHoldSeconds)
            {
                IsWheelOpen = true;
                IsEquipped = false;
                _wheelSelectionVector = Vector2.zero;
                EnsureSelection();
                UpdateWheelSelection(snapshot.FPS.Look);
                Changed?.Invoke(this);
            }
            else if (grenade.IsPressed && IsWheelOpen)
            {
                UpdateWheelSelection(snapshot.FPS.Look);
            }

            if (grenade.WasReleasedThisFrame)
            {
                if (IsWheelOpen)
                {
                    IsWheelOpen = false;
                    IsEquipped = Selected != null;
                    if (!IsEquipped)
                    {
                        LastError = "No throwable is configured for this hero.";
                    }
                    Changed?.Invoke(this);
                }
                else
                {
                    CycleSelection();
                }
            }

            if (snapshot.FPS.Slot1.WasPressedThisFrame ||
                snapshot.FPS.Slot2.WasPressedThisFrame ||
                snapshot.FPS.Slot3.WasPressedThisFrame)
            {
                Unequip();
            }

            if (IsEquipped && snapshot.FPS.Primary.WasPressedThisFrame)
            {
                TryThrow();
            }
        }

        private void CycleSelection()
        {
            if (_available.Length == 0)
            {
                _selectedIndex = -1;
                IsEquipped = false;
                LastError = "No throwable is configured for this hero.";
                Changed?.Invoke(this);
                return;
            }

            int next = _selectedIndex;
            for (int i = 0; i < _available.Length; i++)
            {
                next = (next + 1 + _available.Length) % _available.Length;
                if (_available[next] != null)
                {
                    _selectedIndex = next;
                    IsEquipped = true;
                    LastError = string.Empty;
                    Changed?.Invoke(this);
                    return;
                }
            }

            _selectedIndex = -1;
            IsEquipped = false;
            LastError = "No throwable is configured for this hero.";
            Changed?.Invoke(this);
        }

        private void UpdateWheelSelection(Vector2 lookDelta)
        {
            if (_inputRouter == null || _available.Length == 0)
            {
                return;
            }

            _wheelSelectionVector = Vector2.ClampMagnitude(
                _wheelSelectionVector + lookDelta * 12f,
                Mathf.Max(145f, _selectionDeadZone + 1f));
            Vector2 direction = _wheelSelectionVector;
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
            int clockwise = Mathf.RoundToInt(
                normalized / (Mathf.PI * 2f) * _available.Length) % _available.Length;
            int index = (_available.Length - clockwise) % _available.Length;
            if (_available[index] != null && _selectedIndex != index)
            {
                _selectedIndex = index;
                Changed?.Invoke(this);
            }
        }

        private bool TryThrow()
        {
            ThrowableDefinition definition = Selected;
            if (definition == null || !AppRoot.TryGetInstance(out AppRoot root) ||
                root.Services == null)
            {
                LastError = "Throwable or resource service is unavailable.";
                Changed?.Invoke(this);
                return false;
            }

            ResourceService resources = root.Services.ResourceService;
            if (!resources.TrySpend(definition.UseCost))
            {
                LastError = "Insufficient resources for this throwable.";
                Changed?.Invoke(this);
                return false;
            }

            Camera camera = ResolveAimCamera();
            Vector3 direction = camera != null ? camera.transform.forward : transform.forward;
            Vector3 origin = _throwAnchor != null
                ? _throwAnchor.position
                : camera != null
                    ? camera.transform.position + direction * 0.5f
                    : transform.position + Vector3.up + direction * 0.5f;
            GameObject instance = null;
            try
            {
                instance = definition.ProjectilePrefab != null
                    ? Instantiate(
                        definition.ProjectilePrefab,
                        origin,
                        Quaternion.LookRotation(direction))
                    : new GameObject(
                        definition.DisplayName + "_Runtime",
                        typeof(ThrowableProjectile));
                instance.transform.SetPositionAndRotation(
                    origin,
                    Quaternion.LookRotation(direction));
                ThrowableProjectile projectile =
                    instance.GetComponent<ThrowableProjectile>() ??
                    instance.AddComponent<ThrowableProjectile>();
                projectile.Launch(
                    _identity,
                    _faction != null ? _faction.Faction : FactionId.Neutral,
                    definition,
                    direction,
                    _hitMask);
            }
            catch (Exception exception)
            {
                if (instance != null)
                {
                    Destroy(instance);
                }
                resources.Refund(definition.UseCost);
                LastError = "Throwable launch failed: " + exception.Message;
                Changed?.Invoke(this);
                return false;
            }

            LastError = string.Empty;
            Changed?.Invoke(this);
            return true;
        }

        private void EnsureSelection()
        {
            ClampSelection();
            if (Selected != null)
            {
                return;
            }

            for (int i = 0; i < _available.Length; i++)
            {
                if (_available[i] != null)
                {
                    _selectedIndex = i;
                    return;
                }
            }
        }

        private void ClampSelection()
        {
            if (_available == null || _available.Length == 0)
            {
                _selectedIndex = -1;
                IsEquipped = false;
                return;
            }

            if (_selectedIndex >= _available.Length)
            {
                _selectedIndex = -1;
            }
        }

        private Camera ResolveAimCamera()
        {
            if (_aimCamera == null && AppRoot.TryGetInstance(out AppRoot root) &&
                root.Services != null && root.Services.CurrentSceneContext != null)
            {
                _aimCamera = root.Services.CurrentSceneContext.MainCamera;
            }
            return _aimCamera;
        }

        private bool IsPossessedPawn()
        {
            return AppRoot.TryGetInstance(out AppRoot root) && root.Services != null &&
                   root.Services.PossessionService.CurrentPawn != null &&
                   root.Services.PossessionService.CurrentPawn.gameObject == gameObject;
        }

        private void TryBindInput()
        {
            if (!AppRoot.TryGetInstance(out AppRoot root) || root.Services == null ||
                root.Services.InputRouter == null)
            {
                return;
            }

            InputRouter next = root.Services.InputRouter;
            if (_inputRouter == next)
            {
                return;
            }
            if (_inputRouter != null)
            {
                _inputRouter.SnapshotReady -= HandleSnapshot;
            }
            _inputRouter = next;
            _inputRouter.SnapshotReady += HandleSnapshot;
        }

        private void CacheComponents()
        {
            if (_identity == null) _identity = GetComponent<EntityIdentity>();
            if (_faction == null) _faction = GetComponent<FactionMember>();
        }
    }
}
