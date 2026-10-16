Shader "DogLife/Blossom Petal Impact"
{
    Properties { _Color("Tint",Color)=(1,.45,.68,.85) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha One
        Cull Off
        ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct Input { float4 vertex:POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; };
            struct Output { float4 vertex:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; };
            fixed4 _Color;
            Output vert(Input v){Output o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=_Color;o.color.a*=saturate(v.color.a);return o;}
            fixed4 frag(Output i):SV_Target{
                float2 q=i.uv*2-1;float r=length(q);fixed4 c=i.color;
                // 짧은 섬광과 꽃 모양 가장자리가 겹쳐도 흰 판이 되지 않습니다.
                float wave=.5+.5*cos(atan2(q.y,q.x)*5+r*9);
                c.a*=exp(-r*r*5)*saturate((1-r)*5)*lerp(.55,1,wave);
                return c;
            }
            ENDCG
        }
    }
}

