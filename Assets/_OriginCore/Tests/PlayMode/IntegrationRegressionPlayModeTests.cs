using System.Collections;
using NUnit.Framework;
using OriginCore.Core;
using OriginCore.Gameplay;
using OriginCore.Input;
using OriginCore.SceneFlow;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace OriginCore.Tests.PlayMode
{
    public sealed class IntegrationRegressionPlayModeTests
    {
        private const string SystemTestScenePath =
            "Assets/_OriginCore/Scenes/90_SystemTest.unity";
        private const float SceneLoadTimeoutSeconds = 10f;

        [UnitySetUp]
        public IEnumerator LoadSystemTestScene()
        {
            Time.timeScale = 1f;
            yield return DestroyPersistentRoots();

            int buildIndex = SceneUtility.GetBuildIndexByScenePath(SystemTestScenePath);
            Assert.That(buildIndex, Is.GreaterThanOrEqualTo(0),
                "90_SystemTest is missing from Build Settings.");
            AsyncOperation load = SceneManager.LoadSceneAsync(
                buildIndex,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;

            float deadline = Time.realtimeSinceStartup + SceneLoadTimeoutSeconds;
            while ((!AppRoot.HasInstance || AppRoot.Instance.Services == null ||
                    AppRoot.Instance.Services.CurrentSceneContext == null) &&
                   Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(AppRoot.HasInstance, Is.True);
            Assert.That(AppRoot.Instance.Services, Is.Not.Null);
            Assert.That(AppRoot.Instance.Services.CurrentSceneContext, Is.Not.Null);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (AppRoot.HasInstance && AppRoot.Instance.Services != null)
            {
                PauseService pause = AppRoot.Instance.Services.PauseService;
                if (pause != null && pause.IsPaused)
                {
                    pause.SetPaused(false);
                }
            }

            Time.timeScale = 1f;
            yield return DestroyPersistentRoots();
        }

        [UnityTest]
        public IEnumerator ThreeModeAndPauseCyclesDoNotMultiplyCallbacksOrInfrastructure()
        {
            GameServices services = AppRoot.Instance.Services;
            GameModeController modes = services.GameModeController;
            PauseService pause = services.PauseService;
            SceneContext originalContext = services.CurrentSceneContext;
            HybridControlDriver pawn = services.PossessionService.CurrentPawn;
            Assert.That(modes, Is.Not.Null);
            Assert.That(pause, Is.Not.Null);
            Assert.That(originalContext, Is.Not.Null);
            Assert.That(pawn, Is.Not.Null);
            Assert.That(modes.CurrentMode, Is.EqualTo(GameMode.RTS));
            Assert.That(pause.IsPaused, Is.False);

            int modeChangeCount = 0;
            int pauseChangeCount = 0;
            modes.ModeChanged += _ => modeChangeCount++;
            pause.PauseStateChanged += _ => pauseChangeCount++;

            for (int cycle = 0; cycle < 3; cycle++)
            {
                Assert.That(modes.RequestMode(GameMode.ACT), Is.True);
                yield return null;
                Assert.That(modes.CurrentMode, Is.EqualTo(GameMode.ACT));
                Assert.That(pawn.State,
                    Is.EqualTo(HybridControlState.PendingManualOverride));

                Assert.That(modes.RequestMode(GameMode.RTS), Is.True);
                yield return null;
                Assert.That(modes.CurrentMode, Is.EqualTo(GameMode.RTS));
                Assert.That(pawn.State, Is.EqualTo(HybridControlState.Autopilot));

                Assert.That(modes.RequestMode(GameMode.FPS), Is.True);
                yield return null;
                Assert.That(modes.CurrentMode, Is.EqualTo(GameMode.FPS));
                Assert.That(pawn.State,
                    Is.EqualTo(HybridControlState.PendingManualOverride));

                Assert.That(modes.RequestMode(GameMode.RTS), Is.True);
                yield return null;
                Assert.That(modes.CurrentMode, Is.EqualTo(GameMode.RTS));
                Assert.That(pawn.State, Is.EqualTo(HybridControlState.Autopilot));

                Assert.That(pause.SetPaused(true), Is.True);
                Assert.That(Time.timeScale, Is.Zero);
                Assert.That(services.InputRouter.GameplayEnabled, Is.False);
                yield return null;

                Assert.That(pause.SetPaused(false), Is.True);
                Assert.That(Time.timeScale, Is.EqualTo(1f));
                Assert.That(services.InputRouter.GameplayEnabled, Is.True);
                yield return null;

                AssertStableInfrastructure(services, originalContext);
                Assert.That(modeChangeCount, Is.EqualTo((cycle + 1) * 4));
                Assert.That(pauseChangeCount, Is.EqualTo((cycle + 1) * 2));
            }

            Assert.That(modeChangeCount, Is.EqualTo(12));
            Assert.That(pauseChangeCount, Is.EqualTo(6));
        }

        private static void AssertStableInfrastructure(
            GameServices services,
            SceneContext originalContext)
        {
            Assert.That(Object.FindObjectsOfType<AppRoot>(true).Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsOfType<SceneContext>(true).Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsOfType<EventSystem>(true).Length, Is.EqualTo(1));
            Assert.That(
                Object.FindObjectsOfType<InputSystemUIInputModule>(true).Length,
                Is.EqualTo(1));
            Assert.That(Object.FindObjectsOfType<AudioListener>(true).Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsOfType<Camera>(true).Length, Is.EqualTo(1));
            Assert.That(services.CurrentSceneContext, Is.SameAs(originalContext));
            Assert.That(services.GameModeController.CurrentMode, Is.EqualTo(GameMode.RTS));
            Assert.That(services.InputRouter.ActiveGameplayMap,
                Is.EqualTo(GameplayInputMap.RTS));
            Assert.That(originalContext.CameraModeCoordinator.ActivePriorityCameraCount,
                Is.EqualTo(1));
            Assert.That(originalContext.ModeHudPresenter.Mode, Is.EqualTo(GameMode.RTS));
        }

        private static IEnumerator DestroyPersistentRoots()
        {
            AppRoot[] roots = Object.FindObjectsOfType<AppRoot>(true);
            for (int i = 0; i < roots.Length; i++)
            {
                Object.Destroy(roots[i].gameObject);
            }

            if (roots.Length > 0)
            {
                yield return null;
            }
        }
    }
}
