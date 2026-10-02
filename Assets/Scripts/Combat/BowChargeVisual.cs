using UnityEngine;
namespace DogLife
{
    // 활을 당기는 동안만 유지되는 충전 표시입니다. 프리팹의 선을 갱신합니다.
    public sealed class BowChargeVisual : MonoBehaviour
    {
        public LineRenderer outline, fill, arrowCore;
        public LineRenderer[] motes;
        [Min(.1f)] public float radius=.46f;
        public void Show(float charge,bool human,bool synthesized)
        {
            charge=Mathf.Clamp01(charge);
            Color tint=human?new Color(1,.24f,.14f):new Color(.1f,.75f,1);
            float scale=synthesized?1.3f:1;
            float pulse=charge>=.99f?1+.1f*Mathf.Sin(Time.time*13):1;
            float r=radius*scale*Mathf.Lerp(.75f,1,charge)*pulse;
            Ring(outline,r,1,new Color(tint.r,tint.g,tint.b,.22f+charge*.2f),.035f*scale);
            Color bright=Color.Lerp(tint,Color.white,charge*.45f);bright.a=.6f+charge*.4f;
            Ring(fill,r,Mathf.Max(.025f,charge),bright,(.07f+charge*.045f)*scale);
            arrowCore.useWorldSpace=false;arrowCore.positionCount=2;
            arrowCore.SetPositions(new[]{Vector3.back*.28f,Vector3.forward*(.65f+.4f*charge)*scale});
            arrowCore.startWidth=(.07f+.12f*charge)*scale*pulse;arrowCore.endWidth=.025f;arrowCore.startColor=arrowCore.endColor=bright;
            // 작은 빛줄기가 바깥에서 활시위 쪽으로 빨려 들어옵니다.
            for(int i=0;i<motes.Length;i++)
            {
                float u=Mathf.Repeat(Time.time*(.8f+charge*.8f)+i/(float)motes.Length,1);
                float a=i*2.39996f+Time.time*.45f;Vector3 d=new Vector3(Mathf.Cos(a),Mathf.Sin(a),-.25f);
                var line=motes[i];line.useWorldSpace=false;line.positionCount=2;
                line.SetPositions(new[]{d*r*(1.8f-u*1.6f),d*r*(1.6f-u*1.4f)});
                line.startWidth=(.035f+.03f*charge)*scale;line.endWidth=0;
                line.startColor=line.endColor=new Color(tint.r,tint.g,tint.b,Mathf.Sin(u*Mathf.PI)*(.55f+.45f*charge));
            }
        }
        void Ring(LineRenderer line,float r,float amount,Color color,float width)
        {
            line.useWorldSpace=false;line.positionCount=49;
            for(int i=0;i<49;i++){float a=Mathf.PI*.5f-i/48f*Mathf.PI*2*amount;line.SetPosition(i,new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r,0));}
            line.startWidth=line.endWidth=width;line.startColor=line.endColor=color;
        }
    }
}
