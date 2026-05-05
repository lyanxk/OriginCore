using System;
using Input;
using UnityEngine;

namespace Unit.Ability
{
    [Serializable]
    public class DashAbility : ActFpsUnitAbility
    {
        [Header("Ability")]
        [SerializeField] string abilityId = "ability.act.dash";
        [SerializeField] string displayName = "Dash";
        [SerializeField] Sprite icon;
        [SerializeField] string hotkeyText = "Mouse5";
        [TextArea]
        [SerializeField] string tooltip = "Dash forward quickly.";
        [Min(0f)]
        [SerializeField] float cooldown;

        [Header("Movement")]
        [SerializeField] float dashSpeed = 10f;
        [SerializeField] float dashDuration = 0.2f;

        float _nextReadyTime;
        Vector2 _activationMove;
        bool _hasActivationMove;

        public override string AbilityId => abilityId;
        public override string DisplayName => string.IsNullOrWhiteSpace(displayName) ? "Dash" : displayName;
        public override Sprite Icon => icon;
        public override string HotkeyText => hotkeyText;
        public override string Tooltip => tooltip;
        public override bool IsEnabled => Time.time >= _nextReadyTime;

        public override float Cooldown01
        {
            get
            {
                if (cooldown <= 0f || IsEnabled)
                    return 0f;

                return Mathf.Clamp01((_nextReadyTime - Time.time) / cooldown);
            }
        }

        public override void ProcessInput(InputIntent intent)
        {
            if (!intent.Dash)
                return;

            _activationMove = intent.Move;
            _hasActivationMove = true;
            TryActivate();
            _hasActivationMove = false;
        }

        public override bool TryActivate()
        {
            if (Motor == null || !IsAvailableInCurrentMode || !IsEnabled)
                return false;

            Vector3 dashDirection = ResolveDashDirection();
            if (dashDirection.sqrMagnitude < 1e-6f)
                return false;

            Motor.OverridePlanarVelocity(dashDirection * dashSpeed, dashDuration);

            if (cooldown > 0f)
                _nextReadyTime = Time.time + cooldown;

            return true;
        }

        Vector3 ResolveDashDirection()
        {
            Vector3 forward = ResolvePlanarCameraForward();
            Vector2 move = _hasActivationMove ? _activationMove : Vector2.zero;

            if (move.sqrMagnitude > 0.0001f)
            {
                Vector3 right = Vector3.Cross(Vector3.up, forward);
                Vector3 moveDirection = right * move.x + forward * move.y;
                moveDirection.y = 0f;

                if (moveDirection.sqrMagnitude > 1e-6f)
                    return moveDirection.normalized;
            }

            return forward;
        }

        Vector3 ResolvePlanarCameraForward()
        {
            Vector3 forward = Vector3.zero;
            if (Router != null && Router.TryGetActFpsAimDirection(out Vector3 aimDirection))
                forward = aimDirection;
            else if (CachedTransform != null)
                forward = CachedTransform.forward;
            else
                forward = Vector3.forward;

            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-6f)
                return Vector3.forward;

            return forward.normalized;
        }
    }
}
