using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
namespace DogLife
{
    // Exhibition-only, local-PC rankings. Campaign saves are deliberately separate.
    public sealed class SeasonTrialRecords : MonoBehaviour
    {
        [Serializable] public sealed class Entry { public string name; public float seconds; }
        [Serializable] sealed class Table { public List<Entry> entries=new List<Entry>(); }
        static SeasonTrialRecords instance;
        static readonly string[] Names={"봄","여름","가을","겨울"};
        public static bool IsOpen {get;private set;}
        int season=-1;string scenePath;bool running,finished;float seconds;
        StageOneDirector director;GameObject modal;Action closeModal;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Reset(){instance=null;IsOpen=false;}
        static SeasonTrialRecords Get(){if(!instance){instance=new GameObject("Season trial records").AddComponent<SeasonTrialRecords>();DontDestroyOnLoad(instance.gameObject);}return instance;}
        void OnEnable(){if(instance&&instance!=this){Destroy(gameObject);return;}instance=this;SceneManager.sceneLoaded-=Loaded;SceneManager.sceneLoaded+=Loaded;}
        void OnDisable(){SceneManager.sceneLoaded-=Loaded;if(closeModal!=null)closeModal();}
        void OnDestroy(){SceneManager.sceneLoaded-=Loaded;if(closeModal!=null)closeModal();if(instance==this)instance=null;}
        public static void Prepare(int index,string path){if(!CampaignBuildMode.ExhibitionEnabled)return;var owner=Get();owner.season=index;owner.scenePath=path;owner.seconds=0;owner.running=false;owner.finished=false;owner.director=null;}
        public static void Cancel(){if(!instance)return;instance.season=-1;instance.running=false;instance.finished=false;instance.director=null;instance.StopAllCoroutines();}
        void Loaded(Scene scene,LoadSceneMode mode){
            if(mode==LoadSceneMode.Additive)return;
            if(closeModal!=null)closeModal();
            if(scene.path==scenePath&&season>=0){seconds=0;finished=false;running=true;director=null;}
            else if(scene.name!="EnemyIntroduction")Cancel();
        }
        void Update(){
            if(!running||finished)return;
            if(!director)director=FindObjectsByType<StageOneDirector>(FindObjectsSortMode.None).FirstOrDefault(d=>d.gameObject.scene.path==scenePath);
            if(!director||!director.hud||!director.hud.health)return;
            if(director.hud.health.Dead){running=false;return;}
            if(SeasonLoadingScreen.IsLoading||IsOpen||StoryBook.BlocksInput||BossCinematicUI.Hidden||SkillWindow.BlocksGameplayInput||Time.timeScale<=0)return;
            if(director.streaming&&(!director.streaming.Ready||director.streaming.Busy))return;
            if(!director.hud.motor||!director.hud.motor.enabled||!director.hud.motor.readInput)return;
            seconds+=Time.unscaledDeltaTime;
        }
        public static void Complete(StageOneDirector owner){
            if(!instance||!instance.running||instance.finished||owner.gameObject.scene.path!=instance.scenePath)return;
            instance.running=false;instance.finished=true;
            instance.StartCoroutine(instance.Result(owner));
        }
        IEnumerator Result(StageOneDirector owner){
            while(owner&&(SeasonLoadingScreen.IsLoading||BossCinematicUI.Hidden||StoryBook.BlocksInput||SkillWindow.BlocksGameplayInput))yield return null;
            if(!owner||!owner.hud||owner.hud.health.Dead)yield break;
            int rank=Rank(Read(season),seconds);
            if(rank<=5)ShowName(rank);else ShowBoardInternal(season,"이번 기록 "+Format(seconds)+" · TOP 5에 들지 못했습니다.");
        }
        static string Key(int index)=>"DogLife.SeasonTrial.Top5.v1."+index;
        public static List<Entry> Read(int index){
            try{var table=JsonUtility.FromJson<Table>(PlayerPrefs.GetString(Key(index),""));return (table?.entries??new List<Entry>()).Where(e=>e!=null&&e.seconds>0&&!float.IsNaN(e.seconds)&&!float.IsInfinity(e.seconds)).OrderBy(e=>e.seconds).Take(5).ToList();}
            catch{return new List<Entry>();}
        }
        public static int Rank(List<Entry> entries,float time)=>1+entries.Count(e=>e.seconds<=time);
        public static string Format(float time){int ms=Mathf.FloorToInt(time*1000);return (ms/60000).ToString("00")+":"+((ms/1000)%60).ToString("00")+"."+(ms%1000).ToString("000");}
        static void Save(int index,string name,float time){
            var table=new Table{entries=Read(index)};table.entries.Add(new Entry{name=name,seconds=time});table.entries=table.entries.OrderBy(e=>e.seconds).Take(5).ToList();
            PlayerPrefs.SetString(Key(index),JsonUtility.ToJson(table));PlayerPrefs.Save();
        }
        Font Font()=>StoryBook.Instance&&StoryBook.Instance.Library?StoryBook.Instance.Library.font:UnityEngine.Font.CreateDynamicFontFromOSFont("Malgun Gothic",24);
        RectTransform Panel(string heading){
            if(closeModal!=null)closeModal();
            CombatFeedback.CancelGlobalHitSlow();float previous=Time.timeScale;var cursor=Cursor.lockState;bool visible=Cursor.visible;
            IsOpen=true;Time.timeScale=0;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            modal=new GameObject("Season rankings",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));modal.transform.SetParent(transform,false);
            var canvas=modal.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=31000;
            var scaler=modal.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            var shade=AdventureUI.Rect("Shade",modal.transform,Vector2.zero,Vector2.zero);shade.anchorMin=Vector2.zero;shade.anchorMax=Vector2.one;shade.gameObject.AddComponent<Image>().color=new Color(.02f,.025f,.03f,.9f);
            var panel=AdventureUI.Rect("Panel",modal.transform,Vector2.zero,new Vector2(920,780));AdventureUI.Surface(panel.gameObject);
            AdventureUI.Label(panel,"Heading",heading,new Vector2(0,315),new Vector2(850,70),36,Font());
            bool closed=false;var target=modal;
            closeModal=()=>{if(closed)return;closed=true;IsOpen=false;Time.timeScale=previous;Cursor.lockState=cursor;Cursor.visible=visible;closeModal=null;if(target)Destroy(target);modal=null;};
            return panel;
        }
        void ShowName(int rank){
            var panel=Panel(Names[season]+" 계절 체험 · TOP 5 진입!");var font=Font();
            AdventureUI.Label(panel,"Result",rank+"위  ·  "+Format(seconds),new Vector2(0,195),new Vector2(820,65),34,font);
            AdventureUI.Label(panel,"Prompt","기록에 남길 닉네임을 입력하세요",new Vector2(0,95),new Vector2(820,50),25,font);
            var box=AdventureUI.Rect("Nickname",panel,new Vector2(0,15),new Vector2(630,75));box.gameObject.AddComponent<Image>().color=new Color(.09f,.065f,.045f);
            var input=box.gameObject.AddComponent<InputField>();input.characterLimit=16;input.lineType=InputField.LineType.SingleLine;
            // 입력 전 안내와 입력한 이름이 같은 중심선을 사용합니다.
            input.textComponent=AdventureUI.Label(box,"Text","",Vector2.zero,new Vector2(580,65),29,font);input.textComponent.alignment=TextAnchor.MiddleCenter;
            var placeholder=AdventureUI.Label(box,"Placeholder","닉네임 (최대 16자)",Vector2.zero,new Vector2(580,65),25,font);placeholder.color=new Color(.6f,.55f,.48f);input.placeholder=placeholder;
            var message=AdventureUI.Label(panel,"Validation","이 PC에 저장됩니다 · 빠른 기록 순으로 5명 표시",new Vector2(0,-90),new Vector2(820,60),21,font);
            bool submitted=false;
            AdventureUI.Button(panel,"SaveRecord","닉네임과 기록 저장",new Vector2(0,-205),new Vector2(470,65),font,()=>{
                if(submitted)return;var name=new string(input.text.Where(c=>!char.IsControl(c)&&c!='<'&&c!='>').ToArray()).Trim();
                if(string.IsNullOrWhiteSpace(name)){message.text="닉네임을 입력해주세요.";input.ActivateInputField();return;}
                submitted=true;Save(season,name,seconds);ShowBoardInternal(season,"기록이 저장되었습니다.");
            });
            AdventureUI.Button(panel,"SkipRecord","기록을 남기지 않고 계속",new Vector2(0,-305),new Vector2(470,55),font,()=>closeModal?.Invoke());
            AdventureUI.Theme(panel.gameObject);input.ActivateInputField();
        }
        public static void ShowBoard(int index){if(!CampaignBuildMode.ExhibitionEnabled)return;if(IsOpen||index<0||index>=4)return;Get().ShowBoardInternal(index,"이 PC의 기록 · 로딩 / 메뉴 / 연출 시간 제외");}
        void ShowBoardInternal(int index,string caption){
            var panel=Panel(Names[index]+" 계절 체험 · TOP 5");var font=Font();var entries=Read(index);
            AdventureUI.Label(panel,"Caption",caption,new Vector2(0,235),new Vector2(850,55),21,font);
            for(int i=0;i<5;i++){
                var row=AdventureUI.Rect("Rank "+(i+1),panel,new Vector2(0,145-i*78),new Vector2(820,65));AdventureUI.Surface(row.gameObject);
                AdventureUI.Label(row,"Rank",(i+1)+"위",new Vector2(-340,0),new Vector2(90,55),26,font);
                var name=AdventureUI.Label(row,"Name",i<entries.Count?entries[i].name:"아직 기록이 없습니다",new Vector2(-60,0),new Vector2(420,55),25,font);name.supportRichText=false;
                AdventureUI.Label(row,"Time",i<entries.Count?Format(entries[i].seconds):"—",new Vector2(280,0),new Vector2(225,55),25,font);
            }
            AdventureUI.Button(panel,"CloseRecords","닫기",new Vector2(0,-305),new Vector2(340,60),font,()=>closeModal?.Invoke());AdventureUI.Theme(panel.gameObject);
        }
        public static void AttachTitle(TitleScreen title){
            if(!CampaignBuildMode.ExhibitionEnabled)return;
            foreach(var button in FindObjectsByType<Button>(FindObjectsInactive.Include,FindObjectsSortMode.None)){
                if(button.gameObject.scene!=title.gameObject.scene)continue;
                if(!button.name.StartsWith("Demo Season Button "))continue;int index;if(!int.TryParse(button.name.Substring("Demo Season Button ".Length),out index)||index<0||index>3)continue;
                if(button.transform.parent.Find("Season rank "+index))continue;
                var rect=(RectTransform)button.transform;int captured=index;
                var rank=AdventureUI.Button(rect.parent,"Season rank "+index,"순위",Vector2.zero,new Vector2(76,58),UnityEngine.Font.CreateDynamicFontFromOSFont("Malgun Gothic",22),()=>ShowBoard(captured));
                var target=(RectTransform)rank.transform;target.anchorMin=rect.anchorMin;target.anchorMax=rect.anchorMax;target.pivot=rect.pivot;target.anchoredPosition=rect.anchoredPosition+new Vector2(rect.sizeDelta.x+10,(rect.sizeDelta.y-58)*-.5f);
            }
        }
        void OnGUI(){
            if(!running||!director||IsOpen||SeasonLoadingScreen.IsLoading||StoryBook.BlocksInput||BossCinematicUI.Hidden||SkillWindow.BlocksGameplayInput)return;
            var old=GUI.matrix;GUI.matrix=Matrix4x4.Scale(new Vector3(Screen.width/1920f,Screen.height/1080f,1));
            var style=new GUIStyle(GUI.skin.box){fontSize=21,alignment=TextAnchor.MiddleCenter};style.normal.textColor=AdventureUI.Cream;
            GUI.Box(new Rect(800,22,320,40),Names[season]+" 체험 기록  "+Format(seconds),style);GUI.matrix=old;
        }
    }
}
