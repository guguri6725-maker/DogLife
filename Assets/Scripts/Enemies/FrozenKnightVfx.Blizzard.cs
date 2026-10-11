using UnityEngine;
namespace DogLife
{
    public sealed partial class FrozenKnightVfx
    {
        bool finaleBlizzard,blizzardReleasing;
        FrozenKnightBoss blizzardOwner;
        FrozenKnightSwordCore blizzardCore;
        float blizzardAge,blizzardDuration,blizzardHeight,blizzardDensity,blizzardFade=1,blizzardFadeTime;
        float blizzardForcedProgress;
        public FrozenKnightVfx CreateFinaleBlizzard(FrozenKnightBoss owner,FrozenKnightSwordCore core,Vector3 center,float radius,float duration,float height,float density,float fadeTime)
        {
            var go=new GameObject("FinalStand_EncroachingBlizzard");go.transform.SetParent(owner.transform,false);
            go.layer=gameObject.layer;var fx=go.AddComponent<FrozenKnightVfx>();
            fx.frostMaterial=frostMaterial;fx.finaleBlizzard=true;fx.blizzardOwner=owner;fx.blizzardCore=core;
            fx.origin=center;fx.direction=Vector3.forward;fx.length=radius;fx.blizzardDuration=Mathf.Max(1,duration);
            fx.blizzardHeight=height;fx.blizzardDensity=density;fx.blizzardFadeTime=Mathf.Max(.1f,fadeTime);
            return fx;
        }
        public void FillFinaleBlizzard(float progress){blizzardForcedProgress=Mathf.Clamp01(progress);}
        public void ReleaseFinaleBlizzard(){blizzardReleasing=true;}
        void DrawFinaleBlizzard()
        {
            if(!blizzardOwner||!blizzardOwner.isActiveAndEnabled){Destroy(gameObject);return;}
            if(!blizzardCore||blizzardCore.Broken||blizzardOwner.State==FrozenKnightBoss.CombatState.Dead)blizzardReleasing=true;
            float dt=CombatFeedback.Paused(blizzardOwner)?0:Time.deltaTime;
            if(blizzardReleasing)blizzardFade=Mathf.MoveTowards(blizzardFade,0,dt/blizzardFadeTime);
            else blizzardAge+=dt;
            if(blizzardFade<=0){Destroy(gameObject);return;}
            if(!detailCamera)detailCamera=Camera.main;
            float p=Mathf.Max(blizzardForcedProgress,Mathf.SmoothStep(0,.92f,blizzardAge/blizzardDuration));
            float fade=Mathf.SmoothStep(0,1,blizzardAge/2.5f)*blizzardFade;
            float front=length*Mathf.Lerp(.98f,.06f,p);
            float age=blizzardAge;Clear();
            // 서로 다른 높이와 회전속도의 얇은 냉기층. 중앙 조준선은 마지막까지 읽힙니다.
            for(int layer=0;layer<3;layer++)for(int i=0;i<64;i++)
            {
                float seed=i*.618034f+layer*.271f;
                float angle=i*360f/64+age*(layer%2==0?9:-6);
                float radius=Mathf.Lerp(front,length+1,layer/2f)+Mathf.Sin(seed*17+age*.7f)*.55f;
                float y=.7f+layer*blizzardHeight*.25f+Mathf.Sin(seed*23+age)*.6f;
                var pos=origin+Radial(angle,radius)+Vector3.up*y;
                float size=3.2f+layer*1.4f+p*1.5f;
                ColdPuff(pos,size,blizzardHeight*(.45f+layer*.12f),new Color(.48f,.72f,.87f,(.12f+p*.14f)*fade*blizzardDensity),2,seed*31);
                if(layer==0&&i%2==0)
                {
                    var ground=origin+Radial(angle,radius)+Vector3.up*.12f;
                    SoftSurface(ground+new Vector3(-2,0,-2),ground+new Vector3(2,0,-2),ground+new Vector3(2,0,2),ground+new Vector3(-2,0,2),new Color(.65f,.86f,1,.17f*fade),2,seed);
                }
            }
            // 바람을 따라 휘어지는 가는 눈발과 큰 결정이 서로 다른 속도로 지나갑니다.
            int flakes=Mathf.RoundToInt(230*blizzardDensity);
            for(int i=0;i<flakes;i++)
            {
                float seed=Mathf.Repeat(i*.618034f,1),cycle=Mathf.Repeat(age*(.32f+seed*.3f)+i*.38197f,1);
                float radius=Mathf.Lerp(front,length,Mathf.Repeat(i*.754877f,1));
                float angle=i*137.508f+age*(16+seed*20);
                Vector3 pos=origin+Radial(angle,radius)+Vector3.up*(.15f+(1-cycle)*blizzardHeight);
                float alpha=Mathf.Sin(cycle*Mathf.PI)*fade*(.35f+p*.5f);
                var tangent=Radial(angle+90,.24f+seed*.45f)-Vector3.up*.18f;
                var thin=(detailCamera?detailCamera.transform.up:Vector3.up)*(.008f+seed*.012f);
                SoftSurface(pos-tangent-thin,pos+tangent-thin,pos+tangent+thin,pos-tangent+thin,new Color(.74f,.9f,1,alpha),4,seed*19);
                if(i%5==0)ColdPuff(pos,.045f+seed*.07f,.065f+seed*.1f,new Color(.82f,.96f,1,alpha),3,seed);
            }
            // 별도 메시로 제출하여 일반 공격 범위/검 잔상과 서로 덮어쓰지 않습니다.
            Submit(ribbonMesh);
        }
    }
}
