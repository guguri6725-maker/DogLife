Shader "DogLife/Kraken Flood Surface"
{
 Properties
 {
  _Shallow("Shallow water",Color)=(.13,.43,.44,1)
  _Deep("Deep water",Color)=(.018,.11,.16,1)
  _Foam("Sea foam",Color)=(.79,.89,.84,1)
  _WaveHeight("Wave height",Range(0,.5))=.13
  _Refraction("Refraction",Range(0,.04))=.012
  _Absorption("Depth absorption",Range(.02,1))=.22
  _FoamWidth("Contact foam width",Range(.1,3))=.8
  _Reveal("Surface transition",Range(0,1))=1
 }
 SubShader
 {
  Tags{"Queue"="Transparent-10" "RenderType"="Transparent"}
  GrabPass{"_KrakenSceneColor"}
  Pass
  {
   ZWrite Off Cull Off Blend SrcAlpha OneMinusSrcAlpha
   CGPROGRAM
   #pragma target 3.0
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_fog
   #include "UnityCG.cginc"
   #include "Lighting.cginc"
   sampler2D _KrakenSceneColor;UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
   float4 _Shallow,_Deep,_Foam;float _WaveHeight,_Refraction,_Absorption,_FoamWidth;
   float _Reveal;
   struct v2f{float4 pos:SV_POSITION;float3 world:TEXCOORD0;float4 screen:TEXCOORD1;float4 grab:TEXCOORD2;float eye:TEXCOORD3;UNITY_FOG_COORDS(4)};
   float wave(float2 p){return (sin(dot(p,float2(.49,.21))-_Time.y*1.1)+.42*sin(dot(p,float2(-.32,.79))-_Time.y*1.43)+.18*sin(dot(p,float2(1.6,.65))+_Time.y*1.8))*_WaveHeight;}
   float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
   v2f vert(appdata_base v)
   {
    v2f o;float3 w=mul(unity_ObjectToWorld,v.vertex).xyz;w.y+=wave(w.xz);o.world=w;o.pos=mul(UNITY_MATRIX_VP,float4(w,1));o.screen=ComputeScreenPos(o.pos);o.grab=ComputeGrabScreenPos(o.pos);o.eye=-mul(UNITY_MATRIX_V,float4(w,1)).z;UNITY_TRANSFER_FOG(o,o.pos);return o;
   }
   fixed4 frag(v2f i):SV_Target
   {
    float2 p=i.world.xz;float h=wave(p),dx=(wave(p+float2(.06,0))-h)/.06,dz=(wave(p+float2(0,.06))-h)/.06;
    float2 flow=p*1.9+float2(-.18,.12)*_Time.y;
    dx+=(noise(flow+float2(.08,0))-noise(flow))*.5;dz+=(noise(flow+float2(0,.08))-noise(flow))*.5;
    float3 n=normalize(float3(-dx,1,-dz)),v=normalize(_WorldSpaceCameraPos-i.world);bool under=v.y<0;if(under)n=-n;
    float fresnel=.025+.975*pow(1-saturate(dot(n,v)),5);
    float2 uv=i.screen.xy/i.screen.w;float depth=LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture,uv));float gap=max(0,depth-i.eye);
    float2 refract=n.xz*_Refraction*saturate(gap*.5)*_Reveal;float2 guv=i.grab.xy/i.grab.w;
    float distortedDepth=LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture,uv+refract));refract*=step(i.eye,distortedDepth);
    float3 scene=tex2D(_KrakenSceneColor,guv+refract).rgb;
    float density=1-exp(-gap*_Absorption);float3 tint=lerp(_Shallow.rgb,_Deep.rgb,density);
    float3 water=lerp(scene,tint,density*.88+.08);
    half4 env=UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0,reflect(-v,n),1.5);float3 reflection=DecodeHDR(env,unity_SpecCube0_HDR);
    reflection=max(reflection,lerp(float3(.09,.18,.22),float3(.35,.5,.54),saturate(n.y))*.45);
    water=lerp(water,reflection,min(.8,fresnel));
    float3 light=normalize(_WorldSpaceLightPos0.xyz),halfDir=normalize(light+v);
    water+=_LightColor0.rgb*pow(saturate(dot(n,halfDir)),160)*.75;
    float breakup=noise(p*3.1+_Time.y*.19)*.65+noise(p*9.2-_Time.y*.32)*.35;
    float contact=(1-smoothstep(.02,_FoamWidth,gap))*smoothstep(.22,.7,breakup);
    float crest=smoothstep(_WaveHeight*1.08,_WaveHeight*1.45,h)*smoothstep(.62,.86,breakup)*.16;
    water=lerp(water,_Foam.rgb,saturate(contact+crest)*(under?.12:.7));
    if(under)water=lerp(water,_Shallow.rgb,.18);
    fixed4 c=fixed4(water,_Reveal);UNITY_APPLY_FOG(i.fogCoord,c);return c;
   }
   ENDCG
  }
 }
}
