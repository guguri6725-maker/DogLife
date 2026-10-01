using UnityEngine;
namespace DogLife
{
    // 프리팹에 배치한 선만 사용하며, 장식에는 별도 피해 판정을 넣지 않습니다.
    public sealed class SynthWeaponStrikeAccent : MonoBehaviour
    {
        public LineRenderer core;
        public LineRenderer echo;
        public LineRenderer filaments;
        public Material surfaceMaterial;
        public LineRenderer[] sparks;
        [SerializeField] float duration=.38f;
        [SerializeField,Range(0,2)] float sparkTravel=.65f;
        float age;
        Color tint;
        Vector3[] points;
        float width;
        Vector3[] sparkOrigins,sparkDirections;
        public void Initialize(LineRenderer shape, Color color)
        {
            if(!shape||!core||!echo||!filaments){Destroy(gameObject);return;}
            tint=color; points=new Vector3[shape.positionCount];shape.GetPositions(points);
            if(!shape.useWorldSpace)for(int i=0;i<points.Length;i++)points[i]=shape.transform.TransformPoint(points[i]);
            width=shape.widthMultiplier;
            foreach(var line in new[]{core,echo,filaments})
            {
                line.useWorldSpace=true;line.positionCount=points.Length;line.SetPositions(points);
                line.widthCurve=shape.widthCurve;line.numCapVertices=4;line.numCornerVertices=3;
            }
            // 미리 배치된 작은 파편만 이동시키므로 매 프레임 오브젝트를 만들지 않습니다.
            int count=sparks==null?0:sparks.Length;
            sparkOrigins=new Vector3[count];sparkDirections=new Vector3[count];
            Vector3 center=Vector3.zero;foreach(var point in points)center+=point;center/=Mathf.Max(1,points.Length);
            for(int i=0;i<count;i++)
            {
                if(!sparks[i])continue;
                int at=Mathf.RoundToInt((i+.5f)/count*(points.Length-1));
                sparkOrigins[i]=points[at];
                var tangent=(points[Mathf.Min(at+1,points.Length-1)]-points[Mathf.Max(0,at-1)]).normalized;
                var outward=(points[at]-center).normalized;
                sparkDirections[i]=(outward*.45f+tangent*((i%2==0?1:-1)*.4f)+Vector3.up*(.3f+(i%3)*.17f)).normalized;
                sparks[i].useWorldSpace=true;sparks[i].positionCount=3;
                sparks[i].widthCurve=new AnimationCurve(new Keyframe(0,0),new Keyframe(.35f,1),new Keyframe(1,0));
            }
            Apply(0);
        }
        void Apply(float t)
        {
            // 중심의 밝은 날과 시간차 잔광으로 원래 공격 모양을 강조합니다.
            var bright=Color.Lerp(tint,Color.white,.65f);bright.a=(1-t)*.95f;
            core.startColor=core.endColor=bright;core.widthMultiplier=width*.22f*(1-t);
            var glow=tint;glow.a=(1-t)*.36f;
            echo.startColor=echo.endColor=glow;echo.widthMultiplier=width*(1-t*.5f);
            glow.a=(1-t)*.85f;filaments.startColor=filaments.endColor=glow;filaments.widthMultiplier=width*.075f;
            for(int i=0;i<points.Length;i++)
            {
                float envelope=Mathf.Sin(i*Mathf.PI/Mathf.Max(1,points.Length-1));
                filaments.SetPosition(i,points[i]+Vector3.up*(Mathf.Sin(i*.32f-t*7)*width*.065f*envelope));
                echo.SetPosition(i,points[i]+Vector3.up*(t*width*.12f));
            }
            for(int i=0;i<sparkOrigins.Length;i++)
            {
                var spark=sparks[i];if(!spark)continue;
                float travel=1-Mathf.Pow(1-t,2);
                Vector3 head=sparkOrigins[i]+sparkDirections[i]*travel*sparkTravel*Mathf.Clamp(width,.3f,1.5f);
                Vector3 tail=head-sparkDirections[i]*(.08f+width*.12f)*(1-t);
                spark.SetPosition(0,tail);spark.SetPosition(1,Vector3.Lerp(tail,head,.65f));spark.SetPosition(2,head);
                bright.a=(1-t)*(1-t);spark.startColor=spark.endColor=bright;
                spark.widthMultiplier=Mathf.Clamp(width*.05f,.012f,.07f)*(1-t);
            }
        }
        void Update(){age+=Time.deltaTime;float t=age/Mathf.Max(.05f,duration);if(t>=1){Destroy(gameObject);return;}if(points!=null)Apply(t);}
    }
}
