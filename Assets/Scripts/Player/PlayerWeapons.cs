using System.Collections.Generic;
using UnityEngine;
namespace DogLife
{
    // 무기 교체, 조준, 공격 간격과 발사체 생성을 관리합니다.
    public sealed class PlayerWeapons : MonoBehaviour
    {
        public WeaponDefinition[] loadout;
        public Transform weaponSocket;
        public Camera aimCamera;
        public Material projectileMaterial;
        public GameObject projectilePrefab;
        public bool readInput = true;
        public int Selected { get; private set; }
        public Vector3 AimDirection { get; private set; } = Vector3.right;
        public float Charge { get; private set; }
        public int AmmoMode { get; private set; }
        public WeaponDefinition Current => loadout != null && loadout.Length > 0 ? loadout[Selected] : null;
        public event System.Action Attacked;
        float[] nextAttack;
        float swingUntil;
        bool charging;
        GameObject visual;
        PlayerMotor motor;
        void Awake() { motor = GetComponent<PlayerMotor>(); nextAttack = new float[loadout == null ? 0 : loadout.Length]; }
        void Start() { Equip(0); if (!aimCamera) aimCamera = Camera.main; }
        // 마우스 위치를 월드 좌표로 바꾸고 현재 무기의 입력을 처리합니다.
        void Update()
        {
            if (SkillWindow.BlocksGameplayInput) return;
            if (!Current || !weaponSocket) return;
            if (readInput && Cursor.lockState == CursorLockMode.Locked)
            {
                for (int i = 0; i < loadout.Length && i < 4; i++) if (Input.GetKeyDown(KeyCode.Alpha1 + i)) Equip(i);
                if (Input.GetKeyDown(KeyCode.R)) AmmoMode = (AmmoMode + 1) % 3;
                if (aimCamera)
                {
                    // 화면 중앙의 실제 3D 충돌점을 겨냥합니다. 자기 몸은 조준에서 제외합니다.
                    var ray = aimCamera.ViewportPointToRay(new Vector3(.5f, .5f, 0));
                    var point = ray.GetPoint(80);
                    float nearest = 80;
                    foreach (var hit in Physics.RaycastAll(ray, 80, ~0, QueryTriggerInteraction.Ignore))
                        if (!hit.transform.IsChildOf(transform) && hit.distance < nearest) { nearest = hit.distance; point = hit.point; }
                    AimAt(point);
                }
                if (Current.kind == WeaponKind.ChargeSword)
                {
                    if (Input.GetMouseButtonDown(0) && Time.time >= nextAttack[Selected]) { charging = true; Charge = 0; }
                    if (charging && Input.GetMouseButton(0)) Charge = Mathf.Clamp01(Charge + Time.deltaTime / Current.chargeSeconds);
                    if (charging && Input.GetMouseButtonUp(0)) { TryAttack(Charge); charging = false; Charge = 0; }
                }
                else if (Input.GetMouseButton(0)) TryAttack();
            }
            var rotation = Quaternion.LookRotation(AimDirection) * Quaternion.Euler(0, -90, 0);
            if (Time.time < swingUntil) rotation = Quaternion.AngleAxis(Mathf.Lerp(-65, 65, 1 - (swingUntil - Time.time) / .25f), Vector3.up) * rotation;
            weaponSocket.rotation = rotation;
        }
        void OnDisable() { charging = false; Charge = 0; }
        public void AimAt(Vector3 point)
        {
            var direction = point - weaponSocket.position;
            if (direction.sqrMagnitude < .001f) return;
            AimDirection = direction.normalized;
            if (motor) motor.Face(AimDirection);
        }
        public void Equip(int index)
        {
            if (loadout == null || index < 0 || index >= loadout.Length || !loadout[index] || !weaponSocket) return;
            Selected = index; charging = false; Charge = 0; swingUntil = 0;
            if (visual) Destroy(visual);
            if (Current.visualPrefab) visual = Instantiate(Current.visualPrefab, weaponSocket);
        }
        // 현재 무기 종류에 맞춰 근접 공격 또는 발사체 공격을 실행합니다.
        public bool TryAttack(float charge = 0)
        {
            if (!Current || !weaponSocket || Time.time < nextAttack[Selected]) return false;
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
                    var direction = Quaternion.AngleAxis(angle, Vector3.up) * AimDirection;
                    float size = Current.kind == WeaponKind.ChargeSword ? Mathf.Lerp(.2f, .6f, Mathf.Clamp01(charge)) : .09f;
                    // Cast from the socket, so a muzzle pushed through a wall cannot shoot through it.
                    var projectile = SpawnProjectile(weaponSocket.position, direction, Current.projectileSpeed, Current.damage, Current.range, size, transform, false, projectileMaterial, Current.kind == WeaponKind.Rifle ? new[] { Current.color, Color.red, Color.green }[AmmoMode] : Current.color, projectilePrefab);
                    if (projectile) projectile.dot = Current.kind == WeaponKind.Rifle && AmmoMode != 0;
                }
            }
            Attacked?.Invoke();
            return true;
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
