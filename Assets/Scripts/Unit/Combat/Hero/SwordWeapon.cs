using System;
using UnityEngine;

namespace Unit.Combat.Hero
{
    [Serializable]
    public sealed class SwordWeapon : HeroWeapon
    {
        [SerializeField] GameObject weaponPrefab;
        [SerializeField] GameObject uppercutPrefab;
        [SerializeField, HideInInspector] bool initialized;

        public override string DisplayName => "Sword";
        public override HeroWeaponRangeType RangeType => HeroWeaponRangeType.Melee;
        public override GameObject ViewPrefab => weaponPrefab;
        public GameObject UppercutPrefab => uppercutPrefab;

        public override void Normalize()
        {
            base.Normalize();
            EnsureInitialized();
        }

        void EnsureInitialized()
        {
            if (!initialized)
                SetDefaultBaseDamage(40f);

            initialized = true;
        }

        protected override WeaponAbility[] CreateAbilities()
        {
            return new WeaponAbility[]
            {
                new SwordBackTeleportWeaponAbility(),
                new SwordDirectionalThrustWeaponAbility(),
                new UppercutWeaponAbility()
            };
        }

        public SwordWeapon()
        {
            EnsureInitialized();
        }
    }
}
