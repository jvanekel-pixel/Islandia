# 03 — Roadmap: lifecycle, stages, milestones

## 1. The lifecycle model

**A risk-first stage-gate at the top, continuous flow underneath.**

- **Stage-gate (macro).** The classic game-production sequence — pre-production, production,
  alpha, beta, release candidate, live — because it matches how this product matures: prove the
  pipeline, prove one of everything, build breadth, fill content, harden, ship. Each stage ends at
  a named milestone with objective exit criteria and a published release.
- **Risk-first ordering (from the spiral model).** The things most likely to kill the project —
  whether Unity can be driven headlessly at all, whether the app boots in a headset — are attacked
  in the first two milestones, not the last. The blueprint did the opposite: it deferred every
  proof to the final phase.
- **Verification paired with specification (from the V-model).** Every requirement names the level
  that proves it before any code is written (`04-VERIFICATION.md`).
- **Continuous flow (micro).** No sprints. One driver pulls the next unblocked work package from an
  ordered backlog, with a work-in-progress limit of one, trunk-based, merging on green.

How the classic SDLC phases map:

| SDLC phase | Where it lives here |
| :-- | :-- |
| Requirements | The blueprint as brief; `02-ARCHITECTURE.md`; acceptance criteria in the backlog |
| Design | `02-ARCHITECTURE.md`; the vertical slice (M2) freezes the patterns |
| Implementation | Work packages |
| Verification | Continuous (L0–L5); device and human gates at stage boundaries |
| Deployment | Release workflow at every milestone; a signed APK installed on the headset at M11 |
| Maintenance | M11 runbook and a monthly scheduled maintenance task |

Why not the blueprint's twelve phases as they stand: they are a waterfall by subsystem with all
verification at the end, they order persistence last although everything depends on it, and they
need a human at every boundary.

## 2. Stages and milestones

Every milestone ships a tagged GitHub Release with an installable APK and a report.

| Stage | Milestone | You can… | Exit criteria | Human gate |
| :-- | :-- | :-- | :-- | :-- |
| 0 Orientation | **M0 Keys handed over** | — | Repository, secrets and decisions in place; driver scheduled | **HG-0** (15–30 min) |
| 1 Inception | **M1 Factory online** | Download an APK that CI built from an empty XR project | Headless project creation, configuration tests, full CI, release workflow, engine-free core with tests, all spikes answered | — |
| 2 Pre-production | **M2 First playable** | Sculpt, dig a pond, place a plant and a wall, save, reload, undo — in simulation and in an APK | One of everything end to end; patterns frozen; device gate kit and media probe ready | **HG-1** first headset check (recommended, non-blocking) |
| 3 Production | **M3 Sculptable island** | Shape and paint the whole island at both scales and move around comfortably | Full brush set, terrain undo, dual scale, locomotion, water, Quest 2 terrain budget | — |
| | **M4 Island media** | Pair with your StreamBox, browse it on an in-world screen, watch, and play your music through island speakers; drive it from your phone | Contract client, pairing, browser, video, music, Connect — all green against the mock gateway | **HG-2** StreamBox in-world check (recommended, non-blocking) |
| | **M5 Living garden** | Paint meadows and plant real trees from a browsable inventory | Content pipeline, foliage engine, scatter brush, first botanical batch | — |
| | **M6 Builder's kit** | Build, edit and export structures | Module system, snapping, studio tools, first architecture theme, full save coverage | — |
| | **M7 Bring your own models** | Convert a model in a browser and place it on the island | Gatekeeper, loader, imports folder, web converter, relay (local) | — |
| | **M8 Island companions** | Adopt, customise and live with pets | Pets, navigation, behaviour, hand interaction | — |
| 4 Alpha | **M9 Feature complete** | Use every feature on a stress island without breaking a budget in simulation | Scaler, stress suite, robustness, onboarding, security and licence review | **HG-3** full device pass |
| 5 Beta | **M10 Content complete** | See the full catalogue; device findings fixed | Catalogue targets met; device-driven optimisation; soak; polish | **HG-4** art, comfort and soak review |
| 6 Finished | **M11 On the headset** | Give it to them | Signed installable build, hand-over notes, maintenance task running | — |

Media is fourth, ahead of the garden and the building kit, for two reasons: the owner called it
imperative, and it carries the largest risk that only a headset can retire.

## 3. Human gates

Everything the owner does, in one place. Detail and copy-paste commands are in `00-START-HERE.md`.

| Gate | When | Time | What you do | Blocks the build? |
| :-- | :-- | :-- | :-- | :-- |
| **HG-0** | Now | 15–30 min | Create the repository, add the licence secrets, start the driver | Yes — nothing starts without it |
| **HG-1** | After M2 | 30–45 min first time, 10 after | One-time headset developer setup; run the device script; look around for five minutes. Includes the video probe | **No.** Strongly recommended: it is the earliest proof the app renders in a headset, and it decides how video will be played |
| **HG-2** | After M4 | 20–30 min | Pair the headset with your StreamBox; watch something; play an album; try the phone remote | **No.** Recommended: it is the feature you called imperative |
| **HG-3** | After M9 | 60–90 min | Run the device script; play every feature against a checklist; decide on relay hosting | Blocks M10's device-driven work only |
| **HG-4** | After M10 | 60–90 min + an unattended hour | Review art and comfort; start the soak run. **The last gate** | Blocks M11 |

Between gates the driver never waits for you unless every remaining package depends on your answer.

## 4. Tracks

Work packages are prefixed by track. Within the production stage tracks are largely independent,
so a blocked track does not stall the others.

| Prefix | Track | Blueprint phases it replaces |
| :-- | :-- | :-- |
| `F` | Factory: repository, CI, headless project | 1 (setup) |
| `S` | Spikes: time-boxed risk answers | — |
| `V` | Vertical slice | 1, 2, 3 (thin) |
| `W` | World: terrain, water, foliage engine | 2, 4 |
| `U` | UX: rig, tools, undo, HUD, comfort, hands | 3, 11 (comfort) |
| `C` | Content: catalogue, ingestion, generators, batches | 4, 5 (inventory) |
| `B` | Build kit: modules, snapping, export | 5 |
| `P` | Persistence | 6 (schema), 12 |
| `I` | Import: gatekeeper, loader, converter, relay | 6, 7 |
| `A` | Audio core, radio and screen props | 8, 9 |
| `SB` | StreamBox: the owner's media service, native in-world | — (new requirement) |
| `PET` | Pets, navigation, behaviour | 10, 11 |
| `Q` | Quality: scaler, stress, robustness, device kit | 12 |
| `R` | Release and live operations | 12 |
| `D` | Device-finding fixes, created from gate reports | — |

## 5. What sets the pace

Honest constraints rather than a calendar:

- **About 108 work packages** to M11, each sized for one session.
- **CI is free but not instant.** The repository is public, so GitHub-hosted runner minutes cost
  nothing. A Unity test run takes roughly ten to fifteen minutes and an Android build twenty-five
  to forty-five, so a Unity-touching package still spends most of its wall-clock time waiting.
- **Claude usage limits.** Long Opus sessions draw on your plan's session and weekly limits. The
  driver does at most three packages per session and one session at a time. If you hit limits,
  lower the schedule frequency; nothing is lost.
- **The Quest 2.** It is the tightest of the Quest headsets. Expect the device gates to send work
  back; that is what they are for.
- **Human gates.** The build cannot pass HG-3 without you.
- **Unknowns.** If a spike fails or the first device check shows a black screen, the retro at that
  milestone re-plans. That is the plan working, not failing.

## 6. Re-planning points

Each milestone ends with a retro package that must: update the risk register, re-order the
backlog, rewrite anything in `02`–`04` that reality contradicted, and state in the milestone report
what changed and why. The plan is expected to change most at M1, M2 and after HG-1.
