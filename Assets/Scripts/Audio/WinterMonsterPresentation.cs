using UnityEngine;
using UnityEngine.AI;
namespace DogLife
{
    // Sound cues follow the same normalized action windows as damage and visual effects.
    [RequireComponent(typeof(WinterMonsterEnemy))]
    public sealed class WinterMonsterPresentation:MonoBehaviour
    {
        public AnimationClip deathAnimation;
        [Range(0,1)] public float attackVolume=.7f,deathVolume=.85f,footstepVolume=.35f;
        [Min(0)] public float corpseHold=2;
        WinterMonsterEnemy enemy;CombatHealth health;Animator animator;NavMeshAgent agent;
        WinterMonsterEnemy.Phase previous;int cues;float nextStep;bool started;
        GameAudio.Handle preparation,frost;
        string Prefix=>enemy.Kind==WinterMonsterEnemy.Species.Golem?"Golem":enemy.Kind==WinterMonsterEnemy.Species.Spirit?"SnowSpirit":enemy.Kind==WinterMonsterEnemy.Species.Wolf?"IceWolf":"Mammoth";
        void Awake(){enemy=GetComponent<WinterMonsterEnemy>();health=GetComponent<CombatHealth>();animator=GetComponentInChildren<Animator>();agent=GetComponent<NavMeshAgent>();}
        void OnEnable(){health.Died+=Die;started=false;nextStep=Time.time+.4f;}
        void OnDisable(){health.Died-=Die;preparation.Stop();frost.Stop();}
        GameAudio.Handle Sound(string suffix,float volume=-1)=>GameAudio.Play(Prefix+"_"+suffix,transform.position,volume<0?attackVolume:volume,follow:transform);
        bool At(int bit,float time){if((cues&bit)!=0||enemy.Progress<time)return false;cues|=bit;return true;}
        void Update()
        {
            if(health.Dead||Time.timeScale<=0||CombatFeedback.Paused(enemy))return;
            var state=enemy.State;
            if(!started||state!=previous)
            {
                started=true;previous=state;cues=0;preparation.Stop();frost.Stop();
                if(state==WinterMonsterEnemy.Phase.Warning)
                {
                    preparation=Sound(enemy.Kind==WinterMonsterEnemy.Species.Spirit?"Charge":enemy.Kind==WinterMonsterEnemy.Species.Golem?"Attack_Charge":"Attack_Warning");
                    if(enemy.Kind==WinterMonsterEnemy.Species.Mammoth)frost=Sound("Tusk_Frost",.45f);
                }
            }
            if(state==WinterMonsterEnemy.Phase.Idle&&agent&&agent.enabled&&agent.isOnNavMesh&&agent.velocity.magnitude>.12f&&animator)
            {
                // Use gait phase rather than a wall-clock timer, so footsteps follow animation speed.
                var s=animator.GetCurrentAnimatorStateInfo(0);
                float phase=s.normalizedTime*2;
                if(s.IsName("Walk")&&phase>=nextStep){if(enemy.Kind!=WinterMonsterEnemy.Species.Spirit)Sound("Footstep",footstepVolume);nextStep=Mathf.Floor(phase)+1;}
                else if(!s.IsName("Walk")||phase<nextStep-2)nextStep=Mathf.Floor(phase)+1;
            }
            else nextStep=0;
            if(state==WinterMonsterEnemy.Phase.Attack)
            {
                if(enemy.Kind==WinterMonsterEnemy.Species.Golem&&At(1,.28f)){Sound("Slam");Sound("IceWave_Travel",.5f);Sound("IceSpike_Erupt",.45f);}
                if(enemy.Kind==WinterMonsterEnemy.Species.Wolf){if(At(1,.08f))Sound("Leap");if(At(2,.8f))Sound("Land");}
                if(enemy.Kind==WinterMonsterEnemy.Species.Mammoth&&At(1,.36f))Sound("Tusk_Uppercut");
            }
            if(state==WinterMonsterEnemy.Phase.SecondAttack&&At(1,.36f))Sound("Claw_Swing");
        }
        void Die()
        {
            preparation.Stop();frost.Stop();
            GameAudio.Play(Prefix+"_Death",transform.position,deathVolume,minimumInterval:0);
            if(!deathAnimation||!animator)return;
            // A visual-only corpse survives the existing health object's immediate deactivation.
            // It has no Enemy/Health/Collider, so it cannot hold room clear or pay rewards twice.
            var corpse=Instantiate(animator.gameObject,animator.transform.position,animator.transform.rotation,transform.parent);
            corpse.name=Prefix+" Death Visual";corpse.transform.localScale=animator.transform.lossyScale;
            if(corpse.transform.parent)corpse.transform.localScale=new Vector3(animator.transform.lossyScale.x/corpse.transform.parent.lossyScale.x,animator.transform.lossyScale.y/corpse.transform.parent.lossyScale.y,animator.transform.lossyScale.z/corpse.transform.parent.lossyScale.z);
            foreach(var a in corpse.GetComponentsInChildren<Animator>())a.enabled=false;
            foreach(var c in corpse.GetComponentsInChildren<Collider>())c.enabled=false;
            corpse.AddComponent<WinterDeathVisual>().Begin(deathAnimation,corpseHold,enemy.Kind==WinterMonsterEnemy.Species.Spirit);
        }
    }
}
