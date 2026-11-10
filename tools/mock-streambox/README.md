# Mock StreamBox

A fake StreamBox gateway. It implements what `contract/streambox/v1.json` describes — device
pairing, a small catalogue, the stream broker, music, progress and the Connect socket — and holds
nothing real.

It exists so the Islandia client can be built and tested now, against something that behaves like
the owner's server, without needing the owner's server.

## Run it

```
cd tools/mock-streambox
npm install
npm start
```

Then pair a client against `http://localhost:8787`. Pairing approves itself after 1.5 seconds so
nothing has to play the part of the phone; pass `--auto-approve-ms 0` to require an explicit
`POST /api/pair/{code}/approve` instead.

## What is real and what is not

**Real:** the HTTP and WebSocket shapes, the pairing handshake, bearer authentication, paging,
the remux-versus-transcode decision, signed audio addresses with range support, and — where
`ffmpeg` is installed — a genuinely decodable H.264 + AAC HLS stream and AAC track, generated on
first run and cached in `.media/`.

**Not real:** the library. Every title is invented. Nothing from the owner's library is in this
repository and the privacy check in `tools/lint` keeps it that way.

Without `ffmpeg` the mock still starts and still serves correctly shaped playlists, so the
contract tests pass — it just cannot be used to prove that anything decodes. Proving that needs a
real headset, which is work package `V-14`.

## Endpoints the mock adds for testing

Not part of the contract, and never authenticated, because a test needs them before it has a token.

| Path | Purpose |
| :-- | :-- |
| `GET /mock/health` | Is it up, and how many sessions and streams does it hold |
| `GET /mock/events` | Every progress event it has received |
| `POST /mock/command` | Play the phone: send a Connect command to every attached device |
| `POST /api/pair/{code}/approve` and `/deny` | Play the phone approving a pairing |

## Tests

```
npm test
```

30 tests, about six seconds. They check the mock against the contract file rather than against
itself — otherwise it could drift into agreeing with its own mistakes.
