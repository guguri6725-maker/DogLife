#include "UnityCG.cginc"
// 메시 꽃잎은 양면 명암과 미세한 결, 리본은 흐르는 가장자리를 사용합니다.
struct Input { float4 vertex:POSITION; float3 normal:NORMAL; float4 color:COLOR; float2 uv:TEXCOORD0; };
struct Output { float4 vertex:SV_POSITION; float4 color:COLOR; float3 world:TEXCOORD0; float3 normal:TEXCOORD1; float2 uv:TEXCOORD2; };
float4 _Color;
Output vert(Input v){Output o;o.vertex=UnityObjectToClipPos(v.vertex);o.color=v.color*_Color;o.world=mul(unity_ObjectToWorld,v.vertex).xyz;o.normal=UnityObjectToWorldNormal(v.normal);o.uv=v.uv;return o;}
float springNoise(float3 p){return frac(sin(dot(floor(p),float3(127.1,311.7,74.7)))*43758.5453);}
float4 frag(Output i):SV_Target
{
 float4 c=i.color;
 float3 n=normalize(i.normal+float3(.0001,0,0));
 float light=.58+.42*abs(dot(n,normalize(float3(.3,.8,.5))));
 float rim=pow(1-abs(dot(n,normalize(_WorldSpaceCameraPos-i.world))),3);
 float vein=pow(saturate(sin(dot(i.world,float3(37,19,23)))),16);
 c.rgb*=light*(.91+.09*springNoise(i.world*85));
 c.rgb+=float3(.12,.065,.045)*rim+c.rgb*vein*.11;
 // 기존 꽃잎 메시에는 UV가 없어 실루엣을 그대로 보존합니다.
 if(dot(i.uv,i.uv)>.00001){
  float edge=pow(saturate(1-abs(i.uv.y*2-1)),.65);
  float strands=.72+.28*sin(i.uv.x*48-_Time.y*12+i.uv.y*9);
  c.a*=edge*strands;
 }
 return c;
}
