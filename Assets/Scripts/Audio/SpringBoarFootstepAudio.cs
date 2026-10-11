using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace DogLife
{
    // Detect the planted end of each animated hoof's descent, rather than a timer.
    [DefaultExecutionOrder(200)]
    public sealed class SpringBoarFootstepAudio : MonoBehaviour
    {
        sealed class Hoof
        {public Transform bone;public Animator animator;public float previous,fall,next;public bool sampled;}
        readonly List<Hoof> hooves=new List<Hoof>();
        NavMeshAgent agent;CombatHealth health;
        SpringBoarPhaseTransition transition;SpringBoarDeath death;
        void Start()
        {
            agent=GetComponent<NavMeshAgent>();health=GetComponent<CombatHealth>();
            transition=GetComponent<SpringBoarPhaseTransition>();death=GetComponent<SpringBoarDeath>();
            foreach(var bone in GetComponentsInChildren<Transform>(true))
                if(bone.name=="LFrontFoot"||bone.name=="RFrontFoot"||bone.name=="LRearFoot"||bone.name=="RRearFoot")
                    hooves.Add(new Hoof{bone=bone,animator=bone.GetComponentInParent<Animator>()});
        }
        void LateUpdate()
        {
            bool moving=Time.timeScale>0&&health&&!health.Dead&&agent&&agent.enabled&&agent.isOnNavMesh&&agent.velocity.sqrMagnitude>.04f;
            if(transition&&transition.Transitioning||death&&(death.Dying||death.Corpse))moving=false;
            foreach(var h in hooves)
            {
                if(!h.bone||!h.animator)continue;
                bool walk=moving&&h.animator.isActiveAndEnabled&&h.animator.GetCurrentAnimatorStateInfo(0).IsName("Walk");
                if(!walk){h.sampled=false;h.fall=0;continue;}
                float y=h.bone.position.y-transform.position.y;
                if(!h.sampled){h.previous=y;h.sampled=true;continue;}
                float delta=y-h.previous;h.previous=y;
                if(delta<-.0001f)h.fall-=delta;
                else if(delta>=0&&h.fall>.025f)
                {
                    if(Time.time>=h.next){h.next=Time.time+.2f;GameAudio.Play("Boar_Footstep",h.bone.position,.45f,Random.Range(.94f,1.04f),minimumInterval:.09f);}
                    h.fall=0;
                }
            }
        }
    }
}
