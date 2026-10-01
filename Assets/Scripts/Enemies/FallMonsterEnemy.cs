using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace DogLife
{
    // 기존 NavMeshAgent, CombatHealth, CombatFeedback을 사용하며 가을 공격의 단계만 제어합니다.
    [RequireComponent(typeof(NavMeshAgent), typeof(CombatHealth), typeof(Rigidbody))]
    public sealed class FallMonsterEnemy : MonoBehaviour
    {
        public enum Pattern { Spores, ArmSweep, Spikes, CrowDash }
        public enum Phase { Idle, Anticipation, Attack, Active, Recovery }
        [SerializeField] Pattern pattern;
        [SerializeField] float speed=2, detectionRange=16, attackRange=3.5f, damage=5;
        [SerializeField, Min(.1f)] float anticipation=.65f;
        [SerializeField, Min(.02f)] float attackMotion=.2f;
        [SerializeField, Min(.02f)] float activeDuration=.5f;
        [SerializeField, Min(.05f)] float recovery=.8f;
        [SerializeField, Min(0)] float cooldown=2;
        [SerializeField, Min(.1f)] float radius=3.5f;
        [SerializeField, Min(0)] float spikeSafeRadius=.7f;
        [SerializeField, Min(.05f)] float damageTickInterval=1;
        [SerializeField, Min(.1f)] float armLength=4, armThickness=.38f;
        [SerializeField, Range(30,200)] float sweepAngle=130;
        [SerializeField, Min(.1f)] float dashSpeed=12, dashDistance=10;
        [SerializeField, Min(0)] float flightHeight=.7f;
        [SerializeField, Min(0)] float dashHeight=.3f;
        [Header("피격 밀치기")]
        [SerializeField, Min(0)] float hitPushSpeed;
        [SerializeField, Min(0)] float hitLiftSpeed;
        [SerializeField, Min(.02f)] float hitPushDuration=.25f;
        [SerializeField] LayerMask obstacleMask=1<<8;
        [SerializeField] Transform visualRoot;
        [SerializeField] Animator modelAnimator;
        [SerializeField] FallAttackPresentation presentation;
        [SerializeField] Collider attackCollider;
        public Phase CurrentPhase { get; private set; }
        public Pattern AttackPattern=>pattern;
        public Vector3 LockedDirection { get; private set; }
        public Vector3 AttackOrigin { get; private set; }
        public float Radius=>radius;
        public float SpikeSafeRadius=>spikeSafeRadius;
        public float ArmLength=>armLength;
        public float ArmThickness=>armThickness;
        public float SweepAngle=>sweepAngle;
        public float DashDistance=>Mathf.Min(dashDistance,dashSpeed*activeDuration);
        // 경고선과 바닥 채움이 실제 팔/돌진 캡슐의 같은 외곽을 공유합니다.
        public Vector3 WarningCenter
        {
            get
            {
                if(pattern==Pattern.CrowDash)return AttackOrigin;
                if(pattern==Pattern.ArmSweep&&presentation&&presentation.ArmPoints(out var a,out var b,out var c))
                    return new Vector3(a.x,transform.position.y,a.z);
                return transform.position;
            }
        }
        public void GetWarningOutline(List<Vector3> points,float height)
        {
            points.Clear();var origin=WarningCenter+Vector3.up*height;
            var forward=LockedDirection.sqrMagnitude>.001f?LockedDirection:transform.forward;
            if(pattern==Pattern.CrowDash)
            {
                var side=Vector3.Cross(Vector3.up,forward);var end=origin+forward*DashDistance;
                for(int i=0;i<=24;i++){float a=Mathf.Lerp(-Mathf.PI*.5f,Mathf.PI*.5f,i/24f);points.Add(end+(forward*Mathf.Cos(a)+side*Mathf.Sin(a))*.65f);}
                for(int i=0;i<=24;i++){float a=Mathf.Lerp(Mathf.PI*.5f,Mathf.PI*1.5f,i/24f);points.Add(origin+(forward*Mathf.Cos(a)+side*Mathf.Sin(a))*.65f);}
            }
            else if(pattern==Pattern.ArmSweep)
            {
                float half=sweepAngle*.5f;
                for(int i=0;i<=64;i++)points.Add(origin+Quaternion.AngleAxis(Mathf.Lerp(-half,half,i/64f),Vector3.up)*forward*(armLength+armThickness));
                var end=origin+Quaternion.AngleAxis(half,Vector3.up)*forward*armLength;
                for(int i=1;i<=8;i++)points.Add(end+Quaternion.AngleAxis(half+90*i/8f,Vector3.up)*forward*armThickness);
                for(int i=0;i<=16;i++)points.Add(origin+Quaternion.AngleAxis(Mathf.Lerp(half+90,270-half,i/16f),Vector3.up)*forward*armThickness);
                var start=origin+Quaternion.AngleAxis(-half,Vector3.up)*forward*armLength;
                for(int i=0;i<=8;i++)points.Add(start+Quaternion.AngleAxis(270-half+90*i/8f,Vector3.up)*forward*armThickness);
            }
            if(points.Count>0)points.Add(points[0]);
        }
        public float Progress=>Mathf.Clamp01(elapsed/Mathf.Max(.01f,Duration(CurrentPhase)));
        public bool Windup=>CurrentPhase==Phase.Anticipation;
        NavMeshAgent agent;
        CombatHealth health,targetHealth;
        Transform target;
        Collider targetCollider;
        float elapsed,nextAttack,nextPath,nextTick,dashTravel;
        readonly HashSet<CombatHealth> hitTargets=new HashSet<CombatHealth>();
        readonly Collider[] overlaps=new Collider[64];
        Vector3 previousShoulder,previousElbow,previousHand;
        Vector3 armHitDirection;
        bool previousArmValid, radialPulseUsed;
        Vector3 visualHome;
        CapsuleCollider bodyCollider;
        Vector3 bodyCenter,attackHome;
        float recoveryAltitude,previousFlightAltitude;
        float descentEnd=.3f,ascentStart=.65f;

        void Awake()
        {
            agent=GetComponent<NavMeshAgent>();health=GetComponent<CombatHealth>();
            var body=GetComponent<Rigidbody>();body.isKinematic=true;body.constraints=RigidbodyConstraints.FreezeRotation;
            agent.updateRotation=false;agent.updateUpAxis=false;
            if(visualRoot)visualHome=visualRoot.localPosition;
            bodyCollider=GetComponent<CapsuleCollider>();if(bodyCollider)bodyCenter=bodyCollider.center;
            if(attackCollider)attackHome=attackCollider.transform.localPosition;
            if(modelAnimator)modelAnimator.applyRootMotion=false;
            if(attackCollider)
            {
                attackCollider.isTrigger=true;attackCollider.enabled=false;
                // Inspector의 범위값과 전용 판정 콜라이더 크기를 일치시킵니다.
                if(attackCollider is SphereCollider sphere)
                {
                    float worldRadius=pattern==Pattern.ArmSweep?armThickness:pattern==Pattern.CrowDash?.65f:radius;
                    var scale=sphere.transform.lossyScale;
                    sphere.radius=worldRadius/Mathf.Max(.001f,Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z));
                }
            }
        }
        void OnEnable(){CurrentPhase=Phase.Idle;elapsed=0;nextAttack=Time.time+1;previousArmValid=false;}
        ForestRoom combatRoom;
        void Start()
        {
            combatRoom=GetComponentInParent<ForestRoom>();
            var player=FindFirstObjectByType<PlayerMotor>();
            if(player){target=player.transform;targetHealth=player.GetComponent<CombatHealth>();targetCollider=player.GetComponent<Collider>();
                var own=GetComponent<Collider>();if(own&&targetCollider)Physics.IgnoreCollision(own,targetCollider);}
            Play("Idle",1);
        }
        float Duration(Phase p)=>p==Phase.Anticipation?anticipation:p==Phase.Attack?attackMotion:p==Phase.Active?activeDuration:recovery;
        bool Navigable=>agent&&agent.isActiveAndEnabled&&agent.isOnNavMesh;
        void Stop(){if(Navigable){agent.isStopped=true;agent.ResetPath();agent.velocity=Vector3.zero;}}
        bool Clear(Vector3 a,Vector3 b)=>!Physics.Linecast(a,b,obstacleMask,QueryTriggerInteraction.Ignore);
        void Face(Vector3 dir){if(visualRoot&&dir.sqrMagnitude>.001f)visualRoot.rotation=Quaternion.LookRotation(dir);}
        void Play(string state,float duration)
        {
            if(!modelAnimator||!modelAnimator.isActiveAndEnabled)return;
            // 상태 속도 파라미터로 Inspector의 전투 시간에 동작을 맞춥니다.
            modelAnimator.SetFloat("PhaseSpeed",1/Mathf.Max(.05f,duration));
            modelAnimator.CrossFadeInFixedTime(state,.10f);
        }
        void Enter(Phase phase)
        {
            CurrentPhase=phase;elapsed=0;
            if(attackCollider)attackCollider.enabled=phase==Phase.Active&&pattern!=Pattern.Spikes;
            if(phase==Phase.Anticipation)
            {
                Stop();AttackOrigin=transform.position;hitTargets.Clear();previousArmValid=false;
                // 경고가 처음 나타날 때 조준과 비행 구간을 모두 고정합니다.
                if(pattern==Pattern.CrowDash)
                {
                    var delta=Vector3.ProjectOnPlane(target.position-transform.position,Vector3.up);
                    if(delta.sqrMagnitude>.001f)LockedDirection=delta.normalized;
                    // 가까운 상대에게도 지나친 뒤 하강하지 않도록 낮은 구간을 조준 거리에 맞춥니다.
                    float targetFraction=delta.magnitude/Mathf.Max(.1f,dashSpeed*activeDuration);
                    descentEnd=Mathf.Clamp(targetFraction-.12f,.06f,.35f);
                    ascentStart=Mathf.Clamp(targetFraction+.15f,descentEnd+.15f,.8f);
                    Face(LockedDirection);
                }
                Play("AttackReady",anticipation);
            }
            if(phase==Phase.Attack)Play("Attack",attackMotion+activeDuration);
            if(phase==Phase.Active){nextTick=0;dashTravel=0;radialPulseUsed=false;}
            if(phase==Phase.Recovery){recoveryAltitude=previousFlightAltitude;Stop();Play("Recovery",recovery);}
            if(phase==Phase.Idle){nextAttack=Time.time+cooldown;Play("Idle",1);}
        }
        void FixedUpdate()
        {
            // 로딩된 다음 방에서는 대기하고, 문 닫힘 시작 후에만 AI를 실행합니다.
            if(combatRoom&&!combatRoom.CombatStarted)return;
            if(!health||health.Dead||!targetHealth||targetHealth.Dead){Cancel();return;}
            if(CombatFeedback.Paused(this))return;
            float dt=Time.fixedDeltaTime;
            if(CurrentPhase==Phase.Idle)
            {
                Vector3 delta=Vector3.ProjectOnPlane(target.position-transform.position,Vector3.up);
                bool sight=Clear(transform.position+Vector3.up,target.position);
                if(delta.magnitude<=attackRange&&sight&&Time.time>=nextAttack)
                {LockedDirection=delta.sqrMagnitude>.001f?delta.normalized:transform.forward;Face(LockedDirection);Enter(Phase.Anticipation);return;}
                bool walk=(combatRoom||delta.magnitude<detectionRange)&&(delta.magnitude>Mathf.Max(1,attackRange*.8f)||!sight);
                if(Navigable){agent.isStopped=!walk;agent.speed=speed;agent.stoppingDistance=.6f;
                    if(walk&&Time.time>=nextPath){nextPath=Time.time+.25f;NavMeshHit nh;
                        if(NavMesh.SamplePosition(target.position,out nh,3,new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask}))agent.SetDestination(nh.position);}
                    if(walk)Face(agent.desiredVelocity.sqrMagnitude>.01f?agent.desiredVelocity:delta);}
                if(modelAnimator)modelAnimator.SetBool("Moving",walk);
                return;
            }
            elapsed+=dt;
            if(CurrentPhase==Phase.Active)
            {
                if(pattern==Pattern.Spores&&elapsed>=nextTick){nextTick=elapsed+Mathf.Max(.05f,damageTickInterval);hitTargets.Clear();RadialDamage();}
                if(pattern==Pattern.Spikes&&!radialPulseUsed&&Progress>=.2f)
                {
                    // 가시가 드러나는 순간에만 판정을 열고 즉시 닫습니다. 유지 중에는 피해가 없습니다.
                    radialPulseUsed=true;if(attackCollider)attackCollider.enabled=true;
                    RadialDamage();if(attackCollider)attackCollider.enabled=false;
                }
                if(pattern==Pattern.CrowDash)Dash(dt);
            }
            if(CurrentPhase==Phase.Recovery&&pattern==Pattern.CrowDash&&elapsed<.35f)MoveDash(dashSpeed*(1-elapsed/.35f)*dt);
            if(elapsed>=Duration(CurrentPhase))Enter(CurrentPhase==Phase.Anticipation?Phase.Attack:CurrentPhase==Phase.Attack?Phase.Active:CurrentPhase==Phase.Active?Phase.Recovery:Phase.Idle);
        }
        void LateUpdate()
        {
            if(!health||health.Dead)return;
            if(visualRoot&&pattern==Pattern.CrowDash)
            {
                // 높은 곳 → 하강 → 낮은 통과 → 상승의 U자 궤적입니다. 수평 방향은 고정합니다.
                float altitude=FlightAltitude();
                visualRoot.localPosition=visualHome+Vector3.up*altitude;
                if(bodyCollider)bodyCollider.center=bodyCenter+Vector3.up*altitude;
                if(attackCollider)attackCollider.transform.localPosition=attackHome+Vector3.up*altitude;
                if(CurrentPhase==Phase.Active)
                {
                    float slope=Progress<descentEnd?-.65f*Mathf.Sin(Mathf.PI*Progress/descentEnd):Progress>ascentStart?.65f*Mathf.Sin(Mathf.PI*(Progress-ascentStart)/(1-ascentStart)):0;
                    visualRoot.rotation=Quaternion.LookRotation(LockedDirection+Vector3.up*slope);
                }
                else if(CurrentPhase==Phase.Recovery)visualRoot.rotation=Quaternion.Slerp(visualRoot.rotation,Quaternion.LookRotation(LockedDirection),Time.deltaTime*8);
                previousFlightAltitude=altitude;
            }
            if(presentation)presentation.Apply(this);
            // 팔은 애니메이터와 연장 보정이 끝난 실제 어깨·팔꿈치·손 위치를 검사합니다.
            if(CurrentPhase==Phase.Active&&pattern==Pattern.ArmSweep&&presentation&&presentation.ArmPoints(out var a,out var b,out var c))
            {
                // 손이 실제로 휘둘러진 접선 방향으로 날립니다. 첫 프레임은 회전 방향으로 보완합니다.
                armHitDirection=previousArmValid?Vector3.ProjectOnPlane(c-previousHand,Vector3.up):Vector3.zero;
                if(armHitDirection.sqrMagnitude<.00001f)armHitDirection=Vector3.Cross(Vector3.up,c-a);
                armHitDirection.Normalize();
                if(previousArmValid){float travel=Mathf.Max(Vector3.Distance(previousHand,c),Vector3.Distance(previousElbow,b));
                    int steps=Mathf.Max(1,Mathf.CeilToInt(travel/Mathf.Max(.05f,armThickness*.5f)));
                    for(int i=0;i<=steps;i++){float t=(float)i/steps;ArmDamage(Vector3.Lerp(previousShoulder,a,t),Vector3.Lerp(previousElbow,b,t),Vector3.Lerp(previousHand,c,t));}}
                else ArmDamage(a,b,c);
                previousShoulder=a;previousElbow=b;previousHand=c;previousArmValid=true;
            }
            else previousArmValid=false;
        }
        void RadialDamage()
        {
            var center=transform.position;
            if(!targetCollider)return;
            // 고슴도치의 중앙 위치 표시 안에 완전히 들어간 대상은 가시 피해에서 제외합니다.
            if(pattern==Pattern.Spikes)
            {
                var bounds=targetCollider.bounds;
                float outerEdge=Vector3.ProjectOnPlane(bounds.center-center,Vector3.up).magnitude+Mathf.Max(bounds.extents.x,bounds.extents.z);
                if(outerEdge<=spikeSafeRadius)return;
            }
            var contact=targetCollider.ClosestPoint(center+Vector3.up*.7f);
            if(Vector3.ProjectOnPlane(contact-center,Vector3.up).magnitude<=radius&&Mathf.Abs(contact.y-center.y)<2.2f&&Clear(center+Vector3.up*.6f,contact))Hit(targetHealth,contact);
        }
        void ArmDamage(Vector3 a,Vector3 b,Vector3 c){CapsuleDamage(a,b,armThickness);CapsuleDamage(b,c,armThickness);}
        void CapsuleDamage(Vector3 a,Vector3 b,float r)
        {
            int count=Physics.OverlapCapsuleNonAlloc(a,b,r,overlaps,~0,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++){var h=overlaps[i].GetComponentInParent<CombatHealth>();if(h&&h.player&&Clear(a,overlaps[i].ClosestPoint(a)))Hit(h,overlaps[i].ClosestPoint(b));}
        }
        void Hit(CombatHealth h,Vector3 contact)
        {
            if(!h||!h.player||hitTargets.Contains(h))return;
            hitTargets.Add(h);
            Vector3 direction=pattern==Pattern.ArmSweep?armHitDirection:LockedDirection;
            if(!CombatFeedback.DealDamage(h,damage,contact,direction,1,transform,new Color(1,.6f,.1f))||h.Dead)return;
            // 회피·무적에 막힌 공격과 포자에는 밀치기를 적용하지 않습니다.
            if(pattern==Pattern.Spores)return;
            var motor=h.GetComponent<PlayerMotor>();
            if(motor)motor.ApplyKnockback(direction,hitPushSpeed,hitLiftSpeed,hitPushDuration);
        }
        void Dash(float dt){float distance=Mathf.Min(dashSpeed*dt,dashDistance-dashTravel);if(distance<=0){Enter(Phase.Recovery);return;}MoveDash(distance);}
        float FlightAltitude()
        {
            if(CurrentPhase==Phase.Active)
            {
                float t=Progress;
                if(t<descentEnd)return Mathf.Lerp(flightHeight,dashHeight,Mathf.SmoothStep(0,1,t/descentEnd));
                if(t>ascentStart)return Mathf.Lerp(dashHeight,flightHeight,Mathf.SmoothStep(0,1,(t-ascentStart)/(1-ascentStart)));
                return dashHeight;
            }
            if(CurrentPhase==Phase.Recovery)return Mathf.Lerp(recoveryAltitude,flightHeight,Mathf.SmoothStep(0,1,Progress));
            return flightHeight+.08f*Mathf.Sin(Time.time*4);
        }
        void MoveDash(float distance)
        {
            if(!Navigable)return;
            var from=transform.position;var step=LockedDirection*distance;
            if(Physics.SphereCast(from+Vector3.up*.7f,.45f,LockedDirection,out var hit,distance,obstacleMask,QueryTriggerInteraction.Ignore))step=LockedDirection*Mathf.Max(0,hit.distance-.08f);
            if(agent.Raycast(from+step,out var navHit))step=Vector3.ClampMagnitude(navHit.position-from,Mathf.Max(0,Vector3.Distance(from,navHit.position)-.1f));
            agent.Move(step);dashTravel+=step.magnitude;
            // 지면의 경로 탐색 위치가 아니라 실제 새가 통과하는 높이에서만 피해를 검사합니다.
            if(CurrentPhase==Phase.Active)CapsuleDamage(from+Vector3.up*(previousFlightAltitude+.7f),transform.position+Vector3.up*(FlightAltitude()+.7f),.65f);
            if(step.sqrMagnitude<.00001f&&CurrentPhase==Phase.Active)Enter(Phase.Recovery);
        }
        void Cancel(){Stop();CurrentPhase=Phase.Idle;if(attackCollider)attackCollider.enabled=false;if(presentation)presentation.ResetVisuals();}
        void OnDisable(){Cancel();hitTargets.Clear();previousArmValid=false;if(visualRoot)visualRoot.localPosition=visualHome;if(bodyCollider)bodyCollider.center=bodyCenter;if(attackCollider&&pattern==Pattern.CrowDash)attackCollider.transform.localPosition=attackHome;}
        void OnValidate(){radius=Mathf.Max(.1f,radius);armLength=Mathf.Max(.5f,armLength);activeDuration=Mathf.Max(.02f,activeDuration);}
    }
}
