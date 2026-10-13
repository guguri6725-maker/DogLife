using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DogLife
{
    // A persistent overlay covers scene activation AND the streamed first room.
    public sealed class SeasonLoadingScreen : MonoBehaviour
    {
        static SeasonLoadingScreen instance;
        public static bool IsLoading => instance && instance.busy;
        [SerializeField] float fadeOutSeconds = .4f, fadeInSeconds = .65f, minimumSeconds = 1.2f;
        // 화면 구성과 버튼 이벤트는 로딩 프리팹의 Inspector에서 연결합니다.
        [SerializeField] CanvasGroup group;
        [SerializeField] Text heading, caption, chapterLabel;
        [SerializeField] Button returnButton;
        [SerializeField] CanvasGroup[] paws;
        [SerializeField] RawImage landscape;
        [SerializeField] Texture[] seasonArt;
        [SerializeField] Image accentLine;
        bool busy, failed;
        float started;
        public static string LastReadyScene { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { instance = null; LastReadyScene = null; }

        public static bool Load(string path)
        {
            if (IsLoading || !Application.CanStreamedLevelBeLoaded(path)) return false;
            if (!instance)
            {
                var prefab = Resources.Load<SeasonLoadingScreen>("UI/SeasonLoadingScreen");
                if (!prefab) { Debug.LogError("계절 로딩 화면 프리팹이 없습니다."); return false; }
                instance = Instantiate(prefab);
                DontDestroyOnLoad(instance.gameObject);
            }
            // 이전 전환 뒤 숨긴 프리팹을 다음 로딩 전에 다시 활성화합니다.
            instance.gameObject.SetActive(true);
            instance.StartCoroutine(instance.Transition(path));
            return true;
        }

        IEnumerator Transition(string path)
        {
            busy = true; failed = false; started = Time.unscaledTime;
            returnButton.gameObject.SetActive(false);
            group.gameObject.SetActive(true);
            group.blocksRaycasts = true;
            bool title = path.EndsWith("Title.unity");
            int season = System.Array.IndexOf(SeasonRunSave.Scenes, path);
            string[] names = { "봄의 숲", "여름의 바다", "가을의 길", "겨울의 설원" };
            Color[] accents = { new Color(.94f,.68f,.72f), new Color(.51f,.8f,.88f), new Color(.9f,.66f,.4f), new Color(.73f,.83f,.94f) };
            heading.text = season >= 0 ? names[season] : "DOG LIFE";
            // 계절색은 작은 장식에만 사용하고 제목은 상아색으로 가독성을 유지합니다.
            accentLine.color = season >= 0 ? accents[season] : new Color(.88f,.79f,.61f);
            chapterLabel.text = season >= 0 ? new[]{"I  /  SPRING", "II  /  SUMMER", "III  /  AUTUMN", "IV  /  WINTER"}[season] : "THE JOURNEY HOME";
            if (season >= 0 && season < seasonArt.Length) landscape.texture = seasonArt[season];
            caption.text = title ? "여정을 추억하며..." : season == 1 ? "여름의 바다로 향하는 중..." : heading.text + "으로 향하는 중...";
            Time.timeScale = 0;
            yield return Fade(1, fadeOutSeconds);
            // Present the opaque overlay before expensive scene activation begins.
            yield return null;
            var operation = SceneManager.LoadSceneAsync(path);
            if (operation == null) { Fail("장면을 불러오지 못했습니다."); yield break; }
            yield return operation;
            yield return null; // let destination Start methods register the streamer
            if (season >= 0)
            {
                var stream = ForestRoomStreaming.Instance;
                if (!stream || stream.gameObject.scene.path != path)
                { Fail("첫 방 정보를 찾지 못했습니다."); yield break; }
                while (!stream.Ready)
                {
                    if (!string.IsNullOrEmpty(stream.LoadError))
                    { Fail("첫 방을 준비하지 못했습니다."); yield break; }
                    yield return null;
                }
            }
            // Render the newly prepared world behind the overlay for a few frames.
            for (int i = 0; i < 3; i++) yield return null;
            while (Time.unscaledTime - started < minimumSeconds) yield return null;
            LastReadyScene = path;
            caption.text = "준비 완료";
            yield return Fade(0, fadeInSeconds);
            group.blocksRaycasts = false;
            group.gameObject.SetActive(false);
            busy = false;
            Time.timeScale = 1;
            Cursor.lockState = title ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = title;
        }

        IEnumerator Fade(float target, float duration)
        {
            float from = group.alpha;
            for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
            { group.alpha = Mathf.Lerp(from, target, Mathf.SmoothStep(0, 1, t / duration)); yield return null; }
            group.alpha = target;
        }

        void Fail(string message)
        {
            failed = true; caption.text = message;
            returnButton.gameObject.SetActive(true);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }

        void LateUpdate()
        {
            if (!busy) return;
            Time.timeScale = 0; // destination Awake must not release gameplay early
            for (int i = 0; i < paws.Length; i++)
                paws[i].alpha = .18f + .82f * Mathf.Pow(Mathf.Max(0, Mathf.Sin((Time.unscaledTime-started)*3.4f-i*.8f)), 2);
            if (!failed) { Cursor.lockState = CursorLockMode.None; Cursor.visible = false; }
        }

        // 오류 안내 버튼에 Inspector의 OnClick으로 연결합니다.
        public void ReturnToTitle()
        {
            busy=false;
            Load("Assets/Scenes/Title.unity");
        }
    }
}
