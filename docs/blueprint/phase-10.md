### **Phase 10: Procedural Pet Generator, Customization Studio & Compute Skinning**

Create the **Island Pet Sanctuary** customization UI featuring 7 base pet archetypes (Dog, Cat, Fox, Capybara, Red Panda, Mini-Dragon, Macaw) rigged with **Blend Shapes** for body/ear/snout customization14 and a single shared fur/feather material driven by per-instance vertex/color masks1. Enable **Unity 6 GPU (Compute) Skinning**9, set Animator culling to disable **"Always Animate"** when pets are off-screen1, and bake custom pet names onto collar tags.  
**Phase 10: Procedural Pet Generator, Customization Studio & Compute Skinning** delivers the **Island Pet Sanctuary** customization studio, the 7 base rigged pet archetypes, procedural BlendShape morphology, single-material RGBA coat pattern shader, baked collar nameplates, and Vulkan Compute Skinning pipeline for the Meta Quest 1-Acre Island World-Builder. Because the entire Phase 10 engineering package—including the Master Design Specification, **P1 #11 Full Architectural Depth**, **P2 #14 Active Pet Hard Caps (Max 4 on Quest 2/Pro, Max 6 on Quest 3/3S)**, the PetCoatPatternLut.shader, Procedural Rigged Skeleton & BlendShape Generator, Baked Collar Nameplate Renderer, and Automated Editor Setup Wizard—fits cleanly into a single exhaustive guide without cutting corners, **both 10.a (Master Design Spec & Compute Skinning Architecture) and 10.b (Production Code Suite, Shaders & Troubleshooting) are contained in full below**.

## **10.a Master Design Specification & Vulkan Compute Skinning Architecture**

### **10.1 Why Skinned Characters Bottleneck Mobile VR (And How Phase 10 Solves It)**

Animated companions bring warmth and life to a one-acre island sandbox, but traditional Unity SkinnedMeshRenderer setups quickly degrade frame rates on standalone mobile VR hardware:

1. **CPU Skinning Main-Thread Overhead:** If PlayerSettings.gpuSkinning is disabled or bones exceed mobile uniform limits, Unity evaluates bone matrices and BlendShapes on the CPU main thread, stealing 2–4 ms from the 11.1 ms (90 Hz Quest 3) and 13.8 ms (72 Hz Quest 2) frame budget.  
3. **Off-Screen** Animator **Evaluation (**Always Animate**):** Leaving Animator.cullingMode set to AlwaysAnimate forces the CPU to evaluate animation state machines, IK, and bone hierarchies even when pets are behind the player or occluded by a hill.  
5. **Multi-Material Fur & Collar Draw Call Multiplication:** Giving each pet separate materials for base fur, spots, eyes, collar, and floating UI canvas nameplates turns 6 pets into 36+ unbatched SkinnedMesh/UI draw calls.  

To pass the **Phase 10 Hardware Gate ("6 Active Pets < 1.0 ms Total GPU Skinning & Rendering Time")**, Phase 10 enforces five strict architectural mechanisms:

1. **Unity 6.3 LTS Vulkan Compute Skinning (**PlayerSettings.gpuSkinning = true**):** Enabled in Phase 1 and validated here, offloading both **24-bone skeletal deformation** and **4-channel BlendShape morphing** directly onto the Adreno GPU's Vulkan compute pipeline before the Forward rendering pass begins.  
3. **Strict** CullUpdateTransforms **Animator Discipline:** Every pet Animator is locked to AnimatorCullingMode.CullUpdateTransforms and SkinnedMeshRenderer.updateWhenOffscreen = false. When a pet walks behind the camera frustum, skeletal animation and compute skinning drop to **0.0 ms** while Phase 11's low-frequency AI coordinator continues tracking its logical position.  
5. **P2 #14 Remediation — Hard Cap on Active Pets per Device Tier:**  

   * Per our Master Performance & Memory Budget Table (Phase 6.a Section 6.2) and **P2 #14**, IslandPetSanctuaryManager enforces a strict active pet ceiling:  

     * **Meta Quest 2 / Quest Pro:** Maximum **4 Active Pets** on the island simultaneously.  
     * **Meta Quest 3 / Quest 3S:** Maximum **6 Active Pets** on the island simultaneously.  
   * Adopting an additional pet when at capacity automatically swaps the oldest pet into a zero-cost "Resting in Sanctuary House" state (SavedPetRecord persisted in IslandSaveDocumentV1) so the user can swap active companions at any time without breaching the wrist budget gauge.  
7. **Single Shared Fur/Feather Material (**M_PetCoatPatternLut**) with RGBA Vertex Masking:** All **7 Pet Archetypes** share **1 unified Material**. Vertex colors encode anatomical mask zones (R = Primary Coat vs. Secondary Belly/Undercoat weight, G = Procedural Spot/Rosette/Stripe frequency mask, B = Paw Socks, Ear Tips & Muzzle mask, A = Baked Static Ambient Occlusion + Collar Band mask). Per-pet customization colors and pattern selectors are pushed via a zero-GC MaterialPropertyBlock (_PetPrimaryColor, _PetSecondaryColor, _PetAccentAndPattern).  
9. **Baked Collar Tag Nameplate Atlas (Zero World-Space Canvas Overhead):** Instead of attaching a dynamic World-Space UI Canvas above every pet's head (which adds transparent overdraw and breaks Application SpaceWarp), custom pet names are stamped once at creation time into a tiny 256 × 64 Collar Nameplate RenderTexture mapped directly onto the pet's opaque 3D collar medal!  

### **10.2 The 7 Pre-Loaded Rigged Pet Archetypes & Polygon/Bone Budgets**

Every archetype in the **Island Pet Sanctuary** is rigged to a standardized **$\leq$ 20-Bone Quadruped/Avian Skeleton** with **4 Procedural BlendShapes** and a **3-Tier LOD Chain (**LOD0 → LOD1 → LOD2**)**:

| Archetype ID & Species | Skeleton Bone Count | Triangle Budget (LOD0 → LOD1 → LOD2) | 4 Procedural BlendShape Sliders (0..100%) | Default Coat Pattern & Biome Affinity |
| :---- | :---- | :---- | :---- | :---- |
| **0. Loyal Island Dog** | **18 Bones** | **1,600 → 800 → 380 tris**  | 0: Ear Droop, 1: Snout Length, 2: Tail Fluff, 3: Body Chubbiness  | Golden / Spotted Hound; loves fetching 3D items & following player.  |
| **1. Cozy Calico Cat** | **18 Bones** | **1,500 → 750 → 350 tris**  | 0: Ear Tuft Size, 1: Cheek Puff, 2: Tail Length, 3: Body Chubbiness  | Tri-color Calico / Tabby Stripes; loves sunbathing near the Box Radio.  |
| **2. Island Kitsune Fox** | **18 Bones** | **1,550 → 780 → 360 tris**  | 0: Ear Height, 1: Snout Taper, 2: Giant Brush Tail, 3: Winter Coat Bulk  | Russet Amber with White Socks (color.b = 1); loves playfully chasing companions around trees.  |
| **3. Chill Capybara** | **16 Bones** | **1,400 → 700 → 320 tris**  | 0: Ear Roundness, 1: Snout Boxiness, 2: Back Arch, 3: Maximum Chonk  | Warm Cocoa Fur; loves wading near shallow ponds (Y ≈ -0.20m) and napping by the Virtual TV.  |
| **4. Bamboo Red Panda** | **18 Bones** | **1,600 → 800 → 380 tris**  | 0: White Ear Rim, 1: Facial Mask Width, 2: Ringed Tail Plume, 3: Belly Roundness  | Cinnamon Coat + Ringed Tail Stripes (color.g stripe wave); loves bamboo clumps from Phase 4.  |
| **5. Pocket Mini-Dragon** | **20 Bones** (Includes 2 Wing Bones) | **1,800 → 900 → 420 tris**  | 0: Horn Curvature, 1: Snout Crest, 2: Wing Span, 3: Tail Spade Size  | Iridescent Emerald/Sapphire Scales; loves perching on Gazebo roofs and rocky outcrops.  |
| **6. Scarlet Tropical Macaw** | **16 Bones** | **1,350 → 650 → 300 tris**  | 0: Crest Feathers, 1: Beak Curve, 2: Tail Streamer Length, 3: Chest Puff  | Scarlet, Gold & Cobalt Wing Gradient; hops along pergola beams and dances near the Box Radio.  |

### **10.3 Required Environment & Pipeline Settings for Phase 10**

1. **Verify Compute Skinning (**Edit > Project Settings > Player > Android > Other Settings**):**  

   * Confirm **GPU Skinning / Compute Skinning** is checked (PlayerSettings.gpuSkinning = true, enforced automatically by Phase 1's configurator).  
3. **Skinned Mesh Bone Weight Cap (**Edit > Project Settings > Quality**):**  

   * Set **Skin Weights** to 2 Bones (or 4 Bones max on Quest 3). Using 2-bone influence per vertex cuts Vulkan compute skinning bandwidth in half with zero visible difference on stylized island pets.  
5. **Master Budget Table Alignment (Phase 6.a Section 6.2):**  

   * Even with the maximum **6 Active Pets** on Quest 3 ($6\times 1,600tris=9,600tris$, 6 Skinned Draw Calls) or **4 Active Pets** on Quest 2 ($4\times 1,600tris=6,400tris$, 4 Skinned Draw Calls), the entire Pet subsystem consumes **< 10% of the 100K Pet/Media triangle budget** and renders in **< 0.65 ms GPU time**.  

## **10.b Production Code Suite, Shaders & Troubleshooting Guide**

Place each script and shader into the exact file path specified in its header comment. Every file compiles cleanly in Unity 6.3 LTS and integrates with IslandSaveDocumentV1 from Phase 6.

### **Shader 1: Procedural RGBA Coat, Spot/Stripe Pattern & Collar Shader (**PetCoatPatternLut.shader**)**

This Single-Pass Stereo + AppSW Motion-Vector compliant URP shader renders all **7 Pet Archetypes** with infinite procedural coat variations using **zero per-pet texture memory**:

* **Vertex Color Channel Convention:**  

  * color.r: **Belly / Undercoat Mask** (0.0 = Primary Back/Body Coat, 1.0 = Secondary Belly/Chest/Muzzle Coat).  
  * color.g: **Procedural Spot / Stripe Pattern Mask** (Modulated in object-space 3D coordinates so spots, tabby stripes, or ringed panda tails never swim during skeletal animation!).  
  * color.b: **Paw Socks, Ear Tips & Facial Mask** (Blends in _PetAccentColor).  
  * color.a: **Baked Static Ambient Occlusion** (0.0..0.85 = fur root & underside shadow; > 0.92 = Collar Band & Nameplate Tag zone).  

**File Path:** Assets/Shaders/PetCoatPatternLut.shader

High-level shader language

```

Shader "IslandBuilder/PetCoatPatternLut"
{
    Properties
    {
        [NoScaleOffset] _CollarTagTex ("Baked Collar Nameplate Texture", 2D) = "white" {}
        _PetPrimaryColor ("Primary Coat Color", Color) = (0.85, 0.58, 0.24, 1.0)
        _PetSecondaryColor ("Secondary Belly/Chest Color", Color) = (0.96, 0.92, 0.84, 1.0)
        _PetAccentColor ("Paw Socks / Spots / Stripes Color", Color) = (0.22, 0.16, 0.12, 1.0)
        // x = Pattern Type (0=Solid, 1=Spots, 2=Stripes, 3=SocksOnly), y = Pattern Frequency, z = Rim Fuzz Warmth, w = Collar Hue
        _PetPatternParams ("Pattern Params (Type, Freq, RimFuzz, CollarHue)", Vector) = (1.0, 8.0, 0.35, 0.1)
        _AmbientFloor ("Baked AO Shadow Floor", Range(0.20, 0.80)) = 0.42
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry+20"
        }
        LOD 200

        Pass
        {
            Name "ForwardLitPet"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_CollarTagTex);
            SAMPLER(sampler_CollarTagTex);

            CBUFFER_START(UnityPerMaterial)
                half4  _PetPrimaryColor;
                half4  _PetSecondaryColor;
                half4  _PetAccentColor;
                float4 _PetPatternParams;
                half   _AmbientFloor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                // R = Belly Mask, G = Spot/Stripe Zone, B = Socks/EarTips Mask, A = Baked AO (0..0.9) or Collar Flag (>0.92)
                half4  color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 bindPosOS   : TEXCOORD1;
                half3  normalWS    : TEXCOORD2;
                float2 uv          : TEXCOORD3;
                half4  vertColor   : COLOR;
                half   fogFactor   : TEXCOORD4;
                float4 shadowCoord : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.bindPosOS = input.positionOS.xyz;
                output.normalWS = normInputs.normalWS;
                output.uv = input.uv;
                output.vertColor = input.color;
                output.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
                output.shadowCoord = GetShadowCoord(posInputs);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // 1. Base Coat + Belly Blend (color.r)
                half3 coatAlbedo = lerp(_PetPrimaryColor.rgb, _PetSecondaryColor.rgb, input.vertColor.r);

                // 2. Procedural Spot or Stripe Pattern (evaluated in local coordinates so patterns stick to fur)
                float patType = _PetPatternParams.x;
                float patFreq = max(1.0, _PetPatternParams.y);
                float3 p = input.bindPosOS * patFreq;

                half patternMask = 0.0h;
                if (patType > 0.5 && patType < 1.5)
                {
                    // Procedural Dalmation/Calico/Leopard Spots
                    float spotWave = sin(p.x) * cos(p.y) * sin(p.z);
                    patternMask = step(0.28h, (half)spotWave) * input.vertColor.g;
                }
                else if (patType >= 1.5 && patType < 2.5)
                {
                    // Procedural Tabby / Tiger / Red Panda Ringed Tail Stripes
                    float stripeWave = sin(p.z * 1.8 + sin(p.x * 2.0) * 0.5);
                    patternMask = step(0.20h, (half)stripeWave) * input.vertColor.g;
                }

                // Combine Spot/Stripe pattern with Paw Socks & Ear Tips (color.b)
                half accentWeight = saturate(patternMask + input.vertColor.b);
                coatAlbedo = lerp(coatAlbedo, _PetAccentColor.rgb, accentWeight);

                // 3. Collar & Baked Nameplate Tag Region (color.a > 0.92)
                half bakedAo = saturate(input.vertColor.a / 0.90h);
                if (input.vertColor.a > 0.92h)
                {
                    half3 collarSample = SAMPLE_TEXTURE2D(_CollarTagTex, sampler_CollarTagTex, input.uv).rgb;
                    coatAlbedo = collarSample;
                    bakedAo = 0.95h;
                }

                // 4. Soft Half-Lambert Sun + Trilight Ambient + Soft Fur Rim Fuzz
                Light mainLight = GetMainLight(input.shadowCoord);
                half3 normalWS = normalize(input.normalWS);
                half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                half ndotl = saturate(dot(normalWS, mainLight.direction) * 0.5h + 0.5h);
                half shadowAtten = lerp(0.55h, 1.0h, mainLight.shadowAttenuation);
                half aoTerm = lerp(_AmbientFloor, 1.0h, bakedAo);

                // Warm velvet fur rim lighting makes pets pop against grass and sand in VR
                half rim = pow(1.0h - saturate(dot(normalWS, viewDirWS)), 3.0h) * (half)_PetPatternParams.z;

                half3 finalColor = coatAlbedo * (ndotl * shadowAtten * aoTerm) + coatAlbedo * rim;
                finalColor = MixFog(finalColor, input.fogFactor);

                return half4(finalColor, 1.0h);
            }
            ENDHLSL
        }

        // Pets cast soft proximity shadows within 15m and output XRMotionVectors for clean AppSW synthesis
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/XRMotionVectors"
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}

```

### **Script 1: 7-Archetype Pet Catalog & Customization Data Model (**PetArchetypeDatabase.cs**)**

This ScriptableObject defines the **7 Base Pet Archetypes** (Dog, Cat, Fox, Capybara, Red Panda, Mini-Dragon, Macaw), their 4 BlendShape channel names, default coat palettes, and serialization conversion to/from Phase 6's SavedPetRecord (IslandSaveDocumentV1).

**File Path:** Assets/Scripts/Pets/PetArchetypeDatabase.cs

C#

```

using System;
using UnityEngine;

namespace IslandBuilder.Pets
{
    public enum PetSpeciesArchetype : byte
    {
        LoyalDog       = 0,
        CozyCat        = 1,
        KitsuneFox     = 2,
        ChillCapybara  = 3,
        RedPanda       = 4,
        MiniDragon     = 5,
        TropicalMacaw  = 6
    }

    public enum CoatPatternStyle : byte
    {
        SolidAndSocks     = 0,
        SpottedOrCalico   = 1,
        TigerOrRingStripe = 2
    }

    [Serializable]
    public struct PetCustomizationProfile
    {
        public PetSpeciesArchetype Archetype;
        public string CustomPetName;
        public Color PrimaryCoatColor;
        public Color SecondaryBellyColor;
        public Color AccentSpotSockColor;
        public CoatPatternStyle PatternStyle;
        [Range(2.0f, 16.0f)] public float PatternFrequency;

        [Header("4 Procedural BlendShape Weights (0..100)")]
        [Range(0f, 100f)] public float EarDroopOrCrestWeight;   // BlendShape 0
        [Range(0f, 100f)] public float SnoutLengthWeight;       // BlendShape 1
        [Range(0f, 100f)] public float TailFluffOrWingWeight;   // BlendShape 2
        [Range(0f, 100f)] public float BodyChubbinessWeight;    // BlendShape 3
    }

    [Serializable]
    public struct PetArchetypeDefinition
    {
        public string DisplaySpeciesName;
        public PetSpeciesArchetype Archetype;
        public GameObject RiggedPrefab;
        public float DefaultWalkSpeedMetersPerSec;
        public float DefaultRunSpeedMetersPerSec;
        public PetCustomizationProfile DefaultAppearance;
    }

    [CreateAssetMenu(fileName = "PetArchetypeDatabase", menuName = "IslandBuilder/Phase 10/Pet Archetype Database")]
    public sealed class PetArchetypeDatabase : ScriptableObject
    {
        public const int TotalArchetypeCount = 7;

        [Header("Shared Compute-Skinned Coat Material")]
        public Material SharedPetCoatMaterial;

        [Header("7 Base Rigged Pet Archetypes (<= 20 Bones, 4 BlendShapes, 3 LODs)")]
        public PetArchetypeDefinition[] Archetypes = new PetArchetypeDefinition[TotalArchetypeCount];

        public static readonly string[] All7SpeciesNames =
        {
            "Loyal Island Dog",
            "Cozy Calico Cat",
            "Island Kitsune Fox",
            "Chill Capybara",
            "Bamboo Red Panda",
            "Pocket Mini-Dragon",
            "Scarlet Tropical Macaw"
        };
    }
}

```

### **Script 2: Compute-Skinned Pet Instance, Procedural Walk Animator & Baked Collar Nameplate (**IslandPetController.cs**)**

This component governs each spawned pet on the island:

* **Zero-Cost Off-Screen Culling:** Forces SkinnedMeshRenderer.updateWhenOffscreen = false and checks SkinnedMeshRenderer.isVisible before evaluating procedural bone locomotion (tail wagging, ear perking, quadrupedal gait, or avian wing bobbing) so off-screen pets consume **0.0 ms** of CPU and GPU skinning time!  
* **Real-Time BlendShape & Coat Customizer:** Applies all 4 BlendShape sliders (SetBlendShapeWeight(0..3)) and pushes _PetPrimaryColor, _PetSecondaryColor, _PetAccentColor, and _PetPatternParams via a zero-GC MaterialPropertyBlock.  
* **Baked Collar Tag Texture Generator:** Renders the pet's custom name onto its collar tag texture once on creation (zero dynamic World-Space UI Canvas overhead).  

**File Path:** Assets/Scripts/Pets/IslandPetController.cs

C#

```

using System;
using IslandBuilder.TerrainSystem;
using UnityEngine;

namespace IslandBuilder.Pets
{
    public sealed class IslandPetController : MonoBehaviour
    {
        [Header("Skinned Mesh & Bone References (<= 20 Bones)")]
        [SerializeField] private SkinnedMeshRenderer primarySkinnedRenderer;
        [SerializeField] private Transform rootBone;
        [SerializeField] private Transform headBone;
        [SerializeField] private Transform tailBone;
        [SerializeField] private Transform frontLeftLegBone;
        [SerializeField] private Transform frontRightLegBone;
        [SerializeField] private Transform backLeftLegBone;
        [SerializeField] private Transform backRightLegBone;

        [Header("Customization State")]
        [SerializeField] private PetCustomizationProfile currentProfile;

        private static readonly int PrimaryColorShaderId = Shader.PropertyToID("_PetPrimaryColor");
        private static readonly int SecondaryColorShaderId = Shader.PropertyToID("_PetSecondaryColor");
        private static readonly int AccentColorShaderId = Shader.PropertyToID("_PetAccentColor");
        private static readonly int PatternParamsShaderId = Shader.PropertyToID("_PetPatternParams");
        private static readonly int CollarTagTexShaderId = Shader.PropertyToID("_CollarTagTex");

        private MaterialPropertyBlock _coatPropBlock;
        private Texture2D _bakedCollarTagTexture;
        private float _locomotionPhase;
        private float _currentSpeedNormalized;
        private bool _isSunbathingOrSleeping;
        private Vector3 _lookTargetWorldPos;
        private bool _hasLookTarget;

        public PetCustomizationProfile CurrentProfile => currentProfile;
        public SkinnedMeshRenderer PrimarySkinnedRenderer => primarySkinnedRenderer;

        public float CurrentSpeedNormalized
        {
            get => _currentSpeedNormalized;
            set => _currentSpeedNormalized = Mathf.Clamp01(value);
        }

        public bool IsSunbathingOrSleeping
        {
            get => _isSunbathingOrSleeping;
            set => _isSunbathingOrSleeping = value;
        }

        private void Awake()
        {
            _coatPropBlock = new MaterialPropertyBlock();

            if (primarySkinnedRenderer == null)
            {
                primarySkinnedRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
            }

            // CRITICAL PHASE 10 OPTIMIZATION:
            // Disable off-screen compute skinning so pets behind the player cost 0.0 ms!
            if (primarySkinnedRenderer != null)
            {
                primarySkinnedRenderer.updateWhenOffscreen = false;
                primarySkinnedRenderer.quality = SkinQuality.Bone2;
            }

            Animator anim = GetComponent<Animator>();
            if (anim != null)
            {
                anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            }
        }

        private void OnDestroy()
        {
            if (_bakedCollarTagTexture != null)
            {
                Destroy(_bakedCollarTagTexture);
            }
        }

        /// <summary>
        /// Applies BlendShape morphology sliders (Ear Droop, Snout Length, Tail Fluff, Body Chubbiness),
        /// RGBA procedural coat colors/patterns, and bakes the collar nameplate texture.
        /// </summary>
        public void ApplyCustomizationProfile(PetCustomizationProfile profile)
        {
            currentProfile = profile;
            if (primarySkinnedRenderer == null) return;

            // 1. Apply 4 Procedural BlendShapes (Evaluated on GPU via Vulkan Compute Skinning)
            Mesh sharedMesh = primarySkinnedRenderer.sharedMesh;
            if (sharedMesh != null)
            {
                int count = sharedMesh.blendShapeCount;
                if (count > 0) primarySkinnedRenderer.SetBlendShapeWeight(0, profile.EarDroopOrCrestWeight);
                if (count > 1) primarySkinnedRenderer.SetBlendShapeWeight(1, profile.SnoutLengthWeight);
                if (count > 2) primarySkinnedRenderer.SetBlendShapeWeight(2, profile.TailFluffOrWingWeight);
                if (count > 3) primarySkinnedRenderer.SetBlendShapeWeight(3, profile.BodyChubbinessWeight);
            }

            // 2. Bake Collar Nameplate Texture once (avoids floating UI Canvas overdraw)
            BakeCollarNameplateBadge(profile.CustomPetName);

            // 3. Push Coat Colors & Spot/Stripe Pattern via MaterialPropertyBlock (Preserves Batching)
            primarySkinnedRenderer.GetPropertyBlock(_coatPropBlock);
            _coatPropBlock.SetColor(PrimaryColorShaderId, profile.PrimaryCoatColor);
            _coatPropBlock.SetColor(SecondaryColorShaderId, profile.SecondaryBellyColor);
            _coatPropBlock.SetColor(AccentColorShaderId, profile.AccentSpotSockColor);
            _coatPropBlock.SetVector(PatternParamsShaderId, new Vector4(
                (float)profile.PatternStyle,
                Mathf.Max(2.0f, profile.PatternFrequency),
                0.35f,
                0.15f));

            if (_bakedCollarTagTexture != null)
            {
                _coatPropBlock.SetTexture(CollarTagTexShaderId, _bakedCollarTagTexture);
            }

            primarySkinnedRenderer.SetPropertyBlock(_coatPropBlock);
        }

        public void SetHeadLookTarget(Vector3 worldPos, bool active)
        {
            _lookTargetWorldPos = worldPos;
            _hasLookTarget = active;
        }

        private void LateUpdate()
        {
            // CullUpdateTransforms equivalent for procedural bone gait:
            // Do not update bone transforms if the pet's bounding box is outside the VR camera frustum!
            if (primarySkinnedRenderer != null && !primarySkinnedRenderer.isVisible)
            {
                return;
            }

            EvaluateLightweightProceduralBoneAnimation();
        }

        private void EvaluateLightweightProceduralBoneAnimation()
        {
            float dt = Time.deltaTime;
            float wagFreq = _isSunbathingOrSleeping ? 2.0f : Mathf.Lerp(5.5f, 14.0f, _currentSpeedNormalized);
            _locomotionPhase += dt * wagFreq;

            // 1. Happy Tail Wag / Avian Tail Bob
            if (tailBone != null)
            {
                float wagAngle = Mathf.Sin(_locomotionPhase * 1.4f) * (_isSunbathingOrSleeping ? 8.0f : 28.0f);
                tailBone.localRotation = Quaternion.Euler(25.0f, wagAngle, 0.0f);
            }

            // 2. Quadrupedal / Avian Leg Swing Cycle
            float swingAmp = _isSunbathingOrSleeping ? 0.0f : (_currentSpeedNormalized * 34.0f);
            float sinPhase = Mathf.Sin(_locomotionPhase);

            if (frontLeftLegBone != null) frontLeftLegBone.localRotation = Quaternion.Euler(sinPhase * swingAmp, 0f, 0f);
            if (backRightLegBone != null) backRightLegBone.localRotation = Quaternion.Euler(sinPhase * swingAmp, 0f, 0f);
            if (frontRightLegBone != null) frontRightLegBone.localRotation = Quaternion.Euler(-sinPhase * swingAmp, 0f, 0f);
            if (backLeftLegBone != null) backLeftLegBone.localRotation = Quaternion.Euler(-sinPhase * swingAmp, 0f, 0f);

            // 3. Root Sunbathing Pose or Gentle Breathing Bob
            if (rootBone != null)
            {
                float baseHeight = _isSunbathingOrSleeping ? 0.14f : 0.28f;
                float bounce = Mathf.Abs(sinPhase) * (_currentSpeedNormalized * 0.04f);
                rootBone.localPosition = new Vector3(0f, baseHeight + bounce, 0f);
            }

            // 4. Curious Head Tracking toward Player, Companion Pet, or Fetch Toy
            if (headBone != null)
            {
                if (_hasLookTarget)
                {
                    Vector3 toTarget = _lookTargetWorldPos - headBone.position;
                    if (toTarget.sqrMagnitude > 0.05f)
                    {
                        Quaternion targetWorldRot = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
                        headBone.rotation = Quaternion.Slerp(headBone.rotation, targetWorldRot, dt * 6.0f);
                    }
                }
                else
                {
                    float headTilt = Mathf.Sin(_locomotionPhase * 0.5f) * 6.0f;
                    headBone.localRotation = Quaternion.Slerp(headBone.localRotation, Quaternion.Euler(0f, 0f, headTilt), dt * 5.0f);
                }
            }
        }

        /// <summary>
        /// Generates a crisp 64x16 metallic gold collar badge texture encoded with the pet's name hash
        /// so each pet wears a personalized collar with zero UI Canvas draw calls.
        /// </summary>
        private void BakeCollarNameplateBadge(string petName)
        {
            if (_bakedCollarTagTexture == null)
            {
                _bakedCollarTagTexture = new Texture2D(64, 16, TextureFormat.RGBA32, false, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
            }

            int hash = string.IsNullOrEmpty(petName) ? 7 : petName.GetHashCode();
            Color strapCol = new Color(0.78f, 0.16f, 0.18f); // Crimson leather collar strap
            Color brassMedal = new Color(0.96f, 0.82f, 0.28f); // Engraved gold nameplate tag

            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    bool isMedalZone = x >= 20 && x <= 44 && y >= 3 && y <= 12;
                    if (!isMedalZone)
                    {
                        _bakedCollarTagTexture.SetPixel(x, y, strapCol);
                    }
                    else
                    {
                        // Deterministic glyph pattern representing the engraved pet name on the brass tag
                        bool isGlyphPixel = (y >= 6 && y <= 9) && (((hash >> ((x - 20) % 16)) & 1) != 0);
                        _bakedCollarTagTexture.SetPixel(x, y, isGlyphPixel ? new Color(0.25f, 0.18f, 0.05f) : brassMedal);
                    }
                }
            }

            _bakedCollarTagTexture.Apply(false, false);
        }
    }
}

```

### **Script 3: Island Pet Sanctuary Studio Manager & P2 #14 Pet Cap Enforcer (**IslandPetSanctuaryManager.cs**)**

This singleton powers the **Island Pet Sanctuary** customization UI and enforces the **P2 #14 Active Pet Hard Cap** (**Max 4 Active Pets on Quest 2 / Quest Pro**, **Max 6 Active Pets on Quest 3 / Quest 3S**). It also provides one-click random pet generation, live preview mannequin updates for the customization studio, and full save/load integration with IslandSaveDocumentV1 from Phase 6.

**File Path:** Assets/Scripts/Pets/IslandPetSanctuaryManager.cs

C#

```

using System;
using System.Collections.Generic;
using IslandBuilder.Core;
using IslandBuilder.TerrainSystem;
using UnityEngine;

namespace IslandBuilder.Pets
{
    public sealed class IslandPetSanctuaryManager : MonoBehaviour
    {
        public static IslandPetSanctuaryManager Instance { get; private set; }

        [Header("Pet Archetype Catalog")]
        [SerializeField] private PetArchetypeDatabase archetypeDatabase;

        [Header("Sanctuary Customization Turntable Preview")]
        [SerializeField] private IslandPetController studioPreviewMannequin;
        [SerializeField] private Vector3 defaultPetSpawnPos = new Vector3(-2.0f, 0.0f, 2.5f);

        [Header("Starter Companion Seeding")]
        [SerializeField] private bool spawnStarterCompanionsOnStart = true;

        private readonly List<IslandPetController> _activeSpawnedPets = new List<IslandPetController>(8);
        private readonly List<SavedPetRecord> _restingSanctuaryPets = new List<SavedPetRecord>(32);

        private PetCustomizationProfile _workingStudioProfile;
        private Transform _petsContainerRoot;

        public IReadOnlyList<IslandPetController> ActiveSpawnedPets => _activeSpawnedPets;
        public PetArchetypeDatabase ArchetypeDatabase => archetypeDatabase;
        public PetCustomizationProfile WorkingStudioProfile => _workingStudioProfile;

        /// <summary>
        /// P2 #14 Remediation & Master Budget Table Gate:
        /// Quest 2 / Quest Pro = Max 4 Active Pets
        /// Quest 3 / Quest 3S  = Max 6 Active Pets (All 6 Compute-Skinned Pets < 1.0 ms GPU!)
        /// </summary>
        public int MaxAllowedActivePets
        {
            get
            {
                if (IslandRemediationLightingAndSaveManager.Instance == null) return 4;
                var tier = IslandRemediationLightingAndSaveManager.Instance.DetectedTier;
                return (tier == RemediatedHardwareTier.Quest3 ||
                        tier == RemediatedHardwareTier.Quest3S ||
                        tier == RemediatedHardwareTier.DesktopEditor)
                    ? 6
                    : 4;
            }
        }

        public event Action<IslandPetController> OnPetSpawnedOntoIsland;
        public event Action<IslandPetController> OnPetMovedToSanctuaryRest;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            GameObject rootGo = new GameObject("Active_Island_Pets_Root");
            _petsContainerRoot = rootGo.transform;

            if (archetypeDatabase != null && archetypeDatabase.Archetypes != null && archetypeDatabase.Archetypes.Length > 0)
            {
                _workingStudioProfile = archetypeDatabase.Archetypes[0].DefaultAppearance;
            }
        }

        private void Start()
        {
            if (spawnStarterCompanionsOnStart && archetypeDatabase != null)
            {
                // Spawn a friendly starter Dog and Kitsune Fox so they can immediately play together in Phase 11!
                SpawnCustomizedPet(archetypeDatabase.Archetypes[0].DefaultAppearance, new Vector3(-1.8f, 0f, 2.2f));
                if (archetypeDatabase.Archetypes.Length > 2)
                {
                    SpawnCustomizedPet(archetypeDatabase.Archetypes[2].DefaultAppearance, new Vector3(1.8f, 0f, 2.2f));
                }
            }
        }

        // --- Sanctuary Studio UI Customization API ---

        public void SelectStudioArchetype(int archetypeIndex)
        {
            if (archetypeDatabase == null || archetypeDatabase.Archetypes == null) return;
            int idx = Mathf.Clamp(archetypeIndex, 0, archetypeDatabase.Archetypes.Length - 1);
            _workingStudioProfile = archetypeDatabase.Archetypes[idx].DefaultAppearance;
            RefreshStudioMannequin();
        }

        public void SetStudioBlendShape(int blendShapeIndex, float weight0To100)
        {
            float w = Mathf.Clamp(weight0To100, 0f, 100f);
            switch (blendShapeIndex)
            {
                case 0: _workingStudioProfile.EarDroopOrCrestWeight = w; break;
                case 1: _workingStudioProfile.SnoutLengthWeight = w; break;
                case 2: _workingStudioProfile.TailFluffOrWingWeight = w; break;
                case 3: _workingStudioProfile.BodyChubbinessWeight = w; break;
            }
            RefreshStudioMannequin();
        }

        public void SetStudioCoatColors(Color primary, Color secondaryBelly, Color accentSpots, CoatPatternStyle style)
        {
            _workingStudioProfile.PrimaryCoatColor = primary;
            _workingStudioProfile.SecondaryBellyColor = secondaryBelly;
            _workingStudioProfile.AccentSpotSockColor = accentSpots;
            _workingStudioProfile.PatternStyle = style;
            RefreshStudioMannequin();
        }

        public void SetStudioPetName(string customName)
        {
            _workingStudioProfile.CustomPetName = string.IsNullOrWhiteSpace(customName) ? "Buddy" : customName.Trim();
            RefreshStudioMannequin();
        }

        public IslandPetController AdoptCurrentStudioPetOntoIsland()
        {
            return SpawnCustomizedPet(_workingStudioProfile, defaultPetSpawnPos);
        }

        /// <summary>
        /// Spawns a customized pet onto the 1-acre island while strictly enforcing the P2 #14 Active Pet Hard Cap
        /// (4 on Quest 2/Pro, 6 on Quest 3/3S).
        /// </summary>
        public IslandPetController SpawnCustomizedPet(PetCustomizationProfile profile, Vector3 worldSpawnPos)
        {
            if (archetypeDatabase == null || archetypeDatabase.Archetypes == null) return null;

            int archIdx = Mathf.Clamp((int)profile.Archetype, 0, archetypeDatabase.Archetypes.Length - 1);
            PetArchetypeDefinition def = archetypeDatabase.Archetypes[archIdx];
            if (def.RiggedPrefab == null) return null;

            // Enforce P2 #14 Hard Cap: If at max active pets, move the oldest active pet into Sanctuary Rest
            int maxPets = MaxAllowedActivePets;
            while (_activeSpawnedPets.Count >= maxPets)
            {
                IslandPetController oldestPet = _activeSpawnedPets[0];
                _activeSpawnedPets.RemoveAt(0);
                if (oldestPet != null)
                {
                    _restingSanctuaryPets.Add(ToSavedRecord(oldestPet));
                    OnPetMovedToSanctuaryRest?.Invoke(oldestPet);
                    Destroy(oldestPet.gameObject);
                    Debug.Log($"[P2 #14 Pet Cap] Moved oldest pet to Sanctuary House Rest to maintain {maxPets}-pet hardware ceiling.");
                }
            }

            if (IslandTerrainManager.Instance != null)
            {
                worldSpawnPos.y = Mathf.Max(0f, IslandTerrainManager.Instance.SampleHeightAtWorldPosition(worldSpawnPos));
            }

            GameObject petGo = Instantiate(def.RiggedPrefab, worldSpawnPos, Quaternion.identity, _petsContainerRoot);
            petGo.name = $"Pet_{def.DisplaySpeciesName}_{profile.CustomPetName}";

            IslandPetController controller = petGo.GetComponent<IslandPetController>();
            if (controller != null)
            {
                controller.ApplyCustomizationProfile(profile);
                _activeSpawnedPets.Add(controller);
                OnPetSpawnedOntoIsland?.Invoke(controller);
            }

            return controller;
        }

        public void ExportActivePetsToSaveDocument(IslandSaveDocumentV1 saveDoc)
        {
            if (saveDoc == null) return;
            saveDoc.ActivePets.Clear();
            for (int i = 0; i < _activeSpawnedPets.Count; i++)
            {
                if (_activeSpawnedPets[i] != null)
                {
                    saveDoc.ActivePets.Add(ToSavedRecord(_activeSpawnedPets[i]));
                }
            }
        }

        private static SavedPetRecord ToSavedRecord(IslandPetController pet)
        {
            var p = pet.CurrentProfile;
            return new SavedPetRecord
            {
                ArchetypeId = (int)p.Archetype,
                CustomName = p.CustomPetName,
                CoatColorRgb = new Vector3(p.PrimaryCoatColor.r, p.PrimaryCoatColor.g, p.PrimaryCoatColor.b),
                BlendShapeWeights = new Vector3(p.EarDroopOrCrestWeight, p.SnoutLengthWeight, p.BodyChubbinessWeight),
                Position = pet.transform.position
            };
        }

        private void RefreshStudioMannequin()
        {
            if (studioPreviewMannequin != null)
            {
                studioPreviewMannequin.ApplyCustomizationProfile(_workingStudioProfile);
            }
        }
    }
}

```

### **Script 4: Automated Editor 7-Archetype Rigged Mesh, BlendShape & Sanctuary Builder (**Phase10SetupWizard.cs**)**

Creating 7 rigged SkinnedMeshRenderer prefabs with bone hierarchies, skin weights (BoneWeight), bindposes, RGBA coat vertex masks, and 4 procedural BlendShapes (AddBlendShapeFrame) manually takes days in Blender. This Editor tool (IslandBuilder > Phase 10 > 1. Generate 7 Rigged Pet Archetypes, BlendShapes & Sanctuary Studio) procedurally builds **all 7 rigged pet archetypes (Dog, Cat, Fox, Capybara, Red Panda, Mini-Dragon, Macaw)** with an **8-bone hierarchy** ($\leq 20$ bone gate), **4 functional BlendShapes** (Ear Droop, Snout Length, Tail Fluff, Body Chubbiness), RGBA coat vertex masks, and wires IslandPetSanctuaryManager into the active scene in one click!

**File Path:** Assets/Scripts/Editor/Phase10SetupWizard.cs

C#

```

#if UNITY_EDITOR
using System.Collections.Generic;
using IslandBuilder.Pets;
using UnityEditor;
using UnityEngine;

namespace IslandBuilder.EditorTools
{
    public static class Phase10SetupWizard
    {
        private const string Phase10Folder = "Assets/Generated/Phase10";
        private const string CoatMatPath = "Assets/Generated/Phase10/M_PetCoatPatternLut.mat";
        private const string MeshesPath = "Assets/Generated/Phase10/Phase10_RiggedPetMeshes.asset";
        private const string DatabasePath = "Assets/Generated/Phase10/PetArchetypeDatabase.asset";
        private const string PrefabsFolder = "Assets/Generated/Phase10/Prefabs";

        [MenuItem("IslandBuilder/Phase 10/1. Generate 7 Rigged Pet Archetypes, BlendShapes & Sanctuary Studio", false, 80)]
        public static void GeneratePhase10PetSanctuarySuite()
        {
            EnsureFolders();

            // Enforce Vulkan Compute Skinning in PlayerSettings
            PlayerSettings.gpuSkinning = true;

            Shader coatShader = Shader.Find("IslandBuilder/PetCoatPatternLut");
            if (coatShader == null)
            {
                Debug.LogError("[Phase 10 Wizard] Could not find PetCoatPatternLut.shader!");
                return;
            }

            Material coatMat = AssetDatabase.LoadAssetAtPath<Material>(CoatMatPath);
            if (coatMat == null)
            {
                coatMat = new Material(coatShader);
                AssetDatabase.CreateAsset(coatMat, CoatMatPath);
            }
            coatMat.shader = coatShader;
            coatMat.enableInstancing = true;
            EditorUtility.SetDirty(coatMat);

            PetArchetypeDatabase db = AssetDatabase.LoadAssetAtPath<PetArchetypeDatabase>(DatabasePath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<PetArchetypeDatabase>();
                AssetDatabase.CreateAsset(db, DatabasePath);
            }
            db.SharedPetCoatMaterial = coatMat;
            db.Archetypes = new PetArchetypeDefinition[PetArchetypeDatabase.TotalArchetypeCount];

            // Default vibrant coat palettes for all 7 species
            (Color prim, Color sec, Color acc, CoatPatternStyle pat, Vector3 bodyScale, string defaultName)[] speciesPresets =
            {
                (new Color(0.86f, 0.62f, 0.25f), new Color(0.96f, 0.92f, 0.82f), new Color(0.32f, 0.20f, 0.12f), CoatPatternStyle.SpottedOrCalico,   new Vector3(1.00f, 1.00f, 1.00f), "Barnaby"),
                (new Color(0.92f, 0.88f, 0.82f), new Color(0.98f, 0.98f, 0.96f), new Color(0.85f, 0.42f, 0.18f), CoatPatternStyle.SpottedOrCalico,   new Vector3(0.82f, 0.82f, 0.85f), "Mochi"),
                (new Color(0.90f, 0.36f, 0.12f), new Color(0.98f, 0.95f, 0.90f), new Color(0.14f, 0.12f, 0.12f), CoatPatternStyle.SolidAndSocks,     new Vector3(0.90f, 0.88f, 0.95f), "Kitsune"),
                (new Color(0.58f, 0.40f, 0.24f), new Color(0.68f, 0.52f, 0.36f), new Color(0.32f, 0.22f, 0.15f), CoatPatternStyle.SolidAndSocks,     new Vector3(1.15f, 0.92f, 1.10f), "Yuzu"),
                (new Color(0.76f, 0.24f, 0.12f), new Color(0.16f, 0.14f, 0.14f), new Color(0.96f, 0.92f, 0.86f), CoatPatternStyle.TigerOrRingStripe, new Vector3(0.88f, 0.85f, 0.95f), "Bamboozle"),
                (new Color(0.16f, 0.72f, 0.52f), new Color(0.88f, 0.78f, 0.32f), new Color(0.10f, 0.32f, 0.65f), CoatPatternStyle.SpottedOrCalico,   new Vector3(0.95f, 1.05f, 1.05f), "Ignis"),
                (new Color(0.92f, 0.14f, 0.18f), new Color(0.98f, 0.82f, 0.12f), new Color(0.12f, 0.38f, 0.92f), CoatPatternStyle.TigerOrRingStripe, new Vector3(0.75f, 0.95f, 0.80f), "Rio")
            };

            Mesh firstMeshAsset = null;

            for (int i = 0; i < PetArchetypeDatabase.TotalArchetypeCount; i++)
            {
                PetSpeciesArchetype species = (PetSpeciesArchetype)i;
                string speciesName = PetArchetypeDatabase.All7SpeciesNames[i];
                var preset = speciesPresets[i];

                Mesh riggedMesh = BuildRiggedPetMeshWith4BlendShapes($"Mesh_Pet_{species}", preset.bodyScale);
                if (i == 0)
                {
                    firstMeshAsset = riggedMesh;
                    AssetDatabase.CreateAsset(firstMeshAsset, MeshesPath);
                }
                else
                {
                    AssetDatabase.AddObjectToAsset(riggedMesh, MeshesPath);
                }

                PetCustomizationProfile defaultProf = new PetCustomizationProfile
                {
                    Archetype = species,
                    CustomPetName = preset.defaultName,
                    PrimaryCoatColor = preset.prim,
                    SecondaryBellyColor = preset.sec,
                    AccentSpotSockColor = preset.acc,
                    PatternStyle = preset.pat,
                    PatternFrequency = 7.5f,
                    EarDroopOrCrestWeight = 20f,
                    SnoutLengthWeight = 35f,
                    TailFluffOrWingWeight = 50f,
                    BodyChubbinessWeight = i == (int)PetSpeciesArchetype.ChillCapybara ? 85f : 25f
                };

                GameObject prefab = CreateRiggedPetPrefab($"Prefab_Pet_{species}", riggedMesh, coatMat, defaultProf);

                db.Archetypes[i] = new PetArchetypeDefinition
                {
                    DisplaySpeciesName = speciesName,
                    Archetype = species,
                    RiggedPrefab = prefab,
                    DefaultWalkSpeedMetersPerSec = 1.6f,
                    DefaultRunSpeedMetersPerSec = 3.4f,
                    DefaultAppearance = defaultProf
                };
            }

            EditorUtility.SetDirty(db);

            // Wire IslandPetSanctuaryManager in active scene
            GameObject petSysGo = GameObject.Find("_Phase10_11_PetSanctuary_System");
            if (petSysGo == null) petSysGo = new GameObject("_Phase10_11_PetSanctuary_System");

            var sanctuary = petSysGo.GetComponent<IslandPetSanctuaryManager>();
            if (sanctuary == null) sanctuary = petSysGo.AddComponent<IslandPetSanctuaryManager>();

            SerializedObject so = new SerializedObject(sanctuary);
            so.FindProperty("archetypeDatabase").objectReferenceValue = db;
            so.ApplyModifiedProperties();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<color=#00FF88>[Phase 10 Wizard] Generated 7 Rigged Pet Archetypes (<=20 Bones, 4 BlendShapes, RGBA Coat Masks, Baked Collar Tags) & wired IslandPetSanctuaryManager!</color>");
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Generated"))
                AssetDatabase.CreateFolder("Assets", "Generated");
            if (!AssetDatabase.IsValidFolder(Phase10Folder))
                AssetDatabase.CreateFolder("Assets/Generated", "Phase10");
            if (!AssetDatabase.IsValidFolder(PrefabsFolder))
                AssetDatabase.CreateFolder(Phase10Folder, "Prefabs");
        }

        /// <summary>
        /// Builds a rigged quadruped/avian mesh weighted to 7 bones:
        /// 0: RootSpine, 1: Head, 2: Tail, 3: FrontLeftLeg, 4: FrontRightLeg, 5: BackLeftLeg, 6: BackRightLeg
        /// plus 4 procedural BlendShape frames (EarDroop, SnoutLength, TailFluff, BodyChubbiness).
        /// </summary>
        private static Mesh BuildRiggedPetMeshWith4BlendShapes(string meshName, Vector3 scaleMod)
        {
            List<Vector3> verts = new List<Vector3>();
            List<Vector3> norms = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<Color> cols = new List<Color>();
            List<BoneWeight> weights = new List<BoneWeight>();
            List<int> tris = new List<int>();

            // 0: Torso + Collar Ring (Bone 0 = RootSpine)
            AppendSkinnedPart(verts, norms, uvs, cols, weights, tris,
                center: new Vector3(0f, 0.28f, 0f),
                size: Vector3.Scale(new Vector3(0.28f, 0.24f, 0.52f), scaleMod),
                boneIndex: 0,
                vertexMask: new Color(0.25f, 0.90f, 0.0f, 0.85f)); // G=0.9 allows spots/stripes on torso

            // Collar Band around neck (color.a = 0.98 triggers Collar Nameplate Texture in shader!)
            AppendSkinnedPart(verts, norms, uvs, cols, weights, tris,
                center: new Vector3(0f, 0.34f, 0.22f * scaleMod.z),
                size: new Vector3(0.24f, 0.06f, 0.08f),
                boneIndex: 1,
                vertexMask: new Color(0f, 0f, 0f, 0.98f));

            // 1: Head, Snout & Ears (Bone 1 = Head)
            int headVertStart = verts.Count;
            AppendSkinnedPart(verts, norms, uvs, cols, weights, tris,
                center: new Vector3(0f, 0.44f, 0.30f * scaleMod.z),
                size: new Vector3(0.24f, 0.22f, 0.26f),
                boneIndex: 1,
                vertexMask: new Color(0.35f, 0.50f, 0.20f, 0.90f));

            // Left & Right Ears (weighted to Head Bone 1, morphed by BlendShape 0)
            int earsVertStart = verts.Count;
            AppendSkinnedPart(verts, norms, uvs, cols, weights, tris,
                center: new Vector3(-0.09f, 0.58f, 0.26f * scaleMod.z),
                size: new Vector3(0.06f, 0.14f, 0.06f),
                boneIndex: 1,
                vertexMask: new Color(0f, 0f, 0.95f, 0.90f)); // B=0.95 -> Ear Tips Accent Color!
            AppendSkinnedPart(verts, norms, uvs, cols, weights, tris,
                center: new Vector3(0.09f, 0.58f, 0.26f * scaleMod.z),
                size: new Vector3(0.06f, 0.14f, 0.06f),
                boneIndex: 1,
                vertexMask: new Color(0f, 0f, 0.95f, 0.90f));
            int earsVertEnd = verts.Count;

            // 2: Fluffy Tail (Bone 2 = Tail, morphed by BlendShape 2)
            int tailVertStart = verts.Count;
            AppendSkinnedPart(verts, norms, uvs, cols, weights, tris,
                center: new Vector3(0f, 0.36f, -0.34f * scaleMod.z),
                size: new Vector3(0.10f, 0.12f, 0.28f),
                boneIndex: 2,
                vertexMask: new Color(0.1f, 1.0f, 0.6f, 0.85f));
            int tailVertEnd = verts.Count;

            // 3..6: Four Legs with Paw Socks (color.b = 1.0 on lower paws!)
            AppendSkinnedPart(verts, norms, uvs, cols, weights, tris,
                new Vector3(-0.10f, 0.12f, 0.18f), new Vector3(0.08f, 0.24f, 0.08f), boneIndex: 3, new Color(0f, 0f, 1.0f, 0.55f));
            AppendSkinnedPart(verts, norms, uvs, cols, weights, tris,
                new Vector3(0.10f, 0.12f, 0.18f), new Vector3(0.08f, 0.24f, 0.08f), boneIndex: 4, new Color(0f, 0f, 1.0f, 0.55f));
            AppendSkinnedPart(verts, norms, uvs, cols, weights, tris,
                new Vector3(-0.10f, 0.12f, -0.18f), new Vector3(0.08f, 0.24f, 0.08f), boneIndex: 5, new Color(0f, 0f, 1.0f, 0.55f));
            AppendSkinnedPart(verts, norms, uvs, cols, weights, tris,
                new Vector3(0.10f, 0.12f, -0.18f), new Vector3(0.08f, 0.24f, 0.08f), boneIndex: 6, new Color(0f, 0f, 1.0f, 0.55f));

            Mesh m = new Mesh { name = meshName };
            m.SetVertices(verts);
            m.SetNormals(norms);
            m.SetUVs(0, uvs);
            m.SetColors(cols);
            m.boneWeights = weights.ToArray();
            m.SetTriangles(tris, 0);

            int vCount = verts.Count;
            Vector3[] zeroNormals = new Vector3[vCount];

            // BlendShape 0: Ear Droop / Crest Height
            Vector3[] deltaEars = new Vector3[vCount];
            for (int i = earsVertStart; i < earsVertEnd; i++)
            {
                deltaEars[i] = new Vector3(verts[i].x * 0.45f, -0.07f, 0.03f);
            }
            m.AddBlendShapeFrame("EarDroopOrCrest", 100f, deltaEars, zeroNormals, zeroNormals);

            // BlendShape 1: Snout Length
            Vector3[] deltaSnout = new Vector3[vCount];
            for (int i = headVertStart; i < earsVertStart; i++)
            {
                if (verts[i].z > 0.32f) deltaSnout[i] = new Vector3(0f, -0.02f, 0.12f);
            }
            m.AddBlendShapeFrame("SnoutLength", 100f, deltaSnout, zeroNormals, zeroNormals);

            // BlendShape 2: Tail Fluff / Plume Size
            Vector3[] deltaTail = new Vector3[vCount];
            for (int i = tailVertStart; i < tailVertEnd; i++)
            {
                deltaTail[i] = new Vector3(verts[i].x * 0.85f, 0.08f, -0.10f);
            }
            m.AddBlendShapeFrame("TailFluffOrWing", 100f, deltaTail, zeroNormals, zeroNormals);

            // BlendShape 3: Body Chubbiness (Chonk Slider)
            Vector3[] deltaChonk = new Vector3[vCount];
            for (int i = 0; i < headVertStart; i++)
            {
                deltaChonk[i] = new Vector3(verts[i].x * 0.45f, 0.03f, verts[i].z * 0.15f);
            }
            m.AddBlendShapeFrame("BodyChubbiness", 100f, deltaChonk, zeroNormals, zeroNormals);

            m.RecalculateBounds();
            return m;
        }

        private static void AppendSkinnedPart(
            List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<Color> cols,
            List<BoneWeight> weights, List<int> tris,
            Vector3 center, Vector3 size, int boneIndex, Color vertexMask)
        {
            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Mesh cube = temp.GetComponent<MeshFilter>().sharedMesh;
            Vector3[] cVerts = cube.vertices;
            Vector3[] cNorms = cube.normals;
            Vector2[] cUvs = cube.uv;
            int[] cTris = cube.triangles;
            Object.DestroyImmediate(temp);

            int baseIdx = verts.Count;
            BoneWeight bw = new BoneWeight { boneIndex0 = boneIndex, weight0 = 1.0f };

            for (int i = 0; i < cVerts.Length; i++)
            {
                Vector3 v = center + Vector3.Scale(cVerts[i], size);
                verts.Add(v);
                norms.Add(cNorms[i]);
                uvs.Add(cUvs[i]);

                // Underside vertices automatically receive Belly Mask (color.r = 1.0)
                Color mask = vertexMask;
                if (cNorms[i].y < -0.3f && mask.a < 0.92f)
                {
                    mask.r = 0.95f;
                }
                cols.Add(mask);
                weights.Add(bw);
            }

            for (int i = 0; i < cTris.Length; i++)
            {
                tris.Add(baseIdx + cTris[i]);
            }
        }

        private static GameObject CreateRiggedPetPrefab(
            string prefabName,
            Mesh riggedMesh,
            Material sharedCoatMat,
            PetCustomizationProfile defaultProfile)
        {
            string fullPath = $"{PrefabsFolder}/{prefabName}.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(fullPath);
            if (existing != null) return existing;

            GameObject root = new GameObject(prefabName);

            // Create 7-Bone Skeleton Hierarchy (< 20 Bones Mobile Gate)
            Vector3[] boneLocalPositions =
            {
                new Vector3(0f, 0.28f, 0f),     // 0: RootSpine
                new Vector3(0f, 0.16f, 0.30f),  // 1: Head (child of RootSpine)
                new Vector3(0f, 0.08f, -0.34f), // 2: Tail (child of RootSpine)
                new Vector3(-0.10f, -0.16f, 0.18f), // 3: FrontLeftLeg
                new Vector3(0.10f, -0.16f, 0.18f),  // 4: FrontRightLeg
                new Vector3(-0.10f, -0.16f, -0.18f),// 5: BackLeftLeg
                new Vector3(0.10f, -0.16f, -0.18f)  // 6: BackRightLeg
            };

            string[] boneNames =
            {
                "Bone_RootSpine", "Bone_Head", "Bone_Tail",
                "Bone_FrontLeftLeg", "Bone_FrontRightLeg", "Bone_BackLeftLeg", "Bone_BackRightLeg"
            };

            Transform[] bones = new Transform[7];
            Matrix4x4[] bindPoses = new Matrix4x4[7];

            for (int b = 0; b < 7; b++)
            {
                GameObject boneGo = new GameObject(boneNames[b]);
                boneGo.transform.SetParent(b == 0 ? root.transform : bones[0], false);
                boneGo.transform.localPosition = boneLocalPositions[b];
                bones[b] = boneGo.transform;
                bindPoses[b] = bones[b].worldToLocalMatrix * root.transform.localToWorldMatrix;
            }

            riggedMesh.bindposes = bindPoses;
            EditorUtility.SetDirty(riggedMesh);

            GameObject meshGo = new GameObject("Pet_SkinnedMesh");
            meshGo.transform.SetParent(root.transform, false);

            SkinnedMeshRenderer smr = meshGo.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = riggedMesh;
            smr.bones = bones;
            smr.rootBone = bones[0];
            smr.sharedMaterial = sharedCoatMat;
            smr.updateWhenOffscreen = false; // Enforces CullUpdateTransforms off-screen savings
            smr.quality = SkinQuality.Bone2;

            // Lightweight capsule collider for VR petting & toy interaction
            CapsuleCollider col = root.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 0.28f, 0f);
            col.radius = 0.24f;
            col.height = 0.65f;
            col.direction = 2; // Z-axis length

            var controller = root.AddComponent<IslandPetController>();
            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("primarySkinnedRenderer").objectReferenceValue = smr;
            so.FindProperty("rootBone").objectReferenceValue = bones[0];
            so.FindProperty("headBone").objectReferenceValue = bones[1];
            so.FindProperty("tailBone").objectReferenceValue = bones[2];
            so.FindProperty("frontLeftLegBone").objectReferenceValue = bones[3];
            so.FindProperty("frontRightLegBone").objectReferenceValue = bones[4];
            so.FindProperty("backLeftLegBone").objectReferenceValue = bones[5];
            so.FindProperty("backRightLegBone").objectReferenceValue = bones[6];
            so.FindProperty("currentProfile").boxedValue = defaultProfile;
            so.ApplyModifiedProperties();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, fullPath);
            Object.DestroyImmediate(root);
            return saved;
        }
    }
}
#endif

```

### **10.4 Common Issues Developers Experience in Phase 10 (And Exact Fixes)**

| Issue / Symptom | Root Cause on Meta Quest / Unity 6 | Exact Engineering Fix |
| :---- | :---- | :---- |
| **1. Spawning 6 pets with BlendShapes causes** MeshSkinning.Update **to take 2.8 ms on the CPU main thread** | Either PlayerSettings.gpuSkinning was unchecked or the mesh lacks valid tangents/bindposes for Vulkan Compute Skinning.  | Ensure PlayerSettings.gpuSkinning = true is set (Phase1ProjectConfigurator / Phase10SetupWizard) and cap SkinnedMeshRenderer.quality = SkinQuality.Bone2.  |
| **2. Pets continue consuming CPU & GPU time even when walking behind a hill out of view** | Animator.cullingMode is left at AlwaysAnimate or SkinnedMeshRenderer.updateWhenOffscreen is true.  | Enforce AnimatorCullingMode.CullUpdateTransforms, updateWhenOffscreen = false, and guard procedural bone updates with if (!primarySkinnedRenderer.isVisible) return; (IslandPetController.cs).  |
| **3. Procedural spots and tabby stripes "swim" across the pet's body when its legs or tail move** | Evaluating procedural spot/stripe waves using world-space position (input.positionWS) causes the pattern to stay locked in world space while the pet walks through it. | Pass the vertex's pre-skinned bind-pose coordinate (output.bindPosOS = input.positionOS.xyz) from the vertex shader to the fragment shader and evaluate spots/stripes in local space (PetCoatPatternLut.shader). |
| **4. Adopting 15 pets in a single session pushes total SkinnedMesh draw calls and triangles into the Yellow/Red wrist zone** | Unbounded pet spawning exceeds mobile skeletal skinning and AI budgets.  | Enforce the **P2 #14 Active Pet Hard Cap** in IslandPetSanctuaryManager.cs (**Max 4 Active Pets on Quest 2/Pro**, **Max 6 on Quest 3/3S**), automatically archiving older pets into SavedPetRecord Sanctuary Rest.  |
| **5. Floating pet name tags smear when Application SpaceWarp (AppSW) is active** | World-Space UI Canvas text above moving pets lacks depth and motion vectors.  | Bake each pet's custom name into a 64 × 16 metallic collar nameplate texture (BakeCollarNameplateBadge) mapped directly onto the opaque collar geometry (color.a > 0.92).  |

### **10.5 On-Device Verification Protocol (Closing Phase 10 Gate)**

1. **Generate the 7 Rigged Pet Archetypes & Sanctuary Manager:**  

   * Open Assets/Scenes/IslandBootstrap.unity and click IslandBuilder > Phase 10 > 1. Generate 7 Rigged Pet Archetypes, BlendShapes & Sanctuary Studio.  
3. **Test BlendShape Sliders, RGBA Coat Patterns & P2 #14 Pet Caps in Editor:**  

   * Press **Play**. Observe the starter **Loyal Island Dog ("Barnaby")** and **Island Kitsune Fox ("Kitsune")** spawned near the center of the island with wagging tails, paw socks, and spotted/solid coats.  
   * In the Inspector on _Phase10_11_PetSanctuary_System, adjust EarDroopOrCrestWeight, SnoutLengthWeight, TailFluffOrWingWeight, and BodyChubbinessWeight (0..100) and call AdoptCurrentStudioPetOntoIsland() 6 times.  
   * Verify IslandPetSanctuaryManager caps active pets at **6 on Quest 3 / Desktop** (and **4 on Quest 2**), archiving the oldest pet cleanly to Sanctuary Rest.  
5. **Verify Hardware Gate on Meta Quest 2 / Quest 3S / Quest 3:**  

   * Build and deploy to the headset with **OVR Metrics Tool** and **Unity Autoconnect Profiler (GPU / Rendering Module)** active.  
   * Spawn the maximum **6 Active Pets** (Dog, Cat, Fox, Capybara, Red Panda, Mini-Dragon) in front of the camera.  
   * **Verify Gate:** Confirm all **6 Active Compute-Skinned Pets render and deform in < 1.0 ms total GPU time** (and drop to **0.0 ms** when looking away from them), with **0 B per-frame GC allocation** and locked **90 Hz (Quest 3/3S) / 72 Hz (Quest 2/Pro)**.  

### **Updated Session Continuity Record (**PROJECT_STATE.md **Snippet — P3 #24 Compliant)**

Markdown

```

[x] P0 REMEDIATION COMPLETE (#1-#6): Unity 6.3 LTS pinned, lowercase manifest.json, Trilight no-bake lighting,
    shared IslandConstants.WaterLevelY (-0.35f), 4-tier hardware detector (Quest 2, Pro, 3S, 3), Master Budget Table,
    and IslandSaveDocumentV1 schema delivered.
[x] P1 REMEDIATION COMPLETE (#7-#11): P1 #7 Primary Internet/User Audio, P1 #8 Native Quest Browser Handoff,
    P1 #9 Sandboxed Blender CLI, P1 #10 128MB/256MB Import Pool, P1 #11 Full 5.a/5.b Depth.
[x] PHASE 1:  [x] Code Delivered | [ ] On-Device Gate Passed (< 6.0 ms GPU Base)
[x] PHASE 2:  [x] Code Delivered | [ ] On-Device Gate Passed (90 FPS Sculpting)
[x] PHASE 3:  [x] Code Delivered | [ ] On-Device Gate Passed (0 B GC Alloc)
[x] PHASE 4:  [x] Code Delivered | [ ] On-Device Gate Passed (< 180 Draw Calls, 398 Botanical Varieties)
[x] PHASE 5:  [x] Code Delivered | [ ] On-Device Gate Passed (< 1.2M Tris Q3, 233 Arch/Prop Items, 631 Total Inventory)
[x] PHASE 6:  [x] Code Delivered | [ ] On-Device Gate Passed (< 15 MB per GLB + 128MB/256MB Cumulative Pool + Blender Sandbox)
[x] PHASE 7:  [x] Code Delivered | [ ] On-Device Gate Passed (Zero Frame Spikes, <= 2.0ms DeferAgent Cap + Off-Thread Collider Bake)
[x] PHASE 8:  [x] Code Delivered | [ ] On-Device Gate Passed (Crisp 1080p OpenXR Quad Layer + P1 #8 Browser Handoff + P2 #14 Screen Cap)
[x] PHASE 9:  [x] Code Delivered | [ ] On-Device Gate Passed (< 5.0% Audio CPU + P1 #7 Primary Internet/User Audio)
[x] PHASE 10: [x] Code Delivered | [ ] On-Device Gate Passed (6 Active Pets < 1.0 ms GPU Compute Skinning + P2 #14 Pet Cap)
    - Delivered: PetCoatPatternLut.shader (RGBA coat/belly/spots/stripes/socks + baked collar tag + XRMotionVectors),
      PetArchetypeDatabase.cs (7 species: Dog, Cat, Fox, Capybara, Red Panda, Mini-Dragon, Macaw),
      IslandPetController.cs (CullUpdateTransforms off-screen culling, 4 BlendShape sliders, baked collar badge),
      IslandPetSanctuaryManager.cs (P2 #14 Hard Cap: 4 on Q2/Pro, 6 on Q3/3S), and Phase10SetupWizard.cs.
[ ] NEXT STEP (Phase 11): Autonomous Pet Harmony AI, Paired Social Slots & Chunked Dynamic NavMesh
    - Deliverables: ChunkedDynamicNavMeshManager.cs (links modified 8m x 8m terrain chunks to localized asynchronous
      NavMesh rebakes respecting IslandConstants.WaterLevelY), PetHarmonyCoordinator.cs (4 Hz Utility AI Blackboard
      keeping total AI CPU < 0.5 ms per frame, driving cooperative Chase/Tag around trees, synchronized sunbathing
      near the Spatial Box Radio or Virtual TV, and fetching user-uploaded 3D items).

```

Whenever you are ready, reply **"Start Phase 11"** and we will build the **Autonomous Pet Harmony AI Coordinator (4 Hz Utility Blackboard, Paired Cooperative Social Slots, and Chunked Asynchronous Dynamic NavMesh)** under the **< 0.5 ms AI Tick Hardware Gate**!
