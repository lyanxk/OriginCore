using System;
using System.Collections.Generic;
using OriginCore.Input;
using UnityEngine;

namespace OriginCore.Abilities
{
    [DisallowMultipleComponent]
    public sealed class FlightController : MonoBehaviour
    {
        [SerializeField] private bool _flightEnabled;
        [Min(0f), SerializeField] private float _horizontalSpeedMultiplier = 1f;
        [Min(0f), SerializeField] private float _verticalSpeed = 8f;
        [Min(0f), SerializeField] private float _verticalAcceleration = 24f;
        [Min(0f), SerializeField] private float _glideDescentSpeed = 2f;
        [Range(0.1f, 0.6f), SerializeField] private float _doubleTapWindow = 0.3f;

        private bool _flightActive;
        private bool _isGliding;
        private float _jumpTapRemaining;

        private readonly Dictionary<string, float> _runtimeHorizontalMultipliers =
            new Dictionary<string, float>(StringComparer.Ordinal);

        public event Action<bool> FlightStateChanged;

        public bool FlightAvailable => _flightEnabled;
        public bool FlightEnabled => _flightEnabled && _flightActive;
        public bool IsGliding => FlightEnabled && _isGliding;
        public float HorizontalSpeedMultiplier
        {
            get
            {
                float value = Mathf.Max(0f, _horizontalSpeedMultiplier);
                foreach (float multiplier in _runtimeHorizontalMultipliers.Values)
                {
                    value *= Mathf.Max(0f, multiplier);
                }
                return value;
            }
        }
        public float VerticalSpeed => Mathf.Max(0f, _verticalSpeed);
        public float GlideDescentSpeed => Mathf.Max(0f, _glideDescentSpeed);
        public float DoubleTapWindow => Mathf.Clamp(_doubleTapWindow, 0.1f, 0.6f);

        private void OnEnable()
        {
            _flightActive = _flightEnabled;
            _isGliding = false;
            _jumpTapRemaining = 0f;
        }

        private void OnDisable()
        {
            _isGliding = false;
            _jumpTapRemaining = 0f;
        }

        private void OnValidate()
        {
            _horizontalSpeedMultiplier = Mathf.Max(0f, _horizontalSpeedMultiplier);
            _verticalSpeed = Mathf.Max(0f, _verticalSpeed);
            _verticalAcceleration = Mathf.Max(0f, _verticalAcceleration);
            _glideDescentSpeed = Mathf.Max(0f, _glideDescentSpeed);
            _doubleTapWindow = Mathf.Clamp(_doubleTapWindow, 0.1f, 0.6f);
        }

        public bool SetFlightEnabled(bool enabled)
        {
            bool previous = FlightEnabled;
            bool availabilityChanged = _flightEnabled != enabled;
            _flightEnabled = enabled;
            _flightActive = enabled;
            _isGliding = false;
            _jumpTapRemaining = 0f;
            if (previous != FlightEnabled)
            {
                FlightStateChanged?.Invoke(FlightEnabled);
            }
            return availabilityChanged || previous != FlightEnabled;
        }

        public bool SetFlightActive(bool active)
        {
            bool next = _flightEnabled && active;
            if (_flightActive == next)
            {
                return false;
            }

            _flightActive = next;
            _isGliding = false;
            FlightStateChanged?.Invoke(FlightEnabled);
            return true;
        }

        public bool ProcessJumpInput(InputButtonState jump, float deltaTime)
        {
            _jumpTapRemaining = Mathf.Max(
                0f,
                _jumpTapRemaining - Mathf.Max(0f, deltaTime));
            if (!_flightEnabled || !jump.WasPressedThisFrame)
            {
                return false;
            }

            if (_jumpTapRemaining > 0f)
            {
                _jumpTapRemaining = 0f;
                SetFlightActive(!FlightEnabled);
                return true;
            }

            _jumpTapRemaining = DoubleTapWindow;
            return false;
        }

        public void ClearTransientInput()
        {
            _jumpTapRemaining = 0f;
            _isGliding = false;
        }

        public float ResolveHorizontalSpeed(float baseSpeed)
        {
            return Mathf.Max(0f, baseSpeed) * HorizontalSpeedMultiplier;
        }

        public bool SetRuntimeHorizontalMultiplier(string sourceId, float multiplier)
        {
            if (string.IsNullOrWhiteSpace(sourceId)) return false;
            _runtimeHorizontalMultipliers[sourceId.Trim()] = Mathf.Max(0f, multiplier);
            return true;
        }

        public bool RemoveRuntimeHorizontalMultiplier(string sourceId)
        {
            return !string.IsNullOrWhiteSpace(sourceId) &&
                   _runtimeHorizontalMultipliers.Remove(sourceId.Trim());
        }

        public float ResolveVerticalVelocity(
            float currentVelocity,
            bool ascend,
            bool descend,
            float deltaTime)
        {
            if (!FlightEnabled)
            {
                _isGliding = false;
                return currentVelocity;
            }

            _isGliding = !ascend && !descend;
            float target = ascend
                ? VerticalSpeed
                : descend
                    ? -VerticalSpeed
                    : -GlideDescentSpeed;
            return Mathf.MoveTowards(
                currentVelocity,
                target,
                Mathf.Max(0f, _verticalAcceleration) * Mathf.Max(0f, deltaTime));
        }
    }
}
