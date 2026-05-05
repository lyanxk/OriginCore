using Camera;
  using Core;
  using Input;
  using Unit.Combat.Hero;
  using Unit.Movement;
  using UnityEngine;

  namespace Modes
  {
      public class FpsMode : IControlMode
      {
          const float AimFallReductionDuration = 0.12f;
          const float AimFallReductionMaxFallSpeed = 2f;
          const float AimFallReductionGravityMultiplier = 0.15f;

          public string Name => "FPS";

          readonly UnitBase _unit;
          readonly Transform _fpsPivot;
          readonly HeroBase _hero;

          float _yaw;
          float _pitch;
          bool _isZooming;

          readonly float _pitchMin = -85f;
          readonly float _pitchMax = 85f;

          // 灵敏度/FOV
          public float lookSensitivityX = 45f; // 度/秒
          public float lookSensitivityY = 45f; // 度/秒
          public bool invertY = false;

          public float fov = 110f;
          public float zoomFov = 55f;

          public FpsMode(UnitBase unit, Transform fpsPivot)
          {
              _unit = unit;
              _fpsPivot = fpsPivot;
              _hero = unit != null ? unit.GetComponent<HeroBase>() : null;

              _yaw = _unit.GetYaw();
              _pitch = 0f;
              _isZooming = false;
          }

          public void Enter()
          {
              Cursor.lockState = CursorLockMode.Locked;
              Cursor.visible = false;

              _yaw = _unit.GetYaw();
              _pitch = 0f;
          }

          public void Exit()
          {
              _unit.SetCrouching(false);
              _unit.SetRunning(false);
              _isZooming = false;
              Cursor.lockState = CursorLockMode.None;
              Cursor.visible = true;
          }

          public void Tick(float dt, InputIntent intent)
          {
              float lookX = intent.Look.x * lookSensitivityX * dt;
              float lookY = intent.Look.y * lookSensitivityY * dt;
              if (invertY) lookY = -lookY;

              _yaw += lookX;
              _pitch -= lookY;
              _pitch = Mathf.Clamp(_pitch, _pitchMin, _pitchMax);
              _isZooming = intent.RightHeld;
              if (_isZooming)
              {
                  _unit.ApplyFallSpeedReduction(
                      AimFallReductionDuration,
                      AimFallReductionMaxFallSpeed,
                      AimFallReductionGravityMultiplier);
              }

              _unit.SetYaw(_yaw);

              //调用当前武器攻击
              Vector3 fireDir = Quaternion.Euler(_pitch, _yaw, 0f) * Vector3.forward;
              Transform firePivot = _fpsPivot != null ? _fpsPivot : _unit.transform;
              Vector3 fireOrigin = firePivot.position;

              _hero?.ProcessWeaponSwitchInput(intent);

              //角色技能先处理，Dash/Fly 需要在本帧跳跃和移动前更新角色状态。
              InputIntent abilityIntent = intent;
              abilityIntent.RightClick = false;
              abilityIntent.RightHeld = false;
              _unit.AbilityRouter?.Process(abilityIntent, fireDir, fireOrigin);

              //跳跃
              if (intent.Space && !_unit.IsFlightEnabled)
              {
                  _unit.Jump();
              }

              Quaternion yawRot = Quaternion.Euler(0f, _yaw, 0f);
              Vector3 moveWorld = yawRot * new Vector3(intent.Move.x, 0f, intent.Move.y);
              bool wantsCrouch = intent.Ctrl && !_unit.IsFlightEnabled;
              bool wantsRun = intent.Shift && !wantsCrouch && !_unit.IsFlightEnabled;
              _unit.SetCrouching(wantsCrouch);
              _unit.SetRunning(wantsRun && moveWorld.sqrMagnitude > 0.0001f);
              _unit.MoveImmediate(moveWorld, _unit.GetDirectMoveSpeed(wantsRun, wantsCrouch));

              fireOrigin = firePivot.position;
              _hero?.SetWeaponAimContext(fireOrigin, fireDir);

              //当前武器自己读取输入，确保 FPS 射击使用本帧移动后的相机位置。
              _hero?.ProcessActiveWeaponInput(intent);

              if (intent.LeftHeld)
              {
                  if (_hero != null)
                      _hero.TryUseCurrentWeaponPrimary(fireDir, fireOrigin);
                  else if (_unit.Combat != null && _unit.Combat.TryUsePrimaryInDirection(fireDir))
                  {
                      _unit.ApplyFallSpeedReduction(
                          AimFallReductionDuration,
                          AimFallReductionMaxFallSpeed,
                          AimFallReductionGravityMultiplier);
                  }
              }
          }

          public CameraState GetCameraTarget()
          {
              Quaternion camRot = Quaternion.Euler(_pitch, _yaw, 0f);
              Transform pivot = _fpsPivot != null ? _fpsPivot : _unit.transform;

              return new CameraState
              {
                  Position = pivot.position, 
                  Rotation = camRot,
                  Fov = _isZooming ? zoomFov : fov
              };
          }
      }
  }
