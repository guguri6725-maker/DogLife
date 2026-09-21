using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace DogLife.EditorTools
{
    public static class TitleArtBuilder
    {
        static RectTransform Rect(string name,Transform parent,Vector2 anchor,Vector2 size)
        {
            var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
            r.SetParent(parent,false);r.anchorMin=r.anchorMax=anchor;r.sizeDelta=size;return r;
        }
        public static string Build()
        {
            const string path="Assets/Scenes/Title.unity";
            var previous=SceneManager.GetActiveScene();
            var scene=SceneManager.GetSceneByPath(path);
            bool opened=!scene.isLoaded;
            if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            if(scene.isDirty)throw new System.InvalidOperationException("Title has unsaved changes.");
            SceneManager.SetActiveScene(scene);
            try
            {
                foreach(var go in scene.GetRootGameObjects())Object.DestroyImmediate(go);
                var camera=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener)).GetComponent<Camera>();
                camera.tag="MainCamera";camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.cullingMask=0;
                var canvas=new GameObject("Title Canvas",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster)).GetComponent<Canvas>();
                canvas.renderMode=RenderMode.ScreenSpaceOverlay;
                var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
                var frame=Rect("Title Artwork",canvas.transform,new Vector2(.5f,.5f),new Vector2(1920,1080));
                var fitter=frame.gameObject.AddComponent<AspectRatioFitter>();fitter.aspectMode=AspectRatioFitter.AspectMode.FitInParent;fitter.aspectRatio=646f/363;
                var art=frame.gameObject.AddComponent<RawImage>();art.texture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/UI/TitleWinter.png");art.raycastTarget=false;
                var controller=new GameObject("Title Controller").AddComponent<TitleScreen>();
                var font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/UI/Fonts/NanumGothic-Regular.ttf");
                var status=Rect("Loading Status",frame,new Vector2(.78f,.025f),new Vector2(800,34)).gameObject.AddComponent<Text>();
                status.font=font;status.fontSize=22;status.alignment=TextAnchor.MiddleCenter;status.color=new Color(1,.94f,.80f);status.raycastTarget=false;
                status.gameObject.AddComponent<Outline>().effectColor=new Color(.08f,.05f,.03f,.9f);
                string[] names={"START","LOAD","OPTION","EXIT"};
                var sheet=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/UI/TitleMenu.png");
                Button first=null;
                for(int i=0;i<4;i++)
                {
                    string spritePath="Assets/Art/UI/TitleMenu_"+names[i]+".asset";
                    var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                    if(!sprite){sprite=Sprite.Create(sheet,new UnityEngine.Rect(0,sheet.height*(3-i)/4f,sheet.width,sheet.height/4f),new Vector2(.5f,.5f),100);AssetDatabase.CreateAsset(sprite,spritePath);}
                    var r=Rect(names[i]+" Button",frame,new Vector2(.84f,.40f-i*.10f),new Vector2(445,100));
                    var hit=r.gameObject.AddComponent<Image>();hit.color=Color.clear;
                    var button=r.gameObject.AddComponent<Button>();
                    var label=Rect("Paw and Label",r,new Vector2(.5f,.5f),r.sizeDelta).gameObject.AddComponent<Image>();label.sprite=sprite;label.preserveAspect=true;label.raycastTarget=false;
                    button.targetGraphic=label;var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1.5f,1.3f,.8f);colors.pressedColor=new Color(.65f,.6f,.5f);button.colors=colors;
                    if(i==0){first=button;UnityEventTools.AddPersistentListener(button.onClick,controller.StartGame);}
                    if(i==1)UnityEventTools.AddPersistentListener(button.onClick,controller.LoadGame);
                    if(i==2)UnityEventTools.AddPersistentListener(button.onClick,controller.OpenOptions);
                    if(i==3)UnityEventTools.AddPersistentListener(button.onClick,controller.ExitGame);
                }
                var so=new SerializedObject(controller);so.FindProperty("startButton").objectReferenceValue=first;so.FindProperty("status").objectReferenceValue=status;so.ApplyModifiedPropertiesWithoutUndo();
                new GameObject("EventSystem",typeof(EventSystem),typeof(StandaloneInputModule));
                EditorSceneManager.SaveScene(scene,path);return path;
            }
            finally{SceneManager.SetActiveScene(previous);if(opened)EditorSceneManager.CloseScene(scene,true);}
        }
    }
}
