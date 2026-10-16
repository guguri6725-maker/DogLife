Shader "DogLife/Coastal Kraken Skin"
{
 Properties
 {
  _MainTex("Original skin detail",2D)="white"{}
  _BumpMap("Normal",2D)="bump"{}
  _Shell("Wet coastal shell",Color)=(.16,.34,.38,1)
  _Flesh("Pearl coral suckers",Color)=(.65,.55,.43,1)
  _BumpScale("Relief",Range(0,2))=.65
  _Smoothness("Wet sheen",Range(0,1))=.42
  _Color("Hit feedback baseline",Color)=(.65,.65,.65,1)
 }
 SubShader
 {
  Tags{"RenderType"="Opaque"}
  CGPROGRAM
  #pragma surface surf Standard fullforwardshadows
  #pragma target 3.0
  #include "UnityStandardUtils.cginc"
  sampler2D _MainTex,_BumpMap; fixed4 _Shell,_Flesh,_Color;half _BumpScale,_Smoothness;
  struct Input {float2 uv_MainTex;float2 uv_BumpMap;float3 worldPos;};
  void surf(Input i,inout SurfaceOutputStandard o)
  {
   // 원본 명암과 흡반 무늬를 보존하고 해안 색조로 통일한다.
   fixed3 raw=tex2D(_MainTex,i.uv_MainTex).rgb;
   half luminance=dot(raw,half3(.299,.587,.114));
   half flesh=smoothstep(.012,.105,raw.r-raw.g)*smoothstep(.15,.5,luminance);
   half mottling=.94+.06*sin(i.worldPos.x*3.4+sin(i.worldPos.z*2.1));
   o.Albedo=lerp(_Shell.rgb,_Flesh.rgb,flesh)*(.28+1.55*luminance)*mottling;
   o.Normal=UnpackScaleNormal(tex2D(_BumpMap,i.uv_BumpMap),_BumpScale);
   o.Metallic=0;o.Smoothness=_Smoothness*(.75+.25*luminance);o.Occlusion=.8+.2*luminance;o.Alpha=1;
   // CombatHealth의 기존 _Color 반짝임을 새 외피 셰이더에서도 표시한다.
   half hit=saturate((max(_Color.r,max(_Color.g,_Color.b))-.65)/.35);
   o.Albedo=lerp(o.Albedo,half3(.78,.95,.93),hit*.32);o.Emission=half3(.08,.2,.18)*hit;
  }
  ENDCG
 }
 Fallback "Standard"
}
