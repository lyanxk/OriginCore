using System;
using Cinemachine;
using OriginCore.ACT;
using OriginCore.Core;
using OriginCore.FPS;
using OriginCore.Gameplay;
using UnityEngine;

namespace OriginCore.Cameras
{
    [DisallowMultipleComponent]
    public sealed class CameraModeCoordinator : MonoBehaviour
    {
        [SerializeField] private Camera _outputCamera;
        [SerializeField] private CinemachineBrain _brain;
        [SerializeField] private CinemachineVirtualCamera _rtsCamera;
        [SerializeField] private CinemachineVirtualCamera _actCamera;
        [SerializeField] private CinemachineVirtualCamera _fpsCamera;
        [SerializeField] private int _activePriority = 20;
        [SerializeField] private int _inactivePriority;
        [Min(0f), SerializeField] private float _blendDuration = 0.35f;
        [SerializeField] private GameMode _activeMode = GameMode.RTS;

        public event Action<GameMode, GameMode> ActiveModeChanged;

        public Camera OutputCamera => _outputCamera;
        public CinemachineBrain Brain => _brain;
        public CinemachineVirtualCamera RtsCamera => _rtsCamera;
        public CinemachineVirtualCamera ActCamera => _actCamera;
        public CinemachineVirtualCamera FpsCamera => _fpsCamera;
        public int ActivePriority => _activePriority;
        public int InactivePriority => _inactivePriority;
        public float BlendDuration => _blendDuration;
        public GameMode ActiveMode => _activeMode;
        public CinemachineVirtualCamera ActiveVirtualCamera => GetCamera(_activeMode);
        public bool IsConfigured => _outputCamera != null && _brain != null &&
                                    _rtsCamera != null && _actCamera != null && _fpsCamera != null;

        public int ActivePriorityCameraCount
        {
            get
            {
                int count = 0;
                count += _rtsCamera != null && _rtsCamera.m_Priority == _activePriority ? 1 : 0;
                count += _actCamera != null && _actCamera.m_Priority == _activePriority ? 1 : 0;
                count += _fpsCamera != null && _fpsCamera.m_Priority == _activePriority ? 1 : 0;
                return count;
            }
        }

        private void OnEnable()
        {
            NormalizeSettings();
            ConfigureBrainBlend();
            ApplyPriorities();
        }

        private void OnValidate()
        {
            NormalizeSettings();
            ConfigureBrainBlend();
        }

        public void Configure(
            Camera outputCamera,
            CinemachineBrain brain,
            CinemachineVirtualCamera rtsCamera,
            CinemachineVirtualCamera actCamera,
            CinemachineVirtualCamera fpsCamera,
            int activePriority = 20,
            int inactivePriority = 0,
            float blendDuration = 0.35f)
        {
            _outputCamera = outputCamera;
            _brain = brain;
            _rtsCamera = rtsCamera;
            _actCamera = actCamera;
            _fpsCamera = fpsCamera;
            _inactivePriority = inactivePriority;
            _activePriority = activePriority;
            _blendDuration = blendDuration;
            NormalizeSettings();
            ConfigureBrainBlend();
            ApplyPriorities();
        }

        public bool ApplyMode(GameMode mode)
        {
            if (!IsConfigured)
            {
                return false;
            }

            GameMode previous = _activeMode;
            _activeMode = mode;
            ApplyPriorities();
            if (previous != _activeMode)
            {
                ActiveModeChanged?.Invoke(previous, _activeMode);
            }

            return true;
        }

        public bool BindPawn(HybridControlDriver pawn)
        {
            if (_actCamera == null || _fpsCamera == null)
            {
                return false;
            }

            Transform actTarget = null;
            Transform fpsTarget = null;
            if (pawn != null)
            {
                ActMovementController actMovement = pawn.GetComponent<ActMovementController>();
                FpsMovementController fpsMovement = pawn.GetComponent<FpsMovementController>();
                actTarget = actMovement != null ? actMovement.ActCameraTarget : null;
                fpsTarget = fpsMovement != null ? fpsMovement.FpsCameraTarget : null;
            }

            _actCamera.Follow = actTarget;
            _actCamera.LookAt = actTarget;
            _fpsCamera.Follow = fpsTarget;
            _fpsCamera.LookAt = null;
            _actCamera.PreviousStateIsValid = false;
            _fpsCamera.PreviousStateIsValid = false;
            return pawn == null || actTarget != null && fpsTarget != null;
        }

        public CinemachineVirtualCamera GetCamera(GameMode mode)
        {
            switch (mode)
            {
                case GameMode.RTS:
                    return _rtsCamera;
                case GameMode.ACT:
                    return _actCamera;
                case GameMode.FPS:
                    return _fpsCamera;
                default:
                    return null;
            }
        }

        private void NormalizeSettings()
        {
            _activePriority = Mathf.Max(_inactivePriority + 1, _activePriority);
            _blendDuration = Mathf.Max(0f, _blendDuration);
        }

        private void ConfigureBrainBlend()
        {
            if (_brain != null)
            {
                _brain.m_DefaultBlend = new CinemachineBlendDefinition(
                    CinemachineBlendDefinition.Style.EaseInOut,
                    _blendDuration);
            }
        }

        private void ApplyPriorities()
        {
            SetPriority(_rtsCamera, _activeMode == GameMode.RTS);
            SetPriority(_actCamera, _activeMode == GameMode.ACT);
            SetPriority(_fpsCamera, _activeMode == GameMode.FPS);
        }

        private void SetPriority(CinemachineVirtualCamera virtualCamera, bool active)
        {
            if (virtualCamera != null)
            {
                virtualCamera.m_Priority = active ? _activePriority : _inactivePriority;
            }
        }
    }
}
