Shader "DogLife/Coastal Ground" {
Properties{_MainTex("Sand Atlas",2D)="white"{} _Grass("Grass",2D)="white"{} _Summer("Summer",Range(0,1))=.5}
SubShader{Tags{"RenderType"="Opaque"}
CGPROGRAM
#pragma surface surf Standard fullforwardshadows
#pragma target 3.0
sampler2D _MainTex,_Grass;float _Summer;
struct Input{float3 worldPos;float2 uv_MainTex;};
void surf(Input IN,inout SurfaceOutputStandard o){
float2 p=IN.worldPos.xz;
float variation=(sin(p.x*.36+sin(p.y*.43))*sin(p.y*.27)+1)*.5;
float sand=saturate((_Summer-.18)*1.45+(variation-.5)*.6+abs(p.y)*.008);
fixed3 a=tex2D(_Grass,p*.025).rgb;
fixed3 b=tex2Dgrad(_MainTex,float2(.5,.5)+(.008+frac(p*.13)*.984)*.5,ddx(p*.13)*.492,ddy(p*.13)*.492).rgb;
o.Albedo=lerp(a,b,sand);o.Smoothness=.08;o.Alpha=1;
}
ENDCG
}Fallback "Diffuse"}

