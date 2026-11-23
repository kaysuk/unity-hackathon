Shader "UI/Ivan Blood Frame"
{
    Properties
    {
        [PerRendererData] _MainTex ("UI Texture", 2D) = "white" {}
        _Spatter ("Blood spatter", 2D) = "white" {}
        _Blood ("Blood", Color) = (0.52, 0.055, 0.06, 1)
        _DarkBlood ("Dark blood", Color) = (0.27, 0.015, 0.033, 1)
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

            half4 frag(Varyings input) : SV_Target
            {
                float2 aspect = _ScreenParams.xy / min(_ScreenParams.x, _ScreenParams.y);
                float2 edges = min(input.uv, 1.0 - input.uv) * aspect;
                float distance = min(edges.x, edges.y);
                half corner = 1.0 - smoothstep(0.0, 0.38, max(edges.x, edges.y));
                half width = lerp(0.075, 0.16, input.severity) + 0.07 * corner;
                if (distance >= width) return 0;

                float2 p = input.uv * aspect;
                float2 rotated = float2(0.8 * p.x - 0.6 * p.y, 0.6 * p.x + 0.8 * p.y);
                half smear = tex2D(_Spatter, frac(rotated * 3.6 + float2(0.31, 0.67))).a;
                half flecks = tex2D(_Spatter, frac(p * float2(13.0, 10.0) + smear * 0.06)).a;
                half depth = 1.0 - distance / width;
                half veil = depth * depth;
                half feather = lerp(0.30, 0.09, input.severity);
                half pigment = 0.65 * smear + 0.35 * flecks;
                half stains = smoothstep(0.50 - feather, 0.50 + feather, pigment)
                            * smoothstep(0.02, 0.83, depth);
                half opacity = saturate(0.44 * veil + 0.89 * stains) * lerp(0.29, 0.97, input.severity);
                return half4(lerp(_Blood.rgb, _DarkBlood.rgb, pigment), opacity);
            }
            ENDCG
        }
    }
}
