Shader "Temple/Nimb Protection Aura"
{
    Properties
    {
        [HDR] _Amber ("Soft amber", Color) = (1.14, 0.59, 0.16, 1)
        [HDR] _Gold ("Gold", Color) = (1.85, 1.26, 0.45, 1)
        [HDR] _Glint ("Pale gold", Color) = (1.78, 1.62, 1.13, 1)
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent+10" }
        Pass
        {
            Name "Protection"
            Cull Back
            ZWrite Off
            ZTest LEqual
            Blend One One
            ColorMask RGB

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Amber;
                half4 _Gold;
                half4 _Glint;
            CBUFFER_END

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 positionOS : TEXCOORD1;
            };

            Varyings Vert(float3 position : POSITION)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(position);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.positionOS = position;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 shell = normalize(input.positionOS);
                float3 view = TransformWorldToObjectDir(GetWorldSpaceNormalizeViewDir(input.positionWS));
                float facing = saturate(dot(shell, view));
                float inset = 1.0 - sqrt(saturate(1.0 - facing * facing));

                float time = _Time.y;
                float flow = 8.0 * shell.y + 4.2 * shell.x + 2.3 * shell.z - time * 1.8;
                float wave = sin(flow + 1.1 * sin(3.0 * shell.z + time * 0.7));
                float fineWave = sin(flow * 2.7 + 3.0 * shell.x + time * 1.1);
                float lip = 0.057 + 0.015 * wave + 0.004 * fineWave;
                float offset = inset - lip;
                float feather = smoothstep(0.0, 0.027, inset);

                // The visible contour sits inside the mesh edge. Its halo fades to clear on both sides.
                float core = exp2(-offset * offset * 5900.0);
                float halo = exp2(-offset * offset * 510.0);
                float glint = smoothstep(0.48, 0.98, wave) * (0.73 + 0.27 * fineWave);
                float pulse = 0.92 + 0.08 * sin(time * 2.0);
                half3 light = _Amber.rgb * halo * (0.09 + 0.05 * wave)
                    + _Gold.rgb * core * (0.27 + 0.10 * wave)
                    + _Glint.rgb * core * glint * 0.32;

                return half4(light * feather * pulse, 0.0);
            }
            ENDHLSL
        }
    }
}
