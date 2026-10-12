using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
namespace DogLife
{
    public static class AdventureUI
    {
        public static readonly Color Cream=new Color(1,.92f,.76f), Gold=new Color(1,.69f,.28f);
        public static RectTransform Rect(string name,Transform parent,Vector2 pos,Vector2 size)
        {var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=r.anchorMax=new Vector2(.5f,.5f);r.anchoredPosition=pos;r.sizeDelta=size;return r;}
        public static Text Label(Transform parent,string name,string text,Vector2 pos,Vector2 size,int fontSize,Font font)
        {var t=Rect(name,parent,pos,size).gameObject.AddComponent<Text>();t.text=text;t.font=font;t.fontSize=fontSize;t.color=Cream;t.alignment=TextAnchor.MiddleCenter;t.raycastTarget=false;return t;}
        public static StoryPanelGraphic Surface(GameObject go,bool accent=false)
        {
            var old=go.GetComponent<Image>();if(old){old.color=Color.clear;old.raycastTarget=false;}
            var child=go.transform.Find("Adventure Surface") as RectTransform;
            if(!child){child=Rect("Adventure Surface",go.transform,Vector2.zero,Vector2.zero);child.anchorMin=Vector2.zero;child.anchorMax=Vector2.one;child.SetAsFirstSibling();}
            var graphic=child.GetComponent<StoryPanelGraphic>();if(!graphic)graphic=child.gameObject.AddComponent<StoryPanelGraphic>();graphic.accent=accent;graphic.SetAllDirty();return graphic;
        }
        public static void StyleButton(Button button,bool accent=false)
        {
            button.targetGraphic=Surface(button.gameObject,accent);
            var c=button.colors;c.normalColor=Color.white;c.highlightedColor=new Color(1,.92f,.73f);c.pressedColor=new Color(.7f,.64f,.54f);c.disabledColor=new Color(.43f,.4f,.37f,.75f);button.colors=c;
            if(!button.GetComponent<TitleButtonTween>())button.gameObject.AddComponent<TitleButtonTween>();
            foreach(var t in button.GetComponentsInChildren<Text>(true)){t.color=Cream;t.alignment=TextAnchor.MiddleCenter;t.fontStyle=FontStyle.Bold;t.lineSpacing=1.12f;}
        }
        public static Button Button(Transform parent,string name,string label,Vector2 pos,Vector2 size,Font font,UnityAction action,bool accent=false)
        {var r=Rect(name,parent,pos,size);var b=r.gameObject.AddComponent<Button>();StyleButton(b,accent);Label(r,"Label",label,Vector2.zero,size-new Vector2(24,8),25,font).fontStyle=FontStyle.Bold;b.onClick.AddListener(action);return b;}
        public static void Theme(GameObject root)
        {
            foreach(var t in root.GetComponentsInChildren<Text>(true))
            {
                t.color=Cream;t.lineSpacing=1.12f;
                if(t.name=="Heading"||t.fontSize>=32){t.color=Gold;t.fontStyle=FontStyle.Bold;var sh=t.GetComponent<Shadow>()??t.gameObject.AddComponent<Shadow>();sh.effectColor=new Color(.15f,.065f,.02f,.95f);sh.effectDistance=new Vector2(2,-3);}
            }
            foreach(var img in root.GetComponentsInChildren<Image>(true))
            {
                string n=img.name;
                if(n=="Frame"||n=="Settings Card"||n=="EconomyPanel"||n=="Details"||n=="SourcePanel"||n=="OwnedWeapons"||n=="CharacterEquipment"||n=="IconFrame"||n=="WeaponSlot")Surface(img.gameObject);
                else if(n.Contains("Border")||n.Contains("Divider"))img.color=new Color(.64f,.39f,.2f,.7f);
            }
            foreach(var b in root.GetComponentsInChildren<Button>(true))StyleButton(b,b.name=="Fuse"||b.name=="EquipButton"||b.name=="Apply"||b.name=="Retry");
        }
        static void Place(RectTransform r,float x,float y,float w,float h){r.anchorMin=r.anchorMax=new Vector2(.5f,.5f);r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,h);}
        public static void Economy(EconomyWindow w)
        {
            Theme(w.panel);Place((RectTransform)w.panel.transform,0,0,1060,740);
            Place(w.title.rectTransform,0,292,780,70);w.title.fontSize=40;
            Place(w.message.rectTransform,0,-288,920,76);w.message.fontSize=22;w.message.alignment=TextAnchor.MiddleCenter;
            w.message.horizontalOverflow=HorizontalWrapMode.Wrap;w.message.verticalOverflow=VerticalWrapMode.Truncate;
            var close=w.panel.transform.Find("Close") as RectTransform;if(close)Place(close,455,296,100,54);
            Place((RectTransform)w.fusionPanel.transform,0,8,950,480);
            Place(w.firstIcon.rectTransform,-240,68,150,150);Place(w.secondIcon.rectTransform,240,68,150,150);
            Place(w.firstLabel.rectTransform,-240,-67,344,80);Place(w.secondLabel.rectTransform,240,-67,344,80);
            foreach(var t in new[]{w.firstLabel,w.secondLabel}){t.alignment=TextAnchor.MiddleCenter;t.fontSize=25;t.fontStyle=FontStyle.Bold;t.resizeTextForBestFit=false;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Truncate;}
            string[] arrows={"PreviousFirst","NextFirst","PreviousSecond","NextSecond"};float[] xs={-420,-60,60,420};
            for(int i=0;i<4;i++){var r=w.fusionPanel.transform.Find(arrows[i]) as RectTransform;if(r)Place(r,xs[i],68,60,66);}
            Place((RectTransform)w.fuseButton.transform,0,-181,370,68);
            Label(w.fusionPanel.transform,"Material A","첫 번째 무기",new Vector2(-240,192),new Vector2(320,42),21,w.title.font).color=Gold;
            Label(w.fusionPanel.transform,"Material B","두 번째 무기",new Vector2(240,192),new Vector2(320,42),21,w.title.font).color=Gold;
            Label(w.fusionPanel.transform,"Combine Mark","+",new Vector2(0,68),new Vector2(50,60),38,w.title.font).color=Gold;
            for(int i=0;i<w.itemLabels.Length;i++){var t=w.itemLabels[i];t.alignment=TextAnchor.MiddleCenter;t.fontSize=20;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.lineSpacing=1.2f;Place(t.rectTransform,0,-57,190,78);}
        }
    }
}
