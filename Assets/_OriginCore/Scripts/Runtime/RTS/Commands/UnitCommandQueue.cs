using System;
using System.Collections.Generic;
using OriginCore.Combat;
using OriginCore.Gameplay;
using OriginCore.RTS;
using OriginCore.Units;
using UnityEngine;

namespace OriginCore.RTS.Commands
{
    [DisallowMultipleComponent]
    public sealed class UnitCommandQueue : MonoBehaviour, IRtsCommandStatus,
        IRtsStyledRouteSource
    {
        public const int WaitingCapacity = 5;

        [SerializeField] private EntityIdentity _identity;
        [SerializeField] private NavMeshMovementDriver _movementDriver;
        [SerializeField] private HybridControlDriver _hybridControlDriver;
        [SerializeField] private AttackCapability _attackCapability;
        [SerializeField] private AutoTargetScanner _targetScanner;
        [SerializeField] private CombatStatusController _combatStatuses;

        private readonly Queue<IRtsCommand> _waiting = new Queue<IRtsCommand>(WaitingCapacity);
        private IRtsCommand _current;

        public event Action<UnitCommandQueue> QueueChanged;
        public event Action<UnitCommandQueue, IRtsCommand> CommandStarted;
        public event Action<UnitCommandQueue, IRtsCommand, CommandStatus> CommandFinished;
        public event Action<UnitCommandQueue, IRtsCommand, CommandRejectionReason> CommandRejected;

        public EntityIdentity Identity => _identity;
        public NavMeshMovementDriver MovementDriver => _movementDriver;
        public HybridControlDriver HybridControlDriver => _hybridControlDriver;
        public AttackCapability AttackCapability => _attackCapability;
        public AutoTargetScanner TargetScanner => _targetScanner;
        public IRtsCommand Current => _current;
        public CommandStatus CurrentStatus => _current != null ? _current.Status : CommandStatus.Pending;
        public string CurrentDisplayName => _current != null ? _current.DisplayName : string.Empty;
        public int WaitingCount => _waiting.Count;
        public int TotalCommandCount => (_current != null ? 1 : 0) + _waiting.Count;
        public bool HasCurrentCommand => _current != null && !_current.Status.IsTerminal();
        public bool CanAppend => _waiting.Count < WaitingCapacity;

        private void Awake()
        {
            CacheComponents();
        }

        private void OnEnable()
        {
            CacheComponents();
            BindHybridDriver();
        }

        private void OnDisable()
        {
            UnbindHybridDriver();
            StopAll();
        }

        private void OnValidate()
        {
            CacheComponents();
        }

        private void Update()
        {
            TickCommands(Time.deltaTime);
        }

        public void Configure(
            EntityIdentity identity,
            NavMeshMovementDriver movementDriver,
            HybridControlDriver hybridControlDriver = null,
            AttackCapability attackCapability = null,
            AutoTargetScanner targetScanner = null)
        {
            UnbindHybridDriver();
            _identity = identity != null ? identity : GetComponent<EntityIdentity>();
            _movementDriver = movementDriver != null
                ? movementDriver
                : GetComponent<NavMeshMovementDriver>();
            _hybridControlDriver = hybridControlDriver != null
                ? hybridControlDriver
                : GetComponent<HybridControlDriver>();
            _attackCapability = attackCapability != null
                ? attackCapability
                : GetComponent<AttackCapability>();
            _targetScanner = targetScanner != null
                ? targetScanner
                : GetComponent<AutoTargetScanner>();
            BindHybridDriver();
        }

        public bool TryIssue(
            IRtsCommand command,
            bool append,
            out CommandRejectionReason rejectionReason)
        {
            rejectionReason = CommandRejectionReason.None;
            if (command == null)
            {
                return Reject(command, CommandRejectionReason.InvalidCommand, out rejectionReason);
            }

            if (command.Status != CommandStatus.Pending)
            {
                return Reject(command, CommandRejectionReason.InvalidState, out rejectionReason);
            }

            CacheComponents();
            if (_movementDriver == null && command is MoveCommand)
            {
                return Reject(command, CommandRejectionReason.MissingDriver, out rejectionReason);
            }

            if (command is AttackCommand && _attackCapability == null)
            {
                return Reject(
                    command,
                    CommandRejectionReason.MissingAttackCapability,
                    out rejectionReason);
            }

            if (command is AttackMoveCommand)
            {
                if (_movementDriver == null)
                {
                    return Reject(command, CommandRejectionReason.MissingDriver, out rejectionReason);
                }

                if (_attackCapability == null)
                {
                    return Reject(
                        command,
                        CommandRejectionReason.MissingAttackCapability,
                        out rejectionReason);
                }

                if (_targetScanner == null)
                {
                    return Reject(
                        command,
                        CommandRejectionReason.MissingTargetScanner,
                        out rejectionReason);
                }
            }

            if (!append)
            {
                StopAll();
                StartCommand(command);
                return true;
            }

            if (_current == null)
            {
                StartCommand(command);
                return true;
            }

            if (_waiting.Count >= WaitingCapacity)
            {
                return Reject(command, CommandRejectionReason.QueueFull, out rejectionReason);
            }

            _waiting.Enqueue(command);
            QueueChanged?.Invoke(this);
            return true;
        }

        public bool TryBuildRoute(List<Vector3> points)
        {
            if (points == null)
            {
                return false;
            }

            points.Clear();
            points.Add(transform.position);
            AppendRoutePoint(_current, points);
            foreach (IRtsCommand waitingCommand in _waiting)
            {
                AppendRoutePoint(waitingCommand, points);
            }

            return points.Count > 1;
        }

        public bool TryBuildStyledRoute(List<RtsRouteSegment> segments)
        {
            if (segments == null)
            {
                return false;
            }

            segments.Clear();
            Vector3 routeOrigin = transform.position;
            AppendRouteSegment(_current, ref routeOrigin, segments);
            foreach (IRtsCommand waitingCommand in _waiting)
            {
                AppendRouteSegment(waitingCommand, ref routeOrigin, segments);
            }

            return segments.Count > 0;
        }

        public void TickCommands(float deltaTime)
        {
            if (_combatStatuses != null && _combatStatuses.IsTemporallyLocked)
            {
                return;
            }

            if (_current == null)
            {
                TryStartNext();
                return;
            }

            if (!_current.Status.IsTerminal())
            {
                _current.Tick(Mathf.Max(0f, deltaTime));
            }

            FinishTerminalAndAdvance();
        }

        public int StopAll()
        {
            int cancelledCount = 0;
            IRtsCommand current = _current;
            _current = null;
            if (current != null)
            {
                if (!current.Status.IsTerminal())
                {
                    current.Cancel();
                    cancelledCount++;
                }

                CommandFinished?.Invoke(this, current, current.Status);
            }

            while (_waiting.Count > 0)
            {
                IRtsCommand waiting = _waiting.Dequeue();
                if (waiting != null && !waiting.Status.IsTerminal())
                {
                    waiting.Cancel();
                    cancelledCount++;
                }

                if (waiting != null)
                {
                    CommandFinished?.Invoke(this, waiting, waiting.Status);
                }
            }

            if (current != null || cancelledCount > 0)
            {
                QueueChanged?.Invoke(this);
            }

            return cancelledCount;
        }

        private void StartCommand(IRtsCommand command)
        {
            _current = command;
            UnitCommandContext context = new UnitCommandContext(
                this,
                gameObject,
                _identity,
                _movementDriver,
                _attackCapability,
                _targetScanner);
            command.Begin(context);
            CommandStarted?.Invoke(this, command);
            QueueChanged?.Invoke(this);
            FinishTerminalAndAdvance();
        }

        private void FinishTerminalAndAdvance()
        {
            int guard = WaitingCapacity + 2;
            while (_current != null && _current.Status.IsTerminal() && guard-- > 0)
            {
                IRtsCommand finished = _current;
                CommandStatus status = finished.Status;
                _current = null;
                CommandFinished?.Invoke(this, finished, status);
                QueueChanged?.Invoke(this);
                TryStartNext();
            }
        }

        private void TryStartNext()
        {
            if (_current != null || _waiting.Count == 0)
            {
                return;
            }

            IRtsCommand next = _waiting.Dequeue();
            StartCommand(next);
        }

        private bool Reject(
            IRtsCommand command,
            CommandRejectionReason reason,
            out CommandRejectionReason rejectionReason)
        {
            rejectionReason = reason;
            CommandRejected?.Invoke(this, command, reason);
            return false;
        }

        private static void AppendRoutePoint(IRtsCommand command, List<Vector3> points)
        {
            if (command == null || command.Status.IsTerminal() ||
                !(command is IRtsCommandRoutePointProvider routePointProvider) ||
                routePointProvider.RouteStyle == RtsRouteStyle.None ||
                !routePointProvider.TryGetRoutePoint(out Vector3 routePoint))
            {
                return;
            }

            Vector3 previous = points[points.Count - 1];
            if ((previous - routePoint).sqrMagnitude <= 0.01f)
            {
                return;
            }

            points.Add(routePoint);
        }

        private static void AppendRouteSegment(
            IRtsCommand command,
            ref Vector3 routeOrigin,
            List<RtsRouteSegment> segments)
        {
            if (command == null || command.Status.IsTerminal() ||
                !(command is IRtsCommandRoutePointProvider routePointProvider) ||
                routePointProvider.RouteStyle == RtsRouteStyle.None ||
                !routePointProvider.TryGetRoutePoint(out Vector3 routePoint))
            {
                return;
            }

            if ((routeOrigin - routePoint).sqrMagnitude <= 0.01f)
            {
                return;
            }

            segments.Add(new RtsRouteSegment(
                routeOrigin,
                routePoint,
                routePointProvider.RouteStyle));
            routeOrigin = routePoint;
        }

        private void HandleHybridStateChanged(
            HybridControlDriver driver,
            HybridControlState previous,
            HybridControlState current)
        {
            if (current == HybridControlState.Manual)
            {
                StopAll();
            }
        }

        private void CacheComponents()
        {
            if (_identity == null)
            {
                _identity = GetComponent<EntityIdentity>();
            }

            if (_movementDriver == null)
            {
                _movementDriver = GetComponent<NavMeshMovementDriver>();
            }

            if (_hybridControlDriver == null)
            {
                _hybridControlDriver = GetComponent<HybridControlDriver>();
            }

            if (_attackCapability == null)
            {
                _attackCapability = GetComponent<AttackCapability>();
            }

            if (_targetScanner == null)
            {
                _targetScanner = GetComponent<AutoTargetScanner>();
            }

            if (_combatStatuses == null)
            {
                _combatStatuses = GetComponent<CombatStatusController>();
            }
        }

        private void BindHybridDriver()
        {
            if (_hybridControlDriver != null)
            {
                _hybridControlDriver.StateChanged -= HandleHybridStateChanged;
                _hybridControlDriver.StateChanged += HandleHybridStateChanged;
            }
        }

        private void UnbindHybridDriver()
        {
            if (_hybridControlDriver != null)
            {
                _hybridControlDriver.StateChanged -= HandleHybridStateChanged;
            }
        }
    }
}
