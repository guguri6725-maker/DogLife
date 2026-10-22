using UnityEngine;
using UnityEngine.UI;

namespace DogLife
{
    // 배치·그림·텍스트·버튼 이벤트는 EnemyIntroduction 씬의 Hierarchy에 저장합니다.
    public sealed class EnemyIntroduction : MonoBehaviour
    {
        public const string ScenePath = "Assets/Scenes/EnemyIntroduction.unity";
        [SerializeField] CanvasGroup guide;
        [SerializeField] Button continueButton;
        [SerializeField] Text status;
        [SerializeField] string springScene = "Assets/Scenes/Stage1_Spring.unity";
        bool continuing;
        static string pendingScene;
        static bool pendingDemo;
        string destination;
        bool seasonDemo;

        // 타이틀에서 선택한 진입 경로를 다음 안내 씬 한 번에만 전달합니다.
        public static void Prepare(string scene, bool demo)
        {
            pendingScene = scene;
            pendingDemo = demo;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetEntry() { pendingScene = null; pendingDemo = false; }

        void Awake()
        {
            destination = string.IsNullOrEmpty(pendingScene) ? springScene : pendingScene;
            seasonDemo = pendingDemo;
            ResetEntry();
        }

        void Start()
        {
            Time.timeScale = 1;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            // 기존 버튼의 문구만 바꾸며 OnClick 연결은 씬 Inspector 설정을 유지합니다.
            if (seasonDemo && continueButton)
            {
                var label = continueButton.GetComponentInChildren<Text>();
                if (label) label.text = "계절 체험 시작";
            }
            var music = Resources.Load<AudioClip>("Audio/Music/Title_BGM");
            if (music) GameAudio.PlayMusic(music, this);
        }

        void OnDisable() { GameAudio.StopMusic(this); }

        public void ContinueToStory()
        {
            // 연속 클릭으로 프롤로그나 로딩이 두 번 시작되지 않도록 막습니다.
            if (continuing || StoryBook.BlocksInput || GameSettingsWindow.BlocksGameplayInput) return;
            if (!Application.CanStreamedLevelBeLoaded(destination) ||
                (!seasonDemo && (!StoryBook.Instance || !StoryBook.Instance.Library ||
                StoryBook.Instance.Library.Find("prologue") == null)))
            {
                status.text = "시작 정보를 불러오지 못했습니다. 다시 시도해 주세요.";
                return;
            }
            continuing = true;
            continueButton.interactable = false;
            GameAudio.StopMusic(this);
            guide.alpha = 0;
            guide.blocksRaycasts = false;
            guide.interactable = false;
            if (seasonDemo) BeginAdventure();
            else StoryBook.Instance.BeginNewGame(BeginAdventure);
        }

        void BeginAdventure()
        {
            // 안내 확인 → 프롤로그 종료(건너뛰기 포함) → 기존 로딩 화면 순서입니다.
            // 전시 체험은 선택한 계절로 바로 이동하며 기존 저장을 초기화하지 않습니다.
            if (!seasonDemo) SeasonRunSave.NewGame();
            if (SeasonLoadingScreen.Load(destination)) return;
            continuing = false;
            guide.alpha = 1;
            guide.blocksRaycasts = true;
            guide.interactable = true;
            continueButton.interactable = true;
            status.text = "스테이지를 불러오지 못했습니다. 다시 시도해 주세요.";
        }
    }
}
