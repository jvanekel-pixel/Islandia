# PLAN

Command Center view of `docs/plan/BACKLOG.yaml`. Phase N is milestone MN in `03-ROADMAP.md`; each task is one work package, in backlog order.
The backlog is the source of truth: tick a box here only when that package is `done` there. Cut packages are not listed.

## Phase 1: Factory Online
Why it matters: Proves Unity can be driven headlessly and CI can build an APK, so nothing later rests on an untested pipeline.
> Verify: `make check` | Done when: `M1-RETRO` is merged and a tagged release carries an APK that CI built from the generated project.
- [x] F-01: Repository skeleton, Makefile, lint, meta generator
- [x] F-08: Island.Core package and dotnet test harness
- [x] F-02: Unity licence activation smoke test in CI
- [x] F-03: Create the Unity project headlessly and pin the editor
- [x] F-07b: EditMode and PlayMode tests in CI
- [x] F-07c: The resolved packages' sources, as an artefact
- [x] F-04: Player, graphics and URP configuration as code, with assertion tests
- [x] F-05: OpenXR and Meta feature configuration, headless and committed
- [x] F-06: Assemblies, composition root, boot scene builder, simulation mode
- [x] F-07a: Run the non-Unity checks in CI on every push
- [x] F-07: The rest of the Unity CI pipeline — caching, path filters, package-source artefact
- [x] F-14: The app's identity in code — package id and name under the icon
- [x] F-09: Release workflow, milestone report and human-gate templates
- [ ] F-10: Cloud environment for this repository (needs-human)
- [x] F-13: Auto-merge finished work
- [ ] F-11: Driver scheduled task and end-to-end loop rehearsal
- [ ] S-01: Spike: Unity inside a cloud session
- [ ] S-02: Spike: software-rendered screenshots in CI
- [ ] S-03: Spike: shader compile gate
- [ ] S-04: Spike: frame-synthesis and refresh-rate APIs present in the resolved packages
- [ ] S-05: Spike: CI cost baseline
- [ ] M1-RETRO: Milestone M1 report, retro and release

## Phase 2: First Playable
Why it matters: One of everything end to end freezes the patterns every later feature copies.
> Verify: `make check` | Done when: `V-13` journey test is green in CI and `M2-RETRO` has published the release and opened the HG-1 headset issue.
- [x] C-01a: Catalogue schema, loader and deterministic id generation
- [x] C-01b: Catalogue validator, licence ledger and honest count
- [x] C-02a: Pinned headless Blender, and proof it can do the work
- [ ] C-02b: Blender normalisation pipeline
- [ ] C-02c: Pack ingestion from the owner's asset waves, and licence archiving
- [ ] C-02d: Private-use packs stay out of the public repository
- [ ] V-01: Input abstraction, simulation driver, XR input adapter
- [x] V-02: Terrain model in Core — heightfield, brushes, weights, dirty regions
- [ ] V-03: Terrain runtime — chunk meshes, Burst jobs, async colliders
- [ ] V-04: Terrain and water shaders, heightfield-driven shoreline
- [ ] V-05: Lighting, sky and time of day
- [x] V-06: Save system v1 in Core
- [x] V-07a: Budget ledger and device tier classifier (engine-free)
- [ ] V-07b: Apply the tier settings at runtime
- [x] V-08: Undo/redo core with terrain region deltas
- [ ] V-09: Sculpt tool end to end
- [ ] V-10: Minimal catalogue and placement
- [ ] V-11: Wrist panel v1
- [ ] V-12: Benchmark autopilot and device gate kit
- [ ] V-13: M2 journey test
- [ ] V-14: Media probe — can this app play HLS on the headset at all
- [ ] M2-RETRO: Milestone M2 report, retro, release and HG-1 issue

## Phase 3: Sculptable Island
Why it matters: The ground is what everything else stands on; its brushes, undo and budget must hold before breadth is added.
> Verify: `make check` | Done when: `M3-RETRO` is merged with the terrain budget test green in simulation.
- [ ] W-01: Full brush set, painting and island outline
- [ ] W-02: Terrain undo at scale
- [ ] U-01: Dual-scale rig
- [ ] U-02: Locomotion and comfort
- [ ] W-03: Water, ponds and submerged rules
- [ ] W-04: Terrain performance pass in simulation
- [ ] A-01: Audio core and ambient bed
- [ ] M3-RETRO: Milestone M3 journey, report, retro, release

## Phase 4: Islands and the Train
Why it matters: Arriving by train and making new islands is half the heart of the gift, so it comes straight after the foundations.
> Verify: `make check` | Done when: `M4-RETRO` is merged: a simulated player rides between two islands with only one island resident.
- [x] TR-01a: Island seeds in Core — starting terrain, station site and its reservation
- [x] TR-01: A world of islands in Core — world index and one save file per island
- [ ] TR-02: One island at a time — load, unload and memory
- [ ] TR-03: Station, platform and the track off the island
- [ ] TR-04: Riding the train between islands
- [ ] TR-05: The departure board — choose, name and make islands
- [ ] AV-01: The generic avatar
- [ ] M4-RETRO: Milestone M4 journey, report, retro, release

## Phase 5: Roller Coasters
Why it matters: Building and riding coasters is the other half of the heart of the gift and carries the biggest comfort risk.
> Verify: `make check` | Done when: `M5-RETRO` is merged: a coaster is built and ridden in simulation, and the HG-R ride-comfort issue is open.
- [x] RC-01a: Coaster track pieces and frames in Core — derived from the pack, composed exactly
- [x] RC-01b: Coaster validation in Core — closed, one station, lift, clearance, supports, reservation
- [ ] RC-01: Coasters saved and edited with undo
- [ ] RC-02: Ride physics in Core — speed, forces and intensity
- [ ] RC-03: Track, supports and cars in Unity
- [ ] RC-04: The coaster builder
- [ ] RC-05: Riding a coaster
- [ ] M5-RETRO: Milestone M5 journey, report, retro, release and HG-R issue

## Phase 6: Island Media
Why it matters: StreamBox playing natively in-world is a fixed requirement and its headset risk must be retired early.
> Verify: `make check` | Done when: `M6-RETRO` is merged: pair, browse, watch and music all pass against the mock gateway, and the HG-2 issue is open.
- [x] SB-01a: StreamBox contract snapshot and mock gateway
- [x] SB-01b: StreamBox models and tolerant JSON mapping in Island.Core
- [x] SB-01c: StreamBox client and HTTP transport in Island.Core
- [x] SB-01d: Connect socket transport and remote control
- [ ] SB-02: Media player backend
- [ ] A-04: Screen and speaker props
- [ ] SB-03: Pairing and sign-in
- [ ] SB-04: Library browser
- [ ] SB-05: Watching
- [ ] A-02: Radio and speakers — local files and bundled loops, multi-speaker
- [ ] SB-06: StreamBox music on radios and speakers
- [ ] SB-07: Islandia as a Connect device
- [ ] M6-RETRO: Milestone M6 journey, report, retro, release and HG-2 issue

## Phase 7: Living Garden
Why it matters: Planting from the owner's packs is the first proof the content pipeline delivers things worth placing.
> Verify: `make check` | Done when: `M7-RETRO` is merged with the foliage budget test green in simulation.
- [ ] W-10: Foliage instance store, culling and LOD
- [ ] W-11: Foliage shader
- [ ] W-12: Scatter-and-grow brush and terrain following
- [ ] W-13: Grass
- [ ] C-04: Plant batch 1 from the owner's packs
- [ ] U-10: Inventory browser
- [ ] M7-RETRO: Milestone M7 journey, report, retro, release

## Phase 8: Builder's Kit
Why it matters: Structures and full save coverage make an island keepable, which every later feature assumes.
> Verify: `make check` | Done when: `M8-RETRO` is merged: a built structure survives save, reload, undo and glTF export in CI.
- [ ] B-01: Module and socket model, snap solver
- [ ] B-02: Studio tools — select, move, rotate, scale, multi-select, delete
- [ ] B-03: Architecture shader and shared palette material
- [ ] C-05: Architecture batch 1 from packs — one complete theme, gazebos, props
- [ ] B-04: Draw-call strategy for structures — measure, then decide
- [ ] B-05: Foundations and stilts on slopes and water
- [ ] B-06: glTF export
- [ ] P-10: Save coverage, slots, autosave
- [ ] U-11: Toybox and studio modes
- [ ] M8-RETRO: Milestone M8 journey, report, retro, release

## Phase 9: Bring Your Own Models
Why it matters: The import path is the only one that crosses a trust boundary, so it is built and proven on its own.
> Verify: `make check` | Done when: `M9-RETRO` is merged: a model converted in the browser test suite is received and placed in simulation.
- [x] I-01: GLB gatekeeper in Core
- [ ] I-02: On-device loader
- [ ] I-03: Imports folder
- [ ] I-04: Browser converter
- [ ] I-05: Relay service
- [ ] I-06: Headset pairing and receive flow
- [ ] I-07: Deploying the companion and relay
- [ ] M9-RETRO: Milestone M9 journey, report, retro, release

## Phase 10: Paint Anything
Why it matters: Painting touches every asset type, so it lands only once all of them exist.
> Verify: `make check` | Done when: `M10-RETRO` is merged: fill and brush paint survive save, reload and undo on pack and imported models.
- [ ] PNT-01: Mesh segmentation in Core — paint regions that respect edges
- [ ] PNT-02: Paint model in Core — fills, brush, undo and save
- [ ] PNT-03: Segmentation in both pipelines
- [ ] PNT-04: Paint rendering
- [ ] PNT-05: Paint tools — fill, brush, eyedropper, palette
- [ ] PNT-06: When the regions are wrong — sensitivity and split/merge
- [ ] M10-RETRO: Milestone M10 journey, report, retro, release

## Phase 11: The Island Cat
Why it matters: The cat is the last new feature; finishing it closes the scope before hardening starts.
> Verify: `make check` | Done when: `M11-RETRO` is merged: the cat is met, fed and befriended in simulation within the one-cat budget.
- [ ] CAT-01: The cat, its collar and its dishes — content
- [ ] CAT-02: The cat in Core — befriending and behaviour
- [ ] CAT-03: How the cat gets around
- [ ] CAT-04: Meeting, feeding and keeping the cat
- [ ] U-20: Hand tracking for core tools
- [ ] M11-RETRO: Milestone M11 journey, report, retro, release

## Phase 12: Feature Complete (Alpha)
Why it matters: Hardening against stress islands stops budget drift before any device time is spent.
> Verify: `make check` | Done when: `M12-RETRO` is merged with the stress suite inside every budget and the HG-3 full device pass issue open.
- [ ] Q-01: Quality scaler
- [ ] Q-02: Stress islands and performance regression suite
- [ ] Q-03: Robustness
- [ ] Q-04: Onboarding, settings, accessibility
- [ ] Q-05: Security, privacy and licence review
- [ ] M12-RETRO: Alpha report, retro, release and HG-3 issue

## Phase 13: Content Complete (Beta)
Why it matters: Device findings and the remaining content are fixed here so the final build only needs signing.
> Verify: `make check` | Done when: `M13-RETRO` is merged with every HG-3 finding closed and the HG-4 art, comfort and soak issue open.
- [ ] D-TRIAGE-ALPHA: Triage the alpha device report into fix packages (needs-human)
- [ ] A-03: Optional: internet radio tuner (needs-human)
- [ ] C-10: Later asset waves, island theme by island theme
- [ ] C-11: Architecture themes 2 and 3
- [ ] C-12: Gazebos, rocks, outdoor props, media and cat props
- [ ] Q-10: Visual polish pass
- [ ] Q-11: Soak and leak hunt
- [ ] Q-12: Strings externalised
- [ ] M13-RETRO: Beta report, retro, release and HG-4 issue

## Phase 14: On the Headset
Why it matters: The signed build and hand-over notes are what actually get given; nothing counts until this is installed.
> Verify: `make check` | Done when: `M14-RETRO` is merged: the signed APK is installed on the owner's Quest 2 and the upkeep task is running.
- [ ] R-01: Signed, installable build
- [ ] R-03: Relay readiness
- [ ] R-11: Hand-over notes and monthly upkeep
- [ ] M14-RETRO: It is finished — report, retro, release
