Shader "DogLife/Winter Painted Rock"
{
    Properties
    {
        _MainTex ("Painted rock", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _WorldTiling ("Repeats per metre", Float) = 0.24
        _Glossiness ("Smoothness", Range(0,1)) = 0.16
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
        float _WorldTiling;
        half _Glossiness;
        struct Input { float3 worldPos; float3 worldNormal; };
        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // 월드 좌표의 세 방향을 섞어 늘어난 UV와 면별 텍스처 경계를 없앤다.
            float3 weights = pow(abs(normalize(IN.worldNormal)), 4);
            weights /= max(dot(weights, float3(1,1,1)), 0.0001);
            float3 p = IN.worldPos * _WorldTiling;
            fixed3 color = tex2D(_MainTex, p.zy).rgb * weights.x;
            color += tex2D(_MainTex, p.xz).rgb * weights.y;
            color += tex2D(_MainTex, p.xy).rgb * weights.z;
            o.Albedo = color * _Color.rgb;
            o.Metallic = 0;
            o.Smoothness = _Glossiness;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
