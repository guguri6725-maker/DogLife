using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DogLife
{
    public sealed class TitleScreen : MonoBehaviour
    {
        [SerializeField] private string springScene = "Assets/Scenes/Stage1_Spring.unity";
        [SerializeField] private Button startButton;
        [SerializeField] private Text status;
        // 전시용 단독 체험은 전투 안내를 확인한 뒤 선택한 계절로 이동합니다.
        [SerializeField] private string[] seasonDemoScenes = {
            "Assets/Scenes/Stage1_Spring.unity", "Assets/Scenes/Stage3_Summer.unity",
            "Assets/Scenes/Stage3_Fall.unity", "Assets/Scenes/Stage4_Winter.unity" };
        bool loading;
        AudioClip titleMusic;
        void Start(){ResumeMenuMusic();SeasonTrialRecords.AttachTitle(this);}
        public void ResumeMenuMusic()
        {
            if(loading||!isActiveAndEnabled||gameObject.scene.name!="Title")return;
            if(!titleMusic)titleMusic=Resources.Load<AudioClip>("Audio/Music/Title_BGM");
            if(titleMusic)GameAudio.PlayMusic(titleMusic,this);
        }
        void OnDisable(){GameAudio.StopMusic(this);}

        public void LoadGame()
        {
            if (loading || GameSettingsWindow.BlocksGameplayInput) return;
            string scene;
            if(!SeasonRunSave.PrepareLoad(out scene)){status.text = "저장된 게임이 없습니다.";return;}
            loading=true;startButton.interactable=false;status.text="저장된 계절로 향하는 중...";
            SeasonLoadingScreen.Load(scene);
        }

        public void OpenOptions()
        {
            if (loading || StoryBook.BlocksInput) return;
            var settings = GameSettingsWindow.Instance;
            if (settings) settings.Open();
        }

        public void ExitGame()
        {
            if (loading || GameSettingsWindow.BlocksGameplayInput) return;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void Awake()
        {
            Time.timeScale = 1;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            EnsureResetButton();
        }

        // 전시회 빌드 전용: 다음 관람객을 위해 저장된 진행도·발자국을 전부 지웁니다.
        void EnsureResetButton()
        {
            if (transform.Find("Reset Data Button")) return;
            var font = Font.CreateDynamicFontFromOSFont("Malgun Gothic", 22);
            var root = new GameObject("Reset Data Button", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 500;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            var anchor = AdventureUI.Rect("Anchor", root.transform, Vector2.zero, Vector2.zero);
            anchor.anchorMin = anchor.anchorMax = new Vector2(1, 0); anchor.anchoredPosition = new Vector2(-150, 60);
            AdventureUI.Button(anchor, "Reset", "데이터 초기화", Vector2.zero, new Vector2(240, 60), font, ResetAllData);
        }

        public void ResetAllData()
        {
            if (loading || GameSettingsWindow.BlocksGameplayInput) return;
            PlayerPrefs.DeleteKey(SeasonRunSave.Key);
            PlayerPrefs.Save();
            MetaProgress.ResetAll();
            RunEconomy.ClearCarry();
            if (status) status.text = "모든 데이터가 초기화되었습니다.";
        }

        public void StartGame()
        {
            if (loading || GameSettingsWindow.BlocksGameplayInput) return;
            if (!Application.CanStreamedLevelBeLoaded(springScene))
            {
                status.text = "봄 스테이지를 불러올 수 없습니다.";
                Debug.LogError("Missing spring scene: " + springScene, this);
                return;
            }
            loading = true;
            startButton.interactable = false;
            // 안내는 타이틀 진입점에만 두어 일반 진행의 계절 전환에는 반복되지 않습니다.
            if (!Application.CanStreamedLevelBeLoaded(EnemyIntroduction.ScenePath))
            {
                loading = false;
                startButton.interactable = true;
                status.text = "모험 안내 화면을 불러올 수 없습니다.";
                return;
            }
            EnemyIntroduction.Prepare(springScene, false);
            SceneManager.LoadSceneAsync(EnemyIntroduction.ScenePath);
        }

        public void OpenMemories()
        {
            if(loading||GameSettingsWindow.BlocksGameplayInput)return;
            if(StoryBook.Instance)StoryBook.Instance.OpenGallery();
        }

        // 조작 연습은 새 게임/이어하기와 분리하며 저장 데이터를 쓰지 않습니다.
        public void OpenTutorial()
        {
            if (loading || GameSettingsWindow.BlocksGameplayInput || StoryBook.BlocksInput) return;
            if (!Application.CanStreamedLevelBeLoaded(PracticeTutorial.ScenePath))
            { status.text = "조작 연습 씬을 불러올 수 없습니다."; return; }
            loading = true;
            startButton.interactable = false;
            SeasonLoadingScreen.Load(PracticeTutorial.ScenePath);
        }

        // 전시용 버튼: 진행 상황을 저장/불러오지 않고 선택한 계절 스테이지만 단독으로 체험합니다.
        public void PlaySeasonDemo(int season)
        {
            if (loading || GameSettingsWindow.BlocksGameplayInput) return;
            if (season < 0 || season >= seasonDemoScenes.Length) return;
            string scene = seasonDemoScenes[season];
            if (!Application.CanStreamedLevelBeLoaded(scene))
            {
                status.text = "스테이지를 불러올 수 없습니다.";
                Debug.LogError("Missing demo scene: " + scene, this);
                return;
            }
            if (!Application.CanStreamedLevelBeLoaded(EnemyIntroduction.ScenePath))
            {
                status.text = "모험 안내 화면을 불러올 수 없습니다.";
                return;
            }
            loading = true;
            startButton.interactable = false;
            SeasonTrialRecords.Prepare(season,scene);
            EnemyIntroduction.Prepare(scene, true);
            SceneManager.LoadSceneAsync(EnemyIntroduction.ScenePath);
        }

    }
}
