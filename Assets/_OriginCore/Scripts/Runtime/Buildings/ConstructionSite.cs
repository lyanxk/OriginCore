using System;
using OriginCore.Combat;
using OriginCore.Content;
using OriginCore.Gameplay;
using UnityEngine;

namespace OriginCore.Buildings
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BuildingRuntime), typeof(VitalsComponent))]
    public sealed class ConstructionSite : MonoBehaviour
    {
        [SerializeField] private BuildingRuntime _buildingRuntime;
        [SerializeField] private VitalsComponent _vitals;
        [SerializeField] private bool _constructing;
        [Min(0f), SerializeField] private float _elapsedSeconds;
        [SerializeField] private ParticleSystem _constructionParticles;

        private Material _constructionParticleMaterial;

        public event Action<ConstructionSite> ConstructionCompleted;

        public bool IsConstructing => _constructing;
        public float ElapsedSeconds => Mathf.Max(0f, _elapsedSeconds);
        public float NormalizedProgress => _buildingRuntime != null &&
                                           _buildingRuntime.Definition != null
            ? Mathf.Clamp01(
                _elapsedSeconds / _buildingRuntime.Definition.ConstructionSeconds)
            : 0f;
        public ParticleSystem ConstructionParticles => _constructionParticles;

        private void Awake()
        {
            CacheComponents();
        }

        private void Update()
        {
            if (!_constructing || Time.deltaTime <= 0f || _buildingRuntime == null ||
                _buildingRuntime.Definition == null || _vitals == null || !_vitals.IsAlive)
            {
                return;
            }

            _elapsedSeconds = Mathf.Min(
                _buildingRuntime.Definition.ConstructionSeconds,
                _elapsedSeconds + Time.deltaTime);
            float progress = NormalizedProgress;
            float desiredHealth = Mathf.Lerp(1f, _vitals.MaxHealth, progress);
            _vitals.SetHealth(Mathf.Max(_vitals.Health, desiredHealth));
            if (progress >= 1f)
            {
                _constructing = false;
                _vitals.ResetToMaximum();
                _buildingRuntime.SetOperational(true);
                SetConstructionParticles(false, false);
                ConstructionCompleted?.Invoke(this);
            }
        }

        private void OnDisable()
        {
            SetConstructionParticles(false, true);
        }

        private void OnDestroy()
        {
            if (_constructionParticleMaterial != null)
            {
                Destroy(_constructionParticleMaterial);
            }
        }

        private void OnValidate()
        {
            CacheComponents();
            _elapsedSeconds = Mathf.Max(0f, _elapsedSeconds);
        }

        public void BeginConstruction(BuildingDefinition definition)
        {
            CacheComponents();
            _buildingRuntime.Configure(definition, false);
            UnitDefinition entity = definition != null ? definition.EntityDefinition : null;
            if (_vitals != null && entity != null)
            {
                _vitals.Configure(entity.MaxHealth, entity.MaxShield, entity.MaxEnergy, false);
                _vitals.SetHealth(1f);
                _vitals.SetShield(0f);
            }

            _elapsedSeconds = 0f;
            _constructing = definition != null;
            SetConstructionParticles(_constructing, true);
        }

        public void Restore(float elapsedSeconds, bool constructing)
        {
            _elapsedSeconds = Mathf.Max(0f, elapsedSeconds);
            _constructing = constructing;
            if (!_constructing && _buildingRuntime != null)
            {
                _buildingRuntime.SetOperational(true);
            }

            SetConstructionParticles(_constructing, true);
        }

        private void CacheComponents()
        {
            if (_buildingRuntime == null)
            {
                _buildingRuntime = GetComponent<BuildingRuntime>();
            }

            if (_vitals == null)
            {
                _vitals = GetComponent<VitalsComponent>();
            }
        }

        private void SetConstructionParticles(bool active, bool clear)
        {
            if (active)
            {
                EnsureConstructionParticles();
                if (_constructionParticles != null && !_constructionParticles.isPlaying)
                {
                    _constructionParticles.Play(true);
                }

                return;
            }

            if (_constructionParticles != null)
            {
                _constructionParticles.Stop(
                    true,
                    clear
                        ? ParticleSystemStopBehavior.StopEmittingAndClear
                        : ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private void EnsureConstructionParticles()
        {
            if (_constructionParticles != null)
            {
                return;
            }

            Transform existing = transform.Find("ConstructionParticles");
            if (existing != null)
            {
                _constructionParticles = existing.GetComponent<ParticleSystem>();
            }

            if (_constructionParticles != null)
            {
                return;
            }

            GameObject particleObject = new GameObject("ConstructionParticles");
            particleObject.transform.SetParent(transform, false);
            particleObject.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            _constructionParticles = particleObject.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = _constructionParticles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.86f, 0.94f, 1f, 0.72f),
                Color.white);
            main.maxParticles = 256;

            Vector2 footprint = _buildingRuntime != null &&
                                _buildingRuntime.Definition != null
                ? _buildingRuntime.Definition.Footprint
                : Vector2.one * 2f;
            ParticleSystem.ShapeModule shape = _constructionParticles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(
                Mathf.Max(0.5f, footprint.x * 0.9f),
                0.1f,
                Mathf.Max(0.5f, footprint.y * 0.9f));

            ParticleSystem.EmissionModule emission = _constructionParticles.emission;
            emission.enabled = true;
            emission.rateOverTime = Mathf.Clamp(footprint.x * footprint.y * 2f, 20f, 60f);

            ParticleSystem.VelocityOverLifetimeModule velocity =
                _constructionParticles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            // Unity requires the X/Y/Z velocity curves to share one mode.
            // Keep all three in Constant mode and let startSpeed provide variation.
            velocity.x = 0f;
            velocity.y = 0.9f;
            velocity.z = 0f;

            ParticleSystem.ColorOverLifetimeModule color =
                _constructionParticles.colorOverLifetime;
            color.enabled = true;
            Gradient alphaGradient = new Gradient();
            alphaGradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(new Color(0.84f, 0.94f, 1f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.92f, 0.18f),
                    new GradientAlphaKey(0f, 1f)
                });
            color.color = alphaGradient;

            ParticleSystemRenderer renderer =
                particleObject.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Particles/Standard Unlit");
            }

            if (shader != null)
            {
                _constructionParticleMaterial = new Material(shader)
                {
                    name = "Runtime_ConstructionWhiteParticles",
                    hideFlags = HideFlags.HideAndDontSave
                };
                if (_constructionParticleMaterial.HasProperty("_BaseColor"))
                {
                    _constructionParticleMaterial.SetColor("_BaseColor", Color.white);
                }

                if (_constructionParticleMaterial.HasProperty("_Color"))
                {
                    _constructionParticleMaterial.SetColor("_Color", Color.white);
                }

                renderer.sharedMaterial = _constructionParticleMaterial;
            }
        }
    }
}
