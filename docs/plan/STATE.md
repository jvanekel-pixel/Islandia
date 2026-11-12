# STATE

*The one file to read to know where the project stands. Updated in every pull request.*

| | |
| :-- | :-- |
| **Stage** | 2 — Construction |
| **Current milestone** | M1 Factory online · engine-free work ahead of it on M2, M4, M5 and M10 |
| **In flight** | Nothing from the driver. `RC-01b` is open as PR #42 from the planning conversation |
| **Repository** | https://github.com/jvanekel-pixel/Islandia (public) |
| **Driver** | **Running** on a schedule, a new session each run. Its first run (2026-11-11) was **not** on the `islandia` environment: no .NET, no setup script, unrestricted network. Point the routine at that environment at <https://claude.ai/code/routines> |
| **Last updated** | 2026-11-12 — `F-07` done: the APK builds in CI, the Library is cached, and no editor run may leave the project changed |
| **Target** | Quest 2 · public repository · StreamBox native in-world · **a gift, never published** |

## The plan changed on 2026-11-11

The owner re-planned the project around what makes it theirs. In short (`DECISIONS.md`, 2026-11-11):

- **Trains and roller coasters are the heart of the gift.** Endless small islands, a station on
  each and a train between them (M4); build roller coasters and ride them (M5). Comfort has its own
  early gate, `HG-R`.
- **Content is premade free packs only**, which the owner downloads and uploads to a draft release.
  No generators, no fixed count.
- **One stray cat per island** replaces pets; feed it and it gets a collar and dishes (M11).
- **A generic avatar**; no character creation.
- **Import and paint anything** — the relay is deployed, and painting respects edges (M10).
- StreamBox pairs to a dedicated profile with no restricted libraries.

Milestones were renumbered: media is M6, garden M7, builder M8, import M9, alpha M12, beta M13,
finished M14. Older documents and log entries use the old numbers.

## With the owner now

Being walked through one at a time in the planning conversation. None blocks the next packages.

| What | Unblocks |
| :-- | :-- |
| Point the scheduled driver at the `islandia` environment (above) | `F-10`, and sessions that start with the toolchain installed |
| Headset developer setup: Meta developer organisation, Developer Mode, `adb` on the PC | HG-1 |
| Unity credentials in the cloud environment, if the owner wants sessions to run Unity directly | `S-01`'s optional accelerator |

## Settled with the owner on 2026-11-11

- **Scheduled driver:** a new routine, a fresh session each run, this repository only, Opus,
  daily at 01:05 America/Denver. The owner may move it to hourly after the weekly usage reset; the
  lease check makes hourly runs back-to-back rather than overlapping. Its environment should be
  `islandia`, not Default — the owner is switching it.
- **Signing key:** made on the owner's PC with `tools/owner/new-signing-key.ps1`; the four
  `ANDROID_*` secrets open it in CI (`Signing key` workflow). Fingerprint in `R-01`'s note.
- **Identity:** package id `dev.islandia.app` is final; the name under the icon is *Islandia*
  (ADR-22).
- **Who plays it:** the owner's own Quest 2, played by their partner, who thinks VR is dumb and
  asked for exactly this game. See `DECISIONS.md`.

## Asset wave 1 is uploaded (2026-11-11)

Draft release `assets-wave-1` (id 442151453), 23 archives, ~95 MB. **Every one is CC0** — its
`License.txt` says so — so processed output may be committed to this public repository and
`C-02d`'s private-use path is not needed for this wave. Checked by downloading each archive.

| Packs | Notes |
| :-- | :-- |
| Kenney: `coaster-kit` (183 models), `train-kit` (103) | The two that matter most: `RC-01` and `TR-*` read their piece sets from these |
| Kenney: `animated-characters-protagonists`, `-retro`, `-survivors` | FBX only, rigged and animated — the candidates for `AV-01`'s generic avatar |
| Kenney: nature, fantasy-town, city ×3, building, brick, modular-buildings, castle, hexagon, furniture, food, survival, graveyard, space-station | glb + fbx + obj each |
| KayKit (Kay Lousberg): Forest Nature, Furniture Bits, Halloween Bits | gltf + fbx + obj; no glb |

Gaps found: **no cat model** in any pack (`CAT-*` needs one — a later wave); the release notes
link `kenney.nl/assets/mini-forest` but no archive for it was uploaded.

## Blocked

**Nothing.**

## Waiting on a device (`needs-device`) or a person (`needs-human`)

| What | From | Why it needs a person |
| :-- | :-- | :-- |
| — | — | Nothing at the moment |

## Next up

Take the first `todo` package in `BACKLOG.yaml` whose dependencies are `done` — `make next` prints
them. As of this writing: **`F-14`** (the package id, before anything goes on the headset) and **`F-09`** (release workflow and gate templates) — the last M1 steps — then **`C-02b`** (the Blender
normalisation pipeline), **`V-07b`**, and the engine-free starts of the new features — **`RC-01b`** (coaster validation — PR #42 is already open for it, so skip it), **`RC-01`** (coasters saved, with undo), **`RC-02`** (ride physics), **`PNT-01`** (paint segmentation) — plus
`I-04` and `I-05`. Prefer the current milestone; take an engine-free package when it is waiting
on CI.

**27 of 138 live packages are done** (7 more were cut on 2026-11-11; `RC-01` was split in three on 2026-11-12). `F-01`–`F-06`,
`F-07`, `F-07a`–`F-07c`, `F-08`, `F-13`, `V-02`, `V-06`, `V-07a`, `V-08`, `SB-01a`–`SB-01d`, `C-01a`,
`C-01b`, `C-02a`, `I-01`, `TR-01a`, `TR-01`, `RC-01a`.

### What exists now

- **The Unity project**, generated and committed by its own workflow: editor `6000.3.28f1`, 67
  packages locked, a second import that changes nothing, and every ADR-03 rendering decision set in
  code and asserted from the committed assets (`F-04`).
- **Six CI workflows.** `checks` (L0/L1, every push), `content` (Blender, path-filtered),
  `unity-tests` (EditMode and PlayMode), `android-build` (the APK, on merge to main and by hand), `unity-package-sources` and `unity-configure` (both
  generate; both commit only on dispatch). `auto-merge` lands anything labelled `awaiting-merge`
  once its checks pass.
- **StreamBox reachable end to end** from `Island.Core`: pairing, browsing, playing, progress, music
  and remote control, all proven against the mock gateway in CI.
- **The content pipeline**, less its input: catalogue, schema, validator, licence ledger, honest
  count, and a pinned headless Blender proven to script, measure, export GLB and render a thumbnail
  with no GPU.

### Four things a new session should know

1. **Label last.** `awaiting-merge` means *land this now*; do not push to a branch after labelling
   its pull request. Auto-merge has squashed a PR mid-push and stranded the rest.
2. **Branch from `main`, not from an unmerged branch.** Squash-merging the parent leaves the child
   conflicted.
3. **A workflow's own commit triggers nothing.** After dispatching `unity-configure` or
   `unity-create-project`, dispatch `unity-tests` by hand — pushes made with `GITHUB_TOKEN` do not
   start workflow runs.
4. **The Unity workflows share one queue slot.** They all use the `unity-licence` concurrency
   group, which keeps one run going and *one* waiting; a newer waiting run cancels the older. A
   push to a pull request's branch queues `unity-tests` twice (push and pull_request), which can
   cancel a queued `unity-configure`. When a package needs generated assets, cancel the doomed
   test runs, dispatch `unity-configure`, then dispatch `unity-tests` (F-06 did this).
   `unity-configure`'s dispatch also *settles* (F-07): it runs the tests and one Android build and
   commits what packages wrote, because `unity-tests` and `android-build` fail on any change.

### Open, not blocking

- [#25](https://github.com/jvanekel-pixel/Islandia/issues/25) `human-gate` — answered: archives
  arrive on draft releases, and wave 1 is uploaded. `C-02c`'s pull request closes it.
- [#21](https://github.com/jvanekel-pixel/Islandia/issues/21),
  [#15](https://github.com/jvanekel-pixel/Islandia/issues/15) `infra` — two defects in the mock
  StreamBox gateway, both found by CI, neither urgent.

## Recently merged

- `F-07` the APK built headlessly in CI and kept as an artefact; Library cached; any editor run that leaves the project changed fails; `unity-configure` settles what packages write (2026-11-12)
- `F-06` the boot scene and composition root: `Island.Runtime` and `Island.XR`, ADR-07 checked by lint, a generated scene that a rerun leaves alone, and the first PlayMode tests booting it in simulation (2026-11-12)
- `RC-01a` coaster pieces from the owner's pack: 37 pieces, frames in integer sub-units, a 200-piece circuit that closes exactly (2026-11-12)
- `TR-01` the world of islands: an index that never loads islands, one save file each, deletion, and migration of an old single-island save (2026-11-12)
- `TR-01a` an island from its seed: terrain, station on the shore, reserved ground (2026-11-12, PR #39)
- `F-05` OpenXR for the Quest 2 in code, asserted from disk (2026-11-11, PR #36)
- The owner's re-plan (2026-11-11)
- `F-04` ADR-03 set in code and asserted (2026-11-11, PR #32)
- `F-07c` package sources uploaded from the resolved project (2026-11-11, PR #31)
- `F-13` auto-merge's trigger list checked (2026-11-11, PR #30)
- `F-07b` EditMode and PlayMode tests in CI (2026-11-11, PR #29)
- `F-03` the Unity project, generated (2026-11-11, PRs #27, #28)
- `C-02a` pinned headless Blender (2026-11-11, PR #24)
- `F-02` Unity licence activation in CI — working (2026-11-10)
- `V-02`, `V-06`, `V-08`, `SB-01a`–`SB-01d`, `I-01`, `C-01a`, `C-01b` (2026-11-10/11)

## Measured facts

Filled in as they are measured; planning numbers elsewhere are hypotheses until they appear here.

| Fact | Value | Source |
| :-- | :-- | :-- |
| Local gate `make check` | 7 lint checks, 155 tooling tests, 363 core tests, 30 contract tests, 11 end-to-end — about 60 s | everything merged to 2026-11-12 |
| Coaster pack geometry | grid unit = pack unit (64 sub-units, planned at 2 m); straight 4, turns radius 2 and 4, loop 4 tall; all six styles within 0.58–0.61 units of one centreline | `RC-01a`, `content/facts/rides/coaster-kit.json` |
| Unity EditMode tests in CI | **406** — `Island.Core`, ADR-03, the XR setup, the boot scene and the XR platform | F-06 |
| Unity PlayMode tests in CI | **11** — the boot scene in simulation on scripted input and clock, and the simulation parts; ~0.5 s | F-06 |
| Unity image for configure and tests | `ubuntu-6000.3.28f1-android-3` — OpenXR needs the Android module to write Android settings. Pulled and ran without disk trouble | F-05 |
| .NET SDK in a cloud session | not present; installs to `~/.dotnet` in ~40 s | F-08 |
| Unity editor pinned | `6000.3.28f1` — the newest 6000.3 tag with an Android module | F-02 |
| Unity licence in CI | **works.** Seat request with account credentials; no licence file. 104 s end to end | F-02 |
| Editor version CI really runs | `6000.3.28f1`, reported by the editor itself | F-02 |
| ffmpeg in a cloud session | present, so the mock serves a genuinely decodable stream | SB-01a |
| Terrain on disk | ~129 KB before packing; whole island well under 400 KB | V-06 |
| GitHub REST from a session | runs, check-runs, status, pulls, issues all readable; `actions/secrets` blocked | F-01 |
| Driver self-merge | **Refused** by the permission classifier unless `.claude/settings.json` allows it. See `05-OPERATIONS.md` §7 step 4 | F-01 |
| CI: warm test run / Android build | tests ~7 min end to end even cold; Android build ~28 min cold (IL2CPP, ARM64) | F-06, F-07 |
| Runner minutes per Unity package | — | S-05 |
| Device strings and tier | — | HG-1 |
| Frame time, empty island | — | HG-1 |
| Video backend that works on the headset | — | HG-1 (V-14) |
| StreamBox commit the contract snapshot matches | 9ebfdd6 (read during planning) | SB-01 |
