using System;
using System.Collections.Generic;
using OriginCore.Combat;
using OriginCore.Content;
using OriginCore.Gameplay;
using OriginCore.Units;
using UnityEngine;
using UnityEngine.AI;

namespace OriginCore.Weapons
{
    [DisallowMultipleComponent]
    [RequireComponent(
        typeof(WeaponActionController),
        typeof(CombatHitboxResolver),
        typeof(HybridPawnMotor))]
    public sealed class WeaponActionExecutor : MonoBehaviour
    {
        [SerializeField] private WeaponActionController _actions;
        [SerializeField] private CombatHitboxResolver _hitboxes;
        [SerializeField] private HybridPawnMotor _motor;
        [Min(0f), SerializeField] private float _worldProbeRadius = 0.3f;
        [SerializeField] private LayerMask _worldMask = Physics.DefaultRaycastLayers;

        private readonly Collider[] _teleportProbe = new Collider[16];
        private readonly Collider[] _unitProbe = new Collider[96];
        private readonly List<Collider> _ignoredUnitColliders = new List<Collider>(24);

        private WeaponDefinition _weapon;
        private WeaponActionDefinition _action;
        private WeaponActionIntent _intent;
        private WeaponActionPhaseDefinition _phase;
        private float[] _nextHitTimes = Array.Empty<float>();
        private int[] _remainingHits = Array.Empty<int>();
        private Vector3[] _hitAnchors = Array.Empty<Vector3>();
        private bool[] _hasHitAnchors = Array.Empty<bool>();
        private float _motionDistanceTravelled;
        private bool _teleportAttempted;
        private Vector3 _crossThroughOrigin;
        private Vector3 _crossThroughDestination;
        private bool _crossThroughReturning;
        private float _crossThroughPauseRemaining;

        private void Awake()
        {
            CacheComponents();
        }

        private void OnEnable()
        {
            CacheComponents();
            BindEvents();
        }

        private void OnDisable()
        {
            UnbindEvents();
            ClearExecution();
        }

        private void OnValidate()
        {
            CacheComponents();
            _worldProbeRadius = Mathf.Max(0f, _worldProbeRadius);
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f || _action == null || _actions == null ||
                _actions.CurrentAction != _action)
            {
                return;
            }

            TickMotion(deltaTime);
            TickHits();
        }

        private void HandleActionStarted(
            WeaponActionController controller,
            WeaponDefinition weapon,
            WeaponActionDefinition action)
        {
            _weapon = weapon;
            _action = action;
            _intent = controller.CurrentIntent;
            _phase = null;
            _motionDistanceTravelled = 0f;
            _teleportAttempted = false;
            InitializeCrossThrough(action.Motion);
            WeaponHitDefinition[] hits = action.Hits;
            if (_nextHitTimes.Length < hits.Length)
            {
                _nextHitTimes = new float[hits.Length];
                _remainingHits = new int[hits.Length];
                _hitAnchors = new Vector3[hits.Length];
                _hasHitAnchors = new bool[hits.Length];
            }

            for (int i = 0; i < hits.Length; i++)
            {
                _nextHitTimes[i] = 0f;
                _remainingHits[i] = 0;
                _hasHitAnchors[i] = hits[i] != null && hits[i].AnchorAtActionStart;
                _hitAnchors[i] = _hasHitAnchors[i]
                    ? transform.TransformPoint(hits[i].LocalOffset)
                    : Vector3.zero;
            }
        }

        private void HandlePhaseChanged(
            WeaponActionController controller,
            WeaponActionDefinition action,
            WeaponActionPhaseDefinition phase)
        {
            if (_action != action)
            {
                return;
            }

            _phase = phase;
            WeaponHitDefinition[] hits = action.Hits;
            for (int i = 0; i < hits.Length; i++)
            {
                WeaponHitDefinition hit = hits[i];
                if (hit != null && hit.Phase == phase.Kind)
                {
                    _remainingHits[i] = hit.RepeatCount;
                    _nextHitTimes[i] = 0f;
                }
                else
                {
                    _remainingHits[i] = 0;
                }
            }

            TickHits();
        }

        private void HandleActionEnded(
            WeaponActionController controller,
            WeaponDefinition weapon,
            WeaponActionDefinition action,
            WeaponActionEndReason reason)
        {
            if (_action == action)
            {
                ClearExecution();
            }
        }

        private void TickHits()
        {
            if (_phase == null || _action == null || _weapon == null)
            {
                return;
            }

            WeaponHitDefinition[] hits = _action.Hits;
            for (int i = 0; i < hits.Length; i++)
            {
                WeaponHitDefinition hit = hits[i];
                if (hit == null || hit.Phase != _phase.Kind)
                {
                    continue;
                }

                while ((hit.RepeatUntilPhaseEnds || _remainingHits[i] > 0) &&
                       _actions.PhaseElapsed + 0.0001f >= _nextHitTimes[i])
                {
                    _hitboxes.Resolve(
                        _weapon,
                        _action,
                        hit,
                        _intent,
                        _hasHitAnchors[i],
                        _hitAnchors[i]);
                    if (!hit.RepeatUntilPhaseEnds)
                    {
                        _remainingHits[i]--;
                    }
                    _nextHitTimes[i] += Mathf.Max(0.0001f, hit.RepeatInterval);
                }
            }
        }

        private void TickMotion(float deltaTime)
        {
            if (_motor == null || _action == null || _phase == null ||
                _phase.Kind == WeaponActionPhaseKind.Recovery)
            {
                return;
            }

            WeaponActionMotionDefinition motion = _action.Motion;
            if (motion == null || motion.Kind == WeaponActionMotionKind.None)
            {
                if (_phase.GravityPolicy == WeaponGravityPolicy.Suspend)
                {
                    _motor.Move(Vector3.zero, 0f, deltaTime);
                }
                return;
            }

            if (motion.CollisionPolicy == WeaponCollisionPolicy.IgnoreUnits)
            {
                RefreshIgnoredUnitCollisions(motion);
            }

            Vector3 direction = ResolveMotionDirection();
            switch (motion.Kind)
            {
                case WeaponActionMotionKind.TeleportToTargetFront:
                    TryTeleportToTargetFront(motion);
                    break;
                case WeaponActionMotionKind.Hover:
                    _motor.Move(Vector3.zero, 0f, deltaTime);
                    break;
                case WeaponActionMotionKind.FollowTargetHeight:
                case WeaponActionMotionKind.LaunchWithTarget:
                    MoveTowardsTargetHeight(motion, direction, deltaTime);
                    break;
                case WeaponActionMotionKind.GroundSlam:
                    _motor.Move(
                        Vector3.zero,
                        -Mathf.Max(0.1f, motion.Speed),
                        deltaTime);
                    break;
                case WeaponActionMotionKind.Dash:
                    TickDash(motion, direction, deltaTime);
                    break;
                case WeaponActionMotionKind.CrossThroughAndReturn:
                    TickCrossThroughAndReturn(motion, deltaTime);
                    break;
            }
        }

        private void InitializeCrossThrough(WeaponActionMotionDefinition motion)
        {
            _crossThroughOrigin = transform.position;
            _crossThroughDestination = _crossThroughOrigin;
            _crossThroughReturning = false;
            _crossThroughPauseRemaining = 0f;
            if (motion == null || motion.Kind != WeaponActionMotionKind.CrossThroughAndReturn)
            {
                return;
            }

            Vector3 direction = ResolveMotionDirection();
            float distance = Mathf.Max(0f, motion.Distance);
            if (_intent.HasTarget)
            {
                Vector3 toTarget = Vector3.ProjectOnPlane(
                    _intent.Target.transform.position - _crossThroughOrigin,
                    Vector3.up);
                if (toTarget.sqrMagnitude > 0.000001f)
                {
                    direction = toTarget.normalized;
                    distance = Mathf.Min(
                        distance,
                        toTarget.magnitude + motion.TargetFrontOffset);
                }
            }

            _crossThroughDestination = _crossThroughOrigin + direction * distance;
        }

        private void TickCrossThroughAndReturn(
            WeaponActionMotionDefinition motion,
            float deltaTime)
        {
            if (_crossThroughPauseRemaining > 0f)
            {
                _crossThroughPauseRemaining = Mathf.Max(
                    0f,
                    _crossThroughPauseRemaining - deltaTime);
                _motor.Move(Vector3.zero, 0f, deltaTime);
                return;
            }

            Vector3 destination = _crossThroughReturning
                ? _crossThroughOrigin
                : _crossThroughDestination;
            Vector3 offset = Vector3.ProjectOnPlane(
                destination - transform.position,
                Vector3.up);
            if (offset.sqrMagnitude <= 0.0025f)
            {
                if (_crossThroughReturning)
                {
                    return;
                }

                _crossThroughReturning = true;
                _crossThroughPauseRemaining = 0.04f;
                return;
            }

            float speed = Mathf.Max(0.1f, motion.Speed);
            float distance = Mathf.Min(offset.magnitude, speed * deltaTime);
            Vector3 velocity = offset.normalized * (distance / deltaTime);
            float vertical = _phase.GravityPolicy == WeaponGravityPolicy.Suspend
                ? 0f
                : _motor.VerticalVelocity;
            _motor.Move(velocity, vertical, deltaTime);
        }

        private void TickDash(
            WeaponActionMotionDefinition motion,
            Vector3 direction,
            float deltaTime)
        {
            float remaining = Mathf.Max(0f, motion.Distance - _motionDistanceTravelled);
            if (remaining <= 0f)
            {
                return;
            }

            float phaseDuration = Mathf.Max(0.01f, _phase.Duration);
            float normalized = Mathf.Clamp01(_actions.PhaseElapsed / phaseDuration);
            float speed = motion.Speed > 0f
                ? motion.Speed
                : motion.Distance / phaseDuration;
            if (motion.SpeedCurve != null)
            {
                speed *= Mathf.Max(0f, motion.SpeedCurve.Evaluate(normalized));
            }

            float distance = Mathf.Min(remaining, speed * deltaTime);
            Vector3 velocity = direction * (distance / deltaTime);
            float vertical = _phase.GravityPolicy == WeaponGravityPolicy.Suspend
                ? 0f
                : _motor.VerticalVelocity;
            Vector3 before = transform.position;
            _motor.Move(velocity, vertical, deltaTime);
            _motionDistanceTravelled += Vector3.Distance(before, transform.position);
        }

        private void TryTeleportToTargetFront(WeaponActionMotionDefinition motion)
        {
            if (_teleportAttempted)
            {
                return;
            }
            _teleportAttempted = true;
            if (!_intent.HasTarget)
            {
                _actions.Cancel(WeaponActionEndReason.TargetInvalid);
                return;
            }

            Vector3 targetPosition = _intent.Target.transform.position;
            Vector3 fromTarget = Vector3.ProjectOnPlane(
                transform.position - targetPosition,
                Vector3.up);
            if (fromTarget.sqrMagnitude <= 0.000001f)
            {
                fromTarget = -_intent.Target.transform.forward;
            }
            Vector3 destination = targetPosition +
                                  fromTarget.normalized * motion.TargetFrontOffset;
            NavMeshAgent agent = GetComponent<NavMeshAgent>();
            int areaMask = agent != null ? agent.areaMask : NavMesh.AllAreas;
            Vector3 resolvedDestination;
            bool resolved = agent != null && agent.enabled && agent.isOnNavMesh
                ? NavMeshPositionResolver.TryResolveReachablePosition(
                    agent,
                    destination,
                    1.25f,
                    4f,
                    out resolvedDestination)
                : NavMeshPositionResolver.TryResolvePosition(
                    destination,
                    areaMask,
                    1.25f,
                    4f,
                    out resolvedDestination);
            if (!resolved)
            {
                _actions.Cancel(WeaponActionEndReason.Cancelled);
                return;
            }

            Vector3 probeCenter = resolvedDestination + Vector3.up;
            if (HasTeleportObstruction(probeCenter, _intent.Target))
            {
                _actions.Cancel(WeaponActionEndReason.Cancelled);
                return;
            }

            CharacterController controller = _motor.CharacterController;
            bool controllerWasEnabled = controller != null && controller.enabled;
            if (controller != null)
            {
                controller.enabled = false;
            }

            bool teleported;
            if (agent != null && agent.enabled)
            {
                teleported = agent.Warp(resolvedDestination) && agent.isOnNavMesh;
            }
            else
            {
                transform.position = resolvedDestination;
                teleported = true;
            }

            if (controller != null)
            {
                controller.enabled = controllerWasEnabled;
            }

            if (!teleported)
            {
                _actions.Cancel(WeaponActionEndReason.Cancelled);
                return;
            }

            _motor.ResetMotion();
            if (motion.FaceTarget)
            {
                _motor.RotateTowards(
                    targetPosition - transform.position,
                    10000f,
                    1f);
            }
        }

        private bool HasTeleportObstruction(Vector3 probeCenter, EntityIdentity target)
        {
            int count = Physics.OverlapSphereNonAlloc(
                probeCenter,
                _worldProbeRadius,
                _teleportProbe,
                _worldMask,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider candidate = _teleportProbe[i];
                _teleportProbe[i] = null;
                if (candidate == null || candidate.transform.IsChildOf(transform) ||
                    (target != null && candidate.transform.IsChildOf(target.transform)))
                {
                    continue;
                }
                return true;
            }
            return false;
        }

        private void MoveTowardsTargetHeight(
            WeaponActionMotionDefinition motion,
            Vector3 direction,
            float deltaTime)
        {
            if (!_intent.HasTarget)
            {
                _actions.Cancel(WeaponActionEndReason.TargetInvalid);
                return;
            }

            float deltaHeight = _intent.Target.transform.position.y - transform.position.y;
            float vertical = Mathf.Clamp(
                deltaHeight / Mathf.Max(deltaTime, 0.0001f),
                -Mathf.Max(0.1f, motion.Speed),
                Mathf.Max(0.1f, motion.Speed));
            _motor.Move(direction * motion.Speed, vertical, deltaTime);
        }

        private Vector3 ResolveMotionDirection()
        {
            if (_intent.HasTarget)
            {
                Vector3 targetDirection = Vector3.ProjectOnPlane(
                    _intent.Target.transform.position - transform.position,
                    Vector3.up);
                if (targetDirection.sqrMagnitude > 0.000001f)
                {
                    return targetDirection.normalized;
                }
            }

            Vector3 input = transform.forward * _intent.Move.y +
                            transform.right * _intent.Move.x;
            input = Vector3.ProjectOnPlane(input, Vector3.up);
            return input.sqrMagnitude > 0.000001f
                ? input.normalized
                : transform.forward;
        }

        private void ClearExecution()
        {
            RestoreIgnoredUnitCollisions();
            _weapon = null;
            _action = null;
            _intent = default;
            _phase = null;
            _motionDistanceTravelled = 0f;
            _teleportAttempted = false;
            _crossThroughOrigin = Vector3.zero;
            _crossThroughDestination = Vector3.zero;
            _crossThroughReturning = false;
            _crossThroughPauseRemaining = 0f;
        }

        private void RefreshIgnoredUnitCollisions(WeaponActionMotionDefinition motion)
        {
            CharacterController controller = _motor != null
                ? _motor.CharacterController
                : null;
            if (controller == null) return;
            float radius = Mathf.Max(2f, motion.Distance + 1f);
            int count = Physics.OverlapSphereNonAlloc(
                transform.position,
                radius,
                _unitProbe,
                ~0,
                QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                Collider candidate = _unitProbe[i];
                _unitProbe[i] = null;
                if (candidate == null || candidate == controller ||
                    candidate.transform.IsChildOf(transform) ||
                    candidate.GetComponentInParent<EntityIdentity>() == null ||
                    _ignoredUnitColliders.Contains(candidate))
                {
                    continue;
                }
                Physics.IgnoreCollision(controller, candidate, true);
                _ignoredUnitColliders.Add(candidate);
            }
        }

        private void RestoreIgnoredUnitCollisions()
        {
            CharacterController controller = _motor != null
                ? _motor.CharacterController
                : null;
            if (controller != null)
            {
                for (int i = 0; i < _ignoredUnitColliders.Count; i++)
                {
                    Collider candidate = _ignoredUnitColliders[i];
                    if (candidate != null)
                    {
                        Physics.IgnoreCollision(controller, candidate, false);
                    }
                }
            }
            _ignoredUnitColliders.Clear();
        }

        private void BindEvents()
        {
            UnbindEvents();
            if (_actions == null)
            {
                return;
            }
            _actions.ActionStarted += HandleActionStarted;
            _actions.PhaseChanged += HandlePhaseChanged;
            _actions.ActionEnded += HandleActionEnded;
        }

        private void UnbindEvents()
        {
            if (_actions == null)
            {
                return;
            }
            _actions.ActionStarted -= HandleActionStarted;
            _actions.PhaseChanged -= HandlePhaseChanged;
            _actions.ActionEnded -= HandleActionEnded;
        }

        private void CacheComponents()
        {
            if (_actions == null) _actions = GetComponent<WeaponActionController>();
            if (_hitboxes == null) _hitboxes = GetComponent<CombatHitboxResolver>();
            if (_motor == null) _motor = GetComponent<HybridPawnMotor>();
        }
    }
}
