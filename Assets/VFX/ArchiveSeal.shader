Shader "Temple/Archive Seal"
{
    Properties
    {
        _BaseMap ("Carved stone", 2D) = "white" {}
        _EmissionMap ("Engraving", 2D) = "black" {}
        [HDR] _EmissionColor ("Lit engraving", Color) = (0,0,0,1)
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

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_EmissionMap);
            SAMPLER(sampler_EmissionMap);
            CBUFFER_START(UnityPerMaterial)
                half4 _EmissionColor;
            CBUFFER_END

            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(float3 position : POSITION, float2 uv : TEXCOORD0)
            {
                Varyings output;
                output.position = TransformObjectToHClip(position);
                output.uv = uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 stone = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb;
                half engraving = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).r;
                return half4(stone * 0.22h + engraving * _EmissionColor.rgb, 1);
            }
            ENDHLSL
        }
    }
}
