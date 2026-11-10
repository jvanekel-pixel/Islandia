Shader "IslandBuilder/ArchitecturalAtlasUber"
{
    Properties
    {
        [MainTexture] _ArchAtlasMap ("Architectural ASTC Atlas (2048x2048)", 2D) = "white" {}
        [NoScaleOffset] _GlassMatCapTex ("Opaque Glass MatCap Reflection", 2D) = "gray" {}
        _EmissiveGlowColor ("Baked Lantern / Torch Glow Color", Color) = (1.0, 0.62, 0.22, 1.0)
        _EmissiveIntensity ("Baked Glow Intensity", Range(0.0, 4.0)) = 1.85
        _AmbientFloor ("Baked AO Shadow Floor", Range(0.15, 0.80)) = 0.40
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry+5"
        }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            // 100% Opaque (Even for Conservatory Glass via MatCap!) -> Zero Linear Alpha-Blending Cost
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

            TEXTURE2D(_ArchAtlasMap);
            SAMPLER(sampler_ArchAtlasMap);

            TEXTURE2D(_GlassMatCapTex);
            SAMPLER(sampler_GlassMatCapTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _ArchAtlasMap_ST;
                half4  _EmissiveGlowColor;
                half   _EmissiveIntensity;
                half   _AmbientFloor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                // R = Theme Tint Weight, G = Opaque MatCap Glass Mask, B = Baked Emissive Glow, A = Baked Static AO
                half4  color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float2 uv          : TEXCOORD0;
                half2  matcapUv    : TEXCOORD1;
                half4  vertColor   : COLOR;
                half2  lightAndFog : TEXCOORD2; // x = NdotL * AO, y = Vertex Fog Factor
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
                output.uv = TRANSFORM_TEX(input.uv, _ArchAtlasMap);
                output.vertColor = input.color;

                // Compute View-Space Normal XY in Vertex Shader for ultra-cheap Opaque Glass MatCap lookup
                float3 normalVS = TransformWorldToViewDir(normInputs.normalWS, true);
                output.matcapUv = normalVS.xy * 0.5h + 0.5h;

                Light mainLight = GetMainLight();
                half ndotl = saturate(dot(normInputs.normalWS, mainLight.direction) * 0.5h + 0.5h);
                half bakedAo = lerp(_AmbientFloor, 1.0h, input.color.a);

                output.lightAndFog.x = ndotl * bakedAo;
                output.lightAndFog.y = ComputeFogFactor(posInputs.positionCS.z);
                output.shadowCoord = GetShadowCoord(posInputs);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half3 atlasAlbedo = SAMPLE_TEXTURE2D(_ArchAtlasMap, sampler_ArchAtlasMap, input.uv).rgb;

                // 1. Standard Sun + Baked AO Shading
                Light mainLight = GetMainLight(input.shadowCoord);
                half shadowAtten = lerp(0.55h, 1.0h, mainLight.shadowAttenuation);
                half3 litSurface = atlasAlbedo * (input.lightAndFog.x * shadowAtten);

                // 2. Opaque MatCap Glass Reflection (Conservatories & Windows: input.vertColor.g > 0)
                half glassMask = input.vertColor.g;
                if (glassMask > 0.01h)
                {
                    half3 matCapSky = SAMPLE_TEXTURE2D(_GlassMatCapTex, sampler_GlassMatCapTex, input.matcapUv).rgb;
                    half3 glassTint = half3(0.55h, 0.78h, 0.88h);
                    litSurface = lerp(litSurface, matCapSky * glassTint + matCapSky * 0.45h, glassMask);
                }

                // 3. Baked Emissive Lantern / Torch / FirePit Glow (Zero real-time point light cost!)
                half emissiveMask = input.vertColor.b;
                litSurface += _EmissiveGlowColor.rgb * (emissiveMask * _EmissiveIntensity);

                // 4. Horizon Vertex Fogging
                litSurface = MixFog(litSurface, input.lightAndFog.y);
                return half4(litSurface, 1.0h);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/XRMotionVectors"
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
