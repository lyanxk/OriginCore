using UnityEngine;

namespace OriginCore.RTS
{
    [DisallowMultipleComponent]
    public sealed class RtsCameraBounds : MonoBehaviour
    {
        [SerializeField] private Collider _boundsSource;
        [Min(0f), SerializeField] private float _padding = 1f;

        public Collider BoundsSource => _boundsSource;
        public float Padding => _padding;
        public bool IsConfigured => _boundsSource != null;
        public Bounds WorldBounds => _boundsSource != null
            ? _boundsSource.bounds
            : new Bounds(transform.position, Vector3.zero);

        public void Configure(Collider boundsSource, float padding)
        {
            _boundsSource = boundsSource;
            _padding = Mathf.Max(0f, padding);
        }

        public Vector3 ClampPosition(Vector3 worldPosition)
        {
            if (_boundsSource == null)
            {
                return worldPosition;
            }

            Bounds bounds = _boundsSource.bounds;
            float minX = bounds.min.x + _padding;
            float maxX = bounds.max.x - _padding;
            float minZ = bounds.min.z + _padding;
            float maxZ = bounds.max.z - _padding;
            if (minX > maxX)
            {
                minX = maxX = bounds.center.x;
            }

            if (minZ > maxZ)
            {
                minZ = maxZ = bounds.center.z;
            }

            worldPosition.x = Mathf.Clamp(worldPosition.x, minX, maxX);
            worldPosition.z = Mathf.Clamp(worldPosition.z, minZ, maxZ);
            return worldPosition;
        }

        public bool ContainsHorizontal(Vector3 worldPosition)
        {
            if (_boundsSource == null)
            {
                return false;
            }

            Vector3 clamped = ClampPosition(worldPosition);
            return Mathf.Approximately(clamped.x, worldPosition.x) &&
                   Mathf.Approximately(clamped.z, worldPosition.z);
        }
    }
}
