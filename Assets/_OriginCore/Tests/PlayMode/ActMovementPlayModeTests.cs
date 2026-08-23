using System;
using System.Collections.Generic;
using NUnit.Framework;
using OriginCore.ACT;
using OriginCore.Gameplay;
using OriginCore.Input;
using UnityEngine;
using UnityEngine.AI;

namespace OriginCore.Tests.PlayMode
{
    public sealed class ActMovementPlayModeTests
    {
        [Test]
        public void CameraRelativeMovementUsesTheSuppliedViewBasis()
        {
            using (MovementHarness harness = new MovementHarness(HybridControlState.Manual))
            {
                harness.Settle();
                Vector3 start = harness.Root.transform.position;
                for (int i = 0; i < 6; i++)
                {
                    Assert.That(harness.Controller.Step(
                        CreateInput(Vector2.up),
                        Vector3.right,
                        Vector3.back,
                        0.05f), Is.True);
                }

                Vector3 displacement = harness.Root.transform.position - start;
                Assert.That(displacement.x, Is.GreaterThan(0.2f));
                Assert.That(Mathf.Abs(displacement.z), Is.LessThan(0.05f));
                Assert.That(harness.Motor.PlanarVelocity.x, Is.GreaterThan(0f));
            }
        }

        [Test]
        public void PendingMoveTakesOverAndMovesInTheSameSimulationFrame()
        {
            using (MovementHarness harness = new MovementHarness(HybridControlState.Autopilot))
            {
                GameObject serviceObject = new GameObject("P11_Possession_Test");
                try
                {
                    PossessionService possession = serviceObject.AddComponent<PossessionService>();
                    possession.Configure(0.1f);
                    possession.BindPawn(harness.Driver);
                    Assert.That(possession.PrepareDirectObservation(out string error), Is.True, error);
                    Assert.That(harness.Driver.State,
                        Is.EqualTo(HybridControlState.PendingManualOverride));

                    ActInputSnapshot input = CreateInput(Vector2.up);
                    InputSnapshot snapshot = InputSnapshot.FromAct(123, input);
                    Vector3 start = harness.Root.transform.position;
                    Assert.That(possession.TryHandleDirectInput(snapshot), Is.True);
                    Assert.That(harness.Controller.Step(
                        input,
                        Vector3.forward,
                        Vector3.right,
                        0.1f), Is.True);

                    Assert.That(harness.Driver.State, Is.EqualTo(HybridControlState.Manual));
                    Assert.That(possession.ManualOverrideCount, Is.EqualTo(1));
                    Assert.That(harness.Root.transform.position.z,
                        Is.GreaterThan(start.z));
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(serviceObject);
                }
            }
        }

        [Test]
        public void GroundJumpAllowsOneAirJumpAndLandingResetsIt()
        {
            using (MovementHarness harness = new MovementHarness(HybridControlState.Manual))
            {
                harness.Settle();
                harness.Controller.Step(
                    CreateInput(Vector2.zero, true),
                    Vector3.forward,
                    Vector3.right,
                    0.05f);
                Assert.That(harness.Controller.LastJumpKind, Is.EqualTo(ActJumpKind.Ground));
                Assert.That(harness.Motor.VerticalVelocity, Is.GreaterThan(0f));

                harness.Controller.Step(
                    CreateInput(Vector2.zero),
                    Vector3.forward,
                    Vector3.right,
                    0.08f);
                harness.Controller.Step(
                    CreateInput(Vector2.zero, true),
                    Vector3.forward,
                    Vector3.right,
                    0.02f);
                Assert.That(harness.Controller.LastJumpKind, Is.EqualTo(ActJumpKind.Air));
                Assert.That(harness.Controller.AirJumpsUsed, Is.EqualTo(1));

                harness.Controller.Step(
                    CreateInput(Vector2.zero, true),
                    Vector3.forward,
                    Vector3.right,
                    0.02f);
                Assert.That(harness.Controller.LastJumpKind, Is.EqualTo(ActJumpKind.None));
                Assert.That(harness.Controller.AirJumpsUsed, Is.EqualTo(1));
                harness.Controller.ClearInputBuffer();

                bool landed = false;
                for (int i = 0; i < 300; i++)
                {
                    harness.Controller.Step(
                        CreateInput(Vector2.zero),
                        Vector3.forward,
                        Vector3.right,
                        0.02f);
                    if (i > 5 && harness.Motor.IsGrounded)
                    {
                        landed = true;
                        break;
                    }
                }

                Assert.That(landed, Is.True, "The deterministic harness never landed.");
                Assert.That(harness.Controller.AirJumpsUsed, Is.Zero);
                harness.Controller.Step(
                    CreateInput(Vector2.zero, true),
                    Vector3.forward,
                    Vector3.right,
                    0.02f);
                Assert.That(harness.Controller.LastJumpKind, Is.EqualTo(ActJumpKind.Ground));
            }
        }

        [Test]
        public void CrouchStaysLowUntilTheStandingVolumeIsClear()
        {
            using (MovementHarness harness = new MovementHarness(HybridControlState.Manual))
            {
                harness.Settle();
                harness.Controller.Step(
                    CreateInput(Vector2.zero, false, true),
                    Vector3.forward,
                    Vector3.right,
                    0.02f);
                Assert.That(harness.Motor.IsCrouched, Is.True);
                Assert.That(harness.CharacterController.height,
                    Is.EqualTo(harness.Config.CrouchHeight).Within(0.001f));

                GameObject ceiling = harness.CreateObstacle(
                    "P11_LowRoof_Test",
                    harness.Root.transform.position + new Vector3(0f, 1.35f, 0f),
                    new Vector3(1.5f, 0.2f, 1.5f),
                    0);
                Physics.SyncTransforms();
                harness.Controller.Step(
                    CreateInput(Vector2.zero),
                    Vector3.forward,
                    Vector3.right,
                    0.02f);
                Assert.That(harness.Motor.IsCrouched, Is.True,
                    "A low roof must reject the stand request.");

                ceiling.transform.position += Vector3.up * 5f;
                Physics.SyncTransforms();
                harness.Controller.Step(
                    CreateInput(Vector2.zero),
                    Vector3.forward,
                    Vector3.right,
                    0.02f);
                Assert.That(harness.Motor.IsCrouched, Is.False);
                Assert.That(harness.CharacterController.height,
                    Is.EqualTo(harness.Motor.StandingHeight).Within(0.001f));
            }
        }

        [Test]
        public void AssistedJumpMovesAwayFromContactAndResetsTheAirJump()
        {
            using (MovementHarness harness = new MovementHarness(HybridControlState.Manual))
            {
                harness.Settle();
                harness.Controller.Step(
                    CreateInput(Vector2.zero, true),
                    Vector3.forward,
                    Vector3.right,
                    0.05f);
                harness.Controller.Step(
                    CreateInput(Vector2.zero),
                    Vector3.forward,
                    Vector3.right,
                    0.08f);
                harness.Controller.Step(
                    CreateInput(Vector2.zero, true),
                    Vector3.forward,
                    Vector3.right,
                    0.02f);
                Assert.That(harness.Controller.AirJumpsUsed, Is.EqualTo(1));

                int boostLayer = LayerMask.NameToLayer("BoostSurface");
                Assert.That(boostLayer, Is.GreaterThanOrEqualTo(0));
                Vector3 actorCenter = harness.Motor.WorldCenter;
                harness.CreateObstacle(
                    "P11_Boost_Test",
                    actorCenter + Vector3.left,
                    new Vector3(0.5f, 2f, 0.5f),
                    boostLayer);
                Physics.SyncTransforms();

                harness.Controller.Step(
                    CreateInput(Vector2.zero, true),
                    Vector3.forward,
                    Vector3.right,
                    0.02f);
                Assert.That(harness.Controller.LastJumpKind, Is.EqualTo(ActJumpKind.Boost));
                Assert.That(harness.Motor.PlanarVelocity.x, Is.GreaterThan(0f),
                    "The boost must travel from the contact point toward the pawn.");
                Assert.That(harness.Motor.VerticalVelocity, Is.GreaterThan(0f));
                Assert.That(harness.Controller.AirJumpsUsed, Is.Zero);
                Assert.That(harness.Controller.BoostCooldownRemaining, Is.GreaterThan(0f));
            }
        }

        [Test]
        public void JumpInputBufferExpiresAndConsumesExactlyOnce()
        {
            InputBuffer buffer = new InputBuffer();
            buffer.BufferJump(0.1f);
            buffer.Tick(0.04f);
            Assert.That(buffer.HasBufferedJump, Is.True);
            Assert.That(buffer.JumpRemaining, Is.EqualTo(0.06f).Within(0.0001f));
            Assert.That(buffer.TryConsumeJump(), Is.True);
            Assert.That(buffer.TryConsumeJump(), Is.False);

            buffer.BufferJump(0.05f);
            buffer.Tick(0.06f);
            Assert.That(buffer.HasBufferedJump, Is.False);
        }

        private static ActInputSnapshot CreateInput(
            Vector2 move,
            bool jumpPressed = false,
            bool crouchHeld = false)
        {
            InputButtonState jump = new InputButtonState(
                jumpPressed,
                jumpPressed,
                false);
            InputButtonState crouch = new InputButtonState(
                false,
                crouchHeld,
                !crouchHeld);
            InputButtonState empty = default;
            return new ActInputSnapshot(
                move,
                Vector2.zero,
                jump,
                crouch,
                empty,
                empty,
                empty,
                empty,
                empty,
                empty);
        }

        private sealed class MovementHarness : IDisposable
        {
            private readonly List<GameObject> _extraObjects = new List<GameObject>();

            public MovementHarness(HybridControlState initialState)
            {
                Ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Ground.name = "P11_Ground_Test";
                Ground.layer = 0;
                Ground.transform.position = new Vector3(0f, -0.25f, 0f);
                Ground.transform.localScale = new Vector3(20f, 0.5f, 20f);

                Root = new GameObject("P11_Pawn_Test");
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
                Controller = Root.AddComponent<ActMovementController>();

                GameObject cameraTargetObject = new GameObject("ActCameraTarget");
                cameraTargetObject.transform.SetParent(Root.transform, false);
                cameraTargetObject.transform.localPosition = new Vector3(0f, 1.45f, 0f);

                Config = ScriptableObject.CreateInstance<MovementConfig>();
                Config.Configure(
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

                Motor.Configure(CharacterController);
                Driver.Configure(agent, CharacterController, initialState, 2f);
                int boostLayer = LayerMask.NameToLayer("BoostSurface");
                LayerMask boostMask = boostLayer >= 0 ? 1 << boostLayer : 0;
                Controller.Configure(
                    Driver,
                    Motor,
                    Config,
                    cameraTargetObject.transform,
                    1 << 0,
                    1 << 0,
                    boostMask);
                Root.SetActive(true);
                Physics.SyncTransforms();
            }

            public GameObject Root { get; }
            public GameObject Ground { get; }
            public CharacterController CharacterController { get; }
            public HybridControlDriver Driver { get; }
            public HybridPawnMotor Motor { get; }
            public ActMovementController Controller { get; }
            public MovementConfig Config { get; }

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
                Assert.That(Motor.ProbeGrounded(Config.GroundProbeDistance, 1 << 0), Is.True);
            }

            public GameObject CreateObstacle(
                string name,
                Vector3 position,
                Vector3 scale,
                int layer)
            {
                GameObject obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                obstacle.name = name;
                obstacle.layer = layer;
                obstacle.transform.position = position;
                obstacle.transform.localScale = scale;
                _extraObjects.Add(obstacle);
                return obstacle;
            }

            public void Dispose()
            {
                for (int i = 0; i < _extraObjects.Count; i++)
                {
                    if (_extraObjects[i] != null)
                    {
                        UnityEngine.Object.DestroyImmediate(_extraObjects[i]);
                    }
                }

                UnityEngine.Object.DestroyImmediate(Root);
                UnityEngine.Object.DestroyImmediate(Ground);
                UnityEngine.Object.DestroyImmediate(Config);
            }
        }
    }
}
