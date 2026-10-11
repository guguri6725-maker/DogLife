Shader "DogLife/Season Story Sky"
{
 Properties
 {
  _Zenith("Zenith",Color)=(.16,.43,.68,1)
  _Horizon("Horizon",Color)=(.72,.82,.88,1)
  _Lower("Below horizon",Color)=(.28,.38,.4,1)
  _Dawn("Sunrise / sunset glow",Color)=(1,.65,.4,1)
  _DawnStrength("Warm horizon strength",Range(0,1))=.3
  _SunDirection("Sun direction",Vector)=(0,.25,1,0)
  _SunColor("Sunlight",Color)=(1,.9,.7,1)
  _SunSize("Sun radius radians",Range(.003,.06))=.018
  _SunStrength("Sun brightness",Range(0,4))=1
  _CloudLight("Cloud lit edge",Color)=(1,.94,.85,1)
  _CloudShadow("Cloud shadow",Color)=(.5,.6,.68,1)
  _CloudCoverage("Cloud threshold (lower = more)",Range(.25,.85))=.56
  _CloudScale("Cloud scale",Range(.3,4))=1.2
  _CloudOpacity("Cloud opacity",Range(0,1))=.8
  _Wind("Cloud wind",Vector)=(.003,.001,0,0)
  _Seed("Cloud variation",Float)=1
  _Stars("Stars",Range(0,1))=0
  _MoonDirection("Moon direction",Vector)=(0,.5,-1,0)
  _MoonColor("Moonlight",Color)=(.65,.8,1,1)
  _MoonStrength("Moon brightness",Range(0,2))=0
  _Aurora("Aurora strength",Range(0,1))=0
  _Exposure("Exposure",Range(.3,2))=1
 }
 SubShader
 {
  Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
  Cull Off ZWrite Off
  Pass
  {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma target 3.0
   #include "UnityCG.cginc"
   float4 _Zenith,_Horizon,_Lower,_Dawn,_SunColor,_CloudLight,_CloudShadow,_MoonColor;
   float4 _SunDirection,_MoonDirection,_Wind;
   float _DawnStrength,_SunSize,_SunStrength,_CloudCoverage,_CloudScale,_CloudOpacity,_Seed,_Stars,_MoonStrength,_Aurora,_Exposure;
   struct appdata {float4 vertex:POSITION;};
   struct v2f {float4 vertex:SV_POSITION;float3 direction:TEXCOORD0;};
   v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.direction=v.vertex.xyz;return o;}
   float hash(float3 p){p=frac(p*.1031);p+=dot(p,p.yzx+33.33);return frac((p.x+p.y)*p.z);}
   float noise(float3 p)
   {
    float3 i=floor(p),f=frac(p);f=f*f*(3-2*f);
    return lerp(lerp(lerp(hash(i),hash(i+float3(1,0,0)),f.x),lerp(hash(i+float3(0,1,0)),hash(i+float3(1,1,0)),f.x),f.y),
     lerp(lerp(hash(i+float3(0,0,1)),hash(i+float3(1,0,1)),f.x),lerp(hash(i+float3(0,1,1)),hash(i+1),f.x),f.y),f.z);
   }
   float fbm(float3 p)
   {return noise(p)*.54+noise(p*2.07+7)*.27+noise(p*4.19-11)*.13+noise(p*8.31+4)*.06;}
   float4 frag(v2f i):SV_Target
   {
    float3 d=normalize(i.direction),sun=normalize(_SunDirection.xyz),moon=normalize(_MoonDirection.xyz);
    float upper=saturate(d.y),sd=dot(d,sun),md=dot(d,moon);
    float3 sky=lerp(_Horizon.rgb,_Zenith.rgb,pow(upper,.55));
    sky=lerp(_Lower.rgb,sky,smoothstep(-.14,.025,d.y));
    float warm=pow(saturate(sd*.5+.5),7)*exp(-abs(d.y-sun.y)*4.5)*_DawnStrength;
    sky=lerp(sky,_Dawn.rgb,warm*.8);
    sky+=_SunColor.rgb*(pow(saturate(sd),80)*.14+pow(saturate(sd),700)*.12)*_SunStrength;
    float disk=smoothstep(cos(_SunSize*1.13),cos(_SunSize),sd)*smoothstep(-.018,.006,d.y);
    sky+=_SunColor.rgb*disk*_SunStrength;
    if(_Stars>.001)
    {
     float star=pow(saturate(noise(d*340+12)),55)*12;
     sky+=float3(.74,.84,1)*star*_Stars*smoothstep(.04,.4,upper)*( .84+.16*sin(_Time.y*.5+hash(floor(d*340))*25));
    }
    if(_MoonStrength>.001)
    {
     float moonDisk=smoothstep(cos(.024),cos(.021),md);
     float crater=.82+.18*noise(d*190);
     sky+=_MoonColor.rgb*(moonDisk*crater+pow(saturate(md),170)*.1)*_MoonStrength;
    }
    if(_Aurora>.001)
    {
     float a=atan2(d.z,d.x),h=.26+.08*sin(a*2+_Time.y*.018)+.035*sin(a*5);
     float ribbon=exp(-abs(d.y-h)*26)*.6+exp(-max(0,d.y-h)*13)*smoothstep(h-.008,h+.014,d.y)*.26;
     float threads=.52+.48*sin(a*98+sin(a*17)*2+_Time.y*.1);
     float mask=pow(saturate(d.z*.5+.5),6)*smoothstep(.04,.18,d.y);
     sky+=lerp(float3(.16,.67,.48),float3(.37,.24,.7),saturate((d.y-h)*5))*(ribbon*(.68+.32*threads)*mask*_Aurora);
    }
    // Project through a curved cloud layer: direction-only coordinates keep all six sky faces seamless.
    float3 p=float3(d.x/(upper+.3),.7,d.z/(upper+.3))*_CloudScale;
    p.xz+=_Wind.xy*_Time.y;p+=_Seed;
    float n=fbm(p),detail=fbm(p*2.4+float3(3,1,9));
    float density=smoothstep(_CloudCoverage-.08,_CloudCoverage+.14,n+detail*.10);
    float edge=smoothstep(.0,.1,upper)*(1-smoothstep(.96,1,upper)*.25);
    float light=saturate(.30+detail*.65+max(0,sd)*.22+(1-density)*.25);
    float3 cloud=lerp(_CloudShadow.rgb,_CloudLight.rgb,light);
    cloud+=_SunColor.rgb*pow(saturate(sd),16)*(1-density)*.17*_SunStrength;
    sky=lerp(sky,cloud,density*_CloudOpacity*edge);
    // Fine high wisps remain transparent, keeping the sun and broad cumulus legible.
    float wisps=smoothstep(.62,.84,fbm(float3(p.x*1.4,p.y,p.z*4.5)+19));
    sky=lerp(sky,_CloudLight.rgb,wisps*.12*edge*_CloudOpacity);
    return float4(max(0,sky)*_Exposure,1);
   }
   ENDCG
  }
 }
 Fallback Off
}
