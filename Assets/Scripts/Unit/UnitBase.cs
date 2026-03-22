using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceNamespace: "", sourceAssembly: "", sourceClassName: "UnitBaseMotor")]
public class UnitBase : MonoBehaviour
{
    public enum PerspectiveOption
    {
        None = 0,
        ThirdPersonOnly = 1,
        FirstPersonOnly = 2,
        FirstAndThirdPerson = 3
    }

    [Header("Perspective")]
    [SerializeField] PerspectiveOption perspectiveOption = PerspectiveOption.FirstAndThirdPerson;
    [SerializeField] Transform thirdPersonPivot;
    [SerializeField] Transform firstPersonPivot;

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
    public float repathOnMoveTarget = 0.6f;

    NavMeshPath _path;
    Vector3[] _corners = System.Array.Empty<Vector3>();
    int _cornerIndex;
    float _repathTimer;
    Vector3 _lastRepathDest;

    bool _hasPlanarOverride;
    Vector3 _planarOverrideVel;
    float _planarOverrideTimer;

    readonly Dictionary<object, float> _moveSpeedMultipliers = new Dictionary<object, float>(4);
    float _cachedMoveSpeedMultiplier = 1f;

    public AbilityInputRouter AbilityRouter { get; private set; }
    public UnitCombat Combat { get; private set; }
    public PerspectiveOption ViewOption => perspectiveOption;
    public bool HasThirdPersonView =>
        perspectiveOption == PerspectiveOption.ThirdPersonOnly ||
        perspectiveOption == PerspectiveOption.FirstAndThirdPerson;
    public bool HasFirstPersonView =>
        perspectiveOption == PerspectiveOption.FirstPersonOnly ||
        perspectiveOption == PerspectiveOption.FirstAndThirdPerson;
    public Transform ThirdPersonPivot => thirdPersonPivot;
    public Transform FirstPersonPivot => firstPersonPivot;
    public bool HasDestination => _hasDestination;
    public Vector3 CurrentDestination => _destination;

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
        if (NavMesh.SamplePosition(worldPos, out NavMeshHit hit, sampleRadius, NavMesh.AllAreas))
            _destination = hit.position;
        else
            _destination = worldPos;

        _hasDestination = true;
        RecalculatePath(force: true);
        _repathTimer = repathInterval;
    }

    public void MoveImmediate(Vector3 worldDir, float speed)
    {
        Vector3 dir = worldDir;
        if (dir.sqrMagnitude > 1e-6f)
            dir.Normalize();

        StepMovement(dir * speed * _cachedMoveSpeedMultiplier);
    }

    void Update()
    {
        if (_hasDestination)
        {
            if (IsArrivedToDestination())
            {
                CancelPathing();
                StepMovement(Vector3.zero);
                return;
            }

            _repathTimer -= Time.deltaTime;
            if (_repathTimer <= 0f)
            {
                bool destMoved =
                    (_destination - _lastRepathDest).sqrMagnitude >= repathOnMoveTarget * repathOnMoveTarget;
                RecalculatePath(force: destMoved);
                _repathTimer = repathInterval;
            }

            Vector3 dir = GetPathMoveDir();
            StepMovement(dir * clickMoveSpeed * _cachedMoveSpeedMultiplier);
        }
        else
        {
            StepMovement(Vector3.zero);
        }
    }

    public void Jump()
    {
        if (_cc == null || !_cc.isGrounded)
            return;

        _verticalVel.y = Mathf.Sqrt(2f * jumpHeight * -gravity);
    }

    void StepMovement(Vector3 planarVelocity)
    {
        float dt = Time.deltaTime;

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

    void RecalculatePath(bool force)
    {
        if (!_hasDestination)
            return;

        if (!force && (_destination - _lastRepathDest).sqrMagnitude < 0.0001f)
            return;

        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit startHit, 1.0f, NavMesh.AllAreas))
            return;

        bool ok = NavMesh.CalculatePath(startHit.position, _destination, NavMesh.AllAreas, _path);
        _lastRepathDest = _destination;

        if (!ok || _path.status != NavMeshPathStatus.PathComplete || _path.corners == null || _path.corners.Length == 0)
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
        return dir.sqrMagnitude < 1e-6f ? Vector3.zero : dir.normalized;
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
}
