using System;
using UnityEngine;

namespace Unit.Ability
{
    [Serializable]
    public class RtsCelestialSentenceAbility : RtsAreaDamageAbilityBase
    {
        [Header("Ability")]
        [SerializeField] string abilityId = "ability.rts.celestialSentence";
        [SerializeField] string displayName = "Celestial Sentence";
        [SerializeField] Sprite icon;
        [TextArea]
        [SerializeField] string tooltip = "Release immediately to damage nearby enemies.";
        [Min(0f)]
        [SerializeField] float cooldown;

        [Header("Damage")]
        [Min(0f)]
        [SerializeField] float damage = 50f;
        [Min(0.1f)]
        [SerializeField] float radius = 1f;

        float _nextReadyTime;

        public override string AbilityId => abilityId;
        public override string DisplayName => string.IsNullOrWhiteSpace(displayName) ? "Celestial Sentence" : displayName;
        public override Sprite Icon => icon;
        public override string Tooltip => tooltip;
        public override bool IsEnabled => Time.time >= _nextReadyTime;
        public override RtsAbilityTargetingMode TargetingMode => RtsAbilityTargetingMode.Self;

        public override float Cooldown01
        {
            get
            {
                if (cooldown <= 0f || IsEnabled)
                    return 0f;

                return Mathf.Clamp01((_nextReadyTime - Time.time) / cooldown);
            }
        }

        protected override bool TryActivateSelf()
        {
            if (CachedTransform == null || !IsEnabled)
                return false;

            DamageHostilesInRadius(CachedTransform.position, radius, damage);
            if (cooldown > 0f)
                _nextReadyTime = Time.time + cooldown;

            return true;
        }
    }
}
