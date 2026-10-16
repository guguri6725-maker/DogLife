using System.Collections;
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
        // 전시용 단독 체험 진입점. 저장/이어하기 체계를 거치지 않고 해당 씬을 그대로 띄웁니다.
        [SerializeField] private string[] seasonDemoScenes = {
            "Assets/Scenes/Stage1_Spring.unity", "Assets/Scenes/Stage3_Summer.unity",
            "Assets/Scenes/Stage3_Fall.unity", "Assets/Scenes/Stage4_Winter.unity" };
        bool loading;
        AudioClip titleMusic;
        void Start(){ResumeMenuMusic();}
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
            if(StoryBook.Instance)StoryBook.Instance.BeginNewGame(()=>StartCoroutine(LoadSpring()));
            else StartCoroutine(LoadSpring());
        }

        public void OpenMemories()
        {
            if(loading||GameSettingsWindow.BlocksGameplayInput)return;
            if(StoryBook.Instance)StoryBook.Instance.OpenGallery();
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
            SeasonLoadingScreen.Load(scene);
        }

        IEnumerator LoadSpring()
        {
            SeasonRunSave.NewGame();
            status.text = "봄의 숲으로 향하는 중...";
            yield return null;
            SeasonLoadingScreen.Load(springScene);
        }
    }
}
