using System.Collections.Generic;
using NUnit.Framework;
using OriginCore.Core;
using OriginCore.Input;
using OriginCore.Settings;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace OriginCore.Tests.EditMode
{
    public sealed class InputRebindServiceTests
    {
        private const string InputActionsPath = "Assets/_OriginCore/Input/IA_OriginCore.inputactions";
        private const string AppRootPrefabPath = "Assets/_OriginCore/Prefabs/Core/PF_AppRoot.prefab";

        [Test]
        public void GeneratedWrapperContainsFiveRequiredActionMaps()
        {
            OriginCoreInputActions actions = new OriginCoreInputActions();
            try
            {
                Assert.That(actions.asset.actionMaps.Count, Is.EqualTo(5));
                Assert.That(actions.asset.FindActionMap("Global", false), Is.Not.Null);
                Assert.That(actions.asset.FindActionMap("RTS", false), Is.Not.Null);
                Assert.That(actions.asset.FindActionMap("ACT", false), Is.Not.Null);
                Assert.That(actions.asset.FindActionMap("FPS", false), Is.Not.Null);
                Assert.That(actions.asset.FindActionMap("UI", false), Is.Not.Null);

                AssertActionHasPath(actions.Global.SwitchRTS, "<Keyboard>/f1");
                AssertActionHasPath(actions.Global.SwitchACT, "<Keyboard>/f2");
                AssertActionHasPath(actions.Global.SwitchFPS, "<Keyboard>/f3");
                AssertActionHasPath(actions.Global.Pause, "<Keyboard>/escape");
                AssertActionHasPath(actions.ACT.Move, "<Keyboard>/w");
                AssertActionHasPath(actions.FPS.Aim, "<Mouse>/rightButton");
                AssertActionHasPath(actions.UI.Scroll, "<Mouse>/scroll");
            }
            finally
            {
                DestroyInputActionsImmediate(actions);
            }
        }

        [Test]
        public void BindingOverridesRoundTripThroughSettingsData()
        {
            OriginCoreInputActions actions = new OriginCoreInputActions();
            try
            {
                InputAction jump = actions.ACT.Jump;
                int bindingIndex = FindBindingIndex(jump, "<Keyboard>/space");
                jump.ApplyBindingOverride(bindingIndex, "<Keyboard>/j");

                SettingsData settings = new SettingsData();
                InputBindingPersistence.CaptureIntoSettings(actions.asset, settings);
                Assert.That(settings.BindingOverridesJson, Is.Not.Empty);

                actions.asset.RemoveAllBindingOverrides();
                Assert.That(jump.bindings[bindingIndex].effectivePath, Is.EqualTo("<Keyboard>/space"));

                string error;
                Assert.That(
                    InputBindingPersistence.TryApplyFromSettings(actions.asset, settings, out error),
                    Is.True,
                    error);
                Assert.That(jump.bindings[bindingIndex].effectivePath, Is.EqualTo("<Keyboard>/j"));
            }
            finally
            {
                DestroyInputActionsImmediate(actions);
            }
        }

        [Test]
        public void DuplicateBindingsAreAllowedAndResetAllRestoresDefaults()
        {
            OriginCoreInputActions actions = new OriginCoreInputActions();
            try
            {
                GameObject serviceObject = new GameObject("InputRebindServiceTest");
                serviceObject.SetActive(false);
                try
                {
                    InputRebindService service = serviceObject.AddComponent<InputRebindService>();
                    SettingsData settings = new SettingsData();
                    Assert.That(service.Configure(actions.asset, settings, false), Is.True);

                    string error;
                    Assert.That(service.ApplyBindingOverride(
                        "Global", "SwitchRTS", 0, "<Keyboard>/f4", out error), Is.True, error);
                    Assert.That(service.ApplyBindingOverride(
                        "Global", "SwitchACT", 0, "<Keyboard>/f4", out error), Is.True, error);

                    Assert.That(actions.Global.SwitchRTS.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/f4"));
                    Assert.That(actions.Global.SwitchACT.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/f4"));
                    Assert.That(settings.BindingOverridesJson, Is.Not.Empty);

                    service.ResetAllBindings();
                    Assert.That(actions.Global.SwitchRTS.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/f1"));
                    Assert.That(actions.Global.SwitchACT.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/f2"));
                    Assert.That(settings.BindingOverridesJson, Is.Empty);
                }
                finally
                {
                    Object.DestroyImmediate(serviceObject);
                }
            }
            finally
            {
                DestroyInputActionsImmediate(actions);
            }
        }

        [Test]
        public void GameplayMapSwitchEnablesOneMapAndSuppressesOnlySwitchFrame()
        {
            OriginCoreInputActions actions = new OriginCoreInputActions();
            try
            {
                GameObject routerObject = new GameObject("InputRouterTest");
                routerObject.SetActive(false);
                try
                {
                    InputRouter router = routerObject.AddComponent<InputRouter>();
                    Assert.That(router.Configure(actions.asset, GameplayInputMap.RTS), Is.True);
                    routerObject.SetActive(true);

                    // Plain MonoBehaviour lifecycle callbacks do not run consistently in EditMode.
                    // Reconfiguring while active exercises the same map-enabling branch as OnEnable.
                    Assert.That(router.Configure(actions.asset, GameplayInputMap.RTS), Is.True);

                    Assert.That(actions.Global.Get().enabled, Is.True);
                    Assert.That(actions.UI.Get().enabled, Is.True);
                    Assert.That(actions.RTS.Get().enabled, Is.True);
                    Assert.That(actions.ACT.Get().enabled, Is.False);
                    Assert.That(actions.FPS.Get().enabled, Is.False);

                    int changeCount = 0;
                    router.GameplayMapChanged += _ => changeCount++;
                    Assert.That(router.RequestGameplayMap(GameplayInputMap.ACT, 42), Is.True);
                    Assert.That(router.CurrentSnapshot.GameplaySuppressed, Is.True);
                    Assert.That(router.CurrentSnapshot.Frame, Is.EqualTo(42));
                    Assert.That(actions.RTS.Get().enabled, Is.False);
                    Assert.That(actions.ACT.Get().enabled, Is.True);
                    Assert.That(actions.FPS.Get().enabled, Is.False);
                    Assert.That(changeCount, Is.EqualTo(1));

                    Assert.That(router.RequestGameplayMap(GameplayInputMap.ACT, 42), Is.False);
                    routerObject.SetActive(false);
                    routerObject.SetActive(true);
                    routerObject.SetActive(false);
                    routerObject.SetActive(true);
                    Assert.That(changeCount, Is.EqualTo(1),
                        "Enable/disable must not stack action callbacks or emit mode changes.");
                }
                finally
                {
                    Object.DestroyImmediate(routerObject);
                }
            }
            finally
            {
                DestroyInputActionsImmediate(actions);
            }
        }

        [Test]
        public void MeaningfulDirectInputExcludesLookAndUsesStrictMoveThreshold()
        {
            ActInputSnapshot lookOnly = new ActInputSnapshot(
                Vector2.zero,
                new Vector2(40f, 20f),
                default(InputButtonState),
                default(InputButtonState),
                default(InputButtonState),
                default(InputButtonState),
                default(InputButtonState),
                default(InputButtonState),
                default(InputButtonState),
                default(InputButtonState));
            Assert.That(lookOnly.HasMeaningfulDirectInput(), Is.False);

            ActInputSnapshot exactThreshold = new ActInputSnapshot(
                new Vector2(0.1f, 0f), Vector2.zero,
                default(InputButtonState), default(InputButtonState), default(InputButtonState),
                default(InputButtonState), default(InputButtonState), default(InputButtonState),
                default(InputButtonState), default(InputButtonState));
            Assert.That(exactThreshold.HasMeaningfulDirectInput(), Is.False);

            ActInputSnapshot overThreshold = new ActInputSnapshot(
                new Vector2(0.101f, 0f), Vector2.zero,
                default(InputButtonState), default(InputButtonState), default(InputButtonState),
                default(InputButtonState), default(InputButtonState), default(InputButtonState),
                default(InputButtonState), default(InputButtonState));
            Assert.That(overThreshold.HasMeaningfulDirectInput(), Is.True);

            InputButtonState pressed = new InputButtonState(true, true, false);
            FpsInputSnapshot aimInput = new FpsInputSnapshot(
                Vector2.zero, new Vector2(99f, 99f),
                default(InputButtonState), default(InputButtonState), default(InputButtonState), pressed,
                default(InputButtonState), default(InputButtonState), default(InputButtonState),
                default(InputButtonState), default(InputButtonState), default(InputButtonState));
            Assert.That(aimInput.HasMeaningfulDirectInput(), Is.True);
        }

        [Test]
        public void SuppressionFrameDoesNotLeakIntoFollowingFrame()
        {
            GameplayInputSuppressionFrame suppression = new GameplayInputSuppressionFrame();
            Assert.That(suppression.IsSuppressed(9), Is.False);
            suppression.Arm(9);
            Assert.That(suppression.IsSuppressed(9), Is.True);
            Assert.That(suppression.IsSuppressed(10), Is.False);
            suppression.ClearIfExpired(10);
            Assert.That(suppression.IsArmed, Is.False);
        }

        [Test]
        public void FoundationPrefabAndScenesReferenceSingleInputAsset()
        {
            InputActionAsset actionsAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AppRootPrefabPath);
            Assert.That(actionsAsset, Is.Not.Null, "Run the P04 Apply menu before this test.");
            Assert.That(prefab, Is.Not.Null);

            InputRouter router = prefab.GetComponent<InputRouter>();
            InputRebindService rebindService = prefab.GetComponent<InputRebindService>();
            GameServices services = prefab.GetComponent<GameServices>();
            Assert.That(router, Is.Not.Null);
            Assert.That(rebindService, Is.Not.Null);
            Assert.That(services, Is.Not.Null);
            Assert.That(router.ActionsAsset, Is.SameAs(actionsAsset));
            Assert.That(rebindService.ActionsAsset, Is.SameAs(actionsAsset));
            Assert.That(services.InputRouter, Is.SameAs(router));
            Assert.That(services.InputRebindService, Is.SameAs(rebindService));

            string[] scenePaths =
            {
                "Assets/_OriginCore/Scenes/00_Bootstrap.unity",
                "Assets/_OriginCore/Scenes/01_MainMenu.unity",
                "Assets/_OriginCore/Scenes/90_SystemTest.unity"
            };
            SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                for (int i = 0; i < scenePaths.Length; i++)
                {
                    Scene scene = EditorSceneManager.OpenScene(scenePaths[i], OpenSceneMode.Single);
                    List<EventSystem> eventSystems = FindComponentsInScene<EventSystem>(scene);
                    List<InputSystemUIInputModule> modules = FindComponentsInScene<InputSystemUIInputModule>(scene);
                    Assert.That(eventSystems.Count, Is.EqualTo(1), scene.name);
                    Assert.That(modules.Count, Is.EqualTo(1), scene.name);
                    Assert.That(modules[0].actionsAsset, Is.SameAs(actionsAsset), scene.name);
                    Assert.That(modules[0].move.action.name, Is.EqualTo("Navigate"));
                    Assert.That(modules[0].scrollWheel.action.name, Is.EqualTo("Scroll"));
                }
            }
            finally
            {
                EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
            }
        }

        private static int FindBindingIndex(InputAction action, string path)
        {
            for (int i = 0; i < action.bindings.Count; i++)
            {
                if (action.bindings[i].path == path)
                {
                    return i;
                }
            }

            Assert.Fail(action.name + " is missing binding " + path);
            return -1;
        }

        private static void AssertActionHasPath(InputAction action, string path)
        {
            Assert.That(FindBindingIndex(action, path), Is.GreaterThanOrEqualTo(0));
        }

        private static void DestroyInputActionsImmediate(OriginCoreInputActions actions)
        {
            if (actions == null || actions.asset == null)
            {
                return;
            }

            actions.Disable();
            Object.DestroyImmediate(actions.asset);
        }

        private static List<T> FindComponentsInScene<T>(Scene scene) where T : Component
        {
            List<T> result = new List<T>();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                result.AddRange(roots[i].GetComponentsInChildren<T>(true));
            }

            return result;
        }
    }
}
