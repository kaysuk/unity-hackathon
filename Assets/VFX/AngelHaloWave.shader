Shader "Temple/Angel Halo Wave"
{
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
                float4 _WaveOrigin;
                float4 _Pillars[6];
                float4x4 _ChamberWorldToLocal[6];
                float4 _ChamberLimits[6];
                float _ChamberSlope;
                float _ChamberCount;
                float _Reach;
                float _Charge;
                float _Radius;
                float _IsWall;
            CBUFFER_END

            struct Varyings
            {
                float4 clip : SV_POSITION;
                float3 world : TEXCOORD0;
                float2 uv : TEXCOORD1;
            };

            Varyings Vert(float3 position : POSITION, float2 uv : TEXCOORD0)
            {
                Varyings o;
                float3 world = TransformObjectToWorld(position);
                o.clip = TransformWorldToHClip(world);
                o.world = world;
                o.uv = uv;
                return o;
            }

            // Signed clearance between the source-to-fragment ray segment and the six columns.
            float Clearance(float2 ground)
            {
                float2 path = ground - _WaveOrigin.xz;
                float inverseLength = rcp(max(dot(path, path), 0.00001));
                float clearance = 1000.0;
                [unroll] for (int i = 0; i < 6; ++i)
                {
                    float4 pillar = _Pillars[i];
                    if (pillar.z <= 0) continue;
                    float2 toPillar = pillar.xy - _WaveOrigin.xz;
                    float t = saturate(dot(path, toPillar) * inverseLength);
                    clearance = min(clearance, length(path * t - toPillar) - pillar.z);
                }
                return clearance;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                // The upper-floor rooms shelter Ivan; keep the floor below them lit.
                [unroll] for (int index = 0; index < 6; ++index)
                {
                    if (index >= _ChamberCount) break;
                    float3 local = mul(_ChamberWorldToLocal[index], float4(i.world, 1)).xyz;
                    float4 limits = _ChamberLimits[index];
                    if (local.z > limits.x && local.z < limits.y && abs(local.x) < local.z * _ChamberSlope &&
                        local.y >= limits.z && local.y < limits.w) discard;
                }
                float d = length(i.world.xz - _WaveOrigin.xz);
                float gap = Clearance(i.world.xz);
                float aa = max(0.012, fwidth(gap));
                float exposed = smoothstep(-aa, aa, gap);
                const half3 gold = half3(1.9, 0.72, 0.10);
                const half3 whiteGold = half3(3.3, 2.35, 1.06);
                if (_IsWall > 0.5)
                {
                    // A luminous lower lip and curved ribbons break up the translucent sheet.
                    float phase = i.uv.x * TWO_PI;
                    float y = i.uv.y;
                    float band = y - (0.20 + 0.075 * sin(phase * 6 + _Radius * 0.24)
                                          + 0.04 * sin(phase * 11 - _Radius * 0.15));
                    float ribbon = exp2(-28 * abs(band)) + 0.55 * exp2(-45 * abs(band - 0.14));
                    float foot = exp2(-y * 22);
                    float top = 1 - smoothstep(0.83, 1.0, y);
                    float alpha = (0.20 + 0.36 * ribbon + 0.42 * foot) * top * exposed;
                    return half4(lerp(gold, whiteGold, saturate(ribbon + foot)), saturate(alpha));
                }

                float edgeFade = 1 - smoothstep(_Reach - 0.8, _Reach, d);
                float shade = 1 - exposed;
                float border = exp2(-15 * abs(gap));
                float warning = lerp(0.35, 1.0, _Charge);
                float afterFront = _Radius < 0 ? 1 : saturate((d - _Radius + 1.0) / 1.0);
                float wake = _Radius < 0 ? 0 : exp2(-2.8 * abs(d - _Radius)) * exposed;
                float ink = (0.08 * exposed + 0.34 * shade + 0.25 * border) * warning * afterFront;
                half3 tone = lerp(half3(0.025,0.018,0.012), gold, max(exposed, border));
                return half4(lerp(tone, whiteGold, wake), saturate(ink + 0.52 * wake) * edgeFade);
            }
            ENDHLSL
        }
    }
}
