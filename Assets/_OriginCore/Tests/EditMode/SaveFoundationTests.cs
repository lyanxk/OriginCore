using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using OriginCore.Core;
using OriginCore.Gameplay;
using OriginCore.Save;
using OriginCore.UI.MainMenu;
using OriginCore.UI.Pause;
using OriginCore.UI.Save;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace OriginCore.Tests.EditMode
{
    public sealed class SaveFoundationTests
    {
        private const string AppRootPrefabPath =
            "Assets/_OriginCore/Prefabs/Core/PF_AppRoot.prefab";
        private const string SavePanelPrefabPath =
            "Assets/_OriginCore/Prefabs/UI/PF_LoadGamePanel.prefab";
        private const string MainMenuPrefabPath =
            "Assets/_OriginCore/Prefabs/UI/PF_MainMenu.prefab";
        private const string PauseMenuPrefabPath =
            "Assets/_OriginCore/Prefabs/UI/PF_PauseMenu.prefab";
        private const string SystemTestScenePath =
            "Assets/_OriginCore/Scenes/90_SystemTest.unity";

        [Test]
        public void VersionedSaveRoundTripsAsUtf8AndRenameOnlyChangesDisplayName()
        {
            string directory = CreateTemporaryDirectory("roundtrip_存档");
            try
            {
                SaveRepository repository = new SaveRepository(directory);
                DateTime savedAt = new DateTime(2026, 7, 14, 3, 4, 5, DateTimeKind.Utc);
                SaveGameData source = SaveGameData.Create(
                    "slot_roundtrip",
                    "第一章 ✓",
                    "system_test_0",
                    savedAt);
                source.SavedGameMode = GameMode.ACT;
                source.resources.commanderResource = 321;
                source.resources.crystal = 123;
                source.resources.influenceUsed = 3;
                source.resources.influenceCap = 20;
                source.entities.Add(new EntitySaveData
                {
                    runtimeId = "scene.system_test_0.hero",
                    archetypeId = "unit.hero",
                    position = new Vector3(1f, 2f, 3f),
                    rotation = Quaternion.Euler(0f, 45f, 0f),
                    health = 73f,
                    maxHealth = 100f,
                    hasVitals = true,
                    faction = (int)FactionId.Friendly
                });

                Assert.That(repository.TryWrite(source, out string writeError), Is.True, writeError);
                string path = Path.Combine(directory, "slot_roundtrip.json");
                string json = File.ReadAllText(path);
                Assert.That(json, Does.Contain("\"schemaVersion\": 2"));
                Assert.That(json, Does.Contain("第一章"));
                Assert.That(File.Exists(path + ".tmp"), Is.False);
                Assert.That(File.Exists(path + ".bak"), Is.False);

                Assert.That(repository.TryRead(
                    "slot_roundtrip",
                    out SaveGameData loaded,
                    out string readError), Is.True, readError);
                Assert.That(loaded.schemaVersion, Is.EqualTo(SaveGameData.CurrentSchemaVersion));
                Assert.That(loaded.displayName, Is.EqualTo("第一章 ✓"));
                Assert.That(loaded.SavedGameMode, Is.EqualTo(GameMode.ACT));
                Assert.That(loaded.resources.commanderResource, Is.EqualTo(321));
                Assert.That(loaded.entities, Has.Count.EqualTo(1));

                string originalTimestamp = loaded.savedAtUtc;
                Vector3 originalPosition = loaded.entities[0].position;
                Assert.That(repository.TryRename(
                    "slot_roundtrip",
                    "重命名槽位",
                    out string renameError), Is.True, renameError);
                Assert.That(repository.TryRead(
                    "slot_roundtrip",
                    out SaveGameData renamed,
                    out readError), Is.True, readError);
                Assert.That(renamed.slotId, Is.EqualTo("slot_roundtrip"));
                Assert.That(renamed.displayName, Is.EqualTo("重命名槽位"));
                Assert.That(renamed.savedAtUtc, Is.EqualTo(originalTimestamp));
                Assert.That(renamed.sceneKey, Is.EqualTo("system_test_0"));
                Assert.That(renamed.SavedGameMode, Is.EqualTo(GameMode.ACT));
                Assert.That(renamed.resources.commanderResource, Is.EqualTo(321));
                Assert.That(renamed.entities[0].position, Is.EqualTo(originalPosition));
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        [Test]
        public void UnknownJsonFieldsAreIgnoredWhileKnownSaveStateIsPreserved()
        {
            string directory = CreateTemporaryDirectory("forward_compatibility");
            try
            {
                SaveRepository repository = new SaveRepository(directory);
                SaveGameData source = SaveGameData.Create(
                    "slot_future_fields",
                    "KNOWN DATA",
                    "system_test_0",
                    new DateTime(2026, 7, 14, 6, 0, 0, DateTimeKind.Utc));
                source.resources.commanderResource = 444;
                source.resources.crystal = 222;
                Assert.That(repository.TryWrite(source, out string error), Is.True, error);

                string path = Path.Combine(directory, "slot_future_fields.json");
                string json = File.ReadAllText(path);
                int rootStart = json.IndexOf('{');
                Assert.That(rootStart, Is.GreaterThanOrEqualTo(0));
                string withUnknownFields = json.Insert(
                    rootStart + 1,
                    "\n  \"futureRoot\": { \"revision\": 7 }," +
                    "\n  \"futureArray\": [1, 2, 3],");
                File.WriteAllText(path, withUnknownFields);

                Assert.That(repository.TryRead(
                    "slot_future_fields",
                    out SaveGameData loaded,
                    out error), Is.True, error);
                Assert.That(loaded.schemaVersion, Is.EqualTo(SaveGameData.CurrentSchemaVersion));
                Assert.That(loaded.slotId, Is.EqualTo("slot_future_fields"));
                Assert.That(loaded.displayName, Is.EqualTo("KNOWN DATA"));
                Assert.That(loaded.sceneKey, Is.EqualTo("system_test_0"));
                Assert.That(loaded.resources.commanderResource, Is.EqualTo(444));
                Assert.That(loaded.resources.crystal, Is.EqualTo(222));
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        [Test]
        public void SlotsSortNewestFirstWhileCorruptAndUnknownVersionsRemainDeletable()
        {
            string directory = CreateTemporaryDirectory("metadata");
            try
            {
                SaveRepository repository = new SaveRepository(directory);
                SaveGameData oldSave = SaveGameData.Create(
                    "slot_old", "OLD", "system_test_0",
                    new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
                SaveGameData newSave = SaveGameData.Create(
                    "slot_new", "NEW", "system_test_0",
                    new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));
                Assert.That(repository.TryWrite(oldSave, out string error), Is.True, error);
                Assert.That(repository.TryWrite(newSave, out error), Is.True, error);

                File.WriteAllText(Path.Combine(directory, "slot_corrupt.json"), "{not-json");
                SaveGameData unknown = SaveGameData.Create(
                    "slot_unknown", "FUTURE", "system_test_0",
                    new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
                unknown.schemaVersion = 99;
                File.WriteAllText(
                    Path.Combine(directory, "slot_unknown.json"),
                    JsonUtility.ToJson(unknown, true));

                IReadOnlyList<SaveSlotMetadata> slots = repository.EnumerateSlots();
                Assert.That(slots, Has.Count.EqualTo(4));
                Assert.That(slots[0].SlotId, Is.EqualTo("slot_new"));
                Assert.That(slots[1].SlotId, Is.EqualTo("slot_old"));
                SaveSlotMetadata corrupt = Find(slots, "slot_corrupt");
                SaveSlotMetadata future = Find(slots, "slot_unknown");
                Assert.That(corrupt.IsReadable, Is.False);
                Assert.That(future.IsReadable, Is.False);
                Assert.That(future.SchemaVersion, Is.EqualTo(99));
                Assert.That(future.Error, Does.Contain("Unsupported save schema version"));

                Assert.That(repository.TryDelete("slot_corrupt", out error), Is.True, error);
                Assert.That(repository.TryDelete("slot_unknown", out error), Is.True, error);
                Assert.That(repository.EnumerateSlots(), Has.Count.EqualTo(2));
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        [Test]
        public void FailedAtomicReplacementLeavesThePreviousValidSaveUntouched()
        {
            string directory = CreateTemporaryDirectory("atomic");
            try
            {
                SaveRepository repository = new SaveRepository(directory);
                SaveGameData original = SaveGameData.Create(
                    "slot_atomic", "ORIGINAL", "system_test_0", DateTime.UtcNow);
                original.resources.commanderResource = 100;
                Assert.That(repository.TryWrite(original, out string error), Is.True, error);

                string path = Path.Combine(directory, "slot_atomic.json");
                SaveGameData replacement = SaveGameData.Create(
                    "slot_atomic", "REPLACEMENT", "system_test_0", DateTime.UtcNow);
                replacement.resources.commanderResource = 999;
                using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    Assert.That(repository.TryWrite(replacement, out error), Is.False);
                }

                Assert.That(repository.TryRead(
                    "slot_atomic",
                    out SaveGameData preserved,
                    out error), Is.True, error);
                Assert.That(preserved.displayName, Is.EqualTo("ORIGINAL"));
                Assert.That(preserved.resources.commanderResource, Is.EqualTo(100));
                Assert.That(File.Exists(path + ".tmp"), Is.False);
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        [Test]
        public void P14PrefabsAndStaticSceneIdsAreCompletelyWired()
        {
            GameObject appRoot = AssetDatabase.LoadAssetAtPath<GameObject>(AppRootPrefabPath);
            GameServices services = appRoot != null ? appRoot.GetComponent<GameServices>() : null;
            SaveService saveService = appRoot != null ? appRoot.GetComponent<SaveService>() : null;
            Assert.That(appRoot, Is.Not.Null,
                "Ensure the save/load scene assets are configured before running this test.");
            Assert.That(saveService, Is.Not.Null);
            Assert.That(services.SaveService, Is.SameAs(saveService));

            GameObject panelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SavePanelPrefabPath);
            LoadGamePanel panel = panelPrefab != null
                ? panelPrefab.GetComponent<LoadGamePanel>()
                : null;
            Assert.That(panel, Is.Not.Null);
            Assert.That(panel.RowTemplate, Is.Not.Null);
            Assert.That(panel.ConfirmDialog, Is.Not.Null);
            Assert.That(panel.SaveNameDialog, Is.Not.Null);

            GameObject mainPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MainMenuPrefabPath);
            GameObject pausePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PauseMenuPrefabPath);
            MainMenuController main = mainPrefab.GetComponent<MainMenuController>();
            PauseMenuController pause = pausePrefab.GetComponent<PauseMenuController>();
            Assert.That(main.LoadGamePanel, Is.Not.Null);
            Assert.That(pause.LoadGamePanel, Is.Not.Null);
            Assert.That(mainPrefab.GetComponentsInChildren<LoadGamePanel>(true), Has.Length.EqualTo(1));
            Assert.That(pausePrefab.GetComponentsInChildren<LoadGamePanel>(true), Has.Length.EqualTo(1));

            SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                Scene scene = EditorSceneManager.OpenScene(
                    SystemTestScenePath,
                    OpenSceneMode.Single);
                EntityIdentity[] identities = FindEntityIdentitiesInScene(scene);
                HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
                Assert.That(identities.Length, Is.GreaterThan(0));
                for (int i = 0; i < identities.Length; i++)
                {
                    Assert.That(
                        identities[i].RuntimeId,
                        Does.StartWith("scene.system_test_0."),
                        identities[i].name);
                    Assert.That(identities[i].IsRuntimeSpawned, Is.False, identities[i].name);
                    Assert.That(ids.Add(identities[i].RuntimeId), Is.True, identities[i].RuntimeId);
                }
            }
            finally
            {
                EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
            }
        }

        private static SaveSlotMetadata Find(
            IReadOnlyList<SaveSlotMetadata> slots,
            string slotId)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].SlotId == slotId)
                {
                    return slots[i];
                }
            }

            Assert.Fail("Slot not found: " + slotId);
            return null;
        }

        private static EntityIdentity[] FindEntityIdentitiesInScene(Scene scene)
        {
            List<EntityIdentity> result = new List<EntityIdentity>();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                result.AddRange(roots[i].GetComponentsInChildren<EntityIdentity>(true));
            }

            return result.ToArray();
        }

        private static string CreateTemporaryDirectory(string suffix)
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "OriginCore_P14_" + suffix + "_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return directory;
        }

        private static void DeleteDirectory(string directory)
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }
}
