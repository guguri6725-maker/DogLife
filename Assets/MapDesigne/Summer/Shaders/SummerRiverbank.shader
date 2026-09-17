Shader "DogLife/Summer/Riverbank"
{
 Properties
 {
  _SoilColor("Soil",Color)=(.42,.34,.21,1)
  _RockColor("Rock",Color)=(.29,.32,.28,1)
  _MossColor("Moss",Color)=(.17,.23,.09,1)
  _MossAmount("Moss amount",Range(0,1))=.2
 }
 SubShader
 {
  Tags {"RenderType"="Opaque"}
  CGPROGRAM
  #pragma surface surf Standard fullforwardshadows
  #pragma target 3.0
  #include "SummerFlow.cginc"
  fixed4 _SoilColor,_RockColor,_MossColor;float _MossAmount;
  struct Input {float3 worldPos;float3 worldNormal;INTERNAL_DATA};
  void surf(Input IN,inout SurfaceOutputStandard o)
  {
   float3 n=abs(WorldNormalVector(IN,float3(0,0,1)));
   float2 p=n.y>.55?IN.worldPos.xz:(n.x>n.z?IN.worldPos.zy:IN.worldPos.xy);
   float broad=waterNoise(p*.38),fine=waterNoise(p*8),grain=waterNoise(p*36);
   float rocks=smoothstep(.48,.66,waterNoise(p*1.1));
   fixed3 col=lerp(_SoilColor.rgb,_RockColor.rgb,rocks*.7);
   col=lerp(col,_MossColor.rgb,smoothstep(1-_MossAmount,1,broad));
   // Darken the lower banks beneath the waterline, without shifting the walkable surface.
   float wet=1-smoothstep(-.5,.15,IN.worldPos.y);
   o.Albedo=col*(.8+broad*.22+fine*.18+grain*.12)*(1-wet*.3);
   o.Normal=normalize(float3((fine-.5)*.1,(grain-.5)*.1,1));
   o.Smoothness=lerp(.12,.4,wet);o.Occlusion=1;
  }
  ENDCG
 }
 FallBack "Diffuse"
}
