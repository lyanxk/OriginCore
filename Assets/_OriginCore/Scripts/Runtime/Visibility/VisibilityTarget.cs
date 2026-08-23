using System.Collections.Generic;
using OriginCore.Combat;
using OriginCore.Core;
using OriginCore.Gameplay;
using UnityEngine;

namespace OriginCore.Visibility
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EntityIdentity), typeof(FactionMember))]
    public sealed class VisibilityTarget : MonoBehaviour
    {
        private struct RendererState
        {
            public Renderer Renderer;
            public bool Enabled;
        }

        private struct CanvasState
        {
            public Canvas Canvas;
            public bool Enabled;
        }

        [SerializeField] private EntityIdentity _identity;
        [SerializeField] private FactionMember _faction;
        [SerializeField] private VitalsComponent _vitals;
        [SerializeField] private Selectable _selectable;
        [SerializeField] private Collider _selectionCollider;
        [SerializeField] private bool _rememberAsBuilding;

        private RendererState[] _renderers = new RendererState[0];
        private CanvasState[] _worldCanvases = new CanvasState[0];
        private VisibilitySystem _system;
        private bool _selectableWasEnabled;
        private bool _selectionColliderWasEnabled;
        private bool _hasSuppressedState;
        private bool _isVisible = true;

        public EntityIdentity Identity => _identity;
        public FactionMember Faction => _faction;
        public VitalsComponent Vitals => _vitals;
        public Selectable Selectable => _selectable;
        public Collider SelectionCollider => _selectionCollider;
        public bool RememberAsBuilding => _rememberAsBuilding;
        public VisibilitySystem System => _system;
        public bool IsVisible => _isVisible;
        public bool IsAlive => _vitals == null || _vitals.IsAlive;

        private void Awake()
        {
            CacheComponents();
            CacheVisuals();
        }

        private void OnEnable()
        {
            CacheComponents();
            CacheVisuals();
            TryBindSystem();
        }

        private void Start()
        {
            TryBindSystem();
        }

        private void OnDisable()
        {
            BindSystem(null);
            RestoreSuppressedState();
        }

        private void OnValidate()
        {
            CacheComponents();
        }

        public void Configure(
            EntityIdentity identity,
            FactionMember faction,
            VitalsComponent vitals,
            Selectable selectable,
            Collider selectionCollider,
            bool rememberAsBuilding)
        {
            _identity = identity != null ? identity : GetComponent<EntityIdentity>();
            _faction = faction != null ? faction : GetComponent<FactionMember>();
            _vitals = vitals != null ? vitals : GetComponent<VitalsComponent>();
            _selectable = selectable != null ? selectable : GetComponent<Selectable>();
            _selectionCollider = selectionCollider != null
                ? selectionCollider
                : _selectable != null
                    ? _selectable.SelectionCollider
                    : null;
            _rememberAsBuilding = rememberAsBuilding;
            CacheVisuals();
        }

        public bool BindSystem(VisibilitySystem system)
        {
            if (_system == system)
            {
                return _system != null;
            }

            if (_system != null)
            {
                _system.UnregisterTarget(this);
            }

            _system = system;
            if (_system != null && isActiveAndEnabled)
            {
                _system.RegisterTarget(this);
            }

            return _system != null;
        }

        public bool ApplyVisibility(bool visible)
        {
            if (visible)
            {
                if (_isVisible && !_hasSuppressedState)
                {
                    return false;
                }

                RestoreSuppressedState();
                _isVisible = true;
                return true;
            }

            if (!_hasSuppressedState)
            {
                CaptureAndSuppressState();
                _isVisible = false;
                return true;
            }

            EnforceSuppressedState();
            _isVisible = false;
            return false;
        }

        public bool IsVisibleTo(FactionId observerFaction)
        {
            return _system == null || observerFaction != _system.ObserverFaction || _isVisible;
        }

        private bool TryBindSystem()
        {
            if (_system != null)
            {
                return true;
            }

            if (!AppRoot.TryGetInstance(out AppRoot appRoot) || appRoot.Services == null ||
                appRoot.Services.CurrentSceneContext == null)
            {
                return false;
            }

            return BindSystem(appRoot.Services.CurrentSceneContext.VisibilitySystem);
        }

        private void CacheComponents()
        {
            if (_identity == null)
            {
                _identity = GetComponent<EntityIdentity>();
            }

            if (_faction == null)
            {
                _faction = GetComponent<FactionMember>();
            }

            if (_vitals == null)
            {
                _vitals = GetComponent<VitalsComponent>();
            }

            if (_selectable == null)
            {
                _selectable = GetComponent<Selectable>();
            }

            if (_selectionCollider == null && _selectable != null)
            {
                _selectionCollider = _selectable.SelectionCollider;
            }
        }

        private void CacheVisuals()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            _renderers = new RendererState[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                _renderers[i].Renderer = renderers[i];
                _renderers[i].Enabled = renderers[i] != null && renderers[i].enabled;
            }

            Canvas[] canvases = GetComponentsInChildren<Canvas>(true);
            List<CanvasState> worldCanvases = new List<CanvasState>(canvases.Length);
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas != null && canvas.renderMode == RenderMode.WorldSpace)
                {
                    worldCanvases.Add(new CanvasState
                    {
                        Canvas = canvas,
                        Enabled = canvas.enabled
                    });
                }
            }

            _worldCanvases = worldCanvases.ToArray();
        }

        private void CaptureAndSuppressState()
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer renderer = _renderers[i].Renderer;
                if (renderer == null)
                {
                    continue;
                }

                _renderers[i].Enabled = renderer.enabled;
                renderer.enabled = false;
            }

            for (int i = 0; i < _worldCanvases.Length; i++)
            {
                Canvas canvas = _worldCanvases[i].Canvas;
                if (canvas == null)
                {
                    continue;
                }

                _worldCanvases[i].Enabled = canvas.enabled;
                canvas.enabled = false;
            }

            _selectableWasEnabled = _selectable != null && _selectable.enabled;
            if (_selectable != null)
            {
                _selectable.SetState(SelectionVisualState.Normal);
                _selectable.enabled = false;
            }

            _selectionColliderWasEnabled =
                _selectionCollider != null && _selectionCollider.enabled;
            if (_selectionCollider != null)
            {
                _selectionCollider.enabled = false;
            }

            _hasSuppressedState = true;
        }

        private void EnforceSuppressedState()
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i].Renderer != null)
                {
                    _renderers[i].Renderer.enabled = false;
                }
            }

            for (int i = 0; i < _worldCanvases.Length; i++)
            {
                if (_worldCanvases[i].Canvas != null)
                {
                    _worldCanvases[i].Canvas.enabled = false;
                }
            }

            if (_selectable != null)
            {
                _selectable.enabled = false;
            }

            if (_selectionCollider != null)
            {
                _selectionCollider.enabled = false;
            }
        }

        private void RestoreSuppressedState()
        {
            if (!_hasSuppressedState)
            {
                _isVisible = true;
                return;
            }

            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i].Renderer != null)
                {
                    _renderers[i].Renderer.enabled = _renderers[i].Enabled;
                }
            }

            for (int i = 0; i < _worldCanvases.Length; i++)
            {
                if (_worldCanvases[i].Canvas != null)
                {
                    _worldCanvases[i].Canvas.enabled = _worldCanvases[i].Enabled;
                }
            }

            if (_selectable != null)
            {
                _selectable.enabled = _selectableWasEnabled;
            }

            if (_selectionCollider != null)
            {
                _selectionCollider.enabled = _selectionColliderWasEnabled;
            }

            _hasSuppressedState = false;
            _isVisible = true;
        }
    }
}
