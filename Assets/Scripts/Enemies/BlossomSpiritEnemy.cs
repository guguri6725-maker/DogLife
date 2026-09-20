using UnityEngine;
using UnityEngine.AI;

namespace DogLife
{
    // 기존 StageEnemy를 변경하지 않는 벚꽃 정령 전용 원거리 전투입니다.
    [RequireComponent(typeof(CombatHealth),typeof(Rigidbody),typeof(NavMeshAgent))]
    public sealed class BlossomSpiritEnemy : MonoBehaviour
    {
        public Transform visualRoot,leftHand,rightHand;
        public Animator modelAnimator;
        public GameObject projectilePrefab;
        public ParticleSystem leftCharge,rightCharge;
        public ParticleSystem dashPetals;
        public float dashTriggerDistance=3.2f,dashDistance=3,dashDuration=.38f,dashCooldown=3;
        public float speed=2.2f,damage=4,detectionRange=14,attackRange=9;
        public float retreatDistance=4,preferredDistance=6,projectileSpeed=5;
        public float aimLockTime=.9f,fireTime=1.22f,attackDuration=1.8f,cooldown=1.3f;
        [SerializeField,Min(0),Tooltip("시전/대시 종료 후 다음 기술까지의 최소 대기 시간(초)")] private float skillDelay=3;
        public LayerMask obstacleMask=1<<8;
        Transform target;
        CombatHealth health,targetHealth;
        NavMeshAgent agent;
        bool casting,fired,aimLocked,retreating;
        float castStarted,nextAttack,nextPath;
        Vector3 aimedPoint;
        bool dashing;
        float dashStarted,nextDash;
        Vector3 dashDirection;
        float dashTravelled;
        float GroundOffset=>agent?agent.baseOffset*Mathf.Abs(transform.lossyScale.y):1;
        bool TryDash()
        {
            if(!Navigable||Time.time<nextDash||Time.time<nextAttack)return false;
            var away=Vector3.ProjectOnPlane(transform.position-target.position,Vector3.up).normalized;
            if(away.sqrMagnitude<.001f)away=-visualRoot.forward;
            var filter=new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask};
            foreach(float angle in new[]{0f,40f,-40f})
            {
                var direction=Quaternion.Euler(0,angle,0)*away;NavMeshHit ground,edge;
                if(!NavMesh.SamplePosition(transform.position-Vector3.up*GroundOffset,out ground,.6f,filter))continue;
                if(NavMesh.Raycast(ground.position,ground.position+direction*dashDistance,out edge,filter))continue;
                if(DashBlocked(direction,dashDistance))continue;
                casting=false;fired=false;aimLocked=false;Charge(false);Stop();agent.ResetPath();
                dashing=true;dashStarted=Time.time;dashTravelled=0;dashDirection=direction;nextDash=Time.time+dashCooldown;
                modelAnimator.SetBool(Attacking,false);modelAnimator.SetBool(Moving,false);modelAnimator.CrossFadeInFixedTime("Dash",.06f);
                if(dashPetals)dashPetals.Play();return true;
            }
            nextDash=Time.time+.3f;return false;
        }
        bool DashBlocked(Vector3 direction,float distance)
        {
            float radius=agent.radius*Mathf.Abs(transform.lossyScale.x);
            foreach(var hit in Physics.SphereCastAll(transform.position,radius,direction,distance,~0,QueryTriggerInteraction.Ignore))
                if(!hit.transform.IsChildOf(transform)&&!hit.collider.GetComponentInParent<CombatHealth>())return true;
            return false;
        }
        void TickDash()
        {
            float t=Mathf.Clamp01((Time.time-dashStarted)/dashDuration);
            float desired=dashDistance*(1-Mathf.Pow(1-t,2));float step=desired-dashTravelled;
            var filter=new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask};NavMeshHit ground,edge;
            bool safe=Navigable&&NavMesh.SamplePosition(transform.position-Vector3.up*GroundOffset,out ground,.6f,filter);
            if(safe){NavMesh.SamplePosition(transform.position-Vector3.up*GroundOffset,out ground,.6f,filter);safe=!NavMesh.Raycast(ground.position,ground.position+dashDirection*step,out edge,filter)&&!DashBlocked(dashDirection,step);}
            if(safe&&step>0){agent.Move(dashDirection*step);dashTravelled=desired;}
            if(!safe||t>=1){dashing=false;Stop();nextAttack=Time.time+Mathf.Max(.45f,skillDelay);modelAnimator.CrossFadeInFixedTime("Idle",.12f);if(dashPetals)dashPetals.Stop(true,ParticleSystemStopBehavior.StopEmitting);}
        }
        static readonly int Moving=Animator.StringToHash("Moving"),Attacking=Animator.StringToHash("Attacking");
        public bool Windup=>casting&&!fired;
        void Awake()
        {
            health=GetComponent<CombatHealth>();agent=GetComponent<NavMeshAgent>();agent.updateRotation=false;
            var body=GetComponent<Rigidbody>();body.isKinematic=true;body.constraints=RigidbodyConstraints.FreezeRotation;
        }
        void Start()
        {
            var player=FindFirstObjectByType<PlayerMotor>();
            if(player){target=player.transform;targetHealth=player.GetComponent<CombatHealth>();}
            nextAttack=Time.time+.8f;
        }
        bool Clear(Vector3 from,Vector3 to)
        {var d=to-from;return !Physics.Raycast(from,d.normalized,d.magnitude,obstacleMask,QueryTriggerInteraction.Ignore);}
        bool Navigable=>agent&&agent.isActiveAndEnabled&&agent.isOnNavMesh;
        void Stop(){if(Navigable)agent.isStopped=true;}
        void Face(Vector3 point)
        {
            var dir=Vector3.ProjectOnPlane(point-transform.position,Vector3.up);
            if(visualRoot&&dir.sqrMagnitude>.001f)visualRoot.rotation=Quaternion.LookRotation(dir);
        }
        static void SetCharge(ParticleSystem p,bool value)
        {
            if(!p)return;
            if(value&&!p.isPlaying)p.Play();
            else if(!value)p.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        void Charge(bool value){SetCharge(leftCharge,value);SetCharge(rightCharge,value);}
        bool MoveTo(Vector3 destination)
        {
            if(!Navigable)return false;
            var filter=new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask};
            if(!NavMesh.SamplePosition(destination,out var hit,1.2f,filter))return false;
            var flat=Vector3.ProjectOnPlane(hit.position-transform.position,Vector3.up);
            if(retreating&&flat.sqrMagnitude>.001f&&DashBlocked(flat.normalized,flat.magnitude))return false;
            var path=new NavMeshPath();
            if(!agent.CalculatePath(hit.position,path)||path.status!=NavMeshPathStatus.PathComplete)return false;
            agent.isStopped=false;agent.speed=speed;agent.stoppingDistance=.15f;agent.SetPath(path);return true;
        }
        void FixedUpdate()
        {
            if(!target||!targetHealth||!health||health.Dead||targetHealth.Dead){Cancel();return;}
            float distance=Vector3.ProjectOnPlane(target.position-transform.position,Vector3.up).magnitude;
            if(dashing){TickDash();return;}
            if(distance>detectionRange){Cancel();return;}
            if(distance<dashTriggerDistance&&TryDash())return;
            if(casting)
            {
                Stop();float t=Time.time-castStarted;
                if(!aimLocked){Face(target.position);aimedPoint=target.position;if(t>=aimLockTime)aimLocked=true;}
                Charge(t>=.15f&&t<fireTime);
                if(!fired&&t>=fireTime){fired=true;Shoot();}
                if(t>=attackDuration){casting=false;if(modelAnimator)modelAnimator.SetBool(Attacking,false);nextAttack=Time.time+Mathf.Max(cooldown,skillDelay);Charge(false);}
                return;
            }
            Face(target.position);
            if(distance<retreatDistance)retreating=true;
            if(distance>=preferredDistance)retreating=false;
            bool clear=Clear(transform.position,target.position);
            bool moving=retreating||distance>preferredDistance+.8f||!clear;
            if(!moving)Stop();
            else if(Time.time>=nextPath)
            {
                nextPath=Time.time+.3f;bool found=false;
                if(retreating)
                {
                    Vector3 away=Vector3.ProjectOnPlane(transform.position-target.position,Vector3.up).normalized;
                    if(away.sqrMagnitude<.001f)away=visualRoot?-visualRoot.forward:-transform.forward;
                    foreach(float angle in new[]{0f,45f,-45f,85f,-85f})
                    {
                        Vector3 point=transform.position-Vector3.up*GroundOffset+Quaternion.Euler(0,angle,0)*away*2;
                        if(Vector3.Distance(point+Vector3.up,target.position)<=distance+.3f)continue;
                        if(MoveTo(point)){found=true;break;}
                    }
                }
                else found=MoveTo(target.position-Vector3.up+(transform.position-target.position).normalized*(clear?preferredDistance:.5f));
                if(!found)Stop();
            }
            bool canRetreat=retreating&&Navigable&&!agent.isStopped&&agent.hasPath;
            if(distance<=attackRange&&clear&&!canRetreat&&Time.time>=nextAttack)
            {
                Stop();casting=true;fired=false;aimLocked=false;castStarted=Time.time;aimedPoint=target.position;
                if(modelAnimator){modelAnimator.SetBool(Moving,false);modelAnimator.SetBool(Attacking,true);}
            }
        }
        void Shoot()
        {
            if(!projectilePrefab||!leftHand||!rightHand)return;
            Vector3 origin=(leftHand.position+rightHand.position)*.5f;
            Vector3 direction=(aimedPoint-origin).normalized;
            if(direction.sqrMagnitude<.001f||!Clear(origin,aimedPoint))return;
            var shot=Instantiate(projectilePrefab,origin,Quaternion.LookRotation(direction));
            var projectile=shot.GetComponent<CombatProjectile>();
            projectile.owner=transform;projectile.hostile=true;projectile.direction=direction;
            projectile.speed=projectileSpeed;projectile.damage=damage;projectile.range=12;projectile.radius=.13f;
        }
        void Update(){if(modelAnimator)modelAnimator.SetBool(Moving,!dashing&&!casting&&Navigable&&!agent.isStopped&&agent.velocity.sqrMagnitude>.02f);}
        void Cancel()
        {
            casting=false;fired=false;aimLocked=false;retreating=false;dashing=false;Stop();Charge(false);if(dashPetals)dashPetals.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            if(modelAnimator){modelAnimator.SetBool(Moving,false);modelAnimator.SetBool(Attacking,false);}
        }
        void OnDisable(){Cancel();nextAttack=Time.time+.8f;nextPath=0;}
        void OnGUI()
        {
            if(SkillWindow.IsOpen||!health||health.Dead||!Camera.main)return;
            var p=Camera.main.WorldToScreenPoint(transform.position+Vector3.up*1.15f);if(p.z<=0)return;
            var previous=GUI.color;float y=Screen.height-p.y;
            GUI.color=Color.black;GUI.DrawTexture(new Rect(p.x-27,y,54,6),Texture2D.whiteTexture);
            GUI.color=Windup?new Color(1,.65f,.8f):new Color(.9f,.28f,.48f);
            GUI.DrawTexture(new Rect(p.x-27,y,54*Mathf.Clamp01(health.Current/Mathf.Max(1,health.maxHealth)),6),Texture2D.whiteTexture);GUI.color=previous;
        }
    }
}
