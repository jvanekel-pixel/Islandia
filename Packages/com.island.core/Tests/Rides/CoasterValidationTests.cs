using System;
using System.Collections.Generic;
using System.Linq;
using Island.Core.Rides;
using Island.Core.Terrain;
using Island.Core.World;
using NUnit.Framework;
using P = Island.Core.Rides.CoasterPieces;

namespace Island.Core.Tests.Rides
{
    /// <summary>
    /// ADR-25's rules, per piece. For each rule there is a layout that passes and one that breaks
    /// that rule alone, with the pieces it should name worked out by hand in the comments.
    /// </summary>
    [TestFixture]
    public class CoasterValidationTests
    {
        private const int U = CoasterShapes.SubUnitsPerUnit;

        /// <summary>A station in the island's far south-west corner, clear of every layout here.</summary>
        private static readonly StationSite Faraway = new StationSite(TrackDirection.West,
            new GridRect(8, 8, 56, 40), new GridRect(0, 20, 7, 28));

        private static readonly ICoasterGround Level = new FlatGround(0);

        private static Coaster Build(TrackPoint start, params TrackPiece[] pieces) =>
            Build(new TrackFrame(start, 0), pieces);

        private static Coaster Build(TrackFrame start, params TrackPiece[] pieces)
        {
            var coaster = new Coaster(1, start);
            foreach (var piece in pieces) coaster.Add(piece);
            return coaster;
        }

        /// <summary>
        /// Station north from the start, a lift up one unit, two wide right turns, a hill back
        /// down heading south, a straight, two wide right turns home. The far side is at
        /// x = start + 8 units; the wide turns bulge to z = −4 and +12 units.
        /// </summary>
        private static TrackPiece[] Rectangle(TrackPiece station = null, TrackPiece lift = null, TrackPiece straight = null) => new[]
        {
            station ?? P.Station,                       // 0: (0,0,0) → (0,0,4)
            lift ?? P.LiftHill,                         // 1: → (0,1,8)
            P.TurnLargeRight, P.TurnLargeRight,         // 2, 3: → (4,1,12) east, → (8,1,8) south
            P.HillDown,                                 // 4: → (8,0,4)
            straight ?? P.Straight,                     // 5: → (8,0,0)
            P.TurnLargeRight, P.TurnLargeRight,         // 6, 7: → (4,0,−4) west, → (0,0,0) north
        };

        /// <summary>
        /// A figure of eight whose crossing is <paramref name="over"/> units above the station:
        /// north up the lifts, round to the right, back west along z = 4 over the end of the
        /// station (pieces 7 and 8 meet above (0, 4)), then round to the left and home.
        /// </summary>
        private static TrackPiece[] FigureEight(int over) => new[]
        {
            P.Station,                                  // 0: (0,0,0) → (0,0,4)
            P.LiftHill,                                 // 1: → (0,1,8)
            over == 2 ? P.LiftHill : P.Straight,        // 2: → (0,over,12)
            P.TurnLargeRight, P.TurnLargeRight,         // 3, 4: → (4,·,16) east, → (8,·,12) south
            P.Straight,                                 // 5: → (8,·,8)
            P.TurnLargeRight,                           // 6: → (4,·,4) west
            P.Straight, P.Straight,                     // 7, 8: → (0,·,4) over the station, → (−4,·,4)
            over == 2 ? P.HillDown : P.Straight,        // 9: → (−8,1,4)
            P.TurnLargeLeft, P.TurnLargeLeft,           // 10, 11: → (−12,1,0) south, → (−8,1,−4) east
            P.HillDown,                                 // 12: → (−4,0,−4)
            P.TurnLargeLeft,                            // 13: → (0,0,0) north
        };

        private static CoasterValidation Check(Coaster coaster, ICoasterGround ground = null, StationSite station = null) =>
            CoasterValidator.Validate(coaster, ground ?? Level, station ?? Faraway);

        private static CoasterIssue[] Issues(params (int Piece, CoasterProblem Problem)[] expected) =>
            expected.Select(e => new CoasterIssue(e.Piece, e.Problem)).ToArray();

        private static int[] PiecesWith(CoasterValidation result, CoasterProblem problem) =>
            result.Issues.Where(i => i.Problem == problem).Select(i => i.Piece).Distinct().ToArray();

        // ---- layouts that pass ------------------------------------------------------------------

        [Test]
        public void TheRectangleAndTheFigureOfEightAreValid()
        {
            var rectangle = Build(new TrackPoint(0, 0, 0), Rectangle());
            Assert.That(rectangle.IsClosed, Is.True);
            Assert.That(rectangle.ExitOf(4).Position, Is.EqualTo(new TrackPoint(8 * U, 0, 4 * U)));
            Assert.That(Check(rectangle).Issues, Is.Empty);
            Assert.That(Check(rectangle, new HeightfieldGround(new Heightfield())).Issues, Is.Empty);

            var eight = Build(new TrackPoint(0, 0, 0), FigureEight(2));
            Assert.That(eight.IsClosed, Is.True);
            Assert.That(eight.EntryOf(8).Position, Is.EqualTo(new TrackPoint(0, 2 * U, 4 * U)));
            Assert.That(Check(eight).Issues, Is.Empty);
        }

        [Test]
        public void ValidatingTwiceGivesTheSameAnswerInTheSameOrder()
        {
            var coaster = Build(new TrackPoint(0, 0, 0), FigureEight(1));
            var first = Check(coaster).Issues.ToArray();
            Assert.That(Check(coaster).Issues, Is.EqualTo(first));
            Assert.That(first.Select(i => i.Piece), Is.Ordered);
        }

        // ---- closed ------------------------------------------------------------------------------

        [Test]
        public void AnOpenCircuitIsReportedAtItsLastPieceAlone()
        {
            var rectangle = Rectangle();
            var open = Build(new TrackPoint(0, 0, 0), rectangle.Take(rectangle.Length - 1).ToArray());
            Assert.That(Check(open).Issues, Is.EqualTo(Issues((6, CoasterProblem.Open))));
        }

        [Test]
        public void AnEndThatMeetsTheStartLeaningIsStillOpen()
        {
            // The rectangle begun at its first turn home, so the straight is last; a lean in its
            // place ends exactly on the start, banked one eighth.
            var rectangle = Rectangle(straight: P.TwistRight);
            var at = Build(new TrackPoint(0, 0, 0), Rectangle());
            var coaster = Build(at.EntryOf(6), rectangle.Skip(6).Concat(rectangle.Take(6)).ToArray());
            Assert.That(coaster.OpenEnd.Position, Is.EqualTo(coaster.Start.Position));
            Assert.That(coaster.OpenEnd.Bank, Is.EqualTo(1));
            Assert.That(Check(coaster).Issues, Is.EqualTo(Issues((7, CoasterProblem.Open))));
        }

        [Test]
        public void NoTrackAtAllIsOneIssueForTheCoaster()
        {
            var empty = new Coaster(1, new TrackFrame(new TrackPoint(0, 0, 0), 0));
            Assert.That(Check(empty).Issues, Is.EqualTo(Issues((-1, CoasterProblem.Empty))));
        }

        // ---- one station -------------------------------------------------------------------------

        [Test]
        public void NoStationIsReportedForTheCoaster()
        {
            var coaster = Build(new TrackPoint(0, 0, 0), Rectangle(station: P.Straight));
            Assert.That(Check(coaster).Issues, Is.EqualTo(Issues((-1, CoasterProblem.NoStation))));
        }

        [Test]
        public void ASecondStationIsReportedWhereItIs()
        {
            var coaster = Build(new TrackPoint(0, 0, 0), Rectangle(straight: P.Station));
            Assert.That(Check(coaster).Issues, Is.EqualTo(Issues((5, CoasterProblem.ExtraStation))));
        }

        // ---- a lift before the first drop --------------------------------------------------------

        [Test]
        public void AHillWithNoChainNamesTheFirstDrop()
        {
            var coaster = Build(new TrackPoint(0, 0, 0), Rectangle(lift: P.HillUp));
            Assert.That(Check(coaster).Issues, Is.EqualTo(Issues((4, CoasterProblem.NoLiftBeforeDrop))));
        }

        [Test]
        public void TheLiftIsLookedForInRideOrderFromTheStationRoundTheCircuit()
        {
            // The same rectangle begun at its hill: the station is piece 4 and the drop piece 0,
            // which comes after the lift (piece 5) when ridden.
            var rectangle = Rectangle();
            var at = Build(new TrackPoint(0, 0, 0), rectangle);
            var rotated = Build(at.EntryOf(4), rectangle.Skip(4).Concat(rectangle.Take(4)).ToArray());
            Assert.That(rotated.IsClosed, Is.True);
            Assert.That(rotated.PieceAt(4), Is.SameAs(P.Station));
            Assert.That(Check(rotated).Issues, Is.Empty);

            var noLift = Rectangle(lift: P.HillUp);
            var rotatedNoLift = Build(at.EntryOf(4), noLift.Skip(4).Concat(noLift.Take(4)).ToArray());
            Assert.That(Check(rotatedNoLift).Issues, Is.EqualTo(Issues((0, CoasterProblem.NoLiftBeforeDrop))));
        }

        [Test]
        public void AnOpenTrackIsReadOnlyForwardFromItsStation()
        {
            // A drop before the station is not "before the lift": the train never rides it.
            var coaster = Build(new TrackPoint(0, U, 0), P.HillDown, P.Station, P.LiftHill, P.HillDown);
            Assert.That(PiecesWith(Check(coaster), CoasterProblem.NoLiftBeforeDrop), Is.Empty);
            var unlifted = Build(new TrackPoint(0, U, 0), P.HillDown, P.Station, P.HillUp, P.HillDown);
            Assert.That(PiecesWith(Check(unlifted), CoasterProblem.NoLiftBeforeDrop), Is.EqualTo(new[] { 3 }));
        }

        [Test]
        public void ADipNeedsALiftThoughItEndsLevel()
        {
            // The rectangle with no climb: a straight for the lift and a dip (31 below, back to
            // level) for the hill down. It closes level; the dip is the first place it falls.
            // Started a unit up so the dip stays out of the ground.
            var rectangle = Rectangle(lift: P.Straight);
            rectangle[4] = P.BumpDown;
            var coaster = Build(new TrackPoint(0, U, 0), rectangle);
            Assert.That(coaster.IsClosed, Is.True);
            Assert.That(Check(coaster).Issues, Is.EqualTo(Issues((4, CoasterProblem.NoLiftBeforeDrop))));

            // With the lift back and the dip after the hill, nothing is wrong.
            var lifted = Rectangle(straight: P.BumpDown);
            Assert.That(Check(Build(new TrackPoint(0, U, 0), lifted)).Issues, Is.Empty);
        }

        [TestCase("dip")]
        [TestCase("bump")]
        [TestCase("loop-right")]
        [TestCase("loop-left")]
        public void APieceThatFallsAndComesBackNeedsALiftBeforeIt(string which)
        {
            // Each of these ends level with where it began, but somewhere along it the track goes
            // downhill: the dip at once, the bump after its crest, the loop after its top.
            var piece = which == "dip" ? P.BumpDown : which == "bump" ? P.BumpUp
                      : which == "loop-right" ? P.LoopRight : P.LoopLeft;
            Assert.That(piece.Exit.Y, Is.Zero);
            var unlifted = Check(Build(new TrackPoint(0, U, 0), P.Station, piece, P.Straight));
            Assert.That(PiecesWith(unlifted, CoasterProblem.NoLiftBeforeDrop), Is.EqualTo(new[] { 1 }));

            var lifted = Check(Build(new TrackPoint(0, U, 0), P.Station, P.LiftHill, piece, P.Straight));
            Assert.That(PiecesWith(lifted, CoasterProblem.NoLiftBeforeDrop), Is.Empty);
        }

        // ---- clearance ---------------------------------------------------------------------------

        [Test]
        public void ACrossingOneUnitOverTheStationCollidesAndTwoUnitsClear()
        {
            // Pieces 7 and 8 meet at (0, 1, 4) units, over the station's end (piece 0) and the
            // foot of the lift (piece 1). One unit (64) is less than reach 37 + headroom 64.
            var low = Build(new TrackPoint(0, 0, 0), FigureEight(1));
            Assert.That(low.IsClosed, Is.True);
            var expected = new[]
            {
                new CoasterIssue(0, CoasterProblem.Collides, 7), new CoasterIssue(0, CoasterProblem.Collides, 8),
                new CoasterIssue(1, CoasterProblem.Collides, 7), new CoasterIssue(1, CoasterProblem.Collides, 8),
                new CoasterIssue(7, CoasterProblem.Collides, 0), new CoasterIssue(7, CoasterProblem.Collides, 1),
                new CoasterIssue(8, CoasterProblem.Collides, 0), new CoasterIssue(8, CoasterProblem.Collides, 1),
            };
            Assert.That(Check(low).Issues, Is.EqualTo(expected));
            Assert.That(Check(Build(new TrackPoint(0, 0, 0), FigureEight(2))).Issues, Is.Empty);
        }

        [Test]
        public void ALoopsWayOutBesideItsWayInIsNotACollision()
        {
            // The pack builds the loop's exit one unit to the right of its entry, so the straight
            // before it and the straight after it (pieces 0 and 2) pass 64 sub-units apart.
            var coaster = Build(new TrackPoint(0, 0, 0), P.Straight, P.LoopRight, P.Straight, P.LoopRight, P.LoopRight, P.Straight);
            Assert.That(PiecesWith(Check(coaster), CoasterProblem.Collides), Is.Empty);
        }

        [Test]
        public void NeighboursAreComparedAwayFromTheEndTheyShare()
        {
            // A right loop then a left loop share their top: neighbours, colliding far from the
            // joint between them.
            var coaster = Build(new TrackPoint(0, 0, 0), P.LoopRight, P.LoopLeft);
            Assert.That(Check(coaster).Issues.Where(i => i.Problem == CoasterProblem.Collides), Is.EqualTo(new[]
            {
                new CoasterIssue(0, CoasterProblem.Collides, 1), new CoasterIssue(1, CoasterProblem.Collides, 0),
            }));
        }

        [Test]
        public void ACircuitThatGoesRoundTwiceRunsIntoItself()
        {
            // Eight tight right turns close, but 4..7 lie exactly on 0..3. Piece 7 and piece 0 are
            // neighbours across the start; 3 ends where 0 begins and 5 begins where 0 ends, which
            // is a collision for pieces that are not neighbours.
            var turn = P.TurnSmallRight;
            var round = Build(new TrackPoint(0, 0, 0), turn, turn, turn, turn, turn, turn, turn, turn);
            Assert.That(round.IsClosed, Is.True);
            var others = Check(round).At(0).Where(i => i.Problem == CoasterProblem.Collides).Select(i => i.Other);
            Assert.That(others, Is.EqualTo(new[] { 3, 4, 5 }));
        }

        // ---- the ground and supports -------------------------------------------------------------

        [Test]
        public void AMoundUnderTheFarStraightBuriesThatPieceAlone()
        {
            // Piece 5 runs south along x = 512 (sample 192) from z = 256 to 0 (samples 160 to 128).
            var field = new Heightfield();
            for (var gx = 190; gx <= 194; gx++)
                for (var gz = 140; gz <= 148; gz++)
                    field.SetHeight(gx, gz, 0.5f);
            var coaster = Build(new TrackPoint(0, 0, 0), Rectangle());
            Assert.That(Check(coaster, new HeightfieldGround(field)).Issues, Is.EqualTo(Issues((5, CoasterProblem.Buried))));

            // The same mound with X and Z swapped is under no track.
            var swapped = new Heightfield();
            for (var gx = 140; gx <= 148; gx++)
                for (var gz = 190; gz <= 194; gz++)
                    swapped.SetHeight(gx, gz, 0.5f);
            Assert.That(Check(coaster, new HeightfieldGround(swapped)).Issues, Is.Empty);
        }

        [Test]
        public void TrackLyingOnTheGroundIsNotBuriedAndASubUnitBelowIs()
        {
            var coaster = Build(new TrackPoint(0, 16, 0), Rectangle());
            Assert.That(Check(coaster, new FlatGround(16)).Issues, Is.Empty);
            var sunk = Check(coaster, new FlatGround(17));
            // Pieces 0, 4–7 have samples at 16; 1–3 climb, but 1 starts and 4 ends at 16 too.
            Assert.That(PiecesWith(sunk, CoasterProblem.Buried), Is.EqualTo(new[] { 0, 1, 4, 5, 6, 7 }));
        }

        [Test]
        public void SupportsReachTwelveUnitsAndNoFurther()
        {
            // Starting at 704, the lift's top and everything up there is at 768 — exactly twelve
            // units — and passes. One sub-unit higher, pieces 1–4 have samples at 769.
            Assert.That(Check(Build(new TrackPoint(0, 704, 0), Rectangle())).Issues, Is.Empty);
            var high = Check(Build(new TrackPoint(0, 705, 0), Rectangle()));
            Assert.That(high.Issues, Is.EqualTo(Issues(
                (1, CoasterProblem.SupportTooTall), (2, CoasterProblem.SupportTooTall),
                (3, CoasterProblem.SupportTooTall), (4, CoasterProblem.SupportTooTall))));
        }

        [Test]
        public void TheTopOfALoopNeedsNoSupport()
        {
            // The loop's supports are its four samples at each foot, the highest 32 above its
            // entry; its top is 256 above. So a loop entered at 736 stands; at 737 it does not.
            Assert.That(PiecesWith(Check(Build(new TrackPoint(0, 736, 0), P.LoopRight)), CoasterProblem.SupportTooTall), Is.Empty);
            Assert.That(PiecesWith(Check(Build(new TrackPoint(0, 737, 0), P.LoopRight)), CoasterProblem.SupportTooTall), Is.EqualTo(new[] { 0 }));
        }

        [Test]
        public void TrackBankedPastFortyFiveDegreesNeedsNoSupport()
        {
            // Four leans right roll the track over: 0→1, 1→2, 2→3, 3→4, then a straight at 4.
            // Only samples within one eighth of upright stand on supports: all of the first lean,
            // the first sample of the second, none after.
            var lean = P.TwistRight;
            var over = Build(new TrackPoint(0, 800, 0), lean, lean, lean, lean, P.Straight);
            Assert.That(over.OpenEnd.Bank, Is.EqualTo(4));
            Assert.That(PiecesWith(Check(over), CoasterProblem.SupportTooTall), Is.EqualTo(new[] { 0, 1 }));

            Assert.That(CoasterValidator.IsUpright(over, 1, 0), Is.True);
            Assert.That(CoasterValidator.IsUpright(over, 1, 1), Is.False);

            // All the way round: the fifth lean goes from 4 to 5, which is −3, and is no nearer
            // upright on the way; the seventh comes back within an eighth at its end.
            var round = Build(new TrackPoint(0, 800, 0), lean, lean, lean, lean, lean, lean, lean, lean, P.Straight);
            Assert.That(round.EntryOf(5).Bank, Is.EqualTo(-3));
            Assert.That(round.OpenEnd.Bank, Is.EqualTo(0));
            Assert.That(PiecesWith(Check(round), CoasterProblem.SupportTooTall), Is.EqualTo(new[] { 0, 1, 6, 7, 8 }));

            // Leaning left mirrors it; leaning back up brings the supports back.
            var left = Build(new TrackPoint(0, 800, 0), P.TwistLeft, P.TwistLeft, P.TwistLeft, P.TwistRight, P.TwistRight);
            Assert.That(PiecesWith(Check(left), CoasterProblem.SupportTooTall), Is.EqualTo(new[] { 0, 1, 4 }));
        }

        [Test]
        public void TrackPastTheIslandsEdgeIsOffIsland()
        {
            // The far side is at start + 512: the edge (1024) is on the island, 1025 is not.
            var ground = new HeightfieldGround(new Heightfield());
            Assert.That(Check(Build(new TrackPoint(512, 0, 0), Rectangle()), ground).Issues, Is.Empty);
            var over = Check(Build(new TrackPoint(513, 0, 0), Rectangle()), ground);
            Assert.That(over.Issues, Is.EqualTo(Issues(
                (3, CoasterProblem.OffIsland), (4, CoasterProblem.OffIsland),
                (5, CoasterProblem.OffIsland), (6, CoasterProblem.OffIsland))));
        }

        // ---- the station's reservation -----------------------------------------------------------

        /// <summary>
        /// Facing east: the footprint is samples x 200–240, z 128–160 (x 576–896, z 0–256 in
        /// sub-units), the corridor x 241–256, z 140–148 (x 904–1024, z 96–160).
        /// </summary>
        private static readonly StationSite East = new StationSite(TrackDirection.East,
            new GridRect(200, 128, 240, 160), new GridRect(241, 140, 256, 148));

        [Test]
        public void TrackReachingOverTheStationFootprintIsReported()
        {
            // The far side at x = 26 + 512 = 538 reaches to 538 + 37 = 575, short of 576; one
            // sub-unit east it touches, on the hill's last sample (z 256), the straight, and the
            // first sample of the turn after it (z 0).
            Assert.That(Check(Build(new TrackPoint(26, 0, 0), Rectangle()), station: East).Issues, Is.Empty);
            var touching = Check(Build(new TrackPoint(27, 0, 0), Rectangle()), station: East);
            Assert.That(touching.Issues, Is.EqualTo(Issues(
                (4, CoasterProblem.InStation), (5, CoasterProblem.InStation), (6, CoasterProblem.InStation))));
        }

        [Test]
        public void TrackCrossingTheTrainsCorridorIsReported()
        {
            // The far side at x = 960, between the footprint's end and the island's edge: only
            // the straight (z 0–256) crosses the corridor's z 96–160.
            var crossing = Check(Build(new TrackPoint(448, 0, 0), Rectangle()), station: East);
            Assert.That(crossing.Issues, Is.EqualTo(Issues((5, CoasterProblem.InTrainCorridor))));
        }

        [Test]
        public void TheReservationAgreesWithStationSitePermits()
        {
            foreach (var x in new[] { 26, 27, 448, 300 })
            {
                var coaster = Build(new TrackPoint(x, 0, 0), Rectangle());
                var flagged = Check(coaster, station: East).Issues
                    .Where(i => i.Problem == CoasterProblem.InStation || i.Problem == CoasterProblem.InTrainCorridor)
                    .Select(i => i.Piece).Distinct().ToArray();
                var refused = new List<int>();
                for (var i = 0; i < coaster.Count; i++)
                {
                    var reach = coaster.PieceAt(i).Shape.Reach;
                    var entry = coaster.EntryOf(i);
                    if (coaster.PieceAt(i).Samples.Select(entry.Place).Any(p => !East.Permits(
                            (p.X - reach) / 32f, (p.Z - reach) / 32f, (p.X + reach) / 32f, (p.Z + reach) / 32f)))
                        refused.Add(i);
                }
                Assert.That(flagged, Is.EqualTo(refused), $"start x {x}");
            }
        }

        // ---- reasons -----------------------------------------------------------------------------

        [Test]
        public void EveryProblemHasAPlainReason()
        {
            var reasons = new HashSet<string>(StringComparer.Ordinal);
            foreach (CoasterProblem problem in Enum.GetValues(typeof(CoasterProblem)))
            {
                var reason = new CoasterIssue(0, problem).Message;
                Assert.That(reason, Does.EndWith("."), problem.ToString());
                Assert.That(reason, Does.Not.Contain(problem.ToString()));
                Assert.That(reasons.Add(reason), Is.True, $"{problem} shares its reason");
            }
        }

        [Test]
        public void NothingIsValidatedWithoutGroundOrAStation()
        {
            var coaster = Build(new TrackPoint(0, 0, 0), Rectangle());
            Assert.Throws<ArgumentNullException>(() => CoasterValidator.Validate(null, Level, Faraway));
            Assert.Throws<ArgumentNullException>(() => CoasterValidator.Validate(coaster, null, Faraway));
            Assert.Throws<ArgumentNullException>(() => CoasterValidator.Validate(coaster, Level, null));
        }
    }
}
