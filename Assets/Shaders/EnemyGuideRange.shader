Shader "DogLife/EnemyGuideRange"
{
    Properties
    {
        _Color("Warning color",Color)=(1,.65,.18,.65)
        _Progress("Warning fill",Range(0,1))=0
        _Hole("Inner safe radius",Range(0,.9))=0
        _Rect("Projectile lane",Float)=0
        _Slippery("Persistent ice field",Float)=0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Color;
            float _Progress,_Hole,_Rect,_Slippery;
            fixed4 frag(v2f_img i):SV_Target
            {
                float2 p=i.uv*2-1;
                float d=_Rect>.5 ? max(abs(p.x),abs(p.y)) : length(p);
                clip(.98-d);
                // 점선은 예정 범위, 옅게 차오르는 서리 면은 지속되는 빙판을 나타냅니다.
                if (_Slippery > .5)
                {
                    float angle=atan2(p.y,p.x);
                    float dashed=step(.3,frac(angle*5.73));
                    float rim=smoothstep(.92,.96,d)*dashed;
                    float ice=(1-smoothstep(max(0,_Progress-.04),max(.001,_Progress),d))*step(.001,_Progress);
                    float frost=pow(abs(sin(p.x*21+p.y*9)*sin(p.y*18-p.x*7)),8);
                    return fixed4(_Color.rgb,_Color.a*(rim*.72+ice*(.2+frost*.3)));
                }
                if(_Hole>0)clip(d-_Hole);
                float edge=smoothstep(.92,.97,d);
                if(_Hole>0)edge=max(edge,1-smoothstep(_Hole,_Hole+.04,d));
                float fill=_Rect>.5 ? step(i.uv.y,_Progress) : step(d,lerp(_Hole,1,_Progress));
                return fixed4(_Color.rgb,_Color.a*(.09+.78*edge+.22*fill));
            }
            ENDCG
        }
    }
}
