using UnityEngine;

namespace Unit.Command
{
    public sealed class AttackCommand : IUnitCommand, ICommandRoutePointProvider
    {
        readonly Transform _initialTarget;
        readonly bool _isAttackMove;
        readonly Vector3 _moveDestination;

        readonly float _stopBuffer;
        readonly float _scanInterval;
        readonly float _arriveDist;
        readonly float _repathInterval;
        readonly float _repathDistance;

        Transform _lockedTarget;
        float _scanTimer;
        float _repathTimer;
        bool _hasChasePos;
        Vector3 _lastChasePos;
        Vector3 _resolvedMoveDestination;
        bool _hasResolvedMoveDestination;
        bool _done;
    
        public AttackCommand(
            Transform target,
            float stopBuffer = 0.1f,
            float repathInterval = 0.2f,
            float repathDistance = 0.5f)
        {
            _initialTarget = target;
            _isAttackMove = false;
            _moveDestination = Vector3.zero;

            _stopBuffer = Mathf.Max(0f, stopBuffer);
            _scanInterval = 0.15f;
            _arriveDist = -1f;
            _repathInterval = Mathf.Max(0.05f, repathInterval);
            _repathDistance = Mathf.Max(0.05f, repathDistance);
        }
    
        public AttackCommand(
            Vector3 moveDestination,
            float arriveDist = -1f,
            float scanInterval = 0.15f,
            float stopBuffer = 0.1f,
            float repathInterval = 0.2f,
            float repathDistance = 0.5f)
        {
            _initialTarget = null;
            _isAttackMove = true;
            _moveDestination = moveDestination;

            _stopBuffer = Mathf.Max(0f, stopBuffer);
            _scanInterval = Mathf.Max(0.05f, scanInterval);
            _arriveDist = arriveDist;
            _repathInterval = Mathf.Max(0.05f, repathInterval);
            _repathDistance = Mathf.Max(0.05f, repathDistance);
        }

        public void Begin(UnitContext ctx)
        {
            _done = false;
            _lockedTarget = _initialTarget;
            _scanTimer = 0f;
            _repathTimer = 0f;
            _hasChasePos = false;
            _hasResolvedMoveDestination = false;
            _resolvedMoveDestination = Vector3.zero;

            if (_isAttackMove)
            {
                ctx.Motor.SetDestination(_moveDestination);
                _resolvedMoveDestination = ctx.Motor.CurrentDestination;
                _hasResolvedMoveDestination = ctx.Motor.HasDestination;
            }

            if (ctx.Combat == null)
            {
                if (_isAttackMove)
                    return;

                _done = true;
            }
        }

        public void Tick(UnitContext ctx, float dt)
        {
            if (_done) return;

            if (ctx.Combat == null)
            {
                if (_isAttackMove && IsArrived(ctx))
                {
                    ctx.Motor.CancelPathing();
                    _done = true;
                }
                return;
            }

            if (_lockedTarget == null)
            {
                if (_isAttackMove)
                    TryAcquireTarget(ctx, dt);

                if (_lockedTarget == null)
                {
                    if (_isAttackMove && IsArrived(ctx))
                    {
                        ctx.Motor.CancelPathing();
                        _done = true;
                    }
                    else if (!_isAttackMove)
                    {
                        _done = true;
                    }
                    return;
                }
            }

            if (!ctx.Combat.CanKeepTargetLocked(_lockedTarget))
            {
                _lockedTarget = null;
                _hasChasePos = false;

                if (_isAttackMove)
                {
                    ctx.Motor.SetDestination(_moveDestination);
                    _resolvedMoveDestination = ctx.Motor.CurrentDestination;
                    _hasResolvedMoveDestination = ctx.Motor.HasDestination;
                }
                else
                    _done = true;

                return;
            }

            FaceTarget(ctx, _lockedTarget.position);

            if (ctx.Combat.IsTargetInRange(_lockedTarget, _stopBuffer))
            {
                _hasChasePos = false;
                ctx.Motor.CancelPathing();
                ctx.Combat.TryUsePrimaryOnTarget(_lockedTarget);
                return;
            }

            _repathTimer -= dt;
            Vector3 chasePos = _lockedTarget.position;
            bool targetMoved = !_hasChasePos ||
                               (chasePos - _lastChasePos).sqrMagnitude >= _repathDistance * _repathDistance;

            if (_repathTimer <= 0f || targetMoved)
            {
                ctx.Motor.SetDestination(chasePos);
                _lastChasePos = chasePos;
                _hasChasePos = true;
                _repathTimer = _repathInterval;
            }
        }

        public bool IsDone(UnitContext ctx)
        {
            return _done;
        }

        public void End(UnitContext ctx)
        {
            ctx.Motor.CancelPathing();
        }
        //获取路径点
        public bool TryGetRoutePoint(UnitContext ctx, out Vector3 worldPoint)
        {
            Transform routeTarget = _lockedTarget != null ? _lockedTarget : _initialTarget;
            if (routeTarget != null)
            {
                worldPoint = routeTarget.position;
                return true;
            }

            if (_isAttackMove)
            {
                worldPoint = _hasResolvedMoveDestination ? _resolvedMoveDestination : _moveDestination;
                return true;
            }

            worldPoint = default;
            return false;
        }

        void TryAcquireTarget(UnitContext ctx, float dt)
        {
            _scanTimer -= dt;
            if (_scanTimer > 0f)
                return;

            _scanTimer = _scanInterval;
            _lockedTarget = ctx.Combat.FindNearestTargetInDetectionRange();

            if (_lockedTarget != null)
            {
                _repathTimer = 0f;
                _hasChasePos = false;
            }
        }

        static void FaceTarget(UnitContext ctx, Vector3 targetPos)
        {
            Vector3 to = targetPos - ctx.Transform.position;
            to.y = 0f;
            if (to.sqrMagnitude <= 1e-6f)
                return;

            float yaw = Quaternion.LookRotation(to, Vector3.up).eulerAngles.y;
            ctx.Motor.SetYaw(yaw);
        }

        bool IsArrived(UnitContext ctx)
        {
            float arriveDist = _arriveDist > 0f ? _arriveDist : ctx.Motor.arriveDistance;

            Vector3 a = ctx.Transform.position;
            Vector3 b = _hasResolvedMoveDestination ? _resolvedMoveDestination : _moveDestination;
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b) <= arriveDist;
        }
    }
}
