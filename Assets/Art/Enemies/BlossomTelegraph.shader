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
            struct Output { float4 vertex:SV_POSITION; fixed4 color:COLOR; };
            Output vert(Input v){Output o;o.vertex=UnityObjectToClipPos(v.vertex);o.color=v.color;return o;}
            fixed4 frag(Output i):SV_Target{return i.color;}
            ENDCG
        }
    }
}
