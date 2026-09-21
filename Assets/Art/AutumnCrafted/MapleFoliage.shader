Shader "DogLife/Autumn Crafted Foliage"
{
    Properties
    {
        _MainTex("Veined maple leaf",2D)="white"{}
        _Color("Season color",Color)=(1,1,1,1)
        _Cutoff("Leaf silhouette",Range(0,1))=.4
        _TintStrength("Season tint",Range(0,1))=.7
    }
    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
        Cull Off
        LOD 200
        CGPROGRAM
        #pragma surface surf Standard alphatest:_Cutoff addshadow fullforwardshadows
        #pragma target 3.0
        sampler2D _MainTex;fixed4 _Color;half _TintStrength;
        struct Input{float2 uv_MainTex;};
        void surf(Input IN,inout SurfaceOutputStandard o)
        {
            fixed4 tex=tex2D(_MainTex,IN.uv_MainTex);
            half value=dot(tex.rgb,half3(.3,.59,.11));
            o.Albedo=lerp(tex.rgb, value*_Color.rgb*1.65,_TintStrength);
            o.Alpha=tex.a;o.Metallic=0;o.Smoothness=.14;
            o.Emission=o.Albedo*.065;
        }
        ENDCG
    }
    FallBack "Transparent/Cutout/VertexLit"
}
