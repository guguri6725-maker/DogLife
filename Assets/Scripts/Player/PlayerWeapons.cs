using System.Collections.Generic;
using UnityEngine;
namespace DogLife
{
    // 기본 근접 공격과 조준. 기존 무기 장착/발사/차지 입력은 제거했습니다.
    public sealed class PlayerWeapons : MonoBehaviour
    {
        public Transform weaponSocket;
        public Transform leftWeaponSocket;
        public Transform GetWeaponSocket(InventoryWeapon item)=>item&&item.weaponCategory==InventoryWeapon.WeaponCategory.Bow&&leftWeaponSocket?leftWeaponSocket:weaponSocket;
        public Camera aimCamera;
        public bool readInput = true;
        public Vector3 AimDirection { get; private set; } = Vector3.forward;
        public Vector3 AimPoint { get; private set; }
        [SerializeField] private float aimDistance=100f;
        bool hasAimPoint;
        readonly RaycastHit[] aimHits=new RaycastHit[64];
        readonly Collider[] meleeHits=new Collider[64];
        readonly HashSet<CombatHealth> meleeDamaged=new HashSet<CombatHealth>();
        // 주 궤적의 좌표와 두께를 판정에서도 공유합니다. 잔상/파편은 피해를 주지 않습니다.
        public void ApplyVisibleMelee(InventoryWeapon equipped,Vector3 origin,LineRenderer shape,bool sweep,float damageScale,LineRenderer impact=null)
        {
            meleeDamaged.Clear();
            for(int i=1;i<shape.positionCount;i++)
            {
                Vector3 a=shape.GetPosition(i-1),b=shape.GetPosition(i);
                float u=(i-.5f)/(shape.positionCount-1);
                float radius=Mathf.Max(.015f,shape.widthCurve.Evaluate(u)*shape.widthMultiplier*.5f);
                DamageSegment(equipped,origin,a,b,radius,damageScale);
                if(sweep)DamageSegment(equipped,origin,origin,b,radius,damageScale);
            }
            if(impact)
            {
                Vector3 center=Vector3.zero;
                for(int i=0;i<impact.positionCount-1;i++)center+=impact.GetPosition(i);
                center/=impact.positionCount-1;
                for(int i=1;i<impact.positionCount;i++)
                    DamageSegment(equipped,origin,center,impact.GetPosition(i),impact.widthMultiplier*.5f,damageScale);
            }
            Attacked?.Invoke();
        }
        void DamageSegment(InventoryWeapon equipped,Vector3 origin,Vector3 a,Vector3 b,float radius,float power)
        {
            int count=Physics.OverlapCapsuleNonAlloc(a,b,radius,meleeHits,~0,QueryTriggerInteraction.Ignore);
            var hits=count==meleeHits.Length?Physics.OverlapCapsule(a,b,radius,~0,QueryTriggerInteraction.Ignore):meleeHits;
            if(hits!=meleeHits)count=hits.Length;
            for(int i=0;i<count;i++)
            {
                var target=hits[i].GetComponentInParent<CombatHealth>();
                if(!target||target.player||meleeDamaged.Contains(target))continue;
                Vector3 segment=b-a;
                float t=segment.sqrMagnitude>.0001f?Mathf.Clamp01(Vector3.Dot(hits[i].bounds.center-a,segment)/segment.sqrMagnitude):0;
                Vector3 closest=a+segment*t;
                Vector3 point=hits[i].ClosestPoint(closest);
                if((point-closest).sqrMagnitude<.000001f)point=hits[i].ClosestPoint(origin);
                Vector3 delta=point-origin;
                // 파괴 가능한 장애물 자신은 시야를 가리는 벽으로 처리하지 않습니다.
                if(delta.sqrMagnitude>.0001f&&Physics.Raycast(origin,delta.normalized,out var obstruction,delta.magnitude,1<<8,QueryTriggerInteraction.Ignore)
                    &&obstruction.collider.GetComponentInParent<CombatHealth>()!=target)continue;
                meleeDamaged.Add(target);
                Color tint=equipped.boneOrigin==InventoryWeapon.BoneOrigin.Humanoid?new Color(.8f,.24f,.3f,1):new Color(.5f,.85f,1,1);
                float feedbackPower=attackCombo&&attackCombo.ComboStep==3&&equipped.weaponCategory!=InventoryWeapon.WeaponCategory.Spear?Mathf.Max(1.5f,power):power;
                if(CombatFeedback.DealDamage(target,equipped.damage*power,point,point-origin,feedbackPower,transform,tint)&&target.Dead)
                {
                    var karma=GetComponent<PlayerKarma>();if(karma)karma.RecordDefeat(target,equipped);
                }
            }
        }
        public event System.Action Attacked;
        PlayerMotor motor;
        BasicWeaponCombat attackCombo;
        PlayerInventory inventory;
        void Awake() { motor = GetComponent<PlayerMotor>(); attackCombo = GetComponent<BasicWeaponCombat>(); inventory=GetComponent<PlayerInventory>(); }
        void Start() { if (!aimCamera) aimCamera = Camera.main; }
        void Update()
        {
            if (SkillWindow.BlocksGameplayInput || !readInput || Cursor.lockState != CursorLockMode.Locked)
            {if(attackCombo&&attackCombo.IsCharging)attackCombo.Cancel();return;}
            if (!attackCombo) attackCombo = GetComponent<BasicWeaponCombat>();
            RefreshAimDirection();
            if (attackCombo && attackCombo.Ready && Input.GetMouseButtonDown(0)) attackCombo.PressAttack();
            if(attackCombo&&Input.GetMouseButtonUp(0))attackCombo.ReleaseAttack();
        }
        void OnDisable() { if (attackCombo) attackCombo.Cancel(); }
        void OnApplicationFocus(bool focused){if(!focused&&attackCombo&&attackCombo.IsCharging)attackCombo.Cancel();}
        internal void RefreshAimDirection()
        {
            if(readInput&&aimCamera)
            {
                var ray=aimCamera.ViewportPointToRay(new Vector3(.5f,.5f,0));
                var target=ray.GetPoint(aimDistance);
                int count=Physics.RaycastNonAlloc(ray,aimHits,aimDistance,~0,QueryTriggerInteraction.Ignore);
                // 포화 시에는 전체 결과를 사용해 가까운 벽/적을 놓치지 않습니다.
                var hits=count==aimHits.Length?Physics.RaycastAll(ray,aimDistance,~0,QueryTriggerInteraction.Ignore):aimHits;
                if(hits!=aimHits)count=hits.Length;
                float nearest=float.PositiveInfinity;
                for(int i=0;i<count;i++)
                {
                    var hit=hits[i];
                    if(hit.distance>=nearest||IgnoreAimCollider(hit.collider))continue;
                    nearest=hit.distance;target=hit.point;
                }
                AimAt(target);
            }
        }
        bool IgnoreAimCollider(Collider col)=>col.transform.IsChildOf(transform)||(aimCamera&&col.transform.IsChildOf(aimCamera.transform));
        public Vector3 GetProjectileDirection(Vector3 origin)
        {
            RefreshAimDirection();
            var rayForward=aimCamera?aimCamera.transform.forward:AimDirection;
            var delta=hasAimPoint?AimPoint-origin:rayForward;
            // A wall between the shoulder camera and the player must not turn a shot backwards.
            if(delta.sqrMagnitude<.001f||Vector3.Dot(delta,rayForward)<=0)return rayForward.normalized;
            return delta.normalized;
        }
        public Vector3 SafeLaunchOrigin(Vector3 desired)
        {
            var capsule=GetComponent<CapsuleCollider>();var center=capsule?capsule.bounds.center:transform.position+Vector3.up;
            var delta=desired-center;if(delta.sqrMagnitude<.0001f)return desired;
            var hits=Physics.SphereCastAll(center,.13f,delta.normalized,delta.magnitude,~0,QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
            foreach(var hit in hits)if(!IgnoreAimCollider(hit.collider))return center+delta.normalized*Mathf.Max(0,hit.distance-.02f);
            return desired;
        }
        public void AimAt(Vector3 point)
        {
            AimPoint=point;hasAimPoint=true;
            var direction = Vector3.ProjectOnPlane(point - transform.position, Vector3.up);
            var item=inventory?inventory.EquippedWeapon:null;
            bool melee=item&&item.weaponCategory!=InventoryWeapon.WeaponCategory.Bow&&item.weaponCategory!=InventoryWeapon.WeaponCategory.Boomerang;
            if(aimCamera)
            {
                var view=Vector3.ProjectOnPlane(aimCamera.transform.forward,Vector3.up).normalized;
                // 근접 공격은 카메라와 몸 사이의 적 표면에 조준점이 잡혀도 뒤집히지 않습니다.
                if(view.sqrMagnitude>.01f&&(melee||Vector3.Dot(direction,view)<=.15f))direction=view;
            }
            if (direction.sqrMagnitude < .001f) return;
            AimDirection = direction.normalized;
            if (motor) motor.Face(AimDirection);
        }
        public float ThrustHeight(Vector3 origin,Vector3 forward,float reach)
        {
            int count=Physics.OverlapSphereNonAlloc(origin,reach,meleeHits,~0,QueryTriggerInteraction.Ignore);
            float nearest=float.PositiveInfinity,height=origin.y;
            for(int i=0;i<count;i++)
            {
                var c=meleeHits[i];var health=c.GetComponentInParent<CombatHealth>();
                if(!health||health.player||health.Dead)continue;
                Vector3 delta=c.bounds.center-origin;float along=Vector3.Dot(delta,forward);
                Vector3 side=Vector3.ProjectOnPlane(delta,Vector3.up)-forward*along;
                float width=Mathf.Max(c.bounds.extents.x,c.bounds.extents.z);
                if(along<0||along>reach+width||side.magnitude>width+.12f||along>=nearest)continue;
                if(Physics.Raycast(origin,delta.normalized,delta.magnitude,1<<8,QueryTriggerInteraction.Ignore))continue;
                nearest=along;height=Mathf.Clamp(c.bounds.center.y,origin.y-.8f,origin.y+.3f);
            }
            return height;
        }
        public void ApplyBasicMelee(InventoryWeapon equipped, Vector3 origin, Vector3 forward, float reach, float minimumDot,float damageScale=1f)
        {
            var damaged=new HashSet<CombatHealth>();
            foreach(var hit in Physics.OverlapSphere(origin,reach,~0,QueryTriggerInteraction.Ignore))
            {
                var target=hit.GetComponentInParent<CombatHealth>();
                if(!target||target.player||!damaged.Add(target))continue;
                var offset=target.transform.position-origin;
                if(Vector3.Dot(Vector3.ProjectOnPlane(offset,Vector3.up).normalized,forward)<minimumDot)continue;
                if(Physics.Raycast(origin,offset.normalized,offset.magnitude,1<<8))continue;
                bool wasAlive=!target.Dead;
                target.Damage(equipped.damage*damageScale);
                if(wasAlive&&target.Dead){var karma=GetComponent<PlayerKarma>();if(karma)karma.RecordDefeat(target,equipped);}
            }
            Attacked?.Invoke();
        }
        // 플레이어와 적이 함께 사용하는 공통 발사체를 생성합니다.
        public static CombatProjectile SpawnProjectile(Vector3 origin, Vector3 direction, float speed, float damage, float range, float radius, Transform owner, bool hostile, Material material, Color color, GameObject prefab)
        {
            // Hierarchy에서 만든 발사체 프리팹을 런타임에 복제합니다.
            if (!prefab) { Debug.LogError("Projectile prefab is missing", owner); return null; }
            var go = Instantiate(prefab);
            go.name = hostile ? "Enemy projectile" : "Weapon projectile";
            go.transform.position = origin; go.transform.localScale = Vector3.one * radius * 2;
            var renderer = go.GetComponent<Renderer>(); renderer.sharedMaterial = material;
            var block = new MaterialPropertyBlock(); block.SetColor("_Color", color); renderer.SetPropertyBlock(block);
            var p = go.GetComponent<CombatProjectile>();
            p.direction = direction.normalized; p.speed = speed; p.damage = damage; p.range = range; p.radius = radius; p.owner = owner; p.hostile = hostile;
            return p;
        }
    }
}



