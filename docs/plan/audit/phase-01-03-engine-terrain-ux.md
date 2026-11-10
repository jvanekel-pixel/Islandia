# Audit detail — blueprint Phases 1–3 (engine foundation, terrain, interaction UX)

Sources: `docs/blueprint/phase-01.md` … `phase-03.md`; drafts under `reference/drafts/Assets/`.
Findings are from reading the code and checking a few APIs against the URP source on GitHub.
Nothing was compiled. "Unverified" means not confirmed against primary documentation.
Line numbers (L…) refer to the original single-file document; treat them as approximate pointers.

## Verdict

A plausible skeleton with few syntax errors. The risk is integration and platform assumptions.

| Phase | Compiles unchanged | Behaves as described |
| :-- | :-- | :-- |
| 1 | configurator ~90% (5 setter errors); others ~100% | ~50% — no OpenXR setup, no asset or scene creation, refresh-rate request dead, AppSW entry points unverified |
| 2 | C# ~99% (one typo); algorithms mostly sound; grid indexing and A/B buffer sync check out | water shader's real path will not survive; terrain shader needs keyword and pass fixes |
| 3 | ~100% | ~40–50% of the described behaviour exists; the wizard leaves it unwired |

## Cross-cutting problems for a headless build

- **No wizard opens or saves a scene.** There is no `EditorSceneManager` call anywhere, so every
  piece of wiring is lost in batchmode.
- **No XR Origin and no input actions asset is ever created or enabled.** On a device nothing would
  read a trigger.
- **No assembly definitions and no tests exist.**
- Every "press Play and use the mouse" check must become a PlayMode test with injected input, and
  `Time.deltaTime` must be injectable.
- Referenced but never created by any code: the URP asset and renderer, the boot scene, the
  baseline materials.

## Phase 1 — files

| Draft | Purpose |
| :-- | :-- |
| `Scripts/Editor/Phase1ProjectConfigurator.cs` | Sets and audits Player/URP settings |
| `Scripts/Core/QuestHardwareProfileManager.cs` | Tier detection, foveation, viewport scale, refresh rate |
| `Scripts/Core/DynamicSpaceWarpController.cs` | Ref-counted AppSW toggle through native `DllImport` |
| `Scripts/Core/Phase1PerformanceGateMonitor.cs` | GPU/CPU time, draw calls, green/yellow/red zone |
| `Scripts/Core/Phase1BaselineIslandSpawner.cs` | Flat 64 m island and 512 m ocean meshes |
| `Shaders/Phase1BaselineVertexColor.shader` | Vertex-colour Lambert plus motion-vector pass |

Stated gates: 90 Hz / 11.1 ms (Quest 3/3S), 72 Hz / 13.8 ms (Quest 2); app GPU time < 6.0 ms on the
empty island; < 15 draw calls; "zero GMEM loads" in RenderDoc.
Headset-only: all of those. CI-provable: the settings audit (needs a non-zero exit code; today it
only logs), manifest resolution, script and shader compile for Android/Vulkan, spawner mesh counts,
an IL2CPP Android build.

GUI-only steps that must become code: selecting the Meta Quest build profile; **all** OpenXR setup
(loader, render mode, depth submission, foveation, Meta Quest support, SpaceWarp feature — the
configurator has no OpenXR code at all); assigning the URP asset; normal-map encoding; creating the
scene, the bootstrap object and its four components, the two materials, the XR Origin.

## Phase 2 — files

| Draft | Purpose |
| :-- | :-- |
| `Scripts/Terrain/TerrainSculptJobs.cs` | Sculpt/paint job, chunk rebuild job, collider bake job |
| `Scripts/Terrain/IslandTerrainChunk.cs` | Per-chunk mesh and native buffers |
| `Scripts/Terrain/IslandTerrainManager.cs` | 257² heightmap, 64 chunks, brush, pad leveller, ocean |
| `Scripts/Terrain/VRTerraformingBrushController.cs` | Ray + trigger/mouse → brush, AppSW lock |
| `Shaders/IslandTerrainUber.shader` | Texture-array splat by vertex colour, LUT lighting |
| `Shaders/VulkanWaterDepthFoam.shader` | Opaque water, depth via subpass input |
| `Scripts/Editor/Phase2SetupWizard.cs` | Generates the array, LUT and materials; wires the manager |

Stated gates: 90/72 FPS while sculpting; 0 B GC per frame; ≤ 65 draw calls; 131,072 triangles;
water at Y = −0.35; height limits −3.5 / +18.
CI-provable: grid maths, seam equality of border vertices and normals, buffer sync, dirty-chunk
selection, height sampling, pad-leveller result, triangle count, allocations per stroke, shader
compile.

## Phase 3 — files

| Draft | Purpose |
| :-- | :-- |
| `Scripts/UX/DualScaleWorldRigController.cs` | 1× ↔ 25× rig scaling about a pivot |
| `Scripts/UX/UndoRedoCommandStack.cs` | Ring buffer of spawn/delete/transform records |
| `Scripts/UX/ToyboxInteractionController.cs` | Place prefab with pad levelling; scatter brush |
| `Scripts/UX/ArtistStudioGridGizmoController.cs` | Grid snap, axis drag, undo hotkeys |
| `Scripts/UX/WristCompositorHudController.cs` | 4 Hz budget gauge on a render-texture quad |
| `Shaders/AppSWCompliantWorldUI.shader` | Alpha-clipped opaque UI quad |
| `Scripts/Editor/Phase3SetupWizard.cs` | Adds components, gizmo cubes, wrist quad |

CI-provable: pivot invariant, grid and rotation snap, undo/redo ring semantics including batches
and wrap, scatter spacing and scale range, HUD refresh cadence, allocations.

## Defects

### Will not compile (confirmed against URP source)

1. L466–473: `mainLightRenderingMode`, `supportsMainLightShadows`, `additionalLightsRenderingMode`,
   `reflectionProbeBlending`, `reflectionProbeBoxProjection` have `internal set` in URP 17. Write
   the serialized fields through `SerializedObject` instead.
2. L1444: `public float2HeightLimits;` is missing its type.
3. L504: the `enableRenderCompatibilityMode` setter is get-only and obsolete on newer editors.

### Version and package contradictions

4. **No single editor satisfies the document.** It demands "6000.1 LTS or 6000.2+ LTS" (neither is
   an LTS), pins URP/Core 17.0.3 (the 6000.0 line), later pins "6.3 LTS", and relies on features
   documented only for 6000.6. Pin one editor and let core packages follow it.
5. L226 `com.unity.xr.Hands` — package IDs are lowercase; this fails to resolve.
6. Adaptive Performance pins are likely unresolvable and the package is unused in Phases 1–3.
7. L750–757: AppSW is called through private native entry points (`MetaSetSpaceWarp` and friends).
   The public API is `SpaceWarpFeature.SetSpaceWarp / SetAppSpacePosition / SetAppSpaceRotation`
   (OpenXR plugin 1.15+).
8. L703–709: `TryRequestDisplayRefreshRate` is a static extension method, so the reflection lookup
   returns null and 90 Hz is never requested. It also needs the Meta Quest Display Utilities
   feature, which is never enabled.
9. L184 lists Quest 3S at 2064×2208; it is 1832×1920.

### Shaders and pipeline

10. **Water shader.** Declaring a framebuffer input in a material drawn by URP's ordinary opaque
    pass does not bind depth as an input attachment; a render-graph pass has to declare it. The
    shader also uses the non-multisample macro under mandated 2× MSAA, the non-array variant under
    multiview, and writes depth while reading it. `unity_LastFragDepth` appears in no source found.
    `!defined(UNITY_EDITOR)` is not a shader define. Expect it not to work on device as written.
11. The water fallback path hard-codes the initial shoreline, so dug ponds get no foam ring in the
    Editor or CI, and CI can never exercise the "real" path.
12. `UsePass "Universal Render Pipeline/Lit/…"` pulls in passes that use Lit's own material
    constant buffer. Expect this to break SRP Batcher compatibility, which the prose claims.
    Unity documents writing your own `XRMotionVectors` pass.
13. `shader_feature_local _ASTROTURF_MODE_ON` toggled at runtime is stripped from player builds;
    the astroturf toggle would do nothing on device. Use `multi_compile_local`.
14. Prose says 512² ASTC terrain textures; the code generates 256² uncompressed, non-seamless.

### Logic

15. **Phase 2 kills Phase 1.** The terrain manager deactivates the spawner's GameObject, which is
    the same object hosting the hardware manager, the AppSW controller and the perf monitor.
16. **AppSW latch.** With AppSW on, draws roughly double and the app runs at half rate, so the
    monitor's "red" zone re-arms the hold timer every frame and AppSW never switches off.
17. The Phase 2 gate requires locked 90/72 FPS while sculpting, but the code forces AppSW on during
    sculpting, which renders at 45/36.
18. If GPU timing is unavailable the smoothed GPU time is 0 and the Phase 1 gate passes falsely.
19. Every job is `Complete()`d immediately, so collider baking blocks the main thread. The "never
    stalls" claim is false.
20. `[BurstCompile]` on a job that calls `Physics.BakeMesh` — drop the attribute.
21. An 8 m brush radius touches up to ~16 chunks, not the "1–4" claimed.
22. The pad leveller flattens ~55% at the centre and 0% at the rim, and depends on frame rate.
23. The flat area is a 58 m rounded square, not 64 × 64. Thresholds disagree across files: water
    −0.35, sand −0.20/−0.25, scatter cutoff −0.25, cliff angle 38° vs 35°.
24. Dual-scale rig: tabletop offset puts the island 0.72 m *below* the floor; the recentring lerp
    runs for a frame or two; return to 1:1 ignores terrain height; the camera far plane is scaled
    to 15 km on 16-bit depth for no benefit; the rig reference falls back to the wrong object.
25. Undo: capacity 256 vs 128 in the spec; **no terrain command type**, so sculpting cannot be
    undone; objects that fall off the ring are never destroyed.
26. Zero-GC claims violated by `Instantiate` on every placement, `GameObject.name` strings, and
    legacy UI text rebuilds. Scatter both fires the foliage event and instantiates a prefab.
27. The terraform brush is never disabled by the mode switch; Studio mode can drag terrain chunks.
28. Promised but absent: any OpenXR composition layer (the HUD is a render texture on a world quad,
    the approach the prose calls blurry); HUD canvas, camera and buttons; rotation and scale
    gizmos; multi-select; grab; stilts over water; URP asset creation.

## Pre-agreed fallbacks (adopted in `02-ARCHITECTURE.md`)

1. **Water depth** → upload the existing 257² height buffer as a texture and compute water depth
   as `waterLevel − height` in the water shader. No depth read; identical in Editor, CI and
   device; testable. (ADR-05)
2. **AppSW** → behind an interface with a no-op default. Rely on foveation and viewport scale.
   Revisit after the first device gate using the public API and per-shader motion-vector passes.
   (ADR-18)
3. **Wrist HUD** → the world-space quad is the baseline; composition layers are an isolated,
   device-verified spike. One deterministic batchmode scene builder replaces all wizards. (ADR-06)
