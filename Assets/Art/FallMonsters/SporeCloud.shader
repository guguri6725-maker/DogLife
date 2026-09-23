Shader "DogLife/Fall Spore Cloud"
{
 Properties { _Color("Tint",Color)=(0.5,0.8,0.12,0.35) }
 SubShader {
  Tags { "Queue"="Transparent" "RenderType"="Transparent" }
  Blend SrcAlpha OneMinusSrcAlpha
  ZWrite Off Cull Off
  Pass {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   fixed4 _Color;
   struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
   struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
   v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o;}
   fixed4 frag(v2f i):SV_Target {
    // 가장자리가 사라지는 부드러운 포자 입자이며 충돌이나 피해를 처리하지 않습니다.
    float r=length(i.uv*2-1);fixed4 c=_Color*i.color;c.a*=pow(saturate(1-r),2);return c;
   }
   ENDCG
  }
 }
}
