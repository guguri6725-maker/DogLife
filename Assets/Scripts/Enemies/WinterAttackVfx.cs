using System.Collections.Generic;
using UnityEngine;
namespace DogLife
{
    [RequireComponent(typeof(MeshFilter),typeof(MeshRenderer))]
    public sealed class WinterAttackVfx:MonoBehaviour
    {
        public WinterMonsterEnemy enemy;
        public WinterIcePatch patch;
        [SerializeField,Min(0)] float spreadCrestWidth=.65f;
        [SerializeField] private Material material;
        float VisualScale=>enemy?enemy.SizeScale:1;
        Mesh mesh;MeshRenderer surface;CombatHealth health;
        readonly List<Vector3> vertices=new List<Vector3>(12000);
        readonly List<Color> colors=new List<Color>(12000);
        readonly List<int> indices=new List<int>(18000);
        readonly List<Vector4> iceData=new List<Vector4>(12000);
        float iceSurface,iceSeed,iceBase,iceHeight;
        static readonly Color White=new Color(.84f,.97f,1),Ice=new Color(.34f,.65f,.94f),Deep=new Color(.045f,.14f,.3f);
        public void Initialize(Material mat){material=mat;if(surface)surface.sharedMaterial=mat;}
        void Awake(){mesh=new Mesh{name="Winter frost crystals and warning"};mesh.MarkDynamic();GetComponent<MeshFilter>().sharedMesh=mesh;surface=GetComponent<MeshRenderer>();surface.sharedMaterial=material;surface.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;surface.receiveShadows=false;if(enemy)health=enemy.GetComponent<CombatHealth>();}
        static Color A(Color color,float alpha){color.a=alpha;return color;}
        // 기존 범위 표시와 입체 얼음을 같은 메시 안에서 구분합니다.
        Vector4 IceData(Vector3 p)=>new Vector4(iceSurface,iceSeed,iceSurface>0?Mathf.Clamp01((p.y-iceBase)/Mathf.Max(.001f,iceHeight)):0,0);
        void Tri(Vector3 a,Vector3 b,Vector3 c,Color color){int n=vertices.Count;vertices.Add(transform.InverseTransformPoint(a));vertices.Add(transform.InverseTransformPoint(b));vertices.Add(transform.InverseTransformPoint(c));colors.Add(color);colors.Add(color);colors.Add(color);iceData.Add(IceData(a));iceData.Add(IceData(b));iceData.Add(IceData(c));indices.Add(n);indices.Add(n+1);indices.Add(n+2);}
        void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color){Tri(a,b,c,color);Tri(a,c,d,color);}
        void Line(Vector3 a,Vector3 b,float width,Color color){Vector3 s=Vector3.Cross(b-a,Vector3.up).normalized*width*.5f;if(s.sqrMagnitude<.000001f)s=Vector3.right*width*.5f;Quad(a-s,a+s,b+s,b-s,color);}
        void Crystal(Vector3 p,float height,float width,float alpha,float seed)
        {
            height*=VisualScale;width*=VisualScale;
            if(height<.01f)return;
            if(enemy&&enemy.Kind==WinterMonsterEnemy.Species.Golem){DetailedCrystal(p,height,width,alpha,seed);return;}
            Vector3 tip=p+new Vector3(Mathf.Sin(seed)*height*.25f,height,Mathf.Cos(seed)*height*.18f);
            for(int i=0;i<5;i++)
            {
                float a=i*Mathf.PI*2/5,b=(i+1)*Mathf.PI*2/5;
                Vector3 x=p+new Vector3(Mathf.Cos(a),.08f,Mathf.Sin(a))*width,y=p+new Vector3(Mathf.Cos(b),.08f,Mathf.Sin(b))*width;
                Vector3 mid=(tip-p)*.60f,xx=x+mid,yy=y+mid;
                Quad(x,y,yy,xx,A(Color.Lerp(Deep,Ice,.55f+.18f*(i%3)),alpha*.85f));
                Tri(xx,yy,tip,A(i%2==0?Ice:White,alpha*.88f));
                Line(xx,tip,.008f,A(White,alpha*.75f));
                Line(x,xx,.006f,A(White,alpha*.4f));
            }
        }
        // 큰 얼음은 비대칭 파단면으로 만들고 작은 파편도 같은 재질을 사용합니다.
        void DetailedCrystal(Vector3 p,float height,float width,float alpha,float seed,bool fragments=true)
        {
            iceSurface=1;iceSeed=seed;iceBase=p.y;iceHeight=height;
            const int sides=7;
            Vector3 lean=new Vector3(Mathf.Sin(seed*1.7f),0,Mathf.Cos(seed*2.3f))*height*.16f;
            for(int side=0;side<sides;side++)
            {
                float a=side*Mathf.PI*2/sides+seed,b=(side+1)*Mathf.PI*2/sides+seed;
                float ra=width*(1+.16f*Mathf.Sin(side*4.7f+seed));
                float rb=width*(1+.16f*Mathf.Sin(((side+1)%sides)*4.7f+seed));
                Vector3 da=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*ra;
                Vector3 db=new Vector3(Mathf.Cos(b),0,Mathf.Sin(b))*rb;
                Vector3 a0=p+da*1.18f,b0=p+db*1.18f;
                Vector3 a1=p+da+lean*.36f+Vector3.up*height*.38f;
                Vector3 b1=p+db+lean*.36f+Vector3.up*height*.38f;
                Vector3 a2=p+da*.73f+lean*.78f+Vector3.up*height*(.73f+.09f*Mathf.Sin(side*2.1f+seed));
                Vector3 b2=p+db*.73f+lean*.78f+Vector3.up*height*(.73f+.09f*Mathf.Sin(((side+1)%sides)*2.1f+seed));
                Vector3 tip=p+lean+Vector3.up*height;
                Color tint=A(Color.Lerp(new Color(.64f,.81f,.88f),White,.35f+.15f*Mathf.Sin(side+seed)),alpha);
                Quad(a0,b0,b1,a1,tint);Quad(a1,b1,b2,a2,tint);Tri(a2,b2,tip,tint);
            }
            iceSurface=0;
            // 밑동의 작은 파단 조각으로 지면과 큰 결정의 경계를 연결합니다.
            if(fragments&&height>.45f)
                for(int i=0;i<2;i++)
                {
                    float angle=seed+i*2.4f;
                    Vector3 offset=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*width*1.12f;
                    DetailedCrystal(p+offset,height*(.20f+.08f*i),width*.50f,alpha,seed+17+i,false);
                }
        }
        Vector3 Dir(Vector3 forward,float degrees)=>Quaternion.AngleAxis(degrees,Vector3.up)*forward;
        void Arc(Vector3 p,Vector3 forward,float radius,float angle,float width,Color color)
        {
            int count=Mathf.Max(16,Mathf.CeilToInt(angle/4));
            for(int i=0;i<count;i++){Vector3 a=Dir(forward,-angle*.5f+angle*i/count),b=Dir(forward,-angle*.5f+angle*(i+1)/count);Quad(p+a*radius,p+b*radius,p+b*(radius-width),p+a*(radius-width),color);}
        }
        void Snowflake(Vector3 p,float size,float alpha,float spin)
        {
            for(int i=0;i<6;i++)
            {
                Vector3 d=Dir(Vector3.forward,spin+i*60);Line(p,p+d*size,.018f,A(White,alpha));
                for(int j=1;j<=2;j++){Vector3 q=p+d*size*j*.32f;Line(q,q+Dir(d,45)*size*.23f,.012f,A(White,alpha));Line(q,q+Dir(d,-45)*size*.23f,.012f,A(White,alpha));}
            }
        }
        void Warning(Vector3 p,Vector3 forward,float radius,float angle,float progress,bool original=false)
        {
            p+=Vector3.up*.10f;int segments=48;
            for(int i=0;i<segments;i++)Tri(p,p+Dir(forward,-angle/2+angle*i/segments)*radius,p+Dir(forward,-angle/2+angle*(i+1)/segments)*radius,original?A(Deep,.30f):new Color(1,.30f,.36f,.24f));
            // 옅은 빨강 바탕을 기존 얼음빛으로 채웁니다. 장판 정령은 기존 표시를 유지합니다.
            var fillCenter=p+Vector3.up*.009f;
            if(!original)for(int i=0;i<segments;i++)Tri(fillCenter,fillCenter+Dir(forward,-angle/2+angle*i/segments)*radius*progress,fillCenter+Dir(forward,-angle/2+angle*(i+1)/segments)*radius*progress,A(Ice,.42f));
            Arc(p,forward,radius,angle,.11f,A(Deep,.95f));Arc(p+Vector3.up*.005f,forward,radius-.025f,angle,.045f,A(White,.95f));
            Arc(p+Vector3.up*.009f,forward,radius*(original?Mathf.Lerp(.2f,.98f,progress):progress),angle,original?.055f:.065f,original?A(Ice,.85f):A(White,.9f));
            if(angle<359){Line(p,p+Dir(forward,-angle/2)*radius,.055f,A(White,.9f));Line(p,p+Dir(forward,angle/2)*radius,.055f,A(White,.9f));}
            for(int i=0;i<8;i++){float a=-angle/2+angle*(i+.5f)/8;Vector3 d=Dir(forward,a);Line(p+d*(radius-.2f),p+d*(radius-.4f),.035f,A(Ice,.8f));}
            Snowflake(p+forward*radius*.48f,.25f+.1f*progress,.8f,0);
        }
        void Cracks(Vector3 p,Vector3 forward,float range,float angle,float growth,float alpha)
        {
            bool glacier=enemy&&enemy.Kind==WinterMonsterEnemy.Species.Golem;
            for(int lane=0;lane<5;lane++)
            {
                Vector3 direction=Dir(forward,(lane-2)*angle*.21f),side=Vector3.Cross(Vector3.up,direction);Vector3 prev=p;
                for(int j=1;j<=10;j++)
                {
                    float distance=range*j/10;if(distance>range*growth)break;
                    Vector3 next=p+direction*distance+side*Mathf.Sin(j*7+lane*3)*.12f;
                    Line(prev,next,glacier?.055f:.10f,A(Deep,alpha*(glacier?.42f:1)));Line(prev+Vector3.up*.008f,next+Vector3.up*.008f,glacier?.015f:.036f,A(White,alpha*(glacier?.5f:1)));
                    Vector3 branch=next+Dir(direction,(j%2==0?1:-1)*55)*.35f;Line(next,branch,glacier?.012f:.028f,A(Ice,alpha*(glacier?.5f:1)));prev=next;
                }
            }
        }
        void Shards(Vector3 p,float t,float size)
        {
            float a=Mathf.Sin(Mathf.Clamp01(t)*Mathf.PI);
            for(int i=0;i<28;i++){float angle=i*2.39996f;Vector3 d=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));Vector3 q=p+d*size*t*(.55f+.45f*Mathf.Abs(Mathf.Sin(i*7)))+Vector3.up*a*(.25f+.65f*Mathf.Abs(Mathf.Sin(i*3)));Crystal(q,.10f+.09f*a,.04f,a,i);}
            Arc(p,Vector3.forward,size*t,360,.05f,A(White,a*.8f));
        }
        void DrawPatch()
        {
            Vector3 p=patch.transform.position+Vector3.up*.065f;float radius=patch.Radius*patch.Growth,alpha=patch.Opacity;
            // 얼어붙는 경계에 밝은 서리 띠를 두어 눈 바닥에서도 확산이 보이게 합니다.
            if(patch.Growth<.995f&&radius>.1f)
            {
                Arc(p+Vector3.up*.06f,Vector3.forward,radius,360,spreadCrestWidth,A(White,.9f));
                Arc(p+Vector3.up*.08f,Vector3.forward,radius*.96f,360,spreadCrestWidth*.3f,A(Ice,.85f));
            }
            for(int i=0;i<72;i++){Vector3 a=Dir(Vector3.forward,i*5),b=Dir(Vector3.forward,(i+1)*5);Tri(p,p+a*radius,p+b*radius,A(new Color(.20f,.43f,.67f),.42f*alpha));}
            Arc(p+Vector3.up*.005f,Vector3.forward,radius,360,.11f,A(White,.30f*alpha));
            Arc(p+Vector3.up*.008f,Vector3.forward,radius-.025f,360,.026f,A(White,.70f*alpha));
            // Broken, offset polygon cells resemble frozen water rather than radial spokes.
            // 넓은 보스 장판도 작은 장판과 비슷한 얼음 조각 밀도를 유지합니다.
            int rings=Mathf.Clamp(Mathf.CeilToInt(radius/3),3,24);
            for(int ring=1;ring<=rings;ring++)
            {
                int cells=Mathf.Clamp(Mathf.CeilToInt(2*Mathf.PI*radius*ring/(rings+1)/3),11,96);
                for(int i=0;i<cells;i++)
                {
                float a=i*360f/cells+(ring%2)*13,b=(i+1)*360f/cells+(ring%2)*13;
                float r=radius*(ring/(float)(rings+1)+.08f/(rings+1)*Mathf.Sin(i*13+ring));
                Vector3 x=p+Dir(Vector3.forward,a)*r,y=p+Dir(Vector3.forward,b)*r;
                Line(x,y,.026f,A(Deep,.45f*alpha));Line(x+Vector3.up*.009f,y+Vector3.up*.009f,.010f,A(White,.65f*alpha));
                if(ring<rings&&i%2==0){Vector3 q=p+Dir(Vector3.forward,a+15)*radius*(ring+1)/(rings+1);Line(x,q,.016f,A(White,.5f*alpha));}
                }
            }
            for(int i=0;i<8;i++){Vector3 dir=Dir(Vector3.forward,i*137);Snowflake(p+dir*radius*(.25f+.5f*Mathf.Abs(Mathf.Sin(i*7))),.08f+.05f*Mathf.Sin(i),(.25f+.2f*Mathf.Sin(Time.time*1.5f+i))*alpha,i*19);}
            for(int i=0;i<18;i++){Vector3 d=Dir(Vector3.forward,i*20);Crystal(p+d*(radius-.08f),.06f+.05f*Mathf.Sin(i*3),.07f,alpha*.75f,i);}
        }
        void LateUpdate()
        {
            vertices.Clear();colors.Clear();indices.Clear();iceData.Clear();iceSurface=0;
            if(patch&&patch.IsActive)DrawPatch();
            else if(enemy&&(enemy.isActiveAndEnabled||MonsterAnimationPreview.IsPreviewing(enemy))&&health&&!health.Dead)
            {
                var state=enemy.State;float t=enemy.Progress;Vector3 p=enemy.Origin+Vector3.up*.10f;var kind=enemy.Kind;
                bool warning=state==WinterMonsterEnemy.Phase.Warning||state==WinterMonsterEnemy.Phase.Reaim;
                if(warning||kind!=WinterMonsterEnemy.Species.Spirit&&enemy.Attacking&&enemy.TelegraphProgress<1)
                {
                    if(kind==WinterMonsterEnemy.Species.Wolf){if(enemy.ClawPhase)Warning(enemy.Origin,enemy.Forward,enemy.Range,enemy.Angle,enemy.TelegraphProgress);else Warning(enemy.TargetPoint,enemy.Forward,enemy.LandingRadius,360,enemy.TelegraphProgress);}
                    else if(kind==WinterMonsterEnemy.Species.Spirit){Warning(enemy.TargetPoint,enemy.Forward,enemy.IceRadius,360,t,true);Cracks(p,enemy.Forward,Vector3.Distance(enemy.TargetPoint,enemy.Origin),30,t,.7f);}
                    else Warning(kind==WinterMonsterEnemy.Species.Mammoth?enemy.TargetPoint:enemy.Origin,enemy.Forward,enemy.Range,enemy.Angle,enemy.TelegraphProgress);
                    // 매머드 준비 동작은 엄니 장식 없이 바닥의 공격 범위만 표시합니다.
                }
                if(enemy.Attacking)
                {
                    if(kind==WinterMonsterEnemy.Species.Golem)
                    {
                        float g=enemy.WaveProgress;Cracks(p,enemy.Forward,enemy.Range,enemy.Angle,g,1);
                        for(int lane=0;lane<5;lane++)for(int j=1;j<=7;j++)
                        {
                            float distance=enemy.Range*j/7,age=g-j/7f;
                            if(age<0)continue;float height=Mathf.Clamp01(age/.10f)*(1-Mathf.InverseLerp(.85f,1,t));Vector3 d=Dir(enemy.Forward,(lane-2)*enemy.Angle*.21f);
                            Crystal(p+d*distance, height*(.5f+.65f*j/7f)*(1+.28f*Mathf.Sin(lane*7+j*3)),.13f+.08f*j/7f,.94f,lane*7+j);
                        }
                        if(t>=.28f)Shards(p,Mathf.InverseLerp(.28f,.8f,t),2.1f);
                    }
                    else if(kind==WinterMonsterEnemy.Species.Wolf)
                    {
                        if(enemy.ClawPhase)
                        {
                            float sweep=Mathf.InverseLerp(.24f,.74f,t),alpha=Mathf.Sin(sweep*Mathf.PI);
                            Vector3 center=enemy.transform.position+Vector3.up*.55f*VisualScale;
                            for(int claw=0;claw<3;claw++)for(int i=0;i<28;i++)
                            {
                                float a=-enemy.Angle*.5f+enemy.Angle*(sweep-i*.013f);if(a< -enemy.Angle*.5f)continue;
                                float r=enemy.Range-.12f-claw*.20f;
                                Vector3 x=center+Dir(enemy.Forward,a)*r+Vector3.up*(claw*.14f-i*.008f);
                                Vector3 y=center+Dir(enemy.Forward,a-3)*r+Vector3.up*(claw*.14f-(i+1)*.008f);
                                Line(x,y,.085f*VisualScale,A(i<4?White:Ice,alpha*(1-i/28f)));
                            }
                        }
                        else
                        {
                        if(enemy.TelegraphProgress>=1)Warning(enemy.TargetPoint,enemy.Forward,enemy.LandingRadius,360,1);
                        if(t>=.80f)Shards(enemy.TargetPoint+Vector3.up*.08f,Mathf.InverseLerp(.80f,1,t),enemy.LandingRadius);
                        else for(int i=0;i<12;i++){Vector3 q=enemy.transform.position-enemy.Forward*i*.12f+Vector3.up*(.45f+Mathf.Sin(t*Mathf.PI)*.8f);Crystal(q,.08f,.035f,(1-i/12f)*.8f,i);}
                        }
                    }
                    else if(kind==WinterMonsterEnemy.Species.Spirit){Cracks(p,enemy.Forward,Vector3.Distance(enemy.TargetPoint,enemy.Origin),35,Mathf.Clamp01(t*3),.9f);Shards(enemy.TargetPoint+Vector3.up*.1f,t,enemy.IceRadius);}
                    else
                    {
                        float rise=Mathf.InverseLerp(.25f,.8f,t),alpha=Mathf.Sin(rise*Mathf.PI);
                        Vector3 center=enemy.transform.position+Vector3.up*.1f;
                        for(int lane=0;lane<36;lane++)
                        {
                            Vector3 d=Dir(enemy.Forward,-enemy.Angle/2+enemy.Angle*lane/36),e=Dir(enemy.Forward,-enemy.Angle/2+enemy.Angle*(lane+1)/36);
                            float edge=Mathf.Sin((lane+.5f)/36*Mathf.PI);
                            for(int j=0;j<12;j++){float a=(rise-j*.018f)*Mathf.PI*.75f;if(a<0)continue;float b=a-.018f*Mathf.PI*.75f;
                                Vector3 x=center+d*Mathf.Cos(a)*enemy.Range+Vector3.up*Mathf.Sin(a)*1.65f*VisualScale,y=center+e*Mathf.Cos(a)*enemy.Range+Vector3.up*Mathf.Sin(a)*1.65f*VisualScale;
                                Vector3 z=center+e*Mathf.Cos(b)*enemy.Range+Vector3.up*Mathf.Sin(b)*1.65f*VisualScale,w=center+d*Mathf.Cos(b)*enemy.Range+Vector3.up*Mathf.Sin(b)*1.65f*VisualScale;
                                Quad(x,y,z,w,A(j<2?White:Ice,alpha*edge*(1-j/12f)*.65f));}
                        }
                        Shards(center+enemy.Forward*1.3f,t,1.5f);
                    }
                }
            }
            mesh.Clear();mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetUVs(0,iceData);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();surface.enabled=vertices.Count>0;
        }
        void OnDisable(){if(surface)surface.enabled=false;}
        void OnDestroy(){if(mesh)Destroy(mesh);}
    }
}
