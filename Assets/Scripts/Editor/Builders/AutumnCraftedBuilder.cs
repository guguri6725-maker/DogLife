using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;
namespace DogLife.EditorTools
{
    public static class AutumnCraftedBuilder
    {
        const string Art="Assets/Art/AutumnCrafted";
        const string Rooms="Assets/Scenes/SeasonRooms/AutumnJourney9";
        static Material bark,stone,amber;
        static Material Material(string name,string texture,Color color,bool foliage=false)
        {
            string path=Art+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m){m=new Material(Shader.Find(foliage?"DogLife/Autumn Crafted Foliage":"Standard"));AssetDatabase.CreateAsset(m,path);}
            m.color=color;if(texture!=null)m.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"/Textures/"+texture+".png");
            if(foliage){m.SetFloat("_Cutoff",.38f);m.SetFloat("_TintStrength",.8f);}else m.SetFloat("_Glossiness",.14f);
            m.enableInstancing=true;EditorUtility.SetDirty(m);return m;
        }
        static readonly Color[] Colors={new Color(.64f,.70f,.28f),new Color(.93f,.72f,.22f),new Color(1,.61f,.13f),new Color(.96f,.43f,.105f),new Color(.89f,.32f,.09f),new Color(.84f,.235f,.085f),new Color(.76f,.155f,.08f),new Color(.66f,.12f,.07f),new Color(.55f,.22f,.11f)};
        public static void Init()
        {
            Directory.CreateDirectory(Art+"/Materials");Directory.CreateDirectory(Art+"/Prefabs");AssetDatabase.Refresh();
            foreach(string file in new[]{"Maple_Bark","Maple_Leaf","Autumn_Stone"})
            {var t=(TextureImporter)AssetImporter.GetAtPath(Art+"/Textures/"+file+".png");if(t.maxTextureSize!=1024||t.alphaIsTransparency!=(file=="Maple_Leaf")){t.maxTextureSize=1024;t.alphaIsTransparency=file=="Maple_Leaf";t.mipmapEnabled=true;t.wrapMode=file=="Maple_Leaf"?TextureWrapMode.Clamp:TextureWrapMode.Repeat;t.SaveAndReimport();}}
            bark=Material("Crafted_Bark","Maple_Bark",Color.white);stone=Material("Crafted_Stone","Autumn_Stone",new Color(.84f,.82f,.75f));amber=Material("Crafted_Amber",null,new Color(1,.48f,.08f));amber.EnableKeyword("_EMISSION");amber.SetColor("_EmissionColor",new Color(1,.26f,.02f)*1.1f);
        }
        static void Materials(GameObject go,Material leaves)
        {
            foreach(var r in go.GetComponentsInChildren<Renderer>(true))
            {
                r.sharedMaterials=r.sharedMaterials.Select(m=>{string n=m?m.name:"";return n.Contains("Leaf")?leaves:n.Contains("Stone")?stone:n.Contains("Amber")?amber:bark;}).ToArray();
                r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.On;
            }
        }
        static GameObject Model(string name,Transform parent,Vector3 p,float scale,float yaw,Material leaves)
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Art+"/Models/"+name+".fbx");if(!asset)throw new Exception(name);
            var go=(GameObject)PrefabUtility.InstantiatePrefab(asset,parent);PrefabUtility.UnpackPrefabInstance(go,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);go.name=name;go.transform.localScale=Vector3.one*scale;go.transform.rotation=Quaternion.Euler(0,yaw,0);go.transform.position=p;Materials(go,leaves);return go;
        }
        static void Blocker(GameObject go,float radius,float height)
        {go.layer=8;var c=go.AddComponent<CapsuleCollider>();c.radius=radius;c.height=height;c.center=Vector3.up*(height*.5f);}
        static void MakeTree(Transform parent,int variant,Vector3 point,float scale,float yaw,Material leaves)
        {
            var root=new GameObject("Crafted_Maple_"+variant);root.transform.SetParent(parent,false);root.transform.position=point;root.transform.localScale=Vector3.one*scale;root.transform.rotation=Quaternion.Euler(0,yaw,0);
            var high=Model("Maple_Crafted_"+variant,root.transform,point,1,yaw,leaves);high.transform.localPosition=Vector3.zero;high.transform.localRotation=Quaternion.identity;
            var low=Model("Maple_Crafted_LOD",root.transform,point,1,yaw,leaves);low.transform.localPosition=Vector3.zero;low.transform.localRotation=Quaternion.identity;
            var lod=root.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.15f,high.GetComponentsInChildren<Renderer>()),new LOD(.025f,low.GetComponentsInChildren<Renderer>())});lod.RecalculateBounds();Blocker(root,.7f,3);
        }
        static GameObject Plank(Transform parent,string name,Vector3 p,Vector3 size,Material mat)
        {var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=p;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=mat;Object.DestroyImmediate(g.GetComponent<Collider>());return g;}
        public static void Gate(VineGate gate)
        {
            if(PrefabUtility.IsAnyPrefabInstanceRoot(gate.gameObject))PrefabUtility.UnpackPrefabInstance(gate.gameObject,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            foreach(var c in gate.blockers)if(c&&c.transform!=gate.transform)c.transform.SetParent(gate.transform,true);
            foreach(var c in gate.navigationBlockers)if(c&&c.transform!=gate.transform)c.transform.SetParent(gate.transform,true);
            if(gate.visualRoot&&gate.visualRoot!=gate.gameObject)Object.DestroyImmediate(gate.visualRoot);
            if(gate.animationRoot&&gate.animationRoot!=gate.gameObject)Object.DestroyImmediate(gate.animationRoot);
            gate.visualRoot=null;gate.animationRoot=null;gate.openingClip=null;
            var old=gate.transform.Find("Crafted_Autumn_Portal");if(old)Object.DestroyImmediate(old.gameObject);
            var root=new GameObject("Crafted_Autumn_Portal");root.transform.SetParent(gate.transform,false);
            bool sideRoom=gate.name.Contains("Shop_Left")||gate.name.Contains("Synthesis_Right");
            root.transform.localScale=new Vector3(sideRoom?1f:1.24f,sideRoom?.8f:1f,1f);root.transform.position=new Vector3(gate.transform.position.x,.23f,gate.transform.position.z);
            // Original gate is modeled across local X, with its normal along local Z.
            foreach(float x in new[]{-3.8f,3.8f})
            {
                Plank(root.transform,"Carved_Stone_Foot",new Vector3(x,.6f,0),new Vector3(1.1f,1.2f,1.1f),stone);
                Plank(root.transform,"Maple_Timber_Pillar",new Vector3(x,3.1f,0),new Vector3(.6f,5,.6f),bark);
            }
            Plank(root.transform,"Autumn_Lintel",new Vector3(0,5.7f,0),new Vector3(8.6f,.6f,.8f),bark);
            var panels=new Transform[2];
            for(int side=0;side<2;side++)
            {
                var panel=new GameObject("Sliding_Timber_"+side);panel.transform.SetParent(root.transform,false);panel.transform.localPosition=new Vector3(side==0?-1.75f:1.75f,0,0);panels[side]=panel.transform;
                for(int i=0;i<7;i++)Plank(panel.transform,"Weathered_Plank",new Vector3(-1.5f+i*.5f,2.725f,0),new Vector3(.51f,5.45f,.23f),bark);
                foreach(float y in new[]{.8f,3.8f})Plank(panel.transform,"Cross_Brace",new Vector3(0,y,-.16f),new Vector3(3.5f,.25f,.3f),bark);
            }
            var visual=root.AddComponent<AutumnGateVisual>();visual.gate=gate;visual.leftPanel=panels[0];visual.rightPanel=panels[1];visual.leftClosed=panels[0].localPosition;visual.rightClosed=panels[1].localPosition;
            SeasonCraftedArt.Handles(gate);
            if(PrefabUtility.IsPartOfPrefabInstance(gate))PrefabUtility.RecordPrefabInstancePropertyModifications(gate);
        }
        static void Carpet(Transform parent,int index,Material leaves,float x,float width)
        {
            var v=new List<Vector3>();var uv=new List<Vector2>();var tri=new List<int>();var rng=new System.Random(449+index);int count=160+index*65;
            for(int i=0;i<count;i++)
            {
                float px=x+6+(float)rng.NextDouble()*26,z=(rng.Next(2)==0?1:-1)*(5+(float)rng.NextDouble()*(width-8));float size=.2f+(float)rng.NextDouble()*.35f;var q=Quaternion.Euler(0,rng.Next(360),0);var p=new Vector3(px,.28f,z);int n=v.Count;
                foreach(var corner in new[]{new Vector3(-1,0,-1),new Vector3(-1,0,1),new Vector3(1,0,1),new Vector3(1,0,-1)})v.Add(p+q*corner*size);
                uv.AddRange(new[]{Vector2.zero,Vector2.up,Vector2.one,Vector2.right});tri.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
            }
            string path=Art+"/Carpet_"+index+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(!mesh){mesh=new Mesh();AssetDatabase.CreateAsset(mesh,path);}else mesh.Clear();mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(tri,0);mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
            var go=new GameObject("Textured_Fallen_Maple_Leaves");go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=leaves;
        }
        public static string Apply(int index)
        {
            Init();string path=Rooms+"/Autumn_Room"+(index+1).ToString("00")+".unity";var previous=SceneManager.GetActiveScene();var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);SceneManager.SetActiveScene(scene);
            try
            {
                var room=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ForestRoom>(true)).Single();float x=room.entryX;float[] widths={13,16,12,18,15,19,17,20,22};float plus=widths[index],minus=plus-(index%3-1)*3;
                var foliage=Material("Maple_Season_"+(index+1),"Maple_Leaf",Colors[index],true);
                var decor=room.transform.Find("Autumn_UniqueLandmarks");var landmarks=decor.Cast<Transform>().Where(t=>t.name!="Threshold_TrailMarker").Select(t=>new{p=t.position,n=t.name}).ToArray();foreach(Transform t in decor.Cast<Transform>().ToArray())Object.DestroyImmediate(t.gameObject);
                string[] models={"Autumn_StoneLantern","Autumn_FallenTimber","Autumn_RootStump","Autumn_Waystone","Autumn_TrailMarker"};
                for(int i=0;i<landmarks.Length;i++)
                {
                    var p=landmarks[i].p;p.y=.26f;string model=models[(i+index)%models.Length];var go=Model(model,decor,p,1.05f+(i%3)*.15f,index*37+i*61,foliage);Blocker(go,model.Contains("Timber")?1.8f:model.Contains("Shrine")?1.6f:.85f,2);
                }
                // An authored trail marker identifies each threshold without copying old props.
                var mark=Model("Autumn_TrailMarker",decor,new Vector3(x+8,.26f,-5.5f),.75f,90,foliage);mark.name="Threshold_TrailMarker";Blocker(mark,1,2);
                var oldForest=room.transform.Find("Autumn_MapleForest_"+(index+1));if(oldForest)Object.DestroyImmediate(oldForest.gameObject);
                var forest=new GameObject("Autumn_MapleForest_"+(index+1));forest.transform.SetParent(room.transform,false);
                int trees=7+index/2;for(int i=0;i<trees;i++)
                {float px=x+8+(i/2)*5.2f,side=i%2==0?1:-1,z=side*((side>0?plus:minus)-2.7f);if((index==4&&side>0||index==6&&side<0)&&Mathf.Abs(px-(x+19.2f))<6)continue;MakeTree(forest.transform,i%3+1,new Vector3(px,.26f,z),.72f+(i%3)*.1f,index*31+i*59,foliage);}
                Carpet(forest.transform,index,foliage,x,Mathf.Min(plus,minus));
                foreach(var gate in room.GetComponentsInChildren<VineGate>(true))Gate(gate);
                var shell=room.transform.Find("Autumn_ClippedStone_Layout");
                foreach(var renderer in shell.GetComponentsInChildren<Renderer>(true))
                {string n=renderer.name;if(n.Contains("Timber")||n.Contains("Crate")||n.Contains("Counter"))renderer.sharedMaterial=bark;else if(n.Contains("Synthesis_Stone"))renderer.sharedMaterial=stone;}
                var so=new SerializedObject(room);var groups=so.FindProperty("streamedGeometry");groups.arraySize=3;groups.GetArrayElementAtIndex(0).objectReferenceValue=shell.gameObject;groups.GetArrayElementAtIndex(1).objectReferenceValue=decor.gameObject;groups.GetArrayElementAtIndex(2).objectReferenceValue=forest;so.ApplyModifiedPropertiesWithoutUndo();shell.gameObject.SetActive(true);decor.gameObject.SetActive(true);forest.SetActive(true);
                AutumnPlacementRepair.Arrange(room,index);
                var doors=room.GetComponentsInChildren<VineGate>(true);foreach(var d in doors)d.gameObject.SetActive(false);var nav=room.navigation;var existing=nav.navMeshData;nav.RemoveData();nav.navMeshData=null;nav.BuildNavMesh();var generated=nav.navMeshData;EditorUtility.CopySerialized(generated,existing);EditorUtility.SetDirty(existing);nav.navMeshData=existing;Object.DestroyImmediate(generated);foreach(var d in doors)d.gameObject.SetActive(true);
                EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();return "Room "+(index+1)+": crafted trees/props/materials/gates, navigation baked";
            }
            finally{SceneManager.SetActiveScene(previous);EditorSceneManager.CloseScene(scene,true);}
        }
    }
}

