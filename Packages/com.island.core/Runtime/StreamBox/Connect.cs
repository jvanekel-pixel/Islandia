using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Island.Core.StreamBox
{
    /// <summary>Something the phone has asked this device to do.</summary>
    public enum ConnectCommandKind
    {
        /// <summary>A command this build does not know. Ignore it; do not guess.</summary>
        Unknown,
        Play, Pause, Resume, Seek, SeekBy, Stop, Next, Audio, Subtitle, Volume, Mute,
    }

    /// <summary>What the gateway has sent us.</summary>
    public enum ServerMessageKind
    {
        Unknown, Welcome, Devices, State, Command, Profile, Ack, Error,
    }

    /// <summary>A remote-control command, with whichever argument its kind carries.</summary>
    public sealed class ConnectCommand
    {
        private static readonly IReadOnlyDictionary<string, ConnectCommandKind> Kinds =
            new Dictionary<string, ConnectCommandKind>
            {
                ["play"] = ConnectCommandKind.Play,
                ["pause"] = ConnectCommandKind.Pause,
                ["resume"] = ConnectCommandKind.Resume,
                ["seek"] = ConnectCommandKind.Seek,
                ["seekBy"] = ConnectCommandKind.SeekBy,
                ["stop"] = ConnectCommandKind.Stop,
                ["next"] = ConnectCommandKind.Next,
                ["audio"] = ConnectCommandKind.Audio,
                ["subtitle"] = ConnectCommandKind.Subtitle,
                ["volume"] = ConnectCommandKind.Volume,
                ["mute"] = ConnectCommandKind.Mute,
            };

        public ConnectCommand(ConnectCommandKind kind) => Kind = kind;

        public ConnectCommandKind Kind { get; }

        /// <summary>Set on <c>play</c>.</summary>
        public string ItemId { get; private set; }

        /// <summary>Set on <c>play</c> and <c>seek</c>.</summary>
        public long? PositionMs { get; private set; }

        /// <summary>Set on <c>seekBy</c>; may be negative.</summary>
        public long? DeltaMs { get; private set; }

        /// <summary>Set on <c>audio</c> and <c>subtitle</c>. Null subtitle means none.</summary>
        public int? Index { get; private set; }

        /// <summary>Set on <c>volume</c>, 0 to 1.</summary>
        public double? Level { get; private set; }

        /// <summary>Set on <c>mute</c>.</summary>
        public bool? Muted { get; private set; }

        public static ConnectCommand From(JObject body) =>
            new ConnectCommand(Json.Enum(body, "type", Kinds, ConnectCommandKind.Unknown))
            {
                ItemId = Json.OptionalString(body, "itemId"),
                PositionMs = Json.OptionalLong(body, "positionMs"),
                DeltaMs = Json.OptionalLong(body, "deltaMs"),
                Index = Json.OptionalInt(body, "index"),
                Level = Json.OptionalDouble(body, "level"),
                Muted = body?["muted"]?.Type == JTokenType.Boolean ? (bool?)body["muted"].Value<bool>() : null,
            };
    }

    /// <summary>What this device reports back, so the phone can draw a live remote.</summary>
    public sealed class PlaybackState
    {
        public PlaybackState(string itemId, long positionMs, bool playing)
        {
            ItemId = itemId;
            PositionMs = positionMs;
            Playing = playing;
        }

        /// <summary>Null when the holder of the remote may not see what is playing.</summary>
        public string ItemId { get; }
        public long PositionMs { get; }
        public bool Playing { get; }
        public long? DurationMs { get; set; }
        public bool Buffering { get; set; }
        public double Volume { get; set; } = 1.0;
        public bool Muted { get; set; }
        public bool HasNext { get; set; }
        public int? AudioIndex { get; set; }
        public int? SubtitleIndex { get; set; }

        public JObject ToJson()
        {
            var body = new JObject
            {
                ["itemId"] = ItemId,
                ["positionMs"] = PositionMs,
                ["playing"] = Playing,
                ["buffering"] = Buffering,
                ["volume"] = Volume,
                ["muted"] = Muted,
                ["hasNext"] = HasNext,
                ["audio"] = new JArray(),
                ["subtitles"] = new JArray(),
                ["audioIndex"] = AudioIndex.HasValue ? (JToken)AudioIndex.Value : JValue.CreateNull(),
                ["subtitleIndex"] = SubtitleIndex.HasValue ? (JToken)SubtitleIndex.Value : JValue.CreateNull(),
            };
            if (DurationMs.HasValue) body["durationMs"] = DurationMs.Value;
            return body;
        }
    }

    /// <summary>One message from the gateway on the Connect socket.</summary>
    public sealed class ServerMessage
    {
        private static readonly IReadOnlyDictionary<string, ServerMessageKind> Kinds =
            new Dictionary<string, ServerMessageKind>
            {
                ["welcome"] = ServerMessageKind.Welcome,
                ["devices"] = ServerMessageKind.Devices,
                ["state"] = ServerMessageKind.State,
                ["command"] = ServerMessageKind.Command,
                ["profile"] = ServerMessageKind.Profile,
                ["ack"] = ServerMessageKind.Ack,
                ["error"] = ServerMessageKind.Error,
            };

        public ServerMessage(ServerMessageKind kind) => Kind = kind;

        public ServerMessageKind Kind { get; }
        public string SessionId { get; private set; }
        public ConnectCommand Command { get; private set; }
        public string ErrorCode { get; private set; }
        public string ErrorMessage { get; private set; }

        public static ServerMessage From(JObject body)
        {
            var message = new ServerMessage(Json.Enum(body, "type", Kinds, ServerMessageKind.Unknown))
            {
                SessionId = Json.OptionalString(body, "sessionId"),
                ErrorCode = Json.OptionalString(body, "code"),
                ErrorMessage = Json.OptionalString(body, "message"),
            };
            if (message.Kind == ServerMessageKind.Command)
            {
                var command = Json.OptionalObject(body, "command");
                if (command != null) message.Command = ConnectCommand.From(command);
            }
            return message;
        }

        /// <summary>The first message this device must send: who it is.</summary>
        public static JObject Hello(string deviceToken) => new JObject
        {
            ["type"] = "hello",
            ["role"] = "tv",
            ["token"] = deviceToken,
        };
    }

    /// <summary>Why the gateway closed the socket.</summary>
    public static class ConnectClose
    {
        /// <summary>The device token is gone. Return to pairing; never retry.</summary>
        public const int SignedOut = 4401;

        /// <summary>A newer socket took this device's place. Do not reconnect and fight it.</summary>
        public const int Replaced = 4409;

        /// <summary>Whether reconnecting after this code would be wrong.</summary>
        public static bool IsFinal(int code) => code == SignedOut || code == Replaced;
    }
}
