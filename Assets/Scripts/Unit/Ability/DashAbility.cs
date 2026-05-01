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
            if (intent.Dash)
                TryActivate();
        }

        public override bool TryActivate()
        {
            if (Motor == null || !IsAvailableInCurrentMode || !IsEnabled)
                return false;

            Vector3 dashDirection = CachedTransform != null ? CachedTransform.forward : Vector3.forward;
            dashDirection.y = 0f;
            if (dashDirection.sqrMagnitude < 1e-6f)
                return false;

            dashDirection.Normalize();
            Motor.OverridePlanarVelocity(dashDirection * dashSpeed, dashDuration);

            if (cooldown > 0f)
                _nextReadyTime = Time.time + cooldown;

            return true;
        }
    }
}
