using Core;
using Unit.Command;
using Unit.Movement;
using UnityEngine;

namespace Unit.Combat.Hero
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnitBase))]
    [RequireComponent(typeof(UnitCombat))]
    public sealed class HeroBase : MonoBehaviour
    {
        public enum PerspectiveOption
        {
            None = 0,
            ThirdPersonOnly = 1,
            FirstPersonOnly = 2,
            FirstAndThirdPerson = 3
        }

        [Header("Perspective")]
        [SerializeField] PerspectiveOption perspectiveOption = PerspectiveOption.FirstAndThirdPerson;
        [SerializeField] Transform thirdPersonPivot;
        [SerializeField] Transform firstPersonPivot;

        [Header("Auto Attack")]
        [SerializeField] UnitCombat combat;
        [SerializeField] UnitBase motor;
        [SerializeField] CommandExecutor commandExecutor;
        [Min(0.05f)] public float scanInterval = 0.15f;

        Transform _target;
        float _scanTimer;

        public bool HasThirdPersonView =>
            perspectiveOption == PerspectiveOption.ThirdPersonOnly ||
            perspectiveOption == PerspectiveOption.FirstAndThirdPerson;
        public bool HasFirstPersonView =>
            perspectiveOption == PerspectiveOption.FirstPersonOnly ||
            perspectiveOption == PerspectiveOption.FirstAndThirdPerson;
        public Transform ThirdPersonPivot => thirdPersonPivot;
        public Transform FirstPersonPivot => firstPersonPivot;

        void Awake()
        {
            CacheReferences();
            DisableCombatAutoChase();
        }

        void OnEnable()
        {
            CacheReferences();
            DisableCombatAutoChase();
        }

        void OnValidate()
        {
            CacheReferences();
            DisableCombatAutoChase();
        }

        void Update()
        {
            if (!CanAutoAttack())
            {
                ClearTarget();
                return;
            }

            if (!combat.IsTargetInRange(_target))
            {
                _target = null;
                _scanTimer = 0f;
            }

            _scanTimer -= Time.deltaTime;
            if (_target == null && _scanTimer <= 0f)
            {
                _scanTimer = scanInterval;
                _target = combat.FindNearestTargetInAttackRange();
            }

            if (_target == null)
                return;

            FaceTarget(_target.position);
            combat.TryUsePrimaryOnTarget(_target);
        }

        bool CanAutoAttack()
        {
            if (combat == null || motor == null)
                return false;

            if (commandExecutor != null && !commandExecutor.IsIdle)
                return false;

            ControlModeManager manager = ControlModeManager.Instance;
            return manager == null || manager.unit != motor || manager.CurrentModeName == "RTS";
        }

        void CacheReferences()
        {
            combat = ResolveNearbyComponent(combat);
            motor = ResolveNearbyComponent(motor);
            commandExecutor = ResolveNearbyComponent(commandExecutor);
        }

        void DisableCombatAutoChase()
        {
            if (combat != null)
                combat.autoChaseTargets = false;
        }

        void ClearTarget()
        {
            _target = null;
            _scanTimer = 0f;
        }

        void FaceTarget(Vector3 targetPosition)
        {
            Vector3 direction = targetPosition - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 1e-6f)
                return;

            motor.SetYaw(Quaternion.LookRotation(direction, Vector3.up).eulerAngles.y);
        }

        T ResolveNearbyComponent<T>(T current) where T : Component
        {
            if (current != null)
                return current;

            T resolved = GetComponent<T>();
            if (resolved != null)
                return resolved;

            return GetComponentInParent<T>();
        }
    }
}
