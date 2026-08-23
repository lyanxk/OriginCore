using System;
using OriginCore.Core;
using UnityEngine;

namespace OriginCore.Cameras
{
    public readonly struct CursorPolicyState
    {
        public CursorPolicyState(
            GameMode mode,
            bool pauseOverride,
            bool visible,
            CursorLockMode lockState)
        {
            Mode = mode;
            PauseOverride = pauseOverride;
            Visible = visible;
            LockState = lockState;
        }

        public GameMode Mode { get; }
        public bool PauseOverride { get; }
        public bool Visible { get; }
        public CursorLockMode LockState { get; }
    }

    [DefaultExecutionOrder(-11740)]
    [DisallowMultipleComponent]
    public sealed class CursorPolicy : MonoBehaviour
    {
        [SerializeField] private bool _applyToSystem = true;
        [SerializeField] private GameMode _mode = GameMode.RTS;

        private bool _pauseOverride;
        private CursorPolicyState _state;

        public event Action<CursorPolicyState> StateChanged;

        public GameMode Mode => _mode;
        public bool PauseOverride => _pauseOverride;
        public bool ApplyToSystem => _applyToSystem;
        public CursorPolicyState State => _state;

        private void OnEnable()
        {
            Refresh(true);
        }

        private void OnDisable()
        {
            if (_applyToSystem && Application.isPlaying)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        public void Configure(bool applyToSystem)
        {
            _applyToSystem = applyToSystem;
            Refresh(true);
        }

        public bool ApplyMode(GameMode mode)
        {
            if (_mode == mode)
            {
                Refresh(false);
                return false;
            }

            _mode = mode;
            Refresh(true);
            return true;
        }

        public bool SetPauseOverride(bool pauseOverride)
        {
            if (_pauseOverride == pauseOverride)
            {
                Refresh(false);
                return false;
            }

            _pauseOverride = pauseOverride;
            Refresh(true);
            return true;
        }

        public static CursorPolicyState Resolve(GameMode mode, bool pauseOverride)
        {
            bool visible = pauseOverride || mode == GameMode.RTS;
            return new CursorPolicyState(
                mode,
                pauseOverride,
                visible,
                visible ? CursorLockMode.None : CursorLockMode.Locked);
        }

        private void Refresh(bool forceEvent)
        {
            CursorPolicyState next = Resolve(_mode, _pauseOverride);
            bool changed = next.Mode != _state.Mode ||
                           next.PauseOverride != _state.PauseOverride ||
                           next.Visible != _state.Visible ||
                           next.LockState != _state.LockState;
            _state = next;

            if (_applyToSystem && Application.isPlaying)
            {
                Cursor.lockState = _state.LockState;
                Cursor.visible = _state.Visible;
            }

            if (forceEvent || changed)
            {
                StateChanged?.Invoke(_state);
            }
        }
    }
}
