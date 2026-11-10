Shader "IslandBuilder/BotanicalVertexLUT"
{
    Properties
    {
        [NoScaleOffset] _BotanicalPaletteLUT ("Botanical Palette & Cultivar LUT (X: Gradient, Y: Cultivar Row)", 2D) = "white" {}
        _AmbientFloor ("Baked AO Minimum Floor", Range(0.15, 0.85)) = 0.38
        _SunWrap ("Half-Lambert Sun Wrap", Range(0.0, 0.5)) = 0.35
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry+10"
        }
        LOD 150

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            // 100% Opaque: Zero alpha blending overdraw, keeps Adreno Early-Z / HSR active
            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BotanicalPaletteLUT);
            SAMPLER(sampler_BotanicalPaletteLUT);

            CBUFFER_START(UnityPerMaterial)
                half _AmbientFloor;
                half _SunWrap;
            CBUFFER_END

            // Per-instance V coordinate into _BotanicalPaletteLUT:
            // Allows all 10 flower cultivars of the same base mesh to batch in ONE draw call!
            UNITY_INSTANCING_BUFFER_START(BotanicalInstanceProps)
                UNITY_DEFINE_INSTANCED_PROP(float4, _InstanceColorAndCultivar) // rgb = tint, a = LUT V row [0..1]
            UNITY_INSTANCING_BUFFER_END(BotanicalInstanceProps)

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                // Vertex Color Convention:
                // R = Gradient coordinate (0 = base/trunk, 1 = tip/petal edge)
                // G = Part Mask (0.0 = Bark/Stem/Leaf row, 1.0 = Flower Petal Cultivar row)
                // B = Baked Static Ambient Occlusion (0.0 = deep root shadow, 1.0 = exposed crown)
                // A = Subsurface scattering / rim warmth weight
                half4  color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4  vertColor  : COLOR;
                float2 lutUv      : TEXCOORD0;
                half3  instanceTint : TEXCOORD1;
                half2  lightAndFog  : TEXCOORD2; // x = wrapped NdotL * BakedAO, y = vertex fog
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                // NOTE: World Position Offset (WPO) wind animation is intentionally omitted
                // per VR Archviz optimization mandate to avoid tile-boundary vertex cost.
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = posInputs.positionCS;
                output.vertColor = input.color;

                float4 instData = UNITY_ACCESS_INSTANCED_PROP(BotanicalInstanceProps, _InstanceColorAndCultivar);
                output.instanceTint = instData.rgb;

                // If vertex is stem/leaf (color.g < 0.5), sample row V = 0.05 (Lush Foliage/Bark row).
                // If vertex is petal/blossom (color.g >= 0.5), sample the per-instance cultivar row V = instData.a.
                float lutRowV = lerp(0.05, instData.a, step(0.5, (float)input.color.g));
                output.lutUv = float2(input.color.r, lutRowV);

                // Evaluate directional light + Baked Static AO in the vertex shader (zero fragment light overhead)
                Light mainLight = GetMainLight();
                half ndotl = saturate(dot(normInputs.normalWS, mainLight.direction) * (1.0h - _SunWrap) + _SunWrap);
                half bakedAo = lerp(_AmbientFloor, 1.0h, input.color.b);

                output.lightAndFog.x = ndotl * bakedAo;
                output.lightAndFog.y = ComputeFogFactor(posInputs.positionCS.z);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // Dependent LUT texture read (fast on Adreno tile cache)
                half3 paletteColor = SAMPLE_TEXTURE2D(_BotanicalPaletteLUT, sampler_BotanicalPaletteLUT, input.lutUv).rgb;
                half3 shadedColor = paletteColor * input.instanceTint * input.lightAndFog.x;

                // Subtle warm canopy tip translucency stored in vertex alpha
                shadedColor += paletteColor * (input.vertColor.a * 0.12h);

                // Vertex Fogging for Opaque
                shadedColor = MixFog(shadedColor, input.lightAndFog.y);
                return half4(shadedColor, 1.0h);
            }
            ENDHLSL
        }

        // Dynamic ShadowCaster pass is intentionally omitted on foliage per Blueprint Pillar 4.
        // XRMotionVectors pass included so Application SpaceWarp (AppSW) never smears trees or flowers.
        UsePass "Universal Render Pipeline/Lit/XRMotionVectors"
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
