using System;
using Content;
using UnityEngine;

namespace Unit.Ability
{
    [Serializable]
    public class RtsCelestialSentenceAbility : RtsAreaDamageAbilityBase
    {
        const string AbilityIdValue = "ability.rts.celestialSentence";
        const string IconResourcePath = "AbilityIcons/Rts/rts_celestial_sentence";

        [Header("Ability")]
        [Min(0f)]
        [SerializeField] float cooldown;

        [Header("Damage")]
        [Min(0f)]
        [SerializeField] float damage = 50f;
        [Min(0.1f)]
        [SerializeField] float radius = 1f;

        [NonSerialized] Sprite _icon;
        float _nextReadyTime;

        public override string AbilityId => AbilityIdValue;
        public override string DisplayName => GameText.GetName(AbilityId, AbilityId);
        public override Sprite Icon => LoadIcon(ref _icon, IconResourcePath);
        public override string Tooltip => GameText.GetTooltip(AbilityId);
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
