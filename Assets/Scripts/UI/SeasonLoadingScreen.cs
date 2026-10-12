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
        CanvasGroup group;
        Text heading, caption;
        Button returnButton;
        CanvasGroup[] paws;
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
                var go = new GameObject("Season Loading Screen");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<SeasonLoadingScreen>();
                instance.Build();
            }
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
            heading.color = season >= 0 ? accents[season] : new Color(.88f,.79f,.61f);
            caption.text = title ? "여정을 추억하며..." : heading.text + "으로 향하는 중...";
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

        void Build()
        {
            var canvasObject = new GameObject("Overlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32760;
            var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920,1080); scaler.matchWidthOrHeight = .5f;
            group = canvasObject.GetComponent<CanvasGroup>(); group.alpha = 0;
            var background = new GameObject("Midnight", typeof(RectTransform), typeof(Image)); background.transform.SetParent(canvasObject.transform,false);
            var rect = background.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.sizeDelta = Vector2.zero;
            background.GetComponent<Image>().color = new Color(.035f,.047f,.06f);
            var font = StoryBook.Instance && StoryBook.Instance.Library ? StoryBook.Instance.Library.font : null;
            if (!font) font = Font.CreateDynamicFontFromOSFont(new[]{"Malgun Gothic","Arial"},32);
            Label("Brand", "D O G   L I F E", 22, new Vector2(0,145), font).color = new Color(.66f,.61f,.51f);
            heading = Label("Season", "", 48, new Vector2(0,62), font);
            caption = Label("Status", "", 23, new Vector2(0,-32), font); caption.color = new Color(.74f,.76f,.77f);
            paws = new CanvasGroup[4];
            for(int i=0;i<paws.Length;i++)
            {
                var paw = new GameObject("Paw "+i, typeof(RectTransform), typeof(CanvasGroup), typeof(LoadingPawGraphic));
                paw.transform.SetParent(canvasObject.transform,false);
                var pr=paw.GetComponent<RectTransform>(); pr.sizeDelta=new Vector2(34,38); pr.anchoredPosition=new Vector2((i-1.5f)*54,-115+(i%2)*8);
                paw.GetComponent<LoadingPawGraphic>().color=new Color(.88f,.79f,.61f);
                paws[i]=paw.GetComponent<CanvasGroup>();
            }
            var buttonText=Label("Return to title", "타이틀로 돌아가기",24,new Vector2(0,-210),font);
            buttonText.raycastTarget=true;
            returnButton=buttonText.gameObject.AddComponent<Button>(); returnButton.targetGraphic=buttonText;
            returnButton.onClick.AddListener(()=>{busy=false;Load("Assets/Scenes/Title.unity");});
        }

        Text Label(string name,string value,int size,Vector2 position,Font font)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Text)); go.transform.SetParent(group.transform,false);
            var r=go.GetComponent<RectTransform>();r.sizeDelta=new Vector2(1100,80);r.anchoredPosition=position;
            var text=go.GetComponent<Text>();text.font=font;text.fontSize=size;text.text=value;text.alignment=TextAnchor.MiddleCenter;text.raycastTarget=false;return text;
        }
    }
}
