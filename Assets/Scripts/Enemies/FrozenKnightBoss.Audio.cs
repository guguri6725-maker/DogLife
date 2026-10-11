using UnityEngine;
namespace DogLife
{
    public sealed partial class FrozenKnightBoss
    {
        [Header("보스 사운드")]
        [SerializeField,Range(0,1)] float bossSoundVolume=.8f,bossFootstepVolume=.4f,bossStormVolume=.23f;
        GameAudio.Handle stormSound;
        int horseStep=-1,horseGait;
        void BossSound(string cue,Transform origin=null,float gain=1)
        {var source=origin?origin:transform;GameAudio.Play("WinterBoss_"+cue,source.position,bossSoundVolume*gain,follow:source);}
        void StormSound(bool active)
        {
            stormSound.Stop();
            if(active)stormSound=GameAudio.Play("WinterBoss_ColdStorm_Loop",transform.position,bossStormVolume,follow:transform,loop:true);
        }
        void TickBossAudio()
        {
            if(Time.timeScale<=0||!health||health.Dead||CombatFeedback.Paused(this))return;
            StepAudio(horseAnimator,true,ref horseStep,ref horseGait);
        }
        internal void KnightFootstep(Transform foot)
        {
            if(Time.timeScale<=0||!health||health.Dead||CombatFeedback.Paused(this))return;
            var source=foot?foot:knightAnimator.transform;
            GameAudio.Play("WinterBoss_Footstep",source.position,bossFootstepVolume,1,follow:source,minimumInterval:.12f);
        }
        void StepAudio(Animator animator,bool horse,ref int last,ref int gait)
        {
            if(!animator||!animator.isActiveAndEnabled||animator.speed<=0)return;
            var state=animator.GetCurrentAnimatorStateInfo(0);
            bool moving=state.IsName("Walk")||state.IsName("UnarmedWalk")||horse&&state.IsName("Gallop");
            if(!moving){last=-1;return;}
            int step=Mathf.FloorToInt(state.normalizedTime*(horse?4:2));
            if(gait!=state.shortNameHash){gait=state.shortNameHash;last=step;return;}
            if(step==last)return;last=step;
            GameAudio.Play(horse?"WinterBoss_Horse_Hoof":"WinterBoss_Footstep",animator.transform.position,bossFootstepVolume,Random.Range(.97f,1.03f),follow:animator.transform,minimumInterval:.06f);
        }
        void AnimationSound(Animator animator,string state)
        {
            if(animator==horseAnimator&&state=="Rear")BossSound("Horse_Neigh",horseRoot,.65f);
            if(animator!=knightAnimator)return;
            if(state=="ThroneRise")BossSound("Armor_Move");
            if(state=="ThroneDraw2")BossSound("Sword_Draw",swordRoot);
            if(state=="CinematicMount"||state=="Mount")BossSound("Mount",riderRoot);
        }
    }
}
