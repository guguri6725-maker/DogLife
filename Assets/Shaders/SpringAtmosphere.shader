Shader "DogLife/Spring Atmosphere"
{
 Properties { _Color("Tint",Color)=(1,1,1,1) _Detail("Surface detail",Range(0,1))=.7 _Glow("Pollen brightness",Range(1,3))=1.35 }
 SubShader
 {
  Tags { "Queue"="Transparent+4" "RenderType"="Transparent" }
  Blend SrcAlpha OneMinusSrcAlpha
  ZWrite Off Cull Off
  Pass
  {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma target 3.0
   #include "UnityCG.cginc"
   float4 _Color; float _Detail,_Glow;
   struct appdata {float4 vertex:POSITION; float4 color:COLOR; float4 uv:TEXCOORD0;};
   struct v2f {float4 vertex:SV_POSITION;float4 color:COLOR;float4 uv:TEXCOORD0;float3 world:TEXCOORD1;};
   v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.color=v.color*_Color;o.uv=v.uv;o.world=mul(unity_ObjectToWorld,v.vertex).xyz;return o;}
   float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   float noise(float2 p){float2 a=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(a),hash(a+float2(1,0)),f.x),lerp(hash(a+float2(0,1)),hash(a+1),f.x),f.y);}
   float cloud(float2 p){return noise(p)*.58+noise(p*2.03+9)*.28+noise(p*4.1-3)*.14;}
   float4 frag(v2f i):SV_Target
   {
    float4 c=i.color;float2 uv=i.uv.xy,q=uv*2-1;float mode=i.uv.z,seed=i.uv.w;
    // 경고선은 온전히 남기고 장식 표면에만 결, 먼지, 잔광을 적용합니다.
    if(mode<.5)return c;
    if(mode<1.5){
     float n=cloud(uv*4+float2(seed+_Time.y*.14,-_Time.y*.22));
     float edge=saturate(1-dot(q,q));c.a*=edge*edge*smoothstep(.15,.65,n);
     c.rgb*=lerp(.6,1.15,n);
    }else if(mode<2.5){
     float core=exp(-dot(q,q)*6);c.a*=core*saturate((1-length(q))*4);c.rgb*=_Glow*(1+core*.5);
    }else if(mode<3.5){
     float vein=pow(saturate(cos((uv.x-.5)*35+abs(uv.y-.45)*12)),18);
     float fold=1-abs(q.x)*.3;float grain=noise(uv*65+seed);
     c.rgb*=lerp(1,fold*(.83+grain*.16)+vein*.13,_Detail);
     c.rgb=lerp(c.rgb*float3(.72,.42,.5),c.rgb,smoothstep(0,.65,uv.y));
    }else if(mode<4.5){
     float n=cloud(float2(uv.x*10-_Time.y*2+seed,uv.y*3));
     c.a*=pow(saturate(1-abs(q.y)),.7)*smoothstep(.12,.48,n)*smoothstep(0,.06,uv.x)*(1-smoothstep(.92,1,uv.x));
     c.rgb*=lerp(.75,_Glow,n);
    }else{
     float grain=cloud(float2(uv.x*18,uv.y*3)+seed);
     float groove=pow(saturate(sin(uv.x*50+noise(uv*5)*3)),14);
     c.rgb*=lerp(1,.6+grain*.65-groove*.15,_Detail);
    }
    return c;
   }
   ENDCG
  }
 }
}
