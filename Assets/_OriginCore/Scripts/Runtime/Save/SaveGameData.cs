using System;
using System.Collections.Generic;
using System.Globalization;
using OriginCore.Combat;
using OriginCore.Core;
using OriginCore.Economy;
using OriginCore.Gameplay;
using OriginCore.Matches;
using OriginCore.AI;
using UnityEngine;

namespace OriginCore.Save
{
    [Serializable]
    public sealed class SaveGameData
    {
        public const int CurrentSchemaVersion = 3;
        public const int MaximumDisplayNameLength = 48;

        public int schemaVersion = CurrentSchemaVersion;
        public string slotId = string.Empty;
        public string displayName = string.Empty;
        public string savedAtUtc = string.Empty;
        public string sceneKey = string.Empty;
        public int gameMode = (int)GameMode.RTS;
        public MatchConfigurationSaveData matchConfiguration = new MatchConfigurationSaveData();
        public ResourceSaveData resources = new ResourceSaveData();
        public List<EntitySaveData> entities = new List<EntitySaveData>();
        public List<string> removedSceneEntityIds = new List<string>();
        public ConstructionSaveData construction = new ConstructionSaveData();
        public HeroDeploymentSaveData heroDeployment = new HeroDeploymentSaveData();
        public List<RallyPointSaveData> rallyPoints = new List<RallyPointSaveData>();
        public List<ProductionQueueSaveData> productionQueues = new List<ProductionQueueSaveData>();
        public List<ResourceNodeSaveData> resourceNodes = new List<ResourceNodeSaveData>();
        public List<GathererCargoSaveData> gathererCargo = new List<GathererCargoSaveData>();
        public List<InventoryAbilitySaveData> inventoryAbilities = new List<InventoryAbilitySaveData>();
        public TechnologySaveData technology = new TechnologySaveData();
        public VisibilitySaveData visibility = new VisibilitySaveData();
        public MissionSaveData mission = new MissionSaveData();
        public List<EnemyAiSaveData> enemyAi = new List<EnemyAiSaveData>();

        public GameMode SavedGameMode
        {
            get
            {
                return Enum.IsDefined(typeof(GameMode), gameMode)
                    ? (GameMode)gameMode
                    : GameMode.RTS;
            }
            set => gameMode = (int)value;
        }

        public DateTime SavedAt
        {
            get
            {
                return DateTime.TryParse(
                    savedAtUtc,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out DateTime parsed)
                    ? parsed.ToUniversalTime()
                    : DateTime.MinValue;
            }
        }

        public static SaveGameData Create(
            string newSlotId,
            string newDisplayName,
            string newSceneKey,
            DateTime utcNow)
        {
            MatchConfiguration fallback = MatchConfiguration.CreateSystemTestFallback();
            fallback.sceneKey = string.IsNullOrWhiteSpace(newSceneKey)
                ? string.Empty
                : newSceneKey.Trim();
            SaveGameData data = new SaveGameData
            {
                schemaVersion = CurrentSchemaVersion,
                slotId = newSlotId ?? string.Empty,
                displayName = NormalizeDisplayName(newDisplayName),
                sceneKey = fallback.sceneKey,
                savedAtUtc = utcNow.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)
            };
            data.matchConfiguration.CopyFrom(fallback);
            data.EnsureCollections();
            return data;
        }

        public void EnsureCollections()
        {
            matchConfiguration = matchConfiguration ?? new MatchConfigurationSaveData();
            matchConfiguration.EnsureCollections();
            resources = resources ?? new ResourceSaveData();
            entities = entities ?? new List<EntitySaveData>();
            for (int i = 0; i < entities.Count; i++)
            {
                if (entities[i] != null)
                {
                    entities[i].EnsureCollections();
                }
            }
            removedSceneEntityIds = removedSceneEntityIds ?? new List<string>();
            construction = construction ?? new ConstructionSaveData();
            construction.EnsureCollections();
            heroDeployment = heroDeployment ?? new HeroDeploymentSaveData();
            heroDeployment.Normalize();
            rallyPoints = rallyPoints ?? new List<RallyPointSaveData>();
            productionQueues = productionQueues ?? new List<ProductionQueueSaveData>();
            for (int i = 0; i < productionQueues.Count; i++)
            {
                productionQueues[i]?.EnsureCollections();
            }

            resourceNodes = resourceNodes ?? new List<ResourceNodeSaveData>();
            gathererCargo = gathererCargo ?? new List<GathererCargoSaveData>();

            inventoryAbilities = inventoryAbilities ?? new List<InventoryAbilitySaveData>();
            for (int i = 0; i < inventoryAbilities.Count; i++)
            {
                inventoryAbilities[i]?.EnsureCollections();
            }

            technology = technology ?? new TechnologySaveData();
            technology.EnsureCollections();
            visibility = visibility ?? new VisibilitySaveData();
            visibility.EnsureCollections();
            mission = mission ?? new MissionSaveData();
            mission.EnsureCollections();
            enemyAi = enemyAi ?? new List<EnemyAiSaveData>();
        }

        public void NormalizeForWrite()
        {
            EnsureCollections();
            schemaVersion = CurrentSchemaVersion;
            slotId = string.IsNullOrWhiteSpace(slotId) ? string.Empty : slotId.Trim();
            displayName = NormalizeDisplayName(displayName);
            sceneKey = string.IsNullOrWhiteSpace(sceneKey) ? string.Empty : sceneKey.Trim();
            matchConfiguration.Normalize();
            if (string.IsNullOrWhiteSpace(matchConfiguration.sceneKey))
            {
                matchConfiguration.sceneKey = sceneKey;
            }

            if (string.IsNullOrWhiteSpace(matchConfiguration.contentRevision))
            {
                matchConfiguration.contentRevision = "unversioned";
            }

            if (!Enum.IsDefined(typeof(GameMode), gameMode))
            {
                gameMode = (int)GameMode.RTS;
            }

            if (!DateTime.TryParse(
                    savedAtUtc,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out DateTime parsed))
            {
                savedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            }
            else
            {
                savedAtUtc = parsed.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
            }
        }

        public bool TryValidate(out string error)
        {
            EnsureCollections();
            if (schemaVersion != CurrentSchemaVersion)
            {
                error = "Unsupported save schema version " + schemaVersion + ".";
                return false;
            }

            if (!SaveRepository.IsValidSlotId(slotId))
            {
                error = "Save slot id is missing or invalid.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                error = "Save display name is empty.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(sceneKey))
            {
                error = "Save scene key is empty.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(matchConfiguration.sceneKey) &&
                !string.Equals(matchConfiguration.sceneKey, sceneKey, StringComparison.Ordinal))
            {
                error = "Save match configuration scene key does not match the save header.";
                return false;
            }

            if (SavedAt == DateTime.MinValue)
            {
                error = "Save timestamp is invalid.";
                return false;
            }

            if (!Enum.IsDefined(typeof(GameMode), gameMode))
            {
                error = "Save game mode is invalid.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public static string NormalizeDisplayName(string value)
        {
            string normalized = string.IsNullOrWhiteSpace(value)
                ? "Save"
                : value.Trim();
            return normalized.Length <= MaximumDisplayNameLength
                ? normalized
                : normalized.Substring(0, MaximumDisplayNameLength);
        }
    }

    [Serializable]
    public sealed class MatchConfigurationSaveData
    {
        public int gameModeSource = (int)MatchGameModeSource.Skirmish;
        public string mapId = string.Empty;
        public string sceneKey = string.Empty;
        public string missionId = string.Empty;
        public string commanderId = string.Empty;
        public string heroId = string.Empty;
        public string[] actWeaponIds = new string[4];
        public string[] fpsAvailableWeaponIds = new string[6];
        public string fpsEquippedPrimary = string.Empty;
        public string fpsEquippedSecondary = string.Empty;
        public string fpsEquippedMelee = string.Empty;
        public string contentRevision = string.Empty;

        public bool IsConfigured => !string.IsNullOrWhiteSpace(sceneKey) ||
                                    !string.IsNullOrWhiteSpace(mapId) ||
                                    !string.IsNullOrWhiteSpace(commanderId) ||
                                    !string.IsNullOrWhiteSpace(heroId);

        public void EnsureCollections()
        {
            actWeaponIds = actWeaponIds ?? new string[4];
            fpsAvailableWeaponIds = fpsAvailableWeaponIds ?? new string[6];
        }

        public void Normalize()
        {
            EnsureCollections();
            MatchConfiguration configuration = ToConfiguration();
            configuration.Normalize();
            CopyFrom(configuration);
        }

        public MatchConfiguration ToConfiguration()
        {
            EnsureCollections();
            return new MatchConfiguration
            {
                gameModeSource = Enum.IsDefined(typeof(MatchGameModeSource), gameModeSource)
                    ? (MatchGameModeSource)gameModeSource
                    : MatchGameModeSource.Skirmish,
                mapId = mapId ?? string.Empty,
                sceneKey = sceneKey ?? string.Empty,
                missionId = missionId ?? string.Empty,
                commanderId = commanderId ?? string.Empty,
                heroId = heroId ?? string.Empty,
                actWeaponIds = CloneArray(actWeaponIds),
                fpsAvailableWeaponIds = CloneArray(fpsAvailableWeaponIds),
                fpsEquippedPrimary = fpsEquippedPrimary ?? string.Empty,
                fpsEquippedSecondary = fpsEquippedSecondary ?? string.Empty,
                fpsEquippedMelee = fpsEquippedMelee ?? string.Empty,
                contentRevision = contentRevision ?? string.Empty
            };
        }

        public void CopyFrom(MatchConfiguration configuration)
        {
            if (configuration == null)
            {
                configuration = MatchConfiguration.CreateSystemTestFallback();
            }

            configuration.Normalize();
            gameModeSource = (int)configuration.gameModeSource;
            mapId = configuration.mapId;
            sceneKey = configuration.sceneKey;
            missionId = configuration.missionId;
            commanderId = configuration.commanderId;
            heroId = configuration.heroId;
            actWeaponIds = CloneArray(configuration.actWeaponIds);
            fpsAvailableWeaponIds = CloneArray(configuration.fpsAvailableWeaponIds);
            fpsEquippedPrimary = configuration.fpsEquippedPrimary;
            fpsEquippedSecondary = configuration.fpsEquippedSecondary;
            fpsEquippedMelee = configuration.fpsEquippedMelee;
            contentRevision = configuration.contentRevision;
        }

        private static string[] CloneArray(string[] values)
        {
            if (values == null || values.Length == 0)
            {
                return new string[0];
            }

            string[] clone = new string[values.Length];
            Array.Copy(values, clone, values.Length);
            return clone;
        }
    }

    [Serializable]
    public sealed class ResourceSaveData
    {
        public int commanderResource;
        public int crystal;
        public int influenceUsed;
        public int influenceCap;

        public ResourceSnapshot ToSnapshot()
        {
            return new ResourceSnapshot(
                commanderResource,
                crystal,
                influenceUsed,
                influenceCap);
        }

        public void CopyFrom(ResourceSnapshot snapshot)
        {
            commanderResource = snapshot.CommanderResource;
            crystal = snapshot.Crystal;
            influenceUsed = snapshot.InfluenceUsed;
            influenceCap = snapshot.InfluenceCap;
        }
    }

    [Serializable]
    public sealed class PersistentStatModifierSaveData
    {
        public string sourceId = string.Empty;
        public int statId;
        public int operation;
        public float value;
        public int priority;
        public float durationRemaining;
        public int stackingRule;

        public StatModifierDefinition ToDefinition()
        {
            RuntimeStatId savedStat = Enum.IsDefined(typeof(RuntimeStatId), statId)
                ? (RuntimeStatId)statId
                : RuntimeStatId.MaxHealth;
            StatModifierOperation savedOperation = Enum.IsDefined(typeof(StatModifierOperation), operation)
                ? (StatModifierOperation)operation
                : StatModifierOperation.Add;
            StatStackingRule savedRule = Enum.IsDefined(typeof(StatStackingRule), stackingRule)
                ? (StatStackingRule)stackingRule
                : StatStackingRule.Stack;
            return new StatModifierDefinition(
                sourceId,
                savedStat,
                savedOperation,
                value,
                priority,
                durationRemaining,
                savedRule);
        }
    }

    [Serializable]
    public sealed class EntitySaveData
    {
        public string runtimeId = string.Empty;
        public string archetypeId = string.Empty;
        public bool runtimeSpawned;
        public int faction = (int)FactionId.Neutral;
        public Vector3 position;
        public Quaternion rotation = Quaternion.identity;
        public bool hasVitals;
        public float health;
        public float maxHealth;
        public float shield;
        public float maxShield;
        public float energy;
        public float maxEnergy;
        public int reservedInfluence;
        public float lifetimeRemaining;
        public int promotionLevel;
        public string heroFormId = string.Empty;
        public bool isRespawning;
        public float respawnRemainingSeconds;
        public List<PersistentStatModifierSaveData> persistentModifiers =
            new List<PersistentStatModifierSaveData>();

        public void EnsureCollections()
        {
            persistentModifiers = persistentModifiers ??
                                  new List<PersistentStatModifierSaveData>();
        }

        public FactionId SavedFaction
        {
            get
            {
                return Enum.IsDefined(typeof(FactionId), faction)
                    ? (FactionId)faction
                    : FactionId.Neutral;
            }
            set => faction = (int)value;
        }

        public void CaptureVitals(VitalsSnapshot snapshot)
        {
            hasVitals = true;
            health = snapshot.Health;
            maxHealth = snapshot.MaxHealth;
            shield = snapshot.Shield;
            maxShield = snapshot.MaxShield;
            energy = snapshot.Energy;
            maxEnergy = snapshot.MaxEnergy;
        }

        public VitalsSnapshot ToVitalsSnapshot()
        {
            return new VitalsSnapshot(
                health,
                maxHealth,
                shield,
                maxShield,
                energy,
                maxEnergy);
        }
    }

    [Serializable]
    public sealed class ConstructionSiteSaveData
    {
        public string runtimeId = string.Empty;
        public string buildingId = string.Empty;
        public Vector3 position;
        public Quaternion rotation = Quaternion.identity;
        public float progressSeconds;
        public bool constructing;
        public bool resourcesCommitted;
    }

    [Serializable]
    public sealed class ConstructionSaveData
    {
        public List<ConstructionSiteSaveData> sites = new List<ConstructionSiteSaveData>();

        public void EnsureCollections()
        {
            sites = sites ?? new List<ConstructionSiteSaveData>();
        }
    }

    [Serializable]
    public sealed class HeroDeploymentSaveData
    {
        public bool pending;
        public float remainingSeconds;
        public string hostRuntimeId = string.Empty;

        public void Normalize()
        {
            remainingSeconds = Mathf.Max(0f, remainingSeconds);
            hostRuntimeId = string.IsNullOrWhiteSpace(hostRuntimeId)
                ? string.Empty
                : hostRuntimeId.Trim();
        }
    }

    [Serializable]
    public sealed class RallyPointSaveData
    {
        public string ownerRuntimeId = string.Empty;
        public bool hasValidPoint;
        public Vector3 point;
    }

    [Serializable]
    public sealed class ProductionQueueEntrySaveData
    {
        public string recipeId = string.Empty;
        public float remainingSeconds;
        public int remainingBatchCount = 1;
        public bool costReserved;
        public bool populationReserved;
    }

    [Serializable]
    public sealed class ProductionQueueSaveData
    {
        public string ownerRuntimeId = string.Empty;
        public List<ProductionQueueEntrySaveData> entries = new List<ProductionQueueEntrySaveData>();

        public void EnsureCollections()
        {
            entries = entries ?? new List<ProductionQueueEntrySaveData>();
        }
    }

    [Serializable]
    public sealed class ResourceNodeSaveData
    {
        public string ownerRuntimeId = string.Empty;
        public int resourceType = (int)ResourceType.Crystal;
        public int remaining;
    }

    [Serializable]
    public sealed class GathererCargoSaveData
    {
        public string ownerRuntimeId = string.Empty;
        public int resourceType = (int)ResourceType.Crystal;
        public int amount;
    }

    [Serializable]
    public sealed class WeaponStateSaveData
    {
        public string weaponId = string.Empty;
        public int magazineAmmo;
        public int reserveAmmo;
    }

    [Serializable]
    public sealed class AbilityStateSaveData
    {
        public string abilityId = string.Empty;
        public float cooldownRemaining;
        public int charges;
        public string sharedCooldownGroup = string.Empty;
    }

    [Serializable]
    public sealed class InventoryAbilitySaveData
    {
        public string ownerRuntimeId = string.Empty;
        public string equippedPrimaryId = string.Empty;
        public string equippedSecondaryId = string.Empty;
        public string equippedMeleeId = string.Empty;
        public string activeActWeaponId = string.Empty;
        public string auxiliaryActWeaponId = string.Empty;
        public List<WeaponStateSaveData> weapons = new List<WeaponStateSaveData>();
        public List<AbilityStateSaveData> abilities = new List<AbilityStateSaveData>();

        public void EnsureCollections()
        {
            weapons = weapons ?? new List<WeaponStateSaveData>();
            abilities = abilities ?? new List<AbilityStateSaveData>();
        }
    }

    [Serializable]
    public sealed class EnemyAiSaveData
    {
        public string directorId = string.Empty;
        public int phase = (int)EnemyAiPhase.Dormant;
        public int assaultSerial;
        public float thinkRemaining;
        public float regroupRemaining;
        public int productionCursor;
        public string strategicTargetRuntimeId = string.Empty;
        public int commanderResource;
        public int crystal;
        public int influenceUsed;
        public int influenceCap;
    }

    [Serializable]
    public sealed class TechnologyProgressSaveData
    {
        public string technologyId = string.Empty;
        public float remainingSeconds;
        public bool costReserved;
    }

    [Serializable]
    public sealed class TechnologySaveData
    {
        public List<string> completedTechnologyIds = new List<string>();
        public List<TechnologyProgressSaveData> inProgress = new List<TechnologyProgressSaveData>();

        public void EnsureCollections()
        {
            completedTechnologyIds = completedTechnologyIds ?? new List<string>();
            inProgress = inProgress ?? new List<TechnologyProgressSaveData>();
        }
    }

    [Serializable]
    public sealed class VisibilitySaveData
    {
        public List<string> exploredCellIds = new List<string>();
        public List<string> persistentVisionCellIds = new List<string>();

        public void EnsureCollections()
        {
            exploredCellIds = exploredCellIds ?? new List<string>();
            persistentVisionCellIds = persistentVisionCellIds ?? new List<string>();
        }
    }

    [Serializable]
    public sealed class MissionObjectiveSaveData
    {
        public string objectiveId = string.Empty;
        public float progress;
        public bool completed;
        public bool failed;
    }

    [Serializable]
    public sealed class MissionSaveData
    {
        public string missionId = string.Empty;
        public int waveIndex;
        public float elapsedSeconds;
        public List<MissionObjectiveSaveData> objectives = new List<MissionObjectiveSaveData>();
        public List<string> firedTriggerIds = new List<string>();

        public void EnsureCollections()
        {
            objectives = objectives ?? new List<MissionObjectiveSaveData>();
            firedTriggerIds = firedTriggerIds ?? new List<string>();
        }
    }
}
