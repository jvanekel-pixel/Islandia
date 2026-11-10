# Audit detail — blueprint Phases 8–12 (cinema screens, radio, pets, AI, final audit)

Sources: `docs/blueprint/phase-08.md` … `phase-12.md`, `90-original-state-record.md`; drafts under
`reference/drafts/Assets/`. Findings are from reading; nothing was compiled.
Line numbers (L…) refer to the original single-file document; treat them as approximate pointers.

## Verdict

**The C# will mostly compile, but the headline features are stubs.** The composition layer and web
playback do not exist in code, the "rigged pets" are ten Unity cubes, and the NavMesh is built but
never used for pathing.

| Phase | C# compiles | Shaders | Works as specified |
| :-- | :-- | :-- | :-- |
| 8 Cinema | ~95% | ~85% | ~30% |
| 9 Radio | ~95% | ~95% | ~50% (procedural loop and local files plausible; live radio and multi-speaker no) |
| 10 Pets | ~95% | ~90% | ~40% (pipeline runs; output is box pets) |
| 11 AI | ~90% | — | ~35% |
| 12 Audit | 0% — nothing written | — | 0% |

All four setup wizards are public static methods, so `-executeMethod` works, but none opens or
saves a scene, so their scene edits are lost in batchmode.

## Phase 8 — cinema screens

Drafts: `Shaders/CompositorVideoScreenPunchThrough.shader`,
`Scripts/Media/{VuplexWidevineDrmBootstrapper, VirtualCinemaManager, VirtualCinemaScreenController,
OpenXrCinemaQuadLayerRouter}.cs`, `Scripts/Editor/Phase8SetupWizard.cs`.

Stated gates: frame time within budget with a 1080p layer; max active screens 1 or 2 by tier;
primary 1920×1080, secondary 1280×720; media memory ceiling 96/128 MB; auto-sleep beyond 35 m.
CI-provable: URL classification, LRU cap and eviction, render-texture sizes, distance sleep.
Headset-only: everything about crispness, DRM, browser handoff and frame time.

Defects:

1. **No composition layer exists.** The wizard creates a bare GameObject and the router moves it.
   The phase's central gate is unimplemented. The package is in the manifest with zero references.
2. **Web playback is a `Debug.Log`** in both `#if` branches. YouTube, Vimeo and the DRM test page
   "activate" a screen that shows nothing, even with Vuplex installed. The whole integration is
   two static calls.
3. The primary screen renders as a black hole: the Editor guard is not a real shader macro, the
   keyword is toggled on a shared material so screens clobber each other, the property block value
   is never read, and the `shader_feature_local` variant is stripped from player builds.
4. `UsePass …/XRMotionVectors` in three shaders: unverified, and breaks SRP Batcher compatibility.
5. URL routing is a substring match (`max.com` matches `imax.com`).
6. URLs with query strings fail the `.mp4` test and fall into the no-op web path.
7. `prepareCompleted` handlers accumulate on every load.
8. Prose still promises a PC WebRTC cast and Twitch/Jellyfin bookmarks that do not exist.

## Phase 9 — box radio

Drafts: `Shaders/BoxRadioVuMeter.shader`,
`Scripts/Audio/{PrimaryAudioCatalogAndScanner, SpatialRadioNetworkManager,
SpatialBoxRadioController}.cs`, `Scripts/Editor/Phase9SetupWizard.cs`.

Stated gates: total audio CPU < 5%; 0 B/frame GC; one active stream decoder; 24 real / 64 virtual
voices; DSP buffer 512; rolloff 0.85–24 m.
CI-provable: folder scan, station cycling, procedural clip, fallback on unreachable URL, audio
settings. CI has no audio device, so VU-meter assertions read zero.

Defects:

9. The stream opener wraps the request in `using` and returns the streaming clip; the request is
   disposed on return, so a live stream dies after the first ~64 KB.
10. One streamed clip shared by N audio sources with sample-position sync: a network stream is not
    seekable, so the sync never runs. The "single decoder, in phase" claim is unsupported.
11. A live stream through `DownloadHandlerAudioClip` grows memory without bound.
12. Registration race between radios and the manager; the starter radio may stay silent.
13. Every radio polls the keyboard, so N radios toggle N times.
14. "Physical VR knobs" have no interactable components.
15. `file://` paths are built without escaping.
16. Three ambient stems promised, one implemented; five station genres named, three present.

## Phase 10 — pets

Drafts: `Shaders/PetCoatPatternLut.shader`,
`Scripts/Pets/{PetArchetypeDatabase, IslandPetController, IslandPetSanctuaryManager}.cs`,
`Scripts/Editor/Phase10SetupWizard.cs`.

Stated gates: 6 pets < 1.0 ms GPU; cap 4 or 6 by tier; ≤ 20 bones; 1,350–1,800 triangles with 3
LODs; 4 blend shapes; one shared material.

Defects:

17. **There is no rig and no animation.** Each archetype is the same ten cube primitives (~120
    triangles) with a different scale vector; the macaw and the dragon are box quadrupeds. Seven
    bones, each cube rigidly weighted to one. Motion is a sine wave on local rotation. Leg bones
    sit at the leg centre, so legs spin about their middle. No Animator, clips or LODs.
18. The coat shader uses the skinned position as the bind position, so patterns will swim.
19. The wizard is not idempotent and can leave prefabs with stale meshes.
20. The collar "name" is a bit pattern from a hash code, not text.
21. Save is lossy and one-way; there is no load path.
22. There is no customisation UI.

## Phase 11 — AI, navigation, comfort, hands

Drafts: `Scripts/AI/{ChunkedDynamicNavMeshManager, PetFetchableToy, PetHarmonyCoordinator}.cs`,
`Scripts/UX/VRComfortAndHandTrackingManager.cs`, `Scripts/Editor/Phase11SetupWizard.cs`.

Stated gates: AI < 0.5 ms/frame; 4 Hz tick; 0 B GC; zero main-thread NavMesh block.
CI-provable: state transitions, waypoint validation, NavMesh build completion, tick duration (not
Quest-representative).

Defects:

24. **The NavMesh is never used for pathing.** Movement is a straight line with a height clamp, so
    pets walk through buildings and hover across ponds.
25. **"Chunked" is false.** The modified-chunk list is ignored and every stroke runs a synchronous
    whole-island source collection on the main thread, including pets and the player.
26. The tree for paired play is hard-coded at one coordinate.
27. The utility "formula" is constants plus a random number.
28. Hand joint poses are in rig space but written as world positions — wrong after any teleport,
    snap turn or scale change.
29. Pinch grab/throw, arc teleport and the vignette do not exist; only the F key throws the ball.
30. The fetched toy attaches to the pet root; pets gather behind the screen.

## Phase 12 — final audit

One paragraph, zero code. Promised and written nowhere: the quality scaler, the audit harness and
thermal soak, **the save/load orchestrator**, the CI performance regression gate, the store
pre-submission validator, and the profiling procedure.

## Trailing project-state record

Stale: it is the pre-remediation record ("Ready for Phase 1 Code Implementation"), still listing
the PC WebRTC cast, a Spotify controller interface and file names that match nothing delivered.
It must not seed anything.

## External dependencies

| Dependency | Position |
| :-- | :-- |
| Vuplex 3D WebView (Android Gecko) | Paid, per seat. Guarded by a scripting define so the project compiles without it, but the guarded branch is first compiled only in an Android player build. |
| Widevine | Nothing was validated despite the claim. A texture-based webview is L3 at best. |
| Hard-coded video URLs | A DRM demo page, YouTube, Vimeo, Plex, Netflix, Disney+ and a blocklist of domains. |
| **SomaFM streams** | Three hard-coded stream URLs described as "royalty-cleared". SomaFM's terms do not permit third-party apps. **Remove.** |
| **Spotify** | No SDK, no client ID, no auth flow: a bearer token pasted into a serialized field (which would be committed with the scene). The stated allowlist size is stale. **Remove from v1.** |
| Android permissions | None requested. Music is read from the app-private folder, so users must copy files over USB. |
| Spatialiser | "Unity's built-in OpenXR HRTF spatializer" does not exist; `spatialize = true` is a no-op without a spatialiser plugin. |
| Built-in modules | The manifests omit `video`, `unitywebrequest`, `unitywebrequestaudio`; taken literally the video and radio code will not compile. |
| Quest Browser handoff | `Application.OpenURL` "alongside the island" is unverified; an immersive app is normally backgrounded. |

## The remediation-item map

The document cites a separate "Remediation Plan" that is not included. Reconstructed meanings:

| Item | Meaning | Status in the document |
| :-- | :-- | :-- |
| P0 #1 | Pin Unity 6.3 LTS, lowercase package IDs | Asserted; manifest still pins the 6.0 URP line |
| P0 #2 | No-bake trilight lighting | Implemented in the Phase 6 core file |
| P0 #3 | One shared water-level constant | Constant exists; older phases still hard-code values |
| P0 #4 | Four-tier hardware detection | Implemented, fragile; duplicates the Phase 1 detector |
| P0 #5 | Versioned save schema | Stub only |
| P0 #6 | Ambiguous — used for both tier detection and the budget table | — |
| P1 #7 | Internet radio and user audio primary, Spotify optional | Partially; streams are broken and unlicensed |
| P1 #8 | Quest Browser handoff instead of Netflix-in-webview | An `OpenURL` call |
| P1 #9 | Sandboxed Blender, expiring pairing codes | Not implemented (see Phase 6 audit) |
| P1 #10 | 128/256 MB import memory pool | File-size heuristic only |
| P1 #11 | "Full a/b depth across phases" | Documentation item |
| P2 #12 | Comfort locomotion | Snap turn and seated offset only |
| P2 #13 | Hand tracking | Palm-proximity petting only, wrong coordinate space |
| P2 #14 | Per-tier hard caps (screens, pets, decoders) | Implemented |
| P2 #17 | Runtime-optimiser audit and 60-minute soak | Promised for Phase 12; absent |
| P2 #18 | Ambient soundscape budget | One loop |
| P2 #19 | CI performance regression gate | Promised for Phase 12; absent |
| P3 #21 | Paintable astroturf | Claimed in a header; no code |
| P3 #24 | Per-phase state snippet | Present |
| **P2 #15, #16; P3 #20, #22, #23** | **Never defined anywhere** | If the original remediation plan exists, add it to `docs/blueprint/` |

## Pre-agreed fallbacks (adopted in `02-ARCHITECTURE.md`)

1. **Screens** → Unity `VideoPlayer` to a render texture on a world quad for direct URLs and local
   files; everything web goes to the system browser. Composition-layer quad and Vuplex are
   isolated, device-verified, owner-approved additions. (ADR-15)
2. **Radio** → one decoder feeding other speakers through a ring buffer; station list chosen by
   the user from an open directory; bundled CC0 loops and user files as the always-works path.
   Spotify dropped. (ADR-14)
3. **Pets** → licensed or CC0 rigged animals with real clips where available, otherwise honest
   rigid-part pets with no skinning claim. Pathing through `NavMesh.CalculatePath` on data that is
   actually rebuilt per region, or a heightfield/obstacle grid. Stock XR Interaction Toolkit
   teleport, snap-turn and vignette providers instead of the custom comfort manager. (ADR-16)
