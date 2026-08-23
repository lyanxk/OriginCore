using System.Collections.Generic;
using OriginCore.Abilities;
using OriginCore.Buildings;
using OriginCore.Content;
using OriginCore.Core;
using OriginCore.Economy;
using OriginCore.Gameplay;
using OriginCore.Progression;
using OriginCore.RTS;
using OriginCore.RTS.Commands;
using OriginCore.Weapons;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OriginCore.UI.RTS
{
    [DisallowMultipleComponent]
    public sealed class RtsCommandPanelView : MonoBehaviour
    {
        private enum DynamicCommandKind
        {
            None = 0,
            Ability = 1,
            Build = 2,
            Deploy = 3,
            Promote = 4,
            Produce = 5,
            Research = 6,
            PurchaseWeapon = 7,
            OpenBasicBuildMenu = 8,
            Back = 9,
            OpenAdvancedBuildMenu = 10
        }

        private enum BuildMenuPage
        {
            None = 0,
            Basic = 1,
            Advanced = 2
        }

        [SerializeField] private RtsCommandIssuer _commandIssuer;
        [SerializeField] private SelectionService _selectionService;
        [SerializeField] private ModeHudPresenter _modeHudPresenter;
        [SerializeField] private GameObject _contentRoot;
        [SerializeField] private RectTransform _reservedCommandSlotsRoot;
        [SerializeField] private Button _moveButton;
        [SerializeField] private Button _attackButton;
        [SerializeField] private Button _stopButton;
        [SerializeField] private Button _cancelButton;
        [SerializeField] private SelectionSummaryView _informationView;

        private readonly List<Button> _abilityButtons = new List<Button>();
        private readonly List<TMP_Text> _abilityLabels = new List<TMP_Text>();
        private readonly List<Image> _abilityBackgrounds = new List<Image>();
        private readonly List<Image> _abilityAccents = new List<Image>();
        private readonly List<DynamicCommandKind> _dynamicKinds =
            new List<DynamicCommandKind>();
        private readonly List<UnityEngine.Object> _dynamicPayloads =
            new List<UnityEngine.Object>();
        private readonly List<int> _dynamicAbilityIndices = new List<int>();
        private readonly List<string> _dynamicTitles = new List<string>();
        private readonly List<string> _dynamicDetails = new List<string>();
        private bool _eventsBound;
        private bool _panelChromeReady;
        private AbilityLoadout _displayedLoadout;
        private float _nextCooldownRefreshTime;
        private BuildMenuPage _buildMenuPage;

        private static readonly string[] AbilityHotkeys = { "Q", "W", "E", "B" };

        public RtsCommandIssuer CommandIssuer => _commandIssuer;
        public SelectionService SelectionService => _selectionService;
        public ModeHudPresenter ModeHudPresenter => _modeHudPresenter;
        public GameObject ContentRoot => _contentRoot;
        public RectTransform ReservedCommandSlotsRoot => _reservedCommandSlotsRoot;
        public Button MoveButton => _moveButton;
        public Button AttackButton => _attackButton;
        public Button StopButton => _stopButton;
        public Button CancelButton => _cancelButton;

        private void OnEnable()
        {
            ResolveInformationView();
            ApplyPanelChrome();
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
            if (Time.unscaledTime < _nextCooldownRefreshTime)
            {
                return;
            }

            _nextCooldownRefreshTime = Time.unscaledTime + 0.1f;
            RefreshAbilitySlots();
        }

        public void Configure(
            RtsCommandIssuer commandIssuer,
            SelectionService selectionService,
            ModeHudPresenter modeHudPresenter,
            GameObject contentRoot,
            Button moveButton,
            Button stopButton,
            Button cancelButton,
            Button attackButton = null,
            RectTransform reservedCommandSlotsRoot = null)
        {
            UnbindEvents();
            _commandIssuer = commandIssuer;
            _selectionService = selectionService;
            _modeHudPresenter = modeHudPresenter;
            _contentRoot = contentRoot;
            _reservedCommandSlotsRoot = reservedCommandSlotsRoot;
            _moveButton = moveButton;
            _attackButton = attackButton;
            _stopButton = stopButton;
            _cancelButton = cancelButton;
            ResolveInformationView();
            _panelChromeReady = false;
            ApplyPanelChrome();
            if (isActiveAndEnabled)
            {
                BindEvents();
            }

            Refresh();
            ApplyMode(_modeHudPresenter != null ? _modeHudPresenter.Mode : GameMode.RTS);
        }

        public void Refresh()
        {
            RebindDisplayedLoadout();
            bool hasSelection = _selectionService != null &&
                                _selectionService.SelectedCount > 0;
            bool targeting = _commandIssuer != null &&
                             _commandIssuer.TargetingState != CommandTargetingState.None;
            bool hasMovableSelection = FindSelectedComponent<UnitCommandQueue>() != null;
            bool buildingOnlySelection = hasSelection && !hasMovableSelection &&
                                         HasSelectedImmobileStructure();
            if (_moveButton != null)
            {
                _moveButton.gameObject.SetActive(!buildingOnlySelection);
                _moveButton.interactable = hasMovableSelection && !targeting;
            }

            ReflowReservedSlots();

            if (_attackButton != null)
            {
                _attackButton.gameObject.SetActive(!buildingOnlySelection);
                _attackButton.interactable = hasMovableSelection &&
                                             HasSelectedAttackCommand() &&
                                             !targeting;
            }

            if (_stopButton != null)
            {
                _stopButton.gameObject.SetActive(!buildingOnlySelection);
                _stopButton.interactable = hasMovableSelection;
            }

            if (_cancelButton != null)
            {
                _cancelButton.interactable = targeting;
            }

            RefreshAbilitySlots();
        }

        private void BindEvents()
        {
            if (_eventsBound)
            {
                return;
            }

            if (_commandIssuer != null)
            {
                _commandIssuer.TargetingChanged += HandleTargetingChanged;
                _commandIssuer.CommandStateChanged += HandleCommandStateChanged;
                _commandIssuer.FeedbackChanged += HandleFeedbackChanged;
            }

            if (_selectionService != null)
            {
                _selectionService.SelectionChanged += HandleSelectionChanged;
            }

            if (_modeHudPresenter != null)
            {
                _modeHudPresenter.ModePresented += HandleModePresented;
            }

            _moveButton?.onClick.AddListener(HandleMoveClicked);
            _attackButton?.onClick.AddListener(HandleAttackClicked);
            _stopButton?.onClick.AddListener(HandleStopClicked);
            _cancelButton?.onClick.AddListener(HandleCancelClicked);
            _eventsBound = true;
        }

        private void UnbindEvents()
        {
            if (!_eventsBound)
            {
                return;
            }

            if (_commandIssuer != null)
            {
                _commandIssuer.TargetingChanged -= HandleTargetingChanged;
                _commandIssuer.CommandStateChanged -= HandleCommandStateChanged;
                _commandIssuer.FeedbackChanged -= HandleFeedbackChanged;
            }

            if (_selectionService != null)
            {
                _selectionService.SelectionChanged -= HandleSelectionChanged;
            }

            if (_modeHudPresenter != null)
            {
                _modeHudPresenter.ModePresented -= HandleModePresented;
            }

            _moveButton?.onClick.RemoveListener(HandleMoveClicked);
            _attackButton?.onClick.RemoveListener(HandleAttackClicked);
            _stopButton?.onClick.RemoveListener(HandleStopClicked);
            _cancelButton?.onClick.RemoveListener(HandleCancelClicked);
            BindDisplayedLoadout(null);
            _eventsBound = false;
        }

        private void HandleMoveClicked()
        {
            _commandIssuer?.BeginMoveTargeting();
        }

        private void HandleAttackClicked()
        {
            _commandIssuer?.BeginAttackTargeting();
        }

        private void HandleStopClicked()
        {
            _commandIssuer?.StopSelected();
        }

        private void HandleCancelClicked()
        {
            _commandIssuer?.CancelTargeting();
        }

        private void HandleAbilityClicked(int slotIndex)
        {
            _commandIssuer?.ActivateSelectedAbilitySlot(slotIndex);
        }

        private void HandleTargetingChanged(CommandTargetingChange change)
        {
            Refresh();
        }

        private void HandleCommandStateChanged()
        {
            Refresh();
        }

        private void HandleSelectionChanged()
        {
            _buildMenuPage = BuildMenuPage.None;
            Refresh();
        }

        private void HandleFeedbackChanged(RtsCommandFeedback feedback)
        {
            if (string.IsNullOrWhiteSpace(feedback.Message))
            {
                return;
            }

            ResolveInformationView();
            _informationView?.ShowCommandInfo(
                feedback.IsError ? "COMMAND FAILED" : "COMMAND",
                feedback.Message);
        }

        private void HandleLoadoutChanged(AbilityLoadout loadout)
        {
            RefreshAbilitySlots();
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

        private void RebindDisplayedLoadout()
        {
            AbilityLoadout next = null;
            if (_selectionService != null)
            {
                IReadOnlyList<OriginCore.Gameplay.Selectable> selected =
                    _selectionService.Selected;
                for (int i = 0; i < selected.Count; i++)
                {
                    if (selected[i] != null &&
                        selected[i].TryGetComponent(out AbilityLoadout candidate))
                    {
                        next = candidate;
                        break;
                    }
                }
            }

            BindDisplayedLoadout(next);
        }

        private void BindDisplayedLoadout(AbilityLoadout loadout)
        {
            if (_displayedLoadout == loadout)
            {
                return;
            }

            if (_displayedLoadout != null)
            {
                _displayedLoadout.LoadoutChanged -= HandleLoadoutChanged;
            }

            _displayedLoadout = loadout;
            if (_displayedLoadout != null && _eventsBound)
            {
                _displayedLoadout.LoadoutChanged += HandleLoadoutChanged;
            }
        }

        private void EnsureAbilitySlots()
        {
            if (_reservedCommandSlotsRoot == null || _abilityButtons.Count > 0)
            {
                return;
            }

            TMP_Text templateLabel = _moveButton != null
                ? _moveButton.GetComponentInChildren<TMP_Text>(true)
                : null;
            for (int i = 0; i < _reservedCommandSlotsRoot.childCount; i++)
            {
                Transform slot = _reservedCommandSlotsRoot.GetChild(i);
                Graphic graphic = slot.GetComponent<Graphic>();
                Button button = slot.GetComponent<Button>();
                if (button == null)
                {
                    button = slot.gameObject.AddComponent<Button>();
                }

                button.targetGraphic = graphic;
                button.transition = _moveButton != null
                    ? _moveButton.transition
                    : UnityEngine.UI.Selectable.Transition.ColorTint;
                if (_moveButton != null)
                {
                    button.colors = _moveButton.colors;
                }

                Navigation navigation = button.navigation;
                navigation.mode = Navigation.Mode.None;
                button.navigation = navigation;
                button.onClick.RemoveAllListeners();
                int slotIndex = i;
                button.onClick.AddListener(() => HandleDynamicSlotClicked(slotIndex));

                TMP_Text label = slot.GetComponentInChildren<TMP_Text>(true);
                if (label == null && templateLabel != null)
                {
                    label = Instantiate(templateLabel, slot, false);
                    label.name = "AbilityLabel";
                    RectTransform labelTransform = label.rectTransform;
                    labelTransform.anchorMin = Vector2.zero;
                    labelTransform.anchorMax = Vector2.one;
                    labelTransform.pivot = new Vector2(0.5f, 0.5f);
                    labelTransform.anchoredPosition = Vector2.zero;
                    labelTransform.sizeDelta = new Vector2(-6f, -6f);
                    label.alignment = TextAlignmentOptions.Center;
                    label.raycastTarget = false;
                }

                ConfigureSlotLabel(label);

                _abilityButtons.Add(button);
                _abilityLabels.Add(label);
                _abilityBackgrounds.Add(graphic as Image);
                _abilityAccents.Add(GetOrCreateAccent(slot));
                _dynamicKinds.Add(DynamicCommandKind.None);
                _dynamicPayloads.Add(null);
                _dynamicAbilityIndices.Add(-1);
                _dynamicTitles.Add(string.Empty);
                _dynamicDetails.Add(string.Empty);
                ConfigureInfoRelay(button, i);
            }
        }

        private void RefreshAbilitySlots()
        {
            EnsureAbilitySlots();
            AbilityDefinition[] abilities = _displayedLoadout != null
                ? _displayedLoadout.RtsAbilities
                : null;
            Builder builder = FindSelectedComponent<Builder>();
            ProductionQueue production = FindSelectedComponent<ProductionQueue>();
            BuildingRuntime buildingRuntime = FindSelectedComponent<BuildingRuntime>();
            PromotionController promotion = FindSelectedComponent<PromotionController>();
            bool targeting = _commandIssuer != null &&
                             _commandIssuer.TargetingState != CommandTargetingState.None;
            if (_buildMenuPage != BuildMenuPage.None)
            {
                if (builder == null)
                {
                    _buildMenuPage = BuildMenuPage.None;
                }
                else
                {
                    RefreshBuildSubmenu(builder, targeting, _buildMenuPage);
                    return;
                }
            }

            int reservedAbilitySlots = abilities != null
                ? Mathf.Min(AbilityHotkeys.Length, abilities.Length)
                : 0;
            int firstAbilitySlot = Mathf.Max(
                0,
                _abilityButtons.Count - AbilityHotkeys.Length);
            int recipeIndex = 0;
            int technologyIndex = 0;
            int weaponIndex = 0;
            ProductionRecipe[] recipes = production != null
                ? CopyRecipes(production.Recipes)
                : null;
            TechnologyDefinition[] technologies = buildingRuntime != null &&
                                                  buildingRuntime.Definition != null
                ? buildingRuntime.Definition.Technologies
                : null;
            WeaponDefinition[] weapons = IsArmory(buildingRuntime) &&
                                         AppRoot.TryGetInstance(out AppRoot weaponRoot) &&
                                         weaponRoot.Services != null
                ? weaponRoot.Services.ContentCatalog.Catalog.Weapons
                : null;
            int basicBuildSlot = Mathf.Max(0, _abilityButtons.Count - 4);
            int advancedBuildSlot = Mathf.Max(0, _abilityButtons.Count - 3);
            for (int i = 0; i < _abilityButtons.Count; i++)
            {
                Button button = _abilityButtons[i];
                TMP_Text label = _abilityLabels[i];
                DynamicCommandKind kind = DynamicCommandKind.None;
                UnityEngine.Object payload = null;
                string text = string.Empty;
                string title = string.Empty;
                string detail = string.Empty;
                bool ready = false;
                int abilityIndex = i - firstAbilitySlot;

                if (abilityIndex >= 0 && abilityIndex < reservedAbilitySlots)
                {
                    AbilityDefinition definition = abilities[abilityIndex];
                    if (definition != null)
                    {
                        _displayedLoadout.TryGetRuntime(
                            definition.ContentId,
                            out AbilityRuntime runtime);
                        kind = DynamicCommandKind.Ability;
                        payload = definition;
                        ready = runtime != null && runtime.IsReady;
                        text = "SKILL";
                        title = definition.DisplayName;
                        detail = BuildAbilityDetail(
                            definition,
                            runtime,
                            AbilityHotkeys[abilityIndex]);
                    }
                }
                else if (builder != null && i == basicBuildSlot)
                {
                    kind = DynamicCommandKind.OpenBasicBuildMenu;
                    ready = HasBuildTier(builder, false);
                    text = "BASIC BUILD";
                    title = "BASIC BUILDINGS";
                    detail = "Open the basic building catalogue.";
                }
                else if (builder != null && i == advancedBuildSlot)
                {
                    kind = DynamicCommandKind.OpenAdvancedBuildMenu;
                    ready = HasBuildTier(builder, true);
                    text = "ADVANCED BUILD";
                    title = "ADVANCED BUILDINGS";
                    detail = "Open the advanced and technology building catalogue.";
                }
                else if (production != null && production.AwaitingWorldDeployment &&
                         !HasKind(DynamicCommandKind.Deploy, i))
                {
                    kind = DynamicCommandKind.Deploy;
                    payload = production;
                    ready = true;
                    text = "DEPLOY";
                    title = "DEPLOY";
                    detail = production.CurrentRecipe != null
                        ? "Place the completed " + production.CurrentRecipe.DisplayName +
                          " batch at a valid world position.\nLeft-click to confirm."
                        : "Place the completed production batch at a valid world position.\nLeft-click to confirm.";
                }
                else if (production != null && recipes != null &&
                         recipeIndex < recipes.Length)
                {
                    ProductionRecipe recipe = recipes[recipeIndex++];
                    if (recipe != null)
                    {
                        kind = DynamicCommandKind.Produce;
                        payload = recipe;
                        ready = CanProduce(production, recipe);
                        text = BuildProductionLabel(recipe, production);
                        title = recipe.DisplayName;
                        detail = BuildProductionDetail(recipe, production);
                    }
                }
                else if (technologies != null && technologyIndex < technologies.Length)
                {
                    TechnologyDefinition technology = technologies[technologyIndex++];
                    if (technology != null)
                    {
                        kind = DynamicCommandKind.Research;
                        payload = technology;
                        ready = CanResearch(technology);
                        text = BuildTechnologyLabel(technology);
                        title = technology.DisplayName;
                        detail = BuildTechnologyDetail(technology);
                    }
                }
                else if (weapons != null && weaponIndex < weapons.Length)
                {
                    WeaponDefinition weapon = weapons[weaponIndex++];
                    if (weapon != null)
                    {
                        kind = DynamicCommandKind.PurchaseWeapon;
                        payload = weapon;
                        ready = CanPurchaseWeapon(weapon);
                        text = BuildWeaponLabel(weapon);
                        title = weapon.DisplayName;
                        detail = BuildWeaponDetail(weapon);
                    }
                }
                int promotionSlotIndex = reservedAbilitySlots > 0
                    ? Mathf.Max(0, firstAbilitySlot - 1)
                    : _abilityButtons.Count - 1;
                bool promotionSlot = i == promotionSlotIndex && promotion != null;
                if (promotionSlot)
                {
                    kind = DynamicCommandKind.Promote;
                    payload = promotion;
                    ready = promotion.CanPromote(out _, out _);
                    text = "PROMOTE\n" +
                           (promotion.Definition != null &&
                            promotion.Definition.TargetUnit != null
                               ? promotion.Definition.TargetUnit.DisplayName
                               : string.Empty);
                    title = "PROMOTE";
                    detail = BuildPromotionDetail(promotion);
                }

                _dynamicKinds[i] = kind;
                _dynamicPayloads[i] = payload;
                _dynamicAbilityIndices[i] = kind == DynamicCommandKind.Ability
                    ? abilityIndex
                    : -1;
                _dynamicTitles[i] = title;
                _dynamicDetails[i] = detail;
                button.interactable = kind != DynamicCommandKind.None && ready && !targeting;
                if (button.targetGraphic != null)
                {
                    button.targetGraphic.raycastTarget = kind != DynamicCommandKind.None;
                }

                if (label != null)
                {
                    ConfigureSlotLabel(label);
                    label.gameObject.SetActive(kind != DynamicCommandKind.None);
                    label.text = text;
                    label.color = ready && !targeting
                        ? new Color(0.1f, 0.16f, 0.27f, 1f)
                        : new Color(0.46f, 0.52f, 0.62f, 0.9f);
                }

                ApplyDynamicSlotStyle(i, kind, ready && !targeting);
            }
        }

        private void RefreshBuildSubmenu(
            Builder builder,
            bool targeting,
            BuildMenuPage page)
        {
            int buildingIndex = 0;
            int backSlot = _abilityButtons.Count - 1;
            bool advanced = page == BuildMenuPage.Advanced;
            int buildingCount = CountBuildTier(builder, advanced);
            int firstBuildingSlot = Mathf.Max(0, backSlot - buildingCount);
            for (int i = 0; i < _abilityButtons.Count; i++)
            {
                Button button = _abilityButtons[i];
                TMP_Text label = _abilityLabels[i];
                DynamicCommandKind kind = DynamicCommandKind.None;
                UnityEngine.Object payload = null;
                string text = string.Empty;
                string title = string.Empty;
                string detail = string.Empty;
                bool ready = false;

                if (i == backSlot)
                {
                    kind = DynamicCommandKind.Back;
                    ready = true;
                    text = "BACK";
                    title = "BACK";
                    detail = "Return to the primary command panel.";
                }
                else if (i >= firstBuildingSlot)
                {
                    BuildingDefinition building = null;
                    while (buildingIndex < builder.AvailableBuildings.Count)
                    {
                        BuildingDefinition candidate =
                            builder.AvailableBuildings[buildingIndex++];
                        if (candidate != null &&
                            IsAdvancedBuilding(candidate) == advanced)
                        {
                            building = candidate;
                            break;
                        }
                    }

                    if (building != null)
                    {
                        kind = DynamicCommandKind.Build;
                        payload = building;
                        ready = CanConstruct(building);
                        text = building.DisplayName;
                        title = building.DisplayName;
                        detail = BuildBuildingDetail(building);
                    }
                }

                _dynamicKinds[i] = kind;
                _dynamicPayloads[i] = payload;
                _dynamicAbilityIndices[i] = -1;
                _dynamicTitles[i] = title;
                _dynamicDetails[i] = detail;
                button.interactable = kind != DynamicCommandKind.None &&
                                      !targeting &&
                                      (kind == DynamicCommandKind.Build || ready);
                if (button.targetGraphic != null)
                {
                    button.targetGraphic.raycastTarget = kind != DynamicCommandKind.None;
                }

                if (label != null)
                {
                    ConfigureSlotLabel(label);
                    label.gameObject.SetActive(kind != DynamicCommandKind.None);
                    label.text = text;
                    label.color = ready && !targeting
                        ? new Color(0.08f, 0.13f, 0.22f, 1f)
                        : new Color(0.42f, 0.48f, 0.58f, 0.92f);
                }

                ApplyDynamicSlotStyle(i, kind, ready && !targeting);
            }
        }

        private void ApplyPanelChrome()
        {
            if (_panelChromeReady || _contentRoot == null)
            {
                return;
            }

            _panelChromeReady = true;
            Image panel = _contentRoot.GetComponent<Image>();
            if (panel != null)
            {
                panel.color = new Color(0.93f, 0.96f, 0.99f, 0.97f);
            }

            Outline outline = _contentRoot.GetComponent<Outline>();
            if (outline == null)
            {
                outline = _contentRoot.AddComponent<Outline>();
            }

            outline.enabled = false;
            OriginCoreUiTheme.EnsureInsetBorder(
                _contentRoot.transform,
                "CommandPanelBorder",
                new Color(0.28f, 0.5f, 0.75f, 0.82f),
                2f);

            Shadow panelShadow = null;
            Shadow[] shadows = _contentRoot.GetComponents<Shadow>();
            for (int i = 0; i < shadows.Length; i++)
            {
                if (shadows[i] != null && shadows[i].GetType() == typeof(Shadow))
                {
                    panelShadow = shadows[i];
                    break;
                }
            }

            if (panelShadow == null)
            {
                panelShadow = _contentRoot.AddComponent<Shadow>();
            }

            panelShadow.effectColor = new Color(0.22f, 0.24f, 0.48f, 0.3f);
            panelShadow.effectDistance = new Vector2(-4f, 4f);
            panelShadow.useGraphicAlpha = true;

            StyleCommandButton(_moveButton, new Color(0.15f, 0.72f, 0.92f, 1f));
            StyleCommandButton(_attackButton, new Color(0.94f, 0.24f, 0.22f, 1f));
            StyleCommandButton(_stopButton, new Color(0.94f, 0.64f, 0.18f, 1f));
            StyleCommandButton(_cancelButton, new Color(0.66f, 0.34f, 0.88f, 1f));
            ConfigureInfoRelay(_moveButton, -1);
            ConfigureInfoRelay(_attackButton, -2);
            ConfigureInfoRelay(_stopButton, -3);
            ConfigureInfoRelay(_cancelButton, -4);
            CreatePanelEdge(_contentRoot.transform);
        }

        private static void StyleCommandButton(Button button, Color accent)
        {
            if (button == null)
            {
                return;
            }

            Image background = button.targetGraphic as Image ?? button.GetComponent<Image>();
            if (background != null)
            {
                background.color = Color.Lerp(
                    new Color(0.82f, 0.89f, 0.96f, 0.98f), accent, 0.14f);
                background.raycastTarget = true;
            }

            Outline outline = button.GetComponent<Outline>();
            if (outline == null)
            {
                outline = button.gameObject.AddComponent<Outline>();
            }

            outline.enabled = false;
            OriginCoreUiTheme.EnsureInsetBorder(
                button.transform,
                "CommandButtonBorder",
                Color.Lerp(new Color(0.22f, 0.34f, 0.5f, 1f), accent, 0.52f),
                2f);

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.Lerp(Color.white, accent, 0.12f);
            colors.pressedColor = Color.Lerp(new Color(0.74f, 0.82f, 0.9f, 1f), accent, 0.28f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.62f, 0.68f, 0.75f, 0.52f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                ConfigureSlotLabel(label);
                label.color = new Color(0.1f, 0.16f, 0.27f, 1f);
            }

            Image accentImage = GetOrCreateAccent(button.transform);
            if (accentImage != null)
            {
                accentImage.color = accent;
            }
        }

        private static void ConfigureSlotLabel(TMP_Text label)
        {
            if (label == null)
            {
                return;
            }

            OriginCoreUiTheme.ApplyUiFont(label);
            label.alignment = TextAlignmentOptions.Center;
            label.fontStyle = FontStyles.Bold;
            label.enableAutoSizing = true;
            label.fontSizeMin = 13f;
            label.fontSizeMax = 17f;
            label.enableWordWrapping = true;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.margin = new Vector4(5f, 6f, 5f, 5f);
            label.raycastTarget = false;
        }

        private void ApplyDynamicSlotStyle(
            int index,
            DynamicCommandKind kind,
            bool ready)
        {
            if (index < 0 || index >= _abilityBackgrounds.Count)
            {
                return;
            }

            Color accent = GetCommandAccent(kind);
            Image background = _abilityBackgrounds[index];
            if (background != null)
            {
                background.color = kind == DynamicCommandKind.None
                    ? new Color(0.83f, 0.88f, 0.93f, 0.74f)
                    : Color.Lerp(new Color(0.82f, 0.89f, 0.96f, 0.98f), accent, 0.13f);
            }

            Image accentImage = index < _abilityAccents.Count
                ? _abilityAccents[index]
                : null;
            if (accentImage != null)
            {
                accentImage.color = new Color(
                    accent.r,
                    accent.g,
                    accent.b,
                    kind == DynamicCommandKind.None ? 0.14f : ready ? 1f : 0.38f);
            }

            Button button = _abilityButtons[index];
            Outline outline = button != null ? button.GetComponent<Outline>() : null;
            if (button != null && outline == null)
            {
                outline = button.gameObject.AddComponent<Outline>();
            }

            Color borderColor = kind == DynamicCommandKind.None
                ? new Color(0.42f, 0.53f, 0.64f, 0.82f)
                : Color.Lerp(new Color(0.22f, 0.34f, 0.5f, 1f), accent, 0.48f);
            if (outline != null)
            {
                outline.enabled = false;
            }

            if (button != null)
            {
                OriginCoreUiTheme.EnsureInsetBorder(
                    button.transform,
                    "CommandButtonBorder",
                    borderColor,
                    2f);
            }
        }

        private static Color GetCommandAccent(DynamicCommandKind kind)
        {
            switch (kind)
            {
                case DynamicCommandKind.Ability:
                    return new Color(0.48f, 0.52f, 1f, 1f);
                case DynamicCommandKind.Build:
                    return new Color(0.96f, 0.66f, 0.2f, 1f);
                case DynamicCommandKind.OpenBasicBuildMenu:
                case DynamicCommandKind.OpenAdvancedBuildMenu:
                    return new Color(0.96f, 0.66f, 0.2f, 1f);
                case DynamicCommandKind.Back:
                    return new Color(0.4f, 0.52f, 0.68f, 1f);
                case DynamicCommandKind.Deploy:
                    return new Color(0.16f, 0.82f, 0.78f, 1f);
                case DynamicCommandKind.Promote:
                    return new Color(0.78f, 0.34f, 0.94f, 1f);
                case DynamicCommandKind.Produce:
                    return new Color(0.2f, 0.78f, 0.52f, 1f);
                case DynamicCommandKind.Research:
                    return new Color(0.24f, 0.62f, 0.96f, 1f);
                case DynamicCommandKind.PurchaseWeapon:
                    return new Color(0.94f, 0.34f, 0.38f, 1f);
                default:
                    return new Color(0.36f, 0.48f, 0.6f, 1f);
            }
        }

        private static Image GetOrCreateAccent(Transform parent)
        {
            if (parent == null)
            {
                return null;
            }

            Transform existing = parent.Find("CommandAccent");
            GameObject accentObject;
            if (existing != null)
            {
                accentObject = existing.gameObject;
            }
            else
            {
                accentObject = new GameObject(
                    "CommandAccent",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                accentObject.transform.SetParent(parent, false);
            }

            RectTransform rect = accentObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -1f);
            rect.sizeDelta = new Vector2(-8f, 3f);
            Image image = accentObject.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        private static void CreatePanelEdge(Transform parent)
        {
            if (parent == null || parent.Find("PanelEdge") != null)
            {
                return;
            }

            GameObject edge = new GameObject(
                "PanelEdge",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            edge.transform.SetParent(parent, false);
            RectTransform rect = edge.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, 3f);
            Image image = edge.GetComponent<Image>();
            image.color = new Color(0.24f, 0.66f, 0.92f, 0.92f);
            image.raycastTarget = false;
        }

        private void ReflowReservedSlots()
        {
            if (_reservedCommandSlotsRoot == null)
            {
                return;
            }

            const float margin = 12f;
            const float pitch = 86f;
            const float size = 78f;
            for (int i = 0; i < _reservedCommandSlotsRoot.childCount; i++)
            {
                RectTransform slot = _reservedCommandSlotsRoot.GetChild(i) as RectTransform;
                if (slot == null)
                {
                    continue;
                }

                int cell = i + 4;
                int column = cell % 4;
                int row = cell / 4;
                slot.anchorMin = new Vector2(0f, 1f);
                slot.anchorMax = new Vector2(0f, 1f);
                slot.pivot = new Vector2(0f, 1f);
                slot.anchoredPosition = new Vector2(
                    margin + column * pitch,
                    -margin - row * pitch);
                slot.sizeDelta = new Vector2(size, size);
            }
        }

        private void HandleDynamicSlotClicked(int slotIndex)
        {
            if (_commandIssuer == null || slotIndex < 0 ||
                slotIndex >= _dynamicKinds.Count)
            {
                return;
            }

            switch (_dynamicKinds[slotIndex])
            {
                case DynamicCommandKind.Ability:
                    ShowCommandInfo(slotIndex);
                    _commandIssuer.ActivateSelectedAbilitySlot(
                        _dynamicAbilityIndices[slotIndex]);
                    break;
                case DynamicCommandKind.Build:
                    BuildingDefinition building =
                        _dynamicPayloads[slotIndex] as BuildingDefinition;
                    if (!TryGetConstructionAvailability(
                            building,
                            out _))
                    {
                        ResolveInformationView();
                        _informationView?.ShowCommandInfo(
                            building != null ? building.DisplayName : "BUILD",
                            BuildBuildingDetail(building));
                        break;
                    }

                    if (_commandIssuer.ActivateBuilding(building))
                    {
                        _buildMenuPage = BuildMenuPage.None;
                        RefreshAbilitySlots();
                    }
                    break;
                case DynamicCommandKind.OpenBasicBuildMenu:
                    _buildMenuPage = BuildMenuPage.Basic;
                    RefreshAbilitySlots();
                    break;
                case DynamicCommandKind.OpenAdvancedBuildMenu:
                    _buildMenuPage = BuildMenuPage.Advanced;
                    RefreshAbilitySlots();
                    break;
                case DynamicCommandKind.Back:
                    _buildMenuPage = BuildMenuPage.None;
                    RefreshAbilitySlots();
                    break;
                case DynamicCommandKind.Deploy:
                    _commandIssuer.BeginProductionDeployment(
                        _dynamicPayloads[slotIndex] as ProductionQueue);
                    break;
                case DynamicCommandKind.Promote:
                    _commandIssuer.PromoteSelected();
                    break;
                case DynamicCommandKind.Produce:
                    FindSelectedComponent<ProductionQueue>()?.TryEnqueue(
                        _dynamicPayloads[slotIndex] as ProductionRecipe);
                    RefreshAbilitySlots();
                    break;
                case DynamicCommandKind.Research:
                    ToggleResearch(
                        _dynamicPayloads[slotIndex] as TechnologyDefinition);
                    RefreshAbilitySlots();
                    break;
                case DynamicCommandKind.PurchaseWeapon:
                    PurchaseWeapon(
                        _dynamicPayloads[slotIndex] as WeaponDefinition);
                    RefreshAbilitySlots();
                    break;
            }
        }

        public void ShowCommandInfo(int slotCode)
        {
            ResolveInformationView();
            if (_informationView == null)
            {
                return;
            }

            switch (slotCode)
            {
                case -1:
                    _informationView.ShowCommandInfo(
                        "MOVE",
                        "Move selected units to a reachable world point.\nLeft-click the destination to confirm.");
                    return;
                case -2:
                    _informationView.ShowCommandInfo(
                        "ATTACK",
                        "Attack a hostile target, or attack-move toward a world point.\nLeft-click to confirm.");
                    return;
                case -3:
                    _informationView.ShowCommandInfo(
                        "STOP",
                        "Cancel the current action and all queued commands immediately.");
                    return;
                case -4:
                    _informationView.ShowCommandInfo(
                        "CANCEL",
                        "Leave the current targeting state without issuing a command.");
                    return;
            }

            if (slotCode >= 0 && slotCode < _dynamicTitles.Count &&
                _dynamicKinds[slotCode] != DynamicCommandKind.None)
            {
                _informationView.ShowCommandInfo(
                    _dynamicTitles[slotCode],
                    _dynamicDetails[slotCode]);
            }
        }

        public void ClearCommandInfo()
        {
            ResolveInformationView();
            _informationView?.ClearCommandInfo();
        }

        private void ResolveInformationView()
        {
            if (_informationView != null)
            {
                return;
            }

            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                _informationView = canvas.GetComponentInChildren<SelectionSummaryView>(true);
            }
        }

        private void ConfigureInfoRelay(Button button, int slotCode)
        {
            if (button == null)
            {
                return;
            }

            RtsCommandInfoRelay relay = button.GetComponent<RtsCommandInfoRelay>();
            if (relay == null)
            {
                relay = button.gameObject.AddComponent<RtsCommandInfoRelay>();
            }

            relay.Configure(this, slotCode);
        }

        private bool HasKind(DynamicCommandKind kind, int beforeIndex)
        {
            int count = Mathf.Min(beforeIndex, _dynamicKinds.Count);
            for (int i = 0; i < count; i++)
            {
                if (_dynamicKinds[i] == kind)
                {
                    return true;
                }
            }

            return false;
        }

        private T FindSelectedComponent<T>() where T : Component
        {
            if (_selectionService == null)
            {
                return null;
            }

            IReadOnlyList<OriginCore.Gameplay.Selectable> selected =
                _selectionService.Selected;
            for (int i = 0; i < selected.Count; i++)
            {
                T component = selected[i] != null ? selected[i].GetComponent<T>() : null;
                if (component != null)
                {
                    return component;
                }
            }

            return null;
        }

        private bool HasSelectedImmobileStructure()
        {
            if (_selectionService == null)
            {
                return false;
            }

            IReadOnlyList<OriginCore.Gameplay.Selectable> selected =
                _selectionService.Selected;
            for (int i = 0; i < selected.Count; i++)
            {
                OriginCore.Gameplay.Selectable selectable = selected[i];
                if (selectable == null)
                {
                    continue;
                }

                OriginCore.Gameplay.EntityIdentity identity = selectable.Identity;
                if (selectable.GetComponent<BuildingRuntime>() != null ||
                    selectable.GetComponent<ResourceNode>() != null ||
                    identity != null &&
                    identity.Roles.HasAny(OriginCore.Gameplay.UnitRole.Building))
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasSelectedAttackCommand()
        {
            if (_selectionService == null)
            {
                return false;
            }

            IReadOnlyList<OriginCore.Gameplay.Selectable> selected =
                _selectionService.Selected;
            for (int i = 0; i < selected.Count; i++)
            {
                OriginCore.Gameplay.Selectable selectable = selected[i];
                UnitCommandQueue queue = selectable != null
                    ? selectable.GetComponent<UnitCommandQueue>()
                    : null;
                if (queue != null && queue.AttackCapability != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool CanConstruct(BuildingDefinition definition)
        {
            return TryGetConstructionAvailability(definition, out _);
        }

        private static bool TryGetConstructionAvailability(
            BuildingDefinition definition,
            out string reason)
        {
            if (definition == null)
            {
                reason = "Building definition is unavailable.";
                return false;
            }

            if (!AppRoot.TryGetInstance(out AppRoot appRoot) ||
                appRoot.Services == null)
            {
                reason = "Construction services are unavailable.";
                return false;
            }

            if (!appRoot.Services.TechTree.CanConstruct(definition))
            {
                reason = definition.Unique
                    ? "Locked, or its unique instance already exists."
                    : "This building is still locked.";
                return false;
            }

            if (!appRoot.Services.ResourceService.CanAfford(
                    definition.ConstructionCost))
            {
                reason = "Insufficient resources.";
                return false;
            }

            reason = "Ready to build.";
            return true;
        }

        private static bool HasBuildTier(Builder builder, bool advanced)
        {
            return CountBuildTier(builder, advanced) > 0;
        }

        private static int CountBuildTier(Builder builder, bool advanced)
        {
            int count = 0;
            if (builder == null)
            {
                return count;
            }

            for (int i = 0; i < builder.AvailableBuildings.Count; i++)
            {
                BuildingDefinition building = builder.AvailableBuildings[i];
                if (building != null && IsAdvancedBuilding(building) == advanced)
                {
                    count++;
                }
            }

            return count;
        }

        private static bool IsAdvancedBuilding(BuildingDefinition building)
        {
            if (building == null)
            {
                return false;
            }

            string id = building.ContentId;
            return string.Equals(
                       id,
                       "z.building.sky_altar",
                       System.StringComparison.Ordinal) ||
                   string.Equals(
                       id,
                       "z.building.terrain_lab",
                       System.StringComparison.Ordinal);
        }

        private static bool CanProduce(
            ProductionQueue queue,
            ProductionRecipe recipe)
        {
            return queue != null && recipe != null &&
                   queue.TotalCount < queue.Capacity &&
                   AppRoot.TryGetInstance(out AppRoot appRoot) &&
                   appRoot.Services != null &&
                   appRoot.Services.TechTree.CanProduce(recipe) &&
                   appRoot.Services.ResourceService.CanAfford(recipe.Cost);
        }

        private static bool CanResearch(TechnologyDefinition technology)
        {
            if (technology == null ||
                !AppRoot.TryGetInstance(out AppRoot appRoot) ||
                appRoot.Services == null)
            {
                return false;
            }

            TechnologyState state = appRoot.Services.TechTree.GetState(technology);
            return state == TechnologyState.Researching ||
                   state == TechnologyState.Available &&
                   appRoot.Services.ResourceService.CanAfford(technology.Cost);
        }

        private static void ToggleResearch(TechnologyDefinition technology)
        {
            if (technology == null ||
                !AppRoot.TryGetInstance(out AppRoot appRoot) ||
                appRoot.Services == null)
            {
                return;
            }

            TechTreeService techTree = appRoot.Services.TechTree;
            if (techTree.GetState(technology) == TechnologyState.Researching)
            {
                techTree.CancelResearch();
                return;
            }

            techTree.TryStartResearch(technology, out _);
        }

        private static ProductionRecipe[] CopyRecipes(
            IReadOnlyList<ProductionRecipe> recipes)
        {
            if (recipes == null || recipes.Count == 0)
            {
                return new ProductionRecipe[0];
            }

            ProductionRecipe[] result = new ProductionRecipe[recipes.Count];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = recipes[i];
            }

            return result;
        }

        private static string BuildProductionLabel(
            ProductionRecipe recipe,
            ProductionQueue queue)
        {
            return recipe.DisplayName;
        }

        private static string BuildTechnologyLabel(TechnologyDefinition technology)
        {
            if (!AppRoot.TryGetInstance(out AppRoot appRoot) || appRoot.Services == null)
            {
                return technology.DisplayName;
            }

            TechTreeService techTree = appRoot.Services.TechTree;
            TechnologyState state = techTree.GetState(technology);
            if (state == TechnologyState.Researching)
            {
                return technology.DisplayName;
            }

            if (state == TechnologyState.Completed)
            {
                return technology.DisplayName;
            }

            return technology.DisplayName;
        }

        private static bool IsArmory(BuildingRuntime building)
        {
            return building != null && building.IsOperational &&
                   building.Definition != null && string.Equals(
                       building.Definition.ContentId,
                       "z.building.armory",
                       System.StringComparison.Ordinal);
        }

        private static bool CanPurchaseWeapon(WeaponDefinition weapon)
        {
            if (weapon == null ||
                !AppRoot.TryGetInstance(out AppRoot appRoot) ||
                appRoot.Services == null ||
                !appRoot.Services.ResourceService.CanAfford(weapon.PurchaseCost))
            {
                return false;
            }

            OriginCore.Gameplay.EntityIdentity hero =
                appRoot.Services.MatchSession.ActiveHero;
            return hero != null && hero.GetComponent<WeaponInventory>() != null;
        }

        private static void PurchaseWeapon(WeaponDefinition weapon)
        {
            if (weapon == null ||
                !AppRoot.TryGetInstance(out AppRoot appRoot) ||
                appRoot.Services == null ||
                appRoot.Services.MatchSession.ActiveHero == null)
            {
                return;
            }

            WeaponInventory inventory =
                appRoot.Services.MatchSession.ActiveHero.GetComponent<WeaponInventory>();
            inventory?.TryPurchaseAndEquip(weapon, out _);
        }

        private static string BuildWeaponLabel(WeaponDefinition weapon)
        {
            return weapon.DisplayName;
        }

        private static string BuildAbilityDetail(
            AbilityDefinition definition,
            AbilityRuntime runtime,
            string hotkey)
        {
            string status = runtime == null
                ? "Unavailable"
                : runtime.IsReady
                    ? "Ready"
                    : "Cooldown " + runtime.CooldownRemaining.ToString("0.0") + "s";
            string charges = runtime != null && runtime.MaximumCharges > 1
                ? "   Charges " + runtime.Charges + "/" + runtime.MaximumCharges
                : string.Empty;
            return "Shortcut " + hotkey + "   Target " + definition.TargetType +
                   "\nRange " + definition.Range.ToString("0.#") +
                   "   Radius " + definition.Radius.ToString("0.#") +
                   "\nCooldown " + definition.Cooldown.ToString("0.#") + "s   Energy " +
                   definition.EnergyCost.ToString("0.#") + charges +
                   "\nCost " + FormatCost(definition.ResourceCost) + "   " + status;
        }

        private static string BuildProductionDetail(
            ProductionRecipe recipe,
            ProductionQueue queue)
        {
            string queueState = queue != null
                ? queue.TotalCount + "/" + queue.Capacity + " queued"
                : "Queue unavailable";
            float productionTime = queue != null
                ? queue.GetEffectiveProductionTime(recipe)
                : recipe.ProductionTime;
            return "Batch " + recipe.BatchCount + "   Time " +
                   productionTime.ToString("0.#") + "s\nCost " +
                   FormatCost(recipe.Cost) + "   " + queueState;
        }

        private static string BuildTechnologyDetail(TechnologyDefinition technology)
        {
            string state = "Unavailable";
            if (AppRoot.TryGetInstance(out AppRoot appRoot) && appRoot.Services != null)
            {
                TechTreeService techTree = appRoot.Services.TechTree;
                state = techTree.GetState(technology).ToString();
                if (techTree.GetState(technology) == TechnologyState.Researching)
                {
                    state += "   " + techTree.RemainingSeconds.ToString("0.0") + "s remaining";
                }
            }

            return "Research time " + technology.ResearchSeconds.ToString("0.#") +
                   "s\nCost " + FormatCost(technology.Cost) + "   " + state;
        }

        private static string BuildWeaponDetail(WeaponDefinition weapon)
        {
            return "Damage " + weapon.Damage.ToString("0.#") + "   Range " +
                   weapon.Range.ToString("0.#") + "\nCost " + FormatCost(weapon.PurchaseCost);
        }

        private static string BuildBuildingDetail(BuildingDefinition building)
        {
            if (building == null)
            {
                return "Building information unavailable.";
            }

            Vector2 footprint = building.Footprint;
            TryGetConstructionAvailability(building, out string availability);
            return "Build time " + building.ConstructionSeconds.ToString("0.#") +
                   "s   Footprint " + footprint.x.ToString("0.#") + " x " +
                   footprint.y.ToString("0.#") + "\nCost " +
                   FormatCost(building.ConstructionCost) +
                   "\n" + availability +
                   "\nLeft-click a valid position to confirm.";
        }

        private static string BuildPromotionDetail(PromotionController promotion)
        {
            if (promotion == null || promotion.Definition == null)
            {
                return "Promotion information unavailable.";
            }

            PromotionDefinition definition = promotion.Definition;
            string target = definition.TargetUnit != null
                ? definition.TargetUnit.DisplayName
                : "Unknown unit";
            return "Promote to " + target + "\nCost " +
                   FormatCost(definition.ResourceCost) +
                   "   Target influence " + definition.TargetInfluence;
        }

        private static string FormatCost(ResourceCost cost)
        {
            if (cost.IsZero)
            {
                return "Free";
            }

            return cost.CommanderResource + "R  " + cost.Crystal + "C  " +
                   cost.Influence + "I";
        }
    }
}
