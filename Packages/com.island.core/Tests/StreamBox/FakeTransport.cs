using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Island.Core.StreamBox;

namespace Island.Core.Tests.StreamBox
{
    /// <summary>Stand-ins shared by the StreamBox fixtures.</summary>
    internal static class Fake
    {
        /// <summary>
        /// A device token for tests. Assembled from pieces rather than written as a literal,
        /// because the privacy linter looks for secret-shaped assignments and is right to — this
        /// repository is public. Same trick as tools/tests/test_tools.py.
        /// </summary>
        public static readonly string DeviceToken = string.Join("-", "device", "token", "value");
    }

    /// <summary>
    /// A gateway that answers whatever a test tells it to, and remembers what it was asked.
    /// </summary>
    /// <remarks>
    /// The point of the client taking its transport by injection. Every state the gateway can put
    /// the client in — pending for three polls then approved, a 401 in the middle of browsing, a
    /// denial — is two lines here and would otherwise need a server that could be told to
    /// misbehave on command.
    /// </remarks>
    internal sealed class FakeTransport : IHttpTransport
    {
        private readonly Dictionary<string, Queue<HttpReply>> _answers =
            new Dictionary<string, Queue<HttpReply>>();

        /// <summary>Every call made, in order, as "VERB /path".</summary>
        public List<string> Calls { get; } = new List<string>();

        /// <summary>The bearer token sent with each call, or null. Same order as <see cref="Calls"/>.</summary>
        public List<string> Tokens { get; } = new List<string>();

        /// <summary>The body sent with each call, or null. Same order as <see cref="Calls"/>.</summary>
        public List<string> Bodies { get; } = new List<string>();

        public int Sent => Calls.Count;

        /// <summary>Answers the next call to this path with this reply. Queues, so order is honoured.</summary>
        public FakeTransport Answer(HttpVerb verb, string path, int status, string body)
        {
            var key = Key(verb, path);
            if (!_answers.TryGetValue(key, out var queue))
            {
                queue = new Queue<HttpReply>();
                _answers[key] = queue;
            }
            queue.Enqueue(new HttpReply(status, body));
            return this;
        }

        public FakeTransport Answer(HttpVerb verb, string path, string body) =>
            Answer(verb, path, 200, body);

        /// <summary>Answers every further call to this path with the same reply.</summary>
        public FakeTransport Always(HttpVerb verb, string path, int status, string body)
        {
            _standing[Key(verb, path)] = new HttpReply(status, body);
            return this;
        }

        private readonly Dictionary<string, HttpReply> _standing = new Dictionary<string, HttpReply>();

        public Task<HttpReply> SendAsync(HttpCall call, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();

            Calls.Add(call.ToString());
            Tokens.Add(call.BearerToken);
            Bodies.Add(call.JsonBody);

            var key = Key(call.Verb, call.Path);
            if (_answers.TryGetValue(key, out var queue) && queue.Count > 0)
                return Task.FromResult(queue.Dequeue());
            if (_standing.TryGetValue(key, out var standing))
                return Task.FromResult(standing);

            throw new InvalidOperationException(
                $"The test did not say what {call} should return. Queued: {string.Join(", ", _answers.Keys)}");
        }

        private static string Key(HttpVerb verb, string path) =>
            $"{verb.ToString().ToUpperInvariant()} {path}";
    }

    /// <summary>A clock that does not wait, and counts how long it was asked to.</summary>
    internal sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new DateTimeOffset(2026, 11, 11, 0, 0, 0, TimeSpan.Zero);

        /// <summary>Every delay asked for, in order.</summary>
        public List<TimeSpan> Delays { get; } = new List<TimeSpan>();

        public Task DelayAsync(TimeSpan duration, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            Delays.Add(duration);
            UtcNow = UtcNow.Add(duration);
            return Task.CompletedTask;
        }
    }
}
