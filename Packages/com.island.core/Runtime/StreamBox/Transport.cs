using System;
using System.Threading;
using System.Threading.Tasks;

namespace Island.Core.StreamBox
{
    public enum HttpVerb
    {
        Get,
        Post,
        Put,
        Delete,
    }

    /// <summary>One request to the gateway.</summary>
    /// <remarks>
    /// <para>
    /// The path is relative to the gateway's <c>/api</c> base. <b>Nothing in
    /// <c>Island.Core</c> knows the server's address</b> — the transport holds it, and the
    /// transport is supplied by the Unity layer from device configuration. That is not a layering
    /// nicety: this repository is public (`07-STREAMBOX.md` §1) and an address that never enters
    /// the core cannot be committed from it.
    /// </para>
    /// <para>
    /// <see cref="BearerToken"/> is the device session. A transport must put it in the
    /// <c>Authorization</c> header and <b>nowhere else</b> — never in a URL, a query string, a log
    /// line or an error message. It is the one credential Islandia holds.
    /// </para>
    /// </remarks>
    public readonly struct HttpCall
    {
        public HttpCall(HttpVerb verb, string path, string jsonBody = null, string bearerToken = null)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("A call needs a path.", nameof(path));
            Verb = verb;
            Path = path;
            JsonBody = jsonBody;
            BearerToken = bearerToken;
        }

        public HttpVerb Verb { get; }

        /// <summary>Relative to the gateway's base path, beginning with a slash.</summary>
        public string Path { get; }

        /// <summary>The request body, already serialised, or null.</summary>
        public string JsonBody { get; }

        /// <summary>The device token, for the Authorization header only, or null when unauthenticated.</summary>
        public string BearerToken { get; }

        /// <summary>Describes the call without its credential, for logs and errors.</summary>
        public override string ToString() => $"{Verb.ToString().ToUpperInvariant()} {Path}";
    }

    /// <summary>What came back.</summary>
    public readonly struct HttpReply
    {
        public HttpReply(int status, string body)
        {
            Status = status;
            Body = body ?? string.Empty;
        }

        public int Status { get; }
        public string Body { get; }

        public bool Ok => Status >= 200 && Status < 300;

        /// <summary>The device session is gone. Return to pairing.</summary>
        public bool Unauthorised => Status == 401;

        /// <summary>
        /// Not found. The gateway returns this for anything the paired profile may not see, so it
        /// is not necessarily a missing thing — it may be a thing that is none of our business
        /// (`07-STREAMBOX.md` §1: the gateway is the only authority on access).
        /// </summary>
        public bool NotFound => Status == 404;
    }

    /// <summary>
    /// How the client reaches the gateway.
    /// </summary>
    /// <remarks>
    /// Injectable so the client can be driven by a fake in tests, and so the Unity layer can use
    /// whatever it prefers on device without the client caring. A transport must not throw for an
    /// HTTP status — a 404 and a 401 are answers, and the client decides what they mean. It throws
    /// only when there was no answer at all.
    /// </remarks>
    public interface IHttpTransport
    {
        Task<HttpReply> SendAsync(HttpCall call, CancellationToken cancellation);
    }

    /// <summary>
    /// Time, injectable so tests do not wait.
    /// </summary>
    /// <remarks>
    /// Pairing polls at the interval the server asks for, which is seconds. A test that polled in
    /// real time would take a minute to prove a walk that takes four lines to describe, so it
    /// would either be skipped or quietly shortened until it proved nothing.
    /// </remarks>
    public interface IClock
    {
        DateTimeOffset UtcNow { get; }
        Task DelayAsync(TimeSpan duration, CancellationToken cancellation);
    }

    public sealed class SystemClock : IClock
    {
        public static readonly SystemClock Instance = new SystemClock();

        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

        public Task DelayAsync(TimeSpan duration, CancellationToken cancellation) =>
            Task.Delay(duration, cancellation);
    }

    /// <summary>The gateway answered, but not with what was asked for.</summary>
    public sealed class StreamBoxRequestException : Exception
    {
        public StreamBoxRequestException(HttpCall call, HttpReply reply)
            : base($"{call} returned {reply.Status}.")
        {
            Status = reply.Status;
            Call = call.ToString();
        }

        public int Status { get; }

        /// <summary>The call, without its credential.</summary>
        public string Call { get; }
    }

    /// <summary>
    /// The device session is gone — revoked, expired, or signed out elsewhere.
    /// </summary>
    /// <remarks>
    /// Its own type because it is the one failure with a required response: drop the token and go
    /// back to the pairing screen. Treating it as an ordinary error would leave the app retrying
    /// with a credential that will never work again.
    /// </remarks>
    public sealed class StreamBoxSignedOutException : Exception
    {
        public StreamBoxSignedOutException(string reason)
            : base($"The StreamBox device session is gone: {reason}") { }
    }
}
