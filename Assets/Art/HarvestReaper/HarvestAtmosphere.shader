Shader "DogLife/Harvest Atmosphere"
{
 Properties { _Color("Tint",Color)=(1,1,1,1) _Glow("Ember brightness",Range(1,4))=1.8 }
 SubShader
 {
  Tags { "Queue"="Transparent" "RenderType"="Transparent" }
  Blend SrcAlpha OneMinusSrcAlpha
  ZWrite Off Cull Off
  Pass
  {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma target 3.0
   #include "UnityCG.cginc"
   float4 _Color; float _Glow;
   struct appdata { float4 vertex:POSITION; float4 color:COLOR; float4 uv:TEXCOORD0; };
   struct v2f { float4 vertex:SV_POSITION; float4 color:COLOR; float4 uv:TEXCOORD0; };
   v2f vert(appdata v) { v2f o; o.vertex=UnityObjectToClipPos(v.vertex);o.color=v.color*_Color;o.uv=v.uv;return o; }
   float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
   float noise(float2 p) { float2 a=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(a),hash(a+float2(1,0)),f.x),lerp(hash(a+float2(0,1)),hash(a+1),f.x),f.y); }
   float cloud(float2 p) { return noise(p)*.57+noise(p*2.07+13.7)*.28+noise(p*4.13-7.1)*.15; }
   float4 frag(v2f i):SV_Target
   {
    float4 c=i.color; float2 uv=i.uv.xy;float mode=i.uv.z,seed=i.uv.w;
    // 경고 도형은 그대로 두고 불꽃/연기/잔상만 흐르는 노이즈로 다듬습니다.
    if(mode<.5)return c;
    float2 q=uv*2-1;
    if(mode<1.5)
    {
     float n=cloud(float2(uv.x*5+seed,uv.y*4-_Time.y*2.8));
     float curl=(cloud(float2(uv.y*6-_Time.y*1.7,seed))-.5)*uv.y*1.3;
     float taper=pow(1-uv.y,.55)*.95+.025;
     float body=saturate(1-abs(q.x+curl)/taper);
     float density=smoothstep(.3,.68,body+n*.9-uv.y*.5);
     density*=lerp(.3,1,smoothstep(.2,.7,cloud(float2(uv.x*12+seed,uv.y*8-_Time.y*3.5))));
     c.a*=density*smoothstep(0,.1,uv.y)*(1-smoothstep(.72,1,uv.y));
     c.rgb=lerp(c.rgb*.24,c.rgb*_Glow+float3(.6,.35,.1),pow(body,2)*(1-uv.y))*lerp(.7,1.5,n);
    }
    else if(mode<2.5)
    {
     float n=cloud(uv*5+float2(seed+_Time.y*.18,-_Time.y*.3));
     float edge=saturate(1-dot(q,q));
     c.a*=saturate(edge*1.6)*smoothstep(.18,.68,n)*1.65;
     c.rgb*=lerp(.45,1.3,n);
    }
    else if(mode<3.5)
    {
     float core=exp(-dot(q,q)*5);
     c.a*=core*saturate((1-length(q))*5);
     c.rgb*= _Glow*(1+core);
    }
    else
    {
     float n=cloud(float2(uv.x*9-_Time.y*3+seed,uv.y*3));
     float edge=pow(saturate(1-abs(q.y)),1.5);
     c.a*=edge*smoothstep(.13,.55,n)*smoothstep(0,.07,uv.x)*(1-smoothstep(.85,1,uv.x));
     c.rgb*=lerp(.55,_Glow,edge);
    }
    return c;
   }
   ENDCG
  }
 }
}
