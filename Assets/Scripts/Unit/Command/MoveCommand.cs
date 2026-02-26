using Unit.Command;
using UnityEngine;

public sealed class MoveCommand : IUnitCommand
{
    readonly Vector3 _dest;
    readonly float _arriveDist;
    Vector3 _resolvedDest;
    bool _hasResolvedDest;

    public MoveCommand(Vector3 dest, float arriveDist = -1f)
    {
        _dest = dest;
        _arriveDist = arriveDist;
    }

    public void Begin(UnitContext ctx)
    {
        ctx.Motor.SetDestination(_dest);
        _resolvedDest = ctx.Motor.CurrentDestination;
        _hasResolvedDest = ctx.Motor.HasDestination;
    }

    public void Tick(UnitContext ctx, float dt)
    {
        //移动逻辑在单位里
    }

    public bool IsDone(UnitContext ctx)
    {
        float arriveDist = _arriveDist > 0f ? _arriveDist : ctx.Motor.arriveDistance;
        Vector3 doneDest = _hasResolvedDest ? _resolvedDest : _dest;

        // 用水平距离判定（默认与 motor 的 arriveDistance 保持一致）
        Vector3 a = ctx.Transform.position; a.y = 0f;
        Vector3 b = doneDest;              b.y = 0f;
        return Vector3.Distance(a, b) <= arriveDist;
    }

    public void End(UnitContext ctx)
    {
        ctx.Motor.CancelPathing();
    }
}
