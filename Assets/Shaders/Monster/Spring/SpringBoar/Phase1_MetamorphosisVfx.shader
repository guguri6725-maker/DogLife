Shader "DogLife/Spring Metamorphosis VFX"
{
 SubShader
 {
  Tags {"Queue"="Transparent" "RenderType"="Transparent"}
  Blend SrcAlpha OneMinusSrcAlpha
  ZWrite Off Cull Off
  Pass
  {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   struct appdata {float4 vertex:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
   struct v2f {float4 vertex:SV_POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
   v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.color=v.color;o.uv=v.uv;return o;}
   fixed4 frag(v2f i):SV_Target
   {
    float alpha=i.uv.x>1.5?1:pow(saturate(1-length(i.uv*2-1)),2);
    return fixed4(i.color.rgb,i.color.a*alpha);
   }
   ENDCG
  }
 }
}
