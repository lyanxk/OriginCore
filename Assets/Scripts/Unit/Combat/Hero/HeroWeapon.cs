using System;
using UnityEngine;

namespace Unit.Combat.Hero
{
    public enum HeroWeaponRangeType
    {
        Melee = 0,
        Ranged = 1
    }

    [Serializable]
    public abstract class HeroWeapon
    {
        [Min(0f)]
        [SerializeField] float baseDamage = 10f;

        public abstract string DisplayName { get; }
        public abstract HeroWeaponRangeType RangeType { get; }
        public float BaseDamage => baseDamage;
        public virtual GameObject ViewPrefab => null;

        public virtual bool SupportsAbility(string abilityId)
        {
            return false;
        }

        protected void SetDefaultBaseDamage(float value)
        {
            baseDamage = Mathf.Max(0f, value);
        }
    }
}
