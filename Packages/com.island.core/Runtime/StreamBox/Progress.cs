using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Island.Core.StreamBox
{
    /// <summary>Why a progress report was sent.</summary>
    public enum ProgressEventKind
    {
        Heartbeat,
        Pause,
        Seek,
        Stop,
        TrackChange,
        /// <summary>The app lost focus — in a headset, usually because it was taken off.</summary>
        Background,
        StartOver,
    }

    /// <summary>One report of where the viewer is.</summary>
    /// <remarks>
    /// The id is generated here, by the client, so that replaying a batch after a dropped
    /// connection cannot double-count. StreamBox owns the rules about what the reports mean; this
    /// sends them faithfully and does not second-guess.
    /// </remarks>
    public sealed class ProgressEvent
    {
        private static readonly IReadOnlyDictionary<ProgressEventKind, string> Names =
            new Dictionary<ProgressEventKind, string>
            {
                [ProgressEventKind.Heartbeat] = "heartbeat",
                [ProgressEventKind.Pause] = "pause",
                [ProgressEventKind.Seek] = "seek",
                [ProgressEventKind.Stop] = "stop",
                [ProgressEventKind.TrackChange] = "track_change",
                [ProgressEventKind.Background] = "background",
                [ProgressEventKind.StartOver] = "start_over",
            };

        public ProgressEvent(string id, ProgressEventKind kind, string itemId, string profileId,
                             string playbackId, string playbackStartedAt, string clientTs, long positionMs)
        {
            Id = id;
            Kind = kind;
            ItemId = itemId;
            ProfileId = profileId;
            PlaybackId = playbackId;
            PlaybackStartedAt = playbackStartedAt;
            ClientTs = clientTs;
            PositionMs = positionMs;
        }

        public string Id { get; }
        public ProgressEventKind Kind { get; }
        public string ItemId { get; }
        public string ProfileId { get; }

        /// <summary>One per player load, so the server can group a session's reports.</summary>
        public string PlaybackId { get; }

        public string PlaybackStartedAt { get; }
        public string ClientTs { get; }
        public long PositionMs { get; }
        public long? DurationMs { get; set; }

        /// <summary>Where a seek came from.</summary>
        public long? FromMs { get; set; }

        public JObject ToJson()
        {
            var body = new JObject
            {
                ["id"] = Id,
                ["kind"] = Names[Kind],
                ["itemId"] = ItemId,
                ["profileId"] = ProfileId,
                ["playbackId"] = PlaybackId,
                ["playbackStartedAt"] = PlaybackStartedAt,
                ["clientTs"] = ClientTs,
                ["positionMs"] = PositionMs,
            };
            if (DurationMs.HasValue) body["durationMs"] = DurationMs.Value;
            if (FromMs.HasValue) body["data"] = new JObject { ["fromMs"] = FromMs.Value };
            return body;
        }

        /// <summary>The name this kind travels under. Exposed so a test can hold it to the contract.</summary>
        public static string NameOf(ProgressEventKind kind) => Names[kind];

        /// <summary>Wraps events into the batch shape the gateway expects.</summary>
        public static JObject Batch(IEnumerable<ProgressEvent> events, string sentAt)
        {
            var array = new JArray();
            foreach (var reported in events) array.Add(reported.ToJson());
            if (array.Count == 0)
                throw new ArgumentException("A progress batch must carry at least one event.", nameof(events));
            return new JObject { ["sentAt"] = sentAt, ["events"] = array };
        }
    }
}
