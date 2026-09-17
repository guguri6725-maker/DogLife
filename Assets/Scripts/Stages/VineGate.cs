using UnityEngine;
using UnityEngine.AI;

namespace DogLife
{
    // Blender에서 만든 개방 클립을 정방향/역방향으로 재생합니다. 참조는 Inspector에서 연결합니다.
    public sealed class VineGate : MonoBehaviour
    {
        public GameObject animationRoot, visualRoot;
        public AnimationClip openingClip;
        public Collider[] blockers;
        public NavMeshObstacle[] navigationBlockers;
        public bool startOpen, hideWhenInitiallyOpen;
        [Min(.1f)] public float duration = 2.4f;
        public float Progress { get; private set; }
        public bool IsOpen => Progress >= 1f;
        public bool IsClosed => Progress <= 0f;
        float target;

        void Start()
        {
            Progress = target = startOpen ? 1 : 0;
            ApplyPose();
            if (visualRoot && startOpen && hideWhenInitiallyOpen) visualRoot.SetActive(false);
        }
        public void Open() { SetTarget(1); }
        public void Close() { SetTarget(0); }
        void SetTarget(float value)
        {
            if (target == value) return;
            target = value;
            if (visualRoot) visualRoot.SetActive(true);
            ApplyBlockers();
        }
        void Update()
        {
            if (Mathf.Approximately(Progress, target)) return;
            Progress = Mathf.MoveTowards(Progress, target, Time.deltaTime / Mathf.Max(.1f, duration));
            ApplyPose();
        }
        void ApplyPose()
        {
            if (openingClip && animationRoot) openingClip.SampleAnimation(animationRoot, Progress * openingClip.length);
            ApplyBlockers();
        }
        void ApplyBlockers()
        {
            // 완전히 열린 뒤에 통과시키고, 닫기 시작하면 즉시 역주행을 막습니다.
            bool blocked = target < 1 || !IsOpen;
            foreach (var item in blockers) if (item) item.enabled = blocked;
            foreach (var item in navigationBlockers) if (item) item.enabled = blocked;
        }
    }
}
