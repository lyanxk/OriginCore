using System;
using UnityEngine;

namespace Unit.Combat.Hero
{
    [Serializable]
    public sealed class RevolverWeapon : HeroWeapon
    {
        const string ChargedShotAbilityId = "ability.act.chargedShot";

        [SerializeField] GameObject weaponPrefab;
        [SerializeField] GameObject projectilePrefab;
        [SerializeField] GameObject hitEffectPrefab;
        [SerializeField, HideInInspector] bool initialized;

        public override string DisplayName => "Revolver";
        public override HeroWeaponRangeType RangeType => HeroWeaponRangeType.Ranged;
        public override GameObject ViewPrefab => weaponPrefab;
        public GameObject ProjectilePrefab => projectilePrefab;
        public GameObject HitEffectPrefab => hitEffectPrefab;

        public override bool SupportsAbility(string abilityId)
        {
            return string.Equals(abilityId, ChargedShotAbilityId, StringComparison.OrdinalIgnoreCase);
        }

        void EnsureInitialized()
        {
            if (initialized)
                return;

            SetDefaultBaseDamage(20f);
            initialized = true;
        }

        public RevolverWeapon()
        {
            EnsureInitialized();
        }
    }
}
