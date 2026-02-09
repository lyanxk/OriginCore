using UnityEngine;

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
    
// 平面速度覆盖（planar velocity override）：用于 dash / knockback 等“能力注入”
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
    }

    public void ClearDestination() => _hasDestination = false;

    public void SetDestination(Vector3 worldPos)
    {
        _destination = worldPos;
        _hasDestination = true;
    }

    public void MoveImmediate(Vector3 worldDir, float speed)
    {
        // 玩家通过WASD移动时清除RTS模式下的寻路目标
        _hasDestination = false;

        var dir = worldDir;
        if (dir.sqrMagnitude > 1e-6f) dir.Normalize();

        StepMovement(dir * speed);
    }

    void Update()
    {
        if (_hasDestination)
        {
            Vector3 to = _destination - transform.position;
            to.y = 0f;

            if (to.magnitude <= arriveDistance)
            {
                _hasDestination = false;
                StepMovement(Vector3.zero);
                return;
            }

            Vector3 dir = to.normalized;
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



    public void SetYaw(float yawDegrees)
    {
        var e = transform.eulerAngles;
        e.y = yawDegrees;
        transform.eulerAngles = e;
    }

    public float GetYaw() => transform.eulerAngles.y;
}