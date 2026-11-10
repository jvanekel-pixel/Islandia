# Audit detail — blueprint Phase 6 (browser companion, cloud worker, save schema, tier detection)

Source: `docs/blueprint/phase-06.md`; drafts under `reference/drafts/CompanionCloud/`,
`reference/drafts/CompanionWeb/`, `reference/drafts/Assets/Scripts/Core/IslandRemediationCore.cs`,
`reference/drafts/Assets/Scripts/Cloud/CompanionPairingAndQuotaClient.cs`.
Findings are from reading only; nothing was executed. Confidence is high unless a percentage is given.
Line numbers refer to the original single-file document, so treat them as approximate pointers.

## Verdict

An unexecuted sketch. Its two headline claims — a sandboxed converter and a hard texture cap — are
not implemented, and the end-to-end path is not connected: **nothing on the headset ever receives a
model.** The work packages `I-*` in the backlog replace this design rather than port it.

| Draft | Expected to survive a first run |
| :-- | :-- |
| `IslandRemediationCore.cs` | ~85% compiles (lighting fine); tier logic ~60%; save schema needs a rewrite |
| `CompanionCloud/package.json` | ~60% |
| `blender_safe_convert.py` | fails on every invocation; ~50% of lines reusable |
| `server.js` | boots; ~45% functionally, ~30% after security restructuring |
| `CompanionScalePortal.jsx` | renders; ~65% |
| `CompanionPairingAndQuotaClient.cs` | ~80% of lines, but only ~30% of the client that is needed |

## The data flow the code actually implements

1. Headset has a method to request a pairing code. Nothing calls it and nothing displays the code.
2. Server keeps `code → session` in a process-local `Map`.
3. Browser "verifies" the code and sets a client-side flag only.
4. Browser uploads the file. **The pairing code is not checked before conversion.**
5. Server converts and writes the result to a public, unauthenticated `/assets` directory, forever.
6. Only then is the code looked up, and metadata is sent over a WebSocket — but there is no
   WebSocket client, poller or download trigger anywhere in the Unity code.
7. The on-device loader resolves relative URLs against a hard-coded `http://localhost:8080`, which
   on a headset is the headset.
8. The "cumulative memory quota" is an in-memory counter of `max(2 × file size, 2.2 × file size)`.
   It is not persisted and bears no relation to decoded texture memory.

## Security

No authentication or authorisation exists. No accounts, tokens or device identity.

Real vulnerabilities in the draft:

1. **Open conversion service.** Anyone can POST a file with no code and get Blender compute plus a
   public URL.
2. **Enumerable pairing codes.** ~900k values, no rate limit on verify. An attacker can list live
   codes, push models into a stranger's session, and hijack the WebSocket slot.
3. TTL is not enforced at upload; expired sessions are never swept.
4. Unauthenticated, unlimited session registration; unvalidated payload stored per session; the
   code generator loops forever once the code space is full.
5. Probable single-request process kill via an unparseable WebSocket request target (~80%).
6. **Fail-open texture gate.** Any failure of the KTX step is swallowed and the uncapped file ships.
   Given the CLI flag errors below this branch is expected to be taken every time.
7. Local file disclosure through Blender-resolved external image paths (medium confidence).
8. Possible file disclosure through GLB external URIs resolved by `NodeIO` (~60%; test it).
9. Resource exhaustion: no queue or concurrency cap, no memory/CPU limit on Blender,
   `export_apply=True` applies attacker-supplied modifiers, heavy work on the event loop.
10. Rate limiter is in-memory, upload-only, and collapses to one shared bucket behind a proxy.
11. Error messages returned to clients include command lines and absolute paths.
12. Outputs never deleted, public, named by a truncated hash of the pre-compression file; the
    headset cannot verify what it downloads; same model at two texture caps overwrites itself.
13. Wildcard CORS, no WebSocket origin check, no TLS, `localhost` defaults in all three clients.
14. `multer` 1.x has published denial-of-service CVEs.

Not vulnerable as written: command injection (argv array), upload path traversal, temp cleanup.

Controls the prose promises that the code does not contain:

- The "network-isolated sandbox" is a comment. No Dockerfile, user separation, seccomp, cgroups.
- `--python-autoexec` is not a Blender flag; the real one is `--disable-autoexec`. More importantly
  auto-exec is not the main threat: Blender's importers are memory-unsafe and Blender's own position
  is that untrusted `.blend` files are not safe to open. Only OS-level isolation addresses this.
- "Magic-byte validation" is a two-entry denylist of formats the server never executes.
- "≤ 25,000 triangle cap" is a client-settable ratio with no output check.
- WebP stripping, LOD chain generation, per-class texture caps, UASTC: none implemented.
- Multi-file uploads (`.obj` + `.mtl` + textures, `.gltf` + `.bin`) cannot work at all.

## Bugs that break the non-GLB path on every run

- Uploaded files are stored without an extension, so the Blender script cannot pick an importer.
- `export_Lights=False` is a `TypeError` (`export_lights`).
- `.gltf` is routed to Blender, which has no branch for it.
- Scaling is applied per object about its own origin, so multi-object models fly apart.
- On the GLB path the pivot is computed before the scale is applied.

## Suspect library usage (verify when porting)

- `@gltf-transform/cli` is not a dependency; `npx` would fetch it at request time.
- `etc1s --max-size` (~80%) and `--power-of-two` (~60%) are not valid in CLI v4, which also needs
  the `ktx` binary ≥ 4.3 rather than `toktx`.
- `weld({tolerance})` was removed in v4 (~70%).
- Meshopt-compressed inputs throw without encoder/decoder registration (~75%).
- `collada_import` is gone in Blender 5.x; Blender 4.2 LTS must be pinned (~85%).
- Portal advertises `.stl/.dae/.blend` preview but has no loaders for them, and no
  Draco/KTX2/meshopt decoders on `GLTFLoader`.
- `FBXLoader` does not apply the FBX unit scale but Blender does: preview and result can disagree
  by 100× (~65%).

## Save schema (`IslandSaveDocumentV1`)

A stub. It cannot round-trip an island.

- No producers or consumers except pets, and nothing reads it back.
- Terrain heights and vertex paint are single Base64 strings with no element type, resolution,
  compression or scale fields, and no encode/decode code.
- Structures have no stable instance id (undo needs one), no variant, no connection data.
- Imported models have no source URL and depend on the cache still holding the file.
- Pets store three blend-shape weights where the pet phase defines four; no rotation.
- Media props, player spawn, island name and the grass/astroturf mode flag are absent.
- `SchemaVersion` is never checked; there is no migration; writes are not atomic.

## Hardware tier detection

- Relies on device-model substrings (including internal codenames) and `Screen.width <= 1900`.
  If `Screen.width` reports the eye-buffer width rather than the panel, every Quest 3 is classified
  as a 3S (~50%; needs a headset to settle).
- The Phase 1 two-tier detector is left in place, so there are two sources of truth.
- The budget table gives Quest Pro a 160 MB import cap; the code and prose give 128 MB.

## What the plan does instead

See `02-ARCHITECTURE.md` (ADR-12, ADR-13) and backlog track `I`:

1. **Convert in the browser.** Three.js loaders → `GLTFExporter` → gltf-transform `WebIO` (dedup,
   prune, weld, simplify to an absolute triangle cap, texture resize, Draco/Meshopt via WASM).
   Preview and output are the same scene, which removes the unit mismatch.
2. **Thin relay, no server-side parsing.** Static site + small function + object store with a
   24-hour lifecycle. The headset generates a high-entropy token shown as a QR/long code, then
   polls. The server checks only size and GLB magic.
3. **The headset is the trust boundary.** Before decode it parses the GLB JSON and image headers
   and enforces texture-dimension, triangle, draw-call and real memory-estimate gates.
4. Zero-infrastructure fallback from day one: an imports folder scanned on the device.
5. Lost: `.blend` upload (users export GLB from Blender), Blender-grade FBX/DAE fidelity,
   server-guaranteed sanitisation. A sandboxed Blender worker is a post-launch option, specified as
   a per-job container with no network, read-only root, non-root user and hard resource limits.
