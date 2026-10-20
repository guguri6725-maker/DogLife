using UnityEngine;
using UnityEngine.AI;
namespace DogLife
{
    [RequireComponent(typeof(CombatHealth),typeof(NavMeshAgent),typeof(Rigidbody))]
    public sealed class SpringBoarBoss : MonoBehaviour
    {
        public enum Pattern { Charge, Tusk, Slam, Spikes, None }
        public enum Phase { Move, Warning, Attack, Stunned, Recovery }
        [Header("이동 / 패턴 간격 (월드 단위)")]
        [SerializeField] private float moveSpeed=2.5f, detectionRange=42, cooldown=1.25f, recoveryTime=.55f;
        [SerializeField] private float flankDistance=8, flankDelay=2;
        [Header("광폭 돌진")]
        [SerializeField] private float scrapeTime=2.2f, chargeSpeed=22, chargeDuration=2.8f, wallStunTime=2.5f;
        [SerializeField,Min(1)] private float chargeMaxDistance=18, chargeCooldown=1;
        [SerializeField,Min(0)] private float roomEntryInset=3;
        [SerializeField] private float bodyRadius=2.3f, hornOffset=4.1f, chargeHitRadius=2.4f;
        [Header("송곳니 올려치기")]
        [SerializeField] private float tuskRange=7, tuskAngle=95, tuskWarning=1.2f, tuskTime=1.1f, tuskDamage=2;
        [SerializeField] private float launchSpeed=12, launchLift=6;
        [Header("내려찍기")]
        [SerializeField] private float slamRadius=9, slamWarning=1.35f, slamTime=1.1f;
        [Header("뼈 가시 해금 체력")]
        [SerializeField,Range(0,1)] private float spikeHealthThreshold=.8f;
        [SerializeField,Range(3,5)] private int spikeCount=5;
        [SerializeField] private float spikeWarning=1.1f, spikeLifetime=2.2f, spikeRadius=1.25f;
        [Header("연결")]
        [SerializeField] private Animator animator;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private LayerMask environmentMask=1<<8;
        ForestRoom encounterRoom;bool engaged,chargeWarningWall;float nextCharge,chargeTravelled;
        public void BindRoom(ForestRoom room){encounterRoom=room;}
        public bool Engaged=>engaged;
        CombatHealth health,targetHealth;PlayerMotor player;Collider targetCollider;NavMeshAgent agent;
        readonly RaycastHit[] wallHits=new RaycastHit[64];
        readonly Vector3[] spikes=new Vector3[5];
        float elapsed,nextAttack,nextPath,flankClock,orbit=1,chargeWarningLength=30;bool hit;Pattern previous=Pattern.Spikes;
        GameAudio.Handle chargeSound;
        bool attackSoundPlayed;
        int scrapeSounds;
        public Pattern AttackPattern {get;private set;}
        public Phase State {get;private set;}
        public Vector3 Forward {get;private set;}=Vector3.forward;
        public Vector3 Origin {get;private set;}
        public float Progress=>Mathf.Clamp01(elapsed/Duration());
        public float TuskRange=>tuskRange;
        public float TuskAngle=>tuskAngle;
        public float SlamRadius=>slamRadius;
        // 이펙트와 판정이 같은 파면을 사용하고, 지나간 안쪽에는 피해를 남기지 않습니다.
        public const float SlamWaveWidth=1.15f;
        public float SlamWaveProgress=>Mathf.InverseLerp(.30f,.60f,Progress);
        public float ChargeWidth=>chargeHitRadius;
        public float ChargeLength=>chargeWarningLength;
        public float HornOffset=>hornOffset;
        public float SpikeRadius=>spikeRadius;
        public int SpikeCount {get;private set;}
        public Vector3 SpikePoint(int i)=>spikes[i];
        public bool Enraged=>health&&health.Current<=health.maxHealth*spikeHealthThreshold;
        bool Nav=>agent&&agent.isActiveAndEnabled&&agent.isOnNavMesh;
        void Awake()
        {
            health=GetComponent<CombatHealth>();agent=GetComponent<NavMeshAgent>();
            agent.updateRotation=false;agent.updateUpAxis=false;
            var rb=GetComponent<Rigidbody>();rb.isKinematic=true;rb.constraints=RigidbodyConstraints.FreezeRotation;
            if(animator)animator.applyRootMotion=false;
            if(!GetComponent<SpringBoarFootstepAudio>())gameObject.AddComponent<SpringBoarFootstepAudio>();
        }
        void Start()
        {
            if(!encounterRoom)encounterRoom=GetComponentInParent<ForestRoom>();
            player=FindFirstObjectByType<PlayerMotor>();
            if(player){targetHealth=player.GetComponent<CombatHealth>();targetCollider=player.GetComponent<Collider>();
                foreach(var a in GetComponentsInChildren<Collider>())foreach(var b in player.GetComponentsInChildren<Collider>())Physics.IgnoreCollision(a,b);}
            nextAttack=Time.time+2;Forward=transform.forward;orbit=Random.value<.5f?1:-1;Play("Idle",1,true);
        }
        float Duration()
        {
            if(State==Phase.Stunned)return Mathf.Max(.1f,wallStunTime);
            if(State==Phase.Recovery)return Mathf.Max(.1f,recoveryTime);
            if(State==Phase.Warning)return Mathf.Max(.1f,AttackPattern==Pattern.Charge?scrapeTime:AttackPattern==Pattern.Tusk?tuskWarning:AttackPattern==Pattern.Slam?slamWarning:spikeWarning);
            return Mathf.Max(.1f,AttackPattern==Pattern.Charge?chargeDuration:AttackPattern==Pattern.Tusk?tuskTime:AttackPattern==Pattern.Slam?slamTime:spikeLifetime);
        }
        void Play(string state,float duration,bool loop=false)
        {if(!animator)return;animator.SetBool("Moving",false);animator.SetFloat("PhaseSpeed",loop?1:1/Mathf.Max(.1f,duration));animator.CrossFadeInFixedTime(state,.12f);}
        void Stop(){if(Nav){agent.isStopped=true;agent.ResetPath();agent.velocity=Vector3.zero;}}
        void Face(Vector3 direction,bool snap=false)
        {direction.y=0;if(direction.sqrMagnitude<.001f)return;Forward=direction.normalized;transform.rotation=snap?Quaternion.LookRotation(Forward):Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(Forward),130*Time.fixedDeltaTime);if(visualRoot)visualRoot.localRotation=Quaternion.identity;}
        bool Visible(Vector3 a,Vector3 b)=>!Physics.Linecast(a+Vector3.up,b+Vector3.up,environmentMask,QueryTriggerInteraction.Ignore);
        Pattern Choose(float distance,float angle)
        {
            if(flankClock>=flankDelay&&distance<=slamRadius)return Pattern.Slam;
            bool tuskEligible=distance<=tuskRange&&angle<=tuskAngle*.65f;
            if(tuskEligible&&previous!=Pattern.Tusk)return Pattern.Tusk;
            bool chargeEligible=Time.time>=nextCharge&&previous!=Pattern.Charge&&distance<=chargeMaxDistance;
            bool slamEligible=distance<=slamRadius&&previous!=Pattern.Slam;
            bool spikeEligible=Enraged&&previous!=Pattern.Spikes;
            int count=(chargeEligible?1:0)+(slamEligible?1:0)+(spikeEligible?1:0);
            if(count==0)return Pattern.None; // Approach instead of turning unavailable attacks into charges.
            int choice=Random.Range(0,count);
            if(chargeEligible&&choice--==0)return Pattern.Charge;
            if(slamEligible&&choice--==0)return Pattern.Slam;
            return Pattern.Spikes;
        }
        void Begin(Pattern pattern)
        {
            AttackPattern=pattern;previous=pattern;flankClock=0;Stop();Face(player.transform.position-transform.position,true);
            Origin=transform.position;SpikeCount=0;
            if(pattern==Pattern.Charge){
                chargeTravelled=0;chargeWarningWall=false;
                chargeWarningLength=Mathf.Min(chargeMaxDistance,chargeSpeed*chargeDuration);
                int n=Physics.SphereCastNonAlloc(Origin+Forward*hornOffset+Vector3.up*(bodyRadius+.3f),bodyRadius,Forward,wallHits,chargeWarningLength,~0,QueryTriggerInteraction.Ignore);
                for(int i=0;i<n;i++)if(!wallHits[i].collider.GetComponentInParent<CombatHealth>()&&Mathf.Abs(wallHits[i].normal.y)<.7f)
                {chargeWarningLength=Mathf.Min(chargeWarningLength,Mathf.Max(0,wallHits[i].distance-.15f));chargeWarningWall=true;}
                chargeWarningLength=ClipChargePath(agent,encounterRoom,Origin,Forward,chargeWarningLength,hornOffset+bodyRadius,out bool navBlocked);
                chargeWarningWall|=navBlocked;
            }
            if(pattern==Pattern.Spikes)PlaceSpikes();
            Enter(Phase.Warning);
        }
        // 두 페이즈 모두 실제 이동 경계까지 예고해, 돌진이 먼저 멈추는 긴 경고선을 방지합니다.
        internal static float ClipChargePath(NavMeshAgent nav,ForestRoom room,Vector3 origin,Vector3 forward,float distance,float inset,out bool navBlocked)
        {
            navBlocked=false;
            if(room&&Mathf.Abs(forward.x)>.0001f)
            {
                float edge=forward.x>0?room.exitX-inset:room.entryX+inset;
                distance=Mathf.Min(distance,Mathf.Max(0,(edge-origin.x)/forward.x));
            }
            if(distance>0&&nav&&nav.isActiveAndEnabled&&nav.isOnNavMesh&&nav.Raycast(origin+forward*distance,out var hit))
            {
                navBlocked=true;
                distance=Mathf.Min(distance,Mathf.Max(0,Vector3.Dot(hit.position-origin,forward)-.15f));
            }
            return distance;
        }
        void PlaceSpikes()
        {
            // Positions are locked before the warning begins, leaving escape lanes.
            for(int i=0;i<Mathf.Clamp(spikeCount,3,5);i++)
            {
                float angle=i*137.5f+Random.Range(-12,12);float radius=i==0?.6f:2.8f+(i%2)*1.6f;
                Vector3 p=player.transform.position+Quaternion.AngleAxis(angle,Vector3.up)*Vector3.forward*radius;
                NavMeshHit nav;
                if(!NavMesh.SamplePosition(p,out nav,1.4f,new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask}))continue;
                if(!Visible(player.transform.position,nav.position))continue;
                bool overlaps=false;for(int j=0;j<SpikeCount;j++)if(Vector3.Distance(spikes[j],nav.position)<spikeRadius*2)overlaps=true;
                if(!overlaps)spikes[SpikeCount++]=nav.position;
            }
        }
        void Enter(Phase phase)
        {
            chargeSound.Stop();attackSoundPlayed=false;scrapeSounds=0;
            if(phase==Phase.Stunned)GameAudio.Play("Boar_Wall_Impact",transform.position,1);
            if(phase==Phase.Warning&&AttackPattern==Pattern.Spikes)GameAudio.Play("Boar_Roar",transform.position,.8f,follow:transform);
            if(phase==Phase.Attack&&AttackPattern==Pattern.Charge)chargeSound=GameAudio.Play("Boar_Charge",transform.position,.85f,follow:transform);
            if(State==Phase.Attack&&AttackPattern==Pattern.Charge)nextCharge=Time.time+chargeCooldown;
            State=phase;elapsed=0;hit=false;Stop();
            if(phase==Phase.Move){nextAttack=Time.time+cooldown;orbit=-orbit;Play("Idle",1,true);return;}
            if(phase==Phase.Stunned){Play("Stunned",wallStunTime,true);return;}
            if(phase==Phase.Recovery){SpikeCount=0;Play("Recovery",recoveryTime);return;}
            string clip=AttackPattern==Pattern.Charge?(phase==Phase.Warning?"Scrape":"Charge"):AttackPattern==Pattern.Tusk?(phase==Phase.Warning?"TuskReady":"Tusk"):AttackPattern==Pattern.Slam?(phase==Phase.Warning?"SlamReady":"Slam"):"Roar";
            Play(clip,Duration(),phase==Phase.Attack&&AttackPattern==Pattern.Charge);
        }
        void FixedUpdate()
        {
            if(!health||health.Dead||!player||!targetHealth||targetHealth.Dead){Stop();return;}
            bool inside=!encounterRoom||encounterRoom.CombatStarted;
            if(!inside){if(State!=Phase.Move){Enter(Phase.Move);SpikeCount=0;}Stop();if(animator)animator.SetBool("Moving",false);return;}
            if(!engaged){engaged=true;nextAttack=Mathf.Max(nextAttack,Time.time+1.5f);}
            if(CombatFeedback.Paused(this))return;
            if(State==Phase.Move)
            {
                Vector3 delta=player.transform.position-transform.position;delta.y=0;float distance=delta.magnitude;
                float angle=Vector3.Angle(visualRoot?visualRoot.forward:Forward,delta);
                flankClock=distance<flankDistance&&angle>70?flankClock+Time.fixedDeltaTime:Mathf.Max(0,flankClock-Time.fixedDeltaTime);
                if((encounterRoom||distance<detectionRange)&&Time.time>=nextAttack&&Visible(transform.position,player.transform.position)){var pattern=Choose(distance,angle);if(pattern!=Pattern.None){Begin(pattern);return;}}
                if(Nav&&(encounterRoom||distance<detectionRange))
                {
                    agent.isStopped=false;agent.speed=moveSpeed;agent.stoppingDistance=.3f;
                    if(Time.time>=nextPath){nextPath=Time.time+.35f;Vector3 outwards=distance>.1f?-delta.normalized:-Forward;Vector3 p=player.transform.position+(distance<8?Quaternion.AngleAxis(30*orbit,Vector3.up)*outwards:outwards)*6;
                        if(encounterRoom)p.x=Mathf.Clamp(p.x,encounterRoom.entryX+hornOffset+bodyRadius,encounterRoom.exitX-hornOffset-bodyRadius);
                        NavMeshHit nav;if(NavMesh.SamplePosition(p,out nav,2,new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask}))agent.SetDestination(nav.position);}
                    // Keep watching the player when circling nearby; tangent-facing made every close target look like a flank.
                    if(distance<8)Face(delta);
                    else if(agent.desiredVelocity.sqrMagnitude>.01f)Face(agent.desiredVelocity);
                }else Stop();
                if(animator){float speed=Nav?agent.velocity.magnitude:0;animator.SetBool("Moving",speed>.08f);animator.SetFloat("WalkSpeed",Mathf.Clamp(speed/2.6f,.1f,2));}
                return;
            }
            elapsed+=Time.fixedDeltaTime;
            if(State==Phase.Warning)
            {
                if(AttackPattern==Pattern.Charge&&scrapeSounds<3&&Progress>=.12f+scrapeSounds*.28f)
                {scrapeSounds++;GameAudio.Play("Boar_Scrape",transform.position,.65f,follow:transform);}
                if(elapsed>=Duration())Enter(Phase.Attack);return;
            }
            if(State==Phase.Stunned||State==Phase.Recovery){if(elapsed>=Duration())Enter(Phase.Move);return;}
            if(AttackPattern==Pattern.Charge){Charge();return;}
            float t=Progress;
            if(!attackSoundPlayed&&t>=(AttackPattern==Pattern.Tusk?.32f:AttackPattern==Pattern.Slam?.30f:.08f))
            {
                attackSoundPlayed=true;
                GameAudio.Play(AttackPattern==Pattern.Tusk?"Boar_Tusk_Uppercut":AttackPattern==Pattern.Slam?"Boar_Ground_Slam":"Boar_Bone_Spike",AttackPattern==Pattern.Spikes&&SpikeCount>0?spikes[0]:transform.position,.9f);
            }
            if(!hit&&(AttackPattern==Pattern.Tusk?t>=.32f&&t<=.70f:AttackPattern==Pattern.Slam?t>=.30f&&t<=.8f:t>=.08f))TryHit();
            if(elapsed>=Duration())Enter(Phase.Recovery);
        }
        void Charge()
        {
            Vector3 old=transform.position;float step=chargeSpeed*Time.fixedDeltaTime,allowed=step;bool wall=false;
            int count=Physics.SphereCastNonAlloc(old+Forward*hornOffset+Vector3.up*(bodyRadius+.3f),bodyRadius,Forward,wallHits,step+.15f,~0,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++){var h=wallHits[i];if(h.collider.GetComponentInParent<CombatHealth>()||Mathf.Abs(h.normal.y)>.7f)continue;allowed=Mathf.Min(allowed,Mathf.Max(0,h.distance-.15f));wall=true;}
            allowed=Mathf.Min(allowed,Mathf.Max(0,chargeWarningLength-chargeTravelled));
            Vector3 next=old+Forward*allowed;
            bool roomEdge=false;
            if(encounterRoom){float x=Mathf.Clamp(next.x,encounterRoom.entryX+hornOffset+bodyRadius,encounterRoom.exitX-hornOffset-bodyRadius);roomEdge=Mathf.Abs(x-next.x)>.001f;next.x=x;}
            if(Nav){NavMeshHit edge;if(agent.Raycast(next,out edge)){next=Vector3.MoveTowards(edge.position,old,.15f);wall=true;}agent.Warp(next);}else transform.position=next;
            chargeTravelled+=Vector3.Distance(old,transform.position);
            if(!hit&&targetCollider){Vector3 a=old+Forward*hornOffset,b=transform.position+Forward*hornOffset,p=targetCollider.bounds.center;a.y=p.y;b.y=p.y;Vector3 ab=b-a;float u=ab.sqrMagnitude>.0001f?Mathf.Clamp01(Vector3.Dot(p-a,ab)/ab.sqrMagnitude):0;
                if(Vector3.Distance(p,a+ab*u)<=chargeHitRadius+targetCollider.bounds.extents.x&&Visible(old,player.transform.position))Hit(1,12,3);}
            bool reachedEnd=chargeTravelled>=chargeWarningLength-.01f;
            if(wall||(reachedEnd&&chargeWarningWall)){Enter(Phase.Stunned);return;}
            if(roomEdge||reachedEnd||elapsed>=chargeDuration)Enter(Phase.Recovery);
        }
        public bool Contains(Vector3 p,float radius=0)
        {
            if(AttackPattern==Pattern.Spikes){for(int i=0;i<SpikeCount;i++)if(Mathf.Abs(p.y-spikes[i].y)<4&&Vector3.ProjectOnPlane(p-spikes[i],Vector3.up).magnitude<=spikeRadius+radius)return true;return false;}
            Vector3 d=Vector3.ProjectOnPlane(p-transform.position,Vector3.up);if(Mathf.Abs(p.y-transform.position.y)>9)return false;
            if(AttackPattern==Pattern.Slam)
            {
                float previousProgress=Mathf.Clamp01((elapsed-Time.fixedDeltaTime)/Duration());
                float inner=slamRadius*Mathf.InverseLerp(.30f,.60f,previousProgress)-SlamWaveWidth;
                return d.magnitude<=slamRadius*SlamWaveProgress+radius&&d.magnitude+radius>=Mathf.Max(0,inner);
            }
            float allowance=Mathf.Asin(Mathf.Clamp01(radius/Mathf.Max(.01f,d.magnitude)))*Mathf.Rad2Deg;
            return d.magnitude<=tuskRange+radius&&Vector3.Angle(Forward,d)<=tuskAngle*.5f+allowance;
        }
        void TryHit(){if(targetCollider&&Contains(targetCollider.bounds.center,targetCollider.bounds.extents.x)&&Visible(transform.position,player.transform.position))Hit(AttackPattern==Pattern.Tusk?tuskDamage:1,AttackPattern==Pattern.Tusk?launchSpeed:7,AttackPattern==Pattern.Tusk?launchLift:2);}
        void Hit(float damage,float push,float lift)
        {
            hit=true;Vector3 direction=player.transform.position-transform.position;
            if(CombatFeedback.DealDamage(targetHealth,damage,targetCollider.ClosestPoint(transform.position),direction,1.5f,transform,new Color(1,.45f,.65f),true)&&!targetHealth.Dead)player.ApplyKnockback(direction,push,lift,.65f);
        }
        void OnDisable(){chargeSound.Stop();Stop();SpikeCount=0;}
    }
}
