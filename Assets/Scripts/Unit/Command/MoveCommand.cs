using Unit.Command;
using UnityEngine;

public sealed class MoveCommand : IUnitCommand
{
    readonly Vector3 _dest;
    readonly float _arriveDist;

    public MoveCommand(Vector3 dest, float arriveDist = 0.15f)
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
        // 你的 Motor 自己 Update 里会沿路径走，这里不需要做事
    }

    public bool IsDone(UnitContext ctx)
    {
        // 用水平距离判定（和你 motor 的 arriveDistance 一致）
        Vector3 a = ctx.Transform.position; a.y = 0f;
        Vector3 b = _dest;                 b.y = 0f;
        return Vector3.Distance(a, b) <= _arriveDist;
    }

    public void End(UnitContext ctx)
    {
        ctx.Motor.ClearDestination();
    }
}