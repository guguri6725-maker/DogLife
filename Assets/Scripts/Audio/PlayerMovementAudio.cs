using System.Collections.Generic;
using UnityEngine;
namespace DogLife
{
    [RequireComponent(typeof(PlayerMotor))]
    public sealed class PlayerMovementAudio:MonoBehaviour
    {
        [SerializeField,Min(.2f)] float stepDistance=1.35f;
        [SerializeField,Min(.05f)] float minimumStepInterval=.18f;
        readonly Transform[] feet=new Transform[2];
        readonly List<AnimatorClipInfo> clips=new List<AnimatorClipInfo>();
        PlayerMotor motor;CombatHealth health;Animator animator;Rigidbody body;
        Vector3 previous;float distance,nextStep;bool grounded,initialized,authoredContacts;
        void Awake()
        {
            motor=GetComponent<PlayerMotor>();health=GetComponent<CombatHealth>();body=GetComponent<Rigidbody>();previous=transform.position;
            animator=GetComponentInChildren<Animator>();
            if(!animator)return;
            foreach(var bone in animator.GetComponentsInChildren<Transform>())
            {if(bone.name=="LeftFoot")feet[0]=bone;else if(bone.name=="RightFoot")feet[1]=bone;}
            var relay=animator.GetComponent<FootstepAudioRelay>();
            if(!relay)relay=animator.gameObject.AddComponent<FootstepAudioRelay>();
            relay.Owner=this;
            if(animator.runtimeAnimatorController)
                foreach(var clip in animator.runtimeAnimatorController.animationClips)
                    foreach(var evt in clip.events)if(evt.functionName=="OnFootContact")authoredContacts=true;
        }
        bool CanStep()=>Time.timeScale>0&&motor.enabled&&!(health&&health.Dead)&&motor.Grounded&&!motor.IsDashing&&!motor.IsKnockedBack&&motor.MoveInput.sqrMagnitude>.01f;
        public void FootContact(AnimationEvent evt)
        {
            if(!CanStep()||Time.time<nextStep||!animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion"))return;
            if(body&&Vector3.ProjectOnPlane(body.linearVelocity,Vector3.up).sqrMagnitude<.0025f)return;
            // Only the dominant directional clip owns contacts; blended clips must not double steps.
            animator.GetCurrentAnimatorClipInfo(0,clips);
            AnimationClip dominant=null;float weight=0;
            foreach(var info in clips)if(info.weight>weight){weight=info.weight;dominant=info.clip;}
            if(evt.animatorClipInfo.clip!=dominant)return;
            var foot=feet[Mathf.Clamp(evt.intParameter,0,1)];
            Step(foot?foot.position:transform.position);nextStep=Time.time+minimumStepInterval;
        }
        void LateUpdate()
        {
            Vector3 position=transform.position;float moved=Vector3.ProjectOnPlane(position-previous,Vector3.up).magnitude;previous=position;
            if(!initialized){grounded=motor.Grounded;initialized=true;return;}
            if(Time.timeScale==0)return;
            bool landed=!grounded&&motor.Grounded&&!motor.IsDashing;grounded=motor.Grounded;
            if(landed&&!(health&&health.Dead)){GameAudio.Play("Land",position,.55f);nextStep=Time.time+.2f;}
            if(!CanStep()||moved>2){distance=0;return;}
            if(!authoredContacts){distance+=moved;if(distance>=stepDistance){distance%=stepDistance;Step(position);}}
        }
        void Step(Vector3 position){GameAudio.Variant("Step",position,motor.Crouching?.16f:.30f);}
    }
}
