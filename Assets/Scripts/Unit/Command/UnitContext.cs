using UnityEngine;

namespace Unit.Command
{
    public sealed class UnitContext
    {
        public readonly UnitBaseMotor Motor;
        public readonly Transform Transform;

        // 以后加：Combat, Skills, Animator, Stats, NavAgent...
        public UnitContext(UnitBaseMotor motor)
        {
            Motor = motor;
            Transform = motor.transform;
        }
    }

}