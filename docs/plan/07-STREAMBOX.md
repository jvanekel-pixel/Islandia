# 07 — StreamBox in-world

The owner runs their own private media service, **StreamBox**: films, series, music and
audiobooks, behind a single gateway. It already casts. The requirement here is stronger:
**Islandia is a native StreamBox client.** You pair the headset once, then browse and play your
library on screens and speakers you have placed on the island.

This file is the integration specification. It was written from a read of the StreamBox repository
at commit `9ebfdd6` (2026-11-10). StreamBox is under active development; treat §3 as a snapshot to
be re-checked, not a guarantee.

## 1. Ground rules

- **StreamBox is a separate project with its own process.** No session working in this repository
  pushes to it, opens pull requests on it, or edits it. If something would be better changed on
  the StreamBox side, write it down as a `human-gate` issue here; the owner takes it into that
  project's own phase process.
- **This repository is public.** It never contains the server's address, any token, account name,
  library content, or anything from StreamBox beyond the client contract in §3. A scan enforces
  this (`SB-01a`).
- **The gateway is the only authority on access.** Islandia shows what the gateway returns and
  nothing else. It adds no filter of its own and removes none. It caches nothing across sign-out.
- **No secrets in URLs or logs** beyond the short-lived stream and audio tokens the gateway itself
  issues for that purpose. The device token travels only in the `Authorization` header and in the
  Connect hello message.
- **Zero running cost** on both sides. Nothing here may add a paid or metered service.
- **Legal position unchanged:** Islandia plays what the owner's server serves. No DRM handling.

## 2. Why this is feasible

Three properties of StreamBox's design make a headset client straightforward:

1. **Device pairing already exists** for TVs, modelled on the OAuth device flow. A screen shows a
   code and a QR code; a signed-in phone approves; the device collects a bearer token once.
2. **Playback is brokered.** The client says what it can decode; the gateway returns one
   short-lived HLS address under `/api/stream/<token>/…`. The token in the path is the credential,
   so the media player needs no cookies or headers. The gateway offers a lowest-common-denominator
   profile — H.264 video, stereo AAC, 1080p, SDR — which a Quest 2 decodes in hardware.
3. **Remote control already exists.** Every app holds one WebSocket to the gateway ("Connect");
   TVs register as places to play and phones drive them.

On present evidence **no StreamBox change is needed for a first version.**

## 3. The client contract Islandia uses

All paths are under the gateway's `/api`. All authenticated calls carry
`Authorization: Bearer <device token>`.

### 3.1 Pairing

| Step | Call | Notes |
| :-- | :-- | :-- |
| Start | `POST /pair/start` | Public, rate-limited. Returns `code`, `pollSecret`, `expiresAt`, `approveUrl` (put this in the QR code), `intervalSeconds` |
| Wait | `POST /pair/poll` with the poll secret | `pending` · `denied` · `expired` · `approved` with `deviceToken`, `userName`, `profileName`. The token is handed over **exactly once** |
| Approve | Done on the owner's phone, in the StreamBox app | If the phone is on a different network from the headset the phone must type the code shown in the headset |

The device supplies a name when pairing; the phone shows it before approving. Default
*"Islandia — Quest"*, editable.

The resulting session is a **device session**: revocable from StreamBox like any other. A 401, or a
Connect close with the signed-out code, means the token is gone — return to the pairing screen.

### 3.2 Browsing

| Purpose | Calls |
| :-- | :-- |
| Home | `GET /catalog/home`, `/catalog/latest`, `/catalog/next-up`, `/catalog/resume` |
| Libraries | `GET /catalog/libraries/:id/items`, `/catalog/libraries/:id/facets` |
| Collections | `GET /catalog/collections`, `/catalog/collections/:id` |
| Search | `GET /catalog/search` |
| Continue watching | `GET /progress/continue-watching`, `/progress/up-next` |
| Artwork | The image addresses the gateway returns. Never an engine address |

### 3.3 Playing video

`POST /play` with:

- `itemId`
- `capabilities` — name, video codecs, audio codecs, HDR flag, maximum width and height, maximum
  audio channels, segment container (`mp4` or `ts`)
- optional `audioIndex`, `subtitleIndex`, `startMs`, `maxBitrate`, `burnSubtitle`

Returns `streamId`, `url` (the HLS master playlist), `decision` (`remux` or `transcode`),
`expiresAt`, `durationMs`, the audio track in use, the burned-in subtitle if any, and a list of
subtitle tracks (text tracks as WebVTT under the same token; image tracks can only be burned in).

Islandia's capability profile, to be confirmed by the media probe (`V-14`):

| Field | Value |
| :-- | :-- |
| name | `islandia` |
| video | `h264` (add `hevc` only after a device test proves it) |
| audio | `aac` |
| hdr | false |
| maximum size | 1920 × 1080 |
| audio channels | 2 |
| segment container | whichever the chosen player backend handles best — decided by the probe |

Other calls: `DELETE /play/:token` to stop; `GET /play/active`.

**Idle streams are swept after about five minutes.** After a pause longer than four minutes,
request a fresh stream at the saved position rather than resuming the old one.

### 3.4 Music

`GET /music/home`, `/music/artists[/:id]`, `/music/albums[/:id]`, `/music/genres`,
`/music/search`, `/music/shuffle`, `/music/favorites` (and `PUT`/`DELETE` by id),
`/music/playlists[/:id]`.

`POST /music/play` returns a **signed audio address** that needs no header and supports range
requests. `POST /music/scrobble` reports plays.

Audiobooks follow the same pattern under `/books/…`. They are not in the first version.

### 3.5 Progress

`POST /progress/events` reports playback; `GET /progress/items/:id` reads the saved position;
`GET /progress/items/:id/next` and `/progress/series/:id/next` find what follows;
`GET`/`PUT /progress/items/:id/tracks` remember audio and subtitle choices.

StreamBox's progress rules are covered by property tests on its side. Islandia reports events and
does not re-implement the rules.

### 3.6 Connect

`GET /connect` (WebSocket). First message: `{ type: "hello", role: "tv", token: <device token> }`.

- The server replies `welcome`, then sends `command` messages: `play` (item, optional position and
  list), `pause`, `resume`, `seek`, `seekBy`, `stop`, `next`, `audio`, `subtitle`, `volume`, `mute`.
- The device sends `state` on every change and every few seconds while playing: item, position,
  duration, playing, buffering, track lists and selections, volume, mute, has-next.
- Close codes 4401 (signed out) and 4409 (replaced by a newer socket) must **not** be retried.
- The owner has directed that playback become one-at-a-time per person with the newest device
  winning. Islandia must stop cleanly when told to, and must not fight for the stream.

## 4. The one real technical risk: decoding on the headset

Everything in §3 is ordinary HTTP and is fully testable against a mock. What cannot be proven
without a headset is **whether the chosen player decodes an HLS stream to a texture on a Quest 2
under Vulkan, smoothly.**

The media probe (`V-14`) settles it at the first headset check, using a test stream the app serves
to itself, so it needs no server. Candidates, in order of preference:

| # | Backend | For | Against |
| :-- | :-- | :-- | :-- |
| 1 | **Unity's built-in video player, HLS address** | No extra code; works with Vulkan; free | HLS support on Android must be proven; no control over adaptive bitrate; track switching limited |
| 2 | **Native player (AndroidX Media3 / ExoPlayer) as a small plugin** | Robust HLS, track selection, buffering control; free and open source; the same engine StreamBox's planned Android apps would use | Getting frames into a Vulkan Unity texture is the hard part; more build complexity |
| 3 | **Progressive fallback** | Unity's player certainly plays a progressive MP4 over HTTP | Needs one new gateway route on the StreamBox side; seeking means requesting a new stream at a position |

If 1 works, use it. If not, 2. Option 3 exists so that a failure of both is a small server-side
request to the owner, not the end of the feature.

Subtitles start as **burned in by the server** (`burnSubtitle`), which works with every backend.
Rendering WebVTT text in the world is a later improvement.

## 5. What it looks like in the world

- **Screens.** A TV, a projector with its screen, later a drive-in. Walk up, point, browse, play.
  On a Quest 2 one screen plays at a time.
- **Speakers and radios.** Play the music library through the island's speakers, spatialised.
  Several speakers play the same thing in sync.
- **The phone is a remote.** Islandia appears in the StreamBox app as a place to play.
- **Continuity.** Start a film on the sofa, finish it on the beach. Progress is the server's.
- **Away from home.** The gateway is reachable from the internet, so this works wherever the
  headset has a connection; the gateway's bitrate governor applies.

## 6. Testing without the server

`SB-01a` builds a **mock gateway** from the contract snapshot: pairing, a fixture catalogue with
generated posters, a generated HLS test stream, signed audio, progress and Connect. Every
`[L1]`–`[L3]` criterion in the StreamBox packages runs against it. The mock lives in this
repository and contains nothing from the real library.

The real server is exercised only on the owner's headset, at **HG-2**.

## 7. Keeping up with StreamBox

StreamBox will change. Defences:

- The client tolerates unknown fields and unknown enum values.
- The snapshot records the StreamBox commit it came from. Re-syncing it is a small, explicit task
  the owner triggers; a later improvement is for StreamBox to publish the snapshot itself.
- The Alpha device pass re-runs the whole media journey against the live server.

## 8. Later, not now

Audiobooks · text subtitles · watch-together with friends as avatars · the voice assistant ·
4K or HDR (not on this hardware) · offline downloads.

## 9. Answered by the owner, 2026-11-11

The owner asked for whatever is safest and smartest. Recorded in `DECISIONS.md`, 2026-11-11.

1. **Public description of the contract — kept public.** §3 holds no secret and no address, and
   it is already in the repository's history, so taking it out now would hide nothing. The rules
   that matter stay enforced on every change: no server address, token, account detail or library
   content is ever committed.
2. **Which profile the headset uses — a dedicated one.** The headset pairs to its own StreamBox
   profile, not the owner's. It can be signed out or revoked on its own without touching anyone
   else's devices, and it keeps its own watch history.
3. **Restricted libraries — not reachable.** The dedicated profile has no restricted libraries.
   Islandia shows whatever the gateway returns for the paired profile, so nothing in Islandia
   changes; the pairing instructions at HG-2 say which profile to choose.
