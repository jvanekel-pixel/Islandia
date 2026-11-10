Shader "IslandBuilder/CompositorVideoScreenPunchThrough"
{
    Properties
    {
        [MainTexture] _VideoTex ("1080p Video / Web Texture", 2D) = "black" {}
        _ScreenBrightness ("Screen Brightness", Range(0.0, 2.0)) = 1.0
        [Toggle(_COMPOSITOR_UNDERLAY_HOLE)] _IsCompositorUnderlayHole ("Punch Alpha/Depth Hole for OpenXR Underlay", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            // Render right after opaque geometry so depth is written for hands/props in front of the TV
            "Queue" = "Geometry+25"
        }
        LOD 100

        Pass
        {
            Name "VideoScreenForward"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma shader_feature_local _COMPOSITOR_UNDERLAY_HOLE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_VideoTex);
            SAMPLER(sampler_VideoTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _VideoTex_ST;
                half   _ScreenBrightness;
                half   _IsCompositorUnderlayHole;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _VideoTex);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                #if defined(_COMPOSITOR_UNDERLAY_HOLE) && !defined(UNITY_EDITOR)
                    // Punch a 0-alpha hole into the eye buffer while ZWrite On records the screen's 16-bit depth,
                    // allowing the 1920x1080 OpenXR Underlay Quad Layer to show through with perfect controller occlusion!
                    return half4(0.0h, 0.0h, 0.0h, 0.0h);
                #else
                    half3 vidCol = SAMPLE_TEXTURE2D(_VideoTex, sampler_VideoTex, input.uv).rgb * _ScreenBrightness;
                    return half4(vidCol, 1.0h);
                #endif
            }
            ENDHLSL
        }

        // Mandatory so Application SpaceWarp (AppSW) never warps or smears the TV frame!
        UsePass "Universal Render Pipeline/Lit/XRMotionVectors"
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
