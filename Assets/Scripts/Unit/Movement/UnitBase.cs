using System.Collections.Generic;
using Navigation;
using Unit.Ability;
using Unit.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace Unit.Movement
{
    [RequireComponent(typeof(CharacterController))]
    public class UnitBase : MonoBehaviour
    {
        public float walkSpeed = 5.0f;
        public float runSpeed = 7.5f;
        public float crouchMoveSpeed = 2.5f;
        public float clickMoveSpeed = 4.5f;
        public float gravity = -12f;
        public float arriveDistance = 0.15f;
        public float jumpHeight = 1.4f;
        public float groundedStick = -2f;
        public float terminalVel = -30f;

        [Header("Ground Check")]
        public LayerMask groundMask;
        [Min(0.01f)] public float jumpGroundProbeDistance = 0.45f;
        [Range(0.1f, 1f)] public float groundProbeRadiusScale = 0.9f;

        CharacterController _cc;
        Vector3 _verticalVel;

        bool _hasDestination;
        Vector3 _destination;

        [Header("NavMesh Path")]
        public float sampleRadius = 2.0f;
        public float cornerReachDist = 0.25f;
        public float repathInterval = 0.25f;

        [Header("Occupancy")]
        [Min(0.1f)] public float occupancyRadius = 0.45f;

        [Header("Local Motion Correction")]
        [Min(0f)] public float unitSeparationRange = 0.4f;
        [Min(0f)] public float buildingPushRange = 0.3f;
        [Min(0.1f)] public float buildingPathLookAheadDistance = 2.5f;
        [Min(0f)] public float buildingPathSteerWeight = 2.2f;
        [Min(0f)] public float buildingAvoidanceStrength = 2.25f;
        [Min(0f)] public float buildingEdgeSlideStrength = 1.4f;
        [Min(0f)] public float localCorrectionSpeed = 2.8f;
        [Min(0f)] public float illegalRecoverSearchRadius = 3f;
        [Min(0f)] public float illegalRecoverCooldown = 0.35f;
        [Min(0f)] public float navMeshRecoverSpeed = 3f;
        [Min(0.1f)] public float navMeshCheckRadius = 0.4f;
        [Min(0.1f)] public float navMeshRecoverSampleRadius = 2.5f;

        NavMeshPath _path;
        Vector3[] _corners = System.Array.Empty<Vector3>();
        int _cornerIndex;
        float _repathTimer;
        float _illegalRecoverTimer;
        float _navMeshHardSnapTimer;

        bool _hasPlanarOverride;
        Vector3 _planarOverrideVel;
        float _planarOverrideTimer;
        Vector3 _lastPlanarVelocity;
        bool _flightEnabled;
        float _flightVerticalInput;
        float _flightVerticalSpeed;
        bool _isCrouching;
        bool _isRunning;
        float _fallSpeedReductionTimer;
        float _fallSpeedReductionMaxFallSpeed = 2f;
        float _fallSpeedReductionGravityMultiplier = 0.15f;
        float _collisionDisabledTimer;

        readonly RaycastHit[] _groundHits = new RaycastHit[8];
        readonly Dictionary<object, float> _moveSpeedMultipliers = new Dictionary<object, float>(4);
        float _cachedMoveSpeedMultiplier = 1f;

        public AbilityInputRouter AbilityRouter { get; private set; }
        public UnitCombat Combat { get; private set; }
        public bool HasDestination => _hasDestination;
        public Vector3 CurrentDestination => _destination;
        public float OccupancyRadius => Mathf.Max(0.1f, occupancyRadius);
        public bool IsFlightEnabled => _flightEnabled;
        public bool IsCrouching => _isCrouching;
        public bool IsRunning => _isRunning;
        public Vector3 PlanarVelocity => _lastPlanarVelocity;
        public float PlanarSpeed => _lastPlanarVelocity.magnitude;

        public float GetDirectMoveSpeed(bool run)
        {
            return run ? Mathf.Max(walkSpeed, runSpeed) : walkSpeed;
        }

        public float GetDirectMoveSpeed(bool run, bool crouch)
        {
            return crouch ? Mathf.Min(walkSpeed, crouchMoveSpeed) : GetDirectMoveSpeed(run);
        }

        void OnValidate()
        {
            walkSpeed = Mathf.Max(0f, walkSpeed);
            runSpeed = Mathf.Max(walkSpeed, runSpeed);
            crouchMoveSpeed = Mathf.Clamp(crouchMoveSpeed, 0f, walkSpeed);
            clickMoveSpeed = Mathf.Max(0f, clickMoveSpeed);
            jumpGroundProbeDistance = Mathf.Max(0.01f, jumpGroundProbeDistance);
            groundProbeRadiusScale = Mathf.Clamp(groundProbeRadiusScale, 0.1f, 1f);
        }

        public void OverridePlanarVelocity(Vector3 planarVel, float duration)
        {
            if (duration <= 0f)
                return;

            _hasPlanarOverride = true;
            _planarOverrideVel = planarVel;
            _planarOverrideTimer = Mathf.Max(_planarOverrideTimer, duration);
        }

        public void Launch(Vector3 planarVelocity, float verticalSpeed, float planarDuration)
        {
            if (_flightEnabled)
                SetFlightEnabled(false);

            CancelPathing();

            if (planarDuration > 0f && planarVelocity.sqrMagnitude > 1e-6f)
                OverridePlanarVelocity(planarVelocity, planarDuration);

            if (verticalSpeed > 0f)
                _verticalVel.y = Mathf.Max(_verticalVel.y, verticalSpeed);
        }

        public void ApplyFallSpeedReduction(float duration, float maxFallSpeed, float gravityMultiplier)
        {
            if (duration <= 0f)
                return;

            _fallSpeedReductionTimer = Mathf.Max(_fallSpeedReductionTimer, duration);
            _fallSpeedReductionMaxFallSpeed = Mathf.Max(0.01f, maxFallSpeed);
            _fallSpeedReductionGravityMultiplier = Mathf.Clamp01(gravityMultiplier);

            float maxDownwardVelocity = -_fallSpeedReductionMaxFallSpeed;
            if (_verticalVel.y < maxDownwardVelocity)
                _verticalVel.y = maxDownwardVelocity;
        }

        public void DisableCharacterCollision(float duration)
        {
            if (duration <= 0f)
                return;

            _collisionDisabledTimer = Mathf.Max(_collisionDisabledTimer, duration);
            SetCharacterControllerEnabled(false);
        }

        public void MoveIgnoringCollision(Vector3 worldDelta)
        {
            transform.position += worldDelta;
            if (Time.deltaTime <= 0f)
                return;

            _lastPlanarVelocity = worldDelta / Time.deltaTime;
            _lastPlanarVelocity.y = 0f;
        }

        public void SetMoveSpeedMultiplier(object source, float multiplier)
        {
            if (source == null)
                return;

            if (multiplier <= 1f)
            {
                ClearMoveSpeedMultiplier(source);
                return;
            }

            _moveSpeedMultipliers[source] = multiplier;
            RecalculateMoveSpeedMultiplier();
        }

        public void ClearMoveSpeedMultiplier(object source)
        {
            if (source == null || !_moveSpeedMultipliers.Remove(source))
                return;

            RecalculateMoveSpeedMultiplier();
        }

        public void TeleportTo(Vector3 worldPosition)
        {
            _hasPlanarOverride = false;
            _planarOverrideVel = Vector3.zero;
            _planarOverrideTimer = 0f;
            _lastPlanarVelocity = Vector3.zero;
            _verticalVel = Vector3.zero;

            if (_cc == null)
            {
                transform.position = worldPosition;
                return;
            }

            bool wasEnabled = _cc.enabled;
            if (wasEnabled)
                _cc.enabled = false;

            transform.position = worldPosition;

            if (wasEnabled)
                _cc.enabled = true;
        }

        public void SetFlightEnabled(bool enabled)
        {
            if (_flightEnabled == enabled)
                return;

            _flightEnabled = enabled;
            if (_flightEnabled)
            {
                _isCrouching = false;
                _isRunning = false;
            }
            _flightVerticalInput = 0f;
            _flightVerticalSpeed = 0f;
            _verticalVel = Vector3.zero;
        }

        public void SetCrouching(bool crouching)
        {
            _isCrouching = crouching && !_flightEnabled;
            if (_isCrouching)
                _isRunning = false;
        }

        public void SetRunning(bool running)
        {
            _isRunning = running && !_isCrouching && !_flightEnabled;
        }

        public void SetFlightVerticalInput(float input, float verticalSpeed)
        {
            if (!_flightEnabled)
            {
                _flightVerticalInput = 0f;
                _flightVerticalSpeed = 0f;
                return;
            }

            _flightVerticalInput = Mathf.Clamp(input, -1f, 1f);
            _flightVerticalSpeed = Mathf.Max(0f, verticalSpeed);
        }

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            AbilityRouter = GetComponent<AbilityInputRouter>();
            Combat = GetComponent<UnitCombat>();
            _path = new NavMeshPath();
            SyncOccupancyRadiusFromController();
        }

        void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            OccupancySystem.Instance.RegisterOrUpdateUnit(this, OccupancyRadius);
        }

        void OnDisable()
        {
            if (!Application.isPlaying)
                return;

            if (OccupancySystem.TryGetInstance(out OccupancySystem occupancy))
                occupancy.UnregisterUnit(this);
        }

        public void CancelPathing()
        {
            _hasDestination = false;
            _corners = System.Array.Empty<Vector3>();
            _cornerIndex = 0;
            _repathTimer = 0f;
        }

        public void SetDestination(Vector3 worldPos)
        {
            if (!NavMeshRoadNetwork.TryResolveDestination(worldPos, sampleRadius, out _destination))
                _destination = worldPos;

            _hasDestination = true;
            RecalculatePath();
            _repathTimer = repathInterval;
        }

        public void MoveImmediate(Vector3 worldDir, float speed)
        {
            Vector3 dir = worldDir;
            if (dir.sqrMagnitude > 1e-6f)
                dir.Normalize();

            ApplyMovement(dir * speed * _cachedMoveSpeedMultiplier, Time.deltaTime);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _illegalRecoverTimer = Mathf.Max(0f, _illegalRecoverTimer - dt);
            _navMeshHardSnapTimer = Mathf.Max(0f, _navMeshHardSnapTimer - dt);
            _fallSpeedReductionTimer = Mathf.Max(0f, _fallSpeedReductionTimer - dt);
            TickCollisionDisable(dt);

            ApplyMovement(GetPlannedVelocity(dt), dt);
        }

        public void Jump()
        {
            if (_flightEnabled)
                return;

            if (!CheckGround(jumpGroundProbeDistance))
                return;

            _verticalVel.y = Mathf.Sqrt(2f * jumpHeight * -gravity);
        }

        void ApplyMovement(Vector3 plannedVelocity, float dt)
        {
            Vector3 localCorrection = _flightEnabled
                ? Vector3.zero
                : BuildLocalCorrectionVelocity(dt, plannedVelocity);
            StepMovement(plannedVelocity + localCorrection, plannedVelocity, dt);
            if (_flightEnabled)
                return;
            if (_collisionDisabledTimer > 0f)
                return;

            TryRecoverToLegalPosition();
        }

        Vector3 GetPlannedVelocity(float dt)
        {
            if (!_hasDestination)
                return Vector3.zero;

            if (IsArrivedToDestination())
            {
                CancelPathing();
                return Vector3.zero;
            }

            _repathTimer -= dt;
            if (_repathTimer <= 0f)
            {
                RecalculatePath();
                _repathTimer = repathInterval;
            }

            return GetPathMoveDir() * clickMoveSpeed * _cachedMoveSpeedMultiplier;
        }

        void StepMovement(Vector3 planarVelocity, Vector3 facingVelocity, float dt)
        {
            if (_hasPlanarOverride)
            {
                _planarOverrideTimer -= dt;
                planarVelocity = _planarOverrideVel;

                if (_planarOverrideTimer <= 0f)
                {
                    _hasPlanarOverride = false;
                    _planarOverrideVel = Vector3.zero;
                }
            }

            _lastPlanarVelocity = planarVelocity;
            _lastPlanarVelocity.y = 0f;

            FaceMovementDirection(facingVelocity);

            if (_cc == null || !_cc.enabled)
                return;

            if (_flightEnabled)
            {
                _verticalVel.y = _flightVerticalInput * _flightVerticalSpeed;
            }
            else if (_cc.isGrounded)
            {
                if (_verticalVel.y < 0f)
                    _verticalVel.y = groundedStick;
            }
            else
            {
                bool isFallSpeedReduced = _fallSpeedReductionTimer > 0f;
                float gravityMultiplier = isFallSpeedReduced ? _fallSpeedReductionGravityMultiplier : 1f;
                float maxFallSpeed = isFallSpeedReduced ? -_fallSpeedReductionMaxFallSpeed : terminalVel;

                _verticalVel.y += gravity * gravityMultiplier * dt;
                if (_verticalVel.y < maxFallSpeed)
                    _verticalVel.y = maxFallSpeed;
            }

            Vector3 move = (planarVelocity + _verticalVel) * dt;
            _cc.Move(move);
        }

        bool IsArrivedToDestination()
        {
            Vector3 a = transform.position;
            Vector3 b = _destination;
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b) <= arriveDistance;
        }

        void RecalculatePath()
        {
            if (!_hasDestination)
                return;

            bool ok = NavMeshRoadNetwork.TryBuildPath(
                transform.position,
                _destination,
                _path,
                NavMeshRoadNetwork.DefaultStartSampleRadius,
                sampleRadius);

            if (!ok)
            {
                _corners = System.Array.Empty<Vector3>();
                _cornerIndex = 0;
                return;
            }

            _corners = _path.corners;
            _cornerIndex = 0;
        }

        Vector3 GetPathMoveDir()
        {
            if (_corners == null || _corners.Length == 0)
                return Vector3.zero;

            while (_cornerIndex < _corners.Length)
            {
                Vector3 corner = _corners[_cornerIndex];
                corner.y = transform.position.y;

                float reachDist = (_cornerIndex == _corners.Length - 1)
                    ? arriveDistance
                    : cornerReachDist;

                if (Vector3.Distance(transform.position, corner) <= reachDist)
                    _cornerIndex++;
                else
                    break;
            }

            if (_cornerIndex >= _corners.Length)
                return Vector3.zero;

            Vector3 target = _corners[_cornerIndex];
            target.y = transform.position.y;

            Vector3 dir = target - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f)
                return Vector3.zero;

            dir.Normalize();
            return ApplyPathBuildingSteering(dir);
        }

        void FaceMovementDirection(Vector3 planarVelocity)
        {
            planarVelocity.y = 0f;
            if (planarVelocity.sqrMagnitude <= 1e-6f)
                return;

            float yaw = Quaternion.LookRotation(planarVelocity, Vector3.up).eulerAngles.y;
            SetYaw(yaw);
        }

        public void SetYaw(float yawDegrees)
        {
            Vector3 eulerAngles = transform.eulerAngles;
            eulerAngles.y = yawDegrees;
            transform.eulerAngles = eulerAngles;
        }

        public float GetYaw()
        {
            return transform.eulerAngles.y;
        }

        void RecalculateMoveSpeedMultiplier()
        {
            // Multiple auras use the strongest modifier so support stacks stay bounded in large selections.
            _cachedMoveSpeedMultiplier = 1f;
            foreach (KeyValuePair<object, float> modifier in _moveSpeedMultipliers)
                _cachedMoveSpeedMultiplier = Mathf.Max(_cachedMoveSpeedMultiplier, modifier.Value);
        }

        Vector3 BuildLocalCorrectionVelocity(float dt, Vector3 plannedVelocity)
        {
            if (!OccupancySystem.TryGetInstance(out OccupancySystem occupancy))
                return BuildNavMeshRecoveryVelocity(dt);

            occupancy.RegisterOrUpdateUnit(this, OccupancyRadius);

            Vector3 pos = transform.position;
            Vector3 separation = occupancy.ComputeUnitSeparation(this, pos, OccupancyRadius, unitSeparationRange);
            Vector3 buildingPush = occupancy.ComputeBuildingAvoidance(
                pos,
                plannedVelocity,
                OccupancyRadius,
                buildingPushRange,
                buildingAvoidanceStrength,
                buildingEdgeSlideStrength);
            Vector3 navRecover = BuildNavMeshRecoveryVelocity(dt);
            Vector3 correction = separation + buildingPush + navRecover;
            correction.y = 0f;

            if (correction.sqrMagnitude <= 1e-6f)
                return Vector3.zero;

            float cap = Mathf.Max(0f, localCorrectionSpeed);
            if (cap <= 0f)
                return Vector3.zero;

            float magnitude = Mathf.Min(cap, correction.magnitude);
            return correction.normalized * magnitude;
        }

        Vector3 ApplyPathBuildingSteering(Vector3 baseDirection)
        {
            if (baseDirection.sqrMagnitude <= 1e-6f)
                return Vector3.zero;

            if (!OccupancySystem.TryGetInstance(out OccupancySystem occupancy))
                return baseDirection;

            float lookAhead = Mathf.Max(buildingPathLookAheadDistance, OccupancyRadius + buildingPushRange);
            if (!occupancy.TryGetBuildingPathSteering(
                    transform.position,
                    baseDirection,
                    OccupancyRadius,
                    lookAhead,
                    buildingPushRange,
                    out Vector3 steering))
                return baseDirection;

            Vector3 combined = baseDirection + steering * Mathf.Max(0f, buildingPathSteerWeight);
            combined.y = 0f;
            if (combined.sqrMagnitude <= 1e-6f)
                return baseDirection;

            combined.Normalize();
            if (Vector3.Dot(combined, baseDirection) <= 0.05f)
            {
                combined = baseDirection * 0.2f + steering.normalized;
                combined.y = 0f;
                if (combined.sqrMagnitude <= 1e-6f)
                    return baseDirection;

                combined.Normalize();
            }

            return combined;
        }

        Vector3 BuildNavMeshRecoveryVelocity(float dt)
        {
            if (dt <= 0f)
                return Vector3.zero;

            if (NavMeshRoadNetwork.TrySample(transform.position, navMeshCheckRadius, out _, NavMesh.AllAreas))
                return Vector3.zero;

            if (!NavMeshRoadNetwork.TrySample(transform.position, navMeshRecoverSampleRadius, out Vector3 nearestNav, NavMesh.AllAreas))
                return Vector3.zero;

            Vector3 delta = nearestNav - transform.position;
            delta.y = 0f;
            float dist = delta.magnitude;
            if (dist <= 0.001f)
                return Vector3.zero;

            float speed = Mathf.Max(0f, navMeshRecoverSpeed);
            return delta / dist * speed * Mathf.Clamp01(dist);
        }

        void TryRecoverToLegalPosition()
        {
            if (_illegalRecoverTimer > 0f)
                return;

            if (!OccupancySystem.TryGetInstance(out OccupancySystem occupancy))
                return;

            if (!occupancy.IsPositionOccupied(transform.position, OccupancyRadius * 0.95f, this))
                return;

            if (!occupancy.TryFindNearestLegalPoint(this, transform.position, OccupancyRadius, illegalRecoverSearchRadius, out Vector3 legalPoint))
                return;

            Vector3 delta = legalPoint - transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude <= 0.0025f)
                return;

            bool overlapsBuilding = occupancy.IsOverlappingBuilding(transform.position, OccupancyRadius * 0.95f);
            if (delta.sqrMagnitude >= 0.36f && (overlapsBuilding || !_hasDestination))
                RepositionWithoutPathReset(legalPoint);
            else
                OverridePlanarVelocity(
                    delta.normalized * Mathf.Max(Mathf.Max(0f, localCorrectionSpeed), clickMoveSpeed),
                    0.18f);

            _illegalRecoverTimer = illegalRecoverCooldown;
        }

        void RepositionWithoutPathReset(Vector3 worldPosition)
        {
            if (_cc == null)
            {
                transform.position = worldPosition;
            }
            else
            {
                bool wasEnabled = _cc.enabled;
                if (wasEnabled)
                    _cc.enabled = false;

                transform.position = worldPosition;

                if (wasEnabled)
                    _cc.enabled = true;
            }

            _verticalVel = Vector3.zero;
            if (_hasDestination)
                RecalculatePath();
        }

        void SyncOccupancyRadiusFromController()
        {
            if (_cc == null)
                return;

            occupancyRadius = Mathf.Max(occupancyRadius, _cc.radius);
        }

        void TickCollisionDisable(float dt)
        {
            if (_collisionDisabledTimer <= 0f)
                return;

            _collisionDisabledTimer = Mathf.Max(0f, _collisionDisabledTimer - dt);
            if (_collisionDisabledTimer <= 0f)
                SetCharacterControllerEnabled(true);
        }

        void SetCharacterControllerEnabled(bool enabled)
        {
            if (_cc != null && _cc.enabled != enabled)
                _cc.enabled = enabled;
        }
        
        bool CheckGround(float probeDistance)
        {
            if (_cc == null || !_cc.enabled)
                return false;

            Vector3 controllerCenter = transform.TransformPoint(_cc.center);
            float halfHeight = Mathf.Max(_cc.height * 0.5f, _cc.radius);
            float radius = Mathf.Max(0.01f, _cc.radius * groundProbeRadiusScale);
            float castOffset = Mathf.Max(_cc.skinWidth, 0.02f) + 0.02f;
            Vector3 bottomSphereCenter = controllerCenter + Vector3.down * (halfHeight - _cc.radius);
            Vector3 castOrigin = bottomSphereCenter + Vector3.up * castOffset;
            float castDistance = castOffset + Mathf.Max(0.01f, probeDistance);
            int mask = ResolveGroundMask();

            int hitCount = Physics.SphereCastNonAlloc(
                castOrigin,
                radius,
                Vector3.down,
                _groundHits,
                castDistance,
                mask,
                QueryTriggerInteraction.Ignore);

            if (hitCount <= 0)
                return false;

            float minGroundDot = Mathf.Cos(Mathf.Min(_cc.slopeLimit, 89f) * Mathf.Deg2Rad);
            for (int i = 0; i < hitCount; i++)
            {
                Collider hitCollider = _groundHits[i].collider;
                if (hitCollider == null || IsOwnCollider(hitCollider))
                    continue;

                if (_groundHits[i].normal.y >= minGroundDot)
                    return true;
            }

            return false;
        }

        int ResolveGroundMask()
        {
            if (groundMask.value != 0)
                return groundMask.value;

            int mask = 0;
            int groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer >= 0)
                mask |= 1 << groundLayer;

            int legacyGroundLayer = LayerMask.NameToLayer("Groud");
            if (legacyGroundLayer >= 0)
                mask |= 1 << legacyGroundLayer;

            if (mask == 0)
                mask = Physics.DefaultRaycastLayers;

            groundMask = mask;
            return mask;
        }

        bool IsOwnCollider(Collider target)
        {
            return target.transform == transform || target.transform.IsChildOf(transform);
        }
    }
}
