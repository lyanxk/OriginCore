using System;
using System.Collections.Generic;
using OriginCore.Content;
using OriginCore.Core;
using OriginCore.Matches;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OriginCore.UI.MainMenu
{
    [DisallowMultipleComponent]
    public sealed class MatchSetupView : MonoBehaviour
    {
        [SerializeField] private Button _mapSlotButton;
        [SerializeField] private Button _commanderSlotButton;
        [SerializeField] private Button _heroSlotButton;
        [SerializeField] private Button[] _actWeaponSlots = new Button[4];
        [SerializeField] private Button[] _fpsWeaponSlots = new Button[6];
        [SerializeField] private RectTransform _choiceListRoot;
        [SerializeField] private Button _choiceButtonTemplate;
        [SerializeField] private TMP_Text _choiceTitle;
        [SerializeField] private TMP_Text _statusLabel;
        [SerializeField] private Button _confirmButton;
        [SerializeField] private Button _backButton;

        private readonly List<Button> _generatedChoices = new List<Button>();
        private readonly string[] _actWeaponIds = new string[4];
        private readonly string[] _fpsWeaponIds = new string[6];
        private ContentCatalog _catalog;
        private MapDefinition _selectedMap;
        private CommanderDefinition _selectedCommander;
        private HeroDefinition _selectedHero;
        private MatchGameModeSource _source = MatchGameModeSource.Skirmish;
        private string _preferredMapId = string.Empty;
        private bool _controlsBound;

        public event Action<MatchConfiguration> Confirmed;
        public event Action BackRequested;

        public MatchConfiguration CurrentConfiguration => BuildConfiguration();

        private void OnEnable()
        {
            BindControls();
            TryBindCatalog();
            EnsureDefaults();
            RefreshAll();
        }

        private void OnDisable()
        {
            ClearGeneratedChoices();
        }

        private void OnDestroy()
        {
            if (!_controlsBound)
            {
                return;
            }

            _mapSlotButton?.onClick.RemoveListener(OpenMapChoices);
            _commanderSlotButton?.onClick.RemoveListener(OpenCommanderChoices);
            _heroSlotButton?.onClick.RemoveListener(OpenHeroChoices);
            _confirmButton?.onClick.RemoveListener(Confirm);
            _backButton?.onClick.RemoveListener(RequestBack);
            for (int i = 0; i < _actWeaponSlots.Length; i++)
            {
                int slot = i;
                _actWeaponSlots[i]?.onClick.RemoveListener(() => OpenActWeaponChoices(slot));
            }

            for (int i = 0; i < _fpsWeaponSlots.Length; i++)
            {
                int slot = i;
                _fpsWeaponSlots[i]?.onClick.RemoveListener(() => OpenFpsWeaponChoices(slot));
            }
        }

        public void Show(MatchGameModeSource source, string preferredMapId)
        {
            _source = source;
            _preferredMapId = ContentIdUtility.Normalize(preferredMapId);
            TryBindCatalog();
            EnsureDefaults();
            SelectPreferredMap();
            RefreshAll();
        }

        public void Configure(
            Button mapSlotButton,
            Button commanderSlotButton,
            Button heroSlotButton,
            Button[] actWeaponSlots,
            Button[] fpsWeaponSlots,
            RectTransform choiceListRoot,
            Button choiceButtonTemplate,
            TMP_Text choiceTitle,
            TMP_Text statusLabel,
            Button confirmButton,
            Button backButton)
        {
            _mapSlotButton = mapSlotButton;
            _commanderSlotButton = commanderSlotButton;
            _heroSlotButton = heroSlotButton;
            _actWeaponSlots = NormalizeButtons(actWeaponSlots, 4);
            _fpsWeaponSlots = NormalizeButtons(fpsWeaponSlots, 6);
            _choiceListRoot = choiceListRoot;
            _choiceButtonTemplate = choiceButtonTemplate;
            _choiceTitle = choiceTitle;
            _statusLabel = statusLabel;
            _confirmButton = confirmButton;
            _backButton = backButton;
            if (Application.isPlaying)
            {
                BindControls();
            }
        }

        public void OpenMapChoices()
        {
            ClearGeneratedChoices();
            SetChoiceTitle("SELECT MAP");
            if (_catalog == null)
            {
                return;
            }

            MapDefinition[] maps = _catalog.Maps;
            for (int i = 0; i < maps.Length; i++)
            {
                MapDefinition map = maps[i];
                if (map == null || (_source == MatchGameModeSource.Skirmish && !map.AllowSkirmish))
                {
                    continue;
                }

                MapDefinition captured = map;
                AddChoice(map.DisplayName, () => SelectMap(captured));
            }
        }

        public void OpenCommanderChoices()
        {
            ClearGeneratedChoices();
            SetChoiceTitle("SELECT COMMANDER");
            if (_catalog == null)
            {
                return;
            }

            CommanderDefinition[] commanders = _catalog.Commanders;
            for (int i = 0; i < commanders.Length; i++)
            {
                CommanderDefinition commander = commanders[i];
                if (commander == null || !IsCommanderAllowed(commander))
                {
                    continue;
                }

                CommanderDefinition captured = commander;
                AddChoice(commander.DisplayName, () => SelectCommander(captured));
            }
        }

        public void OpenHeroChoices()
        {
            ClearGeneratedChoices();
            SetChoiceTitle("SELECT HERO");
            if (_catalog == null)
            {
                return;
            }

            HeroDefinition[] heroes = _catalog.Heroes;
            for (int i = 0; i < heroes.Length; i++)
            {
                HeroDefinition hero = heroes[i];
                if (hero == null || !IsHeroAllowed(hero))
                {
                    continue;
                }

                HeroDefinition captured = hero;
                AddChoice(hero.DisplayName, () => SelectHero(captured));
            }
        }

        public void OpenActWeaponChoices(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _actWeaponIds.Length)
            {
                return;
            }

            OpenWeaponChoices(slotIndex, true);
        }

        public void OpenFpsWeaponChoices(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _fpsWeaponIds.Length)
            {
                return;
            }

            OpenWeaponChoices(slotIndex, false);
        }

        public void Confirm()
        {
            MatchConfiguration configuration = BuildConfiguration();
            string error = "CONTENT CATALOG UNAVAILABLE";
            if (_catalog == null || !configuration.TryValidate(_catalog, out error))
            {
                SetStatus(error);
                return;
            }

            Confirmed?.Invoke(configuration);
        }

        private void RequestBack()
        {
            BackRequested?.Invoke();
        }

        private void OpenWeaponChoices(int slotIndex, bool act)
        {
            ClearGeneratedChoices();
            WeaponDefinition requiredActWeapon = act
                ? RequiredActWeaponAt(slotIndex)
                : null;
            if (requiredActWeapon != null)
            {
                SetChoiceTitle("REQUIRED ACT WEAPON");
                AddChoice(
                    requiredActWeapon.DisplayName + " (REQUIRED)",
                    () => SelectWeapon(slotIndex, true, requiredActWeapon));
                return;
            }

            SetChoiceTitle((act ? "ACT" : "FPS") + " WEAPON SLOT " + (slotIndex + 1));
            AddChoice("EMPTY", () => SelectWeapon(slotIndex, act, null));
            if (_catalog == null)
            {
                return;
            }

            WeaponDefinition[] weapons = _catalog.Weapons;
            for (int i = 0; i < weapons.Length; i++)
            {
                WeaponDefinition weapon = weapons[i];
                WeaponModeMask required = act ? WeaponModeMask.ACT : WeaponModeMask.FPS;
                if (weapon == null || (weapon.AvailableModes & required) == 0 ||
                    !IsWeaponAllowed(weapon) ||
                    act && IsRequiredActWeapon(weapon))
                {
                    continue;
                }

                WeaponDefinition captured = weapon;
                AddChoice(weapon.DisplayName, () => SelectWeapon(slotIndex, act, captured));
            }
        }

        private void SelectMap(MapDefinition map)
        {
            _selectedMap = map;
            // The preferred map is only an initial page default. Once the player makes
            // an explicit choice it must not overwrite that choice in EnsureDefaults.
            _preferredMapId = string.Empty;
            if (!IsCommanderAllowed(_selectedCommander))
            {
                _selectedCommander = null;
            }

            if (!IsHeroAllowed(_selectedHero))
            {
                _selectedHero = null;
            }

            EnsureDefaults();
            ClearGeneratedChoices();
            RefreshAll();
        }

        private void SelectCommander(CommanderDefinition commander)
        {
            _selectedCommander = commander;
            ClearGeneratedChoices();
            RefreshAll();
        }

        private void SelectHero(HeroDefinition hero)
        {
            _selectedHero = hero;
            for (int i = 0; i < _actWeaponIds.Length; i++)
            {
                if (!IsWeaponAllowed(ResolveWeapon(_actWeaponIds[i])))
                {
                    _actWeaponIds[i] = string.Empty;
                }
            }

            for (int i = 0; i < _fpsWeaponIds.Length; i++)
            {
                if (!IsWeaponAllowed(ResolveWeapon(_fpsWeaponIds[i])))
                {
                    _fpsWeaponIds[i] = string.Empty;
                }
            }

            PopulateWeaponDefaults();

            ClearGeneratedChoices();
            RefreshAll();
        }

        private void SelectWeapon(int slotIndex, bool act, WeaponDefinition weapon)
        {
            string[] slots = act ? _actWeaponIds : _fpsWeaponIds;
            string id = weapon != null ? weapon.ContentId : string.Empty;
            if (!string.IsNullOrEmpty(id))
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    if (i != slotIndex && string.Equals(slots[i], id, StringComparison.Ordinal))
                    {
                        slots[i] = string.Empty;
                    }
                }
            }

            slots[slotIndex] = id;
            MatchConfiguration.EnsureRequiredActWeapons(
                _selectedHero,
                _actWeaponIds);
            ClearGeneratedChoices();
            RefreshAll();
        }

        private void EnsureDefaults()
        {
            if (_catalog == null)
            {
                return;
            }

            SelectPreferredMap();
            if (!IsCommanderAllowed(_selectedCommander))
            {
                _selectedCommander = FirstAllowedCommander();
            }

            if (!IsHeroAllowed(_selectedHero))
            {
                _selectedHero = FirstAllowedHero();
                PopulateWeaponDefaults();
            }
        }

        private void PopulateWeaponDefaults()
        {
            if (_catalog == null || _selectedHero == null)
            {
                return;
            }

            MatchConfiguration.EnsureRequiredActWeapons(
                _selectedHero,
                _actWeaponIds);

            WeaponDefinition[] weapons = _catalog.Weapons;
            for (int i = 0; i < weapons.Length; i++)
            {
                WeaponDefinition weapon = weapons[i];
                if (weapon == null || !IsWeaponAllowed(weapon))
                {
                    continue;
                }

                if ((weapon.AvailableModes & WeaponModeMask.ACT) != 0)
                {
                    AddFirstEmptyUnique(_actWeaponIds, weapon.ContentId);
                }
                if ((weapon.AvailableModes & WeaponModeMask.FPS) != 0)
                {
                    AddFirstEmptyUnique(_fpsWeaponIds, weapon.ContentId);
                }
            }
        }

        private void SelectPreferredMap()
        {
            if (_catalog == null || _selectedMap != null &&
                (string.IsNullOrEmpty(_preferredMapId) || _selectedMap.ContentId == _preferredMapId))
            {
                return;
            }

            if (!string.IsNullOrEmpty(_preferredMapId) &&
                _catalog.TryGetMap(_preferredMapId, out MapDefinition preferred))
            {
                _selectedMap = preferred;
                return;
            }

            MapDefinition[] maps = _catalog.Maps;
            for (int i = 0; i < maps.Length; i++)
            {
                if (maps[i] != null &&
                    (_source != MatchGameModeSource.Skirmish || maps[i].AllowSkirmish))
                {
                    _selectedMap = maps[i];
                    return;
                }
            }
        }

        private CommanderDefinition FirstAllowedCommander()
        {
            CommanderDefinition[] values = _catalog.Commanders;
            for (int i = 0; i < values.Length; i++)
            {
                if (IsCommanderAllowed(values[i]))
                {
                    return values[i];
                }
            }

            return null;
        }

        private HeroDefinition FirstAllowedHero()
        {
            HeroDefinition[] values = _catalog.Heroes;
            for (int i = 0; i < values.Length; i++)
            {
                if (IsHeroAllowed(values[i]))
                {
                    return values[i];
                }
            }

            return null;
        }

        private bool IsCommanderAllowed(CommanderDefinition commander)
        {
            if (commander == null || _selectedMap == null)
            {
                return false;
            }

            CommanderDefinition[] allowed = _selectedMap.AllowedCommanders;
            return allowed == null || allowed.Length == 0 || Contains(allowed, commander);
        }

        private bool IsHeroAllowed(HeroDefinition hero)
        {
            if (hero == null || _selectedMap == null)
            {
                return false;
            }

            HeroDefinition[] allowed = _selectedMap.AllowedHeroes;
            return allowed == null || allowed.Length == 0 || Contains(allowed, hero);
        }

        private bool IsWeaponAllowed(WeaponDefinition weapon)
        {
            if (weapon == null || _selectedHero == null)
            {
                return false;
            }

            WeaponDefinition[] allowed = _selectedHero.AllowedWeapons;
            return allowed == null || allowed.Length == 0 || Contains(allowed, weapon);
        }

        private WeaponDefinition ResolveWeapon(string id)
        {
            return _catalog != null && !string.IsNullOrEmpty(id) &&
                   _catalog.TryGetWeapon(id, out WeaponDefinition weapon)
                ? weapon
                : null;
        }

        private MatchConfiguration BuildConfiguration()
        {
            MatchConfiguration.EnsureRequiredActWeapons(
                _selectedHero,
                _actWeaponIds);
            MatchConfiguration configuration = new MatchConfiguration
            {
                gameModeSource = _source,
                mapId = _selectedMap != null ? _selectedMap.ContentId : string.Empty,
                sceneKey = _selectedMap != null ? _selectedMap.SceneKey : string.Empty,
                missionId = _source == MatchGameModeSource.Story && _selectedMap != null &&
                            _selectedMap.Missions.Length > 0 && _selectedMap.Missions[0] != null
                    ? _selectedMap.Missions[0].ContentId
                    : string.Empty,
                commanderId = _selectedCommander != null ? _selectedCommander.ContentId : string.Empty,
                heroId = _selectedHero != null ? _selectedHero.ContentId : string.Empty,
                actWeaponIds = CloneSlots(_actWeaponIds),
                fpsAvailableWeaponIds = CloneSlots(_fpsWeaponIds),
                contentRevision = _catalog != null ? _catalog.Revision : string.Empty
            };
            ResolveEquippedWeapons(configuration);
            return configuration;
        }

        private void ResolveEquippedWeapons(MatchConfiguration configuration)
        {
            for (int i = 0; i < _fpsWeaponIds.Length; i++)
            {
                WeaponDefinition weapon = ResolveWeapon(_fpsWeaponIds[i]);
                if (weapon == null)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(configuration.fpsEquippedPrimary) &&
                    (weapon.AllowedSlots & WeaponSlotMask.Primary) != 0)
                {
                    configuration.fpsEquippedPrimary = weapon.ContentId;
                }
                else if (string.IsNullOrEmpty(configuration.fpsEquippedSecondary) &&
                         (weapon.AllowedSlots & WeaponSlotMask.Secondary) != 0)
                {
                    configuration.fpsEquippedSecondary = weapon.ContentId;
                }

                if (string.IsNullOrEmpty(configuration.fpsEquippedMelee) &&
                    (weapon.AllowedSlots & WeaponSlotMask.Melee) != 0)
                {
                    configuration.fpsEquippedMelee = weapon.ContentId;
                }
            }
        }

        private void RefreshAll()
        {
            MatchConfiguration.EnsureRequiredActWeapons(
                _selectedHero,
                _actWeaponIds);
            SetButtonLabel(_mapSlotButton, "MAP\n" + NameOf(_selectedMap));
            SetButtonLabel(_commanderSlotButton, "COMMANDER\n" + NameOf(_selectedCommander));
            SetButtonLabel(_heroSlotButton, "HERO\n" + NameOf(_selectedHero));
            for (int i = 0; i < _actWeaponSlots.Length; i++)
            {
                WeaponDefinition weapon = ResolveWeapon(_actWeaponIds[i]);
                bool required = RequiredActWeaponAt(i) == weapon && weapon != null;
                SetButtonLabel(
                    _actWeaponSlots[i],
                    "ACT " + (i + 1) + "\n" + NameOf(weapon) +
                    (required ? " [REQUIRED]" : string.Empty));
            }

            for (int i = 0; i < _fpsWeaponSlots.Length; i++)
            {
                SetButtonLabel(
                    _fpsWeaponSlots[i],
                    "FPS " + (i + 1) + "\n" + NameOf(ResolveWeapon(_fpsWeaponIds[i])));
            }

            MatchConfiguration configuration = BuildConfiguration();
            string error = "CONTENT CATALOG UNAVAILABLE";
            bool valid = _catalog != null && configuration.TryValidate(_catalog, out error);
            if (_confirmButton != null)
            {
                _confirmButton.interactable = valid;
            }

            SetStatus(valid ? "READY" : error);
        }

        private WeaponDefinition RequiredActWeaponAt(int slotIndex)
        {
            WeaponDefinition[] required = _selectedHero != null
                ? _selectedHero.RequiredActWeapons
                : null;
            return required != null && slotIndex >= 0 && slotIndex < required.Length
                ? required[slotIndex]
                : null;
        }

        private bool IsRequiredActWeapon(WeaponDefinition weapon)
        {
            WeaponDefinition[] required = _selectedHero != null
                ? _selectedHero.RequiredActWeapons
                : null;
            return weapon != null && required != null && Contains(required, weapon);
        }

        private void TryBindCatalog()
        {
            if (_catalog != null || !AppRoot.TryGetInstance(out AppRoot appRoot) ||
                appRoot.Services == null || appRoot.Services.ContentCatalog == null)
            {
                return;
            }

            _catalog = appRoot.Services.ContentCatalog.Catalog;
        }

        private void BindControls()
        {
            if (_controlsBound || _mapSlotButton == null || _commanderSlotButton == null ||
                _heroSlotButton == null || _confirmButton == null || _backButton == null)
            {
                return;
            }

            _mapSlotButton.onClick.AddListener(OpenMapChoices);
            _commanderSlotButton.onClick.AddListener(OpenCommanderChoices);
            _heroSlotButton.onClick.AddListener(OpenHeroChoices);
            for (int i = 0; i < _actWeaponSlots.Length; i++)
            {
                int slot = i;
                _actWeaponSlots[i]?.onClick.AddListener(() => OpenActWeaponChoices(slot));
            }

            for (int i = 0; i < _fpsWeaponSlots.Length; i++)
            {
                int slot = i;
                _fpsWeaponSlots[i]?.onClick.AddListener(() => OpenFpsWeaponChoices(slot));
            }

            _confirmButton.onClick.AddListener(Confirm);
            _backButton.onClick.AddListener(RequestBack);
            _controlsBound = true;
        }

        private void AddChoice(string label, UnityEngine.Events.UnityAction action)
        {
            if (_choiceButtonTemplate == null || _choiceListRoot == null)
            {
                return;
            }

            Button button = Instantiate(_choiceButtonTemplate, _choiceListRoot);
            button.name = "Choice_" + _generatedChoices.Count;
            button.gameObject.SetActive(true);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
            SetButtonLabel(button, label);
            _generatedChoices.Add(button);
        }

        private void ClearGeneratedChoices()
        {
            for (int i = 0; i < _generatedChoices.Count; i++)
            {
                if (_generatedChoices[i] != null)
                {
                    Destroy(_generatedChoices[i].gameObject);
                }
            }

            _generatedChoices.Clear();
        }

        private void SetChoiceTitle(string value)
        {
            if (_choiceTitle != null)
            {
                _choiceTitle.text = value ?? string.Empty;
            }
        }

        private void SetStatus(string value)
        {
            if (_statusLabel != null)
            {
                _statusLabel.text = value ?? string.Empty;
            }
        }

        private static void SetButtonLabel(Button button, string value)
        {
            TMP_Text label = button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
            if (label != null)
            {
                label.text = value ?? string.Empty;
            }
        }

        private static string NameOf(IContentDefinition definition)
        {
            return definition != null ? definition.DisplayName : "EMPTY";
        }

        private static bool Contains<T>(T[] values, T candidate) where T : UnityEngine.Object
        {
            if (values == null || candidate == null)
            {
                return false;
            }

            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] == candidate)
                {
                    return true;
                }
            }

            return false;
        }

        private static string[] CloneSlots(string[] values)
        {
            string[] clone = new string[values.Length];
            Array.Copy(values, clone, values.Length);
            return clone;
        }

        private static void AddFirstEmptyUnique(string[] slots, string id)
        {
            if (slots == null || string.IsNullOrEmpty(id))
            {
                return;
            }

            int emptyIndex = -1;
            for (int i = 0; i < slots.Length; i++)
            {
                if (string.Equals(slots[i], id, StringComparison.Ordinal))
                {
                    return;
                }
                if (emptyIndex < 0 && string.IsNullOrEmpty(slots[i]))
                {
                    emptyIndex = i;
                }
            }

            if (emptyIndex >= 0)
            {
                slots[emptyIndex] = id;
            }
        }

        private static Button[] NormalizeButtons(Button[] values, int size)
        {
            Button[] normalized = new Button[size];
            if (values != null)
            {
                Array.Copy(values, normalized, Mathf.Min(values.Length, size));
            }

            return normalized;
        }
    }
}
