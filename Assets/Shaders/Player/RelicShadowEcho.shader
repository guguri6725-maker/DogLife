Shader "DogLife/Relic Shadow Echo"
{
 Properties { _Color ("Tint", Color) = (.015,.018,.025,.65) }
 SubShader {
 Tags { "Queue"="Transparent" "RenderType"="Transparent" }
 Blend SrcAlpha OneMinusSrcAlpha
 ZWrite Off
 Cull Back
 Pass {
 CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 fixed4 _Color;
 struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; };
 struct v2f { float4 pos:SV_POSITION; float3 normal:TEXCOORD0; float3 view:TEXCOORD1; };
 v2f vert(appdata v) { v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.normal=UnityObjectToWorldNormal(v.normal); o.view=UnityWorldSpaceViewDir(mul(unity_ObjectToWorld,v.vertex).xyz); return o; }
 fixed4 frag(v2f i):SV_Target { float rim=pow(1-saturate(dot(normalize(i.normal),normalize(i.view))),2); return fixed4(_Color.rgb+rim*.035,_Color.a*lerp(.72,1,rim)); }
 ENDCG
 }
 }
}
