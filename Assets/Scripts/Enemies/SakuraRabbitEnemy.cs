using UnityEngine;
using UnityEngine.AI;

namespace DogLife
{
    [RequireComponent(typeof(CombatHealth),typeof(Rigidbody),typeof(NavMeshAgent))]
    public sealed class SakuraRabbitEnemy : MonoBehaviour
    {
        public Animator modelAnimator;
        public Transform visualRoot;
        public LineRenderer landingMarker;
        public ParticleSystem jumpPetals,landPetals;
        public float speed=3.5f,detectionRange=14,orbitRadius=3.8f,maxJumpDistance=6;
        public float crouchTime=.7f,jumpTime=.72f,jumpHeight=1.5f,landingRadius=.85f;
        public float damage=5,landingPause=.3f,retreatTime=.75f,retreatDistance=2,cooldown=1.5f;
        [SerializeField,Min(0),Tooltip("후퇴 종료 후 다음 점프까지의 최소 대기 시간(초)")] private float skillDelay=3;
        public LayerMask obstacleMask=~0;
        enum Phase{Orbit,Crouch,Air,Land,Retreat}
        Phase phase;
        NavMeshAgent agent;
        CombatHealth health,targetHealth;
        Transform target;
        Vector3 jumpStart,landingPoint,lastSafePosition,retreatHeading;
        float phaseStarted,nextAttack,nextPath;
        bool hitApplied,aborted;
        int orbitSign;
        string animationName;
        bool Navigable=>agent&&agent.isActiveAndEnabled&&agent.isOnNavMesh;
        float Offset=>agent?agent.baseOffset:.35f;
        void Awake()
        {
            health=GetComponent<CombatHealth>();agent=GetComponent<NavMeshAgent>();agent.updateRotation=false;
            var body=GetComponent<Rigidbody>();body.isKinematic=true;body.constraints=RigidbodyConstraints.FreezeRotation;
            orbitSign=Random.value<.5f?1:-1;lastSafePosition=transform.position;
        }
        void Start()
        {
            var player=FindFirstObjectByType<PlayerMotor>();
            if(player){target=player.transform;targetHealth=player.GetComponent<CombatHealth>();var own=GetComponent<Collider>();var other=player.GetComponent<Collider>();if(own&&other)Physics.IgnoreCollision(own,other);}
            nextAttack=Time.time+1;
        }
        void Animate(string name)
        {
            if(animationName==name)return;animationName=name;
            if(modelAnimator&&modelAnimator.isActiveAndEnabled)modelAnimator.CrossFadeInFixedTime(name,.05f,0);
        }
        void Stop(){if(Navigable){agent.isStopped=true;agent.ResetPath();}}
        void Face(Vector3 direction)
        {
            direction=Vector3.ProjectOnPlane(direction,Vector3.up);
            if(direction.sqrMagnitude>.001f)transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(direction),540*Time.fixedDeltaTime);
        }
        bool Blocked(Vector3 from,Vector3 to,float radius=.22f)
        {
            var delta=to-from;if(delta.sqrMagnitude<.000001f)return false;
            foreach(var hit in Physics.SphereCastAll(from,radius,delta.normalized,delta.magnitude,obstacleMask,QueryTriggerInteraction.Ignore))
                if(!hit.transform.IsChildOf(transform)&&!hit.collider.GetComponentInParent<CombatHealth>())return true;
            foreach(var hit in Physics.OverlapSphere(to,radius,obstacleMask,QueryTriggerInteraction.Ignore))
                if(!hit.transform.IsChildOf(transform)&&!hit.GetComponentInParent<CombatHealth>())return true;
            return false;
        }
        Vector3 Arc(float t)=>Vector3.Lerp(jumpStart,landingPoint,t)+Vector3.up*(4*jumpHeight*t*(1-t));
        bool FindLanding(Vector3 playerPosition,out Vector3 point)
        {
            point=Vector3.zero;if(!Navigable)return false;
            var filter=new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask};NavMeshHit start,end,edge;
            if(!NavMesh.SamplePosition(transform.position-Vector3.up*Offset,out start,.65f,filter)||!NavMesh.SamplePosition(playerPosition-Vector3.up,out end,.7f,filter))return false;
            if(Vector3.Distance(start.position,end.position)>maxJumpDistance||Mathf.Abs(start.position.y-end.position.y)>.6f)return false;
            if(NavMesh.Raycast(start.position,end.position,out edge,filter))return false;
            point=end.position+Vector3.up*Offset;
            Vector3 previous=transform.position;
            for(int i=1;i<=16;i++)
            {
                float t=i/16f;Vector3 next=Vector3.Lerp(transform.position,point,t)+Vector3.up*(4*jumpHeight*t*(1-t));
                if(Blocked(previous,next))return false;previous=next;
            }
            return true;
        }
        void DrawMarker(float radius)
        {
            if(!landingMarker)return;landingMarker.enabled=true;landingMarker.positionCount=65;
            Vector3 center=landingPoint-Vector3.up*Offset+Vector3.up*.035f;
            for(int i=0;i<=64;i++){float angle=i*Mathf.PI/32;landingMarker.SetPosition(i,center+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*radius);}
        }
        bool BeginCrouch()
        {
            Vector3 point;if(!target||!FindLanding(target.position,out point))return false;
            Stop();jumpStart=transform.position;lastSafePosition=jumpStart;landingPoint=point;
            retreatHeading=Vector3.ProjectOnPlane(jumpStart-landingPoint,Vector3.up).normalized;
            if(retreatHeading.sqrMagnitude<.001f)retreatHeading=-transform.forward;
            phase=Phase.Crouch;phaseStarted=Time.time;hitApplied=false;aborted=false;DrawMarker(landingRadius);Animate("Crouch");return true;
        }
        // 준비와 비행 중에는 피해를 주지 않으며, 착지 시점에 한 번만 검사합니다.
        void ApplyLandingHit()
        {
            if(hitApplied)return;hitApplied=true;
            if(aborted||!targetHealth||targetHealth.Dead||!target)return;
            Vector3 delta=target.position-landingPoint;
            if(Mathf.Abs(delta.y)>1.5f||Vector3.ProjectOnPlane(delta,Vector3.up).magnitude>landingRadius)return;
            if(!Blocked(landingPoint,target.position,.04f))targetHealth.Damage(damage);
        }
        void RestoreNavigation(Vector3 point)
        {
            transform.position=point;
            if(agent){agent.updatePosition=true;if(agent.isActiveAndEnabled)agent.Warp(point);}
        }
        void Land()
        {
            RestoreNavigation(landingPoint);lastSafePosition=transform.position;Stop();phase=Phase.Land;phaseStarted=Time.time;
            ApplyLandingHit();Animate("Land");if(jumpPetals)jumpPetals.Stop(true,ParticleSystemStopBehavior.StopEmitting);
            if(landPetals)landPetals.Play();DrawMarker(landingRadius);
        }
        bool SetDestination(Vector3 point)
        {
            if(!Navigable)return false;var filter=new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask};NavMeshHit hit;
            if(!NavMesh.SamplePosition(point,out hit,.9f,filter))return false;
            var path=new NavMeshPath();if(!agent.CalculatePath(hit.position,path)||path.status!=NavMeshPathStatus.PathComplete)return false;
            agent.isStopped=false;agent.speed=speed;agent.stoppingDistance=.12f;agent.SetPath(path);return true;
        }
        void BeginRetreat()
        {
            if(landingMarker)landingMarker.enabled=false;
            phase=Phase.Retreat;phaseStarted=Time.time;bool moved=false;
            foreach(float angle in new[]{0f,45f,-45f})
                if(SetDestination(transform.position-Vector3.up*Offset+Quaternion.Euler(0,angle,0)*retreatHeading*retreatDistance)){moved=true;break;}
            if(!moved)Stop();Animate(moved?"Retreat":"Idle");
        }
        void FixedUpdate()
        {
            if(!health||health.Dead||!target||!targetHealth||targetHealth.Dead){Cancel();return;}
            float elapsed=Time.time-phaseStarted;
            if(phase==Phase.Crouch)
            {
                Stop();Face(landingPoint-transform.position);if(elapsed>=crouchTime){phase=Phase.Air;phaseStarted=Time.time;agent.updatePosition=false;Animate("Jump");if(jumpPetals)jumpPetals.Play();}return;
            }
            if(phase==Phase.Air)
            {
                float t=Mathf.Clamp01(elapsed/Mathf.Max(.1f,jumpTime));Vector3 next=Arc(t);
                if(Blocked(transform.position,next))
                {
                    // 움직이는 장애물이 끼어들면 안전한 출발 위치로 복귀하고 공격을 취소합니다.
                    aborted=true;landingPoint=lastSafePosition;Land();return;
                }
                transform.position=next;if(t>=1)Land();return;
            }
            if(phase==Phase.Land)
            {Stop();DrawMarker(landingRadius+elapsed*.8f);if(elapsed>=landingPause)BeginRetreat();return;}
            if(phase==Phase.Retreat)
            {
                if(elapsed>=retreatTime){Stop();phase=Phase.Orbit;nextAttack=Time.time+Mathf.Max(cooldown,skillDelay);orbitSign=-orbitSign;Animate("Idle");}return;
            }
            Vector3 delta=Vector3.ProjectOnPlane(target.position-transform.position,Vector3.up);float distance=delta.magnitude;
            if(distance>detectionRange){Stop();Animate("Idle");return;}
            if(Time.time>=nextAttack&&distance>=1.4f&&distance<=maxJumpDistance&&BeginCrouch())return;
            if(Time.time>=nextPath)
            {
                nextPath=Time.time+.3f;Vector3 radial=Vector3.ProjectOnPlane(transform.position-target.position,Vector3.up).normalized;
                if(radial.sqrMagnitude<.001f)radial=transform.right;
                Vector3 destination=target.position-Vector3.up+Quaternion.Euler(0,orbitSign*40,0)*radial*orbitRadius;
                if(!SetDestination(destination))Stop();
            }
            bool moving=Navigable&&!agent.isStopped&&agent.velocity.sqrMagnitude>.02f;
            if(moving)Face(agent.desiredVelocity);else Face(delta);Animate(moving?"Move":"Idle");
        }
        void Cancel()
        {
            if(phase==Phase.Air){transform.position=lastSafePosition;if(agent){agent.updatePosition=true;if(agent.isActiveAndEnabled)agent.Warp(lastSafePosition);}}
            Stop();phase=Phase.Orbit;if(landingMarker)landingMarker.enabled=false;
            if(jumpPetals)jumpPetals.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            if(landPetals)landPetals.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);Animate("Idle");
        }
        void OnDisable(){Cancel();animationName=null;nextAttack=Time.time+1;nextPath=0;}
        void OnGUI()
        {
            if(SkillWindow.IsOpen||!health||health.Dead||!Camera.main)return;
            var p=Camera.main.WorldToScreenPoint(transform.position+Vector3.up*.8f);if(p.z<=0)return;
            var old=GUI.color;float y=Screen.height-p.y;GUI.color=Color.black;GUI.DrawTexture(new Rect(p.x-22,y,44,5),Texture2D.whiteTexture);
            GUI.color=phase==Phase.Crouch?new Color(1,.6f,.8f):new Color(1,.35f,.55f);GUI.DrawTexture(new Rect(p.x-22,y,44*Mathf.Clamp01(health.Current/Mathf.Max(1,health.maxHealth)),5),Texture2D.whiteTexture);GUI.color=old;
        }
    }
}
