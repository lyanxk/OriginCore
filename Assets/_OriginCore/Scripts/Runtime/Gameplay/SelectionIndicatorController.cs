using System;
using UnityEngine;

namespace OriginCore.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class SelectionIndicatorController : MonoBehaviour
    {
        [SerializeField] private GameObject _indicatorRoot;
        [SerializeField] private Renderer[] _renderers = Array.Empty<Renderer>();
        [SerializeField] private Material _previewMaterial;
        [SerializeField] private Material _selectedMaterial;
        [SerializeField] private Material _inspectedMaterial;
        [SerializeField] private SelectionVisualState _state;

        public event Action<SelectionIndicatorController, SelectionVisualState, SelectionVisualState>
            StateChanged;

        public GameObject IndicatorRoot => _indicatorRoot;
        public Material PreviewMaterial => _previewMaterial;
        public Material SelectedMaterial => _selectedMaterial;
        public Material InspectedMaterial => _inspectedMaterial;
        public SelectionVisualState State => _state;

        private void OnEnable()
        {
            ApplyState();
        }

        private void OnDisable()
        {
            if (_indicatorRoot != null)
            {
                _indicatorRoot.SetActive(false);
            }
        }

        public void Configure(
            GameObject indicatorRoot,
            Material previewMaterial,
            Material selectedMaterial,
            Material inspectedMaterial)
        {
            _indicatorRoot = indicatorRoot;
            _previewMaterial = previewMaterial;
            _selectedMaterial = selectedMaterial;
            _inspectedMaterial = inspectedMaterial;
            CacheRenderers();
            ApplyState();
        }

        public bool SetState(SelectionVisualState state)
        {
            if (_state == state)
            {
                ApplyState();
                return false;
            }

            SelectionVisualState previous = _state;
            _state = state;
            ApplyState();
            StateChanged?.Invoke(this, previous, _state);
            return true;
        }

        [ContextMenu("Debug/State/Normal")]
        private void DebugNormal()
        {
            SetState(SelectionVisualState.Normal);
        }

        [ContextMenu("Debug/State/Preview")]
        private void DebugPreview()
        {
            SetState(SelectionVisualState.Preview);
        }

        [ContextMenu("Debug/State/Selected")]
        private void DebugSelected()
        {
            SetState(SelectionVisualState.Selected);
        }

        [ContextMenu("Debug/State/Inspected")]
        private void DebugInspected()
        {
            SetState(SelectionVisualState.Inspected);
        }

        private void CacheRenderers()
        {
            _renderers = _indicatorRoot == null
                ? Array.Empty<Renderer>()
                : _indicatorRoot.GetComponentsInChildren<Renderer>(true);
        }

        private void ApplyState()
        {
            if (_indicatorRoot == null)
            {
                return;
            }

            bool visible = _state != SelectionVisualState.Normal;
            _indicatorRoot.SetActive(visible);
            if (!visible)
            {
                return;
            }

            if (_renderers == null || _renderers.Length == 0)
            {
                CacheRenderers();
            }

            Material material = ResolveMaterial(_state);
            if (material == null)
            {
                return;
            }

            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null)
                {
                    _renderers[i].sharedMaterial = material;
                }
            }
        }

        private Material ResolveMaterial(SelectionVisualState state)
        {
            switch (state)
            {
                case SelectionVisualState.Preview:
                    return _previewMaterial != null ? _previewMaterial : _selectedMaterial;
                case SelectionVisualState.Selected:
                    return _selectedMaterial;
                case SelectionVisualState.Inspected:
                    return _inspectedMaterial;
                default:
                    return null;
            }
        }
    }
}
