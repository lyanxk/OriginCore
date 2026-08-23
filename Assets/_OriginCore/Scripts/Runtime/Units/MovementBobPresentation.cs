using UnityEngine;
using UnityEngine.AI;

namespace OriginCore.Units
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1000)]
    public sealed class MovementBobPresentation : MonoBehaviour
    {
        [SerializeField] private Transform _visualRoot;
        [SerializeField] private NavMeshMovementDriver _movementDriver;
        [SerializeField] private NavMeshAgent _navMeshAgent;
        [SerializeField] private CharacterController _characterController;
        [Min(0f), SerializeField] private float _amplitude = 0.12f;
        [Min(0.01f), SerializeField] private float _cyclesPerSecond = 3f;
        [Min(0f), SerializeField] private float _minimumSpeed = 0.05f;
        [Min(0.01f), SerializeField] private float _returnSpeed = 1.5f;
        [Min(0.1f), SerializeField] private float _maximumSampleSpeed = 40f;

        private Vector3 _baseLocalPosition;
        private Vector3 _lastWorldPosition;
        private float _phase;
        private float _currentOffset;
        private bool _hasPositionSample;

        public Transform VisualRoot => _visualRoot;
        public float Amplitude => _amplitude;
        public float CyclesPerSecond => _cyclesPerSecond;
        public bool IsBobbing { get; private set; }

        private void Reset()
        {
            CacheComponents();
            _visualRoot = transform.Find("VisualRoot");
            CaptureBaseline();
        }

        private void Awake()
        {
            CacheComponents();
            CaptureBaseline();
        }

        private void OnEnable()
        {
            CacheComponents();
            CaptureBaseline();
        }

        private void OnDisable()
        {
            RestoreBaseline();
            _hasPositionSample = false;
            IsBobbing = false;
        }

        private void OnValidate()
        {
            _amplitude = Mathf.Max(0f, _amplitude);
            _cyclesPerSecond = Mathf.Max(0.01f, _cyclesPerSecond);
            _minimumSpeed = Mathf.Max(0f, _minimumSpeed);
            _returnSpeed = Mathf.Max(0.01f, _returnSpeed);
            _maximumSampleSpeed = Mathf.Max(0.1f, _maximumSampleSpeed);
            CacheComponents();
        }

        private void LateUpdate()
        {
            if (_visualRoot == null)
            {
                return;
            }

            float deltaTime = Mathf.Max(0f, Time.deltaTime);
            Vector3 currentWorldPosition = transform.position;
            bool movedByTransform = IsTransformMoving(currentWorldPosition, deltaTime);
            _lastWorldPosition = currentWorldPosition;
            _hasPositionSample = true;

            if (deltaTime <= 0f)
            {
                return;
            }

            bool moving = IsMovementSourceActive() || movedByTransform;
            if (moving)
            {
                _phase = Mathf.Repeat(
                    _phase + deltaTime * _cyclesPerSecond * Mathf.PI * 2f,
                    Mathf.PI * 2f);
                _currentOffset = Mathf.Sin(_phase) * _amplitude;
            }
            else
            {
                _currentOffset = Mathf.MoveTowards(
                    _currentOffset,
                    0f,
                    _returnSpeed * deltaTime);
                if (Mathf.Approximately(_currentOffset, 0f))
                {
                    _phase = 0f;
                }
            }

            IsBobbing = moving || !Mathf.Approximately(_currentOffset, 0f);
            _visualRoot.localPosition = _baseLocalPosition + Vector3.up * _currentOffset;
        }

        public void Configure(
            Transform visualRoot,
            float amplitude = 0.12f,
            float cyclesPerSecond = 3f,
            float minimumSpeed = 0.05f,
            float returnSpeed = 1.5f)
        {
            RestoreBaseline();
            _visualRoot = visualRoot;
            _amplitude = Mathf.Max(0f, amplitude);
            _cyclesPerSecond = Mathf.Max(0.01f, cyclesPerSecond);
            _minimumSpeed = Mathf.Max(0f, minimumSpeed);
            _returnSpeed = Mathf.Max(0.01f, returnSpeed);
            CacheComponents();
            CaptureBaseline();
        }

        private bool IsMovementSourceActive()
        {
            float minimumSpeedSquared = _minimumSpeed * _minimumSpeed;
            if (_movementDriver != null && _movementDriver.IsMoving)
            {
                return true;
            }

            if (_navMeshAgent != null &&
                _navMeshAgent.enabled &&
                _navMeshAgent.velocity.sqrMagnitude > minimumSpeedSquared)
            {
                return true;
            }

            if (_characterController != null &&
                _characterController.enabled)
            {
                Vector3 planarVelocity = Vector3.ProjectOnPlane(
                    _characterController.velocity,
                    Vector3.up);
                if (planarVelocity.sqrMagnitude > minimumSpeedSquared)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsTransformMoving(Vector3 currentWorldPosition, float deltaTime)
        {
            if (!_hasPositionSample || deltaTime <= Mathf.Epsilon)
            {
                return false;
            }

            Vector3 displacement = Vector3.ProjectOnPlane(
                currentWorldPosition - _lastWorldPosition,
                Vector3.up);
            float speedSquared = displacement.sqrMagnitude / (deltaTime * deltaTime);
            return speedSquared > _minimumSpeed * _minimumSpeed &&
                   speedSquared <= _maximumSampleSpeed * _maximumSampleSpeed;
        }

        private void CacheComponents()
        {
            if (_movementDriver == null)
            {
                _movementDriver = GetComponent<NavMeshMovementDriver>();
            }

            if (_navMeshAgent == null)
            {
                _navMeshAgent = GetComponent<NavMeshAgent>();
            }

            if (_characterController == null)
            {
                _characterController = GetComponent<CharacterController>();
            }
        }

        private void CaptureBaseline()
        {
            if (_visualRoot != null)
            {
                _baseLocalPosition = _visualRoot.localPosition;
            }

            _lastWorldPosition = transform.position;
            _phase = 0f;
            _currentOffset = 0f;
            _hasPositionSample = true;
            IsBobbing = false;
        }

        private void RestoreBaseline()
        {
            if (_visualRoot != null)
            {
                _visualRoot.localPosition = _baseLocalPosition;
            }

            _phase = 0f;
            _currentOffset = 0f;
        }
    }
}
