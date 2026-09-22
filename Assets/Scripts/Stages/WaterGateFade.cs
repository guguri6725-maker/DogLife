using UnityEngine;

namespace DogLife
{
    // 문별 진행도로 중앙부터 물 공급을 끊고 남은 물이 아래로 흘러내리게 표현합니다.
    [ExecuteAlways]
    public sealed class WaterGateFade : MonoBehaviour
    {
        public VineGate gate;
        public Renderer[] waterfalls = new Renderer[0];
        [Range(0f, 1f)] public float editorPreview;
        static readonly int Opacity = Shader.PropertyToID("_Opacity");
        static readonly int OpenProgress = Shader.PropertyToID("_OpenProgress");
        static readonly int FlowTime = Shader.PropertyToID("_FlowTime");
        MaterialPropertyBlock block;
        float flowClock;

        void OnEnable() { flowClock = Time.time; }

        void LateUpdate()
        {
            float progress = Application.isPlaying && gate ? gate.Progress : editorPreview;
            // 공급이 끊긴 뒤에도 남은 물은 계속 아래로 흐릅니다. 전체 물막을 얼리지 않습니다.
            if (Application.isPlaying) flowClock += Time.deltaTime;
            if (block == null) block = new MaterialPropertyBlock();
            float visibility = 1f - Mathf.SmoothStep(0f, 1f, progress);
            foreach (var waterfall in waterfalls)
            {
                if (!waterfall || !waterfall.sharedMaterial) continue;
                // 새 문 셰이더는 중앙부터 열고, 기존 물 셰이더는 전체 페이드를 유지합니다.
                waterfall.GetPropertyBlock(block);
                bool centreOpening = waterfall.sharedMaterial.HasProperty(OpenProgress);
                block.SetFloat(Opacity, waterfall.sharedMaterial.GetFloat(Opacity) * (centreOpening ? 1f : visibility));
                if (centreOpening)
                {
                    block.SetFloat(OpenProgress, progress);
                    block.SetFloat(FlowTime, Application.isPlaying ? flowClock : 0f);
                }
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
                block.SetFloat(OpenProgress, 0f);
                block.SetFloat(FlowTime, -1f);
                waterfall.SetPropertyBlock(block);
                waterfall.enabled = true;
            }
        }
    }
}
