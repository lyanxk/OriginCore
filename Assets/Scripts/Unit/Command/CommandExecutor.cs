using System.Collections.Generic;
using Unit.Movement;
using Unit.Selection;
using UnityEngine;
namespace Unit.Command
{
    [DisallowMultipleComponent]
    public class CommandExecutor : MonoBehaviour, ISelectionRouteProvider
    {
        const int MaxCommandCount = 6;

        readonly Queue<IUnitCommand> _queue = new Queue<IUnitCommand>(MaxCommandCount);
        IUnitCommand _current;

        UnitContext _ctx;

        public bool IsIdle => _current == null && _queue.Count == 0;

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

        public bool TryBuildSelectionRoute(List<Vector3> points)
        {
            if (points == null || _ctx == null || _ctx.Transform == null)
                return false;

            points.Clear();
            points.Add(_ctx.Transform.position);

            // Route preview follows the active command first, then each queued destination in order.
            AppendRoutePoint(_current, points);
            foreach (IUnitCommand queuedCommand in _queue)
                AppendRoutePoint(queuedCommand, points);

            return points.Count > 1;
        }

        void AppendRoutePoint(IUnitCommand command, List<Vector3> points)
        {
            if (command is not ICommandRoutePointProvider routePointProvider)
                return;

            if (!routePointProvider.TryGetRoutePoint(_ctx, out Vector3 routePoint))
                return;

            if (points.Count > 0)
            {
                Vector3 previous = points[points.Count - 1];
                if ((previous - routePoint).sqrMagnitude <= 0.01f)
                    return;
            }

            points.Add(routePoint);
        }
    }
}
