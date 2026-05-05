using Unit.Combat;
using Unit.Movement;
using UnityEngine;

namespace Unit.Command
{
    public sealed class UnitContext
    {
        public readonly UnitBase Motor;
        public readonly UnitCombat Combat;
        public readonly Transform Transform;

        // 以后加：Combat, Skills, Animator, Stats, NavAgent...
        public UnitContext(UnitBase motor)
        {
            Motor = motor;
            Combat = motor.Combat;
            Transform = motor.transform;
        }
    }

}
