using System;
using UnityEngine;

namespace Unit.Ability
{
    [Serializable]
    public class RtsBlinkAbility : RtsUnitAbility, IRtsGroundTargetAbility
    {
        [Header("Ability")]
        [SerializeField] string abilityId = "ability.rts.blink";
        [SerializeField] string displayName = "Blink";
        [SerializeField] Sprite icon;
        [TextArea]
        [SerializeField] string tooltip = "Activate, then left-click ground to blink.";
        [Min(0f)]
        [SerializeField] float cooldown = 2f;

        [Header("Blink")]
        [SerializeField] float maxDistance = 5f;

        float _nextReadyTime;

        public override string AbilityId => abilityId;
        public override string DisplayName => string.IsNullOrWhiteSpace(displayName) ? "Blink" : displayName;
        public override Sprite Icon => icon;
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

        public override bool TryActivate()
        {
            if (Motor == null || !IsAvailableInCurrentMode || !IsEnabled)
                return false;

            if (RtsAbilityTargetingState.IsPending(Router, this))
            {
                RtsAbilityTargetingState.Clear();
                return true;
            }

            RtsQueuedOrderState.Clear();
            RtsAbilityTargetingState.SetPending(Router, this, this);
            return true;
        }

        public bool TryActivateAtPoint(Vector3 worldPoint)
        {
            if (Motor == null || !IsEnabled)
                return false;

            Vector3 origin = CachedTransform != null ? CachedTransform.position : worldPoint;
            Vector3 planarOffset = worldPoint - origin;
            planarOffset.y = 0f;

            if (planarOffset.sqrMagnitude < 1e-6f)
                return false;

            float clampedDistance = Mathf.Min(planarOffset.magnitude, Mathf.Max(0.01f, maxDistance));
            Vector3 destination = origin + planarOffset.normalized * clampedDistance;
            destination.y = worldPoint.y;

            Motor.TeleportTo(destination);
            if (cooldown > 0f)
                _nextReadyTime = Time.time + cooldown;

            return true;
        }
    }
}
