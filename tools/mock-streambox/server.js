// A fake StreamBox gateway.
//
// It implements what contract/streambox/v1.json describes: device pairing, a small catalogue, the
// stream broker, music, progress and the Connect socket. It holds no real data and talks to no
// real service, so the Islandia client can be built and tested long before anyone points it at
// the owner's server.
//
//   node server.js [--port 8787] [--auto-approve-ms 1500]
//
// Pairing approves itself after a short delay by default, so a test does not need to drive a
// phone. Pass --auto-approve-ms 0 to require POST /api/pair/{code}/approve instead.

import { createServer } from 'node:http';
import { createReadStream } from 'node:fs';
import { stat } from 'node:fs/promises';
import { randomUUID, randomInt } from 'node:crypto';
import path from 'node:path';
import { WebSocketServer } from 'ws';
import * as fixtures from './fixtures.js';
import { buildHls, buildAudio, mediaDir } from './media.js';

const args = process.argv.slice(2);
const flag = (name, fallback) => {
  const at = args.indexOf(`--${name}`);
  return at === -1 ? fallback : args[at + 1];
};

const PORT = Number(flag('port', 8787));
const AUTO_APPROVE_MS = Number(flag('auto-approve-ms', 1500));

// --- state ------------------------------------------------------------------------------------

const pairings = new Map();   // code -> { pollSecret, status, deviceToken, expiresAt }
const sessions = new Map();   // deviceToken -> { userName, profileName, profileId }
const streams = new Map();    // streamToken -> { itemId, startedAt }
const progress = new Map();   // itemId -> progress state
const events = [];            // every progress event received, for tests to inspect
const connects = new Set();   // live Connect sockets

const MIME = {
  '.m3u8': 'application/vnd.apple.mpegurl',
  '.ts': 'video/mp2t',
  '.m4a': 'audio/mp4',
  '.png': 'image/png',
};

// --- helpers ----------------------------------------------------------------------------------

const json = (res, status, body) => {
  const text = JSON.stringify(body);
  res.writeHead(status, {
    'content-type': 'application/json; charset=utf-8',
    'content-length': Buffer.byteLength(text),
    'cache-control': 'no-store',
  });
  res.end(text);
};

const notFound = (res) => json(res, 404, { error: 'not_found' });

/** Every authenticated route goes through here. An absent or unknown token is a 401. */
const authed = (req) => {
  const header = req.headers.authorization ?? '';
  const token = header.startsWith('Bearer ') ? header.slice(7) : null;
  return token && sessions.has(token) ? { token, ...sessions.get(token) } : null;
};

const readJson = (req) => new Promise((resolve, reject) => {
  let body = '';
  req.on('data', (chunk) => {
    body += chunk;
    if (body.length > 1_000_000) reject(new Error('body too large'));
  });
  req.on('end', () => {
    try { resolve(body ? JSON.parse(body) : {}); } catch (error) { reject(error); }
  });
});

const serveFile = async (res, file, { ranged = false, req = null } = {}) => {
  let info;
  try { info = await stat(file); } catch { return notFound(res); }
  const type = MIME[path.extname(file)] ?? 'application/octet-stream';

  const range = ranged ? req?.headers.range : undefined;
  if (range) {
    const match = /bytes=(\d*)-(\d*)/.exec(range);
    const start = match[1] ? Number(match[1]) : 0;
    const end = match[2] ? Number(match[2]) : info.size - 1;
    res.writeHead(206, {
      'content-type': type,
      'content-range': `bytes ${start}-${end}/${info.size}`,
      'accept-ranges': 'bytes',
      'content-length': end - start + 1,
    });
    return createReadStream(file, { start, end }).pipe(res);
  }

  res.writeHead(200, {
    'content-type': type,
    'content-length': info.size,
    'accept-ranges': ranged ? 'bytes' : 'none',
  });
  createReadStream(file).pipe(res);
};

// --- routes -----------------------------------------------------------------------------------

async function route(req, res, url) {
  const p = url.pathname;

  // ---- pairing: no token yet, by definition -------------------------------------------------

  if (req.method === 'POST' && p === '/api/pair/start') {
    const code = String(randomInt(100000, 999999));
    const pollSecret = randomUUID();
    const expiresAt = new Date(Date.now() + 600_000).toISOString();
    pairings.set(code, { pollSecret, status: 'pending', expiresAt });
    if (AUTO_APPROVE_MS > 0) {
      setTimeout(() => approve(code), AUTO_APPROVE_MS).unref();
    }
    return json(res, 200, {
      code, pollSecret, expiresAt,
      approveUrl: `http://localhost:${PORT}/pair/${code}`,
      intervalSeconds: 1,
    });
  }

  if (req.method === 'POST' && p === '/api/pair/poll') {
    const body = await readJson(req);
    const entry = [...pairings.values()].find((e) => e.pollSecret === body.pollSecret);
    if (!entry) return json(res, 200, { status: 'expired' });
    if (entry.status !== 'approved') return json(res, 200, { status: entry.status });
    if (entry.collected) return json(res, 200, { status: 'expired' }); // handed over exactly once
    entry.collected = true;
    return json(res, 200, {
      status: 'approved',
      deviceToken: entry.deviceToken,
      userName: 'Mock Owner',
      profileName: 'Mock Profile',
    });
  }

  // Stands in for the phone approving. Real StreamBox requires a signed-in person.
  const approveMatch = /^\/api\/pair\/(\d{6})\/(approve|deny)$/.exec(p);
  if (req.method === 'POST' && approveMatch) {
    const [, code, action] = approveMatch;
    if (!pairings.has(code)) return notFound(res);
    if (action === 'approve') approve(code);
    else pairings.get(code).status = 'denied';
    return json(res, 200, { status: pairings.get(code).status });
  }

  // ---- stream and media: the token in the path is the credential ----------------------------

  const streamMatch = /^\/api\/stream\/([^/]+)\/(.+)$/.exec(p);
  if (req.method === 'GET' && streamMatch) {
    const [, token, file] = streamMatch;
    if (!streams.has(token)) return notFound(res);
    if (!/^[\w.-]+$/.test(file)) return notFound(res);
    return serveFile(res, path.join(mediaDir, file));
  }

  if (req.method === 'GET' && p === '/api/music/audio') {
    if (!url.searchParams.get('token')) return notFound(res);
    return serveFile(res, path.join(mediaDir, 'track.m4a'), { ranged: true, req });
  }

  if (req.method === 'GET' && (p.startsWith('/api/images/') || p === '/api/music/art')) {
    // A one-pixel PNG. Shape matters here, not pixels.
    const png = Buffer.from(
      'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==',
      'base64');
    res.writeHead(200, { 'content-type': 'image/png', 'content-length': png.length });
    return res.end(png);
  }

  // ---- the mock's own corner, for tests. Never authenticated: a test needs them before it has a token. ------------------------------------------------------

  if (req.method === 'GET' && p === '/mock/events') return json(res, 200, { events });
  if (req.method === 'GET' && p === '/mock/health') {
    return json(res, 200, { ok: true, sessions: sessions.size, streams: streams.size });
  }

  // ---- everything else needs the device token -----------------------------------------------

  const session = authed(req);
  if (!session) return json(res, 401, { error: 'unauthorized' });

  if (req.method === 'GET' && p === '/api/catalog/home') {
    return json(res, 200, { rows: fixtures.homeRows, libraries: fixtures.libraries });
  }
  if (req.method === 'GET' && p === '/api/catalog/latest') {
    return json(res, 200, { items: fixtures.films });
  }
  if (req.method === 'GET' && p === '/api/catalog/resume') {
    return json(res, 200, { items: fixtures.episodes.slice(1, 2) });
  }
  if (req.method === 'GET' && p === '/api/catalog/next-up') {
    return json(res, 200, { items: fixtures.episodes.slice(2, 3) });
  }
  if (req.method === 'GET' && p === '/api/catalog/search') {
    const q = (url.searchParams.get('q') ?? '').toLowerCase();
    const hits = q ? fixtures.allItems.filter((i) => i.name.toLowerCase().includes(q)) : [];
    return json(res, 200, { items: hits });
  }

  const libraryMatch = /^\/api\/catalog\/libraries\/([^/]+)\/items$/.exec(p);
  if (req.method === 'GET' && libraryMatch) {
    const items = fixtures.allItems.filter((i) => i.libraryId === libraryMatch[1]);
    const offset = Number(url.searchParams.get('offset') ?? 0);
    const limit = Number(url.searchParams.get('limit') ?? 50);
    const page = items.slice(offset, offset + limit);
    const body = { items: page, total: items.length, offset, limit };
    if (offset + page.length < items.length) body.nextOffset = offset + page.length;
    return json(res, 200, body);
  }

  const itemMatch = /^\/api\/catalog\/items\/(.+)$/.exec(p);
  if (req.method === 'GET' && itemMatch) {
    const item = fixtures.byId(decodeURIComponent(itemMatch[1]));
    return item ? json(res, 200, item) : notFound(res);
  }

  // ---- playback ------------------------------------------------------------------------------

  if (req.method === 'POST' && p === '/api/play') {
    const body = await readJson(req);
    const item = fixtures.byId(body.itemId);
    if (!item) return notFound(res);
    if (!body.capabilities?.name) return json(res, 400, { error: 'capabilities_required' });

    const token = randomUUID();
    streams.set(token, { itemId: body.itemId, startedAt: Date.now() });

    // A client asking for more than H.264/AAC gets a transcode, the same decision real
    // StreamBox would make, so the client's handling of either answer is exercised.
    const copyable = body.capabilities.video?.includes('h264')
                  && body.capabilities.audio?.includes('aac');
    return json(res, 200, {
      streamId: randomUUID(),
      url: `/api/stream/${token}/master.m3u8`,
      decision: copyable ? 'remux' : 'transcode',
      reasons: copyable ? [] : ['the client cannot decode the source codecs'],
      expiresAt: new Date(Date.now() + 300_000).toISOString(),
      durationMs: item.media?.durationMs,
      remote: false,
      transcodes4k: false,
      audioIndex: body.audioIndex ?? 0,
      burnedInSubtitle: body.burnSubtitle ? (body.subtitleIndex ?? null) : null,
      subtitles: (item.media?.subtitles ?? [])
        .filter((s) => s.kind === 'text')
        .map((s) => ({ index: s.index, kind: s.kind, language: s.language,
                       url: `/api/stream/${token}/subtitles/${s.index}.vtt` })),
    });
  }

  const stopMatch = /^\/api\/play\/([^/]+)$/.exec(p);
  if (req.method === 'DELETE' && stopMatch) {
    streams.delete(stopMatch[1]);
    res.writeHead(204).end();
    return;
  }
  if (req.method === 'GET' && p === '/api/play/active') {
    return json(res, 200, {
      streams: [...streams.entries()].map(([id, s]) => ({
        id, itemId: s.itemId, decision: 'remux', remote: false, transcodes4k: false,
        startedAt: new Date(s.startedAt).toISOString(),
        lastSeenAt: new Date().toISOString(),
      })),
    });
  }

  // ---- music ---------------------------------------------------------------------------------

  if (req.method === 'GET' && p === '/api/music/home') {
    return json(res, 200, { albums: fixtures.albums });
  }
  if (req.method === 'GET' && p === '/api/music/albums') {
    return json(res, 200, { albums: fixtures.albums.map(({ tracks, ...rest }) => rest) });
  }
  const albumMatch = /^\/api\/music\/albums\/(.+)$/.exec(p);
  if (req.method === 'GET' && albumMatch) {
    const album = fixtures.albums.find((a) => a.id === decodeURIComponent(albumMatch[1]));
    return album ? json(res, 200, album) : notFound(res);
  }
  if (req.method === 'POST' && p === '/api/music/play') {
    const body = await readJson(req);
    const track = fixtures.allTracks.find((t) => t.id === body.trackId);
    if (!track) return notFound(res);
    return json(res, 200, {
      url: `/api/music/audio?token=${randomUUID()}`,
      durationMs: track.durationMs,
    });
  }
  if (req.method === 'POST' && p === '/api/music/scrobble') {
    res.writeHead(204).end();
    return;
  }

  // ---- progress ------------------------------------------------------------------------------

  if (req.method === 'POST' && p === '/api/progress/events') {
    const body = await readJson(req);
    const batch = body.events ?? [];
    for (const event of batch) {
      events.push(event);
      const existing = progress.get(event.itemId) ?? {
        positionMs: 0, watched: false, playCount: 0, inProgress: false, hidden: false, run: 1,
      };
      progress.set(event.itemId, {
        ...existing,
        positionMs: event.positionMs,
        durationMs: event.durationMs ?? existing.durationMs,
        inProgress: event.kind !== 'stop',
        lastPlayedAt: new Date().toISOString(),
      });
    }
    return json(res, 200, {
      results: batch.map((e) => ({ id: e.id, status: 'accepted' })),
    });
  }

  const progressMatch = /^\/api\/progress\/items\/([^/]+)$/.exec(p);
  if (req.method === 'GET' && progressMatch) {
    const id = decodeURIComponent(progressMatch[1]);
    return json(res, 200, progress.get(id) ?? {
      positionMs: 0, watched: false, playCount: 0, inProgress: false, hidden: false, run: 1,
    });
  }

  const nextMatch = /^\/api\/progress\/items\/([^/]+)\/next$/.exec(p);
  if (req.method === 'GET' && nextMatch) {
    const id = decodeURIComponent(nextMatch[1]);
    const at = fixtures.episodes.findIndex((e) => e.id === id);
    const next = at >= 0 ? fixtures.episodes[at + 1] ?? null : null;
    return json(res, 200, { episode: next });
  }

  return notFound(res);
}

function approve(code) {
  const entry = pairings.get(code);
  if (!entry || entry.status !== 'pending') return;
  entry.status = 'approved';
  entry.deviceToken = randomUUID();
  sessions.set(entry.deviceToken, {
    userName: 'Mock Owner', profileName: 'Mock Profile', profileId: randomUUID(),
  });
}

// --- Connect ------------------------------------------------------------------------------------

function attachConnect(server) {
  const wss = new WebSocketServer({ server, path: '/api/connect' });
  wss.on('connection', (socket) => {
    let introduced = false;
    const timer = setTimeout(() => { if (!introduced) socket.close(4401, 'no hello'); }, 5000);

    socket.on('message', (raw) => {
      let message;
      try { message = JSON.parse(raw.toString()); } catch { return; }

      if (message.type === 'hello') {
        if (!sessions.has(message.token)) return socket.close(4401, 'signed out');
        introduced = true;
        clearTimeout(timer);
        connects.add(socket);
        socket.send(JSON.stringify({ type: 'welcome', sessionId: randomUUID() }));
        socket.send(JSON.stringify({ type: 'devices', devices: [] }));
        return;
      }
      if (!introduced) return socket.close(4401, 'hello first');

      // A state report from the device. Tests read these back through /mock/states.
      if (message.type === 'state') socket.lastState = message.state;
    });

    socket.on('close', () => { connects.delete(socket); clearTimeout(timer); });
  });

  // Lets a test play the phone: POST /mock/command {command}
  return (command) => {
    for (const socket of connects) socket.send(JSON.stringify({ type: 'command', command }));
    return connects.size;
  };
}

// --- start ----------------------------------------------------------------------------------------

export async function start({ port = PORT } = {}) {
  const hls = await buildHls();
  const audio = await buildAudio();

  const server = createServer((req, res) => {
    const url = new URL(req.url, `http://localhost:${port}`);
    if (req.method === 'POST' && url.pathname === '/mock/command') {
      return readJson(req)
        .then((body) => json(res, 200, { delivered: sendCommand(body.command) }))
        .catch(() => json(res, 400, { error: 'bad_json' }));
    }
    route(req, res, url).catch((error) => {
      if (!res.headersSent) json(res, 500, { error: 'mock_failed', detail: String(error.message) });
    });
  });

  const sendCommand = attachConnect(server);

  await new Promise((resolve) => server.listen(port, resolve));
  const actual = server.address().port;
  return {
    port: actual,
    url: `http://localhost:${actual}`,
    media: { hls, audio },
    close: () => new Promise((resolve) => { connects.forEach((s) => s.terminate()); server.close(resolve); }),
  };
}

const isMain = process.argv[1] && import.meta.url.endsWith(path.basename(process.argv[1]));
if (isMain) {
  const info = await start();
  console.log(`mock StreamBox listening on ${info.url}`);
  console.log(`  catalogue   ${info.url}/api/catalog/home   (needs a paired device token)`);
  console.log(`  pairing     POST ${info.url}/api/pair/start`);
  console.log(`  media       ${info.media.hls.ok ? 'real H.264/AAC HLS' : 'unavailable: ' + info.media.hls.reason}`);
  console.log(AUTO_APPROVE_MS > 0
    ? `  pairing approves itself after ${AUTO_APPROVE_MS} ms`
    : '  pairing waits for POST /api/pair/{code}/approve');
}
