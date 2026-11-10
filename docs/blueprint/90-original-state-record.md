## **Session Continuity and Project State Record**

Per your request, below is a self-contained **Markdown Project State & Continuity Record**. Save this block as PROJECT_STATE.md (or paste it into the prompt of your next session) to pick right back up with implementation scripts, shader code, or companion web app code without losing any architectural context.

# **PROJECT_STATE.md — Meta Quest 1-Acre Island World-Builder Continuity Record**

**Last Updated:** 2026-11-04 \| **Status:** Master Architecture & 12-Phase Build Program Complete (Ready for Phase 1 Code Implementation)

## **1. Core Architectural Decisions & Hardware Budgets (Locked)**

* **Target Hardware:**  
  * **Meta Quest 3 / 3S:** 2064x2208 per eye; 90 Hz target (11.1 ms frame budget) or 72 Hz (13.8 ms); <1.5M triangles (1.3M–1.8M range); <200 baseline draw calls (200–300 Busy Simulation, 400–600 Medium Simulation).  
  * **Meta Quest 2 / Pro:** 1832x1920 per eye; 72 Hz target (13.8 ms frame budget); <750K triangles (750K–1M range); <100 baseline draw calls (80–200 Busy Simulation, 200–300 Medium Simulation).  
* **Engine & Pipeline:** Unity 6 (6000.x LTS) + URP + Vulkan API + Render Graph + Forward Rendering + Single-Pass Stereo (Multi-View).  
* **Critical Exclusions:**  
  * DO NOT enable GPU Resident Drawer (adds GPU overhead on untethered Quest unless strictly CPU-bound).  
  * DO NOT use Deferred Rendering, Depth Priming, SSAO, HDR, Opaque/Depth Texture copies, or volumetric clouds/fog.  
  * DO NOT use WebP in GLB files (unsupported by default in glTFast).  
* **Active Optimizations:** 2X MSAA (or Unity 6.3+ On-Tile Post-Processing without MSAA), OpenXR Foveated Rendering (15–25% GPU savings on Quest 3), Dynamic Application SpaceWarp (AppSW via xr.OpenXRFrameSynthesis), Vulkan Depth Input Attachment for shoreline foam, and unified Vertex-Color + LUT shaders (avoiding the 175% shader-switching CPU penalty).

## **2. Subsystem Technical Specifications**

> 1. **Island & Terraforming:** 1 Acre (~4,047 m²) = 64m x 64m grid divided into 64 chunks (8m x 8m each, 33x33 vertices at 25cm spacing). Burst Jobs for sculpting; RGBA vertex colors drive Tan Ground (R), Grass/Astroturf (G), Cliff Rock (B), and Path (A).  
> 2. **Foliage & Pre-Loaded Inventory (630+ Items):**  
   * 36 Trees, 42 Bushes, 320 Flower varieties (32 base meshes x 10 LUT palettes), 28 Gazebos, 95 Building Modules, 110 Decor/Live Props.  
   * Higher-triangle opaque silhouettes to eliminate transparent overdraw; dynamic shadows and WPO wind disabled; scaled up slightly with lower density; custom 4-tier LODs.  
> 3. **Browser Companion & 3D Asset Ingestion:**  
   * Web portal accepts .fbx, .obj, .blend, .stl, .dae, .gltf, .glb + interactive 3D Scale & Pivot Cage.  
   * Cloud worker (gltf-transform + Blender CLI + Assimp) converts to .glb, prunes/deduplicates, generates simplified LODs, caps textures at 1024/2048px **KTX2 (KHR_texture_basisu)**, and compresses meshes via **Draco (KHR_draco_mesh_compression)** / **Meshopt**.  
   * Quest runtime loads via com.unity.cloud.gltfast + com.unity.cloud.ktx + com.unity.cloud.draco using a shared TimeBudgetPerFrameDeferAgent and ConsoleLogger. Supports runtime .glb scene export.  
> 4. **Live Media (TVs & Spotify Box Radio):**  
   * **Virtual TVs/Projectors:** Vuplex AndroidGeckoWebView with AndroidGeckoWebView.SetDrmEnabled(true) in Awake(), rendered to an **OpenXR Compositor Layer** for sharp text/video. Includes PC WebRTC Cast / Native Quest Browser Dock for Netflix (due to Widevine VMP restriction on embedded webviews).  
   * **Spotify Box Radio:** Dual-backend (ISpotifyRadioController): Mode A uses Embedded Spotify Web Playback SDK in GeckoView (requires Spotify Premium; non-commercial unless approved in writing by Spotify); Mode B uses Spotify Web API Connect Remote + built-in spatialized Icecast radio streams (commercial-ready).  
> 5. **Autonomous Pets:**  
   * 7 Archetypes with Morph BlendShapes, single shared coat pattern shader, Unity 6 GPU Compute Skinning, Animator "Always Animate" disabled off-screen.  
   * 4 Hz PetHarmonyCoordinator AI + localized async chunked NavMeshSurface rebaking + paired social play slots.

## **3. Implementation Checklist & Next Session Pickup Queue**

* [x] Master System Architecture & Visual Optimization Blueprint  
* [x] Pre-Loaded 630+ Item Inventory Taxonomy & Polygon Budgets  
* [x] 12-Phase Full-Stack Build Program Definition  
* [ ] **NEXT STEP A (Phase 1 & 2 Code):** Write C# IslandTerrainManager.cs, Burst TerrainSculptJob.cs, and HLSL IslandTerrainUber.shader + VulkanWaterDepthFoam.shader.  
* [ ] **NEXT STEP B (Phase 4 Code):** Write C# FoliageInstancingManager.cs and HLSL BotanicalVertexLUT.shader.  
* [ ] **NEXT STEP C (Phase 6 & 7 Code):** Write Node.js gltf-transform Cloud Sanitization script and Unity C# IslandGltfLoader.cs with TimeBudgetPerFrameDeferAgent.  
* [ ] **NEXT STEP D (Phase 8, 9 & 10 Code):** Write C# VirtualCinemaController.cs (Vuplex + Compositor Layer), SpotifyBoxRadio.cs, and PetHarmonyCoordinator.cs.
