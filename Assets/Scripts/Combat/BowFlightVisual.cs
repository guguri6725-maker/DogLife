using UnityEngine;
namespace DogLife
{
    // Hierarchy에서 만든 외형만 꾸밉니다. 크기가 커져도 피해 판정은 바뀌지 않습니다.
    public sealed class BowFlightVisual : MonoBehaviour
    {
        public GameObject body;
        public Renderer[] bones;
        public LineRenderer[] arrowheads;
        public TrailRenderer coreTrail, glowTrail;
        public TrailRenderer[] ribbons;
        [Min(.1f)] public float trailDuration=.48f;
        bool finished;
        public void Initialize(bool human,bool synthesized,float charge)
        {
            Color color=human?new Color(1,.22f,.12f):new Color(.15f,.8f,1);
            Color core=Color.Lerp(color,Color.white,.8f);
            float size=(synthesized?1.45f:1)*Mathf.Lerp(1,1.2f,charge);
            transform.localScale=Vector3.one*size;
            var block=new MaterialPropertyBlock();block.SetColor("_Color",core);
            foreach(var r in bones)if(r)r.SetPropertyBlock(block);
            foreach(var line in arrowheads)
            {
                line.useWorldSpace=false;line.positionCount=3;
                line.SetPositions(new[]{new Vector3(-.23f,0,.2f),new Vector3(0,0,.65f),new Vector3(.23f,0,.2f)});
                line.startWidth=line.endWidth=.1f;line.startColor=line.endColor=core;
            }
            Setup(coreTrail,core,.09f*size,trailDuration);
            Setup(glowTrail,color,.34f*size,trailDuration*1.2f);
            for(int i=0;i<ribbons.Length;i++)if(ribbons[i])
            {ribbons[i].gameObject.SetActive(synthesized);if(synthesized)Setup(ribbons[i],color,.075f*size,trailDuration*1.4f);}
        }
        void Setup(TrailRenderer trail,Color color,float width,float life)
        {
            if(!trail)return;
            trail.Clear();trail.time=life;trail.minVertexDistance=.05f;trail.numCapVertices=3;trail.numCornerVertices=3;
            trail.startWidth=width;trail.endWidth=0;
            var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.Lerp(color,Color.white,.4f),0),new GradientColorKey(color,.25f),new GradientColorKey(color,1)},new[]{new GradientAlphaKey(.95f,0),new GradientAlphaKey(.6f,.25f),new GradientAlphaKey(0,1)});
            trail.colorGradient=gradient;trail.emitting=true;
        }
        public void Finish()
        {
            if(finished)return;finished=true;
            // 충돌 뒤 외형은 없애고 남은 비행 흔적만 부드럽게 지웁니다.
            transform.SetParent(null,true);body.SetActive(false);
            if(coreTrail)coreTrail.emitting=false;if(glowTrail)glowTrail.emitting=false;
            foreach(var trail in ribbons)if(trail)trail.emitting=false;
            Destroy(gameObject,trailDuration*1.5f);
        }
    }
}
