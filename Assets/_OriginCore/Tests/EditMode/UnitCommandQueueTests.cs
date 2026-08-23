using System.Collections.Generic;
using NUnit.Framework;
using OriginCore.RTS.Commands;
using UnityEngine;

namespace OriginCore.Tests.EditMode
{
    public sealed class UnitCommandQueueTests
    {
        [Test]
        public void ShiftWaitingCapacityIsExactlyFiveAndSixthDoesNotOverwrite()
        {
            GameObject root = new GameObject("UnitCommandQueueCapacityTest");
            try
            {
                UnitCommandQueue queue = root.AddComponent<UnitCommandQueue>();
                List<IRtsCommand> starts = new List<IRtsCommand>();
                queue.CommandStarted += (owner, command) => starts.Add(command);

                FakeCommand current = new FakeCommand("Current");
                Assert.That(queue.TryIssue(current, false, out CommandRejectionReason firstReason),
                    Is.True);
                Assert.That(firstReason, Is.EqualTo(CommandRejectionReason.None));

                List<FakeCommand> waiting = new List<FakeCommand>();
                for (int i = 0; i < UnitCommandQueue.WaitingCapacity; i++)
                {
                    FakeCommand command = new FakeCommand("Waiting " + i);
                    waiting.Add(command);
                    Assert.That(queue.TryIssue(command, true, out CommandRejectionReason reason),
                        Is.True);
                    Assert.That(reason, Is.EqualTo(CommandRejectionReason.None));
                }

                FakeCommand sixthWaiting = new FakeCommand("Rejected sixth waiting");
                Assert.That(queue.TryIssue(
                    sixthWaiting,
                    true,
                    out CommandRejectionReason rejection), Is.False);
                Assert.That(rejection, Is.EqualTo(CommandRejectionReason.QueueFull));
                Assert.That(queue.WaitingCount, Is.EqualTo(UnitCommandQueue.WaitingCapacity));
                Assert.That(sixthWaiting.Status, Is.EqualTo(CommandStatus.Pending));
                Assert.That(sixthWaiting.BeginCount, Is.Zero);

                current.Succeed();
                queue.TickCommands(0f);
                Assert.That(queue.Current, Is.SameAs(waiting[0]));
                for (int i = 0; i < waiting.Count; i++)
                {
                    Assert.That(queue.Current, Is.SameAs(waiting[i]));
                    waiting[i].Succeed();
                    queue.TickCommands(0f);
                }

                Assert.That(queue.Current, Is.Null);
                Assert.That(queue.WaitingCount, Is.Zero);
                Assert.That(starts.Count, Is.EqualTo(1 + UnitCommandQueue.WaitingCapacity));
                Assert.That(starts.Contains(sixthWaiting), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void NonShiftReplacesAndStopUnconditionallyClearsCurrentAndWaiting()
        {
            GameObject root = new GameObject("UnitCommandQueueReplaceTest");
            try
            {
                UnitCommandQueue queue = root.AddComponent<UnitCommandQueue>();
                FakeCommand oldCurrent = new FakeCommand("Old current");
                FakeCommand oldWaiting = new FakeCommand("Old waiting");
                Assert.That(queue.TryIssue(oldCurrent, false, out _), Is.True);
                Assert.That(queue.TryIssue(oldWaiting, true, out _), Is.True);

                FakeCommand replacement = new FakeCommand("Replacement");
                Assert.That(queue.TryIssue(replacement, false, out _), Is.True);
                Assert.That(oldCurrent.Status, Is.EqualTo(CommandStatus.Cancelled));
                Assert.That(oldWaiting.Status, Is.EqualTo(CommandStatus.Cancelled));
                Assert.That(oldCurrent.CancelCount, Is.EqualTo(1));
                Assert.That(oldWaiting.CancelCount, Is.EqualTo(1));
                Assert.That(queue.Current, Is.SameAs(replacement));
                Assert.That(queue.WaitingCount, Is.Zero);

                FakeCommand replacementWaiting = new FakeCommand("Replacement waiting");
                Assert.That(queue.TryIssue(replacementWaiting, true, out _), Is.True);
                Assert.That(queue.StopAll(), Is.EqualTo(2));
                Assert.That(replacement.Status, Is.EqualTo(CommandStatus.Cancelled));
                Assert.That(replacementWaiting.Status, Is.EqualTo(CommandStatus.Cancelled));
                Assert.That(queue.Current, Is.Null);
                Assert.That(queue.WaitingCount, Is.Zero);
                Assert.That(queue.TotalCommandCount, Is.Zero);
                Assert.That(queue.StopAll(), Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TerminalCurrentAutomaticallyStartsNextCommand()
        {
            GameObject root = new GameObject("UnitCommandQueueAdvanceTest");
            try
            {
                UnitCommandQueue queue = root.AddComponent<UnitCommandQueue>();
                FakeCommand first = new FakeCommand("First", 1);
                FakeCommand second = new FakeCommand("Second", 1);
                Assert.That(queue.TryIssue(first, false, out _), Is.True);
                Assert.That(queue.TryIssue(second, true, out _), Is.True);

                queue.TickCommands(0.016f);
                Assert.That(first.Status, Is.EqualTo(CommandStatus.Succeeded));
                Assert.That(second.Status, Is.EqualTo(CommandStatus.Running));
                Assert.That(second.BeginCount, Is.EqualTo(1));
                Assert.That(queue.Current, Is.SameAs(second));

                queue.TickCommands(0.016f);
                Assert.That(second.Status, Is.EqualTo(CommandStatus.Succeeded));
                Assert.That(queue.Current, Is.Null);
                Assert.That(queue.TotalCommandCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void IndependentMoveCommandsDoNotShareMutableLifecycleState()
        {
            Vector3 destination = new Vector3(3f, 0f, 4f);
            MoveCommand first = new MoveCommand(destination);
            MoveCommand second = new MoveCommand(destination);

            Assert.That(first, Is.Not.SameAs(second));
            Assert.That(first.Destination, Is.EqualTo(second.Destination));
            first.Cancel();
            Assert.That(first.Status, Is.EqualTo(CommandStatus.Cancelled));
            Assert.That(second.Status, Is.EqualTo(CommandStatus.Pending));
        }

        private sealed class FakeCommand : IRtsCommand
        {
            private readonly int _ticksBeforeSuccess;

            public FakeCommand(string displayName, int ticksBeforeSuccess = int.MaxValue)
            {
                DisplayName = displayName;
                _ticksBeforeSuccess = ticksBeforeSuccess;
            }

            public string DisplayName { get; }
            public CommandStatus Status { get; private set; } = CommandStatus.Pending;
            public string FailureReason => string.Empty;
            public int BeginCount { get; private set; }
            public int TickCount { get; private set; }
            public int CancelCount { get; private set; }

            public void Begin(UnitCommandContext context)
            {
                BeginCount++;
                Status = CommandStatus.Running;
            }

            public void Tick(float deltaTime)
            {
                if (Status != CommandStatus.Running)
                {
                    return;
                }

                TickCount++;
                if (TickCount >= _ticksBeforeSuccess)
                {
                    Status = CommandStatus.Succeeded;
                }
            }

            public void Cancel()
            {
                if (Status.IsTerminal())
                {
                    return;
                }

                CancelCount++;
                Status = CommandStatus.Cancelled;
            }

            public void Succeed()
            {
                if (!Status.IsTerminal())
                {
                    Status = CommandStatus.Succeeded;
                }
            }
        }
    }
}
