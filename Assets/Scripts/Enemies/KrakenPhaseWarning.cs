using System.Collections.Generic;
using UnityEngine;
namespace DogLife
{
    [RequireComponent(typeof(MeshFilter),typeof(MeshRenderer))]
    public sealed class KrakenPhaseWarning:MonoBehaviour
    {
        public enum Shape{None,Rectangle,Circle,Sector,Ripple,Impact,Water,SlamImpact,SweepTrail}
        public Shape Mode;public float Length,Width,Angle=360,Progress;
        Mesh mesh;readonly List<Vector3> vertices=new List<Vector3>(4096);readonly List<int> indices=new List<int>(6144);readonly List<Color> colors=new List<Color>(4096);readonly List<Vector4> uv=new List<Vector4>(4096);
        static readonly Color Deep=new Color(.015f,.075f,.12f,.5f),Fill=new Color(.12f,.56f,.66f,.65f),Foam=new Color(.72f,.94f,.92f,.92f);
        public static KrakenPhaseWarning Create(Transform parent,Material material)
        {
            var go=new GameObject("Ocean warning and foam",typeof(MeshFilter),typeof(MeshRenderer),typeof(KrakenPhaseWarning));go.transform.SetParent(parent,false);var r=go.GetComponent<MeshRenderer>();r.sharedMaterial=material;r.sortingOrder=30;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;r.receiveShadows=false;return go.GetComponent<KrakenPhaseWarning>();
        }
        void Awake(){mesh=new Mesh{name="Kraken filled warning"};mesh.MarkDynamic();GetComponent<MeshFilter>().sharedMesh=mesh;}
        public void Show(Shape shape,Vector3 position,Quaternion rotation,float length,float width,float progress,float angle=360){Mode=shape;transform.SetPositionAndRotation(position,rotation);if(transform.parent){Vector3 s=transform.parent.lossyScale;transform.localScale=new Vector3(1/s.x,1/s.y,1/s.z);}Length=length;Width=width;Progress=progress;Angle=angle;}
        public void Hide(){Mode=Shape.None;if(mesh)mesh.Clear();}
        void Tri(Vector3 a,Vector3 b,Vector3 c,Color color,float kind=0){int n=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);for(int i=0;i<3;i++){indices.Add(n+i);colors.Add(color);uv.Add(new Vector4(i==0?0:1,i==2?1:0,kind,0));}}
        void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color,float kind=0){int n=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);vertices.Add(d);for(int i=0;i<4;i++)colors.Add(color);uv.Add(new Vector4(0,0,kind,0));uv.Add(new Vector4(1,0,kind,0));uv.Add(new Vector4(1,1,kind,0));uv.Add(new Vector4(0,1,kind,0));indices.Add(n);indices.Add(n+1);indices.Add(n+2);indices.Add(n);indices.Add(n+2);indices.Add(n+3);}
        Vector3 D(float a){a*=Mathf.Deg2Rad;return new Vector3(Mathf.Sin(a),0,Mathf.Cos(a));}
        void Ring(float inner,float outer,float angle,Color c,float kind=0)
        {for(int i=0;i<72;i++){Vector3 a=D(-Angle*.5f+angle*i/72),b=D(-Angle*.5f+angle*(i+1)/72);Quad(a*inner,a*outer,b*outer,b*inner,c,kind);}}
        void Rect(float length,float width,Color c,float kind=0){Quad(new Vector3(-width/2,0,0),new Vector3(width/2,0,0),new Vector3(width/2,0,length),new Vector3(-width/2,0,length),c,kind);}
        void Stroke(Vector3 a,Vector3 b,float width,Color color,float kind=6)
        {
            Vector3 side=Vector3.Cross(Vector3.up,(b-a).normalized)*width*.5f;
            a.y=b.y=.035f;Quad(a-side,a+side,b+side,b-side,color,kind);
        }
        void RectangleWarning(float q)
        {
            Rect(Length,Width,Deep,4);Rect(Length*q,Width,Fill,5);
            float half=Width*.5f-.12f;
            var a=new Vector3(-half,0,.12f);var b=new Vector3(half,0,.12f);var c=new Vector3(half,0,Length-.12f);var d=new Vector3(-half,0,Length-.12f);
            Color edge=Color.Lerp(new Color(.2f,.7f,.8f,.8f),Foam,Mathf.SmoothStep(0,1,q));
            Stroke(a,b,.09f,edge);Stroke(b,c,.09f,edge);Stroke(c,d,.09f,edge);Stroke(d,a,.09f,edge);
            float front=Mathf.Clamp(Length*q,.18f,Length-.18f);
            for(int i=0;i<32;i++){
                float x0=Mathf.Lerp(-half,half,i/32f),x1=Mathf.Lerp(-half,half,(i+1)/32f);
                float z0=Mathf.Clamp(front+Mathf.Sin(x0*2.4f+Time.time*2)*.1f,.08f,Length-.08f),z1=Mathf.Clamp(front+Mathf.Sin(x1*2.4f+Time.time*2)*.1f,.08f,Length-.08f);
                Stroke(new Vector3(x0,0,z0),new Vector3(x1,0,z1),.18f,Foam);
            }
            if(q>.8f)Rect(Length,Width,new Color(.45f,.8f,.85f,(q-.8f)*.4f),5);
        }
        void LateUpdate()
        {
            if(Mode==Shape.Impact||Mode==Shape.SlamImpact){Progress+=Time.deltaTime*1.4f;if(Progress>=1)Mode=Shape.None;}
            vertices.Clear();indices.Clear();colors.Clear();uv.Clear();mesh.Clear();if(Mode==Shape.None)return;
            float q=Mathf.Clamp01(Progress);
            if(Mode==Shape.Rectangle)RectangleWarning(q);
            else if(Mode==Shape.SweepTrail){
                float head=-Angle*.5f+q*Angle;
                for(int i=0;i<24;i++){float a=head-i*1.2f,b=a-1.2f;if(b<-Angle*.5f)break;float fade=1-i/24f;
                    Quad(D(a)*.7f,D(a)*Length,D(b)*Length,D(b)*.7f,new Color(.19f,.67f,.73f,fade*.4f),5);
                    Quad(D(a)*(Length-.3f),D(a)*Length,D(b)*Length,D(b)*(Length-.3f),new Color(.76f,.98f,.96f,fade*.85f),2);
                }
                Stroke(D(head)*.6f,D(head)*Length,.15f,Foam);
            }
            else if(Mode==Shape.Circle||Mode==Shape.Sector){Ring(0,Length,Angle,Deep,4);Ring(0,Length*Mathf.Sqrt(q),Angle,Fill,5);Ring(Mathf.Max(0,Length-.11f),Length,Angle,Foam,6);float front=Length*Mathf.Sqrt(q);Ring(Mathf.Max(0,front-.16f),front,Angle,Foam,6);if(Mode==Shape.Sector){Stroke(Vector3.zero,D(-Angle*.5f)*(Length-.1f),.09f,Foam);Stroke(Vector3.zero,D(Angle*.5f)*(Length-.1f),.09f,Foam);}}
            else if(Mode==Shape.SlamImpact){
                Rect(Length,Width,new Color(.16f,.57f,.66f,(1-q)*.65f),1);
                for(int i=0;i<28;i++){
                    float z=(i+.5f)*Length/28;float h=Mathf.Sin(q*Mathf.PI)*(.6f+(i%4)*.27f);
                    for(int s=-1;s<=1;s+=2){float x=s*Width*.5f*Mathf.Lerp(.2f,.95f,q);Vector3 p=new Vector3(x,.02f,z);
                        Quad(p-Vector3.forward*.34f,p+Vector3.forward*.34f,p+Vector3.forward*.18f+Vector3.up*h+Vector3.right*s*.25f,p-Vector3.forward*.18f+Vector3.up*h+Vector3.right*s*.25f,new Color(.68f,.92f,.91f,(1-q)*.8f),2);
                    }
                }
            }
            else if(Mode==Shape.Water){Rect(Length,Width,new Color(.025f,.34f,.42f,.66f),1);for(int i=0;i<16;i++){float z=Mathf.Repeat(i*Length/16+Time.time*.3f,Length);Quad(new Vector3(-Width/2,.025f,z),new Vector3(Width/2,.025f,z+.3f),new Vector3(Width/2,.025f,z+.38f),new Vector3(-Width/2,.025f,z+.08f),new Color(.4f,.86f,.9f,.13f),1);}}
            else
            {
                float t=Mode==Shape.Impact?q:Mathf.Repeat(Time.time*.45f,1);
                Ring(Length*(.4f+t*.65f),Length*(.43f+t*.65f),360,new Color(.65f,.97f,1,(1-t)*.7f),2);
                Ring(Length*.65f,Length*.71f,360,new Color(.08f,.7f,.8f,.4f),1);
                if(Mode==Shape.Impact)for(int i=0;i<36;i++){float a=i*10;Vector3 p=D(a)*Length*(.5f+q*.6f);float h=Mathf.Sin(q*Mathf.PI)*(.7f+(i%5)*.3f);Quad(p-D(a+90)*.12f,p+D(a+90)*.12f,p+D(a+90)*.06f+Vector3.up*h,p-D(a+90)*.06f+Vector3.up*h,new Color(.5f,.95f,1,(1-q)*.65f),2);}
            }
            mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();
        }
        void OnDestroy(){if(mesh)Destroy(mesh);}
    }
}
