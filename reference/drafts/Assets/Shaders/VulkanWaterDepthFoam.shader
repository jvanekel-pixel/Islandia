Shader "IslandBuilder/VulkanWaterDepthFoam"
{
    Properties
    {
        _ShallowColor ("Shallow Turquoise Color", Color) = (0.12, 0.72, 0.76, 1.0)
        _DeepColor ("Deep Ocean Blue Color", Color) = (0.03, 0.24, 0.46, 1.0)
        _SandBedColor ("Submerged Sand Bed Tint", Color) = (0.82, 0.75, 0.58, 1.0)
        _FoamColor ("Shoreline Foam Color", Color) = (0.96, 0.98, 1.0, 1.0)
        _FoamThresholdMeters ("Shoreline Foam Width (Meters)", Range(0.05, 1.5)) = 0.45
        _DepthFadeMeters ("Shallow to Deep Transition (Meters)", Range(0.5, 6.0)) = 2.40
        _WaveSpeed ("Shoreline Ripple Speed", Float) = 1.6
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            // Render immediately after Opaque geometry (2000) so terrain depth is already in on-chip tile memory
            "Queue" = "Geometry+50"
        }
        LOD 100

        Pass
        {
            Name "VulkanOnTileWaterPass"
            Tags { "LightMode" = "UniversalForward" }

            // Fully opaque write eliminates Linear-space alpha blending fill-rate penalty on Quest GPUs
            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // On Vulkan / Adreno tile GPUs, Unity 6 URP Render Graph exposes the current tile's depth
            // via Vulkan Subpass Input / Framebuffer Fetch when _CameraDepthTexture copy is disabled.
            #if defined(SHADER_API_VULKAN) && !defined(UNITY_EDITOR)
                #define USE_VULKAN_ON_TILE_DEPTH_ATTACHMENT 1
                FRAMEBUFFER_INPUT_FLOAT(0);
            #endif

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                half4 _SandBedColor;
                half4 _FoamColor;
                float _FoamThresholdMeters;
                float _DepthFadeMeters;
                float _WaveSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float  eyeDepth   : TEXCOORD1;
                half   fogFactor  : TEXCOORD2;
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
                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.eyeDepth = -TransformWorldToView(posInputs.positionWS).z;
                output.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float waterDepthMeters = _DepthFadeMeters;

                #if defined(USE_VULKAN_ON_TILE_DEPTH_ATTACHMENT)
                    // Read underlying terrain depth directly from Adreno on-chip tile memory (0 GMEM copy!)
                    float rawTileDepth = LOAD_FRAMEBUFFER_INPUT(0, input.positionCS.xy).x;
                    float sceneEyeDepth = LinearEyeDepth(rawTileDepth, _ZBufferParams);
                    waterDepthMeters = max(0.0, sceneEyeDepth - input.eyeDepth);
                #else
                    // Desktop Editor / Non-Subpass analytic fallback around the 64m x 64m island perimeter
                    float2 p = abs(input.positionWS.xz) - float2(23.5, 23.5);
                    float distFromShore = length(max(p, 0.0)) + min(max(p.x, p.y), 0.0) - 5.5;
                    waterDepthMeters = saturate(abs(distFromShore) / 3.0) * _DepthFadeMeters;
                #endif

                // 1. Blend from submerged sandy beach tint -> shallow turquoise -> deep ocean blue
                half shallowFactor = saturate(waterDepthMeters / _DepthFadeMeters);
                half sandVisibility = saturate(1.0h - (waterDepthMeters / (_FoamThresholdMeters * 2.2h)));
                half3 waterCol = lerp(_ShallowColor.rgb, _DeepColor.rgb, shallowFactor);
                waterCol = lerp(waterCol, _SandBedColor.rgb, sandVisibility * 0.55h);

                // 2. Animated dual-band shoreline foam ring wherever terraformed hills/ponds meet the water
                float wavePhase = sin(_Time.y * _WaveSpeed + (input.positionWS.x + input.positionWS.z) * 1.75) * 0.06;
                float effectiveDepth = waterDepthMeters + wavePhase;

                half primaryFoam = 1.0h - saturate(effectiveDepth / _FoamThresholdMeters);
                half secondaryRipple = smoothstep(0.65h, 0.85h, sin((effectiveDepth / _FoamThresholdMeters) * 12.0 - _Time.y * 2.5))
                                       * (1.0h - saturate(effectiveDepth / (_FoamThresholdMeters * 1.6)));

                half foamMask = saturate(step(0.35h, primaryFoam) + secondaryRipple * 0.75h);
                half3 finalColor = lerp(waterCol, _FoamColor.rgb, foamMask);

                // 3. Horizon Vertex Fog
                finalColor = MixFog(finalColor, input.fogFactor);
                return half4(finalColor, 1.0h);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/XRMotionVectors"
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
