using System.Collections.Generic;
using Unit.Command;
using UnityEngine;

[DisallowMultipleComponent]
public class CommandExecutor : MonoBehaviour
{
    const int MaxCommandCount = 6;

    readonly Queue<IUnitCommand> _queue = new Queue<IUnitCommand>(MaxCommandCount);
    IUnitCommand _current;

    UnitContext _ctx;

    void Awake()
    {
        var motor = GetComponent<UnitBase>();
        _ctx = new UnitContext(motor);
    }

    void Update()
    {
        float dt = Time.deltaTime;

        // 没有当前指令：取下一条
        if (_current == null)
        {
            if (_queue.Count == 0) return;
            _current = _queue.Dequeue();
            _current.Begin(_ctx);
        }

        _current.Tick(_ctx, dt);

        if (_current.IsDone(_ctx))
        {
            _current.End(_ctx);
            _current = null; // 下一帧自动取下一条
        }
    }

    public void Enqueue(IUnitCommand cmd, bool append)
    {
        if (!append)
        {
            InterruptAndClear();
        }
        else
        {
            int totalCount = _queue.Count + (_current != null ? 1 : 0);
            if (totalCount >= MaxCommandCount)
                return;
        }

        _queue.Enqueue(cmd);

        // 如果当前没有在执行任何指令，马上开始（不用等下一帧也行）
        if (_current == null)
        {
            _current = _queue.Dequeue();
            _current.Begin(_ctx);
        }
    }

    public void InterruptAndClear()
    {
        // 取消当前
        if (_current != null)
        {
            _current.End(_ctx);
            _current = null;
        }

        // 清队列
        _queue.Clear();

        // 清运动（你的需求：切模式/取消寻路）
        _ctx.Motor.CancelPathing(); 
    }

    public void Clear()
    {
        InterruptAndClear();
    }
}
