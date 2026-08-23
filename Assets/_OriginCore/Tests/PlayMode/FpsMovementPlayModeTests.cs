using System;
using Cinemachine;
using NUnit.Framework;
using OriginCore.ACT;
using OriginCore.Core;
using OriginCore.FPS;
using OriginCore.Gameplay;
using OriginCore.Input;
using OriginCore.Settings;
using OriginCore.UI.FPS;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

namespace OriginCore.Tests.PlayMode
{
    public sealed class FpsMovementPlayModeTests
    {
        [Test]
        public void SprintReleaseLatchesOnlyAtMinimumSpeedAndStopRestoresWalk()
        {
            using (MovementHarness harness = new MovementHarness(HybridControlState.Manual))
            {
                harness.Settle();
                for (int i = 0; i < 20; i++)
                {
                    harness.Controller.Step(
                        CreateInput(Vector2.up),
                        Vector3.forward,
                        Vector3.right,
                        0.05f);
                }

                float walkSpeed = harness.Motor.PlanarVelocity.magnitude;
                Assert.That(walkSpeed,
                    Is.EqualTo(harness.FpsConfig.WalkSpeed).Within(0.05f));

                // Releasing below the configured minimum must fall back to walking.
                harness.Controller.Step(
                    CreateInput(
                        Vector2.up,
                        sprintHeld: true,
                        sprintPressed: true),
                    Vector3.forward,
                    Vector3.right,
                    0.02f);
                Assert.That(harness.Controller.IsSprinting, Is.True);
                Assert.That(harness.Controller.IsSprintLatched, Is.False);
                Assert.That(harness.Motor.PlanarVelocity.magnitude,
                    Is.LessThan(harness.FpsConfig.SprintLatchMinSpeed));
                harness.Controller.Step(
                    CreateInput(Vector2.up, sprintReleased: true),
                    Vector3.forward,
                    Vector3.right,
                    0.02f);
                Assert.That(harness.Controller.IsSprinting, Is.False,
                    "Releasing below sprintLatchMinSpeed must restore walking.");
                Assert.That(harness.Motor.PlanarVelocity.magnitude,
                    Is.EqualTo(harness.FpsConfig.WalkSpeed).Within(0.05f));

                // Releasing at or above the minimum locks sprint until a full stop.
                harness.Controller.Step(
                    CreateInput(
                        Vector2.up,
                        sprintHeld: true,
                        sprintPressed: true),
                    Vector3.forward,
                    Vector3.right,
                    0.05f);
                Assert.That(harness.Motor.PlanarVelocity.magnitude,
                    Is.GreaterThanOrEqualTo(harness.FpsConfig.SprintLatchMinSpeed));
                Assert.That(harness.Controller.IsSprintLatched, Is.False);

                harness.Controller.Step(
                    CreateInput(Vector2.up, sprintReleased: true),
                    Vector3.forward,
                    Vector3.right,
                    0.05f);
                Assert.That(harness.Controller.IsSprintLatched, Is.True,
                    "Releasing at sprintLatchMinSpeed must latch sprint.");
                for (int i = 0; i < 10; i++)
                {
                    harness.Controller.Step(
                        CreateInput(Vector2.up),
                        Vector3.forward,
                        Vector3.right,
                        0.05f);
                }

                float sprintSpeed = harness.Motor.PlanarVelocity.magnitude;
                Assert.That(sprintSpeed,
                    Is.EqualTo(harness.FpsConfig.SprintSpeed).Within(0.05f));
                Assert.That(sprintSpeed, Is.GreaterThan(walkSpeed));

                // A second Shift press is not an off toggle.
                harness.Controller.Step(
                    CreateInput(
                        Vector2.up,
                        sprintHeld: true,
                        sprintPressed: true),
                    Vector3.forward,
                    Vector3.right,
                    0.05f);
                harness.Controller.Step(
                    CreateInput(Vector2.up, sprintReleased: true),
                    Vector3.forward,
                    Vector3.right,
                    0.05f);
                Assert.That(harness.Controller.IsSprintLatched, Is.True,
                    "Pressing Shift again must not turn latched sprint off.");

                // A real stop clears the latch; movement then resumes at walk speed.
                for (int i = 0; i < 10; i++)
                {
                    harness.Controller.Step(
                        CreateInput(Vector2.zero),
                        Vector3.forward,
                        Vector3.right,
                        0.05f);
                }

                Assert.That(harness.Motor.PlanarVelocity.magnitude,
                    Is.EqualTo(0f).Within(0.01f));
                Assert.That(harness.Controller.IsSprinting, Is.False,
                    "A complete stop must clear latched sprint.");
                for (int i = 0; i < 20; i++)
                {
                    harness.Controller.Step(
                        CreateInput(Vector2.up),
                        Vector3.forward,
                        Vector3.right,
                        0.05f);
                }

                Assert.That(harness.Motor.PlanarVelocity.magnitude,
                    Is.EqualTo(harness.FpsConfig.WalkSpeed).Within(0.05f),
                    "Movement after a stop must resume at walking speed.");

                harness.Motor.ResetMotion();
                harness.Controller.Step(
                    CreateInput(Vector2.zero, crouchHeld: true, crouchPressed: true),
                    Vector3.forward,
                    Vector3.right,
                    0.02f);
                Assert.That(harness.Controller.Slide.IsSliding, Is.False,
                    "Low-speed crouch must not enter Slide.");
                Assert.That(harness.Motor.IsCrouched, Is.True);

                harness.Controller.Step(
                    CreateInput(
                        Vector2.up,
                        sprintHeld: true,
                        sprintPressed: true),
                    Vector3.forward,
                    Vector3.right,
                    0.05f);
                Assert.That(harness.Controller.IsSprinting, Is.True);
                harness.Controller.ExitFpsPresentation();
                Assert.That(harness.Controller.IsSprinting, Is.False,
                    "Leaving FPS must restore the default walking state.");
            }
        }

        [Test]
        public void SlideEntersAtSpeedAndExitsByDurationOrJump()
        {
            using (MovementHarness harness = new MovementHarness(HybridControlState.Manual))
            {
                harness.Settle();
                harness.AccelerateToSprint();
                harness.Controller.Step(
                    CreateInput(
                        Vector2.up,
                        crouchHeld: true,
                        crouchPressed: true),
                    Vector3.forward,
                    Vector3.right,
                    0.02f);
                Assert.That(harness.Controller.Slide.IsSliding, Is.True);
                Assert.That(harness.Motor.IsCrouched, Is.True);
                Assert.That(harness.Controller.Slide.Speed,
                    Is.GreaterThan(harness.FpsConfig.SlideMinSpeed));

                for (int i = 0; i < 60; i++)
                {
                    harness.Controller.Step(
                        CreateInput(Vector2.up, crouchHeld: true),
                        Vector3.forward,
                        Vector3.right,
                        0.02f);
                }

                Assert.That(harness.Controller.Slide.IsSliding, Is.False);
                Assert.That(harness.Controller.Slide.LastExitReason,
                    Is.EqualTo(SlideExitReason.Duration).Or.EqualTo(SlideExitReason.Speed));
                Assert.That(harness.Motor.IsCrouched, Is.True);
                harness.Controller.Step(
                    CreateInput(Vector2.zero),
                    Vector3.forward,
                    Vector3.right,
                    0.02f);
                Assert.That(harness.Motor.IsCrouched, Is.False);
            }

            using (MovementHarness harness = new MovementHarness(HybridControlState.Manual))
            {
                harness.Settle();
                harness.AccelerateToSprint();
                harness.Controller.Step(
                    CreateInput(
                        Vector2.up,
                        crouchHeld: true,
                        crouchPressed: true),
                    Vector3.forward,
                    Vector3.right,
                    0.02f);
                Assert.That(harness.Controller.Slide.IsSliding, Is.True);

                harness.Controller.Step(
                    CreateInput(Vector2.up, jumpPressed: true),
                    Vector3.forward,
                    Vector3.right,
                    0.02f);
                Assert.That(harness.Controller.Slide.IsSliding, Is.False);
                Assert.That(harness.Controller.Slide.LastExitReason,
                    Is.EqualTo(SlideExitReason.Jump));
                Assert.That(harness.Controller.JumpedThisStep, Is.True);
                Assert.That(harness.Motor.VerticalVelocity, Is.GreaterThan(0f));
            }
        }

        [Test]
        public void AdsChangesOnlyFovAndRestoresThePreviousLens()
        {
            FpsMovementConfig config = CreateFpsConfig();
            GameObject cameraObject = new GameObject("P12_AimCamera_Test");
            try
            {
                CinemachineVirtualCamera camera =
                    cameraObject.AddComponent<CinemachineVirtualCamera>();
                LensSettings lens = camera.m_Lens;
                lens.FieldOfView = 68f;
                camera.m_Lens = lens;

                AimState aim = new AimState();
                Assert.That(aim.Begin(camera, config), Is.True);
                Assert.That(camera.m_Lens.FieldOfView,
                    Is.EqualTo(config.BaseFov).Within(0.001f));
                Assert.That(aim.Step(true, 0.25f), Is.True);
                Assert.That(aim.IsAiming, Is.True);
                Assert.That(camera.m_Lens.FieldOfView,
                    Is.EqualTo(config.AdsFov).Within(0.001f));

                aim.Step(false, 0.25f);
                Assert.That(aim.IsAiming, Is.False);
                Assert.That(camera.m_Lens.FieldOfView,
                    Is.EqualTo(config.BaseFov).Within(0.001f));
                aim.End();
                Assert.That(camera.m_Lens.FieldOfView, Is.EqualTo(68f).Within(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void CrosshairUsesSettingsRgbAndOnlyShowsInFps()
        {
            GameObject root = new GameObject("P12_CrosshairView_Test");
            GameObject dot = new GameObject(
                "Dot",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            dot.transform.SetParent(root.transform, false);
            try
            {
                Image image = dot.GetComponent<Image>();
                CrosshairView view = root.AddComponent<CrosshairView>();
                view.Configure(image, dot, null, Color.white);
                SettingsData settings = new SettingsData
                {
                    CrosshairColor = new Color(0.2f, 0.45f, 0.8f, 0.1f)
                };
                view.RefreshColor(settings);

                view.ApplyMode(GameMode.RTS);
                Assert.That(view.IsVisible, Is.False);
                view.ApplyMode(GameMode.FPS);
                Assert.That(view.IsVisible, Is.True);
                Assert.That(view.Color.r, Is.EqualTo(0.2f).Within(0.001f));
                Assert.That(view.Color.g, Is.EqualTo(0.45f).Within(0.001f));
                Assert.That(view.Color.b, Is.EqualTo(0.8f).Within(0.001f));
                Assert.That(view.Color.a, Is.EqualTo(1f).Within(0.001f));
                view.ApplyMode(GameMode.ACT);
                Assert.That(view.IsVisible, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void FirstPersonVisibilityRestoresEachRendererStateExactly()
        {
            GameObject root = new GameObject("P12_Visibility_Test");
            GameObject visiblePart = GameObject.CreatePrimitive(PrimitiveType.Cube);
            GameObject disabledPart = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visiblePart.transform.SetParent(root.transform, false);
            disabledPart.transform.SetParent(root.transform, false);
            try
            {
                Renderer visibleRenderer = visiblePart.GetComponent<Renderer>();
                Renderer disabledRenderer = disabledPart.GetComponent<Renderer>();
                disabledRenderer.enabled = false;
                FirstPersonVisibility visibility =
                    root.AddComponent<FirstPersonVisibility>();
                visibility.Configure(new[] { visibleRenderer, disabledRenderer });

                visibility.SetFirstPersonActive(true);
                Assert.That(visibility.IsFirstPersonHidden, Is.True);
                Assert.That(visibleRenderer.enabled, Is.False);
                Assert.That(disabledRenderer.enabled, Is.False);

                visibility.SetFirstPersonActive(false);
                Assert.That(visibility.IsFirstPersonHidden, Is.False);
                Assert.That(visibleRenderer.enabled, Is.True);
                Assert.That(disabledRenderer.enabled, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PendingFpsLookAndReservedInputDoNotTakeOverButAimDoesOnce()
        {
            using (MovementHarness harness = new MovementHarness(HybridControlState.Autopilot))
            {
                GameObject serviceObject = new GameObject("P12_Possession_Test");
                try
                {
                    PossessionService possession = serviceObject.AddComponent<PossessionService>();
                    possession.Configure(0.1f);
                    possession.BindPawn(harness.Driver);
                    Assert.That(possession.PrepareDirectObservation(out string error), Is.True, error);

                    FpsInputSnapshot lookOnly = CreateInput(
                        Vector2.zero,
                        look: new Vector2(20f, -8f));
                    Assert.That(possession.TryHandleDirectInput(
                        InputSnapshot.FromFps(10, lookOnly)), Is.False);
                    Assert.That(harness.Driver.State,
                        Is.EqualTo(HybridControlState.PendingManualOverride));

                    InputButtonState pressed = new InputButtonState(true, true, false);
                    FpsInputSnapshot reservedPrimary = new FpsInputSnapshot(
                        Vector2.zero,
                        Vector2.zero,
                        default(InputButtonState),
                        default(InputButtonState),
                        default(InputButtonState),
                        default(InputButtonState),
                        pressed,
                        default(InputButtonState),
                        default(InputButtonState),
                        default(InputButtonState),
                        default(InputButtonState),
                        default(InputButtonState));
                    Assert.That(possession.TryHandleDirectInput(
                        InputSnapshot.FromFps(11, reservedPrimary)), Is.False);

                    Assert.That(CreateInput(
                        Vector2.zero,
                        sprintHeld: true).HasMeaningfulDirectInput(), Is.False,
                        "A held latch from an earlier/suppressed frame must not take over.");
                    Assert.That(CreateInput(
                        Vector2.zero,
                        sprintHeld: true,
                        sprintPressed: true).HasMeaningfulDirectInput(), Is.True);

                    FpsInputSnapshot aim = CreateInput(Vector2.zero, aimHeld: true);
                    Assert.That(possession.TryHandleDirectInput(
                        InputSnapshot.FromFps(12, aim)), Is.True);
                    Assert.That(harness.Driver.State, Is.EqualTo(HybridControlState.Manual));
                    Assert.That(possession.ManualOverrideCount, Is.EqualTo(1));
                    Assert.That(possession.TryHandleDirectInput(
                        InputSnapshot.FromFps(13, aim)), Is.False);
                    Assert.That(possession.ManualOverrideCount, Is.EqualTo(1));
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(serviceObject);
                }
            }
        }

        private static FpsInputSnapshot CreateInput(
            Vector2 move,
            bool jumpPressed = false,
            bool crouchHeld = false,
            bool crouchPressed = false,
            bool sprintHeld = false,
            bool sprintPressed = false,
            bool sprintReleased = false,
            bool aimHeld = false,
            Vector2 look = default(Vector2))
        {
            InputButtonState jump = new InputButtonState(
                jumpPressed,
                jumpPressed,
                false);
            InputButtonState crouch = new InputButtonState(
                crouchPressed,
                crouchHeld,
                !crouchHeld && !crouchPressed);
            InputButtonState sprint = new InputButtonState(
                sprintPressed,
                sprintHeld || sprintPressed,
                sprintReleased);
            InputButtonState aim = new InputButtonState(
                aimHeld,
                aimHeld,
                false);
            InputButtonState empty = default(InputButtonState);
            return new FpsInputSnapshot(
                move,
                look,
                jump,
                crouch,
                sprint,
                aim,
                empty,
                empty,
                empty,
                empty,
                empty,
                empty);
        }

        private static FpsMovementConfig CreateFpsConfig()
        {
            FpsMovementConfig config = ScriptableObject.CreateInstance<FpsMovementConfig>();
            config.Configure(4f, 7f, 5f, 28f, 5f, 3f, 8f, 0.9f, 75f, 55f, 90f);
            return config;
        }

        private sealed class MovementHarness : IDisposable
        {
            public MovementHarness(HybridControlState initialState)
            {
                Ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Ground.name = "P12_Ground_Test";
                Ground.layer = 0;
                Ground.transform.position = new Vector3(0f, -0.25f, 0f);
                Ground.transform.localScale = new Vector3(200f, 0.5f, 200f);

                Root = new GameObject("P12_Pawn_Test");
                Root.SetActive(false);
                Root.transform.position = Vector3.zero;
                CharacterController = Root.AddComponent<CharacterController>();
                CharacterController.center = new Vector3(0f, 1f, 0f);
                CharacterController.height = 2f;
                CharacterController.radius = 0.45f;
                CharacterController.skinWidth = 0.08f;
                CharacterController.stepOffset = 0.3f;
                CharacterController.minMoveDistance = 0f;

                NavMeshAgent agent = Root.AddComponent<NavMeshAgent>();
                Driver = Root.AddComponent<HybridControlDriver>();
                Motor = Root.AddComponent<HybridPawnMotor>();
                FirstPersonVisibility visibility =
                    Root.AddComponent<FirstPersonVisibility>();
                Controller = Root.AddComponent<FpsMovementController>();

                GameObject cameraTargetObject = new GameObject("FpsCameraTarget");
                cameraTargetObject.transform.SetParent(Root.transform, false);
                cameraTargetObject.transform.localPosition = new Vector3(0f, 1.72f, 0.08f);

                SharedConfig = ScriptableObject.CreateInstance<MovementConfig>();
                SharedConfig.Configure(
                    6f,
                    24f,
                    0.35f,
                    -24f,
                    1.5f,
                    1.2f,
                    1.1f,
                    0.15f,
                    0.12f,
                    1.5f,
                    9f,
                    0.25f,
                    720f,
                    0.12f,
                    -2f,
                    0.12f,
                    -35f,
                    65f);
                FpsConfig = CreateFpsConfig();

                Motor.Configure(CharacterController);
                Driver.Configure(agent, CharacterController, initialState, 2f);
                visibility.Configure(Array.Empty<Renderer>());
                Controller.Configure(
                    Driver,
                    Motor,
                    FpsConfig,
                    SharedConfig,
                    cameraTargetObject.transform,
                    visibility,
                    1 << 0,
                    1 << 0);
                Root.SetActive(true);
                Physics.SyncTransforms();
            }

            public GameObject Root { get; }
            public GameObject Ground { get; }
            public CharacterController CharacterController { get; }
            public HybridControlDriver Driver { get; }
            public HybridPawnMotor Motor { get; }
            public FpsMovementController Controller { get; }
            public MovementConfig SharedConfig { get; }
            public FpsMovementConfig FpsConfig { get; }

            public void Settle()
            {
                for (int i = 0; i < 3; i++)
                {
                    Controller.Step(
                        CreateInput(Vector2.zero),
                        Vector3.forward,
                        Vector3.right,
                        0.02f);
                }

                Physics.SyncTransforms();
                Assert.That(Motor.ProbeGrounded(
                    SharedConfig.GroundProbeDistance,
                    1 << 0), Is.True);
            }

            public void AccelerateToSprint()
            {
                Controller.Step(
                    CreateInput(
                        Vector2.up,
                        sprintHeld: true,
                        sprintPressed: true),
                    Vector3.forward,
                    Vector3.right,
                    0.05f);
                for (int i = 0; i < 3; i++)
                {
                    Controller.Step(
                        CreateInput(Vector2.up, sprintHeld: true),
                        Vector3.forward,
                        Vector3.right,
                        0.05f);
                }

                Assert.That(Motor.PlanarVelocity.magnitude,
                    Is.GreaterThanOrEqualTo(FpsConfig.SprintLatchMinSpeed));
                Controller.Step(
                    CreateInput(Vector2.up, sprintReleased: true),
                    Vector3.forward,
                    Vector3.right,
                    0.05f);
                for (int i = 0; i < 3; i++)
                {
                    Controller.Step(
                        CreateInput(Vector2.up),
                        Vector3.forward,
                        Vector3.right,
                        0.05f);
                }

                Assert.That(Motor.PlanarVelocity.magnitude,
                    Is.GreaterThanOrEqualTo(FpsConfig.SlideMinSpeed));
                Assert.That(Controller.IsSprintLatched, Is.True);
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(Root);
                UnityEngine.Object.DestroyImmediate(Ground);
                UnityEngine.Object.DestroyImmediate(SharedConfig);
                UnityEngine.Object.DestroyImmediate(FpsConfig);
            }
        }
    }
}
