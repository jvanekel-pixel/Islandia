using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Island.Core.StreamBox
{
    /// <summary>What kind of thing a catalogue entry is.</summary>
    public enum ItemKind
    {
        /// <summary>A kind this build does not know. StreamBox may add more; show it plainly.</summary>
        Unknown,
        Movie,
        Series,
        Season,
        Episode,
    }

    /// <summary>One picture, with what is needed to show something before it loads.</summary>
    public sealed class ImageRef
    {
        public ImageRef(string url, string blurhash, double? aspect)
        {
            Url = url;
            Blurhash = blurhash;
            Aspect = aspect;
        }

        /// <summary>Always fetched through the gateway. No engine address ever reaches us.</summary>
        public string Url { get; }

        /// <summary>A few bytes that paint the right colours while the real picture loads.</summary>
        public string Blurhash { get; }

        public double? Aspect { get; }

        public static ImageRef From(JObject body)
        {
            var url = Json.OptionalString(body, "url");
            return url == null ? null : new ImageRef(url, Json.OptionalString(body, "blurhash"),
                                                    Json.OptionalDouble(body, "aspect"));
        }
    }

    /// <summary>How far through something the viewer is.</summary>
    public sealed class PlaybackProgress
    {
        public PlaybackProgress(long positionMs, long? durationMs, bool watched, bool inProgress, int playCount)
        {
            PositionMs = positionMs;
            DurationMs = durationMs;
            Watched = watched;
            InProgress = inProgress;
            PlayCount = playCount;
        }

        public long PositionMs { get; }
        public long? DurationMs { get; }
        public bool Watched { get; }
        public bool InProgress { get; }
        public int PlayCount { get; }

        /// <summary>0 to 1 when the length is known, otherwise null.</summary>
        public double? Fraction =>
            DurationMs.HasValue && DurationMs.Value > 0
                ? (double?)System.Math.Min(1.0, (double)PositionMs / DurationMs.Value)
                : null;

        public static PlaybackProgress From(JObject body, string path) =>
            new PlaybackProgress(
                Json.OptionalLong(body, "positionMs") ?? 0,
                Json.OptionalLong(body, "durationMs"),
                Json.OptionalBool(body, "watched"),
                Json.OptionalBool(body, "inProgress"),
                Json.OptionalInt(body, "playCount") ?? 0);
    }

    /// <summary>A film, programme, series or episode, as the gateway describes it.</summary>
    /// <remarks>
    /// Only what Islandia shows is mapped. StreamBox sends a great deal more — cast, studios,
    /// provider ids, per-track media detail — and the unmapped parts are ignored rather than
    /// rejected, so adding a field there never breaks a build here.
    /// </remarks>
    public sealed class CatalogItem
    {
        private static readonly IReadOnlyDictionary<string, ItemKind> Kinds =
            new Dictionary<string, ItemKind>
            {
                ["movie"] = ItemKind.Movie,
                ["series"] = ItemKind.Series,
                ["season"] = ItemKind.Season,
                ["episode"] = ItemKind.Episode,
            };

        public CatalogItem(string id, ItemKind kind, string libraryId, string name)
        {
            Id = id;
            Kind = kind;
            LibraryId = libraryId;
            Name = name;
            Genres = new List<string>();
        }

        public string Id { get; }
        public ItemKind Kind { get; }
        public string LibraryId { get; }
        public string Name { get; }

        public int? Year { get; private set; }
        public string Overview { get; private set; }
        public long? RuntimeMs { get; private set; }
        public IReadOnlyList<string> Genres { get; private set; }
        public ImageRef Poster { get; private set; }
        public ImageRef Backdrop { get; private set; }
        public ImageRef Thumb { get; private set; }
        public PlaybackProgress Progress { get; private set; }

        /// <summary>Episodes and seasons: which programme they belong to.</summary>
        public string SeriesName { get; private set; }
        public int? SeasonNumber { get; private set; }
        public int? EpisodeNumber { get; private set; }

        public static CatalogItem From(JObject body, string path)
        {
            var item = new CatalogItem(
                Json.RequiredString(body, "id", path),
                Json.Enum(body, "kind", Kinds, ItemKind.Unknown),
                Json.RequiredString(body, "libraryId", path),
                Json.RequiredString(body, "name", path))
            {
                Year = Json.OptionalInt(body, "year"),
                Overview = Json.OptionalString(body, "overview"),
                RuntimeMs = Json.OptionalLong(body, "runtimeMs"),
                Genres = Json.OptionalStrings(body, "genres"),
                SeasonNumber = Json.OptionalInt(body, "seasonNumber"),
                EpisodeNumber = Json.OptionalInt(body, "episodeNumber"),
            };

            var images = Json.OptionalObject(body, "images");
            if (images != null)
            {
                item.Poster = ImageRef.From(Json.OptionalObject(images, "poster"));
                item.Backdrop = ImageRef.From(Json.OptionalObject(images, "backdrop"));
                item.Thumb = ImageRef.From(Json.OptionalObject(images, "thumb"));
            }

            var series = Json.OptionalObject(body, "series");
            if (series != null) item.SeriesName = Json.OptionalString(series, "name");

            var progress = Json.OptionalObject(body, "progress");
            if (progress != null) item.Progress = PlaybackProgress.From(progress, path + ".progress");

            return item;
        }
    }

    /// <summary>One page of a library, and where the next one starts.</summary>
    public sealed class ItemsPage
    {
        public ItemsPage(IReadOnlyList<CatalogItem> items, int total, int offset, int limit, int? nextOffset)
        {
            Items = items;
            Total = total;
            Offset = offset;
            Limit = limit;
            NextOffset = nextOffset;
        }

        public IReadOnlyList<CatalogItem> Items { get; }
        public int Total { get; }
        public int Offset { get; }
        public int Limit { get; }

        /// <summary>
        /// Where to ask from next, or null at the end. It can be further than offset plus the
        /// items returned, because the gateway skips titles this profile may not see.
        /// </summary>
        public int? NextOffset { get; }

        public bool HasMore => NextOffset.HasValue;

        public static ItemsPage From(JObject body) =>
            new ItemsPage(
                Json.RequiredArray(body, "items", "", CatalogItem.From),
                Json.OptionalInt(body, "total") ?? 0,
                Json.OptionalInt(body, "offset") ?? 0,
                Json.OptionalInt(body, "limit") ?? 0,
                Json.OptionalInt(body, "nextOffset"));
    }

    /// <summary>A named strip of items on the home screen.</summary>
    public sealed class HomeRow
    {
        public HomeRow(string id, string title, IReadOnlyList<CatalogItem> items)
        {
            Id = id;
            Title = title;
            Items = items;
        }

        public string Id { get; }
        public string Title { get; }
        public IReadOnlyList<CatalogItem> Items { get; }

        public static HomeRow From(JObject body, string path) =>
            new HomeRow(
                Json.OptionalString(body, "id") ?? "",
                Json.OptionalString(body, "title") ?? "",
                Json.OptionalArray(body, "items", path, CatalogItem.From));
    }
}
