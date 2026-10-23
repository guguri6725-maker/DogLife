Shader "DogLife/Player/Woodland Lit"
{
    Properties
    {
        _MainTex ("Albedo", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _ToneStrength ("Woodland Tone", Range(0,1)) = 1
        _Glossiness ("Smoothness", Range(0,1)) = 0.08
        _Metallic ("Metallic", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0
        sampler2D _MainTex;
        fixed4 _Color;
        half _ToneStrength, _Glossiness, _Metallic;
        struct Input { float2 uv_MainTex; };
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 source = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            float3 c = source.rgb;
            float luminance = dot(c, float3(0.2126, 0.7152, 0.0722));
            // 원본 UV와 무늬를 유지하며 색상 영역 경계를 부드럽게 보정합니다.
            float red = smoothstep(0.035, 0.16, c.r - c.g)
                * (1 - smoothstep(0.48, 0.73, c.g / max(c.r, 0.001)));
            float teal = smoothstep(0.015, 0.11, max(c.g, c.b) - c.r);
            float3 toned = lerp(c, luminance.xxx, 0.16) * float3(0.91, 0.88, 0.83);
            // 붉은 천은 와인색, 청록색 옷은 깊고 차분한 색으로 낮춥니다.
            toned = lerp(toned, lerp(c, luminance.xxx, 0.18) * float3(0.80,0.65,0.73), red);
            toned = lerp(toned, lerp(c, luminance.xxx, 0.22) * float3(0.78,0.82,0.80), teal);
            o.Albedo = lerp(c, toned, _ToneStrength);
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = source.a;
        }
        ENDCG
    }
    FallBack "Standard"
}
