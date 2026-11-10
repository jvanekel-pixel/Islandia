using System;
using System.Collections.Generic;

namespace Island.Core.Budget
{
    /// <summary>The parts of the island that cost something to draw.</summary>
    public enum Subsystem
    {
        Terrain,
        Foliage,
        Architecture,
        ImportedModels,
        PetsMediaAndUi,
    }

    /// <summary>How close to the limit something is.</summary>
    public enum BudgetZone
    {
        /// <summary>Under 80%. Room to spare.</summary>
        Green,
        /// <summary>80% to 95%. Fine, but stop adding.</summary>
        Yellow,
        /// <summary>Over 95%. Something will have to give.</summary>
        Red,
    }

    /// <summary>What one subsystem is allowed, or is using.</summary>
    public readonly struct Allocation
    {
        public Allocation(int drawCalls, int triangles, long bytes)
        {
            DrawCalls = drawCalls;
            Triangles = triangles;
            Bytes = bytes;
        }

        public int DrawCalls { get; }
        public int Triangles { get; }
        public long Bytes { get; }

        public static readonly Allocation Nothing = new Allocation(0, 0, 0);

        public Allocation Plus(Allocation other) =>
            new Allocation(DrawCalls + other.DrawCalls, Triangles + other.Triangles, Bytes + other.Bytes);

        public override string ToString() =>
            $"{DrawCalls} draws, {Triangles:N0} tris, {Bytes / (1024 * 1024)} MB";
    }

    /// <summary>
    /// What a device is allowed, per subsystem and in total.
    /// </summary>
    /// <remarks>
    /// Straight from <c>02-ARCHITECTURE.md</c> ADR-10. Every number here is <b>a planning figure,
    /// not a measurement</b> — they came from the original blueprint and not one has been seen on
    /// hardware. They are written down anyway, because a budget nobody can check is not a budget;
    /// but <see cref="DeviceTiers.IsVerified"/> exists so nothing reports them as fact.
    /// </remarks>
    public sealed class DeviceBudget
    {
        private readonly Dictionary<Subsystem, Allocation> _perSubsystem;

        private DeviceBudget(DeviceTier tier, float frameMilliseconds, int refreshHz,
                             Dictionary<Subsystem, Allocation> perSubsystem,
                             int hardDrawCalls, int hardTriangles, long hardBytes,
                             int maxActivePets, int maxActiveScreens)
        {
            Tier = tier;
            FrameMilliseconds = frameMilliseconds;
            RefreshHz = refreshHz;
            _perSubsystem = perSubsystem;
            HardDrawCalls = hardDrawCalls;
            HardTriangles = hardTriangles;
            HardBytes = hardBytes;
            MaxActivePets = maxActivePets;
            MaxActiveScreens = maxActiveScreens;
        }

        public DeviceTier Tier { get; }
        public float FrameMilliseconds { get; }
        public int RefreshHz { get; }

        /// <summary>
        /// The cap the device imposes, which the sum of the parts must not exceed.
        /// </summary>
        /// <remarks>
        /// ADR-10 states hard caps for draw calls and triangles and a single busy-scene figure for
        /// memory. Memory's cap is therefore that figure itself: there is no slack behind it.
        /// </remarks>
        public int HardDrawCalls { get; }
        public int HardTriangles { get; }
        public long HardBytes { get; }

        public int MaxActivePets { get; }
        public int MaxActiveScreens { get; }

        public Allocation For(Subsystem subsystem) =>
            _perSubsystem.TryGetValue(subsystem, out var allocation) ? allocation : Allocation.Nothing;

        /// <summary>Everything added up: what a busy island is allowed to come to.</summary>
        public Allocation Total
        {
            get
            {
                var total = Allocation.Nothing;
                foreach (var allocation in _perSubsystem.Values) total = total.Plus(allocation);
                return total;
            }
        }

        private static Dictionary<Subsystem, Allocation> Table(
            Allocation terrain, Allocation foliage, Allocation architecture,
            Allocation imported, Allocation petsMediaUi) =>
            new Dictionary<Subsystem, Allocation>
            {
                [Subsystem.Terrain] = terrain,
                [Subsystem.Foliage] = foliage,
                [Subsystem.Architecture] = architecture,
                [Subsystem.ImportedModels] = imported,
                [Subsystem.PetsMediaAndUi] = petsMediaUi,
            };

        private const long Mb = 1024L * 1024L;

        /// <summary>
        /// The budget for a tier.
        /// </summary>
        /// <remarks>
        /// Quest 2 is the one that matters: it is the device this is being made for, and the
        /// tightest. The others exist so the code has somewhere to put a number if it ever runs
        /// on one, and are explicitly unverified.
        /// </remarks>
        public static DeviceBudget For(DeviceTier tier)
        {
            switch (tier)
            {
                case DeviceTier.Quest2:
                case DeviceTier.QuestPro:
                    return new DeviceBudget(tier, 13.8f, 72,
                        Table(
                            terrain:      new Allocation(17, 131_072, 32 * Mb),
                            foliage:      new Allocation(45, 220_000, 48 * Mb),
                            architecture: new Allocation(35, 180_000, 64 * Mb),
                            imported:     new Allocation(25, 120_000, 128 * Mb),
                            petsMediaUi:  new Allocation(18, 60_000, 96 * Mb)),
                        hardDrawCalls: 200, hardTriangles: 750_000, hardBytes: 368 * Mb,
                        maxActivePets: 4, maxActiveScreens: 1);

                case DeviceTier.Quest3S:
                case DeviceTier.Quest3:
                    return new DeviceBudget(tier, 11.1f, 90,
                        Table(
                            terrain:      new Allocation(65, 131_072, 32 * Mb),
                            foliage:      new Allocation(85, 550_000, 64 * Mb),
                            architecture: new Allocation(70, 450_000, 96 * Mb),
                            imported:     new Allocation(45, 250_000, 256 * Mb),
                            petsMediaUi:  new Allocation(25, 100_000, 128 * Mb)),
                        hardDrawCalls: 300, hardTriangles: 1_500_000, hardBytes: 576 * Mb,
                        maxActivePets: 6, maxActiveScreens: 2);

                case DeviceTier.Editor:
                    // Generous on purpose. The editor is not a device and a reading taken there
                    // says nothing about a headset; the blueprint's own notes warned that editor
                    // draw-call counts run two to three times high.
                    return new DeviceBudget(tier, 16.6f, 60,
                        Table(
                            terrain:      new Allocation(200, 500_000, 256 * Mb),
                            foliage:      new Allocation(200, 2_000_000, 256 * Mb),
                            architecture: new Allocation(200, 2_000_000, 256 * Mb),
                            imported:     new Allocation(200, 2_000_000, 512 * Mb),
                            petsMediaUi:  new Allocation(200, 500_000, 256 * Mb)),
                        hardDrawCalls: 2000, hardTriangles: 10_000_000, hardBytes: 4096 * Mb,
                        maxActivePets: 6, maxActiveScreens: 2);

                default:
                    throw new ArgumentOutOfRangeException(nameof(tier), tier, "No budget for that tier.");
            }
        }
    }

    /// <summary>
    /// What the island is currently spending, against what it is allowed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every subsystem reports into this, and the wrist gauge reads it. The point is to make the
    /// cost of what someone has built <em>visible while they are building it</em>, rather than
    /// discovered as a stutter twenty minutes later with no idea which of forty decisions caused
    /// it.
    /// </para>
    /// <para>
    /// The blueprint asserted all of these numbers and measured none of them. This at least makes
    /// them checkable.
    /// </para>
    /// </remarks>
    public sealed class BudgetLedger
    {
        private readonly Dictionary<Subsystem, Allocation> _used = new Dictionary<Subsystem, Allocation>();

        public BudgetLedger(DeviceBudget budget) =>
            Budget = budget ?? throw new ArgumentNullException(nameof(budget));

        public DeviceBudget Budget { get; }

        /// <summary>A subsystem says what it is currently costing. Replaces its previous figure.</summary>
        public void Report(Subsystem subsystem, Allocation used) => _used[subsystem] = used;

        public void Clear() => _used.Clear();

        public Allocation UsedBy(Subsystem subsystem) =>
            _used.TryGetValue(subsystem, out var used) ? used : Allocation.Nothing;

        public Allocation TotalUsed
        {
            get
            {
                var total = Allocation.Nothing;
                foreach (var used in _used.Values) total = total.Plus(used);
                return total;
            }
        }

        /// <summary>How full a subsystem's allowance is: 1.0 means exactly at the limit.</summary>
        public float FractionUsedBy(Subsystem subsystem)
        {
            var allowed = Budget.For(subsystem);
            var used = UsedBy(subsystem);
            return Worst(
                Fraction(used.DrawCalls, allowed.DrawCalls),
                Fraction(used.Triangles, allowed.Triangles),
                Fraction(used.Bytes, allowed.Bytes));
        }

        /// <summary>
        /// How full the island is overall.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Measured against the device's hard caps, consistently for all three measures. An
        /// earlier version compared memory against the sum of the per-subsystem allowances
        /// instead, which made the overall figure mean two different things at once.
        /// </para>
        /// <para>
        /// Note what ADR-10's numbers actually come to on a Quest 2: the allowances add up to 70%
        /// of the draw-call cap, <b>95% of the triangle cap and 100% of the memory figure</b>.
        /// So an island with every subsystem at its allowance is not a comfortable island — it is
        /// at the limit, and this reads red. That is a property of the planning numbers, which
        /// have never been near the hardware, not of this code; see DECISIONS.md 2026-11-10.
        /// Memory is the measure that will bind first.
        /// </para>
        /// </remarks>
        public float FractionUsedOverall()
        {
            var used = TotalUsed;
            return Worst(
                Fraction(used.DrawCalls, Budget.HardDrawCalls),
                Fraction(used.Triangles, Budget.HardTriangles),
                Fraction(used.Bytes, Budget.HardBytes));
        }

        public BudgetZone ZoneFor(Subsystem subsystem) => ZoneOf(FractionUsedBy(subsystem));

        public BudgetZone OverallZone() => ZoneOf(FractionUsedOverall());

        /// <summary>Every subsystem at or past yellow, worst first — what to look at.</summary>
        public IEnumerable<(Subsystem Subsystem, float Fraction, BudgetZone Zone)> Pressures()
        {
            var found = new List<(Subsystem, float, BudgetZone)>();
            foreach (Subsystem subsystem in Enum.GetValues(typeof(Subsystem)))
            {
                var fraction = FractionUsedBy(subsystem);
                var zone = ZoneOf(fraction);
                if (zone != BudgetZone.Green) found.Add((subsystem, fraction, zone));
            }
            found.Sort((a, b) => b.Item2.CompareTo(a.Item2));
            return found;
        }

        /// <summary>Green below 80%, yellow to 95%, red above. The thresholds the wrist gauge uses.</summary>
        public static BudgetZone ZoneOf(float fraction) =>
            fraction >= 0.95f ? BudgetZone.Red
            : fraction >= 0.80f ? BudgetZone.Yellow
            : BudgetZone.Green;

        private static float Fraction(long used, long allowed) =>
            allowed <= 0 ? (used > 0 ? float.PositiveInfinity : 0f) : (float)used / allowed;

        private static float Worst(params float[] fractions)
        {
            var worst = 0f;
            foreach (var fraction in fractions) if (fraction > worst) worst = fraction;
            return worst;
        }
    }
}
