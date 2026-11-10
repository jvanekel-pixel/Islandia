Shader "IslandBuilder/BoxRadioVuMeter"
{
    Properties
    {
        _FaceplateColor ("Radio Dial Faceplate Color", Color) = (0.08, 0.10, 0.14, 1.0)
        _LowVuColor ("VU Meter Low (Emerald)", Color) = (0.10, 0.92, 0.45, 1.0)
        _HighVuColor ("VU Meter Peak (Amber/Coral)", Color) = (1.00, 0.38, 0.18, 1.0)
        _TunerNeedleColor ("Frequency Needle Color", Color) = (0.20, 0.88, 1.00, 1.0)
        _AudioRmsLevel ("Live Audio RMS Level (0..1)", Range(0.0, 1.0)) = 0.25
        _TuningNormalized ("Station Dial Position (0..1)", Range(0.0, 1.0)) = 0.20
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry+12"
        }
        LOD 100

        Pass
        {
            Name "ForwardUnlitVu"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _FaceplateColor;
                half4 _LowVuColor;
                half4 _HighVuColor;
                half4 _TunerNeedleColor;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(RadioVuProps)
                UNITY_DEFINE_INSTANCED_PROP(float, _AudioRmsLevel)
                UNITY_DEFINE_INSTANCED_PROP(float, _TuningNormalized)
            UNITY_INSTANCING_BUFFER_END(RadioVuProps)

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
                half   rmsLevel   : TEXCOORD1;
                half   tuningPos  : TEXCOORD2;
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
                output.uv = input.uv;
                output.rmsLevel = (half)UNITY_ACCESS_INSTANCED_PROP(RadioVuProps, _AudioRmsLevel);
                output.tuningPos = (half)UNITY_ACCESS_INSTANCED_PROP(RadioVuProps, _TuningNormalized);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.uv;
                half3 col = _FaceplateColor.rgb;

                // Top Half (uv.y >= 0.5): Retro Frequency Scale + Glowing Station Needle
                if (uv.y >= 0.5)
                {
                    half tickMarks = step(0.85h, frac(uv.x * 20.0h)) * step(abs(uv.y - 0.75h), 0.12h);
                    col += half3(0.25h, 0.32h, 0.40h) * tickMarks;

                    half needleDist = abs(uv.x - input.tuningPos);
                    half needleMask = smoothstep(0.025h, 0.005h, needleDist);
                    col = lerp(col, _TunerNeedleColor.rgb * 1.5h, needleMask);
                }
                // Bottom Half (uv.y < 0.5): Segmented 16-Bar LED VU Meter
                else
                {
                    float segmentX = floor(uv.x * 16.0) / 16.0;
                    half segGap = step(0.18h, frac(uv.x * 16.0h)) * step(abs(uv.y - 0.25h), 0.14h);
                    half isLit = step(segmentX, input.rmsLevel) * segGap;
                    half3 vuGrad = lerp(_LowVuColor.rgb, _HighVuColor.rgb, segmentX);
                    col = lerp(col + half3(0.04h, 0.05h, 0.06h) * segGap, vuGrad * 1.4h, isLit);
                }

                return half4(col, 1.0h);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/XRMotionVectors"
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
