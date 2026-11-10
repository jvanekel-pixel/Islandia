Shader "IslandBuilder/IslandTerrainUber"
{
    Properties
    {
        [NoScaleOffset] _TerrainAlbedoArray ("Terrain Albedo Array (4 Slices: Sand, Grass, Rock, Path)", 2DArray) = "" {}
        [NoScaleOffset] _ShadingAndTurfLut ("Lighting & Astroturf Sheen LUT", 2D) = "white" {}
        _WorldUvScale ("World Tiling Scale", Float) = 0.35
        _CliffTriplanarScale ("Cliff Triplanar Scale", Float) = 0.20
        _AmbientFloor ("Ambient Light Floor", Range(0.1, 0.8)) = 0.42
        [Toggle(_ASTROTURF_MODE_ON)] _UseAstroturfMode ("Enable Astroturf Micro-Sheen Mode", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }
        LOD 200

        Pass
        {
            Name "ForwardLit"
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
            #pragma shader_feature_local _ASTROTURF_MODE_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D_ARRAY(_TerrainAlbedoArray);
            SAMPLER(sampler_TerrainAlbedoArray);

            TEXTURE2D(_ShadingAndTurfLut);
            SAMPLER(sampler_ShadingAndTurfLut);

            CBUFFER_START(UnityPerMaterial)
                float _WorldUvScale;
                float _CliffTriplanarScale;
                half  _AmbientFloor;
                half  _UseAstroturfMode;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                half4  color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                half3  normalWS    : TEXCOORD1;
                half4  splatWeight : COLOR;
                half2  lightAndFog : TEXCOORD2; // x = NdotL, y = vertex fog factor
                float4 shadowCoord : TEXCOORD3;
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
                output.normalWS = normInputs.normalWS;
                output.splatWeight = input.color;

                Light mainLight = GetMainLight();
                half ndotl = saturate(dot(normInputs.normalWS, mainLight.direction) * 0.5h + 0.5h);
                output.lightAndFog.x = ndotl;
                output.lightAndFog.y = ComputeFogFactor(posInputs.positionCS.z);
                output.shadowCoord = GetShadowCoord(posInputs);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 planarUv = input.positionWS.xz * _WorldUvScale;
                half4 w = input.splatWeight;

                // Sample Texture2DArray slices: 0 = Sand, 1 = Grass/Turf, 2 = Cliff Rock, 3 = Garden Path
                half3 colSand  = SAMPLE_TEXTURE2D_ARRAY(_TerrainAlbedoArray, sampler_TerrainAlbedoArray, planarUv, 0.0).rgb;
                half3 colGrass = SAMPLE_TEXTURE2D_ARRAY(_TerrainAlbedoArray, sampler_TerrainAlbedoArray, planarUv, 1.0).rgb;
                half3 colPath  = SAMPLE_TEXTURE2D_ARRAY(_TerrainAlbedoArray, sampler_TerrainAlbedoArray, planarUv * 1.5, 3.0).rgb;

                // Fast bi-planar projection for steep mountain cliff faces (Slice 2) to prevent UV stretching
                float2 cliffUvX = input.positionWS.zy * _CliffTriplanarScale;
                float2 cliffUvZ = input.positionWS.xy * _CliffTriplanarScale;
                half blendX = abs(input.normalWS.x);
                half3 colRock = lerp(
                    SAMPLE_TEXTURE2D_ARRAY(_TerrainAlbedoArray, sampler_TerrainAlbedoArray, cliffUvZ, 2.0).rgb,
                    SAMPLE_TEXTURE2D_ARRAY(_TerrainAlbedoArray, sampler_TerrainAlbedoArray, cliffUvX, 2.0).rgb,
                    blendX);

                #if defined(_ASTROTURF_MODE_ON)
                    // Astroturf Mode: zero-polygon micro-stripe turf pattern + synthetic sheen LUT
                    half stripe = step(0.0h, sin(input.positionWS.x * 3.14159h * 0.5h));
                    half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                    half fresnel = saturate(1.0h - dot(normalize(input.normalWS), viewDirWS));
                    half3 turfLut = SAMPLE_TEXTURE2D(_ShadingAndTurfLut, sampler_ShadingAndTurfLut, float2(fresnel, 0.75)).rgb;
                    colGrass = lerp(colGrass * 0.92h, colGrass * 1.06h, stripe) + turfLut * (fresnel * 0.14h);
                #endif

                half3 blendedAlbedo = colSand * w.r + colGrass * w.g + colRock * w.b + colPath * w.a;

                // Dependent LUT read for warm tropical lighting ramp + Main Light shadow attenuation
                Light mainLight = GetMainLight(input.shadowCoord);
                half shadowAtten = mainLight.shadowAttenuation;
                half lutCoordX = saturate(input.lightAndFog.x * shadowAtten);
                half3 lightRamp = SAMPLE_TEXTURE2D(_ShadingAndTurfLut, sampler_ShadingAndTurfLut, float2(lutCoordX, 0.25)).rgb;
                half3 finalLit = blendedAlbedo * max(lightRamp, _AmbientFloor.xxx);

                // Vertex Fogging for Opaque
                finalLit = MixFog(finalLit, input.lightAndFog.y);
                return half4(finalLit, 1.0h);
            }
            ENDHLSL
        }

        // ShadowCaster & XRMotionVectors passes for Application SpaceWarp compatibility
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/XRMotionVectors"
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
