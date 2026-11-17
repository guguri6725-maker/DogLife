using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace DogLife
{
    // Extends the existing settings card without replacing its artwork or display controls.
    public sealed class AudioSettingsPanel:MonoBehaviour
    {
        readonly List<GameObject> graphics=new List<GameObject>();
        readonly Slider[] sliders=new Slider[3];readonly Text[] values=new Text[3];
        GameObject audioPage;Text heading,status;Font font;Image displayTab,audioTab;bool refreshing;
        public void Initialize(GameObject panel)
        {
            var card=panel.transform.Find("Settings Card");if(!card)return;
            heading=card.Find("Heading").GetComponent<Text>();font=heading.font;
            status=card.Find("Status").GetComponent<Text>();
            foreach(Transform child in card)
            {
                string n=child.name;
                if(n.StartsWith("Option ")||n.StartsWith("Value ")||n.StartsWith("Previous ")||n.StartsWith("Next ")||n.StartsWith("FPS ")||n=="Live FPS")graphics.Add(child.gameObject);
            }
            var description=card.Find("Description");if(description)description.gameObject.SetActive(false);
            displayTab=MakeButton(card,"DisplayTab","화면",new Vector2(-360,350),()=>Show(false));
            audioTab=MakeButton(card,"AudioTab","소리",new Vector2(-130,350),()=>Show(true));
            audioPage=Rect("Audio Page",card,Vector2.zero,Vector2.zero).gameObject;
            var ar=(RectTransform)audioPage.transform;ar.anchorMin=Vector2.zero;ar.anchorMax=Vector2.one;
            string[] names={"전체 음량","배경음악","효과음"};
            for(int i=0;i<3;i++)
            {
                int index=i;float y=210-i*145;
                Label(audioPage.transform,names[i],new Vector2(-310,y+40),new Vector2(320,48),28);
                var track=Rect("Volume "+i,audioPage.transform,new Vector2(-30,y-15),new Vector2(860,28));
                var bg=track.gameObject.AddComponent<Image>();bg.color=new Color(.12f,.18f,.17f);
                var slider=track.gameObject.AddComponent<Slider>();slider.minValue=0;slider.maxValue=100;slider.wholeNumbers=true;
                var fill=Rect("Fill",track,Vector2.zero,Vector2.zero);fill.anchorMin=Vector2.zero;fill.anchorMax=Vector2.one;
                fill.gameObject.AddComponent<Image>().color=new Color(.8f,.64f,.32f);slider.fillRect=fill;
                var handleArea=Rect("Handle Area",track,Vector2.zero,new Vector2(-20,0));handleArea.anchorMin=Vector2.zero;handleArea.anchorMax=Vector2.one;
                var handle=Rect("Handle",handleArea,Vector2.zero,new Vector2(24,44));
                var hi=handle.gameObject.AddComponent<Image>();hi.color=new Color(1,.9f,.64f);slider.handleRect=handle;slider.targetGraphic=hi;
                sliders[i]=slider;values[i]=Label(audioPage.transform,"",new Vector2(400,y+40),new Vector2(140,48),26);
                slider.onValueChanged.AddListener(v=>Changed(index,v));
            }
            Label(audioPage.transform,"효과음에는 전투 · 발걸음 · 환경 · UI 소리가 포함됩니다.",new Vector2(0,-210),new Vector2(960,70),21);
            MakeButton(audioPage.transform,"Preview Sound","효과음 들어보기",new Vector2(-230,-260),()=>GameAudio.Play("SwordHit",Vector3.zero,.7f,ui:true,minimumInterval:.15f));
            MakeButton(audioPage.transform,"Reset Audio","기본값",new Vector2(30,-260),()=>{GameAudio.Preview(AudioPreferences.Defaults);Refresh();});
            Show(false);
        }
        static RectTransform Rect(string name,Transform parent,Vector2 pos,Vector2 size)
        {var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=r.anchorMax=new Vector2(.5f,.5f);r.anchoredPosition=pos;r.sizeDelta=size;return r;}
        Text Label(Transform parent,string text,Vector2 pos,Vector2 size,int fs)
        // 실행 중 생성하는 소리 설정도 각 표시 영역의 가운데에 맞춥니다.
        {var t=Rect(text==""?"Value":text,parent,pos,size).gameObject.AddComponent<Text>();t.text=text;t.font=font;t.fontSize=fs;t.color=new Color(.94f,.91f,.81f);t.alignment=TextAnchor.MiddleCenter;t.raycastTarget=false;return t;}
        Image MakeButton(Transform parent,string name,string text,Vector2 pos,UnityEngine.Events.UnityAction click)
        {var r=Rect(name,parent,pos,new Vector2(220,54));var image=r.gameObject.AddComponent<Image>();image.color=new Color(.1f,.16f,.14f);var edge=r.gameObject.AddComponent<Outline>();edge.effectColor=new Color(.65f,.53f,.3f);edge.effectDistance=new Vector2(1,-1);var b=r.gameObject.AddComponent<Button>();b.targetGraphic=image;b.onClick.AddListener(click);var label=Label(r,text,Vector2.zero,r.sizeDelta,24);label.alignment=TextAnchor.MiddleCenter;return image;}
        public void Begin(){GameAudio.Revert();Refresh();}
        void Refresh(){refreshing=true;var p=GameAudio.Current;float[] v={p.master,p.music,p.effects};for(int i=0;i<3;i++)if(sliders[i]){sliders[i].value=Mathf.Round(v[i]*100);values[i].text=sliders[i].value+"%";}refreshing=false;}
        void Changed(int index,float value)
        {if(refreshing)return;var p=GameAudio.Current;if(index==0)p.master=value/100;else if(index==1)p.music=value/100;else p.effects=value/100;GameAudio.Preview(p);values[index].text=value+"%";status.text="미리 듣는 중 · 적용하면 저장됩니다.";}
        void Show(bool sound){foreach(var go in graphics)go.SetActive(!sound);if(audioPage)audioPage.SetActive(sound);displayTab.color=sound?new Color(.1f,.16f,.14f):new Color(.32f,.29f,.17f);audioTab.color=sound?new Color(.32f,.29f,.17f):new Color(.1f,.16f,.14f);heading.text=sound?"소리 설정":"화면 · 그래픽 설정";if(sound)Refresh();}
    }
}
