using System.Collections.Generic;
using UnityEngine;
namespace DogLife
{
    // 모든 메시/번개는 표시 전용이며 피해는 SummerMonsterEnemy의 전용 판정에서만 발생합니다.
    [RequireComponent(typeof(MeshFilter),typeof(MeshRenderer))]
    [DefaultExecutionOrder(150)]
    public sealed class SummerWaterVfx:MonoBehaviour
    {
        public SummerMonsterEnemy enemy;
        public SummerHomingBubble bubble;
        public BubbleDebuff debuff;
        [SerializeField] Material material;
        Mesh mesh;MeshRenderer surface;float born;bool burst;
        Camera view;Renderer[] bodyRenderers;MaterialPropertyBlock glowBlock;CombatHealth health;
        struct Fleck { public Vector3 origin,velocity;public float born,life,size;public int kind; }
        readonly List<Fleck> flecks=new List<Fleck>(160);
        readonly Vector3[] boltPoints=new Vector3[19];
        SummerMonsterEnemy.Phase lastPhase;bool phaseSeen;float nextTrail;
        readonly List<Vector3> v=new List<Vector3>(6000);
        readonly List<Color> c=new List<Color>(6000);
        readonly List<int> ix=new List<int>(9000);
        static readonly Color Water=new Color(.24f,.75f,1,.75f),Bright=new Color(.7f,.97f,1,1),Tide=new Color(.12f,.56f,.78f,.95f);
        public void Initialize(Material mat){material=mat;if(surface)surface.sharedMaterial=mat;}
        void Awake(){born=Time.time;mesh=new Mesh{name="Summer water and telegraph"};mesh.MarkDynamic();GetComponent<MeshFilter>().sharedMesh=mesh;surface=GetComponent<MeshRenderer>();surface.sharedMaterial=material;surface.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;surface.receiveShadows=false;if(enemy){health=enemy.GetComponent<CombatHealth>();var animator=enemy.GetComponentInChildren<Animator>();if(animator)bodyRenderers=animator.GetComponentsInChildren<Renderer>();glowBlock=new MaterialPropertyBlock();}}
        public static void Burst(Vector3 p,Material mat){var go=new GameObject("BubblePopVFX");go.transform.position=p;var effect=go.AddComponent<SummerWaterVfx>();effect.Initialize(mat);effect.burst=true;Destroy(go,.4f);}
        void Tri(Vector3 a,Vector3 b,Vector3 d,Color color){int n=v.Count;v.Add(transform.InverseTransformPoint(a));v.Add(transform.InverseTransformPoint(b));v.Add(transform.InverseTransformPoint(d));c.Add(color);c.Add(color);c.Add(color);ix.Add(n);ix.Add(n+1);ix.Add(n+2);}
        void Quad(Vector3 a,Vector3 b,Vector3 d,Vector3 e,Color col){Tri(a,b,d,col);Tri(a,d,e,col);}
        void Ring(Vector3 center,float radius,float width,Color col,int count=72)
        {
            for(int i=0;i<count;i++){float a=i*Mathf.PI*2/count,b=(i+1)*Mathf.PI*2/count;var x=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var y=new Vector3(Mathf.Cos(b),0,Mathf.Sin(b));Quad(center+x*radius,center+y*radius,center+y*(radius-width),center+x*(radius-width),col);}
        }
        void Disc(Vector3 p,float r,Color col){for(int i=0;i<64;i++){float a=i*Mathf.PI/32,b=(i+1)*Mathf.PI/32;Tri(p,p+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*r,p+new Vector3(Mathf.Cos(b),0,Mathf.Sin(b))*r,col);}}
        void Stroke(Vector3 a,Vector3 b,float width,Color col)
        {
            Vector3 normal=view?view.transform.forward:Vector3.forward;
            Vector3 side=Vector3.Cross(b-a,normal).normalized*width*.5f;if(side.sqrMagnitude<.0001f)side=Vector3.right*width*.5f;
            Quad(a-side,a+side,b+side,b-side,col);
        }
        void Orb(Vector3 p,float r,float alpha)
        {
            var camera=view;Vector3 right=camera?camera.transform.right:Vector3.right,up=camera?camera.transform.up:Vector3.up;
            for(int i=0;i<40;i++)
            {
                float a=i*Mathf.PI/20,b=(i+1)*Mathf.PI/20;var d=right*Mathf.Cos(a)+up*Mathf.Sin(a);var e=right*Mathf.Cos(b)+up*Mathf.Sin(b);
                int n=c.Count;Tri(p,p+d*r,p+e*r,new Color(.2f,.65f,1,.08f*alpha));c[n+1]=c[n+2]=new Color(.25f,.7f,1,.35f*alpha);
                Quad(p+d*r,p+e*r,p+e*(r*.95f),p+d*(r*.95f),new Color(.6f,.92f,1,.85f*alpha));
                if(i>9&&i<17)Stroke(p+d*r*.72f,p+e*r*.72f,r*.10f,new Color(.85f,1,1,alpha));
            }
            Glow(p-right*r*.28f+up*r*.35f,r*.25f,new Color(.85f,1,1,.8f*alpha));
            Glow(p+right*r*.3f-up*r*.32f,r*.32f,new Color(.15f,.5f,1,.25f*alpha));
        }
        void Glow(Vector3 p,float radius,Color col)
        {
            Vector3 right=view?view.transform.right:Vector3.right,up=view?view.transform.up:Vector3.up;
            for(int i=0;i<24;i++){float a=i*Mathf.PI/12,b=(i+1)*Mathf.PI/12;int n=c.Count;Tri(p,p+(right*Mathf.Cos(a)+up*Mathf.Sin(a))*radius,p+(right*Mathf.Cos(b)+up*Mathf.Sin(b))*radius,col);var transparent=col;transparent.a=0;c[n+1]=c[n+2]=transparent;}
        }
        void WaterCrown(Vector3 p,float radius,float t)
        {
            float fade=Mathf.Sin(Mathf.Clamp01(t)*Mathf.PI);float spread=radius*(.2f+.8f*t);
            for(int i=0;i<96;i++)
            {
                float a=i*Mathf.PI/48,b=(i+1)*Mathf.PI/48;Vector3 d=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)),e=new Vector3(Mathf.Cos(b),0,Mathf.Sin(b));
                float h=(.14f+.44f*Mathf.Pow(Mathf.Sin(a*5+.4f),6))*fade,k=(.14f+.44f*Mathf.Pow(Mathf.Sin(b*5+.4f),6))*fade;
                Vector3 lipA=p+d*(spread+.18f)+Vector3.up*h,lipB=p+e*(spread+.18f)+Vector3.up*k;
                int n=c.Count;Quad(p+d*spread,p+e*spread,lipB,lipA,new Color(.18f,.62f,.82f,.10f*fade));
                c[n+2]=c[n+4]=c[n+5]=new Color(.6f,.92f,1,.50f*fade);
                Stroke(lipA,lipB,.025f,new Color(.8f,1,1,.65f*fade));
            }
        }
        void Spray(Vector3 center,float t,int amount,float radius)
        {
            for(int i=0;i<amount;i++){float a=i*2.39996f,r=radius*t*(.6f+.4f*Mathf.Sin(i*2));Vector3 p=center+new Vector3(Mathf.Cos(a)*r,Mathf.Sin(t*Mathf.PI)*(1.1f+.6f*Mathf.Sin(i)),Mathf.Sin(a)*r);Vector3 along=(Vector3.up*(1-2*t)+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*.5f).normalized;Stroke(p-along*.07f,p+along*.12f,.04f,new Color(.5f,.9f,1,1-t));Glow(p,.09f,new Color(.6f,.95f,1,.7f*(1-t)));}
        }
        static Color Alpha(Color color,float alpha){color.a=alpha;return color;}
        static float Noise(float seed)=>Mathf.Repeat(Mathf.Sin(seed*127.1f+311.7f)*43758.5453f,1);
        void FeatherStroke(Vector3 a,Vector3 b,float width,Color color)
        {
            Vector3 side=Vector3.Cross(b-a,view?view.transform.forward:Vector3.forward).normalized*width;
            int n=c.Count;Quad(a-side,a,b,b-side,color);c[n]=c[n+3]=c[n+5]=Alpha(color,0);
            n=c.Count;Quad(a,a+side,b+side,b,color);c[n+1]=c[n+2]=c[n+4]=Alpha(color,0);
        }
        void Bolt(Vector3 start,Vector3 end,int seed,float strength,float width=1)
        {
            if(strength<=.01f)return;boltPoints[0]=start;
            int flicker=Mathf.FloorToInt(Time.time*19);float length=Vector3.Distance(start,end);
            for(int j=1;j<=18;j++)
            {
                float u=j/18f,envelope=Mathf.Sin(u*Mathf.PI);float key=seed*31+j*13+flicker*7;
                var next=Vector3.Lerp(start,end,u)+new Vector3(Noise(key)-.5f,(Noise(key+4)-.5f)*.3f,Noise(key+9)-.5f)*envelope*Mathf.Min(.85f,length*.24f);
                boltPoints[j]=next;
                if(j==5||j==11)
                {
                    Vector3 branch=next+new Vector3(Noise(key+15)-.5f,-.25f,Noise(key+2)-.5f)*1.5f;
                    Vector3 mid=Vector3.Lerp(next,branch,.55f)+Vector3.up*.15f;
                    FeatherStroke(next,mid,.065f*width,new Color(.3f,.55f,1,.5f*strength));Stroke(next,mid,.014f*width,Alpha(Bright,.8f*strength));
                    FeatherStroke(mid,branch,.04f*width,new Color(.3f,.55f,1,.4f*strength));Stroke(mid,branch,.008f*width,Alpha(Bright,.6f*strength));
                }
            }
            BoltRibbon(.26f*width,new Color(.18f,.2f,1,.18f*strength));
            BoltRibbon(.085f*width,new Color(.20f,.65f,1,.70f*strength));
            BoltRibbon(.020f*width,new Color(.9f,.99f,1,strength));
        }
        void BoltRibbon(float width,Color color)
        {
            Vector3 lastSide=Vector3.zero;
            for(int i=0;i<19;i++)
            {
                Vector3 tangent=boltPoints[Mathf.Min(18,i+1)]-boltPoints[Mathf.Max(0,i-1)];
                Vector3 side=Vector3.Cross(tangent,view?view.transform.forward:Vector3.forward).normalized*width;
                if(i==0||i==18)side*=.3f;
                if(i>0)
                {
                    var a=boltPoints[i-1];var b=boltPoints[i];int n=c.Count;
                    Quad(a-lastSide,a,b,b-side,color);c[n]=c[n+3]=c[n+5]=Alpha(color,0);
                    n=c.Count;Quad(a,a+lastSide,b+side,b,color);c[n+1]=c[n+2]=c[n+4]=Alpha(color,0);
                }
                lastSide=side;
            }
        }
        void Emit(Vector3 center,int count,float force,int kind)
        {
            for(int i=0;i<count&&flecks.Count<150;i++)
            {
                float angle=i*2.399963f+Time.time;Vector3 direction=new Vector3(Mathf.Cos(angle),.55f+Noise(i+4)*1.3f,Mathf.Sin(angle));
                flecks.Add(new Fleck{origin=center,velocity=direction*force*(.6f+Noise(i+16)*.6f),born=Time.time,life=.45f+Noise(i+31)*.65f,size=.025f+Noise(i+2)*.08f,kind=kind});
            }
        }
        void RenderFlecks()
        {
            for(int i=flecks.Count-1;i>=0;i--)
            {
                var f=flecks[i];float age=Time.time-f.born,u=age/f.life;if(u>=1){flecks.RemoveAt(i);continue;}
                float alpha=Mathf.Clamp01(age/.035f)*Mathf.Pow(1-u,1.4f);
                var p=f.origin+f.velocity*age-Vector3.up*(f.kind==1?.5f:3.5f)*age*age;
                if(f.kind==1){Glow(p,f.size*3,new Color(.28f,.55f,1,alpha*.6f));Stroke(p,p-f.velocity*.018f,f.size*.4f,Alpha(Bright,alpha));}
                else if(f.kind==2)Glow(p,f.size*7*(1+age),new Color(.6f,.88f,.95f,alpha*.16f));
                else
                {
                    Vector3 up=view?view.transform.up:Vector3.up,right=view?view.transform.right:Vector3.right;
                    Tri(p+up*f.size*2,p-right*f.size*.5f,p+right*f.size*.5f,new Color(.42f,.82f,1,alpha));
                    Glow(p,f.size,new Color(.9f,1,1,alpha*.8f));
                }
            }
        }
        void FoamSpiral(Vector3 center,float radius,float time,float strength,bool inward)
        {
            for(int arm=0;arm<3;arm++)
            {
                Vector3 last=center;for(int j=0;j<30;j++)
                {
                    float u=j/29f,a=arm*Mathf.PI*2/3+u*4.5f+(inward?-time:time)*2;
                    var p=center+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*radius*u;
                    if(j>0)Stroke(last,p,.025f+.065f*u,new Color(.6f,.93f,1,strength*u*.5f));last=p;
                }
            }
            for(int i=0;i<28;i++)
            {
                float a=i*2.399963f+time*.3f;float r=radius*(.72f+.16f*Mathf.Sin(i*7));Vector3 d=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)),side=Vector3.Cross(Vector3.up,d);
                var p=center+d*r;float size=.035f+.025f*Noise(i);
                Quad(p-d*size,p+side*size*.7f,p+d*size,p-side*size*.7f,new Color(.8f,.99f,1,strength*.65f));
            }
        }
        void MermaidEffects(Vector3 center,float t,SummerMonsterEnemy.Phase phase)
        {
            if(phase==SummerMonsterEnemy.Phase.Dive)
            {
                center+=enemy.AttackForward*(Mathf.Sin(t*Mathf.PI)*.9f);
                float s=Mathf.Sin(t*Mathf.PI);FoamSpiral(center,1.15f,Time.time,.85f,true);WaterCrown(center,1.25f,Mathf.Clamp01(t*1.25f));
                Ring(center+Vector3.up*.015f,.45f+1.1f*t,.10f,Alpha(Bright,s*.8f));
            }
            else if(enemy.Submerged)
            {
                FoamSpiral(center,.8f+(.2f*Mathf.Sin(Time.time*3)),Time.time,.55f,true);
                if(phase==SummerMonsterEnemy.Phase.Warning)Glow(center+Vector3.up*.08f,.35f+.55f*t,new Color(.3f,.9f,1,.2f+.2f*t));
            }
            if(phase!=SummerMonsterEnemy.Phase.Attack)return;
            float u=Mathf.Clamp01((t-.18f)/.82f);float strength=Mathf.Sin(u*Mathf.PI);
            WaterCrown(center,enemy.Radius,u);FoamSpiral(center,enemy.Radius*(.35f+.6f*u),Time.time,strength,false);
            // Curved water sheets rise around the swimmer, opening into foam at the lip.
            for(int sheet=0;sheet<7;sheet++)
            {
                float angle=sheet*Mathf.PI*2/7;Vector3 last=center;Vector3 lastSide=Vector3.zero;
                for(int j=0;j<=14;j++)
                {
                    float q=j/14f,a=angle+q*.55f;Vector3 radial=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)),side=Vector3.Cross(Vector3.up,radial);
                    float r=(.28f+q*q*1.35f)*(.5f+u*.9f),height=Mathf.Sin(q*Mathf.PI*.85f)*(2.0f+Noise(sheet)*.7f)*strength;
                    var p=center+radial*r+Vector3.up*height;var edge=side*(.16f+.22f*q)*strength;
                    if(j>0){int n=c.Count;Quad(last-lastSide,last+lastSide,p+edge,p-edge,new Color(.12f,.55f,.76f,.3f*strength));c[n+1]=c[n+2]=c[n+4]=new Color(.58f,.93f,1,.55f*strength);Stroke(last+lastSide,p+edge,.035f,Alpha(Bright,strength*.85f));}
                    last=p;lastSide=edge;
                }
            }
            Glow(center+Vector3.up*.3f,.8f,new Color(.45f,.9f,1,Mathf.Exp(-Mathf.Abs(t-.3f)*20)*.65f));
        }
        void JellyEffects(Vector3 center,float t,bool warning)
        {
            var core=enemy.transform.position+Vector3.up*1.8f;
            float pulse=warning?.15f+.5f*t:.9f;
            Glow(core,.65f,new Color(.16f,.28f,1,pulse*.5f));Glow(core,.32f,new Color(.45f,.9f,1,pulse));
            if(warning)
            {
                for(int i=0;i<4;i++){float a=Time.time*2+i*Mathf.PI*.5f;Vector3 edge=core+new Vector3(Mathf.Cos(a),Mathf.Sin(a*2)*.25f,Mathf.Sin(a))*(.5f+.3f*t);Bolt(edge,core,i,.12f+.28f*t,.22f);}
                return;
            }
            for(int i=0;i<7;i++)
            {
                float age=t-(.23f+i*.009f);if(age<0)continue;
                float flash=Mathf.Clamp01((.80f-t)*3)*( .55f+.45f*Mathf.Pow(Mathf.Max(0,Mathf.Cos(age*31+i)),6));
                float angle=i*2.399963f;float radius=enemy.Radius*(i%2==0?.76f:.42f);
                Vector3 foot=center+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*radius;
                Vector3 sky=foot+Vector3.up*(5.5f+Noise(i+5)*1.8f)+new Vector3(.6f*Mathf.Sin(i),0,.6f*Mathf.Cos(i));
                float travel=Mathf.Clamp01(age/.065f);Bolt(sky,Vector3.Lerp(sky,foot,travel),i+3,flash,1.15f);
                if(travel>=1)
                {
                    Glow(foot+Vector3.up*.15f,.65f,new Color(.1f,.35f,1,flash*.5f));Glow(foot+Vector3.up*.08f,.24f,Alpha(Bright,flash));
                    Ring(foot,.2f+age*.85f,.025f,new Color(.4f,.8f,1,flash*.7f));
                    for(int j=0;j<4;j++){float a=angle+j*Mathf.PI/2;Vector3 d=new Vector3(Mathf.Cos(a),.10f,Mathf.Sin(a));FeatherStroke(foot,foot+d*.6f,.08f,new Color(.25f,.6f,1,flash*.5f));Stroke(foot,foot+d*.5f,.018f,Alpha(Bright,flash));}
                    Bolt(center+Vector3.up*.02f,foot,i+41,flash*.45f,.35f);
                }
            }
            if(t>.3f)Ring(center,enemy.Radius*Mathf.Clamp01((t-.3f)*2.6f),.095f,new Color(.3f,.7f,1,(1-t)*.55f));
        }
        void LateUpdate()
        {
            v.Clear();c.Clear();ix.Clear();float time=Time.time;view=Camera.main;
            if(enemy&&enemy.Kind==SummerMonsterEnemy.Species.Jellyfish&&bodyRenderers!=null)
            {
                float intensity=enemy.State==SummerMonsterEnemy.Phase.Warning?enemy.Progress*(.55f+.25f*Mathf.Sin(time*18)):enemy.State==SummerMonsterEnemy.Phase.Attack?1:0;
                foreach(var r in bodyRenderers)if(r){r.GetPropertyBlock(glowBlock);glowBlock.SetColor("_EmissionColor",new Color(.1f,.4f,1)*intensity);r.SetPropertyBlock(glowBlock);}
            }
            if(burst){float t=(time-born)/.4f;if(!phaseSeen){phaseSeen=true;Emit(transform.position,18,1.4f,0);}Orb(transform.position,.4f+t*.35f,1-t);Glow(transform.position,.6f,new Color(.6f,.95f,1,(1-t)*.4f));}
            else if(bubble){Orb(transform.position,SummerHomingBubble.Radius,1);if(time>=nextTrail){nextTrail=time+.075f;Emit(transform.position,1,.2f,0);}}
            else if(debuff&&debuff.Active)
            {
                Vector3 foot=debuff.EffectFootPosition;float fade=debuff.VisualOpacity;
                // A low, watery binding around the legs leaves the face and aim clear.
                Disc(foot,.72f,new Color(.025f,.19f,.32f,.16f*fade));
                Ring(foot,.72f,.04f,Alpha(Tide,.7f*fade));
                for(int band=0;band<2;band++)
                {
                    Vector3 last=foot;
                    for(int j=0;j<=80;j++)
                    {
                        float u=j/80f,a=u*Mathf.PI*2+time*.8f+band*Mathf.PI;
                        float radius=.62f+.045f*Mathf.Sin(a*3-time*2);
                        Vector3 p=foot+new Vector3(Mathf.Cos(a)*radius,.16f+band*.24f+.085f*Mathf.Sin(a*2+time),Mathf.Sin(a)*radius);
                        if(j>0){FeatherStroke(last,p,.09f,new Color(.08f,.58f,.82f,.24f*fade));Stroke(last,p,.022f,Alpha(Bright,.65f*fade));}last=p;
                    }
                }
                for(int i=0;i<12;i++)
                {
                    float rise=Mathf.Repeat(time*.28f+i*.618f,1),a=time*.65f+i*2.39996f;
                    float radius=.54f+.13f*Mathf.Sin(i*3+time*.7f);
                    Vector3 p=foot+new Vector3(Mathf.Cos(a)*radius,.12f+rise*1.45f,Mathf.Sin(a)*radius);
                    float opacity=Mathf.Sin(rise*Mathf.PI)*fade;
                    Orb(p,.09f+.08f*(.5f+.5f*Mathf.Sin(i*7)),opacity);
                    Glow(p,.22f,new Color(.10f,.55f,.92f,.16f*opacity));
                }
            }
            else if(enemy&&enemy.isActiveAndEnabled&&health&&!health.Dead)
            {
                var phase=enemy.State;float t=enemy.Progress;Vector3 center=enemy.WarningCenter;
                bool warning=phase==SummerMonsterEnemy.Phase.Warning,attack=phase==SummerMonsterEnemy.Phase.Attack;
                bool entered=!phaseSeen||phase!=lastPhase;
                if(entered){phaseSeen=true;lastPhase=phase;nextTrail=0;}
                if(entered&&phase==SummerMonsterEnemy.Phase.Dive){Emit(center,22,1.3f,0);Emit(center,8,.6f,2);}
                if(attack&&t>=.30f&&nextTrail==0)
                {
                    nextTrail=float.PositiveInfinity;
                    Emit(center,enemy.Kind==SummerMonsterEnemy.Species.Mermaid?56:28,enemy.Kind==SummerMonsterEnemy.Species.Mermaid?2.4f:1.5f,enemy.Kind==SummerMonsterEnemy.Species.Jellyfish?1:0);
                    Emit(center,12,.7f,2);
                }
                if((warning||attack)&&enemy.Kind!=SummerMonsterEnemy.Species.Crab)
                {
                    float radius=enemy.Radius;Ring(center,radius,.065f,warning?Tide:Bright);Disc(center,radius,new Color(.06f,.22f,.35f,warning?.10f+.08f*t:.15f));
                    Ring(center+Vector3.up*.008f,radius*(warning?Mathf.Lerp(.15f,1,t):1),.04f,Water);
                    for(int i=0;i<12;i++){float a=i*Mathf.PI/6;Vector3 radial=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));Stroke(center+radial*(radius-.16f),center+radial*(radius-.35f),.045f,Tide);}
                    for(int i=0;i<48;i++)if(i%4<2){float a=i*Mathf.PI/24,b=(i+1)*Mathf.PI/24;Vector3 d=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)),e=new Vector3(Mathf.Cos(b),0,Mathf.Sin(b));Quad(center+d*(radius-.22f),center+e*(radius-.22f),center+e*(radius-.245f),center+d*(radius-.245f),Alpha(Bright,.38f));}
                }
                if(enemy.Submerged)
                {
                    Vector3 p=enemy.SubmergedEffectPosition;Disc(p,.58f,new Color(.01f,.07f,.12f,.5f));
                    for(int i=0;i<3;i++){float q=Mathf.Repeat(time*.75f+i/3f,1);Ring(p+Vector3.up*(i*.005f),.3f+q*.65f,.035f,new Color(.35f,.8f,1,(1-q)*.75f));}
                    for(int i=0;i<5;i++){float a=i*2.4f+time;Orb(p+new Vector3(Mathf.Cos(a)*.45f,.12f+Mathf.Repeat(time*.5f+i*.2f,.3f),Mathf.Sin(a)*.45f),.045f,.7f);}
                }
                if(enemy.Kind==SummerMonsterEnemy.Species.Mermaid)MermaidEffects(center,t,phase);
                if(enemy.Kind==SummerMonsterEnemy.Species.Jellyfish&&(warning||attack))JellyEffects(center,t,warning);
                if(enemy.Kind==SummerMonsterEnemy.Species.Tortoise)
                {
                    if(enemy.Defending)
                    {
                        Ring(enemy.SurfaceCenter,1.1f,.045f,Tide);
                        for(int i=0;i<6;i++)
                        {
                            float a=i*Mathf.PI/3;Vector3 normal=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)),side=Vector3.Cross(Vector3.up,normal);
                            var origin=enemy.SurfaceCenter+normal*.8f+Vector3.up*.6f;
                            for(int j=0;j<6;j++){float x=j*Mathf.PI/3,y=(j+1)*Mathf.PI/3;var p=origin+side*Mathf.Cos(x)*.3f+Vector3.up*Mathf.Sin(x)*.3f;var q=origin+side*Mathf.Cos(y)*.3f+Vector3.up*Mathf.Sin(y)*.3f;Tri(origin,p,q,new Color(.025f,.22f,.38f,.14f));FeatherStroke(p,q,.05f,new Color(.12f,.68f,.85f,.35f));Stroke(p,q,.014f,Alpha(Tide,.75f));}
                        }
                    }
                    if(attack)
                    {
                        Vector3 right=Vector3.Cross(Vector3.up,enemy.AttackForward);float snap=Mathf.Exp(-Mathf.Abs(t-.43f)*9);
                        for(int jaw=-1;jaw<=1;jaw+=2){Vector3 last=center;for(int j=0;j<=18;j++){float a=Mathf.Lerp(-1.4f,1.4f,j/18f);var p=center+Vector3.up*(.7f+jaw*Mathf.Sin((t+.12f)*Mathf.PI)*.3f)+right*Mathf.Sin(a)*.6f+enemy.AttackForward*Mathf.Cos(a)*.4f;if(j>0){FeatherStroke(last,p,.1f,Alpha(Water,snap*.55f));Stroke(last,p,.028f,Alpha(Bright,snap));}last=p;}}
                    }
                }
                if(enemy.Kind==SummerMonsterEnemy.Species.Crab&&(warning||attack))
                {
                    Vector3 p=enemy.transform.position+Vector3.up*.9f+enemy.AttackForward*.4f,side=Vector3.Cross(Vector3.up,enemy.AttackForward);
                    if(warning)for(int i=0;i<3;i++){var point=p+side*((i-1)*.55f)+Vector3.up*(.15f*Mathf.Sin(time*4+i));Orb(point,.10f+.23f*t,.85f);Glow(point,.4f,new Color(.3f,.8f,1,.12f+.15f*t));for(int j=0;j<4;j++){float a=time*3+j*Mathf.PI/2;Glow(point+new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*(.5f*(1-t)+.1f),.05f,Alpha(Bright,.6f));}}
                }
            }
            RenderFlecks();mesh.Clear();mesh.SetVertices(v);mesh.SetColors(c);mesh.SetTriangles(ix,0);mesh.RecalculateBounds();surface.enabled=v.Count>0;
        }
        void OnDisable(){flecks.Clear();phaseSeen=false;if(bodyRenderers!=null&&glowBlock!=null)foreach(var r in bodyRenderers)if(r){r.GetPropertyBlock(glowBlock);glowBlock.SetColor("_EmissionColor",Color.black);r.SetPropertyBlock(glowBlock);}}
        void OnDestroy(){if(mesh)Destroy(mesh);}
    }
}
