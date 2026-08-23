using System.Text;
using OriginCore.Buildings;
using OriginCore.Core;
using OriginCore.Gameplay;
using OriginCore.Matches;
using OriginCore.RTS;
using TMPro;
using UnityEngine;
using Image = UnityEngine.UI.Image;

namespace OriginCore.UI.RTS
{
    [DisallowMultipleComponent]
    public sealed class SelectionSummaryView : MonoBehaviour
    {
        [SerializeField] private SelectionService _selectionService;
        [SerializeField] private ModeHudPresenter _modeHudPresenter;
        [SerializeField] private GameObject _contentRoot;
        [SerializeField] private TMP_Text _countLabel;
        [SerializeField] private TMP_Text _entriesLabel;
        [SerializeField] private GameObject _constructionProgressRoot;
        [SerializeField] private Image _constructionProgressFill;
        [Min(1), SerializeField] private int _maximumEntries = 4;

        private readonly StringBuilder _builder = new StringBuilder(160);
        private SelectionService _boundSelectionService;
        private ModeHudPresenter _boundModeHudPresenter;
        private bool _commandInfoActive;
        private string _commandTitle = string.Empty;
        private string _commandDetail = string.Empty;
        private float _nextLiveRefreshTime;

        public SelectionService SelectionService => _selectionService;
        public ModeHudPresenter ModeHudPresenter => _modeHudPresenter;
        public GameObject ContentRoot => _contentRoot;
        public TMP_Text CountLabel => _countLabel;
        public TMP_Text EntriesLabel => _entriesLabel;
        public int MaximumEntries => _maximumEntries;

        private void OnEnable()
        {
            BindEvents();
            Refresh();
            ApplyMode(_modeHudPresenter != null ? _modeHudPresenter.Mode : GameMode.RTS);
        }

        private void OnDisable()
        {
            UnbindEvents();
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextLiveRefreshTime ||
                _selectionService == null || _selectionService.SelectedCount != 1)
            {
                return;
            }

            _nextLiveRefreshTime = Time.unscaledTime + 0.2f;
            Refresh();
        }

        public void Configure(
            SelectionService selectionService,
            ModeHudPresenter modeHudPresenter,
            GameObject contentRoot,
            TMP_Text countLabel,
            TMP_Text entriesLabel,
            int maximumEntries = 4)
        {
            UnbindEvents();
            _selectionService = selectionService;
            _modeHudPresenter = modeHudPresenter;
            _contentRoot = contentRoot;
            _countLabel = countLabel;
            _entriesLabel = entriesLabel;
            _maximumEntries = Mathf.Max(1, maximumEntries);
            if (isActiveAndEnabled)
            {
                BindEvents();
            }

            Refresh();
            ApplyMode(_modeHudPresenter != null ? _modeHudPresenter.Mode : GameMode.RTS);
        }

        public void Refresh()
        {
            OriginCoreUiTheme.ApplyUiFont(_countLabel);
            OriginCoreUiTheme.ApplyUiFont(_entriesLabel);
            ConfigureReadableText();
            if (_commandInfoActive)
            {
                SetConstructionProgress(null);
                if (_countLabel != null)
                {
                    _countLabel.text = _commandTitle;
                }

                if (_entriesLabel != null)
                {
                    _entriesLabel.text = _commandDetail;
                }

                return;
            }

            int selectedCount = _selectionService != null
                ? _selectionService.SelectedCount
                : 0;
            if (_countLabel != null)
            {
                _countLabel.text = "INFORMATION";
            }

            if (_entriesLabel == null)
            {
                return;
            }

            _builder.Length = 0;
            SetConstructionProgress(null);
            if (_selectionService == null)
            {
                _builder.Append("Information unavailable");
            }
            else if (selectedCount == 1)
            {
                AppendEntityInformation(_selectionService.Selected[0]);
            }
            else
            {
                if (selectedCount > 1)
                {
                    _builder.Append(selectedCount);
                    _builder.Append(IsBuilding(_selectionService.Selected[0])
                        ? " BUILDINGS"
                        : " UNITS");
                }

                int visibleCount = Mathf.Min(selectedCount, _maximumEntries);
                for (int i = 0; i < visibleCount; i++)
                {
                    Selectable selectable = _selectionService.Selected[i];
                    if (_builder.Length > 0)
                    {
                        _builder.Append('\n');
                    }

                    _builder.Append("• ");
                    _builder.Append(GetDisplayName(selectable));
                }

                if (selectedCount > visibleCount)
                {
                    if (_builder.Length > 0)
                    {
                        _builder.Append('\n');
                    }

                    _builder.Append("+ ");
                    _builder.Append(selectedCount - visibleCount);
                    _builder.Append(" more");
                }

                if (_selectionService.Inspected != null)
                {
                    if (_builder.Length > 0)
                    {
                        _builder.Append('\n');
                    }

                    _builder.Append("INSPECT: ");
                    _builder.Append(GetDisplayName(_selectionService.Inspected));
                }

                if (_builder.Length == 0)
                {
                    _builder.Append("No active selection");
                }
            }

            _entriesLabel.text = _builder.ToString();
        }

        public void ShowCommandInfo(string title, string detail)
        {
            _commandInfoActive = true;
            _commandTitle = string.IsNullOrWhiteSpace(title)
                ? "COMMAND"
                : title.Trim().ToUpperInvariant();
            _commandDetail = string.IsNullOrWhiteSpace(detail)
                ? "No additional information."
                : detail.Trim();
            Refresh();
        }

        public void ClearCommandInfo()
        {
            if (!_commandInfoActive)
            {
                return;
            }

            _commandInfoActive = false;
            _commandTitle = string.Empty;
            _commandDetail = string.Empty;
            Refresh();
        }

        private void BindEvents()
        {
            if (_selectionService != null && _boundSelectionService != _selectionService)
            {
                if (_boundSelectionService != null)
                {
                    _boundSelectionService.SelectionChanged -= HandleSelectionChanged;
                }

                _boundSelectionService = _selectionService;
                _boundSelectionService.SelectionChanged += HandleSelectionChanged;
            }

            if (_modeHudPresenter != null && _boundModeHudPresenter != _modeHudPresenter)
            {
                if (_boundModeHudPresenter != null)
                {
                    _boundModeHudPresenter.ModePresented -= HandleModePresented;
                }

                _boundModeHudPresenter = _modeHudPresenter;
                _boundModeHudPresenter.ModePresented += HandleModePresented;
            }
        }

        private void UnbindEvents()
        {
            if (_boundSelectionService != null)
            {
                _boundSelectionService.SelectionChanged -= HandleSelectionChanged;
                _boundSelectionService = null;
            }

            if (_boundModeHudPresenter != null)
            {
                _boundModeHudPresenter.ModePresented -= HandleModePresented;
                _boundModeHudPresenter = null;
            }
        }

        private void HandleSelectionChanged()
        {
            _commandInfoActive = false;
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

        private static string GetDisplayName(Selectable selectable)
        {
            return selectable != null && selectable.Identity != null
                ? selectable.Identity.DisplayName
                : "Unknown";
        }

        private void AppendEntityInformation(Selectable selectable)
        {
            _builder.Append(GetDisplayName(selectable));
            if (selectable == null)
            {
                return;
            }

            _builder.Append('\n');
            _builder.Append(IsBuilding(selectable) ? "BUILDING" : "UNIT");
            if (selectable.Vitals != null)
            {
                _builder.Append("   HP ");
                _builder.Append(Mathf.CeilToInt(selectable.Vitals.Health));
                _builder.Append('/');
                _builder.Append(Mathf.CeilToInt(selectable.Vitals.MaxHealth));
                if (selectable.Vitals.MaxShield > 0f)
                {
                    _builder.Append("   SHIELD ");
                    _builder.Append(Mathf.CeilToInt(selectable.Vitals.Shield));
                    _builder.Append('/');
                    _builder.Append(Mathf.CeilToInt(selectable.Vitals.MaxShield));
                }
            }

            ConstructionSite construction = selectable.GetComponent<ConstructionSite>();
            if (construction != null && construction.IsConstructing)
            {
                SetConstructionProgress(construction);
                BuildingRuntime building = selectable.GetComponent<BuildingRuntime>();
                float totalSeconds = building != null && building.Definition != null
                    ? building.Definition.ConstructionSeconds
                    : 0f;
                float remainingSeconds = Mathf.Max(
                    0f,
                    totalSeconds - construction.ElapsedSeconds);
                _builder.Append('\n');
                _builder.Append("CONSTRUCTION  ");
                _builder.Append(Mathf.RoundToInt(construction.NormalizedProgress * 100f));
                _builder.Append('%');
                if (totalSeconds > 0f)
                {
                    _builder.Append("   ");
                    _builder.Append(remainingSeconds.ToString("0.0"));
                    _builder.Append("s remaining");
                }
            }
            else
            {
                HeroDeploymentController deployment =
                    selectable.GetComponent<HeroDeploymentController>();
                if (deployment != null && deployment.IsDeploying)
                {
                    SetInformationProgress(true, deployment.NormalizedProgress);
                    _builder.Append('\n');
                    _builder.Append("HERO DEPLOYMENT  ");
                    _builder.Append(Mathf.RoundToInt(
                        deployment.NormalizedProgress * 100f));
                    _builder.Append('%');
                    _builder.Append("   ");
                    _builder.Append(deployment.RemainingSeconds.ToString("0.0"));
                    _builder.Append("s remaining");
                }
                else
                {
                    ProductionQueue production =
                        selectable.GetComponent<ProductionQueue>();
                    if (production != null)
                    {
                        AppendProductionInformation(production);
                    }
                }
            }
        }

        private void AppendProductionInformation(ProductionQueue production)
        {
            ProductionRecipe recipe = production.CurrentRecipe;
            if (recipe == null)
            {
                _builder.Append('\n');
                _builder.Append("WORKER PRODUCTION IDLE");
                return;
            }

            SetInformationProgress(true, production.NormalizedProgress);
            _builder.Append('\n');
            _builder.Append(recipe.Archetype != null &&
                            recipe.Archetype.Roles.HasAny(UnitRole.Worker)
                ? "WORKER PRODUCTION  "
                : "PRODUCTION  ");
            _builder.Append(Mathf.RoundToInt(production.NormalizedProgress * 100f));
            _builder.Append('%');
            _builder.Append("   ");
            _builder.Append(production.CurrentRemainingTime.ToString("0.0"));
            _builder.Append("s remaining");
            _builder.Append('\n');
            _builder.Append("QUEUE  ");
            _builder.Append(production.TotalCount);
            _builder.Append('/');
            _builder.Append(production.Capacity);
        }

        private void ConfigureReadableText()
        {
            if (_countLabel != null)
            {
                _countLabel.fontStyle = FontStyles.Bold;
                _countLabel.enableAutoSizing = true;
                _countLabel.fontSizeMin = 15f;
                _countLabel.fontSizeMax = 20f;
                _countLabel.color = new Color(0.08f, 0.13f, 0.22f, 1f);
                _countLabel.overflowMode = TextOverflowModes.Ellipsis;
            }

            if (_entriesLabel != null)
            {
                _entriesLabel.enableAutoSizing = true;
                _entriesLabel.fontSizeMin = 13f;
                _entriesLabel.fontSizeMax = 17f;
                _entriesLabel.enableWordWrapping = true;
                _entriesLabel.color = new Color(0.12f, 0.18f, 0.28f, 1f);
                _entriesLabel.overflowMode = TextOverflowModes.Ellipsis;
            }
        }

        private void SetConstructionProgress(ConstructionSite construction)
        {
            bool visible = construction != null && construction.IsConstructing;
            SetInformationProgress(
                visible,
                visible ? construction.NormalizedProgress : 0f);
        }

        private void SetInformationProgress(bool visible, float normalizedProgress)
        {
            if (visible)
            {
                EnsureConstructionProgressBar();
            }

            if (_constructionProgressRoot != null &&
                _constructionProgressRoot.activeSelf != visible)
            {
                _constructionProgressRoot.SetActive(visible);
            }

            if (!visible || _constructionProgressFill == null)
            {
                return;
            }

            RectTransform fillRect = _constructionProgressFill.rectTransform;
            Vector2 anchorMax = fillRect.anchorMax;
            anchorMax.x = Mathf.Clamp01(normalizedProgress);
            fillRect.anchorMax = anchorMax;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
        }

        private void EnsureConstructionProgressBar()
        {
            if (_constructionProgressRoot != null || _contentRoot == null)
            {
                return;
            }

            _constructionProgressRoot = new GameObject(
                "ConstructionProgressBar",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            RectTransform backgroundRect =
                _constructionProgressRoot.GetComponent<RectTransform>();
            backgroundRect.SetParent(_contentRoot.transform, false);
            backgroundRect.anchorMin = new Vector2(0f, 0f);
            backgroundRect.anchorMax = new Vector2(1f, 0f);
            backgroundRect.offsetMin = new Vector2(16f, 10f);
            backgroundRect.offsetMax = new Vector2(-16f, 28f);
            backgroundRect.SetAsLastSibling();

            Image background = _constructionProgressRoot.GetComponent<Image>();
            background.color = new Color(0.72f, 0.79f, 0.88f, 0.96f);
            background.raycastTarget = false;
            OriginCoreUiTheme.EnsureInsetBorder(
                backgroundRect,
                "ConstructionProgressBorder",
                new Color(0.2f, 0.39f, 0.58f, 0.95f),
                1f);

            GameObject fillObject = new GameObject(
                "ConstructionProgressFill",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            RectTransform fillRect = fillObject.GetComponent<RectTransform>();
            fillRect.SetParent(backgroundRect, false);
            fillRect.SetAsFirstSibling();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.offsetMin = new Vector2(2f, 2f);
            fillRect.offsetMax = new Vector2(-2f, -2f);

            _constructionProgressFill = fillObject.GetComponent<Image>();
            _constructionProgressFill.color =
                new Color(0.08f, 0.64f, 0.82f, 1f);
            _constructionProgressFill.raycastTarget = false;
        }

        private static bool IsBuilding(Selectable selectable)
        {
            return selectable != null && selectable.Identity != null &&
                   selectable.Identity.Roles.HasAny(UnitRole.Building);
        }
    }
}
