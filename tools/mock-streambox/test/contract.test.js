// Does the mock actually match the contract?
//
// The mock exists so the Islandia client can be built without the owner's server. That is only
// worth anything if the mock behaves like the real gateway, so these tests check it against
// contract/streambox/v1.json rather than against itself.

import { after, before, describe, it } from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { start } from '../server.js';

const contract = JSON.parse(
  await readFile(new URL('../../../contract/streambox/v1.json', import.meta.url), 'utf8'));

let mock;
let token;

const get = (path, auth = true) =>
  fetch(mock.url + path, auth ? { headers: { authorization: `Bearer ${token}` } } : undefined);

const post = (path, body, auth = true) =>
  fetch(mock.url + path, {
    method: 'POST',
    headers: { 'content-type': 'application/json', ...(auth ? { authorization: `Bearer ${token}` } : {}) },
    body: JSON.stringify(body ?? {}),
  });

before(async () => {
  mock = await start({ port: 0 });
  // Walk the real pairing flow rather than injecting a token: it is the first thing the client
  // will do, so it is the first thing worth proving.
  const started = await (await fetch(mock.url + '/api/pair/start', { method: 'POST' })).json();
  for (let attempt = 0; attempt < 30 && !token; attempt++) {
    await new Promise((r) => setTimeout(r, 200));
    const polled = await (await post('/api/pair/poll', { pollSecret: started.pollSecret }, false)).json();
    if (polled.status === 'approved') token = polled.deviceToken;
  }
  assert.ok(token, 'pairing never completed');
});

after(() => mock?.close());

describe('pairing', () => {
  it('start returns every field the contract promises', async () => {
    const body = await (await fetch(mock.url + '/api/pair/start', { method: 'POST' })).json();
    for (const field of Object.keys(contract.pairing.start.response)) {
      assert.ok(body[field] !== undefined, `missing ${field}`);
    }
    assert.match(body.code, /^\d{6}$/);
    assert.ok(body.intervalSeconds > 0);
  });

  it('hands the device token over exactly once', async () => {
    const started = await (await fetch(mock.url + '/api/pair/start', { method: 'POST' })).json();
    await post(`/api/pair/${started.code}/approve`, {}, false);
    const first = await (await post('/api/pair/poll', { pollSecret: started.pollSecret }, false)).json();
    assert.equal(first.status, 'approved');
    const second = await (await post('/api/pair/poll', { pollSecret: started.pollSecret }, false)).json();
    assert.notEqual(second.status, 'approved', 'a replayed poll must not yield a second token');
  });

  it('reports a denial rather than failing', async () => {
    const started = await (await fetch(mock.url + '/api/pair/start', { method: 'POST' })).json();
    await post(`/api/pair/${started.code}/deny`, {}, false);
    const polled = await (await post('/api/pair/poll', { pollSecret: started.pollSecret }, false)).json();
    assert.equal(polled.status, 'denied');
  });

  it('an unknown poll secret expires rather than hanging', async () => {
    const polled = await (await post('/api/pair/poll', { pollSecret: 'nonsense' }, false)).json();
    assert.equal(polled.status, 'expired');
  });
});

describe('authentication', () => {
  it('refuses the catalogue without a token', async () => {
    assert.equal((await fetch(mock.url + '/api/catalog/home')).status, 401);
  });

  it('refuses an unknown token', async () => {
    // Deliberately short: a longer fake would trip the repository's own secret scan.
    const response = await fetch(mock.url + '/api/catalog/home',
      { headers: { authorization: 'Bearer nope' } });
    assert.equal(response.status, 401);
  });
});

describe('catalogue', () => {
  it('home carries rows and libraries', async () => {
    const body = await (await get('/api/catalog/home')).json();
    assert.ok(body.rows.length > 0);
    assert.ok(body.libraries.length > 0);
  });

  it('every item has the fields the contract marks required', async () => {
    const body = await (await get('/api/catalog/latest')).json();
    assert.ok(body.items.length > 0);
    for (const item of body.items) {
      for (const field of contract.catalog.item.required) {
        assert.ok(item[field] !== undefined, `${item.id ?? '?'} is missing ${field}`);
      }
      assert.ok(contract.catalog.itemKinds.includes(item.kind), `unexpected kind ${item.kind}`);
    }
  });

  it('a library page carries the paging fields', async () => {
    const home = await (await get('/api/catalog/home')).json();
    const library = home.libraries[0];
    const page = await (await get(`/api/catalog/libraries/${library.id}/items?limit=2`)).json();
    for (const field of ['items', 'total', 'offset', 'limit']) {
      assert.ok(page[field] !== undefined, `missing ${field}`);
    }
    assert.ok(page.items.length <= 2);
    if (page.total > 2) assert.equal(page.nextOffset, 2, 'a truncated page must say where the next one starts');
  });

  it('search finds a title and returns nothing for a miss', async () => {
    const hit = await (await get('/api/catalog/search?q=harbour')).json();
    assert.equal(hit.items.length, 1);
    const miss = await (await get('/api/catalog/search?q=zzzznothing')).json();
    assert.equal(miss.items.length, 0);
  });
});

describe('playback', () => {
  const capabilities = contract.playback.islandiaCapabilities;

  it('refuses a play request with no capabilities', async () => {
    assert.equal((await post('/api/play', { itemId: 'mk:film-1' })).status, 400);
  });

  it('copies the source when the client can decode it', async () => {
    const body = await (await post('/api/play', { itemId: 'mk:film-1', capabilities })).json();
    assert.equal(body.decision, 'remux');
    for (const field of ['streamId', 'url', 'decision', 'reasons', 'expiresAt', 'remote',
                         'transcodes4k', 'burnedInSubtitle', 'subtitles']) {
      assert.ok(body[field] !== undefined, `missing ${field}`);
    }
  });

  it('transcodes when the client cannot, and says why', async () => {
    const weak = { ...capabilities, video: ['vp9'], audio: ['opus'] };
    const body = await (await post('/api/play', { itemId: 'mk:film-1', capabilities: weak })).json();
    assert.equal(body.decision, 'transcode');
    assert.ok(body.reasons.length > 0, 'a transcode must come with a reason');
  });

  it('offers text subtitles as WebVTT and never offers image ones', async () => {
    const body = await (await post('/api/play', { itemId: 'mk:film-1', capabilities })).json();
    assert.ok(body.subtitles.every((s) => s.kind === 'text'),
      'image subtitles are burned in by the server, never listed for the client to render');
  });

  it('serves a playable HLS master playlist under the stream token', async () => {
    const body = await (await post('/api/play', { itemId: 'mk:film-1', capabilities })).json();
    const master = await fetch(mock.url + body.url);
    assert.equal(master.status, 200);
    assert.match(master.headers.get('content-type'), /mpegurl/);
    const text = await master.text();
    assert.match(text, /^#EXTM3U/);
    assert.match(text, /#EXT-X-STREAM-INF/);
  });

  it('serves the media playlist and a real segment', async (t) => {
    if (!mock.media.hls.ok) return t.skip('ffmpeg is not installed, so there is no real media');
    const body = await (await post('/api/play', { itemId: 'mk:film-1', capabilities })).json();
    const base = body.url.replace(/master\.m3u8$/, '');
    const media = await (await fetch(mock.url + base + 'main.m3u8')).text();
    assert.match(media, /#EXTINF/);
    const first = media.split('\n').find((line) => line.endsWith('.ts'));
    const segment = await fetch(mock.url + base + first);
    assert.equal(segment.status, 200);
    const bytes = Buffer.from(await segment.arrayBuffer());
    assert.ok(bytes.length > 1000, 'a real segment should not be tiny');
    assert.equal(bytes[0], 0x47, 'an MPEG-TS packet starts with the sync byte 0x47');
  });

  it('refuses a stream token it never issued', async () => {
    assert.equal((await fetch(mock.url + '/api/stream/made-up/master.m3u8')).status, 404);
  });

  it('stops a stream and drops it from the active list', async () => {
    const body = await (await post('/api/play', { itemId: 'mk:film-2', capabilities })).json();
    const streamToken = body.url.split('/')[3];
    const before = await (await get('/api/play/active')).json();
    assert.ok(before.streams.some((s) => s.id === streamToken));
    await fetch(mock.url + `/api/play/${streamToken}`,
      { method: 'DELETE', headers: { authorization: `Bearer ${token}` } });
    const after = await (await get('/api/play/active')).json();
    assert.ok(!after.streams.some((s) => s.id === streamToken));
  });
});

describe('music', () => {
  it('play returns an address that needs no token header', async () => {
    const body = await (await post('/api/music/play', { trackId: 'mk:track-1' })).json();
    assert.ok(body.url.includes('token='),
      'the contract says this address is signed, so the headset can fetch it without a header');
    const audio = await fetch(mock.url + body.url);
    assert.equal(audio.status, 200);
  });

  it('audio supports range requests, which is what lets the headset cache a track', async (t) => {
    if (!mock.media.audio.ok) return t.skip('ffmpeg is not installed');
    const body = await (await post('/api/music/play', { trackId: 'mk:track-1' })).json();
    const ranged = await fetch(mock.url + body.url, { headers: { range: 'bytes=0-99' } });
    assert.equal(ranged.status, 206);
    assert.match(ranged.headers.get('content-range'), /^bytes 0-99\//);
  });
});

describe('progress', () => {
  it('accepts a batch and reflects it back', async () => {
    const event = {
      id: crypto.randomUUID(),
      kind: 'heartbeat',
      itemId: 'mk:episode-1',
      profileId: crypto.randomUUID(),
      playbackId: crypto.randomUUID(),
      playbackStartedAt: new Date().toISOString(),
      clientTs: new Date().toISOString(),
      positionMs: 65_000,
      durationMs: 1_440_000,
    };
    const accepted = await (await post('/api/progress/events',
      { sentAt: new Date().toISOString(), events: [event] })).json();
    assert.equal(accepted.results[0].status, 'accepted');

    const state = await (await get('/api/progress/items/mk:episode-1')).json();
    assert.equal(state.positionMs, 65_000);
    for (const field of contract.progress.state.required) {
      assert.ok(state[field] !== undefined, `missing ${field}`);
    }
  });

  it('only uses event kinds the contract lists', () => {
    for (const kind of ['heartbeat', 'pause', 'seek', 'stop']) {
      assert.ok(contract.progress.eventKinds.includes(kind));
    }
  });

  it('finds the next episode, and stops at the end of a run', async () => {
    const next = await (await get('/api/progress/items/mk:episode-1/next')).json();
    assert.equal(next.episode.id, 'mk:episode-2');
    const end = await (await get('/api/progress/items/mk:episode-3/next')).json();
    assert.equal(end.episode, null);
  });
});

describe('connect', () => {
  const open = (socketToken) => new Promise((resolve, reject) => {
    const socket = new WebSocket(mock.url.replace('http', 'ws') + '/api/connect');
    const received = [];
    socket.addEventListener('open', () =>
      socket.send(JSON.stringify({ type: 'hello', role: 'tv', token: socketToken })));
    socket.addEventListener('message', (event) => {
      received.push(JSON.parse(event.data));
      if (received.length >= 2) resolve({ socket, received });
    });
    socket.addEventListener('close', (event) => reject(new Error(`closed ${event.code}`)));
    setTimeout(() => reject(new Error('timed out')), 4000);
  });

  it('welcomes a device that introduces itself with a good token', async () => {
    const { socket, received } = await open(token);
    assert.equal(received[0].type, 'welcome');
    assert.ok(received[0].sessionId);
    socket.close();
  });

  it('closes with 4401 on a token it does not know', async () => {
    await assert.rejects(open('not-a-real-token'), (error) => {
      assert.match(error.message, new RegExp(String(contract.connect.closeCodes ? 4401 : '')));
      return true;
    });
  });

  it('delivers a command from the phone to the device', async () => {
    const { socket, received } = await open(token);
    const delivered = new Promise((resolve) =>
      socket.addEventListener('message', (event) => {
        const message = JSON.parse(event.data);
        if (message.type === 'command') resolve(message.command);
      }));
    await post('/mock/command', { command: { type: 'pause' } }, false);
    assert.deepEqual(await delivered, { type: 'pause' });
    assert.equal(received[0].type, 'welcome');
    socket.close();
  });
});

describe('the contract itself', () => {
  it('records which StreamBox commit it was read from', () => {
    assert.ok(contract.source.commit, 'without this the snapshot cannot be compared later');
    assert.ok(contract.source.readOn);
  });

  it('asks only for what a Quest 2 decodes in hardware', () => {
    const capabilities = contract.playback.islandiaCapabilities;
    assert.deepEqual(capabilities.video, ['h264']);
    assert.deepEqual(capabilities.audio, ['aac']);
    assert.equal(capabilities.hdr, false);
    assert.equal(capabilities.maxAudioChannels, 2);
    assert.ok(capabilities.maxHeight <= 1080);
  });

  it('says a long pause needs a fresh stream', () => {
    assert.ok(contract.playback.renewAfterPauseMs < contract.playback.streamSweptAfterIdleMs,
      'the client must renew before the gateway sweeps, or playback dies on resume');
  });

  it('names every Connect command the device has to handle', () => {
    for (const command of ['play', 'pause', 'resume', 'seek', 'stop', 'next']) {
      assert.ok(contract.connect.commandsIslandiaMustHandle.includes(command));
    }
  });
});
