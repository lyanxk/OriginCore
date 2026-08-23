using UnityEngine;

namespace OriginCore.ACT
{
    [CreateAssetMenu(
        fileName = "SO_ActMovementConfig",
        menuName = "OriginCore/ACT/Movement Config")]
    public sealed class MovementConfig : ScriptableObject
    {
        [Header("Planar Movement")]
        [Min(0.1f), SerializeField] private float _runSpeed = 6f;
        [Min(0.1f), SerializeField] private float _acceleration = 24f;
        [Range(0f, 1f), SerializeField] private float _airControl = 0.35f;
        [Min(1f), SerializeField] private float _rotationSpeed = 720f;

        [Header("Vertical Movement")]
        [SerializeField] private float _gravity = -24f;
        [Min(0.01f), SerializeField] private float _jumpHeight = 1.5f;
        [Min(0.01f), SerializeField] private float _airJumpHeight = 1.2f;
        [Min(0f), SerializeField] private float _jumpBufferTime = 0.15f;
        [Min(0f), SerializeField] private float _coyoteTime = 0.12f;
        [Min(0.01f), SerializeField] private float _groundProbeDistance = 0.12f;
        [SerializeField] private float _groundedVerticalSpeed = -2f;

        [Header("Crouch")]
        [Min(0.1f), SerializeField] private float _crouchHeight = 1.1f;

        [Header("Assisted Jump")]
        [Min(0.01f), SerializeField] private float _boostRadius = 1.5f;
        [Min(0.1f), SerializeField] private float _boostForce = 9f;
        [Min(0f), SerializeField] private float _boostCooldown = 0.25f;

        [Header("Third Person Look")]
        [Min(0.001f), SerializeField] private float _lookSensitivity = 0.12f;
        [Range(-89f, 0f), SerializeField] private float _minimumPitch = -35f;
        [Range(0f, 89f), SerializeField] private float _maximumPitch = 65f;

        public float RunSpeed => _runSpeed;
        public float Acceleration => _acceleration;
        public float AirControl => _airControl;
        public float RotationSpeed => _rotationSpeed;
        public float Gravity => _gravity;
        public float JumpHeight => _jumpHeight;
        public float AirJumpHeight => _airJumpHeight;
        public float JumpBufferTime => _jumpBufferTime;
        public float CoyoteTime => _coyoteTime;
        public float GroundProbeDistance => _groundProbeDistance;
        public float GroundedVerticalSpeed => _groundedVerticalSpeed;
        public float CrouchHeight => _crouchHeight;
        public float BoostRadius => _boostRadius;
        public float BoostForce => _boostForce;
        public float BoostCooldown => _boostCooldown;
        public float LookSensitivity => _lookSensitivity;
        public float MinimumPitch => _minimumPitch;
        public float MaximumPitch => _maximumPitch;

        public void Configure(
            float runSpeed,
            float acceleration,
            float airControl,
            float gravity,
            float jumpHeight,
            float airJumpHeight,
            float crouchHeight,
            float jumpBufferTime,
            float coyoteTime,
            float boostRadius,
            float boostForce,
            float boostCooldown,
            float rotationSpeed,
            float groundProbeDistance,
            float groundedVerticalSpeed,
            float lookSensitivity,
            float minimumPitch,
            float maximumPitch)
        {
            _runSpeed = runSpeed;
            _acceleration = acceleration;
            _airControl = airControl;
            _gravity = gravity;
            _jumpHeight = jumpHeight;
            _airJumpHeight = airJumpHeight;
            _crouchHeight = crouchHeight;
            _jumpBufferTime = jumpBufferTime;
            _coyoteTime = coyoteTime;
            _boostRadius = boostRadius;
            _boostForce = boostForce;
            _boostCooldown = boostCooldown;
            _rotationSpeed = rotationSpeed;
            _groundProbeDistance = groundProbeDistance;
            _groundedVerticalSpeed = groundedVerticalSpeed;
            _lookSensitivity = lookSensitivity;
            _minimumPitch = minimumPitch;
            _maximumPitch = maximumPitch;
            Normalize();
        }

        private void OnValidate()
        {
            Normalize();
        }

        private void Normalize()
        {
            _runSpeed = Mathf.Max(0.1f, _runSpeed);
            _acceleration = Mathf.Max(0.1f, _acceleration);
            _airControl = Mathf.Clamp01(_airControl);
            _rotationSpeed = Mathf.Max(1f, _rotationSpeed);
            _gravity = -Mathf.Max(0.01f, Mathf.Abs(_gravity));
            _jumpHeight = Mathf.Max(0.01f, _jumpHeight);
            _airJumpHeight = Mathf.Max(0.01f, _airJumpHeight);
            _jumpBufferTime = Mathf.Max(0f, _jumpBufferTime);
            _coyoteTime = Mathf.Max(0f, _coyoteTime);
            _groundProbeDistance = Mathf.Max(0.01f, _groundProbeDistance);
            _groundedVerticalSpeed = -Mathf.Max(0.01f, Mathf.Abs(_groundedVerticalSpeed));
            _crouchHeight = Mathf.Max(0.1f, _crouchHeight);
            _boostRadius = Mathf.Max(0.01f, _boostRadius);
            _boostForce = Mathf.Max(0.1f, _boostForce);
            _boostCooldown = Mathf.Max(0f, _boostCooldown);
            _lookSensitivity = Mathf.Max(0.001f, _lookSensitivity);
            _minimumPitch = Mathf.Clamp(_minimumPitch, -89f, 0f);
            _maximumPitch = Mathf.Clamp(_maximumPitch, 0f, 89f);
            if (_maximumPitch < _minimumPitch)
            {
                _maximumPitch = _minimumPitch;
            }
        }
    }
}
