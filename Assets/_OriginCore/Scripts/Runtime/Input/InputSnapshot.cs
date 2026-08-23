using UnityEngine;

namespace OriginCore.Input
{
    public enum GameplayInputMap
    {
        RTS = 0,
        ACT = 1,
        FPS = 2
    }

    public readonly struct InputButtonState
    {
        public InputButtonState(bool wasPressedThisFrame, bool isPressed, bool wasReleasedThisFrame)
        {
            WasPressedThisFrame = wasPressedThisFrame;
            IsPressed = isPressed;
            WasReleasedThisFrame = wasReleasedThisFrame;
        }

        public bool WasPressedThisFrame { get; }
        public bool IsPressed { get; }
        public bool WasReleasedThisFrame { get; }
        public bool HasDirectIntent => WasPressedThisFrame || IsPressed;
    }

    public readonly struct RtsInputSnapshot
    {
        public RtsInputSnapshot(
            Vector2 point,
            InputButtonState select,
            InputButtonState command,
            InputButtonState attack,
            InputButtonState stop,
            InputButtonState queueModifier,
            InputButtonState selectIdleWorkers,
            InputButtonState selectCombatUnits,
            float cameraZoom)
            : this(
                point,
                select,
                command,
                attack,
                stop,
                queueModifier,
                selectIdleWorkers,
                selectCombatUnits,
                cameraZoom,
                default(InputButtonState),
                default(InputButtonState),
                default(InputButtonState),
                default(InputButtonState))
        {
        }

        public RtsInputSnapshot(
            Vector2 point,
            InputButtonState select,
            InputButtonState command,
            InputButtonState attack,
            InputButtonState stop,
            InputButtonState queueModifier,
            InputButtonState selectIdleWorkers,
            InputButtonState selectCombatUnits,
            float cameraZoom,
            InputButtonState ability1,
            InputButtonState ability2,
            InputButtonState ability3,
            InputButtonState ability4)
            : this(
                point,
                select,
                command,
                attack,
                stop,
                queueModifier,
                selectIdleWorkers,
                selectCombatUnits,
                cameraZoom,
                ability1,
                ability2,
                ability3,
                ability4,
                default(InputButtonState))
        {
        }

        public RtsInputSnapshot(
            Vector2 point,
            InputButtonState select,
            InputButtonState command,
            InputButtonState attack,
            InputButtonState stop,
            InputButtonState queueModifier,
            InputButtonState selectIdleWorkers,
            InputButtonState selectCombatUnits,
            float cameraZoom,
            InputButtonState ability1,
            InputButtonState ability2,
            InputButtonState ability3,
            InputButtonState ability4,
            InputButtonState promote)
        {
            Point = point;
            Select = select;
            Command = command;
            Attack = attack;
            Stop = stop;
            QueueModifier = queueModifier;
            SelectIdleWorkers = selectIdleWorkers;
            SelectCombatUnits = selectCombatUnits;
            CameraZoom = cameraZoom;
            Ability1 = ability1;
            Ability2 = ability2;
            Ability3 = ability3;
            Ability4 = ability4;
            Promote = promote;
        }

        public Vector2 Point { get; }
        public InputButtonState Select { get; }
        public InputButtonState Command { get; }
        public InputButtonState Attack { get; }
        public InputButtonState Stop { get; }
        public InputButtonState QueueModifier { get; }
        public InputButtonState SelectIdleWorkers { get; }
        public InputButtonState SelectCombatUnits { get; }
        public float CameraZoom { get; }
        public InputButtonState Ability1 { get; }
        public InputButtonState Ability2 { get; }
        public InputButtonState Ability3 { get; }
        public InputButtonState Ability4 { get; }
        public InputButtonState Promote { get; }
    }

    public readonly struct ActInputSnapshot
    {
        public ActInputSnapshot(
            Vector2 move,
            Vector2 look,
            InputButtonState jump,
            InputButtonState crouch,
            InputButtonState targetLock,
            InputButtonState primary,
            InputButtonState weaponPrevious,
            InputButtonState weaponNext,
            InputButtonState weaponWheel,
            InputButtonState summonWorker)
            : this(
                move,
                look,
                jump,
                crouch,
                targetLock,
                primary,
                weaponPrevious,
                weaponNext,
                weaponWheel,
                summonWorker,
                default(InputButtonState),
                default(InputButtonState),
                default(InputButtonState),
                default(InputButtonState),
                default(InputButtonState))
        {
        }

        public ActInputSnapshot(
            Vector2 move,
            Vector2 look,
            InputButtonState jump,
            InputButtonState crouch,
            InputButtonState targetLock,
            InputButtonState primary,
            InputButtonState weaponPrevious,
            InputButtonState weaponNext,
            InputButtonState weaponWheel,
            InputButtonState summonWorker,
            InputButtonState ability1,
            InputButtonState ability2,
            InputButtonState ability3,
            InputButtonState ability4,
            InputButtonState transform)
            : this(
                move,
                look,
                jump,
                crouch,
                targetLock,
                primary,
                default(InputButtonState),
                weaponPrevious,
                weaponNext,
                weaponWheel,
                summonWorker,
                ability1,
                ability2,
                ability3,
                ability4,
                transform)
        {
        }

        public ActInputSnapshot(
            Vector2 move,
            Vector2 look,
            InputButtonState jump,
            InputButtonState crouch,
            InputButtonState targetLock,
            InputButtonState primary,
            InputButtonState secondary,
            InputButtonState weaponPrevious,
            InputButtonState weaponNext,
            InputButtonState weaponWheel,
            InputButtonState summonWorker,
            InputButtonState ability1,
            InputButtonState ability2,
            InputButtonState ability3,
            InputButtonState ability4,
            InputButtonState transform)
        {
            Move = move;
            Look = look;
            Jump = jump;
            Crouch = crouch;
            TargetLock = targetLock;
            Primary = primary;
            Secondary = secondary;
            WeaponPrevious = weaponPrevious;
            WeaponNext = weaponNext;
            WeaponWheel = weaponWheel;
            SummonWorker = summonWorker;
            Ability1 = ability1;
            Ability2 = ability2;
            Ability3 = ability3;
            Ability4 = ability4;
            Transform = transform;
        }

        public Vector2 Move { get; }
        public Vector2 Look { get; }
        public InputButtonState Jump { get; }
        public InputButtonState Crouch { get; }
        public InputButtonState TargetLock { get; }
        public InputButtonState Primary { get; }
        public InputButtonState Secondary { get; }
        public InputButtonState WeaponPrevious { get; }
        public InputButtonState WeaponNext { get; }
        public InputButtonState WeaponWheel { get; }
        public InputButtonState SummonWorker { get; }
        public InputButtonState Ability1 { get; }
        public InputButtonState Ability2 { get; }
        public InputButtonState Ability3 { get; }
        public InputButtonState Ability4 { get; }
        public InputButtonState Transform { get; }
        public InputButtonState DirectDash => Ability1;
        public InputButtonState WeaponModifier => Ability2;

        public bool HasMeaningfulDirectInput(float moveThreshold = 0.1f)
        {
            return Move.magnitude > moveThreshold ||
                   Jump.HasDirectIntent ||
                   Crouch.HasDirectIntent ||
                   TargetLock.HasDirectIntent ||
                   Primary.HasDirectIntent ||
                   Secondary.HasDirectIntent ||
                   WeaponPrevious.HasDirectIntent ||
                   WeaponNext.HasDirectIntent ||
                   WeaponWheel.HasDirectIntent ||
                   SummonWorker.HasDirectIntent ||
                   Ability1.HasDirectIntent ||
                   Ability2.HasDirectIntent ||
                   Ability3.HasDirectIntent ||
                   Ability4.HasDirectIntent ||
                   Transform.HasDirectIntent;
        }
    }

    public readonly struct FpsInputSnapshot
    {
        public FpsInputSnapshot(
            Vector2 move,
            Vector2 look,
            InputButtonState jump,
            InputButtonState crouch,
            InputButtonState sprint,
            InputButtonState aim,
            InputButtonState primary,
            InputButtonState slot1,
            InputButtonState slot2,
            InputButtonState slot3,
            InputButtonState grenade,
            InputButtonState summonWorker)
            : this(
                move,
                look,
                jump,
                crouch,
                sprint,
                aim,
                primary,
                slot1,
                slot2,
                slot3,
                grenade,
                summonWorker,
                default(InputButtonState),
                default(InputButtonState),
                default(InputButtonState),
                default(InputButtonState),
                default(InputButtonState))
        {
        }

        public FpsInputSnapshot(
            Vector2 move,
            Vector2 look,
            InputButtonState jump,
            InputButtonState crouch,
            InputButtonState sprint,
            InputButtonState aim,
            InputButtonState primary,
            InputButtonState slot1,
            InputButtonState slot2,
            InputButtonState slot3,
            InputButtonState grenade,
            InputButtonState summonWorker,
            InputButtonState ability1,
            InputButtonState ability2,
            InputButtonState ability3,
            InputButtonState ability4,
            InputButtonState transform)
            : this(
                move,
                look,
                jump,
                crouch,
                sprint,
                aim,
                primary,
                slot1,
                slot2,
                slot3,
                grenade,
                default(InputButtonState),
                summonWorker,
                ability1,
                ability2,
                ability3,
                ability4,
                transform)
        {
        }

        public FpsInputSnapshot(
            Vector2 move,
            Vector2 look,
            InputButtonState jump,
            InputButtonState crouch,
            InputButtonState sprint,
            InputButtonState aim,
            InputButtonState primary,
            InputButtonState slot1,
            InputButtonState slot2,
            InputButtonState slot3,
            InputButtonState grenade,
            InputButtonState weaponWheel,
            InputButtonState summonWorker,
            InputButtonState ability1,
            InputButtonState ability2,
            InputButtonState ability3,
            InputButtonState ability4,
            InputButtonState transform)
        {
            Move = move;
            Look = look;
            Jump = jump;
            Crouch = crouch;
            Sprint = sprint;
            Aim = aim;
            Primary = primary;
            Slot1 = slot1;
            Slot2 = slot2;
            Slot3 = slot3;
            Grenade = grenade;
            WeaponWheel = weaponWheel;
            SummonWorker = summonWorker;
            Ability1 = ability1;
            Ability2 = ability2;
            Ability3 = ability3;
            Ability4 = ability4;
            Transform = transform;
        }

        public Vector2 Move { get; }
        public Vector2 Look { get; }
        public InputButtonState Jump { get; }
        public InputButtonState Crouch { get; }
        public InputButtonState Sprint { get; }
        public InputButtonState Aim { get; }
        public InputButtonState Primary { get; }
        public InputButtonState Slot1 { get; }
        public InputButtonState Slot2 { get; }
        public InputButtonState Slot3 { get; }
        public InputButtonState Grenade { get; }
        public InputButtonState WeaponWheel { get; }
        public InputButtonState SummonWorker { get; }
        public InputButtonState Ability1 { get; }
        public InputButtonState Ability2 { get; }
        public InputButtonState Ability3 { get; }
        public InputButtonState Ability4 { get; }
        public InputButtonState Transform { get; }

        public bool HasMeaningfulDirectInput(float moveThreshold = 0.1f)
        {
            return Move.magnitude > moveThreshold ||
                   Jump.HasDirectIntent ||
                   Crouch.HasDirectIntent ||
                   Sprint.WasPressedThisFrame ||
                   Aim.HasDirectIntent ||
                   Primary.HasDirectIntent ||
                   Slot1.HasDirectIntent ||
                   Slot2.HasDirectIntent ||
                   Slot3.HasDirectIntent ||
                   Grenade.HasDirectIntent ||
                   WeaponWheel.HasDirectIntent ||
                   SummonWorker.HasDirectIntent ||
                   Ability1.HasDirectIntent ||
                   Ability2.HasDirectIntent ||
                   Ability3.HasDirectIntent ||
                   Ability4.HasDirectIntent ||
                   Transform.HasDirectIntent;
        }
    }

    public readonly struct InputSnapshot
    {
        private InputSnapshot(
            int frame,
            GameplayInputMap activeMap,
            bool gameplaySuppressed,
            RtsInputSnapshot rts,
            ActInputSnapshot act,
            FpsInputSnapshot fps)
        {
            Frame = frame;
            ActiveMap = activeMap;
            GameplaySuppressed = gameplaySuppressed;
            RTS = rts;
            ACT = act;
            FPS = fps;
        }

        public int Frame { get; }
        public GameplayInputMap ActiveMap { get; }
        public bool GameplaySuppressed { get; }
        public RtsInputSnapshot RTS { get; }
        public ActInputSnapshot ACT { get; }
        public FpsInputSnapshot FPS { get; }

        public bool HasMeaningfulDirectInput(float moveThreshold = 0.1f)
        {
            if (GameplaySuppressed)
            {
                return false;
            }

            switch (ActiveMap)
            {
                case GameplayInputMap.ACT:
                    return ACT.HasMeaningfulDirectInput(moveThreshold);
                case GameplayInputMap.FPS:
                    return FPS.HasMeaningfulDirectInput(moveThreshold);
                default:
                    return false;
            }
        }

        public static InputSnapshot Suppressed(int frame, GameplayInputMap activeMap)
        {
            return new InputSnapshot(frame, activeMap, true, default(RtsInputSnapshot),
                default(ActInputSnapshot), default(FpsInputSnapshot));
        }

        public static InputSnapshot FromRts(int frame, RtsInputSnapshot rts)
        {
            return new InputSnapshot(frame, GameplayInputMap.RTS, false, rts,
                default(ActInputSnapshot), default(FpsInputSnapshot));
        }

        public static InputSnapshot FromAct(int frame, ActInputSnapshot act)
        {
            return new InputSnapshot(frame, GameplayInputMap.ACT, false, default(RtsInputSnapshot),
                act, default(FpsInputSnapshot));
        }

        public static InputSnapshot FromFps(int frame, FpsInputSnapshot fps)
        {
            return new InputSnapshot(frame, GameplayInputMap.FPS, false, default(RtsInputSnapshot),
                default(ActInputSnapshot), fps);
        }
    }
}
