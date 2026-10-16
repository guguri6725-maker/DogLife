Shader "DogLife/Golem Glacier"
{
    Properties
    {
        _Color("Tint",Color)=(1,1,1,1)
        _IceTint("Deep ice absorption",Color)=(.16,.42,.52,1)
        _Refraction("Refraction",Range(0,.05))=.018
        _Frost("Surface frost",Range(0,1))=.32
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off
        // 가까운 얼음 표면만 그려 내부 삼각형의 겹침을 방지합니다. 범위 표시는 제외합니다.
        Pass
        {
            ZWrite On ColorMask 0
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct input { float4 vertex:POSITION; float4 uv:TEXCOORD0; float4 color:COLOR; };
            struct output { float4 pos:SV_POSITION; float2 mask:TEXCOORD0; };
            output vert(input v){output o;o.pos=UnityObjectToClipPos(v.vertex);o.mask=float2(v.uv.x,v.color.a);return o;}
            float4 frag(output i):SV_Target{clip(i.mask.x-.5);clip(i.mask.y-.12);return 0;}
            ENDCG
        }
        GrabPass { "_GolemIceBackground" }
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            sampler2D _GolemIceBackground;
            float4 _Color,_IceTint;
            float _Refraction,_Frost;
            struct input {float4 vertex:POSITION;float4 color:COLOR;float4 uv:TEXCOORD0;};
            struct output {float4 pos:SV_POSITION;float4 color:COLOR;float3 world:TEXCOORD0;float3 local:TEXCOORD1;float4 grab:TEXCOORD2;float3 data:TEXCOORD3;};
            output vert(input v)
            {
                output o;o.pos=UnityObjectToClipPos(v.vertex);o.color=v.color*_Color;
                o.world=mul(unity_ObjectToWorld,v.vertex).xyz;o.local=v.vertex.xyz;
                o.grab=ComputeGrabScreenPos(o.pos);o.data=v.uv.xyz;return o;
            }
            float2 hash(float2 p){return frac(sin(float2(dot(p,float2(127.1,311.7)),dot(p,float2(269.5,183.3))))*43758.5453);}
            float cracks(float2 uv)
            {
                float2 cell=floor(uv),f=frac(uv);float first=20,second=20;
                [unroll]for(int y=-1;y<=1;y++)[unroll]for(int x=-1;x<=1;x++)
                {
                    float2 offset=float2(x,y),d=offset+hash(cell+offset)-f;float dist=dot(d,d);
                    if(dist<first){second=first;first=dist;}else second=min(second,dist);
                }
                float gap=second-first;
                return 1-smoothstep(.002,.012+fwidth(gap)*.65,gap);
            }
            float4 frag(output i):SV_Target
            {
                if(i.data.x<.5)return i.color;
                // 면 방향, 환경 반사와 배경 굴절로 투명한 얼음의 두께를 표현합니다.
                float3 n=normalize(cross(ddy(i.world),ddx(i.world)));
                float3 view=normalize(_WorldSpaceCameraPos-i.world);n*=dot(n,view)<0?-1:1;
                float facing=saturate(dot(n,view));float fresnel=.055+.945*pow(1-facing,3);
                float2 plane=abs(n.x)>abs(n.z)?i.local.zy:i.local.xy;
                plane+=i.data.y*.173;
                float vein=cracks(plane*5.5+sin(plane.yx*3)*.16);
                float inner=cracks(plane*3.7+view.xy*.09+4.7)*.25;
                float grain=hash(floor(plane*210)).x;
                float frost=saturate(_Frost*((1-smoothstep(.02,.30,i.data.z))*.85+pow(grain,7)*.4)+vein*.17);
                float3 viewNormal=mul((float3x3)UNITY_MATRIX_V,n);
                float4 grab=i.grab;grab.xy+=viewNormal.xy*_Refraction*grab.w*(1-frost);
                float3 background=tex2Dproj(_GolemIceBackground,UNITY_PROJ_COORD(grab)).rgb;
                float thickness=.12+(1-i.data.z)*.22;
                float3 transmission=background*lerp(float3(.94,.99,1),_IceTint.rgb,thickness);
                transmission+=float3(.07,.12,.14)*(1-frost);
                float3 reflected=reflect(-view,n);
                float3 environment=DecodeHDR(UNITY_SAMPLE_TEXCUBE(unity_SpecCube0,reflected),unity_SpecCube0_HDR);
                // 반사 프로브가 없는 미리보기에서도 차가운 하늘의 반사광을 유지합니다.
                environment=max(environment,lerp(float3(.13,.25,.32),float3(.70,.86,.94),saturate(reflected.y*.65+.45)));
                float3 lightDir=normalize(UnityWorldSpaceLightDir(i.world));
                float3 halfDir=normalize(view+lightDir);
                float highlight=(pow(saturate(dot(n,halfDir)),110)*1.8+pow(saturate(dot(n,halfDir)),24)*.18)*(1-frost*.65);
                float diffuse=.35+.65*saturate(dot(n,lightDir));
                float3 color=lerp(transmission,environment,fresnel);
                color+=_LightColor0.rgb*highlight*.9;
                color+=float3(.60,.83,.92)*(vein*.19+inner*.13);
                color=lerp(color,float3(.75,.87,.91)*diffuse,frost);
                return float4(color,saturate(i.color.a+.04));
            }
            ENDCG
        }
    }
}
