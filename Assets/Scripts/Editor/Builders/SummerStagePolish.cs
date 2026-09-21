using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
namespace DogLife.EditorTools
{
    public static class SummerStagePolish
    {
        public static void ConfigureStage(Scene scene)
        {
            foreach(var c in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<BoxCollider>(true)).Where(c=>c.transform.parent&&c.transform.parent.name=="Boundaries"))
            {
                bool horizontal=c.name=="North"||c.name=="South";
                c.transform.position=horizontal?new Vector3(151,5,c.name=="North"?60:-100):new Vector3(c.name=="East"?315:-12,5,-20);
                c.transform.localScale=Vector3.one;c.size=horizontal?new Vector3(328,12,1):new Vector3(1,12,162);
                PrefabUtility.RecordPrefabInstancePropertyModifications(c.transform);PrefabUtility.RecordPrefabInstancePropertyModifications(c);
            }
            foreach(var c in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>(true)).Where(c=>c.CompareTag("MainCamera")))
            {c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=new Color(.4f,.69f,.8f);PrefabUtility.RecordPrefabInstancePropertyModifications(c);}
        }
        public static string Apply()
        {
            var main=SceneManager.GetSceneByPath("Assets/Scenes/Stage3_Summer.unity");bool opened=!main.isLoaded;if(opened)main=EditorSceneManager.OpenScene("Assets/Scenes/Stage3_Summer.unity",OpenSceneMode.Additive);
            ConfigureStage(main);EditorSceneManager.SaveScene(main);if(opened)EditorSceneManager.CloseScene(main,true);
            for(int i=0;i<7;i++){
                var s=EditorSceneManager.OpenScene("Assets/Scenes/SeasonRooms/SummerJourney7/Summer_Room"+(i+1).ToString("00")+".unity",OpenSceneMode.Additive);
                try{
                    var bank=s.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Single(t=>t.name=="Sandy_Shore_Bank");
                    string path="Assets/Art/SeasonCrafted/SummerBank"+i+".asset";
                    var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if(!mesh){
                        mesh=UnityEngine.Object.Instantiate(bank.GetComponent<MeshFilter>().sharedMesh);
                        var v=mesh.vertices;var n=mesh.normals;var uv=new Vector2[v.Length];
                        for(int j=0;j<v.Length;j++){var p=Vector3.Scale(v[j],bank.localScale);uv[j]=Mathf.Abs(n[j].y)>.5f?new Vector2(p.x,p.z)*.2f:Mathf.Abs(n[j].z)>.5f?new Vector2(p.x,p.y)*.2f:new Vector2(p.z,p.y)*.2f;}
                        mesh.uv=uv;AssetDatabase.CreateAsset(mesh,path);
                    }
                    bank.GetComponent<MeshFilter>().sharedMesh=mesh;EditorSceneManager.SaveScene(s);
                }finally{EditorSceneManager.CloseScene(s,true);}
            }
            var water=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/SeasonCrafted/Materials/CoastalWater.mat");
            water.color=new Color(.025f,.29f,.36f);EditorUtility.SetDirty(water);AssetDatabase.SaveAssets();
            return "Stage boundaries, sky background and shoreline UV repaired.";
        }
    }
}
