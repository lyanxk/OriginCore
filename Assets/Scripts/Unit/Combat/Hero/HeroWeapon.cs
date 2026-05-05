using System;
using System.Collections.Generic;
using Input;
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

        [NonSerialized] WeaponAbility[] _abilities;

        public abstract string DisplayName { get; }
        public abstract HeroWeaponRangeType RangeType { get; }
        public float BaseDamage => baseDamage;
        public virtual GameObject ViewPrefab => null;
        public IReadOnlyList<WeaponAbility> Abilities
        {
            get
            {
                EnsureAbilityInstances();
                return _abilities;
            }
        }

        public virtual bool SupportsAbility(string abilityId)
        {
            if (string.IsNullOrWhiteSpace(abilityId))
                return false;

            EnsureAbilityInstances();
            for (int i = 0; i < _abilities.Length; i++)
            {
                WeaponAbility ability = _abilities[i];
                if (ability == null || string.IsNullOrWhiteSpace(ability.AbilityId))
                    continue;

                if (string.Equals(ability.AbilityId, abilityId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public virtual void ProcessInput(HeroBase hero, UnitCombat combat, InputIntent intent)
        {
            EnsureAbilityInstances();
            for (int i = 0; i < _abilities.Length; i++)
            {
                WeaponAbility ability = _abilities[i];
                if (ability == null || !ability.IsEnabled)
                    continue;

                ability.Bind(this, hero, combat);
                ability.ProcessInput(intent);
            }
        }

        public virtual bool ProcessPriorityInput(
            HeroBase hero,
            UnitCombat combat,
            InputIntent intent,
            out bool blocksModeAbilities,
            out bool blocksMovement,
            out bool blocksPrimaryAttack)
        {
            blocksModeAbilities = false;
            blocksMovement = false;
            blocksPrimaryAttack = false;

            EnsureAbilityInstances();
            for (int i = 0; i < _abilities.Length; i++)
            {
                WeaponAbility ability = _abilities[i];
                if (ability == null || !ability.IsEnabled)
                    continue;

                ability.Bind(this, hero, combat);
                if (!ability.ProcessPriorityInput(
                        intent,
                        out bool abilityBlocksModeAbilities,
                        out bool abilityBlocksMovement,
                        out bool abilityBlocksPrimaryAttack))
                    continue;

                blocksModeAbilities |= abilityBlocksModeAbilities;
                blocksMovement |= abilityBlocksMovement;
                blocksPrimaryAttack |= abilityBlocksPrimaryAttack;
                return true;
            }

            return false;
        }

        public virtual void ResetAbilityState()
        {
            EnsureAbilityInstances();
            for (int i = 0; i < _abilities.Length; i++)
            {
                WeaponAbility ability = _abilities[i];
                if (ability == null)
                    continue;

                ability.ResetState();
                ability.Unbind();
            }
        }

        public virtual void Normalize()
        {
            EnsureAbilityInstances();
        }

        protected void SetDefaultBaseDamage(float value)
        {
            baseDamage = Mathf.Max(0f, value);
        }

        protected virtual WeaponAbility[] CreateAbilities()
        {
            return Array.Empty<WeaponAbility>();
        }

        void EnsureAbilityInstances()
        {
            _abilities ??= CreateAbilities() ?? Array.Empty<WeaponAbility>();
        }
    }
}
