using UnityEngine;
using UnityEngine.AI;

public class UnitBaseMotor : MonoBehaviour
{
    public float walkSpeed = 5.0f;
    public float clickMoveSpeed = 4.5f;
    public float gravity = -12f;
    public float arriveDistance = 0.15f;
    public float jumpHeight = 1.4f;      // 跳跃高度
    public float groundedStick = -2f;    // 贴地“吸附”，避免小坡抖动
    public float terminalVel = -30f;     // 最大下落速度

    CharacterController _cc;
    Vector3 _verticalVel;

    bool _hasDestination;
    Vector3 _destination;
    
    [Header("NavMesh Path")]
    public float sampleRadius = 2.0f;        // 点地面后投到 NavMesh 的半径
    public float cornerReachDist = 0.25f;    // 认为到达某个拐点的距离
    public float repathInterval = 0.25f;     // 定时重算（被挡/动态障碍）
    public float repathOnMoveTarget = 0.6f;  // 目标移动超过多少就强制重算（可选）

    NavMeshPath _path;
    Vector3[] _corners = System.Array.Empty<Vector3>();
    int _cornerIndex;
    float _repathTimer;
    Vector3 _lastRepathDest;

    
    // 平面速度覆盖：用于 dash / knockback 等“能力注入”
    bool _hasPlanarOverride;
    Vector3 _planarOverrideVel;
    float _planarOverrideTimer;
    
    public AbilityInputRouter AbilityRouter { get; private set; }

    public void OverridePlanarVelocity(Vector3 planarVel, float duration)
    {
        if (duration <= 0f) return;

        _hasPlanarOverride = true;
        _planarOverrideVel = planarVel;
        _planarOverrideTimer = Mathf.Max(_planarOverrideTimer, duration);
    }

    void Awake()
    {
        _cc = GetComponent<CharacterController>();
        AbilityRouter = GetComponent<AbilityInputRouter>();
        
        _path = new NavMeshPath();
    }
    
    public void ClearDestination()
    {
        _hasDestination = false;

        _corners = System.Array.Empty<Vector3>();
        _cornerIndex = 0;
        _repathTimer = 0f;
    }

    public void SetDestination(Vector3 worldPos)
    {
        // 目标点投到 NavMesh（避免点到不可走区域导致直接失败）
        if (NavMesh.SamplePosition(worldPos, out var hit, sampleRadius, NavMesh.AllAreas))
            _destination = hit.position;
        else
            _destination = worldPos; // 采样失败也先收下（后续算路会失败并停）

        _hasDestination = true;

        RecalculatePath(force: true);
        _repathTimer = repathInterval;
    }

    public void MoveImmediate(Vector3 worldDir, float speed)
    {
        var dir = worldDir;
        if (dir.sqrMagnitude > 1e-6f) dir.Normalize();

        StepMovement(dir * speed);
    }

    void Update()
    {
        if (_hasDestination)
        {
            // 如果 dash/击退正在覆盖，你可以选择“仍然保持 destination 但不推进 cornerIndex”
            // 这里不特判也行，因为 StepMovement 会覆盖 planarVelocity，寻路只是算一个“想走的方向”

            // 到达最终目标判定（水平距离）
            if (IsArrivedToDestination())
            {
                ClearDestination();
                StepMovement(Vector3.zero);
                return;
            }

            // 定时重算路径（动态障碍、推挤）
            _repathTimer -= Time.deltaTime;
            if (_repathTimer <= 0f)
            {
                // 目标移动较大时也可以强制重算（编队偏移/移动目标时更稳）
                bool destMoved = (_destination - _lastRepathDest).sqrMagnitude >= repathOnMoveTarget * repathOnMoveTarget;
                RecalculatePath(force: destMoved);
                _repathTimer = repathInterval;
            }

            // 沿 path.corners 走
            Vector3 dir = GetPathMoveDir();
            StepMovement(dir * clickMoveSpeed);
        }
        else
        {
            // 没有即时输入时，仍然保持重力
            StepMovement(Vector3.zero);
        }
    }
    //跳跃
    public void Jump()
    {
        if (!_cc.isGrounded) return;

        // v = sqrt(2 * h * -g)
        _verticalVel.y = Mathf.Sqrt(2f * jumpHeight * -gravity);
    }
    
    void StepMovement(Vector3 planarVelocity)
    {
        float dt = Time.deltaTime;
        
        // 处理平面速度覆盖
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
        
        if (_cc.isGrounded)
        {
            // 接地时：只给一个小的向下速度保持贴地，但不要继续加重力
            if (_verticalVel.y < 0f)
                _verticalVel.y = groundedStick;
        }
        else
        {
            // 离地时：才受重力影响
            _verticalVel.y += gravity * dt;
            if (_verticalVel.y < terminalVel)
                _verticalVel.y = terminalVel;
        }

        Vector3 move = (planarVelocity + _verticalVel) * dt;
        _cc.Move(move);
    }

    bool IsArrivedToDestination()
    {
        Vector3 a = transform.position; a.y = 0f;
        Vector3 b = _destination;       b.y = 0f;
        return Vector3.Distance(a, b) <= arriveDistance;
    }

    void RecalculatePath(bool force)
    {
        if (!_hasDestination) return;

        // 没有路径 / 拐点走完 / 强制重算
        if (!force && _corners.Length > 0 && _cornerIndex < _corners.Length)
            return;

        // 起点也投到 NavMesh（角色可能在边缘/小台阶上）
        if (!NavMesh.SamplePosition(transform.position, out var startHit, 1.0f, NavMesh.AllAreas))
            return;

        bool ok = NavMesh.CalculatePath(startHit.position, _destination, NavMesh.AllAreas, _path);
        _lastRepathDest = _destination;

        if (!ok || _path.status != NavMeshPathStatus.PathComplete || _path.corners == null || _path.corners.Length == 0)
        {
            // 不可达：停止（你也可以改成走到最近可达点）
            _corners = System.Array.Empty<Vector3>();
            _cornerIndex = 0;
            return;
        }

        _corners = _path.corners;
        _cornerIndex = 0;
    }

    Vector3 GetPathMoveDir()
    {
        if (_corners == null || _corners.Length == 0) return Vector3.zero;

        // 跳过离自己太近的拐点
        while (_cornerIndex < _corners.Length)
        {
            Vector3 c = _corners[_cornerIndex];
            c.y = transform.position.y;

            if (Vector3.Distance(transform.position, c) <= cornerReachDist)
                _cornerIndex++;
            else
                break;
        }

        if (_cornerIndex >= _corners.Length) return Vector3.zero;

        Vector3 target = _corners[_cornerIndex];
        target.y = transform.position.y;

        Vector3 dir = target - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude < 1e-6f) return Vector3.zero;
        return dir.normalized;
    }

    public void SetYaw(float yawDegrees)
    {
        var e = transform.eulerAngles;
        e.y = yawDegrees;
        transform.eulerAngles = e;
    }

    public float GetYaw() => transform.eulerAngles.y;
}