using UnityEngine;

public class ControlModeManager : MonoBehaviour
{
    public InputIntentSource input;

    [Header("References")]
    public PlayerMotor player;
    public CameraRig cameraRig;
    public Transform fpsPivot;
    public Transform actPivot;

    [Header("RTS")]
    public LayerMask groundMask = ~0; // 默认全选；建议只勾Ground
    public float rtsCamHeight = 18f;
    public float rtsCamDistance = 18f;

    IControlMode _rts;
    IControlMode _act;
    IControlMode _fps;

    IControlMode _current;

    void Awake()
    {
        if (input == null) input = FindObjectOfType<InputIntentSource>();

        var mainCam = cameraRig.GetComponent<Camera>();

        _rts = new RTSMode(player, mainCam, groundMask, rtsCamHeight, rtsCamDistance);
        _act = new ACTMode(player, actPivot);
        _fps = new FPSMode(player, fpsPivot);
    }

    void Start()
    {
        SwitchTo(_rts); // 默认RTS
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) SwitchTo(_rts);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SwitchTo(_act);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SwitchTo(_fps);

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
            cameraRig.SetSmooth(0.22f, 0.14f, 0.18f);
        else
            cameraRig.SetSmooth(0.14f, 0.10f, 0.14f);

        // 立刻给一次目标，避免切换瞬间抖一下
        cameraRig.SetTarget(_current.GetCameraTarget());
    }
}