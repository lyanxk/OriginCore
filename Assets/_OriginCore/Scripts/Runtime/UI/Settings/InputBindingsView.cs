using System.Collections.Generic;
using OriginCore.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OriginCore.UI.Settings
{
    [DisallowMultipleComponent]
    public sealed class InputBindingsView : MonoBehaviour
    {
        [SerializeField] private RectTransform _content;
        [SerializeField] private InputBindingRowView _rowTemplate;

        private readonly List<InputBindingRowView> _rows =
            new List<InputBindingRowView>();
        private InputRebindService _service;

        public int RowCount => _rows.Count;

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void Configure(RectTransform content, InputBindingRowView rowTemplate)
        {
            _content = content;
            _rowTemplate = rowTemplate;
        }

        public void Bind(InputRebindService service)
        {
            if (_service != service)
            {
                Unsubscribe();
                _service = service;
            }

            Subscribe();
            Refresh();
        }

        public void Refresh()
        {
            ClearRows();
            if (_service == null || _service.ActionsAsset == null ||
                _content == null || _rowTemplate == null)
            {
                return;
            }

            foreach (InputActionMap map in _service.ActionsAsset.actionMaps)
            {
                foreach (InputAction action in map.actions)
                {
                    for (int bindingIndex = 0; bindingIndex < action.bindings.Count; bindingIndex++)
                    {
                        InputBinding binding = action.bindings[bindingIndex];
                        if (binding.isComposite || string.IsNullOrWhiteSpace(binding.path))
                        {
                            continue;
                        }

                        InputBindingRowView row = Instantiate(_rowTemplate, _content);
                        row.gameObject.name = map.name + "_" + action.name + "_" + bindingIndex;
                        row.gameObject.SetActive(true);
                        string part = binding.isPartOfComposite && !string.IsNullOrWhiteSpace(binding.name)
                            ? " / " + binding.name.ToUpperInvariant()
                            : string.Empty;
                        row.Bind(
                            _service,
                            map.name,
                            action.name,
                            bindingIndex,
                            map.name.ToUpperInvariant() + " / " + action.name.ToUpperInvariant() + part);
                        _rows.Add(row);
                    }
                }
            }
        }

        private void Subscribe()
        {
            if (_service == null)
            {
                return;
            }

            _service.RebindCompleted -= HandleRebindFinished;
            _service.RebindCompleted += HandleRebindFinished;
            _service.RebindCanceled -= HandleRebindFinished;
            _service.RebindCanceled += HandleRebindFinished;
            _service.BindingOverridesChanged -= Refresh;
            _service.BindingOverridesChanged += Refresh;
        }

        private void Unsubscribe()
        {
            if (_service == null)
            {
                return;
            }

            _service.RebindCompleted -= HandleRebindFinished;
            _service.RebindCanceled -= HandleRebindFinished;
            _service.BindingOverridesChanged -= Refresh;
        }

        private void HandleRebindFinished(InputAction action, int bindingIndex)
        {
            Refresh();
        }

        private void ClearRows()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                InputBindingRowView row = _rows[i];
                if (row == null)
                {
                    continue;
                }

                row.gameObject.SetActive(false);
                if (Application.isPlaying)
                {
                    Destroy(row.gameObject);
                }
                else
                {
                    DestroyImmediate(row.gameObject);
                }
            }

            _rows.Clear();
        }
    }
}
