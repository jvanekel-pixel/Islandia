using System;
using System.IO;
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
                    break;
                case LoadOutcome.NotFound:
                    world.Index = new WorldIndex(newWorldSeed);
                    world._indexContainer = new SaveContainer(nowUnixMs);
                    if (world._files.Exists(world.SingleIslandPath))
                    {
                        world.MigrateSingleIsland(nowUnixMs);
                        world.Opening = WorldOpening.MigratedSingleIsland;
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
            if (!string.IsNullOrEmpty(entry.Thumbnail)) _store.Delete(Path.Combine(_root, entry.Thumbnail));
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
        private void MigrateSingleIsland(long nowUnixMs)
        {
            var legacy = _files.Load(SingleIslandPath);
            if (!legacy.HasIsland)
                throw new SaveFormatException("The island from before could not be read: " + legacy.Detail);

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
        }
    }
}
