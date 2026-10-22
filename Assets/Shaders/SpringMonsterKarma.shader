Shader "DogLife/Spring Monster Karma"
{
 Properties
 {
  _MainTex("Original texture",2D)="white"{}
  _Color("Tint",Color)=(1,1,1,1)
  _BumpMap("Normal",2D)="bump"{}
  _BumpScale("Normal strength",Float)=1
  _Glossiness("Smoothness",Range(0,1))=.2
  _Metallic("Metallic",Range(0,1))=0
  _MetallicGlossMap("Metallic map",2D)="white"{}
  _GlossMapScale("Smoothness map scale",Float)=1
  _OcclusionMap("Occlusion",2D)="white"{}
  _OcclusionStrength("Occlusion strength",Range(0,1))=1
  _EmissionMap("Emission",2D)="white"{}
  _EmissionColor("Emission color",Color)=(0,0,0,1)
  _Cutoff("Alpha cutoff",Range(0,1))=.5
 }
 SubShader
 {
  Tags {"RenderType"="Opaque"}
  CGPROGRAM
  #pragma surface surf Standard fullforwardshadows addshadow
  #pragma target 3.0
  #pragma shader_feature_local _NORMALMAP
  #pragma shader_feature_local _METALLICGLOSSMAP
  #pragma shader_feature_local _ALPHATEST_ON
  #include "UnityCG.cginc"
  #include "SpringMonsterKarma.cginc"
  sampler2D _MainTex,_BumpMap,_MetallicGlossMap,_OcclusionMap,_EmissionMap;
  fixed4 _Color,_EmissionColor;
  half _BumpScale,_Glossiness,_Metallic,_GlossMapScale,_OcclusionStrength,_Cutoff;
  struct Input {float2 uv_MainTex;float3 viewDir;};
  void surf(Input IN,inout SurfaceOutputStandard o)
  {
   fixed4 c=tex2D(_MainTex,IN.uv_MainTex)*_Color;
   #ifdef _ALPHATEST_ON
   clip(c.a-_Cutoff);
   #endif
   o.Albedo=c.rgb;o.Alpha=c.a;o.Metallic=_Metallic;o.Smoothness=_Glossiness;
   #ifdef _NORMALMAP
   o.Normal=UnpackScaleNormal(tex2D(_BumpMap,IN.uv_MainTex),_BumpScale);
   #endif
   #ifdef _METALLICGLOSSMAP
   fixed4 metal=tex2D(_MetallicGlossMap,IN.uv_MainTex);o.Metallic=metal.r;o.Smoothness=metal.a*_GlossMapScale;
   #endif
   o.Occlusion=lerp(1,tex2D(_OcclusionMap,IN.uv_MainTex).g,_OcclusionStrength);
   o.Emission=tex2D(_EmissionMap,IN.uv_MainTex).rgb*_EmissionColor.rgb;
   ApplySpringKarma(IN.uv_MainTex,IN.viewDir,o);
  }
  ENDCG
 }
 Fallback "Standard"
}
