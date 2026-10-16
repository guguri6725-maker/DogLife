using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
namespace DogLife
{
 [RequireComponent(typeof(CombatHealth))]
 public sealed class HarvestReaperBoss:MonoBehaviour
 {
  public enum Pattern { Sweep, Cleave, Shockwave, Pumpkins, Storm, SafeWedge }
  public enum Phase { Idle, Warning, Active, Gap, Recovery }
  [Serializable] public class Attack
  {
   [Min(0)] public float damage=1,range=12,width=3,knockback=3,lift=0,warning=1.4f,duration=.8f,recovery=1.3f,cooldown=5;
   [Range(1,360)] public float angle=180;
   [Range(0,1)] public float hitTime=.35f;
  }
  [Serializable] public class PumpkinSettings
  {
   [Min(1)] public int count=5,placementAttempts=100;
   [Min(0)] public float spawnRadius=16,minSpacing=5,explosionWarning=2.4f,explosionRadius=3,explosionDamage=1,explosionDuration=.5f,poolRadius=3,poolDuration=9,poolDamage=1,tickInterval=1,spawnHeight=1.5f,fallSpeed=3;
  }
  [Serializable] public class SpecialSettings
  {
   [Range(0,1)] public float firstHP=.7f,stormHP=.5f,lastHP=.3f;
   [Min(0)] public float innerRadius=7,outerRadius=23,warning=1.7f,explosionDuration=.45f,delay=.45f,damage=1,knockback=3,stormTick=1;
   [Header("Outer storm / gather near boss")]
   [Min(.05f)] public float gatherWarning=3,gatherDuration=2,outerTickInterval=1;
   [Min(0)] public float outerDamage=1;
   [Range(1,360)] public float safeAngle=45,angleStep=45;
   [Min(1)] public int repeats=8;
  }
  [Header("References")][SerializeField] private Animator animator;
  [Header("Standing / encounter")][SerializeField] private float turnSpeed=100,engageRange=40,entryInset=3,initialDelay=2,globalCooldown=2,closeRange=8,closeDwell=5,heightTolerance=8,knockbackDuration=.4f,groundProbeHeight=5,groundProbeDistance=15,navSampleRadius=3;
  [SerializeField] private LayerMask groundMask=1<<8;
  [Header("Smooth facing")]
  [SerializeField,Min(.01f)] private float turnSmoothTime=.32f;
  [SerializeField,Range(0,15)] private float turnDeadZone=1.5f;
  [SerializeField,Min(0)] private float minimumFacingDistance=1.5f;
  [SerializeField,Range(1,45)] private float attackFacingTolerance=8;
  [SerializeField,Range(0,1)] private float recoveryTurnStart=.65f;
  float turnVelocity;
  [Header("Normal patterns")][SerializeField] private Attack sweep=new Attack();
  [SerializeField] private Attack cleave=new Attack{range=24,width=3,warning=1.6f,cooldown=6};
  [SerializeField] private Attack shockwave=new Attack{range=13,warning=1.6f,knockback=12,lift=2,cooldown=10};
  [SerializeField] private Attack pumpkins=new Attack{range=30,warning=1.8f,cooldown=12};
  [SerializeField] private PumpkinSettings pumpkin=new PumpkinSettings();
  [Header("HP specials")][SerializeField] private Attack storm=new Attack{range=22,warning=2.5f,duration=6,recovery=2,knockback=0};
  [SerializeField] private SpecialSettings special=new SpecialSettings();
  [SerializeField,Min(1f),Tooltip("50% 폭풍 예고가 퍼지는 속도 / 플레이어 기본 이동속도")]
  private float stormWarningSpeedMultiplier=1.15f;
  float stormWarningDuration;
  public sealed class Patch { public Vector3 point;public float born,nextTick;public bool exploded; }
  readonly List<Patch> patches=new List<Patch>();
  readonly float[] ready=new float[4];readonly bool[] consumed=new bool[3];readonly List<Pattern> choices=new List<Pattern>(4);
  HarvestReaperAudio sound;
  CombatHealth health,target;PlayerMotor player;Collider targetCollider;ForestRoom room;
  float elapsed,nextAttack,closeClock,nextTick,nextOuterTick;bool gathering;bool hit,engaged;int previous=-1,round,rotationSign;
  public Pattern CurrentPattern{get;private set;} public Phase State{get;private set;}
  public Vector3 Forward{get;private set;}=Vector3.forward;
  public float SafeYaw{get;private set;}
  public bool Engaged=>engaged;
  public bool Gathering=>CurrentPattern==Pattern.SafeWedge&&gathering;
  public bool OuterStormActive=>CurrentPattern==Pattern.SafeWedge&&(State==Phase.Active||(!gathering&&(State==Phase.Warning||State==Phase.Gap)));
  public IReadOnlyList<Patch> Patches=>patches;
  public PumpkinSettings Pumpkin=>pumpkin;public SpecialSettings Special=>special;
  // 미리보기도 폭풍의 실제 구간 길이를 사용합니다.
  public Attack StormAttack=>storm;
  public Attack CurrentAttack=>CurrentPattern==Pattern.Sweep?sweep:CurrentPattern==Pattern.Cleave?cleave:CurrentPattern==Pattern.Shockwave?shockwave:CurrentPattern==Pattern.Pumpkins?pumpkins:storm;
  public float Progress=>Mathf.Clamp01(elapsed/Mathf.Max(.01f,PhaseDuration));
  public float PhaseDuration=>State==Phase.Warning?(CurrentPattern==Pattern.SafeWedge?(gathering?special.gatherWarning:special.warning):CurrentPattern==Pattern.Storm?stormWarningDuration:CurrentAttack.warning):State==Phase.Gap?special.delay:State==Phase.Recovery?CurrentAttack.recovery:CurrentPattern==Pattern.SafeWedge?(gathering?special.gatherDuration:special.explosionDuration):CurrentAttack.duration;
  public Vector3 Ground=>transform.position+Vector3.up*.06f;
  void Awake(){health=GetComponent<CombatHealth>();sound=GetComponent<HarvestReaperAudio>();if(!sound)sound=gameObject.AddComponent<HarvestReaperAudio>();var agent=GetComponent<NavMeshAgent>();if(agent)agent.enabled=false;var body=GetComponent<Rigidbody>();if(body){body.isKinematic=true;body.constraints=RigidbodyConstraints.FreezeAll;}if(animator)animator.applyRootMotion=false;}
  void Start(){player=FindFirstObjectByType<PlayerMotor>();if(player){target=player.GetComponent<CombatHealth>();targetCollider=player.GetComponent<Collider>();}if(!room)room=GetComponentInParent<ForestRoom>();nextAttack=Time.time+initialDelay;Forward=transform.forward;Play("Idle",1);}
  public void BindRoom(ForestRoom value){room=value;}
  void Play(string clip,float duration)
  {
   if(!animator)return;
   // 새 전용 상태와 기존 상태 모두 같은 실제 패턴 시간으로 재생합니다.
   animator.SetFloat("PhaseSpeed",1);animator.speed=clip=="Idle"?1:1/Mathf.Max(.01f,duration);
   animator.CrossFadeInFixedTime(clip,.12f);
  }
  void Update()
  {
   if(!health||health.Dead||!player||!target||target.Dead){Clear();return;}
   bool inside=!room||room.CombatStarted;
   if(!inside){Clear();return;}
   if(!engaged){if(!room&&Vector3.Distance(player.transform.position,transform.position)>engageRange)return;engaged=true;nextAttack=Time.time+initialDelay;}
   if(CombatFeedback.Paused(this))return;
   TickPatches();
   Vector3 delta=player.transform.position-transform.position;delta.y=0;
   closeClock=delta.magnitude<=closeRange?closeClock+Time.deltaTime:0;
   if(State==Phase.Idle)
   {
    TurnToward(delta,Time.deltaTime);
    int pending=PendingSpecial();if(pending>=0){consumed[pending]=true;Begin(pending==1?Pattern.Storm:Pattern.SafeWedge);return;}
    if(Time.time>=nextAttack){var pick=Choose(delta.magnitude);if(pick>=0&&((pick!=0&&pick!=1)||FacingReady(delta)))Begin((Pattern)pick);}
    return;
   }
   if(OuterStormActive&&Time.time>=nextOuterTick)
   {
    nextOuterTick=Time.time+Mathf.Max(.05f,special.outerTickInterval);
    if(targetCollider&&InOuterStorm(targetCollider.bounds.center))Hit(special.outerDamage,0,0,transform.position);
   }
   elapsed+=Time.deltaTime;
   if(State==Phase.Warning){if(elapsed>=PhaseDuration)Enter(Phase.Active);return;}
   if(State==Phase.Gap){if(elapsed>=PhaseDuration){SafeYaw+=rotationSign*special.angleStep;Enter(Phase.Warning);}return;}
   if(State==Phase.Recovery){if(Progress>=recoveryTurnStart)TurnToward(delta,Time.deltaTime);if(elapsed>=PhaseDuration){State=Phase.Idle;nextAttack=Time.time+globalCooldown;Play("Idle",1);}return;}
   if(CurrentPattern==Pattern.Storm){if(Time.time>=nextTick){nextTick=Time.time+Mathf.Max(.05f,special.stormTick);TryAreaHit(storm.damage,storm.knockback,storm.lift);}}
   else if(CurrentPattern==Pattern.SafeWedge){if(!gathering&&!hit){hit=true;if(sound)sound.Impact();if(targetCollider&&InInnerDanger(targetCollider.bounds.center))Hit(special.damage,0,0,transform.position);}}
   else if(CurrentPattern!=Pattern.Pumpkins&&!hit&&Progress>=CurrentAttack.hitTime){hit=true;if(sound)sound.Impact();TryAreaHit(CurrentAttack.damage,CurrentAttack.knockback,CurrentAttack.lift);}
   if(elapsed>=PhaseDuration)
   {
    if(CurrentPattern==Pattern.SafeWedge&&gathering){gathering=false;Enter(Phase.Warning);}
    else if(CurrentPattern==Pattern.SafeWedge&&++round<special.repeats)Enter(Phase.Gap);
    else{if((int)CurrentPattern<4)ready[(int)CurrentPattern]=Time.time+CurrentAttack.cooldown;Enter(Phase.Recovery);}
   }
  }
  bool FacingReady(Vector3 direction)
  {
   return direction.sqrMagnitude<=minimumFacingDistance*minimumFacingDistance||Vector3.Angle(transform.forward,direction)<=attackFacingTolerance;
  }
  void TurnToward(Vector3 direction,float dt)
  {
   direction.y=0;
   if(dt<=0||direction.sqrMagnitude<=minimumFacingDistance*minimumFacingDistance){turnVelocity=0;return;}
   float current=transform.eulerAngles.y,targetYaw=Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg;
   if(Mathf.Abs(Mathf.DeltaAngle(current,targetYaw))<=turnDeadZone){turnVelocity=0;return;}
   float yaw=Mathf.SmoothDampAngle(current,targetYaw,ref turnVelocity,turnSmoothTime,Mathf.Max(0,turnSpeed),dt);
   transform.rotation=Quaternion.Euler(0,yaw,0);
  }
  int PendingSpecial(){float ratio=health.Current/Mathf.Max(1,health.maxHealth);if(!consumed[0]&&ratio<=special.firstHP)return 0;if(!consumed[1]&&ratio<=special.stormHP)return 1;if(!consumed[2]&&ratio<=special.lastHP)return 2;return -1;}
  int Choose(float distance)
  {
   if(closeClock>=closeDwell&&distance<=shockwave.range&&Time.time>=ready[2])return 2;
   choices.Clear();if(distance<=sweep.range&&Time.time>=ready[0])choices.Add(Pattern.Sweep);if(distance<=cleave.range&&Time.time>=ready[1])choices.Add(Pattern.Cleave);if(distance<=pumpkins.range&&Time.time>=ready[3])choices.Add(Pattern.Pumpkins);
   if(choices.Count>1)choices.Remove((Pattern)previous);return choices.Count==0?-1:(int)choices[UnityEngine.Random.Range(0,choices.Count)];
  }
  void Begin(Pattern pattern)
  {
   // 대시/슬로우가 아닌 기본 이동속도로 예고 시간을 고정해 중간에 경계 속도가 바뀌지 않습니다.
   if(pattern==Pattern.Storm)stormWarningDuration=player?storm.range/Mathf.Max(.1f,player.moveSpeed*stormWarningSpeedMultiplier):storm.warning;
   CurrentPattern=pattern;if((int)pattern<4)previous=(int)pattern;closeClock=0;
   // Lock the direction we smoothly reached; never snap to the target at attack start.
   Forward=transform.forward;turnVelocity=0;
   gathering=pattern==Pattern.SafeWedge;nextOuterTick=Time.time;
   if(pattern==Pattern.SafeWedge){round=0;rotationSign=UnityEngine.Random.value<.5f?-1:1;SafeYaw=Mathf.Atan2(Forward.x,Forward.z)*Mathf.Rad2Deg;}
   Enter(Phase.Warning);
  }
  void Enter(Phase phase)
  {
   State=phase;elapsed=0;hit=false;nextTick=Time.time;if(sound)sound.Enter(phase);
   int sector=((Mathf.RoundToInt(Mathf.DeltaAngle(transform.eulerAngles.y,SafeYaw)/45f)%8)+8)%8;
   if(phase==Phase.Gap){Play("WedgeHold"+sector,special.delay);return;}
   if(phase==Phase.Recovery)
   {
    // 마지막으로 가리킨 방향에서 새 대기 자세로 복귀합니다.
    if(CurrentPattern==Pattern.SafeWedge){Play("WedgeRecovery"+sector,CurrentAttack.recovery);return;}
    string recovery=CurrentPattern==Pattern.Sweep?"Sweep":CurrentPattern==Pattern.Cleave?"Cleave":CurrentPattern==Pattern.Shockwave?"Slam":CurrentPattern==Pattern.Pumpkins?"Summon":CurrentPattern==Pattern.SafeWedge?"Gather":"Special";
    Play(recovery+"Recovery",CurrentAttack.recovery);return;
   }
   if(CurrentPattern==Pattern.SafeWedge)
   {
    // 실제 안전 부채꼴과 같은 8방향 손짓을 사용하고, 폭발 직전 손을 내립니다.
    Play(gathering?(phase==Phase.Warning?"GatherReady":"GatherAttack"):(phase==Phase.Warning?"WedgeReady":"WedgeAttack")+sector,PhaseDuration);return;
   }
   string prefix=CurrentPattern==Pattern.Sweep?"Sweep":CurrentPattern==Pattern.Cleave?"Cleave":CurrentPattern==Pattern.Shockwave?"Slam":CurrentPattern==Pattern.Pumpkins?"Summon":"Special";
   Play(prefix+(phase==Phase.Warning?"Ready":"Attack"),PhaseDuration);
   if(phase==Phase.Active&&CurrentPattern==Pattern.Pumpkins)SpawnPumpkins();
  }
  public bool Contains(Vector3 world)
  {
   Vector3 d=world-transform.position;if(Mathf.Abs(d.y)>heightTolerance)return false;d.y=0;float r=d.magnitude;
   if(CurrentPattern==Pattern.SafeWedge)return InOuterStorm(world)||(!gathering&&InInnerDanger(world));
   if(CurrentPattern==Pattern.Cleave){float along=Vector3.Dot(d,Forward);return along>=0&&along<=cleave.range&&Mathf.Abs(Vector3.Dot(d,Vector3.Cross(Vector3.up,Forward)))<=cleave.width*.5f;}
   if(CurrentPattern==Pattern.Sweep)return r<=sweep.range&&Vector3.Angle(Forward,d)<=sweep.angle*.5f;
   return r<=CurrentAttack.range;
  }
  public bool InOuterStorm(Vector3 world)
  {
   Vector3 d=world-transform.position;if(Mathf.Abs(d.y)>heightTolerance)return false;d.y=0;
   return d.magnitude>special.innerRadius&&d.magnitude<=special.outerRadius;
  }
  public bool InInnerDanger(Vector3 world)
  {
   Vector3 d=world-transform.position;if(Mathf.Abs(d.y)>heightTolerance)return false;d.y=0;
   return d.magnitude<=special.innerRadius&&Mathf.Abs(Mathf.DeltaAngle(SafeYaw,Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg))>special.safeAngle*.5f;
  }
  void TryAreaHit(float damage,float push,float lift){if(targetCollider&&Contains(targetCollider.bounds.center))Hit(damage,push,lift,transform.position);}
  void Hit(float damage,float push,float lift,Vector3 origin)
  {
   if(!target||target.Dead)return;Vector3 direction=player.transform.position-origin;
   if(CombatFeedback.DealDamage(target,damage,targetCollider?targetCollider.bounds.center:player.transform.position,direction,1,transform,new Color(.9f,.45f,.13f),true)&&!target.Dead)player.ApplyKnockback(direction,push,lift,knockbackDuration);
  }
  bool Place(Vector3 point,out Vector3 result)
  {
   result=point;NavMeshHit nav;if(!NavMesh.SamplePosition(point,out nav,navSampleRadius,NavMesh.AllAreas))return false;result=nav.position;
   if(room&&(result.x<room.entryX+entryInset||result.x>room.exitX-entryInset))return false;
   RaycastHit floor;if(Physics.Raycast(result+Vector3.up*groundProbeHeight,Vector3.down,out floor,groundProbeDistance,groundMask,QueryTriggerInteraction.Ignore))result=floor.point;
   return true;
  }
  void SpawnPumpkins()
  {
   var made=new List<Vector3>();Vector3 first;if(!Place(player.transform.position,out first))first=new Vector3(player.transform.position.x,transform.position.y,player.transform.position.z);
   if(sound)sound.PumpkinWarning(first);
   made.Add(first);patches.Add(new Patch{point=first,born=Time.time});
   for(int i=1;i<pumpkin.count;i++)for(int attempt=0;attempt<pumpkin.placementAttempts;attempt++)
   {
    Vector2 v=UnityEngine.Random.insideUnitCircle*pumpkin.spawnRadius;Vector3 p;if(!Place(transform.position+new Vector3(v.x,0,v.y),out p))continue;
    bool valid=true;foreach(var other in patches)if(Vector3.Distance(p,other.point)<pumpkin.minSpacing){valid=false;break;}if(!valid)continue;
    made.Add(p);patches.Add(new Patch{point=p,born=Time.time});break;
   }
  }
  void TickPatches()
  {
   for(int i=patches.Count-1;i>=0;i--)
   {
    var p=patches[i];float age=Time.time-p.born;
    if(age<pumpkin.explosionWarning)continue;
    float distance=Vector3.ProjectOnPlane(player.transform.position-p.point,Vector3.up).magnitude;
    bool sameHeight=Mathf.Abs(player.transform.position.y-p.point.y)<=heightTolerance;
    if(!p.exploded){p.exploded=true;if(sound)sound.PumpkinExplosion(p.point);p.nextTick=Time.time+Mathf.Max(.05f,pumpkin.tickInterval);if(sameHeight&&distance<=pumpkin.explosionRadius)Hit(pumpkin.explosionDamage,pumpkins.knockback,pumpkins.lift,p.point);}
    if(age>=pumpkin.explosionWarning+pumpkin.poolDuration){patches.RemoveAt(i);continue;}
    if(Time.time>=p.nextTick){p.nextTick=Time.time+Mathf.Max(.05f,pumpkin.tickInterval);if(sameHeight&&distance<=pumpkin.poolRadius)Hit(pumpkin.poolDamage,0,0,p.point);}
   }
  }
  void Clear(){if(sound)sound.Stop();turnVelocity=0;gathering=false;patches.Clear();if(State!=Phase.Idle){State=Phase.Idle;Play("Idle",1);}engaged=false;}
  void OnDisable(){Clear();}
 }
}
