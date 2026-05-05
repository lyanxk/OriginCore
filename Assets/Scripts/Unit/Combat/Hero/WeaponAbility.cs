using System;
using Input;
using UnityEngine;

namespace Unit.Combat.Hero
{
    [Serializable]
    public abstract class WeaponAbility
    {
        [NonSerialized] HeroWeapon _weapon;
        [NonSerialized] HeroBase _hero;
        [NonSerialized] UnitCombat _combat;

        public abstract string AbilityId { get; }
        public abstract string DisplayName { get; }
        public virtual bool IsEnabled => true;

        protected HeroWeapon Weapon => _weapon;
        protected HeroBase Hero => _hero;
        protected UnitCombat Combat => _combat;
        protected Transform HeroTransform => _hero != null ? _hero.transform : null;

        internal void Bind(HeroWeapon weapon, HeroBase hero, UnitCombat combat)
        {
            _weapon = weapon;
            _hero = hero;
            _combat = combat;
            OnBound();
        }

        internal void Unbind()
        {
            OnUnbound();
            _weapon = null;
            _hero = null;
            _combat = null;
        }

        public virtual void ProcessInput(InputIntent intent)
        {
        }

        public virtual bool ProcessPriorityInput(InputIntent intent, out bool blocksModeAbilities, out bool blocksMovement, out bool blocksPrimaryAttack)
        {
            blocksModeAbilities = false;
            blocksMovement = false;
            blocksPrimaryAttack = false;
            return false;
        }

        public virtual void ResetState()
        {
        }

        protected virtual void OnBound()
        {
        }

        protected virtual void OnUnbound()
        {
            ResetState();
        }
    }
}
