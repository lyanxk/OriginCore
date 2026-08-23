using System;
using System.Collections.Generic;
using OriginCore.Core;
using OriginCore.Debugging;
using OriginCore.Gameplay;
using OriginCore.Input;
using OriginCore.UI;
using OriginCore.UI.RTS;
using UnityEngine;

namespace OriginCore.RTS
{
    [DisallowMultipleComponent]
    public sealed class SelectionService : MonoBehaviour
    {
        private const float ColliderlessClickRadiusPixels = 36f;

        [SerializeField] private Camera _worldCamera;
        [SerializeField] private SelectionBoxView _selectionBoxView;
        [SerializeField] private FactionId _observerFaction = FactionId.Friendly;
        [SerializeField] private LayerMask _selectionMask = ~0;
        [Min(0.1f), SerializeField] private float _maximumRayDistance = 500f;
        [Min(0f), SerializeField] private float _dragThresholdPixels = 8f;

        private readonly List<Selectable> _selected = new List<Selectable>(16);
        private readonly HashSet<Selectable> _selectedMembership = new HashSet<Selectable>();
        private readonly List<Selectable> _preview = new List<Selectable>(32);
        private readonly List<Selectable> _candidateBuffer = new List<Selectable>(64);
        private readonly List<Selectable> _commitBuffer = new List<Selectable>(64);
        private readonly List<MonoBehaviour> _componentBuffer = new List<MonoBehaviour>(8);
        private readonly RaycastHit[] _clickHitBuffer = new RaycastHit[16];

        private InputRouter _inputRouter;
        private GameModeController _modeController;
        private EntityRegistry _entityRegistry;
        private FactionRelationService _factionRelations;
        private IDebugOverlayService _debugOverlay;
        private Selectable _inspected;
        private Selectable _hovered;
        private UnitHoverIndicatorView _hoverIndicatorView;
        private bool _pointerDown;
        private bool _isDragging;
        private bool _pointerSelectionSuppressed;
        private Vector2 _dragStart;
        private Rect _currentDragRect;

        public event Action SelectionChanged;

        public Camera WorldCamera => _worldCamera;
        public SelectionBoxView SelectionBoxView => _selectionBoxView;
        public FactionId ObserverFaction => _observerFaction;
        public LayerMask SelectionMask => _selectionMask;
        public float MaximumRayDistance => _maximumRayDistance;
        public float DragThresholdPixels => _dragThresholdPixels;
        public IReadOnlyList<Selectable> Selected => _selected;
        public Selectable Inspected => _inspected;
        public Selectable Hovered => _hovered;
        public UnitHoverIndicatorView HoverIndicatorView => _hoverIndicatorView;
        public int SelectedCount => _selected.Count;
        public int PreviewCount => _preview.Count;
        public bool IsDragging => _isDragging;
        public bool PointerSelectionSuppressed => _pointerSelectionSuppressed;
        public Rect CurrentDragRect => _currentDragRect;
        public bool IsServiceBound => _inputRouter != null && _modeController != null &&
                                      _entityRegistry != null && _factionRelations != null;

        private void OnEnable()
        {
            _selectionBoxView?.Hide();
            TryBindServices();
        }

        private void Start()
        {
            if (!IsServiceBound)
            {
                TryBindServices();
            }
        }

        private void OnDisable()
        {
            UnbindServices();
            _pointerSelectionSuppressed = false;
            ClearHover();
            CancelPointerInteraction();
            ClearSelectionState(false);
        }

        private void OnValidate()
        {
            _maximumRayDistance = Mathf.Max(0.1f, _maximumRayDistance);
            _dragThresholdPixels = Mathf.Max(0f, _dragThresholdPixels);
        }

        public void Configure(
            Camera worldCamera,
            SelectionBoxView selectionBoxView,
            FactionId observerFaction,
            LayerMask selectionMask,
            float maximumRayDistance = 500f,
            float dragThresholdPixels = 8f)
        {
            _worldCamera = worldCamera;
            _selectionBoxView = selectionBoxView;
            _observerFaction = observerFaction;
            _selectionMask = selectionMask;
            _maximumRayDistance = Mathf.Max(0.1f, maximumRayDistance);
            _dragThresholdPixels = Mathf.Max(0f, dragThresholdPixels);
            _selectionBoxView?.Hide();
        }

        public bool TryBindServices()
        {
            if (!AppRoot.TryGetInstance(out AppRoot appRoot) || appRoot.Services == null)
            {
                return false;
            }

            GameServices services = appRoot.Services;
            if (services.InputRouter == null || services.GameModeController == null ||
                services.EntityRegistry == null || services.FactionRelations == null)
            {
                return false;
            }

            if (_inputRouter == services.InputRouter &&
                _modeController == services.GameModeController &&
                _entityRegistry == services.EntityRegistry &&
                _factionRelations == services.FactionRelations)
            {
                return true;
            }

            UnbindServices();
            _inputRouter = services.InputRouter;
            _modeController = services.GameModeController;
            _entityRegistry = services.EntityRegistry;
            _factionRelations = services.FactionRelations;
            _debugOverlay = services.DebugOverlay;
            _inputRouter.SnapshotReady += HandleSnapshotReady;
            _modeController.ModeChanged += HandleModeChanged;
            _entityRegistry.EntityUnregistered += HandleEntityUnregistered;
            PublishSelectionChanged();
            return true;
        }

        public bool IsSelected(Selectable selectable)
        {
            return selectable != null && _selectedMembership.Contains(selectable);
        }

        public bool SelectOnly(Selectable selectable)
        {
            if (selectable == null || !selectable.IsAlive || _factionRelations == null ||
                !selectable.CanBeCommandedBy(_observerFaction, _factionRelations))
            {
                return false;
            }

            bool changed = ClearSelectedInternal();
            changed |= ClearInspectedInternal();
            changed |= AddSelectedInternal(selectable);
            if (changed)
            {
                PublishSelectionChanged();
            }

            return changed;
        }

        public bool ClickAtScreenPoint(Vector2 screenPoint, bool append)
        {
            if (_worldCamera == null || _factionRelations == null)
            {
                return false;
            }

            Ray ray = _worldCamera.ScreenPointToRay(screenPoint);
            Selectable hitSelectable = null;
            float nearestDistance = float.PositiveInfinity;
            int hitCount = Physics.RaycastNonAlloc(
                ray,
                _clickHitBuffer,
                _maximumRayDistance,
                _selectionMask,
                QueryTriggerInteraction.Collide);
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _clickHitBuffer[i];
                if (hit.collider == null || hit.distance >= nearestDistance ||
                    !Selectable.TryResolve(hit.collider, out Selectable candidate))
                {
                    continue;
                }

                nearestDistance = hit.distance;
                hitSelectable = candidate;
            }

            if (hitSelectable == null)
            {
                hitSelectable = FindColliderlessClickCandidate(screenPoint);
            }

            if (hitSelectable == null || !hitSelectable.IsAlive)
            {
                return append ? false : ClearSelectionState(true);
            }

            if (hitSelectable.CanBeCommandedBy(_observerFaction, _factionRelations))
            {
                bool changed = false;
                if (!append)
                {
                    changed |= ClearSelectedInternal();
                }

                changed |= ClearInspectedInternal();
                changed |= AddSelectedInternal(hitSelectable);
                if (changed)
                {
                    PublishSelectionChanged();
                }

                return changed;
            }

            bool isHostile = hitSelectable.Faction != null &&
                             _factionRelations.AreHostile(
                                 _observerFaction,
                                 hitSelectable.Faction.Faction);
            if (!isHostile)
            {
                return append ? false : ClearSelectionState(true);
            }

            bool inspectionChanged = false;
            if (!append)
            {
                inspectionChanged |= ClearSelectedInternal();
            }

            inspectionChanged |= SetInspectedInternal(hitSelectable);
            if (inspectionChanged)
            {
                PublishSelectionChanged();
            }

            return inspectionChanged;
        }

        private Selectable FindColliderlessClickCandidate(Vector2 screenPoint)
        {
            if (_entityRegistry == null || _worldCamera == null)
            {
                return null;
            }

            _entityRegistry.CollectCommandable(_observerFaction, _candidateBuffer);
            Selectable nearest = null;
            float nearestSqrDistance =
                ColliderlessClickRadiusPixels * ColliderlessClickRadiusPixels;
            for (int i = 0; i < _candidateBuffer.Count; i++)
            {
                Selectable candidate = _candidateBuffer[i];
                if (candidate == null || !candidate.IsAlive ||
                    candidate.GetComponentInChildren<Collider>() != null)
                {
                    continue;
                }

                Vector3 projected = _worldCamera.WorldToScreenPoint(
                    GetSelectionWorldPoint(candidate));
                if (projected.z <= 0f)
                {
                    continue;
                }

                float sqrDistance = ((Vector2)projected - screenPoint).sqrMagnitude;
                if (sqrDistance > nearestSqrDistance)
                {
                    continue;
                }

                nearestSqrDistance = sqrDistance;
                nearest = candidate;
            }

            return nearest;
        }

        public int PreviewScreenRect(Rect screenRect)
        {
            ClearPreviewInternal();
            if (_worldCamera == null || _entityRegistry == null || _factionRelations == null)
            {
                return 0;
            }

            _commitBuffer.Clear();
            bool containsUnit = false;
            _entityRegistry.CollectCommandable(_observerFaction, _candidateBuffer);
            for (int i = 0; i < _candidateBuffer.Count; i++)
            {
                Selectable candidate = _candidateBuffer[i];
                if (candidate == null ||
                    !SelectionBoxCalculator.ContainsWorldPoint(
                        _worldCamera,
                        screenRect,
                        GetSelectionWorldPoint(candidate)))
                {
                    continue;
                }

                _commitBuffer.Add(candidate);
                containsUnit |= !IsBuilding(candidate);
            }

            for (int i = 0; i < _commitBuffer.Count; i++)
            {
                Selectable candidate = _commitBuffer[i];
                if (containsUnit && IsBuilding(candidate))
                {
                    continue;
                }

                _preview.Add(candidate);
                candidate.SetState(SelectionVisualState.Preview);
            }

            return _preview.Count;
        }

        public bool CommitPreview(bool append)
        {
            _commitBuffer.Clear();
            for (int i = 0; i < _preview.Count; i++)
            {
                Selectable candidate = _preview[i];
                if (candidate != null)
                {
                    _commitBuffer.Add(candidate);
                }
            }

            ClearPreviewInternal();
            return ApplyCommandSelection(_commitBuffer, append);
        }

        public int SelectIdleWorkers()
        {
            return SelectShortcut(UnitRole.Worker, true);
        }

        public int SelectCombatAndHero()
        {
            return SelectShortcut(UnitRole.Combat | UnitRole.Hero, false);
        }

        public bool ClearSelection()
        {
            CancelPointerInteraction();
            return ClearSelectionState(true);
        }

        public void CancelPointerInteraction()
        {
            _pointerDown = false;
            _isDragging = false;
            _currentDragRect = default(Rect);
            ClearPreviewInternal();
            _selectionBoxView?.Hide();
        }

        public bool SetPointerSelectionSuppressed(bool suppressed)
        {
            if (_pointerSelectionSuppressed == suppressed)
            {
                if (suppressed)
                {
                    ClearHover();
                    if (_pointerDown || _isDragging)
                    {
                        CancelPointerInteraction();
                    }
                }

                return false;
            }

            _pointerSelectionSuppressed = suppressed;
            if (suppressed)
            {
                ClearHover();
                CancelPointerInteraction();
            }

            return true;
        }

        private void UnbindServices()
        {
            if (_inputRouter != null)
            {
                _inputRouter.SnapshotReady -= HandleSnapshotReady;
            }

            if (_modeController != null)
            {
                _modeController.ModeChanged -= HandleModeChanged;
            }

            if (_entityRegistry != null)
            {
                _entityRegistry.EntityUnregistered -= HandleEntityUnregistered;
            }

            _inputRouter = null;
            _modeController = null;
            _entityRegistry = null;
            _factionRelations = null;
            _debugOverlay = null;
        }

        private void HandleSnapshotReady(InputSnapshot snapshot)
        {
            PruneInvalidSelection();
            if (snapshot.GameplaySuppressed || snapshot.ActiveMap != GameplayInputMap.RTS ||
                _modeController == null || _modeController.CurrentMode != GameMode.RTS)
            {
                ClearHover();
                if (_pointerDown || _isDragging)
                {
                    CancelPointerInteraction();
                }

                return;
            }

            RtsInputSnapshot input = snapshot.RTS;
            if (_pointerSelectionSuppressed)
            {
                ClearHover();
                if (_pointerDown || _isDragging)
                {
                    CancelPointerInteraction();
                }

                return;
            }

            UpdateHover(input.Point);

            if (input.SelectIdleWorkers.WasPressedThisFrame)
            {
                SelectIdleWorkers();
                return;
            }

            if (input.SelectCombatUnits.WasPressedThisFrame)
            {
                SelectCombatAndHero();
                return;
            }

            ProcessPointerInput(input);
        }

        private void HandleModeChanged(GameModeChange change)
        {
            if (change.Current != GameMode.RTS)
            {
                ClearHover();
                CancelPointerInteraction();
            }
        }

        private void HandleEntityUnregistered(EntityIdentity identity)
        {
            if (identity == null || !identity.TryGetComponent(out Selectable selectable))
            {
                return;
            }

            if (_hovered == selectable)
            {
                ClearHover();
            }

            bool changed = RemoveSelectedInternal(selectable);
            if (_inspected == selectable)
            {
                _inspected = null;
                changed = true;
            }

            if (_preview.Remove(selectable) && selectable != null)
            {
                selectable.SetState(SelectionVisualState.Normal);
            }

            if (changed)
            {
                PublishSelectionChanged();
            }
        }

        private void ProcessPointerInput(RtsInputSnapshot input)
        {
            if (input.Select.WasPressedThisFrame)
            {
                bool pointerOverUi = UiPointerUtility.IsScreenPointOverUi(input.Point);
                _pointerDown = !pointerOverUi;
                _isDragging = false;
                _dragStart = input.Point;
                _currentDragRect = default(Rect);
                ClearPreviewInternal();
                _selectionBoxView?.Hide();
            }

            if (!_pointerDown)
            {
                return;
            }

            if (input.Select.IsPressed)
            {
                if (!_isDragging && SelectionBoxCalculator.ExceedsDragThreshold(
                        _dragStart,
                        input.Point,
                        _dragThresholdPixels))
                {
                    _isDragging = true;
                }

                if (_isDragging)
                {
                    _currentDragRect = SelectionBoxCalculator.FromScreenPoints(
                        _dragStart,
                        input.Point);
                    _selectionBoxView?.Show(_currentDragRect);
                    PreviewScreenRect(_currentDragRect);
                }
            }

            if (!input.Select.WasReleasedThisFrame)
            {
                return;
            }

            bool append = input.QueueModifier.IsPressed;
            if (_isDragging)
            {
                CommitPreview(append);
            }
            else
            {
                ClickAtScreenPoint(input.Point, append);
            }

            _pointerDown = false;
            _isDragging = false;
            _currentDragRect = default(Rect);
            _selectionBoxView?.Hide();
        }

        private int SelectShortcut(UnitRole roles, bool requireIdle)
        {
            _commitBuffer.Clear();
            if (_entityRegistry == null || _factionRelations == null)
            {
                return 0;
            }

            _entityRegistry.CollectCommandable(_observerFaction, _candidateBuffer);
            for (int i = 0; i < _candidateBuffer.Count; i++)
            {
                Selectable candidate = _candidateBuffer[i];
                if (candidate == null || candidate.Identity == null ||
                    !candidate.Identity.HasAnyRole(roles) ||
                    requireIdle && HasCurrentCommand(candidate))
                {
                    continue;
                }

                _commitBuffer.Add(candidate);
            }

            ApplyCommandSelection(_commitBuffer, false);
            return _selected.Count;
        }

        private bool HasCurrentCommand(Selectable selectable)
        {
            _componentBuffer.Clear();
            selectable.GetComponents(_componentBuffer);
            for (int i = 0; i < _componentBuffer.Count; i++)
            {
                if (_componentBuffer[i] is IRtsCommandStatus status && status.HasCurrentCommand)
                {
                    return true;
                }
            }

            return false;
        }

        private bool ApplyCommandSelection(List<Selectable> candidates, bool append)
        {
            bool changed = false;
            if (!append)
            {
                changed |= ClearSelectedInternal();
                changed |= ClearInspectedInternal();
            }
            else if (candidates.Count > 0)
            {
                changed |= ClearInspectedInternal();
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                Selectable candidate = candidates[i];
                if (candidate != null && candidate.IsAlive &&
                    candidate.CanBeCommandedBy(_observerFaction, _factionRelations))
                {
                    changed |= AddSelectedInternal(candidate);
                }
            }

            if (changed)
            {
                PublishSelectionChanged();
            }

            return changed;
        }

        private static bool IsBuilding(Selectable selectable)
        {
            return selectable != null && selectable.Identity != null &&
                   selectable.Identity.Roles.HasAny(UnitRole.Building);
        }

        private bool AddSelectedInternal(Selectable selectable)
        {
            if (selectable == null || !_selectedMembership.Add(selectable))
            {
                return false;
            }

            _selected.Add(selectable);
            selectable.SetState(SelectionVisualState.Selected);
            return true;
        }

        private void UpdateHover(Vector2 screenPoint)
        {
            if (_worldCamera == null || _isDragging ||
                UiPointerUtility.IsScreenPointOverUi(screenPoint))
            {
                ClearHover();
                return;
            }

            Ray ray = _worldCamera.ScreenPointToRay(screenPoint);
            Selectable next = null;
            float nearestDistance = float.PositiveInfinity;
            int hitCount = Physics.RaycastNonAlloc(
                ray,
                _clickHitBuffer,
                _maximumRayDistance,
                _selectionMask,
                QueryTriggerInteraction.Collide);
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _clickHitBuffer[i];
                if (hit.collider == null || hit.distance >= nearestDistance ||
                    !Selectable.TryResolve(hit.collider, out Selectable candidate) ||
                    !IsHoverableEntity(candidate))
                {
                    continue;
                }

                nearestDistance = hit.distance;
                next = candidate;
            }

            if (next == null)
            {
                Selectable colliderless = FindColliderlessClickCandidate(screenPoint);
                if (IsHoverableEntity(colliderless))
                {
                    next = colliderless;
                }
            }

            if (_hovered == next)
            {
                if (_hovered != null)
                {
                    EnsureHoverIndicator().Show(
                        _hovered,
                        ResolveHoverColor(_hovered));
                }

                return;
            }

            _hovered = next;
            if (_hovered == null)
            {
                _hoverIndicatorView?.Hide();
                return;
            }

            EnsureHoverIndicator().Show(
                _hovered,
                ResolveHoverColor(_hovered));
        }

        private UnitHoverIndicatorView EnsureHoverIndicator()
        {
            if (_hoverIndicatorView != null)
            {
                return _hoverIndicatorView;
            }

            Transform existing = transform.Find("UnitHoverIndicator");
            GameObject indicator = existing != null
                ? existing.gameObject
                : new GameObject(
                    "UnitHoverIndicator",
                    typeof(MeshFilter),
                    typeof(MeshRenderer));
            indicator.transform.SetParent(transform, false);
            _hoverIndicatorView = indicator.GetComponent<UnitHoverIndicatorView>() ??
                                  indicator.AddComponent<UnitHoverIndicatorView>();
            return _hoverIndicatorView;
        }

        private void ClearHover()
        {
            _hovered = null;
            _hoverIndicatorView?.Hide();
        }

        private static bool IsHoverableEntity(Selectable selectable)
        {
            return selectable != null && selectable.IsAlive && selectable.Identity != null &&
                   selectable.Identity.Roles.HasAny(
                       UnitRole.Worker | UnitRole.Combat | UnitRole.Hero |
                       UnitRole.Building);
        }

        private Color ResolveHoverColor(Selectable selectable)
        {
            bool hostile = selectable != null && selectable.Faction != null &&
                           _factionRelations != null &&
                           _factionRelations.AreHostile(
                               _observerFaction,
                               selectable.Faction.Faction);
            return hostile
                ? UnitHoverIndicatorView.HostileHoverColor
                : UnitHoverIndicatorView.FriendlyHoverColor;
        }

        private bool RemoveSelectedInternal(Selectable selectable)
        {
            if (selectable == null || !_selectedMembership.Remove(selectable))
            {
                return false;
            }

            _selected.Remove(selectable);
            selectable.SetState(SelectionVisualState.Normal);
            return true;
        }

        private bool ClearSelectedInternal()
        {
            if (_selected.Count == 0)
            {
                _selectedMembership.Clear();
                return false;
            }

            for (int i = 0; i < _selected.Count; i++)
            {
                if (_selected[i] != null)
                {
                    _selected[i].SetState(SelectionVisualState.Normal);
                }
            }

            _selected.Clear();
            _selectedMembership.Clear();
            return true;
        }

        private bool SetInspectedInternal(Selectable selectable)
        {
            if (_inspected == selectable)
            {
                selectable?.SetState(SelectionVisualState.Inspected);
                return false;
            }

            ClearInspectedInternal();
            _inspected = selectable;
            _inspected?.SetState(SelectionVisualState.Inspected);
            return _inspected != null;
        }

        private bool ClearInspectedInternal()
        {
            if (_inspected == null)
            {
                _inspected = null;
                return false;
            }

            Selectable previous = _inspected;
            _inspected = null;
            if (!_selectedMembership.Contains(previous))
            {
                previous.SetState(SelectionVisualState.Normal);
            }

            return true;
        }

        private bool ClearSelectionState(bool publish)
        {
            bool changed = ClearSelectedInternal();
            changed |= ClearInspectedInternal();
            if (changed && publish)
            {
                PublishSelectionChanged();
            }

            return changed;
        }

        private void ClearPreviewInternal()
        {
            for (int i = 0; i < _preview.Count; i++)
            {
                Selectable preview = _preview[i];
                if (preview == null)
                {
                    continue;
                }

                preview.SetState(_selectedMembership.Contains(preview)
                    ? SelectionVisualState.Selected
                    : SelectionVisualState.Normal);
            }

            _preview.Clear();
        }

        private void PruneInvalidSelection()
        {
            bool changed = false;
            for (int i = _selected.Count - 1; i >= 0; i--)
            {
                Selectable selectable = _selected[i];
                if (selectable != null && selectable.isActiveAndEnabled && selectable.IsAlive)
                {
                    continue;
                }

                _selected.RemoveAt(i);
                _selectedMembership.Remove(selectable);
                if (selectable != null)
                {
                    selectable.SetState(SelectionVisualState.Normal);
                }

                changed = true;
            }

            if (_inspected != null &&
                (!_inspected.isActiveAndEnabled || !_inspected.IsAlive))
            {
                _inspected.SetState(SelectionVisualState.Normal);
                _inspected = null;
                changed = true;
            }

            if (changed)
            {
                PublishSelectionChanged();
            }
        }

        private void PublishSelectionChanged()
        {
            if (_debugOverlay != null)
            {
                DebugOverlaySnapshot previous = _debugOverlay.Snapshot;
                _debugOverlay.SetSnapshot(new DebugOverlaySnapshot(
                    previous.Mode,
                    _selected.Count,
                    previous.QueuedCommandCount,
                    previous.ResourceValue));
            }

            SelectionChanged?.Invoke();
        }

        private static Vector3 GetSelectionWorldPoint(Selectable selectable)
        {
            return selectable != null
                ? selectable.GetSelectionWorldPoint()
                : Vector3.zero;
        }
    }
}
