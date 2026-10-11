using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace DogLife.EditorTools
{
    public static class SeasonStorySkyBuilder
    {
        public const string Folder="Assets/Art/SeasonLighting/TimeOfDay";
        static readonly string[] Names={"Spring_Morning","Summer_Afternoon","Fall_Sunset","Winter_Predawn"};
        static readonly string[] Scenes={"Assets/Scenes/Stage1_Spring.unity","Assets/Scenes/Stage3_Summer.unity","Assets/Scenes/Stage3_Fall.unity","Assets/Scenes/Stage4_Winter.unity"};
        static Color C(float r,float g,float b)=>new Color(r,g,b,1);
        static Vector3 Direction(float elevation,float azimuth)=>-(Quaternion.Euler(elevation,azimuth,0)*Vector3.forward);
        public static string Build()
        {
            var shader=Shader.Find("DogLife/Season Story Sky");if(!shader||ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("Sky shader is missing or has errors.");
            Directory.CreateDirectory(Folder);Directory.CreateDirectory("Builds/SkyValidation");
            var original=SceneManager.GetActiveScene();var result=new System.Collections.Generic.List<string>();
            for(int i=0;i<4;i++)
            {
                var scene=SceneManager.GetSceneByPath(Scenes[i]);bool opened=!scene.isLoaded;
                if(opened)scene=EditorSceneManager.OpenScene(Scenes[i],OpenSceneMode.Additive);
                if(scene.isDirty)throw new InvalidOperationException("Unsaved scene: "+scene.path);
                try
                {
                    SceneManager.SetActiveScene(scene);
                    string name=Names[i];var mat=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/"+name+".mat");
                    if(!mat){mat=new Material(shader);AssetDatabase.CreateAsset(mat,Folder+"/"+name+".mat");}mat.shader=shader;
                    var profile=AssetDatabase.LoadAssetAtPath<SeasonSkyProfile>(Folder+"/"+name+".asset");
                    if(!profile){profile=ScriptableObject.CreateInstance<SeasonSkyProfile>();AssetDatabase.CreateAsset(profile,Folder+"/"+name+".asset");}
                    Configure(i,profile,mat);
                    var light=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Light>(true)).First(l=>l.type==LightType.Directional);
                    var environment=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<SeasonSkyEnvironment>(true)).FirstOrDefault();
                    if(!environment){var go=new GameObject("Season Sky — "+profile.timeOfDay);SceneManager.MoveGameObjectToScene(go,scene);environment=go.AddComponent<SeasonSkyEnvironment>();}
                    environment.profile=profile;environment.sun=light;environment.Apply();
                    // Capture only the infinite sky, not other loaded seasons or scene geometry.
                    var goCamera=new GameObject("Temporary sky reflection camera");SceneManager.MoveGameObjectToScene(goCamera,scene);
                    var camera=goCamera.AddComponent<Camera>();camera.enabled=false;camera.clearFlags=CameraClearFlags.Skybox;camera.cullingMask=0;camera.farClipPlane=1000;camera.allowHDR=true;
                    try
                    {
                        var cube=AssetDatabase.LoadAssetAtPath<Cubemap>(Folder+"/"+name+"_Reflection.asset");
                        if(!cube){cube=new Cubemap(128,TextureFormat.RGBAHalf,true);cube.name=name+" reflection";AssetDatabase.CreateAsset(cube,Folder+"/"+name+"_Reflection.asset");}
                        if(!camera.RenderToCubemap(cube))throw new InvalidOperationException("Sky reflection render failed: "+name);
                        EditorUtility.SetDirty(cube);profile.reflection=cube;environment.Apply();
                        Capture(camera,profile.lightRotation.y,18,name+"_sky");
                        if(i==3)Capture(camera,180,27,name+"_aurora");
                    }
                    finally{UnityEngine.Object.DestroyImmediate(goCamera);}
                    EditorUtility.SetDirty(mat);EditorUtility.SetDirty(profile);EditorUtility.SetDirty(environment);EditorUtility.SetDirty(light);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(light);PrefabUtility.RecordPrefabInstancePropertyModifications(light.transform);
                    foreach(var sceneCamera in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>(true)))
                    {EditorUtility.SetDirty(sceneCamera);PrefabUtility.RecordPrefabInstancePropertyModifications(sceneCamera);}
                    EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                    result.Add(name+": sky, ambient, sun, haze, reflection, camera applied");
                }
                finally{SceneManager.SetActiveScene(original);if(opened)EditorSceneManager.CloseScene(scene,true);}
            }
            AssetDatabase.SaveAssets();return string.Join("\n",result);
        }
        static void Configure(int i,SeasonSkyProfile p,Material m)
        {
            p.sky=m;m.SetFloat("_Exposure",1);m.SetFloat("_Stars",0);m.SetFloat("_MoonStrength",0);m.SetFloat("_Aurora",0);
            m.SetFloat("_CloudOpacity",.88f);m.SetFloat("_CloudScale",1.25f);m.SetFloat("_CloudCoverage",.58f);m.SetFloat("_Seed",new[]{2.4f,9.2f,17.3f,24.5f}[i]);
            m.SetVector("_Wind",new Vector4(.0022f,.0008f,0,0));m.SetFloat("_SunSize",.018f);m.SetFloat("_SunStrength",1.2f);
            p.reflectionIntensity=.45f;p.fogStart=60;p.fogEnd=230;
            if(i==0)
            {
                p.timeOfDay="봄 · 이른 아침";p.lightRotation=new Vector3(14,115,0);p.lightColor=C(1,.87f,.73f);p.lightIntensity=1;
                p.ambientSky=C(.60f,.68f,.76f);p.ambientHorizon=C(.55f,.53f,.49f);p.ambientGround=C(.27f,.29f,.24f);p.fogColor=C(.74f,.77f,.79f);
                m.SetColor("_Zenith",C(.30f,.55f,.76f));m.SetColor("_Horizon",C(.9f,.80f,.78f));m.SetColor("_Lower",C(.42f,.51f,.55f));
                m.SetColor("_Dawn",C(1,.70f,.54f));m.SetFloat("_DawnStrength",.48f);m.SetColor("_SunColor",C(1,.87f,.68f));
                m.SetColor("_CloudLight",C(1,.88f,.9f));m.SetColor("_CloudShadow",C(.60f,.67f,.77f));m.SetFloat("_CloudCoverage",.60f);
            }
            else if(i==1)
            {
                p.timeOfDay="여름 · 오후";p.lightRotation=new Vector3(55,235,0);p.lightColor=C(1,.97f,.9f);p.lightIntensity=1.1f;
                p.ambientSky=C(.55f,.67f,.74f);p.ambientHorizon=C(.52f,.62f,.64f);p.ambientGround=C(.27f,.33f,.32f);p.fogColor=C(.65f,.8f,.84f);p.fogStart=85;p.fogEnd=285;p.reflectionIntensity=.55f;
                m.SetColor("_Zenith",C(.10f,.40f,.73f));m.SetColor("_Horizon",C(.70f,.87f,.92f));m.SetColor("_Lower",C(.24f,.46f,.55f));
                m.SetColor("_Dawn",C(1,.91f,.74f));m.SetFloat("_DawnStrength",.045f);m.SetColor("_SunColor",C(1,.97f,.86f));m.SetFloat("_SunStrength",1.4f);
                m.SetColor("_CloudLight",C(1,.99f,.95f));m.SetColor("_CloudShadow",C(.5f,.67f,.79f));m.SetFloat("_CloudCoverage",.58f);m.SetFloat("_CloudScale",1.05f);
            }
            else if(i==2)
            {
                p.timeOfDay="가을 · 노을 지는 초저녁";p.lightRotation=new Vector3(6,260,0);p.lightColor=C(1,.72f,.5f);p.lightIntensity=.85f;
                p.ambientSky=C(.48f,.44f,.58f);p.ambientHorizon=C(.54f,.44f,.36f);p.ambientGround=C(.28f,.25f,.24f);p.fogColor=C(.55f,.42f,.43f);p.fogStart=65;p.reflectionIntensity=.4f;
                m.SetColor("_Zenith",C(.24f,.22f,.43f));m.SetColor("_Horizon",C(.96f,.51f,.32f));m.SetColor("_Lower",C(.31f,.26f,.33f));
                m.SetColor("_Dawn",C(1,.63f,.26f));m.SetFloat("_DawnStrength",.82f);m.SetColor("_SunColor",C(1,.63f,.30f));m.SetFloat("_SunStrength",1.45f);m.SetFloat("_SunSize",.027f);
                m.SetColor("_CloudLight",C(1,.60f,.40f));m.SetColor("_CloudShadow",C(.34f,.28f,.43f));m.SetFloat("_CloudCoverage",.55f);
            }
            else
            {
                p.timeOfDay="겨울 · 해 뜨기 전 새벽";p.lightRotation=new Vector3(24,280,0);p.lightColor=C(.7f,.81f,1);p.lightIntensity=.68f;
                p.ambientSky=C(.39f,.48f,.67f);p.ambientHorizon=C(.4f,.46f,.58f);p.ambientGround=C(.25f,.3f,.4f);p.fogColor=C(.28f,.36f,.5f);p.fogStart=65;p.reflectionIntensity=.4f;
                m.SetColor("_Zenith",C(.035f,.07f,.16f));m.SetColor("_Horizon",C(.36f,.44f,.61f));m.SetColor("_Lower",C(.1f,.15f,.23f));
                m.SetColor("_Dawn",C(.68f,.44f,.51f));m.SetFloat("_DawnStrength",.32f);m.SetFloat("_SunStrength",0);m.SetColor("_SunColor",C(.8f,.61f,.68f));
                m.SetColor("_CloudLight",C(.42f,.52f,.70f));m.SetColor("_CloudShadow",C(.14f,.21f,.34f));m.SetFloat("_CloudCoverage",.68f);m.SetFloat("_CloudOpacity",.55f);
                m.SetFloat("_Stars",.65f);m.SetFloat("_MoonStrength",.85f);m.SetFloat("_Aurora",.35f);m.SetColor("_MoonColor",C(.65f,.8f,1));m.SetVector("_MoonDirection",Direction(24,280));
            }
            m.SetVector("_SunDirection",i==3?Direction(-7,100):Direction(p.lightRotation.x,p.lightRotation.y));
        }
        static void Capture(Camera camera,float lightAzimuth,float elevation,string name)
        {
            // Light transform looks toward the ground: look opposite in azimuth to frame its visible disc.
            camera.transform.rotation=Quaternion.Euler(-elevation,lightAzimuth+180,0);camera.fieldOfView=85;
            var rt=new RenderTexture(1280,720,24);camera.targetTexture=rt;camera.Render();
            var old=RenderTexture.active;RenderTexture.active=rt;var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();
            File.WriteAllBytes("Builds/SkyValidation/"+name+".png",texture.EncodeToPNG());RenderTexture.active=old;camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
