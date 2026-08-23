Shader "Hidden/OriginCore/BlackFogOfWar"
{
    Properties
    {
        _FogColor ("Fog Color", Color) = (0.0, 0.0, 0.0, 1.0)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+200"
        }

        Pass
        {
            Name "OriginCoreBlackFogOfWar"
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            ZTest Always
            ColorMask RGB

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_OriginCoreFogTex);
            SAMPLER(sampler_OriginCoreFogTex);

            float4 _OriginCoreFogWorldRect;
            float _OriginCoreFogActive;

            CBUFFER_START(UnityPerMaterial)
                half4 _FogColor;
            CBUFFER_END

            struct Attributes
            {
                uint vertexID : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                output.uv = GetFullScreenTriangleTexCoord(input.vertexID);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                if (_OriginCoreFogActive < 0.5)
                {
                    return half4(0, 0, 0, 0);
                }

                float rawDepth = SampleSceneDepth(input.uv);
                #if UNITY_REVERSED_Z
                    if (rawDepth <= 0.00001)
                    {
                        return half4(0, 0, 0, 0);
                    }
                #else
                    if (rawDepth >= 0.99999)
                    {
                        return half4(0, 0, 0, 0);
                    }
                    rawDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
                #endif

                float3 worldPosition = ComputeWorldSpacePosition(
                    input.uv,
                    rawDepth,
                    UNITY_MATRIX_I_VP);
                float2 worldMinimum = _OriginCoreFogWorldRect.xy;
                float2 worldSize = max(
                    _OriginCoreFogWorldRect.zw,
                    float2(0.0001, 0.0001));
                float2 worldMaximum = worldMinimum + worldSize;

                // The visibility texture only owns the playable map. Geometry and
                // sky beyond it must remain untouched instead of inheriting a
                // clamped edge texel.
                if (worldPosition.x < worldMinimum.x ||
                    worldPosition.x > worldMaximum.x ||
                    worldPosition.z < worldMinimum.y ||
                    worldPosition.z > worldMaximum.y)
                {
                    return half4(0, 0, 0, 0);
                }

                float2 fogUv = (worldPosition.xz - worldMinimum) / worldSize;
                half fogAlpha = SAMPLE_TEXTURE2D(
                    _OriginCoreFogTex,
                    sampler_OriginCoreFogTex,
                    fogUv).a;
                return half4(_FogColor.rgb, fogAlpha * _FogColor.a);
            }
            ENDHLSL
        }
    }
}
