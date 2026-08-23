using System;
using UnityEngine;
using UnityEngine.AI;

namespace OriginCore.Gameplay
{
    public enum HybridControlState
    {
        Autopilot = 0,
        PendingManualOverride = 1,
        Manual = 2
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent), typeof(CharacterController))]
    public sealed class HybridControlDriver : MonoBehaviour
    {
        [SerializeField] private NavMeshAgent _navMeshAgent;
        [SerializeField] private CharacterController _characterController;
        [SerializeField] private HybridControlState _state = HybridControlState.Autopilot;
        [Min(0.1f), SerializeField] private float _navMeshSampleDistance = 2f;

        public event Action<HybridControlDriver, HybridControlState, HybridControlState> StateChanged;

        public NavMeshAgent NavMeshAgent => _navMeshAgent;
        public CharacterController CharacterController => _characterController;
        public HybridControlState State => _state;
        public float NavMeshSampleDistance => _navMeshSampleDistance;
        public string LastError { get; private set; } = string.Empty;
        public bool IsAutopilotActive => _state != HybridControlState.Manual &&
                                         _navMeshAgent != null && _navMeshAgent.enabled;
        public bool IsManualActive => _state == HybridControlState.Manual &&
                                      _characterController != null && _characterController.enabled;

        private void Awake()
        {
            CacheComponents();
        }

        private void OnEnable()
        {
            CacheComponents();
            ApplyOwnership();
        }

        private void OnValidate()
        {
            CacheComponents();
            _navMeshSampleDistance = Mathf.Max(0.1f, _navMeshSampleDistance);
        }

        public void Configure(
            NavMeshAgent navMeshAgent,
            CharacterController characterController,
            HybridControlState initialState = HybridControlState.Autopilot,
            float navMeshSampleDistance = 2f)
        {
            _navMeshAgent = navMeshAgent != null ? navMeshAgent : GetComponent<NavMeshAgent>();
            _characterController = characterController != null
                ? characterController
                : GetComponent<CharacterController>();
            _state = initialState;
            _navMeshSampleDistance = Mathf.Max(0.1f, navMeshSampleDistance);
            LastError = string.Empty;
            ApplyOwnership();
        }

        public bool ArmManualOverride()
        {
            if (_state != HybridControlState.Autopilot)
            {
                return false;
            }

            SetState(HybridControlState.PendingManualOverride);
            return true;
        }

        public bool TryActivateManualOverride()
        {
            if (_state != HybridControlState.PendingManualOverride)
            {
                return false;
            }

            if (_navMeshAgent != null && _navMeshAgent.enabled && _navMeshAgent.isOnNavMesh)
            {
                _navMeshAgent.isStopped = true;
            }

            LastError = string.Empty;
            SetState(HybridControlState.Manual);
            return true;
        }

        public bool TryReturnToAutopilot(out string error)
        {
            error = string.Empty;
            CacheComponents();

            if (_state == HybridControlState.Autopilot)
            {
                LastError = string.Empty;
                ApplyOwnership();
                return true;
            }

            if (_state == HybridControlState.PendingManualOverride)
            {
                LastError = string.Empty;
                SetState(HybridControlState.Autopilot);
                return true;
            }

            if (_navMeshAgent == null || _characterController == null)
            {
                return FailReturn("Hybrid pawn is missing NavMeshAgent or CharacterController.", out error);
            }

            Vector3 originalPosition = transform.position;
            int areaMask = _navMeshAgent.areaMask;
            if (!NavMesh.SamplePosition(
                    originalPosition,
                    out NavMeshHit hit,
                    _navMeshSampleDistance,
                    areaMask))
            {
                return FailReturn(
                    "No NavMesh position was found near the pawn; direct mode remains active.",
                    out error);
            }

            _characterController.enabled = false;
            transform.position = hit.position;
            _navMeshAgent.updatePosition = true;
            _navMeshAgent.updateRotation = true;
            _navMeshAgent.enabled = true;
            bool warped = _navMeshAgent.isOnNavMesh && _navMeshAgent.Warp(hit.position);
            if (!warped)
            {
                _navMeshAgent.enabled = false;
                transform.position = originalPosition;
                _characterController.enabled = true;
                return FailReturn(
                    "NavMeshAgent could not attach to the sampled position; pawn position was preserved.",
                    out error);
            }

            _navMeshAgent.ResetPath();
            _navMeshAgent.isStopped = true;
            LastError = string.Empty;
            SetState(HybridControlState.Autopilot);
            return true;
        }

        private bool FailReturn(string message, out string error)
        {
            LastError = message;
            error = message;
            return false;
        }

        private void SetState(HybridControlState state)
        {
            HybridControlState previous = _state;
            _state = state;
            ApplyOwnership();
            if (previous != _state)
            {
                StateChanged?.Invoke(this, previous, _state);
            }
        }

        private void ApplyOwnership()
        {
            bool manual = _state == HybridControlState.Manual;
            if (manual)
            {
                if (_navMeshAgent != null && _navMeshAgent.enabled)
                {
                    _navMeshAgent.enabled = false;
                }

                if (_characterController != null)
                {
                    _characterController.enabled = true;
                }
            }
            else
            {
                if (_characterController != null && _characterController.enabled)
                {
                    _characterController.enabled = false;
                }

                if (_navMeshAgent != null && !_navMeshAgent.enabled)
                {
                    _navMeshAgent.enabled = true;
                }
            }
        }

        private void CacheComponents()
        {
            if (_navMeshAgent == null)
            {
                _navMeshAgent = GetComponent<NavMeshAgent>();
            }

            if (_characterController == null)
            {
                _characterController = GetComponent<CharacterController>();
            }
        }
    }
}
