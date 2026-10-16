Shader "DogLife/Coastal Water"{
Properties{_Color("Water",Color)=(.03,.55,.66,1)}
SubShader{Tags{"RenderType"="Opaque"} Cull Off
CGPROGRAM
#pragma surface surf Standard vertex:vert
float4 _Color;
struct Input{float3 worldPos;};
void vert(inout appdata_full v){v.vertex.y+=sin(v.vertex.x*.65+_Time.y)*.07+cos(v.vertex.z*.48+_Time.y*.7)*.05;}
void surf(Input IN,inout SurfaceOutputStandard o){
float w=sin(IN.worldPos.x*1.2+IN.worldPos.z*.47+_Time.y)*sin(IN.worldPos.z*1.7-_Time.y*.6);
float foam=pow(saturate(w),10);
o.Albedo=_Color.rgb+foam*.23;o.Emission=_Color.rgb*.07;o.Smoothness=.28;o.Alpha=1;
}
ENDCG
}Fallback "Diffuse"}

