using Island.Core.Content;
using NUnit.Framework;

namespace Island.Core.Tests.Content
{
    [TestFixture]
    public class CatalogueIdTests
    {
        [Test]
        public void TheRuntimeAgreesWithTheGenerator()
        {
            // The whole point of this fixture. tools/content/generate_ids.py computes these in
            // Python at build time and CatalogueId.Of computes them in C# at runtime, and both
            // feed the same numbers into saved islands. If they ever drifted apart the symptom
            // would be an island that loads with some of its contents quietly missing, which is
            // exactly the kind of fault nobody is here to notice.
            //
            // The expected values come from `python3 -c "import zlib; zlib.crc32(b'...')"`, not
            // from running the C# and writing down what it said.
            Assert.That(CatalogueId.Of("tree.oak.broad"), Is.EqualTo(0x44CC33DEu));
            Assert.That(CatalogueId.Of("bush.azalea"), Is.EqualTo(0xB65F9E89u));
            Assert.That(CatalogueId.Of("flower.poppy"), Is.EqualTo(0x44802EA6u));
            Assert.That(CatalogueId.Of("wall.cottage.plain"), Is.EqualTo(0xD6F3BDD5u));
            Assert.That(CatalogueId.Of("a"), Is.EqualTo(0xE8B7BE43u));
            Assert.That(CatalogueId.Of("islandia"), Is.EqualTo(0xB6CF5D7Fu));
        }

        [Test]
        public void NothingIsNotAnItem()
        {
            Assert.That(CatalogueId.None, Is.EqualTo(0u));
            Assert.That(CatalogueId.Of(null), Is.EqualTo(CatalogueId.None));
            Assert.That(CatalogueId.Of(string.Empty), Is.EqualTo(CatalogueId.None));
            Assert.That(CatalogueId.IsKnown(CatalogueId.None), Is.False);
        }

        [Test]
        public void TheSameIdAlwaysGivesTheSameNumber()
        {
            Assert.That(CatalogueId.Of("tree.oak.broad"), Is.EqualTo(CatalogueId.Of("tree.oak.broad")));
        }

        [Test]
        public void IdsAreCaseSensitiveSoTheLowercaseRuleMatters()
        {
            // The catalogue schema forbids capitals in an id. This is why: the two would be
            // different items with no visible difference between them.
            Assert.That(CatalogueId.Of("tree.oak.broad"), Is.Not.EqualTo(CatalogueId.Of("Tree.Oak.Broad")));
        }

        [Test]
        public void TheGeneratedTableIsInternallyConsistent()
        {
            // Holds whether the catalogue has nothing in it or everything: no duplicates, and
            // nothing claiming to be the empty slot. The generator refuses both, so this is the
            // assertion that the committed file really came from that generator.
            var all = CatalogueIds.All;
            Assert.That(all, Is.Unique);
            Assert.That(all, Has.No.Member(CatalogueId.None));

            foreach (var id in all)
            {
                Assert.That(CatalogueId.IsKnown(id), Is.True, $"{id:x8} is in the table but not known");
            }
        }

        [Test]
        public void SomethingOutsideTheCatalogueIsNotKnown()
        {
            Assert.That(CatalogueId.IsKnown(0xDEADBEEFu), Is.False);
        }
    }
}
