using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

public class ControlModeManager : MonoBehaviour
{
    public InputIntentSource input;

    [SerializeField] RectTransform selectionBox; // 选择框

    [Header("Mode Switch Actions 模式切换按键")] public InputActionReference switchRTSAction; // 1
    public InputActionReference switchACTAction; // 2
    public InputActionReference switchFPSAction; // 3

    [FormerlySerializedAs("player")] [Header("References")]
    public UnitBaseMotor unit;

    public CameraRig cameraRig;
    public Transform fpsPivot;
    public Transform actPivot;

    [Header("RTS")] public LayerMask groundMask = ~0; // 默认全选；建议只勾Ground（地面）
    public float rtsCamHeight = 24f;
    public float rtsCamDistance = 24f;

    IControlMode _rts;
    IControlMode _act;
    IControlMode _fps;
    IControlMode _current;
    
    InputAction _switchRTS;
    InputAction _switchACT;
    InputAction _switchFPS;

    void OnEnable()
    {
        switchRTSAction?.action?.Enable();
        switchACTAction?.action?.Enable();
        switchFPSAction?.action?.Enable();
    }

    void OnDisable()
    {
        switchRTSAction?.action?.Disable();
        switchACTAction?.action?.Disable();
        switchFPSAction?.action?.Disable();
    }

    void Awake()
    {
        if (input == null) input = FindObjectOfType<InputIntentSource>();

        var mainCam = cameraRig.GetComponent<Camera>();

        _rts = new RTSMode(unit, mainCam, groundMask, selectionBox, rtsCamHeight, rtsCamDistance);
        _act = new ACTMode(unit, actPivot);
        _fps = new FPSMode(unit, fpsPivot);
        
        _switchRTS = switchRTSAction?.action;
        _switchACT = switchACTAction?.action;
        _switchFPS = switchFPSAction?.action;
    }

    void Start()
    {
        SwitchTo(_rts); // 默认操作模式
    }

    void Update()
    {
        if (_switchRTS != null && switchRTSAction.action.WasPressedThisFrame()) SwitchTo(_rts);
        if (_switchACT != null && switchACTAction.action.WasPressedThisFrame()) SwitchTo(_act);
        if (_switchFPS != null && switchFPSAction.action.WasPressedThisFrame()) SwitchTo(_fps);

        if (_current == null || input == null) return;

        _current.Tick(Time.deltaTime, input.Current);

        // 相机目标交给Rig平滑处理
        cameraRig.SetTarget(_current.GetCameraTarget());
    }

    void SwitchTo(IControlMode mode)
    {
        if (mode == null) return;
        if (_current == mode) return;

        _current?.Exit();
        _current = mode;
        _current.Enter();

        // 根据模式调整过渡手感（你可以随便调）
        if (_current.Name == "RTS")
        {
            cameraRig.SetSmooth(0.22f, 0.14f, 0.18f);
        }
        else if (_current.Name == "ACT")
        {
            cameraRig.SetSmooth(0.14f, 0f, 0.14f);
        }
        else if (_current.Name == "FPS")
        {
            cameraRig.SetSmooth(0f, 0f, 0f);
        }
        else
        {
            cameraRig.SetSmooth(0f, 0f, 0f);
        }

        cameraRig.SetContinuousPositionSmooth(_current.Name == "ACT");

        // 立刻给一次目标，避免切换瞬间抖一下
        cameraRig.SetTarget(_current.GetCameraTarget());
    }
}
