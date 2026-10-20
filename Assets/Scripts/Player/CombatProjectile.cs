using System;
using UnityEngine;
namespace DogLife
{
    // 발사체를 이동시키고 가장 먼저 충돌한 유효 대상에 피해를 줍니다.
    public sealed class CombatProjectile : MonoBehaviour
    {
        public Vector3 direction;
        public float speed, damage, range, radius = .09f;
        public Transform owner;
        public bool hostile;
        public bool ignoreFriendlyMonsters;
        public bool dot;
        public GameObject impactEffect;
        public string impactSound;
        [SerializeField, Min(0)] private float playerLaunchSpeed;
        float travelled;
        // 빠른 발사체도 벽을 통과하지 않도록 구간 전체를 SphereCast로 검사합니다.
        void FixedUpdate()
        {
            float step = Mathf.Min(speed * Time.fixedDeltaTime, range - travelled);
            var hits = Physics.SphereCastAll(transform.position, radius, direction, step, ~0, QueryTriggerInteraction.Collide);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (!MonsterHurtbox.Accepts(hit.collider) || (owner && hit.transform.IsChildOf(owner))) continue;
                // 플레이어 전용 얼음벽은 적의 발사체도 통과하며, 플레이어 공격으로는 계속 부술 수 있습니다.
                if(hostile&&hit.collider.TryGetComponent<FrozenKnightConstruct>(out var iceWall)&&iceWall.IsPillar)continue;
                var health = hit.collider.GetComponentInParent<CombatHealth>();
                if(ignoreFriendlyMonsters&&hostile&&health&&!health.player)continue;
                if (health && health.player == hostile)
                {
                    float before=health.Current;
                    CombatFeedback.DealDamage(health,damage,hit.collider.ClosestPoint(transform.position+direction*hit.distance),direction,1.2f,null,new Color(1,.55f,.73f,1));
                    if (dot) health.ApplyDot();
                    if (health.player && !health.Dead && health.Current<before && playerLaunchSpeed>0)
                    {
                        var motor=health.GetComponent<PlayerMotor>();
                        if(motor)motor.ApplyKnockback(Vector3.zero,0,playerLaunchSpeed,.35f);
                    }
                }
                if(!string.IsNullOrEmpty(impactSound))GameAudio.Play(impactSound,transform.position+direction*Mathf.Clamp(hit.distance,0,step),.8f);
                if (impactEffect)
                {
                    // SphereCast 접촉면 대신 충돌 순간의 탄환 중심에서 터뜨립니다.
                    // 시작부터 겹친 충돌의 hit.point가 원점이어도 효과가 멀리 생기지 않습니다.
                    Vector3 forward = direction.sqrMagnitude > .0001f ? direction.normalized : transform.forward;
                    Vector3 impactPosition = transform.position + forward * Mathf.Clamp(hit.distance, 0, step);
                    Instantiate(impactEffect, impactPosition - forward * .04f, Quaternion.LookRotation(-forward));
                }
                Destroy(gameObject);
                return;
            }
            transform.position += direction * step;
            travelled += step;
            if (travelled >= range) Destroy(gameObject);
        }
    }
}
