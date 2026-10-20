Shader "DogLife/Fusion Ember"
{
    Properties { _Intensity("Intensity", Range(0,4)) = 1.4 }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct Input { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct Varying { float4 vertex:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            float _Intensity;
            Varying vert(Input v)
            {
                Varying o;
                o.vertex=UnityObjectToClipPos(v.vertex);
                o.color=v.color; o.uv=v.uv*2-1;
                return o;
            }
            half4 frag(Varying i):SV_Target
            {
                // 작은 흰 핵과 부드러운 빛 번짐으로 사각 파티클 경계를 숨깁니다.
                float radius=length(i.uv);
                float core=exp(-dot(i.uv,i.uv)*18);
                float glow=exp(-dot(i.uv,i.uv)*4)*(1-smoothstep(.6,1,radius));
                return half4(lerp(i.color.rgb,1,core*.65)*_Intensity,
                    i.color.a*saturate(core+glow*.5));
            }
            ENDCG
        }
    }
}
