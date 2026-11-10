using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Island.Core.StreamBox
{
    /// <summary>Whether the engine could copy the streams as they are, or had to re-encode.</summary>
    public enum StreamDecision
    {
        /// <summary>A decision this build does not know. Treat it as a transcode: the cautious read.</summary>
        Unknown,
        /// <summary>Copied. Cheap for the server, and the original quality.</summary>
        Remux,
        /// <summary>Re-encoded, because the client could not decode the source.</summary>
        Transcode,
    }

    /// <summary>What this client can decode. The gateway plans the stream around it.</summary>
    /// <remarks>
    /// <see cref="Quest"/> is deliberately the floor rather than a guess at the maximum. A Quest 2
    /// decodes H.264 and stereo AAC in hardware; anything beyond that must be proven on a real
    /// device before it is claimed here, because the cost of over-claiming is a black screen in a
    /// headset rather than a compile error.
    /// </remarks>
    public sealed class PlaybackCapabilities
    {
        public PlaybackCapabilities(string name, IReadOnlyList<string> video, IReadOnlyList<string> audio,
                                    bool hdr, int maxWidth, int maxHeight, int maxAudioChannels,
                                    string segmentContainer)
        {
            Name = name;
            Video = video;
            Audio = audio;
            Hdr = hdr;
            MaxWidth = maxWidth;
            MaxHeight = maxHeight;
            MaxAudioChannels = maxAudioChannels;
            SegmentContainer = segmentContainer;
        }

        public string Name { get; }
        public IReadOnlyList<string> Video { get; }
        public IReadOnlyList<string> Audio { get; }
        public bool Hdr { get; }
        public int MaxWidth { get; }
        public int MaxHeight { get; }
        public int MaxAudioChannels { get; }
        public string SegmentContainer { get; }

        /// <summary>
        /// What Islandia asks for on a Quest 2. Matches <c>playback.islandiaCapabilities</c> in
        /// the contract; a test holds the two together.
        /// </summary>
        public static readonly PlaybackCapabilities Quest = new PlaybackCapabilities(
            "islandia",
            new List<string> { "h264" },
            new List<string> { "aac" },
            hdr: false, maxWidth: 1920, maxHeight: 1080, maxAudioChannels: 2,
            segmentContainer: "ts");

        public JObject ToJson() => new JObject
        {
            ["name"] = Name,
            ["video"] = new JArray(Video),
            ["audio"] = new JArray(Audio),
            ["hdr"] = Hdr,
            ["maxWidth"] = MaxWidth,
            ["maxHeight"] = MaxHeight,
            ["maxAudioChannels"] = MaxAudioChannels,
            ["segmentContainer"] = SegmentContainer,
        };
    }

    /// <summary>A request to start playing something.</summary>
    public sealed class PlayRequest
    {
        public PlayRequest(string itemId, PlaybackCapabilities capabilities)
        {
            ItemId = itemId;
            Capabilities = capabilities;
        }

        public string ItemId { get; }
        public PlaybackCapabilities Capabilities { get; }

        /// <summary>Where to start, for resuming. Omitted means the beginning.</summary>
        public long? StartMs { get; set; }

        public int? AudioIndex { get; set; }

        /// <summary>Which subtitle track, or null for none.</summary>
        public int? SubtitleIndex { get; set; }

        /// <summary>
        /// Ask the server to draw the subtitles into the picture. The first version of Islandia
        /// always does: it works with every player backend, where rendering text ourselves does
        /// not.
        /// </summary>
        public bool BurnSubtitle { get; set; }

        public JObject ToJson()
        {
            var body = new JObject
            {
                ["itemId"] = ItemId,
                ["capabilities"] = Capabilities.ToJson(),
            };
            if (StartMs.HasValue) body["startMs"] = StartMs.Value;
            if (AudioIndex.HasValue) body["audioIndex"] = AudioIndex.Value;
            if (SubtitleIndex.HasValue) body["subtitleIndex"] = SubtitleIndex.Value;
            if (BurnSubtitle) body["burnSubtitle"] = true;
            return body;
        }
    }

    /// <summary>A subtitle track offered alongside a stream.</summary>
    public sealed class SubtitleRef
    {
        public SubtitleRef(int index, string language, string url)
        {
            Index = index;
            Language = language;
            Url = url;
        }

        public int Index { get; }
        public string Language { get; }

        /// <summary>WebVTT, under the same stream token. Image tracks are never listed here.</summary>
        public string Url { get; }

        public static SubtitleRef From(JObject body, string path) =>
            new SubtitleRef(
                Json.OptionalInt(body, "index") ?? 0,
                Json.OptionalString(body, "language"),
                Json.OptionalString(body, "url"));
    }

    /// <summary>The gateway's answer: one address, and what it decided.</summary>
    public sealed class PlayResponse
    {
        private static readonly IReadOnlyDictionary<string, StreamDecision> Decisions =
            new Dictionary<string, StreamDecision>
            {
                ["remux"] = StreamDecision.Remux,
                ["transcode"] = StreamDecision.Transcode,
            };

        public PlayResponse(string streamId, string url, StreamDecision decision,
                            IReadOnlyList<string> reasons, IReadOnlyList<SubtitleRef> subtitles)
        {
            StreamId = streamId;
            Url = url;
            Decision = decision;
            Reasons = reasons;
            Subtitles = subtitles;
        }

        public string StreamId { get; }

        /// <summary>The HLS master playlist. Nothing about the engine is visible in or behind it.</summary>
        public string Url { get; }

        public StreamDecision Decision { get; }

        /// <summary>Why a copy was not possible. Empty for a remux.</summary>
        public IReadOnlyList<string> Reasons { get; }

        public IReadOnlyList<SubtitleRef> Subtitles { get; }
        public long? DurationMs { get; private set; }
        public string ExpiresAt { get; private set; }
        public bool Remote { get; private set; }

        /// <summary>The track burned into the picture, if any.</summary>
        public int? BurnedInSubtitle { get; private set; }

        /// <summary>
        /// The stream token, taken from the address. Needed to stop the stream, and to tell one
        /// active stream from another.
        /// </summary>
        public string StreamToken
        {
            get
            {
                if (string.IsNullOrEmpty(Url)) return null;
                var parts = Url.Split('/');
                for (var i = 0; i < parts.Length - 1; i++)
                    if (parts[i] == "stream") return parts[i + 1];
                return null;
            }
        }

        public static PlayResponse From(JObject body) =>
            new PlayResponse(
                Json.RequiredString(body, "streamId", ""),
                Json.RequiredString(body, "url", ""),
                // An unknown decision reads as a transcode, which is the cautious assumption: it
                // means the stream may be expensive for the server, so do not start several.
                Json.Enum(body, "decision", Decisions, StreamDecision.Unknown),
                Json.OptionalStrings(body, "reasons"),
                Json.OptionalArray(body, "subtitles", "", SubtitleRef.From))
            {
                DurationMs = Json.OptionalLong(body, "durationMs"),
                ExpiresAt = Json.OptionalString(body, "expiresAt"),
                Remote = Json.OptionalBool(body, "remote"),
                BurnedInSubtitle = Json.OptionalInt(body, "burnedInSubtitle"),
            };
    }
}
