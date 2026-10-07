using UnityEngine;
namespace DogLife
{
 public sealed class KrakenWave:MonoBehaviour
 {
  [SerializeField] private BoxCollider left,right;
  KrakenBoss owner;float speed,distance,travel,damage,push,lift,width,gap,offset,height,depth;
  readonly Collider[] contacts=new Collider[32];
  public bool Busy{get;private set;} public float Width=>width;public float Gap=>gap;public float Offset=>offset;public float Height=>height;
  public void Launch(KrakenBoss boss,Vector3 origin,Vector3 forward,KrakenBoss.WaveSettings settings,float amount,float knockback,float launch)
  {
   owner=boss;gameObject.SetActive(true);transform.position=origin;transform.rotation=Quaternion.LookRotation(forward);travel=0;speed=settings.speed;distance=settings.travelDistance;damage=amount;push=knockback;lift=launch;width=settings.width;gap=Mathf.Clamp(settings.safeGapWidth,0,width);offset=Mathf.Clamp(settings.safeGapOffset,-width*.5f+gap*.5f,width*.5f-gap*.5f);height=settings.height;depth=settings.depth;Busy=true;
   // 확대된 보스 아래에서도 파도의 안전 통로와 실제 콜라이더 크기를 유지한다.
   Vector3 parentScale=transform.parent?transform.parent.lossyScale:Vector3.one;
   transform.localScale=new Vector3(1/Mathf.Max(.001f,Mathf.Abs(parentScale.x)),1/Mathf.Max(.001f,Mathf.Abs(parentScale.y)),1/Mathf.Max(.001f,Mathf.Abs(parentScale.z)));
   Setup(left,-width*.5f,offset-gap*.5f);Setup(right,offset+gap*.5f,width*.5f);
  }
  void Setup(BoxCollider c,float a,float b){c.center=new Vector3((a+b)*.5f,height*.5f,0);c.size=new Vector3(Mathf.Max(.001f,b-a),height,depth);c.enabled=b-a>.001f;}
  void FixedUpdate()
  {
   if(!Busy)return;if(!owner||!owner.CanDamage){Stop();return;}if(CombatFeedback.Paused(owner))return;
   float step=Mathf.Min(speed*Time.fixedDeltaTime,distance-travel);Vector3 old=transform.position;transform.position+=transform.forward*step;travel+=step;
   // Swept overlap covers the whole physics step so fast waves cannot tunnel.
   Check(left,step);Check(right,step);if(travel>=distance)Stop();
  }
  void Check(BoxCollider c,float step){if(!c.enabled)return;Vector3 center=transform.TransformPoint(c.center)-transform.forward*step*.5f;Vector3 half=c.size*.5f;half.z+=step*.5f;int n=Physics.OverlapBoxNonAlloc(center,half,contacts,transform.rotation,~0,QueryTriggerInteraction.Ignore);for(int i=0;i<n;i++)Touch(contacts[i]);}
  public void Touch(Collider c){if(Busy&&owner)owner.HitPlayer(c,damage,push,lift);}
  public void Stop(){Busy=false;left.enabled=right.enabled=false;gameObject.SetActive(false);}
  void OnDisable(){Busy=false;if(left)left.enabled=false;if(right)right.enabled=false;}
 }
}
