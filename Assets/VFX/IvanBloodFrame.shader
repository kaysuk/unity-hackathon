Shader "UI/Ivan Blood Frame"
{
    Properties
    {
        [PerRendererData] _MainTex ("UI Texture", 2D) = "white" {}
        _Spatter ("Blood spatter", 2D) = "white" {}
        _Blood ("Blood", Color) = (0.46, 0.070, 0.084, 1)
        _DarkBlood ("Dark blood", Color) = (0.19, 0.028, 0.041, 1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _Spatter;
            half4 _Blood, _DarkBlood;

            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                half severity : TEXCOORD1;
            };

            Varyings vert(appdata_full input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = input.texcoord.xy;
                output.severity = input.color.a;
                return output;
            }

            // Local coordinates use the shorter screen dimension so drops keep their shape.
            float edgeStain(float2 offset, float2 inverseRadius)
            {
                return 1.0 - length(offset * inverseRadius);
            }

            // A thin gravity trail finishes in a heavier bead with a small wet highlight.
            half2 bloodRun(float2 fromTop, float startX, float runLength, float drift, float pixel)
            {
                float t = saturate(fromTop.y / runLength);
                float x = fromTop.x - startX - drift * t * (2.0 - t);
                float width = lerp(0.0026, 0.0010, t);
                float thread = (1.0 - smoothstep(width, width + pixel, abs(x)))
                             * (1.0 - smoothstep(runLength - pixel, runLength + pixel, fromTop.y));
                float2 tip = float2(x, (fromTop.y - runLength) * 0.72);
                float bead = 1.0 - smoothstep(0.0034, 0.0034 + pixel, length(tip));
                float glint = 1.0 - smoothstep(0.0006, 0.0006 + pixel, length(tip + float2(0.0014, 0.0012)));
                return half2(max(thread * 0.72, bead), glint);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float pixel = rcp(min(_ScreenParams.x, _ScreenParams.y));
                float2 extent = _ScreenParams.xy * pixel;
                float2 p = input.uv * extent;
                float2 margins = min(p, extent - p);
                if (min(margins.x, margins.y) > 0.28 || input.severity <= 0.0) return 0;

                // Separate pools interrupt the silhouette along each edge and leave open space between them.
                float left = edgeStain(p - float2(-0.115, extent.y * 0.70), float2(3.70, 1.92));
                float foot = edgeStain(p - float2(0.05, -0.095), float2(2.00, 4.35));
                float right = edgeStain(p - float2(extent.x + 0.105, extent.y * 0.31), float2(4.10, 2.50));
                float crown = edgeStain(p - float2(extent.x * 0.83, extent.y + 0.11), float2(2.05, 4.65));
                float silhouette = max(max(left, foot), max(right, crown));

                float2 tilted = float2(0.88 * p.x - 0.48 * p.y, 0.48 * p.x + 0.88 * p.y);
                half blot = tex2D(_Spatter, frac(tilted * 3.4 + float2(0.16, 0.43))).a;
                half grain = tex2D(_Spatter, frac(p * float2(13.7, 10.3) + blot * 0.13)).a;
                float contour = silhouette + 0.31 * (blot - 0.50) + 0.11 * (grain - 0.50)
                              - lerp(0.32, 0.10, input.severity);

                half wash = smoothstep(-0.13, 0.20, contour);
                half deposit = smoothstep(-0.025, 0.055, contour);
                // Pigment gathers in a narrow, broken drying line around the translucent wash.
                half dryingLine = deposit * (1.0 - smoothstep(0.045, 0.13, contour)) * (0.32 + 0.68 * grain);
                half density = 0.18 * wash + deposit * (0.16 + 0.31 * blot) + 0.25 * dryingLine;

                half2 runs = 0;
                float down = extent.y - p.y;
                half wet = smoothstep(0.35, 0.80, input.severity);
                if (wet > 0.0 && down < 0.28)
                {
                    float2 fromTop = float2(p.x, down);
                    runs = max(bloodRun(fromTop, 0.084, lerp(0.065, 0.205, wet), 0.009, pixel),
                               bloodRun(fromTop, extent.x - 0.13, lerp(0.10, 0.254, wet), -0.0065, pixel)) * wet;
                }

                half3 pigment = lerp(_Blood.rgb, _DarkBlood.rgb, saturate(0.22 + 0.60 * blot + dryingLine));
                pigment = lerp(pigment, _Blood.rgb, runs.x);
                pigment += half3(0.15, 0.06, 0.048) * runs.y;
                return half4(pigment, saturate(max(density, runs.x * 0.78)) * input.severity);
            }
            ENDCG
        }
    }
}
