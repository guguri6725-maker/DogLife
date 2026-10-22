#ifndef DOG_LIFE_SPRING_KARMA
#define DOG_LIFE_SPRING_KARMA
float _SpringMonsterKarma;
float skHash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}

// UV에 고정된 불규칙한 튐·흐름·작은 방울을 겹쳐 피막의 두께까지 계산합니다.
float2 skBlood(float2 uv)
{
 // 전투 거리에서도 읽히도록 큰 자국의 면적을 늘립니다.
 float2 q=uv*6;
 float2 cell=floor(q),local=frac(q);
 float seed=skHash(cell);
 float2 p=local-float2(.42+.15*skHash(cell+13),.52);
 float angle=(skHash(cell+31)-.5)*1.6;
 float2 rotated=float2(p.x*cos(angle)-p.y*sin(angle),p.x*sin(angle)+p.y*cos(angle));
 float2 ellipse=rotated/float2(.23+.045*skHash(cell+7),.16+.04*skHash(cell+8));
 float theta=atan2(ellipse.y,ellipse.x);
 float lobes=1+.17*sin(theta*5+seed*19)+.09*sin(theta*9+seed*31);
 float dist=(length(ellipse)-lobes)*.085;
 // 길이와 굵기가 다른 흐름 자국은 끝에서 작은 방울로 모입니다.
 float fall=.17+.15*skHash(cell+21);
 float t=saturate(-p.y/fall);
 float center=sin(t*4+seed*8)*.012*t;
 float width=lerp(.065,.018,t);
 float trail=max(abs(p.x-center)-width,max(-p.y-fall,p.y));
 dist=min(dist,trail);
 dist=min(dist,length(p-float2(sin(4+seed*8)*.012,-fall))-(.025+.015*seed));
 // 같은 크기의 점을 반복하지 않고 주변에 서로 다른 작은 튐을 배치합니다.
 [unroll] for(int i=0;i<3;i++)
 {
  float h=skHash(cell+float2(i*17+53,i*11+9));
  float a=h*6.283185;
  float2 offset=float2(cos(a),sin(a))*(.20+.10*skHash(cell+i+63));
  float radius=.016+.025*skHash(cell+i+81);
  dist=min(dist,length(p-offset)-radius);
 }
 float aa=max(fwidth(dist),.0015);
 float coverage=(1-smoothstep(-aa,aa,dist))*step(.42,seed);
 float thickness=saturate(-dist/.045);
 return float2(coverage,thickness);
}
void ApplySpringKarma(float2 uv,float3 viewDir,inout SurfaceOutputStandard o)
{
 float strength=abs(_SpringMonsterKarma);
 if(strength<.001)return;
 float dark=max(0,_SpringMonsterKarma),light=max(0,-_SpringMonsterKarma);
 float3 original=o.Albedo;
 float luminance=dot(original,float3(.2126,.7152,.0722));
 // 그림자 패스에서 시선 벡터가 0이어도 안전하게 정규화합니다.
 float3 view=viewDir*rsqrt(max(dot(viewDir,viewDir),.0001));
 float3 normal=o.Normal*rsqrt(max(dot(o.Normal,o.Normal),.0001));
 float facing=saturate(dot(view,normal));
 float rim=pow(1-facing,3.5);

 // 가장자리의 얇은 피막과 안쪽의 짙고 젖은 피를 구분해 평평한 붉은 페인트를 피합니다.
 float2 bloodShape=skBlood(uv);
 float blood=bloodShape.x*dark;
 float thickness=bloodShape.y;
 float3 thinFilm=original*float3(.55,.10,.12)+float3(.14,.004,.006);
 float3 pooled=lerp(float3(.36,.008,.018),float3(.55,.016,.028),saturate(luminance));
 float3 red=lerp(thinFilm,pooled,smoothstep(0,.65,thickness));
 o.Albedo=lerp(original,red,blood*.96);
 o.Smoothness=lerp(o.Smoothness,lerp(.42,.88,smoothstep(.05,.65,thickness)),blood);
 // 극단에서도 본래 무늬는 유지하고 윤곽에만 아주 약한 붉은 기운을 남깁니다.
 o.Emission+=float3(.22,.006,.012)*rim*dark*.22;

 // 푸른 쪽: 얼룩·균열을 전혀 사용하지 않고 깨끗한 청백색 진주광을 입힙니다.
 float pale=smoothstep(.18,.72,luminance);
 float3 pearl=original*float3(.92,1.015,1.06)+float3(.015,.028,.035)*pale;
 o.Albedo=lerp(o.Albedo,pearl,light*.65);
 o.Smoothness=lerp(o.Smoothness,max(o.Smoothness,.38),light*pale*.5);
 float breath=.94+.06*sin(_Time.y*1.15);
 o.Emission+=light*breath*(float3(.15,.52,.86)*rim*.8+float3(.025,.065,.10)*pale);
}
#endif
