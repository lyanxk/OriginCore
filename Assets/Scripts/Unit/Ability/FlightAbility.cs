using System;
using Content;
using Input;
using UnityEngine;

namespace Unit.Ability
{
    [Serializable]
    public class FlightAbility : ActUnitAbility
    {
        const string AbilityIdValue = "ability.act.flight";

        [Header("Ability")]
        [SerializeField] Sprite icon;

        [Header("Flight")]
        [Min(0.05f)]
        [SerializeField] float doubleTapWindow = 0.3f;

        float _lastSpacePressedTime = float.NegativeInfinity;

        public override string AbilityId => AbilityIdValue;
        public override string DisplayName => GameText.GetName(AbilityId, AbilityId);
        public override Sprite Icon => icon;
        public override string HotkeyText => GameText.GetHotkey(AbilityId);
        public override string Tooltip => GameText.GetTooltip(AbilityId);

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
            if (intent.Ctrl)
                verticalInput -= 1f;

            Motor.SetFlightVerticalInput(verticalInput, intent.Shift);
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

            Motor.SetFlightVerticalInput(0f, false);
            if (Motor.IsFlightEnabled)
                Motor.SetFlightEnabled(false);
        }
    }
}
