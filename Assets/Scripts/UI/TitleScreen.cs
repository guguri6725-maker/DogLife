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
        bool loading;

        public void LoadGame()
        {
            if (loading || GameSettingsWindow.BlocksGameplayInput) return;
            status.text = "저장된 게임이 없습니다.";
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

        IEnumerator LoadSpring()
        {
            status.text = "봄의 숲으로 향하는 중...";
            yield return null;
            yield return SceneManager.LoadSceneAsync(springScene, LoadSceneMode.Single);
        }
    }
}
