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
        _RealisticSurface ("Refined forest surface",Float)=0
        _EyeCenter ("Eye centre",Vector)=(.057455,-.409757,.414399,0)
        _EyeCenterRight ("Opposite eye centre",Vector)=(.073567,-.407568,.413275,0)
        _EyeRadius ("Eye radius",Float)=.013
    }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Cull Off
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow vertex:vert
        #pragma target 3.0
        #include "UnityCG.cginc"
        #include "../../../SpringMonsterKarma.cginc"
        sampler2D _MainTex, _BumpMap;
        fixed4 _Color, _EdgeColor;
        half _Glossiness, _BumpScale, _Peel, _Reveal, _Cracks;
        float4x4 _BossWorldToLocal;
        float _RealisticSurface,_EyeRadius;float4 _EyeCenter,_EyeCenterRight;
        struct Input { float2 uv_MainTex; float3 worldPos; float3 rest; float3 viewDir; };
        void vert(inout appdata_full v,out Input o){UNITY_INITIALIZE_OUTPUT(Input,o);o.rest=v.texcoord2.xyz;}
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
            // 1페이즈 전환 중에도 기존 텍스처와 새 눈/재질 표현을 유지합니다.
            if(_RealisticSurface>.5&&_Reveal<.5)
            {
                float leaf=smoothstep(.015,.07,c.g-max(c.r,c.b)*.94);
                float bark=smoothstep(.025,.13,c.r-c.b)*(1-leaf);
                float ivory=smoothstep(.40,.72,dot(c.rgb,float3(.2126,.7152,.0722)))*smoothstep(.015,.075,c.r-c.b)*(1-leaf);
                o.Smoothness=lerp(lerp(lerp(_Glossiness,.28,bark),.38,leaf),.46,ivory);
                float3 ep=IN.rest;float3 centre=ep.x>0?_EyeCenterRight.xyz:_EyeCenter.xyz;ep.x=abs(ep.x);
                float d=length((ep-centre)*float3(1,1,1.15));
                float eye=(1-smoothstep(_EyeRadius*.72,_EyeRadius,d))*(1-rim);
                float pupil=1-smoothstep(_EyeRadius*.22,_EyeRadius*.48,d);
                o.Albedo=lerp(o.Albedo,lerp(float3(.055,.024,.009),float3(.007,.005,.003),pupil),eye*.96);
                o.Normal=normalize(lerp(o.Normal,float3(0,0,1),eye*.92));o.Smoothness=lerp(o.Smoothness,.88,eye);
            }
            float cracks=1-smoothstep(.015,.048,abs(noise(p.xy*42+p.z*16)-.5));
            o.Emission=_EdgeColor.rgb*rim*.6+float3(.8,.28,.38)*cracks*_Cracks*.65*(1-_Reveal); o.Alpha=1;
            ApplySpringKarma(IN.uv_MainTex,IN.viewDir,o);
        }
        ENDCG
    }
    Fallback "Diffuse"
}
