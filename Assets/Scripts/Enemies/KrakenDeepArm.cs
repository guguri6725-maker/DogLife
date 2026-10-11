using System.Collections.Generic;
using UnityEngine;

namespace DogLife
{
    // A single rotation-only solver owns this chain. Bind poses, UVs and bone lengths stay intact.
    [DefaultExecutionOrder(180)]
    public sealed class KrakenDeepArm : MonoBehaviour
    {
        readonly List<Transform> bones = new List<Transform>();
        readonly List<SphereCollider> hurt = new List<SphereCollider>();
        Vector3[] points;
        Quaternion[] rest;
        float[] lengths;
        Vector3 anchor, target, bend;
        bool flowingS;
        float shapeBlend;
        float total;
        Transform socketBody;
        Vector3 socketLocal,previousCurve;
        bool curveReady;
        public Vector3 RootPosition=>bones.Count>0?bones[0].position:transform.position;
        public Vector3 SocketPosition=>socketBody?socketBody.TransformPoint(socketLocal):anchor;
        public void Attach(Transform body,Vector3 point){socketBody=body;socketLocal=body.InverseTransformPoint(point);}
        public Vector3 Tip => bones.Count > 0 ? bones[bones.Count-1].position : transform.position;
        public bool Exposed { get; private set; }
        public void Build(GameObject source, Mesh slimMesh, Material material, float reach, float radius)
        {
            var visual = Instantiate(source, transform);
            foreach(var a in visual.GetComponentsInChildren<Animator>()) a.enabled=false;
            var skin=visual.GetComponentInChildren<SkinnedMeshRenderer>();
            if(slimMesh)skin.sharedMesh=slimMesh;
            if(material)skin.sharedMaterial=material;
            skin.updateWhenOffscreen=true;
            var all=visual.GetComponentsInChildren<Transform>();
            for(int i=0;i<18;i++) foreach(var t in all) if(t.name=="Tendril"+i){bones.Add(t);break;}
            if(bones.Count<2){enabled=false;return;}
            float restLength=0;
            for(int i=1;i<bones.Count;i++)restLength+=Vector3.Distance(bones[i-1].position,bones[i].position);
            visual.transform.localScale*=reach/Mathf.Max(.01f,restLength);
            points=new Vector3[bones.Count];rest=new Quaternion[bones.Count];lengths=new float[bones.Count-1];
            for(int i=0;i<bones.Count;i++)rest[i]=bones[i].localRotation;
            for(int i=0;i<lengths.Length;i++){lengths[i]=Vector3.Distance(bones[i].position,bones[i+1].position);total+=lengths[i];}
            for(int i=1;i<bones.Count;i+=2){
                var c=bones[i].gameObject.AddComponent<SphereCollider>();c.isTrigger=true;
                c.radius=Mathf.Lerp(radius,radius*.25f,i/(float)(bones.Count-1))/Mathf.Max(.0001f,bones[i].lossyScale.x);
                c.gameObject.AddComponent<MonsterHurtbox>();c.enabled=false;hurt.Add(c);
            }
            SetShape(transform.position,transform.position+Vector3.up*reach*.7f,Vector3.forward*2);
        }
        public void SetShape(Vector3 start,Vector3 end,Vector3 curve,bool sCurve=false){anchor=start;target=end;bend=curve;flowingS=sCurve;}
        public void SetExposed(bool value){Exposed=value;foreach(var c in hurt)c.enabled=value;}
        public Vector3[] SamplePoints(){var result=new Vector3[bones.Count];for(int i=0;i<bones.Count;i++)result[i]=bones[i].position;return result;}
        void LateUpdate()
        {
            if(points==null)return;
            if(socketBody)anchor=SocketPosition;
            transform.position+=anchor-bones[0].position;
            Vector3 end=anchor+Vector3.ClampMagnitude(target-anchor,total*.995f),axis=(end-anchor).normalized;
            Vector3 curve=Vector3.ProjectOnPlane(bend,axis);
            if(curve.sqrMagnitude<.001f)curve=Vector3.ProjectOnPlane(Vector3.up,axis);
            if(curve.sqrMagnitude<.001f)curve=Vector3.right;
            curve.Normalize();
            // Parallel-transport the bending plane instead of flipping it when a pattern changes.
            Vector3 transported=Vector3.ProjectOnPlane(previousCurve,axis);
            if(curveReady&&transported.sqrMagnitude>.001f)
                curve=Vector3.Slerp(transported.normalized,curve,1-Mathf.Exp(-9*Time.deltaTime)).normalized;
            previousCurve=curve;curveReady=true;
            shapeBlend=Mathf.MoveTowards(shapeBlend,flowingS?1:0,Time.deltaTime*2.5f);
            float targetChord=Vector3.Distance(anchor,end),low=0,high=6;
            // Solve a chain of equal-curvature arcs. Unlike a sine-position curve, this has no pinched apex.
            for(int j=1;j<=32;j++){float a=j*6/32f;if(Trace(a).magnitude<=targetChord){high=a;low=(j-1)*6/32f;break;}}
            for(int j=0;j<14;j++){float a=(low+high)*.5f;if(Trace(a).magnitude>targetChord)low=a;else high=a;}
            float amplitude=(low+high)*.5f;Vector2 result=Trace(amplitude);float correction=-Mathf.Atan2(result.y,result.x);
            points[0]=anchor;float along=0;
            for(int i=0;i<lengths.Length;i++){
                float t=(along+lengths[i]*.5f)/total,angle=amplitude*BendProfile(t)+correction;
                points[i+1]=points[i]+(axis*Mathf.Cos(angle)+curve*Mathf.Sin(angle))*lengths[i];along+=lengths[i];
            }
            // Rebuild from bind rotations to avoid accumulating twist from the previous frame.
            for(int i=0;i<bones.Count;i++)bones[i].localRotation=rest[i];
            for(int i=0;i<bones.Count-1;i++){
                Vector3 current=bones[i+1].position-bones[i].position,desired=points[i+1]-points[i];
                if(current.sqrMagnitude>.000001f&&desired.sqrMagnitude>.000001f)bones[i].rotation=Quaternion.FromToRotation(current,desired)*bones[i].rotation;
            }
        }
        float BendProfile(float t)=>Mathf.Lerp(1-2*t,Mathf.Cos(t*Mathf.PI*2),shapeBlend);
        Vector2 Trace(float amplitude)
        {
            Vector2 end=Vector2.zero;float along=0;
            for(int i=0;i<lengths.Length;i++){float angle=amplitude*BendProfile((along+lengths[i]*.5f)/total);end+=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*lengths[i];along+=lengths[i];}
            return end;
        }
    }
}
