using UnityEngine;

namespace DogLife
{
    // 공용 물 머티리얼을 복제하지 않고 이 문의 폭포만 개방 진행도에 맞춰 투명하게 만듭니다.
    [ExecuteAlways]
    public sealed class WaterGateFade : MonoBehaviour
    {
        public VineGate gate;
        public Renderer[] waterfalls = new Renderer[0];
        [Range(0f, 1f)] public float editorPreview;
        static readonly int Opacity = Shader.PropertyToID("_Opacity");
        MaterialPropertyBlock block;
        float previous = -1f;

        void OnEnable() { previous = -1f; }
        void OnValidate() { previous = -1f; }

        void LateUpdate()
        {
            float progress = Application.isPlaying && gate ? gate.Progress : editorPreview;
            if (Mathf.Approximately(previous, progress)) return;
            previous = progress;
            if (block == null) block = new MaterialPropertyBlock();
            float visibility = 1f - Mathf.SmoothStep(0f, 1f, progress);
            foreach (var waterfall in waterfalls)
            {
                if (!waterfall || !waterfall.sharedMaterial) continue;
                // 기존 강/폭포의 머티리얼 설정은 유지하고 이 렌더러의 불투명도만 덮어씁니다.
                waterfall.GetPropertyBlock(block);
                block.SetFloat(Opacity, waterfall.sharedMaterial.GetFloat(Opacity) * visibility);
                waterfall.SetPropertyBlock(block);
                waterfall.enabled = progress < 1f;
            }
        }

        void OnDisable()
        {
            if (block == null) return;
            foreach (var waterfall in waterfalls)
            {
                if (!waterfall || !waterfall.sharedMaterial) continue;
                waterfall.GetPropertyBlock(block);
                block.SetFloat(Opacity, waterfall.sharedMaterial.GetFloat(Opacity));
                waterfall.SetPropertyBlock(block);
                waterfall.enabled = true;
            }
        }
    }
}
