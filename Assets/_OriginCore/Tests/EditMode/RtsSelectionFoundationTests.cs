using System.Collections.Generic;
using Cinemachine;
using NUnit.Framework;
using OriginCore.RTS;
using OriginCore.SceneFlow;
using OriginCore.UI.RTS;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OriginCore.Tests.EditMode
{
    public sealed class RtsSelectionFoundationTests
    {
        private const string SystemTestScenePath =
            "Assets/_OriginCore/Scenes/90_SystemTest.unity";

        [Test]
        public void SelectionBoxCalculatorNormalizesDirectionAndThreshold()
        {
            Rect rect = SelectionBoxCalculator.FromScreenPoints(
                new Vector2(640f, 480f),
                new Vector2(120f, 80f));
            Assert.That(rect.xMin, Is.EqualTo(120f));
            Assert.That(rect.yMin, Is.EqualTo(80f));
            Assert.That(rect.xMax, Is.EqualTo(640f));
            Assert.That(rect.yMax, Is.EqualTo(480f));
            Assert.That(
                SelectionBoxCalculator.ExceedsDragThreshold(
                    Vector2.zero,
                    new Vector2(6f, 8f),
                    10f),
                Is.True);
            Assert.That(
                SelectionBoxCalculator.ExceedsDragThreshold(
                    Vector2.zero,
                    new Vector2(5f, 5f),
                    10f),
                Is.False);
        }

        [Test]
        public void RtsBoundsClampOnlyHorizontalAxesWithPadding()
        {
            GameObject root = new GameObject("RtsBoundsTest");
            try
            {
                BoxCollider source = root.AddComponent<BoxCollider>();
                source.center = new Vector3(0f, 2.5f, 0f);
                source.size = new Vector3(50f, 5f, 50f);
                RtsCameraBounds bounds = root.AddComponent<RtsCameraBounds>();
                bounds.Configure(source, 1f);

                Vector3 clamped = bounds.ClampPosition(new Vector3(100f, 7f, -100f));
                Assert.That(clamped, Is.EqualTo(new Vector3(24f, 7f, -24f)));
                Assert.That(bounds.ContainsHorizontal(new Vector3(0f, -20f, 0f)), Is.True);
                Assert.That(bounds.ContainsHorizontal(new Vector3(30f, 0f, 0f)), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RtsCameraUsesMaximumTiltByDefaultAndHonorsFocusAndUiGates()
        {
            GameObject rigObject = new GameObject("RtsCameraRigTest");
            GameObject outputObject = new GameObject("OutputCameraTest");
            GameObject virtualObject = new GameObject("VirtualCameraTest");
            GameObject boundsObject = new GameObject("BoundsTest");
            try
            {
                Camera outputCamera = outputObject.AddComponent<Camera>();
                outputObject.transform.rotation = Quaternion.Euler(45f, 0f, 0f);
                CinemachineVirtualCamera virtualCamera =
                    virtualObject.AddComponent<CinemachineVirtualCamera>();
                CinemachineTransposer transposer =
                    virtualCamera.AddCinemachineComponent<CinemachineTransposer>();
                BoxCollider source = boundsObject.AddComponent<BoxCollider>();
                source.size = new Vector3(50f, 5f, 50f);
                RtsCameraBounds bounds = boundsObject.AddComponent<RtsCameraBounds>();
                bounds.Configure(source, 1f);

                RtsCameraController controller =
                    rigObject.AddComponent<RtsCameraController>();
                controller.Configure(
                    rigObject.transform,
                    outputCamera,
                    virtualCamera,
                    bounds,
                    initialZoomNormalized: 1f);

                Assert.That(controller.ZoomNormalized, Is.EqualTo(1f));
                Assert.That(controller.CurrentHeight, Is.EqualTo(controller.MaximumHeight));
                Assert.That(controller.CurrentTilt, Is.EqualTo(controller.MaximumTilt));
                Assert.That(transposer.m_FollowOffset.y, Is.EqualTo(controller.MaximumHeight));

                Vector3 initialPosition = rigObject.transform.position;
                Assert.That(controller.ProcessInput(
                    CreateRtsInput(new Vector2(1919f, 540f), 0f),
                    1f,
                    new Vector2(1920f, 1080f),
                    false,
                    false), Is.False);
                Assert.That(rigObject.transform.position, Is.EqualTo(initialPosition));

                Assert.That(controller.ProcessInput(
                    CreateRtsInput(new Vector2(1919f, 540f), 0f),
                    1f,
                    new Vector2(1920f, 1080f),
                    true,
                    true), Is.False);
                Assert.That(rigObject.transform.position, Is.EqualTo(initialPosition));

                Assert.That(controller.ProcessInput(
                    CreateRtsInput(new Vector2(1919f, 540f), 0f),
                    1f,
                    new Vector2(1920f, 1080f),
                    true,
                    false), Is.True);
                Assert.That(rigObject.transform.position.x, Is.EqualTo(24f).Within(0.001f));
                Assert.That(bounds.ContainsHorizontal(rigObject.transform.position), Is.True);

                float previousZoom = controller.ZoomNormalized;
                Assert.That(controller.ProcessInput(
                    CreateRtsInput(new Vector2(960f, 540f), 120f),
                    0.016f,
                    new Vector2(1920f, 1080f),
                    true,
                    false), Is.True);
                Assert.That(controller.ZoomNormalized, Is.LessThan(previousZoom));
                Assert.That(controller.CurrentHeight, Is.LessThan(controller.MaximumHeight));
            }
            finally
            {
                Object.DestroyImmediate(rigObject);
                Object.DestroyImmediate(outputObject);
                Object.DestroyImmediate(virtualObject);
                Object.DestroyImmediate(boundsObject);
            }
        }

        [Test]
        public void SystemTestSerializesOneCompleteP07Context()
        {
            SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                Scene scene = EditorSceneManager.OpenScene(SystemTestScenePath, OpenSceneMode.Single);
                List<SceneContext> contexts = FindComponentsInScene<SceneContext>(scene);
                Assert.That(contexts.Count, Is.EqualTo(1),
                    "Ensure the RTS selection scene assets are configured before running this test.");
                SceneContext context = contexts[0];
                Assert.That(context.RtsCameraController, Is.Not.Null);
                Assert.That(context.SelectionService, Is.Not.Null);
                Assert.That(context.SelectionBoxView, Is.Not.Null);
                Assert.That(context.SelectionSummaryView, Is.Not.Null);
                Assert.That(context.SelectionService.WorldCamera, Is.SameAs(context.MainCamera));
                Assert.That(context.SelectionService.SelectionBoxView,
                    Is.SameAs(context.SelectionBoxView));
                Assert.That(context.SelectionSummaryView.SelectionService,
                    Is.SameAs(context.SelectionService));
                Assert.That(context.SelectionSummaryView.ModeHudPresenter,
                    Is.SameAs(context.ModeHudPresenter));
                Assert.That(context.SelectionBoxView.IsVisible, Is.False);
            }
            finally
            {
                EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
            }
        }

        private static OriginCore.Input.RtsInputSnapshot CreateRtsInput(
            Vector2 point,
            float zoom)
        {
            return new OriginCore.Input.RtsInputSnapshot(
                point,
                default(OriginCore.Input.InputButtonState),
                default(OriginCore.Input.InputButtonState),
                default(OriginCore.Input.InputButtonState),
                default(OriginCore.Input.InputButtonState),
                default(OriginCore.Input.InputButtonState),
                default(OriginCore.Input.InputButtonState),
                default(OriginCore.Input.InputButtonState),
                zoom);
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
