using UnityEngine;
using UnityEngine.UI;

namespace DogLife
{
    public sealed partial class StoryBook
    {
        GameObject root,reader,album;
        RectTransform safeFrame;
        RawImage art,preview;
        CanvasGroup pageGroup,banner;
        StoryAtmosphere atmosphere;
        // 제목 디자인은 프리팹의 Hierarchy에서 편집하고 참조를 직접 연결합니다.
        [SerializeField] Text heading,subheading;
        [SerializeField] Image rule;
        Text caption,counter,nextLabel,skipLabel,autoLabel,bannerTitle,bannerLine,albumTitle,albumText,albumCount;
        Color headingTint,subtitleTint;
        Vector2 headingPosition;
        Button prev,replayButton;
        Button[] chapterButtons;
        Text[] chapterLabels;
        Image[] chapterMarks;
        StoryChapter selectedChapter;
        readonly Color cream=new Color(.96f,.91f,.79f),ink=new Color(.12f,.17f,.17f),gold=new Color(.7f,.52f,.28f);
        RectTransform R(string name,Transform parent,Vector2 pos,Vector2 size)
        {
            var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);
            r.anchorMin=r.anchorMax=new Vector2(.5f,.5f);r.sizeDelta=size;r.anchoredPosition=pos;return r;
        }
        Image Panel(string name,Transform parent,Vector2 pos,Vector2 size,Color color)
        {var i=R(name,parent,pos,size).gameObject.AddComponent<Image>();i.color=color;i.raycastTarget=false;return i;}
        // 추억 목록과 설명은 재생성되어도 지정된 칸의 중앙 정렬을 유지합니다.
        Text T(string name,Transform parent,string value,Vector2 pos,Vector2 size,int fontSize,Color color,TextAnchor alignment=TextAnchor.MiddleCenter)
        {
            var t=R(name,parent,pos,size).gameObject.AddComponent<Text>();t.font=library.font;t.text=value;t.fontSize=fontSize;t.color=color;
            t.alignment=alignment;t.raycastTarget=false;t.supportRichText=false;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Truncate;t.lineSpacing=1.35f;return t;
        }
        Button B(string name,Transform parent,string label,Vector2 pos,Vector2 size,System.Action action,out Text text)
        {
            var i=Panel(name,parent,pos,size,new Color(.07f,.12f,.13f,.94f));i.raycastTarget=true;
            var b=i.gameObject.AddComponent<Button>();b.targetGraphic=i;
            var colors=b.colors;colors.highlightedColor=new Color(1.5f,1.4f,1.15f);colors.pressedColor=new Color(.75f,.8f,.8f);b.colors=colors;
            text=T("Label",i.transform,label,Vector2.zero,size,24,cream,TextAnchor.MiddleCenter);
            AdventureUI.StyleButton(b,name=="Next"||name=="Replay memory");
            b.onClick.AddListener(()=>{GameAudio.Play("UI_Button_Click",Vector3.zero,.35f,ui:true);action();});
            return b;
        }
        void BuildUI()
        {
            var canvas=new GameObject("Memory Canvas",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster)).GetComponent<Canvas>();canvas.transform.SetParent(transform,false);
            canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=24000;
            var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            var bg=Panel("Story modal",canvas.transform,Vector2.zero,new Vector2(1920,1080),Color.black);
            bg.rectTransform.anchorMin=Vector2.zero;bg.rectTransform.anchorMax=Vector2.one;bg.rectTransform.sizeDelta=Vector2.zero;bg.raycastTarget=true;root=bg.gameObject;
            var frame=R("Safe 16 by 9 frame",bg.transform,Vector2.zero,new Vector2(1920,1080));
            safeFrame=frame;
            var fit=frame.gameObject.AddComponent<AspectRatioFitter>();fit.aspectMode=AspectRatioFitter.AspectMode.FitInParent;fit.aspectRatio=16f/9;
            reader=R("Story reader",frame,Vector2.zero,new Vector2(1920,1080)).gameObject;
            var visual=R("Fading picture",reader.transform,Vector2.zero,new Vector2(1920,1080));pageGroup=visual.gameObject.AddComponent<CanvasGroup>();
            art=R("Illustration",visual,Vector2.zero,new Vector2(1920,1080)).gameObject.AddComponent<RawImage>();art.raycastTarget=false;
            atmosphere=R("Atmosphere",visual,Vector2.zero,new Vector2(1920,1080)).gameObject.AddComponent<StoryAtmosphere>();atmosphere.raycastTarget=false;
            subheading.transform.SetParent(visual,false);
            heading.transform.SetParent(visual,false);
            rule.transform.SetParent(visual,false);
            headingTint=heading.color;subtitleTint=subheading.color;
            headingPosition=heading.rectTransform.anchoredPosition;
            var narrationFrame=R("Narration parchment frame",visual,new Vector2(-40,-326),new Vector2(1650,184));
            var plate=narrationFrame.gameObject.AddComponent<StoryPanelGraphic>();plate.color=new Color(1,1,1,.68f);plate.raycastTarget=false;
            caption=T("Narration",visual,"",new Vector2(-40,-326),new Vector2(1560,156),32,AdventureUI.Cream,TextAnchor.MiddleCenter);
            caption.gameObject.AddComponent<Shadow>().effectColor=new Color(0,0,0,.65f);
            counter=T("Page number",reader.transform,"",new Vector2(-775,-464),new Vector2(130,50),21,cream);
            Text discard;
            prev=B("Previous",reader.transform,"이전",new Vector2(475,-464),new Vector2(135,56),PreviousPage,out discard);
            B("Next",reader.transform,"다음",new Vector2(695,-464),new Vector2(265,56),NextPage,out nextLabel);
            B("Skip",reader.transform,"건너뛰기",new Vector2(697,448),new Vector2(268,50),Finish,out skipLabel);
            B("Auto",reader.transform,"자동 넘김 꺼짐",new Vector2(-535,-464),new Vector2(265,56),()=>{automatic=!automatic;autoLabel.text=automatic?"자동 넘김 켜짐":"자동 넘김 꺼짐";},out autoLabel);
            T("Reader keys",reader.transform,"Space 문장 완성 / 다음     ·     Esc 돌아가기",new Vector2(-45,-465),new Vector2(555,42),18,new Color(.7f,.72f,.69f),TextAnchor.MiddleCenter);
            BuildPresentation(visual);

            album=R("Collected memories",frame,Vector2.zero,new Vector2(1920,1080)).gameObject;
            Panel("Backdrop",album.transform,Vector2.zero,new Vector2(1920,1080),new Color(.065f,.043f,.03f));
            Panel("Book shadow",album.transform,new Vector2(8,-12),new Vector2(1712,836),new Color(0,0,0,.45f));
            Panel("Book binding",album.transform,Vector2.zero,new Vector2(1712,836),gold);
            Panel("Left page",album.transform,new Vector2(-556,0),new Vector2(590,820),new Color(.86f,.81f,.68f));
            Panel("Right page",album.transform,new Vector2(290,0),new Vector2(1094,820),cream);
            for(int j=0;j<9;j++)Panel("Spine shade",album.transform,new Vector2(-258+j*3,0),new Vector2(3,820),new Color(.15f,.11f,.07f,.15f*(1-j/9f)));
            T("Book title",album.transform,"추억",new Vector2(-665,466),new Vector2(380,76),46,cream);
            T("Book subtitle",album.transform,"사계절 너머의 너에게",new Vector2(-315,461),new Vector2(410,50),23,new Color(.65f,.72f,.71f));
            B("Close memories",album.transform,"돌아가기",new Vector2(730,465),new Vector2(240,54),Finish,out discard);
            T("Contents heading",album.transform,"남겨진 마음들",new Vector2(-540,335),new Vector2(480,50),29,ink);
            albumCount=T("Collection count",album.transform,"",new Vector2(-540,280),new Vector2(480,40),19,new Color(.4f,.38f,.3f));
            int n=library.chapters.Length;chapterButtons=new Button[n];chapterLabels=new Text[n];chapterMarks=new Image[n];
            float step=Mathf.Min(97,500f/Mathf.Max(1,n));
            for(int i=0;i<n;i++)
            {
                int index=i;float y=195-i*step;
                var mark=Panel("Memory row "+i,album.transform,new Vector2(-550,y),new Vector2(490,step-12),new Color(.76f,.7f,.56f,.5f));mark.raycastTarget=true;chapterMarks[i]=mark;
                var b=mark.gameObject.AddComponent<Button>();b.targetGraphic=mark;chapterButtons[i]=b;
                chapterLabels[i]=T("Chapter",mark.transform,"",new Vector2(15,0),new Vector2(430,step-12),24,ink);
                b.onClick.AddListener(()=>SelectChapter(index));
            }
            T("Collection note",album.transform,"새로운 여정에서도\n당신의 추억은 남아 있습니다.",new Vector2(-540,-337),new Vector2(480,80),20,new Color(.41f,.4f,.34f));
            Panel("Illustration frame",album.transform,new Vector2(288,138),new Vector2(978,558),gold);
            preview=R("Memory illustration",album.transform,new Vector2(288,138),new Vector2(966,546)).gameObject.AddComponent<RawImage>();preview.raycastTarget=false;
            albumTitle=T("Memory title",album.transform,"",new Vector2(290,-190),new Vector2(970,62),34,ink);
            albumText=T("Memory excerpt",album.transform,"",new Vector2(290,-257),new Vector2(970,90),23,new Color(.34f,.36f,.33f));
            replayButton=B("Replay memory",album.transform,"이야기 다시 보기",new Vector2(637,-355),new Vector2(266,55),()=>{if(selectedChapter!=null)Play(selectedChapter.id);},out discard);
            root.SetActive(false);
            var br=R("Season entrance",canvas.transform,new Vector2(0,290),new Vector2(1160,178));banner=br.gameObject.AddComponent<CanvasGroup>();banner.blocksRaycasts=false;
            Panel("Entrance shade",br,Vector2.zero,new Vector2(1160,178),new Color(.025f,.05f,.055f,.78f));
            bannerTitle=T("Season",br,"",new Vector2(0,34),new Vector2(1100,65),40,cream,TextAnchor.MiddleCenter);
            bannerLine=T("Entrance line",br,"",new Vector2(0,-40),new Vector2(1100,60),24,cream,TextAnchor.MiddleCenter);br.gameObject.SetActive(false);
        }
        void RefreshGallery()
        {
            int count=0,first=-1;
            for(int i=0;i<library.chapters.Length;i++)
            {
                var c=library.chapters[i];bool unlocked=StoryProgress.Unlocked(c.id);if(unlocked){count++;if(first<0)first=i;}
                chapterLabels[i].text=(i+1).ToString("00")+"   "+(unlocked?c.title:"아직 닿지 않은 기억");
            }
            albumCount.text=count+" / "+library.chapters.Length+"개의 추억";SelectChapter(first<0?0:first);
        }
        void SelectChapter(int index)
        {
            selectedChapter=library.chapters[index];bool unlocked=StoryProgress.Unlocked(selectedChapter.id);
            for(int i=0;i<chapterMarks.Length;i++)chapterMarks[i].color=i==index?new Color(.68f,.61f,.45f,.65f):new Color(.76f,.7f,.56f,.35f);
            bool ending=selectedChapter.id=="life"||selectedChapter.id=="farewell"||selectedChapter.id=="revenge";
            preview.texture=unlocked?selectedChapter.pages[ending?selectedChapter.pages.Length-1:0].artwork:null;
            preview.color=unlocked?Color.white:new Color(.14f,.20f,.20f);
            albumTitle.text=unlocked?selectedChapter.title:"아직 닿지 않은 기억";
            albumText.text=unlocked?selectedChapter.pages[0].text:index==0?"새로운 여정을 시작하면 첫 기억이 펼쳐집니다.":"계절의 끝에 도착하면 이 페이지가 채워집니다.";
            replayButton.interactable=unlocked;
        }
        void LateUpdate()
        {
            if(!open||!safeFrame)return;
            float scale=safeFrame.rect.width/1920f;
            reader.transform.localScale=album.transform.localScale=Vector3.one*scale;
        }
    }
}
