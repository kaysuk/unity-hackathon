Shader "Temple/Angel Seal Rim"
{
    Properties { _Charge ("Charge", Range(0, 1)) = 0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Charge;
            CBUFFER_END

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float alongRim : TEXCOORD0;
            };

            Varyings Vert(float3 positionOS : POSITION, float2 uv : TEXCOORD0)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(positionOS);
                output.alongRim = uv.x;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Several crests keep the pulse visible even with only a short arc on screen.
                float phase = frac(input.alongRim * 6 - _Charge * (2 + 3 * _Charge));
                float crest = smoothstep(0.40, 0.95, phase);
                float urgency = smoothstep(0.42, 0.95, _Charge);
                half3 rim = lerp(half3(1.4, 0.42, 0.035), half3(2.4, 0.055, 0.008), urgency);
                return half4(lerp(rim, half3(3.3, 2.7, 1.5), crest), 1);
            }
            ENDHLSL
        }
    }
}
