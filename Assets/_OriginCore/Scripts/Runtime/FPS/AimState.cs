using Cinemachine;
using UnityEngine;

namespace OriginCore.FPS
{
    public sealed class AimState
    {
        private CinemachineVirtualCamera _camera;
        private FpsMovementConfig _config;
        private float _restoreFov;
        private float _adsFovOverride;

        public bool IsActive { get; private set; }
        public bool IsAiming { get; private set; }
        public float RestoreFov => _restoreFov;
        public float CurrentFov => _camera != null
            ? _camera.m_Lens.FieldOfView
            : 0f;

        public bool Begin(
            CinemachineVirtualCamera camera,
            FpsMovementConfig config)
        {
            if (camera == null || config == null)
            {
                return false;
            }

            if (IsActive && _camera == camera && _config == config)
            {
                return true;
            }

            End();
            _camera = camera;
            _config = config;
            _restoreFov = camera.m_Lens.FieldOfView;
            IsActive = true;
            IsAiming = false;
            SetFov(config.BaseFov);
            return true;
        }

        public bool Step(bool aimHeld, float deltaTime)
        {
            if (!IsActive || _camera == null || _config == null)
            {
                IsAiming = false;
                return false;
            }

            IsAiming = aimHeld;
            float targetFov = aimHeld
                ? _adsFovOverride > 0f ? _adsFovOverride : _config.AdsFov
                : _config.BaseFov;
            float currentFov = _camera.m_Lens.FieldOfView;
            SetFov(Mathf.MoveTowards(
                currentFov,
                targetFov,
                _config.FovLerp * Mathf.Max(0f, deltaTime)));
            return true;
        }

        public void End()
        {
            if (IsActive && _camera != null)
            {
                SetFov(_restoreFov);
            }

            _camera = null;
            _config = null;
            _restoreFov = 0f;
            IsActive = false;
            IsAiming = false;
        }

        public void SetAdsFieldOfView(float fieldOfView)
        {
            _adsFovOverride = fieldOfView > 0f
                ? Mathf.Clamp(fieldOfView, 1f, 179f)
                : 0f;
        }

        private void SetFov(float fieldOfView)
        {
            if (_camera == null)
            {
                return;
            }

            LensSettings lens = _camera.m_Lens;
            lens.FieldOfView = Mathf.Clamp(fieldOfView, 1f, 179f);
            _camera.m_Lens = lens;
        }
    }
}
