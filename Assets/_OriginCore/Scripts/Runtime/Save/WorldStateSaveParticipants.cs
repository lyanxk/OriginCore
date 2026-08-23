using System;
using System.Collections.Generic;
using OriginCore.Buildings;
using OriginCore.Content;
using OriginCore.Economy;
using OriginCore.Gameplay;
using OriginCore.Progression;

namespace OriginCore.Save
{
    internal sealed class ConstructionSaveParticipant : ISaveParticipant
    {
        public string ParticipantKey => "construction";
        public int RestoreOrder => 125;

        public void Capture(SaveOperationContext context, SaveGameData data)
        {
            data.construction.EnsureCollections();
            data.construction.sites.Clear();
            ConstructionSite[] sites =
                EntitySaveParticipant.CollectSceneComponents<ConstructionSite>(
                    context.SceneContext.Scene,
                    true);
            for (int i = 0; i < sites.Length; i++)
            {
                ConstructionSite site = sites[i];
                if (site == null)
                {
                    continue;
                }

                EntityIdentity identity = site.GetComponent<EntityIdentity>();
                BuildingRuntime building = site.GetComponent<BuildingRuntime>();
                if (identity == null || building == null || building.Definition == null)
                {
                    context.Warn("An incomplete construction site was skipped while saving.");
                    continue;
                }

                data.construction.sites.Add(new ConstructionSiteSaveData
                {
                    runtimeId = identity.EnsureRuntimeId(),
                    buildingId = building.Definition.ContentId,
                    position = site.transform.position,
                    rotation = site.transform.rotation,
                    progressSeconds = site.ElapsedSeconds,
                    constructing = site.IsConstructing,
                    resourcesCommitted = true
                });
            }
        }

        public void Restore(SaveOperationContext context, SaveGameData data)
        {
            data.construction.EnsureCollections();
            ConstructionSite[] sites =
                EntitySaveParticipant.CollectSceneComponents<ConstructionSite>(
                    context.SceneContext.Scene,
                    true);
            Dictionary<string, ConstructionSite> byId =
                new Dictionary<string, ConstructionSite>(StringComparer.Ordinal);
            for (int i = 0; i < sites.Length; i++)
            {
                EntityIdentity identity = sites[i] != null
                    ? sites[i].GetComponent<EntityIdentity>()
                    : null;
                if (identity != null)
                {
                    byId[identity.EnsureRuntimeId()] = sites[i];
                }
            }

            for (int i = 0; i < data.construction.sites.Count; i++)
            {
                ConstructionSiteSaveData snapshot = data.construction.sites[i];
                if (snapshot == null ||
                    !byId.TryGetValue(snapshot.runtimeId, out ConstructionSite site))
                {
                    context.Warn(
                        "Construction site '" +
                        (snapshot != null ? snapshot.runtimeId : string.Empty) +
                        "' could not be restored.");
                    continue;
                }

                BuildingRuntime building = site.GetComponent<BuildingRuntime>();
                if (building == null || building.Definition == null ||
                    !string.Equals(
                        building.Definition.ContentId,
                        snapshot.buildingId,
                        StringComparison.Ordinal))
                {
                    context.Warn(
                        "Construction definition mismatch for '" +
                        snapshot.runtimeId + "'.");
                    continue;
                }

                site.transform.SetPositionAndRotation(snapshot.position, snapshot.rotation);
                site.Restore(snapshot.progressSeconds, snapshot.constructing);
            }
        }
    }

    internal sealed class ProductionQueueSaveParticipant : ISaveParticipant
    {
        private readonly List<ProductionOrderSnapshot> _captured =
            new List<ProductionOrderSnapshot>(ProductionQueue.DefaultCapacity);

        public string ParticipantKey => "production-queues";
        public int RestoreOrder => 170;

        public void Capture(SaveOperationContext context, SaveGameData data)
        {
            data.productionQueues.Clear();
            ProductionQueue[] queues =
                EntitySaveParticipant.CollectSceneComponents<ProductionQueue>(
                    context.SceneContext.Scene,
                    true);
            for (int i = 0; i < queues.Length; i++)
            {
                ProductionQueue queue = queues[i];
                EntityIdentity identity = queue != null
                    ? queue.GetComponent<EntityIdentity>()
                    : null;
                if (identity == null || queue.CaptureOrders(_captured) == 0)
                {
                    continue;
                }

                ProductionQueueSaveData queueData = new ProductionQueueSaveData
                {
                    ownerRuntimeId = identity.EnsureRuntimeId()
                };
                for (int entryIndex = 0; entryIndex < _captured.Count; entryIndex++)
                {
                    ProductionOrderSnapshot order = _captured[entryIndex];
                    if (order.Recipe == null)
                    {
                        continue;
                    }

                    queueData.entries.Add(new ProductionQueueEntrySaveData
                    {
                        recipeId = order.Recipe.RecipeId,
                        remainingSeconds = order.RemainingTime,
                        remainingBatchCount = order.Recipe.BatchCount,
                        costReserved = true,
                        populationReserved = order.Recipe.Cost.Influence > 0
                    });
                }

                if (queueData.entries.Count > 0)
                {
                    data.productionQueues.Add(queueData);
                }
            }
        }

        public void Restore(SaveOperationContext context, SaveGameData data)
        {
            ProductionQueue[] queues =
                EntitySaveParticipant.CollectSceneComponents<ProductionQueue>(
                    context.SceneContext.Scene,
                    true);
            Dictionary<string, ProductionQueue> byId =
                new Dictionary<string, ProductionQueue>(StringComparer.Ordinal);
            for (int i = 0; i < queues.Length; i++)
            {
                ProductionQueue queue = queues[i];
                EntityIdentity identity = queue != null
                    ? queue.GetComponent<EntityIdentity>()
                    : null;
                if (identity == null)
                {
                    continue;
                }

                queue.RestoreOrders(new ProductionOrderSnapshot[0], out _);
                byId[identity.EnsureRuntimeId()] = queue;
            }

            for (int i = 0; i < data.productionQueues.Count; i++)
            {
                ProductionQueueSaveData snapshot = data.productionQueues[i];
                if (snapshot == null ||
                    !byId.TryGetValue(snapshot.ownerRuntimeId, out ProductionQueue queue))
                {
                    context.Warn(
                        "Production queue '" +
                        (snapshot != null ? snapshot.ownerRuntimeId : string.Empty) +
                        "' could not be restored.");
                    continue;
                }

                snapshot.EnsureCollections();
                List<ProductionOrderSnapshot> orders =
                    new List<ProductionOrderSnapshot>(snapshot.entries.Count);
                bool valid = true;
                for (int entryIndex = 0; entryIndex < snapshot.entries.Count; entryIndex++)
                {
                    ProductionQueueEntrySaveData entry = snapshot.entries[entryIndex];
                    ProductionRecipe recipe = FindRecipe(queue, entry != null
                        ? entry.recipeId
                        : string.Empty);
                    if (recipe == null)
                    {
                        context.Warn(
                            "Saved recipe '" +
                            (entry != null ? entry.recipeId : string.Empty) +
                            "' is unavailable on '" + snapshot.ownerRuntimeId + "'.");
                        valid = false;
                        break;
                    }

                    orders.Add(new ProductionOrderSnapshot(
                        recipe,
                        entry.remainingSeconds,
                        1f - entry.remainingSeconds / recipe.ProductionTime));
                }

                if (valid && !queue.RestoreOrders(orders, out string error))
                {
                    context.Warn(
                        "Production queue '" + snapshot.ownerRuntimeId +
                        "' could not be restored: " + error);
                }
            }
        }

        private static ProductionRecipe FindRecipe(ProductionQueue queue, string recipeId)
        {
            if (queue == null || string.IsNullOrWhiteSpace(recipeId))
            {
                return null;
            }

            IReadOnlyList<ProductionRecipe> recipes = queue.Recipes;
            for (int i = 0; i < recipes.Count; i++)
            {
                if (recipes[i] != null && string.Equals(
                        recipes[i].RecipeId,
                        recipeId,
                        StringComparison.Ordinal))
                {
                    return recipes[i];
                }
            }

            return null;
        }
    }

    internal sealed class ResourceGatheringSaveParticipant : ISaveParticipant
    {
        public string ParticipantKey => "resource-gathering";
        public int RestoreOrder => 160;

        public void Capture(SaveOperationContext context, SaveGameData data)
        {
            data.resourceNodes.Clear();
            ResourceNode[] nodes =
                EntitySaveParticipant.CollectSceneComponents<ResourceNode>(
                    context.SceneContext.Scene,
                    true);
            for (int i = 0; i < nodes.Length; i++)
            {
                ResourceNode node = nodes[i];
                EntityIdentity identity = node != null
                    ? node.GetComponent<EntityIdentity>()
                    : null;
                if (identity == null)
                {
                    context.Warn("A resource node without EntityIdentity was skipped while saving.");
                    continue;
                }

                data.resourceNodes.Add(new ResourceNodeSaveData
                {
                    ownerRuntimeId = identity.EnsureRuntimeId(),
                    resourceType = (int)node.ResourceType,
                    remaining = node.Remaining
                });
            }

            data.gathererCargo.Clear();
            Gatherer[] gatherers =
                EntitySaveParticipant.CollectSceneComponents<Gatherer>(
                    context.SceneContext.Scene,
                    true);
            for (int i = 0; i < gatherers.Length; i++)
            {
                Gatherer gatherer = gatherers[i];
                EntityIdentity identity = gatherer != null
                    ? gatherer.GetComponent<EntityIdentity>()
                    : null;
                if (identity == null || gatherer.CarriedAmount <= 0)
                {
                    continue;
                }

                data.gathererCargo.Add(new GathererCargoSaveData
                {
                    ownerRuntimeId = identity.EnsureRuntimeId(),
                    resourceType = (int)gatherer.CarriedType,
                    amount = gatherer.CarriedAmount
                });
            }
        }

        public void Restore(SaveOperationContext context, SaveGameData data)
        {
            ResourceNode[] nodes =
                EntitySaveParticipant.CollectSceneComponents<ResourceNode>(
                    context.SceneContext.Scene,
                    true);
            Dictionary<string, ResourceNode> nodesById =
                BuildComponentMap(nodes);
            for (int i = 0; i < data.resourceNodes.Count; i++)
            {
                ResourceNodeSaveData snapshot = data.resourceNodes[i];
                if (snapshot == null ||
                    !nodesById.TryGetValue(snapshot.ownerRuntimeId, out ResourceNode node))
                {
                    context.Warn("Resource node '" +
                                 (snapshot != null ? snapshot.ownerRuntimeId : string.Empty) +
                                 "' could not be restored.");
                    continue;
                }

                if ((int)node.ResourceType != snapshot.resourceType)
                {
                    context.Warn("Resource type mismatch for node '" + snapshot.ownerRuntimeId + "'.");
                }
                node.RestoreRemaining(snapshot.remaining);
            }

            Gatherer[] gatherers =
                EntitySaveParticipant.CollectSceneComponents<Gatherer>(
                    context.SceneContext.Scene,
                    true);
            Dictionary<string, Gatherer> gatherersById =
                BuildComponentMap(gatherers);
            for (int i = 0; i < gatherers.Length; i++)
            {
                gatherers[i]?.RestoreCargo(ResourceType.Crystal, 0);
            }

            for (int i = 0; i < data.gathererCargo.Count; i++)
            {
                GathererCargoSaveData snapshot = data.gathererCargo[i];
                if (snapshot == null ||
                    !gatherersById.TryGetValue(snapshot.ownerRuntimeId, out Gatherer gatherer))
                {
                    context.Warn("Gatherer '" +
                                 (snapshot != null ? snapshot.ownerRuntimeId : string.Empty) +
                                 "' could not restore cargo.");
                    continue;
                }

                ResourceType type = Enum.IsDefined(typeof(ResourceType), snapshot.resourceType)
                    ? (ResourceType)snapshot.resourceType
                    : ResourceType.Crystal;
                gatherer.RestoreCargo(type, snapshot.amount);
            }
        }

        private static Dictionary<string, T> BuildComponentMap<T>(T[] components)
            where T : UnityEngine.Component
        {
            Dictionary<string, T> result =
                new Dictionary<string, T>(StringComparer.Ordinal);
            for (int i = 0; i < components.Length; i++)
            {
                T component = components[i];
                EntityIdentity identity = component != null
                    ? component.GetComponent<EntityIdentity>()
                    : null;
                if (identity != null)
                {
                    result[identity.EnsureRuntimeId()] = component;
                }
            }
            return result;
        }
    }

    internal sealed class TechnologySaveParticipant : ISaveParticipant
    {
        public string ParticipantKey => "technology";
        public int RestoreOrder => 220;

        public void Capture(SaveOperationContext context, SaveGameData data)
        {
            data.technology.EnsureCollections();
            data.technology.completedTechnologyIds.Clear();
            data.technology.inProgress.Clear();
            TechTreeService techTree = context.Services.TechTree;
            string[] completed = techTree.CaptureCompletedIds();
            data.technology.completedTechnologyIds.AddRange(completed);
            if (techTree.CurrentResearch != null)
            {
                data.technology.inProgress.Add(new TechnologyProgressSaveData
                {
                    technologyId = techTree.CurrentResearch.ContentId,
                    remainingSeconds = techTree.RemainingSeconds,
                    costReserved = true
                });
            }
        }

        public void Restore(SaveOperationContext context, SaveGameData data)
        {
            data.technology.EnsureCollections();
            if (!context.Services.ContentCatalog.Catalog.TryGetCommander(
                    data.matchConfiguration.commanderId,
                    out CommanderDefinition commander))
            {
                context.Warn("Saved commander technology tree is unavailable.");
                return;
            }

            string currentId = string.Empty;
            float remaining = 0f;
            if (data.technology.inProgress.Count > 0 &&
                data.technology.inProgress[0] != null)
            {
                currentId = data.technology.inProgress[0].technologyId;
                remaining = data.technology.inProgress[0].remainingSeconds;
            }

            context.Services.TechTree.RestoreResearch(
                commander,
                data.technology.completedTechnologyIds.ToArray(),
                currentId,
                remaining);
        }
    }
}
