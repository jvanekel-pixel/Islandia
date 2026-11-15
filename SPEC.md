# SPEC — Islandia

A summary for Command Center. The authority is `02-ARCHITECTURE.md` (locked decisions, ADR-01 to
ADR-27) and `03-ROADMAP.md`; if this file disagrees with them, they win and this file is fixed.

## Product Goal

**Islandia** is a single-player Meta Quest 2 sandbox. The player arrives by train at a small
island, sculpts its terrain, plants it, builds on it, builds a roller coaster and rides it, brings
in their own 3D models and paints anything, befriends the island's stray cat, and watches and
listens to the owner's own media library — served by the owner's StreamBox — on screens and
speakers placed in the world. Every island has a station, and the train runs to as many more
islands as the player wants to make.

**Trains and roller coasters are the point.** Where a judgement call is close, it goes to whatever
makes riding and building them better, and to what makes this a nicer thing to be given.

## Target Audience

One person: the owner's partner, who wears the headset, is not a developer, will not read
instructions, and thinks VR is dumb. The owner builds it as a gift and is the only operator. It is
never published and never sold — there is no public, no store listing and no user accounts.

## Deploy Target

- **Meta Quest 2**, standalone Android (72 Hz, Snapdragon XR2 Gen 1, 6 GB). Nothing may depend on a
  newer headset.
- CI builds a signed IL2CPP ARM64 APK; the owner installs it over USB with `adb`. No store.
- Android package id `dev.islandia.app` (final); target SDK 34, minimum SDK 32.
- A static web companion and a thin relay (free-tier hosting, behind a feature flag) for sending
  models to the headset. Not built yet.

## Current Architecture

- **Engine:** Unity `6000.3.28f1` (pinned), URP, OpenXR with the Unity Meta OpenXR package, XR
  Interaction Toolkit, XR Hands. No Meta XR Core SDK.
- **Headless and code-first:** the project, its settings, the boot scene and every build step are
  produced by editor code run in batch mode in CI. Nobody opens the Unity Editor. Unity YAML is
  generated, never hand-written.
- **Layering (ADR-07):** `Island.Core` in `Packages/com.island.core` is engine-free C# tested with
  `dotnet test`; `Island.Runtime`, `Island.XR` and `Island.Editor` in `Assets/Island/` hold the
  Unity-facing code. Gameplay talks to interfaces with simulation implementations, so the game runs
  in PlayMode tests with scripted input and no headset.
- **Composition:** one generated boot scene with a composition root; everything else is built in
  code. No scene-serialised references.
- **Persistence (ADR-09):** a versioned, sectioned, checksummed binary container with atomic
  writes; a world index plus one save file per island.
- **Budgets (ADR-10):** Quest 2 caps of at most 140 draws, 711K triangles and 368 MB in a busy
  scene, enforced by a runtime budget ledger and by tests.
- **Content (ADR-11):** a data-driven catalogue in `content/catalogue/`; assets come only from free
  packs the owner uploads to draft releases, normalised by a pinned headless Blender.
- **StreamBox (ADR-23):** a native client in `Island.Core` behind injectable transports, tested
  against a mock gateway built from the contract snapshot in `contract/streambox/`. StreamBox
  itself is a separate project that this repository never pushes to.
- **CI (ADR-19):** GitHub Actions with GameCI on free GitHub-hosted runners; trunk-based, one work
  package per pull request, landed by an auto-merge workflow once checks pass.
- **Verification:** levels L0 to L7 (`04-VERIFICATION.md`). L0 to L4 run without a human; headset
  and human checks are recorded as `needs-device` or `needs-human` and never faked.

## Already Implemented

28 of 138 live work packages are done (`docs/plan/BACKLOG.yaml`). Verified on 2026-11-15 by file
inspection and a passing `make check`; Unity and APK results are from CI, not re-run here.

- **Factory (M1, 13 of 22):** repository skeleton, `Makefile`, seven lint checks, meta generator;
  the Unity project created headlessly; rendering (ADR-03) and OpenXR configuration set in code and
  asserted from disk; assemblies, composition root, boot scene builder and simulation mode;
  CI for non-Unity checks, EditMode and PlayMode tests, the Android APK build with Library cache,
  package-source artefacts, Unity licence activation and auto-merge; the app identity read back
  from the built APK.
- **Engine-free core (`Island.Core`):** terrain model with brushes, weights and dirty regions;
  save system v1; undo/redo with terrain region deltas; budget ledger and device tier classifier;
  GLB import gatekeeper; island seeds, station site and world index with one save per island;
  coaster track pieces and exact frames derived from the owner's pack.
- **StreamBox client:** contract snapshot, mock gateway, models and tolerant JSON mapping, HTTP
  client, Connect socket transport and remote control — proven end to end against the mock.
- **Content pipeline, less its input:** catalogue schema, loader, deterministic ids, validator,
  licence ledger, honest count, and a pinned headless Blender proven to do the work.

Nothing is playable yet: no terrain is rendered, no tool exists in the headset, and no content has
been ingested.

## Remaining In Scope (MVP)

The MVP is milestone M14, "On the headset". Task-level detail is in `PLAN.md`.

- **Finish M1:** release workflow and gate templates, cloud environment, driver rehearsal, five
  spikes, the M1 report and release.
- **M2 First playable:** input abstraction, terrain runtime and shaders, lighting, sculpt tool,
  minimal placement, wrist panel, content ingestion of asset wave 1, device gate kit, media probe.
- **M3 Sculptable island:** full brush set, dual-scale rig, locomotion and comfort, water, audio.
- **M4 Islands and the train:** one island resident at a time, stations, the train ride, the
  departure board, the generic avatar.
- **M5 Roller coasters:** validation, save and undo, ride physics, track and cars, builder, riding.
- **M6 Island media:** media player backend, screens and speakers, pairing, library browser,
  watching, music, Islandia as a Connect device.
- **M7 Living garden, M8 Builder's kit, M9 Bring your own models, M10 Paint anything, M11 The
  island cat.**
- **M12 Alpha, M13 Beta, M14 Finished:** quality scaler, stress suite, robustness, onboarding,
  security and licence review, later asset waves, polish, soak, the signed build and hand-over.
- **Human gates:** HG-1 first headset check, HG-R ride comfort, HG-2 StreamBox in-world, HG-3 full
  device pass, HG-4 art, comfort and soak review.

## Out of Scope (Anti-Bloat)

Decided by the owner or the architecture; reopening any of these needs a decision record.

- Store release, listing, compliance validation, monetisation, accounts, analytics, telemetry.
- Any headset other than the Quest 2 as a verified target; anything needing a newer headset.
- Anything that costs money: paid assets, services, dependencies or hosting that can bill.
- Spotify, an in-world web browser, and any DRM-protected service.
- Multiple pets, pet customisation and the pet studio (`PET-01` to `PET-06`, cut) — replaced by one
  stray cat per island.
- Character creation — the avatar is generic.
- Parametric content generators (`C-03`, cut) and a fixed asset-count target.
- `.blend` import and a server-side Blender converter.
- Multiplayer of any kind.
- Meta XR Core SDK / OVRPlugin, Git LFS, self-hosted runners, the Unity Editor GUI.
- Changes to StreamBox itself — requested from the owner through a `human-gate` issue.
- The internet radio tuner (`A-03`) unless the owner asks for it; it is first in the cut order.
