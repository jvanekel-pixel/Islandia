using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Island.Core.StreamBox
{
    /// <summary>
    /// One Connect socket.
    /// </summary>
    /// <remarks>
    /// Pull-shaped rather than event-shaped: <see cref="ReceiveAsync"/> returns the next message,
    /// or null once the socket has closed. An event-based transport would need the session to
    /// cope with callbacks arriving on whatever thread the socket library felt like using, which
    /// on a headset is a source of races nobody would find until a device gate.
    /// </remarks>
    public interface ISocketTransport : IDisposable
    {
        Task ConnectAsync(string path, CancellationToken cancellation);

        Task SendAsync(string json, CancellationToken cancellation);

        /// <summary>The next message, or null once the socket has closed.</summary>
        Task<string> ReceiveAsync(CancellationToken cancellation);

        /// <summary>Why the socket closed, once it has. Null while it is open.</summary>
        int? CloseCode { get; }

        Task CloseAsync(CancellationToken cancellation);
    }

    /// <summary>
    /// Makes sockets. Reconnecting needs a fresh one, because a closed socket cannot be reopened.
    /// </summary>
    public interface ISocketFactory
    {
        ISocketTransport Create();
    }

    /// <summary>
    /// The plain .NET Connect socket, over <see cref="ClientWebSocket"/>.
    /// </summary>
    /// <remarks>
    /// The gateway's address is given to the factory, at the edge, exactly as with
    /// <see cref="HttpTransport"/>: <c>Island.Core</c> never learns it, so it cannot leak into a
    /// public repository from inside the core.
    /// </remarks>
    public sealed class WebSocketTransport : ISocketTransport
    {
        private const int ReceiveBufferBytes = 16 * 1024;

        private readonly Uri _baseAddress;
        private readonly ClientWebSocket _socket = new ClientWebSocket();
        private readonly byte[] _buffer = new byte[ReceiveBufferBytes];

        public WebSocketTransport(Uri baseAddress) =>
            _baseAddress = baseAddress ?? throw new ArgumentNullException(nameof(baseAddress));

        public int? CloseCode { get; private set; }

        public async Task ConnectAsync(string path, CancellationToken cancellation)
        {
            var scheme = _baseAddress.Scheme == "https" ? "wss" : "ws";
            var address = new Uri($"{scheme}://{_baseAddress.Authority}{path}");
            await _socket.ConnectAsync(address, cancellation).ConfigureAwait(false);
        }

        public Task SendAsync(string json, CancellationToken cancellation)
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            return _socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text,
                                     true, cancellation);
        }

        public async Task<string> ReceiveAsync(CancellationToken cancellation)
        {
            var text = new StringBuilder();

            while (true)
            {
                WebSocketReceiveResult frame;
                try
                {
                    frame = await _socket.ReceiveAsync(new ArraySegment<byte>(_buffer), cancellation)
                                         .ConfigureAwait(false);
                }
                catch (WebSocketException)
                {
                    // The socket broke rather than closing politely. Indistinguishable from a
                    // close as far as the session is concerned, and it has no code to report.
                    CloseCode = CloseCode ?? (int)WebSocketCloseStatus.EndpointUnavailable;
                    return null;
                }

                if (frame.MessageType == WebSocketMessageType.Close)
                {
                    CloseCode = (int?)_socket.CloseStatus ?? (int)WebSocketCloseStatus.NormalClosure;
                    return null;
                }

                text.Append(Encoding.UTF8.GetString(_buffer, 0, frame.Count));

                // A long message arrives in pieces. Returning a fragment would hand the session
                // half a JSON document.
                if (frame.EndOfMessage) return text.ToString();
            }
        }

        public async Task CloseAsync(CancellationToken cancellation)
        {
            if (_socket.State != WebSocketState.Open) return;
            try
            {
                await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", cancellation)
                             .ConfigureAwait(false);
            }
            catch (WebSocketException)
            {
                // Already gone. Closing a closed socket is not a failure worth propagating.
            }
        }

        public void Dispose() => _socket.Dispose();
    }

    /// <summary>Makes <see cref="WebSocketTransport"/>s against one gateway.</summary>
    public sealed class WebSocketFactory : ISocketFactory
    {
        private readonly Uri _baseAddress;

        public WebSocketFactory(Uri baseAddress) =>
            _baseAddress = baseAddress ?? throw new ArgumentNullException(nameof(baseAddress));

        public ISocketTransport Create() => new WebSocketTransport(_baseAddress);
    }
}
