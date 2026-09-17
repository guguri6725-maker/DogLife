Shader "DogLife/Summer/Atlas Water Flow"
{
 Properties
 {
  _MainTex("Original colour",2D)="white"{}
  _BumpMap("Original normal",2D)="bump"{}
  _WaterEnabled("Animate water (0 converts to dry rock)",Range(0,1))=1
  _FlowSpeed("Flow speed",Range(0,3))=.7
  _PreviewTime("Preview time (-1 uses game time)",Float)=-1
 }
 SubShader
 {
  Tags {"RenderType"="Opaque"}
  CGPROGRAM
  #pragma surface surf Standard fullforwardshadows
  #pragma target 3.0
  #include "SummerFlow.cginc"
  sampler2D _MainTex,_BumpMap;float _WaterEnabled,_FlowSpeed,_PreviewTime;
  struct Input {float2 uv_MainTex;float2 uv_BumpMap;float3 worldPos;float3 worldNormal;INTERNAL_DATA};
  void surf(Input IN,inout SurfaceOutputStandard o)
  {
   fixed3 c=tex2D(_MainTex,IN.uv_MainTex).rgb;
   // Restrict motion to blue/cyan water, preserving the surrounding rock and leaves.
   float mask=smoothstep(.015,.12,c.b-c.r)*smoothstep(-.025,.09,c.g-c.r);
   // On dry variants also suppress bright, neutral baked foam left around cyan pixels.
   float neutral=1-smoothstep(.06,.2,max(c.r,max(c.g,c.b))-min(c.r,min(c.g,c.b)));
   mask=saturate(mask+(1-_WaterEnabled)*neutral*smoothstep(.5,.85,min(c.r,min(c.g,c.b))));
   float vertical=1-smoothstep(.3,.8,abs(WorldNormalVector(IN,float3(0,0,1)).y));
   float t=(_PreviewTime<0?_Time.y:_PreviewTime)*_FlowSpeed;
   float2 p=lerp(IN.worldPos.xz,float2(-IN.worldPos.y,(IN.worldPos.x+IN.worldPos.z)*.23),vertical);
   float n=flowPattern(p,t);
   float stripe=pow(saturate(n),3)*.6;
   fixed3 water=lerp(fixed3(.045,.26,.28),fixed3(.44,.69,.65),n)+stripe;
   float grain=waterNoise(IN.worldPos.xz*3)*.18;
   fixed3 dry=fixed3(.34,.35,.29)*(0.8+grain+dot(c,fixed3(.21,.72,.07))*.45);
   o.Albedo=lerp(c,lerp(dry,water,_WaterEnabled),mask*.94);
   o.Normal=UnpackNormal(tex2D(_BumpMap,IN.uv_BumpMap));
   o.Normal=normalize(lerp(o.Normal,float3(sin(p.y*8+n*4)*.08,n*.1,1),mask*_WaterEnabled));
   o.Smoothness=lerp(.16,.75,mask*_WaterEnabled);o.Occlusion=1;
  }
  ENDCG
 }
 FallBack "Diffuse"
}
