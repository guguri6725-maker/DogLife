Shader "DogLife/Memory Soul" {
 Properties { _MainTex("Soft light",2D)="white" {} _TintColor("Tint",Color)=(1,1,1,1) }
 SubShader { Tags { "Queue"="Transparent" "RenderType"="Transparent" } Blend SrcAlpha One Cull Off ZWrite Off
 Pass { CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 sampler2D _MainTex; fixed4 _TintColor;
 struct appdata { float4 vertex:POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; };
 struct v2f { float4 pos:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; };
 v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.color=v.color*_TintColor;o.uv=v.uv;return o;}
 half4 frag(v2f i):SV_Target{half4 c=tex2D(_MainTex,i.uv)*i.color;c.rgb*=2.5;return c;}
 ENDCG }
 }
}
