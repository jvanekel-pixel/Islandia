using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Island.Core.StreamBox
{
    public enum ConnectStatus
    {
        /// <summary>Not connected, and will try.</summary>
        Offline,

        /// <summary>A socket is open but has not been introduced yet.</summary>
        Introducing,

        /// <summary>The gateway has said welcome. The island is a place to play.</summary>
        Live,

        /// <summary>Finished, and not coming back without a new pairing or a new session.</summary>
        Done,
    }

    /// <summary>
    /// The Connect socket: the island as something the owner's phone can drive.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Islandia registers as a place to play, the phone sends commands, and the island reports
    /// what it is doing so the phone can draw a live remote (`07-STREAMBOX.md` §3.6).
    /// </para>
    /// <para>
    /// Two close codes must never be retried. <b>4401</b> means the device token is gone, so
    /// reconnecting would fail forever with a credential that will never work again. <b>4409</b>
    /// means a newer socket replaced this one — the owner has directed that playback be
    /// one-at-a-time per person with the newest device winning, so reconnecting would be this
    /// island fighting the phone for the stream. Everything else is a network hiccup and is worth
    /// another try, with backoff.
    /// </para>
    /// <para>
    /// There is deliberately no second loop sending state on a timer. <see cref="ReportAsync"/> is
    /// called by the app as often as it likes and decides for itself whether to send, which keeps
    /// the timing decision in a method a test can drive with a fake clock instead of in a
    /// background task a test has to race.
    /// </para>
    /// </remarks>
    public sealed class ConnectSession
    {
        /// <summary>How often to re-send state while playing, even when nothing has changed.</summary>
        public static readonly TimeSpan StateInterval = TimeSpan.FromSeconds(5);

        /// <summary>First wait after a connection fails for a reason worth retrying.</summary>
        public static readonly TimeSpan FirstBackoff = TimeSpan.FromSeconds(1);

        /// <summary>The longest it will ever wait between attempts.</summary>
        public static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(30);

        private readonly ISocketFactory _sockets;
        private readonly IClock _clock;
        private readonly Func<string> _token;

        private ISocketTransport _socket;
        private string _lastSentSignature;
        private DateTimeOffset _lastSentAt;

        public ConnectSession(ISocketFactory sockets, Func<string> token, IClock clock = null)
        {
            _sockets = sockets ?? throw new ArgumentNullException(nameof(sockets));
            _token = token ?? throw new ArgumentNullException(nameof(token));
            _clock = clock ?? SystemClock.Instance;
        }

        public ConnectStatus Status { get; private set; } = ConnectStatus.Offline;

        /// <summary>The gateway's id for this socket, once it has said welcome.</summary>
        public string SessionId { get; private set; }

        /// <summary>The code that ended this session for good, if one did.</summary>
        public int? FinalCloseCode { get; private set; }

        /// <summary>How many times it has reconnected. For a diagnostics panel, and for tests.</summary>
        public int Attempts { get; private set; }

        /// <summary>The phone has asked for something. Raised on the thread running the pump.</summary>
        public event Action<ConnectCommand> CommandReceived;

        /// <summary>The device token is gone: the app must return to the pairing screen.</summary>
        public event Action SignedOut;

        public event Action<ConnectStatus> StatusChanged;

        // ---- the pump --------------------------------------------------------------------------

        /// <summary>
        /// Keeps a socket open until told to stop, or until a close code says never again.
        /// </summary>
        public async Task RunAsync(CancellationToken cancellation)
        {
            // Uniform with the rest of the loop: cancelling mid-flight throws out of the socket
            // or the delay, so cancelling beforehand throws too rather than returning quietly.
            cancellation.ThrowIfCancellationRequested();

            var backoff = FirstBackoff;

            while (!cancellation.IsCancellationRequested && Status != ConnectStatus.Done)
            {
                var closeCode = await OneConnectionAsync(cancellation).ConfigureAwait(false);

                if (cancellation.IsCancellationRequested) break;

                // The attempt itself may have finished the session for good — no token to send,
                // or a command handler called Stop. Checked here rather than only at the top of
                // the loop, because the Move below would otherwise put it back to Offline and
                // retry forever.
                if (Status == ConnectStatus.Done) return;

                if (closeCode.HasValue && ConnectClose.IsFinal(closeCode.Value))
                {
                    FinalCloseCode = closeCode;
                    Move(ConnectStatus.Done);
                    if (closeCode.Value == ConnectClose.SignedOut) SignedOut?.Invoke();
                    return;
                }

                Move(ConnectStatus.Offline);
                await _clock.DelayAsync(backoff, cancellation).ConfigureAwait(false);

                // Doubling, capped. A gateway that is down comes back on its own schedule, and a
                // headset hammering it every second helps nobody.
                var doubled = TimeSpan.FromTicks(backoff.Ticks * 2);
                backoff = doubled > MaxBackoff ? MaxBackoff : doubled;
            }

            if (Status != ConnectStatus.Done) Move(ConnectStatus.Offline);
        }

        /// <summary>One socket, from connect to close. Returns the close code, if there was one.</summary>
        private async Task<int?> OneConnectionAsync(CancellationToken cancellation)
        {
            var token = _token();
            if (string.IsNullOrEmpty(token))
            {
                // Nothing is paired. Connecting without a token earns a 4401 from the gateway,
                // which would then be read as a sign-out — so do not ask.
                //
                // FinalCloseCode stays null on purpose: the gateway never closed anything, and
                // claiming a 4401 it did not send would put a fiction in front of whoever reads
                // this to find out why Connect stopped. Done plus SignedOut says it accurately.
                Move(ConnectStatus.Done);
                SignedOut?.Invoke();
                return null;
            }

            Attempts++;
            var socket = _sockets.Create();

            try
            {
                Move(ConnectStatus.Introducing);
                await socket.ConnectAsync("/api/connect", cancellation).ConfigureAwait(false);

                _socket = socket;
                _lastSentSignature = null;

                await socket.SendAsync(ServerMessage.Hello(token).ToString(), cancellation)
                            .ConfigureAwait(false);

                while (true)
                {
                    var raw = await socket.ReceiveAsync(cancellation).ConfigureAwait(false);
                    if (raw == null) return socket.CloseCode;

                    Handle(raw);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                // The socket would not open, or broke. Worth retrying: no close code means no
                // instruction from the gateway, so this is the network rather than a decision.
                return null;
            }
            finally
            {
                _socket = null;
                try { await socket.CloseAsync(CancellationToken.None).ConfigureAwait(false); }
                catch (Exception) { /* closing a broken socket is not a new failure */ }
                socket.Dispose();
            }
        }

        private void Handle(string raw)
        {
            JObject body;
            try
            {
                body = Json.ParseObject(raw, "connect message");
            }
            catch (StreamBoxContractException)
            {
                // A message this build cannot parse is not a reason to drop the socket. The
                // gateway will send kinds and shapes a later StreamBox invented; ignoring one is
                // the tolerance `07-STREAMBOX.md` §7 asks for.
                return;
            }

            var message = ServerMessage.From(body);

            switch (message.Kind)
            {
                case ServerMessageKind.Welcome:
                    SessionId = message.SessionId;
                    Move(ConnectStatus.Live);
                    break;

                case ServerMessageKind.Command:
                    // An unknown command is ignored rather than guessed at. Guessing is how a
                    // newer phone makes an older island do the wrong thing.
                    if (message.Command != null && message.Command.Kind != ConnectCommandKind.Unknown)
                        CommandReceived?.Invoke(message.Command);
                    break;
            }
        }

        // ---- reporting -------------------------------------------------------------------------

        /// <summary>
        /// Tells the gateway what the island is doing, if it needs telling.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Safe to call every frame. It sends when something a remote would draw differently has
        /// changed, and otherwise only every <see cref="StateInterval"/> while playing.
        /// </para>
        /// <para>
        /// Position is deliberately not part of "changed". It advances every frame, so counting it
        /// would mean sending seventy-two messages a second to say the obvious. The periodic
        /// re-send is what keeps the phone's scrubber honest.
        /// </para>
        /// </remarks>
        /// <returns>Whether a message was actually sent.</returns>
        public async Task<bool> ReportAsync(PlaybackState state,
                                            CancellationToken cancellation = default)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            var socket = _socket;
            if (socket == null || Status != ConnectStatus.Live) return false;

            var signature = SignatureOf(state);
            var changed = signature != _lastSentSignature;
            var due = state.Playing && _clock.UtcNow - _lastSentAt >= StateInterval;

            if (!changed && !due) return false;

            var body = new JObject { ["type"] = "state", ["state"] = state.ToJson() };
            await socket.SendAsync(body.ToString(), cancellation).ConfigureAwait(false);

            _lastSentSignature = signature;
            _lastSentAt = _clock.UtcNow;
            return true;
        }

        /// <summary>
        /// Everything a remote would draw differently — which is every state field except the
        /// position, and the position is carried by the message anyway.
        /// </summary>
        internal static string SignatureOf(PlaybackState state) => string.Join("|",
            state.ItemId ?? "-",
            state.Playing ? "1" : "0",
            state.Buffering ? "1" : "0",
            state.Muted ? "1" : "0",
            state.HasNext ? "1" : "0",
            state.Volume.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            state.DurationMs?.ToString() ?? "-",
            state.AudioIndex?.ToString() ?? "-",
            state.SubtitleIndex?.ToString() ?? "-");

        /// <summary>
        /// Stops for good, without reconnecting.
        /// </summary>
        /// <remarks>
        /// For a deliberate sign-out or shutdown. A cancelled <see cref="RunAsync"/> leaves the
        /// session merely offline, because the app may want to resume it; this says never again.
        /// </remarks>
        public void Stop() => Move(ConnectStatus.Done);

        private void Move(ConnectStatus status)
        {
            if (Status == status) return;
            Status = status;
            StatusChanged?.Invoke(status);
        }
    }
}
