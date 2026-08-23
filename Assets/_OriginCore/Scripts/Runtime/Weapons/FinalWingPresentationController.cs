using System;
using OriginCore.Abilities;
using OriginCore.Combat;
using OriginCore.Content;
using UnityEngine;

namespace OriginCore.Weapons
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WeaponInventory))]
    public sealed class FinalWingPresentationController : MonoBehaviour
    {
        private const int FeatherCount = 6;

        [SerializeField] private WeaponInventory _inventory;
        [SerializeField] private HeroFormStateMachine _forms;
        [SerializeField] private CombatHitboxResolver _hitboxes;
        [SerializeField] private WeaponActionController _actions;
        [SerializeField] private Vector3 _rootOffset = new Vector3(0f, 1.25f, -0.45f);
        [SerializeField] private Color _prototypeColor =
            new Color(0.82f, 0.93f, 1f, 1f);

        private readonly Transform[] _feathers = new Transform[FeatherCount];
        private readonly Vector3[] _homePositions = new Vector3[FeatherCount];
        private readonly Quaternion[] _homeRotations = new Quaternion[FeatherCount];
        private readonly float[] _pulses = new float[FeatherCount];
        private Transform _visualRoot;
        private Material _runtimeMaterial;
        private int _nextPulse;
        private bool _actionExpanded;

        public bool IsVisible => _visualRoot != null &&
                                 _visualRoot.gameObject.activeSelf;

        private void Awake()
        {
            CacheComponents();
        }

        private void OnEnable()
        {
            CacheComponents();
            EnsureVisuals();
            BindEvents();
            RefreshVisibility();
        }

        private void OnDisable()
        {
            UnbindEvents();
            _actionExpanded = false;
            if (_visualRoot != null)
            {
                _visualRoot.gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (_runtimeMaterial != null)
            {
                Destroy(_runtimeMaterial);
                _runtimeMaterial = null;
            }
        }

        private void OnValidate()
        {
            CacheComponents();
        }

        private void Update()
        {
            RefreshVisibility();
            if (!IsVisible)
            {
                return;
            }

            float time = Time.time;
            for (int i = 0; i < FeatherCount; i++)
            {
                Transform feather = _feathers[i];
                if (feather == null)
                {
                    continue;
                }

                _pulses[i] = Mathf.Max(0f, _pulses[i] - Time.deltaTime * 5f);
                float side = i < 3 ? -1f : 1f;
                float expanded = _actionExpanded ? 0.22f : 0f;
                float bob = Mathf.Sin(time * 2.2f + i * 0.7f) * 0.025f;
                Vector3 targetPosition = _homePositions[i] +
                    new Vector3(side * expanded, bob, -expanded * 0.3f);
                feather.localPosition = Vector3.Lerp(
                    feather.localPosition,
                    targetPosition,
                    1f - Mathf.Exp(-12f * Time.deltaTime));
                feather.localRotation = Quaternion.Slerp(
                    feather.localRotation,
                    _homeRotations[i],
                    1f - Mathf.Exp(-12f * Time.deltaTime));
                float pulse = 1f + _pulses[i] * 0.55f;
                feather.localScale = new Vector3(0.13f, 0.62f, 0.055f) * pulse;
            }
        }

        private void EnsureVisuals()
        {
            if (_visualRoot != null)
            {
                return;
            }

            var root = new GameObject("FinalWing_PrototypeVisual");
            root.layer = gameObject.layer;
            _visualRoot = root.transform;
            _visualRoot.SetParent(transform, false);
            _visualRoot.localPosition = _rootOffset;
            _visualRoot.localRotation = Quaternion.identity;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader != null)
            {
                _runtimeMaterial = new Material(shader)
                {
                    name = "FinalWing Prototype Runtime Material",
                    hideFlags = HideFlags.DontSave
                };
                if (_runtimeMaterial.HasProperty("_BaseColor"))
                {
                    _runtimeMaterial.SetColor("_BaseColor", _prototypeColor);
                }
                if (_runtimeMaterial.HasProperty("_Color"))
                {
                    _runtimeMaterial.SetColor("_Color", _prototypeColor);
                }
            }

            for (int i = 0; i < FeatherCount; i++)
            {
                int tier = i % 3;
                float side = i < 3 ? -1f : 1f;
                GameObject featherObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                featherObject.name = "Feather_" + (i + 1);
                featherObject.layer = gameObject.layer;
                Collider collider = featherObject.GetComponent<Collider>();
                if (collider != null)
                {
                    collider.enabled = false;
                    Destroy(collider);
                }
                Renderer renderer = featherObject.GetComponent<Renderer>();
                if (renderer != null && _runtimeMaterial != null)
                {
                    renderer.sharedMaterial = _runtimeMaterial;
                }

                Transform feather = featherObject.transform;
                feather.SetParent(_visualRoot, false);
                _homePositions[i] = new Vector3(
                    side * (0.36f + tier * 0.22f),
                    0.48f - tier * 0.43f,
                    -tier * 0.08f);
                _homeRotations[i] = Quaternion.Euler(
                    10f + tier * 6f,
                    0f,
                    side * (18f + tier * 13f));
                feather.localPosition = _homePositions[i];
                feather.localRotation = _homeRotations[i];
                feather.localScale = new Vector3(0.13f, 0.62f, 0.055f);
                _feathers[i] = feather;
            }
        }

        private void RefreshVisibility()
        {
            if (_visualRoot == null)
            {
                return;
            }

            WeaponDefinition auxiliary = _inventory != null
                ? _inventory.ActAuxiliary
                : null;
            bool visible = auxiliary != null && _forms != null &&
                           string.Equals(
                               auxiliary.ContentId,
                               BuiltInWeaponActionLibrary.FinalWingWeaponId,
                               StringComparison.Ordinal) &&
                           string.Equals(
                               _forms.CurrentFormId,
                               "form.y.final",
                               StringComparison.Ordinal);
            if (_visualRoot.gameObject.activeSelf != visible)
            {
                _visualRoot.gameObject.SetActive(visible);
            }
        }

        private void HandleHitApplied(
            CombatHitboxResolver resolver,
            WeaponActionHitResult result)
        {
            if (result.Weapon == null || !string.Equals(
                    result.Weapon.ContentId,
                    BuiltInWeaponActionLibrary.FinalWingWeaponId,
                    StringComparison.Ordinal))
            {
                return;
            }

            int index = _nextPulse++ % FeatherCount;
            _pulses[index] = 1f;
        }

        private void HandleActionStarted(
            WeaponActionController controller,
            WeaponDefinition weapon,
            WeaponActionDefinition action)
        {
            _actionExpanded = weapon != null && string.Equals(
                weapon.ContentId,
                BuiltInWeaponActionLibrary.FinalWingWeaponId,
                StringComparison.Ordinal);
        }

        private void HandleActionEnded(
            WeaponActionController controller,
            WeaponDefinition weapon,
            WeaponActionDefinition action,
            WeaponActionEndReason reason)
        {
            if (weapon != null && string.Equals(
                    weapon.ContentId,
                    BuiltInWeaponActionLibrary.FinalWingWeaponId,
                    StringComparison.Ordinal))
            {
                _actionExpanded = false;
            }
        }

        private void BindEvents()
        {
            UnbindEvents();
            if (_hitboxes != null) _hitboxes.HitApplied += HandleHitApplied;
            if (_actions != null)
            {
                _actions.ActionStarted += HandleActionStarted;
                _actions.ActionEnded += HandleActionEnded;
            }
        }

        private void UnbindEvents()
        {
            if (_hitboxes != null) _hitboxes.HitApplied -= HandleHitApplied;
            if (_actions != null)
            {
                _actions.ActionStarted -= HandleActionStarted;
                _actions.ActionEnded -= HandleActionEnded;
            }
        }

        private void CacheComponents()
        {
            if (_inventory == null) _inventory = GetComponent<WeaponInventory>();
            if (_forms == null) _forms = GetComponent<HeroFormStateMachine>();
            if (_hitboxes == null) _hitboxes = GetComponent<CombatHitboxResolver>();
            if (_actions == null) _actions = GetComponent<WeaponActionController>();
        }
    }
}
