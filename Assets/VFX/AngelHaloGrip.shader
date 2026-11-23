Shader "Temple/Halo Grip"
{
    Properties
    {
        _Palm ("Palm position", Vector) = (0, 0, 0, 0)
        _Signal ("Release signal", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Palm;
                float _Signal;
            CBUFFER_END

            struct Varyings
            {
                float4 clip : SV_POSITION;
                float3 world : TEXCOORD0;
                half3 normal : TEXCOORD1;
            };

            Varyings Vert(float3 position : POSITION, float3 normal : NORMAL)
            {
                Varyings o;
                o.world = TransformObjectToWorld(position);
                o.clip = TransformWorldToHClip(o.world);
                o.normal = TransformObjectToWorldNormal(normal);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float facing = saturate(dot(normalize(i.normal), GetWorldSpaceNormalizeViewDir(i.world)));
                float edge = pow(1 - facing, 2);
                float hand = 1 - smoothstep(0.12, 0.48, distance(_Palm.xyz, i.world));
                half3 ember = lerp(half3(0.48, 0.08, 0.018), half3(1.7, 0.71, 0.12), hand);
                half3 flame = lerp(half3(0.08, 0.62, 0.80), half3(1.4, 2.5, 2.7), hand);
                return half4(lerp(ember, flame, _Signal) * (0.72 + edge * 0.85), 1);
            }
            ENDHLSL
        }
    }
}
