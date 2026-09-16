Shader "DogLife/Forest3D Surface"
{
    Properties
    {
        _MainTex ("Forest texture", 2D) = "white" {}
        _Color ("Tint", Color) = (0.82,0.94,0.78,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        sampler2D _MainTex;
        fixed4 _Color;
        struct Input { float2 uv_MainTex; };
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed3 c=tex2D(_MainTex,IN.uv_MainTex).rgb;
            // 재구성 모델의 밝은 배경 잔여색을 잎 색에 섞어 흰 반점을 완화합니다.
            float pale=smoothstep(0.53,0.84,min(c.r,min(c.g,c.b)));
            c=lerp(c,float3(0.28,0.39,0.15),pale*0.85);
            o.Albedo=c*_Color.rgb;
            o.Metallic=0; o.Smoothness=0.03; o.Occlusion=0.95;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
