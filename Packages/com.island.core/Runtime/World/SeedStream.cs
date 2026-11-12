namespace Island.Core.World
{
    /// <summary>
    /// A deterministic stream of numbers from a seed: SplitMix64, integers only.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Not <c>System.Random</c>: its algorithm is an implementation detail that has changed
    /// between .NET versions and differs between Mono and CoreCLR, and an island's seed has to
    /// give the same island in the test run, in the editor and on the headset, this year and
    /// next. SplitMix64 is a published algorithm with published outputs, uses only 64-bit
    /// integer arithmetic, and the tests pin its first values.
    /// </para>
    /// <para>
    /// Each use of a seed — terrain, station, theme, later the cat — draws from its own stream
    /// (<see cref="For"/>), so adding a draw to one never moves what another produces.
    /// </para>
    /// </remarks>
    public sealed class SeedStream
    {
        /// <summary>The purposes a seed is split into. Never renumber; append.</summary>
        public const ulong Terrain = 1, Station = 2, Theme = 3, Cat = 4;

        private ulong _state;

        public SeedStream(ulong seed) => _state = seed;

        /// <summary>The stream for one purpose of one seed.</summary>
        public static SeedStream For(ulong seed, ulong purpose) => new SeedStream(Mix(seed ^ Mix(purpose)));

        public ulong NextULong()
        {
            unchecked
            {
                _state += 0x9E3779B97F4A7C15UL;
                return Mix(_state);
            }
        }

        /// <summary>A whole number from <paramref name="min"/> to <paramref name="max"/>, both included.</summary>
        public int Range(int min, int max)
        {
            if (max <= min) return min;
            var span = (ulong)((long)max - min + 1);
            return (int)((long)min + (long)(NextULong() % span));
        }

        /// <summary>SplitMix64's finaliser: a bijective scramble of 64 bits.</summary>
        public static ulong Mix(ulong z)
        {
            unchecked
            {
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }
    }
}
