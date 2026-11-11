# 02 — Architecture baseline

The locked decisions for this project. **This file supersedes every phase of the blueprint where
they differ.** Change it only as described in `05-OPERATIONS.md` §11.

Decisions marked **(owner)** are the owner's to change; those dated were made by the owner. See
`00-START-HERE.md` §3.

---

## What this is

**Islandia is a gift.** The owner is making it for their partner, who will be the one wearing the
headset. It is not going to a store and is not for sale, and that shapes real decisions rather
than being a footnote: breadth matters less than delight, the first five minutes matter more than
the feature list, and judgement calls go to *what makes this a nice thing to be given* rather than
to what a catalogue page would need.

The person playing it is not a developer and will not be reading instructions.

## Product in one paragraph

**Islandia** — a single-player Meta Quest sandbox. The player arrives by train at a small island,
sculpts its terrain, plants it, builds on it, **builds a roller coaster and rides it**, brings in
their own 3D models and **paints anything**, befriends the island's stray cat, and watches and
listens to their own media library — served by the owner's StreamBox — on screens and speakers
placed in the world. **Every island has a station, and the train runs to as many more islands as
they want to make**, so each one stays small and can be its own idea. Two interaction depths, a
forgiving *toybox* mode and a precise *studio* mode, and two scales, life-size and tabletop.

**Trains and roller coasters are the point.** The person this is for loves both (owner,
2026-11-11). Where a judgement call is close, it goes to whatever makes riding and building them
better.

## ADR-01 — Engine and packages

- **Unity 6000.3 LTS**, exact patch pinned in `ProjectSettings/ProjectVersion.txt` to one for which
  a GameCI `unityci/editor:ubuntu-<version>-android-3` image exists. Chosen in `F-03`.
- Core packages (URP, Core RP, Burst, Collections, Mathematics, Input System) are **not pinned by
  hand**: they follow the editor. `Packages/packages-lock.json` is committed.
- XR stack: Unity OpenXR plugin, *Unity OpenXR: Meta*, XR Interaction Toolkit, XR Hands. **No Meta
  XR Core SDK / OVRPlugin** unless a decision record adds it.
- Starting versions to try (to be confirmed by resolution, not trusted): OpenXR 1.16+, Meta OpenXR
  2.6+, XRI 3.3+, XR Hands 1.7+, AI Navigation 2.0.x, glTFast 6.17+, KTX 3.6+, Draco 5.4+.
- Nothing that needs a newer editor than the pin (on-tile post-processing, depth-as-input-attachment
  render features, UI motion vectors) is in the baseline.
- Upgrading the editor is a decision record plus a full device gate, never incidental.

## ADR-02 — Target device **(owner decision, 2026-11-10)**

- **The Quest 2 is the design, budget and verification target.** It is the owner's headset and the
  only hardware anything will be measured on. 72 Hz, 13.8 ms per frame, Snapdragon XR2 Gen 1,
  1832 × 1920 per eye, 6 GB of memory.
- Everything must fit the Quest 2. No feature may depend on a newer headset.
- Quest 3, 3S and Pro exist as rows in the tier table with more headroom. They are **unverified**
  and are described that way anywhere they are mentioned, until someone measures on one.
- `DeviceTier` is decided by **one pure function** over (device model string, GPU string, eye-buffer
  size), table-driven and unit-tested. Quest 2 is the baseline; unknown devices map to it. The
  table is confirmed with strings captured at the first device gate.
- Meta's December 2027 cut-off for new Quest 2 store submissions does not apply: this is never
  submitted to a store (ADR-20). The headset keeps working regardless.

## ADR-03 — Rendering baseline (kept from the blueprint)

Vulkan only · URP Forward (never Forward+ or Deferred) · Render Graph · single-pass multiview ·
2× MSAA · HDR off · depth texture off · opaque texture off · depth priming off · camera
post-processing off · SRP Batcher on · GPU Resident Drawer off · dynamic batching off · one
directional light, one shadow cascade, 15 m shadow distance, foliage casts no shadows · foveated
rendering on · IL2CPP, ARM64, linear colour, ASTC.

Every item is set by code and asserted by an EditMode test (`F-04`, `F-05`).

Shader rules:

- Every shader carries the stereo instancing macros.
- No `UsePass` from URP Lit. Shaders own their passes and their material constant buffer.
- Runtime-toggled keywords use `multi_compile_local`, never `shader_feature`.
- Every shader used only from code is in *Always Included Shaders* or a shader variant collection.
- Opaque wherever possible. Alpha blending needs a decision record.

## ADR-04 — Lighting

No baked lighting of any kind. One directional light, trilight ambient, ambient occlusion baked
into vertex colours per asset. A time-of-day parameter drives sun and ambient colours.

## ADR-05 — Water and shoreline

One opaque water mesh. Shoreline foam and shallow-water colour are computed from the **terrain
heightfield uploaded as a texture** (`waterLevel − height`), updated for the brush window on each
stroke. No scene-depth read. The depth-input-attachment approach is an optional experiment after a
device gate, behind a flag, and only if it measures better.

## ADR-06 — Headless, code-first project

- One boot scene, generated by `Island.Editor.BootSceneBuilder` in batchmode. It contains the XR
  rig and a `CompositionRoot`; everything else is constructed in code.
- No scene-serialised cross-references to wire by hand. Services are resolved through the
  composition root.
- Editor generators are idempotent static methods callable with `-executeMethod`, open and save
  what they touch, and **throw on failure** so batchmode exits non-zero.
- XR settings assets are generated once, **committed**, and asserted by a test, because batchmode
  builds have historically reset them.
- Generated assets live in `Assets/Island/Generated/` and are reproducible from source. Whether a
  generated asset is committed or rebuilt in CI is decided per asset type in `F-06` and recorded.
- `.meta` files: scripts and folders get deterministic GUIDs from `tools/gen_meta.py`. CI fails if
  an import produces uncommitted `.meta` changes.

## ADR-07 — Assemblies and layering

| Assembly | May reference | Purpose |
| :-- | :-- | :-- |
| `Island.Core` (`Packages/com.island.core`) | nothing from Unity | Constants, maths, data model, save format, catalogue, budget ledger, undo, snapping, AI scoring, import gatekeeper. Tested with `dotnet test` in seconds |
| `Island.Runtime` | Core, Unity engine, URP, Burst | Meshes, jobs, rendering, audio, composition root. No XR types |
| `Island.XR` | Runtime, OpenXR, XRI, XR Hands | Input adapters, rig, device services |
| `Island.Editor` | all of the above, UnityEditor | Generators, configurators, build entry points |
| `*.Tests` | the assembly under test | EditMode and PlayMode suites |

Rules: gameplay talks to `IInputSource`, `IRig`, `IClock`, `IDeviceServices`, never to XR types.
A **simulation implementation** of each lets the whole game run in PlayMode tests with scripted
input and no XR loader. Burst jobs that duplicate Core logic for speed are differential-tested
against the Core reference.

## ADR-08 — World constants

Defined once, in `Island.Core.IslandConstants`. Nothing else may hard-code them.

| Constant | Value |
| :-- | :-- |
| Island extent | 64 m × 64 m, centred on the origin |
| Chunks | 8 × 8, each 8 m × 8 m |
| Chunk resolution | 33 × 33 vertices, 0.25 m spacing, 2,048 triangles |
| Heightfield | 257 × 257 samples, shared by all chunks |
| Height limits | −3.5 m … +18 m |
| Water level | −0.35 m |
| Material weights | R sand · G grass/turf · B rock (auto on slope) · A path |
| Tabletop scale | 1 : 25 |

Derived thresholds (sand line, scatter cut-off, cliff angle) are defined relative to these in the
same file, once.

These are **per island** (ADR-24): every island in the world has the same extent and grid, which
is what keeps one island at a time within the Quest 2's budgets. The station footprint and the
train's corridor off the island are constants here too, and are reserved from sculpting and
placement.

## ADR-09 — Persistence

Designed first; nothing is "done" until it round-trips.

- **Container:** a versioned binary file — header, then independent sections, each with its own
  id, version, length and checksum — plus a small JSON manifest for humans and tooling.
- **Sections** are owned by subsystems through `ISaveSection`. Unknown sections are preserved on
  rewrite. Each section has migration functions and a migration test.
- **Writes are atomic:** temp file, flush, rename; previous save kept as backup; checksum verified
  on load; corrupted save falls back to backup and tells the player.
- **Stable 64-bit instance ids** for everything placed in the world. Undo, save and selection all
  use them.
- Terrain stored as quantised heights and weights with explicit resolution and scale, compressed.
- Save and load are off the main thread where the data allows.
- **A world of islands** (ADR-24): a small world index lists every island; each island is its own
  save file in this container, so an island loads, saves and is deleted on its own, and a damaged
  island cannot take the others with it. Autosave on a timer, on pause and on leaving an island;
  export/import of a save file. (Before 2026-11-11 this read "multiple island slots".)

## ADR-10 — Budgets

Planning numbers for the **Quest 2**, carried over from the blueprint and **unvalidated until
device data replaces them**. Enforced by a runtime **budget ledger** every subsystem reports into,
and by tests.

| Allocation | Quest 2 (72 Hz) | Notes |
| :-- | :-- | :-- |
| Frame time ceiling | 13.8 ms | App GPU time target well under it; headroom for the compositor |
| Terrain and ocean | 17 draws · 131K tris · 32 MB | Idle terrain is merged into 4 × 4 regions; only regions under an active stroke split to chunks |
| Foliage | 45 draws · 220K tris · 48 MB | |
| Architecture and props | 35 draws · 180K tris · 64 MB | |
| Imported models | 25 draws · 120K tris · 128 MB | 15 MB per file |
| Cat, media, UI | 18 draws · 60K tris · 96 MB | Was "pets"; one cat now uses far less than four pets did |
| **Busy-scene total** | **≤ 140 draws · ≤ 711K tris · 368 MB** | Hard caps: 200 draws, 750K triangles, 368 MB |

Hard caps: **1 cat per island, 1 active video screen, 1 audio decoder.** (Was four active pets;
`CAT-02` changes the ledger's cap in code and its test.)

**Rides** — the station, the train, coaster track and cars — are accounted under *Architecture and
props* until measured. `RC-03` measures a 200-piece coaster; if rides need their own row, it is
carved out of the existing allowances by decision record, not added on top, because there is no
slack to add it from. **Only one island is ever resident** (ADR-24), so these numbers are per
island and the number of islands costs storage, not frame time.
Per-frame managed allocation in steady state: 0 bytes, asserted in PlayMode tests.

A playing 1080p video is one hardware decode plus one texture; its cost on a busy island is
measured at the first device gate and the media row adjusted from the result.

**These allowances leave almost no slack.** Their sums come to 70% of the draw-call cap, 95% of
the triangle cap and 100% of the memory figure, so an island with every subsystem at its allowance
is at the limit rather than merely busy, and the ledger reports it as red. The figures are kept as
written because replacing one guess with another would not be an improvement, but **memory is the
measure to take first at `HG-1`**: it is the one with no slack at all. Pinned by
`TheQuest2AllowancesLeaveNoMeaningfulHeadroom`; see `DECISIONS.md`, 2026-11-10.

The wrist gauge shows ledger usage: green below 80%, yellow to 95%, red above.

The tier table also carries the blueprint's larger numbers for Quest 3S and Quest 3 (265 and 290
draws, 1.24M and 1.48M triangles). They are data only, and unverified.

## ADR-11 — Content

The inventory is a **data-driven catalogue**, not code.

- `content/catalogue/*.yaml` is the source of truth: id, category, source, licence, LOD budget,
  sockets, palette set. Generated C# ids and Unity assets derive from it.
- **Source (owner, 2026-11-11): premade packs of free assets, and nothing else.** The owner chooses
  and downloads the packs — the coaster cars and track, the avatar, and everything else — and
  uploads the archives to a **draft GitHub Release** tagged `assets-wave-<n>`, which only the owner
  can see. The ingestion workflow (`C-02c`) fetches them by asset id with `GITHUB_TOKEN`, records
  each archive's checksum, and normalises it with the headless Blender pipeline (scale, pivot,
  LODs, ambient-occlusion bake; palette mapping for flat-colour packs, textures kept within budget
  for textured ones). Parametric generators are no longer a content source. Nothing scripts past
  a download page.
- **Licences.** A pack is admitted if its licence allows use in a private gift that is never
  distributed. A **licence ledger** is generated from the catalogue and shipped as the in-app
  notices screen; attribution is honoured there, not treated as optional. A pack whose licence
  cannot be read is not guessed at: it goes to the owner in a `human-gate` issue.
- **The repository is public, and that is where licences bite.** Each pack is classified
  *redistributable* (CC0, CC-BY and the like) or *private-use-only*. Only redistributable processed
  output is ever committed. Private-use output never enters git, a published release or a workflow
  artefact; CI fetches it at build time, and any APK containing it goes only to a draft release
  (`C-02d`). Raw archives are never committed, whatever their licence.
- A **validator** fails CI if any item exceeds its triangle, LOD, texture or bounds budget, lacks a
  collider or thumbnail, or has open or single-sided geometry where the camera can get behind it.
- **Counting is honest.** Reports state distinct meshes and palette variants separately.
- **No fixed count (owner, 2026-11-11).** The ≈ 120-mesh target of 2026-11-10 is lifted: what is in
  the catalogue is what the owner's packs provide and what passes the validator and the eye.
- **Coherence beats count, per island.** Islands can have themes, so packs in different styles can
  live on different islands without clashing. Items are admitted per theme from a contact sheet,
  not wholesale; twenty things that belong together still beat sixty that do not.

## ADR-12 — Importing the player's own models

- **The headset is the trust boundary.** Before decoding, `Island.Core` parses the GLB container,
  its JSON and its image headers and rejects anything over the triangle, texture-dimension,
  draw-call, file-size or estimated-memory limits. The same rule file is used by the web converter.
- Loading uses glTFast with a **custom material generator** (no post-hoc remapping), a correctly
  configured per-frame time budget, de-duplication of in-flight loads, reference-counted templates
  with eviction, measured memory accounting, and bounds-based colliders.
- **Two delivery paths:** an imports folder on the device (works with no infrastructure), and the
  relay (ADR-13).
- `.blend` is not accepted in v1. Users export glTF from their tool.
- **Imported models are first-class (owner, 2026-11-11):** once placed they are paintable like
  everything else (ADR-26). Segmentation into paint regions runs on the headset after the
  gatekeeper, off the main thread, using the same `Island.Core` code that segments pack assets.

## ADR-13 — Companion

- A **static web app** converts in the browser: Three.js loaders → gltf-transform (prune, dedupe,
  weld, simplify to an absolute triangle cap, resize textures, compress geometry) → preview beside
  a 1.8 m reference figure → download, or send through the relay.
- A **thin relay** stores opaque blobs for at most 24 hours. The headset generates a high-entropy
  token and shows it as a QR code; the browser uploads against the token; the headset polls. The
  relay checks size and GLB magic bytes and **never parses a file**. No accounts.
- Everything runs locally in Docker for CI. **The relay is deployed (owner, D6, 2026-11-11).**
  Hosting must be a free tier, and preferably one that needs **no payment card**, so that it cannot
  run up a bill at all; Google Cloud Run's free tier, the earlier default, requires a billing
  account. `I-07` chooses the provider and the owner creates the account at a human gate.
- A server-side Blender converter is out of scope for v1. If added later it is a per-job container
  with no network, read-only root, non-root user and hard resource limits.

## ADR-14 — Audio **(owner decision, 2026-11-10)**

- **The music source is the owner's StreamBox library** (ADR-23), played through radio and speaker
  props placed in the world. Also: the player's own files from the app folder, and an ambient bed
  from CC0 loops.
- One decoder. Additional speakers are fed from it through a ring buffer.
- StreamBox tracks are fetched to a bounded on-device cache and played from it, which avoids
  decoding a live network stream inside the engine.
- **No Spotify.** An internet radio tuner is optional, not planned, and first in the cut order.
- Spatialisation starts with Unity's built-in 3D panning. An HRTF plugin is a later, device-verified
  option.

## ADR-15 — Screens **(owner decision, 2026-11-10)**

- TV and projector props are **StreamBox screens** (ADR-23): browse the library and play it.
  They also play local files and direct video addresses.
- Playback sits behind `IMediaPlayer`. The backend is chosen from a device measurement (`V-14`):
  Unity's video player with an HLS address if that works on the headset, otherwise a small native
  player plugin, otherwise a progressive fallback (`07-STREAMBOX.md` §4).
- One active video screen on a Quest 2.
- A composition-layer quad for sharper video is an isolated spike after a device gate.
- **No in-world web browser.** No paid media dependency of any kind.
- No feature is described to players as supporting any DRM-protected service.

## ADR-16 — The island cat **(owner, 2026-11-11)**

Replaces adoptable, customisable pets. There is no pet generator and no pet studio.

- **One stray cat lives on each island**, created with the island. Each island's cat looks
  different: coat colour and pattern derive from the island's seed, within what the model supports.
- The player can approach and pet it. **If they choose to feed it, it gets a collar, and a food
  dish and a water dish appear** as ordinary placeable objects — movable, paintable, saved and
  undoable. Once befriended it can be named, and the name is shown on the collar tag as real text.
  Its state (stray, fed, befriended) is saved with the island.
- The model and clips come from the owner's packs; `Animator` controllers are generated by editor
  code. If no rigged cat is available, a free pack is proposed to the owner; a rigid-part cat is
  the honest last resort.
- Behaviour is a deterministic, seeded utility AI in `Island.Core`, ticked at 4 Hz.
- Movement follows real paths around structures, water, the station and coaster supports, rebuilt
  per changed region.
- Petting and feeding use stock XR Interaction Toolkit providers, with controllers and with hands.

## ADR-17 — Draw-call strategy

Measure before building. One shared material per family plus the SRP Batcher and instancing is the
baseline. Foliage instancing buckets by mesh, never by species. Runtime mesh merging of placed
structures is built only if the ledger on a stress island shows it is needed, and must un-merge
on edit and on undo.

## ADR-18 — Application SpaceWarp

Behind `IFrameSynthesis` with a no-op default. Not enabled in v1 unless a device gate shows it is
needed and it is implemented through the public `SpaceWarpFeature` API with per-shader motion
passes. No automatic trigger.

## ADR-19 — Repository and CI **(owner decision, 2026-11-10)**

- **Public repository, GitHub-hosted runners.** No cost. No workflow runs on the owner's PC.
- GitHub Actions with GameCI. Runner image pinned (`ubuntu-24.04`). A free-disk-space step before
  every Unity job. `Library/` cached by lockfile hash.
- Because the repository is public: workflows from forks need approval and never receive secrets;
  **no self-hosted runner is ever attached**; nothing private is committed — no server address, no
  account detail, no paid asset, no keystore. A scan enforces the first two.
- **Nothing from a private-use asset pack is ever public** (ADR-11): not in git, not in a published
  release, not in a workflow artefact. The owner's raw pack archives live on draft releases, which
  only the owner can see. Whenever a build contains private-use assets, its APK goes only to a
  draft release.
- Pull requests run: core tests, lint, EditMode and PlayMode tests, companion suites — each only
  when its paths change. The IL2CPP Android build runs on merge to `main` and on demand.
- Trunk-based: short-lived `claude/wp-*` branches, squash merges, `main` always releasable.
  Required status checks protect `main`.
- No Git LFS until a decision record adds it; meshes are small.

## ADR-20 — How it gets onto the headset **(owner, 2026-11-10)**

Not through a store. This is a gift for one person, so distribution is: CI builds a signed APK,
the owner installs it over USB with `adb`, and that is the whole of it.

Target SDK 34, minimum SDK 32, 64-bit only — not for compliance, but because they are what the
device and toolchain expect. No store compliance validator, no listing media, no submission.

A signing key is still needed, because Android will not install an unsigned build and because an
upgrade must be signed by the same key or it installs as a separate app and loses its saves. The
key is generated once and kept by the owner.

If this ever did go further, the work removed here is recoverable from `DECISIONS.md`.

## ADR-21 — Privacy

No accounts, analytics, advertising or telemetry. The only sign-in is to the owner's own StreamBox
server, whose address and token stay on the device.

There is no public privacy policy to write, because there is no public. But the care is unchanged
where it actually matters: this app talks to a private media server holding a household's library,
and nothing about that may leak into a public repository, a log, or a crash report. That is
enforced by the privacy check on every change, not by a document.

## ADR-22 — Identity

The app is **Islandia**. Android package id `dev.islandia.app` is a placeholder and must be
confirmed by the owner before the first upload to a Meta release channel, because the package id
cannot change afterwards.

## ADR-23 — StreamBox **(owner requirement, 2026-11-10)**

Islandia is a native client of the owner's StreamBox media service. Full specification:
`07-STREAMBOX.md`.

- Pairing by StreamBox's device flow; a bearer device token stored in the platform keystore.
- Browsing, playback, progress and music through the gateway's existing client API. Islandia shows
  what the gateway returns and enforces no access rules of its own.
- Islandia registers as a Connect device, so the StreamBox phone app is its remote.
- The client lives in `Island.Core` behind injectable transports and is tested against a mock
  gateway built from a contract snapshot in `contract/streambox/`.
- **Separate projects.** Nothing in this repository pushes to StreamBox. Requests for StreamBox-side
  changes go to the owner as `human-gate` issues.
- The server address is configuration on the device, never in the repository.

- **The headset pairs to a dedicated StreamBox profile (owner, 2026-11-11)**, one that holds no
  restricted libraries, so it can be revoked on its own and shows only what is meant for it
  (`07-STREAMBOX.md` §9).

## ADR-24 — Islands and the train **(owner, 2026-11-11)**

The world is **as many islands as the player wants**, joined by a train. Each island stays small
and contained — the ADR-08 extent — so each can be its own idea, and the Quest 2 only ever has to
hold one.

- **One island is resident at a time.** Leaving an island saves and unloads it; the next one loads
  while the train is on its way, so the ride *is* the loading transition and never a loading
  screen. Memory returns to its baseline after every trip.
- **Every island has a station** on its shoreline, with a platform, a departure board and a track
  leading off over the water. Its footprint and the track corridor are reserved: nothing sculpts or
  builds over them, and the player cannot move or delete the station.
- **Islands are created on demand** from a seed, which sets the starting terrain, the station
  position, the theme and the cat. There is no upper bound on how many; the departure board lists
  them all, with a thumbnail taken when each was last left.
- **Saves:** a small world index plus one save file per island (ADR-09).
- **The first launch begins on the train,** arriving at the first island's station. It is the
  opening of the gift, so it is designed, not defaulted.
- The train between islands is a seated ride under ADR-25's comfort rules.
- Engine-free logic — the world index, seeds, reservations, travel state — lives in `Island.Core`
  and is tested with `dotnet test`.

## ADR-25 — Roller coasters and riding **(owner, 2026-11-11)**

The player **builds roller coasters on their island and rides them.** Together with the train,
this is what makes Islandia theirs, and it gets the attention that deserves.

- **Track is a chain of pieces** taken from the owner's coaster pack: each piece is catalogue data
  with an entry and an exit frame (position, heading, pitch, bank). Placing a piece composes frames
  exactly; nothing is integrated, so a long circuit closes without drift. The piece set follows
  the pack and is recorded by decision record when `RC-01` reads it.
- **Validation is plain-spoken:** closed circuit, one station, a lift before the first drop, no
  self-intersection, supports that reach the ground, nothing in the station reservation or the
  train corridor. Problems are shown on the track where they are.
- **Ride physics is energy-based** along the centreline — gravity, friction, chain lift, brakes —
  fixed-step and deterministic, in `Island.Core`. Forces are computed per sample and give each
  coaster an intensity rating before anyone rides it.
- **Comfort is a requirement, not a setting buried in a menu.** Riding is seated, in a car that is
  a fixed frame of reference with a visible horizon; a vignette narrows with angular acceleration;
  one button ends any ride with a fade. Options: an intensity cap (a gentle mode slows any coaster
  to a comfortable ride), vignette strength, and **watch from outside**, where the avatar (ADR-27)
  rides and the player watches. The train obeys the same rules with tighter limits on acceleration
  and turn rate.
- Comfort numbers are planning hypotheses until `HG-R` replaces them with what felt right on the
  headset. They live in `Island.Core` as data, once.
- Building works life-size and at tabletop scale, which is the comfortable way to build big.
- Budgets: rides draw on *Architecture and props* until measured (ADR-10). Finished track
  sections are merged and un-merge on edit and undo (ADR-17).

## ADR-26 — Painting anything **(owner, 2026-11-11)**

Everything placed in the world can be painted — pack items, built structures, imported models, the
cat's dishes, coaster cars and the train. This is a major feature and gets real attention.

- **Paint respects edges.** Each mesh is segmented into *paint regions*, split at material-slot
  boundaries, UV-island boundaries and edges sharper than a dihedral threshold (30° by default),
  after welding coincident vertices so split normals do not fragment a smooth surface. Tiny
  regions merge into their neighbour. A **fill** paints one region — the seat of a chair, not the
  whole chair — and highlights what it will paint before it does.
- A **brush** paints the triangles under a sized cursor, which suits the low-poly look of most
  packs. Per-texel painting of detailed textures is not in this version.
- **One segmentation implementation**, in `Island.Core`: run as a CI tool over pack assets at
  ingestion, and on the headset over imported models after the gatekeeper, off the main thread.
- When the regions are wrong, the player can adjust edge sensitivity per object, merge two
  regions, or split one along a drawn line.
- **Data:** a per-instance paint layer — region fills and per-triangle brush overrides — keyed by
  instance id, saved in its own section, every change undoable. An unpainted instance stores
  nothing.
- **Rendering:** region ids are a vertex attribute and fills come from a small per-instance colour
  table on one shared material, so filled instances still batch. A brush-painted instance gets its
  own baked mesh, counted in the ledger.

## ADR-27 — The avatar **(owner, 2026-11-11)**

A **generic premade avatar** from the packs the owner uploaded. There is no character creator, no
anthropomorphic character creation and no customisation; something better may come later, and is
deferred rather than planned. The player sees the avatar only where they can see themselves from
outside: at tabletop scale, and when watching a ride from outside.
