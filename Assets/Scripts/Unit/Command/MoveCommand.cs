using Navigation;
using Unit.Movement;
using UnityEngine;

namespace Unit.Command
{
    public sealed class MoveCommand : IUnitCommand, ICommandRoutePointProvider
    {
        readonly Vector3 _dest;
        readonly float _arriveDist;
        readonly float _stuckTimeout;
        readonly float _stuckMoveThreshold;

        Vector3 _resolvedDest;
        bool _hasResolvedDest;
        Vector3 _lastPosition;
        float _stuckTimer;
        bool _done;
        bool _arrived;

        public MoveCommand(
            Vector3 dest,
            float arriveDist = -1f,
            float stuckTimeout = 2.5f,
            float stuckMoveThreshold = 0.05f)
        {
            _dest = dest;
            _arriveDist = arriveDist;
            _stuckTimeout = Mathf.Max(0.1f, stuckTimeout);
            _stuckMoveThreshold = Mathf.Max(0.001f, stuckMoveThreshold);
        }

        public void Begin(UnitContext ctx)
        {
            _done = false;
            _arrived = false;

            Vector3 targetForPathing = _dest;
            UnitBase motor = ctx?.Motor;
            if (motor != null)
            {
                OccupancyStandingRequest request = OccupancyStandingRequest.CreateDefault(motor.OccupancyRadius);
                request.SearchMode = OccupancySearchMode.RingAroundTarget;

                if (OccupancySystem.Instance.TryAssignStandingPoint(motor, _dest, request, out Vector3 standingPoint))
                    targetForPathing = standingPoint;
            }

            ctx.Motor.SetDestination(targetForPathing);
            _resolvedDest = ctx.Motor.CurrentDestination;
            _hasResolvedDest = ctx.Motor.HasDestination;
            _lastPosition = ctx.Transform.position;
            _stuckTimer = 0f;
        }

        public void Tick(UnitContext ctx, float dt)
        {
            if (_done) return;

            if (ctx?.Motor == null || ctx.Transform == null)
            {
                _done = true;
                return;
            }

            if (!ctx.Motor.HasDestination)
            {
                _done = true;
                return;
            }

            Vector3 current = ctx.Transform.position;
            Vector3 last = _lastPosition;
            current.y = 0f;
            last.y = 0f;

            float movedSqr = (current - last).sqrMagnitude;
            float thresholdSqr = _stuckMoveThreshold * _stuckMoveThreshold;

            if (movedSqr <= thresholdSqr)
                _stuckTimer += dt;
            else
                _stuckTimer = 0f;

            _lastPosition = ctx.Transform.position;

            if (_stuckTimer >= _stuckTimeout)
            {
                ctx.Motor.CancelPathing();
                _done = true;
            }
        }

        public bool IsDone(UnitContext ctx)
        {
            if (_done || ctx?.Motor == null || ctx.Transform == null)
                return true;

            float arriveDist = _arriveDist > 0f ? _arriveDist : ctx.Motor.arriveDistance;
            Vector3 doneDest = _hasResolvedDest ? _resolvedDest : _dest;

            // 用水平距离判定（默认与 motor 的 arriveDistance 保持一致）
            Vector3 a = ctx.Transform.position; a.y = 0f;
            Vector3 b = doneDest;              b.y = 0f;
            _arrived = Vector3.Distance(a, b) <= arriveDist;
            return _arrived;
        }

        public void End(UnitContext ctx)
        {
            if (!_arrived && ctx?.Motor != null && OccupancySystem.TryGetInstance(out OccupancySystem occupancy))
                occupancy.ReleaseClaim(ctx.Motor);

            ctx.Motor.CancelPathing();
        }
        //获取路径点
        public bool TryGetRoutePoint(UnitContext ctx, out Vector3 worldPoint)
        {
            worldPoint = _hasResolvedDest ? _resolvedDest : _dest;
            return true;
        }
    }
}
