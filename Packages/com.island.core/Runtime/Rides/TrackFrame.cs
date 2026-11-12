using System;

namespace Island.Core.Rides
{
    /// <summary>
    /// A point on a coaster, in whole sub-units of the coaster pack's grid.
    /// </summary>
    /// <remarks>
    /// One pack unit is <see cref="CoasterShapes.SubUnitsPerUnit"/> sub-units. Every frame the
    /// pack's pieces produce lands on a sub-unit exactly, so track positions are integers and
    /// composing two hundred pieces adds integers two hundred times: nothing to round, nothing
    /// to drift, and the same answer under CoreCLR, Mono and IL2CPP. Axes are Unity's: +X right
    /// (east), +Y up, +Z forward (north).
    /// </remarks>
    public readonly struct TrackPoint : IEquatable<TrackPoint>
    {
        public TrackPoint(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public int X { get; }
        public int Y { get; }
        public int Z { get; }

        public static TrackPoint operator +(TrackPoint a, TrackPoint b) => new TrackPoint(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static TrackPoint operator -(TrackPoint a, TrackPoint b) => new TrackPoint(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static TrackPoint operator -(TrackPoint a) => new TrackPoint(-a.X, -a.Y, -a.Z);
        public static bool operator ==(TrackPoint a, TrackPoint b) => a.Equals(b);
        public static bool operator !=(TrackPoint a, TrackPoint b) => !a.Equals(b);

        /// <summary>
        /// This point turned about the vertical by whole quarter turns to the right: one turn takes
        /// +Z (forward) to +X (right). Exact, because a quarter turn only swaps and negates.
        /// </summary>
        public TrackPoint Turned(int quarterTurns)
        {
            switch (quarterTurns & 3)
            {
                case 1: return new TrackPoint(Z, Y, -X);
                case 2: return new TrackPoint(-X, Y, -Z);
                case 3: return new TrackPoint(-Z, Y, X);
                default: return this;
            }
        }

        /// <summary>Left for right.</summary>
        public TrackPoint Mirrored() => new TrackPoint(-X, Y, Z);

        public bool Equals(TrackPoint other) => X == other.X && Y == other.Y && Z == other.Z;
        public override bool Equals(object obj) => obj is TrackPoint other && Equals(other);
        public override int GetHashCode() => (X * 73856093) ^ (Y * 19349663) ^ (Z * 83492791);
        public override string ToString() => $"({X}, {Y}, {Z})";
    }

    /// <summary>
    /// Where a piece of track begins or ends, and which way it faces: position, heading, pitch
    /// and bank. Frames are composed, never integrated (ADR-25).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The position is the underside of the track's spine, the line every piece in the pack
    /// rolls about, so a banked end stays on the grid. Heading is whole quarter turns to the
    /// right of +Z; the pack has no diagonal track. Pitch and bank are eighth turns: pitch
    /// raises the nose (the steep slopes end at +1, 45°), bank lowers the right rail (the lean
    /// pieces reach ±1). The orientation is the heading's turn, then the pitch, then the bank.
    /// </para>
    /// <para>
    /// A piece's exit is stored relative to its entry turned only by the heading, the way the
    /// pack's models are built. A piece fits where the frame's pitch and bank are the ones its
    /// entry expects — or, for a piece whose whole centreline lies on its roll axis (straights
    /// and leans), at any bank, which it carries through. Under those rules composing a piece
    /// is a quarter-turn rotation and an integer add, and agrees exactly with composing the
    /// pieces as rigid transforms, which a test checks against a floating-point reference.
    /// </para>
    /// </remarks>
    public readonly struct TrackFrame : IEquatable<TrackFrame>
    {
        public TrackFrame(TrackPoint position, int heading, int pitch = 0, int bank = 0)
        {
            Position = position;
            Heading = heading & 3;
            Pitch = Eighths(pitch);
            Bank = Eighths(bank);
        }

        public TrackPoint Position { get; }

        /// <summary>Quarter turns to the right of +Z, 0 to 3.</summary>
        public int Heading { get; }

        /// <summary>Eighth turns, nose up positive, −3 to 4.</summary>
        public int Pitch { get; }

        /// <summary>Eighth turns, right rail down positive, −3 to 4.</summary>
        public int Bank { get; }

        /// <summary>Whether <paramref name="piece"/> can start here.</summary>
        public bool Accepts(TrackPiece piece)
        {
            if (piece == null) throw new ArgumentNullException(nameof(piece));
            return Pitch == piece.EntryPitch && (piece.RollsFreely || Bank == piece.EntryBank);
        }

        /// <summary>The frame at the far end of <paramref name="piece"/> placed here.</summary>
        public TrackFrame Then(TrackPiece piece)
        {
            if (!Accepts(piece))
            {
                throw new InvalidOperationException(
                    $"{piece.Name} starts at pitch {piece.EntryPitch} and bank {piece.EntryBank}; this end is at pitch {Pitch}, bank {Bank}.");
            }
            var bank = piece.RollsFreely ? Bank + piece.ExitBank - piece.EntryBank : piece.ExitBank;
            return new TrackFrame(Position + piece.Exit.Turned(Heading), Heading + piece.Turn, piece.ExitPitch, bank);
        }

        /// <summary>A point given relative to this frame's heading, in the coaster's own space.</summary>
        public TrackPoint Place(TrackPoint local) => Position + local.Turned(Heading);

        /// <summary>An angle in eighth turns, brought into −3 to 4.</summary>
        public static int Eighths(int value)
        {
            var wrapped = ((value % 8) + 8) % 8;
            return wrapped > 4 ? wrapped - 8 : wrapped;
        }

        public static bool operator ==(TrackFrame a, TrackFrame b) => a.Equals(b);
        public static bool operator !=(TrackFrame a, TrackFrame b) => !a.Equals(b);

        public bool Equals(TrackFrame other) =>
            Position == other.Position && Heading == other.Heading && Pitch == other.Pitch && Bank == other.Bank;

        public override bool Equals(object obj) => obj is TrackFrame other && Equals(other);
        public override int GetHashCode() => Position.GetHashCode() ^ (Heading << 24) ^ (Pitch << 26) ^ (Bank << 29);
        public override string ToString() => $"{Position} heading {Heading} pitch {Pitch} bank {Bank}";
    }
}
