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

        [Header("States")]
        [SerializeField] string idleStateName = "ARM_Stickman|StandIdle_24f";
        [SerializeField] string moveStateName = "ARM_Stickman|WalkCycle_24f";

        [Header("Movement")]
        [Min(0f)] [SerializeField] float movingSpeedThreshold = 0.08f;
        [Min(0f)] [SerializeField] float crossFadeDuration = 0.12f;
        [SerializeField] bool scaleMovePlaybackSpeed = true;
        [Min(0.01f)] [SerializeField] float referenceMoveSpeed = 4.5f;
        [Min(0.01f)] [SerializeField] float minMovePlaybackSpeed = 0.85f;
        [Min(0.01f)] [SerializeField] float maxMovePlaybackSpeed = 1.35f;

        static readonly int SpeedHash = Animator.StringToHash("Speed");
        static readonly int IsMovingHash = Animator.StringToHash("IsMoving");

        Vector3 _lastPosition;
        string _currentStateName;
        bool _hasSpeedParameter;
        bool _hasIsMovingParameter;

        void Reset()
        {
            CacheReferences();
        }

        void Awake()
        {
            CacheReferences();
            ApplyAnimatorController();
            CacheAnimatorParameters();
            _lastPosition = transform.position;
        }

        void OnEnable()
        {
            _lastPosition = transform.position;
            _currentStateName = null;
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
        }

        void LateUpdate()
        {
            if (animator == null)
            {
                CacheReferences();
                ApplyAnimatorController();
                CacheAnimatorParameters();
            }

            if (animator == null)
            {
                enabled = false;
                return;
            }

            float planarSpeed = ResolvePlanarSpeed();
            bool isMoving = planarSpeed > movingSpeedThreshold;

            PlayState(isMoving ? moveStateName : idleStateName, crossFadeDuration);
            KeepCurrentStateLooping();
            SyncParameters(planarSpeed, isMoving);
            SyncPlaybackSpeed(planarSpeed, isMoving);
        }

        void CacheReferences()
        {
            if (motor == null)
                motor = GetComponent<UnitBase>();

            if (animator == null)
                animator = GetComponentInChildren<Animator>(true);
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

        void CacheAnimatorParameters()
        {
            _hasSpeedParameter = false;
            _hasIsMovingParameter = false;

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
            if (stateInfo.loop || !stateInfo.IsName(_currentStateName) || stateInfo.normalizedTime < 0.98f)
                return;

            animator.Play(_currentStateName, 0, 0f);
        }

        void SyncParameters(float planarSpeed, bool isMoving)
        {
            if (_hasSpeedParameter)
                animator.SetFloat(SpeedHash, planarSpeed);

            if (_hasIsMovingParameter)
                animator.SetBool(IsMovingHash, isMoving);
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
