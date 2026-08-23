using System;

namespace OriginCore.Gameplay
{
    [Flags]
    public enum UnitRole
    {
        None = 0,
        Worker = 1 << 0,
        Combat = 1 << 1,
        Hero = 1 << 2,
        Building = 1 << 3,
        Air = 1 << 4
    }

    public static class UnitRoleExtensions
    {
        public static bool HasAny(this UnitRole value, UnitRole roles)
        {
            return roles != UnitRole.None && (value & roles) != 0;
        }

        public static bool HasAll(this UnitRole value, UnitRole roles)
        {
            return (value & roles) == roles;
        }
    }
}
