using System.Collections.Generic;
using UnityEngine;
namespace DogLife
{
    [RequireComponent(typeof(MeshFilter),typeof(MeshRenderer))]
    public sealed class SpringBoarVfx:MonoBehaviour
    {
        public SpringBoarBoss boss;
        [SerializeField] private Material material;
        float VisualScale=>1;
        Mesh mesh;MeshRenderer surface;CombatHealth health;
        readonly List<Vector3> vertices=new List<Vector3>(12000);
        readonly List<Color> colors=new List<Color>(12000);
        readonly List<int> indices=new List<int>(18000);
        [Header("봄맵 벚꽃 색감")]
        [SerializeField] private Color Ivory=new Color(.97f,.92f,.82f),Blush=new Color(.91f,.73f,.77f),Deep=new Color(.43f,.34f,.31f);
        [SerializeField,Range(0,1)] private float petalOpacity=.65f;
        Transform view;
        static readonly Vector2[] PetalOutline={new Vector2(0,0),new Vector2(-.24f,.20f),new Vector2(-.38f,.48f),new Vector2(-.35f,.73f),new Vector2(-.20f,.94f),new Vector2(-.07f,1),new Vector2(0,.88f),new Vector2(.07f,1),new Vector2(.20f,.94f),new Vector2(.35f,.73f),new Vector2(.38f,.48f),new Vector2(.24f,.20f)};
        public void Initialize(Material mat){material=mat;if(surface)surface.sharedMaterial=mat;}
        void Awake(){mesh=new Mesh{name="Spring sakura petals and bone warning"};mesh.MarkDynamic();GetComponent<MeshFilter>().sharedMesh=mesh;surface=GetComponent<MeshRenderer>();surface.sharedMaterial=material;surface.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;surface.receiveShadows=false;if(boss)health=boss.GetComponent<CombatHealth>();}
        static Color A(Color color,float alpha){color.a=alpha;return color;}
        void Tri(Vector3 a,Vector3 b,Vector3 c,Color color){int n=vertices.Count;vertices.Add(transform.InverseTransformPoint(a));vertices.Add(transform.InverseTransformPoint(b));vertices.Add(transform.InverseTransformPoint(c));colors.Add(color);colors.Add(color);colors.Add(color);indices.Add(n);indices.Add(n+1);indices.Add(n+2);}
        void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color){Tri(a,b,c,color);Tri(a,c,d,color);}
        void Line(Vector3 a,Vector3 b,float width,Color color){Vector3 s=Vector3.Cross(b-a,Vector3.up).normalized*width*.5f;if(s.sqrMagnitude<.000001f)s=Vector3.right*width*.5f;Quad(a-s,a+s,b+s,b-s,color);}
        void Crystal(Vector3 p,float height,float width,float alpha,float seed)
        {
            height*=VisualScale;width*=VisualScale;
            if(height<.01f)return;
            Vector3 tip=p+new Vector3(Mathf.Sin(seed)*height*.25f,height,Mathf.Cos(seed)*height*.18f);
            for(int i=0;i<5;i++)
            {
                float a=i*Mathf.PI*2/5,b=(i+1)*Mathf.PI*2/5;
                Vector3 x=p+new Vector3(Mathf.Cos(a),.08f,Mathf.Sin(a))*width,y=p+new Vector3(Mathf.Cos(b),.08f,Mathf.Sin(b))*width;
                Vector3 mid=(tip-p)*.60f,xx=x+mid,yy=y+mid;
                Quad(x,y,yy,xx,A(Color.Lerp(new Color(.28f,.18f,.12f),new Color(.93f,.82f,.62f),.55f+.18f*(i%3)),alpha*.85f));
                Tri(xx,yy,tip,A(i%2==0?new Color(.78f,.66f,.48f):Ivory,alpha*.88f));
                Line(xx,tip,.008f,A(Ivory,alpha*.75f));
                Line(x,xx,.006f,A(Ivory,alpha*.4f));
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
                Vector3 center=p+along*.48f;
                for(int j=0;j<PetalOutline.Length;j++){
                    Vector2 v=PetalOutline[j],w=PetalOutline[(j+1)%PetalOutline.Length];
                    Tri(center,p+across*v.x+along*v.y,p+across*w.x+along*w.y,A(Color.Lerp(Blush,Ivory,.25f+.35f*v.y),alpha));
                }
                Vector3 stamen=p+along*.18f;Vector3 dotX=axisX*size*.025f,dotY=axisY*size*.025f;
                Quad(stamen-dotX,stamen+dotY,stamen+dotX,stamen-dotY,A(Ivory,alpha));
            }
        }
        void Warning(Vector3 p,Vector3 forward,float radius,float angle,float progress)
        {
            p+=Vector3.up*.10f;int segments=48;
            for(int i=0;i<segments;i++)Tri(p,p+Dir(forward,-angle/2+angle*i/segments)*radius,p+Dir(forward,-angle/2+angle*(i+1)/segments)*radius,A(Blush,.09f));
            Arc(p,forward,radius,angle,.11f,A(Deep,.55f));Arc(p+Vector3.up*.005f,forward,radius-.025f,angle,.045f,A(Ivory,.82f));
            Arc(p+Vector3.up*.009f,forward,radius*Mathf.Lerp(.2f,.98f,progress),angle,.055f,A(Blush,.65f));
            if(angle<359){Line(p,p+Dir(forward,-angle/2)*radius,.055f,A(Ivory,.9f));Line(p,p+Dir(forward,angle/2)*radius,.055f,A(Ivory,.9f));}
            for(int i=0;i<8;i++){float a=-angle/2+angle*(i+.5f)/8;Vector3 d=Dir(forward,a);Line(p+d*(radius-.2f),p+d*(radius-.4f),.035f,A(Blush,.8f));}
            Sakura(p+forward*radius*.48f,.42f+.1f*progress,.7f,0);
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
            float time=Time.time;
            Vector3 axisX=view?view.right:Vector3.right,axisY=view?view.up:Vector3.up;
            for(int i=0;i<30;i++){
                float f=Mathf.Repeat(time*(.25f+i%3*.06f)+i*.137f,1);float a=i*2.39996f+time*.4f;
                Vector3 p=origin+new Vector3(Mathf.Cos(a)*spread*f,height*f,Mathf.Sin(a)*spread*f);
                float fade=Mathf.Clamp01(f*8)*(1-f)*strength*petalOpacity;
                Blossom(p,axisX,axisY,.14f+i%3*.035f,fade,i*29+time*(i%2==0?24:-18));
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
        void LateUpdate()
        {
            if(!view&&Camera.main)view=Camera.main.transform;
            vertices.Clear();colors.Clear();indices.Clear();
            if(boss&&boss.isActiveAndEnabled&&health&&!health.Dead){
                var state=boss.State;var pattern=boss.AttackPattern;float t=boss.Progress;
                Vector3 p=boss.transform.position+Vector3.up*.12f,forward=boss.Forward,right=Vector3.Cross(Vector3.up,forward);
                if(state==SpringBoarBoss.Phase.Warning){
                    if(pattern==SpringBoarBoss.Pattern.Charge){
                        Vector3 front=p+forward*boss.HornOffset,end=front+forward*boss.ChargeLength;float w=boss.ChargeWidth;
                        Quad(front-right*w,front+right*w,end+right*w,end-right*w,A(Blush,.09f));
                        Line(front-right*w,end-right*w,.12f,A(Blush,.70f));Line(front+right*w,end+right*w,.12f,A(Blush,.70f));
                        for(int j=1;j<10;j++){Vector3 q=front+forward*(j*boss.ChargeLength/10);Line(q-right*w*.7f,q+forward,.10f,A(Ivory,.8f));Line(q+right*w*.7f,q+forward,.10f,A(Ivory,.8f));}
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
                        Shards(p+forward*4,Mathf.Repeat(Time.time*3,1),3);
                    }else if(pattern==SpringBoarBoss.Pattern.Tusk){
                        float rise=Mathf.InverseLerp(.20f,.78f,t),alpha=Mathf.Sin(rise*Mathf.PI);
                        for(int j=0;j<30;j++)for(int k=0;k<12;k++){
                            float angle=-boss.TuskAngle/2+boss.TuskAngle*j/30f,a=(rise-k*.015f)*Mathf.PI*.7f,b=a-.04f;if(a<0)continue;
                            Vector3 d=Dir(forward,angle),e=Dir(forward,angle+boss.TuskAngle/30);
                            Quad(p+d*Mathf.Cos(a)*boss.TuskRange+Vector3.up*Mathf.Sin(a)*5,p+e*Mathf.Cos(a)*boss.TuskRange+Vector3.up*Mathf.Sin(a)*5,p+e*Mathf.Cos(b)*boss.TuskRange+Vector3.up*Mathf.Sin(b)*5,p+d*Mathf.Cos(b)*boss.TuskRange+Vector3.up*Mathf.Sin(b)*5,A(k<2?Ivory:Blush,alpha*(1-k/12f)*.30f));
                        }Petals(p+forward*3,4,5,alpha);
                    }else if(pattern==SpringBoarBoss.Pattern.Slam&&t>=.30f){
                        float grow=Mathf.InverseLerp(.30f,.60f,t),alpha=1-Mathf.InverseLerp(.72f,1,t);
                        Arc(p,forward,boss.SlamRadius*grow,360,.30f,A(Blush,alpha*.65f));Arc(p+Vector3.up*.06f,forward,boss.SlamRadius*grow,360,.12f,A(Ivory,alpha));
                        Cracks(p,forward,boss.SlamRadius,360,grow,alpha);Shards(p,Mathf.InverseLerp(.30f,1,t),boss.SlamRadius);Petals(p,boss.SlamRadius,4,alpha);
                    }else if(pattern==SpringBoarBoss.Pattern.Spikes){
                        float grow=Mathf.SmoothStep(0,1,Mathf.Clamp01(t/.12f)),fade=1-Mathf.InverseLerp(.8f,1,t);
                        for(int i=0;i<boss.SpikeCount;i++){BoneSpike(boss.SpikePoint(i),grow,fade,i);Petals(boss.SpikePoint(i),1.4f,4,fade);}
                    }
                }else if(state==SpringBoarBoss.Phase.Stunned){
                    if(t<.3f)Shards(p+forward*3,t/.3f,5);
                    for(int i=0;i<5;i++){Vector3 q=p+forward*3+Vector3.up*5+Dir(forward,Time.time*90+i*72)*1.5f;Sakura(q,.38f,1,i*20);}
                    Petals(p+forward*3,2,2,.5f);
                }
            }
            mesh.Clear();mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();
        }
        void OnDisable(){if(mesh)mesh.Clear();}
        void OnDestroy(){if(mesh)Destroy(mesh);}
    }
}
