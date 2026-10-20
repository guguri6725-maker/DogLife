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
            // 유리 안의 불규칙한 안개로 상태를 암시하고 수치 눈금은 드러내지 않습니다.
            float grain(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float mist(float2 p)
            {
                float2 cell=floor(p),f=frac(p);f=f*f*(3-2*f);
                return lerp(lerp(grain(cell),grain(cell+float2(1,0)),f.x),
                    lerp(grain(cell+float2(0,1)),grain(cell+1),f.x),f.y);
            }
            fixed4 frag(v2f i):SV_Target
            {
                float2 p=(i.uv-.5)*2;float d=length(p);float radius=.66;
                float time=_WaveTime*.055;
                float cloud=mist(p*3+float2(time,-time*.7));
                cloud=cloud*.65+mist(p*7+cloud*2-float2(time*.4,time))*.35;
                float veil=smoothstep(.27,.78,cloud);
                // 중립 색은 유지하고, 카르마가 쌓일수록 차가운 청록과 따뜻한 적갈색이 은은하게 드러납니다.
                float mood=smoothstep(0,1,saturate(.5+(_DarkFill-_LightFill)*.5));
                float3 tint=lerp(float3(.17,.34,.34),float3(.38,.22,.20),mood);
                float bowl=sqrt(saturate(1-dot(p,p)/(radius*radius)));
                float3 inside=float3(.035,.042,.046)+tint*veil*(.17+.33*bowl);
                float wisp=pow(saturate(1-abs(cloud-.52)*9),3);
                inside+=tint*wisp*.13*bowl;
                float glow=exp(-max(0,d-radius)*20)*(1-smoothstep(.70,.84,d))*.06;
                float4 result=float4(float3(.35,.30,.22),glow);
                float disc=1-smoothstep(radius-.006,radius+.006,d);
                float reflection=pow(saturate(1-length((p-float2(-.20,.32))*float2(2.1,5))),3)*.16;
                inside+=float3(.73,.77,.74)*reflection;
                inside+=pow(1-bowl,3)*float3(.10,.12,.115)*.45;
                result=lerp(result,float4(inside,.97),disc);
                float rim=(1-smoothstep(.012,.022,abs(d-(radius+.025))));
                float3 gold=lerp(float3(.15,.12,.08),float3(.66,.57,.39),saturate(.5+p.y*.7-p.x*.3));
                gold*=.88+.12*mist(p*48);
                result=lerp(result,float4(gold,1),rim);
                // 낡은 황동 테두리와 가는 덩굴 각인: 측정기 대신 작은 유물처럼 표현합니다.
                float angle=atan2(p.y,p.x);
                float outer=1-smoothstep(.003,.009,abs(d-.745));
                float vine=1-smoothstep(.003,.008,abs(d-(.720+.007*sin(angle*7))));
                result=lerp(result,float4(gold*.64,.9),saturate(outer+vine*.45));
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
