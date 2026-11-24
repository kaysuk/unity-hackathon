Shader "Temple/Nimb Altar Link"
{
    Properties
    {
        [HDR] _Gold ("Gilt thread", Color) = (1.12, 0.61, 0.23, 1)
        [HDR] _Glint ("Moving glint", Color) = (2.2, 1.37, 0.55, 1)
        [HDR] _Garnet ("Garnet source", Color) = (1.55, 0.17, 0.25, 1)
        [HideInInspector] _DistanceStart ("Distance from altar", Float) = 0
        [HideInInspector] _DistanceLength ("Segment length", Float) = 1
        [HideInInspector] _TotalDistance ("Full thread length", Float) = 1
        [HideInInspector] _BreakProgress ("Break progress", Float) = -1
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Cull Off
            ZTest LEqual
            ZWrite Off
            Blend One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Gold;
                half4 _Glint;
                half4 _Garnet;
                float _DistanceStart;
                float _DistanceLength;
                float _TotalDistance;
                float _BreakProgress;
            CBUFFER_END

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(float3 positionOS : POSITION, float2 uv : TEXCOORD0)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(positionOS);
                output.uv = uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float meters = _DistanceStart + input.uv.x * _DistanceLength;
                float across = abs(input.uv.y * 2.0 - 1.0);
                float phase = frac((meters - _Time.y * 7.5) * 0.137);
                // A long soft tail approaches a short bright head in the altar-to-Nimb direction.
                float packet = smoothstep(0.20, 0.70, phase) * (1.0 - smoothstep(0.71, 0.84, phase));
                float bright = packet * (0.82 + 0.18 * sin(_Time.y * 2.9 - meters * 0.32));
                float source = 1.0 - smoothstep(0.18, 1.14, meters);
                float core = exp2(-24.0 * across * across);
                float halo = exp2(-5.5 * across * across) * (1.0 - smoothstep(0.72, 1.0, across));
                float alpha = core * (0.34 + 0.31 * bright) + halo * (0.09 + 0.14 * bright + 0.12 * source);
                half3 color = lerp(_Gold.rgb, _Glint.rgb, bright * 0.74);
                color = lerp(color, _Garnet.rgb, source * 0.90);

                if (_BreakProgress >= 0.0)
                {
                    float severedAt = _BreakProgress * (_TotalDistance + 0.6);
                    float fromBreak = meters - severedAt;
                    float pulse = exp2(-2.0 * abs(fromBreak)) * (1.0 - _BreakProgress);
                    alpha *= smoothstep(-0.24, 0.38, fromBreak) * (1.0 - 0.65 * _BreakProgress);
                    alpha += pulse * (core * 0.48 + halo * 0.19);
                    color = lerp(color, lerp(_Garnet.rgb, _Glint.rgb, 0.63), pulse);
                }

                float segmentMeters = input.uv.x * _DistanceLength;
                float ends = smoothstep(0.0, 0.09, segmentMeters) *
                    (1.0 - smoothstep(_DistanceLength - 0.12, _DistanceLength, segmentMeters));
                alpha = saturate(alpha * ends);
                return half4(color * alpha, alpha);
            }
            ENDHLSL
        }
    }
}
