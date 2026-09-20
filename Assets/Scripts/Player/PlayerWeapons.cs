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
        public event System.Action Attacked;
        PlayerMotor motor;
        BasicWeaponCombat attackCombo;
        void Awake() { motor = GetComponent<PlayerMotor>(); attackCombo = GetComponent<BasicWeaponCombat>(); }
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
                var hits=Physics.RaycastAll(ray,aimDistance,~0,QueryTriggerInteraction.Ignore);
                System.Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
                foreach(var hit in hits){if(IgnoreAimCollider(hit.collider))continue;target=hit.point;break;}
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
            if (direction.sqrMagnitude < .001f) return;
            AimDirection = direction.normalized;
            if (motor) motor.Face(AimDirection);
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
                target.Damage(equipped.damage*damageScale);
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



