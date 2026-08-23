Shader "OriginCore/Terrain/Anime Grass"
{
    Properties
    {
        _GrassColor ("Grass Color", Color) = (0.62, 0.88, 0.68, 1)
        _GrassShadowColor ("Grass Shadow Color", Color) = (0.36, 0.65, 0.48, 1)
        _CliffColor ("Cliff Color", Color) = (0.92, 0.86, 0.72, 1)
        _CliffShadowColor ("Cliff Shadow Color", Color) = (0.64, 0.56, 0.43, 1)
        _TopThreshold ("Top Surface Threshold", Range(0, 1)) = 0.68
        _BlendWidth ("Surface Blend Width", Range(0.01, 0.3)) = 0.10
        _VariationStrength ("Color Variation", Range(0, 0.2)) = 0.035
        _VariationScale ("Variation Scale", Range(0.05, 4)) = 0.65
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_OriginCoreFogTex);
            SAMPLER(sampler_OriginCoreFogTex);

            float4 _OriginCoreFogWorldRect;
            float _OriginCoreFogActive;

            CBUFFER_START(UnityPerMaterial)
                half4 _GrassColor;
                half4 _GrassShadowColor;
                half4 _CliffColor;
                half4 _CliffShadowColor;
                half _TopThreshold;
                half _BlendWidth;
                half _VariationStrength;
                half _VariationScale;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float4 shadowCoord : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);
                output.positionHCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.shadowCoord = GetShadowCoord(positionInputs);
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half HashVariation(float2 position)
            {
                float value = sin(dot(position, float2(1.71, 2.43))) * 0.5 + 0.5;
                return (half)((value - 0.5) * 2.0);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 normalWS = normalize(input.normalWS);
                half topBlend = smoothstep(
                    _TopThreshold - _BlendWidth,
                    _TopThreshold + _BlendWidth,
                    normalWS.y);

                Light mainLight = GetMainLight(input.shadowCoord);
                half diffuse = saturate(dot(normalWS, mainLight.direction));
                half shadow = mainLight.shadowAttenuation * mainLight.distanceAttenuation;
                half litAmount = saturate(0.35h + diffuse * shadow * 0.65h);

                half3 grass = lerp(_GrassShadowColor.rgb, _GrassColor.rgb, litAmount);
                half3 cliff = lerp(_CliffShadowColor.rgb, _CliffColor.rgb, litAmount);
                half3 color = lerp(cliff, grass, topBlend);

                half variation = HashVariation(input.positionWS.xz * _VariationScale);
                color *= 1.0h + variation * _VariationStrength;
                color *= lerp(1.0h.xxx, mainLight.color, 0.22h);
                color = MixFog(color, input.fogFactor);

                // Custom terrain consumes the authoritative mask directly from
                // its real world position. This avoids depending on whether the
                // active URP renderer builds CameraDepthTexture by copy or by a
                // depth prepass; units and buildings remain covered by the
                // full-screen fog-of-war feature.
                if (_OriginCoreFogActive >= 0.5)
                {
                    float2 worldMinimum = _OriginCoreFogWorldRect.xy;
                    float2 worldSize = max(
                        _OriginCoreFogWorldRect.zw,
                        float2(0.0001, 0.0001));
                    float2 worldMaximum = worldMinimum + worldSize;
                    if (input.positionWS.x >= worldMinimum.x &&
                        input.positionWS.x <= worldMaximum.x &&
                        input.positionWS.z >= worldMinimum.y &&
                        input.positionWS.z <= worldMaximum.y)
                    {
                        float2 fogUv =
                            (input.positionWS.xz - worldMinimum) / worldSize;
                        half fogAlpha = SAMPLE_TEXTURE2D(
                            _OriginCoreFogTex,
                            sampler_OriginCoreFogTex,
                            fogUv).a;
                        color = lerp(color, 0.0h.xxx, fogAlpha);
                    }
                }

                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float3 _LightDirection;

            Varyings ShadowVert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                positionWS = ApplyShadowBias(positionWS, normalWS, _LightDirection);
                output.positionHCS = TransformWorldToHClip(positionWS);

                #if UNITY_REVERSED_Z
                    output.positionHCS.z = min(output.positionHCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    output.positionHCS.z = max(output.positionHCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ZTest LEqual
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthOnlyVert
            #pragma fragment DepthOnlyFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct DepthOnlyAttributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DepthOnlyVaryings
            {
                float4 positionHCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DepthOnlyVaryings DepthOnlyVert(DepthOnlyAttributes input)
            {
                DepthOnlyVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 DepthOnlyFrag(DepthOnlyVaryings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
