using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.Events;

namespace DogLife.EditorTools
{
    public static class GameSettingsBuilder
    {
        static Font font;
        static GameObject Element(string name,Transform parent,Vector2 p,Vector2 size)
        {
            var go=new GameObject(name,typeof(RectTransform));var r=go.GetComponent<RectTransform>();r.SetParent(parent,false);
            r.anchorMin=r.anchorMax=new Vector2(.5f,.5f);r.anchoredPosition=p;r.sizeDelta=size;return go;
        }
        static Text Text(Transform parent,string name,string content,Vector2 p,Vector2 size,int fontSize)
        {
            var t=Element(name,parent,p,size).AddComponent<Text>();t.font=font;t.text=content;t.fontSize=fontSize;
            t.color=new Color(.94f,.91f,.81f);t.alignment=TextAnchor.MiddleLeft;t.raycastTarget=false;return t;
        }
        static Button Button(Transform parent,string name,string content,Vector2 p,Vector2 size)
        {
            var go=Element(name,parent,p,size);var image=go.AddComponent<Image>();image.color=new Color(.16f,.25f,.22f);
            var b=go.AddComponent<Button>();b.targetGraphic=image;
            var t=Text(go.transform,"Label",content,Vector2.zero,size,25);t.alignment=TextAnchor.MiddleCenter;return b;
        }
        public static string Build()
        {
            const string path="Assets/Resources/UI/GameSettings.prefab";
            System.IO.Directory.CreateDirectory("Assets/Resources/UI");AssetDatabase.Refresh();
            font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/UI/Fonts/NanumGothic-Regular.ttf");
            var root=new GameObject("Game Settings",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            try
            {
                root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;root.GetComponent<Canvas>().sortingOrder=500;
                var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
                var controller=root.AddComponent<GameSettingsWindow>();
                var open=Button(root.transform,"Open Settings","설정  [Esc]",Vector2.zero,new Vector2(180,54));
                var rect=open.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=Vector2.one;rect.anchoredPosition=new Vector2(-120,-45);
                UnityEventTools.AddPersistentListener(open.onClick,controller.Open);
                var overlay=Element("Settings Panel",root.transform,Vector2.zero,Vector2.zero);
                rect=overlay.GetComponent<RectTransform>();rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;
                overlay.AddComponent<Image>().color=new Color(0,.02f,.02f,.87f);
                var card=Element("Settings Card",overlay.transform,Vector2.zero,new Vector2(1120,950));card.AddComponent<Image>().color=new Color(.045f,.09f,.085f);
                Text(card.transform,"Heading","화면 · 그래픽 설정",new Vector2(0,410),new Vector2(980,66),40);
                Text(card.transform,"Description","내 모니터와 PC 성능에 맞게 조절하세요",new Vector2(0,350),new Vector2(980,40),21);
                string[] names={"모니터","화면 해상도","화면 모드","그래픽 품질","수직 동기화","3D 렌더링 해상도"};
                var labels=new Text[6];var arrows=new Button[12];
                for(int i=0;i<6;i++)
                {
                    float y=278-i*78;
                    Text(card.transform,"Option "+i,names[i],new Vector2(-333,y),new Vector2(315,56),24);
                    labels[i]=Text(card.transform,"Value "+i,"",new Vector2(124,y),new Vector2(540,56),22);labels[i].alignment=TextAnchor.MiddleCenter;
                    arrows[i*2]=Button(card.transform,"Previous "+i,"‹",new Vector2(-190,y),new Vector2(60,54));
                    arrows[i*2+1]=Button(card.transform,"Next "+i,"›",new Vector2(460,y),new Vector2(60,54));
                }
                Text(card.transform,"FPS Heading","FPS 제한",new Vector2(0,-206),new Vector2(980,42),24);
                var buttons=new Button[6];
                for(int i=0;i<6;i++)buttons[i]=Button(card.transform,"FPS "+i,i==5?"제한 없음":PlayerFramePacing.Limits[i]+"",new Vector2(-408+i*163,-260),new Vector2(150,54));
                var status=Text(card.transform,"Status","",new Vector2(0,-314),new Vector2(980,42),20);
                var fps=Text(card.transform,"Live FPS","",new Vector2(0,-351),new Vector2(980,34),18);
                var apply=Button(card.transform,"Apply","적용",new Vector2(230,-412),new Vector2(170,58));UnityEventTools.AddPersistentListener(apply.onClick,controller.Apply);
                var close=Button(card.transform,"Close","닫기 [Esc]",new Vector2(415,-412),new Vector2(170,58));UnityEventTools.AddPersistentListener(close.onClick,controller.Close);
                var confirm=Element("Confirm Display",overlay.transform,Vector2.zero,Vector2.zero);
                rect=confirm.GetComponent<RectTransform>();rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;confirm.AddComponent<Image>().color=new Color(0,0,0,.9f);
                var question=Text(confirm.transform,"Countdown","",new Vector2(0,65),new Vector2(850,170),32);question.alignment=TextAnchor.MiddleCenter;
                var keep=Button(confirm.transform,"Keep","유지",new Vector2(-155,-85),new Vector2(280,65));UnityEventTools.AddPersistentListener(keep.onClick,controller.Keep);
                var revert=Button(confirm.transform,"Revert","되돌리기 [Esc]",new Vector2(155,-85),new Vector2(280,65));UnityEventTools.AddPersistentListener(revert.onClick,controller.Revert);
                var so=new SerializedObject(controller);
                so.FindProperty("panel").objectReferenceValue=overlay;so.FindProperty("confirmation").objectReferenceValue=confirm;
                so.FindProperty("selectionLabel").objectReferenceValue=status;so.FindProperty("liveFps").objectReferenceValue=fps;so.FindProperty("confirmationLabel").objectReferenceValue=question;
                Assign(so,"fpsButtons",buttons);Assign(so,"optionButtons",arrows);Assign(so,"optionLabels",labels);
                so.ApplyModifiedPropertiesWithoutUndo();confirm.SetActive(false);overlay.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root,path);AssetDatabase.SaveAssets();return path;
            }
            finally {Object.DestroyImmediate(root);}
        }
        static void Assign(SerializedObject so,string name,Object[] values)
        {var p=so.FindProperty(name);p.arraySize=values.Length;for(int i=0;i<values.Length;i++)p.GetArrayElementAtIndex(i).objectReferenceValue=values[i];}
    }
}
