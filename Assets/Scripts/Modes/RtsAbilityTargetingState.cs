using Unit.Ability;
using Unit.Selection;
using UnityEngine;

namespace Modes
{
    public static class RtsAbilityTargetingState
    {
        static AbilityInputRouter s_router;
        static RtsUnitAbility s_ability;
        static RtsAbilityTargetingMode s_targetingMode;

        public static bool HasPending => s_router != null
                                         && s_ability != null
                                         && (s_targetingMode == RtsAbilityTargetingMode.Unit
                                             || s_targetingMode == RtsAbilityTargetingMode.Point);
        public static RtsAbilityTargetingMode TargetingMode => HasPending ? s_targetingMode : RtsAbilityTargetingMode.None;
        public static float PointPreviewRadius => HasPending && s_targetingMode == RtsAbilityTargetingMode.Point && s_ability != null
            ? Mathf.Max(0f, s_ability.TargetingPreviewRadius)
            : 0f;

        public static bool IsPending(AbilityInputRouter router, RtsUnitAbility ability)
        {
            return router != null && router == s_router && ability != null && ability == s_ability && HasPending;
        }

        public static void SetPending(AbilityInputRouter router, RtsUnitAbility ability)
        {
            if (router == null || ability == null)
            {
                Clear();
                return;
            }

            RtsAbilityTargetingMode targetingMode = ability.TargetingMode;
            if (targetingMode != RtsAbilityTargetingMode.Unit && targetingMode != RtsAbilityTargetingMode.Point)
            {
                Clear();
                return;
            }

            s_router = router;
            s_ability = ability;
            s_targetingMode = targetingMode;
        }

        public static bool TryActivateAtPoint(Vector3 worldPoint, bool append)
        {
            if (!HasPending || s_targetingMode != RtsAbilityTargetingMode.Point)
                return false;

            if (!s_ability.TryActivatePendingPoint(worldPoint, append))
                return false;

            Clear();
            return true;
        }

        public static bool TryActivateOnUnit(Selectable target, Vector3 worldPoint, bool append)
        {
            if (!HasPending || s_targetingMode != RtsAbilityTargetingMode.Unit)
                return false;

            if (!s_ability.TryActivatePendingUnit(target, worldPoint, append))
                return false;

            Clear();
            return true;
        }

        public static void Clear()
        {
            s_router = null;
            s_ability = null;
            s_targetingMode = RtsAbilityTargetingMode.None;
        }

        public static void Clear(AbilityInputRouter router)
        {
            if (router == null || router != s_router)
                return;

            Clear();
        }
    }
}
