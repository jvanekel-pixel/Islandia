using System.IO;
using Island.Core.StreamBox;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Island.Core.Tests.StreamBox
{
    /// <summary>
    /// What happens when StreamBox sends something this build has never seen.
    /// </summary>
    /// <remarks>
    /// StreamBox is a separate project under active development. It will add fields and enum
    /// cases without warning. The rule these tests hold the client to: <b>be strict about what we
    /// need, relaxed about everything else</b>. An unknown word must never stop a library from
    /// appearing in the headset — but a required field going missing must be a named error, not a
    /// silent zero that shows up later as an empty screen nobody can explain.
    /// </remarks>
    [TestFixture]
    public class ToleranceTests
    {
        private static JObject Item(params (string key, JToken value)[] overrides)
        {
            var body = new JObject
            {
                ["id"] = "mk:film-1",
                ["kind"] = "movie",
                ["libraryId"] = "11111111-1111-4111-8111-111111111111",
                ["name"] = "The Long Afternoon",
            };
            foreach (var (key, value) in overrides) body[key] = value;
            return body;
        }

        // ---- relaxed about what we do not need --------------------------------------------------

        [Test]
        public void AFieldWeHaveNeverHeardOfIsIgnored()
        {
            var body = Item(("somethingAddedNextYear", "surprise"),
                            ("nested", new JObject { ["also"] = "new" }));
            var item = CatalogItem.From(body, "");
            Assert.That(item.Name, Is.EqualTo("The Long Afternoon"));
        }

        [Test]
        public void AnUnknownItemKindShowsAsUnknownRatherThanFailing()
        {
            // If StreamBox adds, say, 'musicVideo', every existing build must still render the
            // page it arrived on.
            var item = CatalogItem.From(Item(("kind", "musicVideo")), "");
            Assert.That(item.Kind, Is.EqualTo(ItemKind.Unknown));
            Assert.That(item.Name, Is.EqualTo("The Long Afternoon"));
        }

        [Test]
        public void AnUnknownStreamDecisionReadsAsUnknown()
        {
            var body = new JObject
            {
                ["streamId"] = "s1", ["url"] = "/api/stream/t/master.m3u8",
                ["decision"] = "someNewStrategy",
            };
            Assert.That(PlayResponse.From(body).Decision, Is.EqualTo(StreamDecision.Unknown));
        }

        [Test]
        public void AnUnknownConnectCommandIsIgnoredNotGuessedAt()
        {
            var command = ConnectCommand.From(new JObject { ["type"] = "doSomethingNew" });
            Assert.That(command.Kind, Is.EqualTo(ConnectCommandKind.Unknown),
                "guessing at a command we do not understand could do the wrong thing to playback");
        }

        [Test]
        public void AnUnknownPairingStatusKeepsPollingRatherThanGivingUp()
        {
            var poll = PairingPoll.From(new JObject { ["status"] = "somethingElse" });
            Assert.That(poll.Status, Is.EqualTo(PairingStatus.Pending),
                "one more poll costs nothing; wrongly deciding 'expired' throws away a pairing " +
                "the person has already approved on their phone");
        }

        [Test]
        public void AbsentOptionalFieldsAreAbsent_NotZero()
        {
            var item = CatalogItem.From(Item(), "");
            Assert.That(item.Year, Is.Null);
            Assert.That(item.RuntimeMs, Is.Null);
            Assert.That(item.Overview, Is.Null);
            Assert.That(item.Poster, Is.Null);
            Assert.That(item.Progress, Is.Null, "never played is not the same as played to zero");
            Assert.That(item.Genres, Is.Empty);
        }

        [Test]
        public void AMalformedOptionalArrayIsEmptyRatherThanFatal()
        {
            var item = CatalogItem.From(Item(("genres", "not an array")), "");
            Assert.That(item.Genres, Is.Empty);
        }

        [Test]
        public void ANullOptionalIsTreatedAsAbsent()
        {
            var item = CatalogItem.From(Item(("year", JValue.CreateNull()),
                                             ("overview", JValue.CreateNull())), "");
            Assert.That(item.Year, Is.Null);
            Assert.That(item.Overview, Is.Null);
        }

        // ---- strict about what we do need --------------------------------------------------------

        [Test]
        public void AMissingRequiredFieldNamesItself()
        {
            var body = Item();
            body.Remove("libraryId");
            var error = Assert.Throws<StreamBoxContractException>(() => CatalogItem.From(body, "items[3]"));
            Assert.That(error.Path, Is.EqualTo("items[3].libraryId"));
            Assert.That(error.Message, Does.Contain("libraryId").And.Contain("missing"));
        }

        [Test]
        public void ARequiredFieldThatIsNullCountsAsMissing()
        {
            var error = Assert.Throws<StreamBoxContractException>(
                () => CatalogItem.From(Item(("name", JValue.CreateNull())), ""));
            Assert.That(error.Path, Is.EqualTo("name"));
        }

        [Test]
        public void ARequiredFieldThatIsEmptyCountsAsMissing()
        {
            // An empty id is worse than an absent one: it would be carried around as a handle
            // that silently matches nothing.
            var error = Assert.Throws<StreamBoxContractException>(
                () => CatalogItem.From(Item(("id", "")), ""));
            Assert.That(error.Path, Is.EqualTo("id"));
        }

        [Test]
        public void ABadElementNamesItsPositionInTheArray()
        {
            var body = new JObject
            {
                ["items"] = new JArray(Item(), Item(("id", JValue.CreateNull())), Item()),
            };
            var error = Assert.Throws<StreamBoxContractException>(() => ItemsPage.From(body));
            Assert.That(error.Path, Is.EqualTo("items[1].id"),
                "finding the bad one in a page of eighty needs the index");
        }

        [Test]
        public void AWrongTypeSaysWhatItFound()
        {
            var body = new JObject { ["items"] = "not an array" };
            var error = Assert.Throws<StreamBoxContractException>(() => ItemsPage.From(body));
            Assert.That(error.Message, Does.Contain("array").And.Contain("string"));
        }

        [Test]
        public void AnEmptyOrUnparseableBodySaysSo()
        {
            Assert.Throws<StreamBoxContractException>(() => Json.ParseObject("", "home"));
            Assert.Throws<StreamBoxContractException>(() => Json.ParseObject("{not json", "home"));
            var error = Assert.Throws<StreamBoxContractException>(() => Json.ParseObject("[1,2]", "home"));
            Assert.That(error.Message, Does.Contain("expected an object"));
        }

        // ---- held to the contract file -------------------------------------------------------------

        [Test]
        public void TheQuestProfileMatchesTheContract()
        {
            // The contract and the code could drift apart silently; this is what stops them.
            var contractPath = Path.Combine(Payloads.RepositoryRoot, "contract", "streambox", "v1.json");
            Assert.That(File.Exists(contractPath), $"the contract should be at {contractPath}");

            var contract = JObject.Parse(File.ReadAllText(contractPath));
            var declared = (JObject)contract["playback"]["islandiaCapabilities"];
            var ours = PlaybackCapabilities.Quest.ToJson();

            foreach (var field in new[] { "name", "hdr", "maxWidth", "maxHeight", "maxAudioChannels", "segmentContainer" })
            {
                Assert.That(ours[field].ToString(), Is.EqualTo(declared[field].ToString()),
                    $"the contract and PlaybackCapabilities.Quest disagree about {field}");
            }
            Assert.That(ours["video"].ToString(Newtonsoft.Json.Formatting.None),
                Is.EqualTo(declared["video"].ToString(Newtonsoft.Json.Formatting.None)));
            Assert.That(ours["audio"].ToString(Newtonsoft.Json.Formatting.None),
                Is.EqualTo(declared["audio"].ToString(Newtonsoft.Json.Formatting.None)));
        }

        [Test]
        public void EveryProgressEventKindUsesTheNameTheContractLists()
        {
            var contractPath = Path.Combine(Payloads.RepositoryRoot, "contract", "streambox", "v1.json");
            var contract = JObject.Parse(File.ReadAllText(contractPath));
            var listed = contract["progress"]["eventKinds"].ToObject<string[]>();

            foreach (ProgressEventKind kind in System.Enum.GetValues(typeof(ProgressEventKind)))
            {
                Assert.That(listed, Contains.Item(ProgressEvent.NameOf(kind)),
                    $"{kind} travels under a name the contract does not list");
            }
        }

        [Test]
        public void NeitherFinalCloseCodeIsRetried()
        {
            Assert.That(ConnectClose.IsFinal(ConnectClose.SignedOut), Is.True);
            Assert.That(ConnectClose.IsFinal(ConnectClose.Replaced), Is.True);
            Assert.That(ConnectClose.IsFinal(1006), Is.False,
                "an abnormal close is a dropped network, which should reconnect");
        }

        [Test]
        public void APollIntervalIsNeverZero()
        {
            // A server answering 0 would have the client spin as fast as it can manage.
            var start = PairingStart.From(new JObject
            {
                ["code"] = "123456", ["pollSecret"] = "s", ["approveUrl"] = "http://x",
                ["intervalSeconds"] = 0,
            });
            Assert.That(start.IntervalSeconds, Is.GreaterThanOrEqualTo(1));
        }
    }
}
