Shader "DogLife/FusionWeaponReveal"
{
 Properties { _MainTex("Texture",2D)="white"{} _SoulColor("Soul",Color)=(.1,.6,1,1) _Reveal("Reveal",Range(0,1))=1 _Bottom("Bottom",Float)=0 _Height("Height",Float)=2 }
 SubShader
 {
  Tags {"RenderType"="Opaque"}
  Pass
  {
   Cull Off
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;};
   struct V {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float2 uv:TEXCOORD2;};
   sampler2D _MainTex;
   float4 _MainTex_ST,_SoulColor;float _Reveal,_Bottom,_Height;
   V vert(A a){V o;o.world=mul(unity_ObjectToWorld,a.positionOS).xyz;o.positionCS=UnityObjectToClipPos(a.positionOS);o.normal=UnityObjectToWorldNormal(a.normalOS);o.uv=TRANSFORM_TEX(a.uv,_MainTex);return o;}
   half4 frag(V i):SV_Target
   {
    // 격자 모양으로 잘리는 대신 가는 빛의 경계가 표면을 따라 드러납니다.
    float n=.5+.25*sin(dot(i.world,float3(71,39,57)))+.25*sin(dot(i.world,float3(-43,83,61)));
    float cutoff=saturate((i.world.y-_Bottom)/max(.01,_Height))*.94+n*.045;
    clip(_Reveal-cutoff-.001);float edge=1-saturate((_Reveal-cutoff)*42);
    float light=.42+.58*saturate(dot(normalize(i.normal),normalize(float3(.3,1,-.5))));
    float rim=pow(1-saturate(abs(dot(normalize(i.normal),normalize(_WorldSpaceCameraPos-i.world)))),3);
    return half4(tex2D(_MainTex,i.uv).rgb*light+_SoulColor.rgb*(edge*2.1+rim*.3),1);
   }
   ENDHLSL
  }
 }
}
