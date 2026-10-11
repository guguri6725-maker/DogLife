using UnityEngine;
namespace DogLife
{
    // 발사 시 조준한 얼음검은 착지 후 같은 검을 꽂아 두며 기존 얼음길의 수명도 관리합니다.
    public sealed class FrozenKnightHazard : MonoBehaviour
    {
        [SerializeField] FrozenKnightDamageArea damageArea;
        [SerializeField] FrozenKnightVfx effects;
        [SerializeField] LayerMask obstacleMask=1<<8;
        [SerializeField] Transform projectileVisual;
        [SerializeField,Min(.01f)] float collisionRadius=.35f;
        [SerializeField] Color projectileTint=new Color(.3f,.78f,1,.9f);
        [SerializeField] Transform bladeTip;
        [SerializeField,Min(.01f)] float plantingTime=.22f,embeddedDepth=.2f,swordFadeTime=.65f,impactRadius=1.35f;
        Transform homingTarget;Collider targetBody;float turnSpeed,iceDuration,iceBorn;
        bool remnant,landed;
        Vector3 landingStart,landingEnd,landingGround;
        Quaternion landingStartRotation,landingRotation;
        Renderer[] swordRenderers;MaterialPropertyBlock swordProperties;
        FrozenKnightBoss owner;
        float speed,maxDistance,travelled,expires;
        bool moving,prepared,launched;float preparedAt;
        Vector3 visualScale;
        void Awake()
        {
            if(!projectileVisual)return;visualScale=projectileVisual.localScale;
            // 검 메시에도 냉기 색을 입히며 공유 재질 자체는 변경하지 않습니다.
            swordProperties=new MaterialPropertyBlock();swordProperties.SetColor("_Tint",projectileTint);
            swordRenderers=projectileVisual.GetComponentsInChildren<Renderer>();
            foreach(var renderer in swordRenderers)renderer.SetPropertyBlock(swordProperties);
        }
        public void Prepare(FrozenKnightBoss source)
        {owner=source;prepared=true;preparedAt=Time.time;damageArea.Disarm();effects.Show(FrozenKnightVfx.Shape.Ring,transform.position,transform.forward,1.1f,0,FrozenKnightVfx.Theme.BladeCharge);if(projectileVisual)projectileVisual.localScale=visualScale*.05f;}
        public void Track(Transform target,float degreesPerSecond,float structureDuration)
        {homingTarget=target;targetBody=target?target.GetComponent<Collider>():null;turnSpeed=degreesPerSecond;iceDuration=structureDuration;}
        public void Launch(FrozenKnightBoss source,float width,float length,float height,float damage,float push,float lift,float moveSpeed,float distance,float duration,float tick,float slowMultiplier=1,float slowDuration=0)
        {
            owner=source;speed=moveSpeed;maxDistance=distance;moving=speed>0;travelled=0;
            // 발사 순간의 위치로 한 번만 조준합니다. 이후 플레이어가 움직여도 직진합니다.
            if(moving&&homingTarget)
            {
                Vector3 goal=targetBody?targetBody.bounds.center:homingTarget.position+Vector3.up;
                Vector3 delta=goal-transform.position;
                if(delta.sqrMagnitude>.001f)transform.rotation=Quaternion.LookRotation(delta);
            }
            homingTarget=null;targetBody=null;
            launched=true;prepared=false;if(projectileVisual)projectileVisual.localScale=visualScale;
            expires=Time.time+(moving?distance/Mathf.Max(.01f,speed):duration);
            var box=(BoxCollider)damageArea.Shape;box.size=new Vector3(width,height,length);box.center=Vector3.up*(moving?0:height*.5f);
            // 이동하는 얼음검은 시각 메시의 실제 위치와 크기에 판정을 맞춥니다.
            if(moving&&projectileVisual){GameAudio.Play("WinterBoss_IceBlade_Fire",transform.position,.65f);FitBladeCollider(box);}
            effects.Show(FrozenKnightVfx.Shape.Line,projectileVisual?transform.position:transform.position-transform.forward*length*.5f,transform.forward,length,width,projectileVisual?FrozenKnightVfx.Theme.BladeFlight:FrozenKnightVfx.Theme.IceTrail);effects.Activate();
            damageArea.Arm(source,damage,push,lift,null,tick,false,slowMultiplier,slowDuration);
        }
        void Update()
        {
            if(!owner||!owner.CanDealDamage){Destroy(gameObject);return;}
            if(remnant){AnimateIce();return;}
            if(homingTarget&&prepared)
            {
                Vector3 goal=targetBody?targetBody.bounds.center:homingTarget.position+Vector3.up;
                Vector3 delta=goal-transform.position;
                if(delta.sqrMagnitude>.001f)transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(delta),turnSpeed*Time.deltaTime);
            }
            if(prepared){effects.MovePose(transform.position,transform.forward);if(projectileVisual)projectileVisual.localScale=visualScale*Mathf.SmoothStep(.05f,1,(Time.time-preparedAt)/.6f);return;}
            if(!launched)return;
            if(Time.time>=expires){FinishProjectile();return;}
            if(!moving)return;
            if(damageArea.HasHit){FinishProjectile();return;}
            float step=Mathf.Min(speed*Time.deltaTime,maxDistance-travelled);
            float tipReach=bladeTip?Mathf.Max(0,Vector3.Dot(bladeTip.position-transform.position,transform.forward)):0;
            if(Physics.SphereCast(transform.position,collisionRadius,transform.forward,out var obstacle,step+tipReach,obstacleMask,QueryTriggerInteraction.Ignore))
            {
                transform.position+=transform.forward*Mathf.Max(0,obstacle.distance-tipReach);
                damageArea.SweepNow();
                FinishProjectile(obstacle.normal.y>.5f?(Vector3?)obstacle.point:null);return;
            }
            transform.position+=transform.forward*step;travelled+=step;effects.MovePose(transform.position,transform.forward);
            damageArea.SweepNow();
            if(damageArea.HasHit){FinishProjectile();return;}
            if(travelled>=maxDistance)FinishProjectile();
        }
        void FitBladeCollider(BoxCollider box)
        {
            bool found=false;Bounds bounds=new Bounds();
            foreach(var mesh in projectileVisual.GetComponentsInChildren<MeshFilter>())
            {
                if(!mesh.sharedMesh)continue;
                var b=mesh.sharedMesh.bounds;
                for(int i=0;i<8;i++)
                {
                    Vector3 corner=b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                    Vector3 local=box.transform.InverseTransformPoint(mesh.transform.TransformPoint(corner));
                    if(!found){bounds=new Bounds(local,Vector3.zero);found=true;}else bounds.Encapsulate(local);
                }
            }
            if(found){box.center=bounds.center;box.size=bounds.size;}
        }
        void FinishProjectile(Vector3? groundContact=null)
        {
            if(moving&&projectileVisual)GameAudio.Play("WinterBoss_IceBlade_Break",transform.position,.5f);
            if(!projectileVisual||!bladeTip||iceDuration<=0){Destroy(gameObject);return;}
            damageArea.Disarm();moving=false;prepared=false;effects.Hide();
            Vector3 ground=transform.position;
            if(groundContact.HasValue)ground=groundContact.Value;
            else if(Physics.Raycast(ground+Vector3.up*2,Vector3.down,out var hit,30,obstacleMask,QueryTriggerInteraction.Ignore))ground=hit.point;
            else{Destroy(gameObject);return;}
            // 피격 후 판정은 끄고, 날아온 검 자체가 내려와 칼끝을 지면에 박습니다.
            remnant=true;iceBorn=Time.time;landingGround=ground;landingStart=transform.position;landingStartRotation=transform.rotation;
            Vector3 lean=Vector3.ProjectOnPlane(transform.forward,Vector3.up).normalized*.15f;
            landingRotation=Quaternion.LookRotation((Vector3.down+lean).normalized,Vector3.forward);
            Vector3 localTip=transform.InverseTransformPoint(bladeTip.position);
            landingEnd=ground+landingRotation*Vector3.forward*embeddedDepth-landingRotation*Vector3.Scale(localTip,transform.lossyScale);
            AnimateIce();
        }
        void AnimateIce()
        {
            float age=Time.time-iceBorn;
            float u=Mathf.SmoothStep(0,1,age/plantingTime);
            transform.SetPositionAndRotation(Vector3.Lerp(landingStart,landingEnd,u),Quaternion.Slerp(landingStartRotation,landingRotation,u));
            if(!landed&&age>=plantingTime){landed=true;effects.Show(FrozenKnightVfx.Shape.Ring,landingGround,Vector3.forward,impactRadius,0,FrozenKnightVfx.Theme.PlantedBlade);effects.Activate();}
            float remaining=plantingTime+iceDuration-age;if(remaining<=0){Destroy(gameObject);return;}
            Color tint=projectileTint;tint.a*=Mathf.Clamp01(remaining/swordFadeTime);swordProperties.SetColor("_Tint",tint);
            foreach(var renderer in swordRenderers)renderer.SetPropertyBlock(swordProperties);
        }
        public void ExtendTrail(Vector3 from,Vector3 to)
        {
            // 직선 질주 하나당 판정 하나를 늘려서 겹치는 Tick과 이펙트 오브젝트 급증을 피합니다.
            if(moving)return;Vector3 delta=to-from;delta.y=0;if(delta.sqrMagnitude<.001f)return;
            transform.SetPositionAndRotation((from+to)*.5f,Quaternion.LookRotation(delta));
            var box=(BoxCollider)damageArea.Shape;var size=box.size;size.z=delta.magnitude;box.size=size;
            damageArea.ResetSweep();
            effects.ResizePath(from,delta.magnitude);
        }
        void OnDisable(){if(damageArea)damageArea.Disarm();if(effects)effects.Hide();}
    }
}
