using UnityEngine;
using UnityEngine.AI;
namespace DogLife
{
    [RequireComponent(typeof(CombatHealth),typeof(NavMeshAgent),typeof(Rigidbody))]
    public sealed class WinterMonsterEnemy : MonoBehaviour
    {
        public enum Species { Golem, Spirit, Wolf, Mammoth }
        public enum Phase { Idle, Warning, Attack, Reaim, SecondAttack, Recovery }
        [SerializeField] private Species species;
        [SerializeField,Min(0)] private float moveSpeed=1.5f;
        [Header("쿨타임 중 이동")]
        [SerializeField,Min(.1f),Tooltip("Scale 1 기준. 이 거리까지 접근한 뒤 주변을 돌아 다음 공격 위치를 잡습니다.")] private float repositionDistance=1.4f;
        [SerializeField,Range(.1f,1f)] private float repositionSpeedMultiplier=.75f;
        [SerializeField,Min(.1f)] private float detectionRange=18,attackDistance=6;
        [SerializeField,Min(.1f)] private float warningTime=1.05f,attackTime=1.25f,recoveryTime=1.2f,cooldown=3;
        [SerializeField,Min(.1f)] private float attackRange=6;
        [SerializeField,Range(10,170)] private float fanAngle=55;
        [SerializeField,Min(.1f)] private float damage=1;
        [Header("골렘 충격파")]
        [SerializeField,Min(.1f)] private float shockwaveSpeed=18;
        [SerializeField,Min(0)] private float golemPushSpeed=1.6f;
        [SerializeField,Min(.02f)] private float golemPushDuration=.14f;
        [Header("늑대 점프 → 앞발 할퀴기")]
        [SerializeField,Min(.1f)] private float reaimTime=.48f,landingRadius=1.3f,leapHeight=1.25f;
        [SerializeField,Min(.1f)] private float clawRange=2.3f,clawTime=.65f;
        [SerializeField,Range(10,170)] private float clawAngle=110;
        [SerializeField,Min(0)] private float wolfFirstLift=2.4f,wolfClawPushSpeed=2.5f;
        [SerializeField,Min(.02f)] private float wolfFirstDuration=.2f,wolfClawPushDuration=.2f;
        [Header("눈 정령 얼음 장판")]
        [SerializeField,Min(.1f)] private float iceRadius=5.4f,iceLifetime=6,iceSteeringAcceleration=3.5f;
        [Header("매머드 상아")]
        [SerializeField,Min(0)] private float stepDistance=.85f,pushSpeed=11,pushLift=7;
        [SerializeField,Min(.02f)] private float pushDuration=.75f;
        [Header("연결")]
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Animator animator;
        [SerializeField] private Material effectMaterial;
        [SerializeField] private LayerMask obstacleMask=1<<8;
        NavMeshAgent agent;CombatHealth health,targetHealth;PlayerMotor player;Collider targetCollider;
        float elapsed,nextAttack,nextPath;float orbitSign=1;bool hit,spawned;Vector3 home,start,end;
        public Species Kind=>species;
        public Phase State {get;private set;}
        public float Progress=>Mathf.Clamp01(elapsed/Duration(State));
        // 늑대 착지/후속타와 장판 생성도 각자의 실제 발동 시점에 맞춥니다.
        public float TelegraphProgress
        {
            get
            {
                float lead=ClawPhase?reaimTime:warningTime;
                float contact=species==Species.Wolf?(ClawPhase?.36f:.8f):species==Species.Golem?.28f:species==Species.Spirit?.32f:.36f;
                float duration=ClawPhase?clawTime:attackTime;
                return Mathf.Clamp01(((State==Phase.Warning||State==Phase.Reaim)?elapsed:lead+elapsed)/Mathf.Max(.01f,lead+duration*contact));
            }
        }
        public Vector3 Forward {get;private set;}=Vector3.forward;
        public Vector3 Origin {get;private set;}
        public Vector3 TargetPoint=>end;
        public bool ClawPhase=>species==Species.Wolf&&(State==Phase.Reaim||State==Phase.SecondAttack);
        public float SizeScale=>Mathf.Max(.01f, Mathf.Abs(transform.lossyScale.x));
        public float Range=>(ClawPhase?clawRange:attackRange)*SizeScale;
        public float Angle=>ClawPhase?clawAngle:fanAngle;
        public float LandingRadius=>landingRadius*SizeScale;
        public float IceRadius=>iceRadius;
        // 끝에 도착한 뒤에도 파면 거리는 진행시켜 남은 얼음에 피해 판정이 붙지 않게 합니다.
        float WaveDistance=>Mathf.Max(0,elapsed-attackTime*.28f)*shockwaveSpeed*SizeScale;
        public float WaveProgress=>Mathf.Clamp01(WaveDistance/Mathf.Max(.1f,Range));
        public bool Attacking=>State==Phase.Attack||State==Phase.SecondAttack;
        bool Nav=>agent&&agent.enabled&&agent.isOnNavMesh;
        void Awake()
        {
            agent=GetComponent<NavMeshAgent>();health=GetComponent<CombatHealth>();
            var rb=GetComponent<Rigidbody>();rb.isKinematic=true;rb.constraints=RigidbodyConstraints.FreezeRotation;
            agent.updateRotation=false;agent.updateUpAxis=false;
            agent.radius*=SizeScale;agent.height*=SizeScale;
            orbitSign=Random.value<.5f?1:-1;
            if(visualRoot)home=visualRoot.localPosition;
            if(animator)animator.applyRootMotion=false;
        }
        void OnEnable(){State=Phase.Idle;nextAttack=Time.time+1;Forward=transform.forward;}
        ForestRoom combatRoom;
        void Start()
        {
            combatRoom=GetComponentInParent<ForestRoom>();
            player=FindFirstObjectByType<PlayerMotor>();if(player){targetHealth=player.GetComponent<CombatHealth>();targetCollider=player.GetComponent<Collider>();var body=GetComponent<Collider>();if(body&&targetCollider)Physics.IgnoreCollision(body,targetCollider);}
            Play("Idle",1);
        }
        float Duration(Phase p)=>Mathf.Max(.05f,p==Phase.Warning?warningTime:p==Phase.Reaim?reaimTime:p==Phase.Recovery?recoveryTime:p==Phase.SecondAttack?clawTime:attackTime);
        void Play(string state,float duration){if(!animator)return;animator.SetBool("Moving",false);animator.SetFloat("PhaseSpeed",1/Mathf.Max(.05f,duration));animator.CrossFadeInFixedTime(state,.1f);}
        void Stop(){if(Nav){agent.isStopped=true;agent.ResetPath();agent.velocity=Vector3.zero;}}
        void Face(Vector3 d,bool immediate=true){d.y=0;if(d.sqrMagnitude>.001f)Forward=d.normalized;if(visualRoot)visualRoot.rotation=immediate?Quaternion.LookRotation(Forward):Quaternion.RotateTowards(visualRoot.rotation,Quaternion.LookRotation(Forward),180*Time.fixedDeltaTime);}
        bool Visible(Vector3 a,Vector3 b)=>!Physics.Linecast(a+Vector3.up*.65f,b+Vector3.up*.65f,obstacleMask,QueryTriggerInteraction.Ignore);
        Vector3 SafeDestination(Vector3 desired)
        {
            Vector3 delta=Vector3.ProjectOnPlane(desired-transform.position,Vector3.up);delta=Vector3.ClampMagnitude(delta,attackRange*SizeScale);
            Vector3 target=transform.position+delta;RaycastHit wall;
            if(delta.sqrMagnitude>.01f&&Physics.SphereCast(transform.position+Vector3.up*.6f*SizeScale,.4f*SizeScale,delta.normalized,out wall,delta.magnitude,obstacleMask,QueryTriggerInteraction.Ignore))target=transform.position+delta.normalized*Mathf.Max(0,wall.distance-.15f);
            if(Nav){NavMeshHit navHit;if(!NavMesh.SamplePosition(target,out navHit,1.5f,new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask}))return transform.position;target=navHit.position;if(agent.Raycast(target,out navHit))target=Vector3.MoveTowards(navHit.position,transform.position,.3f);}
            return target;
        }
        void Aim()
        {
            Face(player.transform.position-transform.position);Origin=transform.position;start=transform.position;
            end=SafeDestination(species==Species.Mammoth?start+Forward*stepDistance*SizeScale:player.transform.position);
        }
        void Enter(Phase phase)
        {
            State=phase;elapsed=0;hit=false;spawned=false;Stop();
            if(phase==Phase.Warning||phase==Phase.Reaim)Aim();
            if(phase==Phase.Warning)Play("AttackReady",warningTime);
            else if(phase==Phase.Reaim)Play("Reaim",reaimTime);
            else if(phase==Phase.Attack||phase==Phase.SecondAttack){start=transform.position;Play(phase==Phase.SecondAttack?"Claw":"Attack",Duration(phase));}
            else if(phase==Phase.Recovery)Play("Recovery",recoveryTime);
            else{nextAttack=Time.time+cooldown;Play("Idle",1);}
        }
        void FixedUpdate()
        {
            // 로딩된 다음 방에서는 대기하고, 문 닫힘 시작 후에만 AI를 실행합니다.
            if(combatRoom&&!combatRoom.CombatStarted)return;
            if(!health||health.Dead||!player||!targetHealth||targetHealth.Dead){Stop();return;}
            if(CombatFeedback.Paused(this))return;
            if(State==Phase.Idle)
            {
                float distance=Vector3.Distance(transform.position,player.transform.position);
                if(distance<attackDistance*SizeScale&&Time.time>=nextAttack&&Visible(transform.position,player.transform.position)){Enter(Phase.Warning);return;}
                bool moving=(combatRoom||distance<detectionRange*SizeScale)&&Nav;
                if(moving)
                {
                    // Attack reach is not a movement stopping distance: keep repositioning during cooldown.
                    float targetRadius=targetCollider?Mathf.Max(targetCollider.bounds.extents.x,targetCollider.bounds.extents.z):.4f;
                    float spacing=Mathf.Max(repositionDistance*SizeScale,agent.radius+targetRadius+.45f);
                    bool nearby=distance<spacing+1f;
                    agent.isStopped=false;agent.speed=moveSpeed*(nearby?repositionSpeedMultiplier:1);agent.stoppingDistance=.12f;
                    if(Time.time>=nextPath)
                    {
                        nextPath=Time.time+.35f;
                        Vector3 outward=Vector3.ProjectOnPlane(transform.position-player.transform.position,Vector3.up).normalized;
                        if(outward.sqrMagnitude<.01f)outward=-Forward;
                        Vector3 direction=nearby?Quaternion.AngleAxis(35*orbitSign,Vector3.up)*outward:outward;
                        Vector3 desired=player.transform.position+direction*spacing;
                        NavMeshHit navHit;
                        if(NavMesh.SamplePosition(desired,out navHit,1f,new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask}))
                            agent.SetDestination(navHit.position);
                        else {orbitSign=-orbitSign;Stop();}
                    }
                    if(agent.desiredVelocity.sqrMagnitude>.01f)Face(agent.desiredVelocity,false);
                }
                else Stop();
                if(animator){float speed=Nav?agent.velocity.magnitude:0;animator.SetBool("Moving",moving&&speed>.08f);if(species!=Species.Spirit){float strideSpeed=species==Species.Golem?.75f:species==Species.Mammoth?.68f:.88f;animator.SetFloat("WalkSpeed",Mathf.Clamp(speed/(strideSpeed*SizeScale),.15f,2.5f),.12f,Time.fixedDeltaTime);}}return;
            }
            elapsed+=Time.fixedDeltaTime;
            if(State==Phase.Warning&&elapsed>=warningTime){Enter(Phase.Attack);return;}
            if(State==Phase.Reaim&&elapsed>=reaimTime){Enter(Phase.SecondAttack);return;}
            if(State==Phase.Recovery&&elapsed>=recoveryTime){Enter(Phase.Idle);return;}
            if(!Attacking)return;
            float t=Progress;
            if(species==Species.Wolf&&State==Phase.Attack||species==Species.Mammoth)
            {
                float travel=species==Species.Wolf?Mathf.SmoothStep(0,1,Mathf.InverseLerp(.08f,.82f,t)):Mathf.SmoothStep(0,1,Mathf.InverseLerp(.12f,.44f,t));
                Vector3 p=Vector3.Lerp(start,end,travel);if(Nav)agent.Warp(p);else transform.position=p;
            }
            if(species==Species.Spirit&&t>=.32f&&!spawned){spawned=true;WinterIcePatch.Create(end,iceRadius,iceLifetime,iceSteeringAcceleration,health,effectMaterial);}
            if(!hit&&species!=Species.Spirit)
            {
                bool active=species==Species.Wolf?(ClawPhase?t>=.36f&&t<=.70f:t>=.8f&&t<=.96f):species==Species.Golem?t>=.28f&&t<=.94f:t>=.36f&&t<=.70f;
                if(active)TryHit();
            }
            if(elapsed>=Duration(State))Enter(species==Species.Wolf&&State==Phase.Attack?Phase.Reaim:Phase.Recovery);
        }
        // Same radius/fan geometry is used by the warning renderer and the damage test.
        public bool Contains(Vector3 point,float bodyRadius=0)
        {
            Vector3 center=species==Species.Wolf&&!ClawPhase?end:species==Species.Mammoth||ClawPhase?transform.position:Origin;
            if(Mathf.Abs(point.y-center.y)>2.3f*SizeScale)return false;
            Vector3 d=Vector3.ProjectOnPlane(point-center,Vector3.up);
            if(species==Species.Wolf&&!ClawPhase)return d.magnitude<=LandingRadius+bodyRadius;
            float reach=Range*(species==Species.Golem?WaveProgress:1);
            if(d.magnitude>reach+bodyRadius)return false;
            if(species==Species.Golem)
            {
                // 결정이 솟는 폭과 이번 물리 프레임에 지나간 구간만 검사합니다.
                float previousCrest=WaveDistance-shockwaveSpeed*SizeScale*Time.fixedDeltaTime;
                float trailingEdge=Mathf.Max(0,previousCrest-Range*.10f);
                if(d.magnitude+bodyRadius<trailingEdge)return false;
            }
            float allowance=Mathf.Asin(Mathf.Clamp01(bodyRadius/Mathf.Max(.01f,d.magnitude)))*Mathf.Rad2Deg;
            return Vector3.Angle(Forward,d)<=Angle*.5f+allowance;
        }
        void TryHit()
        {
            if(!targetCollider)return;
            Vector3 p=targetCollider.bounds.center;float radius=Mathf.Min(targetCollider.bounds.extents.x,targetCollider.bounds.extents.z);
            if(!Contains(p,radius)||!Visible(transform.position,player.transform.position))return;
            hit=true;Vector3 d=player.transform.position-transform.position;if(d.sqrMagnitude<.001f)d=Forward;
            if(!CombatFeedback.DealDamage(targetHealth,damage,targetCollider.ClosestPoint(transform.position+Vector3.up),d,species==Species.Golem?1.6f:1,transform,new Color(.65f,.85f,1))||targetHealth.Dead)return;
            if(species==Species.Mammoth)player.ApplyKnockback(d,pushSpeed,pushLift,pushDuration);
            else if(species==Species.Golem)player.ApplyKnockback(d,golemPushSpeed,0,golemPushDuration);
            else if(species==Species.Wolf)player.ApplyKnockback(d,ClawPhase?wolfClawPushSpeed:0,ClawPhase?0:wolfFirstLift,ClawPhase?wolfClawPushDuration:wolfFirstDuration);
        }
        void LateUpdate()
        {
            if(!visualRoot)return;float y=0;
            if(species==Species.Wolf&&State==Phase.Attack)y=Mathf.Sin(Mathf.InverseLerp(.08f,.82f,Progress)*Mathf.PI)*leapHeight;
            if(species==Species.Spirit&&State==Phase.Idle)y=.08f+Mathf.Sin(Time.time*2)*.05f;
            visualRoot.localPosition=home+Vector3.up*y;
        }
        void OnDisable(){Stop();State=Phase.Idle;if(visualRoot)visualRoot.localPosition=home;}
    }
}
