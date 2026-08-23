using System;
using System.Collections.Generic;
using OriginCore.Buildings;
using OriginCore.Combat;
using OriginCore.Content;
using OriginCore.Core;
using OriginCore.Gameplay;
using OriginCore.RTS.Commands;
using OriginCore.SceneFlow;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace OriginCore.AI
{
    public static class EnemyAiSceneInstaller
    {
        private const string RuntimeRootName = "OriginCore_EnemyAI";

        public static bool TryInstall(
            SceneContext sceneContext,
            MapDefinition mapDefinition,
            GameServices services,
            out EnemyAiDirector director,
            out string error)
        {
            director = null;
            if (mapDefinition == null || mapDefinition.EnemyAiSetup == null)
            {
                error = string.Empty;
                return true;
            }
            if (sceneContext == null || !sceneContext.Scene.IsValid() || services == null)
            {
                error = "Enemy AI installation requires an active scene context and services.";
                return false;
            }

            EnemyAiMapSetup setup = mapDefinition.EnemyAiSetup;
            if (!setup.TryValidate(out error))
            {
                return false;
            }

            EnemyAiDirector[] existingDirectors = Collect<EnemyAiDirector>(sceneContext.Scene);
            if (existingDirectors.Length > 1)
            {
                error = "Map contains more than one EnemyAiDirector.";
                return false;
            }
            if (existingDirectors.Length == 1 && existingDirectors[0] != null)
            {
                director = existingDirectors[0];
                error = string.Empty;
                return true;
            }

            GameObject runtimeRoot = new GameObject(RuntimeRootName);
            SceneManager.MoveGameObjectToScene(runtimeRoot, sceneContext.Scene);
            runtimeRoot.SetActive(false);

            try
            {
                EnemyProductionSite[] sites = CollectEnemyProductionSites(
                    sceneContext.Scene,
                    setup,
                    mapDefinition,
                    runtimeRoot.transform,
                    services.EntityRegistry,
                    out error);
                if (sites == null || sites.Length == 0)
                {
                    UnityEngine.Object.Destroy(runtimeRoot);
                    return false;
                }

                MarkSceneEnemyUnits(sceneContext.Scene, setup.MarkExistingEnemyUnits);
                StrategicTarget[] targets = CollectStrategicTargets(sceneContext.Scene);
                if (targets.Length == 0)
                {
                    error = "Enemy AI map has no living Friendly building StrategicTarget.";
                    UnityEngine.Object.Destroy(runtimeRoot);
                    return false;
                }

                EnemyProductionAccount account =
                    runtimeRoot.AddComponent<EnemyProductionAccount>();
                account.Configure(
                    setup.FundingPolicy,
                    setup.InitialCommanderResource,
                    setup.InitialCrystal,
                    setup.PopulationCap,
                    setup.CommanderResourcePerSecond,
                    setup.CrystalPerSecond);

                director = runtimeRoot.AddComponent<EnemyAiDirector>();
                director.Configure(
                    setup.Definition,
                    mapDefinition.ContentId + ".enemy.main",
                    account,
                    sites,
                    targets,
                    services.EntityRegistry);

                runtimeRoot.SetActive(true);
                SetProducerRallies(sites, mapDefinition.WorldBounds.center);
                if (!director.TryStart())
                {
                    error = "Enemy AI director rejected the installed map setup.";
                    UnityEngine.Object.Destroy(runtimeRoot);
                    director = null;
                    return false;
                }
            }
            catch (Exception exception)
            {
                error = "Enemy AI installation failed: " + exception.Message;
                UnityEngine.Object.Destroy(runtimeRoot);
                director = null;
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static EnemyProductionSite[] CollectEnemyProductionSites(
            Scene scene,
            EnemyAiMapSetup setup,
            MapDefinition mapDefinition,
            Transform runtimeRoot,
            EntityRegistry registry,
            out string error)
        {
            var sites = new List<EnemyProductionSite>(4);
            ProductionQueue[] queues = Collect<ProductionQueue>(scene);
            for (int i = 0; i < queues.Length; i++)
            {
                ProductionQueue queue = queues[i];
                FactionMember faction = queue != null
                    ? queue.GetComponent<FactionMember>()
                    : null;
                if (faction == null || faction.Faction != setup.Definition.Faction)
                {
                    continue;
                }
                EnemyProductionSite site = queue.GetComponent<EnemyProductionSite>();
                if (site == null)
                {
                    site = queue.gameObject.AddComponent<EnemyProductionSite>();
                }
                sites.Add(site);
            }

            if (sites.Count == 0 && setup.CreateProducerWhenMissing)
            {
                Vector3 preferred = setup.ResolvePreferredProducerPosition(
                    mapDefinition.WorldBounds);
                if (!NavMesh.SamplePosition(
                        preferred,
                        out NavMeshHit hit,
                        setup.NavMeshSearchDistance,
                        NavMesh.AllAreas))
                {
                    error = "Enemy producer spawn has no nearby NavMesh position.";
                    return Array.Empty<EnemyProductionSite>();
                }

                GameObject instance = UnityEngine.Object.Instantiate(
                    setup.ProducerPrefab,
                    hit.position,
                    Quaternion.LookRotation(
                        Flatten(mapDefinition.WorldBounds.center - hit.position),
                        Vector3.up),
                    runtimeRoot);
                instance.name = setup.ProducerPrefab.name + "_EnemyPreset";
                FactionMember faction = instance.GetComponent<FactionMember>();
                if (faction == null)
                {
                    error = "Enemy producer prefab has no FactionMember.";
                    UnityEngine.Object.Destroy(instance);
                    return Array.Empty<EnemyProductionSite>();
                }
                faction.SetFaction(setup.Definition.Faction);

                EntityIdentity identity = instance.GetComponent<EntityIdentity>();
                if (identity == null)
                {
                    error = "Enemy producer prefab has no EntityIdentity.";
                    UnityEngine.Object.Destroy(instance);
                    return Array.Empty<EnemyProductionSite>();
                }
                identity.UnregisterFromRegistry();
                identity.AssignRuntimeId(setup.ProducerRuntimeId, true);
                identity.MarkRuntimeSpawned(false);
                if (registry != null && instance.activeInHierarchy)
                {
                    identity.RegisterWith(registry);
                }

                ProductionQueue queue = instance.GetComponent<ProductionQueue>();
                if (queue == null)
                {
                    error = "Enemy producer prefab has no ProductionQueue.";
                    UnityEngine.Object.Destroy(instance);
                    return Array.Empty<EnemyProductionSite>();
                }
                EnemyProductionSite site = instance.GetComponent<EnemyProductionSite>();
                if (site == null)
                {
                    site = instance.AddComponent<EnemyProductionSite>();
                }
                sites.Add(site);
            }

            if (sites.Count == 0)
            {
                error = "Enemy AI map has no production site.";
                return Array.Empty<EnemyProductionSite>();
            }
            error = string.Empty;
            return sites.ToArray();
        }

        private static void MarkSceneEnemyUnits(Scene scene, bool markExisting)
        {
            if (!markExisting)
            {
                return;
            }
            EntityIdentity[] identities = Collect<EntityIdentity>(scene);
            for (int i = 0; i < identities.Length; i++)
            {
                EntityIdentity identity = identities[i];
                FactionMember faction = identity != null
                    ? identity.GetComponent<FactionMember>()
                    : null;
                if (faction == null || faction.Faction != FactionId.Enemy)
                {
                    continue;
                }
                if (identity.HasAnyRole(UnitRole.Hero))
                {
                    if (identity.GetComponent<EnemyHeroBrain>() == null)
                    {
                        identity.gameObject.AddComponent<EnemyHeroBrain>();
                    }
                    continue;
                }
                if (!identity.HasAnyRole(UnitRole.Combat) ||
                    identity.GetComponent<UnitCommandQueue>() == null ||
                    identity.GetComponent<AiControllableUnit>() != null)
                {
                    continue;
                }
                identity.gameObject.AddComponent<AiControllableUnit>();
            }
        }

        private static StrategicTarget[] CollectStrategicTargets(Scene scene)
        {
            var targets = new List<StrategicTarget>(4);
            EntityIdentity[] identities = Collect<EntityIdentity>(scene);
            for (int i = 0; i < identities.Length; i++)
            {
                EntityIdentity identity = identities[i];
                FactionMember faction = identity != null
                    ? identity.GetComponent<FactionMember>()
                    : null;
                VitalsComponent vitals = identity != null
                    ? identity.GetComponent<VitalsComponent>()
                    : null;
                if (identity == null || !identity.HasAnyRole(UnitRole.Building) ||
                    faction == null || faction.Faction != FactionId.Friendly ||
                    vitals != null && !vitals.IsAlive)
                {
                    continue;
                }
                StrategicTarget target = identity.GetComponent<StrategicTarget>();
                if (target == null)
                {
                    target = identity.gameObject.AddComponent<StrategicTarget>();
                }
                targets.Add(target);
            }
            return targets.ToArray();
        }

        private static void SetProducerRallies(
            EnemyProductionSite[] sites,
            Vector3 requestedPoint)
        {
            for (int i = 0; i < sites.Length; i++)
            {
                RallyPointController rally = sites[i] != null && sites[i].Queue != null
                    ? sites[i].Queue.RallyPointController
                    : null;
                rally?.TrySetRallyPoint(requestedPoint, out _);
            }
        }

        private static Vector3 Flatten(Vector3 value)
        {
            value.y = 0f;
            return value.sqrMagnitude > 0.0001f ? value.normalized : Vector3.forward;
        }

        private static T[] Collect<T>(Scene scene) where T : Component
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return Array.Empty<T>();
            }
            GameObject[] roots = scene.GetRootGameObjects();
            var results = new List<T>(32);
            for (int i = 0; i < roots.Length; i++)
            {
                results.AddRange(roots[i].GetComponentsInChildren<T>(true));
            }
            return results.ToArray();
        }
    }
}
