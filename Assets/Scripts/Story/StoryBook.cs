using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DogLife
{
    [DefaultExecutionOrder(-250)]
    public sealed partial class StoryBook : MonoBehaviour
    {
        [SerializeField] StoryLibrary library;
        static StoryBook instance;
        static int consumed=-1;
        readonly HashSet<StageOneDirector> pendingClears=new HashSet<StageOneDirector>();
        readonly HashSet<string> entered=new HashSet<string>();
        readonly HashSet<StageOneDirector> presentedClears=new HashSet<StageOneDirector>();
        public static StoryBook Instance=>instance;
        public static bool IsOpen=>instance&&instance.open;
        public static bool BlocksInput=>IsOpen||Time.frameCount==consumed;
        public static bool AwaitingClear=>instance&&instance.pendingClears.Count>0;
        public StoryLibrary Library=>library;
        public string CurrentChapter=>chapter==null?null:chapter.id;
        public int CurrentPage=>page;
        public bool GalleryOpen=>open&&gallery;
        bool open,gallery,replay,automatic;
        float savedTime,started,pageStarted;
        bool savedVisible;
        CursorLockMode savedCursor;
        GameObject savedSelection;
        Scene openedScene;
        StoryChapter chapter;
        int page;
        Action finished;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics(){instance=null;consumed=-1;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if(instance)return;
            var prefab=Resources.Load<GameObject>("UI/StoryBook");if(prefab)Instantiate(prefab);
        }
        void Awake()
        {
            if(instance&&instance!=this){Destroy(gameObject);return;}
            instance=this;DontDestroyOnLoad(gameObject);BuildUI();
            SceneManager.activeSceneChanged+=SceneChanged;
            StartCoroutine(Entrance(SceneManager.GetActiveScene()));
        }
        void OnDestroy()
        {
            if(instance!=this)return;
            Release(false);SceneManager.activeSceneChanged-=SceneChanged;instance=null;
        }
        void SceneChanged(Scene oldScene,Scene next)
        {
            // Cancel callbacks tied to the scene being abandoned (restart, title, external load).
            StopAllCoroutines();pendingClears.Clear();presentedClears.Clear();Release(false);banner.gameObject.SetActive(false);
            StartCoroutine(Entrance(next));
        }
        public void BeginNewGame(Action continuation)
        {
            if(open)return;entered.Clear();
            // START always opens the first page; collected memories survive a new run.
            Play("prologue",continuation,true);
        }
        public bool Play(string id,Action onFinished=null,bool unlock=false)
        {
            var c=library.Find(id);if(c==null||c.pages==null||c.pages.Length==0)return false;
            if(!unlock&&!StoryProgress.Unlocked(id))return false;
            bool fromGallery=GalleryOpen;
            if(open&&!fromGallery)return false;
            if(unlock)StoryProgress.Unlock(id);
            if(!open)Acquire();
            gallery=false;replay=fromGallery;chapter=c;page=0;finished=onFinished;
            album.SetActive(false);reader.SetActive(true);automatic=false;autoLabel.text="자동 넘김 꺼짐";
            ShowPage();return true;
        }
        public void OpenGallery()
        {
            if(open&&!replay)return;
            if(!open)
            {
                if(GameSettingsWindow.IsOpen||SkillWindow.IsOpen||EconomyWindow.IsOpen)return;
                Acquire();
            }
            gallery=true;replay=false;finished=null;reader.SetActive(false);album.SetActive(true);RefreshGallery();
        }
        void Acquire()
        {
            savedTime=Time.timeScale;savedCursor=Cursor.lockState;savedVisible=Cursor.visible;
            savedSelection=EventSystem.current?EventSystem.current.currentSelectedGameObject:null;
            if(!EventSystem.current)new GameObject("Story EventSystem",typeof(EventSystem),typeof(StandaloneInputModule));
            EventSystem.current.SetSelectedGameObject(null);
            openedScene=SceneManager.GetActiveScene();open=true;consumed=Time.frameCount;
            Time.timeScale=0;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;root.SetActive(true);started=Time.unscaledTime;
        }
        void Release(bool callback)
        {
            if(!open)return;
            var action=finished;finished=null;open=false;gallery=false;replay=false;
            root.SetActive(false);Time.timeScale=savedTime;Cursor.lockState=savedCursor;Cursor.visible=savedVisible;
            consumed=Time.frameCount;
            if(EventSystem.current)EventSystem.current.SetSelectedGameObject(savedSelection?savedSelection:null);
            if(callback&&openedScene==SceneManager.GetActiveScene())action?.Invoke();
        }
        public void Finish()
        {
            if(!open)return;
            if(replay){OpenGallery();return;}
            Release(true);
        }
        public void NextPage()
        {
            if(!open||gallery)return;
            if(Time.unscaledTime-pageStarted<library.fadeDuration){pageStarted=Time.unscaledTime-library.fadeDuration;return;}
            if(page+1<chapter.pages.Length){page++;ShowPage();}else Finish();
        }
        public void PreviousPage(){if(open&&!gallery&&page>0){page--;ShowPage();}}
        void ShowPage()
        {
            var p=chapter.pages[page];art.texture=p.artwork;art.color=p.artwork?Color.white:new Color(.08f,.12f,.15f);
            heading.text=chapter.title;subheading.text=chapter.subtitle;caption.text=p.text;
            counter.text=(page+1).ToString("00")+" / "+chapter.pages.Length.ToString("00");
            nextLabel.text=page==chapter.pages.Length-1?(replay?"추억으로":"계속하기"):"다음";
            prev.interactable=page>0;atmosphere.tint=chapter.accent;rule.color=chapter.accent;
            skipLabel.text=replay?"추억으로 돌아가기":"건너뛰기";
            pageStarted=Time.unscaledTime;pageGroup.alpha=0;
            if(EventSystem.current)EventSystem.current.SetSelectedGameObject(null);
        }
        void Update()
        {
            if(!open)return;
            // Some gameplay scripts update cursor while unscaled cinematics finish; the modal owns it.
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            if(Input.GetKeyDown(KeyCode.Escape)){Finish();return;}
            if(gallery)return;
            float elapsed=Time.unscaledTime-pageStarted;
            pageGroup.alpha=Mathf.SmoothStep(0,1,elapsed/Mathf.Max(.1f,library.fadeDuration));
            float z=1+library.imageZoom*Mathf.SmoothStep(0,1,elapsed/18);
            float w=1/z,h=1/z;var focus=chapter.pages[page].focus;
            float drift=Mathf.Sin(Mathf.Min(elapsed,24)/24f*Mathf.PI*.5f)*.15f*(page%2==0?1:-1);
            art.uvRect=new Rect(Mathf.Clamp01(focus.x+drift)*(1-w),Mathf.Clamp01(focus.y)*(1-h),w,h);
            bool selected=EventSystem.current&&EventSystem.current.currentSelectedGameObject;
            if(Input.GetKeyDown(KeyCode.Space)||Input.GetKeyDown(KeyCode.RightArrow)||(!selected&&Input.GetKeyDown(KeyCode.Return)))
            {
                // Space is also the project's UI Submit binding; consume selection before advancing.
                if(EventSystem.current)EventSystem.current.SetSelectedGameObject(null);
                NextPage();
            }
            else if(Input.GetKeyDown(KeyCode.LeftArrow))PreviousPage();
            if(automatic&&elapsed>Mathf.Max(library.automaticPageSeconds,caption.text.Length*.12f+2))NextPage();
        }
        IEnumerator Entrance(Scene scene)
        {
            yield return null;var c=library.ForScene(scene.name);if(c==null||entered.Contains(c.id))yield break;
            var stream=ForestRoomStreaming.Instance;
            while(stream&&!stream.Ready){if(!string.IsNullOrEmpty(stream.LoadError))yield break;yield return null;}
            while(open||GameSettingsWindow.IsOpen||SkillWindow.IsOpen||EconomyWindow.IsOpen)yield return null;
            entered.Add(c.id);bannerTitle.text=c.title;bannerLine.text=c.entrance;banner.gameObject.SetActive(true);
            for(float t=0;t<5;t+=Time.deltaTime){banner.alpha=Mathf.Min(Mathf.Clamp01(t),Mathf.Clamp01(5-t));yield return null;}
            banner.gameObject.SetActive(false);
        }
        public void QueueClear(StageOneDirector director)
        {
            if(!director||!director.hud||director.hud.health.Dead)return;
            var c=library.ForScene(director.gameObject.scene.name);if(c==null||!presentedClears.Add(director))return;
            pendingClears.Add(director);
            // Save the earned illustration immediately, even if the player quits during presentation.
            bool first=StoryProgress.Unlock(c.id);
            StartCoroutine(ClearPresentation(director,c,first));
        }
        IEnumerator ClearPresentation(StageOneDirector director,StoryChapter c,bool first)
        {
            var key=director;
            yield return new WaitForSeconds(library.clearPause);
            while(director&&(open||GameSettingsWindow.IsOpen||SkillWindow.IsOpen||EconomyWindow.IsOpen||CinematicActive()))yield return null;
            if(!director||director.hud.health.Dead){pendingClears.Remove(key);yield break;}
            if(first)
            {
                Play(c.id);
                while(open)yield return null;
            }
            if(c.id=="winter"&&director&&!director.hud.health.Dead)
            {
                var karma=director.hud.health.GetComponent<PlayerKarma>();
                string ending=library.Ending(karma?karma.Value:0,karma?karma.Maximum:100);
                Play(ending,null,true);
                while(open)yield return null;
            }
            pendingClears.Remove(key);
        }
        static bool CinematicActive()
        {
            foreach(var c in FindObjectsByType<SpringBoarCinematic>(FindObjectsSortMode.None))if(c.Active)return true;
            return false;
        }
    }
}

