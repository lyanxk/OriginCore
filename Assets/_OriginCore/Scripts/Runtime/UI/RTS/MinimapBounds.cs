using UnityEngine;

namespace OriginCore.UI.RTS
{
    [DisallowMultipleComponent]
    public sealed class MinimapBounds : MonoBehaviour
    {
        [SerializeField] private Collider _worldBoundsSource;

        public Collider WorldBoundsSource => _worldBoundsSource;
        public Bounds WorldBounds => _worldBoundsSource != null
            ? _worldBoundsSource.bounds
            : new Bounds(transform.position, Vector3.zero);
        public bool IsConfigured => _worldBoundsSource != null;

        private void OnValidate()
        {
            if (_worldBoundsSource == null)
            {
                _worldBoundsSource = GetComponent<Collider>();
            }
        }

        public void Configure(Collider worldBoundsSource)
        {
            _worldBoundsSource = worldBoundsSource;
        }

        public Vector3 NormalizedToWorld(Vector2 normalized)
        {
            Bounds bounds = WorldBounds;
            float x = Mathf.Lerp(bounds.min.x, bounds.max.x, Mathf.Clamp01(normalized.x));
            float z = Mathf.Lerp(bounds.min.z, bounds.max.z, Mathf.Clamp01(normalized.y));
            return new Vector3(x, bounds.center.y, z);
        }

        public Vector2 WorldToNormalized(Vector3 worldPosition)
        {
            Bounds bounds = WorldBounds;
            float x = bounds.size.x > 0.0001f
                ? Mathf.InverseLerp(bounds.min.x, bounds.max.x, worldPosition.x)
                : 0.5f;
            float y = bounds.size.z > 0.0001f
                ? Mathf.InverseLerp(bounds.min.z, bounds.max.z, worldPosition.z)
                : 0.5f;
            return new Vector2(x, y);
        }
    }
}
