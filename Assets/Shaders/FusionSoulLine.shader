Shader "DogLife/FusionSoulLine"
{
 Properties{_MainTex("Particle",2D)="white"{}}
 SubShader{Tags{"Queue"="Transparent" "RenderType"="Transparent"} Blend SrcAlpha One ZWrite Off Cull Off
 Pass{CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 struct A{float4 vertex:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
 struct V{float4 vertex:SV_POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
 V vert(A a){V o;o.vertex=UnityObjectToClipPos(a.vertex);o.color=a.color;o.uv=a.uv;return o;}
 fixed4 frag(V i):SV_Target{float soft=pow(saturate(1-abs(i.uv.y*2-1)),2);return fixed4(i.color.rgb,i.color.a*soft);}
 ENDCG}}
}
