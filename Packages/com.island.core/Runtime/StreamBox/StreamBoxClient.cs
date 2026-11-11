using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Island.Core.StreamBox
{
    /// <summary>Where the client stands with the gateway.</summary>
    public enum SessionState
    {
        /// <summary>No device token. The pairing screen is the only thing to show.</summary>
        Unpaired,

        /// <summary>A pairing walk is open: a code is on screen and a phone has not answered yet.</summary>
        Pairing,

        /// <summary>Paired. The library is reachable.</summary>
        Ready,
    }

    /// <summary>How a pairing walk ended.</summary>
    public sealed class PairingOutcome
    {
        private PairingOutcome(PairingStatus status, string userName, string profileName)
        {
            Status = status;
            UserName = userName;
            ProfileName = profileName;
        }

        public PairingStatus Status { get; }

        /// <summary>Whose library this now is. Null unless approved.</summary>
        public string UserName { get; }

        public string ProfileName { get; }

        public bool Approved => Status == PairingStatus.Approved;

        internal static PairingOutcome From(PairingPoll poll) =>
            new PairingOutcome(poll.Status, poll.UserName, poll.ProfileName);

        internal static PairingOutcome Of(PairingStatus status) =>
            new PairingOutcome(status, null, null);
    }

    /// <summary>
    /// Islandia's StreamBox client: the HTTP half.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Everything the island does with the owner's library goes through here. The gateway is the
    /// only authority on what exists (`07-STREAMBOX.md` §1) — this client adds no filter of its
    /// own and removes none, and a 404 means "not yours to see" as often as it means "not there".
    /// </para>
    /// <para>
    /// The device token lives in this object and in the <c>Authorization</c> header, nowhere else.
    /// It is never written to a log, a URL, or a save file by anything here; persisting it is the
    /// Unity layer's job, in device storage, and <see cref="Resume"/> is how it comes back.
    /// </para>
    /// <para>
    /// The Connect socket is a separate package (<c>SB-01d</c>). This half is request and response,
    /// which is everything the island needs to browse and to start playing; remote control is what
    /// needs the socket.
    /// </para>
    /// </remarks>
    public sealed class StreamBoxClient
    {
        private readonly IHttpTransport _transport;
        private readonly IClock _clock;

        private string _deviceToken;
        private string _pollSecret;

        public StreamBoxClient(IHttpTransport transport, IClock clock = null)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _clock = clock ?? SystemClock.Instance;
        }

        public SessionState State { get; private set; } = SessionState.Unpaired;

        /// <summary>Whose library, once paired.</summary>
        public string UserName { get; private set; }

        public string ProfileName { get; private set; }

        /// <summary>Raised whenever <see cref="State"/> changes, including on being signed out.</summary>
        public event Action<SessionState> StateChanged;

        /// <summary>
        /// Picks up a device token saved from a previous run, so the headset does not pair twice.
        /// </summary>
        public void Resume(string deviceToken, string userName = null, string profileName = null)
        {
            if (string.IsNullOrEmpty(deviceToken))
                throw new ArgumentException("Resuming needs the token that was saved.", nameof(deviceToken));

            _deviceToken = deviceToken;
            UserName = userName;
            ProfileName = profileName;
            Move(SessionState.Ready);
        }

        /// <summary>
        /// Forgets the device token. The caller is responsible for deleting its saved copy.
        /// </summary>
        public void SignOut()
        {
            _deviceToken = null;
            _pollSecret = null;
            UserName = null;
            ProfileName = null;
            Move(SessionState.Unpaired);
        }

        /// <summary>
        /// The token to persist, for <see cref="Resume"/> on the next run. Null when unpaired.
        /// </summary>
        /// <remarks>
        /// Deliberately a method rather than a property, so that reading the one credential this
        /// object holds is a visible act at every call site.
        /// </remarks>
        public string TokenToPersist() => _deviceToken;

        // ---- pairing ---------------------------------------------------------------------------

        /// <summary>
        /// Starts a pairing walk. The code and QR address it returns go on the headset's screen.
        /// </summary>
        public async Task<PairingStart> BeginPairingAsync(string deviceName,
                                                          CancellationToken cancellation = default)
        {
            if (string.IsNullOrWhiteSpace(deviceName))
                throw new ArgumentException("The phone shows this name before approving, so it must say something.",
                    nameof(deviceName));

            var body = new JObject { ["deviceName"] = deviceName }.ToString();
            var reply = await Send(new HttpCall(HttpVerb.Post, "/pair/start", body), cancellation);
            var start = PairingStart.From(Json.ParseObject(reply.Body, "pair/start"));

            _pollSecret = start.PollSecret;
            Move(SessionState.Pairing);
            return start;
        }

        /// <summary>
        /// Waits for the phone, polling at the interval the server asked for.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The interval is the server's to choose (`07-STREAMBOX.md` §3.1) and this honours it
        /// rather than picking its own: polling faster than asked is how a device flow earns a
        /// rate limit, and the gateway's limiter would then deny the pairing it was trying to
        /// complete.
        /// </para>
        /// <para>
        /// Returns on the first answer that is not <c>pending</c>. Denied and expired both leave
        /// the client unpaired, because both mean this walk is over — a retry is a new walk with a
        /// new code, not another poll.
        /// </para>
        /// </remarks>
        public async Task<PairingOutcome> AwaitApprovalAsync(PairingStart start,
                                                             CancellationToken cancellation = default)
        {
            if (start == null) throw new ArgumentNullException(nameof(start));

            // Never faster than a second, however eager the server says it is.
            var interval = TimeSpan.FromSeconds(Math.Max(1, start.IntervalSeconds));

            while (true)
            {
                cancellation.ThrowIfCancellationRequested();

                var body = new JObject { ["pollSecret"] = start.PollSecret }.ToString();
                var reply = await Send(new HttpCall(HttpVerb.Post, "/pair/poll", body), cancellation);
                var poll = PairingPoll.From(Json.ParseObject(reply.Body, "pair/poll"));

                switch (poll.Status)
                {
                    case PairingStatus.Approved:
                        // The token is handed over exactly once. Losing it here means pairing again.
                        _deviceToken = poll.DeviceToken;
                        _pollSecret = null;
                        UserName = poll.UserName;
                        ProfileName = poll.ProfileName;
                        Move(SessionState.Ready);
                        return PairingOutcome.From(poll);

                    case PairingStatus.Denied:
                    case PairingStatus.Expired:
                        _pollSecret = null;
                        Move(SessionState.Unpaired);
                        return PairingOutcome.From(poll);

                    default:
                        await _clock.DelayAsync(interval, cancellation);
                        break;
                }
            }
        }

        /// <summary>The whole walk, for callers that only want the outcome.</summary>
        public async Task<PairingOutcome> PairAsync(string deviceName, Action<PairingStart> show,
                                                    CancellationToken cancellation = default)
        {
            var start = await BeginPairingAsync(deviceName, cancellation);
            show?.Invoke(start);
            return await AwaitApprovalAsync(start, cancellation);
        }

        // ---- browsing --------------------------------------------------------------------------

        public async Task<List<HomeRow>> HomeAsync(CancellationToken cancellation = default)
        {
            var body = await GetObject("/catalog/home", cancellation);
            return Json.RequiredArray<HomeRow>(body, "rows", "catalog/home", HomeRow.From);
        }

        public Task<ItemsPage> LatestAsync(CancellationToken cancellation = default) =>
            Page("/catalog/latest", cancellation);

        public Task<ItemsPage> NextUpAsync(CancellationToken cancellation = default) =>
            Page("/catalog/next-up", cancellation);

        public Task<ItemsPage> ResumeAsync(CancellationToken cancellation = default) =>
            Page("/catalog/resume", cancellation);

        public Task<ItemsPage> LibraryItemsAsync(string libraryId, CancellationToken cancellation = default)
        {
            if (string.IsNullOrEmpty(libraryId)) throw new ArgumentException("Which library?", nameof(libraryId));
            return Page($"/catalog/libraries/{Escape(libraryId)}/items", cancellation);
        }

        public Task<ItemsPage> SearchAsync(string query, CancellationToken cancellation = default)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            return Page($"/catalog/search?q={Escape(query)}", cancellation);
        }

        private async Task<ItemsPage> Page(string path, CancellationToken cancellation) =>
            ItemsPage.From(await GetObject(path, cancellation));

        // ---- playing ---------------------------------------------------------------------------

        /// <summary>
        /// Asks the gateway for a stream. It decides remux or transcode from the capabilities.
        /// </summary>
        public async Task<PlayResponse> PlayAsync(PlayRequest request, CancellationToken cancellation = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            var reply = await Send(Authenticated(HttpVerb.Post, "/play", request.ToJson().ToString()), cancellation);
            return PlayResponse.From(Json.ParseObject(reply.Body, "play"));
        }

        /// <summary>
        /// Gives a stream back. Idle streams are swept after about five minutes anyway, but
        /// stopping cleanly is what lets the gateway free the transcode now rather than later.
        /// </summary>
        public async Task StopAsync(string streamToken, CancellationToken cancellation = default)
        {
            if (string.IsNullOrEmpty(streamToken))
                throw new ArgumentException("Which stream?", nameof(streamToken));
            await Send(Authenticated(HttpVerb.Delete, $"/play/{Escape(streamToken)}"), cancellation);
        }

        // ---- progress --------------------------------------------------------------------------

        /// <summary>
        /// Reports playback. StreamBox owns the progress rules and tests them on its own side;
        /// Islandia reports events and does not re-implement them (`07-STREAMBOX.md` §3.5).
        /// </summary>
        public async Task ReportProgressAsync(IReadOnlyList<ProgressEvent> events,
                                              CancellationToken cancellation = default)
        {
            if (events == null) throw new ArgumentNullException(nameof(events));

            // Nothing to report is not an error, and an empty batch is one the gateway refuses.
            if (events.Count == 0) return;

            // ProgressEvent.Batch owns the envelope, sentAt included, so the shape lives with the
            // model it belongs to rather than being rebuilt here and drifting from it.
            var body = ProgressEvent.Batch(events, Iso(_clock.UtcNow)).ToString();
            await Send(Authenticated(HttpVerb.Post, "/progress/events", body), cancellation);
        }

        // ---- music -----------------------------------------------------------------------------

        /// <summary>
        /// A signed address for a track, which needs no header — so the island's speakers can
        /// fetch it directly.
        /// </summary>
        public async Task<MusicPlayResponse> MusicPlayAsync(string trackId, CancellationToken cancellation = default)
        {
            if (string.IsNullOrEmpty(trackId)) throw new ArgumentException("Which track?", nameof(trackId));
            var body = new JObject { ["trackId"] = trackId }.ToString();
            var reply = await Send(Authenticated(HttpVerb.Post, "/music/play", body), cancellation);
            return MusicPlayResponse.From(Json.ParseObject(reply.Body, "music/play"));
        }

        /// <summary>The albums the music home screen opens on.</summary>
        public async Task<List<MusicAlbum>> MusicHomeAsync(CancellationToken cancellation = default)
        {
            var body = await GetObject("/music/home", cancellation);
            return Json.RequiredArray<MusicAlbum>(body, "albums", "music/home", MusicAlbum.From);
        }

        /// <summary>Every album, without their track lists.</summary>
        public async Task<List<MusicAlbum>> MusicAlbumsAsync(CancellationToken cancellation = default)
        {
            var body = await GetObject("/music/albums", cancellation);
            return Json.RequiredArray<MusicAlbum>(body, "albums", "music/albums", MusicAlbum.From);
        }

        /// <summary>One album, with its tracks — which is where a track id to play comes from.</summary>
        public async Task<MusicAlbum> MusicAlbumAsync(string albumId,
                                                      CancellationToken cancellation = default)
        {
            if (string.IsNullOrEmpty(albumId)) throw new ArgumentException("Which album?", nameof(albumId));
            var body = await GetObject($"/music/albums/{Escape(albumId)}", cancellation);
            return MusicAlbum.From(body, "music/albums");
        }

        // The snapshot also declares /music/search, /music/artists, /music/playlists and
        // /music/favorites. They are deliberately absent: the mock gateway does not implement
        // them (issue filed), so a client method for them could not be proven against anything
        // and would be a guess at a response shape. They arrive when the mock does.

        // ---- the plumbing ----------------------------------------------------------------------

        private HttpCall Authenticated(HttpVerb verb, string path, string jsonBody = null)
        {
            if (_deviceToken == null)
                throw new StreamBoxSignedOutException("nothing is paired, so there is no token to send");
            return new HttpCall(verb, path, jsonBody, _deviceToken);
        }

        private async Task<JObject> GetObject(string path, CancellationToken cancellation)
        {
            var reply = await Send(Authenticated(HttpVerb.Get, path), cancellation);
            return Json.ParseObject(reply.Body, path);
        }

        /// <summary>
        /// Sends a call and insists on a usable answer.
        /// </summary>
        /// <remarks>
        /// A 401 is the one status with a required consequence: the device session is gone, so the
        /// token is dropped and the client falls back to <see cref="SessionState.Unpaired"/> before
        /// the exception leaves here. Anything else that is not a success becomes a plain request
        /// exception carrying the status, and never the credential.
        /// </remarks>
        private async Task<HttpReply> Send(HttpCall call, CancellationToken cancellation)
        {
            var reply = await _transport.SendAsync(call, cancellation);

            if (reply.Unauthorised)
            {
                SignOut();
                throw new StreamBoxSignedOutException($"{call} was refused with 401");
            }

            if (!reply.Ok) throw new StreamBoxRequestException(call, reply);
            return reply;
        }

        private void Move(SessionState state)
        {
            if (State == state) return;
            State = state;
            StateChanged?.Invoke(state);
        }

        private static string Escape(string value) => Uri.EscapeDataString(value);

        /// <summary>
        /// A timestamp in the shape the gateway's progress events use.
        /// </summary>
        /// <remarks>
        /// Public because the caller builds the events and has to stamp them the same way. Always
        /// UTC and always invariant: the device's locale must not reach the wire, and a progress
        /// report timed in the wrong calendar is worse than none.
        /// </remarks>
        public static string Iso(DateTimeOffset when) =>
            when.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
    }
}
