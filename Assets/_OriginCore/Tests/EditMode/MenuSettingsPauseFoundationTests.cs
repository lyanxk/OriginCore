using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using OriginCore.Core;
using OriginCore.Settings;
using OriginCore.UI;
using OriginCore.UI.Common;
using OriginCore.UI.MainMenu;
using OriginCore.UI.Pause;
using OriginCore.UI.Settings;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using RuntimeSettingsService = OriginCore.Settings.SettingsService;

namespace OriginCore.Tests.EditMode
{
    public sealed class MenuSettingsPauseFoundationTests
    {
        private const string AppRootPrefabPath =
            "Assets/_OriginCore/Prefabs/Core/PF_AppRoot.prefab";
        private const string MainMenuPrefabPath =
            "Assets/_OriginCore/Prefabs/UI/PF_MainMenu.prefab";
        private const string OptionsPrefabPath =
            "Assets/_OriginCore/Prefabs/UI/PF_Options.prefab";
        private const string PauseMenuPrefabPath =
            "Assets/_OriginCore/Prefabs/UI/PF_PauseMenu.prefab";
        private const string HudRootPrefabPath =
            "Assets/_OriginCore/Prefabs/UI/PF_HUDRoot.prefab";
        private const string MainMenuScenePath =
            "Assets/_OriginCore/Scenes/01_MainMenu.unity";
        private const string SystemTestScenePath =
            "Assets/_OriginCore/Scenes/90_SystemTest.unity";

        [Test]
        public void PageAndModalStacksKeepOnlyTheirTopEntryVisible()
        {
            GameObject owner = new GameObject("StackTest");
            GameObject first = new GameObject("First");
            GameObject second = new GameObject("Second");
            GameObject confirm = new GameObject("Confirm");
            first.transform.SetParent(owner.transform);
            second.transform.SetParent(owner.transform);
            confirm.transform.SetParent(owner.transform);
            try
            {
                PageStack pages = owner.AddComponent<PageStack>();
                pages.Configure("First", new[] { "First", "Second" },
                    new[] { first, second });
                Assert.That(pages.CurrentPageId, Is.EqualTo("First"));
                Assert.That(first.activeSelf, Is.True);
                Assert.That(second.activeSelf, Is.False);
                Assert.That(pages.Push("Second"), Is.True);
                Assert.That(first.activeSelf, Is.False);
                Assert.That(second.activeSelf, Is.True);
                Assert.That(pages.Pop(), Is.True);
                Assert.That(first.activeSelf, Is.True);

                ModalStack modals = owner.AddComponent<ModalStack>();
                modals.Configure(new[] { "Confirm" }, new[] { confirm });
                Assert.That(confirm.activeSelf, Is.False);
                Assert.That(modals.Show("Confirm"), Is.True);
                Assert.That(confirm.activeSelf, Is.True);
                Assert.That(modals.CloseTop(), Is.True);
                Assert.That(confirm.activeSelf, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void SettingsNormalizeAndRoundTripAsUtf8AtomicJson()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "OriginCore_P13_设置_" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(directory, "settings.json");
            try
            {
                SettingsData source = SettingsData.CreateDefaults(2560, 1440);
                source.WindowMode = FullScreenMode.Windowed;
                source.Brightness = 9f;
                source.MasterVolume = -3f;
                source.CrosshairColor = new Color(0.2f, 0.4f, 0.8f, 0.2f);
                source.BindingOverridesJson = "{\"note\":\"输入✓\"}";
                SettingsRepository repository = new SettingsRepository(path);

                Assert.That(repository.TrySave(source, out string saveError), Is.True, saveError);
                Assert.That(File.Exists(path + ".tmp"), Is.False);
                Assert.That(repository.TryLoad(out SettingsData loaded, out string loadError),
                    Is.True, loadError);
                Assert.That(loaded, Is.Not.Null);
                Assert.That(loaded.SchemaVersion, Is.EqualTo(SettingsData.CurrentSchemaVersion));
                Assert.That(loaded.ScreenWidth, Is.EqualTo(2560));
                Assert.That(loaded.ScreenHeight, Is.EqualTo(1440));
                Assert.That(loaded.WindowMode, Is.EqualTo(FullScreenMode.Windowed));
                Assert.That(loaded.Brightness, Is.EqualTo(1.5f));
                Assert.That(loaded.MasterVolume, Is.Zero);
                Assert.That(loaded.CrosshairColor.a, Is.EqualTo(1f));
                Assert.That(loaded.BindingOverridesJson, Does.Contain("输入"));
                Assert.That(File.ReadAllText(path), Does.Contain("输入"));
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        [Test]
        public void MasterVolumeMapsZeroToMuteFloorAndOneToUnityGain()
        {
            Assert.That(RuntimeSettingsService.MasterVolumeToDecibels(0f), Is.EqualTo(-80f));
            Assert.That(RuntimeSettingsService.MasterVolumeToDecibels(1f), Is.EqualTo(0f).Within(0.001f));
            Assert.That(RuntimeSettingsService.MasterVolumeToDecibels(0.5f),
                Is.EqualTo(-6.0206f).Within(0.001f));
        }

        [Test]
        public void P13PrefabsAndPersistentServicesAreCompletelyWired()
        {
            GameObject appRoot = AssetDatabase.LoadAssetAtPath<GameObject>(AppRootPrefabPath);
            Assert.That(appRoot, Is.Not.Null,
                "Ensure the menu, settings, pause and HUD assets are configured before running this test.");
            GameServices services = appRoot.GetComponent<GameServices>();
            RuntimeSettingsService settings = appRoot.GetComponent<RuntimeSettingsService>();
            PauseService pause = appRoot.GetComponent<PauseService>();
            BrightnessImageEffect brightness = appRoot.GetComponent<BrightnessImageEffect>();
            Assert.That(services, Is.Not.Null);
            Assert.That(settings, Is.Not.Null);
            Assert.That(pause, Is.Not.Null);
            Assert.That(brightness, Is.Not.Null);
            Assert.That(services.SettingsService, Is.SameAs(settings));
            Assert.That(services.PauseService, Is.SameAs(pause));
            Assert.That(settings.AudioMixer, Is.Not.Null);
            Assert.That(settings.BrightnessEffect, Is.SameAs(brightness));
            Assert.That(appRoot.GetComponent<Volume>(), Is.Not.Null);
            Assert.That(appRoot.GetComponent<Volume>().sharedProfile, Is.Not.Null);

            GameObject main = AssetDatabase.LoadAssetAtPath<GameObject>(MainMenuPrefabPath);
            GameObject options = AssetDatabase.LoadAssetAtPath<GameObject>(OptionsPrefabPath);
            GameObject pausePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PauseMenuPrefabPath);
            GameObject hud = AssetDatabase.LoadAssetAtPath<GameObject>(HudRootPrefabPath);
            Assert.That(main.GetComponent<MainMenuController>(), Is.Not.Null);
            Assert.That(main.GetComponent<PageStack>(), Is.Not.Null);
            Assert.That(main.GetComponentInChildren<ModalStack>(true), Is.Not.Null);
            Assert.That(options.GetComponent<OptionsView>(), Is.Not.Null);
            Assert.That(options.GetComponentInChildren<InputBindingsView>(true), Is.Not.Null);
            Assert.That(pausePrefab.GetComponent<PauseMenuController>(), Is.Not.Null);
            Assert.That(hud.GetComponent<DirectControlHudView>(), Is.Not.Null);
        }

        [Test]
        public void FoundationScenesOwnExactlyOneP13MenuOrGameplayUiFlow()
        {
            SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                Scene main = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
                Assert.That(FindComponentsInScene<MainMenuController>(main).Count, Is.EqualTo(1));
                Assert.That(FindComponentsInScene<PauseMenuController>(main).Count, Is.Zero);

                Scene system = EditorSceneManager.OpenScene(
                    SystemTestScenePath,
                    OpenSceneMode.Single);
                Assert.That(FindComponentsInScene<PauseMenuController>(system).Count, Is.EqualTo(1));
                Assert.That(FindComponentsInScene<DirectControlHudView>(system).Count, Is.EqualTo(1));
            }
            finally
            {
                EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
            }
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
