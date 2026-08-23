using OriginCore.Gameplay;
using OriginCore.RTS.Commands;
using UnityEngine;

namespace OriginCore.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnitCommandQueue), typeof(AutoTargetScanner))]
    public sealed class SimpleEnemyBrain : MonoBehaviour
    {
        [SerializeField] private UnitCommandQueue _commandQueue;
        [SerializeField] private AutoTargetScanner _targetScanner;
        [SerializeField] private AttackCapability _attackCapability;
        [Min(0.1f), SerializeField] private float _scanInterval = 0.5f;

        private float _scanRemaining;

        public UnitCommandQueue CommandQueue => _commandQueue;
        public AutoTargetScanner TargetScanner => _targetScanner;
        public AttackCapability AttackCapability => _attackCapability;
        public float ScanInterval => _scanInterval;
        public int IssuedAttackCount { get; private set; }

        private void Awake()
        {
            CacheComponents();
        }

        private void OnEnable()
        {
            CacheComponents();
            _scanRemaining = 0f;
        }

        private void OnValidate()
        {
            CacheComponents();
            _scanInterval = Mathf.Max(0.1f, _scanInterval);
        }

        private void Update()
        {
            _scanRemaining -= Time.deltaTime;
            if (_scanRemaining > 0f || _commandQueue == null ||
                _targetScanner == null || _attackCapability == null ||
                _commandQueue.TotalCommandCount > 0)
            {
                return;
            }

            _scanRemaining = _scanInterval;
            float scanRadius = Mathf.Max(
                _attackCapability.AttackRange,
                _attackCapability.VisionRange);
            if (!_targetScanner.FindNearestHostile(scanRadius, out EntityIdentity target))
            {
                return;
            }

            if (_commandQueue.TryIssue(
                    new AttackCommand(target),
                    false,
                    out CommandRejectionReason _))
            {
                IssuedAttackCount++;
            }
        }

        public void Configure(
            UnitCommandQueue commandQueue,
            AutoTargetScanner targetScanner,
            AttackCapability attackCapability,
            float scanInterval = 0.5f)
        {
            _commandQueue = commandQueue != null
                ? commandQueue
                : GetComponent<UnitCommandQueue>();
            _targetScanner = targetScanner != null
                ? targetScanner
                : GetComponent<AutoTargetScanner>();
            _attackCapability = attackCapability != null
                ? attackCapability
                : GetComponent<AttackCapability>();
            _scanInterval = Mathf.Max(0.1f, scanInterval);
        }

        private void CacheComponents()
        {
            if (_commandQueue == null)
            {
                _commandQueue = GetComponent<UnitCommandQueue>();
            }

            if (_targetScanner == null)
            {
                _targetScanner = GetComponent<AutoTargetScanner>();
            }

            if (_attackCapability == null)
            {
                _attackCapability = GetComponent<AttackCapability>();
            }
        }
    }
}
