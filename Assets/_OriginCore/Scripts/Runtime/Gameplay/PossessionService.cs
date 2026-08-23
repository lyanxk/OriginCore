using System;
using OriginCore.Input;
using OriginCore.SceneFlow;
using UnityEngine;

namespace OriginCore.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class PossessionService : MonoBehaviour
    {
        [Min(0f), SerializeField] private float _moveThreshold = 0.1f;

        private SceneContext _boundScene;
        private HybridControlDriver _currentPawn;

        public event Action<HybridControlDriver, HybridControlDriver> PawnChanged;
        public event Action<HybridControlDriver, InputSnapshot> AutopilotCancellationRequested;
        public event Action<HybridControlDriver, InputSnapshot> ManualOverrideActivated;

        public HybridControlDriver CurrentPawn
        {
            get
            {
                if (_currentPawn == null)
                {
                    _currentPawn = null;
                }

                return _currentPawn;
            }
        }

        public bool HasPawn => CurrentPawn != null;
        public float MoveThreshold => _moveThreshold;
        public int ManualOverrideCount { get; private set; }
        public int LastManualOverrideFrame { get; private set; } = -1;

        public void Configure(float moveThreshold)
        {
            _moveThreshold = Mathf.Max(0f, moveThreshold);
        }

        public void BindScene(SceneContext context)
        {
            _boundScene = context;
            HybridControlDriver driver = null;
            if (context != null && context.DefaultPawn != null)
            {
                driver = context.DefaultPawn.GetComponent<HybridControlDriver>();
            }

            BindPawn(driver);
        }

        public void UnbindScene(SceneContext context)
        {
            if (_boundScene != context)
            {
                return;
            }

            _boundScene = null;
            BindPawn(null);
        }

        public void BindPawn(HybridControlDriver pawn)
        {
            HybridControlDriver previous = CurrentPawn;
            if (previous == pawn)
            {
                return;
            }

            _currentPawn = pawn;
            ManualOverrideCount = 0;
            LastManualOverrideFrame = -1;
            PawnChanged?.Invoke(previous, _currentPawn);
        }

        public bool PrepareDirectObservation(out string error)
        {
            error = string.Empty;
            HybridControlDriver pawn = CurrentPawn;
            if (pawn == null)
            {
                error = "No possessable pawn is registered in the current SceneContext.";
                return false;
            }

            if (pawn.State == HybridControlState.Autopilot)
            {
                pawn.ArmManualOverride();
            }

            return true;
        }

        public bool TryHandleDirectInput(InputSnapshot snapshot)
        {
            HybridControlDriver pawn = CurrentPawn;
            if (pawn == null || pawn.State != HybridControlState.PendingManualOverride ||
                snapshot.ActiveMap == GameplayInputMap.RTS ||
                !snapshot.HasMeaningfulDirectInput(_moveThreshold))
            {
                return false;
            }

            AutopilotCancellationRequested?.Invoke(pawn, snapshot);
            if (!pawn.TryActivateManualOverride())
            {
                return false;
            }

            ManualOverrideCount++;
            LastManualOverrideFrame = snapshot.Frame;
            ManualOverrideActivated?.Invoke(pawn, snapshot);
            return true;
        }

        public bool TryReturnToRts(out string error)
        {
            HybridControlDriver pawn = CurrentPawn;
            if (pawn == null)
            {
                error = string.Empty;
                return true;
            }

            return pawn.TryReturnToAutopilot(out error);
        }
    }
}
