using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace DogLife.EditorTools
{
    public static class SeasonPassageBuilder
    {
        public const string Folder="Assets/Models/Environment/Shared/SeasonPassages";
        public const float Gap=18, HalfWidth=4.3f, FloorThickness=.5f, Height=9;
        public static readonly string[] Scenes={"Assets/Scenes/Stage1_Spring.unity","Assets/Scenes/Stage3_Summer.unity","Assets/Scenes/Stage3_Fall.unity","Assets/Scenes/Stage4_Winter.unity"};
        static readonly string[] Seasons={"Spring","Summer","Fall","Winter"};
        static readonly string[] SeasonFolders={"Spring","Summer","Autumn","Winter"};
        public static string MaterialsFolder(int season)=>"Assets/Materials/Environment/"+SeasonFolders[season]+"/SeasonPassages";
        static readonly string[] StonePaths={"Assets/MapDesigne/Spring/Materials/Spring_CubeWall.mat","Assets/Materials/Environment/Summer/SummerRebuilt/CoastalStone.mat","Assets/Materials/Environment/Autumn/AutumnCrafted/Crafted_Stone.mat","Assets/Materials/Environment/Winter/WinterOriginal/Slate.mat"};
        static readonly float[] Floors={.24f,-.17f,.25f,.25f};
        public static string Build(int season)
        {
            if(Application.isPlaying)throw new InvalidOperationException("Edit mode required");
            Directory.CreateDirectory(Folder);Directory.CreateDirectory("Builds/PassageValidation/Backup");
            var original=SceneManager.GetActiveScene();var main=SceneManager.GetSceneByPath(Scenes[season]);bool opened=!main.isLoaded;
            if(opened)main=EditorSceneManager.OpenScene(Scenes[season],OpenSceneMode.Additive);
            if(main.isDirty)throw new InvalidOperationException("Save current scene changes first: "+main.path);
            try
            {
                Backup(main.path);SceneManager.SetActiveScene(main);
                var stream=main.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ForestRoomStreaming>(true)).Single();
                var old=main.GetRootGameObjects().FirstOrDefault(g=>g.name=="Season Connecting Passages");if(old)Object.DestroyImmediate(old);
                var group=new GameObject("Season Connecting Passages");SceneManager.MoveGameObjectToScene(group,main);
                var stone=CopyMaterial(StonePaths[season],season,Seasons[season]+"_Stone",season==3?new Color(.64f,.73f,.82f):Color.white);
                var floor=CopyMaterial(season==3?"Assets/Materials/Environment/Winter/WinterOriginal/Snow.mat":season==1?"Assets/MapDesigne/Summer/Materials/WaterRevision/CoastalCave_Ground.mat":"Assets/MapDesigne/Spring/Materials/Journey_Ground.mat",season,Seasons[season]+"_Floor",season==3?new Color(.82f,.88f,.94f):season==2?new Color(.8f,.69f,.5f):Color.white);
                float previousExit=0;
                for(int i=0;i<stream.roomScenePaths.Length;i++)
                {
                    string path=stream.roomScenePaths[i];var scene=SceneManager.GetSceneByPath(path);bool roomOpened=!scene.isLoaded;
                    if(roomOpened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
                    if(scene.isDirty)throw new InvalidOperationException("Unsaved room: "+path);
                    try
                    {
                        Backup(path);
                        var room=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ForestRoom>(true)).Single();
                        var layout=room.GetComponent<RoomPassageLayout>();if(!layout)layout=room.gameObject.AddComponent<RoomPassageLayout>();
                        layout.hasOutgoingPassage=i<stream.roomScenePaths.Length-1;
                        float desiredEntry=i==0?room.entryX:previousExit+Gap;
                        float delta=desiredEntry-room.entryX;
                        // Baked navigation is attached to the room root and translates with it.
                        if(room.navigation)room.navigation.RemoveData();
                        foreach(var root in scene.GetRootGameObjects())root.transform.position+=Vector3.right*delta;
                        room.entryX+=delta;room.exitX+=delta;layout.appliedOffset+=delta;
                        foreach(var spawn in room.GetComponentsInChildren<RoomMonsterSpawner>(true)){spawn.spawnCenter+=Vector3.right*delta;EditorUtility.SetDirty(spawn);}
                        if(room.navigation&&room.navigation.isActiveAndEnabled)room.navigation.AddData();
                        layout.HideOldPassage();EditorUtility.SetDirty(room);EditorUtility.SetDirty(layout);
                        foreach(var root in scene.GetRootGameObjects())PrefabUtility.RecordPrefabInstancePropertyModifications(root.transform);
                        if(i>0)CreatePassage(group.transform,Seasons[season]+"_Passage_"+i,previousExit,Floors[season],stone,floor);
                        previousExit=room.exitX;
                        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                    }
                    finally{if(roomOpened)EditorSceneManager.CloseScene(scene,true);}
                }
                // The old outer safety boundary must not remain inside the expanded route.
                foreach(var c in main.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<BoxCollider>(true)).Where(c=>c.transform.parent&&c.transform.parent.name=="Boundaries"))
                {
                    if(c.name=="East")c.transform.position+=Vector3.right*(previousExit+12-c.bounds.center.x);
                    else if((c.name=="North"||c.name=="South")&&c.bounds.max.x<previousExit+12)
                    {
                        float left=c.bounds.min.x,right=previousExit+12;
                        c.transform.position+=Vector3.right*((left+right)*.5f-c.bounds.center.x);
                        var size=c.size;size.x=(right-left)/Mathf.Abs(c.transform.lossyScale.x);c.size=size;
                    }
                    PrefabUtility.RecordPrefabInstancePropertyModifications(c.transform);PrefabUtility.RecordPrefabInstancePropertyModifications(c);
                }
                EditorSceneManager.MarkSceneDirty(main);EditorSceneManager.SaveScene(main);AssetDatabase.SaveAssets();
                return Seasons[season]+": "+(stream.roomScenePaths.Length-1)+" passages; existing animated doors retained";
            }
            finally{SceneManager.SetActiveScene(original);if(opened)EditorSceneManager.CloseScene(main,true);}
        }
        static void Backup(string path)
        {
            string dest="Builds/PassageValidation/Backup/"+Path.GetFileName(path);
            if(!File.Exists(dest))File.Copy(path,dest);
        }
        static Material CopyMaterial(string source,int season,string name,Color tint)
        {
            string path=MaterialsFolder(season)+"/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            var src=AssetDatabase.LoadAssetAtPath<Material>(source);if(!src)throw new InvalidOperationException(source);
            if(!mat){mat=new Material(src);AssetDatabase.CreateAsset(mat,path);}else mat.CopyPropertiesFromMaterial(src);
            mat.color=tint;mat.mainTextureScale=Vector2.one;mat.enableInstancing=true;EditorUtility.SetDirty(mat);return mat;
        }
        static void CreatePassage(Transform parent,string name,float x,float floorY,Material stone,Material floor)
        {
            var root=new GameObject(name);root.transform.SetParent(parent,false);root.transform.position=new Vector3(x,floorY,0);
            Block(root.transform,"Continuous Floor",new Vector3(Gap/2,-FloorThickness/2,0),new Vector3(Gap+1.2f,FloorThickness,HalfWidth*2+1),floor);
            foreach(int sign in new[]{-1,1})
            {
                Block(root.transform,"Outer Stone Wall "+sign,new Vector3(Gap/2,Height/2,sign*(HalfWidth+.45f)),new Vector3(Gap+1.2f,Height,.9f),stone);
                Block(root.transform,"Wall Footing "+sign,new Vector3(Gap/2,.28f,sign*(HalfWidth+.35f)),new Vector3(Gap,.56f,1.05f),stone);
            }
            // Opposite openings force a readable S-shaped route, hiding the unloaded room.
            Block(root.transform,"First Blind Corner",new Vector3(6,Height/2,(-HalfWidth+2)/2),new Vector3(.9f,Height,HalfWidth+2),stone);
            Block(root.transform,"Second Blind Corner",new Vector3(12,Height/2,(HalfWidth-2)/2),new Vector3(.9f,Height,HalfWidth+2),stone);
            Block(root.transform,"Stone Vault",new Vector3(Gap/2,Height+.35f,0),new Vector3(Gap+1.2f,.7f,HalfWidth*2+1.8f),stone);
            // Simple non-shadow lights make the bend legible without illuminating adjacent rooms.
            foreach(float xx in new[]{3f,9f,15f})
            {
                var lamp=new GameObject("Soft Passage Light");lamp.transform.SetParent(root.transform,false);lamp.transform.localPosition=new Vector3(xx,5,0);
                var light=lamp.AddComponent<Light>();light.type=LightType.Point;light.range=7;light.intensity=.65f;light.color=new Color(1,.88f,.68f);light.shadows=LightShadows.None;
            }
        }
        public static void Block(Transform parent,string name,Vector3 position,Vector3 size,Material material)
        {
            // Unique-sized meshes have metre-scaled UVs; large walls do not stretch one texture.
            string key=string.Format(System.Globalization.CultureInfo.InvariantCulture,"Block_{0:0.00}_{1:0.00}_{2:0.00}",size.x,size.y,size.z);
            string path=Folder+"/"+key+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(!mesh)
            {
                var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);mesh=Object.Instantiate(cube.GetComponent<MeshFilter>().sharedMesh);Object.DestroyImmediate(cube);
                var v=mesh.vertices;var normals=mesh.normals;var uv=mesh.uv;
                for(int j=0;j<v.Length;j++){v[j]=Vector3.Scale(v[j],size);var n=normals[j];uv[j]=Mathf.Abs(n.y)>.5f?new Vector2(v[j].x,v[j].z)*.4f:Mathf.Abs(n.x)>.5f?new Vector2(v[j].z,v[j].y)*.4f:new Vector2(v[j].x,v[j].y)*.4f;}
                mesh.vertices=v;mesh.uv=uv;mesh.RecalculateBounds();mesh.name=key;AssetDatabase.CreateAsset(mesh,path);
            }
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=position;go.layer=8;go.isStatic=true;
            go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;go.AddComponent<BoxCollider>().size=size;
        }
    }
}
