using System.Collections.Generic;
using UnityEngine;

namespace DogLife
{
    [RequireComponent(typeof(MeshFilter),typeof(MeshRenderer))]
    public sealed partial class KrakenDeepVfx:MonoBehaviour
    {
        public enum Form{Band,Beam,Orb,Vortex,Sand,Burst,Sector}
        public Form Shape;
        public Vector3 Size=Vector3.one;
        public float Progress,Intensity=1;
        public bool Impact;
        Mesh mesh;
        Vector3 viewForward=Vector3.forward,viewRight=Vector3.right,viewUp=Vector3.up;
        readonly List<Vector3> v=new List<Vector3>(6000);
        readonly List<int> ix=new List<int>(9000);
        readonly List<Color> col=new List<Color>(6000);
        readonly List<Vector4> uv=new List<Vector4>(6000);
        static readonly Color Foam=new Color(.65f,.94f,.96f,.9f),Blue=new Color(.08f,.48f,.65f,.28f);
        public static KrakenDeepVfx Create(Transform parent,Material material,Form shape)
        {
            var go=new GameObject("Deep sea "+shape,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
            var r=go.GetComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;r.receiveShadows=false;
            var f=go.AddComponent<KrakenDeepVfx>();f.Shape=shape;return f;
        }
        void Awake(){mesh=new Mesh{name="Deep sea layered water"};mesh.MarkDynamic();GetComponent<MeshFilter>().sharedMesh=mesh;}
        void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color,float mode=5)
        {
            int n=v.Count;v.Add(a);v.Add(b);v.Add(c);v.Add(d);color.a*=Intensity;
            for(int j=0;j<4;j++)col.Add(color);
            uv.Add(new Vector4(0,0,mode,0));uv.Add(new Vector4(1,0,mode,0));uv.Add(new Vector4(1,1,mode,0));uv.Add(new Vector4(0,1,mode,0));
            ix.Add(n);ix.Add(n+1);ix.Add(n+2);ix.Add(n);ix.Add(n+2);ix.Add(n+3);
        }
        void Stroke(Vector3 a,Vector3 b,float width,Color c)
        {
            Vector3 side=Vector3.Cross(b-a,viewForward).normalized*width*.5f;
            Color glow=c;glow.a*=.18f;
            Quad(a-side*3,a+side*3,b+side*3,b-side*3,glow,8);
            Quad(a-side,a+side,b+side,b-side,c,6);
        }
        void Ring(Vector3 center,float radius,float width,Quaternion rotation,Color color)
        {
            for(int j=0;j<80;j++){
                float a=j*Mathf.PI/40,b=(j+1)*Mathf.PI/40;
                var d=rotation*new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var e=rotation*new Vector3(Mathf.Cos(b),0,Mathf.Sin(b));
                Quad(center+d*(radius-width),center+d*radius,center+e*radius,center+e*(radius-width),color,6);
            }
        }
        void LateUpdate()
        {
            v.Clear();ix.Clear();col.Clear();uv.Clear();mesh.Clear();float q=Mathf.Clamp01(Progress),time=Time.time;
            var camera=Camera.main;if(camera){viewForward=transform.InverseTransformDirection(camera.transform.forward);viewRight=transform.InverseTransformDirection(camera.transform.right);viewUp=transform.InverseTransformDirection(camera.transform.up);}
            Color edge=Color.Lerp(new Color(.15f,.65f,.82f,.7f),Foam,q);
            if(Shape==Form.Sector){DrawSector(q,time);}
            else if(Shape==Form.Band){
                Vector3 h=Size*.5f;float front=Mathf.Lerp(-h.z,h.z,q);
                for(int s=-1;s<=1;s+=2){float y=s*h.y;
                    Quad(new Vector3(-h.x,y,-h.z),new Vector3(h.x,y,-h.z),new Vector3(h.x,y,h.z),new Vector3(-h.x,y,h.z),new Color(.012f,.075f,.12f,.30f),4);
                    Quad(new Vector3(-h.x,y,-h.z),new Vector3(h.x,y,-h.z),new Vector3(h.x,y,front),new Vector3(-h.x,y,front),Impact?new Color(.32f,.8f,.9f,.5f):Blue);
                    Vector3 a=new Vector3(-h.x,y,-h.z),b=new Vector3(h.x,y,-h.z),c=new Vector3(h.x,y,h.z),d=new Vector3(-h.x,y,h.z);
                    Stroke(a,b,.08f,edge);Stroke(b,c,.08f,edge);Stroke(c,d,.08f,edge);Stroke(d,a,.08f,edge);
                    Stroke(new Vector3(-h.x,y,front),new Vector3(h.x,y,front),.14f,Foam);
                }
                DrawBandDetail(q,time);
            }else if(Shape==Form.Beam){
                float radius=Size.x*.5f;
                for(int i=0;i<24;i++){
                    float a=i*Mathf.PI/12,b=(i+1)*Mathf.PI/12;var d=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*radius;var e=new Vector3(Mathf.Cos(b),Mathf.Sin(b),0)*radius;
                    Quad(d,e,e+Vector3.forward*Size.z,d+Vector3.forward*Size.z,new Color(.06f,.36f,.52f,Impact?.32f:.07f),5);
                    if(!Impact)Quad(d,e,e+Vector3.forward*Size.z*q,d+Vector3.forward*Size.z*q,new Color(.18f,.64f,.74f,.2f),5);
                }
                Ring(Vector3.zero,radius,.07f,Quaternion.Euler(90,0,0),edge);Ring(Vector3.forward*Size.z,radius,.07f,Quaternion.Euler(90,0,0),edge);
                Ring(Vector3.forward*Size.z*q,radius,.1f,Quaternion.Euler(90,0,0),Foam);
                for(int i=0;i<3;i++)for(int j=0;j<32;j++){
                    float z=j*Size.z/32,z1=(j+1)*Size.z/32,a=i*Mathf.PI*2/3+z*.75f-time*(Impact?7:1.5f),b=i*Mathf.PI*2/3+z1*.75f-time*(Impact?7:1.5f);
                    Stroke(new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,z),new Vector3(Mathf.Cos(b)*radius,Mathf.Sin(b)*radius,z1),Impact?.14f:.045f,new Color(.6f,.93f,.95f,Impact?.8f:.4f));
                }
                DrawBeamDetail(q,time);
            }else if(Shape==Form.Sand){
                var cam=Camera.main;Vector3 right=cam?transform.InverseTransformDirection(cam.transform.right):Vector3.right,up=cam?transform.InverseTransformDirection(cam.transform.up):Vector3.up;
                for(int i=0;i<260;i++){
                    float seed=Mathf.Repeat(i*.618034f,1),cycle=Mathf.Repeat(time*.13f+seed,1),angle=i*2.39996f;
                    float r=Mathf.Sqrt(Mathf.Repeat(i*.754877f,1))*Size.x*(.35f+cycle*.7f);
                    Vector3 p=new Vector3(Mathf.Cos(angle)*r,cycle*Size.y,Mathf.Sin(angle)*r);
                    p+=new Vector3(Mathf.Sin(time*.55f+i),0,Mathf.Cos(time*.4f+i))*.3f;
                    float scale=Mathf.Lerp(.18f,1,seed)*(1+cycle);float alpha=Mathf.Sin(cycle*Mathf.PI)*.42f;
                    var c=Color.Lerp(new Color(.21f,.29f,.28f,alpha),new Color(.57f,.52f,.37f,alpha),seed);
                    Quad(p-right*scale-up*scale,p+right*scale-up*scale,p+right*scale+up*scale,p-right*scale+up*scale,c,3);
                }
                for(int i=0;i<100;i++){
                    float a=i*2.39996f,t=Mathf.Repeat(time*.18f+i*.618f,1),r=Mathf.Sqrt(Mathf.Repeat(i*.754f,1))*Size.x;
                    Vector3 p=new Vector3(Mathf.Cos(a)*r,t*Size.y*.6f,Mathf.Sin(a)*r);float s=.015f+(i%4)*.012f;
                    Quad(p-right*s-up*s,p+right*s-up*s,p+right*s+up*s,p-right*s+up*s,new Color(.62f,.57f,.43f,Mathf.Sin(t*Mathf.PI)*.6f),3);
                }
            }else{
                float radius=Size.x;
                int rings=Shape==Form.Vortex?0:3;
                for(int i=0;i<rings;i++){
                    float t=Mathf.Repeat(i/(float)rings+time*(Shape==Form.Vortex?-.25f:.15f),1);
                    float r=Shape==Form.Burst?radius*q:Shape==Form.Vortex?radius*t:radius*(.8f+.2f*Mathf.Sin(time*3+i));
                    Quaternion rot=Shape==Form.Orb||Shape==Form.Burst?Quaternion.Euler(i*30,time*12,0):Quaternion.identity;
                    Ring(Vector3.up*(Shape==Form.Vortex?(t-.5f)*Size.y:0),Mathf.Max(.1f,r),.06f,rot,new Color(.42f,.82f,.9f,(1-t)*.65f));
                }
                if(Shape==Form.Vortex)for(int i=0;i<5;i++)for(int j=0;j<32;j++){
                    float a=i*Mathf.PI*.4f+j*.14f-time*1.7f,b=a+.14f;float r=radius*j/32f;
                    Stroke(new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r),new Vector3(Mathf.Cos(b)*(r+radius/32),0,Mathf.Sin(b)*(r+radius/32)),.12f,Foam);
                }
                DrawPressureDetail(q,time);
            }
            mesh.SetVertices(v);mesh.SetTriangles(ix,0);mesh.SetColors(col);mesh.SetUVs(0,uv);mesh.RecalculateBounds();
        }
        void OnDestroy(){if(mesh)Destroy(mesh);}
    }
}
