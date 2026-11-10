// The fake library.
//
// Every title here is invented. Nothing from the owner's real library is in this repository, and
// the privacy check in tools/lint keeps it that way. The shapes follow contract/streambox/v1.json.

const LIBRARY_FILMS = '11111111-1111-4111-8111-111111111111';
const LIBRARY_SHOWS = '22222222-2222-4222-8222-222222222222';

const image = (seed) => ({
  url: `/api/images/${seed}.png`,
  blurhash: 'L6PZfSjE.AyE_3t7t7R**0o#DgR4',
  aspect: 2 / 3,
});

const media = (durationMs) => ({
  sourceId: 'mock:1',
  container: 'mkv',
  durationMs,
  bitrate: 8_000_000,
  video: { codec: 'h264', width: 1920, height: 1080, range: 'sdr' },
  audio: [{ index: 0, codec: 'aac', channels: 2, language: 'eng', isDefault: true }],
  subtitles: [
    { index: 0, codec: 'subrip', kind: 'text', language: 'eng', isDefault: false, forced: false, external: false },
    { index: 1, codec: 'pgs', kind: 'image', language: 'fra', isDefault: false, forced: false, external: false },
  ],
  flags: { uhd: false, hdr: false, dolbyVision: false, atmos: false, captions: true },
});

const film = (id, name, year, overview, runtimeMinutes) => ({
  id,
  kind: 'movie',
  libraryId: LIBRARY_FILMS,
  name,
  year,
  overview,
  genres: ['Drama'],
  tags: [],
  studios: ['Mock Pictures'],
  runtimeMs: runtimeMinutes * 60_000,
  providerIds: {},
  images: { poster: image(id) },
  media: media(runtimeMinutes * 60_000),
  addedAt: '2026-10-01T12:00:00.000Z',
});

export const films = [
  film('mk:film-1', 'The Long Afternoon', 2019, 'A quiet film about a quiet place.', 104),
  film('mk:film-2', 'Harbour Lights', 2022, 'Two strangers, one ferry, and a storm.', 96),
  film('mk:film-3', 'Saltmarsh', 2024, 'The tide comes in whether or not you are ready.', 118),
];

export const series = {
  id: 'mk:show-1',
  kind: 'series',
  libraryId: LIBRARY_SHOWS,
  name: 'Island Life',
  year: 2023,
  overview: 'Six episodes about building something slowly.',
  genres: ['Documentary'],
  tags: [],
  studios: ['Mock Television'],
  providerIds: {},
  images: { poster: image('mk:show-1') },
  status: 'continuing',
  childCount: 1,
  episodeCount: 3,
};

export const seasons = [{
  id: 'mk:season-1',
  kind: 'season',
  libraryId: LIBRARY_SHOWS,
  name: 'Series 1',
  seasonNumber: 1,
  series: { id: series.id, name: series.name },
  genres: [], tags: [], studios: [], providerIds: {},
  images: { poster: image('mk:season-1') },
  childCount: 3,
}];

export const episodes = [1, 2, 3].map((n) => ({
  id: `mk:episode-${n}`,
  kind: 'episode',
  libraryId: LIBRARY_SHOWS,
  name: ['Foundations', 'The First Wall', 'Weather'][n - 1],
  overview: 'An episode of the fake programme.',
  seasonNumber: 1,
  seasonId: 'mk:season-1',
  episodeNumber: n,
  series: { id: series.id, name: series.name },
  genres: [], tags: [], studios: [], providerIds: {},
  images: { thumb: image(`mk:episode-${n}`) },
  runtimeMs: 24 * 60_000,
  media: media(24 * 60_000),
}));

export const allItems = [...films, series, ...seasons, ...episodes];

export const byId = (id) => allItems.find((item) => item.id === id);

export const libraries = [
  { id: LIBRARY_FILMS, name: 'Films', kind: 'movies' },
  { id: LIBRARY_SHOWS, name: 'Programmes', kind: 'tvshows' },
];

export const homeRows = [
  { id: 'continue', title: 'Carry on watching', items: [episodes[1]] },
  { id: 'latest-films', title: 'Recently added', items: films },
  { id: 'shows', title: 'Programmes', items: [series] },
];

// --- music ------------------------------------------------------------------------------------

export const albums = [
  {
    id: 'mk:album-1',
    name: 'Shoreline',
    artist: 'The Mock Ensemble',
    year: 2021,
    art: { url: '/api/music/art?token=mock-album-1' },
    tracks: [1, 2, 3].map((n) => ({
      id: `mk:track-${n}`,
      name: ['Low Tide', 'Breakwater', 'Gulls'][n - 1],
      artist: 'The Mock Ensemble',
      album: 'Shoreline',
      trackNumber: n,
      durationMs: 10_000,
    })),
  },
];

export const allTracks = albums.flatMap((album) => album.tracks);
