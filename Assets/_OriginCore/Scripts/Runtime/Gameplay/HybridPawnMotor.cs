using UnityEngine;

namespace OriginCore.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class HybridPawnMotor : MonoBehaviour
    {
        private const int QueryCapacity = 32;

        [SerializeField] private CharacterController _characterController;
        [SerializeField] private float _standingHeight = 2f;
        [SerializeField] private Vector3 _standingCenter = new Vector3(0f, 1f, 0f);
        [SerializeField] private bool _isCrouched;

        private readonly Collider[] _queryBuffer = new Collider[QueryCapacity];

        public CharacterController CharacterController => _characterController;
        public float StandingHeight => _standingHeight;
        public Vector3 StandingCenter => _standingCenter;
        public bool IsCrouched => _isCrouched;
        public bool IsGrounded { get; private set; }
        public Vector3 PlanarVelocity { get; private set; }
        public float VerticalVelocity { get; private set; }
        public CollisionFlags LastCollisionFlags { get; private set; }
        public Vector3 WorldCenter => _characterController != null
            ? transform.TransformPoint(_characterController.center)
            : transform.position;

        private void Awake()
        {
            CacheController();
            EnsureStandingGeometry();
        }

        private void OnValidate()
        {
            CacheController();
            EnsureStandingGeometry();
        }

        public void Configure(CharacterController characterController)
        {
            _characterController = characterController != null
                ? characterController
                : GetComponent<CharacterController>();
            if (_characterController == null)
            {
                return;
            }

            _isCrouched = false;
            _standingHeight = _characterController.height;
            _standingCenter = _characterController.center;
            ResetMotion();
        }

        public bool Move(
            Vector3 planarVelocity,
            float verticalVelocity,
            float deltaTime)
        {
            CacheController();
            if (_characterController == null || !_characterController.enabled || deltaTime <= 0f)
            {
                return false;
            }

            PlanarVelocity = Vector3.ProjectOnPlane(planarVelocity, Vector3.up);
            VerticalVelocity = verticalVelocity;
            Vector3 displacement =
                (PlanarVelocity + Vector3.up * VerticalVelocity) * deltaTime;
            LastCollisionFlags = _characterController.Move(displacement);

            if ((LastCollisionFlags & CollisionFlags.Above) != 0 && VerticalVelocity > 0f)
            {
                VerticalVelocity = 0f;
            }

            IsGrounded = _characterController.isGrounded ||
                         (LastCollisionFlags & CollisionFlags.Below) != 0;
            if (IsGrounded && VerticalVelocity < 0f)
            {
                VerticalVelocity = 0f;
            }

            return true;
        }

        public void RotateTowards(Vector3 worldDirection, float degreesPerSecond, float deltaTime)
        {
            Vector3 planarDirection = Vector3.ProjectOnPlane(worldDirection, Vector3.up);
            if (planarDirection.sqrMagnitude <= 0.000001f || deltaTime <= 0f)
            {
                return;
            }

            Quaternion target = Quaternion.LookRotation(planarDirection.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                target,
                Mathf.Max(0f, degreesPerSecond) * deltaTime);
        }

        public bool ProbeGrounded(float probeDistance, LayerMask groundMask)
        {
            CacheController();
            if (_characterController == null || !_characterController.enabled)
            {
                IsGrounded = false;
                return false;
            }

            if (_characterController.isGrounded)
            {
                IsGrounded = true;
                return true;
            }

            GetWorldCapsule(
                _characterController.height,
                _characterController.center,
                out Vector3 center,
                out Vector3 up,
                out float height,
                out float radius);
            Vector3 bottomSphereCenter = center - up * Mathf.Max(0f, height * 0.5f - radius);
            int count = Physics.OverlapSphereNonAlloc(
                bottomSphereCenter,
                radius + Mathf.Max(0f, probeDistance),
                _queryBuffer,
                groundMask.value,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                Collider candidate = _queryBuffer[i];
                if (candidate != null && !IsSelfCollider(candidate))
                {
                    IsGrounded = true;
                    return true;
                }
            }

            IsGrounded = false;
            return false;
        }

        public bool TrySetCrouched(
            bool crouched,
            float crouchHeight,
            LayerMask obstructionMask)
        {
            CacheController();
            if (_characterController == null)
            {
                return false;
            }

            EnsureStandingGeometry();
            if (crouched)
            {
                if (_isCrouched)
                {
                    return true;
                }

                float minimumHeight = _characterController.radius * 2f + 0.01f;
                ApplyHeight(Mathf.Clamp(crouchHeight, minimumHeight, _standingHeight));
                _isCrouched = true;
                return true;
            }

            if (!_isCrouched)
            {
                return true;
            }

            if (HasStandingObstruction(obstructionMask))
            {
                return false;
            }

            _characterController.height = _standingHeight;
            _characterController.center = _standingCenter;
            _isCrouched = false;
            return true;
        }

        public bool HasStandingObstruction(LayerMask obstructionMask)
        {
            CacheController();
            if (_characterController == null || !_isCrouched)
            {
                return false;
            }

            GetWorldCapsule(
                _characterController.height,
                _characterController.center,
                out Vector3 currentCenter,
                out Vector3 up,
                out float currentHeight,
                out float currentRadius);
            GetWorldCapsule(
                _standingHeight,
                _standingCenter,
                out Vector3 standingCenter,
                out _,
                out float standingHeight,
                out float standingRadius);

            Vector3 currentTop = currentCenter + up * (currentHeight * 0.5f);
            Vector3 standingTop = standingCenter + up * (standingHeight * 0.5f);
            float probeRadius = Mathf.Max(
                0.05f,
                Mathf.Min(currentRadius, standingRadius) - _characterController.skinWidth);
            Vector3 start = currentTop + up * _characterController.skinWidth;
            Vector3 end = standingTop - up * probeRadius;
            if (Vector3.Dot(end - start, up) < 0f)
            {
                end = start;
            }

            int count = Physics.OverlapCapsuleNonAlloc(
                start,
                end,
                probeRadius,
                _queryBuffer,
                obstructionMask.value,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider candidate = _queryBuffer[i];
                if (candidate != null && !IsSelfCollider(candidate))
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryFindNearestBoostContact(
            float radius,
            LayerMask boostMask,
            out Vector3 contactPoint,
            out Collider source)
        {
            contactPoint = default;
            source = null;
            if (radius <= 0f)
            {
                return false;
            }

            Vector3 center = WorldCenter;
            int count = Physics.OverlapSphereNonAlloc(
                center,
                radius,
                _queryBuffer,
                boostMask.value,
                QueryTriggerInteraction.Collide);
            float bestDistanceSquared = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                Collider candidate = _queryBuffer[i];
                if (candidate == null || IsSelfCollider(candidate) ||
                    !IsBoostCandidate(candidate))
                {
                    continue;
                }

                Vector3 candidatePoint = candidate.ClosestPoint(center);
                float distanceSquared = (candidatePoint - center).sqrMagnitude;
                if (distanceSquared >= bestDistanceSquared)
                {
                    continue;
                }

                bestDistanceSquared = distanceSquared;
                contactPoint = candidatePoint;
                source = candidate;
            }

            return source != null;
        }

        public void ResetMotion()
        {
            PlanarVelocity = Vector3.zero;
            VerticalVelocity = 0f;
            LastCollisionFlags = CollisionFlags.None;
            IsGrounded = false;
        }

        private void ApplyHeight(float targetHeight)
        {
            float bottom = _characterController.center.y - _characterController.height * 0.5f;
            Vector3 center = _characterController.center;
            center.y = bottom + targetHeight * 0.5f;
            _characterController.height = targetHeight;
            _characterController.center = center;
        }

        private bool IsBoostCandidate(Collider candidate)
        {
            int layer = candidate.gameObject.layer;
            int boostSurfaceLayer = LayerMask.NameToLayer("BoostSurface");
            int unitLayer = LayerMask.NameToLayer("Unit");
            if (layer == boostSurfaceLayer || layer == unitLayer)
            {
                return true;
            }

            EntityIdentity identity = candidate.GetComponentInParent<EntityIdentity>();
            return identity != null && identity.transform != transform &&
                   identity.HasAnyRole(UnitRole.Worker | UnitRole.Combat | UnitRole.Hero);
        }

        private bool IsSelfCollider(Collider candidate)
        {
            Transform candidateTransform = candidate.transform;
            return candidateTransform == transform || candidateTransform.IsChildOf(transform);
        }

        private void GetWorldCapsule(
            float localHeight,
            Vector3 localCenter,
            out Vector3 center,
            out Vector3 up,
            out float height,
            out float radius)
        {
            Vector3 scale = transform.lossyScale;
            float verticalScale = Mathf.Abs(scale.y);
            float radialScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            center = transform.TransformPoint(localCenter);
            up = transform.up;
            radius = _characterController.radius * radialScale;
            height = Mathf.Max(radius * 2f, localHeight * verticalScale);
        }

        private void CacheController()
        {
            if (_characterController == null)
            {
                _characterController = GetComponent<CharacterController>();
            }
        }

        private void EnsureStandingGeometry()
        {
            if (_characterController == null || _isCrouched)
            {
                return;
            }

            _standingHeight = Mathf.Max(
                _characterController.radius * 2f + 0.01f,
                _characterController.height);
            _standingCenter = _characterController.center;
        }
    }
}
