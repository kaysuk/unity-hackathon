Shader "Temple/Investigation Highlight"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "Soft reflected light"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend One One
            ColorMask RGB
            ZWrite Off
            Cull Off
            // Only shade pixels where the original opaque surface wrote depth.
            // Texture cutouts and intervening walls keep their original appearance.
            ZTest Equal

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half facing : TEXCOORD0;
            };

            Varyings Vert(float3 positionOS : POSITION, float3 normalOS : NORMAL)
            {
                float3 positionWS = TransformObjectToWorld(positionOS);
                half3 normalWS = TransformObjectToWorldNormal(normalOS);
                half3 viewWS = GetWorldSpaceNormalizeViewDir(positionWS);
                Varyings output;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.facing = saturate(abs(dot(normalWS, viewWS)));
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half rim = 1.0h - input.facing;
                half3 reflected = half3(0.085h, 0.049h, 0.021h) * (0.48h + 0.52h * rim * rim);
                return half4(reflected, 0.0h);
            }
            ENDHLSL
        }
    }
}
