using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DogLife.EditorTools
{
    public static class StoryBookBuilder
    {
        static StoryPage P(string image,string text)=>new StoryPage{artwork=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Story/"+image+".png"),text=text};
        static StoryChapter C(string id,string title,string subtitle,string scene,string entrance,Color color,params StoryPage[] pages)
            =>new StoryChapter{id=id,title=title,subtitle=subtitle,sceneName=scene,entrance=entrance,accent=color,pages=pages};
        public static string Build()
        {
            string[] images={"Home","Fear","LastNight","Meadow","WinterDoor","Life","Farewell","Revenge"};
            foreach(var image in images)
            {
                string path="Assets/Art/Story/"+image+".png";
                if(!File.Exists(path))throw new InvalidOperationException("Missing illustration: "+path);
                AssetDatabase.ImportAsset(path);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;importer.mipmapEnabled=false;
                importer.isReadable=false;importer.maxTextureSize=2048;importer.wrapMode=TextureWrapMode.Clamp;
                importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.SaveAndReimport();
            }
            Directory.CreateDirectory("Assets/Resources/Story");
            const string asset="Assets/Resources/Story/StoryLibrary.asset";
            var library=AssetDatabase.LoadAssetAtPath<StoryLibrary>(asset);
            if(!library){library=ScriptableObject.CreateInstance<StoryLibrary>();AssetDatabase.CreateAsset(library,asset);}
            library.font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/UI/Fonts/NanumGothic-Regular.ttf");
            var pink=new Color(.96f,.67f,.72f);var blue=new Color(.54f,.8f,.9f);var amber=new Color(.94f,.67f,.38f);var ice=new Color(.73f,.86f,.94f);
            library.chapters=new[]{
                C("prologue","처음의 기억","DOG LIFE · 사계절 너머의 너에게",null,null,amber,
                    P("Home","웰시코기는 주인의 발소리를 기억한다.\n그 소리가 들릴 때마다 꼬리를 흔들었다."),
                    P("Fear","때로는 문이 열렸고, 때로는 아픔이 찾아왔다.\n그래도 코기는 기다렸다. 언젠가는 자신의 이름을 다정하게 불러줄 거라고 믿으면서."),
                    P("LastNight","반복되는 학대 끝에 코기는 목숨을 잃는다.\n마지막 순간까지 바라본 곳은 주인이 돌아올 문이었다."),
                    P("Meadow","다시 눈을 떴을 때, 코기는 낯선 꽃밭에 누워 있었다.\n아프던 몸은 움직였지만, 어디에도 익숙한 냄새는 없었다."),
                    P("Meadow","그곳은 죽은 존재들이 자신의 기억을 내려놓고 다음 삶으로 떠나는 저승이었다.\n저승의 안내자는 코기에게 말한다."),
                    P("Meadow","“사계절의 끝에는 네가 가장 만나고 싶은 존재에게 닿는 문이 있어.”"),
                    P("Meadow","코기는 망설이지 않고 길을 나선다.\n그 문 너머에서 주인을 만나면, 이번에는 자신을 반겨줄지도 모른다고 생각하며.")),
                C("spring","봄 — 기다림","01 · 문 앞의 약속","Stage1_Spring","문이 열리면, 가장 먼저 달려가고 싶었다.",pink,
                    P("Home","벚꽃이 피어난 숲에는 생전의 따뜻한 기억들이 흩어져 있다.\n처음 집에 들어갔던 날, 자신의 이름을 들었던 순간, 잠깐이나마 머리를 쓰다듬어주던 손."),
                    P("Home","코기는 기억 속 주인의 흔적을 따라간다.\n하지만 숲 깊숙이 들어갈수록 꽃 아래에 묻힌 뼈와 상처가 드러난다."),
                    P("Meadow","길을 가로막는 거대한 멧돼지는 죽은 육체를 뿌리로 붙잡고 있다.\n살점이 벗겨져 뼈만 남아도 멈추지 않는 모습은, 끝난 삶을 놓지 못하는 코기의 마음을 비춘다."),
                    P("Home","코기는 그를 쓰러뜨리고 다시 길을 나선다.\n아직은 주인을 만나고 싶다는 마음뿐이다.")),
                C("summer","여름 — 두려움","02 · 익숙한 발소리","Stage3_Summer","그리운 발소리인데, 몸은 먼저 숨을 곳을 찾았다.",blue,
                    P("Fear","물에 잠긴 저승에서는 기억이 소리와 감각으로 돌아온다.\n쾅 닫히는 문, 다가오는 발소리, 숨을 곳을 찾던 밤."),
                    P("Fear","코기는 그리워하던 냄새에 두려움도 함께 묻어 있었다는 사실을 조금씩 떠올린다."),
                    P("Fear","깊은 바다의 크라켄은 촉수로 코기를 붙잡고 물속으로 끌어내린다.\n도망칠 곳이 사라진 수중에서 코기는 다시 한번 자신보다 거대한 존재와 마주한다."),
                    P("Fear","그러나 이번에는 저항할 수 있다."),
                    P("Fear","크라켄을 쓰러뜨린 뒤에도 주인을 향한 그리움은 남아 있다.\n다만 이제는 그 마음에 작은 질문이 섞인다. ‘나는 왜 그 사람을 무서워했을까?’")),
                C("autumn","가을 — 상실","03 · 돌아오지 않은 손","Stage3_Fall","기다리는 마음이 모두 같은 결말을 맞는 것은 아니었다.",amber,
                    P("LastNight","낙엽이 쌓인 길에는 끝내 돌아오지 못한 영혼들의 기억이 남아 있다."),
                    P("LastNight","누군가에게 버려진 존재, 마지막까지 가족을 기다린 존재,\n사랑받았던 날을 기억하며 편안히 떠나는 존재."),
                    P("LastNight","코기는 그들의 흔적을 지나며 자신이 알지 못했던 관계의 모습을 만난다."),
                    P("LastNight","가을의 사신은 영혼이 붙잡고 있는 기억을 거두어들인다.\n코기가 가장 놓지 못하는 기억은 주인과 함께했던 집이다."),
                    P("LastNight","사신과의 싸움 끝에 코기는 잊고 있던 마지막 밤을 온전히 떠올린다.\n문을 바라보며 기다렸던 자신. 끝내 다가오지 않았던 손."),
                    P("LastNight","그날의 기억은, 기다린다고 모든 사랑이 돌아오는 것은 아니라는 사실을 남긴다.")),
                C("winter","겨울 — 마주함","04 · 마지막 문","Stage4_Winter","아픈 기억을 되찾고도, 나는 그 사람이 보고 싶었다.",ice,
                    P("WinterDoor","눈과 얼음으로 뒤덮인 마지막 길에는 익숙한 냄새조차 남아 있지 않다."),
                    P("WinterDoor","코기는 이제 주인이 자신에게 무엇을 했는지 알고 있다. 그런데도 보고 싶다.\n자신을 아프게 한 존재를 그리워하는 마음은 쉽게 사라지지 않는다."),
                    P("WinterDoor","겨울의 기사는 사계절의 끝으로 향하는 문을 지킨다."),
                    P("WinterDoor","코기는 마지막 싸움에서 살아남기 위해,\n그리고 자신의 기다림이 어디에서 끝나는지 확인하기 위해 앞으로 나아간다."),
                    P("WinterDoor","기사가 쓰러지자 문 너머로 익숙한 발소리가 들린다.")),
                C("life","주인과의 삶","마지막 만남 · 재회",null,null,blue,
                    P("WinterDoor","문이 열리고, 익숙한 발소리가 들린다.\n코기는 무심코 꼬리를 흔들다가, 주인의 손이 움직이자 움찔한다."),
                    P("Life","코기는 한참을 바라본다.\n그리고 멈췄던 걸음을 다시 내딛는다."),
                    P("Life","다시 그의 곁에 머물기로 한다.\n사계절을 넘어 찾아온 기다림은, 주인과 함께하는 삶으로 이어진다.")),
                C("farewell","주인을 보고 헤어짐","마지막 만남 · 이별",null,null,amber,
                    P("WinterDoor","문이 열리고, 익숙한 발소리가 들린다.\n코기는 무심코 꼬리를 흔들다가, 주인의 손이 움직이자 움찔한다."),
                    P("Farewell","코기는 가까이 다가가 익숙한 냄새를 맡는다.\n그토록 만나고 싶었던 존재가 눈앞에 있다."),
                    P("Farewell","잠시 바라보던 코기는 조용히 돌아선다.\n이번에는 누군가를 기다리는 대신, 자신의 길을 걸어간다.")),
                C("revenge","주인을 죽임","마지막 만남 · 복수",null,null,new Color(.82f,.32f,.37f),
                    P("WinterDoor","문이 열리고, 익숙한 발소리가 들린다.\n코기는 무심코 꼬리를 흔들다가, 주인의 손이 움직이자 움찔한다."),
                    P("WinterDoor","그리움 뒤에 눌러두었던 두려움이 분노가 된다.\n코기는 몸을 낮추고, 이빨을 드러낸다."),
                    P("Revenge","코기는 자신을 죽음에 이르게 한 주인을 죽인다.\n발소리는 멎었다. 오랜 기다림 끝에는 침묵만 남았다."))
            };
            EditorUtility.SetDirty(library);
            Directory.CreateDirectory("Assets/Resources/UI");
            var go=new GameObject("Story Book");var book=go.AddComponent<StoryBook>();
            var so=new SerializedObject(book);so.FindProperty("library").objectReferenceValue=library;so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(go,"Assets/Resources/UI/StoryBook.prefab");UnityEngine.Object.DestroyImmediate(go);
            AddTitleButton(library.font);ConnectSeasons();AssetDatabase.SaveAssets();return "StoryLibrary, StoryBook prefab, Title button and seasonal progression saved.";
        }
        static void ConnectSeasons()
        {
            string[] scenes={"Assets/Scenes/Stage1_Spring.unity","Assets/Scenes/Stage3_Summer.unity","Assets/Scenes/Stage3_Fall.unity","Assets/Scenes/Stage4_Winter.unity","Assets/Scenes/Title.unity"};
            for(int i=0;i<4;i++)
            {
                var scene=SceneManager.GetSceneByPath(scenes[i]);bool opened=!scene.isLoaded;
                if(opened)scene=EditorSceneManager.OpenScene(scenes[i],OpenSceneMode.Additive);
                try
                {
                    if(scene.isDirty)throw new InvalidOperationException("Unsaved scene: "+scene.path);
                    foreach(var hud in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<PrototypeHUD>(true)))
                    {hud.nextScene=scenes[i+1];PrefabUtility.RecordPrefabInstancePropertyModifications(hud);EditorUtility.SetDirty(hud);}
                    EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                }
                finally{if(opened)EditorSceneManager.CloseScene(scene,true);}
            }
        }
        static void AddTitleButton(Font font)
        {
            const string path="Assets/Scenes/Title.unity";
            var previous=SceneManager.GetActiveScene();var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.isLoaded;
            if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            if(scene.isDirty)throw new InvalidOperationException("Title has unsaved changes.");
            try
            {
                var title=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<TitleScreen>(true)).Single();
                var canvas=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Canvas>(true)).First();
                var existing=canvas.GetComponentsInChildren<Button>(true).FirstOrDefault(x=>x.name=="Memories Button");
                if(existing){ApplyMemoriesArt(existing);EditorSceneManager.SaveScene(scene);return;}
                // Keep the player's custom menu button dimensions/positions intact.
                var go=new GameObject("Memories Button",typeof(RectTransform),typeof(Image),typeof(Button));go.transform.SetParent(canvas.transform,false);
                var r=(RectTransform)go.transform;r.anchorMin=r.anchorMax=new Vector2(0,0);r.pivot=new Vector2(0,0);r.anchoredPosition=new Vector2(72,62);r.sizeDelta=new Vector2(260,76);
                var image=go.GetComponent<Image>();image.color=new Color(.06f,.12f,.13f,.9f);
                var b=go.GetComponent<Button>();b.targetGraphic=image;
                var label=new GameObject("추억",typeof(RectTransform),typeof(Text));label.transform.SetParent(go.transform,false);
                var lr=(RectTransform)label.transform;lr.anchorMin=Vector2.zero;lr.anchorMax=Vector2.one;lr.sizeDelta=Vector2.zero;
                var text=label.GetComponent<Text>();text.font=font;text.text="추억";text.fontSize=33;text.alignment=TextAnchor.MiddleCenter;text.color=new Color(.98f,.86f,.62f);text.raycastTarget=false;
                text.gameObject.AddComponent<Shadow>().effectDistance=new Vector2(2,-3);
                go.AddComponent<TitleButtonTween>();UnityEventTools.AddPersistentListener(b.onClick,title.OpenMemories);
                ApplyMemoriesArt(b);
                EditorSceneManager.SaveScene(scene);
            }
            finally{if(previous.IsValid())SceneManager.SetActiveScene(previous);if(opened)EditorSceneManager.CloseScene(scene,true);}
        }
        public static void RefreshTitleArt()
        {AddTitleButton(AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/UI/Fonts/NanumGothic-Regular.ttf"));}
        static void ApplyMemoriesArt(Button button)
        {
            const string path="Assets/Art/Story/MemoriesButton.png";
            if(!File.Exists(path))return;
            AssetDatabase.ImportAsset(path);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.alphaIsTransparency=true;
            importer.mipmapEnabled=false;importer.maxTextureSize=1024;importer.SaveAndReimport();
            var image=button.GetComponent<Image>();image.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);image.preserveAspect=true;image.color=Color.white;
            var rect=(RectTransform)button.transform;rect.sizeDelta=new Vector2(320,112);
            foreach(var label in button.GetComponentsInChildren<Text>(true))label.gameObject.SetActive(false);
        }
    }
}
