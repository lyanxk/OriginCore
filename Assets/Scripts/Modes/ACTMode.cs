﻿using Camera;
 using Core;
 using Input;
 using Unit.Combat.Hero;
 using Unit.Movement;
 using UnityEngine;

 namespace Modes
 {
     public class ActMode : IControlMode
     {
         public string Name => "ACT";

         readonly UnitBase _unit;
         readonly Transform _tpsPivot;
         readonly HeroBase _hero;

         float _yaw;
         float _pitch;

         readonly float _pitchMin = -35f;
         readonly float _pitchMax = 65f;

         // 第三人称相机参数
         readonly float _distance = 6f;
         readonly float _height = 1f;
    
         // Look 手感参数（建议调这里）
         public float lookSensitivity = 45f;   // 基础灵敏度

         float _yawTarget;
         float _pitchTarget;


         public ActMode(UnitBase unit, Transform tpsPivot)
         {
             _unit = unit;
             _tpsPivot = tpsPivot;
             _hero = unit != null ? unit.GetComponent<HeroBase>() : null;
             _yaw = _unit.GetYaw();
             _pitch = 15f;

             _yawTarget = _yaw;
             _pitchTarget = _pitch;
        
         }

         public void Enter()
         {
             Cursor.lockState = CursorLockMode.Locked;
             Cursor.visible = false;

             _yaw = _unit.GetYaw();
             _pitch = Mathf.Clamp(_pitch, _pitchMin, _pitchMax);

             _yawTarget = _yaw;
             _pitchTarget = _pitch;
         }


         public void Exit()
         {
             _unit.SetCrouching(false);
             _unit.SetRunning(false);
         }

         public void Tick(float dt, InputIntent intent)
         {
             //把输入转换成“目标角度”
             float lookX = intent.Look.x;
             float lookY = intent.Look.y;

             _yawTarget   += lookX * lookSensitivity * dt;
             _pitchTarget -= lookY * lookSensitivity * dt;
             _pitchTarget = Mathf.Clamp(_pitchTarget, _pitchMin, _pitchMax);

             _yaw = _yawTarget;
             _pitch = _pitchTarget;

             _hero?.ProcessWeaponSwitchInput(intent);

             Vector3 cameraForward = Quaternion.Euler(_pitch, _yaw, 0f) * Vector3.forward;
             Vector3 cameraOrigin = GetCameraPosition();
             _hero?.SetWeaponAimContext(_unit.transform.position, cameraForward);

             bool blocksModeAbilities = false;
             bool blocksMovement = false;
             bool blocksPrimaryAttack = false;
             if (_hero != null)
             {
                 _hero.ProcessPriorityWeaponInput(
                     intent,
                     out blocksModeAbilities,
                     out blocksMovement,
                     out blocksPrimaryAttack);
             }

             //角色技能先处理，Dash/Fly 需要在本帧移动和跳跃判断前更新角色状态。
             if (!blocksModeAbilities)
                 _unit.AbilityRouter?.Process(intent, cameraForward, cameraOrigin);

             //移动按视角yaw方向
             Quaternion yawRot = Quaternion.Euler(0f, _yaw, 0f);
             Vector3 moveWorld = yawRot * new Vector3(intent.Move.x, 0f, intent.Move.y);
             bool wantsCrouch = intent.Ctrl && !_unit.IsFlightEnabled;
             bool wantsRun = intent.Shift && !wantsCrouch && !_unit.IsFlightEnabled;
             if (!blocksMovement && wantsCrouch && intent.Shift)
                 _unit.TryStartSlide(moveWorld);

             if (_unit.IsFlightEnabled)
             {
                 _unit.SetCrouching(false);
                 _unit.SetRunning(false);
                 if (!blocksMovement)
                 {
                     _unit.MoveImmediate(
                         moveWorld,
                         intent.Shift ? UnitBase.MoveStance.FlightBoost : UnitBase.MoveStance.Flight);
                 }
             }
             else if (_unit.IsSliding)
             {
                 _unit.SetCrouching(true);
                 _unit.SetRunning(false);
                 if (!blocksMovement)
                     _unit.MoveImmediate(moveWorld, UnitBase.MoveStance.Slide);
             }
             else
             {
                 _unit.SetCrouching(wantsCrouch);
                 _unit.SetRunning(wantsRun && moveWorld.sqrMagnitude > 0.0001f);
                 if (!blocksMovement)
                 {
                     UnitBase.MoveStance moveStance = wantsCrouch
                         ? UnitBase.MoveStance.Crouch
                         : wantsRun ? UnitBase.MoveStance.Run : UnitBase.MoveStance.Walk;
                     _unit.MoveImmediate(moveWorld, moveStance);
                 }
             }
         
             //跳跃
             if (!blocksMovement && intent.Space && !_unit.IsFlightEnabled && !_unit.IsSliding)
             {
                 _unit.Jump();
             }

             //角色朝向：跟随移动方向
             Vector3 planar = new Vector3(_unit.PlanarVelocity.x, 0f, _unit.PlanarVelocity.z);
             if (planar.sqrMagnitude <= 0.0001f)
                 planar = new Vector3(moveWorld.x, 0f, moveWorld.z);
             if (!blocksMovement && planar.sqrMagnitude > 0.0001f)
             {
                 float facingYaw = Quaternion.LookRotation(planar, Vector3.up).eulerAngles.y;
                 _unit.SetYaw(facingYaw);
             }

             //当前武器自己读取输入，确保 Charge Shot 使用本帧移动后的枪口位置。
             _hero?.SetWeaponAimContext(_unit.transform.position, cameraForward);
             if (!blocksMovement)
                 _hero?.ProcessActiveWeaponInput(intent);

             // 按住左键：沿单位面朝方向攻击
             if (intent.LeftHeld && !blocksPrimaryAttack)
             {
                 if (_hero != null)
                     _hero.TryUseCurrentWeaponPrimary(cameraForward);
                 else
                     _unit.Combat?.TryUsePrimaryInDirection(cameraForward);
             }
         }

        public CameraState GetCameraTarget()
        {
             Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);

             return new CameraState
             {
                 Position = GetCameraPosition(),
                 Rotation = rot,
                 Fov = 85f
             };
         }

         Vector3 GetCameraPosition()
         {
             Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);
             Vector3 back = rot * Vector3.back; // 相机朝后
             Transform pivot = _tpsPivot != null ? _tpsPivot : _unit.transform;
             return pivot.position + back * _distance + Vector3.up * _height;
         }

     }
 }
