Shader "DogLife/Forest Streaming Stone"
{
    Properties
    {
        _Color("Stone tint", Color) = (0.36,0.4,0.27,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        fixed4 _Color;
        struct Input { float3 worldPos; float3 worldNormal; };
        float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
        float noise(float2 p)
        {
            float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);
            return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);
        }
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float2 p=abs(IN.worldNormal.x)>.5?IN.worldPos.zy:IN.worldPos.xy;
            float2 grid=p/float2(1.8,.85);
            grid.x+=fmod(floor(grid.y),2)*.5;
            float2 cell=frac(grid);
            float edge=min(min(cell.x,1-cell.x),min(cell.y,1-cell.y));
            float stone=smoothstep(.014,.045,edge);
            float grain=noise(p*14)*.08+noise(p*2)*.15;
            float shade=.75+hash(floor(grid))*.3+grain;
            float moss=smoothstep(.55,.8,noise(p*.7+13));
            fixed3 color=lerp(_Color.rgb*shade,float3(.14,.22,.08),moss*.55);
            o.Albedo=lerp(_Color.rgb*.28,color,stone);
            o.Smoothness=.035;o.Occlusion=lerp(.5,1,stone);
        }
        ENDCG
    }
    FallBack "Diffuse"
}
