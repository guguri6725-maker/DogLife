using UnityEngine;
namespace DogLife
{
    public sealed class SummerHomingBubble:MonoBehaviour
    {
        PlayerMotor target;CombatHealth owner;Vector3 heading;float speed,turn,expires,moveSlow,rollSlow,duration;int index;LayerMask walls;Material material;
        public const float Radius=.38f;
        float pushSpeed,pushDuration;
        public void Initialize(Vector3 origin,Vector3 direction,PlayerMotor player,CombatHealth source,int slot,Material mat,float velocity,float turnRate,float lifetime,float moveReduction,float rollReduction,float debuffTime,LayerMask mask,float knockbackSpeed=1.4f,float knockbackDuration=.12f)
        {
            pushSpeed=knockbackSpeed;pushDuration=knockbackDuration;
            transform.position=origin;heading=direction.normalized;target=player;owner=source;index=slot;material=mat;speed=velocity;turn=turnRate;expires=Time.time+lifetime;moveSlow=moveReduction;rollSlow=rollReduction;duration=debuffTime;walls=mask;
            var visual=gameObject.AddComponent<SummerWaterVfx>();visual.Initialize(mat);visual.bubble=this;
        }
        void Update()
        {
            if(!target||!owner||owner.Dead||!owner.isActiveAndEnabled){Destroy(gameObject);return;}
            if(Time.time>=expires){Pop();return;}
            var h=target.GetComponent<CombatHealth>();if(!h||h.Dead){Destroy(gameObject);return;}
            Vector3 aim=target.transform.position+Vector3.up*.8f+Vector3.right*((index-1)*.45f);
            heading=Vector3.RotateTowards(heading,(aim-transform.position).normalized,turn*Mathf.Deg2Rad*Time.deltaTime,0);
            Vector3 next=transform.position+heading*speed*Time.deltaTime;
            if(Physics.SphereCast(transform.position,Radius,heading,out var wall,speed*Time.deltaTime,walls,QueryTriggerInteraction.Ignore)){Pop();return;}
            var c=target.GetComponent<Collider>();
            if(c)
            {
                Vector3 segment=next-transform.position;float t=segment.sqrMagnitude>.00001f?Mathf.Clamp01(Vector3.Dot(c.bounds.center-transform.position,segment)/segment.sqrMagnitude):0;
                Vector3 point=transform.position+segment*t;
                if((c.ClosestPoint(point)-point).sqrMagnitude<=Radius*Radius)
                {
                    if(!target.IsDashing)
                    {
                        var debuff=target.GetComponent<BubbleDebuff>();if(!debuff)debuff=target.gameObject.AddComponent<BubbleDebuff>();debuff.Apply(moveSlow,rollSlow,duration,material);
                        // 작은 거품이 인어의 띄우기나 이미 진행 중인 피격 반응을 덮지 않습니다.
                        if(!target.IsKnockedBack&&pushSpeed>0)
                        {
                            Vector3 direction=Vector3.ProjectOnPlane(target.transform.position-transform.position,Vector3.up);
                            if(direction.sqrMagnitude<.001f)direction=heading;
                            target.ApplyKnockback(direction,pushSpeed,0,pushDuration);
                        }
                    }
                    Pop(!target.IsDashing);return;
                }
            }
            transform.position=next;
        }
        void Pop(bool hit=false){GameAudio.Play(hit?"Crab_Bubble_Hit":"Crab_Bubble_Expire",transform.position,hit?.65f:.25f,minimumInterval:.06f);SummerWaterVfx.Burst(transform.position,material);Destroy(gameObject);}
    }
}
