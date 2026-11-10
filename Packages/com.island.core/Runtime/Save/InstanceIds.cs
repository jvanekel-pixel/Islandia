using System;

namespace Island.Core.Save
{
    /// <summary>
    /// Hands out the identifiers everything placed on the island is known by.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Undo needs to name the thing it is putting back, a save needs to name what each record
    /// describes, and a selection needs to survive a reload. An index into a list cannot do any of
    /// that: delete the third tree and every id after it means something else. So ids are issued
    /// once, never reused, and never derived from position.
    /// </para>
    /// <para>
    /// 64 bits is not caution for its own sake. At one placement a millisecond it would take
    /// nearly three hundred million years to run out, which means the wrap case need not be
    /// designed for, only refused.
    /// </para>
    /// </remarks>
    public sealed class InstanceIds
    {
        /// <summary>Not a real thing. Zero reads as "nothing" everywhere.</summary>
        public const long None = 0L;

        private long _next;

        public InstanceIds() : this(1L) { }

        public InstanceIds(long next)
        {
            if (next < 1L) throw new ArgumentOutOfRangeException(nameof(next), "Ids start at 1; 0 means nothing.");
            _next = next;
        }

        /// <summary>The id the next call would issue. This is what a save stores.</summary>
        public long Next => _next;

        /// <summary>How many have been issued.</summary>
        public long IssuedCount => _next - 1L;

        public long Issue()
        {
            if (_next == long.MaxValue)
                throw new InvalidOperationException("The island has run out of instance ids, which should not be possible.");
            return _next++;
        }

        /// <summary>
        /// Moves the counter past an id that already exists.
        /// </summary>
        /// <remarks>
        /// Used when loading: ids come back from the file and the allocator must not hand any of
        /// them out again. Called for each, it ends up just past the highest.
        /// </remarks>
        public void Reserve(long id)
        {
            if (id >= _next) _next = id + 1L;
        }

        public static bool IsReal(long id) => id > None;
    }
}
