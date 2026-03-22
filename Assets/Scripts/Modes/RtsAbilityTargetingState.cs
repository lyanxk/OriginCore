using Unit.Ability;

public static class RtsAbilityTargetingState
{
    static AbilityInputRouter s_router;
    static UnitAbility s_ability;
    static IRtsGroundTargetAbility s_groundTargetAbility;

    public static bool HasPending => s_router != null && s_groundTargetAbility != null;

    public static bool IsPending(AbilityInputRouter router, UnitAbility ability)
    {
        return router != null && router == s_router && ability != null && ability == s_ability && HasPending;
    }

    public static void SetPending(AbilityInputRouter router, UnitAbility ability, IRtsGroundTargetAbility groundTargetAbility)
    {
        s_router = router;
        s_ability = ability;
        s_groundTargetAbility = groundTargetAbility;
    }

    public static bool TryActivateAtPoint(UnityEngine.Vector3 worldPoint)
    {
        if (!HasPending)
            return false;

        if (!s_groundTargetAbility.TryActivateAtPoint(worldPoint))
            return false;

        Clear();
        return true;
    }

    public static void Clear()
    {
        s_router = null;
        s_ability = null;
        s_groundTargetAbility = null;
    }

    public static void Clear(AbilityInputRouter router)
    {
        if (router == null || router != s_router)
            return;

        Clear();
    }
}
