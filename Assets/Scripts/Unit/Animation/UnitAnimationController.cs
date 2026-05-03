using Unit.Movement;
using UnityEngine;

namespace Unit.Animation
{
    [DisallowMultipleComponent]
    public class UnitAnimationController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] UnitBase motor;
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

        [Header("Movement")]
        [Min(0f)] [SerializeField] float movingSpeedThreshold = 0.08f;
        [Min(0f)] [SerializeField] float runningSpeedThreshold = 3f;
        [Min(0f)] [SerializeField] float crossFadeDuration = 0.12f;
        [SerializeField] bool scaleMovePlaybackSpeed = true;
        [Min(0.01f)] [SerializeField] float referenceMoveSpeed = 4.5f;
        [Min(0.01f)] [SerializeField] float minMovePlaybackSpeed = 0.85f;
        [Min(0.01f)] [SerializeField] float maxMovePlaybackSpeed = 1.35f;

        static readonly int SpeedHash = Animator.StringToHash("Speed");
        static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
        static readonly int IsRunningHash = Animator.StringToHash("IsRunning");
        static readonly int IsCrouchingHash = Animator.StringToHash("IsCrouching");
        static readonly int IsFlyingHash = Animator.StringToHash("IsFlying");

        Vector3 _lastPosition;
        Quaternion _visualRootBaseLocalRotation;
        string _currentStateName;
        bool _hasVisualRootBaseLocalRotation;
        bool _hasSpeedParameter;
        bool _hasIsMovingParameter;
        bool _hasIsRunningParameter;
        bool _hasIsCrouchingParameter;
        bool _hasIsFlyingParameter;

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

        void OnValidate()
        {
            if (maxMovePlaybackSpeed < minMovePlaybackSpeed)
                maxMovePlaybackSpeed = minMovePlaybackSpeed;

            if (runningSpeedThreshold < movingSpeedThreshold)
                runningSpeedThreshold = movingSpeedThreshold;
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
            bool isCrouching = motor != null && motor.IsCrouching;
            bool isRunning = !isFlying && !isCrouching && isMoving && planarSpeed > ResolveRunningSpeedThreshold();

            PlayState(ResolveLocomotionStateName(isMoving, isRunning, isCrouching, isFlying), crossFadeDuration);
            KeepCurrentStateLooping();
            SyncParameters(planarSpeed, isMoving, isRunning, isCrouching, isFlying);
            SyncPlaybackSpeed(planarSpeed, isMoving);
            ApplyVisualYawOffset();
        }

        void CacheReferences()
        {
            if (motor == null)
                motor = GetComponent<UnitBase>();

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
            _hasIsFlyingParameter = false;

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
                else if (parameter.nameHash == IsFlyingHash && parameter.type == AnimatorControllerParameterType.Bool)
                    _hasIsFlyingParameter = true;
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

        float ResolveRunningSpeedThreshold()
        {
            if (motor == null || motor.runSpeed <= motor.walkSpeed)
                return runningSpeedThreshold;

            return Mathf.Lerp(motor.walkSpeed, motor.runSpeed, 0.5f);
        }

        string ResolveLocomotionStateName(bool isMoving, bool isRunning, bool isCrouching, bool isFlying)
        {
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
            return !string.IsNullOrWhiteSpace(crouchStateName) && stateName == crouchStateName;
        }

        void ApplyVisualYawOffset()
        {
            if (visualRoot == null)
                return;

            CacheVisualRootBaseRotation();
            visualRoot.localRotation = _visualRootBaseLocalRotation * Quaternion.Euler(0f, modelYawOffset, 0f);
        }

        void SyncParameters(float planarSpeed, bool isMoving, bool isRunning, bool isCrouching, bool isFlying)
        {
            if (_hasSpeedParameter)
                animator.SetFloat(SpeedHash, planarSpeed);

            if (_hasIsMovingParameter)
                animator.SetBool(IsMovingHash, isMoving);

            if (_hasIsRunningParameter)
                animator.SetBool(IsRunningHash, isRunning);

            if (_hasIsCrouchingParameter)
                animator.SetBool(IsCrouchingHash, isCrouching);

            if (_hasIsFlyingParameter)
                animator.SetBool(IsFlyingHash, isFlying);
        }

        void SyncPlaybackSpeed(float planarSpeed, bool isMoving)
        {
            if (!scaleMovePlaybackSpeed || !isMoving)
            {
                animator.speed = 1f;
                return;
            }

            float normalizedSpeed = planarSpeed / Mathf.Max(0.01f, referenceMoveSpeed);
            animator.speed = Mathf.Clamp(normalizedSpeed, minMovePlaybackSpeed, maxMovePlaybackSpeed);
        }
    }
}
