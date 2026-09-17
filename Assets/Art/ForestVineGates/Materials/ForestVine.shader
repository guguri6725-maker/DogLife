Shader "DogLife/Forest Vine"
{
    Properties { _Color ("Tint", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        fixed4 _Color;
        struct Input { fixed4 color : COLOR; };
        // Blender에서 칠한 나무껍질과 잎 색을 하나의 재질로 표시합니다.
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            o.Albedo = IN.color.rgb * _Color.rgb;
            o.Metallic = 0;
            o.Smoothness = .12;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
