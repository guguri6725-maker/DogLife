Shader "DogLife/Summer/Waterfall Sheet"
{
 Properties
 {
  _Tint("Water tint",Color)=(.26,.65,.68,1)
  _FoamColor("Foam colour",Color)=(.82,.94,.91,1)
  _FlowSpeed("Fall speed",Range(0,8))=2.2
  _Opacity("Opacity",Range(0,1))=.72
  _PreviewTime("Preview time (-1 uses game time)",Float)=-1
 }
 SubShader
 {
  Tags {"Queue"="Transparent" "RenderType"="Transparent"} Cull Off ZWrite Off
  CGPROGRAM
  #pragma surface surf Standard alpha:fade vertex:vert
  #pragma target 3.0
  #include "SummerFlow.cginc"
  fixed4 _Tint,_FoamColor;float _FlowSpeed,_Opacity,_PreviewTime;
  struct Input {float3 worldPos;float4 color:COLOR;};
  void vert(inout appdata_full v)
  {
   // A subtle sideways flutter breaks the rigid silhouette without moving the bank.
   float t=_PreviewTime<0?_Time.y:_PreviewTime;
   // FBX 단위/문 크기에 관계없이 물막은 월드 기준 3.5cm만 흔들립니다.
   float3 world=mul(unity_ObjectToWorld,v.vertex).xyz;
   float3 sideways=mul((float3x3)unity_ObjectToWorld,float3(1,0,0));
   sideways/=max(length(sideways),.0001);
   world+=sideways*sin(world.y*2.4+t*3)*.035*sin(v.color.g*3.14159);
   v.vertex=mul(unity_WorldToObject,float4(world,1));
  }
  void surf(Input IN,inout SurfaceOutputStandard o)
  {
   float t=(_PreviewTime<0?_Time.y:_PreviewTime)*_FlowSpeed;
   float across=IN.worldPos.x+IN.worldPos.z;
   float n=flowPattern(float2(-IN.worldPos.y,across*2),t);
   float foam=smoothstep(.45,.73,n);
   float edge=smoothstep(0,.18,1-IN.color.r);
   o.Albedo=lerp(_Tint.rgb,_FoamColor.rgb,foam*.85);
   o.Normal=normalize(float3(sin(across*15+n*5)*.08,n*.13,1));
   o.Smoothness=.85;o.Alpha=edge*_Opacity*(.55+.45*n);
  }
  ENDCG
 }
 FallBack "Transparent/Diffuse"
}
