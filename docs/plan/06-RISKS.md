# 06 — Risk register

Live document. Updated at every milestone retro and whenever a risk fires. Each risk has a
**pre-agreed fallback** so the driver can act without asking.

Likelihood and impact: H / M / L. "Signal" is the earliest thing that would tell us.

## Project-level

| ID | Risk | L | I | Earliest signal | Mitigation | Pre-agreed fallback |
| :-- | :-- | :-- | :-- | :-- | :-- | :-- |
| RK-01 | Unity cannot be configured for XR entirely headlessly (settings reset, features not enabled) | M | H | `F-05` tests; HG-1 black screen | Generate once, commit the assets, assert in CI | Owner opens the project once on their PC and commits the XR settings (15 min, one time). Recorded as a human gate |
| RK-02 | Personal licence activation fails or expires in CI | M | H | `F-02` | Smoke workflow; clear failure messages | Human-gate issue with re-issue steps; driver continues on engine-free and companion tracks |
| RK-03 | The app does not render correctly in a headset (stereo, tracking, black eye) and nobody finds out for weeks | M | H | HG-1 | Offer HG-1 right after M2; shader rules; committed XR config | Fix packages jump the queue; no new feature work until it boots |
| RK-04 | CI is slow or flaky enough to stall the driver (queue times, licence activation, runner disk) | M | M | `S-05`; repeated `infra` issues | Path filters, cache, engine-free core, cap of four Unity runs per package | Driver switches to tracks that need no Unity (`SB-01`, `I-*`, core logic, content tooling) and opens an `infra` issue |
| RK-05 | Claude usage limits throttle the driver | M | M | Session ends early with a limit notice | Three packages per session; one driver | Nothing is lost; the next scheduled session resumes. Owner lowers the frequency if it recurs |
| RK-06 | An autonomous session merges something wrong and later sessions build on it | M | H | Journey tests regress; retro | Required checks; journeys from every milestone keep running; independent review for high-risk packages; small PRs | Revert the merge commit; add the missing test first; continue |
| RK-07 | The driver drifts from the plan or "fixes" a gate by weakening it | M | H | `DECISIONS.md` entries that lower a threshold; test count dropping | Rules in `CLAUDE.md`; acceptance lines cannot be deleted without a record; CI reports test counts per run | Retro restores the gate; the offending change is reverted |
| RK-08 | **The Quest 2 cannot hold frame rate with everything on the island.** It is the tightest Quest and every budget is a guess until measured | H | H | HG-1, HG-3 | Quest 2 budgets from day one; ledger everywhere; merged terrain regions; scaler; stress islands; budgets recalibrated from data | Reduce caps through data, not code: foliage density, draw distance, pet count, render scale. Features degrade before frame rate does |
| RK-09 | Scope is too large to finish | M | H | Milestone retros slipping | Tracks are independent; each milestone is shippable | Cut order, pre-agreed: internet radio (already optional) → PET track beyond two archetypes → `I-05`/`I-06` relay (keep the imports folder) → architecture themes beyond the first. **StreamBox playback is never cut** |
| RK-10 | Plan documents rot as reality diverges | M | M | Sessions quoting stale sections | Plan edits ride in the same PR; retros rewrite | — |
| RK-11 | Something private is committed to the public repository (server address, token, library content, keystore) | L | H | Secret and address scan in L0 | Scan on every push; server address is device configuration only; mock gateway uses invented fixtures; `.gitignore` covers key and licence files | Rotate whatever leaked; purge history; the driver stops and opens a `human-gate` issue |

## Technical

| ID | Risk | L | I | Earliest signal | Mitigation | Pre-agreed fallback |
| :-- | :-- | :-- | :-- | :-- | :-- | :-- |
| RT-01 | No way to see rendered output before a device gate | H | M | `S-02` | Try software rendering and WebGL | Accept it: visuals are verified only at human gates; HG-1 becomes more important |
| RT-02 | Burst jobs behave differently from the Core reference | L | M | `V-03` differential tests | Differential tests on random input | Use the managed reference where the difference cannot be closed, if it meets the time budget |
| RT-03 | Collider updates hitch on device | M | M | HG-1 | Truly asynchronous bake; update on stroke end | Coarser collision mesh than render mesh |
| RT-04 | Custom foliage instancing underperforms or mis-renders under multiview | M | H | HG-1, HG-3 | Bucket by mesh; stereo-safe culling | GameObjects with LOD groups and instanced materials for trees and bushes; custom path only for grass and flowers |
| RT-05 | CC0 packs do not cover the catalogue, or the mixed styles look incoherent | M | H | `C-02`, `C-04` contact sheets | Palette unification; generators for families | Smaller, coherent catalogue from one creator's packs; owner decides at HG-4 |
| RT-06 | Asset source sites change URLs or terms | M | L | `C-02` checksum failures | Pin URLs and checksums; archive the licence text | Substitute pack; cached copy in CI storage |
| RT-07 | Live audio streaming cannot be made reliable on Quest | H | L | `A-03` spike, if ever built | Not needed: StreamBox music is fetched to a cache and played from it | Leave internet radio unbuilt |
| RT-08 | glTFast on device: stripped decoders, missing shaders, memory spikes | M | H | `I-02` build-report check; HG-3 | Preserve assemblies; always-include shaders; gatekeeper before decode | Restrict imports to uncompressed-texture GLB under a tighter size cap |
| RT-09 | Browser-side conversion is too slow or fails on large or exotic files | M | M | `I-04` fixture corpus | Workers; clear limits; tell the user why | Accept only glTF/GLB in v1; document exporting from each tool |
| RT-10 | No good CC0 rigged animals | M | M | `PET-01` | Survey sources first | Rigid-part pets, honestly presented; fewer archetypes |
| RT-11 | Incremental navigation rebuild is too slow or too coarse | M | M | `PET-04` | Region-scoped rebuild off the main thread | Grid path-finding over the heightfield with stamped obstacles |
| RT-12 | Dual-scale rig causes discomfort or precision problems | M | M | HG-1, HG-3 | Fixed pivot; fade on transition; unscaled clip planes | Tabletop as a separate miniature view rather than rig scaling |
| RT-13 | XR Interaction Toolkit behaviour differs between simulation and device | M | M | HG-1 | Thin adapter; journeys cover logic only | Device-found bugs become `D-*` packages with a simulated reproduction where possible |
| RT-14 | An editor or package update breaks the build | L | M | CI | Exact pin; lockfile committed | Stay on the pin; upgrade only by decision record and device gate |
| RT-15 | Build size or memory exceeds device limits once content is complete | M | M | `L4` size report trend | Track APK size per commit; texture budgets in the validator | Lower texture resolution tier; split optional content |
| RT-16 | **Video will not decode to a texture on a Quest 2 under Vulkan with Unity's player and an HLS address** | M | H | `V-14` at HG-1 | Probe in M2 with a self-served test stream; `IMediaPlayer` abstraction | Native player plugin; if that fails too, a progressive stream — one new route requested from the StreamBox side (`07-STREAMBOX.md` §4) |
| RT-17 | StreamBox's client contract changes under Islandia | M | M | HG-2, HG-3; contract tests after a snapshot refresh | Tolerant client; snapshot records its source commit; mock built from the snapshot | Refresh the snapshot and fix; nothing ships to anyone but the owner, so breakage is visible immediately |
| RT-18 | A playing 1080p video on a busy island pushes the Quest 2 over its frame budget | M | M | HG-2 | One screen at a time; media row in the ledger; measured at the first gate | Lower the requested resolution or bitrate through the capability profile; dim or simplify the scene around an active screen |
| RT-19 | Video looks soft or shimmers on a mesh at Quest 2 resolution | M | M | HG-2 | Mip bias and screen sizing tuned on device | Composition-layer quad spike |

## External and legal

| ID | Risk | L | I | Earliest signal | Mitigation | Pre-agreed fallback |
| :-- | :-- | :-- | :-- | :-- | :-- | :-- |
| RX-01 | Store requirements change before submission | M | M | `R-01` re-read | Requirements re-read from the primary source at M11, not trusted from this plan | Fix packages before HG-5 |
| RX-02 | An asset's licence turns out not to be what the pipeline recorded | L | H | `Q-05` licence audit | Only CC0; licence text archived with checksum | Remove the asset; the catalogue is data |
| RX-03 | Linking to third-party radio stations draws a complaint | L | M | Post-launch | User-chosen stations only; nothing bundled or branded | Remove the tuner in a point release |
| RX-04 | The relay is abused as free file hosting | M | M | Relay metrics | Token required; 24-hour expiry; size cap; rate limits; cost cap | Turn the relay off; imports folder still works |
| RX-05 | Children's-privacy or content rules apply in ways not anticipated | L | H | `R-03` | No accounts, no data collection, no sharing between users | Owner takes advice before submission; flagged in the go/no-go pack |
| RX-06 | The Unity Personal licence terms stop fitting (revenue threshold, CI use) | L | H | Owner | Flagged at HG-0 and HG-5 | Owner upgrades the licence |
| RX-07 | Islandia needs something StreamBox does not offer, and the StreamBox project's own process delays it | L | M | `M4-RETRO` cross-repository note | First version designed to need no server change | Carry on with what the existing contract allows; the owner schedules the change in StreamBox |
| RX-08 | Meta stops accepting new Quest 2 submissions (announced for after December 2027) before the game is ready | M | M | `R-01` re-read | Dates re-checked at M11 | Distribute by side-loading or a release channel; it is the owner's own headset first |

## Fired

*(none yet — when a risk fires, move its row here with the date, what happened and what was done)*
