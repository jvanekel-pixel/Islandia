Shader "IslandBuilder/Phase1BaselineVertexColor"
{
    Properties
    {
        _BaseColor ("Base Tint", Color) = (1, 1, 1, 1)
        _AmbientFloor ("Ambient Floor", Range(0.0, 1.0)) = 0.45
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }
        LOD 100

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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                 half4 color       : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 vertexColor : COLOR;
                half  ndotl       : TEXCOORD0;
                half  fogFactor   : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half  _AmbientFloor;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = posInputs.positionCS;
                output.vertexColor = input.color * _BaseColor;

                // Evaluate directional light NdotL in the vertex shader to save fragment math on mobile tiles
                Light mainLight = GetMainLight();
                half ndotl = saturate(dot(normInputs.normalWS, mainLight.direction));
                output.ndotl = lerp(_AmbientFloor, 1.0h, ndotl);

                // Vertex Fogging for Opaque (Zero fragment texture fetch overhead)
                output.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half3 litColor = input.vertexColor.rgb * input.ndotl;
                litColor = MixFog(litColor, input.fogFactor);
                return half4(litColor, 1.0h);
            }
            ENDHLSL
        }

        // Built-in URP XR Motion Vectors pass so Application SpaceWarp (AppSW) synthesizes clean frames
        UsePass "Universal Render Pipeline/Lit/XRMotionVectors"
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
