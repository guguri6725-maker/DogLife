using UnityEngine;
using UnityEngine.AI;

namespace DogLife
{
    // 전역 시간을 바꾸지 않고 맞은 몸과 근접 공격 애니메이션만 짧게 멈춥니다.
    [DefaultExecutionOrder(15000)]
    public sealed class CombatFeedback : MonoBehaviour
    {
        [SerializeField,Range(0,.12f)] private float normalStop=.04f;
        [SerializeField,Range(0,.15f)] private float heavyStop=.08f;
        [SerializeField,Range(0,30)] private float recoilAngle=13;
        [SerializeField,Min(.05f)] private float recoilDuration=.22f;
        Animator animator;
        NavMeshAgent agent;
        Transform bone;
        float stopUntil,nextStop,recoilStarted=-10,originalSpeed,agentSpeed,strength;
        bool stopped,poseApplied;
        Quaternion baseRotation;
        Vector3 recoilAxis;
        public bool IsStopped=>stopped&&Time.time<stopUntil;
        void Awake()
        {
            animator=GetComponentInChildren<Animator>();agent=GetComponent<NavMeshAgent>();
            var bones=GetComponentsInChildren<Transform>();
            foreach(string name in new[]{"Spine2","Chest","Spine","Head"})
            {
                foreach(var t in bones)if(t.name==name){bone=t;break;}
                if(bone)break;
            }
        }
        public static bool Paused(Component actor)
        {var feedback=actor.GetComponent<CombatFeedback>();return feedback&&feedback.IsStopped;}
        public static bool DealDamage(CombatHealth target,float amount,Vector3 contact,Vector3 direction,float power,Transform attacker,Color tint,bool useFullPlayerDamage=false)
        {
            if(!target)return false;
            float before=target.Current;target.Damage(amount,useFullPlayerDamage);
            if(target.Current>=before)return false;
            if(!target.player)CombatContactVfx.Spawn(contact,direction,tint,power);
            if(!target.Dead&&target.gameObject.activeInHierarchy)
            {
                var feedback=target.GetComponent<CombatFeedback>();if(!feedback)feedback=target.gameObject.AddComponent<CombatFeedback>();
                feedback.React(direction,power);
            }
            if(attacker&&attacker.gameObject.activeInHierarchy)
            {
                var feedback=attacker.GetComponent<CombatFeedback>();if(!feedback)feedback=attacker.gameObject.AddComponent<CombatFeedback>();
                feedback.Freeze(power);
            }
            return true;
        }
        public void Freeze(float power)
        {
            if(Time.time<nextStop)return;
            float duration=power>=1.4f?heavyStop:normalStop;
            nextStop=Time.time+duration+.07f;
            if(!stopped)
            {
                if(animator){originalSpeed=animator.speed;animator.speed=0;}
                if(agent){agentSpeed=agent.speed;agent.speed=0;}
            }
            stopped=true;stopUntil=Time.time+duration;
        }
        void React(Vector3 direction,float power)
        {
            Freeze(power);strength=Mathf.Clamp(power,.6f,1.65f);recoilStarted=Time.time;
            Vector3 worldAxis=Vector3.Cross(Vector3.up,direction.normalized).normalized;
            recoilAxis=bone&&bone.parent?bone.parent.InverseTransformDirection(worldAxis):worldAxis;
            if(recoilAxis.sqrMagnitude<.01f)recoilAxis=Vector3.right;
        }
        void RestorePose(){if(poseApplied&&bone)bone.localRotation=baseRotation;poseApplied=false;}
        void Update()
        {
            RestorePose();
            if(stopped&&!IsStopped)Resume();
        }
        void LateUpdate()
        {
            if(!bone)return;
            float t=(Time.time-recoilStarted)/recoilDuration;
            if(t<0||t>=1)return;
            baseRotation=bone.localRotation;
            float envelope=Mathf.Sin(Mathf.PI*Mathf.Sqrt(t))*(1-t);
            bone.localRotation=Quaternion.AngleAxis(recoilAngle*strength*envelope,recoilAxis)*baseRotation;poseApplied=true;
        }
        void Resume()
        {
            if(!stopped)return;stopped=false;
            if(animator)animator.speed=originalSpeed;
            if(agent)agent.speed=agentSpeed;
        }
        public void CancelHitStop(){Resume();}
        void OnDisable(){RestorePose();Resume();recoilStarted=-10;nextStop=0;}
    }
}
