Shader "DogLife/Forest Boar Surface"
{
 Properties
 {
  _MainTex("Original colour",2D)="white"{}
  _BumpMap("Original normal",2D)="bump"{}
  _Color("Tint",Color)=(1,1,1,1)
  _BumpScale("Surface relief",Range(0,2))=.65
  _Glossiness("Base smoothness",Range(0,1))=.16
  _EyeCenter("Eye centre in original mesh",Vector)=(.057455,-.409757,.414399,0)
  _EyeCenterRight("Opposite eye centre",Vector)=(.073567,-.407568,.413275,0)
  _EyeRadius("Eye radius",Float)=.013
  _RealisticSurface("Preserve surface during phase transition",Float)=1
 }
 SubShader
 {
  Tags {"RenderType"="Opaque"}
  CGPROGRAM
  #pragma surface surf Standard fullforwardshadows vertex:vert addshadow
  #pragma target 3.0
  #include "UnityCG.cginc"
  sampler2D _MainTex,_BumpMap;
  fixed4 _Color;
  float _BumpScale,_Glossiness,_EyeRadius;
  float4 _EyeCenter,_EyeCenterRight;
  struct Input {float2 uv_MainTex;float3 rest;};
  // 추가 UV에 원형 좌표를 보존해 움직이는 피부에도 재질 영역을 고정합니다.
  void vert(inout appdata_full v,out Input o){UNITY_INITIALIZE_OUTPUT(Input,o);o.rest=v.texcoord2.xyz;}
  void surf(Input IN,inout SurfaceOutputStandard o)
  {
   float3 c=tex2D(_MainTex,IN.uv_MainTex).rgb*_Color.rgb;
   float luminance=dot(c,float3(.2126,.7152,.0722));
   // 기존 색상과 UV를 보존하고 잎, 나무, 털의 거칠기만 분리합니다.
   float leaf=smoothstep(.015,.07,c.g-max(c.r,c.b)*.94);
   float bark=smoothstep(.025,.13,c.r-c.b)*(1-leaf);
   float ivory=smoothstep(.40,.72,luminance)*smoothstep(.015,.075,c.r-c.b)*(1-leaf);
   float smoothness=lerp(_Glossiness,.28,bark);
   smoothness=lerp(smoothness,.38,leaf);smoothness=lerp(smoothness,.46,ivory);
   o.Albedo=c;o.Normal=UnpackScaleNormal(tex2D(_BumpMap,IN.uv_MainTex),_BumpScale);
   o.Occlusion=1;o.Metallic=0;
   // 눈 영역만 작고 어두운 안구와 젖은 표면으로 정리합니다.
   float3 p=IN.rest;float3 centre=p.x>0?_EyeCenterRight.xyz:_EyeCenter.xyz;p.x=abs(p.x);
   float distance=length((p-centre)*float3(1,1,1.15));
   float eye=1-smoothstep(_EyeRadius*.72,_EyeRadius,distance);
   float pupil=1-smoothstep(_EyeRadius*.22,_EyeRadius*.48,distance);
   float3 iris=lerp(float3(.055,.024,.009),float3(.007,.005,.003),pupil);
   o.Albedo=lerp(o.Albedo,iris,eye*.96);
   o.Normal=normalize(lerp(o.Normal,float3(0,0,1),eye*.92));
   o.Smoothness=lerp(smoothness,.88,eye);o.Alpha=1;
  }
  ENDCG
 }
 Fallback "Standard"
}
