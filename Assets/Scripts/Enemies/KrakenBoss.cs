using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
namespace DogLife
{
 [RequireComponent(typeof(CombatHealth))]
 public sealed class KrakenBoss:MonoBehaviour
 {
  public enum Pattern{Slam,Cross,Capture,Wave,Fury}
  public enum Phase{Idle,Warning,Active,Ground,Recovery,Gap,Groggy}
  [Serializable] public class Attack
  {
   [SerializeField,Min(0)] public float damage=1,length=11,width=3,radius=3,warning=1.7f,duration=.65f,groundDuration=2.2f,recovery=1.4f,cooldown=7,knockback=4,lift=1;
  }
  [Serializable] public class CrossSettings
  {
   [SerializeField,Min(1)] public int count=4;
   [SerializeField,Min(0)] public float spawnRadius=10,delay=.25f;
   [SerializeField] public bool simultaneous=true;
  }
  [Serializable] public class CaptureSettings
  {
   [SerializeField] public bool bindEnabled=true;
   [SerializeField,Min(0)] public float bindDuration=1.1f,throwSpeed=14,throwLift=7,throwDuration=.7f,throwAnimation=.6f;
  }
  [Serializable] public class WaveSettings
  {
   [SerializeField,Min(.1f)] public float speed=10,width=22,travelDistance=25,depth=1.8f,height=4,safeGapWidth=4;
   [SerializeField] public float safeGapOffset=0;
  }
  [Serializable] public class FurySettings
  {
   [SerializeField,Range(0,1)] public float triggerHP=.5f;
   [SerializeField,Min(1)] public int waveCount=3,tentaclesPerWave=2;
   [SerializeField,Min(0)] public float radius=3,damage=1,warning=1.6f,delayBetweenWaves=.8f,tentacleDelay=.22f,spawnRadius=12,minSpacing=5,finalDamage=2,finalRadius=5,finalWarning=2.2f,groggyDuration=6,roarDuration=1.8f;
  }
  [Header("References")][SerializeField] private Animator bodyAnimator;
  [SerializeField] private KrakenTentacle tentaclePrefab;
  [SerializeField] private Animator[] restingTentacles;
  [SerializeField] private KrakenWave wavePrefab;
  [Header("Encounter")][SerializeField,Min(0)] private float engageRange=45,entryInset=3,initialDelay=2,globalCooldown=2,turnSpeed=60,turnSmoothTime=.5f,spawnSampleRadius=3,arenaMargin=2,hitHeight=3,waveArmSpacing=3,waveArmLength=6,waveArmWidth=2,knockbackDuration=.45f;
  [Header("Normal patterns")][SerializeField] private Attack slam=new Attack();
  [SerializeField] private Attack cross=new Attack{length=10,width=2.7f,cooldown=10};
  [SerializeField] private CrossSettings surrounding=new CrossSettings();
  [SerializeField] private Attack capture=new Attack{warning=1.8f,cooldown=12,groundDuration=2};
  [SerializeField] private CaptureSettings binding=new CaptureSettings();
  [SerializeField] private Attack wave=new Attack{warning=2,cooldown=10,knockback=13,lift=2};
  [SerializeField] private WaveSettings tide=new WaveSettings();
  [Header("Deep sea fury")][SerializeField] private FurySettings fury=new FurySettings();
  [SerializeField,Min(1)] private int placementAttempts=32;
  readonly List<KrakenTentacle> tentacles=new List<KrakenTentacle>();
  readonly HashSet<CombatHealth> struck=new HashSet<CombatHealth>();readonly List<int> choices=new List<int>();
  readonly float[] cooldowns=new float[4];
  CombatHealth health,target;PlayerMotor player;ForestRoom room;KrakenWave currentWave;
  float elapsed,nextAttack,yawVelocity,boundUntil;bool engaged,specialUsed,launched,bound;int previous=-1,round;KrakenTentacle bindingTentacle;
  public Pattern CurrentPattern{get;private set;} public Phase State{get;private set;}
  public bool Engaged=>engaged;public bool SpecialUsed=>specialUsed;public int FuryRound=>round;
  public Vector3 Forward{get;private set;}=Vector3.forward;
  public float Progress=>Mathf.Clamp01(elapsed/Mathf.Max(.01f,Duration));
  public IReadOnlyList<KrakenTentacle> Tentacles=>tentacles;
  public KrakenWave CurrentWave=>currentWave;
  public WaveSettings Tide=>tide;public Attack CurrentAttack=>CurrentPattern==Pattern.Slam?slam:CurrentPattern==Pattern.Cross?cross:CurrentPattern==Pattern.Capture?capture:wave;
  public float Duration=>State==Phase.Warning?(CurrentPattern==Pattern.Fury?(round<0?fury.roarDuration:round>=fury.waveCount?fury.finalWarning:fury.warning):CurrentAttack.warning):State==Phase.Recovery?CurrentAttack.recovery:State==Phase.Gap?fury.delayBetweenWaves:State==Phase.Groggy?fury.groggyDuration:CurrentAttack.duration;
  void Awake(){health=GetComponent<CombatHealth>();var nav=GetComponent<NavMeshAgent>();if(nav)nav.enabled=false;var rb=GetComponent<Rigidbody>();if(rb){rb.isKinematic=true;rb.constraints=RigidbodyConstraints.FreezeAll;}}
  void Start(){player=FindFirstObjectByType<PlayerMotor>();if(player)target=player.GetComponent<CombatHealth>();if(!room)room=GetComponentInParent<ForestRoom>();nextAttack=Time.time+initialDelay;Forward=transform.forward;Play("Idle",1);}
  public void BindRoom(ForestRoom value){room=value;}
  void Play(string state,float duration){if(!bodyAnimator)return;bodyAnimator.SetFloat("PhaseSpeed",state=="Idle"?1:1/Mathf.Max(.01f,duration));bodyAnimator.CrossFadeInFixedTime(state,.15f);}
  void Enter(Phase state){State=state;elapsed=0;}
  bool Inside()=>!room||room.CombatStarted;
  void Update()
  {
   if(!health||health.Dead||!player||!target||target.Dead||!Inside()){Clear();return;}
   if(!engaged){if(!room&&Vector3.Distance(player.transform.position,transform.position)>engageRange)return;engaged=true;nextAttack=Time.time+initialDelay;}
   if(CombatFeedback.Paused(this))return;
   if(bound&&Time.time>=boundUntil){bound=false;if(bindingTentacle)bindingTentacle.Throw(binding.throwAnimation);player.ApplyKnockback(player.transform.position-transform.position,binding.throwSpeed,binding.throwLift,binding.throwDuration);}
   elapsed+=Time.deltaTime;
   if(State==Phase.Idle)
   {
    Vector3 d=player.transform.position-transform.position;d.y=0;
    if(d.sqrMagnitude>1){float y=Mathf.SmoothDampAngle(transform.eulerAngles.y,Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg,ref yawVelocity,turnSmoothTime,turnSpeed);transform.rotation=Quaternion.Euler(0,y,0);}
    if(!specialUsed&&health.Current/Mathf.Max(1,health.maxHealth)<=fury.triggerHP){specialUsed=true;Begin(Pattern.Fury);return;}
    if(Time.time>=nextAttack){int pick=Choose();if(pick>=0)Begin((Pattern)pick);}return;
   }
   if(State==Phase.Warning&&elapsed>=Duration)
   {
    if(CurrentPattern==Pattern.Fury&&round<0){round=0;PrepareFury();return;}
    Enter(Phase.Active);launched=true;Play(CurrentPattern==Pattern.Wave?"Wave":"Command",CurrentAttack.duration);
    if(CurrentPattern==Pattern.Wave){LaunchWave();foreach(var t in tentacles)if(t.gameObject.activeSelf)t.Activate();}
    else foreach(var t in tentacles)if(t.gameObject.activeSelf)t.Activate();
   }
   if(State==Phase.Active)
   {
    bool busy=false;foreach(var t in tentacles)if(t.gameObject.activeSelf&&t.Busy)busy=true;
    if(currentWave&&currentWave.Busy)busy=true;
    if(!busy&&!bound&&launched)
    {
     if(CurrentPattern==Pattern.Fury){if(round>=fury.waveCount){Enter(Phase.Groggy);Play("Groggy",fury.groggyDuration);}else{round++;Enter(Phase.Gap);}}
     else{cooldowns[(int)CurrentPattern]=Time.time+CurrentAttack.cooldown;Enter(Phase.Recovery);Play("Idle",1);}
    }
   }
   if(State==Phase.Gap&&elapsed>=Duration)PrepareFury();
   if((State==Phase.Recovery||State==Phase.Groggy)&&elapsed>=Duration){Enter(Phase.Idle);nextAttack=Time.time+globalCooldown;Play("Idle",1);if(restingTentacles!=null)foreach(var a in restingTentacles)if(a)a.CrossFadeInFixedTime("Idle",.2f);}
  }
  int Choose(){choices.Clear();for(int i=0;i<4;i++)if(Time.time>=cooldowns[i])choices.Add(i);if(choices.Count>1)choices.Remove(previous);return choices.Count==0?-1:choices[UnityEngine.Random.Range(0,choices.Count)];}
  public void Begin(Pattern pattern)
  {
   if(restingTentacles!=null)foreach(var a in restingTentacles)if(a){a.SetFloat("PhaseSpeed",1);a.CrossFadeInFixedTime(pattern==Pattern.Fury?"Dive":"Ready",.2f);}
   CurrentPattern=pattern;if((int)pattern<4)previous=(int)pattern;Forward=transform.forward;yawVelocity=0;struck.Clear();launched=false;
   foreach(var t in tentacles)t.ResetHazard();
   if(pattern==Pattern.Fury){round=-1;Enter(Phase.Warning);Play("Roar",fury.roarDuration);return;}
   Vector3 aim=player.transform.position;aim.y=transform.position.y;
   if(pattern==Pattern.Slam)
   {
    Vector3 dir=(aim-transform.position).normalized;if(dir.sqrMagnitude<.01f)dir=Forward;Forward=dir;
    Configure(0,transform.position,dir,slam.length,slam.width,false,slam,0);
   }
   if(pattern==Pattern.Cross)
   {
    for(int i=0;i<Mathf.Max(1,surrounding.count);i++)
    {
     Vector3 dir=Quaternion.Euler(0,360f*i/Mathf.Max(1,surrounding.count),0)*Forward;
     Vector3 origin=ValidPoint(aim+dir*surrounding.spawnRadius);
     Vector3 toward=aim-origin;toward.y=0;float length=Mathf.Min(cross.length,toward.magnitude);
     Configure(i,origin,toward.sqrMagnitude>.01f?toward.normalized:-dir,length,cross.width,false,cross,surrounding.simultaneous?0:i*surrounding.delay);
    }
   }
   if(pattern==Pattern.Capture)Configure(0,ValidPoint(aim),Forward,capture.radius,capture.radius,true,capture,0);
   if(pattern==Pattern.Wave)
   {
    for(int i=0;i<2;i++)Configure(i,transform.position+Vector3.Cross(Vector3.up,Forward)*(i==0?-waveArmSpacing:waveArmSpacing),Forward,waveArmLength,waveArmWidth,false,wave,0,true);
   }
   Enter(Phase.Warning);Play("Command",Duration);
  }
  KrakenTentacle Get(int index)
  {
   while(tentacles.Count<=index){var t=Instantiate(tentaclePrefab,transform);t.name="Tentacle_"+tentacles.Count;t.Owner=this;tentacles.Add(t);}return tentacles[index];
  }
  void Configure(int index,Vector3 point,Vector3 direction,float length,float width,bool circle,Attack cfg,float delay,bool cosmetic=false)
  {
   var t=Get(index);float hold=CurrentPattern==Pattern.Capture?Mathf.Max(cfg.groundDuration,binding.bindDuration+binding.throwAnimation):cfg.groundDuration;
   t.Configure(this,point,direction,length,width,circle,cfg.warning,cfg.duration,hold,cfg.recovery,delay,cfg.damage,cfg.knockback,cfg.lift,hitHeight,cosmetic);
  }
  Vector3 ValidPoint(Vector3 point)
  {
   point.y=transform.position.y;if(room)point.x=Mathf.Clamp(point.x,room.entryX+entryInset+arenaMargin,room.exitX-arenaMargin);
   NavMeshHit nav;if(NavMesh.SamplePosition(point,out nav,spawnSampleRadius,NavMesh.AllAreas))return nav.position;
   if(player&&NavMesh.SamplePosition(player.transform.position,out nav,spawnSampleRadius,NavMesh.AllAreas))return nav.position;return transform.position;
  }
  void PrepareFury()
  {
   struck.Clear();launched=false;foreach(var t in tentacles)t.ResetHazard();
   bool final=round>=fury.waveCount;int count=final?1:Mathf.Max(1,fury.tentaclesPerWave);
   var placed=new List<Vector3>();Vector3 center=player.transform.position;
   for(int i=0;i<count;i++)
   {
    Vector3 point=ValidPoint(center);
    if(i>0){bool found=false;for(int attempt=0;attempt<placementAttempts;attempt++){Vector2 r=UnityEngine.Random.insideUnitCircle*fury.spawnRadius;point=ValidPoint(center+new Vector3(r.x,0,r.y));found=true;foreach(var p in placed)if(Vector3.Distance(p,point)<fury.minSpacing){found=false;break;}if(found)break;}if(!found)continue;}
    placed.Add(point);var t=Get(i);float r0=final?fury.finalRadius:fury.radius;
    t.Configure(this,point,Forward,r0,r0,true,final?fury.finalWarning:fury.warning,slam.duration,slam.groundDuration,slam.recovery,i*fury.tentacleDelay,final?fury.finalDamage:fury.damage,slam.knockback,slam.lift,hitHeight,false);
   }
   Enter(Phase.Warning);Play("Roar",Duration);
  }
  void LaunchWave(){if(!currentWave)currentWave=Instantiate(wavePrefab,transform);currentWave.Launch(this,transform.position,Forward,tide,wave.damage,wave.knockback,wave.lift);}
  public bool HitPlayer(Collider collider,float damage,float push,float lift,KrakenTentacle tentacle=null)
  {
   if(!engaged||!isActiveAndEnabled||health.Dead)return false;var h=collider.GetComponentInParent<CombatHealth>();if(!h||h!=target||struck.Contains(h))return false;
   // A group of simultaneous tentacles / both wave halves count as one attack.
   struck.Add(h);Vector3 dir=player.transform.position-transform.position;
   bool hit=CombatFeedback.DealDamage(h,damage,collider.bounds.center,dir,1,transform,new Color(.15f,.7f,1),true);
   if(!hit)return false;
   if(CurrentPattern==Pattern.Capture&&binding.bindEnabled&&!h.Dead){bound=true;boundUntil=Time.time+binding.bindDuration;bindingTentacle=tentacle;player.ApplyKnockback(Vector3.zero,0,0,binding.bindDuration);if(tentacle)tentacle.Bind(binding.bindDuration);}
   else player.ApplyKnockback(dir,push,lift,knockbackDuration);
   return true;
  }
  public bool CanDamage=>engaged&&isActiveAndEnabled&&health&&!health.Dead;
  void Clear()
  {
   if(bound&&player){player.ApplyKnockback(Vector3.zero,0,0,.02f);bound=false;}
   foreach(var t in tentacles)if(t)t.ResetHazard();if(currentWave)currentWave.Stop();State=Phase.Idle;engaged=false;launched=false;
  }
  void OnDisable(){Clear();}
 }
}
