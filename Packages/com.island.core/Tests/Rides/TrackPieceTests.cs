using System;
using System.Collections.Generic;
using Island.Core.Content;
using Island.Core.Rides;
using NUnit.Framework;

namespace Island.Core.Tests.Rides
{
    /// <summary>
    /// The piece set is the owner's pack, measured: these pin what the measuring found and check
    /// that reversing and mirroring a model into a piece keeps every piece self-consistent.
    /// </summary>
    [TestFixture]
    public class TrackPieceTests
    {
        private static TrackPoint P(int x, int y, int z) => new TrackPoint(x, y, z);

        private const int U = CoasterShapes.SubUnitsPerUnit;

        [Test]
        public void EveryPieceHasAPermanentIdThatFindsIt()
        {
            Assert.That(CoasterPieces.All.Length, Is.EqualTo(37));
            var numbers = new HashSet<uint>();
            foreach (var piece in CoasterPieces.All)
            {
                Assert.That(piece.Id.StartsWith("coaster.", StringComparison.Ordinal), Is.True, piece.Id);
                Assert.That(piece.NumericId, Is.EqualTo(CatalogueId.Of(piece.Id)));
                Assert.That(piece.NumericId, Is.Not.EqualTo(CatalogueId.None));
                Assert.That(numbers.Add(piece.NumericId), Is.True, $"{piece.Id} shares its number");
                Assert.That(CoasterPieces.Find(piece.NumericId), Is.SameAs(piece));
                Assert.That(CoasterPieces.Find(piece.Id), Is.SameAs(piece));
            }
            Assert.That(CoasterPieces.Find("COASTER.STATION"), Is.Null, "ids compare ordinally");
            Assert.That(CoasterPieces.Find(12345u), Is.Null);
        }

        [Test]
        public void TheFramesAreTheOnesMeasuredFromThePack()
        {
            // One unit forward is 64 sub-units; the tight turn has radius 2, the wide one 4.
            Assert.That(CoasterPieces.Straight.Exit, Is.EqualTo(P(0, 0, 4 * U)));
            Assert.That(CoasterPieces.StraightShort.Exit, Is.EqualTo(P(0, 0, U)));
            Assert.That(CoasterPieces.TurnSmallRight.Exit, Is.EqualTo(P(2 * U, 0, 2 * U)));
            Assert.That(CoasterPieces.TurnSmallRight.Turn, Is.EqualTo(1));
            Assert.That(CoasterPieces.TurnLargeRight.Exit, Is.EqualTo(P(4 * U, 0, 4 * U)));
            Assert.That(CoasterPieces.HelixSmallRightUp.Exit, Is.EqualTo(P(2 * U, U, 2 * U)));
            Assert.That(CoasterPieces.ShiftRight.Exit, Is.EqualTo(P(2 * U, 0, 4 * U)));
            Assert.That(CoasterPieces.HillUp.Exit, Is.EqualTo(P(0, U, 4 * U)));
            Assert.That(CoasterPieces.HillHalfUp.Exit, Is.EqualTo(P(0, U / 2, 4 * U)));
            Assert.That(CoasterPieces.BumpUp.Exit, Is.EqualTo(P(0, 0, 4 * U)));
            Assert.That(CoasterPieces.LoopRight.Exit, Is.EqualTo(P(U, 0, 0)), "the loop comes out one unit to the side");

            Assert.That((CoasterPieces.ClimbStart.EntryPitch, CoasterPieces.ClimbStart.ExitPitch), Is.EqualTo((0, 1)));
            Assert.That((CoasterPieces.ClimbEnd.EntryPitch, CoasterPieces.ClimbEnd.ExitPitch), Is.EqualTo((1, 0)));
            Assert.That((CoasterPieces.TwistLeft.EntryBank, CoasterPieces.TwistLeft.ExitBank), Is.EqualTo((0, -1)));
            Assert.That((CoasterPieces.TwistRight.EntryBank, CoasterPieces.TwistRight.ExitBank), Is.EqualTo((0, 1)));
        }

        [Test]
        public void ReversingAModelRidesItTheOtherWay()
        {
            Assert.That(CoasterPieces.TurnSmallLeft.Exit, Is.EqualTo(P(-2 * U, 0, 2 * U)));
            Assert.That(CoasterPieces.TurnSmallLeft.Turn, Is.EqualTo(3));
            Assert.That(CoasterPieces.HelixSmallLeftDown.Exit, Is.EqualTo(P(-2 * U, -U, 2 * U)));
            Assert.That(CoasterPieces.HillDown.Exit, Is.EqualTo(P(0, -U, 4 * U)));
            Assert.That((CoasterPieces.DropStart.EntryPitch, CoasterPieces.DropStart.ExitPitch), Is.EqualTo((0, -1)));
            Assert.That((CoasterPieces.DropEnd.EntryPitch, CoasterPieces.DropEnd.ExitPitch), Is.EqualTo((-1, 0)));
            Assert.That(CoasterPieces.DropStart.Exit, Is.EqualTo(P(0, -U, 4 * U)));
        }

        [Test]
        public void MirroringAModelSwapsLeftForRight()
        {
            Assert.That(CoasterPieces.HelixSmallLeftUp.Exit, Is.EqualTo(P(-2 * U, U, 2 * U)));
            Assert.That(CoasterPieces.HelixSmallLeftUp.Turn, Is.EqualTo(3));
            Assert.That(CoasterPieces.HelixSmallRightDown.Exit, Is.EqualTo(P(2 * U, -U, 2 * U)));
            Assert.That(CoasterPieces.HelixSmallRightDown.Turn, Is.EqualTo(1));
            Assert.That(CoasterPieces.ShiftLeft.Exit, Is.EqualTo(P(-2 * U, 0, 4 * U)));
            Assert.That(CoasterPieces.LoopLeft.Exit, Is.EqualTo(P(-U, 0, 0)));
            Assert.That(CoasterPieces.LoopLeft.ModelMirrored, Is.True);
            Assert.That(CoasterPieces.LoopRight.ModelMirrored, Is.False);
        }

        [Test]
        public void EveryCentrelineRunsFromTheEntryToTheExit()
        {
            foreach (var piece in CoasterPieces.All)
            {
                Assert.That(piece.Samples[0], Is.EqualTo(P(0, 0, 0)), piece.Id);
                Assert.That(piece.Samples[piece.Samples.Count - 1], Is.EqualTo(piece.Exit), piece.Id);
                for (var i = 1; i < piece.Samples.Count; i++)
                {
                    var step = piece.Samples[i] - piece.Samples[i - 1];
                    var squared = (long)step.X * step.X + (long)step.Y * step.Y + (long)step.Z * step.Z;
                    Assert.That(squared, Is.LessThanOrEqualTo((U / 2 + 1) * (U / 2 + 1)), $"{piece.Id}: samples {i - 1}-{i} too far apart");
                }
                Assert.That(piece.Supports, Is.Ordered.Ascending, piece.Id);
                foreach (var index in piece.Supports)
                    Assert.That(index, Is.InRange(0, piece.Samples.Count - 1), piece.Id);
                Assert.That(piece.Length, Is.GreaterThanOrEqualTo(Math.Abs(piece.Exit.Z)), piece.Id);
            }
        }

        [Test]
        public void TheModelLandsOnThePiece()
        {
            // RC-03 places the model by ModelOffset, ModelTurn and ModelMirrored. Whatever the
            // placement, the model's two measured ends must land on the piece's two ends.
            foreach (var piece in CoasterPieces.All)
            {
                var shape = piece.Shape;
                var reversed = piece.Placed == Placement.Reversed || piece.Placed == Placement.MirroredReversed;
                var start = ModelToPiece(piece, reversed ? shape.ExitAt : shape.EntryAt);
                var end = ModelToPiece(piece, reversed ? shape.EntryAt : shape.ExitAt);
                Assert.That(start, Is.EqualTo(P(0, 0, 0)), piece.Id);
                Assert.That(end, Is.EqualTo(piece.Exit), piece.Id);
            }
        }

        private static TrackPoint ModelToPiece(TrackPiece piece, TrackPoint model) =>
            piece.ModelOffset + (piece.ModelMirrored ? model.Mirrored() : model).Turned(piece.ModelTurn);

        [Test]
        public void OnlyTrackOnItsRollAxisFitsAtAnyBank()
        {
            var free = new List<string>();
            foreach (var piece in CoasterPieces.All) if (piece.RollsFreely) free.Add(piece.Id);
            Assert.That(free, Is.EquivalentTo(new[]
            {
                "coaster.straight", "coaster.straight.short", "coaster.twist.left", "coaster.twist.right",
            }), "the station is level, and a bump or a turn leans off its axis");
        }

        [Test]
        public void TheRolesAndIntensitiesSayWhatEachPieceIsFor()
        {
            var stations = 0;
            foreach (var piece in CoasterPieces.All)
            {
                if (piece.Role == TrackRole.Station)
                {
                    stations++;
                    Assert.That(piece.Exit.Y, Is.Zero, "a station is level");
                }
                if (piece.Role == TrackRole.Lift)
                {
                    Assert.That(piece.Climbs, Is.True, $"{piece.Id}: a lift climbs");
                    Assert.That(piece.Intensity, Is.EqualTo(RideIntensity.Gentle), $"{piece.Id}: a lift is slow");
                }
            }
            Assert.That(stations, Is.EqualTo(1));
            Assert.That(CoasterPieces.LoopRight.Intensity, Is.EqualTo(RideIntensity.Thrilling));
            Assert.That(CoasterPieces.TurnLargeRight.Intensity, Is.EqualTo(RideIntensity.Gentle));
            Assert.That(CoasterPieces.DropStart.Drops, Is.True);
        }

        [Test]
        public void QuarterTurnsAreExact()
        {
            var p = P(3, 5, 7);
            Assert.That(p.Turned(1), Is.EqualTo(P(7, 5, -3)), "a right turn takes forward to right");
            Assert.That(P(0, 0, 1).Turned(1), Is.EqualTo(P(1, 0, 0)));
            Assert.That(p.Turned(4), Is.EqualTo(p));
            Assert.That(p.Turned(-1), Is.EqualTo(p.Turned(3)));
            Assert.That(p.Turned(1).Turned(1).Turned(1).Turned(1), Is.EqualTo(p));
            Assert.That(TrackFrame.Eighths(9), Is.EqualTo(1));
            Assert.That(TrackFrame.Eighths(-5), Is.EqualTo(3));
            Assert.That(TrackFrame.Eighths(4), Is.EqualTo(4));
        }

        private static readonly Placement[] EveryPlacement =
            { Placement.AsIs, Placement.Reversed, Placement.Mirrored, Placement.MirroredReversed };

        /// <summary>
        /// Every piece, in every placement, ends where its own measured centreline ends and leaves
        /// the way that centreline leaves. The exit data and the samples are transformed by separate
        /// code, so a mistake in mirroring or reversing one shows up against the other — which the
        /// four-fold circuit cannot see, because four quarter-turned copies of any step cancel.
        /// </summary>
        [Test]
        public void EveryPlacedPieceEndsWhereItsOwnCentrelineEnds()
        {
            var checkedCount = 0;
            foreach (var catalogued in CoasterPieces.All)
            foreach (var placement in EveryPlacement)
            {
                var piece = TrackPiece.Place(catalogued.Shape, "test." + catalogued.Id, catalogued.Name,
                    placement, catalogued.Role, catalogued.Intensity);
                var label = $"{catalogued.Shape.Name} {placement}";
                var samples = piece.Samples;
                Assert.That(samples.Count, Is.GreaterThanOrEqualTo(2), label);
                Assert.That(samples[0], Is.EqualTo(P(0, 0, 0)), label + ": starts at its entry");
                Assert.That(samples[samples.Count - 1], Is.EqualTo(piece.Exit), label + ": ends at its exit");

                // Leaves the way its last stretch of centreline points.
                var d = samples[samples.Count - 1] - samples[samples.Count - 2];
                if (d.X != 0 || d.Z != 0)
                {
                    var heading = Math.Abs(d.Z) >= Math.Abs(d.X) ? (d.Z > 0 ? 0 : 2) : (d.X > 0 ? 1 : 3);
                    Assert.That(piece.Turn, Is.EqualTo(heading), label + ": exit heading");
                    var run = Math.Max(Math.Abs(d.X), Math.Abs(d.Z));
                    if (piece.ExitPitch == 0) Assert.That(Math.Abs(d.Y) * 4, Is.LessThanOrEqualTo(run), label + ": leaves level");
                    else Assert.That(Math.Sign(d.Y), Is.EqualTo(Math.Sign(piece.ExitPitch)), label + ": leaves climbing or falling");
                }

                // And sets off along +Z, the entry frame's own heading.
                var e = samples[1] - samples[0];
                if (e.X != 0 || e.Z != 0)
                {
                    Assert.That(Math.Abs(e.Z), Is.GreaterThanOrEqualTo(Math.Abs(e.X)), label + ": sets off straight ahead");
                    Assert.That(e.Z, Is.GreaterThan(0), label + ": sets off forwards");
                    if (piece.EntryPitch != 0) Assert.That(Math.Sign(e.Y), Is.EqualTo(Math.Sign(piece.EntryPitch)), label + ": sets off climbing or falling");
                }
                checkedCount++;
            }
            Assert.That(checkedCount, Is.EqualTo(37 * 4));
        }
    }
}
