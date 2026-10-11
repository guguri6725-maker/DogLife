Shader "DogLife/Spring Boar Peeling"
{
    Properties
    {
        _MainTex ("Body texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _BumpMap ("Normal", 2D) = "bump" {}
        _BumpScale ("Normal strength", Range(0,2)) = .3
        _Glossiness ("Smoothness", Range(0,1)) = .18
        _Peel ("Peeling", Range(0,1)) = 0
        _Reveal ("Reveal skeleton", Float) = 0
        _Cracks ("Root energy cracks", Range(0,1)) = 0
        _EdgeColor ("Exposed bark edge", Color) = (.65,.38,.32,1)
    }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Cull Off
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0
        #include "UnityCG.cginc"
        sampler2D _MainTex, _BumpMap;
        fixed4 _Color, _EdgeColor;
        half _Glossiness, _BumpScale, _Peel, _Reveal, _Cracks;
        float4x4 _BossWorldToLocal;
        struct Input { float2 uv_MainTex; float3 worldPos; };
        float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
        float noise(float2 p)
        {
            float2 i=floor(p), f=frac(p); f=f*f*(3-2*f);
            return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);
        }
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float3 p=mul(_BossWorldToLocal,float4(IN.worldPos,1)).xyz;
            float order=saturate(.65-p.y*.75+p.z*.55);
            float n=order*.7+noise(p.xz*24+p.y*6.3)*.2+noise(p.xy*53+p.z*8.7)*.1;
            float edge=n-lerp(-.08,1.08,_Peel);
            edge=lerp(edge,-edge,_Reveal);
            clip(edge);
            fixed4 c=tex2D(_MainTex,IN.uv_MainTex)*_Color;
            float rim=(1-smoothstep(0,.055,edge))*step(.001,_Peel);
            o.Albedo=lerp(c.rgb,_EdgeColor.rgb,rim*.7);
            o.Normal=UnpackScaleNormal(tex2D(_BumpMap,IN.uv_MainTex),_BumpScale);
            o.Metallic=0; o.Smoothness=_Glossiness;
            float cracks=1-smoothstep(.015,.048,abs(noise(p.xy*42+p.z*16)-.5));
            o.Emission=_EdgeColor.rgb*rim*.6+float3(.8,.28,.38)*cracks*_Cracks*.65*(1-_Reveal); o.Alpha=1;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
