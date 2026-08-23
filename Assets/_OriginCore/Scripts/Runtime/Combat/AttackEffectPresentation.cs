using OriginCore.Content;
using OriginCore.Gameplay;
using OriginCore.Weapons;
using UnityEngine;
using UnityEngine.Rendering;

namespace OriginCore.Combat
{
    [DisallowMultipleComponent]
    public sealed class AttackEffectPresentation : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] private AttackCapability _attackCapability;
        [SerializeField] private WeaponController _weaponController;
        [SerializeField] private WeaponActionController _weaponActions;
        [SerializeField] private CombatHitboxResolver _hitboxResolver;
        [SerializeField] private FactionMember _faction;
        [SerializeField] private Transform _visualRoot;
        [SerializeField] private Material _effectMaterial;
        [Min(0.05f), SerializeField] private float _duration = 0.18f;
        [Min(0.01f), SerializeField] private float _beamWidth = 0.09f;
        [Min(0.05f), SerializeField] private float _impactStartScale = 0.16f;
        [Min(0.1f), SerializeField] private float _impactEndScale = 0.7f;
        [Min(0.5f), SerializeField] private float _previewDistance = 2.5f;

        private static Material _fallbackMaterial;
        private MaterialPropertyBlock _impactBlock;
        private LineRenderer _beam;
        private Transform _impact;
        private MeshRenderer _impactRenderer;
        private Vector3 _effectEnd;
        private Color _effectColor;
        private float _remaining;

        public int PlayCount { get; private set; }
        public bool IsPlaying => _remaining > 0f;
        public Material EffectMaterial => _effectMaterial;

        private void Awake()
        {
            CacheComponents();
            if (Application.isPlaying)
            {
                EnsureVisuals();
            }
        }

        private void OnEnable()
        {
            CacheComponents();
            if (Application.isPlaying)
            {
                EnsureVisuals();
            }

            BindEvents();
        }

        private void OnDisable()
        {
            UnbindEvents();
            StopEffect();
        }

        private void OnValidate()
        {
            _duration = Mathf.Max(0.05f, _duration);
            _beamWidth = Mathf.Max(0.01f, _beamWidth);
            _impactStartScale = Mathf.Max(0.05f, _impactStartScale);
            _impactEndScale = Mathf.Max(_impactStartScale, _impactEndScale);
            _previewDistance = Mathf.Max(0.5f, _previewDistance);
            CacheComponents();
        }

        private void Update()
        {
            if (_remaining <= 0f)
            {
                return;
            }

            float deltaTime = Mathf.Max(0f, Time.deltaTime);
            if (deltaTime <= 0f)
            {
                return;
            }

            _remaining = Mathf.Max(0f, _remaining - deltaTime);
            float progress = 1f - _remaining / _duration;
            float alpha = 1f - progress;
            Color faded = new Color(
                _effectColor.r,
                _effectColor.g,
                _effectColor.b,
                alpha);

            if (_beam != null)
            {
                _beam.startColor = faded;
                _beam.endColor = new Color(faded.r, faded.g, faded.b, 0f);
                _beam.widthMultiplier = Mathf.Lerp(_beamWidth, 0f, progress);
            }

            if (_impact != null)
            {
                _impact.position = _effectEnd;
                float scale = Mathf.Lerp(
                    _impactStartScale,
                    _impactEndScale,
                    progress);
                _impact.localScale = Vector3.one * scale;
            }

            if (_impactRenderer != null)
            {
                _impactBlock.Clear();
                _impactBlock.SetColor(BaseColorId, faded);
                _impactBlock.SetColor(ColorId, faded);
                _impactRenderer.SetPropertyBlock(_impactBlock);
            }

            if (_remaining <= 0f)
            {
                StopEffect();
            }
        }

        public void Configure(Material effectMaterial, Transform visualRoot = null)
        {
            UnbindEvents();
            _effectMaterial = effectMaterial;
            _visualRoot = visualRoot != null
                ? visualRoot
                : transform.Find("VisualRoot");
            CacheComponents();
            ApplyMaterial();
            if (isActiveAndEnabled)
            {
                BindEvents();
            }
        }

        public void Play(Vector3 start, Vector3 end)
        {
            if (!Application.isPlaying)
            {
                return;
            }

            EnsureVisuals();
            if (_beam == null || _impact == null || _impactRenderer == null)
            {
                return;
            }

            if ((end - start).sqrMagnitude <= 0.0001f)
            {
                end = start + transform.forward * _previewDistance;
            }

            _effectColor = ResolveFactionColor();
            _effectEnd = end;
            _remaining = _duration;
            PlayCount++;

            _beam.gameObject.SetActive(true);
            _beam.positionCount = 2;
            _beam.SetPosition(0, start);
            _beam.SetPosition(1, end);
            _beam.widthMultiplier = _beamWidth;
            _beam.startColor = _effectColor;
            _beam.endColor = new Color(
                _effectColor.r,
                _effectColor.g,
                _effectColor.b,
                0f);

            _impact.gameObject.SetActive(true);
            _impact.position = end;
            _impact.localScale = Vector3.one * _impactStartScale;
            _impactBlock.Clear();
            _impactBlock.SetColor(BaseColorId, _effectColor);
            _impactBlock.SetColor(ColorId, _effectColor);
            _impactRenderer.SetPropertyBlock(_impactBlock);
        }

        private void HandleAttackPerformed(
            AttackCapability source,
            EntityIdentity target,
            DamageResult result)
        {
            if (target != null && result.Changed)
            {
                Play(ResolveOrigin(source != null ? source.AttackOrigin : null),
                    ResolveTargetPoint(target));
            }
        }

        private void HandleShotFired(WeaponDefinition weapon)
        {
            Vector3 start = ResolveOrigin(null);
            Play(start, start + transform.forward * _previewDistance);
        }

        private void HandleActionStarted(
            WeaponActionController controller,
            WeaponDefinition weapon,
            WeaponActionDefinition action)
        {
            Vector3 start = ResolveOrigin(null);
            Play(start, start + transform.forward * _previewDistance);
        }

        private void HandleHitApplied(
            CombatHitboxResolver resolver,
            WeaponActionHitResult result)
        {
            if (result.Target != null)
            {
                Play(ResolveOrigin(null), ResolveTargetPoint(result.Target));
            }
        }

        private void BindEvents()
        {
            UnbindEvents();
            bool hasSpecializedWeaponVfx =
                GetComponent<WeaponSkillVfxPresentation>() != null;
            if (_attackCapability != null)
            {
                _attackCapability.AttackPerformed += HandleAttackPerformed;
            }

            if (_weaponController != null)
            {
                _weaponController.ShotFired += HandleShotFired;
            }

            if (_weaponActions != null && !hasSpecializedWeaponVfx)
            {
                _weaponActions.ActionStarted += HandleActionStarted;
            }

            if (_hitboxResolver != null && !hasSpecializedWeaponVfx)
            {
                _hitboxResolver.HitApplied += HandleHitApplied;
            }
        }

        private void UnbindEvents()
        {
            if (_attackCapability != null)
            {
                _attackCapability.AttackPerformed -= HandleAttackPerformed;
            }

            if (_weaponController != null)
            {
                _weaponController.ShotFired -= HandleShotFired;
            }

            if (_weaponActions != null)
            {
                _weaponActions.ActionStarted -= HandleActionStarted;
            }

            if (_hitboxResolver != null)
            {
                _hitboxResolver.HitApplied -= HandleHitApplied;
            }
        }

        private void CacheComponents()
        {
            if (_attackCapability == null)
            {
                _attackCapability = GetComponentInChildren<AttackCapability>(true);
            }

            if (_weaponController == null)
            {
                _weaponController = GetComponentInChildren<WeaponController>(true);
            }

            if (_weaponActions == null)
            {
                _weaponActions = GetComponentInChildren<WeaponActionController>(true);
            }

            if (_hitboxResolver == null)
            {
                _hitboxResolver = GetComponentInChildren<CombatHitboxResolver>(true);
            }

            if (_faction == null)
            {
                _faction = GetComponent<FactionMember>();
            }

            if (_visualRoot == null)
            {
                _visualRoot = transform.Find("VisualRoot");
            }
        }

        private void EnsureVisuals()
        {
            if (_impactBlock == null)
            {
                _impactBlock = new MaterialPropertyBlock();
            }

            if (_beam == null)
            {
                GameObject beamObject = new GameObject("AttackEffect_Beam");
                beamObject.layer = gameObject.layer;
                beamObject.transform.SetParent(transform, false);
                _beam = beamObject.AddComponent<LineRenderer>();
                _beam.useWorldSpace = true;
                _beam.loop = false;
                _beam.alignment = LineAlignment.View;
                _beam.textureMode = LineTextureMode.Stretch;
                _beam.numCapVertices = 2;
                _beam.numCornerVertices = 2;
                _beam.shadowCastingMode = ShadowCastingMode.Off;
                _beam.receiveShadows = false;
                _beam.gameObject.SetActive(false);
            }

            if (_impact == null)
            {
                GameObject impactObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                impactObject.name = "AttackEffect_Impact";
                impactObject.layer = gameObject.layer;
                impactObject.transform.SetParent(transform, false);
                Collider collider = impactObject.GetComponent<Collider>();
                if (collider != null)
                {
                    collider.enabled = false;
                    Destroy(collider);
                }

                _impact = impactObject.transform;
                _impactRenderer = impactObject.GetComponent<MeshRenderer>();
                _impactRenderer.shadowCastingMode = ShadowCastingMode.Off;
                _impactRenderer.receiveShadows = false;
                impactObject.SetActive(false);
            }

            ApplyMaterial();
        }

        private void ApplyMaterial()
        {
            Material material = _effectMaterial != null
                ? _effectMaterial
                : GetFallbackMaterial();
            if (_beam != null)
            {
                _beam.sharedMaterial = material;
            }

            if (_impactRenderer != null)
            {
                _impactRenderer.sharedMaterial = material;
            }
        }

        private Vector3 ResolveOrigin(Transform explicitOrigin)
        {
            if (explicitOrigin != null)
            {
                return explicitOrigin.position;
            }

            if (_visualRoot != null)
            {
                return _visualRoot.position;
            }

            return transform.position + Vector3.up * 0.75f;
        }

        private static Vector3 ResolveTargetPoint(EntityIdentity target)
        {
            Selectable selectable = target != null
                ? target.GetComponent<Selectable>()
                : null;
            return selectable != null
                ? selectable.GetSelectionWorldPoint()
                : target != null
                    ? target.transform.position + Vector3.up * 0.75f
                    : Vector3.zero;
        }

        private Color ResolveFactionColor()
        {
            if (_faction == null)
            {
                return new Color(1f, 0.85f, 0.25f, 1f);
            }

            switch (_faction.Faction)
            {
                case FactionId.Enemy:
                    return new Color(1f, 0.18f, 0.12f, 1f);
                case FactionId.Friendly:
                    return new Color(0.2f, 0.85f, 1f, 1f);
                default:
                    return new Color(1f, 0.85f, 0.25f, 1f);
            }
        }

        private void StopEffect()
        {
            _remaining = 0f;
            if (_beam != null)
            {
                _beam.gameObject.SetActive(false);
            }

            if (_impact != null)
            {
                _impact.gameObject.SetActive(false);
            }
        }

        private static Material GetFallbackMaterial()
        {
            if (_fallbackMaterial != null)
            {
                return _fallbackMaterial;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            if (shader == null)
            {
                return null;
            }

            _fallbackMaterial = new Material(shader)
            {
                name = "MAT_AttackEffect_RuntimeFallback",
                hideFlags = HideFlags.HideAndDontSave,
                renderQueue = (int)RenderQueue.Transparent
            };
            if (_fallbackMaterial.HasProperty("_Surface"))
            {
                _fallbackMaterial.SetFloat("_Surface", 1f);
                _fallbackMaterial.SetFloat("_ZWrite", 0f);
                _fallbackMaterial.SetFloat(
                    "_SrcBlend",
                    (float)BlendMode.SrcAlpha);
                _fallbackMaterial.SetFloat(
                    "_DstBlend",
                    (float)BlendMode.OneMinusSrcAlpha);
                _fallbackMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }

            return _fallbackMaterial;
        }
    }
}
