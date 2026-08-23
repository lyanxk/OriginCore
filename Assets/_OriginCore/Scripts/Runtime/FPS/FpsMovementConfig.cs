using UnityEngine;

namespace OriginCore.FPS
{
    [CreateAssetMenu(
        fileName = "SO_FpsMovementConfig",
        menuName = "OriginCore/Movement/FPS Movement Config")]
    public sealed class FpsMovementConfig : ScriptableObject
    {
        [Min(0f), SerializeField] private float _walkSpeed = 4f;
        [Min(0f), SerializeField] private float _sprintSpeed = 7f;
        [Min(0f), SerializeField] private float _sprintLatchMinSpeed = 5f;
        [Min(0f), SerializeField] private float _acceleration = 28f;
        [Min(0f), SerializeField] private float _slideMinSpeed = 5f;
        [Min(0f), SerializeField] private float _slideImpulse = 3f;
        [Min(0f), SerializeField] private float _slideFriction = 8f;
        [Min(0.01f), SerializeField] private float _slideDuration = 0.9f;
        [Range(1f, 179f), SerializeField] private float _baseFov = 75f;
        [Range(1f, 179f), SerializeField] private float _adsFov = 55f;
        [Min(0f), SerializeField] private float _fovLerp = 90f;

        public float WalkSpeed => _walkSpeed;
        public float SprintSpeed => _sprintSpeed;
        public float SprintLatchMinSpeed => _sprintLatchMinSpeed;
        public float Acceleration => _acceleration;
        public float SlideMinSpeed => _slideMinSpeed;
        public float SlideImpulse => _slideImpulse;
        public float SlideFriction => _slideFriction;
        public float SlideDuration => _slideDuration;
        public float BaseFov => _baseFov;
        public float AdsFov => _adsFov;
        public float FovLerp => _fovLerp;

        private void OnValidate()
        {
            Normalize();
        }

        public void Configure(
            float walkSpeed,
            float sprintSpeed,
            float sprintLatchMinSpeed,
            float acceleration,
            float slideMinSpeed,
            float slideImpulse,
            float slideFriction,
            float slideDuration,
            float baseFov,
            float adsFov,
            float fovLerp)
        {
            _walkSpeed = walkSpeed;
            _sprintSpeed = sprintSpeed;
            _sprintLatchMinSpeed = sprintLatchMinSpeed;
            _acceleration = acceleration;
            _slideMinSpeed = slideMinSpeed;
            _slideImpulse = slideImpulse;
            _slideFriction = slideFriction;
            _slideDuration = slideDuration;
            _baseFov = baseFov;
            _adsFov = adsFov;
            _fovLerp = fovLerp;
            Normalize();
        }

        private void Normalize()
        {
            _walkSpeed = Mathf.Max(0f, _walkSpeed);
            _sprintSpeed = Mathf.Max(_walkSpeed, _sprintSpeed);
            _sprintLatchMinSpeed = Mathf.Clamp(
                _sprintLatchMinSpeed,
                _walkSpeed,
                _sprintSpeed);
            _acceleration = Mathf.Max(0f, _acceleration);
            _slideMinSpeed = Mathf.Max(0f, _slideMinSpeed);
            _slideImpulse = Mathf.Max(0f, _slideImpulse);
            _slideFriction = Mathf.Max(0f, _slideFriction);
            _slideDuration = Mathf.Max(0.01f, _slideDuration);
            _baseFov = Mathf.Clamp(_baseFov, 1f, 179f);
            _adsFov = Mathf.Clamp(_adsFov, 1f, _baseFov);
            _fovLerp = Mathf.Max(0f, _fovLerp);
        }
    }
}
