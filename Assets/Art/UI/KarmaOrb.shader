Shader "DogLife/UI/Karma Water"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture",2D)="white"{}
        _LightFill("Light fill",Range(0,1))=.5
        _DarkFill("Dark fill",Range(0,1))=.5
        _WaveTime("Wave time",Float)=0
        _StencilComp("Stencil Comparison",Float)=8
        _Stencil("Stencil ID",Float)=0
        _StencilOp("Stencil Operation",Float)=0
        _StencilWriteMask("Stencil Write Mask",Float)=255
        _StencilReadMask("Stencil Read Mask",Float)=255
        _ColorMask("Color Mask",Float)=15
    }
    SubShader
    {
        Tags {"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="False"}
        Stencil {Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask]}
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata {float4 vertex:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
            struct v2f {float4 vertex:SV_POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;float4 world:TEXCOORD1;};
            float _LightFill,_DarkFill,_WaveTime;float4 _ClipRect;
            v2f vert(appdata v){v2f o;o.world=v.vertex;o.vertex=UnityObjectToClipPos(v.vertex);o.color=v.color;o.uv=v.uv;return o;}
            fixed4 frag(v2f i):SV_Target
            {
                float2 p=(i.uv-.5)*2;float d=length(p);float radius=.66;
                float side=smoothstep(-.015,.015,p.x);
                float fill=lerp(_LightFill,_DarkFill,side);
                float3 water=lerp(float3(.18,.73,1),float3(.92,.12,.21),side);
                float pulse=.92+.08*sin(_WaveTime*1.8);
                float glow=exp(-max(0,d-radius)*12)*smoothstep(.99,.79,d)*(.08+.65*fill*fill)*pulse;
                float4 result=float4(water,glow);
                float disc=1-smoothstep(radius-.006,radius+.006,d);
                float3 inside=lerp(float3(.045,.075,.12),float3(.13,.035,.055),side);
                float height=lerp(-radius-.05,radius+.05,fill);
                float wave=(sin(p.x*12+_WaveTime*1.7)+.45*sin(p.x*21-_WaveTime*1.2))*.018*sin(fill*3.14159);
                float backWave=sin(p.x*10-_WaveTime*1.4)*.02*sin(fill*3.14159);
                float wet=1-smoothstep(height+wave-.008,height+wave+.008,p.y);
                float back=1-smoothstep(height+backWave+.02,height+backWave+.04,p.y);
                inside=lerp(inside,water*.37,back*.7);
                float3 liquid=water*(.52+.42*saturate((p.y+radius)/(2*radius)));
                inside=lerp(inside,liquid,wet);
                float crest=(1-smoothstep(.006,.022,abs(p.y-height-wave)))*step(.01,fill)*step(fill,.99);
                inside+=water*crest*.65;
                float seam=(1-smoothstep(.007,.016,abs(p.x)))*.7;
                inside=lerp(inside,float3(.65,.57,.40),seam);
                float reflection=pow(saturate(1-length((p-float2(-.20,.32))*float2(2.4,6))),4)*.22;
                inside+=reflection;
                result=lerp(result,float4(inside,.97),disc);
                float rim=(1-smoothstep(.018,.027,abs(d-(radius+.018))));
                float3 gold=lerp(float3(.30,.21,.11),float3(.93,.82,.57),saturate(.5+p.y*.7-p.x*.3));
                result=lerp(result,float4(gold,1),rim);
                result*=i.color;
                #ifdef UNITY_UI_CLIP_RECT
                result.a*=UnityGet2DClipping(i.world.xy,_ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(result.a-.001);
                #endif
                return result;
            }
            ENDCG
        }
    }
}
