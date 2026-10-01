Shader "DogLife/Synth Weapon Ribbon"
{
    Properties
    {
        _Intensity("Light intensity",Range(0,4))=1.5
        _Core("Bright filament",Range(0,1))=.4
        _Flow("Flow speed",Float)=2
    }
    SubShader
    {
        Tags {"Queue"="Transparent+15" "RenderType"="Transparent" "IgnoreProjector"="True"}
        Blend SrcAlpha One
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float _Intensity,_Core,_Flow;
            struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
            struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
            v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o;}
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float noise(float2 p)
            {
                float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);
                return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);
            }
            fixed4 frag(v2f i):SV_Target
            {
                // 폭의 가장자리를 흩뜨리고 중앙에는 가늘고 밝은 빛을 남깁니다.
                float t=_Time.y*_Flow;
                float n=noise(float2(i.uv.x*24-t,i.uv.y*7+t*.3));
                float y=abs(i.uv.y-.5)*2;
                float veil=pow(saturate(1-y),1.7)*(.3+.7*n);
                float vein=exp(-pow((i.uv.y-.5+(n-.5)*.1)*25,2));
                float ends=smoothstep(0,.035,i.uv.x)*smoothstep(0,.035,1-i.uv.x);
                float alpha=saturate(veil*.75+vein*_Core)*ends*i.color.a;
                float3 color=lerp(i.color.rgb,float3(1,.96,.9),vein*_Core*.65);
                return float4(color*_Intensity,alpha);
            }
            ENDCG
        }
    }
}
