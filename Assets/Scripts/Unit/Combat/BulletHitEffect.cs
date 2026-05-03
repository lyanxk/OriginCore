using UnityEngine;

namespace Unit.Combat
{
    [DisallowMultipleComponent]
    public class BulletHitEffect : MonoBehaviour
    {
        [Min(0.01f)] [SerializeField] float lifetime = 1.25f;
        [SerializeField] bool destroyWhenComplete = true;
        [SerializeField] bool orientForwardToSurfaceNormal = true;

        ParticleSystem[] _particles;
        Light[] _lights;
        float[] _lightIntensities;
        float _elapsed;
        bool _isPlaying;

        void Awake()
        {
            CacheComponents();
        }

        void OnEnable()
        {
            Play(transform.position, transform.forward);
        }

        void Update()
        {
            if (!_isPlaying)
                return;

            _elapsed += Time.deltaTime;
            FadeLights();

            if (_elapsed < lifetime || AnyParticlesAlive())
                return;

            _isPlaying = false;
            if (destroyWhenComplete)
                Destroy(gameObject);
        }

        public void Play(Vector3 position, Vector3 surfaceNormal)
        {
            CacheComponents();

            transform.position = position;
            if (orientForwardToSurfaceNormal && surfaceNormal.sqrMagnitude > 1e-6f)
                transform.rotation = Quaternion.LookRotation(surfaceNormal.normalized, Vector3.up);

            _elapsed = 0f;
            _isPlaying = true;
            RestoreLights();

            for (int i = 0; i < _particles.Length; i++)
            {
                ParticleSystem ps = _particles[i];
                if (ps == null)
                    continue;

                ps.Clear(true);
                ps.Play(true);
            }
        }

        void CacheComponents()
        {
            _particles ??= GetComponentsInChildren<ParticleSystem>(true);
            _lights ??= GetComponentsInChildren<Light>(true);

            if (_lightIntensities != null && _lightIntensities.Length == _lights.Length)
                return;

            _lightIntensities = new float[_lights.Length];
            for (int i = 0; i < _lights.Length; i++)
                _lightIntensities[i] = _lights[i] != null ? _lights[i].intensity : 0f;
        }

        void RestoreLights()
        {
            for (int i = 0; i < _lights.Length; i++)
            {
                Light lightComponent = _lights[i];
                if (lightComponent == null)
                    continue;

                lightComponent.enabled = true;
                lightComponent.intensity = _lightIntensities[i];
            }
        }

        void FadeLights()
        {
            if (_lights == null || _lightIntensities == null)
                return;

            float t = lifetime > 0f ? Mathf.Clamp01(_elapsed / lifetime) : 1f;
            float intensityScale = 1f - t;
            for (int i = 0; i < _lights.Length; i++)
            {
                Light lightComponent = _lights[i];
                if (lightComponent == null)
                    continue;

                lightComponent.intensity = _lightIntensities[i] * intensityScale;
                if (t >= 1f)
                    lightComponent.enabled = false;
            }
        }

        bool AnyParticlesAlive()
        {
            if (_particles == null)
                return false;

            for (int i = 0; i < _particles.Length; i++)
            {
                ParticleSystem ps = _particles[i];
                if (ps != null && ps.IsAlive(true))
                    return true;
            }

            return false;
        }
    }
}
