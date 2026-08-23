using System;
using OriginCore.Cameras;
using OriginCore.Debugging;
using OriginCore.Gameplay;
using OriginCore.Input;
using OriginCore.SceneFlow;
using OriginCore.UI;
using UnityEngine;

namespace OriginCore.Core
{
    [DefaultExecutionOrder(-11760)]
    [DisallowMultipleComponent]
    public sealed class GameModeController : MonoBehaviour
    {
        [SerializeField] private InputRouter _inputRouter;
        [SerializeField] private PossessionService _possessionService;
        [SerializeField] private CursorPolicy _cursorPolicy;
        [SerializeField] private DebugOverlayService _debugOverlay;
        [SerializeField] private GameMode _initialMode = GameMode.RTS;

        private SceneContext _boundScene;
        private CameraModeCoordinator _cameraCoordinator;
        private ModeHudPresenter _modeHud;
        private GameMode _currentMode = GameMode.RTS;
        private bool _initialized;

        public event Action<GameModeChange> ModeChanged;
        public event Action<GameMode, string> ModeChangeRejected;

        public InputRouter InputRouter => _inputRouter;
        public PossessionService PossessionService => _possessionService;
        public CursorPolicy CursorPolicy => _cursorPolicy;
        public GameMode InitialMode => _initialMode;
        public GameMode CurrentMode => _currentMode;
        public bool IsInitialized => _initialized;
        public SceneContext BoundScene => _boundScene;
        public CameraModeCoordinator CameraCoordinator => _cameraCoordinator;
        public ModeHudPresenter ModeHud => _modeHud;

        public void Configure(
            InputRouter inputRouter,
            PossessionService possessionService,
            CursorPolicy cursorPolicy,
            DebugOverlayService debugOverlay,
            GameMode initialMode = GameMode.RTS)
        {
            _inputRouter = inputRouter;
            _possessionService = possessionService;
            _cursorPolicy = cursorPolicy;
            _debugOverlay = debugOverlay;
            _initialMode = initialMode;
            if (!_initialized)
            {
                _currentMode = _initialMode;
            }
        }

        public bool Initialize()
        {
            if (_initialized)
            {
                return true;
            }

            if (_inputRouter == null || _possessionService == null ||
                _cursorPolicy == null || _debugOverlay == null)
            {
                Debug.LogError("[OriginCore Modes] Persistent mode service references are incomplete.", this);
                return false;
            }

            _currentMode = _initialMode;
            _inputRouter.GameplayMapRequested += HandleGameplayMapRequested;
            _inputRouter.SnapshotReady += HandleSnapshotReady;
            _possessionService.PawnChanged += HandlePawnChanged;
            _initialized = true;

            GameplayInputMap initialMap = _initialMode.ToGameplayInputMap();
            if (_inputRouter.ActiveGameplayMap != initialMap)
            {
                _inputRouter.RequestGameplayMap(initialMap, Time.frameCount);
            }

            ApplyPresentation();
            return true;
        }

        public void Shutdown()
        {
            if (!_initialized)
            {
                return;
            }

            _inputRouter.GameplayMapRequested -= HandleGameplayMapRequested;
            _inputRouter.SnapshotReady -= HandleSnapshotReady;
            _possessionService.PawnChanged -= HandlePawnChanged;
            if (_boundScene != null)
            {
                _possessionService.UnbindScene(_boundScene);
            }

            _cameraCoordinator?.BindPawn(null);
            _boundScene = null;
            _cameraCoordinator = null;
            _modeHud = null;
            _initialized = false;
        }

        public void BindScene(SceneContext context)
        {
            if (_boundScene != null && _boundScene != context)
            {
                _possessionService.UnbindScene(_boundScene);
            }

            _boundScene = context;
            _cameraCoordinator = context != null ? context.CameraModeCoordinator : null;
            _modeHud = context != null ? context.ModeHudPresenter : null;
            _possessionService.BindScene(context);
            _cameraCoordinator?.BindPawn(_possessionService.CurrentPawn);

            if (_currentMode.IsDirectControl() && !_possessionService.HasPawn)
            {
                RequestMode(GameMode.RTS, Time.frameCount);
            }
            else
            {
                ApplyPresentation();
            }
        }

        public void UnbindScene(SceneContext context)
        {
            if (_boundScene != context)
            {
                return;
            }

            _cameraCoordinator?.BindPawn(null);
            _possessionService.UnbindScene(context);
            _boundScene = null;
            _cameraCoordinator = null;
            _modeHud = null;
        }

        public bool RequestMode(GameMode requestedMode)
        {
            return RequestMode(requestedMode, Time.frameCount);
        }

        public bool RequestMode(GameMode requestedMode, int frame)
        {
            if (!_initialized)
            {
                return Reject(requestedMode, "GameModeController is not initialized.");
            }

            if (!Enum.IsDefined(typeof(GameMode), requestedMode))
            {
                return Reject(requestedMode, "Unknown GameMode value.");
            }

            if (_currentMode == requestedMode)
            {
                ApplyPresentation();
                return false;
            }

            if (requestedMode.IsDirectControl() && !_possessionService.HasPawn)
            {
                return Reject(
                    requestedMode,
                    "No possessable pawn is registered; ACT/FPS mode was not entered.",
                    false);
            }

            GameMode previousMode = _currentMode;
            GameplayInputMap previousMap = previousMode.ToGameplayInputMap();
            GameplayInputMap requestedMap = requestedMode.ToGameplayInputMap();
            bool inputMapChanged = false;
            if (_inputRouter.ActiveGameplayMap != requestedMap)
            {
                if (!_inputRouter.RequestGameplayMap(requestedMap, frame))
                {
                    return Reject(requestedMode, "The requested Gameplay input map could not be enabled.");
                }

                inputMapChanged = true;
            }

            bool possessionChanged = true;
            string possessionError = string.Empty;
            if (previousMode == GameMode.RTS && requestedMode.IsDirectControl())
            {
                possessionChanged = _possessionService.PrepareDirectObservation(out possessionError);
            }
            else if (requestedMode == GameMode.RTS)
            {
                possessionChanged = _possessionService.TryReturnToRts(out possessionError);
            }

            if (!possessionChanged)
            {
                if (inputMapChanged && _inputRouter.ActiveGameplayMap != previousMap)
                {
                    _inputRouter.RequestGameplayMap(previousMap, frame);
                }

                return Reject(requestedMode, possessionError);
            }

            _currentMode = requestedMode;
            ApplyPresentation();
            ModeChanged?.Invoke(new GameModeChange(previousMode, _currentMode, frame));
            return true;
        }

        private void HandleGameplayMapRequested(GameplayInputMap requestedMap, int frame)
        {
            RequestMode(requestedMap.ToGameMode(), frame);
        }

        private void HandleSnapshotReady(InputSnapshot snapshot)
        {
            if (!_currentMode.IsDirectControl() || snapshot.ActiveMap.ToGameMode() != _currentMode)
            {
                return;
            }

            _possessionService.TryHandleDirectInput(snapshot);
        }

        private void HandlePawnChanged(HybridControlDriver previous, HybridControlDriver current)
        {
            _cameraCoordinator?.BindPawn(current);
        }

        private void ApplyPresentation()
        {
            _cursorPolicy?.ApplyMode(_currentMode);
            if (_cameraCoordinator != null && !_cameraCoordinator.ApplyMode(_currentMode))
            {
                Debug.LogError("[OriginCore Modes] Scene camera coordinator is incomplete.", _cameraCoordinator);
            }

            _modeHud?.ApplyMode(_currentMode);
            if (_debugOverlay != null)
            {
                DebugOverlaySnapshot previous = _debugOverlay.Snapshot;
                _debugOverlay.SetSnapshot(new DebugOverlaySnapshot(
                    _currentMode.ToString(),
                    previous.SelectedCount,
                    previous.QueuedCommandCount,
                    previous.ResourceValue));
            }
        }

        private bool Reject(
            GameMode requestedMode,
            string message,
            bool logAsError = true)
        {
            string normalized = string.IsNullOrWhiteSpace(message)
                ? "Mode request was rejected."
                : message.Trim();
            _modeHud?.ShowError(normalized);
            ModeChangeRejected?.Invoke(requestedMode, normalized);
            if (logAsError)
            {
                Debug.LogError("[OriginCore Modes] " + normalized, this);
            }
            else
            {
                Debug.Log("[OriginCore Modes] " + normalized, this);
            }

            return false;
        }
    }
}
