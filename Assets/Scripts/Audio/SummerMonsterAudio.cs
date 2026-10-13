using UnityEngine;
using UnityEngine.AI;
namespace DogLife
{
    [RequireComponent(typeof(SummerMonsterEnemy))]
    public sealed class SummerMonsterAudio : MonoBehaviour
    {
        [Range(0,1)] public float attackVolume=.72f,footstepVolume=.28f;
        SummerMonsterEnemy enemy;CombatHealth health;SummerAudioEmitter voice;Animator animator;NavMeshAgent agent;
        int lastStep=-1;
        void Awake(){enemy=GetComponent<SummerMonsterEnemy>();health=GetComponent<CombatHealth>();voice=SummerAudioEmitter.Get(this);animator=GetComponentInChildren<Animator>();agent=GetComponent<NavMeshAgent>();}
        void OnEnable(){health.Died+=Stop;}
        void OnDisable(){health.Died-=Stop;Stop();}
        public void Stop(){if(voice)voice.StopAll();lastStep=-1;}
        public void Enter(SummerMonsterEnemy.Phase state)
        {
            voice.volume=attackVolume;voice.StopPreparation();
            if(state!=SummerMonsterEnemy.Phase.Burrow&&state!=SummerMonsterEnemy.Phase.Warning)voice.StopSustain();
            switch(enemy.Kind)
            {
                case SummerMonsterEnemy.Species.Mermaid:
                    if(state==SummerMonsterEnemy.Phase.Dive)voice.Cue("Mermaid_Dive");
                    if(state==SummerMonsterEnemy.Phase.Burrow)voice.Sustain("Mermaid_Underwater_Loop",.38f);
                    if(state==SummerMonsterEnemy.Phase.Warning)voice.Prepare("Mermaid_Ambush_Warning");
                    if(state==SummerMonsterEnemy.Phase.Recovery)voice.Cue("Mermaid_Water_Return",.7f);
                    break;
                case SummerMonsterEnemy.Species.Jellyfish:
                    if(state==SummerMonsterEnemy.Phase.Warning)voice.Prepare("Jellyfish_Charge");break;
                case SummerMonsterEnemy.Species.Tortoise:
                    if(state==SummerMonsterEnemy.Phase.Warning)voice.Prepare("Tortoise_Bite_Ready");
                    if(state==SummerMonsterEnemy.Phase.GuardIn)voice.Cue("Tortoise_Shell_Enter",.8f);
                    if(state==SummerMonsterEnemy.Phase.GuardOut)voice.Cue("Tortoise_Shell_Exit",.7f);break;
            }
        }
        public void Impact()
        {
            voice.StopPreparation();voice.StopSustain();
            voice.Cue(enemy.Kind==SummerMonsterEnemy.Species.Mermaid?"Mermaid_Emerge_Attack":enemy.Kind==SummerMonsterEnemy.Species.Jellyfish?"Jellyfish_Discharge":enemy.Kind==SummerMonsterEnemy.Species.Tortoise?"Tortoise_Bite":"Crab_Bubble_Cast");
        }
        public void Block(){GameAudio.Play("Tortoise_Shell_Block",transform.position,.6f,minimumInterval:.12f);}
        void Update()
        {
            if(health.Dead||Time.timeScale<=0||CombatFeedback.Paused(enemy))return;
            if(enemy.Kind!=SummerMonsterEnemy.Species.Tortoise||enemy.State!=SummerMonsterEnemy.Phase.Idle||!animator||!agent||!agent.enabled||!agent.isOnNavMesh||agent.velocity.sqrMagnitude<.02f){lastStep=-1;return;}
            var s=animator.GetCurrentAnimatorStateInfo(0);if(!s.IsName("Walk")){lastStep=-1;return;}
            int step=Mathf.FloorToInt(s.normalizedTime*2);
            if(lastStep>=0&&step!=lastStep)GameAudio.Play("Tortoise_Footstep",transform.position,footstepVolume,minimumInterval:.15f);
            lastStep=step;
        }
    }
}
