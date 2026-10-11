using System.Collections.Generic;
using UnityEngine;

namespace DogLife
{
    [RequireComponent(typeof(MeshFilter),typeof(MeshRenderer))]
    public sealed class SpringBoarMetamorphosisVfx : MonoBehaviour
    {
        [SerializeField] Material material;
        [SerializeField] Transform chest,head;
        [SerializeField] Transform leftEye,rightEye;
        [SerializeField,Range(40,300)] int petalCount=180;
        [SerializeField,Range(3,12)] int rootCount=7;
        [SerializeField,Min(.1f)] float petalSize=.16f,inwardRadius=9,outwardRadius=12;
        [SerializeField] Color pink=new Color(.95f,.64f,.72f),ivory=new Color(1,.89f,.8f),bark=new Color(.29f,.18f,.15f);
        Mesh mesh;
        readonly List<Vector3> vertices=new List<Vector3>(16000);
        readonly List<Color> colors=new List<Color>(16000);
        readonly List<Vector2> uv=new List<Vector2>(16000);
        readonly List<int> triangles=new List<int>(24000);
        float progress;
        bool running,finished;
        public float DeathOpacity {get;set;}=1;
        static readonly Vector2[] outline={new Vector2(0,0),new Vector2(-.3f,.23f),new Vector2(-.47f,.65f),new Vector2(-.35f,.94f),new Vector2(-.12f,1),new Vector2(0,.84f),new Vector2(.12f,1),new Vector2(.35f,.94f),new Vector2(.47f,.65f),new Vector2(.3f,.23f)};
        void Awake()
        {
            mesh=new Mesh {name="Spring metamorphosis petals roots and mist"};mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
        }
        public void Begin() { running=true;finished=false;progress=0; }
        public void SetProgress(float value) { progress=value; }
        public void Finish() { running=false;finished=true;progress=1; }
        public void Clear() { running=finished=false;if(mesh)mesh.Clear(); }
        static Color Alpha(Color c,float alpha) { c.a=alpha;return c; }
        void Tri(Vector3 a,Vector3 b,Vector3 c,Color color,Vector2 ta,Vector2 tb,Vector2 tc)
        {
            int n=vertices.Count;vertices.Add(transform.InverseTransformPoint(a));vertices.Add(transform.InverseTransformPoint(b));vertices.Add(transform.InverseTransformPoint(c));
            colors.Add(color);colors.Add(color);colors.Add(color);uv.Add(ta);uv.Add(tb);uv.Add(tc);
            triangles.Add(n);triangles.Add(n+1);triangles.Add(n+2);
        }
        void Tri(Vector3 a,Vector3 b,Vector3 c,Color color) { Tri(a,b,c,color,Vector2.one*2,Vector2.one*2,Vector2.one*2); }
        void Glow(Vector3 p,Vector3 right,Vector3 up,float size,Color color)
        {
            right*=size;up*=size;
            Tri(p-right-up,p+right-up,p+right+up,color,Vector2.zero,Vector2.right,Vector2.one);
            Tri(p-right-up,p+right+up,p-right+up,color,Vector2.zero,Vector2.one,Vector2.up);
        }
        void Petal(Vector3 p,Vector3 right,Vector3 up,float size,float angle,Color tint)
        {
            float a=angle*Mathf.Deg2Rad;
            Vector3 x=(right*Mathf.Cos(a)+up*Mathf.Sin(a))*size;
            Vector3 y=(-right*Mathf.Sin(a)+up*Mathf.Cos(a))*size;
            for(int i=0;i<outline.Length;i++)
            {
                Vector2 b=outline[i],c=outline[(i+1)%outline.Length];
                Tri(p+y*.43f,p+x*b.x+y*b.y,p+x*c.x+y*c.y,tint);
                var light=Color.Lerp(tint,ivory,.28f);light.a=tint.a;
                colors[colors.Count-3]=light;
            }
        }
        void Ribbon(Vector3 a,Vector3 b,float width,Color color)
        {
            Vector3 side=Vector3.Cross(b-a,Vector3.up).normalized*width;
            if(side.sqrMagnitude<.000001f)side=transform.right*width;
            Tri(a-side,b-side,b+side,color);Tri(a-side,b+side,a+side,color);
        }
        void LateUpdate()
        {
            if(!mesh||(!running&&!finished))return;
            var camera=Camera.main;
            Vector3 right=camera?camera.transform.right:Vector3.right,up=camera?camera.transform.up:Vector3.up;
            Vector3 forward=transform.forward,side=transform.right;
            float scale=transform.lossyScale.x/10f;
            Vector3 origin=transform.position;
            Vector3 center=chest?chest.position-forward*scale:origin+Vector3.up*4*scale;
            vertices.Clear();colors.Clear();triangles.Clear();uv.Clear();
            if(running)
            {
                float charge=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.19f,.39f,progress))*(1-Mathf.InverseLerp(.60f,.76f,progress));
                // Root strands follow the rib cage as the boss rears up.
                for(int r=0;r<rootCount;r++)
                {
                    Vector3 last=Vector3.zero;
                    for(int j=0;j<25;j++)
                    {
                        float t=j/24f,a=r*6.28318f/rootCount+t*2.1f+Mathf.Sin(t*9+r)*.24f;
                        Vector3 p=center+forward*(t-.5f)*5*scale+side*Mathf.Cos(a)*(1.8f+Mathf.Sin(t*12+r)*.12f)*scale+Vector3.up*Mathf.Sin(a)*1.5f*scale;
                        float width=Mathf.Lerp(.11f,.025f,t)*scale;
                        if(j>0)
                        {
                            Ribbon(last,p,width,Alpha(bark,charge));
                            Ribbon(last+up*.015f,p+up*.015f,width*.17f,Alpha(pink,charge*.65f));
                            if(j%6==0)
                            {
                                Vector3 tip=p+forward*.4f*scale+Vector3.up*Mathf.Sin(r+j)*.35f*scale;
                                Ribbon(p,tip,width*.5f,Alpha(bark,charge));
                            }
                        }
                        last=p;
                    }
                }
                for(int i=0;i<petalCount;i++)
                {
                    float seed=Mathf.Repeat(Mathf.Sin(i*78.233f)*43758.5453f,1),angle=i*2.39996f;
                    float inward=Mathf.Clamp01((progress-.19f)/.34f);
                    float burst=Mathf.Clamp01((progress-.53f)/.3f);
                    float radius=progress<.53f?Mathf.Lerp(inwardRadius,1.7f,inward):Mathf.Lerp(1.7f,outwardRadius,1-Mathf.Pow(1-burst,2));
                    radius*=.65f+seed*.5f;angle+=progress<.53f?inward*4.5f:2.4f+burst*1.5f;
                    Vector3 p=center+(side*Mathf.Cos(angle)+forward*Mathf.Sin(angle))*radius*scale;
                    p+=Vector3.up*((seed-.5f)*5+Mathf.Sin(progress*13+i)*.25f+burst*1.2f)*scale;
                    float alpha=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.18f,.30f,progress))*(1-Mathf.InverseLerp(.72f,.92f,progress));
                    Color tint=Alpha(Color.Lerp(pink,ivory,seed),alpha*.85f);
                    float size=petalSize*(.7f+seed)*scale;
                    Petal(p,right,up,size,angle*57+progress*420,tint);
                    if(i%19==0) for(int j=1;j<5;j++)Petal(p,right,up,size,angle*57+j*72,tint);
                    if(i%4==0)Glow(p,right,up,size*.55f,Alpha(ivory,alpha*.25f));
                }
                // Landing: a low cloud and scattered petals, without an attack warning disc.
                float wave=Mathf.Clamp01((progress-.79f)/.21f);
                if(wave>0)
                {
                    for(int i=0;i<64;i++)
                    {
                        float angle=i*2.39996f,radius=(2+wave*11)*( .75f+(i%7)*.04f)*scale;
                        Vector3 p=origin+(side*Mathf.Cos(angle)+forward*Mathf.Sin(angle))*radius+Vector3.up*(.12f+Mathf.Sin(wave*Mathf.PI)*(i%5)*.16f)*scale;
                        float fade=Mathf.Sin(wave*Mathf.PI);
                        Petal(p,right,up,petalSize*1.4f*scale,i*31+wave*360,Alpha(i%2==0?pink:ivory,fade*.8f));
                        if(i%3==0)Glow(p,right,up,(.5f+wave)*scale,Alpha(new Color(.56f,.44f,.38f),fade*.18f));
                    }
                }
            }
            if(head && progress>.74f)
            {
                float alpha=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.74f,.9f,progress))*DeathOpacity;
                for(int sign=-1;sign<=1;sign+=2)
                {
                    var anchor=sign<0?leftEye:rightEye;
                    Vector3 eye=anchor?anchor.position:head.position+forward*1.25f*scale+side*sign*1.35f*scale;
                    Glow(eye,right,up,.20f*scale,Alpha(pink,alpha*.65f));
                    Glow(eye,right,up,.075f*scale,Alpha(ivory,alpha*.85f));
                }
            }
            mesh.Clear();mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();
        }
        void OnDisable() { Clear(); }
        void OnDestroy() { if(mesh)Destroy(mesh); }
    }
}
