using System.Collections.Generic;
using UnityEngine;
namespace DogLife
{
    // 무기 교체, 조준, 공격 간격과 발사체 생성을 관리합니다.
    public sealed class SideViewWeapons : MonoBehaviour
    {
        public WeaponDefinition[] loadout;
        public Transform weaponSocket;
        public Camera aimCamera;
        public Material projectileMaterial;
        public bool readInput = true;
        public int Selected { get; private set; }
        public Vector3 AimDirection { get; private set; } = Vector3.right;
        public float Charge { get; private set; }
        public int AmmoMode { get; private set; }
        public WeaponDefinition Current => loadout[Selected];
        public event System.Action Attacked;
        float[] nextAttack;
        float swingUntil;
        bool charging;
        GameObject visual;
        SideViewMotor motor;
        void Awake() { motor = GetComponent<SideViewMotor>(); nextAttack = new float[loadout.Length]; }
        void Start() { Equip(0); if (!aimCamera) aimCamera = Camera.main; }
        // 마우스 위치를 월드 좌표로 바꾸고 현재 무기의 입력을 처리합니다.
        void Update()
        {
            if (readInput)
            {
                for (int i = 0; i < loadout.Length && i < 4; i++) if (Input.GetKeyDown(KeyCode.Alpha1 + i)) Equip(i);
                if (Input.GetKeyDown(KeyCode.R)) AmmoMode = (AmmoMode + 1) % 3;
                if (aimCamera)
                {
                    var ray = aimCamera.ScreenPointToRay(Input.mousePosition);
                    var plane = new Plane(Vector3.forward, new Vector3(0, 0, transform.position.z));
                    if (plane.Raycast(ray, out float distance)) AimAt(ray.GetPoint(distance));
                }
                if (Current.kind == WeaponKind.ChargeSword)
                {
                    if (Input.GetMouseButtonDown(0) && Time.time >= nextAttack[Selected]) { charging = true; Charge = 0; }
                    if (charging && Input.GetMouseButton(0)) Charge = Mathf.Clamp01(Charge + Time.deltaTime / Current.chargeSeconds);
                    if (charging && Input.GetMouseButtonUp(0)) { TryAttack(Charge); charging = false; Charge = 0; }
                }
                else if (Input.GetMouseButton(0)) TryAttack();
            }
            float angle = Mathf.Atan2(AimDirection.y, AimDirection.x) * Mathf.Rad2Deg;
            if (Time.time < swingUntil) angle += Mathf.Lerp(-65, 65, 1 - (swingUntil - Time.time) / .25f);
            weaponSocket.rotation = Quaternion.Euler(0, 0, angle);
        }
        void OnDisable() { charging = false; Charge = 0; }
        public void AimAt(Vector3 point)
        {
            var direction = point - weaponSocket.position; direction.z = 0;
            if (direction.sqrMagnitude < .001f) return;
            AimDirection = direction.normalized;
            if (motor) motor.Face(AimDirection.x);
        }
        public void Equip(int index)
        {
            if (index < 0 || index >= loadout.Length) return;
            Selected = index; charging = false; Charge = 0; swingUntil = 0;
            if (visual) Destroy(visual);
            visual = Instantiate(Current.visualPrefab, weaponSocket);
        }
        // 현재 무기 종류에 맞춰 근접 공격 또는 발사체 공격을 실행합니다.
        public bool TryAttack(float charge = 0)
        {
            if (Time.time < nextAttack[Selected]) return false;
            nextAttack[Selected] = Time.time + Mathf.Max(.05f, Current.cooldown);
            bool sword = Current.kind == WeaponKind.Sword || Current.kind == WeaponKind.ChargeSword;
            if (sword)
            {
                swingUntil = Time.time + .25f;
                var damaged = new HashSet<CombatHealth>();
                foreach (var hit in Physics.OverlapSphere(weaponSocket.position, Current.meleeRange, ~0, QueryTriggerInteraction.Ignore))
                {
                    var health = hit.GetComponentInParent<CombatHealth>();
                    if (!health || health.player || !damaged.Add(health)) continue;
                    var offset = health.transform.position - weaponSocket.position;
                    if (Vector3.Dot(offset.normalized, AimDirection) < .25f) continue;
                    if (Physics.Raycast(weaponSocket.position, offset.normalized, offset.magnitude, 1 << 8)) continue;
                    health.Damage(Current.kind == WeaponKind.Sword ? Current.damage : 10);
                }
            }
            if (Current.kind != WeaponKind.Sword)
            {
                for (int i = 0; i < Current.pellets; i++)
                {
                    float angle = Current.pellets == 1 ? 0 : Mathf.Lerp(-Current.spread / 2, Current.spread / 2, (float)i / (Current.pellets - 1));
                    var direction = Quaternion.Euler(0, 0, angle) * AimDirection;
                    float size = Current.kind == WeaponKind.ChargeSword ? Mathf.Lerp(.2f, .6f, Mathf.Clamp01(charge)) : .09f;
                    // Cast from the socket, so a muzzle pushed through a wall cannot shoot through it.
                    var projectile = SpawnProjectile(weaponSocket.position, direction, Current.projectileSpeed, Current.damage, Current.range, size, transform, false, projectileMaterial, Current.kind == WeaponKind.Rifle ? new[] { Current.color, Color.red, Color.green }[AmmoMode] : Current.color);
                    projectile.dot = Current.kind == WeaponKind.Rifle && AmmoMode != 0;
                }
            }
            Attacked?.Invoke();
            return true;
        }
        // 플레이어와 적이 함께 사용하는 공통 발사체를 생성합니다.
        public static SideViewProjectile SpawnProjectile(Vector3 origin, Vector3 direction, float speed, float damage, float range, float radius, Transform owner, bool hostile, Material material, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = hostile ? "Enemy projectile" : "Weapon projectile";
            var collider = go.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
            go.transform.position = origin; go.transform.localScale = Vector3.one * radius * 2;
            var renderer = go.GetComponent<Renderer>(); renderer.sharedMaterial = material;
            var block = new MaterialPropertyBlock(); block.SetColor("_Color", color); renderer.SetPropertyBlock(block);
            var p = go.AddComponent<SideViewProjectile>();
            p.direction = direction.normalized; p.speed = speed; p.damage = damage; p.range = range; p.radius = radius; p.owner = owner; p.hostile = hostile;
            return p;
        }
    }
}
