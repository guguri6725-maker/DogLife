Shader "DogLife/Spring Cinematic Foliage"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = .5
        _Metallic ("Metallic", Range(0,1)) = 0
        _BumpMap ("Normal", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Float) = 1
        _OcclusionMap ("Occlusion", 2D) = "white" {}
        _OcclusionStrength ("Occlusion Strength", Range(0,1)) = 1
        _EmissionMap ("Emission", 2D) = "white" {}
        _EmissionColor ("Emission Color", Color) = (0,0,0,0)
        [HideInInspector] _CinematicFade ("Cinematic Fade", Range(0,1)) = 0
        [HideInInspector] _CinematicRect ("Cinematic Rect", Vector) = (0,0,1,1)
        [HideInInspector] _CinematicCameraPosition ("Cinematic Camera", Vector) = (0,0,0,0)
        [HideInInspector] _CinematicDepth ("Cinematic Depth", Vector) = (0,0,1,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 300
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0
        // 런타임에 복제한 원본 재질의 옵션을 빌드에서도 사용할 수 있게 보존합니다.
        #pragma multi_compile_local _ _NORMALMAP
        #pragma multi_compile_local _ _EMISSION
        #include "UnityCG.cginc"
        sampler2D _MainTex, _BumpMap, _OcclusionMap, _EmissionMap;
        fixed4 _Color, _EmissionColor;
        half _Glossiness, _Metallic, _BumpScale, _OcclusionStrength;
        float _CinematicFade;
        float4 _CinematicRect, _CinematicCameraPosition, _CinematicDepth;
        float4x4 _CinematicViewProjection;
        struct Input { float2 uv_MainTex; float3 worldPos; };
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            // 카메라와 보스 사이의 식물 표면만 지워 합쳐진 메시의 주변 숲은 유지합니다.
            float4 projected = mul(_CinematicViewProjection, float4(IN.worldPos, 1));
            float2 uv = projected.xy / max(projected.w, .0001) * .5 + .5;
            float2 center = (_CinematicRect.xy + _CinematicRect.zw) * .5;
            float2 radius = max((_CinematicRect.zw - _CinematicRect.xy) * .5, .001);
            float2 edge = abs((uv - center) / radius);
            float mask = 1 - smoothstep(.88, 1, max(edge.x, edge.y));
            float depth = dot(IN.worldPos - _CinematicCameraPosition.xyz, _CinematicDepth.xyz);
            mask *= step(0, depth) * (1 - smoothstep(_CinematicDepth.w - .2, _CinematicDepth.w + .2, depth));
            float noise = frac(sin(dot(floor(IN.worldPos * 180), float3(12.9898, 78.233, 37.719))) * 43758.5453);
            clip(1 - mask * _CinematicFade - (.001 + noise * .998));
            fixed4 color = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = color.rgb;
            o.Metallic = _Metallic; o.Smoothness = _Glossiness;
            o.Occlusion = lerp(1, tex2D(_OcclusionMap, IN.uv_MainTex).g, _OcclusionStrength);
            #ifdef _NORMALMAP
            o.Normal = UnpackScaleNormal(tex2D(_BumpMap, IN.uv_MainTex), _BumpScale);
            #endif
            #ifdef _EMISSION
            o.Emission = tex2D(_EmissionMap, IN.uv_MainTex).rgb * _EmissionColor.rgb;
            #endif
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Standard"
}
