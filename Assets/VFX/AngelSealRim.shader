Shader "Temple/Angel Seal Rim"
{
    Properties
    {
        _Charge ("Charge", Range(0, 1)) = 0
        [HideInInspector] _SealLayer ("Seal layer", Float) = 0
        [HideInInspector] _BeamProgress ("Beam progress", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Cull Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Charge;
                float _SealLayer;
                float _BeamProgress;
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

            float SegmentDistance(float2 p, float2 a, float2 b)
            {
                float2 edge = b - a;
                float t = saturate(dot(p - a, edge) / dot(edge, edge));
                return length(p - a - t * edge);
            }

            half4 DrawEye(float2 uv)
            {
                float2 p = uv * 2.0 - 1.0;
                float aa = max(0.001, 0.65 * max(fwidth(p.x), fwidth(p.y)));
                // Keep the pupil halfway between the apex and base, at the centre of the strike.
                const float halfHeight = 0.59;
                float2 apex = float2(0.0, halfHeight);
                float2 corner = float2(0.72, -halfHeight);
                float side = SegmentDistance(float2(abs(p.x), p.y), apex, corner);
                float lowerEdge = SegmentDistance(p, float2(-corner.x, corner.y), corner);
                float frame = min(side, lowerEdge) - 0.012;

                // Two circular arcs open symmetrically around a stationary iris.
                const float halfSpan = 0.31;
                float opening = lerp(0.047, 0.151, smoothstep(0.05, 0.82, _Charge));
                float arcCenter = (halfSpan * halfSpan - opening * opening) / (2.0 * opening);
                float lens = length(float2(p.x, abs(p.y) + arcCenter)) - (arcCenter + opening);
                float taper = saturate(1.0 - abs(p.x) / halfSpan);
                float lidWidth = 0.008 + taper * lerp(0.006, 0.020, smoothstep(-0.05, 0.06, p.y));
                float lid = abs(lens) - lidWidth;
                float contour = min(frame, lid);
                float etching = 1.0 - smoothstep(-aa, aa, contour);
                float fill = 1.0 - smoothstep(-aa, aa, lens);

                float radial = length(p);
                float iris = 1.0 - smoothstep(0.100 - aa, 0.100 + aa, radial);
                float pupilSize = lerp(0.057, 0.042, smoothstep(0.35, 0.95, _Charge));
                float pupil = 1.0 - smoothstep(pupilSize - aa, pupilSize + aa, radial);
                float angle = atan2(p.y, p.x + 0.00001);
                float fibers = 0.5 + 0.5 * cos(17.0 * angle + 0.7 * sin(5.0 * angle));
                float resolveFibers = smoothstep(0.0, 0.012, 0.017 - aa);
                half3 irisGold = lerp(half3(0.68, 0.25, 0.050), half3(1.55, 0.80, 0.19),
                    0.68 - 0.3 * smoothstep(0.050, 0.100, radial) + (fibers - 0.5) * 0.42 * resolveFibers);
                half3 eyeColor = lerp(half3(0.135, 0.066, 0.024), irisGold, iris);
                eyeColor = lerp(eyeColor, half3(0.014, 0.008, 0.004), pupil);
                float glint = 1.0 - smoothstep(0.010 - aa, 0.010 + aa, length(p - float2(-0.021, 0.025)));
                eyeColor = lerp(eyeColor, half3(2.1, 1.48, 0.65), glint);

                half3 gold = lerp(half3(1.05, 0.50, 0.11), half3(2.15, 1.34, 0.48), _Charge);
                float glow = 0.17 * exp2(-80.0 * max(0.0, contour));
                float haloAlpha = max(etching, glow);
                float faceAlpha = fill * 0.96;
                float alpha = faceAlpha + haloAlpha * (1.0 - faceAlpha);
                half3 premultiplied = gold * haloAlpha + eyeColor * faceAlpha * (1.0 - haloAlpha);
                return half4(premultiplied / max(alpha, 0.001), alpha);
            }

            half4 DrawBeam(float2 uv)
            {
                float angle = TWO_PI * uv.x;
                float height = saturate(uv.y);
                float t = saturate(_BeamProgress);
                // Integer angular frequencies keep every filament continuous across the tube seam.
                float rays = 0.5 + 0.5 * cos(7.0 * angle + 0.65 * sin(3.0 * angle) + 0.30 * height);
                float strands = pow(saturate(0.5 + 0.5 * cos(19.0 * angle - 0.6 * sin(5.0 * angle))), 8.0);
                float current = 0.5 + 0.5 * cos(14.0 * height - 15.0 * t + 1.5 * sin(4.0 * angle));
                float foot = exp2(-9.0 * height);
                float tip = 1.0 - smoothstep(0.59 + 0.13 * rays, 1.0, height);
                float dissolve = 1.0 - smoothstep(0.42, 1.0, t);
                float energy = rays * rays * (0.19 + 0.09 * current) + 0.22 * strands + 0.28 * foot;
                float alpha = saturate((0.035 + energy) * tip * dissolve);
                half3 ember = half3(2.3, 0.72, 0.10);
                half3 flame = half3(3.6, 2.65, 1.08);
                return half4(lerp(ember, flame, saturate(foot + 0.65 * strands + 0.30 * current)), alpha);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                if (_SealLayer > 1.5) return DrawBeam(input.uv);
                if (_SealLayer > 0.5) return DrawEye(input.uv);
                float across = abs(input.uv.y * 2.0 - 1.0);
                float ink = 1.0 - smoothstep(0.42, 1.0, across);
                half3 gold = lerp(half3(0.90, 0.45, 0.10), half3(1.40, 0.82, 0.27), _Charge);
                return half4(gold, ink * lerp(0.67, 0.88, _Charge));
            }
            ENDHLSL
        }
    }
}
