using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OriginCore.Settings
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Volume))]
    public sealed class BrightnessImageEffect : MonoBehaviour
    {
        [SerializeField] private Volume _volume;

        private VolumeProfile _runtimeProfile;
        private ColorAdjustments _colorAdjustments;

        public Volume Volume => _volume;
        public float CurrentBrightness { get; private set; } = 1f;

        private void Awake()
        {
            if (_volume == null)
            {
                _volume = GetComponent<Volume>();
            }
        }

        private void OnDestroy()
        {
            if (_runtimeProfile != null)
            {
                for (int i = 0; i < _runtimeProfile.components.Count; i++)
                {
                    VolumeComponent component = _runtimeProfile.components[i];
                    if (component != null)
                    {
                        Destroy(component);
                    }
                }

                Destroy(_runtimeProfile);
                _runtimeProfile = null;
                _colorAdjustments = null;
            }
        }

        public void Configure(Volume volume)
        {
            _volume = volume != null ? volume : GetComponent<Volume>();
        }

        public bool Apply(float brightness)
        {
            CurrentBrightness = Mathf.Clamp(brightness, 0.5f, 1.5f);
            if (!EnsureRuntimeProfile())
            {
                return false;
            }

            _colorAdjustments.active = true;
            _colorAdjustments.postExposure.overrideState = true;
            _colorAdjustments.postExposure.value = Mathf.Log(CurrentBrightness, 2f);
            return true;
        }

        private bool EnsureRuntimeProfile()
        {
            if (_colorAdjustments != null)
            {
                return true;
            }

            if (_volume == null)
            {
                _volume = GetComponent<Volume>();
            }

            if (_volume == null)
            {
                return false;
            }

            // Volume.profile performs a deep runtime clone of every component. A plain
            // Instantiate(profile) would retain references to component sub-assets and
            // allow runtime brightness changes to mutate the project asset in memory.
            _runtimeProfile = _volume.profile;
            _runtimeProfile.name = "OriginCore Runtime Brightness";
            if (!_runtimeProfile.TryGet(out _colorAdjustments))
            {
                _colorAdjustments = _runtimeProfile.Add<ColorAdjustments>(true);
            }

            return _colorAdjustments != null;
        }
    }
}
