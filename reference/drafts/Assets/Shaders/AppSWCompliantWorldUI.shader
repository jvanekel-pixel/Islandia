Shader "IslandBuilder/AppSWCompliantWorldUI"
{
    Properties
    {
        [MainTexture] _BaseMap ("UI / Compositor Texture", 2D) = "white" {}
        [MainColor] _BaseColor ("Tint Color", Color) = (1, 1, 1, 1)
        _AlphaCutoff ("Alpha Cutoff", Range(0.01, 0.99)) = 0.25
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "AlphaTest"
        }
        LOD 100

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            // ZWrite On + Alpha Clip ensures 0 transparent alpha-blending overdraw and writes 16-bit depth for AppSW
            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half   _AlphaCutoff;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
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
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.color = input.color * _BaseColor;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * input.color;
                clip(texColor.a - _AlphaCutoff);
                return half4(texColor.rgb, 1.0h);
            }
            ENDHLSL
        }

        // Critical: Outputs depth and velocity to prevent Application SpaceWarp pixel-pulling behind UI/Gizmos
        UsePass "Universal Render Pipeline/Lit/XRMotionVectors"
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
