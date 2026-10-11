using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace DogLife
{
    public sealed class SpringBoarPhaseTwo:MonoBehaviour
    {
        public enum Pattern { Charge, Tusks, Bloom, Storm }
        public enum Shape { Circle, Sector, Lane }
        public enum Effect { Spike, Sweep, Lift, Roots, Slam, Charge }
        [System.Serializable] public sealed class Strike
        {
            [Min(.1f)] public float range=8,warning=1.1f,active=.3f;
            [Range(1,360)] public float angle=100;
            [Min(.1f)] public float width=2.4f;
            [Min(0)] public float damage=2,push=7,lift=2;
            [Range(.1f,1)] public float propagation=.65f;
        }
        public sealed class Hazard
        {
            public Shape shape;public Effect effect;public Vector3 origin,forward;
            public float range,angle,width,warning,active,age,damage,push,lift,propagation=.65f;
            public bool hit,ambient,visualOnly,soundPlayed;public int group;
            public float Progress=>Mathf.Clamp01(age/Mathf.Max(.01f,warning));
            public float AttackProgress=>Mathf.Clamp01((age-warning)/Mathf.Max(.01f,active));
            public bool Contains(Vector3 point,float padding)
            {
                Vector3 d=point-origin;if(Mathf.Abs(d.y)>8)return false;d.y=0;
                if(shape==Shape.Lane){float z=Vector3.Dot(d,forward),x=Vector3.Dot(d,Vector3.Cross(Vector3.up,forward));return z>=-padding&&z<=range+padding&&Mathf.Abs(x)<=width+padding;}
                float reach=effect==Effect.Roots?range*Mathf.Clamp01(AttackProgress/propagation):range;
                if(d.magnitude>reach+padding)return false;
                return shape==Shape.Circle||Vector3.Angle(forward,d)<=angle*.5f+Mathf.Asin(Mathf.Clamp01(padding/Mathf.Max(.01f,d.magnitude)))*Mathf.Rad2Deg;
            }
        }
        [Header("이동 / 패턴 선택")]
        [SerializeField] float moveSpeed=3.4f,rotationSpeed=110,preferredDistance=6,decisionDelay=.85f,recovery=.7f;
        [SerializeField] float chargeCooldown=6.5f,tusksCooldown=3,bloomCooldown=4;
        [Header("2연속 돌진")]
        [SerializeField] Strike charge=new Strike{range=18,warning=1.5f,width=2.4f,damage=2,push=12,lift=3};
        [SerializeField] float chargeSpeed=24,secondChargeWarning=.8f,brakeTime=.45f,wallStun=3,bodyRadius=2.3f,hornOffset=4.1f;
        [Header("송곳니 연속 공격")]
        [SerializeField] Strike sweep=new Strike{range=8,angle=150,warning=1.1f,active=.35f,damage=2,push=6,lift=0};
        [SerializeField] Strike lift=new Strike{range=9,angle=65,warning=.8f,active=.35f,damage=2,push=12,lift=6};
        [SerializeField] float tuskFollowDelay=.2f;
        [SerializeField,Min(0)] float swingLead=.22f;
        [Header("갈비뼈 개화 / 4갈래 사이 안전 통로")]
        [SerializeField] Strike bloom=new Strike{range=12,angle=42,warning=1.5f,active=.6f,damage=2,push=8,lift=2};
        [SerializeField,Range(2,6)] int bloomBranches=4;
        [Header("상시 추적 뼈 가시")]
        [SerializeField] Strike spike=new Strike{range=1.5f,warning=1,active=.18f,damage=1,push=3,lift=1};
        [SerializeField] float spikeInterval=3.5f,spikeResumeDelay=2,impactSeparation=.45f,spikeLinger=.65f;
        [Header("HP 특수 / 벚꽃 뿌리 폭주")]
        [SerializeField,Range(0,1)] float stormHealth=.5f;
        [SerializeField] Strike storm=new Strike{range=22,angle=38,warning=1.25f,active=.6f,damage=2,push=9,lift=2};
        [SerializeField] Strike finalSlam=new Strike{range=11,warning=1.5f,active=.45f,damage=2,push=12,lift=4};
        [SerializeField,Range(1,6)] int stormWaves=3;
        [SerializeField] float stormRoar=1.5f,waveDelay=.35f,groggyDuration=4;
        [Header("참조")]
        [SerializeField] Animator animator;
        [SerializeField] SpringBoarVfx effects;
        [SerializeField] LayerMask environmentMask=1<<8;
        public readonly List<Hazard> Hazards=new List<Hazard>(32);
        public bool Active {get;private set;}
        public bool Special {get;private set;}
        public bool VulnerableWindow {get;private set;}
        public Pattern CurrentPattern {get;private set;}
        public bool Charging {get;private set;}
        public bool Stunned {get;private set;}
        public float Pulse {get;private set;}
        public Transform RibAnchor {get;private set;}
        CombatHealth health,targetHealth;PlayerMotor player;Collider target;NavMeshAgent agent;ForestRoom room;
        readonly float[] ready=new float[3];readonly int[] used=new int[3];readonly RaycastHit[] hits=new RaycastHit[64];
        readonly HashSet<int> hitGroups=new HashSet<int>();
        readonly HashSet<int> warningSoundGroups=new HashSet<int>(),impactSoundGroups=new HashSet<int>();
        GameAudio.Handle chargeSound;
        int previous=-1,groupId;float nextSpike,nextPath;bool specialUsed,wallCollision;
        bool Nav=>agent&&agent.isActiveAndEnabled&&agent.isOnNavMesh;
        bool Live=>Active&&health&&!health.Dead&&targetHealth&&!targetHealth.Dead;
        public void Activate(Animator skeleton)
        {
            if(Active||!isActiveAndEnabled)return;animator=skeleton;health=GetComponent<CombatHealth>();agent=GetComponent<NavMeshAgent>();room=GetComponentInParent<ForestRoom>();
            player=FindFirstObjectByType<PlayerMotor>();targetHealth=player?player.GetComponent<CombatHealth>():null;target=player?player.GetComponent<Collider>():null;
            if(!player)return;
            foreach(var bone in animator.GetComponentsInChildren<Transform>())if(bone.name=="Chest"){RibAnchor=bone;break;}
            effects=GetComponentInChildren<SpringBoarVfx>(true);if(effects){effects.phaseTwo=this;effects.gameObject.SetActive(true);}
            Active=true;nextSpike=Time.time+spikeResumeDelay;StartCoroutine(Combat());
        }
        void Stop(){if(Nav){agent.isStopped=true;agent.ResetPath();agent.velocity=Vector3.zero;}}
        void Pose(string name,float duration=1,bool loop=false)
        {if(!animator)return;animator.SetBool("Moving",false);animator.SetFloat("PhaseSpeed",loop?1:1/Mathf.Max(.1f,duration));animator.CrossFadeInFixedTime(name,.12f,0);}
        Vector3 Direction(){var d=player.transform.position-transform.position;d.y=0;return d.sqrMagnitude>.01f?d.normalized:transform.forward;}
        void Face(){transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(Direction()),rotationSpeed*Time.deltaTime);}
        IEnumerator Wait(float seconds,bool face=false)
        {float t=0;while(Live&&t<seconds){if(!CombatFeedback.Paused(this)){t+=Time.deltaTime;if(face)Face();}yield return null;}}
        IEnumerator Combat()
        {
            yield return Wait(spikeResumeDelay);
            while(Live)
            {
                if(room&&!room.CombatStarted){yield return null;continue;}
                if(!specialUsed&&health.Current<=health.maxHealth*stormHealth)
                {specialUsed=true;Special=true;Hazards.Clear();Stop();yield return Storm();Special=false;nextSpike=Time.time+spikeResumeDelay;}
                else
                {
                    int choice=Choose();
                    if(choice<0){Approach();yield return null;continue;}
                    Stop();CurrentPattern=(Pattern)choice;used[choice]++;previous=choice;
                    if(choice==0)yield return DoubleCharge();else if(choice==1)yield return Tusks();else yield return Bloom();
                    ready[choice]=Time.time+(choice==0?chargeCooldown:choice==1?tusksCooldown:bloomCooldown);
                    Pose("Recovery",recovery);yield return Wait(recovery);
                }
                float until=Time.time+decisionDelay;
                while(Live&&Time.time<until&&(specialUsed||health.Current>health.maxHealth*stormHealth)){Approach();yield return null;}
            }
            Cleanup();
        }
        int Choose()
        {
            float distance=Vector3.Distance(player.transform.position,transform.position);int best=-1,score=int.MaxValue;
            for(int i=0;i<3;i++)
            {
                bool eligible=i==0?distance<=charge.range:i==1?distance<=lift.range:distance<=bloom.range;
                if(!eligible||Time.time<ready[i]||!Visible(transform.position,player.transform.position))continue;
                int s=used[i]*2+(i==previous?3:0);if(s<score||(s==score&&Random.value<.5f)){best=i;score=s;}
            }
            return best;
        }
        bool Visible(Vector3 a,Vector3 b)=>!Physics.Linecast(a+Vector3.up,b+Vector3.up,environmentMask,QueryTriggerInteraction.Ignore);
        void Approach()
        {
            Face();if(Nav){agent.isStopped=false;agent.speed=moveSpeed;agent.stoppingDistance=.4f;
                if(Time.time>=nextPath){nextPath=Time.time+.3f;Vector3 p=player.transform.position-Direction()*preferredDistance;
                    if(room)p.x=Mathf.Clamp(p.x,room.entryX+hornOffset+bodyRadius,room.exitX-hornOffset-bodyRadius);
                    if(NavMesh.SamplePosition(p,out var sample,2,new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask}))agent.SetDestination(sample.position);}}
            if(animator){animator.SetBool("Moving",Nav&&agent.velocity.sqrMagnitude>.03f);animator.SetFloat("WalkSpeed",Nav?Mathf.Clamp(agent.velocity.magnitude/2.6f,.1f,1.5f):1);}
        }
        float SeparatedWarning(float requested)
        {
            foreach(var h in Hazards)if(h.ambient&&h.age<h.warning){float remaining=h.warning-h.age;if(Mathf.Abs(requested-remaining)<impactSeparation)requested=remaining+impactSeparation;}
            return requested;
        }
        Hazard Add(Strike s,Shape shape,Effect effect,Vector3 origin,Vector3 forward,float warning,int group=0,bool ambient=false)
        {
            var h=new Hazard{shape=shape,effect=effect,origin=origin,forward=forward,range=s.range,angle=s.angle,width=s.width,warning=warning,active=s.active,damage=s.damage,push=s.push,lift=s.lift,propagation=s.propagation,group=group==0?++groupId:group,ambient=ambient};Hazards.Add(h);
            if(warningSoundGroups.Add(h.group))
            {
                if(effect==Effect.Roots||effect==Effect.Spike)GameAudio.Play("Boar_Root_Warning",origin,effect==Effect.Spike?.35f:.65f);
                else if(effect==Effect.Charge)GameAudio.Play("Boar_Scrape",origin,.65f);
            }
            return h;
        }
        IEnumerator Telegraph(Strike s,Effect effect,string pose,Shape shape,string attackPose=null)
        {
            yield return Wait(.18f,true);float duration=SeparatedWarning(s.warning);
            Add(s,shape,effect,transform.position,transform.forward,duration);Pose(pose,duration);
            float lead=attackPose==null?0:Mathf.Min(swingLead,duration*.4f);
            yield return Wait(duration-lead);if(attackPose!=null)Pose(attackPose,s.active+lead+.25f);yield return Wait(lead);
        }
        IEnumerator Tusks()
        {
            yield return Telegraph(sweep,Effect.Sweep,"P2SweepReady",Shape.Sector,"P2Sweep");yield return Wait(sweep.active+.25f);
            yield return Wait(tuskFollowDelay,true);
            yield return Telegraph(lift,Effect.Lift,"TuskReady",Shape.Sector,"Tusk");yield return Wait(lift.active+.25f);
        }
        IEnumerator Bloom()
        {
            yield return Wait(.2f,true);float duration=SeparatedWarning(bloom.warning);int group=++groupId;
            for(int i=0;i<bloomBranches;i++){var h=Add(bloom,Shape.Sector,Effect.Roots,transform.position,Quaternion.AngleAxis(i*360f/bloomBranches,Vector3.up)*transform.forward,duration,group);h.angle=Mathf.Min(h.angle,360f/bloomBranches-24);}
            Pose("P2BloomReady",duration);yield return Wait(duration);Pose("P2Bloom",bloom.active+.6f);yield return Wait(bloom.active+.6f);
        }
        IEnumerator DoubleCharge()
        {
            wallCollision=false;
            for(int pass=0;pass<2&&Live;pass++)
            {
                float turnTime=Vector3.Angle(transform.forward,Direction())/Mathf.Max(1,rotationSpeed);
                float aimTime=Mathf.Max(pass==0?.2f:brakeTime,turnTime);
                Pose("P2Brake",aimTime);yield return Wait(aimTime,true);
                float duration=SeparatedWarning(pass==0?charge.warning:secondChargeWarning);
                var direction=transform.forward;
                var h=Add(charge,Shape.Lane,Effect.Charge,transform.position+direction*hornOffset,direction,duration);h.visualOnly=true;
                float allowed=FreeDistance(h.origin,direction,charge.range);h.range=allowed;h.active=Mathf.Max(.15f,allowed/chargeSpeed);
                Pose(pass==0?"Scrape":"P2Brake",duration);yield return Wait(duration);
                Pose("Charge",1,true);Charging=true;chargeSound=GameAudio.Play("Boar_Charge",transform.position,.85f,follow:transform);float travelled=0;bool hit=false;
                while(Live&&travelled<allowed)
                {
                    if(Time.timeScale==0||CombatFeedback.Paused(this)){yield return null;continue;}
                    float step=Mathf.Min(chargeSpeed*Time.deltaTime,allowed-travelled),clear=FreeDistance(transform.position+direction*hornOffset,direction,step+.2f);
                    if(clear<step){wallCollision=true;break;}
                    Vector3 old=transform.position,next=old+direction*step;
                    if(room&&(next.x<room.entryX+hornOffset+bodyRadius||next.x>room.exitX-hornOffset-bodyRadius)){wallCollision=true;break;}
                    if(Nav){if(agent.Raycast(next,out var edge)){wallCollision=true;break;}agent.Warp(next);}else transform.position=next;
                    var contact=new Hazard{shape=Shape.Lane,origin=old+direction*hornOffset,forward=direction,range=step,width=charge.width,damage=charge.damage,push=charge.push,lift=charge.lift};
                    if(!hit&&target&&contact.Contains(target.bounds.center,target.bounds.extents.x)){hit=true;Deal(contact);}
                    travelled+=step;yield return null;
                }
                Charging=false;chargeSound.Stop();Hazards.Remove(h);
                // The telegraph was clipped to a wall: reaching its end is a collision too.
                wallCollision|=allowed<charge.range-.05f;
                if(wallCollision){GameAudio.Play("Boar_Wall_Impact",transform.position,1);Stunned=true;VulnerableWindow=true;Hazards.Clear();Pose("Stunned",1,true);yield return Wait(wallStun);Stunned=VulnerableWindow=false;nextSpike=Time.time+spikeResumeDelay;yield break;}
                Pose("P2Brake",brakeTime);
            }
        }
        float FreeDistance(Vector3 from,Vector3 direction,float distance)
        {
            int n=Physics.SphereCastNonAlloc(from+Vector3.up*(bodyRadius+.3f),bodyRadius,direction,hits,distance,~0,QueryTriggerInteraction.Ignore);
            for(int i=0;i<n;i++)if(!hits[i].collider.GetComponentInParent<CombatHealth>()&&Mathf.Abs(hits[i].normal.y)<.7f)distance=Mathf.Min(distance,Mathf.Max(0,hits[i].distance-.15f));return distance;
        }
        IEnumerator Storm()
        {
            CurrentPattern=Pattern.Storm;GameAudio.Play("Boar_Roar",transform.position,.9f,follow:transform);Pose("P2Storm",stormRoar);yield return Wait(stormRoar);
            // Alternate the original danger directions with the previous wave's safe corridors.
            Vector3 basis=Quaternion.AngleAxis(Random.Range(0,90),Vector3.up)*transform.forward;
            for(int wave=0;wave<stormWaves&&Live;wave++)
            {
                int group=++groupId;
                for(int i=0;i<4;i++){var h=Add(storm,Shape.Sector,Effect.Roots,transform.position,Quaternion.AngleAxis(i*90+(wave%2)*45,Vector3.up)*basis,storm.warning,group);h.angle=Mathf.Min(55,h.angle);}
                Pose("P2BloomReady",storm.warning);yield return Wait(storm.warning);Pose("P2Bloom",storm.active);yield return Wait(storm.active+waveDelay);
            }
            yield return Telegraph(finalSlam,Effect.Slam,"SlamReady",Shape.Circle,"Slam");yield return Wait(finalSlam.active+.25f);
            Hazards.Clear();VulnerableWindow=true;Pose("Stunned",1,true);yield return Wait(groggyDuration);VulnerableWindow=false;
        }
        void Update()
        {
            if(!Active)return;if(!Live){StopAllCoroutines();Cleanup();return;}
            if(CombatFeedback.Paused(this)||Time.timeScale==0)return;
            Pulse=Mathf.MoveTowards(Pulse,0,Time.deltaTime*1.5f);
            for(int i=Hazards.Count-1;i>=0;i--)
            {
                var h=Hazards[i];h.age+=Time.deltaTime;
                // Audio belongs to the attack event, not to a successful damage result.
                if(!h.soundPlayed&&h.age>=h.warning)
                {
                    h.soundPlayed=true;
                    if(!h.visualOnly&&impactSoundGroups.Add(h.group))
                    {
                        string key=h.effect==Effect.Spike?"Boar_Bone_Spike":h.effect==Effect.Roots?"Boar_Root_Eruption":h.effect==Effect.Slam?"Boar_Ground_Slam":"Boar_Tusk_Uppercut";
                        GameAudio.Play(key,h.origin,h.ambient?.6f:.9f,h.effect==Effect.Sweep?.9f:1);
                    }
                }
                if(!h.visualOnly&&!h.hit&&h.age>=h.warning&&h.age<=h.warning+h.active&&target&&h.Contains(target.bounds.center,target.bounds.extents.x))
                {h.hit=true;if(hitGroups.Add(h.group))Deal(h);}
                if(h.age>h.warning+h.active+(h.effect==Effect.Spike?spikeLinger:0))Hazards.RemoveAt(i);
            }
            if(Hazards.Count==0){hitGroups.Clear();warningSoundGroups.Clear();impactSoundGroups.Clear();}
            if(!Special&&!VulnerableWindow&&(!room||room.CombatStarted)&&Time.time>=nextSpike)
            {
                bool conflict=false;foreach(var h in Hazards)if(!h.ambient&&Mathf.Abs(h.warning-h.age-spike.warning)<impactSeparation)conflict=true;
                if(!conflict)
                {
                    var position=player.transform.position;bool found=!Nav;
                    if(NavMesh.SamplePosition(position,out var sample,2,new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask})){position=sample.position;found=true;}
                    if(found){Add(spike,Shape.Circle,Effect.Spike,position,transform.forward,spike.warning,ambient:true);Pulse=1;}
                    nextSpike=Time.time+spikeInterval;
                }
            }
        }
        void Deal(Hazard h)
        {
            if(!Visible(h.origin,player.transform.position))return;
            Vector3 direction=player.transform.position-h.origin;
            if(CombatFeedback.DealDamage(targetHealth,h.damage,target.ClosestPoint(h.origin),direction,1.5f,transform,new Color(.94f,.65f,.73f),true)&&!targetHealth.Dead)player.ApplyKnockback(direction,h.push,h.lift,.55f);
        }
        void Cleanup(){chargeSound.Stop();Active=Special=VulnerableWindow=Charging=Stunned=false;Hazards.Clear();hitGroups.Clear();warningSoundGroups.Clear();impactSoundGroups.Clear();Stop();}
        void OnDisable(){StopAllCoroutines();Cleanup();}
    }
}
