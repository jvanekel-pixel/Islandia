using System;
using System.Collections.Generic;
using Island.Core.History;
using NUnit.Framework;

namespace Island.Core.Tests.History
{
    /// <summary>A command that records what was done to it, so the stack's behaviour is visible.</summary>
    internal sealed class SpyCommand : IUndoableCommand
    {
        private readonly List<string> _log;

        public SpyCommand(List<string> log, string label, long bytes = 10, params long[] subjects)
        {
            _log = log;
            Label = label;
            Bytes = bytes;
            Subjects = subjects ?? Array.Empty<long>();
        }

        public string Label { get; }
        public long Bytes { get; }
        public long[] Subjects { get; }

        public int Undos { get; private set; }
        public int Redos { get; private set; }
        public int Discards { get; private set; }

        public void Undo() { Undos++; _log.Add($"undo:{Label}"); }
        public void Redo() { Redos++; _log.Add($"redo:{Label}"); }
        public void Discard() { Discards++; _log.Add($"discard:{Label}"); }
    }

    [TestFixture]
    public class UndoStackTests
    {
        private List<string> _log;
        private UndoStack _stack;

        [SetUp]
        public void SetUp()
        {
            _log = new List<string>();
            _stack = new UndoStack();
        }

        private SpyCommand Record(string label, long bytes = 10, params long[] subjects)
        {
            var command = new SpyCommand(_log, label, bytes, subjects);
            _stack.Record(command);
            return command;
        }

        [Test]
        public void NothingToUndoIsNotAnError()
        {
            Assert.That(_stack.Undo(), Is.False);
            Assert.That(_stack.Redo(), Is.False);
            Assert.That(_stack.UndoDepth, Is.Zero);
            Assert.That(_stack.NextUndoLabel, Is.Null);
        }

        [Test]
        public void OneStepGoesBackAndForward()
        {
            var command = Record("raise");
            Assert.That(_stack.NextUndoLabel, Is.EqualTo("raise"));

            Assert.That(_stack.Undo(), Is.True);
            Assert.That(command.Undos, Is.EqualTo(1));
            Assert.That(_stack.UndoDepth, Is.Zero);
            Assert.That(_stack.RedoDepth, Is.EqualTo(1));

            Assert.That(_stack.Redo(), Is.True);
            Assert.That(command.Redos, Is.EqualTo(1));
            Assert.That(_stack.UndoDepth, Is.EqualTo(1));
        }

        [Test]
        public void StepsComeBackInReverseOrder()
        {
            Record("first");
            Record("second");
            Record("third");

            _stack.Undo();
            _stack.Undo();
            Assert.That(_log, Is.EqualTo(new[] { "undo:third", "undo:second" }));
        }

        [Test]
        public void RedoRepeatsInTheOrderItHappened()
        {
            Record("first");
            Record("second");
            _stack.Undo();
            _stack.Undo();
            _log.Clear();

            _stack.Redo();
            _stack.Redo();
            Assert.That(_log, Is.EqualTo(new[] { "redo:first", "redo:second" }));
        }

        [Test]
        public void DoingSomethingNewReleasesTheFutureItReplaced()
        {
            // The draft zeroed a redo counter and left the entries in its buffer, so the commands
            // were never released and, worse, a later walk could reach them again.
            var abandoned = Record("abandoned");
            _stack.Undo();
            Assert.That(abandoned.Discards, Is.Zero);

            Record("instead");
            Assert.That(abandoned.Discards, Is.EqualTo(1), "the abandoned future must be released");
            Assert.That(_stack.RedoDepth, Is.Zero);
            Assert.That(_stack.Redo(), Is.False);
        }

        [Test]
        public void MemoryHeldIsTheSumOfWhatTheCommandsClaim()
        {
            Record("a", bytes: 100);
            Record("b", bytes: 250);
            Assert.That(_stack.BytesHeld, Is.EqualTo(350));

            _stack.Undo();
            Assert.That(_stack.BytesHeld, Is.EqualTo(350), "an undone step is still held, for redo");

            _stack.Clear();
            Assert.That(_stack.BytesHeld, Is.Zero);
        }

        [Test]
        public void ClearingReleasesEverythingOnBothSides()
        {
            var undone = Record("undone");
            var done = Record("done");
            _stack.Undo();

            _stack.Clear();
            Assert.That(done.Discards, Is.EqualTo(1));
            Assert.That(undone.Discards, Is.EqualTo(1));
            Assert.That(_stack.UndoDepth, Is.Zero);
            Assert.That(_stack.RedoDepth, Is.Zero);
        }
    }

    [TestFixture]
    public class UndoStackBatchTests
    {
        private List<string> _log;
        private UndoStack _stack;

        [SetUp]
        public void SetUp()
        {
            _log = new List<string>();
            _stack = new UndoStack();
        }

        private void Record(string label, long bytes = 10) =>
            _stack.Record(new SpyCommand(_log, label, bytes));

        [Test]
        public void ABatchIsOneStep()
        {
            Record("before");
            _stack.BeginBatch();
            Record("a");
            Record("b");
            Record("c");
            _stack.EndBatch();

            Assert.That(_stack.Undo(), Is.True);
            Assert.That(_log, Is.EqualTo(new[] { "undo:c", "undo:b", "undo:a" }),
                "the whole batch goes back, innermost first");
            Assert.That(_stack.UndoDepth, Is.EqualTo(1), "only 'before' is left");
        }

        [Test]
        public void ABatchComesForwardAsOneStepToo()
        {
            _stack.BeginBatch();
            Record("a");
            Record("b");
            _stack.EndBatch();
            _stack.Undo();
            _log.Clear();

            Assert.That(_stack.Redo(), Is.True);
            Assert.That(_log, Is.EqualTo(new[] { "redo:a", "redo:b" }));
            Assert.That(_stack.RedoDepth, Is.Zero);
        }

        [Test]
        public void TwoBatchesDoNotRunTogether()
        {
            _stack.BeginBatch();
            Record("a1");
            Record("a2");
            _stack.EndBatch();
            _stack.BeginBatch();
            Record("b1");
            Record("b2");
            _stack.EndBatch();

            _stack.Undo();
            Assert.That(_log, Is.EqualTo(new[] { "undo:b2", "undo:b1" }));
            Assert.That(_stack.UndoDepth, Is.EqualTo(2), "the first batch is untouched");
        }

        [Test]
        public void UnbatchedStepsAreNotMistakenForOneBatch()
        {
            // Batch id 0 means "on its own". If that were treated as a shared id, a run of
            // ordinary steps would all vanish on one undo.
            Record("a");
            Record("b");
            Record("c");

            _stack.Undo();
            Assert.That(_log, Is.EqualTo(new[] { "undo:c" }));
            Assert.That(_stack.UndoDepth, Is.EqualTo(2));
        }

        [Test]
        public void BatchesNest()
        {
            // A tool that batches internally may be called by a tool that batches. The person
            // performed one action and should get one step.
            _stack.BeginBatch();
            Record("outer");
            _stack.BeginBatch();
            Record("inner");
            _stack.EndBatch();
            Record("outer again");
            _stack.EndBatch();

            _stack.Undo();
            Assert.That(_log, Is.EqualTo(new[] { "undo:outer again", "undo:inner", "undo:outer" }));
            Assert.That(_stack.UndoDepth, Is.Zero);
        }

        [Test]
        public void AnEmptyBatchRecordsNothing()
        {
            _stack.BeginBatch();
            _stack.EndBatch();
            Assert.That(_stack.UndoDepth, Is.Zero);
        }

        [Test]
        public void ClosingABatchNobodyOpenedIsAMistakeWorthHearingAbout()
        {
            Assert.Throws<InvalidOperationException>(() => _stack.EndBatch());
        }

        [Test]
        public void UndoingInsideAnOpenBatchIsRefused()
        {
            // It would record half an action and then take back the wrong amount of it.
            _stack.BeginBatch();
            Record("a");
            Assert.Throws<InvalidOperationException>(() => _stack.Undo());
            Assert.Throws<InvalidOperationException>(() => _stack.Redo());
            Assert.That(_stack.InBatch, Is.True);
        }
    }

    [TestFixture]
    public class UndoStackBoundsTests
    {
        private List<string> _log;

        [SetUp]
        public void SetUp() => _log = new List<string>();

        [Test]
        public void TheDefaultsAreTheOnesTheSpecAsksFor()
        {
            // The draft's constant said 256 while its own comment said 128.
            var stack = new UndoStack();
            Assert.That(stack.MaxCommands, Is.EqualTo(128));
            Assert.That(UndoStack.DefaultMaxCommands, Is.EqualTo(128));
        }

        [Test]
        public void TheOldestStepFallsOffWhenThereAreTooMany()
        {
            var stack = new UndoStack(maxCommands: 3, maxBytes: 1_000_000);
            var oldest = new SpyCommand(_log, "oldest");
            stack.Record(oldest);
            for (var i = 0; i < 3; i++) stack.Record(new SpyCommand(_log, $"newer{i}"));

            Assert.That(stack.UndoDepth, Is.EqualTo(3));
            Assert.That(oldest.Discards, Is.EqualTo(1),
                "a step that falls off the end must release what it owns");
        }

        [Test]
        public void TheOldestStepFallsOffWhenHistoryIsTooLarge()
        {
            var stack = new UndoStack(maxCommands: 1000, maxBytes: 300);
            var first = new SpyCommand(_log, "first", bytes: 200);
            stack.Record(first);
            stack.Record(new SpyCommand(_log, "second", bytes: 200));

            Assert.That(first.Discards, Is.EqualTo(1));
            Assert.That(stack.BytesHeld, Is.EqualTo(200));
            Assert.That(stack.UndoDepth, Is.EqualTo(1));
        }

        [Test]
        public void EvictionTakesAWholeBatchOrNoneOfIt()
        {
            // Half a batch would undo into a state the world was never in.
            var stack = new UndoStack(maxCommands: 3, maxBytes: 1_000_000);
            stack.BeginBatch();
            var a = new SpyCommand(_log, "a");
            var b = new SpyCommand(_log, "b");
            stack.Record(a);
            stack.Record(b);
            stack.EndBatch();

            stack.Record(new SpyCommand(_log, "c"));
            stack.Record(new SpyCommand(_log, "d"));

            Assert.That(a.Discards, Is.EqualTo(1));
            Assert.That(b.Discards, Is.EqualTo(1), "its partner went, so it must go too");
            Assert.That(stack.UndoDepth, Is.EqualTo(2));
        }

        [Test]
        public void TheStepJustTakenIsAlwaysUndoableEvenIfItAloneIsTooBig()
        {
            // Being unable to take back the thing you just did is worse than holding one
            // oversized delta. Refusing to record it is the same failure in a different coat.
            var stack = new UndoStack(maxCommands: 10, maxBytes: 100);
            var enormous = new SpyCommand(_log, "a very large stroke", bytes: 10_000);
            stack.Record(enormous);

            Assert.That(stack.UndoDepth, Is.EqualTo(1));
            Assert.That(enormous.Discards, Is.Zero);
            Assert.That(stack.Undo(), Is.True);
        }

        [Test]
        public void EvictionNeverEmptiesHistoryEntirely()
        {
            var stack = new UndoStack(maxCommands: 10, maxBytes: 100);
            stack.Record(new SpyCommand(_log, "big one", bytes: 10_000));
            stack.Record(new SpyCommand(_log, "another big one", bytes: 10_000));
            Assert.That(stack.UndoDepth, Is.EqualTo(1), "the newest survives, the older one goes");
        }

        [Test]
        public void ABoundOfZeroIsRefused()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new UndoStack(0, 1000));
            Assert.Throws<ArgumentOutOfRangeException>(() => new UndoStack(10, 0));
        }
    }

    [TestFixture]
    public class UndoStackForgettingTests
    {
        private List<string> _log;
        private UndoStack _stack;

        [SetUp]
        public void SetUp()
        {
            _log = new List<string>();
            _stack = new UndoStack();
        }

        [Test]
        public void HistoryKnowsWhichObjectsItMentions()
        {
            _stack.Record(new SpyCommand(_log, "move tree", 10, 42L));
            Assert.That(_stack.Touches(42L), Is.True);
            Assert.That(_stack.Touches(43L), Is.False);
        }

        [Test]
        public void NothingIsNotAnObject()
        {
            _stack.Record(new SpyCommand(_log, "a stroke", 10));
            Assert.That(_stack.Touches(Island.Core.Save.InstanceIds.None), Is.False);
        }

        [Test]
        public void DestroyingAnObjectDropsTheStepsThatMentionIt()
        {
            // Otherwise undo reaches for something that is no longer there.
            var mentions = new SpyCommand(_log, "move tree", 10, 42L);
            var unrelated = new SpyCommand(_log, "raise ground", 10);
            _stack.Record(mentions);
            _stack.Record(unrelated);

            _stack.Forget(42L);

            Assert.That(mentions.Discards, Is.EqualTo(1));
            Assert.That(unrelated.Discards, Is.Zero);
            Assert.That(_stack.UndoDepth, Is.EqualTo(1));
            Assert.That(_stack.Touches(42L), Is.False);
        }

        [Test]
        public void ForgettingReachesTheRedoBranchToo()
        {
            var mentions = new SpyCommand(_log, "delete tree", 10, 42L);
            _stack.Record(mentions);
            _stack.Undo();

            _stack.Forget(42L);
            Assert.That(mentions.Discards, Is.EqualTo(1));
            Assert.That(_stack.RedoDepth, Is.Zero);
        }

        [Test]
        public void ForgettingTakesTheWholeBatchWithIt()
        {
            var mentions = new SpyCommand(_log, "scatter one", 10, 42L);
            var partner = new SpyCommand(_log, "scatter two", 10, 43L);
            _stack.BeginBatch();
            _stack.Record(mentions);
            _stack.Record(partner);
            _stack.EndBatch();

            _stack.Forget(42L);
            Assert.That(partner.Discards, Is.EqualTo(1),
                "undoing the rest of the batch would give a state that never existed");
            Assert.That(_stack.UndoDepth, Is.Zero);
        }

        [Test]
        public void ForgettingSomethingHistoryNeverMentionedChangesNothing()
        {
            var command = new SpyCommand(_log, "raise ground", 10);
            _stack.Record(command);
            _stack.Forget(999L);
            Assert.That(command.Discards, Is.Zero);
            Assert.That(_stack.UndoDepth, Is.EqualTo(1));
        }

        [Test]
        public void EveryCommandIsReleasedExactlyOnce()
        {
            // Discard releases resources, so twice is as wrong as never.
            var commands = new List<SpyCommand>();
            for (var i = 0; i < 5; i++)
            {
                var command = new SpyCommand(_log, $"step{i}", 10, i);
                commands.Add(command);
                _stack.Record(command);
            }

            _stack.Undo();
            _stack.Undo();
            _stack.Forget(0L);
            _stack.Clear();

            foreach (var command in commands)
                Assert.That(command.Discards, Is.EqualTo(1), command.Label);
        }
    }

    [TestFixture]
    public class UndoStackNotificationTests
    {
        [Test]
        public void TheGaugeHearsAboutEveryChange()
        {
            var log = new List<string>();
            var stack = new UndoStack();
            var changes = 0;
            stack.Changed += () => changes++;

            stack.Record(new SpyCommand(log, "a"));
            Assert.That(changes, Is.EqualTo(1));
            stack.Undo();
            Assert.That(changes, Is.EqualTo(2));
            stack.Redo();
            Assert.That(changes, Is.EqualTo(3));
            stack.Clear();
            Assert.That(changes, Is.EqualTo(4));
        }

        [Test]
        public void NothingHappeningIsNotAChange()
        {
            var stack = new UndoStack();
            var changes = 0;
            stack.Changed += () => changes++;

            stack.Undo();
            stack.Redo();
            stack.Clear();
            stack.Forget(42L);
            Assert.That(changes, Is.Zero);
        }
    }
}
