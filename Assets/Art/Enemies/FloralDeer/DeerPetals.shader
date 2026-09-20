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
            #include "UnityCG.cginc"
            struct Input { float4 vertex:POSITION; fixed4 color:COLOR; };
            struct Output { float4 vertex:SV_POSITION; fixed4 color:COLOR; };
            fixed4 _Color;
            Output vert(Input v){Output o;o.vertex=UnityObjectToClipPos(v.vertex);o.color=_Color;o.color.a*=saturate(v.color.a);return o;}
            fixed4 frag(Output i):SV_Target{return i.color;}
            ENDCG
        }
    }
}
