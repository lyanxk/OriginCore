using UnityEngine;
namespace Camera
{
    [RequireComponent(typeof(UnityEngine.Camera))]
    public class CameraRig : MonoBehaviour
    {
        public float smoothTimePosition = 0.18f;
        public float smoothTimeRotation = 0.12f;
        public float smoothTimeFov = 0.15f;

        Vector3 _posVel;
        float _fovVel;

        UnityEngine.Camera _cam;

        CameraState _target;
        bool _hasTarget;
        bool _useSmooth;
        bool _keepPositionSmooth;

        const float PositionEpsilonSqr = 0.0001f;
        const float RotationEpsilonDeg = 0.1f;
        const float FovEpsilon = 0.01f;

        void Awake()
        {
            _cam = GetComponent<UnityEngine.Camera>();
            _target = new CameraState { Position = transform.position, Rotation = transform.rotation, Fov = _cam.fieldOfView };
            _hasTarget = true;
        }

        public void SetTarget(CameraState target)
        {
            _target = target;
            _hasTarget = true;
        }

        // 平滑模式切换
        public void SetSmooth(float posTime, float rotTime, float fovTime)
        {
            smoothTimePosition = Mathf.Max(0f, posTime);
            smoothTimeRotation = Mathf.Max(0f, rotTime);
            smoothTimeFov = Mathf.Max(0f, fovTime);
            _useSmooth = smoothTimePosition > 0f || smoothTimeRotation > 0f || smoothTimeFov > 0f;
            if (_useSmooth)
            {
                _posVel = Vector3.zero;
                _fovVel = 0f;
            }
        }

        public void SetContinuousPositionSmooth(bool enabled)
        {
            _keepPositionSmooth = enabled;
            if (!enabled)
                _posVel = Vector3.zero;
        }

        void LateUpdate()
        {
            if (!_hasTarget) return;

            bool smoothPosition = _useSmooth || _keepPositionSmooth;
            if (!smoothPosition || smoothTimePosition <= 0f)
            {
                transform.position = _target.Position;
                _posVel = Vector3.zero;
            }
            else
            {
                transform.position = Vector3.SmoothDamp(transform.position, _target.Position, ref _posVel, smoothTimePosition);
            }

            if (_useSmooth && smoothTimeRotation > 0f)
            {
                float t = 1f - Mathf.Exp(-Time.deltaTime / smoothTimeRotation);
                transform.rotation = Quaternion.Slerp(transform.rotation, _target.Rotation, t);
            }
            else
            {
                transform.rotation = _target.Rotation;
            }

            if (_useSmooth && smoothTimeFov > 0f)
            {
                _cam.fieldOfView = Mathf.SmoothDamp(_cam.fieldOfView, _target.Fov, ref _fovVel, smoothTimeFov);
            }
            else
            {
                _cam.fieldOfView = _target.Fov;
                _fovVel = 0f;
            }

            if (_useSmooth && IsAtTarget())
            {
                _useSmooth = false;
                if (!_keepPositionSmooth)
                    _posVel = Vector3.zero;
                _fovVel = 0f;
            }
        }

        bool IsAtTarget()
        {
            if ((transform.position - _target.Position).sqrMagnitude > PositionEpsilonSqr) return false;
            if (smoothTimeRotation > 0f && Quaternion.Angle(transform.rotation, _target.Rotation) > RotationEpsilonDeg) return false;
            if (smoothTimeFov > 0f && Mathf.Abs(_cam.fieldOfView - _target.Fov) > FovEpsilon) return false;
            return true;
        }
    }
}
