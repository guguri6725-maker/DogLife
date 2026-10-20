using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations;
using UnityEngine.Playables;

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
        [Range(.05f,1f), Tooltip("중앙 통로가 충분히 열린 시점에 충돌벽을 해제합니다. 1은 애니메이션 끝까지 대기합니다.")]
        public float collisionReleaseProgress = 1f;
        public float Progress { get; private set; }
        public bool IsOpen => Progress >= 1f;
        public bool IsClosed => Progress <= 0f;
        public bool IsPassable => target > 0 && Progress >= collisionReleaseProgress;
        float target;
        bool initialized;
        GameAudio.Handle gateSound;
        PlayableGraph poseGraph;
        AnimationClipPlayable poseClip;

        void Start()
        {
            Progress = target = startOpen ? 1 : 0;
            ApplyPose();
            if (visualRoot && startOpen && hideWhenInitiallyOpen) visualRoot.SetActive(false);
            initialized=true;
        }
        public void Open() { SetTarget(1); }
        public void Close() { SetTarget(0); }
        void SetTarget(float value)
        {
            if (target == value) return;
            target = value;
            gateSound.Stop();
            if(initialized&&SpringSoundscape.IsSpringScene(gameObject.scene))
                gateSound=GameAudio.Play(value>0?"Spring_Gate_Open":"Spring_Gate_Close",transform.position,.75f,2.4f/Mathf.Max(.1f,duration),follow:transform);
            else if(initialized&&WinterSoundscape.IsWinter(gameObject.scene))
                gateSound=GameAudio.Play(value>0?"Winter_Door_Open":"Winter_Door_Close",transform.position,.65f,2.4f/Mathf.Max(.1f,duration),follow:transform);
            else if(initialized&&(gameObject.scene.name=="Stage3_Summer"||gameObject.scene.path.Contains("/SeasonRooms/Summer/")))
                gateSound=GameAudio.Play(value>0?"Summer_Door_Open":"Summer_Door_Close",transform.position,.65f,3f/Mathf.Max(.1f,duration),follow:transform);
            else if(initialized&&FallSoundscape.IsFallScene(gameObject.scene))
                gateSound=GameAudio.Play(value>0?"Autumn_Door_Open":"Autumn_Door_Close",transform.position,.7f,3.2f/Mathf.Max(.1f,duration),follow:transform);
            if (visualRoot) visualRoot.SetActive(true);
            ApplyBlockers();
        }
        void Update()
        {
            // MoveTowards must reach the exact endpoint: an approximate early exit can
            // leave Progress just below 1 while IsOpen and collision still stay closed.
            if (Progress == target) return;
            Progress = Mathf.MoveTowards(Progress, target, Time.deltaTime / Mathf.Max(.1f, duration));
            ApplyPose();
            if(Progress==target)gateSound.Stop();
        }
        void OnDisable(){gateSound.Stop();}
        void ApplyPose()
        {
            if (openingClip && animationRoot)
            {
                if(openingClip.legacy)openingClip.SampleAnimation(animationRoot,Progress*openingClip.length);
                else
                {
                    if(!poseGraph.IsValid())
                    {
                        var animator=animationRoot.GetComponent<Animator>();
                        if(!animator)animator=animationRoot.AddComponent<Animator>();
                        animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                        poseGraph=PlayableGraph.Create("Vine gate pose");
                        poseGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                        poseClip=AnimationClipPlayable.Create(poseGraph,openingClip);
                        poseClip.SetSpeed(0);
                        AnimationPlayableOutput.Create(poseGraph,"Gate",animator).SetSourcePlayable(poseClip);
                        poseGraph.Play();
                    }
                    poseClip.SetTime(Progress*openingClip.length);
                    poseGraph.Evaluate(0);
                }
            }
            ApplyBlockers();
        }
        void OnDestroy(){if(poseGraph.IsValid())poseGraph.Destroy();}
        void ApplyBlockers()
        {
            // 시각적으로 통로가 확보되면 통과시킵니다. 닫을 때는 기존처럼 역주행을 차단합니다.
            bool blocked = !IsPassable;
            foreach (var item in blockers) if (item) item.enabled = blocked;
            foreach (var item in navigationBlockers) if (item) item.enabled = blocked;
        }
    }
}
