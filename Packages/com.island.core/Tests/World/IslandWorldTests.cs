using System.Linq;
using Island.Core.Save;
using Island.Core.Terrain;
using Island.Core.Tests.Save;
using Island.Core.World;
using NUnit.Framework;

namespace Island.Core.Tests.World
{
    /// <summary>
    /// Many islands, one file each, and a small index that lists them without opening any.
    /// </summary>
    [TestFixture]
    public class IslandWorldTests
    {
        private const string Root = "/world";
        private const long Monday = 1_762_000_000_000L;
        private const long Tuesday = Monday + 86_400_000L;

        private FakeFileStore _store;

        [SetUp]
        public void SetUp() => _store = new FakeFileStore();

        private IslandWorld Open() => IslandWorld.Open(_store, Root, 0xA11CE, Tuesday);

        private string[] IslandFiles() =>
            _store.Paths.Where(p => p.StartsWith(Root + "/islands/") && p.EndsWith(".island")).ToArray();

        // ---- the index ---------------------------------------------------------------------------

        [Test]
        public void ANewWorldHasNoIslandsUntilOneIsMade()
        {
            var world = Open();
            Assert.That(world.Opening, Is.EqualTo(WorldOpening.Created));
            Assert.That(world.Index.Islands, Is.Empty);
            Assert.That(IslandFiles(), Is.Empty);

            var entry = world.CreateIsland("Halt", Monday);
            Assert.That(IslandFiles(), Is.EqualTo(new[] { world.IslandPath(entry.Id) }));
        }

        [Test]
        public void TheIndexListsEachIslandByStableIdWithWhatTheBoardShows()
        {
            var world = Open();
            world.CreateIsland("  Lighthouse  ", Monday, seed: 7);
            world.CreateIsland("Pines", Tuesday);
            world.Index.SetThumbnail(1, "thumbnails/island-1.png");
            world.SaveIndex(Tuesday);

            var islands = Open().Index.Islands;
            Assert.That(islands.Select(i => i.Id), Is.EqualTo(new[] { 1L, 2L }));
            Assert.That(islands[0].Name, Is.EqualTo("Lighthouse"));
            Assert.That(islands[0].Seed, Is.EqualTo(7UL));
            Assert.That(islands[0].Theme, Is.EqualTo(IslandGenesis.ThemeFor(7)));
            Assert.That(islands[0].CreatedAtUnixMs, Is.EqualTo(Monday));
            Assert.That(islands[0].Thumbnail, Is.EqualTo("thumbnails/island-1.png"));
            Assert.That(islands[1].Seed, Is.EqualTo(world.Index.SeedFor(2)), "an island made without a seed gets the world's");
            Assert.That(islands[1].Thumbnail, Is.Null, "no picture until the island has been left");
        }

        [Test]
        public void IdsAreNeverReused()
        {
            var world = Open();
            for (var i = 0; i < 3; i++) world.CreateIsland("Island " + i, Monday);
            world.DeleteIsland(3, Monday);
            Assert.That(world.CreateIsland("Again", Monday).Id, Is.EqualTo(4L));
            Assert.That(Open().Index.NextIslandId, Is.EqualTo(5L));
        }

        [Test]
        public void TheListHasNoFixedUpperBound()
        {
            var index = new WorldIndex(1);
            for (var i = 0; i < 20_000; i++) index.Add("Island " + i, (ulong)i, "meadow", Monday + i);

            var container = new SaveContainer(Monday);
            container.Put(index.ToSection());
            var back = WorldIndex.FromSection(SaveContainer.FromBytes(container.ToBytes()).Find(WorldIndex.SectionId));

            Assert.That(back.Islands.Count, Is.EqualTo(20_000));
            Assert.That(back.Islands[19_999].Name, Is.EqualTo("Island 19999"));
            Assert.That(back.NextIslandId, Is.EqualTo(20_001L));

            // And on disk, islands are made one after another for as long as anyone likes.
            var world = Open();
            for (var i = 0; i < 12; i++) world.CreateIsland("Stop " + i, Monday);
            Assert.That(IslandFiles().Length, Is.EqualTo(12));
        }

        [Test]
        public void LoadingTheIndexNeverLoadsAnIsland()
        {
            var world = Open();
            for (var i = 0; i < 3; i++) world.CreateIsland("Island " + i, Monday);
            _store.Operations.Clear();

            var reopened = Open();

            Assert.That(reopened.Index.Islands.Count, Is.EqualTo(3));
            Assert.That(_store.Operations, Is.EqualTo(new[] { "read " + reopened.IndexPath }),
                "opening the world read something besides the index");
            Assert.That(_store.Read(reopened.IndexPath).Length, Is.LessThan(1024),
                "the index is a list, not a copy of the islands");
        }

        // ---- one file per island --------------------------------------------------------------------

        [Test]
        public void EachIslandIsItsOwnFileAndTheIndexIsAnother()
        {
            var world = Open();
            for (var i = 0; i < 3; i++) world.CreateIsland("Island " + i, Monday);

            Assert.That(IslandFiles(), Is.EqualTo(new[] { 1L, 2L, 3L }.Select(world.IslandPath).ToArray()));
            foreach (var path in IslandFiles())
            {
                var island = SaveContainer.FromBytes(_store.Read(path));
                Assert.That(island.Find(TerrainSaveSection.Id), Is.Not.Null, path);
                Assert.That(island.Find(StationSaveSection.Id), Is.Not.Null, path);
            }
            var index = SaveContainer.FromBytes(_store.Read(world.IndexPath));
            Assert.That(index.Sections.Select(s => s.Id), Is.EqualTo(new[] { WorldIndex.SectionId }));
        }

        [Test]
        public void DeletingAnIslandRemovesOnlyItsFiles()
        {
            var world = Open();
            for (var i = 0; i < 3; i++) world.CreateIsland("Island " + i, Monday);
            var middle = world.LoadIsland(2);
            world.SaveIsland(middle, Tuesday);  // so it has a backup as well
            Assert.That(_store.Exists(SaveFile.BackupPathFor(world.IslandPath(2))));
            var others = new[] { world.IslandPath(1), world.IslandPath(3) }.ToDictionary(p => p, p => _store.Read(p));

            Assert.That(world.DeleteIsland(2, Tuesday), Is.True);

            Assert.That(_store.Exists(world.IslandPath(2)), Is.False);
            Assert.That(_store.Exists(SaveFile.BackupPathFor(world.IslandPath(2))), Is.False);
            foreach (var pair in others) Assert.That(_store.Read(pair.Key), Is.EqualTo(pair.Value), pair.Key);
            Assert.That(Open().Index.Islands.Select(i => i.Id), Is.EqualTo(new[] { 1L, 3L }));
        }

        [Test]
        public void ADamagedIslandDoesNotTakeTheOthersWithIt()
        {
            var world = Open();
            for (var i = 0; i < 3; i++) world.CreateIsland("Island " + i, Monday);
            _store.Corrupt(world.IslandPath(2));

            Assert.That(world.LoadIsland(2).Outcome, Is.EqualTo(LoadOutcome.Lost));
            Assert.That(world.LoadIsland(1).Outcome, Is.EqualTo(LoadOutcome.Loaded));
            Assert.That(world.LoadIsland(3).Outcome, Is.EqualTo(LoadOutcome.Loaded));
        }

        // ---- from one island to a world of them -----------------------------------------------------

        [Test]
        public void ASingleIslandSaveBecomesAOneIslandWorld()
        {
            // What a build from before the world of islands left behind: one file, one island, and
            // a section this build has never heard of.
            var old = new Heightfield();
            TerrainSculptor.Apply(old, new TerrainBrush(BrushKind.Raise, -10f, -10f, 6f, 4f), 1f);
            TerrainSculptor.Apply(old, new TerrainBrush(BrushKind.Paint, 8f, 8f, 5f, 8f), 1f);
            var legacy = new SaveContainer(Monday);
            legacy.Put(new TerrainSaveSection(old));
            legacy.Put(new SaveSection("WTHR", 2, new byte[] { 1, 2, 3 }));
            var legacyPath = Root + "/" + IslandWorld.SingleIslandFileName;
            new SaveFile(_store).Save(legacyPath, legacy);
            var expected = new Heightfield();
            SaveContainer.FromBytes(_store.Read(legacyPath)).ReadInto(new TerrainSaveSection(expected));

            var world = Open();

            Assert.That(world.Opening, Is.EqualTo(WorldOpening.MigratedSingleIsland));
            var entry = world.Index.Islands.Single();
            Assert.That(entry.Name, Is.EqualTo(IslandWorld.MigratedIslandName));
            Assert.That(entry.CreatedAtUnixMs, Is.EqualTo(Monday));

            var island = world.LoadIsland(entry.Id);
            Assert.That(island.Outcome, Is.EqualTo(LoadOutcome.Loaded));
            var site = island.Station;
            var margin = 3 * (int)(1f / IslandConstants.VertexSpacingMetres);
            for (var z = 0; z < IslandConstants.HeightfieldResolution; z++)
            for (var x = 0; x < IslandConstants.HeightfieldResolution; x++)
            {
                if (site.IsReserved(x, z))
                {
                    Assert.That(island.Terrain.IsLocked(x, z), $"({x}, {z}) is reserved but not locked");
                    continue;
                }
                if (site.Front.Contains(x, z)) continue;  // cut back to water, so the station faces the sea
                var nearStation = x >= site.Footprint.MinX - margin && x <= site.Footprint.MaxX + margin &&
                                  z >= site.Footprint.MinZ - margin && z <= site.Footprint.MaxZ + margin;
                if (nearStation) continue;
                Assert.That(island.Terrain.HeightAt(x, z),
                    Is.EqualTo(expected.HeightAt(x, z)).Within(TerrainSaveSection.QuantisationStepMetres), $"({x}, {z})");
                Assert.That(island.Terrain.WeightAt(x, z, IslandConstants.SurfaceChannelGrass),
                    Is.EqualTo(expected.WeightAt(x, z, IslandConstants.SurfaceChannelGrass)), $"({x}, {z})");
            }
            // The old island was flat to its edges; its station still looks out over water.
            for (var z = site.Front.MinZ; z <= site.Front.MaxZ; z++)
            for (var x = site.Front.MinX; x <= site.Front.MaxX; x++)
                if (!site.Footprint.Contains(x, z))
                    Assert.That(IslandConstants.IsSubmerged(island.Terrain.HeightAt(x, z)), $"({x}, {z})");

            var file = SaveContainer.FromBytes(_store.Read(world.IslandPath(entry.Id)));
            Assert.That(file.Find("WTHR")?.Payload, Is.EqualTo(new byte[] { 1, 2, 3 }), "the unknown section came along");
            Assert.That(_store.Exists(legacyPath), Is.False);
            Assert.That(_store.Exists(legacyPath + IslandWorld.MigratedSuffix), "the old file is kept, renamed");

            var again = Open();
            Assert.That(again.Opening, Is.EqualTo(WorldOpening.Loaded));
            Assert.That(again.Index.Islands.Count, Is.EqualTo(1));
        }

        // ---- when the index is lost or old -------------------------------------------------------

        [Test]
        public void AnIndexRecoveredFromAnOldBackupNeverReissuesAnIdOrOverwritesAnIsland()
        {
            var world = Open();
            world.CreateIsland("One", Monday);
            world.CreateIsland("Two", Monday);
            var three = world.CreateIsland("Three", Monday);
            var threeBytes = _store.Read(world.IslandPath(three.Id));
            _store.Corrupt(world.IndexPath); // its backup lists only One and Two

            var recovered = Open();
            Assert.That(recovered.Opening, Is.EqualTo(WorldOpening.RecoveredFromBackup));
            Assert.That(recovered.Index.Islands.Select(i => i.Id), Is.EqualTo(new[] { 1L, 2L, 3L }),
                "the island the backup did not know is listed again");

            var four = recovered.CreateIsland("Four", Tuesday);
            Assert.That(four.Id, Is.EqualTo(4L));
            Assert.That(_store.Read(recovered.IslandPath(3)), Is.EqualTo(threeBytes), "island 3 untouched");
        }

        [Test]
        public void ARecoveredIndexDropsAnIslandWhoseFileIsGone()
        {
            var world = Open();
            world.CreateIsland("One", Monday);
            world.CreateIsland("Two", Monday);
            world.CreateIsland("Three", Monday);
            world.DeleteIsland(3, Tuesday); // the index's backup still lists 3
            _store.Corrupt(world.IndexPath);

            var recovered = Open();
            Assert.That(recovered.Opening, Is.EqualTo(WorldOpening.RecoveredFromBackup));
            Assert.That(recovered.Index.Islands.Select(i => i.Id), Is.EqualTo(new[] { 1L, 2L }));
            Assert.That(recovered.CreateIsland("Next", Tuesday).Id, Is.EqualTo(4L), "3 is still never reissued");
        }

        [Test]
        public void AMissingIndexIsRebuiltFromTheIslandFilesAndOverwritesNothing()
        {
            var world = Open();
            world.CreateIsland("One", Monday);
            world.CreateIsland("Two", Monday);
            var oneBytes = _store.Read(world.IslandPath(1));
            _store.Delete(world.IndexPath);
            _store.Delete(SaveFile.BackupPathFor(world.IndexPath));

            var rebuilt = Open();
            Assert.That(rebuilt.Opening, Is.EqualTo(WorldOpening.Rebuilt));
            Assert.That(rebuilt.Detail, Is.Not.Null, "the player is told");
            Assert.That(rebuilt.Index.Islands.Select(i => i.Id), Is.EqualTo(new[] { 1L, 2L }));
            Assert.That(rebuilt.LoadIsland(1).Terrain, Is.Not.Null);
            Assert.That(rebuilt.CreateIsland("Three", Tuesday).Id, Is.EqualTo(3L));
            Assert.That(_store.Read(rebuilt.IslandPath(1)), Is.EqualTo(oneBytes));
        }

        [Test]
        public void ANewIslandSkipsAnIdWhoseFileIsAlreadyOnDisk()
        {
            var world = Open();
            _store.Put(world.IslandPath(1), new byte[] { 9, 9, 9 }); // a stray file the index never knew

            var entry = world.CreateIsland("Fresh", Monday);
            Assert.That(entry.Id, Is.EqualTo(2L));
            Assert.That(_store.Read(world.IslandPath(1)), Is.EqualTo(new byte[] { 9, 9, 9 }));
        }

        [Test]
        public void AnUnreadableOldSaveIsKeptAsideAndANewWorldOpens()
        {
            var legacyPath = Root + "/" + IslandWorld.SingleIslandFileName;
            _store.Put(legacyPath, new byte[] { 1, 2, 3, 4 });

            var world = Open();
            Assert.That(world.Opening, Is.EqualTo(WorldOpening.Created));
            Assert.That(world.Detail, Is.Not.Null, "the player is told");
            Assert.That(_store.Exists(legacyPath), Is.False);
            Assert.That(_store.Read(legacyPath + IslandWorld.UnreadableSuffix), Is.EqualTo(new byte[] { 1, 2, 3, 4 }));
            Assert.That(Open().Opening, Is.EqualTo(WorldOpening.Loaded), "and it opens normally from then on");
        }

        // ---- thumbnails and names ----------------------------------------------------------------

        [TestCase("../outside.png")]
        [TestCase("/sdcard/anything")]
        [TestCase("islands/island-2.island")]
        [TestCase("thumbnails/../islands/island-2.island")]
        [TestCase("thumbnails/deeper/one.png")]
        [TestCase("thumbnails\\one.png")]
        [TestCase("C:thumbnails/one.png")]
        public void AThumbnailMustStayInsideTheThumbnailFolder(string path)
        {
            var world = Open();
            world.CreateIsland("One", Monday);
            Assert.Throws<System.ArgumentException>(() => world.Index.SetThumbnail(1, path));
        }

        [Test]
        public void DeletingAnIslandCannotBeAimedAtAnotherIslandThroughItsThumbnail()
        {
            var world = Open();
            world.CreateIsland("One", Monday);
            world.CreateIsland("Two", Monday);
            world.Index.SetThumbnail(1, "thumbnails/island-1.png");
            world.SaveIndex(Monday);
            _store.Put(Root + "/thumbnails/island-1.png", new byte[] { 7 });

            world.DeleteIsland(1, Tuesday);
            Assert.That(_store.Exists(Root + "/thumbnails/island-1.png"), Is.False);
            Assert.That(_store.Exists(world.IslandPath(2)), Is.True);
        }

        [Test]
        public void ALongNameIsNeverCutThroughTheMiddleOfAnEmoji()
        {
            var world = Open();
            var name = new string('a', WorldIndex.MaxNameLength - 1) + "\U0001F682" + "tail"; // a steam train
            world.CreateIsland(name, Monday);
            var saved = Open().Index.Islands[0].Name;
            Assert.That(saved, Is.EqualTo(new string('a', WorldIndex.MaxNameLength - 1)));
            // By char, which is ordinal everywhere: on Unity's Mono a culture-sensitive string search
            // for U+FFFD matches any string at all, so Does.Not.Contain with it always fails there.
            Assert.That(saved.IndexOf('\uFFFD'), Is.EqualTo(-1), "no replacement character");
        }
    }
}
