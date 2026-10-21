using UnityEngine;
using UnityEngine.Rendering;

namespace DogLife
{
    // 대시 중의 실제 자세를 고정된 메시 풀에 저장합니다. 잔상 오브젝트는 생성하지 않습니다.
    [DefaultExecutionOrder(200)]
    public sealed class PlayerDashAfterimage : MonoBehaviour
    {
        [SerializeField] PlayerMotor motor;
        [SerializeField] SkinnedMeshRenderer[] sources;
        [SerializeField] Material material;
        [SerializeField, Range(2, 10)] int capacity = 6;
        [SerializeField, Min(.02f)] float sampleInterval = .045f;
        [SerializeField, Min(.05f)] float lifetime = .24f;
        [SerializeField, Min(.02f)] float minimumDistance = .15f;
        [SerializeField] Color tint = new Color(.48f, .87f, 1f, .42f);
        Mesh[,] meshes;
        Matrix4x4[,] matrices;
        bool[,] visible;
        float[] born;
        MaterialPropertyBlock properties;
        CombatHealth health;
        int next;
        float nextSample;
        Vector3 lastPosition;
        bool wasDashing;
        static readonly int ColorId = Shader.PropertyToID("_Color");

        void Awake()
        {
            health = GetComponent<CombatHealth>();
            int count = sources == null ? 0 : sources.Length;
            meshes = new Mesh[capacity, count];
            matrices = new Matrix4x4[capacity, count];
            visible = new bool[capacity, count];
            born = new float[capacity];
            properties = new MaterialPropertyBlock();
            for (int i = 0; i < capacity; i++)
            {
                born[i] = float.NegativeInfinity;
                for (int j = 0; j < count; j++)
                {
                    meshes[i, j] = new Mesh { name = "Dash afterimage (pooled)" };
                    meshes[i, j].MarkDynamic();
                }
            }
        }

        void LateUpdate()
        {
            if (!motor || !material || meshes == null) return;
            if (health && health.Dead) { Clear(); return; }
            bool dashing = motor.isActiveAndEnabled && motor.IsDashing;
            if (dashing && !wasDashing) nextSample = 0;
            // 벽에 막혀 제자리에 있을 때는 같은 위치에 잔상을 겹쳐 만들지 않습니다.
            if (dashing && Time.time >= nextSample && (!wasDashing ||
                (transform.position - lastPosition).sqrMagnitude >= minimumDistance * minimumDistance))
            {
                for (int j = 0; j < sources.Length; j++)
                {
                    var source = sources[j];
                    visible[next, j] = source && source.enabled && source.gameObject.activeInHierarchy;
                    if (!visible[next, j]) continue;
                    // 앉은 대시의 낮아진 몸과 목도리까지 현재 스킨 자세에 함께 굽습니다.
                    source.BakeMesh(meshes[next, j], false);
                    // 앉기 축소와 임포트 배율이 메시 안에 반영되므로 여기서 크기를 다시 곱하지 않습니다.
                    matrices[next, j] = Matrix4x4.TRS(source.transform.position, source.transform.rotation, Vector3.one);
                }
                born[next] = Time.time;
                next = (next + 1) % born.Length;
                lastPosition = transform.position;
                nextSample = Time.time + sampleInterval;
            }
            wasDashing = dashing;
            for (int i = 0; i < born.Length; i++)
            {
                float age = Time.time - born[i];
                if (age >= lifetime) continue;
                var color = tint;
                color.a *= Mathf.Pow(1 - Mathf.Clamp01(age / lifetime), 1.6f);
                properties.SetColor(ColorId, color);
                for (int j = 0; j < sources.Length; j++)
                {
                    if (!visible[i, j]) continue;
                    var mesh = meshes[i, j];
                    for (int sub = 0; sub < mesh.subMeshCount; sub++)
                        Graphics.DrawMesh(mesh, matrices[i, j], material, gameObject.layer, null, sub,
                            properties, ShadowCastingMode.Off, false, null, LightProbeUsage.Off);
                }
            }
        }

        void Clear()
        {
            wasDashing = false;
            if (born != null) for (int i = 0; i < born.Length; i++) born[i] = float.NegativeInfinity;
        }
        void OnDisable() { Clear(); }
        void OnDestroy()
        {
            if (meshes == null) return;
            foreach (var mesh in meshes) if (mesh) Destroy(mesh);
        }
    }
}
