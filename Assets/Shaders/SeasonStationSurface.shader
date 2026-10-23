Shader "DogLife/Season Station Surface"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _MainTex ("Handcrafted Surface", 2D) = "white" {}
        [Normal] _BumpMap ("Surface Relief", 2D) = "bump" {}
        _BumpScale ("Relief Strength", Range(0,2)) = 0.45
        _Metallic ("Metallic", Range(0,1)) = 0
        _Glossiness ("Smoothness", Range(0,1)) = 0.2
        _EmissionColor ("Lantern Glow", Color) = (0,0,0,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0
        #include "UnityStandardUtils.cginc"
        sampler2D _MainTex, _BumpMap;
        fixed4 _Color, _EmissionColor;
        half _Metallic, _Glossiness, _BumpScale;
        struct Input { float2 uv_MainTex; float2 uv_BumpMap; };
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            // 직접 만든 나뭇결·직물·석재의 요철을 계절 조명에 반영합니다.
            fixed4 surface = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = surface.rgb;
            o.Normal = UnpackScaleNormal(tex2D(_BumpMap, IN.uv_BumpMap), _BumpScale);
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Emission = surface.rgb * _EmissionColor.rgb;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
