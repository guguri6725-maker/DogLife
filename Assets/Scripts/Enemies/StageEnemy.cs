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
        void Start()
        {
            var player = FindFirstObjectByType<SideViewMotor>(); target = player ? player.transform : null;
            body = GetComponent<Rigidbody>(); health = GetComponent<CombatHealth>();
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
            var point = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * (boss ? 2.5f : 1.5f));
            if (point.z <= 0 || point.x < 0 || point.x > Screen.width) return;
            var width = boss ? 160 : 55;
            GUI.color = Color.black; GUI.DrawTexture(new Rect(point.x - width / 2, Screen.height - point.y, width, 7), Texture2D.whiteTexture);
            GUI.color = Windup ? Color.yellow : new Color(.95f, .35f, .25f); GUI.DrawTexture(new Rect(point.x - width / 2, Screen.height - point.y, width * health.Current / health.maxHealth, 7), Texture2D.whiteTexture);
            GUI.color = Color.white;
            if (Windup) GUI.Label(new Rect(point.x - 40, Screen.height - point.y - 25, 100, 25), "! ATTACK");
        }
    }
}
