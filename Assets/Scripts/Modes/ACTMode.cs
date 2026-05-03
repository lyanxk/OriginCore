﻿using Camera;
 using Core;
 using Input;
 using Unit.Movement;
 using UnityEngine;

 namespace Modes
 {
     public class ActMode : IControlMode
     {
         public string Name => "ACT";

         readonly UnitBase _unit;
         readonly Transform _tpsPivot;

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

         bool _isDashing;
         float _dashTimer;
         Vector3 _dashDir;


         public ActMode(UnitBase unit, Transform tpsPivot)
         {
             _unit = unit;
             _tpsPivot = tpsPivot;
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

             //移动按视角yaw方向
             Quaternion yawRot = Quaternion.Euler(0f, _yaw, 0f);
             Vector3 moveWorld = yawRot * new Vector3(intent.Move.x, 0f, intent.Move.y);
             bool wantsCrouch = intent.Ctrl && !_unit.IsFlightEnabled;
             bool wantsRun = intent.Shift && !wantsCrouch && !_unit.IsFlightEnabled;
             _unit.SetCrouching(wantsCrouch);
             _unit.SetRunning(wantsRun && moveWorld.sqrMagnitude > 0.0001f);
             _unit.MoveImmediate(moveWorld, _unit.GetDirectMoveSpeed(wantsRun, wantsCrouch));
        
             //跳跃
             if (intent.Space)
             {
                 _unit.Jump();
             }
        
             //技能执行
             _unit.AbilityRouter?.Process(intent);

             //角色朝向：跟随移动方向
             Vector3 planar = new Vector3(moveWorld.x, 0f, moveWorld.z);
             if (planar.sqrMagnitude > 0.0001f)
             {
                 float facingYaw = Quaternion.LookRotation(planar, Vector3.up).eulerAngles.y;
                 _unit.SetYaw(facingYaw);
             }

             // 按住左键：沿单位面朝方向攻击
             if (intent.LeftHeld)
                 _unit.Combat?.TryUsePrimaryInDirection(_unit.transform.forward);
         }
         public CameraState GetCameraTarget()
         {
             Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);
             Vector3 back = rot * Vector3.back; // 相机朝后
             Transform pivot = _tpsPivot != null ? _tpsPivot : _unit.transform;
             Vector3 pos = pivot.position + back * _distance + Vector3.up * _height;

             return new CameraState
             {
                 Position = pos,
                 Rotation = rot,
                 Fov = 85f
             };
         }
     }
 }
