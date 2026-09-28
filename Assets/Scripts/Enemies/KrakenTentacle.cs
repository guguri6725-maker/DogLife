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
  public KrakenBoss Owner{get;set;}
  public enum Mode{Hidden,Warning,Delay,Strike,Ground,Retract}
  public Mode State{get;private set;}
  public bool Busy=>State!=Mode.Hidden;
  public bool Cosmetic=>cosmetic;
  public bool Circle{get;private set;}public float Length{get;private set;}public float Width{get;private set;}
  public float Progress=>Mathf.Clamp01(clock/Mathf.Max(.01f,State==Mode.Warning?warning:State==Mode.Strike?duration:State==Mode.Ground?hold:retractTime));
  public float WarningFill=>State==Mode.Warning?Mathf.Clamp01(clock/Mathf.Max(.01f,warning+delay+duration*impactTime)):State==Mode.Delay?Mathf.Clamp01((warning+clock)/Mathf.Max(.01f,warning+delay+duration*impactTime)):State==Mode.Strike?Mathf.Clamp01((warning+delay+clock)/Mathf.Max(.01f,warning+delay+duration*impactTime)):1;
  public bool BeforeImpact=>State==Mode.Warning||State==Mode.Delay||(State==Mode.Strike&&Progress<impactTime);
  public Vector3 Origin=>transform.position;public Vector3 Forward=>transform.forward;
  float clock,warning,duration,hold,delay,damage,push,lift;bool cosmetic,activated;readonly Collider[] contacts=new Collider[32];
  public void Configure(KrakenBoss owner,Vector3 p,Vector3 forward,float length,float width,bool circle,float warningTime,float attackDuration,float groundDuration,float recovery,float offset,float amount,float knockback,float launch,float height,bool displayOnly)
  {
   Owner=owner;gameObject.SetActive(true);transform.position=p;transform.rotation=Quaternion.LookRotation(forward);transform.localScale=Vector3.one;
   Length=Mathf.Max(.1f,length);Width=Mathf.Max(.1f,width);Circle=circle;warning=warningTime;duration=Mathf.Max(.05f,attackDuration);hold=groundDuration;delay=offset;damage=amount;push=knockback;lift=launch;cosmetic=displayOnly;activated=false;clock=0;State=Mode.Warning;
   SetColliders(false,false);
   attackBox.center=hitBox.center=new Vector3(0,height*.5f,Length*.5f);attackBox.size=hitBox.size=new Vector3(Width,height,Length);
   attackSphere.center=hitSphere.center=Vector3.up*height*.35f;attackSphere.radius=hitSphere.radius=Length;
   if(visual){float scale=circle?Mathf.Max(.6f,length/3f):Length/authoredLength;visual.localScale=circle?Vector3.one*scale:new Vector3(Width/authoredWidth,scale,groundThickness/authoredDepth);visual.localPosition=Vector3.zero;visual.localRotation=Quaternion.identity;}
   Play(circle?"Dive":"Ready",warning);
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
   // The impact animation reaches its striking pose at 30%; damage follows the same frame.
   if(State==Mode.Strike&&clock>=duration*impactTime){SetColliders(!cosmetic,false);CheckOverlaps();}
   if(State==Mode.Strike&&clock>=duration){State=Mode.Ground;clock=0;SetColliders(false,!cosmetic);Play(Circle?"Bind":"Ground",hold);}
   if(State==Mode.Ground&&clock>=hold){State=Mode.Retract;clock=0;SetColliders(false,false);Play("Retract",retractTime);}
   if(visual&&!Circle)
   {
    float lean=State==Mode.Strike?Mathf.SmoothStep(0,1,Mathf.Clamp01(Progress/Mathf.Max(.01f,impactTime))):State==Mode.Ground?1:State==Mode.Retract?1-Mathf.SmoothStep(0,1,Progress):0;
    visual.localRotation=Quaternion.Euler(slamLean*lean,0,0);
   }
   if(State==Mode.Retract){if(visual)visual.localPosition=Vector3.down*authoredLength*Progress*visual.localScale.y;if(clock>=retractTime)ResetHazard();}
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
  void OnDisable(){if(attackBox)SetColliders(false,false);State=Mode.Hidden;}
 }
}
