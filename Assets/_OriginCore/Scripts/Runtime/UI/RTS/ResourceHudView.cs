using OriginCore.Core;
using OriginCore.Economy;
using TMPro;
using UnityEngine;

namespace OriginCore.UI.RTS
{
    [DisallowMultipleComponent]
    public sealed class ResourceHudView : MonoBehaviour
    {
        private const float PreferredWidth = 500f;
        private const float PreferredHeight = 44f;
        private const float MinimumFontSize = 14f;
        private const float MaximumFontSize = 17f;
        private const float LeftTextPadding = 8f;
        private const float RightTextPadding = 18f;

        [SerializeField] private ResourceService _resourceService;
        [SerializeField] private ModeHudPresenter _modeHudPresenter;
        [SerializeField] private GameObject _contentRoot;
        [SerializeField] private TMP_Text _resourceLabel;

        private ResourceService _boundResourceService;
        private ModeHudPresenter _boundModeHudPresenter;

        public ResourceService ResourceService => _resourceService;
        public ModeHudPresenter ModeHudPresenter => _modeHudPresenter;
        public GameObject ContentRoot => _contentRoot;
        public TMP_Text ResourceLabel => _resourceLabel;

        private void OnEnable()
        {
            ApplyLayout();
            TryResolveResourceService();
            BindEvents();
            Refresh();
            ApplyMode(_modeHudPresenter != null ? _modeHudPresenter.Mode : GameMode.RTS);
        }

        private void Start()
        {
            if (_resourceService == null && TryResolveResourceService())
            {
                BindEvents();
                Refresh();
            }
        }

        private void OnDisable()
        {
            UnbindEvents();
        }

        public void Configure(
            ResourceService resourceService,
            ModeHudPresenter modeHudPresenter,
            GameObject contentRoot,
            TMP_Text resourceLabel)
        {
            UnbindEvents();
            _resourceService = resourceService;
            _modeHudPresenter = modeHudPresenter;
            _contentRoot = contentRoot;
            _resourceLabel = resourceLabel;
            ApplyLayout();
            if (isActiveAndEnabled)
            {
                TryResolveResourceService();
                BindEvents();
            }

            Refresh();
            ApplyMode(_modeHudPresenter != null ? _modeHudPresenter.Mode : GameMode.RTS);
        }

        public void Refresh()
        {
            if (_resourceLabel == null)
            {
                return;
            }

            ResourceSnapshot snapshot = _resourceService != null
                ? _resourceService.Snapshot
                : default(ResourceSnapshot);
            _resourceLabel.text =
                "COMMANDER  " + snapshot.CommanderResource + "    " +
                "CRYSTAL  " + snapshot.Crystal + "    " +
                "INFLUENCE  " + snapshot.InfluenceUsed + " / " +
                snapshot.InfluenceCap;
        }

        private bool TryResolveResourceService()
        {
            if (_resourceService != null)
            {
                return true;
            }

            if (!AppRoot.TryGetInstance(out AppRoot appRoot) || appRoot.Services == null)
            {
                return false;
            }

            _resourceService = appRoot.Services.ResourceService;
            return _resourceService != null;
        }

        private void BindEvents()
        {
            if (_boundResourceService != _resourceService)
            {
                if (_boundResourceService != null)
                {
                    _boundResourceService.ResourceChanged -= HandleResourceChanged;
                }

                _boundResourceService = _resourceService;
                if (_boundResourceService != null)
                {
                    _boundResourceService.ResourceChanged += HandleResourceChanged;
                }
            }

            if (_boundModeHudPresenter != _modeHudPresenter)
            {
                if (_boundModeHudPresenter != null)
                {
                    _boundModeHudPresenter.ModePresented -= HandleModePresented;
                }

                _boundModeHudPresenter = _modeHudPresenter;
                if (_boundModeHudPresenter != null)
                {
                    _boundModeHudPresenter.ModePresented += HandleModePresented;
                }
            }
        }

        private void UnbindEvents()
        {
            if (_boundResourceService != null)
            {
                _boundResourceService.ResourceChanged -= HandleResourceChanged;
                _boundResourceService = null;
            }

            if (_boundModeHudPresenter != null)
            {
                _boundModeHudPresenter.ModePresented -= HandleModePresented;
                _boundModeHudPresenter = null;
            }
        }

        private void HandleResourceChanged(ResourceChanged change)
        {
            Refresh();
        }

        private void HandleModePresented(GameMode mode)
        {
            ApplyMode(mode);
        }

        private void ApplyMode(GameMode mode)
        {
            if (_contentRoot != null && _contentRoot.activeSelf != (mode == GameMode.RTS))
            {
                _contentRoot.SetActive(mode == GameMode.RTS);
            }
        }

        private void ApplyLayout()
        {
            if (transform is RectTransform rootRect)
            {
                rootRect.anchorMin = Vector2.one;
                rootRect.anchorMax = Vector2.one;
                rootRect.pivot = Vector2.one;
                rootRect.anchoredPosition = Vector2.zero;
                rootRect.sizeDelta = new Vector2(PreferredWidth, PreferredHeight);
            }

            if (_contentRoot != null &&
                _contentRoot.transform is RectTransform contentRect)
            {
                contentRect.anchorMin = Vector2.zero;
                contentRect.anchorMax = Vector2.one;
                contentRect.offsetMin = Vector2.zero;
                contentRect.offsetMax = Vector2.zero;
            }

            if (_resourceLabel == null)
            {
                return;
            }

            RectTransform labelRect = _resourceLabel.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            _resourceLabel.alignment = TextAlignmentOptions.MidlineRight;
            _resourceLabel.margin = new Vector4(
                LeftTextPadding,
                0f,
                RightTextPadding,
                0f);
            _resourceLabel.enableAutoSizing = true;
            _resourceLabel.fontSizeMin = MinimumFontSize;
            _resourceLabel.fontSizeMax = MaximumFontSize;
        }
    }
}
