using OriginCore.Buildings;
using OriginCore.Gameplay;
using UnityEngine;

namespace OriginCore.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AttackCapability), typeof(AutoTargetScanner))]
    public sealed class TurretAutoAttackController : MonoBehaviour
    {
        [SerializeField] private BuildingRuntime _buildingRuntime;
        [SerializeField] private AttackCapability _attackCapability;
        [SerializeField] private AutoTargetScanner _targetScanner;
        [Min(0.05f), SerializeField] private float _scanInterval = 0.2f;

        private float _scanRemaining;

        public BuildingRuntime BuildingRuntime => _buildingRuntime;
        public AttackCapability AttackCapability => _attackCapability;
        public AutoTargetScanner TargetScanner => _targetScanner;
        public float ScanInterval => _scanInterval;
        public int AttackAttemptCount { get; private set; }

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
            _scanInterval = Mathf.Max(0.05f, _scanInterval);
        }

        private void Update()
        {
            if (Time.deltaTime <= 0f || _attackCapability == null ||
                !_attackCapability.enabled || _targetScanner == null ||
                _buildingRuntime != null && !_buildingRuntime.IsOperational)
            {
                return;
            }

            _scanRemaining -= Time.deltaTime;
            if (_scanRemaining > 0f)
            {
                return;
            }

            _scanRemaining = _scanInterval;
            if (!_targetScanner.FindNearestHostile(
                    _attackCapability.AttackRange,
                    out EntityIdentity target))
            {
                return;
            }

            AttackAttemptCount++;
            _attackCapability.TryAttack(target, out _);
        }

        public void Configure(
            BuildingRuntime buildingRuntime,
            AttackCapability attackCapability,
            AutoTargetScanner targetScanner,
            float scanInterval = 0.2f)
        {
            _buildingRuntime = buildingRuntime != null
                ? buildingRuntime
                : GetComponent<BuildingRuntime>();
            _attackCapability = attackCapability != null
                ? attackCapability
                : GetComponent<AttackCapability>();
            _targetScanner = targetScanner != null
                ? targetScanner
                : GetComponent<AutoTargetScanner>();
            _scanInterval = Mathf.Max(0.05f, scanInterval);
        }

        private void CacheComponents()
        {
            if (_buildingRuntime == null)
            {
                _buildingRuntime = GetComponent<BuildingRuntime>();
            }

            if (_attackCapability == null)
            {
                _attackCapability = GetComponent<AttackCapability>();
            }

            if (_targetScanner == null)
            {
                _targetScanner = GetComponent<AutoTargetScanner>();
            }
        }
    }
}
