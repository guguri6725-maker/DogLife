using System;
using UnityEngine;
namespace DogLife
{
    // 발사체를 이동시키고 가장 먼저 충돌한 유효 대상에 피해를 줍니다.
    public sealed class SideViewProjectile : MonoBehaviour
    {
        public Vector3 direction;
        public float speed, damage, range, radius = .09f;
        public Transform owner;
        public bool hostile;
        public bool dot;
        float travelled;
        // 빠른 발사체도 벽을 통과하지 않도록 구간 전체를 SphereCast로 검사합니다.
        void FixedUpdate()
        {
            float step = Mathf.Min(speed * Time.fixedDeltaTime, range - travelled);
            var hits = Physics.SphereCastAll(transform.position, radius, direction, step, ~0, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (owner && hit.transform.IsChildOf(owner)) continue;
                var health = hit.collider.GetComponentInParent<CombatHealth>();
                if (health && health.player == hostile) { health.Damage(damage); if (dot) health.ApplyDot(); }
                Destroy(gameObject);
                return;
            }
            transform.position += direction * step;
            travelled += step;
            if (travelled >= range) Destroy(gameObject);
        }
    }
}
