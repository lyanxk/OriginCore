using Unit.Ability;
using Unit.Command;
using UnityEngine;

public sealed class RtsAbilityCommand : IUnitCommand, ICommandRoutePointProvider
{
    readonly RtsUnitAbility _ability;
    readonly RtsAbilityTargetingMode _targetingMode;
    readonly Selectable _unitTarget;
    readonly Vector3 _worldPoint;

    bool _done;

    public RtsAbilityCommand(RtsUnitAbility ability)
    {
        _ability = ability;
        _targetingMode = RtsAbilityTargetingMode.Self;
        _unitTarget = null;
        _worldPoint = default;
    }

    public RtsAbilityCommand(RtsUnitAbility ability, Vector3 worldPoint)
    {
        _ability = ability;
        _targetingMode = RtsAbilityTargetingMode.Point;
        _unitTarget = null;
        _worldPoint = worldPoint;
    }

    public RtsAbilityCommand(RtsUnitAbility ability, Selectable unitTarget, Vector3 worldPoint)
    {
        _ability = ability;
        _targetingMode = RtsAbilityTargetingMode.Unit;
        _unitTarget = unitTarget;
        _worldPoint = worldPoint;
    }

    public void Begin(UnitContext ctx)
    {
        _done = false;
        TryExecute();
    }

    public void Tick(UnitContext ctx, float dt)
    {
        if (_done)
            return;

        TryExecute();
    }

    public bool IsDone(UnitContext ctx)
    {
        return _done;
    }

    public void End(UnitContext ctx)
    {
    }

    public bool TryGetRoutePoint(UnitContext ctx, out Vector3 worldPoint)
    {
        switch (_targetingMode)
        {
            case RtsAbilityTargetingMode.Unit:
                if (_unitTarget != null)
                {
                    worldPoint = _unitTarget.transform.position;
                    return true;
                }

                worldPoint = _worldPoint;
                return true;

            case RtsAbilityTargetingMode.Point:
                worldPoint = _worldPoint;
                return true;

            default:
                worldPoint = default;
                return false;
        }
    }

    void TryExecute()
    {
        if (_ability == null || !_ability.IsAvailableInCurrentMode)
        {
            _done = true;
            return;
        }

        if (_ability.ShouldWaitForQueuedExecution())
            return;

        switch (_targetingMode)
        {
            case RtsAbilityTargetingMode.Self:
                _ability.TryExecuteQueuedSelf();
                _done = true;
                break;

            case RtsAbilityTargetingMode.Unit:
                if (_unitTarget == null)
                {
                    _done = true;
                    break;
                }

                _ability.TryExecuteQueuedUnit(_unitTarget, _worldPoint);
                _done = true;
                break;

            case RtsAbilityTargetingMode.Point:
                _ability.TryExecuteQueuedPoint(_worldPoint);
                _done = true;
                break;

            default:
                _done = true;
                break;
        }
    }
}
