using System;
using System.Collections.Generic;
using OriginCore.Buildings;
using OriginCore.Combat;
using OriginCore.Content;
using OriginCore.Economy;
using OriginCore.Gameplay;
using UnityEngine;

namespace OriginCore.Progression
{
    public enum TechnologyState
    {
        Locked = 0,
        Available = 1,
        Researching = 2,
        Completed = 3
    }

    public readonly struct TechnologyProgress
    {
        public TechnologyProgress(
            TechnologyDefinition definition,
            TechnologyState state,
            float remainingSeconds)
        {
            Definition = definition;
            State = state;
            RemainingSeconds = Mathf.Max(0f, remainingSeconds);
        }

        public TechnologyDefinition Definition { get; }
        public TechnologyState State { get; }
        public float RemainingSeconds { get; }
    }

    [DefaultExecutionOrder(-11680)]
    [DisallowMultipleComponent]
    public sealed class TechTreeService : MonoBehaviour
    {
        private readonly HashSet<string> _completed =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _permanentUnlocks =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _unlockReferences =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _buildingCounts =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly HashSet<string> _appliedModifierSources =
            new HashSet<string>(StringComparer.Ordinal);

        private ContentCatalogService _catalogService;
        private ResourceService _resources;
        private EntityRegistry _entities;
        private CommanderDefinition _commander;
        private TechnologyDefinition _current;
        private float _remainingSeconds;
        private bool _initialized;

        public event Action StateChanged;
        public event Action<TechnologyProgress> ResearchChanged;

        public CommanderDefinition Commander => _commander;
        public TechnologyDefinition CurrentResearch => _current;
        public float RemainingSeconds => Mathf.Max(0f, _remainingSeconds);
        public IReadOnlyCollection<string> CompletedTechnologyIds => _completed;

        private void Update()
        {
            if (!_initialized || _current == null || Time.deltaTime <= 0f)
            {
                return;
            }

            _remainingSeconds = Mathf.Max(0f, _remainingSeconds - Time.deltaTime);
            ResearchChanged?.Invoke(new TechnologyProgress(
                _current,
                TechnologyState.Researching,
                _remainingSeconds));
            if (_remainingSeconds <= 0f)
            {
                CompleteCurrent();
            }
        }

        internal bool Initialize(
            ContentCatalogService catalogService,
            ResourceService resources,
            EntityRegistry entities)
        {
            if (catalogService == null || resources == null || entities == null)
            {
                Debug.LogError("[OriginCore Tech] Required services are unavailable.", this);
                return false;
            }

            Shutdown();
            _catalogService = catalogService;
            _resources = resources;
            _entities = entities;
            _entities.EntityRegistered += HandleEntityRegistered;
            _initialized = true;
            return true;
        }

        internal void Shutdown()
        {
            if (_entities != null)
            {
                _entities.EntityRegistered -= HandleEntityRegistered;
            }

            _catalogService = null;
            _resources = null;
            _entities = null;
            _commander = null;
            _current = null;
            _remainingSeconds = 0f;
            _completed.Clear();
            _permanentUnlocks.Clear();
            _unlockReferences.Clear();
            _buildingCounts.Clear();
            _appliedModifierSources.Clear();
            _initialized = false;
        }

        public void ResetForCommander(CommanderDefinition commander)
        {
            RemoveAppliedModifiers();
            _commander = commander;
            _current = null;
            _remainingSeconds = 0f;
            _completed.Clear();
            _permanentUnlocks.Clear();
            _unlockReferences.Clear();
            _buildingCounts.Clear();
            ApplyCommanderModifiersToAll();
            RebuildBuildingStateFromEntities();
            StateChanged?.Invoke();
        }

        public TechnologyState GetState(TechnologyDefinition technology)
        {
            if (technology == null || _commander == null || !CommanderOffers(technology))
            {
                return TechnologyState.Locked;
            }

            if (_completed.Contains(technology.ContentId))
            {
                return TechnologyState.Completed;
            }

            if (!IsUnlocked(technology.RequiredUnlockId))
            {
                return TechnologyState.Locked;
            }

            if (_current == technology)
            {
                return TechnologyState.Researching;
            }

            string[] prerequisites = technology.PrerequisiteTechnologyIds;
            for (int i = 0; i < prerequisites.Length; i++)
            {
                if (!_completed.Contains(prerequisites[i]))
                {
                    return TechnologyState.Locked;
                }
            }

            return TechnologyState.Available;
        }

        public bool TryStartResearch(TechnologyDefinition technology, out string error)
        {
            if (!_initialized || _resources == null)
            {
                error = "Technology service is unavailable.";
                return false;
            }

            if (_current != null)
            {
                error = "Another technology is already being researched.";
                return false;
            }

            if (GetState(technology) != TechnologyState.Available)
            {
                error = "Technology is locked or already completed.";
                return false;
            }

            if (!_resources.TrySpend(technology.Cost))
            {
                error = "Insufficient resources for research.";
                return false;
            }

            _current = technology;
            _remainingSeconds = technology.ResearchSeconds;
            error = string.Empty;
            ResearchChanged?.Invoke(new TechnologyProgress(
                _current,
                TechnologyState.Researching,
                _remainingSeconds));
            StateChanged?.Invoke();
            return true;
        }

        public bool CancelResearch()
        {
            if (_current == null)
            {
                return false;
            }

            TechnologyDefinition cancelled = _current;
            _current = null;
            _remainingSeconds = 0f;
            _resources?.Refund(cancelled.Cost);
            ResearchChanged?.Invoke(new TechnologyProgress(
                cancelled,
                GetState(cancelled),
                0f));
            StateChanged?.Invoke();
            return true;
        }

        public bool IsUnlocked(string contentOrTechnologyId)
        {
            string id = ContentIdUtility.Normalize(contentOrTechnologyId);
            if (string.IsNullOrEmpty(id))
            {
                return true;
            }

            return _completed.Contains(id) || _permanentUnlocks.Contains(id) ||
                   _unlockReferences.TryGetValue(id, out int references) && references > 0;
        }

        public bool CanConstruct(BuildingDefinition definition)
        {
            if (definition == null || !IsUnlocked(definition.RequiredUnlockId))
            {
                return false;
            }

            return !definition.Unique || GetBuildingCount(definition.ContentId) == 0;
        }

        public bool CanProduce(ProductionRecipe recipe)
        {
            return recipe != null && IsUnlocked(recipe.RequiredTechnologyId);
        }

        public void RegisterUnlocks(string[] contentIds)
        {
            ChangeUnlockReferences(contentIds, 1);
        }

        public void UnregisterUnlocks(string[] contentIds)
        {
            ChangeUnlockReferences(contentIds, -1);
        }

        public void RegisterBuilding(BuildingDefinition definition)
        {
            if (definition == null)
            {
                return;
            }

            ChangeCount(_buildingCounts, definition.ContentId, 1);
            StateChanged?.Invoke();
        }

        public void UnregisterBuilding(BuildingDefinition definition)
        {
            if (definition == null)
            {
                return;
            }

            ChangeCount(_buildingCounts, definition.ContentId, -1);
            StateChanged?.Invoke();
        }

        public void ActivateBuilding(BuildingDefinition definition)
        {
            if (definition == null)
            {
                return;
            }

            RegisterUnlocks(definition.UnlockedContentIds);
            StateChanged?.Invoke();
        }

        public void DeactivateBuilding(BuildingDefinition definition)
        {
            if (definition == null)
            {
                return;
            }

            UnregisterUnlocks(definition.UnlockedContentIds);
            StateChanged?.Invoke();
        }

        public int GetBuildingCount(string buildingId)
        {
            return _buildingCounts.TryGetValue(
                ContentIdUtility.Normalize(buildingId),
                out int count)
                ? Mathf.Max(0, count)
                : 0;
        }

        public string[] CaptureCompletedIds()
        {
            string[] values = new string[_completed.Count];
            _completed.CopyTo(values);
            Array.Sort(values, StringComparer.Ordinal);
            return values;
        }

        public void RestoreResearch(
            CommanderDefinition commander,
            string[] completedIds,
            string currentTechnologyId,
            float remainingSeconds)
        {
            ResetForCommander(commander);
            if (completedIds != null)
            {
                for (int i = 0; i < completedIds.Length; i++)
                {
                    if (_catalogService != null && _catalogService.Catalog != null &&
                        _catalogService.Catalog.TryGetTechnology(
                            completedIds[i],
                            out TechnologyDefinition technology) &&
                        CommanderOffers(technology))
                    {
                        CommitTechnology(technology, false);
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(currentTechnologyId) &&
                _catalogService.Catalog.TryGetTechnology(
                    currentTechnologyId,
                    out TechnologyDefinition current) &&
                GetState(current) == TechnologyState.Available)
            {
                _current = current;
                _remainingSeconds = Mathf.Clamp(
                    remainingSeconds,
                    0f,
                    current.ResearchSeconds);
            }

            StateChanged?.Invoke();
        }

        private void CompleteCurrent()
        {
            TechnologyDefinition completed = _current;
            _current = null;
            _remainingSeconds = 0f;
            CommitTechnology(completed, true);
        }

        private void CommitTechnology(TechnologyDefinition technology, bool publish)
        {
            if (technology == null || !_completed.Add(technology.ContentId))
            {
                return;
            }

            string[] unlocks = technology.UnlockedContentIds;
            for (int i = 0; i < unlocks.Length; i++)
            {
                if (!string.IsNullOrEmpty(unlocks[i]))
                {
                    _permanentUnlocks.Add(unlocks[i]);
                }
            }

            _appliedModifierSources.Add(technology.ContentId);
            ApplyTechnologyModifiersToAll(technology);
            if (publish)
            {
                ResearchChanged?.Invoke(new TechnologyProgress(
                    technology,
                    TechnologyState.Completed,
                    0f));
                StateChanged?.Invoke();
            }
        }

        private void HandleEntityRegistered(EntityIdentity identity)
        {
            ApplyCommanderModifiers(identity);
            if (_commander == null)
            {
                return;
            }

            TechnologyDefinition[] technologies = _commander.Technologies;
            for (int i = 0; i < technologies.Length; i++)
            {
                TechnologyDefinition technology = technologies[i];
                if (technology != null && _completed.Contains(technology.ContentId))
                {
                    ApplyTechnologyModifiers(technology, identity);
                }
            }
        }

        private void RebuildBuildingStateFromEntities()
        {
            if (_entities == null)
            {
                return;
            }

            IReadOnlyList<EntityIdentity> entities = _entities.Entities;
            for (int i = 0; i < entities.Count; i++)
            {
                EntityIdentity identity = entities[i];
                BuildingRuntime building = identity != null
                    ? identity.GetComponent<BuildingRuntime>()
                    : null;
                if (building == null || building.Definition == null)
                {
                    continue;
                }

                RegisterBuilding(building.Definition);
                if (building.IsOperational)
                {
                    ActivateBuilding(building.Definition);
                }
            }
        }

        private void ApplyCommanderModifiersToAll()
        {
            if (_entities == null)
            {
                return;
            }

            IReadOnlyList<EntityIdentity> entities = _entities.Entities;
            for (int i = 0; i < entities.Count; i++)
            {
                ApplyCommanderModifiers(entities[i]);
            }
        }

        private void ApplyCommanderModifiers(EntityIdentity identity)
        {
            if (_commander == null || identity == null ||
                !_commander.GlobalModifierTargetRoles.HasAny(identity.Roles) ||
                !IsFriendly(identity))
            {
                return;
            }

            RuntimeStatBlock stats = identity.GetComponent<RuntimeStatBlock>();
            if (stats == null)
            {
                return;
            }

            StatModifierDefinition[] modifiers = _commander.GlobalModifiers;
            for (int i = 0; i < modifiers.Length; i++)
            {
                stats.AddModifier(modifiers[i]);
                _appliedModifierSources.Add(modifiers[i].SourceId);
            }
        }

        private void ApplyTechnologyModifiersToAll(TechnologyDefinition technology)
        {
            if (_entities == null)
            {
                return;
            }

            IReadOnlyList<EntityIdentity> entities = _entities.Entities;
            for (int i = 0; i < entities.Count; i++)
            {
                ApplyTechnologyModifiers(technology, entities[i]);
            }
        }

        private static void ApplyTechnologyModifiers(
            TechnologyDefinition technology,
            EntityIdentity identity)
        {
            if (technology == null || identity == null || !IsFriendly(identity) ||
                !TargetsArchetype(technology, identity.ArchetypeId))
            {
                return;
            }

            RuntimeStatBlock stats = identity.GetComponent<RuntimeStatBlock>();
            if (stats == null)
            {
                return;
            }

            StatModifierDefinition[] modifiers = technology.Modifiers;
            for (int i = 0; i < modifiers.Length; i++)
            {
                stats.AddModifier(modifiers[i]);
            }
        }

        private void RemoveAppliedModifiers()
        {
            if (_entities == null || _appliedModifierSources.Count == 0)
            {
                _appliedModifierSources.Clear();
                return;
            }

            IReadOnlyList<EntityIdentity> entities = _entities.Entities;
            foreach (string sourceId in _appliedModifierSources)
            {
                for (int i = 0; i < entities.Count; i++)
                {
                    RuntimeStatBlock stats = entities[i] != null
                        ? entities[i].GetComponent<RuntimeStatBlock>()
                        : null;
                    stats?.RemoveModifiersBySource(sourceId);
                }
            }

            _appliedModifierSources.Clear();
        }

        private bool CommanderOffers(TechnologyDefinition technology)
        {
            if (_commander == null || technology == null)
            {
                return false;
            }

            TechnologyDefinition[] values = _commander.Technologies;
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] == technology)
                {
                    return true;
                }
            }

            return false;
        }

        private void ChangeUnlockReferences(string[] contentIds, int delta)
        {
            if (contentIds == null || delta == 0)
            {
                return;
            }

            for (int i = 0; i < contentIds.Length; i++)
            {
                string id = ContentIdUtility.Normalize(contentIds[i]);
                if (!string.IsNullOrEmpty(id))
                {
                    ChangeCount(_unlockReferences, id, delta);
                }
            }
        }

        private static void ChangeCount(
            Dictionary<string, int> values,
            string id,
            int delta)
        {
            id = ContentIdUtility.Normalize(id);
            if (string.IsNullOrEmpty(id))
            {
                return;
            }

            values.TryGetValue(id, out int current);
            int next = Mathf.Max(0, current + delta);
            if (next == 0)
            {
                values.Remove(id);
            }
            else
            {
                values[id] = next;
            }
        }

        private static bool TargetsArchetype(
            TechnologyDefinition technology,
            string archetypeId)
        {
            string[] targets = technology.TargetArchetypeIds;
            if (targets == null || targets.Length == 0)
            {
                return true;
            }

            for (int i = 0; i < targets.Length; i++)
            {
                if (string.Equals(targets[i], archetypeId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsFriendly(EntityIdentity identity)
        {
            FactionMember faction = identity.GetComponent<FactionMember>();
            return faction == null || faction.Faction == FactionId.Friendly;
        }
    }
}
