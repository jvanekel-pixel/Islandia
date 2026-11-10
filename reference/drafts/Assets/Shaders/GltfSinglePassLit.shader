Shader "IslandBuilder/GltfSinglePassLit"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Color (KTX2 ASTC)", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color Factor", Color) = (1, 1, 1, 1)
        [NoScaleOffset] _MetallicGlossMap ("Metallic (B) Roughness (G) Map", 2D) = "white" {}
        _Metallic ("Metallic Factor", Range(0.0, 1.0)) = 0.0
        _Roughness ("Roughness Factor", Range(0.0, 1.0)) = 0.65
        [NoScaleOffset] _EmissionMap ("Emissive Map", 2D) = "black" {}
        [HDR] _EmissionColor ("Emissive Factor", Color) = (0, 0, 0, 0)
        _AmbientFloor ("Trilight Ambient Floor", Range(0.15, 0.75)) = 0.38
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry+15"
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
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            TEXTURE2D(_MetallicGlossMap);
            SAMPLER(sampler_MetallicGlossMap);

            TEXTURE2D(_EmissionMap);
            SAMPLER(sampler_EmissionMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half   _Metallic;
                half   _Roughness;
                half4  _EmissionColor;
                half   _AmbientFloor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                half3  normalWS    : TEXCOORD1;
                float2 uv          : TEXCOORD2;
                half4  vertColor   : COLOR;
                half   fogFactor   : TEXCOORD3;
                float4 shadowCoord : TEXCOORD4;
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
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.vertColor = input.color;
                output.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
                output.shadowCoord = GetShadowCoord(posInputs);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 baseTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                // Note: If source model has no vertex colors, Unity defaults input.color to (1,1,1,1)
                half3 albedo = baseTex.rgb * _BaseColor.rgb * input.vertColor.rgb;

                // glTF 2.0 Metallic-Roughness convention: G = Roughness, B = Metallic
                half4 mrSample = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, input.uv);
                half roughness = saturate(mrSample.g * _Roughness);
                half metallic = saturate(mrSample.b * _Metallic);

                Light mainLight = GetMainLight(input.shadowCoord);
                half3 normalWS = normalize(input.normalWS);
                half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half3 halfDir = normalize(mainLight.direction + viewDirWS);

                half ndotl = saturate(dot(normalWS, mainLight.direction));
                half shadowAtten = lerp(0.50h, 1.0h, mainLight.shadowAttenuation);

                // Lightweight mobile Blinn-Phong / GGX approximation (avoids full PBR register pressure on Adreno)
                half nh = saturate(dot(normalWS, halfDir));
                half specPower = exp2(10.0h * (1.0h - roughness) + 1.0h);
                half specTerm = pow(nh, specPower) * (1.0h - roughness) * lerp(0.04h, 0.5h, metallic);

                half3 ambient = SampleSHVertex(normalWS);
                half3 diffuseLight = max(_AmbientFloor.xxx, ambient * 0.5h + mainLight.color * (ndotl * shadowAtten));

                half3 litColor = albedo * diffuseLight + mainLight.color * (specTerm * shadowAtten);

                // Emissive contribution
                half3 emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb * _EmissionColor.rgb;
                litColor += emission;

                // Horizon Vertex Fogging
                litColor = MixFog(litColor, input.fogFactor);
                return half4(litColor, 1.0h);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/XRMotionVectors"
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
