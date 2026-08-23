using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using OriginCore.Core;
using OriginCore.Input;
using OriginCore.SceneFlow;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace OriginCore.Tests.PlayMode
{
    public sealed class BootstrapSceneFlowTests
    {
        private const string BootstrapScenePath = "Assets/_OriginCore/Scenes/00_Bootstrap.unity";
        private const string MainMenuScenePath = "Assets/_OriginCore/Scenes/01_MainMenu.unity";
        private const string SystemTestScenePath = "Assets/_OriginCore/Scenes/90_SystemTest.unity";
        private const float SceneLoadTimeoutSeconds = 10f;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return DestroyPersistentRoot();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return DestroyPersistentRoot();
        }

        [UnityTest]
        public IEnumerator BootstrapLoadsMainMenuWithOneAppRoot()
        {
            AsyncOperation bootstrapLoad = LoadBuildSceneAsync(BootstrapScenePath);
            Assert.That(bootstrapLoad, Is.Not.Null);
            yield return bootstrapLoad;
            yield return WaitForActiveScene(MainMenuScenePath);

            AssertSingleAppRootAndContext(MainMenuScenePath);
            AssertNoDuplicateInfrastructure();
        }

        [UnityTest]
        public IEnumerator DirectSystemTestPlayCreatesOneAppRoot()
        {
            AsyncOperation directLoad = LoadBuildSceneAsync(SystemTestScenePath);
            Assert.That(directLoad, Is.Not.Null);
            yield return directLoad;
            yield return null;

            AssertSingleAppRootAndContext(SystemTestScenePath);
            AssertNoDuplicateInfrastructure();
            AssertSystemTestPlaceholderScene();
        }

        [UnityTest]
        public IEnumerator RoundTripReplacesSceneContextWithoutDuplicatingAppRoot()
        {
            AsyncOperation directLoad = LoadBuildSceneAsync(MainMenuScenePath);
            Assert.That(directLoad, Is.Not.Null);
            yield return directLoad;
            yield return null;

            AssertSingleAppRootAndContext(MainMenuScenePath);
            AppRoot appRoot = AppRoot.Instance;
            SceneContext originalContext = appRoot.Services.CurrentSceneContext;

            Assert.That(appRoot.Services.SceneFlow.LoadScene(SceneCatalog.SystemTestSceneKey), Is.True);
            yield return WaitForActiveScene(SystemTestScenePath);
            Assert.That(originalContext == null, Is.True, "The unloaded MainMenu SceneContext is still alive.");
            AssertSingleAppRootAndContext(SystemTestScenePath);

            SceneContext systemTestContext = appRoot.Services.CurrentSceneContext;
            AsyncOperation returnLoad = appRoot.Services.SceneFlow.LoadSceneAsync(SceneCatalog.MainMenuSceneKey);
            Assert.That(returnLoad, Is.Not.Null);
            yield return returnLoad;
            yield return WaitForActiveScene(MainMenuScenePath);

            Assert.That(systemTestContext == null, Is.True, "The unloaded SystemTest SceneContext is still alive.");
            AssertSingleAppRootAndContext(MainMenuScenePath);
            AssertNoDuplicateInfrastructure();
        }

        private static IEnumerator WaitForActiveScene(string expectedPath)
        {
            float deadline = Time.realtimeSinceStartup + SceneLoadTimeoutSeconds;
            while (SceneManager.GetActiveScene().path != expectedPath &&
                   Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(expectedPath));
            yield return null;
        }

        private static AsyncOperation LoadBuildSceneAsync(string scenePath)
        {
            int buildIndex = SceneUtility.GetBuildIndexByScenePath(scenePath);
            Assert.That(buildIndex, Is.GreaterThanOrEqualTo(0), scenePath + " is missing from Build Settings.");
            return SceneManager.LoadSceneAsync(buildIndex, LoadSceneMode.Single);
        }

        private static void AssertSingleAppRootAndContext(string expectedScenePath)
        {
            AppRoot[] appRoots = Object.FindObjectsOfType<AppRoot>(true);
            Assert.That(appRoots.Length, Is.EqualTo(1));
            Assert.That(AppRoot.Instance, Is.SameAs(appRoots[0]));
            Assert.That(AppRoot.Instance.Services, Is.Not.Null);
            Assert.That(AppRoot.Instance.Services.InputRouter, Is.Not.Null);
            Assert.That(AppRoot.Instance.Services.InputRebindService, Is.Not.Null);
            Assert.That(AppRoot.Instance.Services.InputRouter.IsInitialized, Is.True);
            Assert.That(AppRoot.Instance.Services.InputRouter.ActiveGameplayMap, Is.EqualTo(GameplayInputMap.RTS));

            SceneContext context = AppRoot.Instance.Services.CurrentSceneContext;
            Assert.That(context, Is.Not.Null);
            Assert.That(context.Scene.path, Is.EqualTo(expectedScenePath));
        }

        private static void AssertNoDuplicateInfrastructure()
        {
            Assert.That(Object.FindObjectsOfType<AppRoot>(true).Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsOfType<EventSystem>(true).Length, Is.EqualTo(1));
            InputSystemUIInputModule[] inputModules =
                Object.FindObjectsOfType<InputSystemUIInputModule>(true);
            Assert.That(inputModules.Length, Is.EqualTo(1));
            Assert.That(inputModules[0].actionsAsset,
                Is.SameAs(AppRoot.Instance.Services.InputRouter.ActionsAsset));
            Assert.That(Object.FindObjectsOfType<AudioListener>(true).Length, Is.LessThanOrEqualTo(1));
        }

        private static void AssertSystemTestPlaceholderScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            GameObject testbed = FindRoot(scene, "P03_Testbed");
            Assert.That(testbed, Is.Not.Null);

            Transform actors = testbed.transform.Find("Actors");
            Assert.That(actors, Is.Not.Null);
            string[] actorNames =
            {
                "Hero",
                "Friendly_01",
                "Friendly_02",
                "Friendly_03",
                "Enemy_01",
                "Enemy_02",
                "Worker_01",
                "ProducerBuilding",
                "ResourceCrystal"
            };

            for (int i = 0; i < actorNames.Length; i++)
            {
                Assert.That(actors.Find(actorNames[i]), Is.Not.Null, "Missing actor " + actorNames[i]);
            }

            SceneContext context = AppRoot.Instance.Services.CurrentSceneContext;
            List<Camera> sceneCameras = FindComponentsInScene<Camera>(scene);
            Assert.That(sceneCameras.Count, Is.EqualTo(1));
            Assert.That(sceneCameras, Does.Contain(context.MainCamera));
            Assert.That(context.MinimapMapDefinition, Is.Not.Null,
                "The minimap must use scene-owned drawing data instead of a Camera.");
            Assert.That(context.MinimapView, Is.Not.Null);
            Assert.That(FindComponentsInScene<AudioListener>(scene).Count, Is.EqualTo(1));

            Assert.That(context.SpawnPoints.Count, Is.EqualTo(7));
            for (int i = 0; i < context.SpawnPoints.Count; i++)
            {
                NavMeshHit hit;
                Assert.That(
                    NavMesh.SamplePosition(context.SpawnPoints[i].position, out hit, 1f, NavMesh.AllAreas),
                    Is.True,
                    "Spawn point " + i + " is off the NavMesh.");
            }
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == name)
                {
                    return roots[i];
                }
            }

            return null;
        }

        private static System.Collections.Generic.List<T> FindComponentsInScene<T>(Scene scene)
            where T : Component
        {
            System.Collections.Generic.List<T> result = new System.Collections.Generic.List<T>();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                result.AddRange(roots[i].GetComponentsInChildren<T>(true));
            }

            return result;
        }

        private static IEnumerator DestroyPersistentRoot()
        {
            if (AppRoot.HasInstance)
            {
                Object.Destroy(AppRoot.Instance.gameObject);
                yield return null;
            }
        }
    }
}
