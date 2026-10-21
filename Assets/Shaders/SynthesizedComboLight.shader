Shader "DogLife/Synthesized Combo Light"
{
    Properties { _Color ("Tint", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            float4 _Color;
            v2f vert(appdata v) { v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.color=v.color*_Color; o.uv=v.uv; return o; }
            float4 frag(v2f i) : SV_Target
            {
                // 선 가장자리는 부드럽게, 중심은 밝게 남겨 불투명한 띠처럼 보이지 않게 합니다.
                float edge=abs(i.uv.y*2-1);
                float feather=1-smoothstep(.18,1,edge);
                float core=1-smoothstep(0,.32,edge);
                return float4(i.color.rgb*(1.4+core*1.3),i.color.a*feather);
            }
            ENDHLSL
        }
    }
}
