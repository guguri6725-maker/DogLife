Shader "DogLife/Forest3D Ground"
{
    Properties
    {
        _Moss ("Moss", Color) = (0.13,0.20,0.065,1)
        _Soil ("Forest trail", Color) = (0.32,0.25,0.14,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        struct Input { float3 worldPos; };
        fixed4 _Moss, _Soil;
        // 월드 좌표의 작은 색 변화로 반복 타일과 사각형 경계를 줄입니다.
        float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
        float noise(float2 p)
        {
            float2 i=floor(p),f=frac(p); f=f*f*(3-2*f);
            return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);
        }
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float2 p=IN.worldPos.xz;
            float n=noise(p*0.7)*0.6+noise(p*2.6)*0.3+noise(p*12)*0.1;
            // 여섯 공터를 굽은 숲길로 연결하고 마지막은 보스 공간으로 넓힙니다.
            float d=abs(p.y+2.8*sin(p.x/18.0))-3.1;
            for(int i=0;i<7;i++)
            {
                float x=i<6?11+i*23:155;
                d=min(d,distance(p,float2(x,-2.8*sin(x/18.0)))-(i<6?6.3:8.0));
            }
            float trail=1-smoothstep(-0.6,0.9,d+(n-0.5)*1.8);
            o.Albedo=lerp(_Moss.rgb,_Soil.rgb,trail)*(0.75+n*0.5);
            o.Metallic=0; o.Smoothness=0.04; o.Occlusion=0.92;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
