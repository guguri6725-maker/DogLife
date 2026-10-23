using System.Collections.Generic;
using UnityEngine;

namespace DogLife
{
    [RequireComponent(typeof(LineRenderer))]
    public sealed class BlossomTelegraphVfx : MonoBehaviour
    {
        [SerializeField] private bool corridor;
        [SerializeField] private float warningDuration=1.42f;
        [SerializeField] private Material material;
        LineRenderer outline;
        Mesh mesh;
        MeshRenderer surface;
        float began;
        bool wasVisible;
        readonly List<Vector3> clipped=new List<Vector3>(40);
        readonly List<Vector3> vertices=new List<Vector3>(1024);
        readonly List<Color> colors=new List<Color>(1024);
        readonly List<int> indices=new List<int>(2048);
        void Ensure()
        {
            if(!outline)outline=GetComponent<LineRenderer>();
            if(mesh)return;
            var child=new GameObject("BlossomWarningDecoration");child.layer=gameObject.layer;child.transform.SetParent(transform,false);
            mesh=new Mesh{name="Blossom telegraph"};mesh.MarkDynamic();child.AddComponent<MeshFilter>().sharedMesh=mesh;
            surface=child.AddComponent<MeshRenderer>();surface.sharedMaterial=material;
            surface.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;surface.receiveShadows=false;
        }
        void Triangle(Vector3 a,Vector3 b,Vector3 c,Color color)
        {
            int n=vertices.Count;
            vertices.Add(transform.InverseTransformPoint(a));vertices.Add(transform.InverseTransformPoint(b));vertices.Add(transform.InverseTransformPoint(c));
            colors.Add(color);colors.Add(color);colors.Add(color);indices.Add(n);indices.Add(n+1);indices.Add(n+2);
        }
        void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color){Triangle(a,b,c,color);Triangle(a,c,d,color);}
        void Petal(Vector3 origin,Vector3 direction,float size,Color color)
        {
            Vector3 side=Vector3.Cross(Vector3.up,direction);
            // Rounded lobes and a small notch give each petal a cherry-blossom silhouette.
            Vector3 previous=origin;
            for(int i=1;i<=16;i++)
            {
                float angle=i*Mathf.PI*2/16;
                float along=(1-Mathf.Cos(angle))*.5f;
                float width=Mathf.Sin(angle)*.33f*Mathf.Sqrt(along);
                if(i==8)along=.88f;
                Vector3 next=origin+(direction*along+side*width)*size;
                Triangle(origin+direction*size*.45f,previous,next,color);
                previous=next;
            }
        }
        public void RenderPreview(float progress)
        {
            Ensure();if(outline.positionCount<5)return;
            vertices.Clear();colors.Clear();indices.Clear();
            // 옅은 빨강으로 전체 범위를 먼저 알리고 벚꽃색이 차오르게 표시합니다.
            progress=Mathf.Clamp01(progress);
            Color pink=new Color(1,.30f,.36f,.24f),fill=new Color(1,.50f,.75f,.42f),bright=new Color(1,.76f,.88f,.8f);
            if(corridor)
            {
                bool rounded=outline.positionCount==35;
                var a=outline.GetPosition(rounded?33:0)+Vector3.up*.004f;var b=outline.GetPosition(rounded?0:1)+Vector3.up*.004f;
                var c=outline.GetPosition(rounded?16:2)+Vector3.up*.004f;var d=outline.GetPosition(rounded?17:3)+Vector3.up*.004f;
                Quad(a,b,c,d,pink);
                if(rounded)for(int i=0;i<16;i++)
                {
                    Triangle((b+c)*.5f,outline.GetPosition(i)+Vector3.up*.004f,outline.GetPosition(i+1)+Vector3.up*.004f,pink);
                    Triangle((a+d)*.5f,outline.GetPosition(i+17)+Vector3.up*.004f,outline.GetPosition(i+18)+Vector3.up*.004f,pink);
                }
                var start=(a+d)*.5f;var end=(b+c)*.5f;var forward=(end-start).normalized;var side=(d-a).normalized;
                float length=Vector3.Distance(start,end),width=Vector3.Distance(a,d)*.40f;
                // 캡슐 외곽을 진행 평면으로 잘라 둥근 끝까지 빈틈 없이 채웁니다.
                clipped.Clear();float min=float.PositiveInfinity,max=float.NegativeInfinity;
                int count=outline.positionCount-1;
                for(int i=0;i<count;i++){float dot=Vector3.Dot(outline.GetPosition(i)-start,forward);min=Mathf.Min(min,dot);max=Mathf.Max(max,dot);}
                float cut=Mathf.Lerp(min,max,progress);
                for(int i=0;i<count;i++)
                {
                    Vector3 x=outline.GetPosition(i),y=outline.GetPosition((i+1)%count);
                    float dx=Vector3.Dot(x-start,forward)-cut,dy=Vector3.Dot(y-start,forward)-cut;
                    if(dx<=0)clipped.Add(x+Vector3.up*.013f);
                    if((dx<=0)!=(dy<=0))clipped.Add(Vector3.Lerp(x,y,dx/(dx-dy))+Vector3.up*.013f);
                }
                for(int i=1;i+1<clipped.Count;i++)Triangle(clipped[0],clipped[i],clipped[i+1],fill);
                for(float distance=.65f;distance<length-.35f;distance+=1.1f)
                {
                    var center=start+forward*distance;float alpha=.22f+.55f*Mathf.Pow(Mathf.Max(0,1-Mathf.Abs(distance/length-progress)*3),2);
                    var tint=new Color(1,.65f,.81f,alpha);
                    Quad(center-side*width-forward*.23f,center-side*width-forward*.15f,center+forward*.18f,center+forward*.1f,tint);
                    Quad(center+side*width-forward*.15f,center+side*width-forward*.23f,center+forward*.1f,center+forward*.18f,tint);
                }
                for(int i=0;i<5;i++)Petal(start+forward*.3f,Quaternion.Euler(0,i*72,0)*Vector3.forward,.18f,bright);
            }
            else
            {
                Vector3 center=Vector3.zero;int count=outline.positionCount-1;
                for(int i=0;i<count;i++)center+=outline.GetPosition(i);center/=count;center+=Vector3.up*.004f;
                float radius=Vector3.ProjectOnPlane(outline.GetPosition(0)-center,Vector3.up).magnitude;
                for(int i=0;i<96;i++)
                {
                    float a=i*Mathf.PI/48,b=(i+1)*Mathf.PI/48;
                    var v0=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var v1=new Vector3(Mathf.Cos(b),0,Mathf.Sin(b));
                    Triangle(center,center+v0*radius,center+v1*radius,pink);
                    var fillCenter=center+Vector3.up*.009f;
                    Triangle(fillCenter,fillCenter+v0*radius*progress,fillCenter+v1*radius*progress,fill);
                    if(i/96f<progress)Quad(center+v0*radius*.89f,center+v0*radius*.94f,center+v1*radius*.94f,center+v1*radius*.89f,bright);
                    if(i%12<8)Quad(center+v0*radius*.70f,center+v0*radius*.715f,center+v1*radius*.715f,center+v1*radius*.70f,new Color(1,.5f,.72f,.4f));
                }
                for(int i=0;i<5;i++)Petal(center,Quaternion.Euler(0,i*72,0)*Vector3.forward,radius*.42f,new Color(1,.65f,.8f,.55f));
            }
            mesh.Clear();mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();surface.enabled=true;
        }
        void LateUpdate()
        {
            Ensure();bool visible=outline.enabled;
            if(!visible){surface.enabled=false;wasVisible=false;return;}
            if(!wasVisible)began=Time.time;wasVisible=true;
            var rabbit=GetComponentInParent<SakuraRabbitEnemy>();var deer=GetComponentInParent<FloralDeerEnemy>();
            RenderPreview(rabbit?rabbit.TelegraphProgress:deer?deer.TelegraphProgress:Mathf.Clamp01((Time.time-began)/Mathf.Max(.1f,warningDuration)));
        }
        void OnDisable(){wasVisible=false;if(surface)surface.enabled=false;}
        void OnDestroy(){if(mesh){if(Application.isPlaying)Destroy(mesh);else DestroyImmediate(mesh);}}
    }
}
