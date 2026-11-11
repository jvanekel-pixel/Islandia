# STATE

*The one file to read to know where the project stands. Updated in every pull request.*

| | |
| :-- | :-- |
| **Stage** | 2 — Construction |
| **Current milestone** | M1 Factory online · engine-free work ahead of it on M2, M4, M5 and M10 |
| **In flight** | Nothing. `F-05` landed in #36 — the first work package from the new scheduled driver |
| **Repository** | https://github.com/jvanekel-pixel/Islandia (public) |
| **Driver** | **Running** on a schedule, a new session each run. Its first run (2026-11-11) was **not** on the `islandia` environment: no .NET, no setup script, unrestricted network. Point the routine at that environment at <https://claude.ai/code/routines> |
| **Last updated** | 2026-11-11 — the owner's re-plan: trains, roller coasters, endless islands, a stray cat, painting, premade packs |
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
them. As of this writing: **`F-06`** (assemblies, composition root, boot scene — the next M1 step), **`C-02b`** (the Blender
normalisation pipeline), **`V-07b`**, and the engine-free starts of the new features — **`TR-01`**
(the world of islands), **`RC-01`** (coaster track model), **`PNT-01`** (paint segmentation) — plus
`I-04` and `I-05`. Prefer the current milestone; take an engine-free package when it is waiting
on CI.

**22 of 134 live packages are done** (7 more were cut on 2026-11-11). `F-01`–`F-05`,
`F-07a`–`F-07c`, `F-08`, `F-13`, `V-02`, `V-06`, `V-07a`, `V-08`, `SB-01a`–`SB-01d`, `C-01a`,
`C-01b`, `C-02a`, `I-01`.

### What exists now

- **The Unity project**, generated and committed by its own workflow: editor `6000.3.28f1`, 67
  packages locked, a second import that changes nothing, and every ADR-03 rendering decision set in
  code and asserted from the committed assets (`F-04`).
- **Five CI workflows.** `checks` (L0/L1, every push), `content` (Blender, path-filtered),
  `unity-tests` (EditMode and PlayMode), `unity-package-sources` and `unity-configure` (both
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

### Open, not blocking

- [#25](https://github.com/jvanekel-pixel/Islandia/issues/25) `human-gate` — answered: archives
  arrive on draft releases, and wave 1 is uploaded. `C-02c`'s pull request closes it.
- [#21](https://github.com/jvanekel-pixel/Islandia/issues/21),
  [#15](https://github.com/jvanekel-pixel/Islandia/issues/15) `infra` — two defects in the mock
  StreamBox gateway, both found by CI, neither urgent.

## Recently merged

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
| Local gate `make check` | 7 lint checks, 134 tooling tests, 312 core tests, 30 contract tests, 11 end-to-end — about 40 s | everything merged to 2026-11-11 |
| Unity EditMode tests in CI | **349** — the 312 `Island.Core` ones, 22 asserting ADR-03, 15 asserting the XR setup | F-04, F-05, F-07b |
| Unity image for configure and tests | `ubuntu-6000.3.28f1-android-3` — OpenXR needs the Android module to write Android settings. Pulled and ran without disk trouble | F-05 |
| .NET SDK in a cloud session | not present; installs to `~/.dotnet` in ~40 s | F-08 |
| Unity editor pinned | `6000.3.28f1` — the newest 6000.3 tag with an Android module | F-02 |
| Unity licence in CI | **works.** Seat request with account credentials; no licence file. 104 s end to end | F-02 |
| Editor version CI really runs | `6000.3.28f1`, reported by the editor itself | F-02 |
| ffmpeg in a cloud session | present, so the mock serves a genuinely decodable stream | SB-01a |
| Terrain on disk | ~129 KB before packing; whole island well under 400 KB | V-06 |
| GitHub REST from a session | runs, check-runs, status, pulls, issues all readable; `actions/secrets` blocked | F-01 |
| Driver self-merge | **Refused** by the permission classifier unless `.claude/settings.json` allows it. See `05-OPERATIONS.md` §7 step 4 | F-01 |
| Unity editor version pinned | — | F-03 |
| CI: warm test run / Android build | — | S-05 |
| Runner minutes per Unity package | — | S-05 |
| Device strings and tier | — | HG-1 |
| Frame time, empty island | — | HG-1 |
| Video backend that works on the headset | — | HG-1 (V-14) |
| StreamBox commit the contract snapshot matches | 9ebfdd6 (read during planning) | SB-01 |
