using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Island.Core.StreamBox
{
    /// <summary>One track.</summary>
    public sealed class MusicTrack
    {
        public MusicTrack(string id, string name, string artist, string album, long durationMs, int? trackNumber)
        {
            Id = id;
            Name = name;
            Artist = artist;
            Album = album;
            DurationMs = durationMs;
            TrackNumber = trackNumber;
        }

        public string Id { get; }
        public string Name { get; }
        public string Artist { get; }
        public string Album { get; }
        public long DurationMs { get; }
        public int? TrackNumber { get; }

        public static MusicTrack From(JObject body, string path) =>
            new MusicTrack(
                Json.RequiredString(body, "id", path),
                Json.RequiredString(body, "name", path),
                Json.OptionalString(body, "artist"),
                Json.OptionalString(body, "album"),
                Json.OptionalLong(body, "durationMs") ?? 0,
                Json.OptionalInt(body, "trackNumber"));
    }

    /// <summary>An album, with its tracks when the details were asked for.</summary>
    public sealed class MusicAlbum
    {
        public MusicAlbum(string id, string name, string artist, int? year,
                          ImageRef art, IReadOnlyList<MusicTrack> tracks)
        {
            Id = id;
            Name = name;
            Artist = artist;
            Year = year;
            Art = art;
            Tracks = tracks;
        }

        public string Id { get; }
        public string Name { get; }
        public string Artist { get; }
        public int? Year { get; }
        public ImageRef Art { get; }

        /// <summary>Empty on a listing; filled on the album's own endpoint.</summary>
        public IReadOnlyList<MusicTrack> Tracks { get; }

        public static MusicAlbum From(JObject body, string path) =>
            new MusicAlbum(
                Json.RequiredString(body, "id", path),
                Json.RequiredString(body, "name", path),
                Json.OptionalString(body, "artist"),
                Json.OptionalInt(body, "year"),
                ImageRef.From(Json.OptionalObject(body, "art")),
                Json.OptionalArray(body, "tracks", path, MusicTrack.From));
    }

    /// <summary>Where to fetch a track's audio.</summary>
    /// <remarks>
    /// The address is signed and needs no header, which is what lets the headset pull a track into
    /// a bounded cache with range requests and play it from disk — rather than decoding a live
    /// socket, which is the thing that goes wrong on a mobile network.
    /// </remarks>
    public sealed class MusicPlayResponse
    {
        public MusicPlayResponse(string url, long? durationMs)
        {
            Url = url;
            DurationMs = durationMs;
        }

        public string Url { get; }
        public long? DurationMs { get; }

        public static MusicPlayResponse From(JObject body) =>
            new MusicPlayResponse(
                Json.RequiredString(body, "url", ""),
                Json.OptionalLong(body, "durationMs"));
    }
}
