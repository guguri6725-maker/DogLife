using System.Collections.Generic;
using UnityEngine;
namespace DogLife
{
    [RequireComponent(typeof(MeshFilter),typeof(MeshRenderer))]
    public sealed class SpringBoarVfx:MonoBehaviour
    {
        public SpringBoarBoss boss;
        public SpringBoarPhaseTwo phaseTwo;
        public SpringBoarDeath death;
        [SerializeField] private Material material;
        [Header("꽃가루 / 흙먼지 디테일")]
        [SerializeField,Range(8,48)] int pollenCount=28;
        [SerializeField,Range(0,1)] float dustOpacity=.55f;
        readonly List<Vector4> uv=new List<Vector4>(16000);
        float VisualScale=>1;
        Mesh mesh;MeshRenderer surface;CombatHealth health;
        readonly List<Vector3> vertices=new List<Vector3>(12000);
        readonly List<Color> colors=new List<Color>(12000);
        readonly List<int> indices=new List<int>(18000);
        [Header("봄맵 벚꽃 색감")]
        [SerializeField] private Color Ivory=new Color(.97f,.92f,.82f),Blush=new Color(.91f,.73f,.77f),Deep=new Color(.43f,.34f,.31f);
        [SerializeField,Range(0,1)] private float petalOpacity=.65f;
        Transform view;
        readonly RaycastHit[] chargeWalls=new RaycastHit[32];
        bool wasChargeStunned;
        float chargeImpactAt=-10;
        static readonly Vector2[] PetalOutline={new Vector2(0,0),new Vector2(-.24f,.20f),new Vector2(-.38f,.48f),new Vector2(-.35f,.73f),new Vector2(-.20f,.94f),new Vector2(-.07f,1),new Vector2(0,.88f),new Vector2(.07f,1),new Vector2(.20f,.94f),new Vector2(.35f,.73f),new Vector2(.38f,.48f),new Vector2(.24f,.20f)};
        public void Initialize(Material mat){material=mat;if(surface)surface.sharedMaterial=mat;}
        void Awake(){mesh=new Mesh{name="Spring sakura petals and bone warning",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.MarkDynamic();GetComponent<MeshFilter>().sharedMesh=mesh;surface=GetComponent<MeshRenderer>();surface.sharedMaterial=material;surface.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;surface.receiveShadows=false;if(boss)health=boss.GetComponent<CombatHealth>();}
        static Color A(Color color,float alpha){color.a=alpha;return color;}
        void Tri(Vector3 a,Vector3 b,Vector3 c,Color color){int n=vertices.Count;vertices.Add(transform.InverseTransformPoint(a));vertices.Add(transform.InverseTransformPoint(b));vertices.Add(transform.InverseTransformPoint(c));colors.Add(color);colors.Add(color);colors.Add(color);uv.Add(Vector4.zero);uv.Add(Vector4.zero);uv.Add(Vector4.zero);indices.Add(n);indices.Add(n+1);indices.Add(n+2);}
        void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color){Tri(a,b,c,color);Tri(a,c,d,color);}
        void Line(Vector3 a,Vector3 b,float width,Color color){Vector3 s=Vector3.Cross(b-a,Vector3.up).normalized*width*.5f;if(s.sqrMagnitude<.000001f)s=Vector3.right*width*.5f;Quad(a-s,a+s,b+s,b-s,color);}
        // 테이퍼를 가진 굽은 뿌리: 여러 마디를 연결해 각진 결정 형태를 없앱니다.
        void Crystal(Vector3 p,float height,float width,float alpha,float seed)
        {
            if(height<.01f)return;
            const int levels=7,sides=7;
            for(int j=0;j<levels;j++)for(int i=0;i<sides;i++){
                float t=j/(float)levels,u=(j+1f)/levels,a=i*Mathf.PI*2/sides,b=(i+1f)*Mathf.PI*2/sides;
                Vector3 bend=new Vector3(Mathf.Sin(seed),0,Mathf.Cos(seed));
                Vector3 c=p+Vector3.up*height*t+bend*height*.24f*t*t,d=p+Vector3.up*height*u+bend*height*.24f*u*u;
                float r=width*Mathf.Pow(1-t, .8f),r2=width*Mathf.Pow(1-u,.8f);
                Color tint=A(Color.Lerp(new Color(.24f,.16f,.105f),Ivory,t*.72f),alpha);
                tint*=new Color(.8f+.2f*Mathf.Cos(a-1),.8f+.2f*Mathf.Cos(a-1),.8f+.2f*Mathf.Cos(a-1),1);
                Surface(c+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*r,c+new Vector3(Mathf.Cos(b),0,Mathf.Sin(b))*r,d+new Vector3(Mathf.Cos(b),0,Mathf.Sin(b))*r2,d+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*r2,tint,5,seed);
            }
        }
        void Surface(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color,float mode,float seed)
        {
            int n=uv.Count;Quad(a,b,c,d,color);
            uv[n]=new Vector4(0,0,mode,seed);uv[n+1]=new Vector4(1,0,mode,seed);uv[n+2]=new Vector4(1,1,mode,seed);
            uv[n+3]=uv[n];uv[n+4]=uv[n+2];uv[n+5]=new Vector4(0,1,mode,seed);
        }
        void Atmosphere(Vector3 p,float radius,float strength)
        {
            Vector3 x=view?view.right:Vector3.right,y=view?view.up:Vector3.up;
            for(int i=0;i<pollenCount;i++){
                float t=Mathf.Repeat(Time.time*.22f+i*.618f,1),a=i*2.39996f+Time.time*.16f;
                Vector3 q=p+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*radius*Mathf.Sqrt(Mathf.Repeat(i*.381f,1))+Vector3.up*(.2f+t*2.3f);
                float size=.025f+(i%3)*.012f;
                Surface(q-x*size-y*size,q+x*size-y*size,q+x*size+y*size,q-x*size+y*size,A(Ivory,Mathf.Sin(t*Mathf.PI)*strength),2,i);
            }
            for(int i=0;i<10;i++){
                float a=i*2.39996f;Vector3 q=p+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*radius*.65f+Vector3.up*.28f;
                float w=Mathf.Min(1.7f,radius*.6f);
                Surface(q-x*w-y*.55f,q+x*w-y*.55f,q+x*w+y*.55f,q-x*w+y*.55f,A(new Color(.61f,.51f,.40f),dustOpacity*strength),1,i*2.7f);
            }
        }
        Vector3 Dir(Vector3 forward,float degrees)=>Quaternion.AngleAxis(degrees,Vector3.up)*forward;
        void Arc(Vector3 p,Vector3 forward,float radius,float angle,float width,Color color)
        {
            int count=Mathf.Max(16,Mathf.CeilToInt(angle/4));
            for(int i=0;i<count;i++){Vector3 a=Dir(forward,-angle*.5f+angle*i/count),b=Dir(forward,-angle*.5f+angle*(i+1)/count);Quad(p+a*radius,p+b*radius,p+b*(radius-width),p+a*(radius-width),color);}
        }
        void Sakura(Vector3 p,float size,float alpha,float spin)
        { Blossom(p,Vector3.right,Vector3.forward,size,alpha,spin); }
        void Blossom(Vector3 p,Vector3 axisX,Vector3 axisY,float size,float alpha,float spin)
        {
            // Five rounded petals with small tip clefts form a complete cherry blossom.
            for(int petal=0;petal<5;petal++){
                float a=(spin+petal*72)*Mathf.Deg2Rad;
                Vector3 along=(axisX*Mathf.Sin(a)+axisY*Mathf.Cos(a))*size;
                Vector3 across=(axisX*Mathf.Cos(a)-axisY*Mathf.Sin(a))*size;
                Vector3 normal=Vector3.Cross(axisX,axisY).normalized;
                Vector3 center=p+along*.48f+normal*size*.13f;
                for(int j=0;j<PetalOutline.Length;j++){
                    Vector2 v=PetalOutline[j],w=PetalOutline[(j+1)%PetalOutline.Length];
                    int n=uv.Count;
                    Tri(center,p+across*v.x+along*v.y,p+across*w.x+along*w.y,A(Color.Lerp(Blush,Ivory,.25f+.35f*v.y),alpha));
                    uv[n]=new Vector4(.5f,.48f,3,petal);uv[n+1]=new Vector4(v.x+.5f,v.y,3,petal);uv[n+2]=new Vector4(w.x+.5f,w.y,3,petal);
                }
                Vector3 stamen=p+along*.18f;Vector3 dotX=axisX*size*.025f,dotY=axisY*size*.025f;
                Quad(stamen-dotX,stamen+dotY,stamen+dotX,stamen-dotY,A(Ivory,alpha));
            }
        }
        void Warning(Vector3 p,Vector3 forward,float radius,float angle,float progress)
        {
            p+=Vector3.up*.10f;int segments=48;
            for(int i=0;i<segments;i++)Tri(p,p+Dir(forward,-angle/2+angle*i/segments)*radius,p+Dir(forward,-angle/2+angle*(i+1)/segments)*radius,new Color(1,.30f,.36f,.24f));
            // 빨강 바탕 위로 기존 벚꽃색이 차오릅니다. 같은 외곽과 진행도를 사용합니다.
            var filledCenter=p+Vector3.up*.009f;
            for(int i=0;i<segments;i++)Tri(filledCenter,filledCenter+Dir(forward,-angle/2+angle*i/segments)*radius*progress,filledCenter+Dir(forward,-angle/2+angle*(i+1)/segments)*radius*progress,A(Blush,.42f));
            Arc(p,forward,radius,angle,.11f,A(Deep,.55f));Arc(p+Vector3.up*.005f,forward,radius-.025f,angle,.045f,A(Ivory,.82f));
            Arc(p+Vector3.up*.009f,forward,radius*Mathf.Lerp(.2f,.98f,progress),angle,.055f,A(Blush,.65f));
            if(angle<359){Line(p,p+Dir(forward,-angle/2)*radius,.055f,A(Ivory,.9f));Line(p,p+Dir(forward,angle/2)*radius,.055f,A(Ivory,.9f));}
            for(int i=0;i<8;i++){float a=-angle/2+angle*(i+.5f)/8;Vector3 d=Dir(forward,a);Line(p+d*(radius-.2f),p+d*(radius-.4f),.035f,A(Blush,.8f));}
            Sakura(p+forward*radius*.48f,.42f+.1f*progress,.7f,0);
            // 피격 영역 안에서만 생명력 무늬와 채워지는 타이밍 띠를 그립니다.
            for(int i=0;i<segments;i++){
                Vector3 a=Dir(forward,-angle/2+angle*i/segments),b=Dir(forward,-angle/2+angle*(i+1)/segments);
                float fill=radius*Mathf.Clamp01(progress);
                Quad(p+a*Mathf.Max(0,fill-.32f),p+a*fill,p+b*fill,p+b*Mathf.Max(0,fill-.32f),A(Ivory,.28f));
            }
        }
        void Cracks(Vector3 p,Vector3 forward,float range,float angle,float growth,float alpha)
        {
            for(int lane=0;lane<5;lane++)
            {
                Vector3 direction=Dir(forward,(lane-2)*angle*.21f),side=Vector3.Cross(Vector3.up,direction);Vector3 prev=p;
                for(int j=1;j<=10;j++)
                {
                    float distance=range*j/10;if(distance>range*growth)break;
                    Vector3 next=p+direction*distance+side*Mathf.Sin(j*7+lane*3)*.12f;
                    Line(prev,next,.10f,A(Deep,alpha));Line(prev+Vector3.up*.008f,next+Vector3.up*.008f,.036f,A(Ivory,alpha));
                    Vector3 branch=next+Dir(direction,(j%2==0?1:-1)*55)*.35f;Line(next,branch,.028f,A(Blush,alpha));prev=next;
                }
            }
        }
        void Shards(Vector3 p,float t,float size)
        {
            float a=Mathf.Sin(Mathf.Clamp01(t)*Mathf.PI);
            for(int i=0;i<28;i++){float angle=i*2.39996f;Vector3 d=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));Vector3 q=p+d*size*t*(.55f+.45f*Mathf.Abs(Mathf.Sin(i*7)))+Vector3.up*a*(.25f+.65f*Mathf.Abs(Mathf.Sin(i*3)));Crystal(q,.10f+.09f*a,.04f,a,i);}
            Arc(p,Vector3.forward,size*t,360,.05f,A(Ivory,a*.8f));
        }
        void Petals(Vector3 origin,float spread,float height,float strength)
        {
            Atmosphere(origin,spread,strength*.75f);
            float time=Time.time;
            Vector3 axisX=view?view.right:Vector3.right,axisY=view?view.up:Vector3.up;
            for(int i=0;i<40;i++){
                float f=Mathf.Repeat(time*(.25f+i%3*.06f)+i*.137f,1);float a=i*2.39996f+time*.4f;
                Vector3 p=origin+new Vector3(Mathf.Cos(a)*spread*f,height*f,Mathf.Sin(a)*spread*f);
                float fade=Mathf.Clamp01(f*8)*(1-f)*strength*petalOpacity;
                // 공중에서는 완성된 꽃 대신 접힌 낱장 꽃잎이 회전하며 흩날립니다.
                float angle=(i*29+time*(i%2==0?54:-38))*Mathf.Deg2Rad,size=.45f+i%3*.08f;
                Vector3 along=(axisX*Mathf.Sin(angle)+axisY*Mathf.Cos(angle))*size;
                Vector3 across=(axisX*Mathf.Cos(angle)-axisY*Mathf.Sin(angle))*size;
                Vector3 center=p+along*.45f+Vector3.Cross(axisX,axisY)*size*.18f;
                for(int j=0;j<PetalOutline.Length;j++){
                    Vector2 v=PetalOutline[j],w=PetalOutline[(j+1)%PetalOutline.Length];int n=uv.Count;
                    Tri(center,p+across*v.x+along*v.y,p+across*w.x+along*w.y,A(Color.Lerp(Blush,Ivory,v.y*.6f),fade));
                    uv[n]=new Vector4(.5f,.45f,3,i);uv[n+1]=new Vector4(v.x+.5f,v.y,3,i);uv[n+2]=new Vector4(w.x+.5f,w.y,3,i);
                }
            }
        }
        void SoftWave(Vector3 p,float radius,float width,float alpha)
        {
            for(int i=0;i<72;i++){
                float a=i*5*Mathf.Deg2Rad,b=(i+1)*5*Mathf.Deg2Rad;
                Vector3 d=new Vector3(Mathf.Sin(a),0,Mathf.Cos(a)),e=new Vector3(Mathf.Sin(b),0,Mathf.Cos(b));
                int n=uv.Count;
                Surface(p+d*Mathf.Max(0,radius-width),p+e*Mathf.Max(0,radius-width),p+e*radius,p+d*radius,A(Blush,alpha),4,0);
                for(int k=n;k<uv.Count;k++){Vector4 t=uv[k];t.x=(i+t.x)/72f;uv[k]=t;}
            }
        }
        void ImpactCrown(Vector3 p,float radius,float growth,float fade)
        {
            // 충격파 앞에 낮은 흙먼지와 꽃잎 테두리를 겹쳐 바닥에서 솟는 무게감을 줍니다.
            Vector3 x=view?view.right:Vector3.right,y=view?view.up:Vector3.up;
            for(int i=0;i<24;i++){
                float angle=i*15*Mathf.Deg2Rad;Vector3 d=new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle));
                Vector3 q=p+d*radius*growth+Vector3.up*(.25f+.25f*Mathf.Sin(i*7));
                float w=1.0f+(i%3)*.20f,h=.65f+(i%4)*.15f;
                Surface(q-x*w-y*h,q+x*w-y*h,q+x*w+y*h,q-x*w+y*h,A(new Color(.66f,.52f,.42f),fade*.82f),1,i*2.9f);
                Vector3 tip=q+d*.45f+Vector3.up*.1f;
                Line(q,tip,.025f,A(Ivory,fade*.7f));
            }
        }
        void BoneSpike(Vector3 p,float grow,float fade,int seed)
        {
            float height=3.8f*grow;Crystal(p,height,.40f,fade,seed);
            Vector3 bend=new Vector3(Mathf.Sin(seed)*.35f,height*.75f,Mathf.Cos(seed)*.35f);
            Crystal(p+bend,height*.4f,.18f,fade,seed+2);
            for(int j=0;j<8;j++){
                Vector3 q=p+new Vector3(Mathf.Sin(j*1.2f+seed)*.4f,height*j/9,Mathf.Cos(j*1.2f+seed)*.4f);
                Sakura(q,.18f,fade,j*31);
            }
            Arc(p+Vector3.up*.07f,Vector3.forward,boss.SpikeRadius,360,.09f,A(Blush,fade*.65f));
        }
        // 뿔이 벽 안쪽을 향해도 이펙트 중심은 벽의 보스 쪽 면에 둡니다.
        Vector3 ChargeVisualPoint(Vector3 origin,Vector3 forward,float length)
        {
            float clear=length;
            int count=Physics.RaycastNonAlloc(origin+Vector3.up*1.2f,forward,chargeWalls,length+.3f,~0,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++)
            {
                var hit=chargeWalls[i];
                if(hit.collider.GetComponentInParent<CombatHealth>()||Mathf.Abs(hit.normal.y)>.7f)continue;
                clear=Mathf.Min(clear,Mathf.Max(0,hit.distance-.35f));
            }
            return origin+forward*clear;
        }
        void ChargeWindup(Vector3 origin,Vector3 forward,float progress)
        {
            // 길이가 0인 돌진도 준비 동작을 읽을 수 있게 몸 양옆에 꽃가루를 모읍니다.
            var right=Vector3.Cross(Vector3.up,forward);
            float alpha=.35f+.55f*progress;
            for(int side=-1;side<=1;side+=2)
            {
                var shoulder=origin+right*side*2.7f+Vector3.up*(1.5f+progress*.8f);
                Petals(shoulder, .65f,1.2f,alpha);
                Sakura(shoulder,.28f+progress*.18f,alpha,Time.time*90);
            }
            Petals(origin-forward+Vector3.up*.3f,2,1,.3f+progress*.4f);
        }
        void LateUpdate()
        {
            if(!view&&Camera.main)view=Camera.main.transform;
            vertices.Clear();colors.Clear();indices.Clear();uv.Clear();
            if(boss&&(boss.isActiveAndEnabled||MonsterAnimationPreview.IsPreviewing(boss))&&health&&!health.Dead){
                var state=boss.State;var pattern=boss.AttackPattern;float t=boss.Progress;
                Vector3 p=boss.transform.position+Vector3.up*.12f,forward=boss.Forward,right=Vector3.Cross(Vector3.up,forward);
                if(state==SpringBoarBoss.Phase.Warning){
                    if(pattern==SpringBoarBoss.Pattern.Charge){
                        ChargeWindup(p,forward,t);
                        Vector3 front=ChargeVisualPoint(p,forward,boss.HornOffset),end=ChargeVisualPoint(p,forward,boss.HornOffset+boss.ChargeLength);float w=boss.ChargeWidth;
                        Quad(front-right*w,front+right*w,end+right*w,end-right*w,new Color(1,.30f,.36f,.24f));
                        Vector3 filledEnd=Vector3.Lerp(front,end,t),lift=Vector3.up*.009f;
                        Quad(front-right*w+lift,front+right*w+lift,filledEnd+right*w+lift,filledEnd-right*w+lift,A(Blush,.42f));
                        Line(front-right*w,end-right*w,.12f,A(Blush,.70f));Line(front+right*w,end+right*w,.12f,A(Blush,.70f));
                        for(int j=1;j<10;j++){Vector3 q=Vector3.Lerp(front,end,j/10f);Line(q-right*w*.7f,q+forward,.10f,A(Ivory,.8f));Line(q+right*w*.7f,q+forward,.10f,A(Ivory,.8f));}
                        Petals(p+forward*2-right,3,2.5f,1);
                    }else if(pattern==SpringBoarBoss.Pattern.Tusk){
                        Warning(p,forward,boss.TuskRange,boss.TuskAngle,t);
                        for(int sign=-1;sign<=1;sign+=2){Vector3 q=p+forward*3.6f+right*sign*1.3f+Vector3.up*2;
                            Sakura(q,.55f+t*.3f,1,Time.time*60);Crystal(q,.6f*t,.12f,1,sign);}
                        Petals(p+forward*3,2,3,.8f);
                    }else if(pattern==SpringBoarBoss.Pattern.Slam){Warning(p,forward,boss.SlamRadius,360,t);Petals(p,4,5,t);}
                    else{
                        for(int i=0;i<boss.SpikeCount;i++)Warning(boss.SpikePoint(i),forward,boss.SpikeRadius,360,t);
                        Petals(p+Vector3.up*3,4,5,1);
                    }
                }else if(state==SpringBoarBoss.Phase.Attack){
                    if(pattern==SpringBoarBoss.Pattern.Charge){
                        Petals(p,5,4,1);
                        for(int side=-1;side<=1;side+=2)for(int j=0;j<12;j++){
                            Vector3 q=p+right*side*(2.3f+j*.05f)-forward*j*.6f+Vector3.up*(.5f+Mathf.Sin(j*.3f)*.6f);
                            Line(q,q-forward*.8f,.25f,A(j%3==0?Ivory:Blush,1-j/12f));}
                        Shards(ChargeVisualPoint(p,forward,4),Mathf.Repeat(Time.time*3,1),3);
                    }else if(pattern==SpringBoarBoss.Pattern.Tusk){
                        float rise=Mathf.InverseLerp(.20f,.78f,t),alpha=Mathf.Sin(rise*Mathf.PI);
                        for(int j=0;j<30;j++)for(int k=0;k<12;k++){
                            float angle=-boss.TuskAngle/2+boss.TuskAngle*j/30f,a=(rise-k*.015f)*Mathf.PI*.7f,b=a-.04f;if(a<0)continue;
                            Vector3 d=Dir(forward,angle),e=Dir(forward,angle+boss.TuskAngle/30);
                            Surface(p+d*Mathf.Cos(a)*boss.TuskRange+Vector3.up*Mathf.Sin(a)*5,p+e*Mathf.Cos(a)*boss.TuskRange+Vector3.up*Mathf.Sin(a)*5,p+e*Mathf.Cos(b)*boss.TuskRange+Vector3.up*Mathf.Sin(b)*5,p+d*Mathf.Cos(b)*boss.TuskRange+Vector3.up*Mathf.Sin(b)*5,A(k<2?Ivory:Blush,alpha*(1-k/12f)*.30f),4,j*.17f);
                        }Petals(p+forward*3,4,5,alpha);
                    }else if(pattern==SpringBoarBoss.Pattern.Slam&&t>=.30f){
                        float grow=boss.SlamWaveProgress,alpha=1-Mathf.InverseLerp(.72f,1,t);
                        Arc(p,forward,boss.SlamRadius*grow,360,.30f,A(Blush,alpha*.65f));Arc(p+Vector3.up*.06f,forward,boss.SlamRadius*grow,360,.12f,A(Ivory,alpha));
                        SoftWave(p+Vector3.up*.035f,boss.SlamRadius*grow,SpringBoarBoss.SlamWaveWidth,alpha*.72f);ImpactCrown(p,boss.SlamRadius,grow,alpha);Cracks(p,forward,boss.SlamRadius,360,grow,alpha);Shards(p,Mathf.InverseLerp(.30f,1,t),boss.SlamRadius);Petals(p,boss.SlamRadius,4,alpha);
                    }else if(pattern==SpringBoarBoss.Pattern.Spikes){
                        float grow=Mathf.SmoothStep(0,1,Mathf.Clamp01(t/.12f)),fade=1-Mathf.InverseLerp(.8f,1,t);
                        for(int i=0;i<boss.SpikeCount;i++){BoneSpike(boss.SpikePoint(i),grow,fade,i);Petals(boss.SpikePoint(i),1.4f,4,fade);}
                    }
                }else if(state==SpringBoarBoss.Phase.Stunned){
                    if(t<.3f)Shards(ChargeVisualPoint(p,forward,3),t/.3f,5);
                    for(int i=0;i<5;i++){Vector3 q=p+forward*3+Vector3.up*5+Dir(forward,Time.time*90+i*72)*1.5f;Sakura(q,.38f,1,i*20);}
                    Petals(p+forward*3,2,2,.5f);
                }
            }
            if(phaseTwo&&phaseTwo.Active)DrawPhaseTwo();
            if(death&&death.Dying)
            {
                float t=death.Progress;
                var contact=death.Contact;contact.y=death.transform.position.y+.12f;
                if(t>.18f&&t<.4f)Atmosphere(contact+death.transform.forward*2,1,Mathf.Sin(Mathf.InverseLerp(.18f,.4f,t)*Mathf.PI)*.4f);
                if(t>.65f){float p=Mathf.InverseLerp(.65f,1,t);ImpactCrown(contact,4,p,Mathf.Sin(p*Mathf.PI)*.65f);Petals(contact,4,2,(1-p)*.55f);}
            }
            mesh.Clear();mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();
        }
        void DrawPhaseTwo()
        {
            foreach(var h in phaseTwo.Hazards)
            {
                var p=h.origin+Vector3.up*.11f;float t=h.Progress;
                if(h.effect==SpringBoarPhaseTwo.Effect.Roots&&!phaseTwo.Special&&phaseTwo.RibAnchor)
                {
                    float stretch=h.age<h.warning?t*.18f:Mathf.Sin(h.AttackProgress*Mathf.PI);
                    var root=phaseTwo.RibAnchor.position+h.forward*.8f;
                    var tip=Vector3.Lerp(root+h.forward*1.2f,p+h.forward*h.range,stretch);
                    RibBranch(root,tip,.22f,Mathf.Clamp01(stretch*6));
                }
                if(h.age<h.warning)
                {
                    if(h.effect==SpringBoarPhaseTwo.Effect.Charge)
                    {
                        var body=phaseTwo.transform.position+Vector3.up*.11f;
                        ChargeWindup(body,h.forward,t);
                        p=ChargeVisualPoint(body,h.forward,Vector3.Distance(body,h.origin));
                    }
                    if(h.shape==SpringBoarPhaseTwo.Shape.Lane)
                    {
                        var side=Vector3.Cross(Vector3.up,h.forward)*h.width;var end=p+h.forward*h.range;
                        if(h.effect==SpringBoarPhaseTwo.Effect.Charge)end=ChargeVisualPoint(p,h.forward,h.range);
                        Quad(p-side,p+side,end+side,end-side,new Color(1,.30f,.36f,.24f));
                        var fill=Vector3.Lerp(p,end,t);
                        Quad(p-side+Vector3.up*.009f,p+side+Vector3.up*.009f,fill+side+Vector3.up*.009f,fill-side+Vector3.up*.009f,A(Blush,.42f));
                        Line(p-side,end-side,.1f,A(Ivory,.8f));Line(p+side,end+side,.1f,A(Ivory,.8f));
                        for(int j=1;j<8;j++)Sakura(Vector3.Lerp(p,end,j/8f),.24f,.6f,0);
                    }
                    else
                    {
                        float angle=h.shape==SpringBoarPhaseTwo.Shape.Circle?360:h.angle;
                        Warning(p,h.forward,h.range,angle,t);
                        // 공통 Warning에서 채워 중복 반투명 면을 피합니다.
                        Cracks(p,h.forward,h.range,angle,t,.55f);
                    }
                    if(h.effect==SpringBoarPhaseTwo.Effect.Spike)Sakura(p,.55f+t*.25f,.8f,t*35);
                }
                else
                {
                    float a=h.AttackProgress,fade=1-a;
                    switch(h.effect)
                    {
                        case SpringBoarPhaseTwo.Effect.Spike:
                            float alive=1-Mathf.Clamp01((h.age-h.warning-h.active)/.65f);
                            BoneSpike(p,Mathf.SmoothStep(0,1,Mathf.Min(1,a*4)),alive,h.group);Shards(p,a,h.range);break;
                        case SpringBoarPhaseTwo.Effect.Roots:
                            float grow=Mathf.Clamp01(a/h.propagation);
                            Cracks(p,h.forward,h.range,h.angle,grow,fade);
                            for(int lane=-1;lane<=1;lane++)for(int j=1;j<=2;j++)
                            {float distance=j/2f;var q=p+Dir(h.forward,lane*h.angle*.28f)*(h.range*distance);float rise=Mathf.Clamp01((grow-distance+.2f)/.2f);if(rise>0){BoneSpike(q,rise*(.65f+j*.07f),fade,j+lane+3);RibBranch(p+Vector3.up*.2f,q,.28f,fade*.8f);}}
                            Petals(p+h.forward*h.range*.5f,h.range*.3f,4,fade);break;
                        case SpringBoarPhaseTwo.Effect.Sweep:
                            for(int k=0;k<12;k++)
                            {
                                // 경고 부채꼴 밖으로 잔상이 새지 않도록 판정과 같은 각도를 사용합니다.
                                float angle=Mathf.Clamp(h.SweepAngle-k*(SpringBoarPhaseTwo.Hazard.SweepTrailAngle/11),-h.angle*.5f,h.angle*.5f);
                                Vector3 u=Dir(h.forward,angle),v=Dir(h.forward,Mathf.Min(h.angle*.5f,angle+SpringBoarPhaseTwo.Hazard.SweepTipAngle));
                                Quad(p+u*(h.range-1.3f)+Vector3.up*2,p+u*h.range+Vector3.up*2,p+v*h.range+Vector3.up*2,p+v*(h.range-1.3f)+Vector3.up*2,A(k<2?Ivory:Blush,(1-k/12f)*.65f));
                            }Petals(p+h.forward*4,3,3,fade);break;
                        case SpringBoarPhaseTwo.Effect.Lift:
                            for(int k=0;k<16;k++)for(int sign=-1;sign<=1;sign+=2)
                            {
                                float r=Mathf.Max(0,a-k*.016f)*1.4f,s=r-.08f;
                                var side=Vector3.Cross(Vector3.up,h.forward)*sign;
                                var u=p+h.forward*Mathf.Cos(r)*h.range+Vector3.up*(1+Mathf.Sin(r)*6)+side;
                                var v=p+h.forward*Mathf.Cos(s)*h.range+Vector3.up*(1+Mathf.Sin(s)*6)+side;
                                Quad(u-side*.5f,u+side*.5f,v+side*.5f,v-side*.5f,A(k<2?Ivory:Blush,(1-k/16f)*.65f));
                            }Petals(p+h.forward*4,3,6,fade);break;
                        case SpringBoarPhaseTwo.Effect.Slam:
                            SoftWave(p,h.range*a,SpringBoarPhaseTwo.Hazard.SlamWaveWidth,fade);ImpactCrown(p,h.range,a,fade);Shards(p,a,h.range);Petals(p,h.range,4,fade);break;
                    }
                }
            }
            var center=phaseTwo.transform.position;
            if(phaseTwo.Stunned&&!wasChargeStunned)chargeImpactAt=Time.time;
            wasChargeStunned=phaseTwo.Stunned;
            float impact=(Time.time-chargeImpactAt)/.7f;
            if(phaseTwo.Stunned&&impact>=0&&impact<1)
            {
                var contact=ChargeVisualPoint(center+Vector3.up*.12f,phaseTwo.transform.forward,3);
                Shards(contact,impact,3);Petals(contact,2.5f,3,1-impact);
            }
            if(phaseTwo.Charging){Petals(center,4,3,.85f);Atmosphere(center,4,.8f);}
            if(phaseTwo.Pulse>0)Petals(center+Vector3.up*4,2,1,phaseTwo.Pulse);
            if(phaseTwo.Special)Petals(center+Vector3.up*2,5,5,.7f);
            if(phaseTwo.Stunned)for(int i=0;i<5;i++)Sakura(center+Vector3.up*5+Dir(phaseTwo.transform.forward,Time.time*65+i*72)*1.7f,.4f,.8f,i*20);
        }
        void RibBranch(Vector3 root,Vector3 tip,float width,float alpha)
        {
            for(int j=0;j<12;j++)
            {
                float t=j/12f,u=(j+1)/12f;
                Vector3 a=Vector3.Lerp(root,tip,t)+Vector3.up*Mathf.Sin(t*Mathf.PI)*1.3f,b=Vector3.Lerp(root,tip,u)+Vector3.up*Mathf.Sin(u*Mathf.PI)*1.3f;
                var tangent=(b-a).normalized;var side=Vector3.Cross(tangent,Vector3.up).normalized;var up=Vector3.Cross(side,tangent);
                for(int k=0;k<6;k++)
                {
                    float x=k*Mathf.PI/3,y=(k+1)*Mathf.PI/3;
                    Vector3 p=side*Mathf.Cos(x)+up*Mathf.Sin(x),q=side*Mathf.Cos(y)+up*Mathf.Sin(y);
                    Surface(a+p*width*(1-t),a+q*width*(1-t),b+q*width*(1-u),b+p*width*(1-u),A(Color.Lerp(Deep,Ivory,t),alpha),5,j);
                }
                if(j%3==0)Blossom(a,view?view.right:Vector3.right,view?view.up:Vector3.up,.27f,alpha,j*33);
            }
        }
        void OnDisable(){if(mesh)mesh.Clear();}
        void OnDestroy(){if(mesh)Destroy(mesh);}
    }
}

