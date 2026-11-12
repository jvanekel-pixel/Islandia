using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Island.Core.Save;
using Island.Core.Terrain;

namespace Island.Core.World
{
    /// <summary>How the world came to be open.</summary>
    public enum WorldOpening
    {
        /// <summary>The index read cleanly.</summary>
        Loaded,
        /// <summary>The index was unreadable and its backup was used; tell the player.</summary>
        RecoveredFromBackup,
        /// <summary>Nothing was saved here: a new, empty world.</summary>
        Created,
        /// <summary>A save from before the world of islands became its first island.</summary>
        MigratedSingleIsland,
        /// <summary>The index was missing but island files were found; it was rebuilt from them. Tell the player.</summary>
        Rebuilt,
    }

    /// <summary>One island read from its own file, with its station reserved again.</summary>
    public sealed class LoadedIsland
    {
        internal LoadedIsland(long id, LoadResult result, Heightfield terrain, StationSite station)
        {
            Id = id;
            Outcome = result.Outcome;
            Detail = result.Detail;
            Container = result.Container;
            Terrain = terrain;
            Station = station;
        }

        public long Id { get; }
        public LoadOutcome Outcome { get; }
        public string Detail { get; }

        /// <summary>Null when the island could not be read.</summary>
        public Heightfield Terrain { get; }
        public StationSite Station { get; }

        /// <summary>Kept so sections this build does not know survive the next save.</summary>
        internal SaveContainer Container { get; }
    }

    /// <summary>
    /// The world of islands on disk (ADR-09, ADR-24): one small index file and one V-06 save per island.
    /// </summary>
    /// <remarks>
    /// <code>
    ///   &lt;root&gt;/world.index               the index: one WRLD section, nothing else
    ///   &lt;root&gt;/islands/island-&lt;id&gt;.island  one island: TERR, STAN and whatever comes later
    /// </code>
    /// Writes are ordered so an interruption leaves at worst an orphaned island file, never an
    /// index entry pointing at nothing: a new island's file is written before the index lists it,
    /// and a deleted island leaves the index before its file goes.
    /// </remarks>
    public sealed class IslandWorld
    {
        public const string IndexFileName = "world.index";
        public const string IslandsFolder = "islands";

        /// <summary>Where a build from before the world of islands kept its one island.</summary>
        public const string SingleIslandFileName = "island.island";

        /// <summary>What that file is renamed to once migrated. Kept, never deleted.</summary>
        public const string MigratedSuffix = ".pre-world";

        /// <summary>The name a migrated island is given; the player can rename it.</summary>
        public const string MigratedIslandName = "Home";

        /// <summary>What an unreadable single-island save is renamed to, so it is kept but not retried forever.</summary>
        public const string UnreadableSuffix = ".unreadable";

        private static readonly Regex IslandFileName =
            new Regex(@"^island-([0-9]{1,18})\.island(\.backup)?$", RegexOptions.CultureInvariant);

        private readonly IFileStore _store;
        private readonly SaveFile _files;
        private readonly string _root;
        private SaveContainer _indexContainer;

        private IslandWorld(IFileStore store, string root)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _files = new SaveFile(store);
            _root = root ?? throw new ArgumentNullException(nameof(root));
        }

        public WorldIndex Index { get; private set; }
        public WorldOpening Opening { get; private set; }

        /// <summary>What the player should be told about how the world opened; null when nothing went wrong.</summary>
        public string Detail { get; private set; }

        public string IndexPath => Path.Combine(_root, IndexFileName);
        public string SingleIslandPath => Path.Combine(_root, SingleIslandFileName);
        public string IslandPath(long id) => Path.Combine(_root, IslandsFolder, $"island-{id}.island");

        /// <summary>
        /// Opens the world at <paramref name="root"/>, reading the index and nothing else. With no
        /// index, a single-island save is migrated if there is one, and otherwise the world is new.
        /// </summary>
        /// <remarks>
        /// When the index came back from its backup, or is missing while island files exist, it is
        /// reconciled with the island files on disk: islands it had lost are listed again, entries
        /// whose file is gone are dropped, and no id already used on disk can be issued again. That
        /// looks at file names only; no island is read.
        /// </remarks>
        /// <exception cref="SaveFormatException">The index and its backup are both unreadable.</exception>
        public static IslandWorld Open(IFileStore store, string root, ulong newWorldSeed, long nowUnixMs)
        {
            var world = new IslandWorld(store, root);
            var result = world._files.Load(world.IndexPath);
            switch (result.Outcome)
            {
                case LoadOutcome.Loaded:
                case LoadOutcome.RecoveredFromBackup:
                    world._indexContainer = result.Container;
                    world.Index = WorldIndex.FromSection(result.Container.Find(WorldIndex.SectionId));
                    world.Opening = result.Outcome == LoadOutcome.Loaded ? WorldOpening.Loaded : WorldOpening.RecoveredFromBackup;
                    world.Detail = result.Detail;
                    if (world.Opening == WorldOpening.RecoveredFromBackup) world.Reconcile(nowUnixMs);
                    break;
                case LoadOutcome.NotFound:
                    world.Index = new WorldIndex(newWorldSeed);
                    world._indexContainer = new SaveContainer(nowUnixMs);
                    if (world._files.Exists(world.SingleIslandPath) && world.MigrateSingleIsland(nowUnixMs))
                    {
                        world.Opening = WorldOpening.MigratedSingleIsland;
                    }
                    else if (world.IslandIdsOnDisk().Count > 0)
                    {
                        world.Reconcile(nowUnixMs);
                        world.Opening = WorldOpening.Rebuilt;
                        world.Detail = world.Detail ?? "The list of islands was missing, so it was made again from the islands found.";
                    }
                    else if (world.Opening == WorldOpening.Created && world.Detail != null)
                    {
                        // The single-island save could not be read and was set aside: start fresh.
                        world.SaveIndex(nowUnixMs);
                    }
                    else
                    {
                        world.SaveIndex(nowUnixMs);
                        world.Opening = WorldOpening.Created;
                    }
                    break;
                default:
                    throw new SaveFormatException("The list of islands could not be read: " + result.Detail);
            }
            return world;
        }

        /// <summary>Makes a new island from a seed — the world's next one unless given — and lists it.</summary>
        public IslandEntry CreateIsland(string name, long nowUnixMs, ulong? seed = null)
        {
            // An id whose file is still on disk is never issued, whatever the index says: issuing it
            // would overwrite that island and then its backup.
            while (_files.Exists(IslandPath(Index.NextIslandId))) Index.ReserveIdsThrough(Index.NextIslandId);
            var chosen = seed ?? Index.SeedFor(Index.NextIslandId);
            var island = IslandGenesis.Generate(chosen);
            var entry = Index.Add(name, chosen, island.Theme, nowUnixMs);
            try
            {
                var container = new SaveContainer(nowUnixMs);
                container.Put(new TerrainSaveSection(island.Terrain));
                container.Put(new StationSaveSection(island.Station));
                _files.Save(IslandPath(entry.Id), container);
            }
            catch
            {
                Index.Remove(entry.Id);
                throw;
            }
            SaveIndex(nowUnixMs);
            return entry;
        }

        /// <summary>Reads one island's file, and only that one.</summary>
        public LoadedIsland LoadIsland(long id)
        {
            var entry = Index.Find(id) ?? throw new ArgumentException($"There is no island {id} in this world.", nameof(id));
            var result = _files.Load(IslandPath(id));
            if (!result.HasIsland) return new LoadedIsland(id, result, null, null);

            var terrain = new Heightfield();
            result.Container.ReadInto(new TerrainSaveSection(terrain));
            var station = new StationSaveSection(null);
            var site = result.Container.ReadInto(station)
                ? station.Site
                : IslandGenesis.EnsureStation(terrain, entry.Seed);
            site.LockInto(terrain);
            return new LoadedIsland(id, result, terrain, site);
        }

        /// <summary>Writes an island back to its own file, keeping any section this build does not know.</summary>
        public void SaveIsland(LoadedIsland island, long nowUnixMs)
        {
            if (island?.Terrain == null) throw new ArgumentException("That island was never loaded.", nameof(island));
            if (Index.Find(island.Id) == null) throw new ArgumentException($"Island {island.Id} is not in this world.", nameof(island));
            var container = new SaveContainer(nowUnixMs);
            if (island.Container != null)
                foreach (var section in island.Container.Sections) container.Put(section);
            container.Put(new TerrainSaveSection(island.Terrain));
            container.Put(new StationSaveSection(island.Station));
            _files.Save(IslandPath(island.Id), container);
        }

        /// <summary>Removes an island from the list, then its file, its backup and its thumbnail. No other file is touched.</summary>
        public bool DeleteIsland(long id, long nowUnixMs)
        {
            var entry = Index.Find(id);
            if (entry == null) return false;
            Index.Remove(id);
            SaveIndex(nowUnixMs);

            var path = IslandPath(id);
            _store.Delete(path);
            _store.Delete(SaveFile.BackupPathFor(path));
            _store.Delete(SaveFile.TempPathFor(path));
            if (WorldIndex.IsSafeThumbnail(entry.Thumbnail)) _store.Delete(Path.Combine(_root, entry.Thumbnail));
            return true;
        }

        public void SaveIndex(long nowUnixMs)
        {
            var container = new SaveContainer(nowUnixMs);
            foreach (var section in _indexContainer.Sections) container.Put(section);
            container.Put(Index.ToSection());
            _files.Save(IndexPath, container);
            _indexContainer = container;
        }

        /// <summary>
        /// Turns a single-island save into island 1 of a new world: its sections kept, a station
        /// surveyed onto its ground, the index written, and the old file renamed rather than deleted.
        /// </summary>
        /// <returns>
        /// False when the old save cannot be read at all. It is then renamed with
        /// <see cref="UnreadableSuffix"/> — kept, never deleted — and the world opens new, so one bad
        /// file cannot stop the game from ever starting.
        /// </returns>
        private bool MigrateSingleIsland(long nowUnixMs)
        {
            var legacy = _files.Load(SingleIslandPath);
            if (!legacy.HasIsland)
            {
                foreach (var old in new[] { SingleIslandPath, SaveFile.BackupPathFor(SingleIslandPath) })
                    if (_store.Exists(old)) _store.Move(old, old + UnreadableSuffix, overwrite: true);
                Opening = WorldOpening.Created;
                Detail = "The island from before could not be read, so a new world was started. The old file was kept.";
                return false;
            }

            var terrain = new Heightfield();
            legacy.Container.ReadInto(new TerrainSaveSection(terrain));
            var seed = Index.SeedFor(Index.NextIslandId);
            var site = IslandGenesis.EnsureStation(terrain, seed);

            var container = new SaveContainer(nowUnixMs);
            foreach (var section in legacy.Container.Sections) container.Put(section);
            container.Put(new TerrainSaveSection(terrain));
            container.Put(new StationSaveSection(site));

            var entry = Index.Add(MigratedIslandName, seed, IslandGenesis.ThemeFor(seed), legacy.Container.SavedAtUnixMs);
            _files.Save(IslandPath(entry.Id), container);
            SaveIndex(nowUnixMs);
            Detail = legacy.Detail;

            foreach (var old in new[] { SingleIslandPath, SaveFile.BackupPathFor(SingleIslandPath) })
                if (_store.Exists(old)) _store.Move(old, old + MigratedSuffix, overwrite: true);
            return true;
        }

        /// <summary>Ids of every island with a file (or a backup) on disk. Names only; nothing is read.</summary>
        private SortedSet<long> IslandIdsOnDisk()
        {
            var ids = new SortedSet<long>();
            foreach (var path in _store.List(Path.Combine(_root, IslandsFolder), "island-*"))
            {
                var match = IslandFileName.Match(Path.GetFileName(path) ?? "");
                if (match.Success && long.TryParse(match.Groups[1].Value, out var id) && id > 0) ids.Add(id);
            }
            return ids;
        }

        /// <summary>
        /// Brings the index into line with the island files on disk after it was recovered or lost:
        /// lost islands listed again, entries pointing at nothing dropped, used ids never reissued.
        /// </summary>
        private void Reconcile(long nowUnixMs)
        {
            var onDisk = IslandIdsOnDisk();
            var changed = false;

            foreach (var entry in new List<IslandEntry>(Index.Islands))
                if (!onDisk.Contains(entry.Id)) { Index.Remove(entry.Id); changed = true; }

            foreach (var id in onDisk)
            {
                Index.ReserveIdsThrough(id);
                if (Index.Find(id) != null) continue;
                var seed = Index.SeedFor(id);
                Index.Adopt(id, $"Island {id}", seed, IslandGenesis.ThemeFor(seed), nowUnixMs);
                changed = true;
            }

            if (changed || Opening != WorldOpening.Loaded) SaveIndex(nowUnixMs);
        }
    }
}
