using UnityEngine;
namespace DogLife
{
    // 발사·명중 시 이미 배치된 선을 팽창시키고 감쇠시킵니다.
    public sealed class BowReleaseVisual : MonoBehaviour
    {
        public LineRenderer ring, core;
        public LineRenderer[] sparks;
        [Min(.05f)] public float duration=.3f;
        float age,size=1;Color tint;bool initialized;
        public void Initialize(bool human,bool synthesized,float charge,bool impact=false)
        {
            tint=human?new Color(1,.23f,.12f):new Color(.12f,.75f,1);
            size=(synthesized?1.45f:1)*Mathf.Lerp(1,1.2f,charge)*(impact?.7f:1);
            initialized=true;Draw(0);Destroy(gameObject,duration);
        }
        void Update(){if(!initialized)return;age+=Time.deltaTime;Draw(Mathf.Clamp01(age/duration));}
        void Draw(float t)
        {
            float fade=(1-t)*(1-t),r=Mathf.Lerp(.12f,.75f,t)*size;
            ring.useWorldSpace=false;ring.positionCount=49;
            for(int i=0;i<49;i++){float a=i*Mathf.PI/24;ring.SetPosition(i,new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r,t*.2f));}
            ring.startWidth=ring.endWidth=.12f*(1-t)*size;ring.startColor=ring.endColor=new Color(tint.r,tint.g,tint.b,fade);
            core.useWorldSpace=false;core.positionCount=2;core.SetPositions(new[]{Vector3.back*.18f,Vector3.forward*(.8f+t*.6f)*size});
            core.startWidth=.27f*(1-t)*size;core.endWidth=0;core.startColor=core.endColor=new Color(1,.95f,.88f,fade);
            for(int i=0;i<sparks.Length;i++)
            {
                float a=(i+.12f)*Mathf.PI*2/sparks.Length;Vector3 d=new Vector3(Mathf.Cos(a),Mathf.Sin(a),.2f);
                var line=sparks[i];line.useWorldSpace=false;line.positionCount=2;
                line.SetPositions(new[]{d*(r*.65f),d*(r+.2f*size*(1-t))});line.startWidth=.065f*(1-t)*size;line.endWidth=0;
                line.startColor=line.endColor=new Color(tint.r,tint.g,tint.b,fade);
            }
        }
    }
}
