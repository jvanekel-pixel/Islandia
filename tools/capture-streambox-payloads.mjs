// Captures real responses from the mock gateway as test fixtures.
//
// The mapping tests in Island.Core run against these rather than against payloads written by
// hand. Hand-written fixtures record what the author believed the server sends, which is exactly
// the belief the test is supposed to be checking.
//
//   node tools/capture-streambox-payloads.mjs
//
// Re-run it whenever the mock changes, and commit the result.

import { writeFile, mkdir } from 'node:fs/promises';
import path from 'node:path';
import { start } from './mock-streambox/server.js';

const out = path.join('Packages', 'com.island.core', 'Tests', 'StreamBox', 'payloads');
await mkdir(out, { recursive: true });

const mock = await start({ port: 0 });
const save = async (name, body) => {
  await writeFile(path.join(out, name + '.json'), JSON.stringify(body, null, 2) + '\n', 'utf8');
  console.log('captured', name);
};

try {
  // The pairing walk, exactly as the client will do it.
  const started = await (await fetch(mock.url + '/api/pair/start', { method: 'POST' })).json();
  await save('pair-start', started);

  const pollBody = { pollSecret: started.pollSecret };
  const asJson = (r) => r.json();
  const post = (p, body, token) => fetch(mock.url + p, {
    method: 'POST',
    headers: { 'content-type': 'application/json', ...(token ? { authorization: `Bearer ${token}` } : {}) },
    body: JSON.stringify(body ?? {}),
  }).then(asJson);

  await save('pair-poll-pending', await post('/api/pair/poll', pollBody));
  let approved;
  for (let i = 0; i < 30 && !approved; i++) {
    await new Promise((r) => setTimeout(r, 200));
    const polled = await post('/api/pair/poll', pollBody);
    if (polled.status === 'approved') approved = polled;
  }
  await save('pair-poll-approved', approved);
  const token = approved.deviceToken;

  const get = (p) => fetch(mock.url + p, { headers: { authorization: `Bearer ${token}` } }).then(asJson);

  await save('catalog-home', await get('/api/catalog/home'));
  await save('catalog-latest', await get('/api/catalog/latest'));

  const home = await get('/api/catalog/home');
  const library = home.libraries[0];
  await save('catalog-library-page', await get(`/api/catalog/libraries/${library.id}/items?limit=2`));

  const capabilities = {
    name: 'islandia', video: ['h264'], audio: ['aac'], hdr: false,
    maxWidth: 1920, maxHeight: 1080, maxAudioChannels: 2, segmentContainer: 'ts',
  };
  await save('play-remux', await post('/api/play', { itemId: 'mk:film-1', capabilities }, token));
  await save('play-transcode', await post('/api/play',
    { itemId: 'mk:film-1', capabilities: { ...capabilities, video: ['vp9'], audio: ['opus'] } }, token));

  await save('music-home', await get('/api/music/home'));
  await save('music-album', await get('/api/music/albums/mk:album-1'));
  await save('music-play', await post('/api/music/play', { trackId: 'mk:track-1' }, token));

  await post('/api/progress/events', {
    sentAt: new Date().toISOString(),
    events: [{
      id: crypto.randomUUID(), kind: 'heartbeat', itemId: 'mk:episode-1',
      profileId: crypto.randomUUID(), playbackId: crypto.randomUUID(),
      playbackStartedAt: new Date().toISOString(), clientTs: new Date().toISOString(),
      positionMs: 65000, durationMs: 1440000,
    }],
  }, token);
  await save('progress-state', await get('/api/progress/items/mk:episode-1'));
  await save('progress-next-episode', await get('/api/progress/items/mk:episode-1/next'));

  // One Connect exchange: the welcome, and a command from the phone.
  const socket = new WebSocket(mock.url.replace('http', 'ws') + '/api/connect');
  const messages = [];
  await new Promise((resolve, reject) => {
    socket.addEventListener('open', () =>
      socket.send(JSON.stringify({ type: 'hello', role: 'tv', token })));
    socket.addEventListener('message', async (event) => {
      messages.push(JSON.parse(event.data));
      if (messages.length === 2) {
        await fetch(mock.url + '/mock/command', {
          method: 'POST', headers: { 'content-type': 'application/json' },
          body: JSON.stringify({ command: { type: 'seek', positionMs: 42000 } }),
        });
      }
      if (messages.some((m) => m.type === 'command')) resolve();
    });
    socket.addEventListener('error', reject);
    setTimeout(() => reject(new Error('Connect capture timed out')), 5000);
  });
  socket.close();
  await save('connect-welcome', messages.find((m) => m.type === 'welcome'));
  await save('connect-command-seek', messages.find((m) => m.type === 'command'));
} finally {
  await mock.close();
}

console.log('\nCaptured into', out);
