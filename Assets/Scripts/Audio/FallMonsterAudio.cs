using UnityEngine;
using UnityEngine.AI;
namespace DogLife
{
    public sealed class FallMonsterAudio : MonoBehaviour
    {
        [Range(0,1)] public float attackVolume=.72f, movementVolume=.23f;
        FallMonsterEnemy enemy; CombatHealth health; SummerAudioEmitter voice;
        Animator animator; NavMeshAgent agent; int lastStep=-1;
        void Awake(){enemy=GetComponent<FallMonsterEnemy>();health=GetComponent<CombatHealth>();voice=SummerAudioEmitter.Get(this);animator=GetComponentInChildren<Animator>();agent=GetComponent<NavMeshAgent>();}
        void OnEnable(){health.Died+=Stop;}
        void OnDisable(){health.Died-=Stop;Stop();}
        public void Stop(){voice.StopAll();lastStep=-1;}
        public void Enter(FallMonsterEnemy.Phase phase)
        {
            voice.volume=attackVolume;voice.StopPreparation();
            var kind=enemy.AttackPattern;
            if(phase==FallMonsterEnemy.Phase.Anticipation)
                voice.Prepare(kind==FallMonsterEnemy.Pattern.Spores?"Mycelium_Charge":kind==FallMonsterEnemy.Pattern.ArmSweep?"Scarecrow_Attack_Ready":kind==FallMonsterEnemy.Pattern.Spikes?"Hedgehog_Spike_Ready":"Crow_Dash_Ready");
            if(phase==FallMonsterEnemy.Phase.Active&&kind!=FallMonsterEnemy.Pattern.Spikes)
            {
                voice.Cue(kind==FallMonsterEnemy.Pattern.Spores?"Mycelium_Spore_Burst":kind==FallMonsterEnemy.Pattern.ArmSweep?"Scarecrow_Arm_Sweep":"Crow_Dash");
                if(kind==FallMonsterEnemy.Pattern.Spores)voice.Sustain("Mycelium_Spore_Loop",.35f);
            }
            if(phase==FallMonsterEnemy.Phase.Recovery)
            {voice.StopSustain();if(kind==FallMonsterEnemy.Pattern.Spikes)voice.Cue("Hedgehog_Spike_Retract",.6f);}
        }
        public void SpikeImpact(){voice.Cue("Hedgehog_Spike_Erupt");}
        void Update()
        {
            if(!enemy.enabled||health.Dead||Time.timeScale<=0||CombatFeedback.Paused(enemy)||enemy.CurrentPhase!=FallMonsterEnemy.Phase.Idle||!animator){lastStep=-1;return;}
            bool crow=enemy.AttackPattern==FallMonsterEnemy.Pattern.CrowDash;
            if(!crow&&(enemy.AttackPattern!=FallMonsterEnemy.Pattern.ArmSweep||!agent||!agent.enabled||!agent.isOnNavMesh||agent.velocity.sqrMagnitude<.02f)){lastStep=-1;return;}
            var state=animator.GetCurrentAnimatorStateInfo(0);
            if(!crow&&!state.IsName("Walk")){lastStep=-1;return;}
            int step=Mathf.FloorToInt(state.normalizedTime*(crow?1:2));
            if(lastStep>=0&&step!=lastStep)GameAudio.Play(crow?"Crow_Wing_Flap":"Scarecrow_Footstep",transform.position,movementVolume,follow:transform,minimumInterval:.2f);
            lastStep=step;
        }
    }
}
