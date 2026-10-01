Shader "DogLife/Synth Weapon Aura"
{
    Properties
    {
        [HDR] _AuraColor("Aura", Color) = (0.1,0.55,1.5,1)
        _Thickness("Shell thickness", Range(0,0.08)) = 0.012
    }
    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Back
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _AuraColor;
            float _Thickness;
            struct v2f { float4 pos:SV_POSITION; float3 world:TEXCOORD0; float3 normal:TEXCOORD1; float3 local:TEXCOORD2; };
            v2f vert(appdata_base v)
            {
                v2f o;
                // 원본 표면을 조금 감싸는 껍질에 흐르는 빛을 표시합니다.
                v.vertex.xyz += v.normal * _Thickness;
                o.pos=UnityObjectToClipPos(v.vertex);
                o.world=mul(unity_ObjectToWorld,v.vertex).xyz;
                o.normal=UnityObjectToWorldNormal(v.normal);
                o.local=v.vertex.xyz;
                return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                float rim=pow(1-saturate(abs(dot(normalize(i.normal),normalize(_WorldSpaceCameraPos-i.world)))),1.8);
                float flow=0.5+0.5*sin(i.local.y*32+i.local.x*17-_Time.y*4);
                float filaments=pow(flow,9)*0.28;
                float pulse=0.8+0.2*sin(_Time.y*2.2);
                return float4(_AuraColor.rgb, saturate((rim*0.7+filaments)*pulse)*_AuraColor.a);
            }
            ENDCG
        }
    }
}
