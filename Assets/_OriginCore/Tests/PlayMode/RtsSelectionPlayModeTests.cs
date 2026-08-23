using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using OriginCore.Core;
using OriginCore.Gameplay;
using OriginCore.Input;
using OriginCore.RTS;
using OriginCore.SceneFlow;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace OriginCore.Tests.PlayMode
{
    public sealed class RtsSelectionPlayModeTests
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
            while ((!AppRoot.HasInstance || AppRoot.Instance.Services == null ||
                    AppRoot.Instance.Services.CurrentSceneContext == null ||
                    AppRoot.Instance.Services.CurrentSceneContext.SelectionService == null ||
                    !AppRoot.Instance.Services.CurrentSceneContext.SelectionService.IsServiceBound ||
                    AppRoot.Instance.Services.EntityRegistry.Count < 9) &&
                   Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(AppRoot.HasInstance, Is.True);
            Assert.That(AppRoot.Instance.Services, Is.Not.Null);
            Assert.That(AppRoot.Instance.Services.CurrentSceneContext, Is.Not.Null);
            Assert.That(AppRoot.Instance.Services.CurrentSceneContext.SelectionService, Is.Not.Null,
                "Ensure the RTS selection scene assets are configured before running this test.");
            Assert.That(
                AppRoot.Instance.Services.CurrentSceneContext.SelectionService.IsServiceBound,
                Is.True);
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
        public IEnumerator ClickShiftBoxInspectAndShortcutsKeepOnlyCommandableUnits()
        {
            DisableExistingInputDevices();
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            _testDevices.Add(keyboard);
            _testDevices.Add(mouse);
            yield return null;

            GameServices services = AppRoot.Instance.Services;
            SceneContext context = services.CurrentSceneContext;
            SelectionService selection = context.SelectionService;
            Camera camera = context.MainCamera;
            Assert.That(services.GameModeController.CurrentMode, Is.EqualTo(GameMode.RTS));
            Assert.That(selection.SelectedCount, Is.Zero);

            List<Selectable> commandable = new List<Selectable>();
            services.EntityRegistry.CollectCommandable(FactionId.Friendly, commandable);
            Assert.That(commandable.Count, Is.GreaterThanOrEqualTo(2));
            Selectable first = FindVisible(commandable, camera, null);
            Selectable second = FindVisible(commandable, camera, first);
            Assert.That(first, Is.Not.Null);
            Assert.That(second, Is.Not.Null);

            yield return Click(mouse, ToScreenPoint(camera, first));
            Assert.That(selection.SelectedCount, Is.EqualTo(1));
            Assert.That(selection.IsSelected(first), Is.True, "first click selection");
            Assert.That(first.State, Is.EqualTo(SelectionVisualState.Selected));

            QueueKeyboard(keyboard, Key.LeftShift);
            yield return null;
            yield return Click(mouse, ToScreenPoint(camera, second));
            QueueKeyboard(keyboard);
            yield return null;
            Assert.That(selection.SelectedCount, Is.EqualTo(2));
            Assert.That(selection.IsSelected(first), Is.True, "shift retained first");
            Assert.That(selection.IsSelected(second), Is.True, "shift selected second");

            Vector2 emptyPoint = FindEmptyScreenPoint(camera, selection);
            yield return Click(mouse, emptyPoint);
            Assert.That(selection.SelectedCount, Is.Zero);

            // The minimap is intentionally flush with the lower-left window edge.
            // Start just to its right so the drag represents world input, not UI input.
            Vector2 dragStart = new Vector2(
                Mathf.Min(340f, Screen.width * 0.24f),
                5f);
            Vector2 dragEnd = new Vector2(Screen.width - 5f, Screen.height - 5f);
            QueueMouse(mouse, dragStart, true);
            yield return null;
            QueueMouse(mouse, dragEnd, true);
            yield return null;
            Assert.That(selection.IsDragging, Is.True, "dragging after move");
            Assert.That(
                selection.PreviewCount,
                Is.GreaterThan(0),
                BuildDragDiagnostics(selection, services.InputRouter, camera, commandable));
            Assert.That(context.SelectionBoxView.IsVisible, Is.True, "selection box visible");
            QueueMouse(mouse, dragEnd, false);
            yield return null;
            Assert.That(selection.IsDragging, Is.False);
            Assert.That(selection.PreviewCount, Is.Zero);
            Assert.That(context.SelectionBoxView.IsVisible, Is.False);
            Assert.That(selection.SelectedCount, Is.GreaterThan(0));
            AssertSelectionIsCommandable(selection, services.FactionRelations);

            List<EntityIdentity> hostileEntities = new List<EntityIdentity>();
            services.EntityRegistry.CollectHostile(FactionId.Friendly, hostileEntities);
            Selectable enemy = FindVisibleSelectable(hostileEntities, camera);
            Assert.That(enemy, Is.Not.Null);
            yield return Click(mouse, ToScreenPoint(camera, enemy));
            Assert.That(selection.SelectedCount, Is.Zero);
            Assert.That(selection.Inspected, Is.SameAs(enemy));
            Assert.That(enemy.State, Is.EqualTo(SelectionVisualState.Inspected));
            Assert.That(selection.IsSelected(enemy), Is.False);

            QueueKeyboard(keyboard, Key.LeftShift, Key.Digit1);
            yield return null;
            QueueKeyboard(keyboard);
            yield return null;
            Assert.That(selection.SelectedCount, Is.GreaterThan(0));
            Assert.That(selection.Inspected, Is.Null);
            for (int i = 0; i < selection.SelectedCount; i++)
            {
                Assert.That(selection.Selected[i].Identity.Roles.HasAny(UnitRole.Worker), Is.True,
                    "worker shortcut only");
            }

            QueueKeyboard(keyboard, Key.LeftShift, Key.Digit2);
            yield return null;
            QueueKeyboard(keyboard);
            yield return null;
            Assert.That(selection.SelectedCount, Is.GreaterThan(0));
            for (int i = 0; i < selection.SelectedCount; i++)
            {
                Assert.That(
                    selection.Selected[i].Identity.Roles.HasAny(UnitRole.Combat | UnitRole.Hero),
                    Is.True,
                    "combat shortcut only");
            }
        }

        private static IEnumerator Click(Mouse mouse, Vector2 position)
        {
            QueueMouse(mouse, position, true);
            yield return null;
            QueueMouse(mouse, position, false);
            yield return null;
        }

        private static void QueueMouse(Mouse mouse, Vector2 position, bool leftPressed)
        {
            MouseState state = new MouseState { position = position };
            if (leftPressed)
            {
                state = state.WithButton(MouseButton.Left);
            }

            InputSystem.QueueStateEvent(mouse, state);
        }

        private static void QueueKeyboard(Keyboard keyboard, params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        }

        private static Vector2 ToScreenPoint(Camera camera, Selectable selectable)
        {
            bool isVisible = TryToScreenPoint(camera, selectable, out Vector2 screenPoint);
            Assert.That(isVisible, Is.True, selectable.name);
            return screenPoint;
        }

        private static bool TryToScreenPoint(
            Camera camera,
            Selectable selectable,
            out Vector2 point)
        {
            Vector3 worldPoint = selectable.GetSelectionWorldPoint();
            Vector3 screenPoint = camera.WorldToScreenPoint(worldPoint);
            point = new Vector2(screenPoint.x, screenPoint.y);
            return screenPoint.z > 0f &&
                   point.x >= 0f && point.x <= Screen.width &&
                   point.y >= 0f && point.y <= Screen.height;
        }

        private static Selectable FindVisible(
            List<Selectable> candidates,
            Camera camera,
            Selectable excluded)
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                Selectable candidate = candidates[i];
                if (candidate == excluded)
                {
                    continue;
                }

                if (TryToScreenPoint(camera, candidate, out Vector2 point) &&
                    point.x > 340f && point.x < Screen.width - 340f &&
                    point.y > 80f && point.y < Screen.height - 80f &&
                    !IsScreenPointOverUi(point))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static Selectable FindVisibleSelectable(
            List<EntityIdentity> candidates,
            Camera camera)
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                if (!candidates[i].TryGetComponent(out Selectable selectable))
                {
                    continue;
                }

                if (TryToScreenPoint(camera, selectable, out Vector2 point) &&
                    point.x > 340f && point.x < Screen.width - 340f &&
                    point.y > 80f && point.y < Screen.height - 80f &&
                    !IsScreenPointOverUi(point))
                {
                    return selectable;
                }
            }

            return null;
        }

        private static bool IsScreenPointOverUi(Vector2 point)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return false;
            }

            PointerEventData eventData = new PointerEventData(eventSystem)
            {
                position = point
            };
            List<RaycastResult> results = new List<RaycastResult>();
            eventSystem.RaycastAll(eventData, results);
            return results.Count > 0;
        }

        private static Vector2 FindEmptyScreenPoint(
            Camera camera,
            SelectionService selection)
        {
            const int columns = 7;
            const int rows = 5;
            const float edgePadding = 40f;
            float minX = Mathf.Min(edgePadding, Screen.width * 0.1f);
            float maxX = Mathf.Max(minX, Screen.width - minX);
            float minY = Mathf.Min(edgePadding, Screen.height * 0.1f);
            float maxY = Mathf.Max(minY, Screen.height - minY);

            for (int row = 0; row < rows; row++)
            {
                float normalizedY = rows > 1 ? row / (float)(rows - 1) : 0.5f;
                for (int column = 0; column < columns; column++)
                {
                    float normalizedX = columns > 1
                        ? column / (float)(columns - 1)
                        : 0.5f;
                    Vector2 candidate = new Vector2(
                        Mathf.Lerp(minX, maxX, normalizedX),
                        Mathf.Lerp(minY, maxY, normalizedY));
                    if (IsScreenPointOverUi(candidate))
                    {
                        continue;
                    }

                    Ray ray = camera.ScreenPointToRay(candidate);
                    if (!Physics.Raycast(
                            ray,
                            selection.MaximumRayDistance,
                            selection.SelectionMask,
                            QueryTriggerInteraction.Collide))
                    {
                        return candidate;
                    }
                }
            }

            Assert.Fail(
                "Could not find a non-UI empty screen point for the clear-selection check.");
            return Vector2.zero;
        }

        private static void AssertSelectionIsCommandable(
            SelectionService selection,
            FactionRelationService relations)
        {
            for (int i = 0; i < selection.SelectedCount; i++)
            {
                Selectable selectable = selection.Selected[i];
                Assert.That(
                    selectable.CanBeCommandedBy(FactionId.Friendly, relations),
                    Is.True,
                    selectable.name);
                Assert.That(selectable.State, Is.EqualTo(SelectionVisualState.Selected));
            }
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

        private static string BuildDragDiagnostics(
            SelectionService selection,
            InputRouter router,
            Camera camera,
            List<Selectable> candidates)
        {
            List<string> projected = new List<string>();
            for (int i = 0; i < candidates.Count; i++)
            {
                bool visible = TryToScreenPoint(camera, candidates[i], out Vector2 point);
                projected.Add(candidates[i].name + "=" + point + "/visible=" + visible);
            }

            InputAction pointAction = router.ActionsAsset.FindAction("RTS/Point", true);
            string activeControl = pointAction.activeControl != null
                ? pointAction.activeControl.path
                : "<none>";
            return "Drag diagnostics: rect=" + selection.CurrentDragRect +
                   ", snapshotPoint=" + router.CurrentSnapshot.RTS.Point +
                   ", screen=" + Screen.width + "x" + Screen.height +
                   ", pointControl=" + activeControl +
                   ", candidates=[" + string.Join(", ", projected) + "].";
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
