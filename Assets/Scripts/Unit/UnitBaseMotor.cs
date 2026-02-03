using UnityEngine;

public class UnitBaseMotor : MonoBehaviour
{
    public float walkSpeed = 5.0f;
    public float clickMoveSpeed = 4.5f;
    public float gravity = -18f;
    public float arriveDistance = 0.15f;

    CharacterController _cc;
    Vector3 _verticalVel;

    bool _hasDestination;
    Vector3 _destination;

    void Awake()
    {
        _cc = GetComponent<CharacterController>();
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

    void StepMovement(Vector3 planarVelocity)
    {
        if (_cc.isGrounded && _verticalVel.y < 0f)
            _verticalVel.y = -1f;

        _verticalVel.y += gravity * Time.deltaTime;

        Vector3 move = planarVelocity * Time.deltaTime;
        move += _verticalVel * Time.deltaTime;

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