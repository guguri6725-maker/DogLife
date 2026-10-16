Shader "DogLife/Season Atlas" {
Properties { _MainTex("Atlas",2D)="white"{} _Color("Tint",Color)=(1,1,1,1) _Atlas("UV region",Vector)=(0,0,0.5,0.5) }
SubShader { Tags{"RenderType"="Opaque"} Cull Off
CGPROGRAM
#pragma surface surf Standard fullforwardshadows
#pragma target 3.0
sampler2D _MainTex;fixed4 _Color;float4 _Atlas;
struct Input{float2 uv_MainTex;};
void surf(Input IN,inout SurfaceOutputStandard o){
float2 uv=_Atlas.xy+(0.008+frac(IN.uv_MainTex)*.984)*_Atlas.zw;
o.Albedo=tex2Dgrad(_MainTex,uv,ddx(IN.uv_MainTex)*.984*_Atlas.zw,ddy(IN.uv_MainTex)*.984*_Atlas.zw).rgb*_Color.rgb;o.Smoothness=.13;o.Alpha=1;
}
ENDCG
}Fallback "Diffuse"}

