using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DogLife.EditorTools
{
    public static class TitleSceneBuilder
    {
        static Font font;
        static Sprite circle;
        static readonly Color Ink = new Color(.045f, .09f, .085f);
        static readonly Color Cream = new Color(.94f, .91f, .81f);
        static readonly Color Pink = new Color(.94f, .57f, .64f);

        static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f,.5f);
            rect.anchoredPosition = position; rect.sizeDelta = size;
            return rect;
        }
        static Image Shape(string name, Transform parent, Vector2 p, Vector2 size, Color color, bool round = false)
        {
            var image = Rect(name,parent,p,size).gameObject.AddComponent<Image>();
            image.color=color; image.raycastTarget=false;
            if(round)image.sprite=circle;
            return image;
        }
        static Text Label(string name, Transform parent, string value, Vector2 p, Vector2 size, int sizeFont, Color color, TextAnchor alignment=TextAnchor.MiddleLeft)
        {
            var text=Rect(name,parent,p,size).gameObject.AddComponent<Text>();
            text.font=font; text.text=value; text.fontSize=sizeFont; text.color=color;
            text.alignment=alignment; text.raycastTarget=false;
            return text;
        }
        static void Branch(Transform parent, Vector2 a, Vector2 b, float width)
        {
            var line=Shape("Branch",parent,(a+b)*.5f,new Vector2(Vector2.Distance(a,b),width),new Color(.31f,.40f,.31f)).rectTransform;
            line.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg);
        }
        static void Flower(Transform parent, Vector2 p, float size)
        {
            for(int i=0;i<5;i++)
            {
                float angle=i*72*Mathf.Deg2Rad;
                var petal=Shape("Petal",parent,p+new Vector2(Mathf.Sin(angle),Mathf.Cos(angle))*size*.3f,new Vector2(size*.47f,size*.66f),Pink,true).rectTransform;
                petal.localRotation=Quaternion.Euler(0,0,-i*72);
            }
            Shape("Flower center",parent,p,Vector2.one*size*.17f,Cream,true);
        }

        public static string Build()
        {
            const string path="Assets/Scenes/Title.unity";
            if(System.IO.File.Exists(path))throw new System.InvalidOperationException("Title scene already exists.");
            if(EditorSceneManager.GetActiveScene().isDirty)throw new System.InvalidOperationException("Save the current scene before building Title.");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/UI/NanumGothic-Regular.ttf");
            circle=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            var camera=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener)).GetComponent<Camera>();
            camera.tag="MainCamera";camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Ink;
            camera.transform.position=new Vector3(0,0,-10);
            var light=new GameObject("Directional Light",typeof(Light)).GetComponent<Light>();light.type=LightType.Directional;light.intensity=.7f;
            var canvas=new GameObject("Title Canvas",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster)).GetComponent<Canvas>();
            canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            var bg=Shape("Background",canvas.transform,Vector2.zero,new Vector2(1920,1080),Ink).rectTransform;
            bg.anchorMin=Vector2.zero;bg.anchorMax=Vector2.one;bg.sizeDelta=Vector2.zero;
            Shape("Moon",canvas.transform,new Vector2(430,60),new Vector2(640,640),new Color(.13f,.23f,.20f),true);
            Shape("Moon inset",canvas.transform,new Vector2(460,90),new Vector2(580,580),Ink,true);
            var art=Rect("Blossom illustration",canvas.transform,new Vector2(450,-80),new Vector2(600,700));
            Branch(art,new Vector2(40,-280),new Vector2(-10,40),16);
            Branch(art,new Vector2(-10,40),new Vector2(120,300),11);
            Branch(art,new Vector2(0,-70),new Vector2(-180,130),10);
            Branch(art,new Vector2(32,120),new Vector2(220,180),8);
            Branch(art,new Vector2(-120,70),new Vector2(-230,65),6);
            Flower(art,new Vector2(120,300),95);Flower(art,new Vector2(-180,130),85);
            Flower(art,new Vector2(220,180),110);Flower(art,new Vector2(-230,65),54);
            Flower(art,new Vector2(15,55),70);Flower(art,new Vector2(70,220),50);
            for(int i=0;i<12;i++)
            {
                var petal=Shape("Floating petal "+i,art,new Vector2(-280+(i*97)%580,-200+(i*137)%620),new Vector2(12,25),new Color(Pink.r,Pink.g,Pink.b,.35f),true).rectTransform;
                petal.localRotation=Quaternion.Euler(0,0,i*31);
            }
            Label("Chapter",canvas.transform,"01  /  THE BLOSSOM PATH",new Vector2(-510,270),new Vector2(660,50),21,Pink);
            var title=Label("Game title",canvas.transform,"DOG LIFE",new Vector2(-490,150),new Vector2(700,150),104,Cream);title.fontStyle=FontStyle.Bold;
            Label("Subtitle",canvas.transform,"뼈에 새겨진 여정, 다시 피어나는 봄.",new Vector2(-490,35),new Vector2(700,60),25,new Color(.66f,.73f,.66f));
            Shape("Title rule",canvas.transform,new Vector2(-540,-50),new Vector2(600,2),new Color(.30f,.39f,.32f));
            var buttonImage=Shape("Start Button",canvas.transform,new Vector2(-650,-170),new Vector2(380,90),Cream);buttonImage.raycastTarget=true;
            var button=buttonImage.gameObject.AddComponent<Button>();button.targetGraphic=buttonImage;
            var colors=button.colors;colors.highlightedColor=Pink;colors.pressedColor=new Color(.72f,.43f,.51f);button.colors=colors;
            Label("Start label",button.transform,"Start   →",Vector2.zero,new Vector2(350,80),32,Ink,TextAnchor.MiddleCenter);
            var status=Label("Loading status",canvas.transform,"봄의 숲에서 여정을 시작하세요.",new Vector2(-500,-265),new Vector2(680,50),20,new Color(.57f,.66f,.58f));
            Label("Footer",canvas.transform,"DOG LIFE     /     SPRING",new Vector2(-580,-460),new Vector2(520,40),16,new Color(.46f,.56f,.49f));
            var controller=new GameObject("Title Controller").AddComponent<TitleScreen>();
            var serialized=new SerializedObject(controller);serialized.FindProperty("startButton").objectReferenceValue=button;serialized.FindProperty("status").objectReferenceValue=status;serialized.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(button.onClick,controller.StartGame);
            new GameObject("EventSystem",typeof(EventSystem),typeof(StandaloneInputModule));
            EditorSceneManager.SaveScene(scene,path);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(path,true)}.Concat(EditorBuildSettings.scenes.Where(s=>s.path!=path)).ToArray();
            return path;
        }
    }
}
