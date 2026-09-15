using UnityEngine;
namespace DogLife
{
    // Visible warning first, then one overlap-based 3D damage pulse. Never needs an animation event.
    public sealed class SeaHazard : MonoBehaviour
    {
        public CombatHealth source;
        public Vector3 halfExtents;
        public float warning = 1, activeDuration = .3f, damage = 15;
        public bool safeMarker;
        public bool Fired { get; private set; }
        float born;
        Renderer visual;
        MaterialPropertyBlock block;
        void Start() { born = Time.time; visual = GetComponent<Renderer>(); block = new MaterialPropertyBlock(); }
        void Update()
        {
            if (!source || source.Dead) { Destroy(gameObject); return; }
            float age = Time.time - born;
            if (!safeMarker && age >= warning && !Fired)
            {
                Fired = true;
                var player = FindFirstObjectByType<SideViewMotor>();
                foreach (var collider in Physics.OverlapBox(transform.position, halfExtents, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                    if (player && collider.transform.IsChildOf(player.transform)) { player.GetComponent<CombatHealth>().Damage(damage); break; }
            }
            block.SetColor("_Color", safeMarker ? Color.green : Fired ? Color.red : Color.Lerp(new Color(.15f, .5f, .9f), Color.yellow, Mathf.PingPong(age * 3, 1)));
            visual.SetPropertyBlock(block);
            if (age > warning + activeDuration) Destroy(gameObject);
        }
        public static SeaHazard Create(CombatHealth owner, Vector3 center, Vector3 size, float warning, float damage, Material material, bool safe = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = safe ? "Safe zone - GREEN" : "Sea attack warning";
            var collider = go.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
            go.transform.position = center; go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
            var h = go.AddComponent<SeaHazard>(); h.source = owner; h.halfExtents = size / 2; h.warning = warning; h.damage = damage; h.safeMarker = safe;
            return h;
        }
    }
}
