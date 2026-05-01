using Camera;
using Input;
using Modes;
using UI.HUD;
using Unit.Combat.Hero;
using Unit.Command;
using Unit.Movement;
using Unit.Selection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace Core
{
    public class ControlModeManager : MonoBehaviour
    {
        public static ControlModeManager Instance { get; private set; }

        public InputIntentSource input;
        public string CurrentModeName => _current != null ? _current.Name : string.Empty;

        [SerializeField] RectTransform selectionBox; // 选择框
        [FormerlySerializedAs("switchRTSAction")]
        [Header("Mode Switch Actions 模式切换按键")] public InputActionReference switchRtsAction; // 1
        [FormerlySerializedAs("switchACTAction")]
        public InputActionReference switchActAction; // 2
        [FormerlySerializedAs("switchFPSAction")]
        public InputActionReference switchFpsAction; // 3

        [FormerlySerializedAs("player")] [Header("References")]
        public UnitBase unit;

        public CameraRig cameraRig;
        public Transform fpsPivot;
        public Transform actPivot;
        [SerializeField] GameObject rtsCommandPanel;

        [Header("RTS")] public LayerMask groundMask = ~0; // 默认全选；建议只勾Ground（地面）
        public float rtsCamHeight = 24f;
        public float rtsCamDistance = 24f;

        IControlMode _rts;
        IControlMode _act;
        IControlMode _fps;
        IControlMode _current;
    
        InputAction _switchRts;
        InputAction _switchAct;
        InputAction _switchFps;
        CommandExecutor _unitCommandExecutor;
        bool _pendingActFpsTakeoverClear;
        UnityEngine.Camera _mainCam;

        void OnEnable()
        {
            switchRtsAction?.action?.Enable();
            switchActAction?.action?.Enable();
            switchFpsAction?.action?.Enable();
        }

        void OnDisable()
        {
            switchRtsAction?.action?.Disable();
            switchActAction?.action?.Disable();
            switchFpsAction?.action?.Disable();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
                Debug.LogWarning("Multiple ControlModeManager instances detected. Using the latest one.", this);

            Instance = this;

            if (input == null) input = FindObjectOfType<InputIntentSource>();

            _mainCam = cameraRig != null ? cameraRig.GetComponent<UnityEngine.Camera>() : null;
            RebuildModes(unit, ResolveHero(unit));
            TryCacheRtsCommandPanel();
        
            _switchRts = switchRtsAction?.action;
            _switchAct = switchActAction?.action;
            _switchFps = switchFpsAction?.action;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void Start()
        {
            SwitchTo(_rts); // 默认操作模式
        }

        void Update()
        {
            if (_switchRts != null && switchRtsAction.action.WasPressedThisFrame()) SwitchTo(_rts);
            if (_switchAct != null && switchActAction.action.WasPressedThisFrame())
            {
                if (!IsInRtsMode() || TryPrepareSwitchFromRts(requireThirdPerson: true, requireFirstPerson: false))
                    SwitchTo(_act);
            }

            if (_switchFps != null && switchFpsAction.action.WasPressedThisFrame())
            {
                if (!IsInRtsMode() || TryPrepareSwitchFromRts(requireThirdPerson: false, requireFirstPerson: true))
                    SwitchTo(_fps);
            }

            if (_current == null || input == null) return;

            bool hasTakeoverInput = HasActFpsTakeoverInput(input.Current);
            if (_pendingActFpsTakeoverClear && hasTakeoverInput)
            {
                _unitCommandExecutor?.InterruptAndClear();
                _pendingActFpsTakeoverClear = false;
            }

            if (_pendingActFpsTakeoverClear)
                _current.Tick(Time.deltaTime, CreateLookOnlyIntent(input.Current));
            else
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
            _pendingActFpsTakeoverClear = (_current == _act || _current == _fps);

            //模式切换平滑
            if (_current.Name == "RTS") {
                cameraRig.SetSmooth(0.22f, 0.14f, 0.18f);
            }
            else if (_current.Name == "ACT") {
                cameraRig.SetSmooth(0.14f, 0f, 0.14f);
            }
            else if (_current.Name == "FPS") {
                cameraRig.SetSmooth(0f, 0f, 0f);
            }
            else {
                cameraRig.SetSmooth(0f, 0f, 0f);
            }

            cameraRig.SetContinuousPositionSmooth(_current.Name == "ACT");

            cameraRig.SetTarget(_current.GetCameraTarget());
            SetRtsCommandPanelActive(_current.Name == "RTS");
        }

        //启用GUI 右下角指令
        void SetRtsCommandPanelActive(bool isRtsMode)
        {
            if (rtsCommandPanel == null)
                TryCacheRtsCommandPanel();

            if (rtsCommandPanel != null && rtsCommandPanel.activeSelf != isRtsMode)
                rtsCommandPanel.SetActive(isRtsMode);
        }
        //缓存GUI对象
        void TryCacheRtsCommandPanel()
        {
            if (rtsCommandPanel != null)
                return;

            CommandCardController commandCard = FindObjectOfType<CommandCardController>();
            if (commandCard != null)
                rtsCommandPanel = commandCard.gameObject;
        }
        //判断目前角色是否处于RTS模式的控制下
        static bool HasActFpsTakeoverInput(InputIntent intent)
        {
            const float deadZone = 0.0001f;

            return intent.Move.sqrMagnitude > deadZone ||
                   intent.LeftClick ||
                   intent.LeftHeld ||
                   intent.RightClick ||
                   intent.RightHeld ||
                   intent.Space ||
                   intent.SpaceHeld ||
                   intent.Dash ||
                   intent.AttackPressed ||
                   intent.Cancel;
        }
        //处理RTS模式切换后 其他输入前的视角移动输入
        static InputIntent CreateLookOnlyIntent(InputIntent source)
        {
            source.Move = Vector2.zero;
            source.LeftClick = false;
            source.LeftHeld = false;
            source.RightClick = false;
            source.RightHeld = false;
            source.Space = false;
            source.SpaceHeld = false;
            source.Dash = false;
            source.AttackPressed = false;
            source.Cancel = false;
            source.CommandQ = false;
            source.CommandW = false;
            source.CommandE = false;
            source.CommandR = false;
            source.CommandA = false;
            source.CommandS = false;
            source.CommandD = false;
            source.CommandF = false;
            source.CommandZ = false;
            source.CommandX = false;
            source.CommandC = false;
            source.CommandV = false;
            source.CommandM = false;
            return source;
        }

        bool IsInRtsMode()
        {
            return _current != null && _current.Name == "RTS";
        }

        bool TryPrepareSwitchFromRts(bool requireThirdPerson, bool requireFirstPerson)
        {
            SelectionManager selection = SelectionManager.Instance;
            if (selection == null || selection.SelectedCount <= 0)
                return false;

            Selectable selected = selection.Primary;
            if (selected == null)
                selected = selection.Selected[selection.SelectedCount - 1];

            if (selected == null)
                return false;

            UnitBase selectedUnit = selected.GetComponent<UnitBase>();
            if (selectedUnit == null)
                selectedUnit = selected.GetComponentInParent<UnitBase>();

            if (selectedUnit == null)
                return false;

            HeroBase selectedHero = ResolveHero(selectedUnit);
            if (selectedHero == null)
                return false;

            if (requireThirdPerson && !selectedHero.HasThirdPersonView)
                return false;

            if (requireFirstPerson && !selectedHero.HasFirstPersonView)
                return false;

            RebuildModes(selectedUnit, selectedHero);
            return true;
        }

        void RebuildModes(UnitBase nextUnit, HeroBase hero)
        {
            if (nextUnit == null || _mainCam == null)
                return;

            unit = nextUnit;
            actPivot = hero != null && hero.ThirdPersonPivot != null ? hero.ThirdPersonPivot : nextUnit.transform;
            fpsPivot = hero != null && hero.FirstPersonPivot != null ? hero.FirstPersonPivot : nextUnit.transform;

            _rts = new RtsMode(unit, _mainCam, groundMask, selectionBox, rtsCamHeight, rtsCamDistance);
            _act = new ActMode(unit, actPivot);
            _fps = new FpsMode(unit, fpsPivot);
            _unitCommandExecutor = unit.GetComponent<CommandExecutor>();
        }

        static HeroBase ResolveHero(UnitBase targetUnit)
        {
            if (targetUnit == null)
                return null;

            HeroBase hero = targetUnit.GetComponent<HeroBase>();
            if (hero != null)
                return hero;

            return targetUnit.GetComponentInParent<HeroBase>();
        }

    }
}
