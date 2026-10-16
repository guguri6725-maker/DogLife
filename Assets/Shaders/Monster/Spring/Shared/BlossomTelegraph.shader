Shader "DogLife/Blossom Telegraph"
{
    SubShader
    {
        Tags { "Queue"="Transparent+5" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off ZWrite Off
        Offset -1,-1
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct Input { float4 vertex:POSITION; fixed4 color:COLOR; };
            struct Output { float4 vertex:SV_POSITION; fixed4 color:COLOR;float3 world:TEXCOORD0; };
            Output vert(Input v){Output o;o.vertex=UnityObjectToClipPos(v.vertex);o.color=v.color;o.world=mul(unity_ObjectToWorld,v.vertex).xyz;return o;}
            fixed4 frag(Output i):SV_Target{
                fixed4 c=i.color;
                // 범위 외곽은 선명하게 유지하고 내부 바닥에만 꽃가루의 결을 입힙니다.
                float detail=sin(i.world.x*19+sin(i.world.z*11))*sin(i.world.z*23+i.world.x*3);
                float grain=.65+.35*detail*detail;
                c.a*=lerp(grain,1,smoothstep(.5,.75,c.a));
                c.rgb*=.92+.08*grain;
                return c;
            }
            ENDCG
        }
    }
}
