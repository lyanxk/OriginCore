using OriginCore.Combat;
using OriginCore.Gameplay;
using OriginCore.RTS.Commands;
using UnityEngine;

namespace OriginCore.AI
{
    public interface IEnemyHeroBrain
    {
        bool IsOperational { get; }
        EntityIdentity CurrentTarget { get; }
    }

    [DisallowMultipleComponent]
    public sealed class EnemyHeroBrain : MonoBehaviour, IEnemyHeroBrain
    {
        [SerializeField, Min(0.05f)] private float _thinkInterval = 0.35f;
        [SerializeField, Min(0.1f)] private float _acquisitionRadius = 24f;
        [SerializeField] private EntityIdentity _identity;
        [SerializeField] private VitalsComponent _vitals;
        [SerializeField] private AutoTargetScanner _scanner;
        [SerializeField] private UnitCommandQueue _commands;

        private float _thinkRemaining;
        private AttackCommand _issuedAttack;

        public bool IsOperational => isActiveAndEnabled &&
            _identity != null && _scanner != null && _commands != null &&
            (_vitals == null || _vitals.IsAlive);
        public EntityIdentity CurrentTarget { get; private set; }

        private void Awake()
        {
            CacheComponents();
        }

        private void OnEnable()
        {
            CacheComponents();
            _thinkRemaining = 0f;
        }

        private void OnDisable()
        {
            CancelIssuedAttack();
            CurrentTarget = null;
        }

        private void OnValidate()
        {
            _thinkInterval = Mathf.Max(0.05f, _thinkInterval);
            _acquisitionRadius = Mathf.Max(0.1f, _acquisitionRadius);
            CacheComponents();
        }

        private void Update()
        {
            if (Time.deltaTime <= 0f || !IsOperational)
            {
                return;
            }

            _thinkRemaining -= Time.deltaTime;
            if (_thinkRemaining > 0f)
            {
                return;
            }

            _thinkRemaining = Mathf.Max(0.05f, _thinkInterval);
            TickBrain();
        }

        public void TickBrain()
        {
            CacheComponents();
            if (!IsOperational)
            {
                CancelIssuedAttack();
                CurrentTarget = null;
                return;
            }

            if (CurrentTarget != null &&
                !_scanner.IsValidVisibleHostile(CurrentTarget))
            {
                CancelIssuedAttack();
                CurrentTarget = null;
            }

            // Respect an externally owned command. EnemyAiDirector never commands
            // heroes, but scripted encounters may temporarily take control.
            if (_commands.HasCurrentCommand && _commands.Current != _issuedAttack)
            {
                return;
            }

            if (_issuedAttack != null && !_issuedAttack.Status.IsTerminal())
            {
                return;
            }

            _issuedAttack = null;
            if (!_scanner.FindNearestHostile(_acquisitionRadius, out EntityIdentity target))
            {
                CurrentTarget = null;
                return;
            }

            var attack = new AttackCommand(target);
            if (_commands.TryIssue(
                    attack,
                    false,
                    out CommandRejectionReason _))
            {
                CurrentTarget = target;
                _issuedAttack = attack;
            }
        }

        private void CancelIssuedAttack()
        {
            if (_issuedAttack != null && _commands != null &&
                _commands.Current == _issuedAttack)
            {
                _commands.StopAll();
            }
            _issuedAttack = null;
        }

        private void CacheComponents()
        {
            if (_identity == null) _identity = GetComponent<EntityIdentity>();
            if (_vitals == null) _vitals = GetComponent<VitalsComponent>();
            if (_scanner == null) _scanner = GetComponent<AutoTargetScanner>();
            if (_commands == null) _commands = GetComponent<UnitCommandQueue>();
        }
    }
}
