using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceNamespace: "", sourceAssembly: "", sourceClassName: "UnitBaseMotor")]
public class UnitBase : MonoBehaviour
{
    public float walkSpeed = 5.0f;
    public float clickMoveSpeed = 4.5f;
    public float gravity = -12f;
    public float arriveDistance = 0.15f;
    public float jumpHeight = 1.4f;
    public float groundedStick = -2f;
    public float terminalVel = -30f;

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
    [Min(0.1f)] public float navMeshHardSnapDistance = 1.25f;
    [Min(0f)] public float navMeshHardSnapCooldown = 0.25f;

    NavMeshPath _path;
    Vector3[] _corners = System.Array.Empty<Vector3>();
    int _cornerIndex;
    float _repathTimer;
    float _illegalRecoverTimer;
    float _navMeshHardSnapTimer;

    bool _hasPlanarOverride;
    Vector3 _planarOverrideVel;
    float _planarOverrideTimer;

    readonly Dictionary<object, float> _moveSpeedMultipliers = new Dictionary<object, float>(4);
    float _cachedMoveSpeedMultiplier = 1f;

    public AbilityInputRouter AbilityRouter { get; private set; }
    public UnitCombat Combat { get; private set; }
    public bool HasDestination => _hasDestination;
    public Vector3 CurrentDestination => _destination;
    public float OccupancyRadius => Mathf.Max(0.1f, occupancyRadius);

    public void OverridePlanarVelocity(Vector3 planarVel, float duration)
    {
        if (duration <= 0f)
            return;

        _hasPlanarOverride = true;
        _planarOverrideVel = planarVel;
        _planarOverrideTimer = Mathf.Max(_planarOverrideTimer, duration);
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
        CancelPathing();
        _hasPlanarOverride = false;
        _planarOverrideVel = Vector3.zero;
        _planarOverrideTimer = 0f;
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

        ApplyMovement(GetPlannedVelocity(dt), dt);
    }

    public void Jump()
    {
        if (_cc == null || !_cc.isGrounded)
            return;

        _verticalVel.y = Mathf.Sqrt(2f * jumpHeight * -gravity);
    }

    void ApplyMovement(Vector3 plannedVelocity, float dt)
    {
        Vector3 localCorrection = BuildLocalCorrectionVelocity(dt, plannedVelocity);
        StepMovement(plannedVelocity + localCorrection, dt);
        TryRecoverToLegalPosition();
        TrySnapBackToNavMesh();
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

    void StepMovement(Vector3 planarVelocity, float dt)
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

        FaceMovementDirection(planarVelocity);

        if (_cc == null)
            return;

        if (_cc.isGrounded)
        {
            if (_verticalVel.y < 0f)
                _verticalVel.y = groundedStick;
        }
        else
        {
            _verticalVel.y += gravity * dt;
            if (_verticalVel.y < terminalVel)
                _verticalVel.y = terminalVel;
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

    void TrySnapBackToNavMesh()
    {
        if (_navMeshHardSnapTimer > 0f)
            return;

        if (NavMeshRoadNetwork.TrySample(transform.position, navMeshCheckRadius, out _, NavMesh.AllAreas))
            return;

        if (!NavMeshRoadNetwork.TrySample(transform.position, navMeshRecoverSampleRadius, out Vector3 nearestNav, NavMesh.AllAreas))
            return;

        Vector3 delta = nearestNav - transform.position;
        delta.y = 0f;
        if (delta.sqrMagnitude < navMeshHardSnapDistance * navMeshHardSnapDistance)
            return;

        RepositionWithoutPathReset(nearestNav);
        _navMeshHardSnapTimer = navMeshHardSnapCooldown;
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
}
