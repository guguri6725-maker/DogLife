Shader "DogLife/Summer/Flowing Water"
{
 Properties
 {
  _DeepColor("Deep water",Color)=(.025,.22,.24,1)
  _ShallowColor("Shallow water",Color)=(.14,.48,.43,1)
  _FoamColor("Foam",Color)=(.7,.86,.77,1)
  _FlowSpeed("Flow speed (metres per second)",Range(0,3))=.45
  _RippleStrength("Ripple strength",Range(0,.3))=.08
  _Smoothness("Smoothness",Range(0,1))=.8
  _Opacity("Water opacity",Range(.2,1))=.88
  _PreviewTime("Preview time (-1 uses game time)",Float)=-1
 }
 SubShader
 {
  Tags {"Queue"="Transparent" "RenderType"="Transparent"} Cull Off ZWrite Off
  CGPROGRAM
  #pragma surface surf Standard alpha:premul
  #pragma target 3.0
  #include "SummerFlow.cginc"
  fixed4 _DeepColor,_ShallowColor,_FoamColor;
  float _FlowSpeed,_RippleStrength,_Smoothness,_PreviewTime,_Opacity;
  struct Input {float3 worldPos;float2 uv_MainTex;float4 color:COLOR;};
  void surf(Input IN,inout SurfaceOutputStandard o)
  {
   float t=(_PreviewTime<0?_Time.y:_PreviewTime)*_FlowSpeed;
   float2 p=IN.worldPos.xz*2.3;
   float n=flowPattern(p,t);
   // Vertex red encodes proximity to either bank; the central channel stays darker.
   float bank=IN.color.r;
   float foam=smoothstep(.57,.76,n)*.22+smoothstep(.78,.99,bank)*smoothstep(.28,.66,n)*.7;
   o.Albedo=lerp(lerp(_DeepColor.rgb,_ShallowColor.rgb,saturate(bank*.65+n*.45)),_FoamColor.rgb,foam);
   float dx=(flowPattern(p+float2(.07,0),t)-n)/.07;
   float dy=(flowPattern(p+float2(0,.07),t)-n)/.07;
   o.Normal=normalize(float3(-dx*_RippleStrength,-dy*_RippleStrength,1));
   o.Smoothness=_Smoothness-foam*.35;o.Metallic=.05;o.Occlusion=1;
   // 새 얕은 물길 가장자리의 정점 알파만 부드럽게 지웁니다. 기존 강은 알파 1을 유지합니다.
   o.Alpha=saturate(_Opacity+foam*.1)*IN.color.a;
  }
  ENDCG
 }
 FallBack "Diffuse"
}
