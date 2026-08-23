using System;
using UnityEngine;

namespace OriginCore.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(DamageReceiver))]
    public sealed class HitFlash : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] private DamageReceiver _damageReceiver;
        [SerializeField] private Renderer[] _renderers = Array.Empty<Renderer>();
        [Min(0.01f), SerializeField] private float _duration = 0.12f;
        [SerializeField] private Color _flashColor = Color.white;

        private MaterialPropertyBlock[] _originalBlocks = Array.Empty<MaterialPropertyBlock>();
        private MaterialPropertyBlock[] _flashBlocks = Array.Empty<MaterialPropertyBlock>();
        private float _remaining;
        private bool _isFlashing;

        public DamageReceiver DamageReceiver => _damageReceiver;
        public Renderer[] Renderers => _renderers;
        public float Duration => _duration;
        public Color FlashColor => _flashColor;
        public bool IsFlashing => _isFlashing;
        public int FlashCount { get; private set; }

        private void Awake()
        {
            CacheReceiver();
            CachePropertyBlocks();
        }

        private void OnEnable()
        {
            CacheReceiver();
            CachePropertyBlocks();
            BindEvents();
        }

        private void OnDisable()
        {
            UnbindEvents();
            RestoreOriginalBlocks();
        }

        private void OnValidate()
        {
            _duration = Mathf.Max(0.01f, _duration);
            CacheReceiver();
        }

        private void Update()
        {
            if (!_isFlashing)
            {
                return;
            }

            _remaining -= Mathf.Max(0f, Time.deltaTime);
            if (_remaining <= 0f)
            {
                RestoreOriginalBlocks();
            }
        }

        public void Configure(
            DamageReceiver damageReceiver,
            Renderer[] renderers,
            float duration,
            Color flashColor)
        {
            UnbindEvents();
            _damageReceiver = damageReceiver != null
                ? damageReceiver
                : GetComponent<DamageReceiver>();
            _renderers = renderers ?? Array.Empty<Renderer>();
            _duration = Mathf.Max(0.01f, duration);
            _flashColor = flashColor;
            CachePropertyBlocks();
            if (isActiveAndEnabled)
            {
                BindEvents();
            }
        }

        public void Flash()
        {
            if (_renderers == null || _renderers.Length == 0)
            {
                return;
            }

            if (_flashBlocks.Length != _renderers.Length)
            {
                CachePropertyBlocks();
            }

            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer targetRenderer = _renderers[i];
                if (targetRenderer == null)
                {
                    continue;
                }

                MaterialPropertyBlock block = _flashBlocks[i];
                targetRenderer.GetPropertyBlock(block);
                block.SetColor(BaseColorId, _flashColor);
                block.SetColor(ColorId, _flashColor);
                targetRenderer.SetPropertyBlock(block);
            }

            _remaining = _duration;
            _isFlashing = true;
            FlashCount++;
        }

        private void HandleDamageApplied(
            DamageReceiver receiver,
            DamageInfo damageInfo,
            DamageResult result)
        {
            if (result.Changed)
            {
                Flash();
            }
        }

        private void CacheReceiver()
        {
            if (_damageReceiver == null)
            {
                _damageReceiver = GetComponent<DamageReceiver>();
            }
        }

        private void CachePropertyBlocks()
        {
            int count = _renderers != null ? _renderers.Length : 0;
            _originalBlocks = new MaterialPropertyBlock[count];
            _flashBlocks = new MaterialPropertyBlock[count];
            for (int i = 0; i < count; i++)
            {
                _originalBlocks[i] = new MaterialPropertyBlock();
                _flashBlocks[i] = new MaterialPropertyBlock();
                if (_renderers[i] != null)
                {
                    _renderers[i].GetPropertyBlock(_originalBlocks[i]);
                    _renderers[i].GetPropertyBlock(_flashBlocks[i]);
                }
            }
        }

        private void RestoreOriginalBlocks()
        {
            if (_renderers != null && _originalBlocks.Length == _renderers.Length)
            {
                for (int i = 0; i < _renderers.Length; i++)
                {
                    if (_renderers[i] != null)
                    {
                        _renderers[i].SetPropertyBlock(_originalBlocks[i]);
                    }
                }
            }

            _remaining = 0f;
            _isFlashing = false;
        }

        private void BindEvents()
        {
            if (_damageReceiver != null)
            {
                _damageReceiver.DamageApplied -= HandleDamageApplied;
                _damageReceiver.DamageApplied += HandleDamageApplied;
            }
        }

        private void UnbindEvents()
        {
            if (_damageReceiver != null)
            {
                _damageReceiver.DamageApplied -= HandleDamageApplied;
            }
        }
    }
}
