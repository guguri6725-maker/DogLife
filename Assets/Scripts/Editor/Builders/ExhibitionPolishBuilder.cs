#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace DogLife.EditorTools
{
    public static class ExhibitionPolishBuilder
    {
        static Font Font => AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/UI/NanumGothic-Regular.ttf");
        static RectTransform Rect(string name,Transform parent,Vector2 position,Vector2 size) => AdventureUI.Rect(name,parent,position,size);
        static Image Image(string name,Transform parent,Vector2 position,Vector2 size,Color color)
        {var image=Rect(name,parent,position,size).gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=false;return image;}
        static Text Text(string name,Transform parent,string value,Vector2 pos,Vector2 size,int fontSize)
        {return AdventureUI.Label(parent,name,value,pos,size,fontSize,Font);}
        static Image Fill(string name,Transform parent,Color color)
        {var image=Image(name,parent,Vector2.zero,Vector2.zero,color);var r=image.rectTransform;r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;return image;}
        public static string Build()
        {
            BuildBossHUD();BuildPumpkin();StyleTitle();AssetDatabase.SaveAssets();return "Boss HUD, pumpkin prefab and title cards saved.";
        }
        static void BuildBossHUD()
        {
            Directory.CreateDirectory("Assets/Resources/UI");
            var root=new GameObject("Boss Encounter HUD",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(CanvasGroup),typeof(BossEncounterHUD));
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=35;
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            var hud=root.GetComponent<BossEncounterHUD>();hud.group=root.GetComponent<CanvasGroup>();hud.group.alpha=0;hud.group.blocksRaycasts=false;hud.group.interactable=false;
            var card=Rect("Boss plaque",root.transform,new Vector2(0,-22),new Vector2(760,108));card.anchorMin=card.anchorMax=new Vector2(.5f,1);card.pivot=new Vector2(.5f,1);
            var background=card.gameObject.AddComponent<StoryPanelGraphic>();background.raycastTarget=false;
            hud.emblem=Rect("Season seal",card,new Vector2(-328,2),new Vector2(82,82)).gameObject.AddComponent<SeasonEmblemGraphic>();hud.emblem.raycastTarget=false;
            hud.bossName=Text("Boss name",card,"수확의 사신",new Vector2(25,25),new Vector2(560,34),25);hud.bossName.fontStyle=FontStyle.Bold;
            hud.phaseLabel=Text("Phase",card,"BOSS ENCOUNTER",new Vector2(25,-3),new Vector2(560,24),15);hud.phaseLabel.color=new Color(.86f,.73f,.53f);
            var bed=Image("Health track",card,new Vector2(2,-31),new Vector2(550,16),new Color(.07f,.045f,.035f));
            hud.trail=Fill("Damage trail",bed.transform,new Color(.94f,.74f,.45f));
            hud.fill=Fill("Health",bed.transform,SeasonEmblemGraphic.Accent(0));
            hud.highlight=Fill("Rim light",bed.transform,Color.white);var hr=hud.highlight.rectTransform;hr.anchorMin=new Vector2(0,1);hr.anchorMax=Vector2.one;hr.pivot=new Vector2(.5f,1);hr.sizeDelta=new Vector2(0,2);
            hud.progressLabel=Text("Value",card,"100%",new Vector2(324,-31),new Vector2(82,26),19);
            PrefabUtility.SaveAsPrefabAsset(root,"Assets/Resources/UI/BossEncounterHUD.prefab");Object.DestroyImmediate(root);
        }
        static Material Mat(string path,Color color,float smoothness)
        {
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m){m=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(m,path);}
            m.color=color;m.SetFloat("_Glossiness",smoothness);EditorUtility.SetDirty(m);return m;
        }
        static void BuildPumpkin()
        {
            const string folder="Assets/Art/ExhibitionPolish/Pumpkin/";
            AssetDatabase.ImportAsset(folder+"HarvestPumpkin.fbx",ImportAssetOptions.ForceSynchronousImport);
            foreach(var name in new[]{"HarvestPumpkin_Albedo","HarvestPumpkin_Normal"})
            {
                var path=folder+name+".png";AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.maxTextureSize=1024;
                importer.textureType=name.EndsWith("Normal")?TextureImporterType.NormalMap:TextureImporterType.Default;importer.SaveAndReimport();
            }
            var rind=Mat(folder+"PumpkinRind.mat",Color.white,.24f);rind.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"HarvestPumpkin_Albedo.png");rind.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"HarvestPumpkin_Normal.png"));rind.EnableKeyword("_NORMALMAP");rind.SetFloat("_BumpScale",.45f);
            var stem=Mat(folder+"PumpkinStem.mat",new Color(.19f,.13f,.055f),.08f);
            var ember=Mat(folder+"PumpkinEmber.mat",new Color(.55f,.13f,.012f),.2f);ember.EnableKeyword("_EMISSION");ember.SetColor("_EmissionColor",new Color(1,.31f,.035f)*1.5f);
            var root=new GameObject("Harvest Pumpkin");var visual=root.AddComponent<HarvestPumpkinVisual>();
            var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(folder+"HarvestPumpkin.fbx"));model.transform.SetParent(root.transform,false);
            // FBX forward is +Z in Unity; the original carved face looks along Blender -Y.
            var renderers=model.GetComponentsInChildren<MeshRenderer>();
            foreach(var r in renderers){r.sharedMaterial=r.name.Contains("Ember")?ember:r.name.Contains("Stem")?stem:rind;}
            visual.embers=renderers.Where(r=>r.name.Contains("Ember")).Cast<Renderer>().ToArray();
            Directory.CreateDirectory("Assets/Resources/VFX");PrefabUtility.SaveAsPrefabAsset(root,"Assets/Resources/VFX/HarvestPumpkin.prefab");Object.DestroyImmediate(root);
        }
        static void StyleTitle()
        {
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(scene.path!="Assets/Scenes/Title.unity")throw new System.InvalidOperationException("Open Title before styling exhibition cards.");
            var buttons=Object.FindObjectsByType<Button>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            string[] names={"봄","여름","가을","겨울"},captions={"처음 플레이 추천","심해로 이어지는 계절","수확의 사신이 기다리는 숲","설원의 마지막 전투"};
            for(int i=0;i<4;i++)
            {
                var button=buttons.First(b=>b.name=="Demo Season Button "+i);var rect=(RectTransform)button.transform;
                rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(28,-108-i*102);rect.sizeDelta=new Vector2(330,88);
                AdventureUI.StyleButton(button);
                var tween=new SerializedObject(button.GetComponent<TitleButtonTween>());tween.FindProperty("visual").objectReferenceValue=rect;tween.FindProperty("hoverScale").floatValue=1.035f;tween.ApplyModifiedPropertiesWithoutUndo();
                var label=button.GetComponentsInChildren<Text>(true).First(t=>t.name=="Label");label.text=names[i];label.fontSize=26;label.alignment=TextAnchor.MiddleLeft;label.color=AdventureUI.Cream;label.rectTransform.anchorMin=label.rectTransform.anchorMax=new Vector2(.5f,.5f);label.rectTransform.pivot=new Vector2(.5f,.5f);label.rectTransform.anchoredPosition=new Vector2(38,16);label.rectTransform.sizeDelta=new Vector2(210,34);
                foreach(string name in new[]{"Season emblem","Description","Arrow"}){var old=rect.Find(name);if(old)Object.DestroyImmediate(old.gameObject);}
                var seal=Rect("Season emblem",rect,new Vector2(-117,0),new Vector2(76,76)).gameObject.AddComponent<SeasonEmblemGraphic>();seal.season=i;seal.raycastTarget=false;
                var detail=Text("Description",rect,captions[i],new Vector2(38,-17),new Vector2(210,26),16);detail.alignment=TextAnchor.MiddleLeft;detail.color=i==0?AdventureUI.Gold:new Color(.82f,.73f,.61f);
                Text("Arrow",rect,"›",new Vector2(143,10),new Vector2(24,40),32).color=SeasonEmblemGraphic.Accent(i);
            }
            var header=Object.FindObjectsByType<Text>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(t=>t.name=="Demo Header");
            header.text="사계절 체험";header.fontSize=27;header.fontStyle=FontStyle.Bold;header.color=AdventureUI.Gold;header.alignment=TextAnchor.MiddleLeft;
            var h=header.rectTransform;h.anchorMin=h.anchorMax=h.pivot=new Vector2(0,1);h.anchoredPosition=new Vector2(32,-30);h.sizeDelta=new Vector2(330,38);
            var oldCaption=h.parent.Find("Demo invitation");if(oldCaption)Object.DestroyImmediate(oldCaption.gameObject);
            var caption=Text("Demo invitation",h.parent,"계절을 골라 바로 시작하세요",Vector2.zero,new Vector2(330,28),18);caption.alignment=TextAnchor.MiddleLeft;caption.rectTransform.anchorMin=caption.rectTransform.anchorMax=caption.rectTransform.pivot=new Vector2(0,1);caption.rectTransform.anchoredPosition=new Vector2(32,-72);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        }
    }
}
#endif
