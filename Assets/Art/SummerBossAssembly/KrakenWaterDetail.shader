Shader "DogLife/Kraken Living Water"
{
 Properties{_Color("Tint",Color)=(1,1,1,1) _Breakup("Foam breakup",Range(0,1))=.55 _DetailScale("Foam scale",Range(.2,5))=1.8 _Sheen("Water sheen",Range(0,1))=.45}
 SubShader
 {
  Tags{"Queue"="Transparent" "RenderType"="Transparent"}
  Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
  Pass
  {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma target 3.0
   #pragma multi_compile_fog
   #include "UnityCG.cginc"
   #include "Lighting.cginc"
   fixed4 _Color;float _Breakup,_DetailScale,_Sheen;
   struct appdata{float4 vertex:POSITION;fixed4 color:COLOR;float4 uv:TEXCOORD0;};
   struct v2f{float4 vertex:SV_POSITION;fixed4 color:COLOR;float3 world:TEXCOORD0;float4 uv:TEXCOORD1;UNITY_FOG_COORDS(2)};
   v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.color=v.color*_Color;o.world=mul(unity_ObjectToWorld,v.vertex).xyz;o.uv=v.uv;UNITY_TRANSFER_FOG(o,o.vertex);return o;}
   float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   float noise(float2 p)
   {
    float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);
    return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);
   }
   float detail(float2 p){return noise(p)*.57+noise(p*2.03+7.3)*.28+noise(p*4.13-3.1)*.15;}
   fixed4 frag(v2f i):SV_Target
   {
    fixed4 c=i.color;float kind=i.uv.z;
    if(kind>6.5){
     float2 uv=i.uv.xy;float2 disc=uv*2-1;float r=length(disc);
     if(kind<7.5){
      float rim=smoothstep(.65,.87,r)*(1-smoothstep(.9,1,r));
      float glint=pow(saturate(1-length(disc-float2(-.32,.37))/.4),3);
      c.rgb+=glint*.45;c.a*=saturate(rim*.78+glint*.8+saturate(1-r)*.055);
     }else if(kind<8.5){
      float soft=pow(saturate(1-abs(uv.x*2-1)),1.6);
      c.a*=soft*(.65+.35*noise(i.world.xz*5-_Time.y));
     }else{
      float3 p=i.world*.9;float n=detail(p.xz+p.y*.37+_Time.y*float2(-.12,.09));
      float veins=pow(saturate(1-abs(sin(p.x*2.4+p.z*1.7+p.y*2+n*5+_Time.y))),10);
      float3 normal=normalize(cross(ddy(i.world),ddx(i.world)));
      float fresnel=pow(1-abs(dot(normal,normalize(_WorldSpaceCameraPos-i.world))),2);
      c.rgb=lerp(c.rgb,float3(.55,.91,.88),veins*.4);c.a*=.4+veins*.4+fresnel*.35;
     }
     return c;
    }
    if(kind>3.5){
     float2 p=i.world.xz;float n=detail(p*1.4+float2(-.18,.11)*_Time.y);
     float veins=pow(saturate(1-abs(sin(p.x*2.7+p.y*1.9+n*6+_Time.y*.8))),9);
     float fine=noise(p*13-_Time.y*.35);
     if(kind<4.5){c.rgb*=.78+.3*n;c.a*=.82+.18*n;}
     else if(kind<5.5){c.rgb=lerp(c.rgb,float3(.62,.88,.88),veins*.33);c.a*=.65+.3*n;}
     else{c.rgb*=.85+.15*fine;c.a*=.65+.35*smoothstep(.2,.7,n+fine*.25);}
     return c;
    }
    // 판정 경계와 채워지는 선딜 표시는 별도 모드로 선명하게 유지한다.
    if(kind<.5){return c;}
    float2 flow=(i.world.xz+i.world.y*float2(.37,.51))*_DetailScale+float2(-.32,.17)*_Time.y;
    float n=detail(flow),fine=noise(flow*9+_Time.y*.25);
    if(kind>2.5)
    {
     float2 p=i.uv.xy*2-1;float falloff=saturate(1-dot(p,p));
     float vapor=detail(i.uv.xy*5+i.world.xz*.21+_Time.y*.15);
     c.a*=falloff*falloff*smoothstep(.12,.65,vapor);c.rgb*=.85+.15*vapor;
    }
    else if(kind>1.5)
    {
     // 크기가 다른 거품 덩어리와 작은 구멍을 겹쳐 포말의 밀도를 만든다.
     float density=smoothstep(.28,.7,n),bubbles=smoothstep(.27,.54,fine);
     c.a*=lerp(.45,1,density)*lerp(.7,1,bubbles)*lerp(1,.9,_Breakup);
     c.a*=smoothstep(0,.16,i.uv.x)*smoothstep(1,.84,i.uv.x)*(1-smoothstep(.5,1,i.uv.y+(.5-fine)*.28));
     c.rgb=lerp(c.rgb,float3(.9,.96,.93),density*.65)*(.82+.18*fine);
    }
    else
    {
     float3 normal=normalize(cross(ddy(i.world),ddx(i.world)));
     float3 view=normalize(_WorldSpaceCameraPos-i.world);normal*=dot(normal,view)<0?-1:1;
     normal=normalize(normal+float3((n-.5)*.3,0,(fine-.5)*.12));
     float fresnel=pow(1-saturate(dot(normal,view)),3);
     float3 halfDir=normalize(normalize(_WorldSpaceLightPos0.xyz)+view);
     float spec=pow(saturate(dot(normal,halfDir)),48)*_Sheen;
     c.rgb*=.7+.22*n;c.rgb+=float3(.22,.34,.35)*fresnel+_LightColor0.rgb*spec;
     c.a*=lerp(.7,.96,fresnel)*(1-_Breakup*.2*n);
    }
    UNITY_APPLY_FOG(i.fogCoord,c);return c;
   }
   ENDCG
  }
 }
}
