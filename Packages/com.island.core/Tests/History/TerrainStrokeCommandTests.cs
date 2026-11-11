using System;
using Island.Core.History;
using Island.Core.Terrain;
using NUnit.Framework;

namespace Island.Core.Tests.History
{
    [TestFixture]
    public class TerrainStrokeCommandTests
    {
        private Heightfield _field;

        [SetUp]
        public void SetUp() => _field = new Heightfield();

        /// <summary>Runs a stroke the way the sculpt tool will: open, apply for a while, close.</summary>
        private static TerrainStrokeCommand Stroke(Heightfield field, TerrainBrush brush,
                                                   int frames = 12, float seconds = 1f / 72f,
                                                   string label = "Raise ground")
        {
            var capture = TerrainStrokeCommand.Begin(field, label);
            var region = DirtyRegion.Empty;
            for (var i = 0; i < frames; i++) region = region.Union(TerrainSculptor.Apply(field, brush, seconds));
            return capture.Complete(region);
        }

        private static TerrainBrush Raise(float x = 0f, float z = 0f, float radius = 4f) =>
            new TerrainBrush(BrushKind.Raise, x, z, radius, strengthPerSecond: 2f);

        private float[] Snapshot() => _field.CopyHeights();

        private byte[] WeightSnapshot()
        {
            var resolution = _field.SampleResolution;
            var copy = new byte[resolution * resolution * IslandConstants.SurfaceChannelCount];
            var at = 0;
            for (var z = 0; z < resolution; z++)
                for (var x = 0; x < resolution; x++)
                    for (var c = 0; c < IslandConstants.SurfaceChannelCount; c++)
                        copy[at++] = _field.WeightAt(x, z, c);
            return copy;
        }

        // ---- the property that matters ---------------------------------------------------------

        [Test]
        public void UndoPutsEveryHeightBackExactly()
        {
            var before = Snapshot();
            var command = Stroke(_field, Raise());
            Assert.That(command, Is.Not.Null);
            Assert.That(_field.CopyHeights(), Is.Not.EqualTo(before), "the stroke must have done something");

            command.Undo();

            // Exactly, not nearly. Undo that leaves the ground almost where it was leaves a seam
            // where the restored region meets the ground around it — which is why this stores
            // floats rather than the 16-bit quantisation the save format uses.
            Assert.That(_field.CopyHeights(), Is.EqualTo(before));
        }

        [Test]
        public void RedoPutsEveryHeightBackExactly()
        {
            var command = Stroke(_field, Raise());
            var after = Snapshot();

            command.Undo();
            command.Redo();

            Assert.That(_field.CopyHeights(), Is.EqualTo(after));
        }

        [Test]
        public void UndoPutsEveryWeightBackExactly()
        {
            // Paint changes weights and not heights, and the blend renormalises so the four always
            // sum to 255. Replaying it backwards would not return the original proportions.
            var paint = new TerrainBrush(BrushKind.Paint, 0f, 0f, 4f, strengthPerSecond: 3f,
                                         surfaceChannel: IslandConstants.SurfaceChannelRock);
            var before = WeightSnapshot();
            var command = Stroke(_field, paint, label: "Paint rock");

            Assert.That(command, Is.Not.Null);
            Assert.That(WeightSnapshot(), Is.Not.EqualTo(before), "the paint must have done something");

            command.Undo();
            Assert.That(WeightSnapshot(), Is.EqualTo(before));
        }

        [Test]
        public void WeightsStillSumToTwoHundredAndFiftyFiveAfterUndo()
        {
            var paint = new TerrainBrush(BrushKind.Paint, 0f, 0f, 4f, 3f,
                                         IslandConstants.SurfaceChannelGrass);
            Stroke(_field, paint, label: "Paint grass").Undo();

            for (var z = 0; z < _field.SampleResolution; z += 7)
                for (var x = 0; x < _field.SampleResolution; x += 7)
                    Assert.That(_field.WeightSumAt(x, z), Is.EqualTo(255), $"at ({x}, {z})");
        }

        [Test]
        public void GoingBackAndForthManyTimesDoesNotDrift()
        {
            // Every cycle writes stored values rather than recomputing them, so there is nothing
            // to accumulate. This is the test that would catch it if that ever stopped being true.
            var before = Snapshot();
            var command = Stroke(_field, Raise());
            var after = Snapshot();

            for (var i = 0; i < 20; i++)
            {
                command.Undo();
                Assert.That(_field.CopyHeights(), Is.EqualTo(before), $"undo {i}");
                command.Redo();
                Assert.That(_field.CopyHeights(), Is.EqualTo(after), $"redo {i}");
            }
        }

        [Test]
        public void ARoundTripIsExactForAwkwardValues()
        {
            // Terrain that is not smooth, and includes the values floating point is worst at.
            var random = new Random(20261111);
            for (var z = 0; z < _field.SampleResolution; z++)
            {
                for (var x = 0; x < _field.SampleResolution; x++)
                {
                    var choice = random.Next(6);
                    var height =
                        choice == 0 ? 0f
                        : choice == 1 ? -0f
                        : choice == 2 ? IslandConstants.MinHeightMetres
                        : choice == 3 ? IslandConstants.MaxHeightMetres
                        : choice == 4 ? (float)(random.NextDouble() * 1e-7)
                        : (float)(random.NextDouble() * 20.0 - 10.0);
                    _field.SetHeight(x, z, height);
                }
            }

            var before = Snapshot();
            var command = Stroke(_field, Raise(radius: 8f));
            Assert.That(command, Is.Not.Null);

            command.Undo();
            var restored = _field.CopyHeights();
            for (var i = 0; i < before.Length; i++)
            {
                Assert.That(BitConverter.SingleToInt32Bits(restored[i]),
                    Is.EqualTo(BitConverter.SingleToInt32Bits(before[i])),
                    $"sample {i} came back as a different number");
            }
        }

        // ---- what it stores --------------------------------------------------------------------

        [Test]
        public void OnlyTheRegionTheStrokeTouchedIsStored()
        {
            var command = Stroke(_field, Raise(radius: 2f));
            var whole = _field.SampleResolution * _field.SampleResolution;

            Assert.That(command.SampleCount, Is.LessThan(whole / 4),
                "a small brush should not store the whole island");
            Assert.That(command.SampleCount, Is.GreaterThan(0));
        }

        [Test]
        public void GroundOutsideTheStrokeIsLeftAloneByUndo()
        {
            // The command writes a rectangle. If that rectangle were wrong, or wrongly placed,
            // undo would stamp stale values over ground the stroke never touched.
            _field.SetHeight(200, 200, 3.5f);
            var faraway = _field.HeightAt(200, 200);

            Stroke(_field, Raise(x: -20f, z: -20f, radius: 2f)).Undo();
            Assert.That(_field.HeightAt(200, 200), Is.EqualTo(faraway));
        }

        [Test]
        public void TheDeltaIsCompressed()
        {
            var command = Stroke(_field, Raise(radius: 8f));
            var raw = (long)command.SampleCount *
                      (sizeof(float) * 2 + IslandConstants.SurfaceChannelCount * 2);

            TestContext.WriteLine(
                $"{command.SampleCount:N0} samples: {raw:N0} bytes raw, {command.Bytes:N0} stored"
                + $" ({100L * command.Bytes / raw}%)");

            Assert.That(command.Bytes, Is.LessThan(raw),
                "a smooth region's floats share most of their bytes and should compress");
        }

        [Test]
        public void WhatItHoldsDoesNotChangeOverItsLife()
        {
            // The stack keeps a running total of bytes held, so a command whose size moved would
            // make that total drift until eviction stopped working.
            var command = Stroke(_field, Raise());
            var claimed = command.Bytes;
            command.Undo();
            command.Redo();
            Assert.That(command.Bytes, Is.EqualTo(claimed));
        }

        [Test]
        public void ItCarriesNoInstanceIdsBecauseTheGroundIsNotAnObject()
        {
            Assert.That(Stroke(_field, Raise()).Subjects, Is.Empty);
        }

        [Test]
        public void ItKeepsTheLabelItWasGiven()
        {
            Assert.That(Stroke(_field, Raise(), label: "Flatten for a path").Label,
                Is.EqualTo("Flatten for a path"));
        }

        // ---- the edges -------------------------------------------------------------------------

        [Test]
        public void AStrokeThatChangedNothingIsNotAStep()
        {
            // A tap that misses the island, or a flatten on ground already at the target. Putting
            // it in history would give the person an undo that appears to do nothing.
            var capture = TerrainStrokeCommand.Begin(_field, "Raise ground");
            Assert.That(capture.Complete(DirtyRegion.Empty), Is.Null);
        }

        [Test]
        public void AStrokeOverTheWholeIslandStillWorks()
        {
            var before = Snapshot();
            var capture = TerrainStrokeCommand.Begin(_field, "Raise everything");
            var region = DirtyRegion.Empty;

            // Walk the brush across the island so the dirty region covers all of it.
            for (var x = -32f; x <= 32f; x += 4f)
                for (var z = -32f; z <= 32f; z += 4f)
                    region = region.Union(TerrainSculptor.Apply(_field, Raise(x, z, 8f), 1f / 72f));

            var command = capture.Complete(region);
            Assert.That(command, Is.Not.Null);
            command.Undo();
            Assert.That(_field.CopyHeights(), Is.EqualTo(before));
        }

        [Test]
        public void ApplyingAReleasedCommandIsRefusedLoudly()
        {
            // Discard frees the blobs. Writing after that would silently stamp nothing, or throw
            // a null reference from somewhere unhelpful.
            var command = Stroke(_field, Raise());
            command.Discard();

            var error = Assert.Throws<InvalidOperationException>(() => command.Undo());
            Assert.That(error.Message, Does.Contain("left history"));
        }

        [Test]
        public void ReleasingTwiceIsHarmless()
        {
            var command = Stroke(_field, Raise());
            command.Discard();
            Assert.DoesNotThrow(() => command.Discard());
        }

        [Test]
        public void BeginningWithoutAFieldIsRefused()
        {
            Assert.Throws<ArgumentNullException>(() => TerrainStrokeCommand.Begin(null, "Raise"));
        }

        // ---- with the stack --------------------------------------------------------------------

        [Test]
        public void AStrokeGoesThroughTheStackAsOneStep()
        {
            var stack = new UndoStack();
            var before = Snapshot();

            stack.Record(Stroke(_field, Raise()));
            Assert.That(stack.UndoDepth, Is.EqualTo(1), "a stroke held for twelve frames is one step");
            Assert.That(stack.NextUndoLabel, Is.EqualTo("Raise ground"));

            Assert.That(stack.Undo(), Is.True);
            Assert.That(_field.CopyHeights(), Is.EqualTo(before));

            Assert.That(stack.Redo(), Is.True);
            Assert.That(_field.CopyHeights(), Is.Not.EqualTo(before));
        }

        [Test]
        public void SeveralStrokesUndoOneAtATimeInOrder()
        {
            var stack = new UndoStack();
            var start = Snapshot();

            stack.Record(Stroke(_field, Raise(x: -8f)));
            var afterFirst = Snapshot();
            stack.Record(Stroke(_field, Raise(x: 8f)));

            stack.Undo();
            Assert.That(_field.CopyHeights(), Is.EqualTo(afterFirst));
            stack.Undo();
            Assert.That(_field.CopyHeights(), Is.EqualTo(start));
        }

        [Test]
        public void StrokesBatchedTogetherUndoAsOne()
        {
            var stack = new UndoStack();
            var start = Snapshot();

            stack.BeginBatch();
            stack.Record(Stroke(_field, Raise(x: -8f)));
            stack.Record(Stroke(_field, Raise(x: 8f)));
            stack.EndBatch();

            Assert.That(stack.Undo(), Is.True);
            Assert.That(_field.CopyHeights(), Is.EqualTo(start), "both strokes went back together");
            Assert.That(stack.UndoDepth, Is.Zero);
        }

        [Test]
        public void HistoryStaysInsideItsMemoryBoundUnderAWorkingSession()
        {
            // Sixty strokes is a few minutes of sculpting. The bound has to hold without anyone
            // thinking about it, and the oldest strokes have to be released rather than merely
            // dropped.
            var stack = new UndoStack(maxCommands: 128, maxBytes: 256 * 1024);

            for (var i = 0; i < 60; i++)
            {
                var x = -24f + i % 12 * 4f;
                var z = -24f + i / 12 * 4f;
                var command = Stroke(_field, Raise(x, z, 6f), frames: 6);
                if (command != null) stack.Record(command);
            }

            Assert.That(stack.BytesHeld, Is.LessThanOrEqualTo(256 * 1024));
            Assert.That(stack.UndoDepth, Is.GreaterThan(0));
            Assert.That(stack.Undo(), Is.True, "the most recent stroke is always undoable");
        }
    }
}
