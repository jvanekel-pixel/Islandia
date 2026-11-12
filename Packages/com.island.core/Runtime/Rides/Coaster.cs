using System;
using System.Collections.Generic;

namespace Island.Core.Rides
{
    /// <summary>The pack's six track styles. Every style has the same pieces with the same centrelines.</summary>
    public enum TrackStyle : byte
    {
        Steel = 0,
        Wood = 1,
        Mouse = 2,
        Hanging = 3,
        Monorail = 4,
        Flume = 5,
    }

    /// <summary>
    /// A roller coaster: a chain of pieces, each starting exactly where the one before it ends.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The chain is the only thing stored. Every frame is recomputed from <see cref="Start"/> by
    /// composing pieces, so there is no stored position that could disagree with the pieces, and
    /// a piece placed at the open end is exactly there — snapping is not a tolerance, it is the
    /// definition. Recomposing a few hundred pieces is a few hundred integer adds.
    /// </para>
    /// <para>
    /// An edit is refused, and changes nothing, if any piece after it would no longer fit — a
    /// turn after a lean that has been taken away, say. Whether the track joins up, has a
    /// station, or runs into anything is validation (<c>RC-01b</c>), not a rule of the chain:
    /// a coaster being built spends most of its life unfinished.
    /// </para>
    /// </remarks>
    public sealed class Coaster
    {
        /// <summary>More than an island can hold; a bound so a damaged save cannot ask for millions.</summary>
        public const int MaxPieces = 1024;

        private readonly List<TrackPiece> _pieces = new List<TrackPiece>();
        private readonly List<TrackFrame> _frames = new List<TrackFrame>();

        public Coaster(long instanceId, TrackFrame start, TrackStyle style = TrackStyle.Steel)
        {
            if (instanceId <= 0) throw new ArgumentOutOfRangeException(nameof(instanceId), "A coaster needs a real instance id.");
            InstanceId = instanceId;
            Start = start;
            Style = style;
            _frames.Add(start);
        }

        public long InstanceId { get; }

        /// <summary>Where the first piece begins, in the coaster's own grid.</summary>
        public TrackFrame Start { get; }

        /// <summary>The skin every piece is drawn in. Changing it is an edit, so <see cref="Changed"/> fires.</summary>
        public TrackStyle Style
        {
            get => _style;
            set
            {
                if (_style == value) return;
                _style = value;
                Changed?.Invoke();
            }
        }

        private TrackStyle _style;

        public int Count => _pieces.Count;

        public TrackPiece PieceAt(int index) => _pieces[index];

        /// <summary>The frame piece <paramref name="index"/> starts at. <c>EntryOf(Count)</c> is the open end.</summary>
        public TrackFrame EntryOf(int index)
        {
            if (index < 0 || index > _pieces.Count) throw new ArgumentOutOfRangeException(nameof(index));
            return _frames[index];
        }

        public TrackFrame ExitOf(int index)
        {
            if (index < 0 || index >= _pieces.Count) throw new ArgumentOutOfRangeException(nameof(index));
            return _frames[index + 1];
        }

        /// <summary>Where the next piece goes.</summary>
        public TrackFrame OpenEnd => _frames[_frames.Count - 1];

        /// <summary>The track ends exactly where it starts, facing the same way.</summary>
        public bool IsClosed => _pieces.Count > 0 && OpenEnd == Start;

        /// <summary>Raised after every edit, so the track and its validation can refresh.</summary>
        public event Action Changed;

        // ---- editing ------------------------------------------------------------------------------

        public bool CanAdd(TrackPiece piece) => CanInsert(_pieces.Count, piece);

        public void Add(TrackPiece piece) => Insert(_pieces.Count, piece);

        public bool CanInsert(int index, TrackPiece piece)
        {
            if (piece == null || index < 0 || index > _pieces.Count || _pieces.Count >= MaxPieces) return false;
            return Compose(index, piece, skip: -1) != null;
        }

        /// <summary>Puts a piece at <paramref name="index"/>; the pieces after it follow on from it.</summary>
        public void Insert(int index, TrackPiece piece)
        {
            if (piece == null) throw new ArgumentNullException(nameof(piece));
            if (index < 0 || index > _pieces.Count) throw new ArgumentOutOfRangeException(nameof(index));
            if (_pieces.Count >= MaxPieces) throw new InvalidOperationException($"A coaster holds at most {MaxPieces} pieces.");
            var frames = Compose(index, piece, skip: -1)
                ?? throw new InvalidOperationException($"{piece.Name} does not fit at piece {index}.");
            _pieces.Insert(index, piece);
            Commit(frames);
        }

        public bool CanRemoveAt(int index) =>
            index >= 0 && index < _pieces.Count && Compose(-1, null, skip: index) != null;

        /// <summary>Takes out piece <paramref name="index"/>; the pieces after it close up behind it.</summary>
        public TrackPiece RemoveAt(int index)
        {
            if (index < 0 || index >= _pieces.Count) throw new ArgumentOutOfRangeException(nameof(index));
            var frames = Compose(-1, null, skip: index)
                ?? throw new InvalidOperationException($"Removing piece {index} would leave a piece after it with nothing it fits on.");
            var removed = _pieces[index];
            _pieces.RemoveAt(index);
            Commit(frames);
            return removed;
        }

        /// <summary>
        /// The frames the chain would have with <paramref name="insert"/> placed at
        /// <paramref name="at"/>, or with piece <paramref name="skip"/> left out — or null if
        /// some piece would not fit where it lands.
        /// </summary>
        private List<TrackFrame> Compose(int at, TrackPiece insert, int skip)
        {
            var frames = new List<TrackFrame>(_pieces.Count + 2) { Start };
            var frame = Start;
            for (var i = 0; i <= _pieces.Count; i++)
            {
                if (i == at)
                {
                    if (!frame.Accepts(insert)) return null;
                    frame = frame.Then(insert);
                    frames.Add(frame);
                }
                if (i == _pieces.Count) break;
                if (i == skip) continue;
                if (!frame.Accepts(_pieces[i])) return null;
                frame = frame.Then(_pieces[i]);
                frames.Add(frame);
            }
            return frames;
        }

        private void Commit(List<TrackFrame> frames)
        {
            _frames.Clear();
            _frames.AddRange(frames);
            Changed?.Invoke();
        }
    }
}
