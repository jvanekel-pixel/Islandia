using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Island.Core.StreamBox;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Island.Core.Tests.StreamBox
{
    /// <summary>A Connect socket that says what a test tells it to and remembers what it was sent.</summary>
    internal sealed class FakeSocket : ISocketTransport
    {
        private readonly Queue<string> _inbound = new Queue<string>();

        public FakeSocket(int? closeCodeWhenDrained = null) => DrainedCloseCode = closeCodeWhenDrained;

        /// <summary>What <see cref="CloseCode"/> becomes once the scripted messages run out.</summary>
        public int? DrainedCloseCode { get; set; }

        /// <summary>When set, connecting throws — a gateway that is not answering.</summary>
        public bool RefuseToConnect { get; set; }

        /// <summary>
        /// When set, running out of scripted messages blocks instead of closing, so a test can
        /// drive sends against a live session rather than racing the pump's exit.
        /// </summary>
        public bool HoldOpenWhenDrained { get; set; }

        private readonly SemaphoreSlim _held = new SemaphoreSlim(0);

        /// <summary>Lets a held-open socket finish, so the pump can exit.</summary>
        public void Release() => _held.Release();

        public List<string> Sent { get; } = new List<string>();
        public int? CloseCode { get; private set; }
        public bool Connected { get; private set; }
        public bool Closed { get; private set; }
        public bool Disposed { get; private set; }
        public string PathUsed { get; private set; }

        public FakeSocket Say(string json)
        {
            _inbound.Enqueue(json);
            return this;
        }

        public FakeSocket Say(JObject body) => Say(body.ToString());

        public Task ConnectAsync(string path, CancellationToken cancellation)
        {
            PathUsed = path;
            if (RefuseToConnect) throw new InvalidOperationException("nothing is listening");
            Connected = true;
            return Task.CompletedTask;
        }

        public Task SendAsync(string json, CancellationToken cancellation)
        {
            Sent.Add(json);
            return Task.CompletedTask;
        }

        public async Task<string> ReceiveAsync(CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            if (_inbound.Count > 0) return _inbound.Dequeue();

            if (HoldOpenWhenDrained) await _held.WaitAsync(cancellation).ConfigureAwait(false);

            CloseCode = DrainedCloseCode;
            return null;
        }

        public Task CloseAsync(CancellationToken cancellation)
        {
            Closed = true;
            return Task.CompletedTask;
        }

        public void Dispose() => Disposed = true;
    }

    /// <summary>
    /// Hands out scripted sockets, then a final one, so a reconnect loop always terminates in a
    /// test rather than spinning until the suite times out.
    /// </summary>
    internal sealed class FakeSocketFactory : ISocketFactory
    {
        private readonly Queue<FakeSocket> _queued = new Queue<FakeSocket>();

        public List<FakeSocket> Made { get; } = new List<FakeSocket>();

        public FakeSocketFactory Then(FakeSocket socket)
        {
            _queued.Enqueue(socket);
            return this;
        }

        public ISocketTransport Create()
        {
            var socket = _queued.Count > 0
                ? _queued.Dequeue()
                : new FakeSocket(ConnectClose.Replaced);
            Made.Add(socket);
            return socket;
        }
    }

    [TestFixture]
    public class ConnectSessionTests
    {
        private FakeSocketFactory _sockets;
        private FakeClock _clock;

        private static JObject Welcome(string sessionId = "session-1") =>
            new JObject { ["type"] = "welcome", ["sessionId"] = sessionId };

        private static JObject Command(string type, params object[] pairs)
        {
            var command = new JObject { ["type"] = type };
            for (var i = 0; i + 1 < pairs.Length; i += 2)
                command[(string)pairs[i]] = JToken.FromObject(pairs[i + 1]);
            return new JObject { ["type"] = "command", ["command"] = command };
        }

        [SetUp]
        public void SetUp()
        {
            _sockets = new FakeSocketFactory();
            _clock = new FakeClock();
        }

        private ConnectSession Session() => SessionWith(Fake.DeviceToken);

        private ConnectSession SessionWith(string token) =>
            new ConnectSession(_sockets, () => token, _clock);

        // ---- introductions ---------------------------------------------------------------------

        [Test]
        public async Task ItIntroducesItselfBeforeAnythingElse()
        {
            var socket = new FakeSocket(ConnectClose.Replaced).Say(Welcome());
            _sockets.Then(socket);

            await Session().RunAsync(CancellationToken.None);

            Assert.That(socket.PathUsed, Is.EqualTo("/api/connect"));
            Assert.That(socket.Sent, Is.Not.Empty);

            var hello = JObject.Parse(socket.Sent[0]);
            Assert.That(hello["type"]?.Value<string>(), Is.EqualTo("hello"));
            Assert.That(hello["role"]?.Value<string>(), Is.EqualTo("tv"),
                "the island registers as a place to play");
            Assert.That(hello["token"]?.Value<string>(), Is.EqualTo(Fake.DeviceToken));
        }

        [Test]
        public async Task WelcomeMakesItLive()
        {
            var seen = new List<ConnectStatus>();
            var session = Session();
            session.StatusChanged += status => seen.Add(status);
            _sockets.Then(new FakeSocket(ConnectClose.Replaced).Say(Welcome("abc")));

            await session.RunAsync(CancellationToken.None);

            Assert.That(seen, Does.Contain(ConnectStatus.Introducing));
            Assert.That(seen, Does.Contain(ConnectStatus.Live));
            Assert.That(session.SessionId, Is.EqualTo("abc"));
        }

        [Test]
        public async Task WithNothingPairedItDoesNotEvenTry()
        {
            // Connecting without a token earns a 4401, which would then be read as a sign-out and
            // send the app round a loop it could have skipped.
            var session = SessionWith(null);
            var signedOut = 0;
            session.SignedOut += () => signedOut++;

            await session.RunAsync(CancellationToken.None);

            Assert.That(_sockets.Made, Is.Empty, "no socket should have been opened");
            Assert.That(session.Status, Is.EqualTo(ConnectStatus.Done));
            Assert.That(signedOut, Is.EqualTo(1));
        }

        // ---- commands --------------------------------------------------------------------------

        [Test]
        public async Task ACommandFromThePhoneArrives()
        {
            var received = new List<ConnectCommand>();
            var session = Session();
            session.CommandReceived += command => received.Add(command);

            _sockets.Then(new FakeSocket(ConnectClose.Replaced)
                .Say(Welcome())
                .Say(Command("play", "itemId", "mk:film-1", "positionMs", 42000L)));

            await session.RunAsync(CancellationToken.None);

            Assert.That(received, Has.Count.EqualTo(1));
            Assert.That(received[0].Kind, Is.EqualTo(ConnectCommandKind.Play));
            Assert.That(received[0].ItemId, Is.EqualTo("mk:film-1"));
            Assert.That(received[0].PositionMs, Is.EqualTo(42000L));
        }

        [Test]
        public async Task EveryCommandTheContractNamesIsRecognised()
        {
            var kinds = new List<ConnectCommandKind>();
            var session = Session();
            session.CommandReceived += command => kinds.Add(command.Kind);

            var socket = new FakeSocket(ConnectClose.Replaced).Say(Welcome());
            foreach (var name in new[] { "play", "pause", "resume", "seek", "seekBy", "stop",
                                         "next", "audio", "subtitle", "volume", "mute" })
                socket.Say(Command(name));
            _sockets.Then(socket);

            await session.RunAsync(CancellationToken.None);

            Assert.That(kinds, Is.EqualTo(new[]
            {
                ConnectCommandKind.Play, ConnectCommandKind.Pause, ConnectCommandKind.Resume,
                ConnectCommandKind.Seek, ConnectCommandKind.SeekBy, ConnectCommandKind.Stop,
                ConnectCommandKind.Next, ConnectCommandKind.Audio, ConnectCommandKind.Subtitle,
                ConnectCommandKind.Volume, ConnectCommandKind.Mute,
            }));
        }

        [Test]
        public async Task ACommandThisBuildDoesNotKnowIsIgnoredRatherThanGuessedAt()
        {
            // StreamBox will add commands. Guessing is how a newer phone makes an older island do
            // the wrong thing.
            var received = 0;
            var session = Session();
            session.CommandReceived += _ => received++;

            _sockets.Then(new FakeSocket(ConnectClose.Replaced)
                .Say(Welcome())
                .Say(Command("teleportTheSofa"))
                .Say(Command("pause")));

            await session.RunAsync(CancellationToken.None);
            Assert.That(received, Is.EqualTo(1), "only the one it understands");
        }

        [Test]
        public async Task AMessageItCannotParseDoesNotDropTheSocket()
        {
            var received = 0;
            var session = Session();
            session.CommandReceived += _ => received++;

            _sockets.Then(new FakeSocket(ConnectClose.Replaced)
                .Say(Welcome())
                .Say("{not json at all")
                .Say(Command("pause")));

            await session.RunAsync(CancellationToken.None);
            Assert.That(received, Is.EqualTo(1), "the socket survived the bad message");
        }

        [Test]
        public async Task AMessageKindItDoesNotKnowIsHarmless()
        {
            var session = Session();
            _sockets.Then(new FakeSocket(ConnectClose.Replaced)
                .Say(Welcome())
                .Say(new JObject { ["type"] = "somethingNewEntirely", ["payload"] = 7 }));

            await session.RunAsync(CancellationToken.None);
            Assert.That(session.FinalCloseCode, Is.EqualTo(ConnectClose.Replaced));
        }

        // ---- the two codes that must not be retried --------------------------------------------

        [Test]
        public async Task SignedOutEndsItAndSaysSo()
        {
            var signedOut = 0;
            var session = Session();
            session.SignedOut += () => signedOut++;
            _sockets.Then(new FakeSocket(ConnectClose.SignedOut).Say(Welcome()));

            await session.RunAsync(CancellationToken.None);

            Assert.That(session.Status, Is.EqualTo(ConnectStatus.Done));
            Assert.That(session.FinalCloseCode, Is.EqualTo(4401));
            Assert.That(signedOut, Is.EqualTo(1), "the app must go back to the pairing screen");
            Assert.That(_sockets.Made, Has.Count.EqualTo(1), "reconnecting would fail forever");
        }

        [Test]
        public async Task BeingReplacedEndsItQuietly()
        {
            // The owner has directed that playback be one-at-a-time with the newest device
            // winning. Reconnecting here would be this island fighting the phone for the stream.
            var signedOut = 0;
            var session = Session();
            session.SignedOut += () => signedOut++;
            _sockets.Then(new FakeSocket(ConnectClose.Replaced).Say(Welcome()));

            await session.RunAsync(CancellationToken.None);

            Assert.That(session.Status, Is.EqualTo(ConnectStatus.Done));
            Assert.That(session.FinalCloseCode, Is.EqualTo(4409));
            Assert.That(signedOut, Is.Zero, "being replaced is not being signed out");
            Assert.That(_sockets.Made, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task AnOrdinaryDropIsRetried()
        {
            _sockets
                .Then(new FakeSocket(1006).Say(Welcome()))
                .Then(new FakeSocket(1006).Say(Welcome()))
                .Then(new FakeSocket(ConnectClose.Replaced).Say(Welcome()));

            var session = Session();
            await session.RunAsync(CancellationToken.None);

            Assert.That(_sockets.Made, Has.Count.EqualTo(3));
            Assert.That(session.Attempts, Is.EqualTo(3));
        }

        [Test]
        public async Task AGatewayThatWillNotAnswerIsRetried()
        {
            _sockets
                .Then(new FakeSocket { RefuseToConnect = true })
                .Then(new FakeSocket(ConnectClose.Replaced).Say(Welcome()));

            await Session().RunAsync(CancellationToken.None);
            Assert.That(_sockets.Made, Has.Count.EqualTo(2));
        }

        [Test]
        public async Task TheWaitBetweenAttemptsDoubles()
        {
            for (var i = 0; i < 4; i++) _sockets.Then(new FakeSocket(1006).Say(Welcome()));
            _sockets.Then(new FakeSocket(ConnectClose.Replaced).Say(Welcome()));

            await Session().RunAsync(CancellationToken.None);

            Assert.That(_clock.Delays, Is.EqualTo(new[]
            {
                TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(8),
            }));
        }

        [Test]
        public async Task TheWaitStopsGrowingAtTheCap()
        {
            // A gateway that is down comes back on its own schedule, and a headset hammering it
            // helps nobody — but nor should it wait ten minutes once it is back.
            for (var i = 0; i < 12; i++) _sockets.Then(new FakeSocket(1006).Say(Welcome()));
            _sockets.Then(new FakeSocket(ConnectClose.Replaced).Say(Welcome()));

            await Session().RunAsync(CancellationToken.None);

            Assert.That(_clock.Delays[_clock.Delays.Count - 1], Is.EqualTo(ConnectSession.MaxBackoff));
            foreach (var delay in _clock.Delays)
                Assert.That(delay, Is.LessThanOrEqualTo(ConnectSession.MaxBackoff));
        }

        [Test]
        public async Task EverySocketIsClosedAndDisposedWhenItIsFinishedWith()
        {
            var first = new FakeSocket(1006).Say(Welcome());
            var last = new FakeSocket(ConnectClose.Replaced).Say(Welcome());
            _sockets.Then(first).Then(last);

            await Session().RunAsync(CancellationToken.None);

            foreach (var socket in new[] { first, last })
            {
                Assert.That(socket.Closed, Is.True);
                Assert.That(socket.Disposed, Is.True);
            }
        }

        [Test]
        public void BeingCancelledLeavesItOfflineRatherThanFinished()
        {
            // The app may want to resume. Only a close code the gateway chose, or Stop(), means
            // never again.
            var session = Session();
            using (var cancel = new CancellationTokenSource())
            {
                cancel.Cancel();
                Assert.ThrowsAsync<OperationCanceledException>(() => session.RunAsync(cancel.Token));
            }
            Assert.That(session.Status, Is.Not.EqualTo(ConnectStatus.Done));
        }

        [Test]
        public async Task StopMeansNeverAgain()
        {
            var session = Session();
            session.Stop();
            await session.RunAsync(CancellationToken.None);
            Assert.That(_sockets.Made, Is.Empty);
            Assert.That(session.Status, Is.EqualTo(ConnectStatus.Done));
        }
    }

    [TestFixture]
    public class ConnectStateReportingTests
    {
        private FakeSocketFactory _sockets;
        private FakeClock _clock;
        private FakeSocket _socket;
        private ConnectSession _session;
        private Task _running;

        [SetUp]
        public async Task SetUp()
        {
            _sockets = new FakeSocketFactory();
            _clock = new FakeClock();

            // A socket that stays open, so the test drives sends rather than racing the pump.
            _socket = new FakeSocket(ConnectClose.Replaced) { HoldOpenWhenDrained = true };
            _socket.Say(new JObject { ["type"] = "welcome", ["sessionId"] = "session-1" });
            _sockets.Then(_socket);

            _session = new ConnectSession(_sockets, () => Fake.DeviceToken, _clock);
            _running = _session.RunAsync(CancellationToken.None);

            // Let the pump read the welcome.
            for (var i = 0; i < 20 && _session.Status != ConnectStatus.Live; i++) await Task.Yield();
            Assert.That(_session.Status, Is.EqualTo(ConnectStatus.Live), "the fixture did not go live");
        }

        [TearDown]
        public void TearDown()
        {
            _session.Stop();
            _socket.Release();
        }

        private static PlaybackState Playing(long positionMs = 0) =>
            new PlaybackState("mk:film-1", positionMs, playing: true) { DurationMs = 7_200_000 };

        private List<string> States() =>
            _socket.Sent.FindAll(sent => sent.Contains("\"state\""));

        [Test]
        public async Task TheFirstReportIsAlwaysSent()
        {
            Assert.That(await _session.ReportAsync(Playing()), Is.True);
            Assert.That(States(), Has.Count.EqualTo(1));
        }

        [Test]
        public async Task SomethingARemoteWouldDrawDifferentlyIsSent()
        {
            await _session.ReportAsync(Playing());
            var paused = new PlaybackState("mk:film-1", 1000, playing: false) { DurationMs = 7_200_000 };

            Assert.That(await _session.ReportAsync(paused), Is.True);
            Assert.That(States(), Has.Count.EqualTo(2));
        }

        [Test]
        public async Task ThePositionMovingIsNotAChange()
        {
            // It advances every frame. Counting it would mean seventy-two messages a second to
            // say the obvious; the periodic re-send is what keeps the phone's scrubber honest.
            await _session.ReportAsync(Playing(0));
            for (var i = 1; i <= 30; i++)
                Assert.That(await _session.ReportAsync(Playing(i * 14)), Is.False, $"frame {i}");

            Assert.That(States(), Has.Count.EqualTo(1));
        }

        [Test]
        public async Task WhilePlayingItReportsAgainEveryFewSeconds()
        {
            await _session.ReportAsync(Playing(0));
            _clock.UtcNow = _clock.UtcNow.Add(ConnectSession.StateInterval);

            Assert.That(await _session.ReportAsync(Playing(5000)), Is.True);
            Assert.That(States(), Has.Count.EqualTo(2));
        }

        [Test]
        public async Task WhilePausedItStaysQuiet()
        {
            var paused = new PlaybackState("mk:film-1", 1000, playing: false);
            await _session.ReportAsync(paused);
            _clock.UtcNow = _clock.UtcNow.Add(TimeSpan.FromMinutes(10));

            Assert.That(await _session.ReportAsync(paused), Is.False,
                "nothing is happening, so there is nothing to say");
        }

        [Test]
        public async Task TheReportCarriesWhatTheContractAsksFor()
        {
            await _session.ReportAsync(Playing(42_000));

            var body = JObject.Parse(States()[0]);
            Assert.That(body["type"]?.Value<string>(), Is.EqualTo("state"));
            var state = (JObject)body["state"];
            foreach (var field in new[] { "itemId", "positionMs", "durationMs", "playing",
                                          "buffering", "audio", "audioIndex", "subtitles",
                                          "subtitleIndex", "volume", "muted", "hasNext" })
                Assert.That(state.ContainsKey(field), Is.True, $"{field} is missing");
            Assert.That(state["positionMs"]?.Value<long>(), Is.EqualTo(42_000));
        }

        [Test]
        public void ReportingNothingIsAMistake()
        {
            Assert.ThrowsAsync<ArgumentNullException>(() => _session.ReportAsync(null));
        }

        [Test]
        public async Task EveryFieldARemoteDrawsCountsAsAChange()
        {
            // The companion piece to ThePositionMovingIsNotAChange: position is the only field
            // excluded, and the others must each wake a report. Driven through ReportAsync rather
            // than by reaching for the throttle's internals, so it tests the behaviour the phone
            // actually sees.
            await _session.ReportAsync(Playing());
            var sentSoFar = 1;

            var variations = new Dictionary<string, PlaybackState>
            {
                ["a different item"] = new PlaybackState("mk:film-2", 0, true) { DurationMs = 7_200_000 },
                ["paused"] = new PlaybackState("mk:film-1", 0, false) { DurationMs = 7_200_000 },
                ["buffering"] = new PlaybackState("mk:film-1", 0, true) { DurationMs = 7_200_000, Buffering = true },
                ["muted"] = new PlaybackState("mk:film-1", 0, true) { DurationMs = 7_200_000, Muted = true },
                ["a next episode"] = new PlaybackState("mk:film-1", 0, true) { DurationMs = 7_200_000, HasNext = true },
                ["quieter"] = new PlaybackState("mk:film-1", 0, true) { DurationMs = 7_200_000, Volume = 0.5 },
                ["a known duration"] = new PlaybackState("mk:film-1", 0, true) { DurationMs = 1_000 },
                ["another audio track"] = new PlaybackState("mk:film-1", 0, true) { DurationMs = 7_200_000, AudioIndex = 2 },
                ["a subtitle"] = new PlaybackState("mk:film-1", 0, true) { DurationMs = 7_200_000, SubtitleIndex = 1 },
            };

            foreach (var variation in variations)
            {
                Assert.That(await _session.ReportAsync(variation.Value), Is.True,
                    $"{variation.Key} should have been reported");
                sentSoFar++;
                Assert.That(States(), Has.Count.EqualTo(sentSoFar));

                // Back to the baseline, so the next variation differs from it and not from its
                // predecessor.
                await _session.ReportAsync(Playing());
                sentSoFar++;
            }
        }
    }
}
