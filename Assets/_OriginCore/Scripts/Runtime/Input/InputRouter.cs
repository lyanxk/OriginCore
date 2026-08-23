using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OriginCore.Input
{
    [DefaultExecutionOrder(-11780)]
    [DisallowMultipleComponent]
    public sealed class InputRouter : MonoBehaviour
    {
        [SerializeField] private InputActionAsset _actionsAsset;
        [SerializeField] private GameplayInputMap _initialGameplayMap = GameplayInputMap.RTS;
        [SerializeField] private bool _gameplayEnabled = true;

        private InputActionMap _globalMap;
        private InputActionMap _rtsMap;
        private InputActionMap _actMap;
        private InputActionMap _fpsMap;
        private InputActionMap _uiMap;

        private InputAction _switchRts;
        private InputAction _switchAct;
        private InputAction _switchFps;
        private InputAction _pause;

        private InputAction _rtsPoint;
        private InputAction _rtsSelect;
        private InputAction _rtsCommand;
        private InputAction _rtsAttack;
        private InputAction _rtsStop;
        private InputAction _rtsQueueModifier;
        private InputAction _rtsSelectIdleWorkers;
        private InputAction _rtsSelectCombatUnits;
        private InputAction _rtsCameraZoom;
        private InputAction _rtsAbility1;
        private InputAction _rtsAbility2;
        private InputAction _rtsAbility3;
        private InputAction _rtsAbility4;
        private InputAction _rtsPromote;

        private InputAction _actMove;
        private InputAction _actLook;
        private InputAction _actJump;
        private InputAction _actCrouch;
        private InputAction _actLock;
        private InputAction _actPrimary;
        private InputAction _actSecondary;
        private InputAction _actWeaponPrevious;
        private InputAction _actWeaponNext;
        private InputAction _actWeaponWheel;
        private InputAction _actSummonWorker;
        private InputAction _actAbility1;
        private InputAction _actAbility2;
        private InputAction _actAbility3;
        private InputAction _actAbility4;
        private InputAction _actTransform;

        private InputAction _fpsMove;
        private InputAction _fpsLook;
        private InputAction _fpsJump;
        private InputAction _fpsCrouch;
        private InputAction _fpsSprint;
        private InputAction _fpsAim;
        private InputAction _fpsPrimary;
        private InputAction _fpsSlot1;
        private InputAction _fpsSlot2;
        private InputAction _fpsSlot3;
        private InputAction _fpsGrenade;
        private InputAction _fpsWeaponWheel;
        private InputAction _fpsSummonWorker;
        private InputAction _fpsAbility1;
        private InputAction _fpsAbility2;
        private InputAction _fpsAbility3;
        private InputAction _fpsAbility4;
        private InputAction _fpsTransform;
        private InputAction _uiPoint;

        private bool _initialized;
        private GameplayInputMap _activeGameplayMap;
        private GameplayInputSuppressionFrame _suppressionFrame;

        public event Action<GameplayInputMap> GameplayMapChanged;
        public event Action<GameplayInputMap, int> GameplayMapRequested;
        public event Action PauseRequested;
        public event Action<InputSnapshot> SnapshotReady;

        public InputActionAsset ActionsAsset => _actionsAsset;
        public GameplayInputMap ActiveGameplayMap => _activeGameplayMap;
        public bool GameplayEnabled => _gameplayEnabled;
        public bool IsInitialized => _initialized;
        public Vector2 PointerPosition { get; private set; }
        public InputSnapshot CurrentSnapshot { get; private set; }

        private void OnEnable()
        {
            if (!TryInitialize())
            {
                enabled = false;
                return;
            }

            EnableConfiguredMaps();
        }

        private void OnDisable()
        {
            DisableOwnedMaps();
            _suppressionFrame.Clear();
        }

        private void Update()
        {
            ProcessFrame(Time.frameCount);
        }

        public bool Configure(InputActionAsset actionsAsset, GameplayInputMap initialGameplayMap)
        {
            if (actionsAsset == null)
            {
                return false;
            }

            bool wasActive = isActiveAndEnabled;
            if (wasActive)
            {
                DisableOwnedMaps();
            }

            _actionsAsset = actionsAsset;
            _initialGameplayMap = initialGameplayMap;
            _activeGameplayMap = initialGameplayMap;
            _initialized = false;
            ResetCachedActions();

            if (!TryInitialize())
            {
                return false;
            }

            if (wasActive)
            {
                EnableConfiguredMaps();
            }

            return true;
        }

        public bool RequestGameplayMap(GameplayInputMap gameplayMap)
        {
            return RequestGameplayMap(gameplayMap, Time.frameCount);
        }

        public bool RequestGameplayMap(GameplayInputMap gameplayMap, int frame)
        {
            if (!_initialized && !TryInitialize())
            {
                return false;
            }

            if (_activeGameplayMap == gameplayMap)
            {
                return false;
            }

            InputActionMap previousMap = GetGameplayMap(_activeGameplayMap);
            if (previousMap != null)
            {
                previousMap.Disable();
            }

            _activeGameplayMap = gameplayMap;
            if (_gameplayEnabled && isActiveAndEnabled)
            {
                GetGameplayMap(_activeGameplayMap).Enable();
            }

            _suppressionFrame.Arm(frame);
            CurrentSnapshot = InputSnapshot.Suppressed(frame, _activeGameplayMap);
            GameplayMapChanged?.Invoke(_activeGameplayMap);
            return true;
        }

        public void SetGameplayEnabled(bool gameplayEnabled)
        {
            SetGameplayEnabled(gameplayEnabled, Time.frameCount);
        }

        public void SetGameplayEnabled(bool gameplayEnabled, int frame)
        {
            if (_gameplayEnabled == gameplayEnabled)
            {
                return;
            }

            _gameplayEnabled = gameplayEnabled;
            if (!_initialized && !TryInitialize())
            {
                return;
            }

            InputActionMap activeMap = GetGameplayMap(_activeGameplayMap);
            if (gameplayEnabled && isActiveAndEnabled)
            {
                activeMap.Enable();
                _suppressionFrame.Arm(frame);
            }
            else
            {
                activeMap.Disable();
                CurrentSnapshot = InputSnapshot.Suppressed(frame, _activeGameplayMap);
            }
        }

        public InputActionMap GetGameplayMap(GameplayInputMap gameplayMap)
        {
            switch (gameplayMap)
            {
                case GameplayInputMap.RTS:
                    return _rtsMap;
                case GameplayInputMap.ACT:
                    return _actMap;
                case GameplayInputMap.FPS:
                    return _fpsMap;
                default:
                    throw new ArgumentOutOfRangeException(nameof(gameplayMap), gameplayMap, null);
            }
        }

        private void ProcessFrame(int frame)
        {
            if (!_initialized)
            {
                return;
            }

            PointerPosition = _uiPoint != null
                ? _uiPoint.ReadValue<Vector2>()
                : Vector2.zero;

            // Global is evaluated before Gameplay, but mode switching is still gameplay.
            // Pause/menu flows keep the Pause action alive while suppressing F-key mode changes.
            if (_gameplayEnabled)
            {
                if (_switchRts.WasPressedThisFrame())
                {
                    GameplayMapRequested?.Invoke(GameplayInputMap.RTS, frame);
                }
                else if (_switchAct.WasPressedThisFrame())
                {
                    GameplayMapRequested?.Invoke(GameplayInputMap.ACT, frame);
                }
                else if (_switchFps.WasPressedThisFrame())
                {
                    GameplayMapRequested?.Invoke(GameplayInputMap.FPS, frame);
                }
            }

            if (_pause.WasPressedThisFrame())
            {
                PauseRequested?.Invoke();
            }

            if (!_gameplayEnabled || _suppressionFrame.IsSuppressed(frame))
            {
                CurrentSnapshot = InputSnapshot.Suppressed(frame, _activeGameplayMap);
            }
            else
            {
                CurrentSnapshot = ReadGameplaySnapshot(frame);
            }

            SnapshotReady?.Invoke(CurrentSnapshot);
            _suppressionFrame.ClearIfExpired(frame);
        }

        private bool TryInitialize()
        {
            if (_initialized)
            {
                return true;
            }

            if (_actionsAsset == null)
            {
                Debug.LogError("[OriginCore Input] InputRouter has no IA_OriginCore asset.", this);
                return false;
            }

            try
            {
                _globalMap = RequireMap("Global");
                _rtsMap = RequireMap("RTS");
                _actMap = RequireMap("ACT");
                _fpsMap = RequireMap("FPS");
                _uiMap = RequireMap("UI");

                _switchRts = RequireAction(_globalMap, "SwitchRTS");
                _switchAct = RequireAction(_globalMap, "SwitchACT");
                _switchFps = RequireAction(_globalMap, "SwitchFPS");
                _pause = RequireAction(_globalMap, "Pause");

                _rtsPoint = RequireAction(_rtsMap, "Point");
                _rtsSelect = RequireAction(_rtsMap, "Select");
                _rtsCommand = RequireAction(_rtsMap, "Command");
                _rtsAttack = RequireAction(_rtsMap, "Attack");
                _rtsStop = RequireAction(_rtsMap, "Stop");
                _rtsQueueModifier = RequireAction(_rtsMap, "QueueModifier");
                _rtsSelectIdleWorkers = RequireAction(_rtsMap, "SelectIdleWorkers");
                _rtsSelectCombatUnits = RequireAction(_rtsMap, "SelectCombatUnits");
                _rtsCameraZoom = RequireAction(_rtsMap, "CameraZoom");
                _rtsAbility1 = FindOptionalAction(_rtsMap, "Ability1");
                _rtsAbility2 = FindOptionalAction(_rtsMap, "Ability2");
                _rtsAbility3 = FindOptionalAction(_rtsMap, "Ability3");
                _rtsAbility4 = FindOptionalAction(_rtsMap, "Ability4");
                _rtsPromote = FindOptionalAction(_rtsMap, "Promote");

                _actMove = RequireAction(_actMap, "Move");
                _actLook = RequireAction(_actMap, "Look");
                _actJump = RequireAction(_actMap, "Jump");
                _actCrouch = RequireAction(_actMap, "Crouch");
                _actLock = RequireAction(_actMap, "Lock");
                _actPrimary = RequireAction(_actMap, "Primary");
                _actSecondary = FindOptionalAction(_actMap, "Secondary");
                _actWeaponPrevious = RequireAction(_actMap, "WeaponPrevious");
                _actWeaponNext = RequireAction(_actMap, "WeaponNext");
                _actWeaponWheel = RequireAction(_actMap, "WeaponWheel");
                _actSummonWorker = RequireAction(_actMap, "SummonWorker");
                _actAbility1 = FindOptionalAction(_actMap, "Ability1");
                _actAbility2 = FindOptionalAction(_actMap, "Ability2");
                _actAbility3 = FindOptionalAction(_actMap, "Ability3");
                _actAbility4 = FindOptionalAction(_actMap, "Ability4");
                _actTransform = FindOptionalAction(_actMap, "Transform");

                _fpsMove = RequireAction(_fpsMap, "Move");
                _fpsLook = RequireAction(_fpsMap, "Look");
                _fpsJump = RequireAction(_fpsMap, "Jump");
                _fpsCrouch = RequireAction(_fpsMap, "Crouch");
                _fpsSprint = RequireAction(_fpsMap, "Sprint");
                _fpsAim = RequireAction(_fpsMap, "Aim");
                _fpsPrimary = RequireAction(_fpsMap, "Primary");
                _fpsSlot1 = RequireAction(_fpsMap, "Slot1");
                _fpsSlot2 = RequireAction(_fpsMap, "Slot2");
                _fpsSlot3 = RequireAction(_fpsMap, "Slot3");
                _fpsGrenade = RequireAction(_fpsMap, "Grenade");
                _fpsWeaponWheel = FindOptionalAction(_fpsMap, "WeaponWheel");
                _fpsSummonWorker = RequireAction(_fpsMap, "SummonWorker");
                _fpsAbility1 = FindOptionalAction(_fpsMap, "Ability1");
                _fpsAbility2 = FindOptionalAction(_fpsMap, "Ability2");
                _fpsAbility3 = FindOptionalAction(_fpsMap, "Ability3");
                _fpsAbility4 = FindOptionalAction(_fpsMap, "Ability4");
                _fpsTransform = FindOptionalAction(_fpsMap, "Transform");
                _uiPoint = RequireAction(_uiMap, "Point");

                _activeGameplayMap = _initialGameplayMap;
                _initialized = true;
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError("[OriginCore Input] IA_OriginCore is invalid: " + exception.Message, this);
                ResetCachedActions();
                return false;
            }
        }

        private void EnableConfiguredMaps()
        {
            _globalMap.Enable();
            _uiMap.Enable();
            _rtsMap.Disable();
            _actMap.Disable();
            _fpsMap.Disable();
            if (_gameplayEnabled)
            {
                GetGameplayMap(_activeGameplayMap).Enable();
            }
        }

        private void DisableOwnedMaps()
        {
            if (!_initialized)
            {
                return;
            }

            _globalMap.Disable();
            _rtsMap.Disable();
            _actMap.Disable();
            _fpsMap.Disable();
            _uiMap.Disable();
        }

        private InputSnapshot ReadGameplaySnapshot(int frame)
        {
            switch (_activeGameplayMap)
            {
                case GameplayInputMap.RTS:
                    return InputSnapshot.FromRts(frame, new RtsInputSnapshot(
                        _rtsPoint.ReadValue<Vector2>(),
                        ReadButton(_rtsSelect),
                        ReadButton(_rtsCommand),
                        ReadButton(_rtsAttack),
                        ReadButton(_rtsStop),
                        ReadButton(_rtsQueueModifier),
                        ReadButton(_rtsSelectIdleWorkers),
                        ReadButton(_rtsSelectCombatUnits),
                        _rtsCameraZoom.ReadValue<float>(),
                        ReadButton(_rtsAbility1),
                        ReadButton(_rtsAbility2),
                        ReadButton(_rtsAbility3),
                        ReadButton(_rtsAbility4),
                        ReadButton(_rtsPromote)));
                case GameplayInputMap.ACT:
                    return InputSnapshot.FromAct(frame, new ActInputSnapshot(
                        _actMove.ReadValue<Vector2>(),
                        _actLook.ReadValue<Vector2>(),
                        ReadButton(_actJump),
                        ReadButton(_actCrouch),
                        ReadButton(_actLock),
                        ReadButton(_actPrimary),
                        ReadButton(_actSecondary),
                        ReadButton(_actWeaponPrevious),
                        ReadButton(_actWeaponNext),
                        ReadButton(_actWeaponWheel),
                        ReadButton(_actSummonWorker),
                        ReadButton(_actAbility1),
                        ReadButton(_actAbility2),
                        ReadButton(_actAbility3),
                        ReadButton(_actAbility4),
                        ReadButton(_actTransform)));
                case GameplayInputMap.FPS:
                    return InputSnapshot.FromFps(frame, new FpsInputSnapshot(
                        _fpsMove.ReadValue<Vector2>(),
                        _fpsLook.ReadValue<Vector2>(),
                        ReadButton(_fpsJump),
                        ReadButton(_fpsCrouch),
                        ReadButton(_fpsSprint),
                        ReadButton(_fpsAim),
                        ReadButton(_fpsPrimary),
                        ReadButton(_fpsSlot1),
                        ReadButton(_fpsSlot2),
                        ReadButton(_fpsSlot3),
                        ReadButton(_fpsGrenade),
                        ReadButton(_fpsWeaponWheel),
                        ReadButton(_fpsSummonWorker),
                        ReadButton(_fpsAbility1),
                        ReadButton(_fpsAbility2),
                        ReadButton(_fpsAbility3),
                        ReadButton(_fpsAbility4),
                        ReadButton(_fpsTransform)));
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private InputActionMap RequireMap(string mapName)
        {
            InputActionMap map = _actionsAsset.FindActionMap(mapName, false);
            if (map == null)
            {
                throw new InvalidOperationException("Missing Action Map '" + mapName + "'.");
            }

            return map;
        }

        private static InputAction RequireAction(InputActionMap map, string actionName)
        {
            InputAction action = map.FindAction(actionName, false);
            if (action == null)
            {
                throw new InvalidOperationException(
                    "Missing action '" + map.name + "/" + actionName + "'.");
            }

            return action;
        }

        private static InputAction FindOptionalAction(InputActionMap map, string actionName)
        {
            return map != null ? map.FindAction(actionName, false) : null;
        }

        private static InputButtonState ReadButton(InputAction action)
        {
            if (action == null)
            {
                return default(InputButtonState);
            }

            return new InputButtonState(
                action.WasPressedThisFrame(),
                action.IsPressed(),
                action.WasReleasedThisFrame());
        }

        private void ResetCachedActions()
        {
            _initialized = false;
            _globalMap = null;
            _rtsMap = null;
            _actMap = null;
            _fpsMap = null;
            _uiMap = null;
            PointerPosition = Vector2.zero;
        }
    }
}
