Shader "Temple/Nimb Rune Dissolve"
{
    Properties
    {
        _RuneMap ("Rune texture", 2D) = "white" {}
        _RuneElevation ("Mesh limits and inverse size", Vector) = (0, 1, 0, 1)
        _RuneCut ("Erasure", Range(0, 1)) = 0
        _RuneFlash ("Break flash", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" }
        Pass
        {
            Name "Rune dissolve"
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_RuneMap);
            SAMPLER(sampler_RuneMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _RuneMap_ST;
                float4 _RuneElevation;
                float _RuneCut;
                float _RuneFlash;
            CBUFFER_END

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 glyph : TEXCOORD0;
                float2 uv : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
            };

            Varyings Vert(float3 position : POSITION, float3 normal : NORMAL, float2 uv : TEXCOORD0)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(position);
                output.glyph = float2((position.x - _RuneElevation.z) * _RuneElevation.w,
                    (position.y - _RuneElevation.x) * _RuneElevation.y);
                output.uv = TRANSFORM_TEX(uv, _RuneMap);
                output.normalWS = TransformObjectToWorldNormal(normal);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 glyph = input.glyph;
                float coarse = sin(16.0 * glyph.x + 3.5 * glyph.y);
                float fine = sin(53.0 * glyph.x - 4.0 * glyph.y) * sin(19.0 * glyph.y + 17.0 * glyph.x);
                float front = glyph.y + .065 * coarse + .028 * fine - lerp(-.15, 1.11, _RuneCut);
                float width = max(.016, fwidth(front) * 1.15);
                clip(front);

                float ember = 1.0 - smoothstep(.018, max(.082, width * 2.2), front);
                float edge = 1.0 - smoothstep(width * .23, width, front);
                float glint = smoothstep(.56, .98, sin(82.0 * glyph.x + 38.0 * glyph.y));
                half3 textureColor = SAMPLE_TEXTURE2D(_RuneMap, sampler_RuneMap, input.uv).rgb;
                half facing = .60 + .40 * saturate(dot(normalize(input.normalWS), half3(.46, .79, .40)));
                half3 gold = textureColor * half3(.79, .46, .16) * facing;
                half3 glow = _RuneFlash * half3(2.15, 1.29, .42)
                    + ember * half3(.80, .20, .031)
                    + edge * half3(1.58, .92, .27) * (.79 + .49 * glint);
                return half4(gold + glow, 1.0);
            }
            ENDHLSL
        }
    }
}
