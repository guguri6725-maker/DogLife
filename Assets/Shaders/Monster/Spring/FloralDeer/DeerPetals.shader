Shader "DogLife/Deer Petals"
{
    Properties { _Color("Tint",Color)=(1,.45,.68,.85) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "Assets/Shaders/SpringPetalSurface.cginc"
            ENDCG
        }
    }
}
