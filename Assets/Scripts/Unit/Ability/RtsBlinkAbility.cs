using System;
using Content;
using UnityEngine;

namespace Unit.Ability
{
    [Serializable]
    public class RtsBlinkAbility : RtsUnitAbility
    {
        const string AbilityIdValue = "ability.rts.blink";
        const string IconResourcePath = "AbilityIcons/Rts/rts_blink";

        [Header("Ability")]
        [Min(0f)]
        [SerializeField] float cooldown = 2f;

        [Header("Blink")]
        [SerializeField] float maxDistance = 5f;

        [NonSerialized] Sprite _icon;
        float _nextReadyTime;

        public override string AbilityId => AbilityIdValue;
        public override string DisplayName => GameText.GetName(AbilityId, AbilityId);
        public override Sprite Icon => LoadIcon(ref _icon, IconResourcePath);
        public override string Tooltip => GameText.GetTooltip(AbilityId);
        public override bool IsEnabled => Time.time >= _nextReadyTime;
        public override RtsAbilityTargetingMode TargetingMode => RtsAbilityTargetingMode.Point;

        public override float Cooldown01
        {
            get
            {
                if (cooldown <= 0f || IsEnabled)
                    return 0f;

                return Mathf.Clamp01((_nextReadyTime - Time.time) / cooldown);
            }
        }

        protected override bool TryActivateOnPoint(Vector3 worldPoint)
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
