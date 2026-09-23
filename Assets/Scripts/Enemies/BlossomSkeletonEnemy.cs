using UnityEngine;
using UnityEngine.AI;

namespace DogLife
{
    [RequireComponent(typeof(Rigidbody), typeof(CombatHealth), typeof(NavMeshAgent))]
    // StageEnemy의 체력/내비게이션/Animator 규약을 사용하는 해골 전용 근접 몬스터입니다.
    public sealed class BlossomSkeletonEnemy : MonoBehaviour
    {
        public float speed=2, damage=5, detectionRange=13, attackRange=1.7f;
        public float hitRange=2, attackAngle=100, hitHeight=1.4f;
        public float hitTime=.61f, attackDuration=1.05f, recovery=.2f;
        [SerializeField,Min(0),Tooltip("공격 종료 후 다음 공격까지의 최소 대기 시간(초)")] private float skillDelay=2.5f;
        [SerializeField,Min(0)] private float knockbackSpeed=3;
        public Transform visualRoot;
        public Animator modelAnimator;
        public LayerMask obstacleMask=1<<8;
        public bool Windup => attacking && Time.time-attackStarted<hitTime;
        CombatHealth health, targetHealth;
        Transform target;
        NavMeshAgent agent;
        Renderer[] visualRenderers;
        bool attacking, hitApplied;
        float attackStarted, nextAttack, nextPathUpdate;
        Vector3 lockedAim;
        BlossomSkeletonAttackVfx attackVfx;
        float Size=>Mathf.Max(.01f,transform.lossyScale.x);
        // 모델 배율을 다시 곱하지 않고 실제 베기 궤적의 전방 도달 거리를 구합니다.
        public float EffectiveAttackRange
        {
            get
            {
                float reach=0;
                var forward=visualRoot?visualRoot.forward:transform.forward;
                if(attackVfx)for(int i=0;i<=32;i++)
                    if(attackVfx.TryGetStrikeSegment(i/32f,out var grip,out var tip))
                        reach=Mathf.Max(reach,Vector3.Dot(grip-transform.position,forward),Vector3.Dot(tip-transform.position,forward));
                return reach>.1f?Mathf.Min(attackRange*Size,Mathf.Max(.1f,reach-.1f)):Mathf.Min(attackRange,hitRange)*Size;
            }
        }
        static readonly int Moving=Animator.StringToHash("Moving"), Attacking=Animator.StringToHash("Attacking");

        void Awake()
        {
            health=GetComponent<CombatHealth>();agent=GetComponent<NavMeshAgent>();
            attackVfx=GetComponentInChildren<BlossomSkeletonAttackVfx>(true);
            var body=GetComponent<Rigidbody>();body.isKinematic=true;body.constraints=RigidbodyConstraints.FreezeRotation;
            agent.updateRotation=false;
            visualRenderers=(visualRoot?visualRoot:transform).GetComponentsInChildren<Renderer>(true);
            if(modelAnimator)modelAnimator.applyRootMotion=false;
        }
        void Start()
        {
            var player=FindFirstObjectByType<PlayerMotor>();
            if(player)
            {
                target=player.transform;targetHealth=player.GetComponent<CombatHealth>();
                var own=GetComponent<Collider>();var other=player.GetComponent<Collider>();
                if(own&&other)Physics.IgnoreCollision(own,other);
            }
            nextAttack=Time.time+1;
        }
        bool ClearSight(Vector3 delta) => !Physics.Raycast(transform.position,delta.normalized,delta.magnitude,obstacleMask,QueryTriggerInteraction.Ignore);
        public bool CanHit(Vector3 position)
        {
            var delta=position-transform.position;
            if(Mathf.Abs(delta.y)>hitHeight*Size)return false;
            var flat=Vector3.ProjectOnPlane(delta,Vector3.up);
            return flat.magnitude<=hitRange*Size && Vector3.Angle(lockedAim,flat)<=attackAngle*.5f && ClearSight(delta);
        }
        void StopNavigation()
        {
            if(agent&&agent.isActiveAndEnabled&&agent.isOnNavMesh){agent.isStopped=true;agent.ResetPath();agent.velocity=Vector3.zero;}
        }
        void FixedUpdate()
        {
            if(CombatFeedback.Paused(this)){attackStarted+=Time.fixedDeltaTime;nextAttack+=Time.fixedDeltaTime;return;}
            if(!target||!targetHealth||health.Dead||targetHealth.Dead){CancelAttack();return;}
            if(attacking)
            {
                StopNavigation();float elapsed=Time.time-attackStarted;
                if(!hitApplied&&elapsed>=hitTime)
                {
                    hitApplied=true;
                    if(attackVfx?attackVfx.HitsTarget(targetHealth)&&ClearSight(target.position-transform.position):CanHit(target.position))
                    {
                        float before=targetHealth.Current;
                        var collider=target.GetComponent<Collider>();
                        CombatFeedback.DealDamage(targetHealth,damage,collider?collider.ClosestPoint(transform.position):target.position,lockedAim,1.4f,transform,new Color(1,.55f,.72f,1));
                        var motor=target.GetComponent<PlayerMotor>();
                        if(motor&&!targetHealth.Dead&&targetHealth.Current<before)motor.ApplyKnockback(target.position-transform.position,knockbackSpeed,.6f,.22f);
                    }
                }
                bool animationFinished=!modelAnimator||!modelAnimator.isActiveAndEnabled||
                    (modelAnimator.GetCurrentAnimatorStateInfo(0).IsName("Attack")&&modelAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime>=1);
                if(elapsed>=attackDuration&&animationFinished)
                {attacking=false;nextAttack=Time.time+Mathf.Max(recovery,skillDelay);if(modelAnimator)modelAnimator.SetBool(Attacking,false);}
                return;
            }
            if(Time.time<nextAttack&&Time.time-attackStarted<attackDuration+recovery){StopNavigation();return;}
            var delta=target.position-transform.position;float distance=Vector3.ProjectOnPlane(delta,Vector3.up).magnitude;bool clear=ClearSight(delta);
            float effectiveRange=EffectiveAttackRange;
            float stopDistance=effectiveRange*.85f;
            bool walking=distance<detectionRange&&(distance>stopDistance||!clear);
            var heading=Vector3.ProjectOnPlane(delta,Vector3.up).normalized;
            if(agent.isActiveAndEnabled&&agent.isOnNavMesh)
            {
                agent.isStopped=!walking;agent.speed=speed;agent.stoppingDistance=clear?stopDistance:.1f;
                if(walking&&Time.time>=nextPathUpdate)
                {
                    nextPathUpdate=Time.time+.25f;
                    var filter=new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask};
                    if(NavMesh.SamplePosition(target.position-Vector3.up,out var hit,3,filter))agent.SetDestination(hit.position);
                    else agent.ResetPath();
                }
                if(walking&&agent.desiredVelocity.sqrMagnitude>.001f)heading=agent.desiredVelocity.normalized;
            }
            if(visualRoot&&heading.sqrMagnitude>.001f)visualRoot.rotation=Quaternion.LookRotation(heading);
            if(distance<=effectiveRange&&clear&&Time.time>=nextAttack)
            {
                lockedAim=Vector3.ProjectOnPlane(delta,Vector3.up).normalized;
                if(lockedAim.sqrMagnitude<.001f)lockedAim=visualRoot?visualRoot.forward:transform.forward;
                if(visualRoot)visualRoot.rotation=Quaternion.LookRotation(lockedAim);
                attacking=true;hitApplied=false;attackStarted=Time.time;StopNavigation();
                if(modelAnimator){modelAnimator.SetBool(Moving,false);modelAnimator.SetBool(Attacking,true);}
            }
        }
        void Update()
        {
            if(!modelAnimator||!modelAnimator.isActiveAndEnabled)return;
            modelAnimator.SetBool(Moving,!attacking&&!health.Dead&&targetHealth&&!targetHealth.Dead&&agent.isActiveAndEnabled&&agent.isOnNavMesh&&!agent.isStopped&&agent.velocity.sqrMagnitude>.01f);
        }
        void CancelAttack()
        {
            attacking=false;hitApplied=false;StopNavigation();
            if(modelAnimator&&modelAnimator.isActiveAndEnabled){modelAnimator.SetBool(Moving,false);modelAnimator.SetBool(Attacking,false);}
        }
        void OnDisable(){CancelAttack();nextPathUpdate=0;nextAttack=Time.time+.5f;}
    }
}

