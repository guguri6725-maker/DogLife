Shader "DogLife/Spring Mote"
{
 Properties { _Color("Tint",Color)=(1,1,1,1) _Dust("Dust / pollen",Range(0,1))=0 }
 SubShader
 {
  Tags { "Queue"="Transparent+3" "RenderType"="Transparent" }
  Blend SrcAlpha OneMinusSrcAlpha
  Cull Off ZWrite Off
  Pass
  {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   float4 _Color;float _Dust;
   struct appdata {float4 vertex:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
   struct v2f {float4 vertex:SV_POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
   v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.color=v.color*_Color;o.uv=v.uv;return o;}
   float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   float noise(float2 p){float2 a=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(a),hash(a+float2(1,0)),f.x),lerp(hash(a+float2(0,1)),hash(a+1),f.x),f.y);}
   float4 frag(v2f i):SV_Target{
    float2 q=i.uv*2-1;float edge=saturate(1-dot(q,q));float n=noise(i.uv*5+float2(_Time.y*.13,-_Time.y*.2))*.7+noise(i.uv*11)*.3;
    float4 c=i.color;c.a*=lerp(exp(-dot(q,q)*7)*edge,edge*edge*smoothstep(.15,.7,n),_Dust);
    c.rgb*=lerp(1.7,.65+n*.5,_Dust);return c;
   }
   ENDCG
  }
 }
}
