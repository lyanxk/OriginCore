using UnityEngine;

public struct CombatSkillRequest
{
    public Vector3 Direction;
    public Transform Target;
    public Vector3 Origin;

    public CombatSkillRequest(Vector3 direction, Transform target, Vector3 origin)
    {
        Direction = direction;
        Target = target;
        Origin = origin;
    }
}
