using System.Collections;
using NUnit.Framework;
using OriginCore.Core;
using OriginCore.RTS.Commands;
using OriginCore.UI;
using OriginCore.UI.FPS;
using OriginCore.UI.MainMenu;
using OriginCore.UI.Pause;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace OriginCore.Tests.PlayMode
{
    public sealed class MenuSettingsPausePlayModeTests
    {
        private const string MainMenuScenePath =
            "Assets/_OriginCore/Scenes/01_MainMenu.unity";
        private const string SystemTestScenePath =
            "Assets/_OriginCore/Scenes/90_SystemTest.unity";
        private const float TimeoutSeconds = 10f;

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
        }

        [UnityTest]
        public IEnumerator EscapePriorityCancelsTargetingThenPausesAndResumes()
        {
            yield return LoadAndWaitForServices(SystemTestScenePath);
            GameServices services = AppRoot.Instance.Services;
            PauseService pause = services.PauseService;
            RtsCommandIssuer issuer = services.CurrentSceneContext.RtsCommandIssuer;
            PauseMenuController menu = Object.FindObjectOfType<PauseMenuController>(true);
            Assert.That(pause, Is.Not.Null);
            Assert.That(issuer, Is.Not.Null);
            Assert.That(menu, Is.Not.Null);
            Assert.That(issuer.BeginMoveTargeting(), Is.True);

            Assert.That(pause.ProcessEscapeRequest(), Is.True);
            Assert.That(issuer.TargetingState, Is.EqualTo(CommandTargetingState.None));
            Assert.That(pause.IsPaused, Is.False);

            GameMode modeBeforePause = services.GameModeController.CurrentMode;
            Assert.That(pause.ProcessEscapeRequest(), Is.True);
            Assert.That(pause.IsPaused, Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(services.InputRouter.GameplayEnabled, Is.False);
            Assert.That(services.CursorPolicy.PauseOverride, Is.True);
            Assert.That(menu.VisibleRoot.activeSelf, Is.True);
            Assert.That(services.GameModeController.CurrentMode, Is.EqualTo(modeBeforePause));

            Assert.That(pause.ProcessEscapeRequest(), Is.True);
            Assert.That(pause.IsPaused, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(services.InputRouter.GameplayEnabled, Is.True);
            Assert.That(services.CursorPolicy.PauseOverride, Is.False);
            Assert.That(menu.VisibleRoot.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator ThreeModeHudAndCrosshairRemainMutuallyExclusive()
        {
            yield return LoadAndWaitForServices(SystemTestScenePath);
            GameServices services = AppRoot.Instance.Services;
            DirectControlHudView hud = Object.FindObjectOfType<DirectControlHudView>(true);
            CrosshairView crosshair = Object.FindObjectOfType<CrosshairView>(true);
            Assert.That(hud, Is.Not.Null);
            Assert.That(crosshair, Is.Not.Null);

            Transform rts = hud.transform.Find("RTSOverlay");
            Transform act = hud.transform.Find("ACTOverlay");
            Transform fps = hud.transform.Find("FPSOverlay");
            Assert.That(rts.gameObject.activeSelf, Is.True);
            Assert.That(act.gameObject.activeSelf, Is.False);
            Assert.That(fps.gameObject.activeSelf, Is.False);
            Assert.That(crosshair.IsVisible, Is.False);

            Assert.That(services.GameModeController.RequestMode(GameMode.ACT), Is.True);
            yield return null;
            Assert.That(rts.gameObject.activeSelf, Is.False);
            Assert.That(act.gameObject.activeSelf, Is.True);
            Assert.That(fps.gameObject.activeSelf, Is.False);
            Assert.That(crosshair.IsVisible, Is.False);

            Assert.That(services.GameModeController.RequestMode(GameMode.FPS), Is.True);
            yield return null;
            Assert.That(rts.gameObject.activeSelf, Is.False);
            Assert.That(act.gameObject.activeSelf, Is.False);
            Assert.That(fps.gameObject.activeSelf, Is.True);
            Assert.That(crosshair.IsVisible, Is.True);
        }

        [UnityTest]
        public IEnumerator MainMenuRoutesPagesAndConsumesEscapeBeforePause()
        {
            yield return LoadAndWaitForServices(MainMenuScenePath);
            MainMenuController menu = Object.FindObjectOfType<MainMenuController>(true);
            PauseService pause = AppRoot.Instance.Services.PauseService;
            Assert.That(menu, Is.Not.Null);
            Assert.That(AppRoot.Instance.Services.InputRouter.GameplayEnabled, Is.False);
            Assert.That(menu.PageStack.CurrentPageId, Is.EqualTo(MainMenuController.MainPageId));

            menu.OpenPlay();
            Assert.That(menu.PageStack.CurrentPageId, Is.EqualTo(MainMenuController.PlayPageId));
            menu.OpenStory();
            Assert.That(menu.PageStack.CurrentPageId, Is.EqualTo(MainMenuController.StoryPageId));
            menu.GoBack();
            Assert.That(menu.PageStack.CurrentPageId, Is.EqualTo(MainMenuController.PlayPageId));
            menu.GoBack();
            menu.OpenOptions();
            Assert.That(menu.PageStack.CurrentPageId, Is.EqualTo(MainMenuController.OptionsPageId));
            Assert.That(pause.ProcessEscapeRequest(), Is.True);
            Assert.That(menu.PageStack.CurrentPageId, Is.EqualTo(MainMenuController.MainPageId));
            Assert.That(pause.IsPaused, Is.False);

            menu.ShowQuitConfirmation();
            Assert.That(menu.ModalStack.Depth, Is.EqualTo(1));
            Assert.That(pause.ProcessEscapeRequest(), Is.True);
            Assert.That(menu.ModalStack.Depth, Is.Zero);
            Assert.That(pause.IsPaused, Is.False);
        }

        private static IEnumerator LoadAndWaitForServices(string scenePath)
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

            int buildIndex = SceneUtility.GetBuildIndexByScenePath(scenePath);
            Assert.That(buildIndex, Is.GreaterThanOrEqualTo(0));
            AsyncOperation load = SceneManager.LoadSceneAsync(buildIndex, LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;

            float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
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
    }
}
