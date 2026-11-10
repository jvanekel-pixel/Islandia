# 04 — Verification: how "done" is proven without a human

The blueprint proved everything by putting on a headset. This plan proves as much as possible
before that, automatically, and batches what remains into four scripted human gates.

## 1. The ladder

Prove each acceptance criterion at the **lowest level that can prove it**.

| Level | What | Where it runs | Typical time | Proves |
| :-- | :-- | :-- | :-- | :-- |
| **L0 Static** | Lint, formatting, `.meta` discipline, assembly-reference rules, forbidden-API scan, catalogue and licence validation, shader-rule scan | Session and CI | seconds | Structure and hygiene |
| **L1 Core** | `dotnet test` on `Island.Core` | Session and CI | seconds | Logic: terrain maths, save round-trips and migrations, undo, snapping, budgets, AI, import gatekeeper |
| **L2 EditMode** | Unity batchmode EditMode tests | CI | ~10 min | Project configuration, generators, Burst jobs against Core references, meshes, shaders compile without errors |
| **L3 PlayMode** | Unity batchmode PlayMode tests in simulation mode | CI | ~10–15 min | The game boots; scripted input drives real features end to end; zero steady-state allocations; save → reload equality |
| **L4 Build** | IL2CPP ARM64 Android build | CI, on merge | ~25–45 min | It links, strips and packages; shader variants exist; APK size and manifest are within limits |
| **L5 Visual** | Software-rendered screenshots attached to the PR | CI, if spike `S-02` succeeds | minutes | Gross visual sanity. Never performance |
| **L6 Device** | Scripted benchmark on a real headset | Owner's PC, one command | 10–20 min | Frame time, real draw calls, memory, thermals, tracking, stereo correctness |
| **L7 Human** | The owner's judgement | Headset | 30–90 min | Comfort, feel, art, sound, fun |

Companion stack equivalents: unit tests (L1), Docker integration tests with a headset-simulator
client (L3), Playwright end-to-end in headless Chromium (L3), fuzz and abuse fixtures (L3).

## 2. What each level must contain

**L0** — introduced in `F-01` and extended as rules are needed:

- No `UnityEngine` reference in `Island.Core`. No XR type outside `Island.XR`.
- No `UsePass`, no `shader_feature` on runtime-toggled keywords, stereo macros present.
- No `FindObjectsOfType`, LINQ, string concatenation or `Instantiate` in assemblies marked hot-path.
- Every `.cs`, shader and folder has a committed `.meta`.
- Every catalogue entry has a source, licence, budgets, collider and thumbnail.

**L2** — fixed suites that must always exist once introduced:

- `ProjectConfigurationTests`: every setting in ADR-03 and the XR feature set, read back from the
  committed assets. This replaces the blueprint's "Audit PASS" console line.
- `ShaderSmokeTests`: every project shader compiles for Android/Vulkan with zero errors.
- `GeneratorTests`: each generator is idempotent — running it twice changes nothing.

**L3** — fixed suites:

- `BootTests`: the boot scene loads in simulation mode with no errors or warnings in the log.
- `AllocationTests`: representative steady-state frames of each subsystem allocate zero bytes.
- `RoundTripTests`: build a scripted island, save, reload into a fresh world, compare state.
- `JourneyTests`: one scripted user journey per milestone (§5).

**L4** — the build job also emits and checks: APK size, target and minimum SDK, permissions,
architectures, included shader list, and a stripped-assemblies report.

## 3. Mapping the blueprint's gates

| Blueprint gate | Level | How |
| :-- | :-- | :-- |
| "Audit PASS" on settings | L2 | `ProjectConfigurationTests` |
| 0 B GC per frame | L3 | Allocation constraint in PlayMode |
| Chunk seams cannot crack | L1 + L2 | Border vertex and normal equality |
| Terrain and scene draw-call limits (Quest 2: ≤ 17 idle terrain, ≤ 140 busy scene) | L3, then L6 | Ledger assertions on stress islands; confirmed on device |
| Triangle budgets | L0 + L3 | Catalogue validator; ledger totals |
| 398 varieties / 631 items | L0 | Catalogue counts, distinct meshes reported separately |
| Weld → one draw | L2 | Renderer and sub-mesh counts |
| GLB < 15 MB, texture caps | L1 | Gatekeeper tests and fixture corpus |
| Import ≤ 2 ms main-thread | L3 (relative), L6 | Max frame time while loading |
| Pairing and delivery | L3 | Relay integration tests with a simulated headset |
| Audio CPU < 5% | L6 | Device profiler capture |
| Video decodes and stays in sync | **L6 only** | Media probe, then HG-2 against the real server |
| AI tick < 0.5 ms | L3 (relative), L6 | Marker timing |
| 90 / 72 Hz locked, GPU time, "zero GMEM loads" | **L6 only** | Benchmark autopilot |
| Crisp video, comfort, hand tracking | **L7 only** | Human gate checklist |

"Relative" means CI timing is a regression signal against its own history, never an absolute pass.

## 4. The device gate kit

Built in `V-12`, used at every human gate. The owner's whole job is: plug in, run, wear, wait.

**In the app:** a benchmark mode started by a launch parameter. It loads generated stress islands,
flies a fixed camera path, exercises sculpting, scattering, placing, importing, pets and media on
a script, and writes one JSON report per scenario with frame-time percentiles, ledger readings,
memory, device identifiers and a pass/fail against ADR-10. It also saves a handful of screenshots.

**On the PC:** `tools/device/Run-DeviceGate.ps1`:

1. Downloads the APK for the requested release.
2. Installs it with `adb`, enables the performance-stats log, keeps the headset awake.
3. Launches each scenario, waits, pulls the reports, logs and screenshots.
4. Restores the headset settings it changed.
5. Uploads everything to the gate's GitHub issue.

**In the repository:** `tools/device/analyze.py` turns the upload into
`docs/reports/device/<date>-<release>.md`, compares against the previous run, and proposes fix
packages. It is unit-tested against recorded fixture logs so the analysis itself is trusted.

A driver session then triages: each failed measurement becomes a `D-*` work package.

## 5. Milestone journeys

Each milestone adds one scripted journey to `JourneyTests`, and every earlier journey keeps
passing.

| Milestone | Journey |
| :-- | :-- |
| M2 | Boot → raise a hill → dig a pond → place a plant and a wall → save → reload → state equal → undo twice |
| M3 | Full brush set across chunk borders → paint all four surfaces → level a pad → switch scale and back → walk by teleport |
| M4 | Pair with the mock gateway → browse → play a film → pause five minutes → resume → next episode → play an album on two speakers → a Connect command from a simulated phone pauses it → sign out clears everything |
| M5 | Scatter a meadow of 3,000 plants across a hill → sculpt under it → plants follow → remove some → undo |
| M6 | Build a two-storey house from 40 modules with snapping → edit one wall → export → re-import the export |
| M7 | Convert a fixture model in the browser → send through the relay → simulated headset receives → gatekeeper accepts → placed; hostile fixtures rejected |
| M8 | Adopt two pets → customise → they path around a house and a pond → fetch → save → reload |
| M9 | A generated worst-case island at 100% of every budget runs all of the above for ten simulated minutes |

## 6. Definition of Done for a work package

- [ ] Every acceptance criterion has evidence at a stated level, or is tagged `needs-device` /
      `needs-human` and listed in `STATE.md`.
- [ ] All required CI checks are green on the merge commit.
- [ ] The feature is **reachable by a player** in the running app, or the package says why not.
- [ ] If it changes the world: it saves, loads, undoes, and reports to the budget ledger.
- [ ] No new warning in the build log; no new entry in the forbidden-API scan.
- [ ] No hand-written Unity YAML; no step that needs the Editor GUI.
- [ ] `BACKLOG.yaml` and `STATE.md` updated in the same PR.
- [ ] Anything learned that contradicts the plan is written into the plan.

## 7. What automation cannot tell you

Stated plainly so nobody mistakes a green pipeline for a shippable game:

- Whether it holds frame rate on the device.
- Whether both eyes render correctly, and whether anything shimmers, swims or pops.
- Whether it is comfortable, and whether hand tracking and controllers feel right.
- Whether it looks good, sounds good, or is fun.
- Whether video decodes smoothly on the headset, and whether your StreamBox plays through it.

Until the first device gate, the honest status of every one of these is **unknown**.
