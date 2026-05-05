using System;
using UnityEngine;

namespace Unit.Combat.Hero
{
    [Serializable]
    public sealed class RevolverWeapon : HeroWeapon
    {
        [SerializeField] GameObject weaponPrefab;
        [SerializeField] GameObject projectilePrefab;
        [SerializeField] GameObject hitEffectPrefab;
        [SerializeField, HideInInspector] bool initialized;

        public override string DisplayName => "Revolver";
        public override HeroWeaponRangeType RangeType => HeroWeaponRangeType.Ranged;
        public override GameObject ViewPrefab => weaponPrefab;
        public GameObject ProjectilePrefab => projectilePrefab;
        public GameObject HitEffectPrefab => hitEffectPrefab;

        public override void Normalize()
        {
            base.Normalize();
            EnsureInitialized();
        }

        void EnsureInitialized()
        {
            if (!initialized)
                SetDefaultBaseDamage(20f);

            initialized = true;
        }

        protected override WeaponAbility[] CreateAbilities()
        {
            return new WeaponAbility[]
            {
                new ChargeShotWeaponAbility()
            };
        }

        public RevolverWeapon()
        {
            EnsureInitialized();
        }
    }
}
