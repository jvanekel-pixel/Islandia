using System;
using System.Collections.Generic;
using Island.Core.Rides;
using NUnit.Framework;

namespace Island.Core.Tests.Rides
{
    /// <summary>
    /// A coaster is a chain of frames composed exactly. These build long circuits and check
    /// that every piece starts precisely where the last ended, against a floating-point
    /// composition of the same pieces as rigid transforms.
    /// </summary>
    [TestFixture]
    public class CoasterTests
    {
        private const int U = CoasterShapes.SubUnitsPerUnit;

        private static readonly TrackFrame Origin = new TrackFrame(new TrackPoint(0, 0, 0), 0);

        private static Coaster Build(params TrackPiece[] pieces)
        {
            var coaster = new Coaster(1, Origin);
            foreach (var piece in pieces) coaster.Add(piece);
            return coaster;
        }

        // ---- snapping ---------------------------------------------------------------------------

        [Test]
        public void APiecePlacedAtTheOpenEndStartsExactlyThere()
        {
            var coaster = Build(CoasterPieces.Station, CoasterPieces.LiftHill, CoasterPieces.TurnSmallRight,
                                CoasterPieces.DropStart, CoasterPieces.DropEnd, CoasterPieces.LoopRight);
            for (var i = 1; i < coaster.Count; i++)
                Assert.That(coaster.EntryOf(i), Is.EqualTo(coaster.ExitOf(i - 1)));
            Assert.That(coaster.OpenEnd, Is.EqualTo(coaster.ExitOf(coaster.Count - 1)));

            // Station 4 forward, lift 4 forward and 1 up, a right turn of radius 2 to face +X,
            // then two units down over eight and a loop that steps one unit to the right (-Z).
            Assert.That(coaster.ExitOf(2), Is.EqualTo(new TrackFrame(new TrackPoint(2 * U, U, 10 * U), 1)));
            Assert.That(coaster.OpenEnd, Is.EqualTo(new TrackFrame(new TrackPoint(10 * U, -U, 9 * U), 1)));
        }

        [Test]
        public void FourRightTurnsComeBackToTheStart()
        {
            var turn = CoasterPieces.TurnSmallRight;
            var coaster = Build(turn, turn, turn);
            Assert.That(coaster.IsClosed, Is.False);
            coaster.Add(turn);
            Assert.That(coaster.IsClosed, Is.True);
            Assert.That(coaster.OpenEnd, Is.EqualTo(coaster.Start));
        }

        [Test]
        public void APieceThatDoesNotFitIsRefusedAndChangesNothing()
        {
            var coaster = Build(CoasterPieces.Straight, CoasterPieces.ClimbStart);
            var end = coaster.OpenEnd;
            Assert.That(end.Pitch, Is.EqualTo(1));

            // The pack has no straight at 45°: after a steep climb starts, only its end fits.
            Assert.That(coaster.CanAdd(CoasterPieces.Straight), Is.False);
            Assert.That(coaster.CanAdd(CoasterPieces.DropEnd), Is.False);
            Assert.Throws<InvalidOperationException>(() => coaster.Add(CoasterPieces.TurnSmallRight));
            Assert.That(coaster.Count, Is.EqualTo(2));
            Assert.That(coaster.OpenEnd, Is.EqualTo(end));

            Assert.That(coaster.CanAdd(CoasterPieces.ClimbEnd), Is.True);
            coaster.Add(CoasterPieces.ClimbEnd);
            Assert.That(coaster.OpenEnd.Pitch, Is.Zero);
            Assert.That(coaster.OpenEnd.Position, Is.EqualTo(new TrackPoint(0, 2 * U, 12 * U)));
        }

        [Test]
        public void ALeanCarriesThroughStraightsAndMustBeUndoneBeforeATurn()
        {
            var coaster = Build(CoasterPieces.TwistLeft, CoasterPieces.Straight);
            Assert.That(coaster.OpenEnd.Bank, Is.EqualTo(-1));
            Assert.That(coaster.CanAdd(CoasterPieces.TurnSmallRight), Is.False, "the pack has no banked turns");
            Assert.That(coaster.CanAdd(CoasterPieces.Station), Is.False, "a station is level");

            coaster.Add(CoasterPieces.TwistLeft);
            Assert.That(coaster.OpenEnd.Bank, Is.EqualTo(-2), "two leans put the track on its side");
            coaster.Add(CoasterPieces.TwistRight);
            coaster.Add(CoasterPieces.TwistRight);
            Assert.That(coaster.OpenEnd.Bank, Is.Zero);
            Assert.That(coaster.CanAdd(CoasterPieces.TurnSmallRight), Is.True);
        }

        // ---- editing the middle ----------------------------------------------------------------

        [Test]
        public void InsertingAPieceMovesEverythingAfterItAndRemovingItPutsThemBack()
        {
            var coaster = Build(CoasterPieces.Station, CoasterPieces.TurnSmallRight, CoasterPieces.Straight);
            var before = coaster.OpenEnd;
            var changes = 0;
            coaster.Changed += () => changes++;

            coaster.Insert(1, CoasterPieces.HillUp);
            Assert.That(coaster.PieceAt(1), Is.SameAs(CoasterPieces.HillUp));
            Assert.That(coaster.OpenEnd.Position, Is.EqualTo(before.Position + new TrackPoint(0, U, 4 * U)));
            for (var i = 1; i < coaster.Count; i++) Assert.That(coaster.EntryOf(i), Is.EqualTo(coaster.ExitOf(i - 1)));

            Assert.That(coaster.RemoveAt(1), Is.SameAs(CoasterPieces.HillUp));
            Assert.That(coaster.OpenEnd, Is.EqualTo(before));
            Assert.That(changes, Is.EqualTo(2));
        }

        [Test]
        public void AnEditThatWouldStrandALaterPieceIsRefused()
        {
            var coaster = Build(CoasterPieces.Straight, CoasterPieces.ClimbStart, CoasterPieces.ClimbEnd, CoasterPieces.Straight);
            var end = coaster.OpenEnd;

            Assert.That(coaster.CanRemoveAt(1), Is.False, "the climb's end would follow a level straight");
            Assert.Throws<InvalidOperationException>(() => coaster.RemoveAt(1));
            Assert.That(coaster.CanInsert(2, CoasterPieces.Straight), Is.False, "a straight cannot start at 45°");
            Assert.That(coaster.CanInsert(1, CoasterPieces.TwistLeft), Is.False, "the climb cannot start leaning");
            Assert.That(coaster.Count, Is.EqualTo(4));
            Assert.That(coaster.OpenEnd, Is.EqualTo(end));

            Assert.That(coaster.CanRemoveAt(3), Is.True);
            Assert.That(coaster.CanRemoveAt(0), Is.True);
        }

        // ---- the 200-piece circuit -------------------------------------------------------------

        /// <summary>
        /// Two hundred pieces using every shape in the set: four sides of 49 pieces whose climbs,
        /// shifts, loops and leans each cancel out, joined by four right-hand corners — two of
        /// them climbing and falling helices.
        /// </summary>
        internal static List<TrackPiece> TwoHundredPieceCircuit()
        {
            var side = new List<TrackPiece>
            {
                CoasterPieces.Straight,
                CoasterPieces.HillUp, CoasterPieces.HillDown,
                CoasterPieces.HillHalfUp, CoasterPieces.HillHalfDown,
                CoasterPieces.ShiftRight, CoasterPieces.ShiftLeft,
                CoasterPieces.LoopRight, CoasterPieces.LoopLeft,
                CoasterPieces.ClimbStart, CoasterPieces.ClimbEnd, CoasterPieces.DropStart, CoasterPieces.DropEnd,
                CoasterPieces.TwistLeft, CoasterPieces.Straight, CoasterPieces.StraightShort, CoasterPieces.TwistRight,
                CoasterPieces.BumpUp, CoasterPieces.BumpDown,
                CoasterPieces.TurnLargeRight, CoasterPieces.TurnLargeLeft, CoasterPieces.TurnLargeLeft, CoasterPieces.TurnLargeRight,
                CoasterPieces.TurnSmallLeft, CoasterPieces.TurnSmallRight, CoasterPieces.TurnSmallRight, CoasterPieces.TurnSmallLeft,
            };
            while (side.Count < 49) side.Add(side.Count % 2 == 0 ? CoasterPieces.Straight : CoasterPieces.StraightShort);

            var corners = new[]
            {
                CoasterPieces.HelixSmallRightUp, CoasterPieces.TurnSmallRight,
                CoasterPieces.HelixSmallRightDown, CoasterPieces.TurnSmallRight,
            };
            var circuit = new List<TrackPiece>();
            for (var s = 0; s < 4; s++)
            {
                var copy = new List<TrackPiece>(side);
                if (s == 0)
                {
                    copy[0] = CoasterPieces.Station;
                    copy[1] = CoasterPieces.LiftHill;
                }
                circuit.AddRange(copy);
                circuit.Add(corners[s]);
            }
            return circuit;
        }

        [Test]
        public void ATwoHundredPieceCircuitClosesExactly()
        {
            var start = new TrackFrame(new TrackPoint(-7 * U, 3 * U, 11 * U), 3);
            var coaster = new Coaster(9, start);
            foreach (var piece in TwoHundredPieceCircuit()) coaster.Add(piece);

            Assert.That(coaster.Count, Is.EqualTo(200));
            Assert.That(coaster.OpenEnd, Is.EqualTo(start), "integer frames close with no error at all");
            Assert.That(coaster.IsClosed, Is.True);
        }

        [Test]
        public void ComposingThePiecesAsRigidTransformsAgreesAtEveryPiece()
        {
            // An independent composition: each piece as the rigid transform from its entry frame
            // to its exit frame, multiplied out in doubles. It agrees with the integer frames at
            // every one of the 200 pieces, and ends well under a millimetre from the start.
            var start = new TrackFrame(new TrackPoint(5 * U, 0, -2 * U), 1);
            var coaster = new Coaster(3, start);
            var position = new[] { (double)start.Position.X, start.Position.Y, start.Position.Z };
            var rotation = Rotation(start.Heading, start.Pitch, start.Bank);

            foreach (var piece in TwoHundredPieceCircuit())
            {
                var at = coaster.OpenEnd;
                var entryBank = piece.RollsFreely ? at.Bank : piece.EntryBank;
                var exitBank = piece.RollsFreely ? at.Bank + piece.ExitBank - piece.EntryBank : piece.ExitBank;
                var entry = Rotation(0, piece.EntryPitch, entryBank);
                var exit = Rotation(piece.Turn, piece.ExitPitch, exitBank);
                var step = Apply(Transpose(entry), new double[] { piece.Exit.X, piece.Exit.Y, piece.Exit.Z });
                var turn = Multiply(Transpose(entry), exit);

                var moved = Apply(rotation, step);
                for (var k = 0; k < 3; k++) position[k] += moved[k];
                rotation = Multiply(rotation, turn);

                coaster.Add(piece);
                var frame = coaster.OpenEnd;
                Assert.That(position[0], Is.EqualTo(frame.Position.X).Within(1e-6), piece.Id);
                Assert.That(position[1], Is.EqualTo(frame.Position.Y).Within(1e-6), piece.Id);
                Assert.That(position[2], Is.EqualTo(frame.Position.Z).Within(1e-6), piece.Id);
                var expected = Rotation(frame.Heading, frame.Pitch, frame.Bank);
                for (var r = 0; r < 3; r++)
                    for (var c = 0; c < 3; c++)
                        Assert.That(rotation[r, c], Is.EqualTo(expected[r, c]).Within(1e-9), $"{piece.Id} orientation");
            }

            var gap = 0.0;
            for (var k = 0; k < 3; k++)
            {
                var d = (position[k] - new[] { start.Position.X, start.Position.Y, start.Position.Z }[k]) * CoasterPieces.MetresPerSubUnit;
                gap += d * d;
            }
            Assert.That(Math.Sqrt(gap), Is.LessThan(0.001), "metres between the last exit and the first entry");
        }

        // ---- a floating-point reference -------------------------------------------------------

        /// <summary>Columns right, up, forward: heading's quarter turns, then pitch, then bank (eighths).</summary>
        private static double[,] Rotation(int heading, int pitch, int bank)
        {
            double h = heading * Math.PI / 2, p = pitch * Math.PI / 4, b = bank * Math.PI / 4;
            var forward = new[] { Math.Sin(h) * Math.Cos(p), Math.Sin(p), Math.Cos(h) * Math.Cos(p) };
            var levelRight = new[] { Math.Cos(h), 0.0, -Math.Sin(h) };
            var levelUp = Cross(forward, levelRight);
            var up = new double[3];
            for (var i = 0; i < 3; i++) up[i] = levelUp[i] * Math.Cos(b) + levelRight[i] * Math.Sin(b);
            var right = Cross(up, forward);
            var m = new double[3, 3];
            for (var r = 0; r < 3; r++)
            {
                m[r, 0] = right[r];
                m[r, 1] = up[r];
                m[r, 2] = forward[r];
            }
            return m;
        }

        private static double[] Cross(double[] a, double[] b) =>
            new[] { a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0] };

        private static double[] Apply(double[,] m, double[] v)
        {
            var o = new double[3];
            for (var r = 0; r < 3; r++) o[r] = m[r, 0] * v[0] + m[r, 1] * v[1] + m[r, 2] * v[2];
            return o;
        }

        private static double[,] Multiply(double[,] a, double[,] b)
        {
            var o = new double[3, 3];
            for (var r = 0; r < 3; r++)
                for (var c = 0; c < 3; c++)
                    o[r, c] = a[r, 0] * b[0, c] + a[r, 1] * b[1, c] + a[r, 2] * b[2, c];
            return o;
        }

        private static double[,] Transpose(double[,] m)
        {
            var o = new double[3, 3];
            for (var r = 0; r < 3; r++)
                for (var c = 0; c < 3; c++)
                    o[r, c] = m[c, r];
            return o;
        }
    }
}
