using UnityEngine;
using UnityEngine.AI;
namespace DogLife
{
    [RequireComponent(typeof(CombatHealth),typeof(NavMeshAgent),typeof(Rigidbody))]
    public sealed class SummerMonsterEnemy : CombatDamageGate
    {
        public enum Species { Mermaid, Jellyfish, Tortoise, Crab }
        public enum Phase { Idle, Dive, Burrow, Warning, Attack, Recovery, GuardIn, Guard, GuardOut }
        [SerializeField] private Species species;
        [SerializeField] private float moveSpeed=2, detectionRange=18,attackDistance=4;
        [SerializeField,Min(.1f)] private float warningTime=.85f,attackTime=.45f,recoveryTime=1.1f,cooldown=2.5f;
        [SerializeField,Min(.1f)] private float attackRadius=2,damage=1;
        [SerializeField,Min(0),Tooltip("이동 표면 위 물 렌더러에 범위 표시가 묻히지 않도록 띄우는 높이")]
        private float surfaceEffectHeight=.23f;
        [Header("인어 잠수")]
        [SerializeField,Min(.1f)] private float diveTime=.55f,submergedSpeed=3.5f,maxSubmergedTime=4,diveDepth=3;
        [Header("거북 방어")]
        [SerializeField,Min(.1f)] private float defenseDuration=1.5f,defenseCooldown=4;
        [SerializeField] private float defenseSenseRange=4;
        [Header("소라게 거품")]
        [SerializeField,Min(.1f)] private float bubbleSpeed=2,bubbleLifetime=7,bubbleTurnSpeed=55;
        [SerializeField,Range(0,.85f)] private float moveReduction=.3f,rollReduction=.3f;
        [SerializeField,Min(.1f)] private float debuffDuration=3;
        [Header("플레이어 피격 반응")]
        [SerializeField,Min(.02f),Tooltip("해파리 적중 시 이동/공격 정지 시간")]
        private float jellyStunDuration=.5f;
        [SerializeField,Min(0)] private float tortoisePushSpeed=3;
        [SerializeField,Min(.02f)] private float tortoisePushDuration=.22f;
        [SerializeField,Min(0)] private float mermaidLaunchSpeed=11,mermaidLaunchLift=7;
        [SerializeField,Min(.02f)] private float mermaidLaunchDuration=.75f;
        [SerializeField,Min(0)] private float bubblePushSpeed=1.4f;
        [SerializeField,Min(.02f)] private float bubblePushDuration=.12f;
        [Header("프리팹 연결")]
        [SerializeField] private Transform visualRoot,bubbleOrigin,submergedEffectAnchor;
        [SerializeField] private Animator animator;
        [SerializeField] private SphereCollider attackCollider;
        [SerializeField] private SummerWaterVfx presentation;
        [SerializeField] private Material effectMaterial;
        [SerializeField] private LayerMask obstacleMask=1<<8;
        public Species Kind=>species;
        public Phase State { get; private set; }
        public float Radius=>attackRadius;
        public Vector3 AttackForward=>facing;
        public Vector3 WarningCenter=>AreaCenter+Vector3.up*surfaceEffectHeight;
        public Vector3 SurfaceCenter=>transform.position+Vector3.up*surfaceEffectHeight;
        public Vector3 SubmergedEffectPosition=>submergedEffectAnchor?submergedEffectAnchor.position:transform.position+Vector3.up*.065f;
        public float Progress=>Mathf.Clamp01(elapsed/Mathf.Max(.01f,Duration(State)));
        // 준비와 공격 선행 동작을 합쳐 첫 판정/발사 순간에 채움을 완료합니다.
        public float TelegraphProgress=>Mathf.Clamp01((State==Phase.Warning?elapsed:warningTime+elapsed)/Mathf.Max(.01f,warningTime+attackTime*.30f));
        public Vector3 AreaCenter=>transform.position+(species==Species.Tortoise?facing*1.05f:Vector3.zero);
        public bool Submerged=>State==Phase.Dive||State==Phase.Burrow||State==Phase.Warning&&species==Species.Mermaid;
        public bool Defending=>State==Phase.GuardIn||State==Phase.Guard||State==Phase.GuardOut;
        public float DefenseRemaining=>Mathf.Max(0,nextDefense-Time.time);
        NavMeshAgent agent;CombatHealth health,targetHealth;PlayerMotor player;Collider body,targetCollider;
        Vector3 facing=Vector3.forward,visualHome;float elapsed,nextAttack,nextPath,nextDefense;bool hit,spawned;
        bool Nav=>agent&&agent.isActiveAndEnabled&&agent.isOnNavMesh;
        SummerMonsterAudio sound;bool soundedAttack;
        void Awake()
        {
            agent=GetComponent<NavMeshAgent>();health=GetComponent<CombatHealth>();body=GetComponent<Collider>();
            var rb=GetComponent<Rigidbody>();rb.isKinematic=true;rb.constraints=RigidbodyConstraints.FreezeRotation;
            agent.updateRotation=false;agent.updateUpAxis=false;
            if(visualRoot)visualHome=visualRoot.localPosition;
            if(animator)animator.applyRootMotion=false;
            if(attackCollider){attackCollider.isTrigger=true;attackCollider.enabled=false;attackCollider.radius=attackRadius;}
            sound=GetComponent<SummerMonsterAudio>();if(!sound)sound=gameObject.AddComponent<SummerMonsterAudio>();
        }
        void OnEnable(){State=Phase.Idle;elapsed=0;nextAttack=Time.time+1;nextDefense=Time.time;}
        ForestRoom combatRoom;
        void Start()
        {
            combatRoom=GetComponentInParent<ForestRoom>();
            player=FindFirstObjectByType<PlayerMotor>();if(player){targetHealth=player.GetComponent<CombatHealth>();targetCollider=player.GetComponent<Collider>();if(body&&targetCollider)Physics.IgnoreCollision(body,targetCollider);}
            Play("Idle",1);
        }
        float Duration(Phase p)=>p==Phase.Dive?diveTime:p==Phase.Burrow?maxSubmergedTime:p==Phase.Warning?warningTime:p==Phase.Attack?attackTime:p==Phase.Recovery?recoveryTime:p==Phase.Guard?defenseDuration:.25f;
        void Stop(){if(Nav){agent.isStopped=true;agent.ResetPath();agent.velocity=Vector3.zero;}}
        void Face(Vector3 d){d.y=0;if(d.sqrMagnitude<.001f)return;facing=d.normalized;if(visualRoot)visualRoot.rotation=Quaternion.LookRotation(facing);}
        void Play(string state,float duration)
        {
            if(!animator)return;
            // 인어의 잠수 이동→공격 대기는 같은 수영 루프를 이어 재생합니다.
            if(species==Species.Mermaid&&state=="Burrow"&&
                (animator.GetCurrentAnimatorStateInfo(0).IsName(state)||
                 animator.IsInTransition(0)&&animator.GetNextAnimatorStateInfo(0).IsName(state)))return;
            animator.SetFloat("PhaseSpeed",1/Mathf.Max(.05f,duration));
            // 짧은 출현 공격은 타이밍을 유지하고 긴 동작만 여유 있게 연결합니다.
            float blend=species==Species.Mermaid?Mathf.Min(.16f,duration*.25f):.10f;
            animator.CrossFadeInFixedTime(state,blend);
        }
        void Enter(Phase state)
        {
            State=state;elapsed=0;hit=false;spawned=false;
            soundedAttack=false;if(sound)sound.Enter(state);
            if(attackCollider)attackCollider.enabled=false;
            if(state!=Phase.Burrow&&state!=Phase.Idle)Stop();
            if(state==Phase.Dive)Play("Dive",diveTime);
            if(state==Phase.Burrow)Play("Burrow",1);
            // 인어도 추적을 멈추면 몸을 모으는 별도 준비 자세를 보여줍니다.
            if(state==Phase.Warning)Play("AttackReady",warningTime);
            if(state==Phase.Attack)Play(species==Species.Mermaid?"Emerge":"Attack",attackTime);
            if(state==Phase.Recovery)Play("Recovery",recoveryTime);
            if(state==Phase.GuardIn)Play("GuardIn",.25f);
            if(state==Phase.Guard)Play("GuardLoop",1);
            if(state==Phase.GuardOut)Play("GuardOut",.25f);
            if(state==Phase.Idle){nextAttack=Time.time+cooldown;Play("Idle",1);}
        }
        public override bool BlocksDamage(float amount)
        {
            if(species!=Species.Tortoise||!health||health.Dead)return false;
            if(Defending){if(sound)sound.Block();return true;}
            // 물기 도중에는 방어로 끊지 않습니다. 대기/후딜 동안만 방어를 시작합니다.
            if(Time.time>=nextDefense&&(State==Phase.Idle||State==Phase.Recovery)){Enter(Phase.GuardIn);if(sound)sound.Block();return true;}
            return false;
        }
        void Chase(Vector3 target,float speed)
        {
            if(!Nav)return;agent.isStopped=false;agent.speed=speed;agent.stoppingDistance=.5f;
            if(Time.time>=nextPath){nextPath=Time.time+.18f;NavMeshHit h;if(NavMesh.SamplePosition(target,out h,2,new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask}))agent.SetDestination(h.position);}
            Face(agent.desiredVelocity.sqrMagnitude>.01f?agent.desiredVelocity:target-transform.position);
        }
        bool VisibleTarget()=>!Physics.Linecast(transform.position+Vector3.up*.8f,player.transform.position+Vector3.up*.8f,obstacleMask,QueryTriggerInteraction.Ignore);
        void FixedUpdate()
        {
            // 로딩된 다음 방에서는 대기하고, 문 닫힘 시작 후에만 AI를 실행합니다.
            if(combatRoom&&!combatRoom.CombatStarted)return;
            if(!health||health.Dead||!player||!targetHealth||targetHealth.Dead){Cancel();return;}
            if(CombatFeedback.Paused(this))return;
            var delta=Vector3.ProjectOnPlane(player.transform.position-transform.position,Vector3.up);float distance=delta.magnitude;
            if(State==Phase.Idle)
            {
                if(species==Species.Tortoise&&player.AttackLocked&&distance<defenseSenseRange&&Time.time>=nextDefense&&VisibleTarget()){Enter(Phase.GuardIn);return;}
                if(distance<attackDistance&&Time.time>=nextAttack&&VisibleTarget()){Face(delta);Enter(species==Species.Mermaid?Phase.Dive:Phase.Warning);return;}
                bool move=(combatRoom||distance<detectionRange)&&distance>attackDistance*.7f;
                if(move)Chase(player.transform.position,moveSpeed);else Stop();
                if(animator)animator.SetBool("Moving",move);return;
            }
            elapsed+=Time.fixedDeltaTime;
            if(State==Phase.Dive&&elapsed>=diveTime)Enter(Phase.Burrow);
            else if(State==Phase.Burrow)
            {
                Chase(player.transform.position,submergedSpeed);
                // 추적 제한에 걸려도 현재 위치에 경고 후 출현합니다. 공격 없이 지상으로 튀어나오지 않습니다.
                if(distance<1.7f&&VisibleTarget()||elapsed>=maxSubmergedTime)
                {Face(delta);Enter(Phase.Warning);}
            }
            else if(State==Phase.Warning&&elapsed>=warningTime)Enter(Phase.Attack);
            else if(State==Phase.Attack)
            {
                bool active=Progress>=.30f&&Progress<=.72f;
                if(!soundedAttack&&Progress>=.30f){soundedAttack=true;if(sound)sound.Impact();}
                if(attackCollider){attackCollider.transform.position=AreaCenter+Vector3.up*.8f;attackCollider.enabled=active&&species!=Species.Crab;}
                if(species==Species.Crab&&!spawned&&Progress>=.3f){spawned=true;LaunchBubbles();}
                if(active&&!hit&&species!=Species.Crab)DamageArea();
                if(elapsed>=attackTime)Enter(Phase.Recovery);
            }
            else if(State==Phase.Recovery&&elapsed>=recoveryTime)Enter(Phase.Idle);
            else if(State==Phase.GuardIn&&elapsed>=.25f)Enter(Phase.Guard);
            else if(State==Phase.Guard&&elapsed>=defenseDuration)Enter(Phase.GuardOut);
            else if(State==Phase.GuardOut&&elapsed>=.25f){nextDefense=Time.time+defenseCooldown;Enter(Phase.Idle);}
        }
        void DamageArea()
        {
            if(!attackCollider||!targetCollider)return;
            Vector3 center=attackCollider.transform.position,contact=targetCollider.ClosestPoint(center);
            if((contact-center).sqrMagnitude>attackRadius*attackRadius)return;
            if(Physics.Linecast(center,contact,obstacleMask,QueryTriggerInteraction.Ignore))return;
            hit=true;
            Vector3 direction=Vector3.ProjectOnPlane(player.transform.position-transform.position,Vector3.up);
            if(direction.sqrMagnitude<.001f)direction=facing;
            // 회피/피격 무적으로 피해가 거부되면 정지나 넉백도 적용하지 않습니다.
            if(!CombatFeedback.DealDamage(targetHealth,damage,contact,direction,1,transform,new Color(.3f,.8f,1))||targetHealth.Dead)return;
            if(species==Species.Jellyfish)player.ApplyKnockback(direction,0,0,jellyStunDuration);
            else if(species==Species.Tortoise)player.ApplyKnockback(direction,tortoisePushSpeed,0,tortoisePushDuration);
            else if(species==Species.Mermaid)player.ApplyKnockback(direction,mermaidLaunchSpeed,mermaidLaunchLift,mermaidLaunchDuration);
        }
        void LaunchBubbles()
        {
            for(int i=0;i<3;i++)
            {
                var go=new GameObject("HomingBubble_"+(i+1));UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,gameObject.scene);
                var p=go.AddComponent<SummerHomingBubble>();var dir=Quaternion.AngleAxis((i-1)*28,Vector3.up)*facing;
                p.Initialize((bubbleOrigin?bubbleOrigin.position:transform.position+Vector3.up*.9f)+Vector3.Cross(Vector3.up,facing)*(i-1)*1.05f,dir,player,health,i,effectMaterial,bubbleSpeed,bubbleTurnSpeed,bubbleLifetime,moveReduction,rollReduction,debuffDuration,obstacleMask,bubblePushSpeed,bubblePushDuration);
            }
        }
        void LateUpdate()
        {
            if(!visualRoot)return;
            if(!health||health.Dead||targetHealth&&targetHealth.Dead){Cancel();return;}
            float y=0;
            if(species==Species.Mermaid)
            {
                float t=Progress,pitch=0;
                if(State==Phase.Dive)
                {
                    // 허리를 숙인 뒤 위로 튀지 않고 부드럽게 아래로 잠깁니다.
                    float sink=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.23f,1,t));
                    y=-diveDepth*sink;
                    // 입수 자세는 척추·골반·팔·꼬리 클립이 담당하며 전체 모델을 돌리지 않습니다.
                }
                // 수중 경고 때 몸을 세워 출현 자세로 이어갑니다.
                else if(Submerged){y=-diveDepth;}
                else if(State==Phase.Attack)
                {
                    float rise=Mathf.SmoothStep(0,1,Mathf.Clamp01(t/.30f));
                    float leap=Mathf.Sin(Mathf.Clamp01((t-.15f)/.85f)*Mathf.PI);
                    y=Mathf.Lerp(-diveDepth,0,rise)+leap*1.05f;
                    pitch=Mathf.Lerp(0,-24,rise)*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.5f,1,t)));
                }
                else y=State==Phase.Recovery?-.10f*Mathf.Sin(t*Mathf.PI):Mathf.Sin(Time.time*2)*.08f;
                visualRoot.rotation=Quaternion.LookRotation(facing)*Quaternion.Euler(pitch,0,0);
                if(body)body.enabled=!Submerged&&!(State==Phase.Attack&&Progress<.30f);
            }
            if(species==Species.Jellyfish)y=.35f+Mathf.Sin(Time.time*2.4f)*.10f;
            visualRoot.localPosition=visualHome+Vector3.up*y;
        }
        void Cancel(){Stop();if(sound)sound.Stop();if(attackCollider)attackCollider.enabled=false;if(body)body.enabled=true;if(visualRoot)visualRoot.localPosition=visualHome;}
        void OnDisable(){Cancel();State=Phase.Idle;}
    }
}
