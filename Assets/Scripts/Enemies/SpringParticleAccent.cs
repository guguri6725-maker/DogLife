using UnityEngine;

namespace DogLife
{
    // Hierarchy에 연결한 입자만 재생하며, 공격 판정이나 타이밍을 변경하지 않습니다.
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class SpringParticleAccent : MonoBehaviour
    {
        [SerializeField] ParticleSystem pollen, dust;
        [SerializeField, Range(0, 48)] int pollenCount = 20;
        [SerializeField, Range(0, 20)] int dustCount = 8;
        ParticleSystem source;
        bool wasPlaying;
        float previousTime;
        void Awake() { source = GetComponent<ParticleSystem>(); }
        void LateUpdate()
        {
            bool playing = source && source.isPlaying;
            if (playing && (!wasPlaying || source.time + .02f < previousTime))
            {
                if (pollen) pollen.Emit(pollenCount);
                if (dust) dust.Emit(dustCount);
            }
            wasPlaying = playing;
            previousTime = source ? source.time : 0;
        }
        void OnDisable()
        {
            wasPlaying = false;
            if (pollen) pollen.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (dust) dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
