using UnityEngine;
namespace DogLife
{
    // 봄 씬에만 배치하며 카르마 원본은 Inspector에서 직접 연결합니다.
    public sealed class SpringMonsterKarma : MonoBehaviour
    {
        [SerializeField] PlayerKarma source;
        [SerializeField, Range(0, .5f)] float neutralZone = .08f;
        [SerializeField, Min(.1f)] float transitionSeconds = 2;
        [SerializeField, Range(0, 1)] float intensity = 1;
        float current;
        static readonly int Karma = Shader.PropertyToID("_SpringMonsterKarma");
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetGlobal() { Shader.SetGlobalFloat(Karma, 0); }
        void OnEnable() { current = 0; Shader.SetGlobalFloat(Karma, 0); }
        void Update()
        {
            float ratio = source ? source.Value / source.Maximum : 0;
            float target = Mathf.Sign(ratio) * Mathf.SmoothStep(0, 1,
                Mathf.InverseLerp(neutralZone, 1, Mathf.Abs(ratio))) * intensity;
            current = Mathf.MoveTowards(current, target, Time.unscaledDeltaTime / Mathf.Max(.1f, transitionSeconds));
            Shader.SetGlobalFloat(Karma, current);
        }
        // 다음 계절과 전투 안내 씬에 봄의 외형 상태가 남지 않도록 초기화합니다.
        void OnDisable() { Shader.SetGlobalFloat(Karma, 0); }
    }
}
