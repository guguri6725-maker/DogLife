using UnityEngine;
namespace DogLife
{
 // HitCollider is a solid query target under the boss's single CombatHealth.
 // AttackCollider is an independent trigger and never receives player weapon hits.
 public sealed class KrakenTentacle:MonoBehaviour
 {
  [SerializeField] private Animator animator;
  [SerializeField] private Transform visual;
  [SerializeField] private BoxCollider attackBox,hitBox;
  [SerializeField] private SphereCollider attackSphere,hitSphere;
  [SerializeField,Min(.1f)] private float authoredLength=8,retractTime=.65f;
  [SerializeField] private float slamLean=78,authoredWidth=5.7f,authoredDepth=6,groundThickness=1.5f;
  [SerializeField,Range(0,1)] private float impactTime=.3f;
  [Header("전진과 내려찍기 동기화")]
  [SerializeField,Range(0,45)] private float windupLean=18;
  [SerializeField,Min(0)] private float windupBackstep=1.6f,windupHeight=.65f;
  [SerializeField,Range(-5,5)] private float windupJointBend=-1.4f,impactJointBend=1.05f;
  readonly System.Collections.Generic.List<Transform> swingBones=new System.Collections.Generic.List<Transform>();
  readonly System.Collections.Generic.List<Quaternion> swingRest=new System.Collections.Generic.List<Quaternion>();
  void Awake()
  {
   if(!visual)return;
   foreach(var bone in visual.GetComponentsInChildren<Transform>(true))
    if(bone.name.StartsWith("Tendril")&&int.TryParse(bone.name.Substring(7),out _)){swingBones.Add(bone);swingRest.Add(bone.localRotation);}
  }
  void RestoreSwingBones(){for(int i=0;i<swingBones.Count;i++)if(swingBones[i])swingBones[i].localRotation=swingRest[i];}
  public KrakenBoss Owner{get;set;}
  public enum Mode{Hidden,Warning,Delay,Strike,Ground,Retract}
  public Mode State{get;private set;}
  public bool Busy=>State!=Mode.Hidden;
  public bool Cosmetic=>cosmetic;
  public bool Circle{get;private set;}public float Length{get;private set;}public float Width{get;private set;}
  public float Progress=>Mathf.Clamp01(clock/Mathf.Max(.01f,State==Mode.Warning?warning:State==Mode.Strike?duration:State==Mode.Ground?hold:retractTime));
  public float WarningFill=>State==Mode.Warning?Mathf.Clamp01(clock/Mathf.Max(.01f,warning+delay+duration*impactTime)):State==Mode.Delay?Mathf.Clamp01((warning+clock)/Mathf.Max(.01f,warning+delay+duration*impactTime)):State==Mode.Strike?Mathf.Clamp01((warning+delay+clock)/Mathf.Max(.01f,warning+delay+duration*impactTime)):1;
  public bool BeforeImpact=>State==Mode.Warning||State==Mode.Delay||(State==Mode.Strike&&Progress<impactTime);
  // VFX도 실제 판정 시작 시점을 공유하여 선딜에 충격이 먼저 보이지 않게 한다.
  public float ImpactProgress=>BeforeImpact?-1:Mathf.Clamp01((Progress-impactTime)/Mathf.Max(.01f,1-impactTime));
  public Vector3 Origin=>transform.position;public Vector3 Forward=>transform.forward;
  float clock,warning,duration,hold,delay,damage,push,lift;bool cosmetic,activated;readonly Collider[] contacts=new Collider[32];
  public void Configure(KrakenBoss owner,Vector3 p,Vector3 forward,float length,float width,bool circle,float warningTime,float attackDuration,float groundDuration,float recovery,float offset,float amount,float knockback,float launch,float height,bool displayOnly)
  {
   Owner=owner;gameObject.SetActive(true);transform.position=p;transform.rotation=Quaternion.LookRotation(forward);
   RestoreSwingBones();
   // 보스 본체 확대와 별개로 경고, 촉수, 판정은 월드 단위로 일치시킨다.
   Vector3 parentScale=transform.parent?transform.parent.lossyScale:Vector3.one;
   transform.localScale=new Vector3(1/Mathf.Max(.001f,Mathf.Abs(parentScale.x)),1/Mathf.Max(.001f,Mathf.Abs(parentScale.y)),1/Mathf.Max(.001f,Mathf.Abs(parentScale.z)));
   Length=Mathf.Max(.1f,length);Width=Mathf.Max(.1f,width);Circle=circle;warning=warningTime;duration=Mathf.Max(.05f,attackDuration);hold=groundDuration;delay=offset;damage=amount;push=knockback;lift=launch;cosmetic=displayOnly;activated=false;clock=0;State=Mode.Warning;
   SetColliders(false,false);
   attackBox.center=hitBox.center=new Vector3(0,height*.5f,Length*.5f);attackBox.size=hitBox.size=new Vector3(Width,height,Length);
   attackSphere.center=hitSphere.center=Vector3.up*height*.35f;attackSphere.radius=hitSphere.radius=Length;
   if(visual){float scale=circle?Mathf.Max(.6f,length/3f):Length/authoredLength;visual.localScale=circle?Vector3.one*scale:new Vector3(Width/authoredWidth,scale,groundThickness/authoredDepth);visual.localPosition=Vector3.zero;visual.localRotation=Quaternion.identity;}
   Play(circle?"Dive":"Ready",warning);
   if(!circle)ApplySlamPose(0,0);
   // Exposed flesh should be attackable without blocking player locomotion.
   var motor=FindFirstObjectByType<PlayerMotor>();if(motor)foreach(var c in motor.GetComponents<Collider>()){Physics.IgnoreCollision(hitBox,c,true);Physics.IgnoreCollision(hitSphere,c,true);}
  }
  void Play(string state,float seconds){if(!animator)return;animator.SetFloat("PhaseSpeed",state=="Idle"?1:1/Mathf.Max(.01f,seconds));animator.CrossFadeInFixedTime(state,.08f);}
  void SetColliders(bool attack,bool receive){attackBox.enabled=attack&&!Circle;attackSphere.enabled=attack&&Circle;hitBox.enabled=receive&&!Circle;hitSphere.enabled=receive&&Circle;}
  public void Activate(){activated=true;State=Mode.Delay;clock=0;}
  void Update()
  {
   if(State==Mode.Hidden)return;if(!Owner||!Owner.CanDamage){ResetHazard();return;}if(CombatFeedback.Paused(Owner))return;clock+=Time.deltaTime;
   if(State==Mode.Delay&&activated&&clock>=delay){State=Mode.Strike;clock=0;Play(Circle?"Emerge":"Strike",duration);}
   // 이동, 내려찍기, 물보라가 공유하는 충돌 시점에만 판정을 연다.
   if(State==Mode.Strike&&clock>=duration*impactTime){SetColliders(!cosmetic,false);CheckOverlaps();}
   if(State==Mode.Strike&&clock>=duration){State=Mode.Ground;clock=0;SetColliders(false,!cosmetic);Play(Circle?"Bind":"Ground",hold);}
   if(State==Mode.Ground&&clock>=hold){State=Mode.Retract;clock=0;SetColliders(false,false);Play("Retract",retractTime);}
   if(State==Mode.Retract){if(visual&&Circle)visual.localPosition=Vector3.down*authoredLength*Progress*visual.localScale.y;if(clock>=retractTime)ResetHazard();}
  }
  void ApplySlamPose(float fall,float retract)
  {
   if(!visual)return;
   // 전진 후 회전하는 두 동작으로 나뉘지 않도록 같은 곡선으로 동시에 이동한다.
   visual.localRotation=Quaternion.Euler(Mathf.Lerp(windupLean,slamLean,fall),0,0);
   visual.localPosition=Vector3.back*windupBackstep*(1-fall)+Vector3.up*windupHeight*(1-fall)
    -Vector3.up*authoredLength*retract*visual.localScale.y;
  }
  void LateUpdate()
  {
   if(Circle||State==Mode.Hidden||!Owner||CombatFeedback.Paused(Owner))return;
   float q=State==Mode.Strike?Mathf.Clamp01(Progress/Mathf.Max(.01f,impactTime)):State==Mode.Ground?1:State==Mode.Retract?1-Progress:0;
   float fall=Mathf.SmoothStep(0,1,q);
   // 전체 회전에 원본의 90도 굽힘을 또 더하지 않는다. 작은 곡률로 한 번만 내려친다.
   float bend=Mathf.Lerp(windupJointBend,impactJointBend,fall);
   for(int i=0;i<swingBones.Count;i++)swingBones[i].localRotation=swingRest[i]*Quaternion.Euler(bend,0,0);
   ApplySlamPose(fall,State==Mode.Retract?Progress:0);
  }
  void CheckOverlaps()
  {
   int n=Circle?Physics.OverlapSphereNonAlloc(attackSphere.transform.TransformPoint(attackSphere.center),attackSphere.radius,contacts,~0,QueryTriggerInteraction.Ignore):Physics.OverlapBoxNonAlloc(attackBox.transform.TransformPoint(attackBox.center),attackBox.size*.5f,contacts,transform.rotation,~0,QueryTriggerInteraction.Ignore);
   for(int i=0;i<n;i++)Touch(contacts[i]);
  }
  public void Touch(Collider other){if(State==Mode.Strike&&(attackBox.enabled||attackSphere.enabled)&&Owner)Owner.HitPlayer(other,damage,push,lift,this);}
  public void Bind(float time){Play("Bind",time);}
  public void Throw(float time){Play("Throw",time);}
  public void ResetHazard(){SetColliders(false,false);State=Mode.Hidden;activated=false;gameObject.SetActive(false);}
  void OnDisable(){RestoreSwingBones();if(attackBox)SetColliders(false,false);State=Mode.Hidden;}
 }
}
