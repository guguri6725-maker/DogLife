using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DogLife
{
    [DefaultExecutionOrder(-200)]
    public sealed class GameSettingsWindow : MonoBehaviour
    {
        [SerializeField] GameObject panel, confirmation;
        [SerializeField] Button[] fpsButtons, optionButtons;
        [SerializeField] Text[] optionLabels;
        [SerializeField] Text selectionLabel, liveFps, confirmationLabel;
        static GameSettingsWindow instance;
        static int consumedFrame=-1;
        public static bool IsOpen => instance&&instance.panel.activeSelf;
        public static bool BlocksGameplayInput => IsOpen||Time.frameCount==consumedFrame;
        public bool AwaitingConfirmation { get; private set; }
        public bool Applying { get; private set; }
        GraphicsPreferences draft, previous;
        float previousTimeScale, elapsed, deadline;
        CursorLockMode previousCursor;
        bool previousCursorVisible;
        int frames;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetState(){instance=null;consumedFrame=-1;WorldRenderScale.Scale=1;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            if(instance)return;
            var prefab=Resources.Load<GameObject>("UI/GameSettings");
            if(prefab)Instantiate(prefab);
        }
        void Awake()
        {
            if(instance&&instance!=this){Destroy(gameObject);return;}
            instance=this;DontDestroyOnLoad(gameObject);panel.SetActive(false);confirmation.SetActive(false);
            for(int i=0;i<fpsButtons.Length;i++){int choice=i;fpsButtons[i].onClick.AddListener(()=>SelectLimit(choice));}
            for(int i=0;i<optionButtons.Length;i++){int choice=i;optionButtons[i].onClick.AddListener(()=>CycleOption(choice/2,choice%2==0?-1:1));}
            SceneManager.activeSceneChanged+=SceneChanged;
        }
        IEnumerator Start()
        {
            Applying=true;
            var saved=GameGraphicsSettings.Load();GameGraphicsSettings.SetCurrent(saved);GameGraphicsSettings.ApplyGraphics(saved);
            yield return GameGraphicsSettings.ApplyDisplay(saved);
            Applying=false;
        }
        void SceneChanged(Scene oldScene,Scene newScene){if(IsOpen)Close();}
        void OnDestroy()
        {
            if(instance!=this)return;
            if(panel.activeSelf){Time.timeScale=previousTimeScale;Cursor.lockState=previousCursor;Cursor.visible=previousCursorVisible;}
            SceneManager.activeSceneChanged-=SceneChanged;instance=null;
        }
        void Update()
        {
            var camera=Camera.main;
            if(camera)
            {
                var scaling=camera.GetComponent<WorldRenderScale>();
                if(!scaling)scaling=camera.gameObject.AddComponent<WorldRenderScale>();
                scaling.enabled=WorldRenderScale.Scale<.999f;
            }
            if(!SkillWindow.IsOpen&&(Input.GetKeyDown(KeyCode.Escape)||Input.GetKeyDown(KeyCode.F10)))Toggle();
            if(AwaitingConfirmation)
            {
                float remaining=deadline-Time.unscaledTime;
                confirmationLabel.text="이 화면 설정을 유지할까요?\n"+Mathf.CeilToInt(Mathf.Max(0,remaining))+"초 후 이전 설정으로 돌아갑니다.";
                if(remaining<=0)Revert();
            }
            if(!IsOpen)return;
            elapsed+=Time.unscaledDeltaTime;frames++;
            if(elapsed>=.5f){liveFps.text="설정 화면 FPS  "+Mathf.RoundToInt(frames/elapsed)+"   ·   게임은 일시정지 중";elapsed=0;frames=0;}
        }
        public void Toggle(){if(AwaitingConfirmation){Revert();return;}if(IsOpen)Close();else Open();}
        public void Open()
        {
            if(IsOpen||SkillWindow.IsOpen||Applying||GameGraphicsSettings.Current==null)return;
            if(!EventSystem.current)new GameObject("Settings EventSystem",typeof(EventSystem),typeof(StandaloneInputModule));
            previousTimeScale=Time.timeScale;previousCursor=Cursor.lockState;previousCursorVisible=Cursor.visible;
            draft=GameGraphicsSettings.Current.Copy();
            panel.SetActive(true);Time.timeScale=0;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            consumedFrame=Time.frameCount;elapsed=0;frames=0;liveFps.text="FPS 측정 중";RefreshSelection();
        }
        public void Close()
        {
            if(Applying)return;
            if(AwaitingConfirmation){Revert();return;}
            if(!panel.activeSelf)return;
            panel.SetActive(false);Time.timeScale=previousTimeScale;
            Cursor.lockState=previousCursor;Cursor.visible=previousCursorVisible;consumedFrame=Time.frameCount;
            if(EventSystem.current)EventSystem.current.SetSelectedGameObject(null);
        }
        public void SelectLimit(int index)
        {
            if(draft==null||Applying||AwaitingConfirmation||index<0||index>=PlayerFramePacing.Limits.Length)return;
            draft.fps=PlayerFramePacing.Limits[index];RefreshSelection();
        }
        static int Wrap(int index,int count){return (index%count+count)%count;}
        public void CycleOption(int row,int direction)
        {
            if(draft==null||Applying||AwaitingConfirmation)return;
            var displays=GameGraphicsSettings.Displays();
            switch(row)
            {
                case 0:
                    if(displays.Count==0)break;
                    draft.monitor=displays[Wrap(GameGraphicsSettings.MonitorIndex(draft,displays)+direction,displays.Count)].name;
                    var available=GameGraphicsSettings.Resolutions(draft);
                    if(!available.Contains(new Vector2Int(draft.width,draft.height))){var size=available[available.Count-1];draft.width=size.x;draft.height=size.y;}
                    break;
                case 1:
                    var sizes=GameGraphicsSettings.Resolutions(draft);
                    var next=sizes[Wrap(sizes.IndexOf(new Vector2Int(draft.width,draft.height))+direction,sizes.Count)];draft.width=next.x;draft.height=next.y;break;
                case 2:draft.mode=Wrap(draft.mode+direction,3);break;
                case 3:draft.quality=Wrap(draft.quality+direction,3);break;
                case 4:draft.vsync=!draft.vsync;break;
                case 5:draft.scale=GameGraphicsSettings.Scales[Wrap(Array.IndexOf(GameGraphicsSettings.Scales,draft.scale)+direction,GameGraphicsSettings.Scales.Length)];break;
            }
            RefreshSelection();
        }
        public void Apply(){if(draft!=null&&!Applying&&!AwaitingConfirmation)StartCoroutine(ApplyRoutine());}
        IEnumerator ApplyRoutine()
        {
            Applying=true;selectionLabel.text="설정을 적용하고 있습니다…";
            previous=GameGraphicsSettings.Current.Copy();
            GameGraphicsSettings.ApplyGraphics(draft);
            yield return GameGraphicsSettings.ApplyDisplay(draft);
            Applying=false;
            if(GameGraphicsSettings.DisplayChanged(previous,draft))
            {AwaitingConfirmation=true;deadline=Time.unscaledTime+15;confirmation.SetActive(true);}
            else {GameGraphicsSettings.Commit(draft);RefreshSelection();selectionLabel.text="적용 및 저장 완료";}
        }
        public void Keep()
        {
            if(!AwaitingConfirmation)return;
            AwaitingConfirmation=false;confirmation.SetActive(false);GameGraphicsSettings.Commit(draft);RefreshSelection();selectionLabel.text="적용 및 저장 완료";
        }
        public void Revert(){if(AwaitingConfirmation&&!Applying)StartCoroutine(RevertRoutine());}
        IEnumerator RevertRoutine()
        {
            AwaitingConfirmation=false;Applying=true;confirmation.SetActive(false);
            GameGraphicsSettings.ApplyGraphics(previous);yield return GameGraphicsSettings.ApplyDisplay(previous);
            draft=previous.Copy();Applying=false;RefreshSelection();selectionLabel.text="이전 설정으로 복원했습니다.";
        }
        void RefreshSelection()
        {
            var displays=GameGraphicsSettings.Displays();
            var index=GameGraphicsSettings.MonitorIndex(draft,displays);
            optionLabels[0].text=displays.Count>0?(index+1)+". "+displays[index].name+" / "+Mathf.RoundToInt((float)displays[index].refreshRate.value)+"Hz":"현재 모니터";
            optionLabels[1].text=draft.width+" × "+draft.height;
            optionLabels[2].text=new[]{"창 모드","테두리 없는 전체화면","전체화면"}[draft.mode];
            optionLabels[3].text=new[]{"낮음","보통","높음"}[draft.quality];
            optionLabels[4].text=draft.vsync?"켜짐 · 모니터 주사율에 동기화":"꺼짐 · FPS 제한 사용";
            optionLabels[5].text=draft.scale+"%"+(draft.scale<100?" · 3D만 낮추고 UI는 선명하게":" · 원본 해상도");
            selectionLabel.text=draft.vsync?"수직 동기화가 켜져 있으면 FPS 제한보다 우선합니다.":"변경 후 적용을 누르세요. 닫으면 미적용 변경은 취소됩니다.";
            for(int i=0;i<fpsButtons.Length;i++)
            {
                bool active=PlayerFramePacing.Limits[i]==draft.fps;
                fpsButtons[i].interactable=!draft.vsync;
                fpsButtons[i].GetComponent<Image>().color=active?new Color(.94f,.57f,.64f):new Color(.16f,.25f,.22f);
                fpsButtons[i].GetComponentInChildren<Text>().color=active?new Color(.04f,.09f,.08f):new Color(.94f,.91f,.81f);
            }
        }
    }
}

