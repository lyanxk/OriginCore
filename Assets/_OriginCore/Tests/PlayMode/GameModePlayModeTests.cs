using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using OriginCore.ACT;
using OriginCore.Cameras;
using OriginCore.Core;
using OriginCore.FPS;
using OriginCore.Gameplay;
using OriginCore.Input;
using OriginCore.SceneFlow;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace OriginCore.Tests.PlayMode
{
    public sealed class GameModePlayModeTests
    {
        private const string SystemTestScenePath =
            "Assets/_OriginCore/Scenes/90_SystemTest.unity";
        private const float SceneLoadTimeoutSeconds = 10f;

        private readonly List<InputDevice> _testDevices = new List<InputDevice>();
        private InputSettings.BackgroundBehavior _previousBackgroundBehavior;
        private InputSettings.EditorInputBehaviorInPlayMode _previousEditorInputBehavior;
        private bool _inputSettingsCaptured;

        [UnitySetUp]
        public IEnumerator LoadSystemTestScene()
        {
            RemoveTestDevices();
            yield return DestroyPersistentRoots();
            ApplyTestInputSettings();

            int buildIndex = SceneUtility.GetBuildIndexByScenePath(SystemTestScenePath);
            Assert.That(buildIndex, Is.GreaterThanOrEqualTo(0),
                "90_SystemTest is missing from Build Settings.");
            AsyncOperation load = SceneManager.LoadSceneAsync(buildIndex, LoadSceneMode.Single);
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
        public IEnumerator UnloadSystemTestScene()
        {
            RemoveTestDevices();
            yield return DestroyPersistentRoots();
            RestoreInputSettings();
        }

        [UnityTest]
        public IEnumerator FKeysSwitchPresentationAndMeaningfulInputTakesOverExactlyOnce()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            _testDevices.Add(keyboard);
            _testDevices.Add(mouse);

            GameServices services = AppRoot.Instance.Services;
            InputRouter inputRouter = services.InputRouter;
            GameModeController modes = services.GameModeController;
            PossessionService possession = services.PossessionService;
            CursorPolicy cursor = services.CursorPolicy;
            SceneContext context = services.CurrentSceneContext;
            CameraModeCoordinator cameras = context.CameraModeCoordinator;
            HybridControlDriver pawn = possession.CurrentPawn;
            Assert.That(modes, Is.Not.Null);
            Assert.That(possession, Is.Not.Null);
            Assert.That(cursor, Is.Not.Null);
            Assert.That(cameras, Is.Not.Null);
            Assert.That(pawn, Is.Not.Null);
            ActMovementController actMovement = pawn.GetComponent<ActMovementController>();
            FpsMovementController fpsMovement = pawn.GetComponent<FpsMovementController>();
            Assert.That(actMovement, Is.Not.Null);
            Assert.That(fpsMovement, Is.Not.Null);
            Assert.That(cameras.ActCamera.Follow, Is.SameAs(actMovement.ActCameraTarget));
            Assert.That(cameras.ActCamera.LookAt, Is.SameAs(actMovement.ActCameraTarget));
            Assert.That(cameras.FpsCamera.Follow, Is.SameAs(fpsMovement.FpsCameraTarget));
            Assert.That(cameras.FpsCamera.LookAt, Is.Null);
            Assert.That(modes.CurrentMode, Is.EqualTo(GameMode.RTS));
            Assert.That(services.InputRouter.ActiveGameplayMap, Is.EqualTo(GameplayInputMap.RTS));
            Assert.That(pawn.State, Is.EqualTo(HybridControlState.Autopilot));
            Assert.That(cameras.ActiveVirtualCamera, Is.SameAs(cameras.RtsCamera));
            Assert.That(cameras.ActivePriorityCameraCount, Is.EqualTo(1));

            NavMeshAgent agent = pawn.NavMeshAgent;
            CharacterController characterController = pawn.CharacterController;
            Assert.That(agent.enabled, Is.True);
            Assert.That(characterController.enabled, Is.False);
            Assert.That(agent.isOnNavMesh, Is.True,
                "The P06 hero must start attached to the SystemTest NavMesh.");

            Vector3 requestedDestination = pawn.transform.position + Vector3.forward * 7f;
            Assert.That(
                NavMesh.SamplePosition(
                    requestedDestination,
                    out NavMeshHit destinationHit,
                    4f,
                    agent.areaMask),
                Is.True);
            Assert.That(agent.SetDestination(destinationHit.position), Is.True);
            yield return null;
            Assert.That(agent.pathPending || agent.hasPath, Is.True,
                "The test pawn needs an active autopilot path before switching mode.");

            int cancellationEvents = 0;
            int activationEvents = 0;
            possession.AutopilotCancellationRequested += (_, __) => cancellationEvents++;
            possession.ManualOverrideActivated += (_, __) => activationEvents++;

            yield return PressUntil(
                keyboard.f2Key,
                () => modes.CurrentMode == GameMode.ACT,
                "F2 was not consumed by InputRouter within the PlayMode frame budget.",
                () => BuildInputDiagnostics(
                    inputRouter,
                    inputRouter.ActionsAsset.FindAction("Global/SwitchACT", true),
                    keyboard.f2Key,
                    modes));
            Assert.That(modes.CurrentMode, Is.EqualTo(GameMode.ACT));
            Assert.That(services.InputRouter.ActiveGameplayMap, Is.EqualTo(GameplayInputMap.ACT));
            Assert.That(pawn.State, Is.EqualTo(HybridControlState.PendingManualOverride));
            Assert.That(agent.enabled, Is.True,
                "Changing presentation alone must not interrupt autopilot.");
            Assert.That(characterController.enabled, Is.False);
            Assert.That(agent.pathPending || agent.hasPath, Is.True,
                "The active autopilot path must survive RTS to ACT observation mode.");
            Assert.That(possession.ManualOverrideCount, Is.Zero);
            Assert.That(cameras.ActiveVirtualCamera, Is.SameAs(cameras.ActCamera));
            Assert.That(cameras.ActivePriorityCameraCount, Is.EqualTo(1));
            Assert.That(cursor.State.Visible, Is.False);
            Assert.That(cursor.State.LockState, Is.EqualTo(CursorLockMode.Locked));
            Assert.That(context.ModeHudPresenter.Mode, Is.EqualTo(GameMode.ACT));

            InputSystem.QueueDeltaStateEvent(mouse.delta, new Vector2(180f, -90f));
            yield return null;
            yield return null;
            InputSystem.QueueDeltaStateEvent(mouse.delta, Vector2.zero);
            yield return null;
            Assert.That(pawn.State, Is.EqualTo(HybridControlState.PendingManualOverride),
                "Look input alone must not take control from autopilot.");
            Assert.That(possession.ManualOverrideCount, Is.Zero);
            Assert.That(cancellationEvents, Is.Zero);
            Assert.That(activationEvents, Is.Zero);

            yield return PressUntil(
                keyboard.wKey,
                () => pawn.State == HybridControlState.Manual,
                "Meaningful ACT movement was not consumed within the PlayMode frame budget.");
            Assert.That(pawn.State, Is.EqualTo(HybridControlState.Manual));
            Assert.That(agent.enabled, Is.False);
            Assert.That(characterController.enabled, Is.True);
            Assert.That(possession.ManualOverrideCount, Is.EqualTo(1));
            Assert.That(cancellationEvents, Is.EqualTo(1));
            Assert.That(activationEvents, Is.EqualTo(1));

            yield return Tap(keyboard.wKey);
            Assert.That(possession.ManualOverrideCount, Is.EqualTo(1),
                "Manual takeover must be a one-shot transition.");
            Assert.That(cancellationEvents, Is.EqualTo(1));
            Assert.That(activationEvents, Is.EqualTo(1));

            yield return PressUntil(
                keyboard.f3Key,
                () => modes.CurrentMode == GameMode.FPS,
                "F3 was not consumed by InputRouter within the PlayMode frame budget.");
            Assert.That(modes.CurrentMode, Is.EqualTo(GameMode.FPS));
            Assert.That(services.InputRouter.ActiveGameplayMap, Is.EqualTo(GameplayInputMap.FPS));
            Assert.That(pawn.State, Is.EqualTo(HybridControlState.Manual),
                "ACT to FPS must preserve the current possession state.");
            Assert.That(cameras.ActiveVirtualCamera, Is.SameAs(cameras.FpsCamera));
            Assert.That(cameras.ActivePriorityCameraCount, Is.EqualTo(1));
            Assert.That(context.ModeHudPresenter.Mode, Is.EqualTo(GameMode.FPS));

            Vector3 beforeReturn = pawn.transform.position;
            yield return PressUntil(
                keyboard.f1Key,
                () => modes.CurrentMode == GameMode.RTS,
                "F1 was not consumed by InputRouter within the PlayMode frame budget.");
            Assert.That(modes.CurrentMode, Is.EqualTo(GameMode.RTS));
            Assert.That(services.InputRouter.ActiveGameplayMap, Is.EqualTo(GameplayInputMap.RTS));
            Assert.That(pawn.State, Is.EqualTo(HybridControlState.Autopilot));
            Assert.That(agent.enabled, Is.True);
            Assert.That(characterController.enabled, Is.False);
            Assert.That(agent.isStopped, Is.True);
            Assert.That(Vector3.Distance(beforeReturn, pawn.transform.position), Is.LessThan(1f),
                "Returning to RTS must not jump the hero to another location.");
            Assert.That(Vector3.Distance(Vector3.zero, pawn.transform.position), Is.GreaterThan(1f),
                "Returning to RTS must never move the hero to world origin.");
            Assert.That(cameras.ActiveVirtualCamera, Is.SameAs(cameras.RtsCamera));
            Assert.That(cameras.ActivePriorityCameraCount, Is.EqualTo(1));
            Assert.That(cursor.State.Visible, Is.True);
            Assert.That(cursor.State.LockState, Is.EqualTo(CursorLockMode.None));
            Assert.That(context.ModeHudPresenter.Mode, Is.EqualTo(GameMode.RTS));
            Camera[] sceneCameras = Object.FindObjectsOfType<Camera>(true);
            Assert.That(sceneCameras.Length, Is.EqualTo(1));
            Assert.That(sceneCameras, Does.Contain(context.MainCamera));
            Assert.That(context.MinimapMapDefinition, Is.Not.Null);
            Assert.That(context.MinimapView, Is.Not.Null);
            Assert.That(Object.FindObjectsOfType<AudioListener>(true).Length, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator DirectModeWithoutPawnIsRejectedWithoutThrowing()
        {
            GameServices services = AppRoot.Instance.Services;
            services.PossessionService.BindPawn(null);
            int rejectionCount = 0;
            GameMode rejectedMode = GameMode.RTS;
            string rejectionMessage = string.Empty;
            services.GameModeController.ModeChangeRejected += (mode, message) =>
            {
                rejectionCount++;
                rejectedMode = mode;
                rejectionMessage = message;
            };

            Assert.That(services.GameModeController.RequestMode(GameMode.ACT), Is.False);
            Assert.That(services.GameModeController.CurrentMode, Is.EqualTo(GameMode.RTS));
            Assert.That(services.InputRouter.ActiveGameplayMap, Is.EqualTo(GameplayInputMap.RTS));
            Assert.That(rejectionCount, Is.EqualTo(1));
            Assert.That(rejectedMode, Is.EqualTo(GameMode.ACT));
            StringAssert.Contains("No possessable pawn", rejectionMessage);
            StringAssert.Contains("No possessable pawn", services.CurrentSceneContext.ModeHudPresenter.LastError);
            yield return null;
        }

        private IEnumerator Tap(KeyControl key)
        {
            QueueKeyState(key, true);
            yield return null;
            yield return null;
            QueueKeyState(key, false);
            yield return null;
        }

        private IEnumerator PressUntil(
            KeyControl key,
            Func<bool> condition,
            string failureMessage,
            Func<string> diagnostics = null)
        {
            QueueKeyState(key, true);
            const int maxFrames = 6;
            int elapsedFrames = 0;
            while (!condition() && elapsedFrames < maxFrames)
            {
                elapsedFrames++;
                yield return null;
            }

            bool reached = condition();
            string diagnosticDetails = !reached && diagnostics != null
                ? diagnostics()
                : string.Empty;
            QueueKeyState(key, false);
            yield return null;
            Assert.That(
                reached,
                Is.True,
                string.IsNullOrEmpty(diagnosticDetails)
                    ? failureMessage
                    : failureMessage + "\n" + diagnosticDetails);
        }

        private static string BuildInputDiagnostics(
            InputRouter router,
            InputAction action,
            KeyControl key,
            GameModeController modes)
        {
            List<string> controls = new List<string>();
            for (int i = 0; i < action.controls.Count; i++)
            {
                controls.Add(action.controls[i].path);
            }

            List<string> effectivePaths = new List<string>();
            for (int i = 0; i < action.bindings.Count; i++)
            {
                effectivePaths.Add(action.bindings[i].effectivePath);
            }

            return string.Format(
                "Diagnostics: router(active={0}, enabled={1}, initialized={2}), " +
                "modeController(initialized={3}, mode={4}), " +
                "action(enabled={5}, pressed={6}, mapEnabled={7}), " +
                "key(deviceAdded={8}, deviceEnabled={9}, pressed={10}), " +
                "bindings=[{11}], controls=[{12}], frame={13}.",
                router.gameObject.activeInHierarchy,
                router.isActiveAndEnabled,
                router.IsInitialized,
                modes.IsInitialized,
                modes.CurrentMode,
                action.enabled,
                action.IsPressed(),
                action.actionMap.enabled,
                key.device.added,
                key.device.enabled,
                key.isPressed,
                string.Join(", ", effectivePaths),
                string.Join(", ", controls),
                Time.frameCount);
        }

        private static void QueueKeyState(KeyControl key, bool pressed)
        {
            Keyboard keyboard = key.device as Keyboard;
            Assert.That(keyboard, Is.Not.Null, "The supplied key does not belong to a Keyboard.");
            InputSystem.QueueStateEvent(
                keyboard,
                pressed ? new KeyboardState(key.keyCode) : new KeyboardState());
        }

        private void RemoveTestDevices()
        {
            for (int i = _testDevices.Count - 1; i >= 0; i--)
            {
                InputDevice device = _testDevices[i];
                if (device != null && device.added)
                {
                    InputSystem.RemoveDevice(device);
                }
            }

            _testDevices.Clear();
        }

        private void ApplyTestInputSettings()
        {
            if (!_inputSettingsCaptured)
            {
                _previousBackgroundBehavior = InputSystem.settings.backgroundBehavior;
                _previousEditorInputBehavior =
                    InputSystem.settings.editorInputBehaviorInPlayMode;
                _inputSettingsCaptured = true;
            }

            // The Test Runner, rather than the Game View, owns editor focus while this
            // test executes. Synthetic devices must remain enabled and routed to gameplay.
            InputSystem.settings.backgroundBehavior =
                InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        }

        private void RestoreInputSettings()
        {
            if (!_inputSettingsCaptured)
            {
                return;
            }

            InputSystem.settings.backgroundBehavior = _previousBackgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = _previousEditorInputBehavior;
            _inputSettingsCaptured = false;
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
