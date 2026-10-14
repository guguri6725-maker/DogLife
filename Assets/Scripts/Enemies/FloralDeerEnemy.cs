using UnityEngine;
using UnityEngine.AI;

namespace DogLife
{
    [RequireComponent(typeof(CombatHealth),typeof(Rigidbody),typeof(NavMeshAgent))]
    public sealed class FloralDeerEnemy : MonoBehaviour
    {
        public Animator modelAnimator;
        public Transform visualRoot;
        public LineRenderer warning;
        public ParticleSystem scrapeEffect,chargeEffect,impactEffect;
        public float speed=2.3f,detectionRange=14,attackRange=9;
        public float windupTime=1.35f,chargeSpeed=9,chargeDistance=7,recoveryTime=1,cooldown=1.1f;
        public float damage=7,hitRadius=.38f,bodyFront=.42f;
        [SerializeField,Min(0),Tooltip("회복 종료 후 다음 돌진까지의 최소 대기 시간(초)")] private float skillDelay=3.5f;
        [SerializeField,Min(0)] private float knockbackSpeed=9;
        [SerializeField,Min(0)] private float knockbackLift=4.5f;
        public bool IsCharging=>phase==Phase.Charge;
        public Vector3 ImpactDirection(Vector3 victimPosition,Vector3 contactOrigin)
        {
            var right=Vector3.Cross(Vector3.up,lockedDirection).normalized;
            float side=Vector3.Dot(victimPosition-contactOrigin,right);
            return Mathf.Abs(side)<HornRadius*.25f?lockedDirection:(lockedDirection*.65f+right*Mathf.Sign(side)*.9f).normalized;
        }
        enum Phase{Track,Windup,Charge,Recover}
        Phase phase;
        CombatHealth health,targetHealth;
        Transform target;
        NavMeshAgent agent;
        Vector3 lockedDirection;
        float phaseStarted,travelled,nextAttack,nextPath;
        bool hitApplied;
        int scrapeCount;
        string animationName;
        GameAudio.Handle readySound,chargeSound;
        public bool Windup=>phase==Phase.Windup;
        bool Navigable=>agent&&agent.isActiveAndEnabled&&agent.isOnNavMesh;
        float WorldScale=>Mathf.Max(.01f,transform.lossyScale.y);
        float GroundOffset=>(agent?agent.baseOffset:1)*WorldScale;
        float HornReach=>bodyFront*WorldScale;
        float HornRadius=>hitRadius*WorldScale;
        // 돌진으로 도달하지 못하는 거리에서는 준비 동작을 시작하지 않습니다.
        public float EffectiveAttackRange=>Mathf.Min(attackRange,chargeDistance+HornReach+HornRadius);
        void Awake()
        {
            health=GetComponent<CombatHealth>();agent=GetComponent<NavMeshAgent>();agent.updateRotation=false;
            var body=GetComponent<Rigidbody>();body.isKinematic=true;body.constraints=RigidbodyConstraints.FreezeRotation;
        }
        ForestRoom combatRoom;
        void Start()
        {
            combatRoom=GetComponentInParent<ForestRoom>();
            var player=FindFirstObjectByType<PlayerMotor>();
            if(player)
            {
                target=player.transform;targetHealth=player.GetComponent<CombatHealth>();
                // 머리/목 피격 판정이 플레이어 이동을 막지 않도록 모두 제외합니다.
                foreach(var own in GetComponentsInChildren<Collider>())
                    foreach(var other in player.GetComponentsInChildren<Collider>())Physics.IgnoreCollision(own,other);
            }
            nextAttack=Time.time+1;
        }
        void Animate(string name)
        {
            // 새 보행의 접지 속도에 실제 이동 속도를 맞추고, 공격 동작은 원래 속도로 재생합니다.
            if(modelAnimator)
            {
                const float authoredWalkSpeed=.28f/(.75f*1.2f);
                float modelScale=Mathf.Max(.001f,Mathf.Abs(modelAnimator.transform.lossyScale.z));
                float travelSpeed=Navigable?Vector3.ProjectOnPlane(agent.velocity,Vector3.up).magnitude:0;
                modelAnimator.speed=name=="Walk"?travelSpeed/(authoredWalkSpeed*modelScale):1f;
            }
            if(animationName==name)return;animationName=name;
            if(modelAnimator&&modelAnimator.isActiveAndEnabled)modelAnimator.CrossFadeInFixedTime(name,.06f,0);
        }
        void Stop(){if(Navigable){agent.isStopped=true;agent.ResetPath();}}
        void StopEffects()
        {
            readySound.Stop();chargeSound.Stop();
            if(warning)warning.enabled=false;
            if(scrapeEffect)scrapeEffect.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            if(chargeEffect)chargeEffect.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            if(impactEffect)impactEffect.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        // 몸 앞쪽의 실제 이동 구간을 검사하여 얇은 벽도 통과하지 않습니다.
        public float ObstacleDistance(Vector3 position,Vector3 direction,float distance)
        {
            float allowed=distance;Vector3 origin=position+direction*HornReach;
            foreach(var hit in Physics.SphereCastAll(origin,HornRadius,direction,distance,~0,QueryTriggerInteraction.Ignore))
            {
                if(hit.transform.IsChildOf(transform)||hit.collider.GetComponentInParent<CombatHealth>())continue;
                allowed=Mathf.Min(allowed,Mathf.Max(0,hit.distance-.04f));
            }
            foreach(var hit in Physics.OverlapSphere(origin,HornRadius,~0,QueryTriggerInteraction.Ignore))
                if(!hit.transform.IsChildOf(transform)&&!hit.GetComponentInParent<CombatHealth>())return 0;
            return allowed;
        }
        void DrawWarning()
        {
            if(!warning)return;
            float length=ObstacleDistance(transform.position,lockedDirection,chargeDistance);
            Vector3 start=transform.position-Vector3.up*GroundOffset+Vector3.up*.035f;
            start+=lockedDirection*HornReach;
            Vector3 end=start+lockedDirection*length,side=Vector3.Cross(Vector3.up,lockedDirection);
            warning.positionCount=35;
            for(int i=0;i<=16;i++)
            {
                float angle=Mathf.Lerp(-Mathf.PI*.5f,Mathf.PI*.5f,i/16f);
                warning.SetPosition(i,end+(lockedDirection*Mathf.Cos(angle)+side*Mathf.Sin(angle))*HornRadius);
                angle+=Mathf.PI;
                warning.SetPosition(i+17,start+(lockedDirection*Mathf.Cos(angle)+side*Mathf.Sin(angle))*HornRadius);
            }
            warning.SetPosition(34,warning.GetPosition(0));warning.enabled=true;
        }
        void BeginWindup(Vector3 direction)
        {
            readySound=GameAudio.Play("Deer_Charge_Ready",transform.position,.8f,follow:transform);
            Stop();phase=Phase.Windup;phaseStarted=Time.time;scrapeCount=0;hitApplied=false;
            lockedDirection=Vector3.ProjectOnPlane(direction,Vector3.up).normalized;
            if(lockedDirection.sqrMagnitude<.001f)lockedDirection=transform.forward;
            transform.rotation=Quaternion.LookRotation(lockedDirection);DrawWarning();Animate("Windup");
        }
        void BeginRecovery()
        {
            Stop();phase=Phase.Recover;phaseStarted=Time.time;StopEffects();Animate("Recover");
        }
        void DamageSweep(Vector3 from,Vector3 to)
        {
            if(hitApplied||!targetHealth||targetHealth.Dead)return;
            foreach(var collider in Physics.OverlapCapsule(from+lockedDirection*HornReach,to+lockedDirection*HornReach,HornRadius,~0,QueryTriggerInteraction.Ignore))
            {
                if(collider.GetComponentInParent<CombatHealth>()!=targetHealth)continue;
                hitApplied=true;float before=targetHealth.Current;
                CombatFeedback.DealDamage(targetHealth,damage,collider.ClosestPoint(to+lockedDirection*HornReach),lockedDirection,1.6f,transform,new Color(1,.7f,.8f,1));
                var motor=target.GetComponent<PlayerMotor>();
                if(motor&&!targetHealth.Dead&&targetHealth.Current<before)motor.ApplyKnockback(ImpactDirection(target.position,from),knockbackSpeed,knockbackLift,.65f);
                Animate("Impact");if(impactEffect){impactEffect.transform.position=to+lockedDirection*HornReach;impactEffect.Play();}break;
            }
        }
        void TickCharge()
        {
            if(!Navigable){BeginRecovery();return;}
            float requested=Mathf.Min(chargeSpeed*Time.fixedDeltaTime,chargeDistance-travelled);
            Vector3 from=transform.position;float step=ObstacleDistance(from,lockedDirection,requested);
            var filter=new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask};
            NavMeshHit startHit,edge;
            if(!NavMesh.SamplePosition(from-Vector3.up*GroundOffset,out startHit,.8f,filter)){BeginRecovery();return;}
            if(NavMesh.Raycast(startHit.position,startHit.position+lockedDirection*step,out edge,filter))step=Mathf.Min(step,Mathf.Max(0,edge.distance-.05f));
            if(step<=.001f){BeginRecovery();return;}
            Vector3 to=from+lockedDirection*step;DamageSweep(from,to);agent.Move(lockedDirection*step);travelled+=step;
            if(step<requested-.001f||travelled>=chargeDistance-.01f||Time.time-phaseStarted>=chargeDistance/Mathf.Max(.1f,chargeSpeed)+.3f)BeginRecovery();
        }
        void FixedUpdate()
        {
            // 로딩된 다음 방에서는 대기하고, 문 닫힘 시작 후에만 AI를 실행합니다.
            if(combatRoom&&!combatRoom.CombatStarted)return;
            if(CombatFeedback.Paused(this)){phaseStarted+=Time.fixedDeltaTime;nextAttack+=Time.fixedDeltaTime;return;}
            if(!health||health.Dead||!target||!targetHealth||targetHealth.Dead){Cancel();return;}
            if(phase==Phase.Windup)
            {
                Stop();float elapsed=Time.time-phaseStarted;
                if(scrapeCount<2&&elapsed>=.25f+scrapeCount*.45f){scrapeCount++;if(scrapeEffect){scrapeEffect.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);scrapeEffect.Play();}}
                if(elapsed>=windupTime){readySound.Stop();chargeSound=GameAudio.Play("Deer_Charge",transform.position,.9f,follow:transform);phase=Phase.Charge;phaseStarted=Time.time;travelled=0;if(warning)warning.enabled=false;if(chargeEffect)chargeEffect.Play();Animate("Charge");}
                return;
            }
            if(phase==Phase.Charge){TickCharge();return;}
            if(phase==Phase.Recover)
            {
                Stop();if(Time.time-phaseStarted>=recoveryTime){phase=Phase.Track;nextAttack=Time.time+Mathf.Max(cooldown,skillDelay);Animate("Idle");}return;
            }
            var delta=Vector3.ProjectOnPlane(target.position-transform.position,Vector3.up);float distance=delta.magnitude;
            if(!combatRoom&&distance>detectionRange){Stop();Animate("Idle");return;}
            if(delta.sqrMagnitude>.001f)transform.rotation=Quaternion.LookRotation(delta);
            bool clear=ObstacleDistance(transform.position,delta.normalized,Mathf.Max(0,distance-bodyFront))>=distance-bodyFront-.1f;
            float reachableRange=EffectiveAttackRange;
            if(Time.time>=nextAttack&&distance<=reachableRange&&clear){BeginWindup(delta);return;}
            if(distance>reachableRange*.8f||!clear)
            {
                if(Navigable&&Time.time>=nextPath)
                {
                    nextPath=Time.time+.3f;agent.isStopped=false;agent.speed=speed;agent.stoppingDistance=clear?reachableRange*.7f:.1f;
                    var filter=new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask};NavMeshHit hit;
                    if(NavMesh.SamplePosition(target.position-Vector3.up,out hit,2,filter))agent.SetDestination(hit.position);
                }
                Animate(Navigable&&agent.velocity.sqrMagnitude>.02f?"Walk":"Idle");
            }
            else{Stop();Animate("Idle");}
        }
        void Cancel(){Stop();StopEffects();phase=Phase.Track;hitApplied=false;scrapeCount=0;Animate("Idle");}
        void OnDisable(){Cancel();animationName=null;nextAttack=Time.time+1;nextPath=0;}
    }
}

