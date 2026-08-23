using UnityEngine;

namespace OriginCore.Abilities
{
    [DisallowMultipleComponent]
    public sealed class DirectMovementTraits : MonoBehaviour
    {
        [Min(0f), SerializeField] private float _directSpeedMultiplier = 1f;
        [Min(0f), SerializeField] private float _slideFrictionMultiplier = 1f;
        [SerializeField] private bool _ignoreLandingStun;

        public float DirectSpeedMultiplier => Mathf.Max(0f, _directSpeedMultiplier);
        public float SlideFrictionMultiplier => Mathf.Max(0f, _slideFrictionMultiplier);
        public bool IgnoreLandingStun => _ignoreLandingStun;

        private void OnValidate()
        {
            _directSpeedMultiplier = Mathf.Max(0f, _directSpeedMultiplier);
            _slideFrictionMultiplier = Mathf.Max(0f, _slideFrictionMultiplier);
        }
    }
}
