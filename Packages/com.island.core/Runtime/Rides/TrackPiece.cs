using System;
using System.Collections.Generic;
using Island.Core.Content;

namespace Island.Core.Rides
{
    /// <summary>What a piece of track is for.</summary>
    public enum TrackRole : byte
    {
        Track = 0,
        /// <summary>Where riders board. A coaster has exactly one.</summary>
        Station = 1,
        /// <summary>Carries the chain that pulls the train up; one must come before the first drop.</summary>
        Lift = 2,
    }

    /// <summary>
    /// How strong a piece feels, from its geometry alone: the builder's palette and the gentle
    /// setting use it until RC-02 rates a whole coaster from its forces.
    /// </summary>
    public enum RideIntensity : byte
    {
        Gentle = 0,
        Lively = 1,
        Thrilling = 2,
    }

    /// <summary>How a pack model becomes a piece.</summary>
    public enum Placement : byte
    {
        AsIs = 0,
        /// <summary>Ridden from its exit to its entry: a right turn becomes a left, a climb a descent.</summary>
        Reversed = 1,
        /// <summary>Left for right, for shapes the pack has in one hand only.</summary>
        Mirrored = 2,
        MirroredReversed = 3,
    }

    /// <summary>
    /// One of the pack's models as measured: the frame at each open end, relative to the model's
    /// origin, and its centreline. Generated from <c>content/facts/rides/coaster-kit.json</c>.
    /// </summary>
    public sealed class TrackShape
    {
        public TrackShape(string name, string model, TrackPoint entryAt, int entryPitch, int entryBank,
                          TrackPoint exitAt, int exitHeading, int exitPitch, int exitBank,
                          int length, int reach, int[] samples, int[] supports)
        {
            if (samples == null || samples.Length < 6 || samples.Length % 3 != 0)
                throw new ArgumentException("A shape needs at least two centreline samples.", nameof(samples));
            Name = name;
            Model = model;
            EntryAt = entryAt;
            EntryPitch = entryPitch;
            EntryBank = entryBank;
            ExitAt = exitAt;
            ExitHeading = exitHeading;
            ExitPitch = exitPitch;
            ExitBank = exitBank;
            Length = length;
            Reach = reach;
            var points = new TrackPoint[samples.Length / 3];
            for (var i = 0; i < points.Length; i++) points[i] = new TrackPoint(samples[3 * i], samples[3 * i + 1], samples[3 * i + 2]);
            Samples = points;
            Supports = supports ?? Array.Empty<int>();
        }

        public string Name { get; }

        /// <summary>The model name after <c>coaster-&lt;style&gt;-</c>, the same in all six styles.</summary>
        public string Model { get; }

        public TrackPoint EntryAt { get; }
        public int EntryPitch { get; }
        public int EntryBank { get; }
        public TrackPoint ExitAt { get; }
        public int ExitHeading { get; }
        public int ExitPitch { get; }
        public int ExitBank { get; }

        /// <summary>Along the centreline, in sub-units.</summary>
        public int Length { get; }

        /// <summary>The furthest any of the six styles' geometry lies from the centreline, in sub-units.</summary>
        public int Reach { get; }

        /// <summary>The centreline from entry to exit, relative to the entry, no further apart than half a unit.</summary>
        public IReadOnlyList<TrackPoint> Samples { get; }

        /// <summary>The samples a support may stand under: all of them, except where the track is inverted.</summary>
        public IReadOnlyList<int> Supports { get; }
    }

    /// <summary>
    /// A piece a player can build with: a pack model in one of four placements, with a permanent id.
    /// </summary>
    /// <remarks>
    /// Everything here is relative to the piece's entry frame, turned by that frame's heading only
    /// (see <see cref="TrackFrame"/>). For <c>RC-03</c>: the model goes at
    /// <c>entry.Place(ModelOffset)</c>, turned <see cref="ModelTurn"/> quarter turns from the
    /// entry's heading, scaled −1 in X first when <see cref="ModelMirrored"/>, and — for a piece
    /// that <see cref="RollsFreely"/> — rolled by the entry's bank about its forward axis.
    /// </remarks>
    public sealed class TrackPiece
    {
        private TrackPiece() { }

        public string Id { get; private set; }

        /// <summary>CRC-32 of <see cref="Id"/>, the number a saved coaster stores.</summary>
        public uint NumericId { get; private set; }

        public string Name { get; private set; }
        public TrackShape Shape { get; private set; }
        public Placement Placed { get; private set; }
        public TrackRole Role { get; private set; }
        public RideIntensity Intensity { get; private set; }

        public int EntryPitch { get; private set; }
        public int EntryBank { get; private set; }

        /// <summary>Where the exit is, from the entry.</summary>
        public TrackPoint Exit { get; private set; }

        /// <summary>Quarter turns to the right between entry and exit.</summary>
        public int Turn { get; private set; }

        public int ExitPitch { get; private set; }
        public int ExitBank { get; private set; }

        /// <summary>
        /// Whether the whole centreline lies on the roll axis, so the piece fits at any bank and
        /// carries it through, adding its own lean. Straights and the lean pieces do.
        /// </summary>
        public bool RollsFreely { get; private set; }

        public int Length => Shape.Length;
        public IReadOnlyList<TrackPoint> Samples { get; private set; }
        public IReadOnlyList<int> Supports { get; private set; }

        public TrackPoint ModelOffset { get; private set; }
        public int ModelTurn { get; private set; }
        public bool ModelMirrored { get; private set; }

        /// <summary>Ends higher than it starts.</summary>
        public bool Climbs => Exit.Y > 0;

        /// <summary>Ends lower than it starts.</summary>
        public bool Drops => Exit.Y < 0;

        /// <summary>
        /// Goes downhill anywhere along it: some centreline sample is lower than the one before
        /// it. A dip, a bump and a loop all fall although each ends level, so a train needs a
        /// lift to have carried it up before any of them. Climbing and level pieces do not fall.
        /// </summary>
        public bool Falls { get; private set; }

        public static TrackPiece Place(TrackShape shape, string id, string name, Placement placed,
                                       TrackRole role, RideIntensity intensity)
        {
            if (shape == null) throw new ArgumentNullException(nameof(shape));
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("A piece needs an id.", nameof(id));

            var piece = new TrackPiece
            {
                Id = id,
                NumericId = CatalogueId.Of(id),
                Name = name,
                Shape = shape,
                Placed = placed,
                Role = role,
                Intensity = intensity,
                EntryPitch = TrackFrame.Eighths(shape.EntryPitch),
                EntryBank = TrackFrame.Eighths(shape.EntryBank),
                Exit = shape.ExitAt - shape.EntryAt,
                Turn = shape.ExitHeading & 3,
                ExitPitch = TrackFrame.Eighths(shape.ExitPitch),
                ExitBank = TrackFrame.Eighths(shape.ExitBank),
                Samples = shape.Samples,
                Supports = shape.Supports,
                ModelOffset = -shape.EntryAt,
            };
            if (placed == Placement.Mirrored || placed == Placement.MirroredReversed) piece.Mirror();
            if (placed == Placement.Reversed || placed == Placement.MirroredReversed) piece.Reverse();

            var onAxis = piece.Turn == 0 && piece.EntryPitch == 0 && piece.ExitPitch == 0 && role != TrackRole.Station;
            foreach (var sample in piece.Samples) onAxis &= sample.X == 0 && sample.Y == 0;
            piece.RollsFreely = onAxis;
            for (var k = 1; k < piece.Samples.Count; k++)
                piece.Falls |= piece.Samples[k].Y < piece.Samples[k - 1].Y;
            return piece;
        }

        private void Mirror()
        {
            Exit = Exit.Mirrored();
            Turn = (-Turn) & 3;
            EntryBank = TrackFrame.Eighths(-EntryBank);
            ExitBank = TrackFrame.Eighths(-ExitBank);
            Samples = Map(Samples, p => p.Mirrored(), false);
            ModelOffset = ModelOffset.Mirrored();
            ModelTurn = (-ModelTurn) & 3;
            ModelMirrored = !ModelMirrored;
        }

        /// <summary>
        /// Ridden backwards: the new entry is the old exit facing the other way, so pitch and bank
        /// change sign, and everything is re-expressed from there.
        /// </summary>
        private void Reverse()
        {
            var back = (-Turn - 2) & 3; // from the old entry's heading to the new one's, undone
            var oldExit = Exit;
            Samples = Map(Samples, p => (p - oldExit).Turned(back), true);
            var count = Samples.Count;
            var supports = new int[Supports.Count];
            for (var i = 0; i < supports.Length; i++) supports[supports.Length - 1 - i] = count - 1 - Supports[i];
            Supports = supports;
            ModelOffset = (ModelOffset - oldExit).Turned(back);
            ModelTurn = (ModelTurn + back) & 3;

            Exit = (-oldExit).Turned(back);
            Turn = (-Turn) & 3;
            var entryPitch = EntryPitch;
            var entryBank = EntryBank;
            EntryPitch = TrackFrame.Eighths(-ExitPitch);
            EntryBank = TrackFrame.Eighths(-ExitBank);
            ExitPitch = TrackFrame.Eighths(-entryPitch);
            ExitBank = TrackFrame.Eighths(-entryBank);
        }

        private static TrackPoint[] Map(IReadOnlyList<TrackPoint> points, Func<TrackPoint, TrackPoint> map, bool backwards)
        {
            var mapped = new TrackPoint[points.Count];
            for (var i = 0; i < mapped.Length; i++) mapped[backwards ? mapped.Length - 1 - i : i] = map(points[i]);
            return mapped;
        }

        public override string ToString() => Id;
    }
}
