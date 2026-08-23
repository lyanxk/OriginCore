using System.Collections.Generic;
using Cinemachine;
using NUnit.Framework;
using OriginCore.Cameras;
using OriginCore.Core;
using OriginCore.Gameplay;
using OriginCore.Input;
using OriginCore.SceneFlow;
using OriginCore.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace OriginCore.Tests.EditMode
{
    public sealed class ModeFoundationTests
    {
        private const string AppRootPrefabPath =
            "Assets/_OriginCore/Prefabs/Core/PF_AppRoot.prefab";
        private const string HeroPrefabPath =
            "Assets/_OriginCore/Prefabs/Units/PF_HeroPlaceholder.prefab";
        private const string SystemTestScenePath =
            "Assets/_OriginCore/Scenes/90_SystemTest.unity";

        [Test]
        public void GameModeMappingsAndCursorPolicyMatchTheThreeModeContract()
        {
            Assert.That(GameMode.RTS.ToGameplayInputMap(), Is.EqualTo(GameplayInputMap.RTS));
            Assert.That(GameMode.ACT.ToGameplayInputMap(), Is.EqualTo(GameplayInputMap.ACT));
            Assert.That(GameMode.FPS.ToGameplayInputMap(), Is.EqualTo(GameplayInputMap.FPS));
            Assert.That(GameplayInputMap.RTS.ToGameMode(), Is.EqualTo(GameMode.RTS));
            Assert.That(GameplayInputMap.ACT.ToGameMode(), Is.EqualTo(GameMode.ACT));
            Assert.That(GameplayInputMap.FPS.ToGameMode(), Is.EqualTo(GameMode.FPS));

            CursorPolicyState rts = CursorPolicy.Resolve(GameMode.RTS, false);
            CursorPolicyState act = CursorPolicy.Resolve(GameMode.ACT, false);
            CursorPolicyState fps = CursorPolicy.Resolve(GameMode.FPS, false);
            CursorPolicyState paused = CursorPolicy.Resolve(GameMode.FPS, true);
            Assert.That(rts.Visible, Is.True);
            Assert.That(rts.LockState, Is.EqualTo(CursorLockMode.None));
            Assert.That(act.Visible, Is.False);
            Assert.That(act.LockState, Is.EqualTo(CursorLockMode.Locked));
            Assert.That(fps.Visible, Is.False);
            Assert.That(fps.LockState, Is.EqualTo(CursorLockMode.Locked));
            Assert.That(paused.Visible, Is.True);
            Assert.That(paused.LockState, Is.EqualTo(CursorLockMode.None));
        }

        [Test]
        public void PendingManualOverrideIgnoresLookAndActivatesExactlyOnce()
        {
            GameObject pawnObject = new GameObject("P06_Pawn_UnitTest");
            GameObject serviceObject = new GameObject("P06_Possession_UnitTest");
            pawnObject.SetActive(false);
            serviceObject.SetActive(false);
            try
            {
                CharacterController characterController =
                    pawnObject.AddComponent<CharacterController>();
                NavMeshAgent agent = pawnObject.AddComponent<NavMeshAgent>();
                HybridControlDriver driver = pawnObject.AddComponent<HybridControlDriver>();
                driver.Configure(
                    agent,
                    characterController,
                    HybridControlState.Autopilot,
                    2f);

                PossessionService possession = serviceObject.AddComponent<PossessionService>();
                possession.Configure(0.1f);
                possession.BindPawn(driver);
                int cancellationEvents = 0;
                int activationEvents = 0;
                possession.AutopilotCancellationRequested += (_, __) => cancellationEvents++;
                possession.ManualOverrideActivated += (_, __) => activationEvents++;

                Assert.That(possession.PrepareDirectObservation(out string error), Is.True, error);
                Assert.That(driver.State, Is.EqualTo(HybridControlState.PendingManualOverride));
                Assert.That(driver.IsAutopilotActive, Is.True);

                InputSnapshot lookOnly = InputSnapshot.FromAct(
                    20,
                    CreateActSnapshot(Vector2.zero, new Vector2(400f, -250f)));
                Assert.That(possession.TryHandleDirectInput(lookOnly), Is.False);
                Assert.That(driver.State, Is.EqualTo(HybridControlState.PendingManualOverride));
                Assert.That(cancellationEvents, Is.Zero);
                Assert.That(activationEvents, Is.Zero);

                InputSnapshot movement = InputSnapshot.FromAct(
                    21,
                    CreateActSnapshot(new Vector2(0f, 0.5f), Vector2.zero));
                Assert.That(possession.TryHandleDirectInput(movement), Is.True);
                Assert.That(driver.State, Is.EqualTo(HybridControlState.Manual));
                Assert.That(agent.enabled, Is.False);
                Assert.That(characterController.enabled, Is.True);
                Assert.That(possession.ManualOverrideCount, Is.EqualTo(1));
                Assert.That(possession.LastManualOverrideFrame, Is.EqualTo(21));
                Assert.That(cancellationEvents, Is.EqualTo(1));
                Assert.That(activationEvents, Is.EqualTo(1));

                Assert.That(possession.TryHandleDirectInput(movement), Is.False);
                Assert.That(possession.ManualOverrideCount, Is.EqualTo(1));
                Assert.That(cancellationEvents, Is.EqualTo(1));
                Assert.That(activationEvents, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(serviceObject);
                Object.DestroyImmediate(pawnObject);
            }
        }

        [Test]
        public void ReturningFromPendingRestoresAutopilotWithoutRequiringANavMeshSnap()
        {
            GameObject pawnObject = new GameObject("P06_PendingReturn_UnitTest");
            GameObject serviceObject = new GameObject("P06_PendingReturn_ServiceTest");
            pawnObject.SetActive(false);
            serviceObject.SetActive(false);
            try
            {
                CharacterController characterController =
                    pawnObject.AddComponent<CharacterController>();
                NavMeshAgent agent = pawnObject.AddComponent<NavMeshAgent>();
                HybridControlDriver driver = pawnObject.AddComponent<HybridControlDriver>();
                driver.Configure(
                    agent,
                    characterController,
                    HybridControlState.Autopilot,
                    2f);
                PossessionService possession = serviceObject.AddComponent<PossessionService>();
                possession.BindPawn(driver);

                Assert.That(possession.PrepareDirectObservation(out string prepareError),
                    Is.True, prepareError);
                Assert.That(driver.State, Is.EqualTo(HybridControlState.PendingManualOverride));
                Assert.That(possession.TryReturnToRts(out string returnError),
                    Is.True, returnError);
                Assert.That(driver.State, Is.EqualTo(HybridControlState.Autopilot));
                Assert.That(agent.enabled, Is.True);
                Assert.That(characterController.enabled, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(serviceObject);
                Object.DestroyImmediate(pawnObject);
            }
        }

        [Test]
        public void P06AssetsBindServicesPawnCamerasAndModeHud()
        {
            GameObject appRoot = AssetDatabase.LoadAssetAtPath<GameObject>(AppRootPrefabPath);
            Assert.That(appRoot, Is.Not.Null, "Run the P06 Apply menu before this test.");
            GameServices services = appRoot.GetComponent<GameServices>();
            GameModeController modeController = appRoot.GetComponent<GameModeController>();
            PossessionService possession = appRoot.GetComponent<PossessionService>();
            CursorPolicy cursor = appRoot.GetComponent<CursorPolicy>();
            Assert.That(services, Is.Not.Null);
            Assert.That(modeController, Is.Not.Null);
            Assert.That(possession, Is.Not.Null);
            Assert.That(cursor, Is.Not.Null);
            Assert.That(services.GameModeController, Is.SameAs(modeController));
            Assert.That(services.PossessionService, Is.SameAs(possession));
            Assert.That(services.CursorPolicy, Is.SameAs(cursor));

            GameObject hero = AssetDatabase.LoadAssetAtPath<GameObject>(HeroPrefabPath);
            Assert.That(hero, Is.Not.Null);
            HybridControlDriver heroDriver = hero.GetComponent<HybridControlDriver>();
            Assert.That(heroDriver, Is.Not.Null);
            Assert.That(heroDriver.NavMeshAgent, Is.SameAs(hero.GetComponent<NavMeshAgent>()));
            Assert.That(heroDriver.CharacterController,
                Is.SameAs(hero.GetComponent<CharacterController>()));
            Assert.That(heroDriver.State, Is.EqualTo(HybridControlState.Autopilot));
            Assert.That(hero.GetComponent<CapsuleCollider>(), Is.Null);
            Assert.That(hero.GetComponents<Collider>().Length, Is.EqualTo(1));
            Selectable heroSelectable = hero.GetComponent<Selectable>();
            Transform selectionHitboxTransform = hero.transform.Find("SelectionHitbox");
            CapsuleCollider selectionHitbox = selectionHitboxTransform != null
                ? selectionHitboxTransform.GetComponent<CapsuleCollider>()
                : null;
            Assert.That(heroSelectable, Is.Not.Null);
            Assert.That(selectionHitbox, Is.Not.Null);
            Assert.That(selectionHitbox.enabled, Is.True);
            Assert.That(selectionHitbox.isTrigger, Is.True);
            Assert.That(heroSelectable.SelectionCollider, Is.SameAs(selectionHitbox));

            SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                Scene scene = EditorSceneManager.OpenScene(SystemTestScenePath, OpenSceneMode.Single);
                List<SceneContext> contexts = FindComponentsInScene<SceneContext>(scene);
                List<CameraModeCoordinator> coordinators =
                    FindComponentsInScene<CameraModeCoordinator>(scene);
                List<ModeHudPresenter> presenters =
                    FindComponentsInScene<ModeHudPresenter>(scene);
                Assert.That(contexts.Count, Is.EqualTo(1));
                Assert.That(coordinators.Count, Is.EqualTo(1));
                Assert.That(presenters.Count, Is.EqualTo(1));
                List<Camera> cameras = FindComponentsInScene<Camera>(scene);
                Assert.That(cameras.Count, Is.EqualTo(1));
                Assert.That(FindComponentsInScene<AudioListener>(scene).Count, Is.EqualTo(1));
                Assert.That(FindComponentsInScene<CinemachineVirtualCamera>(scene).Count,
                    Is.EqualTo(3));

                SceneContext context = contexts[0];
                Assert.That(cameras, Does.Contain(context.MainCamera));
                Assert.That(context.MinimapMapDefinition, Is.Not.Null);
                CameraModeCoordinator coordinator = coordinators[0];
                ModeHudPresenter presenter = presenters[0];
                Assert.That(context.CameraModeCoordinator, Is.SameAs(coordinator));
                Assert.That(context.ModeHudPresenter, Is.SameAs(presenter));
                Assert.That(coordinator.ActiveMode, Is.EqualTo(GameMode.RTS));
                Assert.That(coordinator.ActivePriorityCameraCount, Is.EqualTo(1));
                Assert.That(coordinator.ActiveVirtualCamera, Is.SameAs(coordinator.RtsCamera));
                Assert.That(presenter.Mode, Is.EqualTo(GameMode.RTS));
                Assert.That(presenter.RtsRoot.activeSelf, Is.True);
                Assert.That(presenter.ActRoot.activeSelf, Is.False);
                Assert.That(presenter.FpsRoot.activeSelf, Is.False);
                Assert.That(context.DefaultPawn.GetComponent<HybridControlDriver>(), Is.Not.Null);
            }
            finally
            {
                EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
            }
        }

        private static ActInputSnapshot CreateActSnapshot(Vector2 move, Vector2 look)
        {
            InputButtonState none = default(InputButtonState);
            return new ActInputSnapshot(
                move,
                look,
                none,
                none,
                none,
                none,
                none,
                none,
                none,
                none);
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
