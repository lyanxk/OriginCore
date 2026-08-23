using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using OriginCore.Combat;
using OriginCore.Core;
using OriginCore.Economy;
using OriginCore.Gameplay;
using OriginCore.RTS;
using OriginCore.Save;
using OriginCore.Units;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace OriginCore.Tests.PlayMode
{
    public sealed class SaveLoadPlayModeTests
    {
        private const string SystemTestScenePath =
            "Assets/_OriginCore/Scenes/90_SystemTest.unity";
        private const float TimeoutSeconds = 15f;

        private readonly List<string> _temporaryDirectories = new List<string>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            AppRoot[] roots = Object.FindObjectsOfType<AppRoot>(true);
            for (int i = 0; i < roots.Length; i++)
            {
                Object.Destroy(roots[i].gameObject);
            }

            yield return null;
            // A destroyed paused AppRoot may restore its captured time scale during
            // OnDestroy. Reset after destruction to keep this fixture isolated.
            Time.timeScale = 1f;
            for (int i = 0; i < _temporaryDirectories.Count; i++)
            {
                if (Directory.Exists(_temporaryDirectories[i]))
                {
                    Directory.Delete(_temporaryDirectories[i], true);
                }
            }

            _temporaryDirectories.Clear();
        }

        [UnityTest]
        public IEnumerator SaveLoadRestoresResourcesModeEntitiesAndClearsTransientState()
        {
            yield return LoadAndWaitForServices();
            GameServices services = AppRoot.Instance.Services;
            SaveService save = services.SaveService;
            string directory = CreateTemporaryDirectory("restore");
            Assert.That(save.ConfigureRepositoryPath(directory, out string error), Is.True, error);

            EntityIdentity hero = FindByRole(services.EntityRegistry, UnitRole.Hero);
            Assert.That(hero, Is.Not.Null);
            string heroId = hero.RuntimeId;
            Vector3 heroPosition = hero.transform.position;
            Quaternion heroRotation = hero.transform.rotation;
            VitalsComponent heroVitals = hero.GetComponent<VitalsComponent>();
            Assert.That(heroVitals, Is.Not.Null);
            heroVitals.SetHealth(73f);
            heroVitals.SetShield(Mathf.Min(11f, heroVitals.MaxShield));

            services.ResourceService.Restore(new ResourceSnapshot(321, 123, 1, 20));
            Assert.That(services.GameModeController.RequestMode(GameMode.ACT), Is.True);

            UnitSpawner spawner = Object.FindObjectOfType<UnitSpawner>(true);
            Assert.That(spawner, Is.Not.Null);
            string restoredUnitId = "runtime_saved_worker";
            Vector3 unitPosition = spawner.SpawnPoint.position + Vector3.forward * 2f;
            Assert.That(spawner.TryRestoreSpawn(
                spawner.WorkerDefinition.ArchetypeId,
                restoredUnitId,
                unitPosition,
                Quaternion.identity,
                FactionId.Friendly,
                1,
                services.ResourceService,
                out EntityIdentity savedRuntimeUnit,
                out error), Is.True, error);
            yield return null;

            int expectedEntityCount = services.EntityRegistry.Count;
            SelectionService selection = services.CurrentSceneContext.SelectionService;
            Vector3 screenPoint = services.CurrentSceneContext.MainCamera.WorldToScreenPoint(
                hero.transform.position + Vector3.up);
            Assert.That(selection.ClickAtScreenPoint(screenPoint, false), Is.True);
            Assert.That(selection.SelectedCount, Is.EqualTo(1));

            Assert.That(save.TrySaveNew(
                "RESTORE TEST",
                false,
                out SaveSlotMetadata metadata,
                out error), Is.True, error);
            Assert.That(metadata, Is.Not.Null);

            services.GameModeController.RequestMode(GameMode.RTS);
            // The saved worker still owns one Influence reservation until the scene
            // is replaced. Keep the live wallet consistent so its OnDisable release
            // remains valid during load.
            services.ResourceService.Restore(new ResourceSnapshot(1, 2, 1, 5));
            heroVitals.SetHealth(1f);
            MoveEntityToNearbyNavMesh(hero, heroPosition + Vector3.right * 4f);
            Assert.That(spawner.TryRestoreSpawn(
                spawner.CombatDefinition.ArchetypeId,
                "runtime_after_save",
                spawner.SpawnPoint.position + Vector3.left * 2f,
                Quaternion.identity,
                FactionId.Friendly,
                0,
                services.ResourceService,
                out _,
                out error), Is.True, error);
            yield return null;
            Assert.That(services.EntityRegistry.Count, Is.EqualTo(expectedEntityCount + 1));

            Assert.That(save.TryBeginLoad(metadata.SlotId, out error), Is.True, error);
            yield return WaitForSaveOperation(save);
            yield return null;

            services = AppRoot.Instance.Services;
            Assert.That(services.SaveService.CurrentSlotId, Is.EqualTo(metadata.SlotId));
            Assert.That(services.ResourceService.Snapshot,
                Is.EqualTo(new ResourceSnapshot(321, 123, 1, 20)));
            Assert.That(services.GameModeController.CurrentMode, Is.EqualTo(GameMode.ACT));
            Assert.That(services.CurrentSceneContext.SelectionService.SelectedCount, Is.Zero);

            EntityIdentity restoredHero = FindByRuntimeId(services.EntityRegistry, heroId);
            Assert.That(restoredHero, Is.Not.Null);
            Assert.That(Vector3.Distance(restoredHero.transform.position, heroPosition),
                Is.LessThan(0.1f));
            Assert.That(Quaternion.Angle(restoredHero.transform.rotation, heroRotation),
                Is.LessThan(0.1f));
            Assert.That(restoredHero.GetComponent<VitalsComponent>().Health,
                Is.EqualTo(73f).Within(0.01f));
            Assert.That(FindByRuntimeId(services.EntityRegistry, restoredUnitId), Is.Not.Null);
            Assert.That(FindByRuntimeId(services.EntityRegistry, "runtime_after_save"), Is.Null);
            Assert.That(services.EntityRegistry.Count, Is.EqualTo(expectedEntityCount));
            AssertRuntimeIdsAreUnique(services.EntityRegistry);
            Assert.That(Object.FindObjectsOfType<AppRoot>(true), Has.Length.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator UnknownRuntimeArchetypeIsSkippedWithoutAbortingTheLoad()
        {
            yield return LoadAndWaitForServices();
            GameServices services = AppRoot.Instance.Services;
            SaveService save = services.SaveService;
            string directory = CreateTemporaryDirectory("unknown");
            Assert.That(save.ConfigureRepositoryPath(directory, out string error), Is.True, error);
            Assert.That(save.TrySaveNew(
                "UNKNOWN ARCHETYPE",
                false,
                out SaveSlotMetadata metadata,
                out error), Is.True, error);
            Assert.That(save.Repository.TryRead(
                metadata.SlotId,
                out SaveGameData data,
                out error), Is.True, error);
            data.entities.Add(new EntitySaveData
            {
                runtimeId = "runtime_unknown_archetype",
                archetypeId = "unit.does.not.exist",
                runtimeSpawned = true,
                position = Vector3.zero,
                rotation = Quaternion.identity,
                faction = (int)FactionId.Friendly
            });
            Assert.That(save.Repository.TryWrite(data, out error), Is.True, error);

            LogAssert.Expect(
                LogType.Warning,
                new Regex("Unknown runtime archetype.*unit\\.does\\.not\\.exist"));
            Assert.That(save.TryBeginLoad(metadata.SlotId, out error), Is.True, error);
            yield return WaitForSaveOperation(save);
            yield return null;

            services = AppRoot.Instance.Services;
            Assert.That(FindByRuntimeId(
                services.EntityRegistry,
                "runtime_unknown_archetype"), Is.Null);
            Assert.That(services.SaveService.CurrentSlotId, Is.EqualTo(metadata.SlotId));
        }

        [UnityTest]
        public IEnumerator FailedSaveAndExitPreservesTheOldSlotAndDoesNotQuit()
        {
            yield return LoadAndWaitForServices();
            SaveService save = AppRoot.Instance.Services.SaveService;
            string validDirectory = CreateTemporaryDirectory("save_exit_valid");
            Assert.That(save.ConfigureRepositoryPath(
                validDirectory,
                out string error), Is.True, error);
            Assert.That(save.TrySaveNew(
                "VALID SLOT",
                false,
                out SaveSlotMetadata valid,
                out error), Is.True, error);
            SaveRepository validRepository = new SaveRepository(validDirectory);

            int quitRequests = 0;
            save.SetQuitOverride(() => quitRequests++);
            string blockerPath = Path.Combine(
                CreateTemporaryDirectory("save_exit_blocked"),
                "not_a_directory");
            File.WriteAllText(blockerPath, "block");
            Assert.That(save.ConfigureRepositoryPath(
                Path.Combine(blockerPath, "Saves"),
                out error), Is.True, error);

            LogAssert.Expect(LogType.Error, new Regex("\\[OriginCore P14\\].+"));
            Assert.That(save.TrySaveNew(
                "MUST FAIL",
                true,
                out _,
                out error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(quitRequests, Is.Zero);
            Assert.That(validRepository.TryRead(
                valid.SlotId,
                out SaveGameData preserved,
                out error), Is.True, error);
            Assert.That(preserved.displayName, Is.EqualTo("VALID SLOT"));
        }

        private static IEnumerator LoadAndWaitForServices()
        {
            AppRoot[] existingRoots = Object.FindObjectsOfType<AppRoot>(true);
            for (int i = 0; i < existingRoots.Length; i++)
            {
                Object.Destroy(existingRoots[i].gameObject);
            }

            if (existingRoots.Length > 0)
            {
                yield return null;
            }

            Time.timeScale = 1f;

            int buildIndex = SceneUtility.GetBuildIndexByScenePath(SystemTestScenePath);
            Assert.That(buildIndex, Is.GreaterThanOrEqualTo(0));
            AsyncOperation load = SceneManager.LoadSceneAsync(buildIndex, LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;

            float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while ((!AppRoot.HasInstance || AppRoot.Instance.Services == null ||
                    AppRoot.Instance.Services.CurrentSceneContext == null ||
                    AppRoot.Instance.Services.SaveService == null) &&
                   Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(AppRoot.HasInstance, Is.True);
            Assert.That(AppRoot.Instance.Services, Is.Not.Null);
            Assert.That(AppRoot.Instance.Services.CurrentSceneContext, Is.Not.Null);
            Assert.That(AppRoot.Instance.Services.SaveService.IsInitialized, Is.True);
            yield return null;
        }

        private static IEnumerator WaitForSaveOperation(SaveService save)
        {
            float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (save != null && save.IsBusy && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(save, Is.Not.Null);
            Assert.That(save.IsBusy, Is.False, "Save operation timed out.");
        }

        private string CreateTemporaryDirectory(string suffix)
        {
            string directory = Path.Combine(
                Application.temporaryCachePath,
                "OriginCore_P14_" + suffix + "_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            _temporaryDirectories.Add(directory);
            return directory;
        }

        private static EntityIdentity FindByRole(EntityRegistry registry, UnitRole role)
        {
            IReadOnlyList<EntityIdentity> entities = registry.Entities;
            for (int i = 0; i < entities.Count; i++)
            {
                if (entities[i] != null && entities[i].HasAnyRole(role))
                {
                    return entities[i];
                }
            }

            return null;
        }

        private static EntityIdentity FindByRuntimeId(EntityRegistry registry, string runtimeId)
        {
            IReadOnlyList<EntityIdentity> entities = registry.Entities;
            for (int i = 0; i < entities.Count; i++)
            {
                if (entities[i] != null && entities[i].RuntimeId == runtimeId)
                {
                    return entities[i];
                }
            }

            return null;
        }

        private static void AssertRuntimeIdsAreUnique(EntityRegistry registry)
        {
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            IReadOnlyList<EntityIdentity> entities = registry.Entities;
            for (int i = 0; i < entities.Count; i++)
            {
                Assert.That(ids.Add(entities[i].RuntimeId), Is.True, entities[i].RuntimeId);
            }
        }

        private static void MoveEntityToNearbyNavMesh(
            EntityIdentity identity,
            Vector3 requested)
        {
            Assert.That(NavMesh.SamplePosition(
                requested,
                out NavMeshHit hit,
                6f,
                NavMesh.AllAreas), Is.True);
            NavMeshAgent agent = identity.GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                Assert.That(agent.Warp(hit.position), Is.True);
            }
            else
            {
                identity.transform.position = hit.position;
            }
        }
    }
}
