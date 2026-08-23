using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using OriginCore.Core;
using OriginCore.Gameplay;
using OriginCore.Input;
using OriginCore.RTS;
using OriginCore.RTS.Commands;
using OriginCore.SceneFlow;
using OriginCore.UI;
using OriginCore.Units;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace OriginCore.Tests.PlayMode
{
    public sealed class RtsMoveCommandTests
    {
        private const string SystemTestScenePath =
            "Assets/_OriginCore/Scenes/90_SystemTest.unity";
        private const float SceneLoadTimeoutSeconds = 10f;

        private readonly List<InputDevice> _testDevices = new List<InputDevice>();
        private readonly List<InputDevice> _disabledDevices = new List<InputDevice>();
        private InputSettings.BackgroundBehavior _previousBackgroundBehavior;
        private InputSettings.EditorInputBehaviorInPlayMode _previousEditorInputBehavior;
        private bool _inputSettingsCaptured;

        [UnitySetUp]
        public IEnumerator LoadSystemTestScene()
        {
            RemoveTestDevices();
            RestoreDisabledInputDevices();
            yield return DestroyPersistentRoots();
            ApplyTestInputSettings();

            int buildIndex = SceneUtility.GetBuildIndexByScenePath(SystemTestScenePath);
            Assert.That(buildIndex, Is.GreaterThanOrEqualTo(0));
            AsyncOperation load = SceneManager.LoadSceneAsync(buildIndex, LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;

            float deadline = Time.realtimeSinceStartup + SceneLoadTimeoutSeconds;
            while (!IsP08Ready() && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(IsP08Ready(), Is.True,
                "Ensure the RTS command scene assets are configured before running this test.");
            EntityIdentity hero = FindHero(AppRoot.Instance.Services);
            Assert.That(hero, Is.Not.Null);
            NavMeshAgent agent = hero.GetComponent<NavMeshAgent>();
            deadline = Time.realtimeSinceStartup + SceneLoadTimeoutSeconds;
            while ((agent == null || !agent.enabled || !agent.isOnNavMesh) &&
                   Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(agent, Is.Not.Null);
            Assert.That(agent.enabled, Is.True);
            Assert.That(agent.isOnNavMesh, Is.True);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator UnloadSystemTestScene()
        {
            RemoveTestDevices();
            RestoreDisabledInputDevices();
            yield return DestroyPersistentRoots();
            RestoreInputSettings();
        }

        [UnityTest]
        public IEnumerator RightClickShiftAppendAndSStopUseTheInputPipeline()
        {
            DisableExistingInputDevices();
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            _testDevices.Add(keyboard);
            _testDevices.Add(mouse);
            yield return null;

            GameServices services = AppRoot.Instance.Services;
            SceneContext context = services.CurrentSceneContext;
            EntityIdentity hero = FindHero(services);
            UnitCommandQueue queue = hero.GetComponent<UnitCommandQueue>();
            NavMeshMovementDriver movement = hero.GetComponent<NavMeshMovementDriver>();
            SelectOnly(context.SelectionService, context.MainCamera, hero);
            Assert.That(context.SelectionService.SelectedCount, Is.EqualTo(1));

            Assert.That(TryFindReachableScreenPoint(
                context.RtsCommandIssuer,
                context.MainCamera,
                movement.Agent,
                5f,
                null,
                out Vector2 firstScreenPoint,
                out Vector3 firstDestination), Is.True);
            Assert.That(TryFindReachableScreenPoint(
                context.RtsCommandIssuer,
                context.MainCamera,
                movement.Agent,
                5f,
                firstDestination,
                out Vector2 secondScreenPoint,
                out _), Is.True);

            int requestsBefore = movement.SetDestinationRequestCount;
            yield return RightClick(mouse, firstScreenPoint);
            Assert.That(movement.SetDestinationRequestCount,
                Is.EqualTo(requestsBefore + 1),
                "Move must call SetDestination exactly once when it begins.");
            Assert.That(queue.HasCurrentCommand, Is.True);
            Assert.That(queue.WaitingCount, Is.Zero);

            QueueKeyboard(keyboard, Key.LeftShift);
            QueueMouse(mouse, secondScreenPoint, true);
            yield return null;
            QueueMouse(mouse, secondScreenPoint, false);
            QueueKeyboard(keyboard);
            yield return null;
            Assert.That(queue.HasCurrentCommand, Is.True);
            Assert.That(queue.WaitingCount, Is.EqualTo(1));
            Assert.That(movement.SetDestinationRequestCount,
                Is.EqualTo(requestsBefore + 1),
                "A waiting move must not call SetDestination before becoming current.");

            QueueKeyboard(keyboard, Key.S);
            yield return null;
            QueueKeyboard(keyboard);
            yield return null;
            Assert.That(queue.Current, Is.Null);
            Assert.That(queue.WaitingCount, Is.Zero);
            Assert.That(queue.TotalCommandCount, Is.Zero);
            Assert.That(movement.Status, Is.EqualTo(NavMeshMovementStatus.Cancelled));
            Assert.That(movement.Agent.isStopped, Is.True);
        }

        [UnityTest]
        public IEnumerator ArrivalAutomaticallyStartsTheNextMoveExactlyOnce()
        {
            GameServices services = AppRoot.Instance.Services;
            SceneContext context = services.CurrentSceneContext;
            EntityIdentity hero = FindHero(services);
            UnitCommandQueue queue = hero.GetComponent<UnitCommandQueue>();
            NavMeshMovementDriver movement = hero.GetComponent<NavMeshMovementDriver>();
            SelectOnly(context.SelectionService, context.MainCamera, hero);

            Assert.That(TryFindReachableScreenPoint(
                context.RtsCommandIssuer,
                context.MainCamera,
                movement.Agent,
                2f,
                null,
                out _,
                out Vector3 firstDestination), Is.True);
            Assert.That(TryFindReachableScreenPoint(
                context.RtsCommandIssuer,
                context.MainCamera,
                movement.Agent,
                2f,
                firstDestination,
                out _,
                out Vector3 secondDestination), Is.True);

            Assert.That(context.RtsCommandIssuer.IssueMove(firstDestination, false), Is.True);
            Assert.That(context.RtsCommandIssuer.IssueMove(secondDestination, true), Is.True);
            Assert.That(queue.WaitingCount, Is.EqualTo(1));

            float deadline = Time.realtimeSinceStartup + 12f;
            while (movement.SetDestinationRequestCount < 2 &&
                   Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(movement.SetDestinationRequestCount, Is.EqualTo(2),
                "Arrival must finish the current command and start the queued move once.");
            Assert.That(queue.WaitingCount, Is.Zero);
            queue.StopAll();
        }

        [UnityTest]
        public IEnumerator InvalidPathFailsWithoutJammingQueueOrRepeatingDestinationRequests()
        {
            EntityIdentity hero = FindHero(AppRoot.Instance.Services);
            UnitCommandQueue queue = hero.GetComponent<UnitCommandQueue>();
            NavMeshMovementDriver movement = hero.GetComponent<NavMeshMovementDriver>();
            int requestsBefore = movement.SetDestinationRequestCount;
            MoveCommand invalid = new MoveCommand(
                hero.transform.position + Vector3.up * 1000f);

            Assert.That(queue.TryIssue(invalid, false, out CommandRejectionReason rejection),
                Is.True);
            Assert.That(rejection, Is.EqualTo(CommandRejectionReason.None));
            Assert.That(invalid.Status, Is.EqualTo(CommandStatus.Failed));
            Assert.That(invalid.FailureReason, Is.Not.Empty);
            Assert.That(queue.Current, Is.Null);
            Assert.That(queue.WaitingCount, Is.Zero);
            Assert.That(movement.SetDestinationRequestCount, Is.EqualTo(requestsBefore));
            yield return null;
            Assert.That(queue.Current, Is.Null);
            Assert.That(movement.SetDestinationRequestCount, Is.EqualTo(requestsBefore));
        }

        [UnityTest]
        public IEnumerator DirectControlCancelsHeroQueueOnFirstMeaningfulInputOnly()
        {
            GameServices services = AppRoot.Instance.Services;
            SceneContext context = services.CurrentSceneContext;
            EntityIdentity hero = FindHero(services);
            UnitCommandQueue queue = hero.GetComponent<UnitCommandQueue>();
            NavMeshMovementDriver movement = hero.GetComponent<NavMeshMovementDriver>();
            HybridControlDriver hybrid = hero.GetComponent<HybridControlDriver>();
            SelectOnly(context.SelectionService, context.MainCamera, hero);

            Assert.That(TryFindReachableScreenPoint(
                context.RtsCommandIssuer,
                context.MainCamera,
                movement.Agent,
                4f,
                null,
                out _,
                out Vector3 destination), Is.True);
            Assert.That(context.RtsCommandIssuer.IssueMove(destination, false), Is.True);
            Assert.That(queue.HasCurrentCommand, Is.True);

            Assert.That(services.GameModeController.RequestMode(
                GameMode.ACT,
                Time.frameCount + 1), Is.True);
            Assert.That(hybrid.State, Is.EqualTo(HybridControlState.PendingManualOverride));
            Assert.That(queue.HasCurrentCommand, Is.True,
                "Changing presentation must preserve the Hero RTS command.");

            ActInputSnapshot act = new ActInputSnapshot(
                Vector2.right,
                Vector2.zero,
                default(InputButtonState),
                default(InputButtonState),
                default(InputButtonState),
                default(InputButtonState),
                default(InputButtonState),
                default(InputButtonState),
                default(InputButtonState),
                default(InputButtonState));
            InputSnapshot directInput = InputSnapshot.FromAct(Time.frameCount + 2, act);
            Assert.That(services.PossessionService.TryHandleDirectInput(directInput), Is.True);
            Assert.That(queue.TotalCommandCount, Is.Zero);
            Assert.That(movement.Status, Is.EqualTo(NavMeshMovementStatus.Cancelled));
            Assert.That(hybrid.State, Is.EqualTo(HybridControlState.Manual));
            Assert.That(services.PossessionService.ManualOverrideCount, Is.EqualTo(1));
            Assert.That(services.PossessionService.TryHandleDirectInput(directInput), Is.False);
            Assert.That(services.PossessionService.ManualOverrideCount, Is.EqualTo(1));

            Assert.That(services.GameModeController.RequestMode(
                GameMode.RTS,
                Time.frameCount + 3), Is.True);
            Assert.That(hybrid.State, Is.EqualTo(HybridControlState.Autopilot));
            yield return null;
        }

        [UnityTest]
        public IEnumerator IssuerCreatesASeparateMoveCommandForEverySelectedUnit()
        {
            GameServices services = AppRoot.Instance.Services;
            SceneContext context = services.CurrentSceneContext;
            int selectedCount = context.SelectionService.SelectCombatAndHero();
            Assert.That(selectedCount, Is.GreaterThanOrEqualTo(2));
            EntityIdentity hero = FindHero(services);
            NavMeshAgent heroAgent = hero.GetComponent<NavMeshAgent>();
            Assert.That(TryFindReachableScreenPoint(
                context.RtsCommandIssuer,
                context.MainCamera,
                heroAgent,
                3f,
                null,
                out _,
                out Vector3 destination), Is.True);

            Assert.That(context.RtsCommandIssuer.IssueMove(destination, false), Is.True);
            List<IRtsCommand> commands = new List<IRtsCommand>();
            for (int i = 0; i < context.SelectionService.SelectedCount; i++)
            {
                Selectable selectable = context.SelectionService.Selected[i];
                if (selectable.TryGetComponent(out UnitCommandQueue queue) &&
                    queue.Current != null)
                {
                    commands.Add(queue.Current);
                }
            }

            Assert.That(commands.Count, Is.GreaterThanOrEqualTo(2));
            for (int i = 0; i < commands.Count; i++)
            {
                for (int j = i + 1; j < commands.Count; j++)
                {
                    Assert.That(commands[i], Is.Not.SameAs(commands[j]));
                }
            }

            context.RtsCommandIssuer.StopSelected();
            yield return null;
        }

        private static bool IsP08Ready()
        {
            if (!AppRoot.HasInstance || AppRoot.Instance.Services == null ||
                AppRoot.Instance.Services.CurrentSceneContext == null)
            {
                return false;
            }

            GameServices services = AppRoot.Instance.Services;
            SceneContext context = services.CurrentSceneContext;
            return context.SelectionService != null &&
                   context.SelectionService.IsServiceBound &&
                   context.RtsCommandIssuer != null &&
                   context.RtsCommandIssuer.IsServiceBound &&
                   services.EntityRegistry.Count >= 9;
        }

        private static EntityIdentity FindHero(GameServices services)
        {
            IReadOnlyList<EntityIdentity> entities = services.EntityRegistry.Entities;
            for (int i = 0; i < entities.Count; i++)
            {
                EntityIdentity entity = entities[i];
                if (entity != null && entity.HasAnyRole(UnitRole.Hero))
                {
                    return entity;
                }
            }

            return null;
        }

        private static void SelectOnly(
            SelectionService selection,
            Camera camera,
            EntityIdentity identity)
        {
            Selectable selectable = identity.GetComponent<Selectable>();
            Assert.That(selectable, Is.Not.Null);
            Vector3 worldPoint = selectable.GetSelectionWorldPoint();
            Vector3 projected = camera.WorldToScreenPoint(worldPoint);
            Assert.That(projected.z, Is.GreaterThan(0f));
            Assert.That(selection.ClickAtScreenPoint(
                new Vector2(projected.x, projected.y), false), Is.True);
            Assert.That(selection.SelectedCount, Is.EqualTo(1));
            Assert.That(selection.Selected[0], Is.SameAs(selectable));
        }

        private static bool TryFindReachableScreenPoint(
            RtsCommandIssuer issuer,
            Camera camera,
            NavMeshAgent agent,
            float minimumDistance,
            Vector3? excludedDestination,
            out Vector2 screenPoint,
            out Vector3 destination)
        {
            screenPoint = default(Vector2);
            destination = default(Vector3);
            float bestDistance = -1f;
            int xSteps = 8;
            int ySteps = 5;
            for (int y = 0; y < ySteps; y++)
            {
                float screenY = Mathf.Lerp(210f, Screen.height - 70f, y / (float)(ySteps - 1));
                for (int x = 0; x < xSteps; x++)
                {
                    float screenX = Mathf.Lerp(70f, Screen.width - 70f, x / (float)(xSteps - 1));
                    Vector2 candidateScreen = new Vector2(screenX, screenY);
                    if (!issuer.TryResolveGroundPoint(candidateScreen, out Vector3 candidate))
                    {
                        continue;
                    }

                    if (UiPointerUtility.IsScreenPointOverUi(candidateScreen))
                    {
                        continue;
                    }

                    float distance = HorizontalDistance(agent.transform.position, candidate);
                    if (distance < minimumDistance || distance <= bestDistance)
                    {
                        continue;
                    }

                    if (excludedDestination.HasValue &&
                        HorizontalDistance(excludedDestination.Value, candidate) < minimumDistance)
                    {
                        continue;
                    }

                    NavMeshPath path = new NavMeshPath();
                    if (!agent.CalculatePath(candidate, path) ||
                        path.status != NavMeshPathStatus.PathComplete)
                    {
                        continue;
                    }

                    bestDistance = distance;
                    screenPoint = candidateScreen;
                    destination = candidate;
                }
            }

            return bestDistance >= minimumDistance;
        }

        private static float HorizontalDistance(Vector3 left, Vector3 right)
        {
            left.y = 0f;
            right.y = 0f;
            return Vector3.Distance(left, right);
        }

        private static IEnumerator RightClick(Mouse mouse, Vector2 position)
        {
            QueueMouse(mouse, position, true);
            yield return null;
            QueueMouse(mouse, position, false);
            yield return null;
        }

        private static void QueueMouse(Mouse mouse, Vector2 position, bool rightPressed)
        {
            MouseState state = new MouseState { position = position };
            if (rightPressed)
            {
                state = state.WithButton(MouseButton.Right);
            }

            InputSystem.QueueStateEvent(mouse, state);
        }

        private static void QueueKeyboard(Keyboard keyboard, params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
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

        private void DisableExistingInputDevices()
        {
            RestoreDisabledInputDevices();
            for (int i = 0; i < InputSystem.devices.Count; i++)
            {
                InputDevice device = InputSystem.devices[i];
                if (device.enabled && (device is Pointer || device is Keyboard))
                {
                    InputSystem.DisableDevice(device);
                    _disabledDevices.Add(device);
                }
            }
        }

        private void RestoreDisabledInputDevices()
        {
            for (int i = _disabledDevices.Count - 1; i >= 0; i--)
            {
                InputDevice device = _disabledDevices[i];
                if (device != null && device.added && !device.enabled)
                {
                    InputSystem.EnableDevice(device);
                }
            }

            _disabledDevices.Clear();
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
