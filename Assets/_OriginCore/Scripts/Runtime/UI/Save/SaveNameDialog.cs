using System;
using OriginCore.Save;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OriginCore.UI.Save
{
    [DisallowMultipleComponent]
    public sealed class SaveNameDialog : MonoBehaviour
    {
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private TMP_InputField _nameInput;
        [SerializeField] private TMP_Text _statusLabel;
        [SerializeField] private Button _confirmButton;
        [SerializeField] private Button _cancelButton;

        private Action<string> _confirmAction;
        private bool _buttonsBound;

        public bool IsOpen => gameObject.activeSelf;
        public TMP_InputField NameInput => _nameInput;

        private void OnEnable()
        {
            BindButtons();
        }

        private void OnDestroy()
        {
            if (!_buttonsBound)
            {
                return;
            }

            if (_confirmButton != null) _confirmButton.onClick.RemoveListener(Confirm);
            if (_cancelButton != null) _cancelButton.onClick.RemoveListener(Cancel);
        }

        public void Configure(
            TMP_Text titleLabel,
            TMP_InputField nameInput,
            TMP_Text statusLabel,
            Button confirmButton,
            Button cancelButton)
        {
            _titleLabel = titleLabel;
            _nameInput = nameInput;
            _statusLabel = statusLabel;
            _confirmButton = confirmButton;
            _cancelButton = cancelButton;
            if (_nameInput != null)
            {
                _nameInput.characterLimit = SaveGameData.MaximumDisplayNameLength;
            }

            if (Application.isPlaying)
            {
                BindButtons();
            }
        }

        public void Show(string title, string initialName, Action<string> confirmAction)
        {
            _confirmAction = confirmAction;
            if (_titleLabel != null)
            {
                _titleLabel.text = string.IsNullOrWhiteSpace(title) ? "SAVE NAME" : title;
            }

            if (_statusLabel != null)
            {
                _statusLabel.text = string.Empty;
            }

            if (_nameInput != null)
            {
                _nameInput.text = initialName ?? string.Empty;
            }

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            _nameInput?.ActivateInputField();
            _nameInput?.Select();
        }

        public bool TryHandleEscape()
        {
            if (!IsOpen)
            {
                return false;
            }

            Cancel();
            return true;
        }

        public void Confirm()
        {
            string entered = _nameInput != null ? _nameInput.text : string.Empty;
            if (string.IsNullOrWhiteSpace(entered))
            {
                if (_statusLabel != null)
                {
                    _statusLabel.text = "ENTER A SAVE NAME";
                }

                return;
            }

            Action<string> action = _confirmAction;
            string normalized = SaveGameData.NormalizeDisplayName(entered);
            Close();
            action?.Invoke(normalized);
        }

        public void Cancel()
        {
            Close();
        }

        private void Close()
        {
            _confirmAction = null;
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        private void BindButtons()
        {
            if (_buttonsBound || _confirmButton == null || _cancelButton == null)
            {
                return;
            }

            _confirmButton.onClick.AddListener(Confirm);
            _cancelButton.onClick.AddListener(Cancel);
            _buttonsBound = true;
        }
    }
}
