using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OriginCore.UI.Common
{
    [DisallowMultipleComponent]
    public sealed class ConfirmDialog : MonoBehaviour
    {
        public const string DefaultModalId = "Confirm";

        [SerializeField] private ModalStack _modalStack;
        [SerializeField] private string _modalId = DefaultModalId;
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private TMP_Text _messageLabel;
        [SerializeField] private Button _yesButton;
        [SerializeField] private Button _noButton;

        private Action _yesAction;
        private Action _noAction;
        private bool _buttonsBound;

        public TMP_Text TitleLabel => _titleLabel;
        public TMP_Text MessageLabel => _messageLabel;

        private void Awake()
        {
            BindButtons();
        }

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

            if (_yesButton != null)
            {
                _yesButton.onClick.RemoveListener(Confirm);
            }

            if (_noButton != null)
            {
                _noButton.onClick.RemoveListener(Cancel);
            }
            _buttonsBound = false;
        }

        public void Configure(
            ModalStack modalStack,
            string modalId,
            TMP_Text titleLabel,
            TMP_Text messageLabel,
            Button yesButton,
            Button noButton)
        {
            _modalStack = modalStack;
            _modalId = string.IsNullOrWhiteSpace(modalId) ? DefaultModalId : modalId;
            _titleLabel = titleLabel;
            _messageLabel = messageLabel;
            _yesButton = yesButton;
            _noButton = noButton;
            if (Application.isPlaying)
            {
                BindButtons();
            }
        }

        public bool Show(string title, string message, Action yesAction, Action noAction = null)
        {
            if (_modalStack == null)
            {
                return false;
            }

            if (_titleLabel != null)
            {
                _titleLabel.text = title ?? string.Empty;
            }

            if (_messageLabel != null)
            {
                _messageLabel.text = message ?? string.Empty;
            }

            _yesAction = yesAction;
            _noAction = noAction;
            return _modalStack.Show(_modalId);
        }

        public void Confirm()
        {
            Action action = _yesAction;
            ClearActions();
            _modalStack?.CloseTop();
            action?.Invoke();
        }

        public void Cancel()
        {
            Action action = _noAction;
            ClearActions();
            _modalStack?.CloseTop();
            action?.Invoke();
        }

        private void BindButtons()
        {
            if (_buttonsBound || _yesButton == null || _noButton == null)
            {
                return;
            }

            _yesButton.onClick.AddListener(Confirm);
            _noButton.onClick.AddListener(Cancel);
            _buttonsBound = true;
        }

        private void ClearActions()
        {
            _yesAction = null;
            _noAction = null;
        }
    }
}
