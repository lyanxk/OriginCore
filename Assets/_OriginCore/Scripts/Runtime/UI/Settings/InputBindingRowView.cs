using OriginCore.Input;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OriginCore.UI.Settings
{
    [DisallowMultipleComponent]
    public sealed class InputBindingRowView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _actionLabel;
        [SerializeField] private TMP_Text _bindingLabel;
        [SerializeField] private Button _rebindButton;

        private InputRebindService _service;
        private string _mapName = string.Empty;
        private string _actionName = string.Empty;
        private int _bindingIndex = -1;
        private bool _buttonBound;

        private void Awake()
        {
            BindButton();
        }

        private void OnDestroy()
        {
            if (_buttonBound && _rebindButton != null)
            {
                _rebindButton.onClick.RemoveListener(BeginRebind);
            }
        }

        public void Configure(
            TMP_Text actionLabel,
            TMP_Text bindingLabel,
            Button rebindButton)
        {
            _actionLabel = actionLabel;
            _bindingLabel = bindingLabel;
            _rebindButton = rebindButton;
            if (Application.isPlaying)
            {
                BindButton();
            }
        }

        public void Bind(
            InputRebindService service,
            string mapName,
            string actionName,
            int bindingIndex,
            string displayName)
        {
            _service = service;
            _mapName = mapName ?? string.Empty;
            _actionName = actionName ?? string.Empty;
            _bindingIndex = bindingIndex;
            if (_actionLabel != null)
            {
                _actionLabel.text = displayName ?? string.Empty;
            }

            RefreshBindingLabel();
        }

        public void RefreshBindingLabel()
        {
            if (_bindingLabel == null)
            {
                return;
            }

            InputActionAsset asset = _service != null ? _service.ActionsAsset : null;
            InputAction action = asset?.FindActionMap(_mapName, false)?.FindAction(_actionName, false);
            if (action == null || _bindingIndex < 0 || _bindingIndex >= action.bindings.Count)
            {
                _bindingLabel.text = "UNAVAILABLE";
                return;
            }

            _bindingLabel.text = action.GetBindingDisplayString(
                _bindingIndex,
                InputBinding.DisplayStringOptions.DontOmitDevice);
        }

        private void BeginRebind()
        {
            if (_service == null || _bindingLabel == null)
            {
                return;
            }

            if (_service.TryBeginInteractiveRebind(
                    _mapName,
                    _actionName,
                    _bindingIndex,
                    out string error))
            {
                _bindingLabel.text = "PRESS INPUT...  ESC TO CANCEL";
            }
            else
            {
                _bindingLabel.text = string.IsNullOrWhiteSpace(error) ? "REBIND FAILED" : error;
            }
        }

        private void BindButton()
        {
            if (_buttonBound || _rebindButton == null)
            {
                return;
            }

            _rebindButton.onClick.AddListener(BeginRebind);
            _buttonBound = true;
        }
    }
}
