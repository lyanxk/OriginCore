using System.Collections.Generic;
using Navigation;
using Unit.Ability;
using Unit.Combat;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

namespace Unit.Movement
{
    [RequireComponent(typeof(CharacterController))]
    public class UnitBase : MonoBehaviour
    {
        public enum MoveStance
        {
            Walk,
            Run,
            Crouch,
            Slide,
            Flight,
            FlightBoost
        }

        [Header("Ground Movement")]
        [FormerlySerializedAs("walkSpeed")]
        public float walkMaxSpeed = 5.0f;
        [FormerlySerializedAs("runSpeed")]
        public float runMaxSpeed = 7.5f;
        [FormerlySerializedAs("crouchMoveSpeed")]
        public float crouchMaxSpeed = 2.5f;
        [FormerlySerializedAs("clickMoveSpeed")]
        public float clickMoveMaxSpeed = 4.5f;
        [Min(0f)] public float groundAcceleration = 22f;
        [Min(0f)] public float groundDeceleration = 18f;
        [Min(0f)] public float overspeedDeceleration = 34f;

        [Header("Slide Movement")]
        [Min(0f)] public float slideMaxSpeed = 10.25f;
        [Min(0f)] public float slideAcceleration = 12f;
        [Min(0f)] public float slideMinimumStartSpeed = 5.5f;
        [Min(0f)] public float slideMinimumSpeedBoost = 1f;
        [Min(0f)] public float slideMaximumSpeedBoost = 2.75f;
        [Min(0.01f)] public float slideDuration = 0.45f;
        [Min(0f)] public float slideCooldown = 0.15f;
        [Min(0f)] public float slideInputDeadZone = 0.1f;

        [Header("Flight Movement")]
        [Min(0f)] public float flightMoveSpeed = 7f;
        [Min(0f)] public float flightBoostMoveSpeed = 10.75f;

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
        Vector3 _directPlanarVelocity;
        Vector3 _lastPlanarVelocity;
        int _directMoveFrame = -1;
        bool _flightEnabled;
        float _flightVerticalInput;
        float _flightVerticalSpeed;
        bool _isCrouching;
        bool _isRunning;
        bool _isSliding;
        float _slideTimer;
        float _nextSlideTime;
        Vector3 _slideDirection;
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
        public bool IsCrouching => _isCrouching || _isSliding;
        public bool IsRunning => _isRunning && !_isSliding;
        public bool IsSliding => _isSliding;
        public Vector3 PlanarVelocity => _lastPlanarVelocity;
        public float PlanarSpeed => _lastPlanarVelocity.magnitude;

        void OnValidate()
        {
            NormalizeMovementSettings();
        }

        void NormalizeMovementSettings()
        {
            walkMaxSpeed = Mathf.Max(0f, walkMaxSpeed);
            runMaxSpeed = Mathf.Max(walkMaxSpeed, runMaxSpeed);
            crouchMaxSpeed = Mathf.Clamp(crouchMaxSpeed, 0f, walkMaxSpeed);
            clickMoveMaxSpeed = Mathf.Max(0f, clickMoveMaxSpeed);
            groundAcceleration = Mathf.Max(0f, groundAcceleration);
            groundDeceleration = Mathf.Max(0f, groundDeceleration);
            overspeedDeceleration = Mathf.Max(groundAcceleration, overspeedDeceleration);
            slideMaxSpeed = Mathf.Max(Mathf.Max(runMaxSpeed, walkMaxSpeed + 0.1f), slideMaxSpeed);
            slideAcceleration = Mathf.Max(0f, slideAcceleration);
            slideMinimumStartSpeed = Mathf.Clamp(slideMinimumStartSpeed, walkMaxSpeed + 0.1f, slideMaxSpeed);
            slideMinimumSpeedBoost = Mathf.Max(0f, slideMinimumSpeedBoost);
            slideMaximumSpeedBoost = Mathf.Max(slideMinimumSpeedBoost, slideMaximumSpeedBoost);
            slideDuration = Mathf.Max(0.01f, slideDuration);
            slideCooldown = Mathf.Max(0f, slideCooldown);
            slideInputDeadZone = Mathf.Max(0f, slideInputDeadZone);
            flightMoveSpeed = Mathf.Max(0f, flightMoveSpeed);
            flightBoostMoveSpeed = Mathf.Max(flightMoveSpeed, runMaxSpeed + slideMaximumSpeedBoost + 0.1f);
            jumpGroundProbeDistance = Mathf.Max(0.01f, jumpGroundProbeDistance);
            groundProbeRadiusScale = Mathf.Clamp(groundProbeRadiusScale, 0.1f, 1f);
        }

        public void OverridePlanarVelocity(Vector3 planarVel, float duration)
        {
            if (duration <= 0f)
                return;

            CancelSlide();
            _hasPlanarOverride = true;
            _planarOverrideVel = planarVel;
            _directPlanarVelocity = planarVel;
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
            _directPlanarVelocity = _lastPlanarVelocity;
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
            _directPlanarVelocity = Vector3.zero;
            _lastPlanarVelocity = Vector3.zero;
            _verticalVel = Vector3.zero;
            CancelSlide();

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
                CancelSlide();
                _directPlanarVelocity = Vector3.zero;
            }
            _flightVerticalInput = 0f;
            _flightVerticalSpeed = 0f;
            _verticalVel = Vector3.zero;
        }

        public void SetCrouching(bool crouching)
        {
            if (_flightEnabled)
            {
                _isCrouching = false;
                return;
            }

            if (_isSliding && !crouching)
                return;

            _isCrouching = crouching;
            if (_isCrouching)
                _isRunning = false;
        }

        public void SetRunning(bool running)
        {
            _isRunning = running && !_isCrouching && !_isSliding && !_flightEnabled;
        }

        public void SetFlightVerticalInput(float input, bool boosted)
        {
            if (!_flightEnabled)
            {
                _flightVerticalInput = 0f;
                _flightVerticalSpeed = 0f;
                return;
            }

            _flightVerticalInput = Mathf.Clamp(input, -1f, 1f);
            _flightVerticalSpeed = ResolveFlightMoveSpeed(boosted);
        }

        void Awake()
        {
            NormalizeMovementSettings();
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
            CancelSlide();

            if (!NavMeshRoadNetwork.TryResolveDestination(worldPos, sampleRadius, out _destination))
                _destination = worldPos;

            _hasDestination = true;
            RecalculatePath();
            _repathTimer = repathInterval;
        }

        public void MoveImmediate(Vector3 worldDir, MoveStance stance)
        {
            float dt = Time.deltaTime;
            _directMoveFrame = Time.frameCount;
            ApplyMovement(BuildDirectPlanarVelocity(worldDir, stance, dt), dt);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _illegalRecoverTimer = Mathf.Max(0f, _illegalRecoverTimer - dt);
            _navMeshHardSnapTimer = Mathf.Max(0f, _navMeshHardSnapTimer - dt);
            _fallSpeedReductionTimer = Mathf.Max(0f, _fallSpeedReductionTimer - dt);
            TickSlide(dt);
            TickCollisionDisable(dt);

            if (_directMoveFrame != Time.frameCount)
                ApplyMovement(GetPlannedVelocity(dt), dt);
        }

        public void Jump()
        {
            if (_flightEnabled)
                return;

            if (_isSliding)
                return;

            if (!CheckGround(jumpGroundProbeDistance))
                return;

            _verticalVel.y = Mathf.Sqrt(2f * jumpHeight * -gravity);
        }

        public bool TryStartSlide(Vector3 worldDir)
        {
            if (_flightEnabled || _isSliding || !_isRunning || Time.time < _nextSlideTime)
                return false;

            Vector3 direction = worldDir;
            direction.y = 0f;
            if (direction.sqrMagnitude <= slideInputDeadZone * slideInputDeadZone)
                return false;

            float currentSpeed = Mathf.Max(_directPlanarVelocity.magnitude, PlanarSpeed);
            if (currentSpeed < slideMinimumStartSpeed)
                return false;

            direction.Normalize();
            float speed01 = Mathf.InverseLerp(slideMinimumStartSpeed, Mathf.Max(slideMinimumStartSpeed, runMaxSpeed), currentSpeed);
            float speedBoost = Mathf.Lerp(slideMinimumSpeedBoost, slideMaximumSpeedBoost, speed01);
            float startSpeed = Mathf.Min(slideMaxSpeed, currentSpeed + speedBoost);

            _slideDirection = direction;
            _slideTimer = slideDuration;
            _nextSlideTime = Time.time + slideDuration + slideCooldown;
            _isSliding = true;
            _isCrouching = true;
            _isRunning = false;
            _directPlanarVelocity = direction * startSpeed;
            CancelPathing();
            return true;
        }

        Vector3 BuildDirectPlanarVelocity(Vector3 worldDir, MoveStance stance, float dt)
        {
            Vector3 direction = worldDir;
            direction.y = 0f;

            if (direction.sqrMagnitude > 1e-6f)
                direction.Normalize();
            else
                direction = Vector3.zero;

            if (_flightEnabled)
            {
                bool boosted = stance == MoveStance.FlightBoost;
                _directPlanarVelocity = direction * ResolveFlightMoveSpeed(boosted);
                return _directPlanarVelocity;
            }

            MoveStance activeStance = _isSliding ? MoveStance.Slide : stance;
            if (activeStance == MoveStance.Slide)
                direction = _slideDirection.sqrMagnitude > 1e-6f ? _slideDirection : direction;

            float maxSpeed = ResolveGroundMaxSpeed(activeStance);
            float acceleration = ResolveGroundAcceleration(activeStance);
            return AcceleratePlanarVelocity(direction, maxSpeed, acceleration, dt);
        }

        Vector3 AcceleratePlanarVelocity(Vector3 direction, float maxSpeed, float acceleration, float dt)
        {
            if (dt <= 0f)
                return _directPlanarVelocity;

            if (direction.sqrMagnitude <= 1e-6f)
            {
                float braking = _directPlanarVelocity.magnitude > maxSpeed + 0.01f
                    ? overspeedDeceleration
                    : groundDeceleration;
                _directPlanarVelocity = Vector3.MoveTowards(
                    _directPlanarVelocity,
                    Vector3.zero,
                    braking * dt);
                return _directPlanarVelocity;
            }

            Vector3 targetVelocity = direction * maxSpeed;
            float currentSpeed = _directPlanarVelocity.magnitude;
            float activeAcceleration = currentSpeed > maxSpeed + 0.01f
                ? overspeedDeceleration
                : acceleration;

            _directPlanarVelocity = Vector3.MoveTowards(
                _directPlanarVelocity,
                targetVelocity,
                activeAcceleration * dt);
            return _directPlanarVelocity;
        }

        float ResolveGroundMaxSpeed(MoveStance stance)
        {
            switch (stance)
            {
                case MoveStance.Run:
                    return runMaxSpeed * _cachedMoveSpeedMultiplier;
                case MoveStance.Crouch:
                    return crouchMaxSpeed * _cachedMoveSpeedMultiplier;
                case MoveStance.Slide:
                    return slideMaxSpeed;
                default:
                    return walkMaxSpeed * _cachedMoveSpeedMultiplier;
            }
        }

        float ResolveGroundAcceleration(MoveStance stance)
        {
            return stance == MoveStance.Slide ? slideAcceleration : groundAcceleration;
        }

        float ResolveFlightMoveSpeed(bool boosted)
        {
            return boosted ? flightBoostMoveSpeed : flightMoveSpeed;
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

            return GetPathMoveDir() * clickMoveMaxSpeed * _cachedMoveSpeedMultiplier;
        }

        void StepMovement(Vector3 planarVelocity, Vector3 facingVelocity, float dt)
        {
            if (_hasPlanarOverride)
            {
                _planarOverrideTimer -= dt;
                planarVelocity = _planarOverrideVel;
                facingVelocity = _planarOverrideVel;
                _directPlanarVelocity = _planarOverrideVel;

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
                    delta.normalized * Mathf.Max(Mathf.Max(0f, localCorrectionSpeed), clickMoveMaxSpeed),
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

        void TickSlide(float dt)
        {
            if (!_isSliding)
                return;

            if (_flightEnabled)
            {
                CancelSlide();
                return;
            }

            _slideTimer -= dt;
            if (_slideTimer > 0f)
                return;

            CancelSlide();
        }

        void CancelSlide()
        {
            _isSliding = false;
            _slideTimer = 0f;
            _slideDirection = Vector3.zero;
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
