Shader "DogLife/EnemyGuideSilhouette"
{
    Properties
    {
        _Tint("Silhouette rim", Color) = (0.6,0.5,0.3,1)
        _Body("Hidden body", Color) = (0.018,0.025,0.03,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            Cull Front
            CGPROGRAM
            #pragma vertex outline
            #pragma fragment edge
            #include "UnityCG.cginc"
            half4 _Tint;
            float4 outline(appdata_base v):SV_POSITION
            {
                v.vertex.xyz += normalize(v.normal) * .018;
                return UnityObjectToClipPos(v.vertex);
            }
            half4 edge():SV_Target { return half4(_Tint.rgb*.8,1); }
            ENDCG
        }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0; float3 positionWS:TEXCOORD1; };
            half4 _Tint, _Body;
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = mul(unity_ObjectToWorld,v.positionOS).xyz;
                o.positionCS = UnityObjectToClipPos(v.positionOS);
                o.normalWS = UnityObjectToWorldNormal(v.normalOS);
                return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                // 원본 텍스처는 사용하지 않고 어두운 몸체와 얇은 윤곽만 보여줍니다.
                // 내부 굴곡에 빛을 주지 않아 얼굴과 장식의 세부 형태를 가립니다.
                return half4(_Body.rgb + _Tint.rgb*.12,1);
            }
            ENDHLSL
        }
    }
}
