using System;
using UnityEngine;

namespace Unit.Ability
{
    [Serializable]
    public class FlightAbility : ActUnitAbility
    {
        [Header("Ability")]
        [SerializeField] string abilityId = "ability.act.flight";
        [SerializeField] string displayName = "Flight";
        [SerializeField] Sprite icon;
        [SerializeField] string hotkeyText = "Double Space";
        [TextArea]
        [SerializeField] string tooltip = "Double-tap Space to toggle flight. While flying, Space rises and Shift descends.";

        [Header("Flight")]
        [Min(0.05f)]
        [SerializeField] float doubleTapWindow = 0.3f;
        [Min(0.1f)]
        [SerializeField] float verticalSpeed = 6f;

        float _lastSpacePressedTime = float.NegativeInfinity;

        public override string AbilityId => abilityId;
        public override string DisplayName => string.IsNullOrWhiteSpace(displayName) ? "Flight" : displayName;
        public override Sprite Icon => icon;
        public override string HotkeyText => hotkeyText;
        public override string Tooltip => tooltip;

        public override void ProcessInput(InputIntent intent)
        {
            if (Motor == null)
                return;

            if (!IsAvailableInCurrentMode)
            {
                StopFlight();
                return;
            }

            if (intent.Space)
            {
                if (Time.time - _lastSpacePressedTime <= doubleTapWindow)
                {
                    Motor.SetFlightEnabled(!Motor.IsFlightEnabled);
                    _lastSpacePressedTime = float.NegativeInfinity;
                }
                else
                {
                    _lastSpacePressedTime = Time.time;
                }
            }

            if (!Motor.IsFlightEnabled)
                return;

            float verticalInput = 0f;
            if (intent.SpaceHeld)
                verticalInput += 1f;
            if (intent.Shift)
                verticalInput -= 1f;

            Motor.SetFlightVerticalInput(verticalInput, verticalSpeed);
        }

        public override void Tick(float deltaTime)
        {
            if (!IsAvailableInCurrentMode)
                StopFlight();
        }

        protected override void OnUnbound()
        {
            StopFlight();
        }

        void StopFlight()
        {
            if (Motor == null)
                return;

            Motor.SetFlightVerticalInput(0f, 0f);
            if (Motor.IsFlightEnabled)
                Motor.SetFlightEnabled(false);
        }
    }
}
