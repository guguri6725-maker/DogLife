Shader "DogLife/Dash Afterimage"
{
    Properties { _Color ("Tint", Color) = (.48,.87,1,.42) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Color;
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f { float4 pos : SV_POSITION; float3 normal : TEXCOORD0; float3 view : TEXCOORD1; };
            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.view = UnityWorldSpaceViewDir(mul(unity_ObjectToWorld, v.vertex).xyz);
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                // 몸 안쪽은 옅게, 실루엣 가장자리는 밝게 표시합니다.
                float rim = pow(1 - saturate(dot(normalize(i.normal), normalize(i.view))), 1.5);
                return fixed4(lerp(_Color.rgb, fixed3(.9,.98,1), rim * .65), _Color.a * lerp(.2,1,rim));
            }
            ENDCG
        }
    }
}
