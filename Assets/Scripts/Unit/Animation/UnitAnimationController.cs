using Unit.Combat.Hero;
using Unit.Movement;
using UnityEngine;

namespace Unit.Animation
{
    [DisallowMultipleComponent]
    public class UnitAnimationController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] UnitBase motor;
        [SerializeField] HeroBase hero;
        [SerializeField] Animator animator;
        [SerializeField] RuntimeAnimatorController animatorController;
        [SerializeField] bool disableRootMotion = true;
        [SerializeField] Transform visualRoot;
        [SerializeField] float modelYawOffset = 0f;

        [Header("States")]
        [SerializeField] string idleStateName = "ARM_Stickman|StandIdle_24f";
        [SerializeField] string moveStateName = "ARM_Stickman|WalkCycle_24f";
        [SerializeField] string runStateName = "ARM_Stickman|RunCycle_16f";
        [SerializeField] string crouchStateName = "ARM_Stickman|CrouchDown_24f";
        [SerializeField] string crouchMoveStateName = "ARM_Stickman|CrouchWalk_24f";
        [SerializeField] string flightStateName = "ARM_Stickman|FlyForward_24f";
        [SerializeField] string holdingIdleStateName = "ARM_Stickman|AimGun_24f";
        [SerializeField] string holdingMoveStateName = "ARM_Stickman|AimWalk_24f";
        [SerializeField] string holdingRunStateName = "ARM_Stickman|AimRun_16f";
        [SerializeField] string holdingCrouchStateName = "ARM_Stickman|AimCrouchDown_24f";
        [SerializeField] string holdingCrouchMoveStateName = "ARM_Stickman|AimCrouchWalk_24f";
        [SerializeField] string holdingFlightStateName = "ARM_Stickman|AimFlyForward_24f";

        [Header("Movement")]
        [Min(0f)] [SerializeField] float movingSpeedThreshold = 0.08f;
        [Min(0f)] [SerializeField] float crossFadeDuration = 0.12f;

        static readonly int SpeedHash = Animator.StringToHash("Speed");
        static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
        static readonly int IsRunningHash = Animator.StringToHash("IsRunning");
        static readonly int IsCrouchingHash = Animator.StringToHash("IsCrouching");
        static readonly int IsSlidingHash = Animator.StringToHash("IsSliding");
        static readonly int IsFlyingHash = Animator.StringToHash("IsFlying");
        static readonly int IsHoldingGunHash = Animator.StringToHash("IsHoldingGun");

        Vector3 _lastPosition;
        Quaternion _visualRootBaseLocalRotation;
        string _currentStateName;
        bool _hasVisualRootBaseLocalRotation;
        bool _hasSpeedParameter;
        bool _hasIsMovingParameter;
        bool _hasIsRunningParameter;
        bool _hasIsCrouchingParameter;
        bool _hasIsSlidingParameter;
        bool _hasIsFlyingParameter;
        bool _hasIsHoldingGunParameter;

        void Reset()
        {
            CacheReferences();
        }

        void Awake()
        {
            CacheReferences();
            ApplyAnimatorController();
            CacheVisualRootBaseRotation();
            ApplyVisualYawOffset();
            CacheAnimatorParameters();
            _lastPosition = transform.position;
        }

        void OnEnable()
        {
            _lastPosition = transform.position;
            _currentStateName = null;
            CacheVisualRootBaseRotation();
            ApplyVisualYawOffset();
            PlayState(idleStateName, 0f);
        }

        void OnDisable()
        {
            if (animator != null)
                animator.speed = 1f;
        }

        void LateUpdate()
        {
            if (animator == null)
            {
                CacheReferences();
                ApplyAnimatorController();
                CacheVisualRootBaseRotation();
                CacheAnimatorParameters();
            }

            if (animator == null)
            {
                enabled = false;
                return;
            }

            float planarSpeed = ResolvePlanarSpeed();
            bool isMoving = planarSpeed > movingSpeedThreshold;
            bool isFlying = motor != null && motor.IsFlightEnabled;
            bool isSliding = motor != null && motor.IsSliding;
            bool isCrouching = motor != null && motor.IsCrouching;
            bool isRunning = !isFlying && !isSliding && !isCrouching && isMoving && motor != null && motor.IsRunning;
            bool isHoldingGun = hero != null &&
                                hero.CurrentWeapon != null &&
                                hero.CurrentWeapon.RangeType == HeroWeaponRangeType.Ranged;

            PlayState(ResolveLocomotionStateName(isMoving, isRunning, isCrouching, isFlying, isHoldingGun), crossFadeDuration);
            KeepCurrentStateLooping();
            SyncParameters(planarSpeed, isMoving, isRunning, isCrouching, isSliding, isFlying, isHoldingGun);
            ApplyVisualYawOffset();
        }

        void CacheReferences()
        {
            if (motor == null)
                motor = GetComponent<UnitBase>();

            if (hero == null)
                hero = GetComponent<HeroBase>();

            if (animator == null)
                animator = GetComponentInChildren<Animator>(true);

            if (visualRoot == null && animator != null)
                visualRoot = animator.transform;
        }

        void ApplyAnimatorController()
        {
            if (animator == null || animatorController == null)
                return;

            if (animator.runtimeAnimatorController != animatorController)
                animator.runtimeAnimatorController = animatorController;

            if (disableRootMotion)
                animator.applyRootMotion = false;
        }

        void CacheVisualRootBaseRotation()
        {
            if (visualRoot == null || _hasVisualRootBaseLocalRotation)
                return;

            _visualRootBaseLocalRotation = visualRoot.localRotation;
            _hasVisualRootBaseLocalRotation = true;
        }

        void CacheAnimatorParameters()
        {
            _hasSpeedParameter = false;
            _hasIsMovingParameter = false;
            _hasIsRunningParameter = false;
            _hasIsCrouchingParameter = false;
            _hasIsSlidingParameter = false;
            _hasIsFlyingParameter = false;
            _hasIsHoldingGunParameter = false;

            if (animator == null || animator.runtimeAnimatorController == null)
                return;

            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                AnimatorControllerParameter parameter = parameters[i];
                if (parameter.nameHash == SpeedHash && parameter.type == AnimatorControllerParameterType.Float)
                    _hasSpeedParameter = true;
                else if (parameter.nameHash == IsMovingHash && parameter.type == AnimatorControllerParameterType.Bool)
                    _hasIsMovingParameter = true;
                else if (parameter.nameHash == IsRunningHash && parameter.type == AnimatorControllerParameterType.Bool)
                    _hasIsRunningParameter = true;
                else if (parameter.nameHash == IsCrouchingHash && parameter.type == AnimatorControllerParameterType.Bool)
                    _hasIsCrouchingParameter = true;
                else if (parameter.nameHash == IsSlidingHash && parameter.type == AnimatorControllerParameterType.Bool)
                    _hasIsSlidingParameter = true;
                else if (parameter.nameHash == IsFlyingHash && parameter.type == AnimatorControllerParameterType.Bool)
                    _hasIsFlyingParameter = true;
                else if (parameter.nameHash == IsHoldingGunHash && parameter.type == AnimatorControllerParameterType.Bool)
                    _hasIsHoldingGunParameter = true;
            }
        }

        float ResolvePlanarSpeed()
        {
            float dt = Time.deltaTime;
            Vector3 currentPosition = transform.position;
            Vector3 delta = currentPosition - _lastPosition;
            _lastPosition = currentPosition;
            delta.y = 0f;

            float measuredSpeed = dt > 0f ? delta.magnitude / dt : 0f;
            float motorSpeed = motor != null ? motor.PlanarSpeed : 0f;
            return Mathf.Max(measuredSpeed, motorSpeed);
        }

        string ResolveLocomotionStateName(bool isMoving, bool isRunning, bool isCrouching, bool isFlying, bool isHoldingGun)
        {
            if (isHoldingGun)
                return ResolveHoldingLocomotionStateName(isMoving, isRunning, isCrouching, isFlying);

            if (isFlying && !string.IsNullOrWhiteSpace(flightStateName))
                return flightStateName;

            if (isCrouching)
            {
                if (isMoving && !string.IsNullOrWhiteSpace(crouchMoveStateName))
                    return crouchMoveStateName;

                if (!string.IsNullOrWhiteSpace(crouchStateName))
                    return crouchStateName;
            }

            if (!isMoving)
                return idleStateName;

            if (isRunning && !string.IsNullOrWhiteSpace(runStateName))
                return runStateName;

            return moveStateName;
        }

        string ResolveHoldingLocomotionStateName(bool isMoving, bool isRunning, bool isCrouching, bool isFlying)
        {
            if (isFlying && !string.IsNullOrWhiteSpace(holdingFlightStateName))
                return holdingFlightStateName;

            if (isCrouching)
            {
                if (isMoving && !string.IsNullOrWhiteSpace(holdingCrouchMoveStateName))
                    return holdingCrouchMoveStateName;

                if (!string.IsNullOrWhiteSpace(holdingCrouchStateName))
                    return holdingCrouchStateName;
            }

            if (!isMoving && !string.IsNullOrWhiteSpace(holdingIdleStateName))
                return holdingIdleStateName;

            if (isRunning && !string.IsNullOrWhiteSpace(holdingRunStateName))
                return holdingRunStateName;

            if (!string.IsNullOrWhiteSpace(holdingMoveStateName))
                return holdingMoveStateName;

            return ResolveLocomotionStateName(isMoving, isRunning, isCrouching, isFlying, false);
        }

        void PlayState(string stateName, float fadeDuration)
        {
            if (animator == null || string.IsNullOrWhiteSpace(stateName))
                return;

            if (_currentStateName == stateName)
                return;

            if (fadeDuration <= 0f)
                animator.Play(stateName, 0, 0f);
            else
                animator.CrossFadeInFixedTime(stateName, fadeDuration);

            _currentStateName = stateName;
        }

        void KeepCurrentStateLooping()
        {
            if (animator == null || string.IsNullOrWhiteSpace(_currentStateName) || animator.IsInTransition(0))
                return;

            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            if (IsHoldPoseState(_currentStateName))
                return;

            if (stateInfo.loop || !stateInfo.IsName(_currentStateName) || stateInfo.normalizedTime < 0.98f)
                return;

            animator.Play(_currentStateName, 0, 0f);
        }

        bool IsHoldPoseState(string stateName)
        {
            return (!string.IsNullOrWhiteSpace(crouchStateName) && stateName == crouchStateName) ||
                   (!string.IsNullOrWhiteSpace(holdingCrouchStateName) && stateName == holdingCrouchStateName);
        }

        void ApplyVisualYawOffset()
        {
            if (visualRoot == null)
                return;

            CacheVisualRootBaseRotation();
            visualRoot.localRotation = _visualRootBaseLocalRotation * Quaternion.Euler(0f, modelYawOffset, 0f);
        }

        void SyncParameters(float planarSpeed, bool isMoving, bool isRunning, bool isCrouching, bool isSliding, bool isFlying, bool isHoldingGun)
        {
            if (_hasSpeedParameter)
                animator.SetFloat(SpeedHash, planarSpeed);

            if (_hasIsMovingParameter)
                animator.SetBool(IsMovingHash, isMoving);

            if (_hasIsRunningParameter)
                animator.SetBool(IsRunningHash, isRunning);

            if (_hasIsCrouchingParameter)
                animator.SetBool(IsCrouchingHash, isCrouching);

            if (_hasIsSlidingParameter)
                animator.SetBool(IsSlidingHash, isSliding);

            if (_hasIsFlyingParameter)
                animator.SetBool(IsFlyingHash, isFlying);

            if (_hasIsHoldingGunParameter)
                animator.SetBool(IsHoldingGunHash, isHoldingGun);
        }

    }
}
