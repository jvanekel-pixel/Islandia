using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Island.Core.StreamBox
{
    /// <summary>
    /// The plain .NET transport: an <see cref="HttpClient"/> against one gateway.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Good enough for the headset and necessary for the tests. The Unity layer may substitute a
    /// <c>UnityWebRequest</c> transport if a device gate shows a reason to — that is the whole
    /// point of <see cref="IHttpTransport"/> — but nothing needs one until something measured says
    /// so, and an unused abstraction with one implementation is cheaper than two.
    /// </para>
    /// <para>
    /// The gateway's address is given here, at the edge, and never enters the client. This
    /// repository is public, so an address the core cannot see is an address the core cannot
    /// commit (`07-STREAMBOX.md` §1).
    /// </para>
    /// <para>
    /// The bearer token goes in the <c>Authorization</c> header of the one request it belongs to
    /// and is never set as a default header on the shared client, so a call made while signed out
    /// cannot accidentally carry the last token this transport saw.
    /// </para>
    /// </remarks>
    public sealed class HttpTransport : IHttpTransport, IDisposable
    {
        private readonly HttpClient _http;
        private readonly bool _ownsClient;
        private readonly string _basePath;

        /// <summary>
        /// </summary>
        /// <param name="baseAddress">The gateway, e.g. <c>https://gateway.example</c>.</param>
        /// <param name="basePath">The API root. StreamBox serves <c>/api</c>.</param>
        /// <param name="timeout">How long to wait for an answer before giving up.</param>
        public HttpTransport(Uri baseAddress, string basePath = "/api", TimeSpan? timeout = null)
        {
            if (baseAddress == null) throw new ArgumentNullException(nameof(baseAddress));

            _http = new HttpClient { BaseAddress = baseAddress, Timeout = timeout ?? TimeSpan.FromSeconds(20) };
            _ownsClient = true;
            _basePath = Normalise(basePath);
        }

        /// <summary>For a caller that manages the <see cref="HttpClient"/> itself.</summary>
        public HttpTransport(HttpClient http, string basePath = "/api")
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
            _ownsClient = false;
            _basePath = Normalise(basePath);
        }

        public async Task<HttpReply> SendAsync(HttpCall call, CancellationToken cancellation)
        {
            using (var request = new HttpRequestMessage(Method(call.Verb), _basePath + call.Path))
            {
                if (call.JsonBody != null)
                    request.Content = new StringContent(call.JsonBody, Encoding.UTF8, "application/json");

                if (call.BearerToken != null)
                {
                    request.Headers.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", call.BearerToken);
                }

                using (var response = await _http.SendAsync(request, cancellation).ConfigureAwait(false))
                {
                    // Every status is an answer the client decides about, so nothing here throws
                    // for a 401 or a 404.
                    var body = response.Content == null
                        ? string.Empty
                        : await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return new HttpReply((int)response.StatusCode, body);
                }
            }
        }

        private static HttpMethod Method(HttpVerb verb)
        {
            switch (verb)
            {
                case HttpVerb.Get: return HttpMethod.Get;
                case HttpVerb.Post: return HttpMethod.Post;
                case HttpVerb.Put: return HttpMethod.Put;
                case HttpVerb.Delete: return HttpMethod.Delete;
                default: throw new ArgumentOutOfRangeException(nameof(verb), verb, "No such verb.");
            }
        }

        private static string Normalise(string basePath)
        {
            if (string.IsNullOrEmpty(basePath)) return string.Empty;
            var trimmed = basePath.TrimEnd('/');
            return trimmed.StartsWith("/", StringComparison.Ordinal) ? trimmed : "/" + trimmed;
        }

        public void Dispose()
        {
            if (_ownsClient) _http.Dispose();
        }
    }
}
