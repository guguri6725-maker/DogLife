using UnityEngine;
namespace DogLife
{
    [RequireComponent(typeof(Rigidbody), typeof(CombatHealth))]
    // 플레이어를 감지해 추적하고 근접 또는 원거리 공격을 수행합니다.
    public sealed class StageEnemy : MonoBehaviour
    {
        public bool ranged, boss;
        public float speed = 2, damage = 5, detectionRange = 13;
        public Transform visualRoot;
        public Material projectileMaterial;
        public bool Windup => windupUntil > Time.time;
        Transform target;
        CombatHealth health;
        Rigidbody body;
        float attackAt, windupUntil;
        Vector3 lockedAim;
        Renderer[] healthBarRenderers;
        void Start()
        {
            var player = FindFirstObjectByType<SideViewMotor>(); target = player ? player.transform : null;
            body = GetComponent<Rigidbody>(); health = GetComponent<CombatHealth>();
            // 모델의 실제 크기로 체력바 위치를 계산하기 위해 외형만 보관합니다.
            healthBarRenderers = (visualRoot ? visualRoot : transform).GetComponentsInChildren<Renderer>(true);
            body.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            if (target) Physics.IgnoreCollision(GetComponent<Collider>(), target.GetComponent<Collider>());
            attackAt = Time.time + 1;
        }
        // 물리 프레임마다 이동, 공격 예고와 실제 공격을 처리합니다.
        void FixedUpdate()
        {
            if (!target || health.Dead || target.GetComponent<CombatHealth>().Dead) { body.linearVelocity = new Vector3(0, body.linearVelocity.y, 0); return; }
            var delta = target.position - transform.position; delta.z = 0;
            float distance = delta.magnitude;
            float range = boss ? 15 : ranged ? 9 : 1.7f;
            var velocity = body.linearVelocity;
            bool walking = distance < detectionRange && distance > (boss ? 7 : ranged ? 6 : 1.3f) && !Windup;
            velocity.x = walking ? Mathf.Sign(delta.x) * speed : 0; velocity.z = 0; body.linearVelocity = velocity;
            if (visualRoot)
            {
                visualRoot.localRotation = Quaternion.Euler(0, delta.x > 0 ? 0 : 180, 0);
                visualRoot.localScale = Vector3.one * (Windup ? 1 + .08f * Mathf.Sin(Time.time * 35) : 1);
            }
            if (windupUntil > 0 && Time.time >= windupUntil)
            {
                windupUntil = 0; attackAt = Time.time + (boss ? 2.2f : 1.4f);
                if (boss)
                {
                    for (int i = 0; i < 24; i++) Shoot(Quaternion.Euler(0, 0, i * 15) * Vector3.right, 5.5f);
                }
                else if (ranged) Shoot(lockedAim, 7);
                else if (distance < 2) target.GetComponent<CombatHealth>().Damage(damage);
            }
            if (distance <= range && Time.time >= attackAt && windupUntil == 0)
            {
                if (Physics.Raycast(transform.position, delta.normalized, distance, 1 << 8)) return;
                lockedAim = delta.normalized; windupUntil = Time.time + (boss ? .9f : .55f);
            }
        }
        void Shoot(Vector3 direction)
        { Shoot(direction, 7); }
        void Shoot(Vector3 direction, float projectileSpeed)
        {
            SideViewWeapons.SpawnProjectile(transform.position, direction, projectileSpeed, damage, 24, boss ? .14f : .12f, transform, true, projectileMaterial, boss ? new Color(1, .4f, .15f) : new Color(.85f, .4f, 1));
        }
        // 적 머리 위에 체력과 공격 예고를 표시합니다.
        void OnGUI()
        {
            if (!health || health.Dead || !Camera.main) return;
            // 고정 높이 대신 활성 모델의 위쪽 경계를 따라가므로 크기와 피벗이 달라도 맞습니다.
            var bounds = new Bounds(transform.position, Vector3.zero);
            bool hasBounds = false;
            foreach (var renderer in healthBarRenderers)
            {
                if (!renderer || !renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                    (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer))) continue;
                if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            var anchor = hasBounds ? new Vector3(bounds.center.x, bounds.max.y, bounds.center.z)
                : transform.position + Vector3.up * (boss ? 2.5f : 1.5f);
            // 카메라 렌더 해상도와 게임 화면 해상도가 달라도 같은 위치에 표시합니다.
            var point = Camera.main.WorldToViewportPoint(anchor);
            if (point.z <= 0 || point.x < 0 || point.x > 1 || point.y < 0 || point.y > 1) return;
            var viewport = Camera.main.rect;
            point.x = (viewport.x + point.x * viewport.width) * Screen.width;
            point.y = (viewport.y + point.y * viewport.height) * Screen.height;
            // 화면 좌표를 GUI 좌표로 바꾸고 머리와 일정한 픽셀 간격을 둡니다.
            float top = Screen.height - point.y - 17;
            var previousColor = GUI.color;
            var width = boss ? 160 : 55;
            GUI.color = Color.black; GUI.DrawTexture(new Rect(point.x - width / 2, top, width, 7), Texture2D.whiteTexture);
            float fraction = health.maxHealth > 0 ? Mathf.Clamp01(health.Current / health.maxHealth) : 0;
            GUI.color = Windup ? Color.yellow : new Color(.95f, .35f, .25f); GUI.DrawTexture(new Rect(point.x - width / 2, top, width * fraction, 7), Texture2D.whiteTexture);
            GUI.color = Color.white;
            if (Windup) GUI.Label(new Rect(point.x - 40, top - 25, 100, 25), "! ATTACK");
            GUI.color = previousColor;
        }
    }
}
