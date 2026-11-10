using System;
using System.Linq;
using System.Text;
using Island.Core.Save;
using Island.Core.Terrain;
using NUnit.Framework;

namespace Island.Core.Tests.Save
{
    /// <summary>
    /// An island is hours of someone's work with no copy anywhere else. These tests are mostly
    /// about the ways that work could be lost.
    /// </summary>
    [TestFixture]
    public class SaveTests
    {
        private const string Path = "/islands/slot1.island";

        private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);
        private static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes);

        private static SaveContainer ContainerWith(params (string id, int version, string body)[] sections)
        {
            var container = new SaveContainer(1_762_000_000_000L);
            foreach (var (id, version, body) in sections)
                container.Put(new SaveSection(id, version, Bytes(body)));
            return container;
        }

        // ---- the container ---------------------------------------------------------------------

        [Test]
        public void SectionsComeBackAsTheyWentIn()
        {
            var original = ContainerWith(("TERR", 1, "the ground"), ("PETS", 3, "a dog"));
            var reloaded = SaveContainer.FromBytes(original.ToBytes());

            Assert.That(reloaded.Sections.Count, Is.EqualTo(2));
            Assert.That(Text(reloaded.Find("TERR").Payload), Is.EqualTo("the ground"));
            Assert.That(reloaded.Find("PETS").Version, Is.EqualTo(3), "each section keeps its own version");
            Assert.That(reloaded.SavedAtUnixMs, Is.EqualTo(1_762_000_000_000L));
        }

        [Test]
        public void ASectionThisBuildHasNeverHeardOfSurvivesARewrite()
        {
            // Without this, opening an island in an older build and saving it would silently
            // delete everything a newer build had added — the quiet kind of data loss, where
            // nothing looks wrong until much later.
            var written = ContainerWith(("TERR", 1, "the ground"), ("WTHR", 7, "weather, from the future"));
            var reloaded = SaveContainer.FromBytes(written.ToBytes());

            reloaded.Put(new SaveSection("TERR", 1, Bytes("the ground, edited")));
            var again = SaveContainer.FromBytes(reloaded.ToBytes());

            Assert.That(Text(again.Find("WTHR").Payload), Is.EqualTo("weather, from the future"));
            Assert.That(again.Find("WTHR").Version, Is.EqualTo(7));
            Assert.That(Text(again.Find("TERR").Payload), Is.EqualTo("the ground, edited"));
        }

        [Test]
        public void PuttingASectionTwiceReplacesItRatherThanAddingIt()
        {
            var container = ContainerWith(("TERR", 1, "first"));
            container.Put(new SaveSection("TERR", 1, Bytes("second")));
            Assert.That(container.Sections.Count, Is.EqualTo(1));
            Assert.That(Text(container.Find("TERR").Payload), Is.EqualTo("second"));
        }

        [Test]
        public void ACorruptSectionIsNamedRatherThanGuessedAt()
        {
            var bytes = ContainerWith(("TERR", 1, "the ground"), ("PETS", 1, "a dog")).ToBytes();
            // Into the second section's payload, past both sections' framing, so the id is still
            // trustworthy and the message can say which part of the island is damaged.
            bytes[bytes.Length - 2] ^= 0xFF;

            var error = Assert.Throws<SaveFormatException>(() => SaveContainer.FromBytes(bytes));
            Assert.That(error.Message, Does.Contain("PETS").And.Contain("corrupt"));
        }

        [Test]
        public void CorruptionInTheFramingIsCaughtToo()
        {
            // The case that matters most and is easiest to miss: a flipped bit in a length or a
            // section id does not fail to parse, it yields a plausible-looking island with
            // something quietly missing. Checksumming only the payloads would let this through.
            var original = ContainerWith(("TERR", 1, "the ground"), ("PETS", 1, "a dog")).ToBytes();
            for (var index = 0; index < original.Length; index++)
            {
                var bytes = (byte[])original.Clone();
                bytes[index] ^= 0xFF;
                Assert.Throws<SaveFormatException>(() => SaveContainer.FromBytes(bytes),
                    $"a flipped bit at byte {index} went unnoticed");
            }
        }

        [Test]
        public void TheSavedDateIsProtectedAsWellAsTheContents()
        {
            var bytes = ContainerWith(("TERR", 1, "x")).ToBytes();
            bytes[12] ^= 0xFF;   // inside the timestamp
            var error = Assert.Throws<SaveFormatException>(() => SaveContainer.FromBytes(bytes));
            Assert.That(error.Message, Does.Contain("header"));
        }

        [Test]
        public void ATruncatedFileSaysSoInsteadOfReadingRubbish()
        {
            var bytes = ContainerWith(("TERR", 1, new string('x', 40_000))).ToBytes();
            for (var keep = 28; keep < bytes.Length; keep += Math.Max(1, bytes.Length / 20))
            {
                var cut = bytes.Take(keep).ToArray();
                Assert.Throws<SaveFormatException>(() => SaveContainer.FromBytes(cut),
                    $"a file cut to {keep} of {bytes.Length} bytes should be refused, not half-read");
            }
        }

        [Test]
        public void SomethingThatIsNotASaveIsRejectedImmediately()
        {
            Assert.Throws<SaveFormatException>(() => SaveContainer.FromBytes(Bytes("just a text file, honestly")));
            Assert.Throws<SaveFormatException>(() => SaveContainer.FromBytes(new byte[0]));
            Assert.Throws<SaveFormatException>(() => SaveContainer.FromBytes(null));
        }

        [Test]
        public void ASaveFromANewerBuildSaysSoRatherThanFailingObscurely()
        {
            // A genuinely newer file has a *valid* header saying format 99 — so this builds one
            // properly rather than flipping a bit, which the header checksum would now catch
            // first and report as damage.
            var bytes = ContainerWith(("TERR", 1, "x")).ToBytes();
            BitConverter.GetBytes(99).CopyTo(bytes, 8);                       // the format field
            BitConverter.GetBytes(Crc32.Compute(bytes, 0, 24)).CopyTo(bytes, 24);   // its checksum

            var error = Assert.Throws<SaveFormatException>(() => SaveContainer.FromBytes(bytes));
            Assert.That(error.Message, Does.Contain("newer version"),
                "someone opening their island in an older build deserves to be told why, not " +
                "shown a corruption message that makes them think they have lost it");
        }

        [Test]
        public void BigSectionsAreCompressedAndSmallOnesAreNot()
        {
            var repetitive = new string('a', 100_000);
            var bytes = ContainerWith(("TERR", 1, repetitive)).ToBytes();
            Assert.That(bytes.Length, Is.LessThan(repetitive.Length / 4), "repetitive data should pack down hard");
            Assert.That(Text(SaveContainer.FromBytes(bytes).Find("TERR").Payload), Is.EqualTo(repetitive));

            // Compression that makes a payload bigger is not used, so a tiny section never costs
            // more than it did.
            var tiny = ContainerWith(("TERR", 1, "hi")).ToBytes();
            Assert.That(Text(SaveContainer.FromBytes(tiny).Find("TERR").Payload), Is.EqualTo("hi"));
        }

        [Test]
        public void ASectionIdIsExactlyFourPrintableCharacters()
        {
            Assert.Throws<ArgumentException>(() => new SaveSection("TOOLONG", 1, new byte[0]));
            Assert.Throws<ArgumentException>(() => new SaveSection("AB", 1, new byte[0]));
            Assert.Throws<ArgumentException>(() => new SaveSection("A\u0001BC", 1, new byte[0]));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SaveSection("TERR", 0, new byte[0]));
        }

        // ---- writing without losing the old one ---------------------------------------------------

        [Test]
        public void SavingKeepsThePreviousIslandAsABackup()
        {
            var store = new FakeFileStore();
            var file = new SaveFile(store);

            file.Save(Path, ContainerWith(("TERR", 1, "monday")));
            file.Save(Path, ContainerWith(("TERR", 1, "tuesday")));

            Assert.That(Text(SaveContainer.FromBytes(store.Read(Path)).Find("TERR").Payload), Is.EqualTo("tuesday"));
            Assert.That(Text(SaveContainer.FromBytes(store.Read(SaveFile.BackupPathFor(Path))).Find("TERR").Payload),
                Is.EqualTo("monday"));
        }

        [Test]
        public void TheNewSaveIsWrittenAsideAndCheckedBeforeAnythingIsOverwritten()
        {
            var store = new FakeFileStore();
            new SaveFile(store).Save(Path, ContainerWith(("TERR", 1, "an island")));

            var writeIndex = store.Operations.FindIndex(o => o.StartsWith("write "));
            var moveIndex = store.Operations.FindIndex(o => o.Contains("-> " + Path));
            Assert.That(store.Operations[writeIndex], Does.Contain(".writing"),
                "the new bytes must land beside the save, not on top of it");
            Assert.That(store.Operations.Any(o => o.StartsWith("read ") && o.Contains(".writing")), Is.True,
                "and be read back before being trusted");
            Assert.That(moveIndex, Is.GreaterThan(writeIndex));
        }

        [Test]
        public void AnInterruptedWriteLeavesTheOldIslandUntouched()
        {
            var store = new FakeFileStore();
            var file = new SaveFile(store);
            file.Save(Path, ContainerWith(("TERR", 1, "a weekend of work")));

            store.FailWriteContaining = ".writing";
            Assert.Throws<System.IO.IOException>(() => file.Save(Path, ContainerWith(("TERR", 1, "the next edit"))));

            var after = file.Load(Path);
            Assert.That(after.Outcome, Is.EqualTo(LoadOutcome.Loaded));
            Assert.That(Text(after.Container.Find("TERR").Payload), Is.EqualTo("a weekend of work"),
                "an interrupted save must never cost the player the island they already had");
        }

        [Test]
        public void ASaveThatDoesNotReadBackIsRefusedBeforeItCanOverwriteAnything()
        {
            var store = new FakeFileStore();
            var file = new SaveFile(store);
            file.Save(Path, ContainerWith(("TERR", 1, "the good one")));

            // A disk that accepts the write and returns nonsense. Found now, while the previous
            // save is still intact, rather than at load time when it is too late.
            var original = store.Read(Path);
            store.Put(SaveFile.TempPathFor(Path), Bytes("not a save at all"));
            var brokenStore = new RefusingStore(store);

            Assert.Throws<SaveFormatException>(
                () => new SaveFile(brokenStore).Save(Path, ContainerWith(("TERR", 1, "the new one"))));
            Assert.That(store.Read(Path), Is.EqualTo(original));
        }

        [Test]
        public void ACorruptSaveFallsBackToTheBackupAndSaysWhy()
        {
            var store = new FakeFileStore();
            var file = new SaveFile(store);
            file.Save(Path, ContainerWith(("TERR", 1, "monday")));
            file.Save(Path, ContainerWith(("TERR", 1, "tuesday")));
            store.Corrupt(Path);

            var result = file.Load(Path);
            Assert.That(result.Outcome, Is.EqualTo(LoadOutcome.RecoveredFromBackup));
            Assert.That(Text(result.Container.Find("TERR").Payload), Is.EqualTo("monday"));
            Assert.That(result.Detail, Is.Not.Null.And.Not.Empty, "the player should be told what happened");
        }

        [Test]
        public void NoSaveAtAllIsNotAnError()
        {
            var result = new SaveFile(new FakeFileStore()).Load("/islands/brand-new.island");
            Assert.That(result.Outcome, Is.EqualTo(LoadOutcome.NotFound));
            Assert.That(result.HasIsland, Is.False);
        }

        [Test]
        public void BothCopiesGoneIsReportedPlainlyRatherThanThrowing()
        {
            var store = new FakeFileStore();
            var file = new SaveFile(store);
            file.Save(Path, ContainerWith(("TERR", 1, "monday")));
            file.Save(Path, ContainerWith(("TERR", 1, "tuesday")));
            store.Corrupt(Path);
            store.Corrupt(SaveFile.BackupPathFor(Path));

            var result = file.Load(Path);
            Assert.That(result.Outcome, Is.EqualTo(LoadOutcome.Lost));
            Assert.That(result.Detail, Does.Contain("backup"));
        }

        // ---- instance ids ------------------------------------------------------------------------

        [Test]
        public void IdsAreNeverZeroAndNeverRepeat()
        {
            var ids = new InstanceIds();
            var issued = Enumerable.Range(0, 1000).Select(_ => ids.Issue()).ToList();
            Assert.That(issued, Is.Unique);
            Assert.That(issued, Has.None.EqualTo(InstanceIds.None));
            Assert.That(issued.First(), Is.EqualTo(1L));
        }

        [Test]
        public void LoadingNeverHandsOutAnIdThatAlreadyExists()
        {
            // The failure this prevents: load an island, place one more tree, and have it collide
            // with something already there — so undo removes the wrong object.
            var ids = new InstanceIds();
            foreach (var existing in new[] { 5L, 99L, 42L }) ids.Reserve(existing);
            Assert.That(ids.Issue(), Is.EqualTo(100L));
        }

        [Test]
        public void TheCounterSurvivesASaveAndReload()
        {
            var ids = new InstanceIds();
            for (var i = 0; i < 10; i++) ids.Issue();
            var reloaded = new InstanceIds(ids.Next);
            Assert.That(reloaded.Issue(), Is.EqualTo(11L));
            Assert.That(InstanceIds.IsReal(InstanceIds.None), Is.False);
        }

        // ---- migrations ---------------------------------------------------------------------------

        [Test]
        public void AnOldSectionIsBroughtForwardOneStepAtATime()
        {
            var applied = new System.Collections.Generic.List<string>();
            var migrations = new SectionMigrations("TERR", 3)
                .Step(1, payload => { applied.Add("1->2"); return Bytes(Text(payload) + " +2"); })
                .Step(2, payload => { applied.Add("2->3"); return Bytes(Text(payload) + " +3"); });

            Assert.That(Text(migrations.Migrate(Bytes("v1 data"), 1)), Is.EqualTo("v1 data +2 +3"));
            Assert.That(applied, Is.EqualTo(new[] { "1->2", "2->3" }));
        }

        [Test]
        public void ACurrentSectionIsLeftAlone()
        {
            var migrations = new SectionMigrations("TERR", 2).Step(1, _ => Bytes("migrated"));
            Assert.That(Text(migrations.Migrate(Bytes("already current"), 2)), Is.EqualTo("already current"));
        }

        [Test]
        public void AMissingStepSaysWhichIslandCannotBeOpened()
        {
            var migrations = new SectionMigrations("TERR", 3).Step(2, p => p);
            Assert.That(migrations.CanMigrateFrom(1), Is.False);
            Assert.That(migrations.CanMigrateFrom(2), Is.True);

            var error = Assert.Throws<SaveFormatException>(() => migrations.Migrate(Bytes("x"), 1));
            Assert.That(error.Message, Does.Contain("TERR").And.Contain("cannot be opened"));
        }

        [Test]
        public void ASectionFromTheFutureIsRefusedClearly()
        {
            var migrations = new SectionMigrations("TERR", 2);
            var error = Assert.Throws<SaveFormatException>(() => migrations.Migrate(Bytes("x"), 5));
            Assert.That(error.Message, Does.Contain("newer version"));
        }

        [Test]
        public void StepsMustGoForwardsAndNotRepeat()
        {
            var migrations = new SectionMigrations("TERR", 3).Step(1, p => p);
            Assert.Throws<InvalidOperationException>(() => migrations.Step(1, p => p));
            Assert.Throws<ArgumentOutOfRangeException>(() => migrations.Step(3, p => p));
            Assert.Throws<ArgumentOutOfRangeException>(() => migrations.Step(0, p => p));
        }

        /// <summary>A store that hands back something other than what was written.</summary>
        private sealed class RefusingStore : IFileStore
        {
            private readonly FakeFileStore _inner;
            public RefusingStore(FakeFileStore inner) => _inner = inner;
            public bool Exists(string path) => _inner.Exists(path);
            public byte[] Read(string path) => Bytes("not a save at all");
            public void Write(string path, byte[] bytes) { }
            public void Move(string from, string to, bool overwrite) => _inner.Move(from, to, overwrite);
            public void Copy(string from, string to, bool overwrite) => _inner.Copy(from, to, overwrite);
            public void Delete(string path) => _inner.Delete(path);
            public System.Collections.Generic.IEnumerable<string> List(string d, string p) => _inner.List(d, p);
        }
    }
}
