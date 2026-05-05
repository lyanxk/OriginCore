using System;
using UnityEngine;

namespace Unit.Ability
{
    [Serializable]
    public class RtsVoidImplosionAbility : RtsAreaDamageAbilityBase
    {
        [Header("Ability")]
        [SerializeField] string abilityId = "ability.rts.voidImplosion";
        [SerializeField] string displayName = "Void Implosion";
        [SerializeField] Sprite icon;
        [TextArea]
        [SerializeField] string tooltip = "Activate, then left-click a point to implode nearby enemies.";
        [Min(0f)]
        [SerializeField] float cooldown;

        [Header("Damage")]
        [Min(0f)]
        [SerializeField] float damage = 50f;
        [Min(0.1f)]
        [SerializeField] float radius = 1f;

        [Header("VFX")]
        [SerializeField] GameObject effectPrefab;
        [Min(0f)]
        [SerializeField] float effectGroundOffset = 0.05f;

        float _nextReadyTime;

        public override string AbilityId => abilityId;
        public override string DisplayName => string.IsNullOrWhiteSpace(displayName) ? "Void Implosion" : displayName;
        public override Sprite Icon => icon;
        public override string Tooltip => tooltip;
        public override bool IsEnabled => Time.time >= _nextReadyTime;
        public override float TargetingPreviewRadius => radius;
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
            if (!IsEnabled)
                return false;

            DamageHostilesInRadius(worldPoint, radius, damage);
            SpawnEffect(worldPoint);
            if (cooldown > 0f)
                _nextReadyTime = Time.time + cooldown;

            return true;
        }

        void SpawnEffect(Vector3 worldPoint)
        {
            if (effectPrefab == null)
                return;

            Vector3 spawnPoint = worldPoint + Vector3.up * effectGroundOffset;
            GameObject effect = UnityEngine.Object.Instantiate(effectPrefab, spawnPoint, Quaternion.identity);
            ParticleSystem[] particles = effect.GetComponentsInChildren<ParticleSystem>(true);
            float lifetime = 0f;

            for (int i = 0; i < particles.Length; i++)
            {
                ParticleSystem.MainModule main = particles[i].main;
                float particleLifetime = main.duration;
                if (main.startLifetime.mode == ParticleSystemCurveMode.TwoConstants)
                    particleLifetime += main.startLifetime.constantMax;
                else
                    particleLifetime += main.startLifetime.constant;

                lifetime = Mathf.Max(lifetime, particleLifetime);
            }

            UnityEngine.Object.Destroy(effect, Mathf.Max(1f, lifetime + 0.25f));
        }
    }
}
