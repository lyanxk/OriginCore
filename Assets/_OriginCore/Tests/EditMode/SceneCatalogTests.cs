using System.Collections.Generic;
using NUnit.Framework;
using OriginCore.SceneFlow;
using UnityEditor;
using UnityEngine.SceneManagement;

namespace OriginCore.Tests.EditMode
{
    public sealed class SceneCatalogTests
    {
        private const string CatalogPath = "Assets/_OriginCore/Data/Configs/SO_SceneCatalog.asset";
        private const string MainMenuPath = "Assets/_OriginCore/Scenes/01_MainMenu.unity";
        private const string SystemTestPath = "Assets/_OriginCore/Scenes/90_SystemTest.unity";

        [Test]
        public void CatalogMapsRequiredSceneKeys()
        {
            SceneCatalog catalog = AssetDatabase.LoadAssetAtPath<SceneCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null, "Run the P02 Apply menu before executing the tests.");

            string mainMenuPath;
            string systemTestPath;
            Assert.That(catalog.TryGetScenePath(SceneCatalog.MainMenuSceneKey, out mainMenuPath), Is.True);
            Assert.That(catalog.TryGetScenePath(SceneCatalog.SystemTestSceneKey, out systemTestPath), Is.True);
            Assert.That(mainMenuPath, Is.EqualTo(MainMenuPath));
            Assert.That(systemTestPath, Is.EqualTo(SystemTestPath));

            List<string> issues = new List<string>();
            catalog.CollectValidationIssues(issues);
            CollectionAssert.IsEmpty(issues);
        }

        [TestCase("Assets/_OriginCore/Scenes/01_MainMenu.unity", "Assets/_OriginCore/Scenes/01_MainMenu")]
        [TestCase("Assets/_OriginCore/Scenes/01_MainMenu.UNITY", "Assets/_OriginCore/Scenes/01_MainMenu")]
        [TestCase("01_MainMenu", "01_MainMenu")]
        public void RuntimeLoadPathRemovesOnlyUnityExtension(string input, string expected)
        {
            Assert.That(SceneCatalog.ToRuntimeLoadPath(input), Is.EqualTo(expected));
        }

        [TestCase(MainMenuPath, 1)]
        [TestCase(SystemTestPath, 2)]
        public void CatalogScenePathsResolveToExpectedBuildIndices(string scenePath, int expectedBuildIndex)
        {
            Assert.That(SceneUtility.GetBuildIndexByScenePath(scenePath), Is.EqualTo(expectedBuildIndex));
        }
    }
}
