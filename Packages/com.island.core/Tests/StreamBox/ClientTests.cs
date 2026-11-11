using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Island.Core.StreamBox;
using NUnit.Framework;

namespace Island.Core.Tests.StreamBox
{
    [TestFixture]
    public class PairingWalkTests
    {
        private FakeTransport _gateway;
        private FakeClock _clock;
        private StreamBoxClient _client;

        private const string Start = @"{
            ""code"": ""WXYZ-42"",
            ""pollSecret"": ""poll-secret-value"",
            ""approveUrl"": ""https://example.invalid/approve?code=WXYZ-42"",
            ""expiresAt"": ""2026-11-11T00:10:00.000Z"",
            ""intervalSeconds"": 3
        }";

        private const string Pending = @"{ ""status"": ""pending"" }";

        private const string Approved = @"{
            ""status"": ""approved"",
            ""deviceToken"": ""device-token-value"",
            ""userName"": ""Copper"",
            ""profileName"": ""Headset""
        }";

        [SetUp]
        public void SetUp()
        {
            _gateway = new FakeTransport();
            _clock = new FakeClock();
            _client = new StreamBoxClient(_gateway, _clock);
        }

        [Test]
        public void AFreshClientHasNothingToShowButPairing()
        {
            Assert.That(_client.State, Is.EqualTo(SessionState.Unpaired));
            Assert.That(_client.TokenToPersist(), Is.Null);
        }

        [Test]
        public async Task StartingAWalkGivesTheCodeAndTheQrAddress()
        {
            _gateway.Answer(HttpVerb.Post, "/pair/start", Start);

            var start = await _client.BeginPairingAsync("Islandia — Quest");

            Assert.That(start.Code, Is.EqualTo("WXYZ-42"));
            Assert.That(start.ApproveUrl, Does.Contain("WXYZ-42"));
            Assert.That(start.IntervalSeconds, Is.EqualTo(3));
            Assert.That(_client.State, Is.EqualTo(SessionState.Pairing));
        }

        [Test]
        public async Task TheDeviceNameGoesToThePhoneSoItCanBeRecognised()
        {
            _gateway.Answer(HttpVerb.Post, "/pair/start", Start);
            await _client.BeginPairingAsync("Islandia — Quest");
            Assert.That(_gateway.Bodies[0], Does.Contain("Islandia"));
        }

        [Test]
        public void PairingWithoutANameIsRefused()
        {
            // The phone shows this before approving. A blank name asks someone to approve
            // something unidentifiable.
            Assert.ThrowsAsync<ArgumentException>(() => _client.BeginPairingAsync("  "));
            Assert.ThrowsAsync<ArgumentException>(() => _client.BeginPairingAsync(null));
        }

        [Test]
        public async Task StartingDoesNotSendACredentialBecauseThereIsNotOneYet()
        {
            _gateway.Answer(HttpVerb.Post, "/pair/start", Start);
            await _client.BeginPairingAsync("Islandia — Quest");
            Assert.That(_gateway.Tokens[0], Is.Null);
        }

        [Test]
        public async Task ItPollsAtTheIntervalTheServerAskedFor()
        {
            // The server's choice, not ours. Polling faster than asked is how a device flow earns
            // a rate limit, and the limiter would then deny the pairing it was trying to finish.
            _gateway.Answer(HttpVerb.Post, "/pair/start", Start);
            _gateway.Answer(HttpVerb.Post, "/pair/poll", Pending);
            _gateway.Answer(HttpVerb.Post, "/pair/poll", Pending);
            _gateway.Answer(HttpVerb.Post, "/pair/poll", Approved);

            var start = await _client.BeginPairingAsync("Islandia — Quest");
            await _client.AwaitApprovalAsync(start);

            Assert.That(_clock.Delays, Is.EqualTo(new[] { TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3) }));
        }

        [Test]
        public async Task ItNeverPollsFasterThanOnceASecondHoweverEagerTheServerSays()
        {
            _gateway.Answer(HttpVerb.Post, "/pair/start", Start.Replace("\"intervalSeconds\": 3", "\"intervalSeconds\": 0"));
            _gateway.Answer(HttpVerb.Post, "/pair/poll", Pending);
            _gateway.Answer(HttpVerb.Post, "/pair/poll", Approved);

            var start = await _client.BeginPairingAsync("Islandia — Quest");
            await _client.AwaitApprovalAsync(start);

            Assert.That(_clock.Delays[0], Is.EqualTo(TimeSpan.FromSeconds(1)));
        }

        [Test]
        public async Task ApprovalLeavesTheClientReadyAndHoldingTheToken()
        {
            _gateway.Answer(HttpVerb.Post, "/pair/start", Start);
            _gateway.Answer(HttpVerb.Post, "/pair/poll", Approved);

            var start = await _client.BeginPairingAsync("Islandia — Quest");
            var outcome = await _client.AwaitApprovalAsync(start);

            Assert.That(outcome.Approved, Is.True);
            Assert.That(outcome.UserName, Is.EqualTo("Copper"));
            Assert.That(outcome.ProfileName, Is.EqualTo("Headset"));
            Assert.That(_client.State, Is.EqualTo(SessionState.Ready));
            Assert.That(_client.TokenToPersist(), Is.EqualTo("device-token-value"));
        }

        [Test]
        public async Task ADenialEndsTheWalkRatherThanPollingOn()
        {
            // A retry is a new walk with a new code, not another poll on a secret the owner has
            // already said no to.
            _gateway.Answer(HttpVerb.Post, "/pair/start", Start);
            _gateway.Answer(HttpVerb.Post, "/pair/poll", @"{ ""status"": ""denied"" }");

            var start = await _client.BeginPairingAsync("Islandia — Quest");
            var outcome = await _client.AwaitApprovalAsync(start);

            Assert.That(outcome.Status, Is.EqualTo(PairingStatus.Denied));
            Assert.That(outcome.Approved, Is.False);
            Assert.That(_client.State, Is.EqualTo(SessionState.Unpaired));
            Assert.That(_client.TokenToPersist(), Is.Null);
        }

        [Test]
        public async Task AnExpiredCodeEndsTheWalkToo()
        {
            _gateway.Answer(HttpVerb.Post, "/pair/start", Start);
            _gateway.Answer(HttpVerb.Post, "/pair/poll", @"{ ""status"": ""expired"" }");

            var start = await _client.BeginPairingAsync("Islandia — Quest");
            var outcome = await _client.AwaitApprovalAsync(start);

            Assert.That(outcome.Status, Is.EqualTo(PairingStatus.Expired));
            Assert.That(_client.State, Is.EqualTo(SessionState.Unpaired));
        }

        [Test]
        public async Task TheWholeWalkCanBeDrivenInOneCall()
        {
            _gateway.Answer(HttpVerb.Post, "/pair/start", Start);
            _gateway.Answer(HttpVerb.Post, "/pair/poll", Approved);

            PairingStart shown = null;
            var outcome = await _client.PairAsync("Islandia — Quest", start => shown = start);

            Assert.That(shown, Is.Not.Null, "the code must reach the screen before the wait begins");
            Assert.That(shown.Code, Is.EqualTo("WXYZ-42"));
            Assert.That(outcome.Approved, Is.True);
        }

        [Test]
        public async Task GivingUpOnAWalkStopsThePolling()
        {
            _gateway.Answer(HttpVerb.Post, "/pair/start", Start);
            _gateway.Always(HttpVerb.Post, "/pair/poll", 200, Pending);

            var start = await _client.BeginPairingAsync("Islandia — Quest");
            using (var cancel = new CancellationTokenSource())
            {
                cancel.Cancel();

                // CatchAsync, not ThrowsAsync: ThrowsAsync demands the exact type, and
                // cancellation surfaces as OperationCanceledException under .NET 9 but as its
                // subclass TaskCanceledException under Unity's runtime. The contract is that
                // cancelling throws, not which flavour — and the Unity test run is what found
                // this, because `dotnet test` alone never saw the difference.
                Assert.CatchAsync<OperationCanceledException>(
                    () => _client.AwaitApprovalAsync(start, cancel.Token));
            }
        }

        [Test]
        public async Task TheStateChangeIsAnnouncedSoTheScreenCanFollowIt()
        {
            var seen = new List<SessionState>();
            _client.StateChanged += state => seen.Add(state);

            _gateway.Answer(HttpVerb.Post, "/pair/start", Start);
            _gateway.Answer(HttpVerb.Post, "/pair/poll", Approved);
            await _client.PairAsync("Islandia — Quest", null);

            Assert.That(seen, Is.EqualTo(new[] { SessionState.Pairing, SessionState.Ready }));
        }

        [Test]
        public async Task ResumingASavedTokenSkipsPairingEntirely()
        {
            _client.Resume("saved-token", "Copper", "Headset");
            Assert.That(_client.State, Is.EqualTo(SessionState.Ready));
            Assert.That(_client.UserName, Is.EqualTo("Copper"));

            _gateway.Answer(HttpVerb.Get, "/catalog/home", @"{ ""rows"": [] }");
            await _client.HomeAsync();
            Assert.That(_gateway.Tokens[0], Is.EqualTo("saved-token"));
        }

        [Test]
        public void ResumingNothingIsAMistakeWorthHearingAbout()
        {
            Assert.Throws<ArgumentException>(() => _client.Resume(null));
            Assert.Throws<ArgumentException>(() => _client.Resume(""));
        }
    }

    [TestFixture]
    public class SignedOutTests
    {
        private FakeTransport _gateway;
        private StreamBoxClient _client;

        [SetUp]
        public void SetUp()
        {
            _gateway = new FakeTransport();
            _client = new StreamBoxClient(_gateway, new FakeClock());
            _client.Resume("device-token-value");
        }

        [Test]
        public void A401SendsTheClientBackToPairing()
        {
            // The one status with a required consequence. Treating it as an ordinary error would
            // leave the app retrying with a credential that will never work again.
            _gateway.Answer(HttpVerb.Get, "/catalog/home", 401, @"{ ""error"": ""unauthorised"" }");

            Assert.ThrowsAsync<StreamBoxSignedOutException>(() => _client.HomeAsync());
            Assert.That(_client.State, Is.EqualTo(SessionState.Unpaired));
            Assert.That(_client.TokenToPersist(), Is.Null, "the dead token must not be kept");
        }

        [Test]
        public void A401ClearsWhoTheLibraryBelongedTo()
        {
            _client.Resume("device-token-value", "Copper", "Headset");
            _gateway.Answer(HttpVerb.Get, "/catalog/home", 401, "{}");

            Assert.ThrowsAsync<StreamBoxSignedOutException>(() => _client.HomeAsync());
            Assert.That(_client.UserName, Is.Null);
            Assert.That(_client.ProfileName, Is.Null);
        }

        [Test]
        public void CallingAnythingWhileUnpairedIsRefusedBeforeItReachesTheNetwork()
        {
            _client.SignOut();
            Assert.ThrowsAsync<StreamBoxSignedOutException>(() => _client.HomeAsync());
            Assert.That(_gateway.Sent, Is.Zero, "there is no point asking without a credential");
        }

        [Test]
        public void SigningOutForgetsEverything()
        {
            _client.Resume("device-token-value", "Copper", "Headset");
            _client.SignOut();

            Assert.That(_client.State, Is.EqualTo(SessionState.Unpaired));
            Assert.That(_client.TokenToPersist(), Is.Null);
            Assert.That(_client.UserName, Is.Null);
        }

        [Test]
        public void AnotherFailureIsJustAFailureAndKeepsTheSession()
        {
            _gateway.Answer(HttpVerb.Get, "/catalog/home", 500, "{}");

            var error = Assert.ThrowsAsync<StreamBoxRequestException>(() => _client.HomeAsync());
            Assert.That(error.Status, Is.EqualTo(500));
            Assert.That(_client.State, Is.EqualTo(SessionState.Ready), "a server fault is not a sign-out");
        }

        [Test]
        public void AnErrorNeverCarriesTheCredential()
        {
            // The device token is the one secret Islandia holds. An exception message is the
            // easiest place for it to escape into a log.
            _gateway.Answer(HttpVerb.Get, "/catalog/home", 500, "{}");

            var error = Assert.ThrowsAsync<StreamBoxRequestException>(() => _client.HomeAsync());
            Assert.That(error.Message, Does.Not.Contain("device-token-value"));
            Assert.That(error.Call, Does.Not.Contain("device-token-value"));
        }
    }

    [TestFixture]
    public class ClientCallTests
    {
        private FakeTransport _gateway;
        private StreamBoxClient _client;

        [SetUp]
        public void SetUp()
        {
            _gateway = new FakeTransport();
            _client = new StreamBoxClient(_gateway, new FakeClock());
            _client.Resume("device-token-value");
        }

        [Test]
        public async Task EveryCallAfterPairingCarriesTheToken()
        {
            _gateway.Answer(HttpVerb.Get, "/catalog/home", @"{ ""rows"": [] }");
            await _client.HomeAsync();
            Assert.That(_gateway.Tokens[0], Is.EqualTo("device-token-value"));
        }

        [Test]
        public async Task SearchEscapesWhatThePersonTyped()
        {
            // A title with a space, an ampersand or a slash in it must not change the path.
            _gateway.Always(HttpVerb.Get, "/catalog/search?q=dog%20%26%20bird%2Fcat", 200,
                @"{ ""items"": [], ""total"": 0 }");

            await _client.SearchAsync("dog & bird/cat");
            Assert.That(_gateway.Calls[0], Is.EqualTo("GET /catalog/search?q=dog%20%26%20bird%2Fcat"));
        }

        [Test]
        public async Task ALibraryIdIsEscapedToo()
        {
            _gateway.Always(HttpVerb.Get, "/catalog/libraries/mk%3Alib-1/items", 200,
                @"{ ""items"": [], ""total"": 0 }");
            await _client.LibraryItemsAsync("mk:lib-1");
            Assert.That(_gateway.Calls[0], Does.Contain("mk%3Alib-1"));
        }

        [Test]
        public void AskingForNoLibraryIsRefused()
        {
            Assert.ThrowsAsync<ArgumentException>(() => _client.LibraryItemsAsync(""));
            Assert.ThrowsAsync<ArgumentNullException>(() => _client.SearchAsync(null));
        }

        [Test]
        public async Task StoppingAStreamSendsADeleteForThatTokenOnly()
        {
            _gateway.Answer(HttpVerb.Delete, "/play/stream-token", 204, "");
            await _client.StopAsync("stream-token");
            Assert.That(_gateway.Calls[0], Is.EqualTo("DELETE /play/stream-token"));
        }

        [Test]
        public void StoppingNothingIsRefused()
        {
            Assert.ThrowsAsync<ArgumentException>(() => _client.StopAsync(null));
        }

        [Test]
        public async Task ReportingNoProgressSendsNothing()
        {
            // An empty batch is one the gateway refuses, and having nothing to say is not an error.
            await _client.ReportProgressAsync(Array.Empty<ProgressEvent>());
            Assert.That(_gateway.Sent, Is.Zero);
        }

        [Test]
        public void ReportingNullIsAMistake()
        {
            Assert.ThrowsAsync<ArgumentNullException>(() => _client.ReportProgressAsync(null));
        }

        [Test]
        public async Task AProgressBatchCarriesTheEnvelopeTheGatewayExpects()
        {
            _gateway.Answer(HttpVerb.Post, "/progress/events", 202, "{}");
            var reported = new ProgressEvent(
                id: "11111111-1111-4111-8111-111111111111",
                kind: ProgressEventKind.Heartbeat,
                itemId: "mk:film-1",
                profileId: "mk:profile-1",
                playbackId: "playback-1",
                playbackStartedAt: "2026-11-11T00:00:00.000Z",
                clientTs: "2026-11-11T00:00:30.000Z",
                positionMs: 30_000);

            await _client.ReportProgressAsync(new[] { reported });

            Assert.That(_gateway.Bodies[0], Does.Contain("\"sentAt\""),
                "the envelope is ProgressEvent.Batch's job and must not be rebuilt here");
            Assert.That(_gateway.Bodies[0], Does.Contain("mk:film-1"));
        }

        [Test]
        public async Task AskingForATrackGivesASignedAddressThatNeedsNoHeader()
        {
            _gateway.Answer(HttpVerb.Post, "/music/play",
                @"{ ""url"": ""/api/audio/abc123/track.m4a?token=signed"", ""durationMs"": 215000 }");

            var play = await _client.MusicPlayAsync("mk:track-1");

            Assert.That(play.Url, Does.Contain("token="));
            Assert.That(play.DurationMs, Is.EqualTo(215_000));
        }

        [Test]
        public void AskingForNoTrackIsRefused()
        {
            Assert.ThrowsAsync<ArgumentException>(() => _client.MusicPlayAsync(""));
        }

        [Test]
        public async Task AHomeWithNoRowsIsAnEmptyLibraryNotAFailure()
        {
            _gateway.Answer(HttpVerb.Get, "/catalog/home", @"{ ""rows"": [] }");
            Assert.That(await _client.HomeAsync(), Is.Empty);
        }

        [Test]
        public void ANoTransportClientIsRefusedAtConstruction()
        {
            Assert.Throws<ArgumentNullException>(() => new StreamBoxClient(null));
        }
    }
}
