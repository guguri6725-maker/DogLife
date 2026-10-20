using System.Collections.Generic;
using UnityEngine;

namespace DogLife
{
    public sealed partial class FrozenKnightVfx
    {
        public enum CinematicMoment { Wake, Draw, IceSpread, Mount, Death, Summon }
        [Header("연출 전용 — Hierarchy에 배치한 냉기 조명")]
        [SerializeField] Light cinematicLight;
        [SerializeField,Range(0,5)] float cinematicBrightness=2.6f;
        sealed class CinematicPulse
        {
            public CinematicMoment moment;
            public Transform anchor;
            public Vector3 origin;
            public float floor,born,duration,radius;
        }
        readonly List<CinematicPulse> cinematicPulses=new List<CinematicPulse>(8);
        Mesh cinematicMesh;
        public void PlayCinematic(CinematicMoment moment,Transform anchor,float floor,float duration,float radius)
        {
            if(!anchor)return;
            // 파편별 오브젝트 대신 한 개의 재사용 메시로 여러 소환 위치까지 묶어 그립니다.
            if(cinematicPulses.Count>=8)cinematicPulses.RemoveAt(0);
            cinematicPulses.Add(new CinematicPulse{moment=moment,anchor=anchor,origin=anchor.position,
                floor=floor,born=Time.time,duration=Mathf.Max(.2f,duration),radius=radius});
        }
        public void ClearCinematic()
        {
            cinematicPulses.Clear();
            if(cinematicLight)cinematicLight.intensity=0;
        }
        static Vector3 CinematicRadial(float angle,float radius)
        {
            float a=angle*Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius);
        }
        void CinematicRing(Vector3 center,float radius,float width,Color tint)
        {
            for(int i=0;i<64;i++)
            {
                float a=i*360f/64,b=(i+1)*360f/64;
                Quad(center+CinematicRadial(a,radius-width),center+CinematicRadial(a,radius),
                    center+CinematicRadial(b,radius),center+CinematicRadial(b,radius-width),tint);
            }
        }
        void DrawCinematic()
        {
            cinematicPulses.RemoveAll(p=>Time.time-p.born>=p.duration);
            if(cinematicPulses.Count==0){if(cinematicLight)cinematicLight.intensity=0;return;}
            if(!cinematicMesh){cinematicMesh=new Mesh{name="Frozen Knight cinematic frost"};cinematicMesh.MarkDynamic();}
            if(!detailCamera)detailCamera=Camera.main;
            Clear();float brightness=0;Vector3 lightPosition=transform.position;
            foreach(var pulse in cinematicPulses)
            {
                float age=Time.time-pulse.born,t=Mathf.Clamp01(age/pulse.duration);
                // 초반에는 충분히 빛나고, 연출 마지막 35%에서만 부드럽게 사라집니다.
                float fade=Mathf.SmoothStep(0,1,age/.25f)*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.65f,1,t)));
                Vector3 anchor=pulse.anchor?pulse.anchor.position:pulse.origin;
                Vector3 ground=new Vector3(anchor.x,pulse.floor+.09f,anchor.z);
                bool draw=pulse.moment==CinematicMoment.Draw;
                bool spread=pulse.moment==CinematicMoment.IceSpread;
                bool death=pulse.moment==CinematicMoment.Death;
                bool gather=pulse.moment==CinematicMoment.Mount||pulse.moment==CinematicMoment.Summon;
                float intensity=fade*(draw?1.2f:spread?1.1f:.7f);
                if(intensity>brightness){brightness=intensity;lightPosition=draw?anchor:ground+Vector3.up*2.4f;}
                // 얼굴과 무기 실루엣은 남기고, 발밑 파동과 옆으로 흐르는 서리로 장면을 채웁니다.
                float ring=gather?Mathf.Lerp(pulse.radius,1.2f,Mathf.SmoothStep(0,1,t)):
                    Mathf.Lerp(.5f,pulse.radius,Mathf.SmoothStep(0,1,Mathf.Min(1,t*1.6f)));
                CinematicRing(ground,ring,.16f,new Color(.48f,.84f,1,.8f*fade));
                CinematicRing(ground+Vector3.up*.015f,ring*.82f,.045f,new Color(.8f,.96f,1,.5f*fade));
                // 낮게 감기는 두 줄의 냉기로 기다리는 구간에도 흐름을 주되 머리 높이는 비웁니다.
                if(!draw&&!spread)
                    for(int band=0;band<2;band++)for(int j=0;j<24;j++)
                    {
                        float u=j/24f,v=(j+1)/24f,angle=age*50+band*180;
                        Vector3 a=ground+CinematicRadial(angle+u*220,2.2f)+Vector3.up*(.25f+u*2.1f);
                        Vector3 b=ground+CinematicRadial(angle+v*220,2.2f)+Vector3.up*(.25f+v*2.1f);
                        SoftSurface(a-Vector3.up*.15f,a+Vector3.up*.15f,b+Vector3.up*.15f,b-Vector3.up*.15f,
                            new Color(.4f,.8f,1,.34f*fade),4,band);
                    }
                for(int i=0;i<18;i++)
                {
                    float seed=i*.618034f,cycle=Mathf.Repeat(age*.35f+seed,1);
                    float r=spread?Mathf.Lerp(.5f,pulse.radius,t):Mathf.Lerp(.8f,pulse.radius,Mathf.Repeat(seed,1));
                    Vector3 pos=ground+CinematicRadial(i*137.5f+age*(gather?28:9),r);
                    pos.y+=.2f+cycle*(death?2.8f:.65f);
                    float size=spread?2.8f:1.3f+Mathf.Repeat(seed,1);
                    ColdPuff(pos,size,size*.6f,new Color(.32f,.65f,.83f,.42f*fade),2,seed);
                }
                for(int i=0;i<64;i++)
                {
                    float seed=Mathf.Repeat(i*.618034f,1),u=Mathf.Repeat(age*.42f+seed,1);
                    float r=gather?Mathf.Lerp(pulse.radius,.45f,u):Mathf.Lerp(.5f,pulse.radius,seed);
                    Vector3 pos=ground+CinematicRadial(i*137.5f+age*24,r);
                    pos.y+=(death?u*6:Mathf.Sin(u*Mathf.PI)*(gather?3.2f:1.6f))+.12f;
                    float alpha=Mathf.Sin(u*Mathf.PI)*fade;
                    ColdPuff(pos,.18f,.3f,new Color(.62f,.88f,1,.95f*alpha),3,seed);
                    if(i%4==0)IcePlate(pos,Quaternion.Euler(i*31+age*90,i*73,age*55),
                        .06f,.2f,.035f,alpha*.8f);
                }
                if(draw&&swordBase&&swordTip)
                {
                    // 발도 궤적을 실제 칼에 붙여, 등 뒤에 남거나 화면 전체를 덮는 섬광을 피합니다.
                    for(int i=0;i<16;i++)
                    {
                        float u=i/15f;Vector3 pos=Vector3.Lerp(swordBase.position,swordTip.position,u);
                        float glint=.3f+.7f*Mathf.Pow(Mathf.Max(0,Mathf.Sin(age*5-u*6)),6);
                        ColdPuff(pos,.35f,.65f,new Color(.65f,.91f,1,glint*fade),3,i);
                    }
                    ColdPuff(swordTip.position,1.4f,1.4f,new Color(.62f,.88f,1,.5f*fade),3,0);
                }
            }
            Submit(cinematicMesh);
            if(cinematicLight){cinematicLight.transform.position=lightPosition;cinematicLight.intensity=brightness*cinematicBrightness;}
        }
    }
}
