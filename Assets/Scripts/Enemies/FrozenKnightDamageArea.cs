using System.Collections.Generic;
using UnityEngine;
namespace DogLife
{
    // 미리 배치한 Trigger만 판정에 사용합니다. 모델/VFX 콜라이더는 사용하지 않습니다.
    public sealed class FrozenKnightDamageArea : MonoBehaviour
    {
        [SerializeField] Collider attackCollider;
        [SerializeField] LayerMask targetMask = ~0;
        [SerializeField, Min(.02f)] float sweepStep = .2f;
        readonly Collider[] contacts = new Collider[64];
        readonly Dictionary<CombatHealth,float> nextTicks = new Dictionary<CombatHealth,float>();
        HashSet<CombatHealth> hits;
        FrozenKnightBoss owner;
        float damage, push, lift, tick;
        float slowMultiplier=1,slowDuration;
        Vector3 previous;
        Quaternion previousRotation;
        bool sectorMode;
        float sectorRadius,sectorAngle,sectorHeight;
        public bool SectorArmed=>Armed&&sectorMode;
        public bool Armed { get; private set; }
        public bool HasHit { get; private set; }
        public Collider Shape => attackCollider;
        void Awake() { Disarm(); }
        public void Arm(FrozenKnightBoss source,float amount,float knockback,float launch,HashSet<CombatHealth> group=null,float tickInterval=0,bool useSector=false,float movementMultiplier=1,float movementSlowDuration=0)
        {
            owner=source;damage=amount;push=knockback;lift=launch;tick=tickInterval;
            slowMultiplier=movementMultiplier;slowDuration=movementSlowDuration;
            hits=group??new HashSet<CombatHealth>();nextTicks.Clear();HasHit=false;
            sectorMode=useSector;previous=transform.position;previousRotation=transform.rotation;Armed=true;attackCollider.enabled=!sectorMode;
            CheckAt(previous,previousRotation);
        }
        public void ArmSector(FrozenKnightBoss source,float amount,float knockback,float launch,float radius,float angle,float height,HashSet<CombatHealth> group=null)
        {
            // 바닥 경고와 같은 반경/각도를 사용합니다. 검 모델의 높이로 지상 타격이 누락되지 않습니다.
            sectorRadius=radius;sectorAngle=angle;sectorHeight=height;
            Arm(source,amount,knockback,launch,group,0,true);
        }
        public void Disarm() { Armed=false;if(attackCollider)attackCollider.enabled=false; }
        public void ResetSweep(){previous=transform.position;previousRotation=transform.rotation;}
        void OnDisable() { Disarm(); }
        void OnTriggerEnter(Collider other) { Touch(other); }
        void OnTriggerStay(Collider other) { Touch(other); }
        void FixedUpdate()
        { SweepNow(); }
        // 투사체는 이동 직후 검사하여 지면 충돌로 판정이 꺼지기 전에 타격을 전달합니다.
        public void SweepNow()
        {
            if(!Armed)return;
            if(!owner||!owner.CanDealDamage){Disarm();return;}
            // 고속 이동 구간도 같은 Trigger 형상으로 여러 지점 검사해 관통을 방지합니다.
            var now=transform.position;var rotation=transform.rotation;
            float reach=attackCollider.bounds.extents.magnitude;
            float swept=Vector3.Distance(previous,now)+Quaternion.Angle(previousRotation,rotation)*Mathf.Deg2Rad*reach;
            int steps=Mathf.Clamp(Mathf.CeilToInt(swept/Mathf.Max(.02f,sweepStep)),1,64);
            for(int i=1;i<=steps;i++)CheckAt(Vector3.Lerp(previous,now,(float)i/steps),Quaternion.Slerp(previousRotation,rotation,(float)i/steps));
            previous=now;previousRotation=rotation;
        }
        void CheckAt(Vector3 position,Quaternion rotation)
        {
            if(!attackCollider)return;
            int n=0;
            if(sectorMode)
                n=Physics.OverlapBoxNonAlloc(position+Vector3.up*sectorHeight*.5f,new Vector3(sectorRadius,sectorHeight*.5f,sectorRadius),contacts,rotation,targetMask,QueryTriggerInteraction.Ignore);
            else if(attackCollider is BoxCollider box)
                n=Physics.OverlapBoxNonAlloc(position+rotation*Vector3.Scale(box.center,box.transform.lossyScale),Vector3.Scale(box.size*.5f,Abs(box.transform.lossyScale)),contacts,rotation,targetMask,QueryTriggerInteraction.Ignore);
            else if(attackCollider is SphereCollider sphere)
                n=Physics.OverlapSphereNonAlloc(position+rotation*Vector3.Scale(sphere.center,sphere.transform.lossyScale),sphere.radius*Max(Abs(sphere.transform.lossyScale)),contacts,targetMask,QueryTriggerInteraction.Ignore);
            for(int i=0;i<n;i++)Touch(contacts[i]);
            // 구조물이 많은 방에서 버퍼가 가득 차도 플레이어 판정이 빠지지 않게 합니다.
            if(n==contacts.Length)
            {
                Collider[] overflow=null;
                if(sectorMode)overflow=Physics.OverlapBox(position+Vector3.up*sectorHeight*.5f,new Vector3(sectorRadius,sectorHeight*.5f,sectorRadius),rotation,targetMask,QueryTriggerInteraction.Ignore);
                else if(attackCollider is BoxCollider b)overflow=Physics.OverlapBox(position+rotation*Vector3.Scale(b.center,b.transform.lossyScale),Vector3.Scale(b.size*.5f,Abs(b.transform.lossyScale)),rotation,targetMask,QueryTriggerInteraction.Ignore);
                else if(attackCollider is SphereCollider s)overflow=Physics.OverlapSphere(position+rotation*Vector3.Scale(s.center,s.transform.lossyScale),s.radius*Max(Abs(s.transform.lossyScale)),targetMask,QueryTriggerInteraction.Ignore);
                if(overflow!=null)foreach(var collider in overflow)Touch(collider);
            }
        }
        static Vector3 Abs(Vector3 v)=>new Vector3(Mathf.Abs(v.x),Mathf.Abs(v.y),Mathf.Abs(v.z));
        static float Max(Vector3 v)=>Mathf.Max(v.x,Mathf.Max(v.y,v.z));
        public void Touch(Collider other)
        {
            if(!Armed||!owner||!owner.CanDealDamage)return;
            if(sectorMode&&!InsideSector(other))return;
            var target=other.GetComponentInParent<CombatHealth>();
            if(!target||!target.player||target.Dead)return;
            if(tick>0){if(nextTicks.TryGetValue(target,out var next)&&Time.time<next)return;}
            else if(hits.Contains(target))return;
            if(!owner.DealHit(target,damage,push,lift,transform.position))return;
            // 실제 피해가 들어갔을 때만 감속하므로 대시/피격 무적 중에는 적용되지 않습니다.
            if(slowDuration>0&&target.TryGetComponent<PlayerMotor>(out var motor))motor.ApplyMovementSlow(slowMultiplier,slowDuration);
            HasHit=true;
            if(tick>0)nextTicks[target]=Time.time+tick;else hits.Add(target);
        }
        bool InsideSector(Collider other)
        {
            var bounds=other.bounds;var origin=transform.position;
            if(bounds.max.y<origin.y||bounds.min.y>origin.y+sectorHeight)return false;
            // 원호 및 양쪽 선분까지의 거리로 플레이어 몸체가 경계에 걸친 경우도 포함합니다.
            Vector3 center=bounds.center-origin;center.y=0;
            float bodyRadius=Mathf.Max(bounds.extents.x,bounds.extents.z),distance=center.magnitude;
            if(distance>sectorRadius+bodyRadius)return false;
            if(sectorAngle>=359||distance<=bodyRadius)return true;
            float half=sectorAngle*.5f;
            if(Vector3.Angle(transform.forward,center)<=half)return true;
            for(int side=0;side<2;side++)
            {
                float sign=side==0?-1:1;
                Vector3 edge=Quaternion.AngleAxis(half*sign,Vector3.up)*transform.forward;
                Vector3 nearest=edge*Mathf.Clamp(Vector3.Dot(center,edge),0,sectorRadius);
                if((center-nearest).sqrMagnitude<=bodyRadius*bodyRadius)return true;
            }
            return false;
        }
    }
}
