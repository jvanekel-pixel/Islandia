// Generates the media the mock serves.
//
// ffmpeg makes a real, decodable H.264 + AAC HLS stream and a real AAC track, which matters:
// the media probe (work package V-14) needs something a headset can genuinely try to play, and a
// structurally valid but undecodable stub would prove nothing. Where ffmpeg is missing the mock
// still starts and still serves correctly shaped playlists, so contract tests pass — it just
// cannot be used to prove decoding.

import { execFile } from 'node:child_process';
import { mkdir, readdir, stat } from 'node:fs/promises';
import { promisify } from 'node:util';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const run = promisify(execFile);

/**
 * Where generated media is cached between runs: always beside this module, never relative to
 * whatever directory the caller happened to start in. Resolving against the working directory
 * meant the capture script, run from the repository root, scattered .media/ there instead.
 */
export const mediaDir = path.join(path.dirname(fileURLToPath(import.meta.url)), '.media');

async function hasFfmpeg() {
  try {
    await run('ffmpeg', ['-version']);
    return true;
  } catch {
    return false;
  }
}

async function alreadyBuilt(file) {
  try {
    return (await stat(file)).size > 0;
  } catch {
    return false;
  }
}

/**
 * A short HLS stream: colour bars with a burned-in timer, plus a quiet tone, cut into segments.
 * H.264 baseline and stereo AAC — the floor every Quest decodes in hardware, and exactly what
 * the Islandia capability profile in the contract asks for.
 */
export async function buildHls({ seconds = 12, segmentSeconds = 4 } = {}) {
  await mkdir(mediaDir, { recursive: true });
  const master = path.join(mediaDir, 'master.m3u8');
  if (await alreadyBuilt(master)) return { ok: true, cached: true };
  if (!(await hasFfmpeg())) return { ok: false, reason: 'ffmpeg is not installed' };

  await run('ffmpeg', [
    '-y', '-hide_banner', '-loglevel', 'error',
    '-f', 'lavfi', '-i', `testsrc=size=1280x720:rate=30:duration=${seconds}`,
    '-f', 'lavfi', '-i', `sine=frequency=440:duration=${seconds}`,
    '-c:v', 'libx264', '-profile:v', 'baseline', '-level', '3.1', '-pix_fmt', 'yuv420p',
    '-g', String(30 * segmentSeconds), '-keyint_min', String(30 * segmentSeconds),
    '-c:a', 'aac', '-ac', '2', '-b:a', '128k',
    '-f', 'hls',
    '-hls_time', String(segmentSeconds),
    '-hls_playlist_type', 'vod',
    '-hls_segment_filename', path.join(mediaDir, 'segment%d.ts'),
    path.join(mediaDir, 'main.m3u8'),
  ]);

  // StreamBox hands the client a master playlist, so the mock does too rather than exposing
  // ffmpeg's single media playlist directly.
  const { writeFile } = await import('node:fs/promises');
  await writeFile(master,
    '#EXTM3U\n' +
    '#EXT-X-VERSION:3\n' +
    '#EXT-X-STREAM-INF:BANDWIDTH=2500000,RESOLUTION=1280x720,CODECS="avc1.42e01e,mp4a.40.2"\n' +
    'main.m3u8\n', 'utf8');

  return { ok: true, cached: false };
}

/** A short AAC track, for the music endpoints. */
export async function buildAudio({ seconds = 10 } = {}) {
  await mkdir(mediaDir, { recursive: true });
  const track = path.join(mediaDir, 'track.m4a');
  if (await alreadyBuilt(track)) return { ok: true, cached: true };
  if (!(await hasFfmpeg())) return { ok: false, reason: 'ffmpeg is not installed' };

  await run('ffmpeg', [
    '-y', '-hide_banner', '-loglevel', 'error',
    '-f', 'lavfi', '-i', `sine=frequency=330:duration=${seconds}`,
    '-c:a', 'aac', '-ac', '2', '-b:a', '160k',
    track,
  ]);
  return { ok: true, cached: false };
}

export async function builtFiles() {
  try {
    return await readdir(mediaDir);
  } catch {
    return [];
  }
}
