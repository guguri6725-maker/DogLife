Shader "DogLife/Fracturing Ice"
{
 Properties { _Tint("Ice",Color)=(.25,.65,.85,1) _CrackProgress("Damage cracks",Range(0,1))=0 }
 SubShader
 {
  Tags { "RenderType"="Opaque" "Queue"="Geometry" } Cull Back ZWrite On
  Pass
  {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   float4 _Tint;float _CrackProgress;
   struct appdata{float4 vertex:POSITION;float3 normal:NORMAL;};
   struct v2f{float4 pos:SV_POSITION;float3 local:TEXCOORD0;float3 world:TEXCOORD1;float3 normal:TEXCOORD2;};
   v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.local=v.vertex.xyz;o.world=mul(unity_ObjectToWorld,v.vertex).xyz;o.normal=UnityObjectToWorldNormal(v.normal);return o;}
   float2 hash(float2 p){return frac(sin(float2(dot(p,float2(127.1,311.7)),dot(p,float2(269.5,183.3))))*43758.5453);}
   fixed4 frag(v2f i):SV_Target
   {
    float3 n=normalize(i.normal);float2 uv=abs(n.x)>abs(n.z)?i.local.zy:i.local.xy;uv*=float2(6,9);
    float2 cell=floor(uv),f=frac(uv);float nearest=20,second=20;
    for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++){float2 o=float2(x,y),v=o+hash(cell+o)-f;float d=dot(v,v);if(d<nearest){second=nearest;nearest=d;}else second=min(second,d);}
    // 타격마다 더 많은 가지가 표면을 가로질러 퍼지고 깊은 틈과 밝은 가장자리가 함께 드러납니다.
    float spread=smoothstep(0,.08,_CrackProgress-(.08+hash(cell).x*.72));
    float gap=second-nearest;
    float dark=(1-smoothstep(.015,.10,gap))*spread;
    float rim=(1-smoothstep(.10,.18,gap))*spread-dark;
    float light=.4+.6*abs(dot(n,normalize(float3(.3,1,.5))));
    float fresnel=pow(1-saturate(abs(dot(n,normalize(_WorldSpaceCameraPos-i.world)))),3);
    float3 ice=_Tint.rgb*light+fresnel*float3(.2,.4,.55)+pow(saturate(1-nearest),8)*.13;
    ice=lerp(ice,float3(.015,.07,.12),dark*.95)+rim*float3(.65,.85,1);
    return float4(ice,1);
   }
   ENDCG
  }
 }
}
