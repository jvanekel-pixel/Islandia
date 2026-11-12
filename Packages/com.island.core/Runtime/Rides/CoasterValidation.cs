using System;
using System.Collections.Generic;
using Island.Core.World;

namespace Island.Core.Rides
{
    /// <summary>What can be wrong with a coaster (ADR-25). Each has one plain-spoken reason.</summary>
    public enum CoasterProblem : byte
    {
        /// <summary>No pieces at all. Reported for the coaster, not a piece.</summary>
        Empty = 1,
        /// <summary>The track does not come back to where it starts. Reported at the last piece.</summary>
        Open = 2,
        /// <summary>No station. Reported for the coaster, not a piece.</summary>
        NoStation = 3,
        /// <summary>Every station after the first, in ride order from piece 0.</summary>
        ExtraStation = 4,
        /// <summary>The first piece that falls, when no lift comes between the station and it.</summary>
        NoLiftBeforeDrop = 5,
        /// <summary>Within the clearance envelope of another piece; <see cref="CoasterIssue.Other"/> names it.</summary>
        Collides = 6,
        /// <summary>Some of the piece is below the ground.</summary>
        Buried = 7,
        /// <summary>A support would have to be taller than <see cref="CoasterClearance.MaxSupportHeight"/>.</summary>
        SupportTooTall = 8,
        /// <summary>Some of the piece is beyond the island's edge, where there is no ground.</summary>
        OffIsland = 9,
        /// <summary>Over the station's footprint, which is kept clear (ADR-24).</summary>
        InStation = 10,
        /// <summary>Over the train's corridor, which is kept clear (ADR-24).</summary>
        InTrainCorridor = 11,
    }

    /// <summary>One problem, where it is.</summary>
    public readonly struct CoasterIssue : IEquatable<CoasterIssue>
    {
        public CoasterIssue(int piece, CoasterProblem problem, int other = -1)
        {
            Piece = piece;
            Problem = problem;
            Other = other;
        }

        /// <summary>The piece to show it on, or −1 for the coaster as a whole.</summary>
        public int Piece { get; }

        public CoasterProblem Problem { get; }

        /// <summary>The piece this one runs into, for <see cref="CoasterProblem.Collides"/>; otherwise −1.</summary>
        public int Other { get; }

        /// <summary>The reason, in words the person building it would use.</summary>
        public string Message => CoasterValidator.Describe(Problem);

        public bool Equals(CoasterIssue other) => Piece == other.Piece && Problem == other.Problem && Other == other.Other;
        public override bool Equals(object obj) => obj is CoasterIssue other && Equals(other);
        public override int GetHashCode() => (Piece * 397) ^ ((int)Problem << 20) ^ Other;
        public override string ToString() => Other >= 0 ? $"piece {Piece}: {Problem} with {Other}" : $"piece {Piece}: {Problem}";
    }

    /// <summary>Everything wrong with a coaster, in a fixed order: by piece, then problem, then other piece.</summary>
    public sealed class CoasterValidation
    {
        internal CoasterValidation(List<CoasterIssue> issues)
        {
            issues.Sort((a, b) => a.Piece != b.Piece ? a.Piece.CompareTo(b.Piece)
                                : a.Problem != b.Problem ? a.Problem.CompareTo(b.Problem)
                                : a.Other.CompareTo(b.Other));
            Issues = issues;
        }

        public IReadOnlyList<CoasterIssue> Issues { get; }

        public bool IsValid => Issues.Count == 0;

        /// <summary>The problems shown on piece <paramref name="piece"/> (−1 for the coaster's own).</summary>
        public List<CoasterIssue> At(int piece)
        {
            var found = new List<CoasterIssue>();
            foreach (var issue in Issues)
                if (issue.Piece == piece) found.Add(issue);
            return found;
        }

        public bool Has(CoasterProblem problem)
        {
            foreach (var issue in Issues)
                if (issue.Problem == problem) return true;
            return false;
        }
    }

    /// <summary>
    /// The space a coaster's track and riders need, in sub-units. Planning figures, recorded in
    /// DECISIONS.md (2026-11-12); RC-03 and HG-R may revise them.
    /// </summary>
    public static class CoasterClearance
    {
        /// <summary>
        /// Two centrelines closer than this sideways are one track inside another: ¾ unit, 1.5 m.
        /// The pack's cars are 0.7 units wide, so two passing side by side need more than 0.7; the
        /// pack's own loop puts its way out one unit beside its way in, so it must be less.
        /// </summary>
        public const int Lateral = 48;

        /// <summary>
        /// Room for a seated rider above the structure: one unit, 2 m. Two centrelines closer than
        /// this plus the taller piece's reach vertically, and closer than <see cref="Lateral"/>
        /// sideways, collide. Symmetric, because riders hang below the track at the top of a loop.
        /// </summary>
        public const int Headroom = 64;

        /// <summary>
        /// Across a joint, the track itself is close to the track it continues: samples of two
        /// neighbouring pieces that are both this near their shared end are not compared. Two
        /// points on a smooth joint at up to 45° that are closer than <see cref="Lateral"/>
        /// sideways lie within Lateral·√2 of it together; twice Lateral leaves room for the curves.
        /// </summary>
        public const int JointRadius = 2 * Lateral;

        /// <summary>The tallest support column: twelve units, 24 m.</summary>
        public const int MaxSupportHeight = 12 * CoasterShapes.SubUnitsPerUnit;
    }

    /// <summary>
    /// Checks a coaster against ADR-25's rules and says, per piece, what is wrong and why.
    /// </summary>
    /// <remarks>
    /// Integers throughout, so the headset and the tests agree: positions are the pieces' own
    /// centreline samples placed by exact frames, ground comes from <see cref="ICoasterGround"/>
    /// in sub-units, and the station's reservation is asked with boxes whose corners are exact
    /// in metres. A coaster being built is unfinished most of the time; validation reports, it
    /// never refuses an edit.
    /// </remarks>
    public static class CoasterValidator
    {
        public static CoasterValidation Validate(Coaster coaster, ICoasterGround ground, StationSite station)
        {
            if (coaster == null) throw new ArgumentNullException(nameof(coaster));
            if (ground == null) throw new ArgumentNullException(nameof(ground));
            if (station == null) throw new ArgumentNullException(nameof(station));

            var issues = new List<CoasterIssue>();
            if (coaster.Count == 0)
            {
                issues.Add(new CoasterIssue(-1, CoasterProblem.Empty));
                return new CoasterValidation(issues);
            }

            var points = Place(coaster);
            CheckCircuit(coaster, issues);
            CheckClearance(coaster, points, issues);
            CheckGround(coaster, points, ground, issues);
            CheckReservation(coaster, points, station, issues);
            return new CoasterValidation(issues);
        }

        public static string Describe(CoasterProblem problem)
        {
            switch (problem)
            {
                case CoasterProblem.Empty: return "There is no track yet.";
                case CoasterProblem.Open: return "The track does not join up: this end has to meet the start.";
                case CoasterProblem.NoStation: return "A coaster needs a station, where riders get on.";
                case CoasterProblem.ExtraStation: return "A coaster has one station; this is a second.";
                case CoasterProblem.NoLiftBeforeDrop: return "The train falls here before a lift has carried it up.";
                case CoasterProblem.Collides: return "This runs into another part of the track.";
                case CoasterProblem.Buried: return "The track goes into the ground here.";
                case CoasterProblem.SupportTooTall: return "This is too high for supports to reach the ground.";
                case CoasterProblem.OffIsland: return "This goes past the edge of the island.";
                case CoasterProblem.InStation: return "This is over the station, which is kept clear.";
                case CoasterProblem.InTrainCorridor: return "This crosses the train's line, which is kept clear.";
                default: return problem.ToString();
            }
        }

        /// <summary>Every piece's centreline samples, in the coaster's grid.</summary>
        private static TrackPoint[][] Place(Coaster coaster)
        {
            var points = new TrackPoint[coaster.Count][];
            for (var i = 0; i < coaster.Count; i++)
            {
                var entry = coaster.EntryOf(i);
                var samples = coaster.PieceAt(i).Samples;
                var placed = new TrackPoint[samples.Count];
                for (var k = 0; k < placed.Length; k++) placed[k] = entry.Place(samples[k]);
                points[i] = placed;
            }
            return points;
        }

        // ---- closed, one station, a lift before the first drop -----------------------------------

        private static void CheckCircuit(Coaster coaster, List<CoasterIssue> issues)
        {
            var count = coaster.Count;
            if (!coaster.IsClosed) issues.Add(new CoasterIssue(count - 1, CoasterProblem.Open));

            var station = -1;
            for (var i = 0; i < count; i++)
            {
                if (coaster.PieceAt(i).Role != TrackRole.Station) continue;
                if (station < 0) station = i;
                else issues.Add(new CoasterIssue(i, CoasterProblem.ExtraStation));
            }
            if (station < 0) issues.Add(new CoasterIssue(-1, CoasterProblem.NoStation));

            // Ride order from the station (or the first piece), round the circuit if it is one.
            var from = station < 0 ? 0 : station;
            var steps = coaster.IsClosed ? count : count - from;
            for (var step = station < 0 ? 0 : 1; step < steps; step++)
            {
                var index = (from + step) % count;
                var piece = coaster.PieceAt(index);
                if (piece.Role == TrackRole.Lift) return;
                if (piece.Drops)
                {
                    issues.Add(new CoasterIssue(index, CoasterProblem.NoLiftBeforeDrop));
                    return;
                }
            }
        }

        // ---- clearance --------------------------------------------------------------------------

        private static void CheckClearance(Coaster coaster, TrackPoint[][] points, List<CoasterIssue> issues)
        {
            var count = coaster.Count;
            var boxes = new Box[count];
            for (var i = 0; i < count; i++) boxes[i] = Box.Around(points[i]);

            for (var i = 0; i < count; i++)
            {
                for (var j = i + 1; j < count; j++)
                {
                    var vertical = Math.Max(coaster.PieceAt(i).Shape.Reach, coaster.PieceAt(j).Shape.Reach) + CoasterClearance.Headroom;
                    if (!boxes[i].Near(boxes[j], CoasterClearance.Lateral, vertical)) continue;

                    // The ends two neighbours share: one, or both for a circuit of two pieces. The
                    // last piece meets the first wherever its end is on the start, closed or not:
                    // an end that arrives leaning is open, not a collision.
                    var joints = new List<TrackPoint>(2);
                    if (j == i + 1) joints.Add(coaster.EntryOf(j).Position);
                    if (i == 0 && j == count - 1 && coaster.OpenEnd.Position == coaster.Start.Position)
                        joints.Add(coaster.Start.Position);

                    if (Collide(points[i], points[j], joints, vertical))
                    {
                        issues.Add(new CoasterIssue(i, CoasterProblem.Collides, j));
                        issues.Add(new CoasterIssue(j, CoasterProblem.Collides, i));
                    }
                }
            }
        }

        private static bool Collide(TrackPoint[] a, TrackPoint[] b, List<TrackPoint> joints, int vertical)
        {
            const long lateral2 = (long)CoasterClearance.Lateral * CoasterClearance.Lateral;
            foreach (var p in a)
            {
                foreach (var q in b)
                {
                    long dx = p.X - q.X, dz = p.Z - q.Z;
                    if (dx * dx + dz * dz >= lateral2 || Math.Abs(p.Y - q.Y) >= vertical) continue;
                    if (AtJoint(p, q, joints)) continue;
                    return true;
                }
            }
            return false;
        }

        private static bool AtJoint(TrackPoint p, TrackPoint q, List<TrackPoint> joints)
        {
            const long radius2 = (long)CoasterClearance.JointRadius * CoasterClearance.JointRadius;
            foreach (var joint in joints)
                if (Distance2(p, joint) < radius2 && Distance2(q, joint) < radius2) return true;
            return false;
        }

        private static long Distance2(TrackPoint a, TrackPoint b)
        {
            long dx = a.X - b.X, dy = a.Y - b.Y, dz = a.Z - b.Z;
            return dx * dx + dy * dy + dz * dz;
        }

        private readonly struct Box
        {
            private Box(int minX, int minY, int minZ, int maxX, int maxY, int maxZ)
            {
                MinX = minX; MinY = minY; MinZ = minZ;
                MaxX = maxX; MaxY = maxY; MaxZ = maxZ;
            }

            private int MinX { get; }
            private int MinY { get; }
            private int MinZ { get; }
            private int MaxX { get; }
            private int MaxY { get; }
            private int MaxZ { get; }

            public static Box Around(TrackPoint[] points)
            {
                int minX = int.MaxValue, minY = int.MaxValue, minZ = int.MaxValue;
                int maxX = int.MinValue, maxY = int.MinValue, maxZ = int.MinValue;
                foreach (var p in points)
                {
                    minX = Math.Min(minX, p.X); maxX = Math.Max(maxX, p.X);
                    minY = Math.Min(minY, p.Y); maxY = Math.Max(maxY, p.Y);
                    minZ = Math.Min(minZ, p.Z); maxZ = Math.Max(maxZ, p.Z);
                }
                return new Box(minX, minY, minZ, maxX, maxY, maxZ);
            }

            public bool Near(Box other, int lateral, int vertical) =>
                MinX - other.MaxX < lateral && other.MinX - MaxX < lateral &&
                MinZ - other.MaxZ < lateral && other.MinZ - MaxZ < lateral &&
                MinY - other.MaxY < vertical && other.MinY - MaxY < vertical;
        }

        // ---- the ground and supports ------------------------------------------------------------

        private static void CheckGround(Coaster coaster, TrackPoint[][] points, ICoasterGround ground, List<CoasterIssue> issues)
        {
            for (var i = 0; i < coaster.Count; i++)
            {
                var piece = coaster.PieceAt(i);
                bool off = false, buried = false, tall = false;
                var supports = piece.Supports;
                var next = 0;
                for (var k = 0; k < points[i].Length; k++)
                {
                    var p = points[i][k];
                    var supported = false;
                    while (next < supports.Count && supports[next] < k) next++;
                    if (next < supports.Count && supports[next] == k) supported = IsUpright(coaster, i, k);

                    if (!ground.TryGetGround(p.X, p.Z, out var height))
                    {
                        off = true;
                        continue;
                    }
                    if (height > p.Y) buried = true;
                    else if (supported && p.Y - height > CoasterClearance.MaxSupportHeight) tall = true;
                }
                if (off) issues.Add(new CoasterIssue(i, CoasterProblem.OffIsland));
                if (buried) issues.Add(new CoasterIssue(i, CoasterProblem.Buried));
                if (tall) issues.Add(new CoasterIssue(i, CoasterProblem.SupportTooTall));
            }
        }

        /// <summary>
        /// Whether sample <paramref name="k"/> of piece <paramref name="i"/> is banked no more than
        /// 45° (one eighth), so a support can stand under it. The bank runs evenly from the piece's
        /// entry to its exit, compared in whole eighths times the number of steps. RC-03 stands
        /// columns where this and <see cref="TrackPiece.Supports"/> agree.
        /// </summary>
        public static bool IsUpright(Coaster coaster, int i, int k)
        {
            var sampleCount = coaster.PieceAt(i).Samples.Count;
            var entry = coaster.EntryOf(i).Bank;
            var exit = entry + TrackFrame.Eighths(coaster.ExitOf(i).Bank - entry);
            var steps = sampleCount - 1;
            var scaled = entry * (steps - k) + exit * k;
            return Math.Abs(scaled) <= steps;
        }

        // ---- the station's reservation ----------------------------------------------------------

        private static void CheckReservation(Coaster coaster, TrackPoint[][] points, StationSite station, List<CoasterIssue> issues)
        {
            for (var i = 0; i < coaster.Count; i++)
            {
                var reach = coaster.PieceAt(i).Shape.Reach;
                bool inStation = false, inCorridor = false;
                foreach (var p in points[i])
                {
                    float minX = CoasterSpace.MetresOf(p.X - reach), maxX = CoasterSpace.MetresOf(p.X + reach);
                    float minZ = CoasterSpace.MetresOf(p.Z - reach), maxZ = CoasterSpace.MetresOf(p.Z + reach);
                    inStation |= station.Footprint.Overlaps(minX, minZ, maxX, maxZ);
                    inCorridor |= station.Corridor.Overlaps(minX, minZ, maxX, maxZ);
                }
                if (inStation) issues.Add(new CoasterIssue(i, CoasterProblem.InStation));
                if (inCorridor) issues.Add(new CoasterIssue(i, CoasterProblem.InTrainCorridor));
            }
        }
    }
}
