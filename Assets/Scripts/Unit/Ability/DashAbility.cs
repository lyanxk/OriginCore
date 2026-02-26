using System;
using Unit.Ability;
using UnityEngine;

public class DashAbility : MonoBehaviour, IAbilityInput, IActivatableAbility
{
    [Header("Ability")]
    [SerializeField] string abilityId = "ability.dash";
    [SerializeField] string displayName = "Dash";
    [SerializeField] AbilityAvailableMode availableMode = AbilityAvailableMode.ACTAndFPS;
    [SerializeField] Sprite icon;
    [SerializeField] string hotkeyText = "Mouse5";
    [TextArea]
    [SerializeField] string tooltip = "Dash forward quickly.";
    [Min(0f)]
    [SerializeField] float cooldown = 0f;

    [Header("Movement")]
    public float dashSpeed = 10f;
    public float dashDuration = 0.2f;

    UnitBase _motor;
    Transform _tf;
    float _nextReadyTime;

    public string AbilityId => abilityId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? "Dash" : displayName;
    public AbilityAvailableMode AvailableMode => availableMode;
    public Sprite Icon => icon;
    public string HotkeyText => hotkeyText;
    public string Tooltip => tooltip;
    public bool IsEnabled => Time.time >= _nextReadyTime;

    public float Cooldown01
    {
        get
        {
            if (cooldown <= 0f || IsEnabled)
                return 0f;

            return Mathf.Clamp01((_nextReadyTime - Time.time) / cooldown);
        }
    }

    void Awake()
    {
        _motor = GetComponent<UnitBase>();
        _tf = transform;
    }

    public void ProcessInput(InputIntent intent)
    {
        if (intent.Dash)
            TryActivate();
    }

    public bool TryActivate()
    {
        if (_motor == null)
            return false;

        string currentMode = ControlModeManager.Instance != null ? ControlModeManager.Instance.CurrentModeName : string.Empty;
        if (!availableMode.IsAvailableInMode(currentMode))
            return false;

        if (!IsEnabled)
            return false;

        bool dashed = DashForward();
        if (!dashed)
            return false;

        if (cooldown > 0f)
            _nextReadyTime = Time.time + cooldown;

        return true;
    }

    public bool DashForward()
    {
        if (_motor == null) return false;

        Vector3 dir = _tf.forward;
        dir.y = 0f;

        if (dir.sqrMagnitude < 1e-6f) return false;
        dir.Normalize();

        _motor.OverridePlanarVelocity(dir * dashSpeed, dashDuration);
        return true;
    }
}
