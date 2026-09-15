using System;
using UnityEngine;
namespace DogLife
{
    public sealed class SideViewProjectile : MonoBehaviour
    {
        public Vector3 direction;
        public float speed, damage, range, radius = .09f;
        public Transform owner;
        public bool hostile;
        public bool dot;
        float travelled;
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
