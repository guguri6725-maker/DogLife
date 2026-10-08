Shader "DogLife/FusionSigil"
{
 Properties{_Tint("Soul tint",Color)=(.2,.7,1,1) _Strength("Intensity",Float)=1 _Clock("Unscaled time",Float)=0}
 SubShader
 {
  Tags{"Queue"="Transparent" "RenderType"="Transparent"}
  Blend SrcAlpha One ZWrite Off Cull Off
  Pass
  {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   struct v2f{float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;};
   float4 _Tint;float _Strength,_Clock;
   v2f vert(appdata_base v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.texcoord.xy*2-1;return o;}
   float ring(float r,float at,float width){return 1-smoothstep(width,width+.009,abs(r-at));}
   fixed4 frag(v2f i):SV_Target
   {
    float r=length(i.uv),a=atan2(i.uv.y,i.uv.x),t=_Clock*.12;
    float bands=ring(r,.87,.004)+ring(r,.81,.002)+ring(r,.62,.004)+ring(r,.57,.002);
    float ticks=pow(saturate(cos((a+t)*24)),32)*smoothstep(.63,.65,r)*(1-smoothstep(.77,.80,r));
    float teeth=ring(r,.715,.014)*step(.1,sin((a+t)*12))*step(-.2,cos((a+t)*36));
    float petals=ring(r,.35+.09*cos(a*6-t),.003)*.7;
    float spokes=pow(saturate(cos(a*6-t)),100)*smoothstep(.38,.45,r)*(1-smoothstep(.56,.61,r));
    float mist=exp(-r*5)*(.2+.08*sin(a*5-r*18+t*10));
    float arc=ring(r,.94,.008)*pow(saturate(sin(a*3+t*7)),6);
    float intensity=(bands+ticks+teeth+petals+spokes+arc)*.65+mist;
    return float4(_Tint.rgb*(.8+intensity*.4),saturate(intensity*_Strength)*(1-smoothstep(.96,1,r)));
   }
   ENDCG
  }
 }
}
