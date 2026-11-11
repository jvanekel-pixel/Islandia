using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Island.Core.StreamBox;
using NUnit.Framework;

namespace Island.Core.Tests.StreamBox
{
    /// <summary>
    /// The client against the real mock gateway, over real HTTP.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Everything else in this fixture's neighbourhood runs against a fake transport, which proves
    /// the client's logic and proves nothing about whether it can talk to a server. This runs the
    /// whole pairing walk and a browse-play-stop journey against <c>tools/mock-streambox</c>, in a
    /// child process, over a socket.
    /// </para>
    /// <para>
    /// Marked <c>EndToEnd</c> and excluded from <c>make core-test</c>, which exists to be a
    /// one-second suite. <c>make streambox-e2e</c> runs it, after installing the mock's dependency.
    /// </para>
    /// <para>
    /// It waits in real time, because the point is the real thing: the mock asks to be polled once
    /// a second and approves itself shortly after. A few seconds.
    /// </para>
    /// </remarks>
    [TestFixture]
    [Category("EndToEnd")]
    public class StreamBoxEndToEndTests
    {
        private Process _mock;
        private int _port;
        private HttpTransport _transport;

        private static string MockDirectory
        {
            get
            {
                var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
                while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "tools", "mock-streambox")))
                    directory = directory.Parent;

                if (directory == null)
                    Assert.Fail("Could not find tools/mock-streambox above the test directory.");
                return Path.Combine(directory.FullName, "tools", "mock-streambox");
            }
        }

        [OneTimeSetUp]
        public async Task StartTheMock()
        {
            var directory = MockDirectory;

            if (!Directory.Exists(Path.Combine(directory, "node_modules")))
            {
                Assert.Fail("The mock gateway's dependency is not installed. Run: make mock-install "
                            + "(or use make streambox-e2e, which does it).");
            }

            _port = FreePort();

            _mock = new Process
            {
                StartInfo = new ProcessStartInfo("node", $"server.js --port {_port} --auto-approve-ms 400")
                {
                    WorkingDirectory = directory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                },
            };

            if (!_mock.Start()) Assert.Fail("Could not start node. Is it installed?");

            // Drain the pipes, or a chatty server fills its buffer and blocks.
            _mock.BeginOutputReadLine();
            _mock.BeginErrorReadLine();

            await WaitForHealth();

            _transport = new HttpTransport(new Uri($"http://127.0.0.1:{_port}"));
        }

        [OneTimeTearDown]
        public void StopTheMock()
        {
            _transport?.Dispose();

            if (_mock == null) return;
            try
            {
                if (!_mock.HasExited)
                {
                    _mock.Kill();
                    _mock.WaitForExit(5000);
                }
            }
            catch (InvalidOperationException)
            {
                // Already gone. Nothing to do.
            }
            finally
            {
                _mock.Dispose();
            }
        }

        private async Task WaitForHealth()
        {
            using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) })
            {
                var deadline = DateTime.UtcNow.AddSeconds(30);
                while (DateTime.UtcNow < deadline)
                {
                    if (_mock.HasExited)
                        Assert.Fail($"The mock gateway exited with code {_mock.ExitCode} before answering.");

                    try
                    {
                        var reply = await http.GetAsync($"http://127.0.0.1:{_port}/mock/health");
                        if (reply.IsSuccessStatusCode) return;
                    }
                    catch (HttpRequestException)
                    {
                        // Not listening yet.
                    }

                    await Task.Delay(200);
                }
            }

            Assert.Fail($"The mock gateway did not answer on port {_port} within 30 seconds.");
        }

        private static int FreePort()
        {
            // Asking the operating system beats picking a number: two of these fixtures running
            // at once, or a stray mock from an earlier run, would otherwise collide.
            var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();
            var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private StreamBoxClient NewClient() => new StreamBoxClient(_transport);

        // ---- the journey -----------------------------------------------------------------------

        [Test]
        public async Task TheWholePairingWalkWorksAgainstARealServer()
        {
            var client = NewClient();
            PairingStart shown = null;

            var outcome = await client.PairAsync("Islandia — Quest", start => shown = start);

            Assert.That(shown, Is.Not.Null);
            Assert.That(shown.Code, Is.Not.Null.And.Not.Empty, "a code has to go on the screen");
            Assert.That(shown.ApproveUrl, Does.Contain(shown.Code),
                "the QR address must carry the code the phone is approving");
            Assert.That(shown.IntervalSeconds, Is.GreaterThan(0));

            Assert.That(outcome.Approved, Is.True);
            Assert.That(client.State, Is.EqualTo(SessionState.Ready));
            Assert.That(client.TokenToPersist(), Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task BrowsingPlayingAndStoppingWorkEndToEnd()
        {
            var client = NewClient();
            await client.PairAsync("Islandia — Quest", null);

            var home = await client.HomeAsync();
            Assert.That(home, Is.Not.Empty, "the fixture library should have rows");

            var film = FirstPlayable(home);
            Assert.That(film, Is.Not.Null, "the fixture library should have something playable");

            var play = await client.PlayAsync(new PlayRequest(film.Id, PlaybackCapabilities.Quest));
            Assert.That(play.StreamId, Is.Not.Null.And.Not.Empty);
            Assert.That(play.Url, Does.StartWith("/api/stream/"),
                "the token lives in the path, so the player needs no header");
            Assert.That(play.DurationMs, Is.GreaterThan(0));

            // The stream token is the path segment after /api/stream/.
            var streamToken = play.Url.Split('/')[3];
            await client.StopAsync(streamToken);
        }

        [Test]
        public async Task TheStreamTheGatewayHandsBackIsReallyServed()
        {
            // The client's job ends at the address. This checks the address is not a fiction,
            // because a playable-looking URL that 404s is the failure that would otherwise be
            // found on the headset.
            var client = NewClient();
            await client.PairAsync("Islandia — Quest", null);

            var film = FirstPlayable(await client.HomeAsync());
            var play = await client.PlayAsync(new PlayRequest(film.Id, PlaybackCapabilities.Quest));

            using (var http = new HttpClient())
            {
                var master = await http.GetAsync($"http://127.0.0.1:{_port}{play.Url}");
                Assert.That((int)master.StatusCode, Is.EqualTo(200));
                var text = await master.Content.ReadAsStringAsync();
                Assert.That(text, Does.StartWith("#EXTM3U"));
            }
        }

        [Test]
        public async Task TheMusicHomeScreenHasSomethingOnIt()
        {
            var client = NewClient();
            await client.PairAsync("Islandia — Quest", null);
            Assert.That(await client.MusicHomeAsync(), Is.Not.Empty);
        }

        [Test]
        public async Task SearchReachesTheRealServer()
        {
            var client = NewClient();
            await client.PairAsync("Islandia — Quest", null);

            var page = await client.SearchAsync("a");
            Assert.That(page, Is.Not.Null);
            Assert.That(page.Total, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public async Task ProgressIsAcceptedByTheRealServer()
        {
            var client = NewClient();
            await client.PairAsync("Islandia — Quest", null);

            var film = FirstPlayable(await client.HomeAsync());
            var now = DateTimeOffset.UtcNow;

            await client.ReportProgressAsync(new[]
            {
                new ProgressEvent(
                    id: Guid.NewGuid().ToString(),
                    kind: ProgressEventKind.Heartbeat,
                    itemId: film.Id,
                    profileId: "mk:profile-1",
                    playbackId: Guid.NewGuid().ToString(),
                    playbackStartedAt: StreamBoxClient.Iso(now.AddSeconds(-30)),
                    clientTs: StreamBoxClient.Iso(now),
                    positionMs: 30_000),
            });
        }

        [Test]
        public async Task MusicGivesASignedAddressThatReallyServesAudio()
        {
            var client = NewClient();
            await client.PairAsync("Islandia — Quest", null);

            var albums = await client.MusicAlbumsAsync();
            Assert.That(albums, Is.Not.Empty, "the fixture library should have music");

            // The album list omits track lists, so a track id comes from one album.
            var album = await client.MusicAlbumAsync(albums[0].Id);
            Assert.That(album.Tracks, Is.Not.Empty);

            var play = await client.MusicPlayAsync(album.Tracks[0].Id);
            Assert.That(play.Url, Does.Contain("token="));

            using (var http = new HttpClient())
            {
                var audio = await http.GetAsync($"http://127.0.0.1:{_port}{play.Url}");
                Assert.That((int)audio.StatusCode, Is.EqualTo(200),
                    "a signed address must need no Authorization header");
            }
        }

        [Test]
        public async Task AnInventedTokenIsRefusedAndSendsTheClientBackToPairing()
        {
            // The behaviour a revoked device session produces, against a server that really
            // checks rather than a fake told to say 401.
            var client = NewClient();
            client.Resume("a-token-this-server-never-issued");

            Assert.ThrowsAsync<StreamBoxSignedOutException>(() => client.HomeAsync());
            Assert.That(client.State, Is.EqualTo(SessionState.Unpaired));
            Assert.That(client.TokenToPersist(), Is.Null);
            await Task.CompletedTask;
        }

        [Test]
        public async Task TwoHeadsetsPairIndependently()
        {
            // Device sessions are per device. One pairing must not hand its token to the other.
            var first = NewClient();
            var second = NewClient();

            await first.PairAsync("Islandia — Quest", null);
            await second.PairAsync("Islandia — spare", null);

            Assert.That(first.TokenToPersist(), Is.Not.EqualTo(second.TokenToPersist()));
            Assert.That(await first.HomeAsync(), Is.Not.Empty);
            Assert.That(await second.HomeAsync(), Is.Not.Empty);
        }

        // ---- Connect -----------------------------------------------------------------------------

        [Test]
        public async Task ThePhoneCanDriveTheIslandOverARealSocket()
        {
            // The whole point of Connect: Islandia registers as a place to play and the owner's
            // phone tells it what to do. Driven here by the mock's /mock/command, which exists to
            // play the phone.
            var client = NewClient();
            await client.PairAsync("Islandia — Quest", null);

            var session = new ConnectSession(
                new WebSocketFactory(new Uri($"http://127.0.0.1:{_port}")),
                client.TokenToPersist);

            var received = new System.Collections.Concurrent.ConcurrentQueue<ConnectCommand>();
            session.CommandReceived += command => received.Enqueue(command);

            using (var stop = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
            {
                var running = session.RunAsync(stop.Token);

                await WaitFor(() => session.Status == ConnectStatus.Live, "the socket to go live");
                Assert.That(session.SessionId, Is.Not.Null.And.Not.Empty,
                    "the gateway should have said welcome with a session id");

                await PlayThePhone(new { type = "pause" });
                await WaitFor(() => received.Count > 0, "the command to arrive");

                Assert.That(received.TryDequeue(out var command), Is.True);
                Assert.That(command.Kind, Is.EqualTo(ConnectCommandKind.Pause));

                // Reporting state must not throw against a real server; the mock keeps the last
                // one but offers no way to read it back (see issue #21).
                Assert.That(await session.ReportAsync(
                    new PlaybackState("mk:film-1", 1000, playing: false)), Is.True);

                session.Stop();
                stop.Cancel();
                await Settled(running);
            }
        }

        [Test]
        public async Task ARevokedTokenClosesTheSocketWithSignedOut()
        {
            // 4401 against a server that really checks, rather than a fake told to say it. This
            // is the path a device revoked from the StreamBox app takes.
            var session = new ConnectSession(
                new WebSocketFactory(new Uri($"http://127.0.0.1:{_port}")),
                () => "a-token-this-server-never-issued");

            var signedOut = 0;
            session.SignedOut += () => signedOut++;

            using (var stop = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
            {
                var running = session.RunAsync(stop.Token);
                await WaitFor(() => session.Status == ConnectStatus.Done, "the gateway to refuse it");
                await Settled(running);

                Assert.That(session.FinalCloseCode, Is.EqualTo(ConnectClose.SignedOut));
                Assert.That(signedOut, Is.EqualTo(1));
                Assert.That(session.Attempts, Is.EqualTo(1), "4401 must never be retried");
            }
        }

        private async Task PlayThePhone(object command)
        {
            using (var http = new HttpClient())
            {
                var body = Newtonsoft.Json.JsonConvert.SerializeObject(new { command });
                var reply = await http.PostAsync($"http://127.0.0.1:{_port}/mock/command",
                    new StringContent(body, System.Text.Encoding.UTF8, "application/json"));
                Assert.That((int)reply.StatusCode, Is.EqualTo(200));
            }
        }

        private static async Task WaitFor(Func<bool> condition, string what, int seconds = 15)
        {
            var deadline = DateTime.UtcNow.AddSeconds(seconds);
            while (DateTime.UtcNow < deadline)
            {
                if (condition()) return;
                await Task.Delay(50);
            }
            Assert.Fail($"Waited {seconds}s for {what} and it did not happen.");
        }

        /// <summary>Lets the run loop finish, treating its cancellation as the expected end.</summary>
        private static async Task Settled(Task running)
        {
            try { await running; }
            catch (OperationCanceledException) { /* how a cancelled pump ends */ }
        }

        private static CatalogItem FirstPlayable(System.Collections.Generic.List<HomeRow> rows)
        {
            foreach (var row in rows)
                foreach (var item in row.Items)
                    if (item.Kind == ItemKind.Movie || item.Kind == ItemKind.Episode)
                        return item;
            return null;
        }
    }
}
