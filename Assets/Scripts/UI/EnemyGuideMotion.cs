using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Playables;
using UnityEngine.Animations;

namespace DogLife
{
    // 모델·카메라·머티리얼·UI 참조는 씬 Inspector에 직접 연결합니다.
    public sealed class EnemyGuideMotion : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        [SerializeField] Animator model;
        [SerializeField] Transform motionRoot;
        [SerializeField] Camera previewCamera;
        [SerializeField] RawImage stillImage, motionImage;
        [SerializeField] CanvasGroup guide;
        [SerializeField] Text phaseLabel;
        [SerializeField] AnimationClip idle, first, second, third, fourth;
        [SerializeField] Vector4 durations = new Vector4(.8f, .6f, .5f, .7f);
        [SerializeField] string firstLabel, secondLabel, thirdLabel, fourthLabel;
        [SerializeField] Vector3 travel;
        [SerializeField] Transform groundEffect, projectile;
        [SerializeField] Vector3 projectileTravel = new Vector3(0, 0, 2);
        [SerializeField] Renderer rangeIndicator;
        [SerializeField, Range(1,4)] int warningStage = 2;
        [SerializeField, Range(1,4)] int attackStage = 3;
        [SerializeField, Range(1,4)] int rangeEndStage = 3;
        [SerializeField, Range(0,1)] float attackMoment;
        [SerializeField] bool slipperyField;
        [SerializeField] Transform[] spikes;
        Vector3[] spikeHomes;
        MaterialPropertyBlock rangeProperties;
        static EnemyGuideMotion active;
        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        AnimationClipPlayable[] clips;
        Vector3 home, effectScale, projectileHome;
        float elapsed, visibility;
        bool playing, initialized;
        public bool IsPreviewPlaying => playing;

        void Start()
        {
            if (!model || !motionRoot || !previewCamera || !stillImage || !motionImage ||
                !idle || !first || !second || !third || !fourth) return;
            home = motionRoot.localPosition;
            if (groundEffect) effectScale = groundEffect.localScale;
            if (projectile) projectileHome = projectile.localPosition;
            initialized = true;
            rangeProperties = new MaterialPropertyBlock();
            spikeHomes = new Vector3[spikes == null ? 0 : spikes.Length];
            for (int i = 0; i < spikeHomes.Length; i++)
                if (spikes[i]) { spikeHomes[i] = spikes[i].localPosition; spikes[i].gameObject.SetActive(false); }
            if (rangeIndicator) rangeIndicator.enabled = false;
            previewCamera.enabled = false;
            motionImage.color = new Color(1, 1, 1, 0);
            if (groundEffect) groundEffect.gameObject.SetActive(false);
            if (projectile) projectile.gameObject.SetActive(false);
            phaseLabel.text = "동작 보기";
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            BeginPreview();
        }
        public void OnPointerExit(PointerEventData eventData)
        {
            if (!EventSystem.current || EventSystem.current.currentSelectedGameObject != gameObject) StopPreview();
        }
        public void OnSelect(BaseEventData eventData) { BeginPreview(); }
        public void OnDeselect(BaseEventData eventData) { StopPreview(); }

        public void BeginPreview()
        {
            if (!initialized || playing || !CanPreview()) return;
            if (active && active != this) active.StopPreview();
            active = this;
            // 실제 애니메이션 클립을 수동 평가해 이벤트·전투 AI 없이 자세만 재생합니다.
            graph = PlayableGraph.Create("Enemy guide " + name);
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            mixer = AnimationMixerPlayable.Create(graph, 5);
            var source = new[] { idle, first, second, third, fourth };
            clips = new AnimationClipPlayable[5];
            for (int i = 0; i < clips.Length; i++)
            {
                clips[i] = AnimationClipPlayable.Create(graph, source[i]);
                clips[i].SetApplyFootIK(false);
                clips[i].SetApplyPlayableIK(false);
                graph.Connect(clips[i], 0, mixer, i);
            }
            AnimationPlayableOutput.Create(graph, "Silhouette", model).SetSourcePlayable(mixer);
            graph.Play();
            elapsed = 0;
            playing = true;
            previewCamera.enabled = true;
            EvaluatePose();
        }

        bool CanPreview() => guide && guide.alpha > .9f && guide.interactable &&
            !StoryBook.BlocksInput && !GameSettingsWindow.BlocksGameplayInput;

        void Update()
        {
            if (!initialized) return;
            if (playing && !CanPreview()) StopPreview();
            if (playing) { elapsed += Time.unscaledDeltaTime; EvaluatePose(); }
            // 정지 그림과 움직이는 실루엣 사이를 짧게 페이드합니다.
            visibility = Mathf.MoveTowards(visibility, playing ? 1 : 0, Time.unscaledDeltaTime * 6);
            motionImage.color = new Color(1, 1, 1, visibility);
            stillImage.color = new Color(1, 1, 1, 1 - visibility);
        }

        void EvaluatePose()
        {
            float total = .55f + durations.x + durations.y + durations.z + durations.w;
            float t = Mathf.Repeat(elapsed, total), remaining = t;
            int stage = 0;
            while (stage < 4 && remaining >= Duration(stage)) { remaining -= Duration(stage); stage++; }
            float p = Mathf.Clamp01(remaining / Duration(stage));
            int previous = (stage + 4) % 5;
            float blend = Mathf.SmoothStep(0, 1, remaining / .16f);
            for (int i = 0; i < clips.Length; i++) mixer.SetInputWeight(i, 0);
            clips[previous].SetTime(Clip(previous).length * .98f);
            clips[stage].SetTime(Clip(stage).length * p);
            mixer.SetInputWeight(previous, 1 - blend);
            mixer.SetInputWeight(stage, blend);
            graph.Evaluate(0);
            float move = stage == 0 ? 0 : stage == 1 ? Mathf.SmoothStep(0, 1, p) : stage < 4 ? 1 : 1 - Mathf.SmoothStep(0, 1, p);
            motionRoot.localPosition = home + travel * move;
            phaseLabel.text = stage == 0 ? "동작 살펴보기" : stage == 1 ? firstLabel : stage == 2 ? secondLabel : stage == 3 ? thirdLabel : fourthLabel;
            if (groundEffect)
            {
                bool visible = stage == 3 || stage == 4;
                groundEffect.gameObject.SetActive(visible);
                groundEffect.localScale = effectScale * (stage == 3 ? Mathf.SmoothStep(.05f, 1, p) : 1 - Mathf.SmoothStep(0, 1, p));
            }
            if (projectile)
            {
                bool visible = stage == 2 && p > .55f;
                projectile.gameObject.SetActive(visible);
                projectile.localPosition = projectileHome + projectileTravel * Mathf.Clamp01((p - .55f) / .45f);
            }
            if (rangeIndicator)
            {
                // 경고를 먼저 채운 뒤 실제 공격 순간에 붉게 전환합니다.
                bool visible = stage >= warningStage && stage <= rangeEndStage;
                bool fired = stage > attackStage || stage == attackStage && p >= attackMoment;
                float progress = stage > attackStage ? 1 : stage == attackStage ? (attackMoment > 0 ? Mathf.Clamp01(p / attackMoment) : 1) : p;
                rangeIndicator.enabled = visible;
                rangeProperties.SetColor("_Color", fired ? new Color(1,.22f,.12f,.8f) : new Color(1,.65f,.18f,.65f));
                rangeProperties.SetFloat("_Progress", progress);
                // 빙판은 타격 경고와 구분하고, 생성 후 같은 자리에 남는 이동 방해 구역으로 보여줍니다.
                rangeProperties.SetFloat("_Slippery", slipperyField ? 1 : 0);
                if (slipperyField)
                {
                    float age = t - (.55f + durations.x + durations.y * attackMoment);
                    float growth = fired ? Mathf.SmoothStep(0, 1, Mathf.Clamp01(age / .65f)) : 0;
                    float fade = stage == 4 ? 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01((p - .8f) / .2f)) : 1;
                    rangeProperties.SetColor("_Color", new Color(.28f,.85f,1, (fired ? .85f : .4f) * fade));
                    rangeProperties.SetFloat("_Progress", growth);
                }
                rangeIndicator.SetPropertyBlock(rangeProperties);
            }
            for (int i = 0; i < spikeHomes.Length; i++)
            {
                if (!spikes[i]) continue;
                float rise = stage == 3 ? Mathf.SmoothStep(0,1,p/.35f) : stage == 4 ? 1-Mathf.SmoothStep(0,1,p/.6f) : 0;
                spikes[i].gameObject.SetActive(rise > 0);
                spikes[i].localPosition = spikeHomes[i] + Vector3.down * (1-rise) * 1.4f;
            }
        }

        float Duration(int stage) => Mathf.Max(.2f, stage == 0 ? .55f : durations[stage - 1]);
        AnimationClip Clip(int stage) => stage == 0 ? idle : stage == 1 ? first : stage == 2 ? second : stage == 3 ? third : fourth;

        public void StopPreview()
        {
            playing = false;
            if (graph.IsValid()) graph.Destroy();
            if (previewCamera) previewCamera.enabled = false;
            if (initialized && motionRoot) motionRoot.localPosition = home;
            if (rangeIndicator) rangeIndicator.enabled = false;
            if (spikes != null) foreach (var spike in spikes) if (spike) spike.gameObject.SetActive(false);
            if (groundEffect) groundEffect.gameObject.SetActive(false);
            if (projectile) projectile.gameObject.SetActive(false);
            if (phaseLabel) phaseLabel.text = "동작 보기";
            if (active == this) active = null;
        }
        void OnDisable() { StopPreview(); }
        void OnDestroy() { StopPreview(); }
    }
}
