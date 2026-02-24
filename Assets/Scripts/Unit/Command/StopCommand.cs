using Unit.Command;
using UnityEngine;

public sealed class StopCommand : IUnitCommand
{
    bool _done;

    public void Begin(UnitContext ctx)
    {
        _done = true;

        if (ctx == null || ctx.Motor == null)
            return;

        ctx.Motor.CancelPathing();

        // Clear short-lived planar overrides such as dashes.
        ctx.Motor.OverridePlanarVelocity(Vector3.zero, 0.01f);
    }

    public void Tick(UnitContext ctx, float dt)
    {
    }

    public bool IsDone(UnitContext ctx)
    {
        return _done;
    }

    public void End(UnitContext ctx)
    {
        if (ctx?.Motor != null)
            ctx.Motor.CancelPathing();
    }
}
