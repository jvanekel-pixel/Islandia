# 01 — Audit of the original blueprint

**Subject:** *Executive Blueprint: High-Fidelity Meta Quest Island World-Builder* (15,125 lines,
1.4 MB, 64 embedded code files, 12 phases). Now split into `docs/blueprint/` and
`reference/drafts/`.

**Method:** every phase was read in full, code included, by independent reviewers; the highest-risk
API claims were checked against URP and glTFast source and against current vendor documentation;
this cloud environment was probed directly. Nothing in the blueprint was compiled or run by its
author, and nothing was compiled here either — there is no Unity in this container. Per-phase
detail, with line references, is in `docs/plan/audit/`.

## 1. Verdict

The blueprint is a **strong design brief and a weak implementation**. Its rendering strategy for
tile-based mobile GPUs is well reasoned and worth keeping. Its code is API-shaped scaffolding that
mostly compiles in principle but, feature by feature, does between 30% and 50% of what the prose
around it claims. Its build process cannot be executed by anyone, human or agent, as written.

Three things would have sunk a literal execution of it:

1. **Every phase closes with "click this menu, press Play, put on the headset."** There are no
   tests, no batchmode steps, no assembly definitions. The setup wizards do not open or save a
   scene, so in a headless run their work evaporates. All eleven written phases are marked "Code
   Delivered" and zero are marked verified.
2. **The headline features are stubs.** No composition layer exists. Web video is a log statement.
   Pets are ten cubes on a sine wave. The NavMesh is built and never used for pathing. The cloud
   converter's sandbox is a comment. Nothing on the headset can receive an uploaded model.
   Save/load — which everything depends on — is promised for a phase that was never written.
3. **The 631-item inventory is about six shapes.** Every tree is the same cone stack, every
   building module the same box.

None of this is fatal to the project. It changes what the plan is: not "port twelve phases," but
"build a verified product using the blueprint as a detailed brief."

## 2. Findings that change the plan

| # | Finding | Severity | What the plan does |
| :-- | :-- | :-- | :-- |
| 1 | Verification is 100% manual and on-device; no tests exist | Blocker for autonomy | Verification ladder with six automated levels; device checks scripted and batched into four human gates (`04-VERIFICATION.md`) |
| 2 | All assembly, wiring and configuration assumes the Editor GUI | Blocker for cloud | Code-first project: one generated boot scene, runtime composition, every setting produced and asserted by batchmode code (ADR-06) |
| 3 | No editor version satisfies the document (asks for non-existent LTS streams, pins the 6.0 URP line, later says 6.3, relies on 6.6-only features) | High | Pin Unity 6000.3 LTS; core packages follow the editor; lockfile committed (ADR-01) |
| 4 | Inventory is ~6 geometries / ~27 looks, not 631 items | High — it is the product | A content track: CC0 packs plus parametric generators, licence ledger, budget validator, honest counts (ADR-11) |
| 5 | Save format is a stub with no producers, consumers, versioning or atomic writes; the orchestrator was deferred to an unwritten phase | High | Persistence designed first, in the vertical slice; every feature ships its save section (ADR-09) |
| 6 | Cloud converter: open to anyone, enumerable pairing codes, no sandbox, fail-open texture cap, and no receive path on the headset | High (security) | Replaced: browser-side conversion, a thin relay that never parses files, the headset as trust boundary (ADR-12/13) |
| 7 | Spotify cannot be shipped to the public (development mode is capped at five users; leaving it needs a registered business with 250k monthly users) | High (scope) | Removed. **The owner's own StreamBox library replaces it**, natively in-world (ADR-14, ADR-23, `07-STREAMBOX.md`) |
| 8 | The bundled SomaFM streams are not licensed for third-party apps | High (legal) | Removed. User-chosen stations from an open directory, plus CC0 loops and the user's own files (ADR-14) |
| 9 | Water foam depends on reading depth through a mechanism that does not work as written and that CI could never exercise | High | Foam computed from the terrain heightmap; identical everywhere and testable (ADR-05) |
| 10 | Application SpaceWarp is driven through private entry points, and its trigger logic latches on permanently | Medium | Behind an interface, off by default, revisited after the first device gate (ADR-18) |
| 11 | Vuplex web view is a paid per-seat asset and its integration is two lines; Netflix cannot work | Medium | Removed. Screens are StreamBox screens; the playback backend is chosen from a device measurement (ADR-15) |
| 12 | The blueprint spreads effort across four headsets; the owner has one, a Quest 2, which Meta moved to end-of-feature-development on 2 Nov 2026 | Medium (scope) | **Quest 2 is the single design and test target** (owner decision). Other tiers are data only and unverified (ADR-02) |
| 13 | Two hardware-tier detectors, both fragile; one may classify every Quest 3 as a 3S | Medium | One pure classifier with a table, unit-tested, confirmed at the first device gate |
| 14 | Retro-fitted "remediation" items contradict earlier phases (lighting model, water level, tiers, package names), and five numbered items are cited but never defined | Medium | One consolidated baseline (`02-ARCHITECTURE.md`) supersedes all phase text |
| 15 | Phase 12 is one paragraph | Medium | Its contents are distributed: scaler, stress scenes, soak, store validator, CI perf gate are real work packages |
| 16 | Performance budgets are asserted, never measured; the perf monitor can pass a gate with a reading of zero | Medium | Budget ledger enforced in tests; numbers treated as hypotheses until device data replaces them |
| 17 | Shaders claim SRP Batcher compatibility while borrowing passes that break it; runtime-toggled keywords would be stripped from builds | Medium | Shader smoke test in CI; own motion-vector passes; `multi_compile` for runtime toggles |
| 18 | Several features have no way to be invoked at all (snap solver, architecture catalogue, pairing-code request, teleport) | Medium | "Reachable by a user in the running app" is part of the Definition of Done |

## 3. What is sound and kept

- The **rendering baseline**: Vulkan, URP forward, render graph, single-pass multiview, 2× MSAA,
  no HDR / depth texture / opaque texture / post-processing, SRP Batcher, foveation.
- The **world model**: a 64 m × 64 m island, 8 × 8 chunks, 33² vertices per chunk, one shared
  257² heightfield so seams cannot crack, vertex-colour material weights. The grid indexing and
  double-buffer logic in the terrain draft check out.
- **Opaque, instanced, vertex-coloured foliage** with palette look-ups instead of alpha cards.
- **No baked lighting**: one directional light, trilight ambient, vertex ambient occlusion.
- The **dual-track UX** idea: a toybox mode and a studio mode, and a tabletop scale.
- **Per-tier hard caps** on screens, pets and audio decoders, and a single budget table.
- The **troubleshooting tables** in each phase, which are genuinely useful as known-pitfall lists.

## 4. Scope: claimed versus present

| Area | Blueprint claims | What the drafts contain |
| :-- | :-- | :-- |
| Engine setup | Automated configurator and validator | Player and partial URP settings; no OpenXR setup, no asset or scene creation |
| Terrain | Zero-GC, never-stalling sculpting | Sound core; collider bake blocks the main thread; pad leveller wrong |
| Water | On-tile depth foam | Not functional as written |
| UX | Two modes, gizmos, multi-select, compositor HUD, undo | Grid snap and axis drag; no terrain undo; HUD is a plain quad |
| Foliage | 398 varieties | 4 meshes; batching keyed so the draw-call gate cannot pass |
| Architecture | 233 items, snapping, welding | 2 meshes; snap solver uncalled; weld-nearby always returns null |
| Model import | Sandboxed cloud conversion, streamed to headset | Insecure server; headset never receives anything |
| On-device loading | Spike-free streaming | Compiles against real APIs; budget misconfigured, textures flipped, shaders stripped |
| Screens | DRM web video on compositor layers | Direct-URL video player; no web view, no layer |
| Radio | Live radio, multi-speaker sync, Spotify | Local files and a synth loop plausible; live streams break after ~64 KB |
| Pets | Rigged, skinned, customisable | Ten cubes each; no rig, animation, UI or load path |
| Pet AI | Utility AI on a chunked NavMesh | State machine with straight-line movement |
| Comfort and hands | Teleport, snap turn, vignette, pinch | Snap turn, seated offset, proximity petting in the wrong space |
| Save/load | Versioned round trip | Data classes only |
| Final audit | Scaler, soak, CI perf gate, store validator | Not written |

## 5. What this cloud environment can and cannot do

Probed directly on 2026-11-10:

| Fact | Consequence |
| :-- | :-- |
| 4 vCPU, 16 GB RAM, ~30 GB disk, Ubuntu 24.04; Node 22, Python 3.13, Java 21, Docker present | Enough for the companion stack, the engine-free C# core, and content tooling |
| No Unity, no .NET SDK, no Blender installed | Added by the environment setup script (`F-10`) |
| The Docker daemon starts; Docker Hub is rate-limited; `mirror.gcr.io` works, and a pull of a GameCI editor image through it transferred several GB before it was stopped (not run to completion) | A Unity editor image can probably be pulled into a session in minutes — confirmed or refuted by spike `S-01` |
| Unity's download, package-registry and licensing hosts are blocked by the default network policy | Unity runs in GitHub Actions by default. Running it in-session needs allow-listed hosts and a licence the owner places in the environment — an optional accelerator, not a dependency |
| No headset, no GPU | Rendering, frame time, tracking, comfort and DRM can only be proven at human gates |
| Sessions cannot push tags or delete branches; `gh pr` / `gh issue` do not work (GraphQL blocked) | Releases are cut by a workflow; PRs and issues go through built-in GitHub tools or REST |
| Scheduled tasks fire at most hourly and start fresh sessions; idle VMs pause | The driver loop is resumable from the repository alone (`05-OPERATIONS.md`) |

## 6. Things only the owner can supply

Collected here so nothing surprises you later. Only the first block is needed now.

| When | What | Why |
| :-- | :-- | :-- |
| **Orientation** | GitHub repository; Claude GitHub App on it; Unity Personal licence file and login as Actions secrets | Nothing can start without these |
| First device gate | Meta developer organisation, developer mode on the headset, `adb` on a PC | Installing and measuring builds |
| Model-relay deployment | A hosting account and a deploy credential | Only for import over the internet; the imports folder works without it |
| StreamBox check | Your server's address typed once in the headset; approving the pairing on your phone | Proving your library plays in-world |
| Release | Signing keystore; Meta app ID and upload credential; privacy policy sign-off; store listing | Publishing |
| Ongoing | Renewing the Unity licence file when it expires | The driver opens an issue when it happens |

## 7. Addendum — the owner's decisions and StreamBox (2026-11-10)

After this audit the owner set three things: the Quest 2 as the only target, a public repository
with no running cost, and a hard requirement that their own media service works natively in-world.

That service, **StreamBox**, was read at commit `9ebfdd6`. It is a mature gateway in front of
Jellyfin, Navidrome and Audiobookshelf, with a typed client contract. Three of its existing
features make a headset client practical with no server change: device-code pairing for TVs, a
stream broker that returns short-lived HLS addresses needing no headers, and a remote-control
socket. The integration is specified in `07-STREAMBOX.md` and built in milestone M4.

What remains unproven is decoding HLS to a texture on a Quest 2 under Vulkan. A probe built into
M2 settles it at the first headset check, with two fallbacks.

## 8. Residual uncertainty

- Version numbers, store rules and vendor terms in this audit come from documentation search
  summaries dated November 2026, not from full reads of every primary page. The first factory
  work packages re-verify the ones that matter by actually resolving and building.
- Confidence percentages in the per-phase files are reviewers' estimates from reading code.
- Every performance number in the blueprint is a hypothesis until the first device gate.
- If the separate "Remediation Plan" the blueprint cites still exists, add it to `docs/blueprint/`;
  five of its numbered items (P2 #15, #16; P3 #20, #22, #23) are referenced but never described.
