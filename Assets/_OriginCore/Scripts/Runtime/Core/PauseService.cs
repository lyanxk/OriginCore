using System;
using System.Collections.Generic;
using OriginCore.Cameras;
using OriginCore.Input;
using OriginCore.RTS.Commands;
using OriginCore.SceneFlow;
using UnityEngine;

namespace OriginCore.Core
{
    public interface IUiEscapeHandler
    {
        bool TryHandleEscape();
    }

    public readonly struct PauseStateChange
    {
        public PauseStateChange(bool isPaused, float previousTimeScale)
        {
            IsPaused = isPaused;
            PreviousTimeScale = previousTimeScale;
        }

        public bool IsPaused { get; }
        public float PreviousTimeScale { get; }
    }

    [DefaultExecutionOrder(-11770)]
    [DisallowMultipleComponent]
    public sealed class PauseService : MonoBehaviour
    {
        [SerializeField] private bool _applyTimeScale = true;

        private readonly List<IUiEscapeHandler> _escapeHandlers =
            new List<IUiEscapeHandler>();
        private InputRouter _inputRouter;
        private InputRebindService _inputRebindService;
        private CursorPolicy _cursorPolicy;
        private GameServices _services;
        private float _timeScaleBeforePause = 1f;
        private bool _initialized;

        public event Action<PauseStateChange> PauseStateChanged;

        public bool IsPaused { get; private set; }
        public bool IsInitialized => _initialized;
        public float TimeScaleBeforePause => _timeScaleBeforePause;

        public void Configure(bool applyTimeScale)
        {
            _applyTimeScale = applyTimeScale;
        }

        public bool Initialize(
            InputRouter inputRouter,
            InputRebindService inputRebindService,
            CursorPolicy cursorPolicy,
            GameServices services)
        {
            if (_initialized)
            {
                return true;
            }

            if (inputRouter == null || inputRebindService == null ||
                cursorPolicy == null || services == null)
            {
                Debug.LogError("[OriginCore Pause] Persistent service references are incomplete.", this);
                return false;
            }

            _inputRouter = inputRouter;
            _inputRebindService = inputRebindService;
            _cursorPolicy = cursorPolicy;
            _services = services;
            IsPaused = false;
            _inputRouter.SetGameplayEnabled(true);
            _cursorPolicy.SetPauseOverride(false);
            _inputRouter.PauseRequested += HandlePauseRequested;
            _initialized = true;
            return true;
        }

        public void Shutdown()
        {
            if (!_initialized)
            {
                return;
            }

            if (IsPaused)
            {
                SetPaused(false);
            }

            _inputRouter.PauseRequested -= HandlePauseRequested;
            _escapeHandlers.Clear();
            _inputRouter = null;
            _inputRebindService = null;
            _cursorPolicy = null;
            _services = null;
            _initialized = false;
        }

        public void RegisterEscapeHandler(IUiEscapeHandler handler)
        {
            if (handler == null)
            {
                return;
            }

            _escapeHandlers.Remove(handler);
            _escapeHandlers.Add(handler);
        }

        public void UnregisterEscapeHandler(IUiEscapeHandler handler)
        {
            if (handler != null)
            {
                _escapeHandlers.Remove(handler);
            }
        }

        public bool RequestToggle()
        {
            if (!_initialized)
            {
                return false;
            }

            return SetPaused(!IsPaused);
        }

        public bool SetPaused(bool paused)
        {
            if (!_initialized || IsPaused == paused)
            {
                return false;
            }

            if (paused)
            {
                _timeScaleBeforePause = _applyTimeScale
                    ? Mathf.Max(0.0001f, Time.timeScale)
                    : 1f;
                IsPaused = true;
                _inputRouter.SetGameplayEnabled(false);
                _cursorPolicy.SetPauseOverride(true);
                if (_applyTimeScale)
                {
                    Time.timeScale = 0f;
                }
            }
            else
            {
                if (_applyTimeScale)
                {
                    Time.timeScale = _timeScaleBeforePause;
                }

                IsPaused = false;
                _inputRouter.SetGameplayEnabled(true);
                _cursorPolicy.SetPauseOverride(false);
            }

            PauseStateChanged?.Invoke(new PauseStateChange(IsPaused, _timeScaleBeforePause));
            return true;
        }

        public bool ProcessEscapeRequest()
        {
            if (!_initialized)
            {
                return false;
            }

            if (_inputRebindService.IsRebinding)
            {
                _inputRebindService.CancelCurrentRebind();
                return true;
            }

            if (_inputRebindService.LastRebindCanceledFrame == Time.frameCount)
            {
                return true;
            }

            for (int i = _escapeHandlers.Count - 1; i >= 0; i--)
            {
                IUiEscapeHandler handler = _escapeHandlers[i];
                if (IsDestroyedUnityObject(handler))
                {
                    _escapeHandlers.RemoveAt(i);
                    continue;
                }

                if (handler.TryHandleEscape())
                {
                    return true;
                }
            }

            if (!IsPaused && TryCancelCommandTargeting())
            {
                return true;
            }

            return RequestToggle();
        }

        private void HandlePauseRequested()
        {
            ProcessEscapeRequest();
        }

        private bool TryCancelCommandTargeting()
        {
            SceneContext context = _services != null ? _services.CurrentSceneContext : null;
            RtsCommandIssuer issuer = context != null ? context.RtsCommandIssuer : null;
            return issuer != null &&
                   issuer.TargetingState != CommandTargetingState.None &&
                   issuer.CancelTargeting();
        }

        private static bool IsDestroyedUnityObject(IUiEscapeHandler handler)
        {
            if (handler == null)
            {
                return true;
            }

            return handler is UnityEngine.Object unityObject && unityObject == null;
        }
    }
}
