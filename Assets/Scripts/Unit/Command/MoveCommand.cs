using Unit.Command;
using UnityEngine;

public sealed class MoveCommand : IUnitCommand
{
    readonly Vector3 _dest;
    readonly float _arriveDist;

    public MoveCommand(Vector3 dest, float arriveDist = -1f)
    {
        _dest = dest;
        _arriveDist = arriveDist;
    }

    public void Begin(UnitContext ctx)
    {
        ctx.Motor.SetDestination(_dest);
    }

    public void Tick(UnitContext ctx, float dt)
    {
        //移动逻辑在单位里
    }

    public bool IsDone(UnitContext ctx)
    {
        float arriveDist = _arriveDist > 0f ? _arriveDist : ctx.Motor.arriveDistance;

        // 用水平距离判定（默认与 motor 的 arriveDistance 保持一致）
        Vector3 a = ctx.Transform.position; a.y = 0f;
        Vector3 b = _dest;                 b.y = 0f;
        return Vector3.Distance(a, b) <= arriveDist;
    }

    public void End(UnitContext ctx)
    {
        ctx.Motor.CancelPathing();
    }
}
