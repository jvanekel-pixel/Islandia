using System.IO;
using Island.Core.StreamBox;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Island.Core.Tests.StreamBox
{
    /// <summary>
    /// Maps real captured responses. If StreamBox changes shape, these fail.
    /// </summary>
    [TestFixture]
    public class MappingTests
    {
        [Test]
        public void PairingStartCarriesWhatTheHeadsetMustShow()
        {
            var start = PairingStart.From(Payloads.Load("pair-start"));
            Assert.That(start.Code, Does.Match(@"^\d{6}$"));
            Assert.That(start.PollSecret, Is.Not.Null.And.Not.Empty);
            Assert.That(start.ApproveUrl, Is.Not.Null, "this goes in the QR code");
            Assert.That(start.IntervalSeconds, Is.GreaterThan(0));
        }

        [Test]
        public void APendingPollCarriesNoToken()
        {
            var poll = PairingPoll.From(Payloads.Load("pair-poll-pending"));
            Assert.That(poll.Status, Is.EqualTo(PairingStatus.Pending));
            Assert.That(poll.DeviceToken, Is.Null);
        }

        [Test]
        public void AnApprovedPollCarriesTheToken()
        {
            var poll = PairingPoll.From(Payloads.Load("pair-poll-approved"));
            Assert.That(poll.Status, Is.EqualTo(PairingStatus.Approved));
            Assert.That(poll.DeviceToken, Is.Not.Null.And.Not.Empty);
            Assert.That(poll.UserName, Is.Not.Null);
        }

        [Test]
        public void HomeRowsMapWithTheirItems()
        {
            var body = Payloads.Load("catalog-home");
            var rows = Json.RequiredArray(body, "rows", "", HomeRow.From);
            Assert.That(rows, Is.Not.Empty);
            Assert.That(rows[0].Title, Is.Not.Null.And.Not.Empty);
            Assert.That(rows[0].Items, Is.Not.Empty);
        }

        [Test]
        public void ItemsCarryWhatAScreenNeedsToDrawThem()
        {
            var body = Payloads.Load("catalog-latest");
            var items = Json.RequiredArray(body, "items", "", CatalogItem.From);
            Assert.That(items, Is.Not.Empty);

            var film = items[0];
            Assert.That(film.Id, Is.Not.Null.And.Not.Empty);
            Assert.That(film.Kind, Is.EqualTo(ItemKind.Movie));
            Assert.That(film.Name, Is.Not.Null.And.Not.Empty);
            Assert.That(film.LibraryId, Is.Not.Null.And.Not.Empty);
            Assert.That(film.Year, Is.Not.Null);
            Assert.That(film.Poster, Is.Not.Null, "a film with no poster leaves a hole on the shelf");
            Assert.That(film.Poster.Url, Does.StartWith("/api/"),
                "artwork must come through the gateway, never from the engine directly");
            Assert.That(film.Genres, Is.Not.Empty);
        }

        [Test]
        public void ALibraryPageSaysWhereTheNextOneStarts()
        {
            var page = ItemsPage.From(Payloads.Load("catalog-library-page"));
            Assert.That(page.Items.Count, Is.LessThanOrEqualTo(page.Limit));
            Assert.That(page.Total, Is.GreaterThanOrEqualTo(page.Items.Count));
            if (page.Total > page.Items.Count)
            {
                Assert.That(page.HasMore, Is.True);
                Assert.That(page.NextOffset, Is.GreaterThan(page.Offset));
            }
        }

        [Test]
        public void ARemuxIsReportedWithNoReasons()
        {
            var play = PlayResponse.From(Payloads.Load("play-remux"));
            Assert.That(play.Decision, Is.EqualTo(StreamDecision.Remux));
            Assert.That(play.Reasons, Is.Empty, "there is nothing to explain when the source was copied");
            Assert.That(play.Url, Does.Contain(".m3u8"));
            Assert.That(play.StreamToken, Is.Not.Null, "stopping the stream needs this");
        }

        [Test]
        public void ATranscodeComesWithAReason()
        {
            var play = PlayResponse.From(Payloads.Load("play-transcode"));
            Assert.That(play.Decision, Is.EqualTo(StreamDecision.Transcode));
            Assert.That(play.Reasons, Is.Not.Empty,
                "a transcode costs the server real work; it should always say why");
        }

        [Test]
        public void OnlyTextSubtitlesAreOffered()
        {
            var play = PlayResponse.From(Payloads.Load("play-remux"));
            foreach (var subtitle in play.Subtitles)
            {
                Assert.That(subtitle.Url, Is.Not.Null,
                    "image subtitles are burned in by the server and never listed for us to render");
            }
        }

        [Test]
        public void AnAlbumMapsWithItsTracksInOrder()
        {
            var album = MusicAlbum.From(Payloads.Load("music-album"), "");
            Assert.That(album.Name, Is.Not.Null.And.Not.Empty);
            Assert.That(album.Tracks, Is.Not.Empty);
            Assert.That(album.Tracks[0].DurationMs, Is.GreaterThan(0));
            for (var i = 1; i < album.Tracks.Count; i++)
            {
                Assert.That(album.Tracks[i].TrackNumber, Is.GreaterThan(album.Tracks[i - 1].TrackNumber));
            }
        }

        [Test]
        public void AMusicAddressIsSignedSoNoHeaderIsNeeded()
        {
            var play = MusicPlayResponse.From(Payloads.Load("music-play"));
            Assert.That(play.Url, Does.Contain("token="),
                "this is what lets the headset fetch a track into a cache without a header");
        }

        [Test]
        public void ProgressMapsAndComputesAFraction()
        {
            var state = PlaybackProgress.From(Payloads.Load("progress-state"), "");
            Assert.That(state.PositionMs, Is.EqualTo(65_000));
            Assert.That(state.DurationMs, Is.EqualTo(1_440_000));
            Assert.That(state.Fraction, Is.EqualTo(65_000d / 1_440_000d).Within(0.0001));
            Assert.That(state.InProgress, Is.True);
        }

        [Test]
        public void TheNextEpisodeMaps()
        {
            var body = Payloads.Load("progress-next-episode");
            var episode = CatalogItem.From(Json.RequiredObject(body, "episode", ""), "episode");
            Assert.That(episode.Kind, Is.EqualTo(ItemKind.Episode));
            Assert.That(episode.EpisodeNumber, Is.EqualTo(2));
            Assert.That(episode.SeriesName, Is.Not.Null, "an episode should know its programme");
        }

        [Test]
        public void TheConnectWelcomeMaps()
        {
            var message = ServerMessage.From(Payloads.Load("connect-welcome"));
            Assert.That(message.Kind, Is.EqualTo(ServerMessageKind.Welcome));
            Assert.That(message.SessionId, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void ASeekCommandFromThePhoneMaps()
        {
            var message = ServerMessage.From(Payloads.Load("connect-command-seek"));
            Assert.That(message.Kind, Is.EqualTo(ServerMessageKind.Command));
            Assert.That(message.Command.Kind, Is.EqualTo(ConnectCommandKind.Seek));
            Assert.That(message.Command.PositionMs, Is.EqualTo(42_000));
        }
    }
}
