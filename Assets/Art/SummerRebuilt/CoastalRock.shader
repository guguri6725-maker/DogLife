Shader "DogLife/Coastal Rock Detail"
{
    Properties
    {
        _MainTex("Original atlas",2D)="white"{}
        _StoneTex("Detailed coastal stone",2D)="gray"{}
        _StoneScale("Stone tiles per metre",Float)=.32
        _Color("Tint",Color)=(1,1,1,1)
        _StoneOnly("Stone only",Float)=0
    }
    SubShader
    {
        Tags {"RenderType"="Opaque"}
        Cull Off
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        sampler2D _MainTex,_StoneTex;
        float _StoneScale,_StoneOnly;
        fixed4 _Color;
        struct Input {float2 uv_MainTex;float3 worldPos;float3 worldNormal;};
        void surf(Input IN,inout SurfaceOutputStandard o)
        {
            fixed3 atlas=tex2D(_MainTex,IN.uv_MainTex).rgb;
            // 돌은 월드 좌표에서 세 방향으로 투영하여 긴 벽과 윗면의 늘어짐을 없앤다.
            float3 weights=pow(abs(IN.worldNormal),4);weights/=max(dot(weights,1),.001);
            float3 p=IN.worldPos*_StoneScale;
            fixed3 stone=tex2D(_StoneTex,p.zy).rgb*weights.x+tex2D(_StoneTex,p.xz).rgb*weights.y+tex2D(_StoneTex,p.xy).rgb*weights.z;
            float mask=max(_StoneOnly,step(IN.uv_MainTex.x,.5)*step(.5,IN.uv_MainTex.y));
            o.Albedo=lerp(atlas,stone,mask)*_Color.rgb;
            o.Smoothness=.16;
            o.Alpha=1;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
