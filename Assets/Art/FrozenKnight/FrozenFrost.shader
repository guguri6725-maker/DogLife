Shader "DogLife/Frozen Knight Frost"
{
    Properties { _Tint ("Frost tint", Color) = (1,1,1,1) _SurfaceDetail("Ice surface",Float)=0 _FieldOrigin("Field center",Vector)=(0,0,0,1) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Tint;
            float _SurfaceDetail;float4 _FieldOrigin;
            float2 IceHash(float2 p){return frac(sin(float2(dot(p,float2(127.1,311.7)),dot(p,float2(269.5,183.3))))*43758.5453);}
            struct appdata { float4 vertex:POSITION; fixed4 color:COLOR; float3 normal:NORMAL; };
            struct v2f { float4 position:SV_POSITION; fixed4 color:COLOR; float3 world:TEXCOORD0; float3 normal:TEXCOORD1; };
            v2f vert(appdata v) { v2f o; o.position=UnityObjectToClipPos(v.vertex);o.world=mul(unity_ObjectToWorld,v.vertex).xyz;o.color=v.color*_Tint;o.normal=UnityObjectToWorldNormal(v.normal);return o; }
            fixed4 frag(v2f i):SV_Target
            {
                // 일직선 줄무늬 대신 미세한 서리 입자만 섞어 범위 경계를 또렷하게 유지합니다.
                float2 cell=floor(i.world.xz*24);
                float grain=frac(sin(dot(cell,float2(127.1,311.7)))*43758.5453);
                float frost=.94+.06*grain;
                // 입체 결정에는 면별 밝기와 얇은 가장자리 광택을 주고 바닥 경고는 원색을 유지합니다.
                float valid=step(.1,dot(i.normal,i.normal));float3 n=normalize(i.normal+float3(0,.0001,0));
                float facet=.55+.45*abs(dot(n,normalize(float3(.4,1,.6))));
                float rim=pow(1-saturate(abs(dot(n,normalize(_WorldSpaceCameraPos-i.world)))),3)*valid;
                float3 rgb=i.color.rgb*frost*lerp(1,facet,valid)+rim*float3(.16,.28,.32);float alpha=i.color.a;
                if(_SurfaceDetail>.01)
                {
                    // 공간에 고정된 얼음 판과 균열 위로 냉기만 천천히 흐릅니다.
                    float2 uv=(i.world.xz-_FieldOrigin.xz)*.8,cell=floor(uv),f=frac(uv);float d1=10,d2=10;
                    for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++)
                    {float2 o=float2(x,y),v=o+IceHash(cell+o)-f;float d=dot(v,v);if(d<d1){d2=d1;d1=d;}else d2=min(d2,d);}
                    float crack=(1-smoothstep(.012,.075,d2-d1))*_SurfaceDetail;
                    float flow=sin(uv.x*.85+uv.y*.6+sin(uv.y*.43-_Time.y*.35)*1.6+_Time.y*.6)*.5+.5;
                    float pulse=pow(saturate(sin(length(uv)*.9-_Time.y*1.8)*.5+.5),6);
                    rgb=lerp(rgb,float3(.08,.28,.46),.32)+crack*float3(.3,.65,.8)+flow*.07+crack*pulse*.14;
                    alpha=saturate(alpha*(.72+flow*.28)+crack*.18);
                }
                return fixed4(rgb,alpha);
            }
            ENDCG
        }
    }
}
