using System.Collections;
using System.Collections.Generic;
using Unit.Ability;
using UnityEngine;

public class DashAbility : MonoBehaviour, IAbilityInput
{
    public float dashSpeed = 10f;     
    public float dashDuration = 0.2f; 

    UnitBaseMotor _motor;
    Transform _tf;

    void Awake()
    {
        _motor = GetComponent<UnitBaseMotor>();
        _tf = transform;
    }

    public void ProcessInput(InputIntent intent)
    {
        if (intent.Dash)
            DashForward();
    }
    
    public void DashForward()
    {
        if (_motor == null) return;

        // 面朝方向（forward），只取水平，避免抬头冲天/低头插地
        Vector3 dir = _tf.forward;
        dir.y = 0f;

        if (dir.sqrMagnitude < 1e-6f) return;
        dir.Normalize();

        _motor.OverridePlanarVelocity(dir * dashSpeed, dashDuration);
    }
}
