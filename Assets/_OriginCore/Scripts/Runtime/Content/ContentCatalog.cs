using System;
using System.Collections.Generic;
using System.Text;
using OriginCore.Gameplay;
using UnityEngine;

namespace OriginCore.Content
{
    [Serializable]
    public sealed class UnitContentRegistration
    {
        [SerializeField] private UnitDefinition _definition;
        [SerializeField] private GameObject _prefab;

        public UnitDefinition Definition => _definition;
        public GameObject Prefab => _prefab;

        public UnitContentRegistration()
        {
        }

        public UnitContentRegistration(UnitDefinition definition, GameObject prefab)
        {
            _definition = definition;
            _prefab = prefab;
        }
    }

    [CreateAssetMenu(fileName = "SO_ContentCatalog", menuName = "OriginCore/Content/Catalog")]
    public sealed class ContentCatalog : ScriptableObject
    {
        [SerializeField] private string _revision = "dev-1";
        [SerializeField] private CommanderDefinition[] _commanders = new CommanderDefinition[0];
        [SerializeField] private HeroDefinition[] _heroes = new HeroDefinition[0];
        [SerializeField] private HeroFormDefinition[] _heroForms = new HeroFormDefinition[0];
        [SerializeField] private UnitContentRegistration[] _units = new UnitContentRegistration[0];
        [SerializeField] private BuildingDefinition[] _buildings = new BuildingDefinition[0];
        [SerializeField] private AbilityDefinition[] _abilities = new AbilityDefinition[0];
        [SerializeField] private WeaponDefinition[] _weapons = new WeaponDefinition[0];
        [SerializeField] private TechnologyDefinition[] _technologies = new TechnologyDefinition[0];
        [SerializeField] private PromotionDefinition[] _promotions = new PromotionDefinition[0];
        [SerializeField] private MapDefinition[] _maps = new MapDefinition[0];
        [SerializeField] private MissionDefinition[] _missions = new MissionDefinition[0];

        private readonly Dictionary<string, UnityEngine.Object> _all =
            new Dictionary<string, UnityEngine.Object>(StringComparer.Ordinal);
        private readonly Dictionary<string, CommanderDefinition> _commanderIndex =
            new Dictionary<string, CommanderDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, HeroDefinition> _heroIndex =
            new Dictionary<string, HeroDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, HeroFormDefinition> _heroFormIndex =
            new Dictionary<string, HeroFormDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, UnitContentRegistration> _unitIndex =
            new Dictionary<string, UnitContentRegistration>(StringComparer.Ordinal);
        private readonly Dictionary<string, BuildingDefinition> _buildingIndex =
            new Dictionary<string, BuildingDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, AbilityDefinition> _abilityIndex =
            new Dictionary<string, AbilityDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, WeaponDefinition> _weaponIndex =
            new Dictionary<string, WeaponDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, TechnologyDefinition> _technologyIndex =
            new Dictionary<string, TechnologyDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, PromotionDefinition> _promotionIndex =
            new Dictionary<string, PromotionDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, MapDefinition> _mapIndex =
            new Dictionary<string, MapDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, MissionDefinition> _missionIndex =
            new Dictionary<string, MissionDefinition>(StringComparer.Ordinal);

        private bool _indexBuilt;

        public string Revision => string.IsNullOrWhiteSpace(_revision) ? "dev-1" : _revision.Trim();
        public CommanderDefinition[] Commanders => _commanders;
        public HeroDefinition[] Heroes => _heroes;
        public HeroFormDefinition[] HeroForms => _heroForms;
        public UnitContentRegistration[] Units => _units;
        public BuildingDefinition[] Buildings => _buildings;
        public AbilityDefinition[] Abilities => _abilities;
        public WeaponDefinition[] Weapons => _weapons;
        public TechnologyDefinition[] Technologies => _technologies;
        public PromotionDefinition[] Promotions => _promotions;
        public MapDefinition[] Maps => _maps;
        public MissionDefinition[] Missions => _missions;

        private void OnValidate()
        {
            _revision = string.IsNullOrWhiteSpace(_revision) ? "dev-1" : _revision.Trim();
            _commanders = _commanders ?? new CommanderDefinition[0];
            _heroes = _heroes ?? new HeroDefinition[0];
            _heroForms = _heroForms ?? new HeroFormDefinition[0];
            _units = _units ?? new UnitContentRegistration[0];
            _buildings = _buildings ?? new BuildingDefinition[0];
            _abilities = _abilities ?? new AbilityDefinition[0];
            _weapons = _weapons ?? new WeaponDefinition[0];
            _technologies = _technologies ?? new TechnologyDefinition[0];
            _promotions = _promotions ?? new PromotionDefinition[0];
            _maps = _maps ?? new MapDefinition[0];
            _missions = _missions ?? new MissionDefinition[0];
            _indexBuilt = false;
        }

        public bool TryValidate(out string error)
        {
            StringBuilder issues = new StringBuilder();
            BuildIndex(issues);
            ValidateUnitPrefabs(issues);
            ValidateCommanderReferences(issues);
            ValidateHeroReferences(issues);
            ValidateWeaponProfiles(issues);
            ValidateBuildingReferences(issues);
            ValidateTechnologyGraph(issues);
            ValidatePromotionReferences(issues);
            ValidateMapReferences(issues);
            error = issues.ToString();
            return error.Length == 0;
        }

        public bool TryGetAny(string contentId, out UnityEngine.Object definition)
        {
            EnsureIndex();
            return _all.TryGetValue(ContentIdUtility.Normalize(contentId), out definition);
        }

        public bool TryGetCommander(string contentId, out CommanderDefinition definition)
        {
            EnsureIndex();
            return _commanderIndex.TryGetValue(ContentIdUtility.Normalize(contentId), out definition);
        }

        public bool TryGetHero(string contentId, out HeroDefinition definition)
        {
            EnsureIndex();
            return _heroIndex.TryGetValue(ContentIdUtility.Normalize(contentId), out definition);
        }

        public bool TryGetHeroForm(string contentId, out HeroFormDefinition definition)
        {
            EnsureIndex();
            return _heroFormIndex.TryGetValue(ContentIdUtility.Normalize(contentId), out definition);
        }

        public bool TryGetUnit(string contentId, out UnitDefinition definition, out GameObject prefab)
        {
            EnsureIndex();
            if (_unitIndex.TryGetValue(
                    ContentIdUtility.Normalize(contentId),
                    out UnitContentRegistration registration) && registration != null)
            {
                definition = registration.Definition;
                prefab = registration.Prefab;
                return definition != null && prefab != null;
            }

            definition = null;
            prefab = null;
            return false;
        }

        public bool TryGetUnit(UnitDefinition definition, out GameObject prefab)
        {
            prefab = null;
            return definition != null && TryGetUnit(definition.ArchetypeId, out _, out prefab);
        }

        public bool TryGetBuilding(string contentId, out BuildingDefinition definition)
        {
            EnsureIndex();
            return _buildingIndex.TryGetValue(ContentIdUtility.Normalize(contentId), out definition);
        }

        public bool TryGetAbility(string contentId, out AbilityDefinition definition)
        {
            EnsureIndex();
            return _abilityIndex.TryGetValue(ContentIdUtility.Normalize(contentId), out definition);
        }

        public bool TryGetWeapon(string contentId, out WeaponDefinition definition)
        {
            EnsureIndex();
            return _weaponIndex.TryGetValue(ContentIdUtility.Normalize(contentId), out definition);
        }

        public bool TryGetTechnology(string contentId, out TechnologyDefinition definition)
        {
            EnsureIndex();
            return _technologyIndex.TryGetValue(ContentIdUtility.Normalize(contentId), out definition);
        }

        public bool TryGetPromotion(string contentId, out PromotionDefinition definition)
        {
            EnsureIndex();
            return _promotionIndex.TryGetValue(ContentIdUtility.Normalize(contentId), out definition);
        }

        public void Configure(
            string revision,
            CommanderDefinition[] commanders,
            HeroDefinition[] heroes,
            HeroFormDefinition[] heroForms,
            UnitContentRegistration[] units,
            BuildingDefinition[] buildings,
            AbilityDefinition[] abilities,
            WeaponDefinition[] weapons,
            TechnologyDefinition[] technologies,
            PromotionDefinition[] promotions,
            MapDefinition[] maps,
            MissionDefinition[] missions)
        {
            _revision = string.IsNullOrWhiteSpace(revision) ? "dev-1" : revision.Trim();
            _commanders = CloneOrEmpty(commanders);
            _heroes = CloneOrEmpty(heroes);
            _heroForms = CloneOrEmpty(heroForms);
            _units = CloneOrEmpty(units);
            _buildings = CloneOrEmpty(buildings);
            _abilities = CloneOrEmpty(abilities);
            _weapons = CloneOrEmpty(weapons);
            _technologies = CloneOrEmpty(technologies);
            _promotions = CloneOrEmpty(promotions);
            _maps = CloneOrEmpty(maps);
            _missions = CloneOrEmpty(missions);
            _indexBuilt = false;
        }

        public bool TryGetMap(string contentId, out MapDefinition definition)
        {
            EnsureIndex();
            return _mapIndex.TryGetValue(ContentIdUtility.Normalize(contentId), out definition);
        }

        public bool TryGetMission(string contentId, out MissionDefinition definition)
        {
            EnsureIndex();
            return _missionIndex.TryGetValue(ContentIdUtility.Normalize(contentId), out definition);
        }

        private void EnsureIndex()
        {
            if (!_indexBuilt)
            {
                BuildIndex(null);
            }
        }

        private void BuildIndex(StringBuilder issues)
        {
            ClearIndices();
            AddDefinitions(_commanders, _commanderIndex, "commander", issues);
            AddDefinitions(_heroes, _heroIndex, "hero", issues);
            AddDefinitions(_heroForms, _heroFormIndex, "hero form", issues);
            AddUnits(issues);
            AddDefinitions(_buildings, _buildingIndex, "building", issues);
            AddDefinitions(_abilities, _abilityIndex, "ability", issues);
            AddDefinitions(_weapons, _weaponIndex, "weapon", issues);
            AddDefinitions(_technologies, _technologyIndex, "technology", issues);
            AddDefinitions(_promotions, _promotionIndex, "promotion", issues);
            AddDefinitions(_maps, _mapIndex, "map", issues);
            AddDefinitions(_missions, _missionIndex, "mission", issues);
            _indexBuilt = true;
        }

        private void ClearIndices()
        {
            _all.Clear();
            _commanderIndex.Clear();
            _heroIndex.Clear();
            _heroFormIndex.Clear();
            _unitIndex.Clear();
            _buildingIndex.Clear();
            _abilityIndex.Clear();
            _weaponIndex.Clear();
            _technologyIndex.Clear();
            _promotionIndex.Clear();
            _mapIndex.Clear();
            _missionIndex.Clear();
        }

        private void AddDefinitions<T>(
            T[] values,
            Dictionary<string, T> target,
            string label,
            StringBuilder issues)
            where T : ContentDefinition
        {
            if (values == null)
            {
                return;
            }

            for (int i = 0; i < values.Length; i++)
            {
                T value = values[i];
                if (value == null)
                {
                    AppendIssue(issues, label + " entry " + i + " is null.");
                    continue;
                }

                string id = value.ContentId;
                if (!TryRegisterGlobal(id, value, label, issues))
                {
                    continue;
                }

                target[id] = value;
            }
        }

        private void AddUnits(StringBuilder issues)
        {
            if (_units == null)
            {
                return;
            }

            for (int i = 0; i < _units.Length; i++)
            {
                UnitContentRegistration registration = _units[i];
                if (registration == null || registration.Definition == null)
                {
                    AppendIssue(issues, "unit entry " + i + " has no definition.");
                    continue;
                }

                string id = registration.Definition.ArchetypeId;
                if (!TryRegisterGlobal(id, registration.Definition, "unit", issues))
                {
                    continue;
                }

                _unitIndex[id] = registration;
            }
        }

        private bool TryRegisterGlobal(
            string id,
            UnityEngine.Object value,
            string label,
            StringBuilder issues)
        {
            if (!ContentIdUtility.IsValid(id))
            {
                AppendIssue(issues, label + " has invalid content id '" + id + "'.");
                return false;
            }

            if (_all.TryGetValue(id, out UnityEngine.Object existing))
            {
                AppendIssue(
                    issues,
                    "content id '" + id + "' is duplicated by " + existing.name +
                    " and " + value.name + ".");
                return false;
            }

            _all.Add(id, value);
            return true;
        }

        private void ValidateUnitPrefabs(StringBuilder issues)
        {
            if (_units == null)
            {
                return;
            }

            for (int i = 0; i < _units.Length; i++)
            {
                UnitContentRegistration registration = _units[i];
                if (registration == null || registration.Definition == null)
                {
                    continue;
                }

                if (registration.Prefab == null)
                {
                    AppendIssue(issues, "unit '" + registration.Definition.ArchetypeId + "' has no prefab.");
                    continue;
                }

                EntityIdentity identity = registration.Prefab.GetComponent<EntityIdentity>();
                if (identity == null || identity.Definition != registration.Definition)
                {
                    AppendIssue(
                        issues,
                        "unit prefab '" + registration.Prefab.name +
                        "' does not own an EntityIdentity with the registered definition.");
                }
            }
        }

        private void ValidateHeroReferences(StringBuilder issues)
        {
            for (int i = 0; i < _heroes.Length; i++)
            {
                HeroDefinition hero = _heroes[i];
                if (hero == null)
                {
                    continue;
                }

                if (hero.UnitDefinition == null || hero.Prefab == null)
                {
                    AppendIssue(issues, "hero '" + hero.ContentId + "' requires a unit definition and prefab.");
                }
                else if (!_unitIndex.ContainsKey(hero.UnitDefinition.ArchetypeId))
                {
                    AppendIssue(issues, "hero '" + hero.ContentId + "' references an unregistered unit.");
                }

                ValidateAbilityArray(hero.RtsAbilities, hero.ContentId, issues);
                ValidateAbilityArray(hero.ActAbilities, hero.ContentId, issues);
                ValidateAbilityArray(hero.FpsAbilities, hero.ContentId, issues);
                WeaponDefinition[] weapons = hero.AllowedWeapons;
                for (int weaponIndex = 0; weaponIndex < weapons.Length; weaponIndex++)
                {
                    WeaponDefinition weapon = weapons[weaponIndex];
                    if (weapon == null || !_weaponIndex.ContainsKey(weapon.ContentId))
                    {
                        AppendIssue(issues, "hero '" + hero.ContentId + "' references an unregistered weapon.");
                    }
                }

                WeaponDefinition[] requiredWeapons = hero.RequiredActWeapons;
                if (requiredWeapons.Length > 4)
                {
                    AppendIssue(
                        issues,
                        "hero '" + hero.ContentId +
                        "' requires more weapons than the four ACT loadout slots can hold.");
                }

                for (int requiredIndex = 0;
                     requiredIndex < requiredWeapons.Length;
                     requiredIndex++)
                {
                    WeaponDefinition requiredWeapon = requiredWeapons[requiredIndex];
                    if (requiredWeapon == null)
                    {
                        AppendIssue(
                            issues,
                            "hero '" + hero.ContentId +
                            "' has a null required ACT weapon.");
                    }
                    else if (!_weaponIndex.ContainsKey(requiredWeapon.ContentId))
                    {
                        AppendIssue(
                            issues,
                            "hero '" + hero.ContentId +
                            "' requires an unregistered ACT weapon.");
                    }
                    else if (!ContainsReference(weapons, requiredWeapon))
                    {
                        AppendIssue(
                            issues,
                            "hero '" + hero.ContentId +
                            "' requires an ACT weapon that is not in AllowedWeapons.");
                    }
                    else if ((requiredWeapon.AvailableModes & WeaponModeMask.ACT) == 0)
                    {
                        AppendIssue(
                            issues,
                            "hero '" + hero.ContentId +
                            "' required weapon is not available in ACT mode.");
                    }
                    else
                    {
                        for (int previous = 0; previous < requiredIndex; previous++)
                        {
                            if (requiredWeapons[previous] == requiredWeapon)
                            {
                                AppendIssue(
                                    issues,
                                    "hero '" + hero.ContentId +
                                    "' contains a duplicate required ACT weapon.");
                                break;
                            }
                        }
                    }
                }

                HeroFormDefinition[] forms = hero.Forms;
                for (int formIndex = 0; formIndex < forms.Length; formIndex++)
                {
                    HeroFormDefinition form = forms[formIndex];
                    if (form == null || !_heroFormIndex.ContainsKey(form.ContentId))
                    {
                        AppendIssue(issues, "hero '" + hero.ContentId + "' references an unregistered form.");
                        continue;
                    }

                    ValidateAbilityArray(form.RtsAbilities, form.ContentId, issues);
                    ValidateAbilityArray(form.ActAbilities, form.ContentId, issues);
                    ValidateAbilityArray(form.FpsAbilities, form.ContentId, issues);
                    if (!string.IsNullOrEmpty(form.RequiredPreviousFormId) &&
                        !_heroFormIndex.ContainsKey(form.RequiredPreviousFormId))
                    {
                        AppendIssue(
                            issues,
                            "hero form '" + form.ContentId + "' has unknown predecessor '" +
                            form.RequiredPreviousFormId + "'.");
                    }
                }
            }
        }

        private void ValidateCommanderReferences(StringBuilder issues)
        {
            for (int i = 0; i < _commanders.Length; i++)
            {
                CommanderDefinition commander = _commanders[i];
                BuildingDefinition baseBuilding = commander != null
                    ? commander.BaseBuilding
                    : null;
                if (baseBuilding == null)
                {
                    continue;
                }

                if (!_buildingIndex.ContainsKey(baseBuilding.ContentId))
                {
                    AppendIssue(
                        issues,
                        "commander '" + commander.ContentId +
                        "' references an unregistered base building.");
                }
                else if (!ContainsReference(commander.Buildings, baseBuilding))
                {
                    AppendIssue(
                        issues,
                        "commander '" + commander.ContentId +
                        "' base building is not in the commander's building list.");
                }
            }
        }

        private void ValidateAbilityArray(
            AbilityDefinition[] abilities,
            string ownerId,
            StringBuilder issues)
        {
            if (abilities == null)
            {
                return;
            }

            for (int i = 0; i < abilities.Length; i++)
            {
                AbilityDefinition ability = abilities[i];
                if (ability == null)
                {
                    continue;
                }

                if (!_abilityIndex.ContainsKey(ability.ContentId))
                {
                    AppendIssue(issues, "content '" + ownerId + "' references an unregistered ability.");
                }
            }
        }

        private static bool ContainsReference<T>(T[] values, T candidate)
            where T : UnityEngine.Object
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

        private void ValidateWeaponProfiles(StringBuilder issues)
        {
            var actionSetIds = new HashSet<string>(StringComparer.Ordinal);
            var actionIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < _weapons.Length; i++)
            {
                WeaponDefinition weapon = _weapons[i];
                if (weapon == null)
                {
                    continue;
                }

                if (!weapon.TryValidateActionProfile(out string profileError))
                {
                    AppendIssue(issues, profileError);
                    continue;
                }

                bool isFormalYActionWeapon =
                    string.Equals(weapon.ContentId,
                        BuiltInWeaponActionLibrary.TimeslotWeaponId,
                        StringComparison.Ordinal) ||
                    string.Equals(weapon.ContentId,
                        BuiltInWeaponActionLibrary.SequenceWeaponId,
                        StringComparison.Ordinal) ||
                    string.Equals(weapon.ContentId,
                        BuiltInWeaponActionLibrary.FinalWingWeaponId,
                        StringComparison.Ordinal);
                if (isFormalYActionWeapon && !weapon.HasExplicitActActionSet)
                {
                    AppendIssue(
                        issues,
                        "formal Y weapon '" + weapon.ContentId +
                        "' must reference a serialized WeaponActionSetDefinition asset.");
                    continue;
                }

                if (!string.IsNullOrEmpty(weapon.RequiredFormId) &&
                    !_heroFormIndex.ContainsKey(weapon.RequiredFormId))
                {
                    AppendIssue(
                        issues,
                        "weapon '" + weapon.ContentId + "' requires unknown hero form '" +
                        weapon.RequiredFormId + "'.");
                }

                WeaponActionSetDefinition actionSet = weapon.ActActionSet;
                if (actionSet == null)
                {
                    continue;
                }

                if (!actionSetIds.Add(actionSet.ContentId))
                {
                    AppendIssue(
                        issues,
                        "weapon action set ID '" + actionSet.ContentId +
                        "' is referenced more than once; each formal weapon needs its own set.");
                }

                WeaponActionDefinition[] actions = actionSet.Actions;
                for (int actionIndex = 0; actionIndex < actions.Length; actionIndex++)
                {
                    WeaponActionDefinition action = actions[actionIndex];
                    if (action != null && !actionIds.Add(action.ActionId))
                    {
                        AppendIssue(
                            issues,
                            "weapon action ID '" + action.ActionId +
                            "' is duplicated across action sets.");
                    }
                }
            }
        }

        private void ValidateBuildingReferences(StringBuilder issues)
        {
            for (int i = 0; i < _buildings.Length; i++)
            {
                BuildingDefinition building = _buildings[i];
                if (building == null)
                {
                    continue;
                }

                if (building.EntityDefinition == null || building.Prefab == null)
                {
                    AppendIssue(
                        issues,
                        "building '" + building.ContentId + "' requires an entity definition and prefab.");
                }
                else if (!_unitIndex.ContainsKey(building.EntityDefinition.ArchetypeId))
                {
                    AppendIssue(issues, "building '" + building.ContentId + "' references an unregistered entity.");
                }

                TechnologyDefinition[] technologies = building.Technologies;
                for (int techIndex = 0; techIndex < technologies.Length; techIndex++)
                {
                    TechnologyDefinition technology = technologies[techIndex];
                    if (technology == null || !_technologyIndex.ContainsKey(technology.ContentId))
                    {
                        AppendIssue(issues, "building '" + building.ContentId + "' references an unregistered technology.");
                    }
                }
            }
        }

        private void ValidateTechnologyGraph(StringBuilder issues)
        {
            Dictionary<string, int> states = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < _technologies.Length; i++)
            {
                TechnologyDefinition technology = _technologies[i];
                if (technology != null)
                {
                    VisitTechnology(technology, states, issues);
                }
            }
        }

        private void ValidatePromotionReferences(StringBuilder issues)
        {
            for (int i = 0; i < _promotions.Length; i++)
            {
                PromotionDefinition promotion = _promotions[i];
                if (promotion == null)
                {
                    continue;
                }

                if (promotion.SourceUnit == null || promotion.TargetUnit == null)
                {
                    AppendIssue(
                        issues,
                        "promotion '" + promotion.ContentId + "' requires source and target units.");
                    continue;
                }

                if (!_unitIndex.ContainsKey(promotion.SourceUnit.ArchetypeId) ||
                    !_unitIndex.ContainsKey(promotion.TargetUnit.ArchetypeId))
                {
                    AppendIssue(
                        issues,
                        "promotion '" + promotion.ContentId + "' references an unregistered unit.");
                }
            }
        }

        private bool VisitTechnology(
            TechnologyDefinition technology,
            Dictionary<string, int> states,
            StringBuilder issues)
        {
            if (states.TryGetValue(technology.ContentId, out int state))
            {
                if (state == 1)
                {
                    AppendIssue(issues, "technology cycle contains '" + technology.ContentId + "'.");
                    return false;
                }

                return state == 2;
            }

            states[technology.ContentId] = 1;
            string[] prerequisites = technology.PrerequisiteTechnologyIds;
            for (int i = 0; i < prerequisites.Length; i++)
            {
                string prerequisiteId = prerequisites[i];
                if (prerequisiteId == technology.ContentId)
                {
                    AppendIssue(issues, "technology '" + technology.ContentId + "' references itself.");
                    continue;
                }

                if (!_technologyIndex.TryGetValue(prerequisiteId, out TechnologyDefinition prerequisite))
                {
                    AppendIssue(
                        issues,
                        "technology '" + technology.ContentId + "' has unknown prerequisite '" +
                        prerequisiteId + "'.");
                    continue;
                }

                VisitTechnology(prerequisite, states, issues);
            }

            states[technology.ContentId] = 2;
            return true;
        }

        private void ValidateMapReferences(StringBuilder issues)
        {
            for (int i = 0; i < _maps.Length; i++)
            {
                MapDefinition map = _maps[i];
                if (map == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(map.SceneKey))
                {
                    AppendIssue(issues, "map '" + map.ContentId + "' has no scene key.");
                }
                if (map.MinimapDefinition == null)
                {
                    AppendIssue(issues, "map '" + map.ContentId + "' has no minimap definition.");
                }
                if (map.VisibilityElevation == null)
                {
                    AppendIssue(issues, "map '" + map.ContentId + "' has no visibility elevation definition.");
                }
                if (map.EnemyAiSetup != null &&
                    !map.EnemyAiSetup.TryValidate(out string aiError))
                {
                    AppendIssue(
                        issues,
                        "map '" + map.ContentId + "' has invalid enemy AI: " + aiError);
                }

                MissionDefinition[] missions = map.Missions;
                for (int missionIndex = 0; missionIndex < missions.Length; missionIndex++)
                {
                    MissionDefinition mission = missions[missionIndex];
                    if (mission == null || !_missionIndex.ContainsKey(mission.ContentId))
                    {
                        AppendIssue(issues, "map '" + map.ContentId + "' references an unregistered mission.");
                    }
                }
            }
        }

        private static void AppendIssue(StringBuilder issues, string issue)
        {
            if (issues == null)
            {
                return;
            }

            if (issues.Length > 0)
            {
                issues.AppendLine();
            }

            issues.Append(issue);
        }

        private static T[] CloneOrEmpty<T>(T[] values)
        {
            return values != null ? (T[])values.Clone() : new T[0];
        }
    }
}
