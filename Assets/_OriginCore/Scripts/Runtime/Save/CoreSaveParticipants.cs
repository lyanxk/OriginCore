using System;
using System.Collections.Generic;
using OriginCore.AI;
using OriginCore.Abilities;
using OriginCore.Buildings;
using OriginCore.Combat;
using OriginCore.Core;
using OriginCore.Gameplay;
using OriginCore.RTS.Commands;
using OriginCore.Units;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace OriginCore.Save
{
    internal sealed class EntitySaveParticipant : ISaveParticipant
    {
        private readonly HashSet<string> _capturedIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, EntityIdentity> _staticById =
            new Dictionary<string, EntityIdentity>(StringComparer.Ordinal);
        private readonly HashSet<string> _restoredIds =
            new HashSet<string>(StringComparer.Ordinal);
        private bool _activeHeroReused;

        public string ParticipantKey => "entities";
        public int RestoreOrder => 100;

        public void Capture(SaveOperationContext context, SaveGameData data)
        {
            data.entities.Clear();
            data.removedSceneEntityIds.Clear();
            _capturedIds.Clear();

            EntityIdentity[] identities = CollectSceneComponents<EntityIdentity>(
                context.SceneContext.Scene,
                true);
            for (int i = 0; i < identities.Length; i++)
            {
                EntityIdentity identity = identities[i];
                if (identity == null)
                {
                    continue;
                }

                string runtimeId = identity.EnsureRuntimeId();
                if (!_capturedIds.Add(runtimeId))
                {
                    context.Warn("Duplicate runtime id '" + runtimeId + "' was skipped while saving.");
                    continue;
                }

                bool active = identity.gameObject.activeInHierarchy;
                VitalsComponent vitals = identity.GetComponent<VitalsComponent>();
                bool alive = vitals == null || vitals.IsAlive;
                RespawnController respawn = identity.GetComponent<RespawnController>();
                bool awaitingRespawn = respawn != null && respawn.IsRespawning;
                if (!active || (!alive && !awaitingRespawn))
                {
                    if (!identity.IsRuntimeSpawned)
                    {
                        data.removedSceneEntityIds.Add(runtimeId);
                    }

                    continue;
                }

                EntitySaveData snapshot = new EntitySaveData
                {
                    runtimeId = runtimeId,
                    archetypeId = identity.ArchetypeId,
                    runtimeSpawned = identity.IsRuntimeSpawned,
                    position = identity.transform.position,
                    rotation = identity.transform.rotation
                };

                if (identity.TryGetComponent(out FactionMember faction))
                {
                    snapshot.SavedFaction = faction.Faction;
                }

                if (vitals != null)
                {
                    snapshot.CaptureVitals(vitals.Snapshot);
                }

                if (identity.TryGetComponent(out PopulationOwner populationOwner) &&
                    populationOwner.HasReservation)
                {
                    snapshot.reservedInfluence = populationOwner.ReservedInfluence;
                }

                HeroFormStateMachine forms = identity.GetComponent<HeroFormStateMachine>();
                if (forms != null)
                {
                    snapshot.heroFormId = forms.CurrentFormId;
                }

                if (awaitingRespawn)
                {
                    snapshot.isRespawning = true;
                    snapshot.respawnRemainingSeconds = respawn.RemainingSeconds;
                }

                LifetimeComponent lifetime = identity.GetComponent<LifetimeComponent>();
                if (lifetime != null && lifetime.IsActive)
                {
                    snapshot.lifetimeRemaining = lifetime.RemainingSeconds;
                }

                CapturePersistentModifiers(identity, snapshot);

                data.entities.Add(snapshot);
            }
        }

        public void Restore(SaveOperationContext context, SaveGameData data)
        {
            context.SceneContext.SelectionService?.ClearSelection();
            _staticById.Clear();
            _restoredIds.Clear();
            _activeHeroReused = false;
            EntityIdentity[] current = CollectSceneComponents<EntityIdentity>(
                context.SceneContext.Scene,
                true);
            for (int i = 0; i < current.Length; i++)
            {
                EntityIdentity identity = current[i];
                if (identity == null || identity.IsRuntimeSpawned)
                {
                    continue;
                }

                string runtimeId = identity.EnsureRuntimeId();
                if (!_staticById.ContainsKey(runtimeId))
                {
                    _staticById.Add(runtimeId, identity);
                }
                else
                {
                    context.Warn("Scene contains duplicate static runtime id '" + runtimeId + "'.");
                }

                identity.GetComponent<UnitCommandQueue>()?.StopAll();
            }

            for (int i = 0; i < data.removedSceneEntityIds.Count; i++)
            {
                string removedId = data.removedSceneEntityIds[i];
                if (_staticById.TryGetValue(removedId, out EntityIdentity removed) &&
                    removed != null && removed.gameObject.activeSelf)
                {
                    removed.gameObject.SetActive(false);
                }
            }

            UnitSpawner[] spawners = CollectSceneComponents<UnitSpawner>(
                context.SceneContext.Scene,
                true);
            for (int i = 0; i < data.entities.Count; i++)
            {
                EntitySaveData snapshot = data.entities[i];
                if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.runtimeId))
                {
                    context.Warn("An entity snapshot without a runtime id was skipped.");
                    continue;
                }

                if (!_restoredIds.Add(snapshot.runtimeId))
                {
                    context.Warn(
                        "Duplicate saved runtime id '" + snapshot.runtimeId + "' was skipped.");
                    continue;
                }

                EntityIdentity identity;
                if (snapshot.runtimeSpawned)
                {
                    identity = TryReuseActiveHero(context, snapshot) ??
                               RestoreSpawnedEntity(context, spawners, snapshot);
                    if (identity == null)
                    {
                        continue;
                    }
                }
                else if (!_staticById.TryGetValue(snapshot.runtimeId, out identity) || identity == null)
                {
                    context.Warn(
                        "Static entity '" + snapshot.runtimeId + "' is not present in scene '" +
                        context.SceneContext.Scene.name + "'.");
                    continue;
                }

                if (!identity.gameObject.activeSelf)
                {
                    identity.gameObject.SetActive(true);
                }

                ApplySnapshot(identity, snapshot);
            }

            context.SceneContext.SelectionService?.ClearSelection();
        }

        private EntityIdentity TryReuseActiveHero(
            SaveOperationContext context,
            EntitySaveData snapshot)
        {
            if (_activeHeroReused || context.Services.MatchSession == null)
            {
                return null;
            }

            EntityIdentity hero = context.Services.MatchSession.ActiveHero;
            if (hero == null || !hero.HasAnyRole(UnitRole.Hero) ||
                !string.Equals(hero.ArchetypeId, snapshot.archetypeId, StringComparison.Ordinal))
            {
                return null;
            }

            hero.UnregisterFromRegistry();
            if (!hero.AssignRuntimeId(snapshot.runtimeId, true) ||
                !hero.RegisterWith(context.Services.EntityRegistry))
            {
                context.Warn("Active hero runtime id could not be restored.");
                return null;
            }

            hero.MarkRuntimeSpawned(snapshot.runtimeSpawned);
            _activeHeroReused = true;
            return hero;
        }

        private static EntityIdentity RestoreSpawnedEntity(
            SaveOperationContext context,
            UnitSpawner[] spawners,
            EntitySaveData snapshot)
        {
            for (int i = 0; i < spawners.Length; i++)
            {
                UnitSpawner spawner = spawners[i];
                if (spawner == null || !spawner.gameObject.activeInHierarchy ||
                    !spawner.CanRestoreArchetype(snapshot.archetypeId))
                {
                    continue;
                }

                if (spawner.TryRestoreSpawn(
                        snapshot.archetypeId,
                        snapshot.runtimeId,
                        snapshot.position,
                        snapshot.rotation,
                        snapshot.SavedFaction,
                        snapshot.reservedInfluence,
                        ResolveProductionAccount(context, snapshot.SavedFaction),
                        out EntityIdentity restored,
                        out string error))
                {
                    return restored;
                }

                context.Warn(
                    "Runtime entity '" + snapshot.runtimeId + "' could not be restored: " + error);
                return null;
            }

            context.Warn(
                "Unknown runtime archetype '" + snapshot.archetypeId +
                "' was skipped for entity '" + snapshot.runtimeId + "'.");
            return null;
        }

        private static IProductionResourceAccount ResolveProductionAccount(
            SaveOperationContext context,
            FactionId faction)
        {
            if (faction == FactionId.Enemy)
            {
                EnemyAiDirector[] directors = CollectSceneComponents<EnemyAiDirector>(
                    context.SceneContext.Scene,
                    true);
                for (int i = 0; i < directors.Length; i++)
                {
                    EnemyAiDirector director = directors[i];
                    if (director != null && director.Faction == faction &&
                        director.Account != null && director.Account.IsConfigured)
                    {
                        return director.Account;
                    }
                }
            }
            return context.Services.ResourceService;
        }

        private static void ApplySnapshot(EntityIdentity identity, EntitySaveData snapshot)
        {
            identity.GetComponent<UnitCommandQueue>()?.StopAll();
            if (identity.TryGetComponent(out FactionMember faction))
            {
                faction.SetFaction(snapshot.SavedFaction);
            }

            NavMeshAgent agent = identity.GetComponent<NavMeshAgent>();
            CharacterController characterController = identity.GetComponent<CharacterController>();
            bool controllerWasEnabled = characterController != null && characterController.enabled;
            if (controllerWasEnabled)
            {
                characterController.enabled = false;
            }

            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                if (!agent.Warp(snapshot.position))
                {
                    identity.transform.position = snapshot.position;
                }
            }
            else
            {
                identity.transform.position = snapshot.position;
            }

            identity.transform.rotation = snapshot.rotation;
            if (controllerWasEnabled && characterController != null)
            {
                characterController.enabled = true;
            }

            if (snapshot.hasVitals && identity.TryGetComponent(out VitalsComponent vitals))
            {
                vitals.Restore(snapshot.ToVitalsSnapshot());
            }

            LifetimeComponent lifetime = identity.GetComponent<LifetimeComponent>();
            if (lifetime != null)
            {
                lifetime.Restore(
                    snapshot.lifetimeRemaining,
                    snapshot.lifetimeRemaining > 0f);
            }
        }

        private static void CapturePersistentModifiers(
            EntityIdentity identity,
            EntitySaveData snapshot)
        {
            RuntimeStatBlock stats = identity.GetComponent<RuntimeStatBlock>();
            if (stats == null)
            {
                return;
            }

            IReadOnlyList<RuntimeStatModifier> modifiers = stats.Modifiers;
            for (int i = 0; i < modifiers.Count; i++)
            {
                RuntimeStatModifier modifier = modifiers[i];
                if (modifier == null || IsDerivedModifier(modifier.SourceId))
                {
                    continue;
                }

                snapshot.persistentModifiers.Add(new PersistentStatModifierSaveData
                {
                    sourceId = modifier.SourceId,
                    statId = (int)modifier.StatId,
                    operation = (int)modifier.Operation,
                    value = modifier.Value,
                    priority = modifier.Priority,
                    durationRemaining = modifier.IsPermanent ? 0f : modifier.DurationRemaining,
                    stackingRule = (int)modifier.StackingRule
                });
            }
        }

        private static bool IsDerivedModifier(string sourceId)
        {
            return sourceId.StartsWith("form.y.", StringComparison.Ordinal) ||
                   sourceId.StartsWith("passive.hero-y.", StringComparison.Ordinal) ||
                   sourceId.StartsWith("hero-form.", StringComparison.Ordinal);
        }

        internal static T[] CollectSceneComponents<T>(Scene scene, bool includeInactive)
            where T : Component
        {
            List<T> results = new List<T>();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                results.AddRange(roots[i].GetComponentsInChildren<T>(includeInactive));
            }

            return results.ToArray();
        }
    }

    internal sealed class RallyPointSaveParticipant : ISaveParticipant
    {
        public string ParticipantKey => "rally-points";
        public int RestoreOrder => 150;

        public void Capture(SaveOperationContext context, SaveGameData data)
        {
            data.rallyPoints.Clear();
            RallyPointController[] controllers =
                EntitySaveParticipant.CollectSceneComponents<RallyPointController>(
                    context.SceneContext.Scene,
                    true);
            for (int i = 0; i < controllers.Length; i++)
            {
                RallyPointController controller = controllers[i];
                if (controller == null || controller.Identity == null)
                {
                    continue;
                }

                data.rallyPoints.Add(new RallyPointSaveData
                {
                    ownerRuntimeId = controller.Identity.EnsureRuntimeId(),
                    hasValidPoint = controller.HasValidRallyPoint,
                    point = controller.RallyPoint
                });
            }
        }

        public void Restore(SaveOperationContext context, SaveGameData data)
        {
            RallyPointController[] controllers =
                EntitySaveParticipant.CollectSceneComponents<RallyPointController>(
                    context.SceneContext.Scene,
                    true);
            Dictionary<string, RallyPointController> byId =
                new Dictionary<string, RallyPointController>(StringComparer.Ordinal);
            for (int i = 0; i < controllers.Length; i++)
            {
                RallyPointController controller = controllers[i];
                if (controller?.Identity != null)
                {
                    byId[controller.Identity.EnsureRuntimeId()] = controller;
                }
            }

            for (int i = 0; i < data.rallyPoints.Count; i++)
            {
                RallyPointSaveData snapshot = data.rallyPoints[i];
                if (snapshot == null || !snapshot.hasValidPoint ||
                    !byId.TryGetValue(snapshot.ownerRuntimeId, out RallyPointController controller))
                {
                    continue;
                }

                if (!controller.TrySetRallyPoint(snapshot.point, out string error))
                {
                    context.Warn(
                        "Rally point for '" + snapshot.ownerRuntimeId +
                        "' could not be restored: " + error);
                }
            }
        }
    }

    internal sealed class ResourceSaveParticipant : ISaveParticipant
    {
        public string ParticipantKey => "resources";
        public int RestoreOrder => 200;

        public void Capture(SaveOperationContext context, SaveGameData data)
        {
            data.resources.CopyFrom(context.Services.ResourceService.Snapshot);
        }

        public void Restore(SaveOperationContext context, SaveGameData data)
        {
            context.Services.ResourceService.Restore(data.resources.ToSnapshot());
        }
    }

    internal sealed class VisibilitySaveParticipant : ISaveParticipant
    {
        public string ParticipantKey => "visibility";
        public int RestoreOrder => 250;

        public void Capture(SaveOperationContext context, SaveGameData data)
        {
            data.visibility.EnsureCollections();
            data.visibility.exploredCellIds.Clear();
            VisitBridges(context, bridge => bridge.CaptureExploredState(data.visibility));
        }

        public void Restore(SaveOperationContext context, SaveGameData data)
        {
            data.visibility.EnsureCollections();
            VisitBridges(context, bridge => bridge.RestoreExploredState(data.visibility));
        }

        private static void VisitBridges(
            SaveOperationContext context,
            Action<IVisibilitySaveBridge> visitor)
        {
            MonoBehaviour[] behaviours =
                EntitySaveParticipant.CollectSceneComponents<MonoBehaviour>(
                    context.SceneContext.Scene,
                    true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IVisibilitySaveBridge bridge)
                {
                    visitor(bridge);
                }
            }
        }
    }

    internal sealed class GameModeSaveParticipant : ISaveParticipant
    {
        public string ParticipantKey => "game-mode";
        public int RestoreOrder => 300;

        public void Capture(SaveOperationContext context, SaveGameData data)
        {
            data.SavedGameMode = context.Services.GameModeController.CurrentMode;
        }

        public void Restore(SaveOperationContext context, SaveGameData data)
        {
            GameMode requested = data.SavedGameMode;
            GameModeController controller = context.Services.GameModeController;
            if (controller.CurrentMode != requested && !controller.RequestMode(requested))
            {
                context.Warn("Saved game mode '" + requested + "' could not be restored.");
            }
            else if (controller.CurrentMode == requested)
            {
                controller.RequestMode(requested);
            }
        }
    }
}
