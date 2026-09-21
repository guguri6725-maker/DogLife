using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace DogLife.EditorTools
{
    public static class SeasonCraftedArt
    {
        public const string Art="Assets/Art/SeasonCrafted";
        public static Material Bark,Stone,Leaf,Bronze,Blue,Canvas;
        public static Material Make(string name,Color color,int quadrant=-1)
        {
            string path=Art+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m){m=new Material(Shader.Find(quadrant<0?"Standard":"DogLife/Season Atlas"));AssetDatabase.CreateAsset(m,path);}
            m.color=color;m.enableInstancing=true;
            if(quadrant>=0){m.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"/Textures/SeasonAtlas.png");m.SetVector("_Atlas",new Vector4(quadrant%2*.5f,quadrant<2?.5f:0,.5f,.5f));}
            else m.SetFloat("_Glossiness",name=="Bronze"?.45f:.16f);
            if(name=="Bronze")m.SetFloat("_Metallic",.7f);
            EditorUtility.SetDirty(m);return m;
        }
        public static void Init()
        {
            Bark=Make("AncientBark",Color.white,0);Stone=Make("CoastalStone",Color.white,2);Leaf=Make("PalmLeaf",Color.white,3);
            Bronze=Make("Bronze",new Color(.52f,.31f,.095f));Blue=Make("SeaCanvas",new Color(.08f,.58f,.74f));Canvas=Make("IvoryCanvas",new Color(.98f,.9f,.74f));
            var importer=(TextureImporter)AssetImporter.GetAtPath(Art+"/Textures/SeasonAtlas.png");
            if(importer.maxTextureSize!=2048){importer.maxTextureSize=2048;importer.mipmapEnabled=true;importer.SaveAndReimport();}
        }
        public static GameObject Model(string name,Transform parent,Vector3 position,float scale,float yaw,bool solid=true)
        {
            var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Art+"/Models/"+name+".fbx"),parent);
            PrefabUtility.UnpackPrefabInstance(go,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            go.name=name;go.transform.position=position;go.transform.rotation=Quaternion.Euler(0,yaw,0);go.transform.localScale=Vector3.one*scale;
            foreach(var r in go.GetComponentsInChildren<Renderer>()){
                r.sharedMaterials=r.sharedMaterials.Select(m=>m.name.Contains("Leaf")?Leaf:m.name.Contains("Stone")?Stone:m.name.Contains("Bronze")?Bronze:m.name.Contains("Blue")?Blue:m.name.Contains("Canvas")?Canvas:Bark).ToArray();
                r.gameObject.isStatic=true;
            }
            var bounds=AutumnPlacementRepair.BoundsOf(go.transform);go.transform.position+=Vector3.up*(position.y-bounds.min.y);
            if(solid){
                go.layer=8;var c=go.AddComponent<BoxCollider>();bounds=AutumnPlacementRepair.BoundsOf(go.transform);
                c.center=go.transform.InverseTransformPoint(bounds.center);c.size=bounds.size/scale;
                if(name.Contains("Palm")){c.center=new Vector3(0,1.4f,0);c.size=new Vector3(.8f,2.8f,.8f);}
            }
            return go;
        }
        public static void Handles(VineGate gate)
        {
            Init();var visual=gate.GetComponentInChildren<AutumnGateVisual>(true);if(!visual)return;
            foreach(var panel in new[]{visual.leftPanel,visual.rightPanel}){
                foreach(var old in panel.Cast<Transform>().Where(t=>t.name=="BronzeDoorRing").ToArray())UnityEngine.Object.DestroyImmediate(old.gameObject);
                foreach(int face in new[]{-1,1}){
                    var go=Model("BronzeDoorRing",panel,Vector3.zero,1.65f,0,false);
                    go.transform.localPosition=new Vector3(panel==visual.leftPanel?1.22f:-1.22f,2.6f,face*.27f);
                    go.transform.localRotation=Quaternion.Euler(0,face<0?180:0,0);
                    foreach(var r in go.GetComponentsInChildren<Renderer>())r.gameObject.isStatic=false;
                }
            }
        }
        public static string AddAutumn(int index)
        {
            Init();var previous=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/SeasonRooms/AutumnJourney9/Autumn_Room"+(index+1).ToString("00")+".unity",UnityEditor.SceneManagement.OpenSceneMode.Additive);
            try{
                var room=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ForestRoom>(true)).Single();
                foreach(Transform t in room.transform)if(t.name.StartsWith("Autumn_"))t.gameObject.SetActive(true);
                foreach(var gate in room.GetComponentsInChildren<VineGate>(true))Handles(gate);
                var decor=room.transform.Find("Autumn_UniqueLandmarks");
                foreach(var t in decor.Cast<Transform>().Where(t=>t.name=="AncientSnag"||t.name=="ExposedRoots").ToArray())UnityEngine.Object.DestroyImmediate(t.gameObject);
                for(int j=0;j<4;j++)Model(j==0?"AncientSnag":"ExposedRoots",decor,new Vector3(room.entryX+9+j*6,.25f,j%2==0?5:-5),j==0?.55f:.45f,37*j+index*29,j==0);
                AutumnPlacementRepair.Arrange(room,index);
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
                return "Room "+(index+1)+": new root/deadwood props="+decor.Cast<Transform>().Count(t=>t.name=="AncientSnag"||t.name=="ExposedRoots");
            }finally{UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);}
        }
    }
}
