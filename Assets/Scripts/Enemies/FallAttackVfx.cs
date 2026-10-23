using System.Collections.Generic;
using UnityEngine;

namespace DogLife
{
    // Hierarchy에 미리 연결한 렌더러만 사용합니다. 이 메시에는 피해 판정이 없습니다.
    [DefaultExecutionOrder(100)]
    public sealed class FallAttackVfx : MonoBehaviour
    {
        [SerializeField] FallMonsterEnemy enemy;
        [SerializeField] FallAttackPresentation presentation;
        [SerializeField,Range(0,1)] float warningOpacity=.12f;
        [SerializeField] Color leafColor=new Color(1,.48f,.10f,.8f);
        [SerializeField] Color brightColor=new Color(1,.82f,.40f,.85f);
        [SerializeField] Transform sporeOrigin,leftWingTip,rightWingTip;
        [SerializeField,Min(.05f)] float trailLifetime=.28f;
        [SerializeField,Range(16,180)] int maxParticles=112;
        [Header("입체 포자 / 흙먼지 / 잔광")]
        [SerializeField,Range(8,32)] int sporeCloudLayers=18;
        [SerializeField,Range(0,1)] float sporeCloudOpacity=.62f;
        [SerializeField,Range(.1f,2)] float dustScale=1.3f;
        [SerializeField,Range(0,1)] float trailOpacity=.48f;
        Camera viewCamera;
        Mesh mesh;MeshRenderer surface;CombatHealth health;Transform visual;
        readonly List<Vector3> vertices=new List<Vector3>(4096);
        readonly List<Color> colors=new List<Color>(4096);
        readonly List<int> indices=new List<int>(6144);
        readonly List<Vector4> uv=new List<Vector4>(4096);
        readonly List<Vector3> warningPoints=new List<Vector3>(112);
        struct Stroke { public Vector3 a,b;public float time; }
        readonly List<Stroke> strokes=new List<Stroke>(32);
        readonly List<Stroke> wingStrokes=new List<Stroke>(32);
        struct Fleck { public Vector3 position,velocity;public float born,life,size,spin;public int kind;public Color tint; }
        readonly List<Fleck> flecks=new List<Fleck>(128);
        float nextEmission;int seed;
        FallMonsterEnemy.Phase previousPhase;
        void Awake()
        {
            mesh=new Mesh{name="Autumn warning and leaves"};mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh=mesh;surface=GetComponent<MeshRenderer>();surface.enabled=false;
            if(enemy){health=enemy.GetComponent<CombatHealth>();var animator=enemy.GetComponentInChildren<Animator>();if(animator)visual=animator.transform;}
        }
        void Triangle(Vector3 a,Vector3 b,Vector3 c,Color color)
        {
            int n=vertices.Count;vertices.Add(transform.InverseTransformPoint(a));vertices.Add(transform.InverseTransformPoint(b));vertices.Add(transform.InverseTransformPoint(c));
            colors.Add(color);colors.Add(color);colors.Add(color);uv.Add(Vector4.zero);uv.Add(Vector4.zero);uv.Add(Vector4.zero);indices.Add(n);indices.Add(n+1);indices.Add(n+2);
        }
        void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color){Triangle(a,b,c,color);Triangle(a,c,d,color);}
        // 공유 셰이더로 연기와 잔상을 부드럽게 표현하며 경고 도형은 기존 모양을 유지합니다.
        void SoftQuad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color,float mode,float seed)
        {
            int n=uv.Count;Quad(a,b,c,d,color);
            uv[n]=new Vector4(0,0,mode,seed);uv[n+1]=new Vector4(1,0,mode,seed);uv[n+2]=new Vector4(1,1,mode,seed);
            uv[n+3]=uv[n];uv[n+4]=uv[n+2];uv[n+5]=new Vector4(0,1,mode,seed);
        }
        // 경계선과 같은 외곽으로 채워 표시와 실제 캡슐 폭의 차이를 없앱니다.
        void FillAttackOutline(Color tint,float progress=1,float lift=0)
        {
            enemy.GetWarningOutline(warningPoints,.085f);
            var center=enemy.WarningCenter+Vector3.up*.085f;
            for(int i=1;i<warningPoints.Count;i++)Triangle(center+Vector3.up*lift,Vector3.Lerp(center,warningPoints[i-1],progress)+Vector3.up*lift,Vector3.Lerp(center,warningPoints[i],progress)+Vector3.up*lift,tint);
        }
        Color Alpha(Color c,float a){c.a=a;return c;}
        void Leaf(Vector3 p,Vector3 along,Vector3 side,float size,Color c)
        {
            // 톱니와 세 갈래 끝, 접힌 단면, 측맥을 가진 마른 잎입니다.
            Vector3 previous=p-along*size*.65f;
            for(int i=1;i<=16;i++)
            {
                float a=i*Mathf.PI/8,forward=-Mathf.Cos(a);
                float lobes=1+.22f*Mathf.Cos(a*6);
                var next=p+along*(forward*.8f+.15f)*size+side*(Mathf.Sin(a)*.43f*lobes*size);
                Triangle(p+Vector3.up*size*.1f,previous,next,i<8?c:Alpha(new Color(c.r*.67f,c.g*.65f,c.b*.6f),c.a));previous=next;
            }
            var basePoint=p-along*size*.65f;var tip=p+along*size*.95f;
            Quad(basePoint-side*size*.018f,basePoint+side*size*.018f,tip+side*size*.008f,tip-side*size*.008f,Alpha(brightColor,c.a*.65f));
            for(int i=0;i<3;i++)for(int sign=-1;sign<=1;sign+=2)
            {
                var a=p+along*(i*.24f-.3f)*size;var b=a+along*size*.2f+side*sign*size*(.3f-i*.055f);
                Quad(a,a+along*size*.015f,b+along*size*.015f,b,Alpha(brightColor,c.a*.38f));
            }
        }
        void Puff(Vector3 p,float radius,Color c,float mode=2,float seed=0)
        {
            if(!viewCamera)return;
            var right=viewCamera.transform.right*radius;var up=viewCamera.transform.up*radius;
            SoftQuad(p-right-up,p+right-up,p+right+up,p-right+up,c,mode,seed);
        }
        void SporeVolume(Vector3 center,float progress)
        {
            // 큰 구름은 낮게 깔고 작은 포자만 떠올려 몸체와 회피 경계가 보이게 합니다.
            float fade=Mathf.SmoothStep(0,1,Mathf.Clamp01(progress*8))*(1-Mathf.SmoothStep(.82f,1,progress));
            for(int i=0;i<sporeCloudLayers;i++)
            {
                float a=i*2.399963f+Time.time*.18f,r=enemy.Radius*(.18f+.53f*Mathf.Repeat(i*.618f,1));
                float size=Mathf.Min(enemy.Radius-r,enemy.Radius*(.28f+.07f*(i%3)));
                Vector3 at=center+new Vector3(Mathf.Cos(a)*r,.4f+.12f*(i%4),Mathf.Sin(a)*r);
                Puff(at,size, new Color(.54f,.65f,.23f,sporeCloudOpacity*fade),2,i*3.27f);
                Puff(at+Vector3.up*(.35f+.2f*Mathf.Sin(Time.time+i)),.035f+(i%3)*.013f,new Color(.64f,.64f,.21f,.6f*fade),3,i);
            }
        }
        void BurrowMark(Vector3 center,float sink)
        {
            if(sink<=.01f)return;
            float fade=Mathf.SmoothStep(0,1,Mathf.Clamp01(sink*4));
            float radius=enemy.SpikeSafeRadius;
            // 판정 반경 안에만 얕은 흙 웅덩이와 울퉁불퉁한 가장자리를 그립니다.
            for(int i=0;i<48;i++)
            {
                float a=i*Mathf.PI/24,b=(i+1)*Mathf.PI/24;
                var da=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var db=new Vector3(Mathf.Cos(b),0,Mathf.Sin(b));
                float ra=radius*(.79f+.035f*Mathf.Sin(a*7)),rb=radius*(.79f+.035f*Mathf.Sin(b*7));
                var edgeA=center+da*ra+Vector3.up*(.045f+.016f*Mathf.Sin(a*9))*sink;
                var edgeB=center+db*rb+Vector3.up*(.045f+.016f*Mathf.Sin(b*9))*sink;
                int n=vertices.Count;
                Triangle(center,edgeA,edgeB,new Color(.13f,.075f,.035f,.50f*fade));
                colors[n+1]=colors[n+2]=new Color(.29f,.16f,.065f,.72f*fade);
                Quad(edgeA,center+da*radius*.94f,center+db*radius*.94f,edgeB,new Color(.40f,.255f,.12f,(.42f+.1f*Mathf.Sin(a*5))*fade));
            }
            // 땅을 뚫고 나온 자리에 크기와 방향이 다른 낙엽이 포개집니다.
            for(int i=0;i<13;i++)
            {
                float a=i*Mathf.PI*2/13+.08f*Mathf.Sin(i*3.1f);var radial=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                float lift=.025f+.012f*Mathf.Sin(Time.time*3+i)*sink;
                var p=center+radial*radius*(.66f+.06f*Mathf.Sin(i*2.4f))+Vector3.up*lift;
                var along=Quaternion.AngleAxis(35+25*Mathf.Sin(i),Vector3.up)*radial;
                Color tint=Color.Lerp(new Color(.47f,.23f,.065f),new Color(.94f,.56f,.16f),(i%4)/3f);tint.a=.88f*fade;
                Leaf(p,along,Vector3.Cross(Vector3.up,along),radius*(.115f+.025f*(i%3)),tint);
            }
            // 얇은 긁힌 흙 자국은 중앙에 두어 공격 가시와 구분합니다.
            for(int i=0;i<5;i++)
            {
                float a=i*2.399963f;var dir=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var side=Vector3.Cross(Vector3.up,dir)*.012f;
                var start=center+dir*radius*.16f+Vector3.up*.003f;var end=center+dir*radius*(.4f+.03f*i)+Vector3.up*.004f;
                Quad(start-side,start+side,end+side*.3f,end-side*.3f,new Color(.66f,.43f,.20f,.3f*fade));
            }
        }
        void Emit(Vector3 origin,Vector3 velocity,int kind,float size,float life,Color tint)
        {
            if(flecks.Count>=maxParticles)return;
            flecks.Add(new Fleck{position=origin,velocity=velocity,kind=kind,size=size,life=life,born=Time.time,spin=seed++*2.399963f,tint=tint});
        }
        void Spray(Vector3 origin,int count,float force,int kind)
        {
            for(int i=0;i<count;i++)
            {
                float angle=(seed+i)*2.399963f;var dir=new Vector3(Mathf.Cos(angle),.45f+.2f*(i%3),Mathf.Sin(angle));
                var tint=kind==2?new Color(.55f,.36f,.17f,.34f):kind==3?new Color(.79f,.79f,.28f,.68f):Color.Lerp(new Color(.53f,.20f,.045f,.9f),leafColor,(i%5)/4f);
                Emit(origin,dir*force*(.65f+.12f*(i%4)),kind,kind==2?.4f:kind==3?.07f:.10f+.035f*(i%3),kind==2?.7f:1.05f,tint);
            }
        }
        void RenderFlecks()
        {
            for(int i=flecks.Count-1;i>=0;i--)
            {
                var f=flecks[i];float age=Time.time-f.born,t=age/f.life;if(t>=1){flecks.RemoveAt(i);continue;}
                float gravity=f.kind==2?.12f:f.kind==3?-.1f:2.2f;
                var p=f.position+f.velocity*age-Vector3.up*(gravity*age*age);
                var tint=Alpha(f.tint,f.tint.a*Mathf.Clamp01(age/.07f)*Mathf.Pow(1-t,1.3f));
                if(f.kind==2){Puff(p,f.size*dustScale*(1+age*2.3f),tint,2,f.spin);continue;}
                if(f.kind==3){Puff(p,f.size*2,tint,3,f.spin);continue;}
                float angle=f.spin+age*(f.kind==1?11:6);var along=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),.25f).normalized;
                var side=Vector3.Cross(along,Vector3.forward).normalized;
                if(f.kind==1){var tip=p+along*f.size;Triangle(p-along*f.size,tip,p+side*f.size*.2f,tint);Triangle(p-along*f.size,p-side*f.size*.15f,tip,Alpha(brightColor,tint.a*.5f));}
                else Leaf(p,along,side,f.size,tint);
            }
        }
        void LateUpdate()
        {
            if(!enemy||(!enemy.enabled&&!MonsterAnimationPreview.IsPreviewing(enemy))||!health||health.Dead){Clear();return;}
            var phase=enemy.CurrentPhase;bool ready=phase==FallMonsterEnemy.Phase.Anticipation;
            bool active=phase==FallMonsterEnemy.Phase.Active;
            bool warning=ready||phase==FallMonsterEnemy.Phase.Attack||active;
            bool entered=phase!=previousPhase;
            if(entered&&phase==FallMonsterEnemy.Phase.Anticipation){strokes.Clear();wingStrokes.Clear();nextEmission=0;}
            vertices.Clear();colors.Clear();indices.Clear();uv.Clear();
            if(!viewCamera)viewCamera=Camera.main;
            var center=enemy.WarningCenter+Vector3.up*.085f;
            float progress=enemy.TelegraphProgress;
            // 옅은 빨강 바탕 위로 기존 낙엽색이 차오릅니다.
            var tint=new Color(1,.30f,.36f,.24f);
            var pattern=enemy.AttackPattern;
            if(pattern==FallMonsterEnemy.Pattern.Spikes&&presentation)BurrowMark(center,presentation.BurrowAmount);
            if(warning)
            {
                if(pattern==FallMonsterEnemy.Pattern.CrowDash)
                {
                    var start=enemy.AttackOrigin+Vector3.up*.085f;var dir=enemy.LockedDirection;var side=Vector3.Cross(Vector3.up,dir)*.65f;var end=start+dir*enemy.DashDistance;
                    FillAttackOutline(tint);
                    FillAttackOutline(Alpha(leafColor,.42f),progress,.009f);
                    for(float d=.7f;d<enemy.DashDistance-.3f;d+=1.25f)
                    {
                        var p=start+dir*d;float pulse=.3f+.5f*Mathf.Max(0,1-Mathf.Abs(d/enemy.DashDistance-progress)*3);
                        Quad(p-side*.7f-dir*.2f,p-side*.7f-dir*.12f,p+dir*.2f,p+dir*.12f,Alpha(brightColor,pulse));
                        Quad(p+side*.7f-dir*.12f,p+side*.7f-dir*.2f,p+dir*.12f,p+dir*.2f,Alpha(brightColor,pulse));
                    }
                }
                else
                {
                    bool sweep=pattern==FallMonsterEnemy.Pattern.ArmSweep;
                    float radius=sweep?enemy.ArmLength+enemy.ArmThickness:enemy.Radius;
                    if(sweep){FillAttackOutline(tint);FillAttackOutline(Alpha(leafColor,.42f),progress,.009f);}
                    float inner=pattern==FallMonsterEnemy.Pattern.Spikes?enemy.SpikeSafeRadius:0;
                    for(int i=0;i<64;i++)
                    {
                        float a=sweep?Mathf.Lerp(-enemy.SweepAngle*.5f,enemy.SweepAngle*.5f,i/64f):i*360f/64;
                        float b=sweep?Mathf.Lerp(-enemy.SweepAngle*.5f,enemy.SweepAngle*.5f,(i+1)/64f):(i+1)*360f/64;
                        Vector3 da=Quaternion.AngleAxis(a,Vector3.up)*(sweep?enemy.LockedDirection:Vector3.forward),db=Quaternion.AngleAxis(b,Vector3.up)*(sweep?enemy.LockedDirection:Vector3.forward);
                        if(!sweep)Quad(center+da*inner,center+da*radius,center+db*radius,center+db*inner,tint);
                        // 고슴도치 중앙 안전지대는 채우지 않습니다.
                        if(!sweep){float edge=Mathf.Lerp(inner,radius,progress);var p=center+Vector3.up*.009f;Quad(p+da*inner,p+da*edge,p+db*edge,p+db*inner,Alpha(leafColor,.42f));}
                        if(i/64f<progress)Quad(center+da*(radius-.16f),center+da*(radius-.12f),center+db*(radius-.12f),center+db*(radius-.16f),Alpha(brightColor,.68f));
                        if(i%4<2)Quad(center+da*(radius-.28f),center+da*(radius-.265f),center+db*(radius-.265f),center+db*(radius-.28f),Alpha(brightColor,.28f));
                        if(i%8==4){var d=(da+db).normalized;Leaf(center+d*radius*.78f,d,Vector3.Cross(Vector3.up,d),.24f,Alpha(leafColor,.65f));}
                    }
                }
            }
            // 실물 팔의 어깨~손 궤적을 짧게 남기므로 공격 속도가 바뀌어도 잔상이 따라갑니다.
            if(active&&pattern==FallMonsterEnemy.Pattern.ArmSweep&&(strokes.Count==0||Time.time-strokes[strokes.Count-1].time>=.008f)&&presentation&&presentation.ArmPoints(out var shoulder,out var elbow,out var hand))
                strokes.Add(new Stroke{a=Vector3.Lerp(shoulder,hand,.25f),b=hand,time=Time.time});
            strokes.RemoveAll(s=>Time.time-s.time>trailLifetime);
            for(int i=1;i<strokes.Count;i++)
            {
                var a=strokes[i-1];var b=strokes[i];float fade=1-(Time.time-a.time)/trailLifetime;
                SoftQuad(a.a,a.b,b.b,b.a,Alpha(leafColor,fade*trailOpacity),4,i*.31f);
                SoftQuad(Vector3.Lerp(a.a,a.b,.92f),a.b,b.b,Vector3.Lerp(b.a,b.b,.92f),Alpha(brightColor,fade*.65f),4,i*.31f);
            }
            // 네 패턴은 서로 다른 위치와 시점에서 입자를 방출합니다. 기존 피해 판정은 건드리지 않습니다.
            if(pattern==FallMonsterEnemy.Pattern.ArmSweep&&active&&strokes.Count>0&&Time.time>=nextEmission)
            {nextEmission=Time.time+.055f;Spray(strokes[strokes.Count-1].b,3,.8f,0);Spray(strokes[strokes.Count-1].b,2,1.5f,1);}
            if(pattern==FallMonsterEnemy.Pattern.Spikes)
            {
                if(ready&&Time.time>=nextEmission){nextEmission=Time.time+.11f;var origin=center+enemy.LockedDirection*.45f;Spray(origin,3,.9f,1);Spray(origin,2,.45f,2);}
                if(entered&&active)for(int i=0;i<16;i++)
                {
                    float a=i*Mathf.PI/8;var dir=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                    Emit(center+dir*enemy.Radius*.65f,dir*.8f+Vector3.up*.1f,2,.45f,.85f,new Color(.6f,.40f,.19f,.4f));Spray(center+dir*enemy.Radius*.55f,2,1.4f,0);
                }
            }
            if(pattern==FallMonsterEnemy.Pattern.Spores&&active&&Time.time>=nextEmission)
            {
                nextEmission=Time.time+.08f;var origin=sporeOrigin?sporeOrigin.position:center+Vector3.up*1.5f;
                Spray(origin,5,1.35f,3);
                float a=seed*2.399963f;var dir=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                Emit(center+dir*.4f,dir*.75f+Vector3.up*.06f,2,.65f,1.25f,new Color(.55f,.62f,.19f,.20f));
            }
            if(pattern==FallMonsterEnemy.Pattern.Spores&&active)SporeVolume(center,enemy.Progress);
            if(pattern==FallMonsterEnemy.Pattern.CrowDash)
            {
                if(active&&leftWingTip&&rightWingTip)
                {
                    // 일시정지 중 같은 점이 계속 누적되지 않도록 시간 간격을 둡니다.
                    if(wingStrokes.Count==0||Time.time-wingStrokes[wingStrokes.Count-1].time>=.008f)
                        wingStrokes.Add(new Stroke{a=leftWingTip.position,b=rightWingTip.position,time=Time.time});
                    if(Time.time>=nextEmission){nextEmission=Time.time+.05f;foreach(var point in new[]{leftWingTip.position,rightWingTip.position})Emit(point,-enemy.LockedDirection*1.5f+Vector3.up*.3f,0,.12f,.85f,leafColor);}
                }
                wingStrokes.RemoveAll(s=>Time.time-s.time>trailLifetime);
                for(int i=1;i<wingStrokes.Count;i++)
                {
                    var a=wingStrokes[i-1];var b=wingStrokes[i];float fade=1-(Time.time-a.time)/trailLifetime;
                    var width=Vector3.up*.055f*fade;
                    SoftQuad(a.a-width*3,a.a+width*3,b.a+width*3,b.a-width*3,Alpha(leafColor,trailOpacity*fade),4,i*.2f);
                    SoftQuad(a.b-width*3,a.b+width*3,b.b+width*3,b.b-width*3,Alpha(leafColor,trailOpacity*fade),4,i*.2f);
                    SoftQuad(a.a-width,a.a+width,b.a+width,b.a-width,Alpha(brightColor,.65f*fade),4,i*.2f);
                    SoftQuad(a.b-width,a.b+width,b.b+width,b.b-width,Alpha(brightColor,.65f*fade),4,i*.2f);
                }
            }
            RenderFlecks();previousPhase=phase;
            mesh.Clear();mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();surface.enabled=vertices.Count>0;
        }
        void Clear(){strokes.Clear();wingStrokes.Clear();flecks.Clear();if(surface)surface.enabled=false;}
        void OnDisable(){Clear();}
        void OnDestroy(){if(mesh)Destroy(mesh);}
    }
}
