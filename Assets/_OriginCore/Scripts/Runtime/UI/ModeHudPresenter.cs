using System;
using OriginCore.Core;
using TMPro;
using UnityEngine;

namespace OriginCore.UI
{
    [DisallowMultipleComponent]
    public sealed class ModeHudPresenter : MonoBehaviour
    {
        [SerializeField] private TMP_Text _modeLabel;
        [SerializeField] private GameObject _rtsRoot;
        [SerializeField] private GameObject _actRoot;
        [SerializeField] private GameObject _fpsRoot;
        [SerializeField] private GameMode _mode = GameMode.RTS;

        public event Action<GameMode> ModePresented;

        public TMP_Text ModeLabel => _modeLabel;
        public GameObject RtsRoot => _rtsRoot;
        public GameObject ActRoot => _actRoot;
        public GameObject FpsRoot => _fpsRoot;
        public GameMode Mode => _mode;
        public string LastError { get; private set; } = string.Empty;

        private void OnEnable()
        {
            ApplyMode(_mode);
        }

        public void Configure(
            TMP_Text modeLabel,
            GameObject rtsRoot,
            GameObject actRoot,
            GameObject fpsRoot)
        {
            _modeLabel = modeLabel;
            _rtsRoot = rtsRoot;
            _actRoot = actRoot;
            _fpsRoot = fpsRoot;
            ApplyMode(_mode);
        }

        public void ApplyMode(GameMode mode)
        {
            _mode = mode;
            LastError = string.Empty;
            SetActive(_rtsRoot, mode == GameMode.RTS);
            SetActive(_actRoot, mode == GameMode.ACT);
            SetActive(_fpsRoot, mode == GameMode.FPS);
            RefreshLabel();
            ModePresented?.Invoke(_mode);
        }

        public void ShowError(string message)
        {
            LastError = string.IsNullOrWhiteSpace(message) ? "Unknown mode error." : message.Trim();
            RefreshLabel();
        }

        private void RefreshLabel()
        {
            if (_modeLabel == null)
            {
                return;
            }

            _modeLabel.text = string.IsNullOrEmpty(LastError)
                ? "MODE: " + _mode
                : "MODE: " + _mode + "\nERROR: " + LastError;
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }
    }
}
