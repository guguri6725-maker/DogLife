using System.Collections.Generic;
using UnityEngine;

namespace DogLife
{
    // 전투 판정과 분리된 해골 전용 연출. 궤적은 실제 Attack 클립에서 추출합니다.
    public sealed class BlossomSkeletonAttackVfx : MonoBehaviour
    {
        public Animator animator;
        public Material material;
        public Vector3[] grips, tips;
        public float duration=1.65f, swingStart=.95f, impactTime=1.25f;
        Mesh mesh;
        MeshRenderer meshRenderer;
        CombatHealth health;
        readonly List<Vector3> vertices=new List<Vector3>(2048);
        readonly List<Color> colors=new List<Color>(2048);
        readonly List<int> triangles=new List<int>(4096);
        static readonly int Attacking=Animator.StringToHash("Attacking");
        readonly Collider[] hitBuffer=new Collider[32];
        public bool HitsTarget(CombatHealth target)
        {
            if(!animator||grips==null||tips==null||tips.Length<2||grips.Length!=tips.Length)return false;
            for(int i=0;i<=32;i++)
            {
                float t=Mathf.Lerp(swingStart,impactTime,i/32f);
                Vector3 a=transform.TransformPoint(Sample(grips,t)),b=transform.TransformPoint(Sample(tips,t));
                int count=Physics.OverlapCapsuleNonAlloc(a,b,.06f,hitBuffer,~0,QueryTriggerInteraction.Ignore);
                var hits=count==hitBuffer.Length?Physics.OverlapCapsule(a,b,.06f,~0,QueryTriggerInteraction.Ignore):hitBuffer;
                if(hits!=hitBuffer)count=hits.Length;
                for(int j=0;j<count;j++)if(hits[j].GetComponentInParent<CombatHealth>()==target)return true;
            }
            return false;
        }

        void Awake(){health=GetComponentInParent<CombatHealth>();}
        void LateUpdate()
        {
            if(!animator||!animator.isActiveAndEnabled||(health&&health.Dead)||!animator.GetBool(Attacking)) {Clear();return;}
            var state=animator.GetCurrentAnimatorStateInfo(0);
            if(!state.IsName("Attack")){Clear();return;}
            RenderAt(state.normalizedTime*duration);
        }
        Vector3 Sample(Vector3[] points,float time)
        {
            float f=Mathf.Clamp01(time/duration)*(points.Length-1);int i=Mathf.FloorToInt(f);
            return transform.InverseTransformPoint(animator.transform.TransformPoint(Vector3.Lerp(points[i],points[Mathf.Min(i+1,points.Length-1)],f-i)));
        }
        void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color)
        {
            int n=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);vertices.Add(d);
            for(int i=0;i<4;i++)colors.Add(color);
            triangles.Add(n);triangles.Add(n+1);triangles.Add(n+2);triangles.Add(n);triangles.Add(n+2);triangles.Add(n+3);
        }
        void Petal(Vector3 p,Vector3 a,Vector3 b,Color color)
        {
            int n=vertices.Count;
            vertices.Add(p);vertices.Add(p-a);vertices.Add(p-a*.25f+b*.7f);
            vertices.Add(p+a*.6f+b);vertices.Add(p+a*.35f);
            vertices.Add(p+a*.6f-b);vertices.Add(p-a*.25f-b*.7f);
            for(int i=0;i<7;i++)colors.Add(color);
            for(int i=0;i<6;i++){triangles.Add(n);triangles.Add(n+1+i);triangles.Add(n+1+(i+1)%6);}
        }
        // 편집기 미리보기에서도 동일한 시간 기반 연출을 검사합니다.
        public void RenderAt(float time)
        {
            if(!animator||!material||grips==null||tips==null||grips.Length<2||tips.Length!=grips.Length||time<swingStart||time>=duration){Clear();return;}
            if(!mesh)
            {
                mesh=new Mesh{name="Blossom slash (runtime)"};mesh.MarkDynamic();
                var filter=GetComponent<MeshFilter>();if(!filter)filter=gameObject.AddComponent<MeshFilter>();filter.sharedMesh=mesh;
                meshRenderer=GetComponent<MeshRenderer>();if(!meshRenderer)meshRenderer=gameObject.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterial=material;meshRenderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;meshRenderer.receiveShadows=false;
            }
            vertices.Clear();colors.Clear();triangles.Clear();
            float end=Mathf.Min(time,impactTime),start=Mathf.Max(swingStart,time-.19f);
            for(int i=0;i<32&&start<end;i++)
            {
                float t0=Mathf.Lerp(start,end,i/32f),t1=Mathf.Lerp(start,end,(i+1)/32f);
                Vector3 g0=Sample(grips,t0),g1=Sample(grips,t1),p0=Sample(tips,t0),p1=Sample(tips,t1);
                float alpha=Mathf.Pow((i+1)/32f,1.4f)*Mathf.Clamp01((impactTime+.19f-time)/.19f);
                float taper=Mathf.Sin(Mathf.PI*(i+.5f)/32f);
                Quad(Vector3.Lerp(g0,p0,1-.46f*taper),p0,p1,Vector3.Lerp(g1,p1,1-.46f*taper),new Color(1,.22f,.5f,alpha*.55f*taper));
                Quad(Vector3.Lerp(g0,p0,.92f),p0,p1,Vector3.Lerp(g1,p1,.92f),new Color(1,.78f,.87f,alpha*.65f*taper));
            }
            // 공격 중에만 짧게 흩어지고 사라지는 꽃잎을 표시합니다.
            for(int i=0;i<38;i++)
            {
                float seed=i*2.399963f,born=Mathf.Lerp(swingStart,impactTime,i/37f),age=time-born;
                if(age<0||age>.34f)continue;
                Vector3 p=Sample(tips,born)+new Vector3(Mathf.Sin(seed)*.8f,.35f+Mathf.Cos(seed)*.35f,Mathf.Cos(seed)*.7f)*age;
                p.y-=age*age*.8f;float size=(.055f+.018f*(i%3))*Mathf.Clamp01((.34f-age)/.12f);
                Vector3 a=new Vector3(Mathf.Cos(seed+age*7),Mathf.Sin(seed+age*7),0)*size;
                Vector3 b=new Vector3(-a.y,a.x,Mathf.Sin(seed)*size)*.55f;
                Petal(p,a,b,new Color(1,.48f+.015f*(i%8),.72f,Mathf.Clamp01((.34f-age)/.1f)));
            }
            float impactAge=time-impactTime;
            if(impactAge>=0&&impactAge<.3f)
            {
                Vector3 center=Sample(tips,impactTime);
                center.y=transform.InverseTransformPoint(animator.transform.position).y+.025f;
                float radius=.08f+impactAge*4.2f;
                for(int i=0;i<40;i++)
                {
                    float a=i*Mathf.PI/20,b=(i+1)*Mathf.PI/20;
                    Vector3 v0=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)),v1=new Vector3(Mathf.Cos(b),0,Mathf.Sin(b));
                    Quad(center+v0*radius,center+v0*(radius+.12f),center+v1*(radius+.12f),center+v1*radius,new Color(1,.65f,.82f,1-impactAge/.3f));
                }
            }
            mesh.Clear();mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();meshRenderer.enabled=vertices.Count>0;
        }
        void Clear(){if(meshRenderer)meshRenderer.enabled=false;}
        void OnDisable(){Clear();}
        void OnDestroy(){if(mesh){if(Application.isPlaying)Destroy(mesh);else DestroyImmediate(mesh);}}
    }
}

