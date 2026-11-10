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
