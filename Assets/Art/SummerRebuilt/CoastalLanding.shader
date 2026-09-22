// 닫힌 입체 거품의 바깥 면만 그려 뒷면이 윗면에 겹쳐 보이지 않게 합니다.
Shader "DogLife/Coastal Landing Foam"
{
 Properties {
 _MainTex("Water atlas",2D)="white"{}
 _Color("Foam tint",Color)=(.72,.91,.95,.72)
 _Opacity("Opacity",Range(0,1))=.6
 _GateMode("Gate",Float)=0
 _OpenProgress("Opening",Range(0,1))=0
 _FlowTime("Clock",Float)=-1
 }
 SubShader {
 Tags {"Queue"="Transparent+10" "RenderType"="Transparent"}
 Cull Back ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
 Pass {
 CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 sampler2D _MainTex;float4 _Color;float _Opacity,_GateMode,_OpenProgress,_FlowTime;
 struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float3 world:TEXCOORD1;};
 v2f vert(appdata_base v){
 v2f o;
 float t=_FlowTime<0?_Time.y:_FlowTime;
 float2 q=v.texcoord.xy*2-1;
 // 중앙에만 작은 높이 변화를 주고 가장자리는 수면 아래에 고정한다.
 float envelope=pow(saturate(1-abs(q.y)),2)*saturate(1-q.x*q.x);
 float top=step(-.09,v.vertex.y);
 v.vertex.y+=sin(abs(q.y)*16-t*2.3+q.x*3)*.025*envelope*top;
 o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.texcoord;o.world=mul(unity_ObjectToWorld,v.vertex).xyz;return o;}
 fixed4 frag(v2f i):SV_Target {
 float t=_FlowTime<0?_Time.y:_FlowTime;
 float2 q=i.uv*2-1;
 float d=abs(q.y);
 float noise=.04*sin(q.x*23+t)+.025*sin(q.x*47-t);
 float mask=(1-smoothstep(.32,1,d+noise))*(1-smoothstep(.82,1,abs(q.x)));
 // 넓고 굵은 거품 띠가 바깥으로 퍼져 낮은 카메라에서도 잘 보인다.
 float wave=pow(.5+.5*sin(d*14-t*2.3+q.x),3);
 float grain=tex2D(_MainTex,float2(.51,0.01)+frac(i.uv*float2(3,1)+float2(0,-t*.13))*.48).g;
 if(_GateMode>.5){float stopAt=.04+.50*pow(abs(q.x),1.25);float drained=saturate((_OpenProgress-stopAt)/.42);mask*=1-smoothstep(.72,1,drained);}
  // 접점 중앙은 같은 바닥 물로 덮어 문턱의 마른 띠가 비치지 않게 한다.
 float2 flow=i.world.zx*.16-float2(0,_Time.y*.25*.16);
 float2 tile=frac(flow);tile.x=1-abs(frac(flow.x*.5)*2-1);
 float blend=sin(tile.y*3.14159);blend*=blend;
 float2 uv1=float2(.5,0)+(.012+tile*.976)*.5;
 float2 uv2=float2(.5,0)+(.012+float2(tile.x,frac(flow.y+.5))*.976)*.5;
 float3 baseWater=lerp(tex2D(_MainTex,uv2).rgb,tex2D(_MainTex,uv1).rgb,blend)*float3(.55,.84,.89);
 float foam=saturate((.42+.55*wave)*(.6+.4*grain));
 return fixed4(lerp(baseWater,_Color.rgb,foam),mask*_Opacity);
 }
 ENDCG
 }
 }
}

