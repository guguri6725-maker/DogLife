using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DogLife.EditorTools
{
    // Editor-only authoring: all geometry and navigation are saved, never generated during play.
    public static class AutumnNineRoomBuilder
    {
        const string Folder="Assets/Scenes/SeasonRooms/AutumnJourney9";
        const string Art="Assets/Art/AutumnJourney9";
        static readonly string[] Titles={"EarlyAutumn_Trail","Golden_Bend","Windfallen_Pass","Amber_Ruins","Harvest_Market","Copper_Roots","Crimson_Workshop","Scarlet_Grove","LateAutumn_Sanctuary"};
        static readonly float[] Width={13,16,12,18,15,19,17,20,22};
        static readonly Color[] LeafColors={new Color(.58f,.66f,.15f),new Color(.78f,.67f,.13f),new Color(.94f,.57f,.08f),new Color(.92f,.40f,.055f),new Color(.88f,.29f,.04f),new Color(.76f,.20f,.035f),new Color(.68f,.12f,.035f),new Color(.57f,.075f,.03f),new Color(.43f,.12f,.045f)};
        static Material ground,stone,cap,bark;
        static Material Mat(string name,Color color)
        {
            string path=Art+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m){m=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(m,path);}
            m.color=color;m.SetFloat("_Glossiness",.12f);EditorUtility.SetDirty(m);return m;
        }
        static void Init()
        {
            Directory.CreateDirectory(Folder);Directory.CreateDirectory(Art);AssetDatabase.Refresh();
            ground=AssetDatabase.LoadAssetAtPath<Material>("Assets/MapDesigne/Spring/Materials/Spring_Room01_TexturedGround.mat");
            stone=AssetDatabase.LoadAssetAtPath<Material>("Assets/MapDesigne/Spring/Materials/Spring_CubeWall.mat");
            cap=AssetDatabase.LoadAssetAtPath<Material>("Assets/MapDesigne/Spring/Materials/Spring_CubeWall_Coping.mat");
            bark=Mat("Weathered_Maple_Bark",new Color(.24f,.135f,.065f));
        }
        sealed class Geometry
        {
            public List<Vector3> v=new List<Vector3>();public List<Vector2> uv=new List<Vector2>();public List<int> triangles=new List<int>();
            public void Tri(Vector3 a,Vector3 b,Vector3 c,bool doubleSide=false)
            {int n=v.Count;v.AddRange(new[]{a,b,c});uv.AddRange(new[]{new Vector2(a.x,a.z)/38.4f,new Vector2(b.x,b.z)/38.4f,new Vector2(c.x,c.z)/38.4f});triangles.AddRange(new[]{n,n+1,n+2});if(doubleSide)Tri(c,b,a);}
            public void Branch(Vector3 a,Vector3 b,float ra,float rb)
            {
                var q=Quaternion.FromToRotation(Vector3.up,(b-a).normalized);
                for(int i=0;i<7;i++){float t=i*Mathf.PI*2/7,u=(i+1)*Mathf.PI*2/7;Vector3 p=q*new Vector3(Mathf.Cos(t),0,Mathf.Sin(t)),r=q*new Vector3(Mathf.Cos(u),0,Mathf.Sin(u));Tri(a+p*ra,b+p*rb,b+r*rb);Tri(a+p*ra,b+r*rb,a+r*ra);}
            }
            public void Leaf(Vector3 p,Quaternion q,float size)
            {
                Vector3[] outline={new Vector3(0,0,1),new Vector3(.20f,0,.36f),new Vector3(.70f,0,.65f),new Vector3(.46f,0,.03f),new Vector3(.85f,0,-.10f),new Vector3(.22f,0,-.35f),new Vector3(0,0,-.55f),new Vector3(-.22f,0,-.35f),new Vector3(-.85f,0,-.10f),new Vector3(-.46f,0,.03f),new Vector3(-.7f,0,.65f),new Vector3(-.2f,0,.36f)};
                for(int i=0;i<outline.Length;i++)Tri(p+q*(Vector3.up*.045f*size),p+q*(outline[(i+1)%outline.Length]*size),p+q*(outline[i]*size),true);
            }
            public GameObject Save(string name,Transform parent,Material material,bool solid=false)
            {
                string path=Art+"/"+name+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(!mesh){mesh=new Mesh();AssetDatabase.CreateAsset(mesh,path);}else mesh.Clear();
                mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
                var go=new GameObject(name);go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;go.isStatic=true;
                if(solid){go.layer=8;go.AddComponent<MeshCollider>().sharedMesh=mesh;}return go;
            }
        }
        static GameObject Box(Transform parent,string name,Vector3 p,Vector3 size,Material m,bool solid=true)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.position=p;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=m;go.isStatic=true;go.layer=solid?8:0;if(!solid)Object.DestroyImmediate(go.GetComponent<Collider>());
            if(m==stone||m==cap)
            {
                string path=Art+"/StoneUV_"+Mathf.RoundToInt(size.x*100)+"_"+Mathf.RoundToInt(size.y*100)+"_"+Mathf.RoundToInt(size.z*100)+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(!mesh){mesh=Object.Instantiate(go.GetComponent<MeshFilter>().sharedMesh);var vertices=mesh.vertices;var normals=mesh.normals;var tex=new Vector2[vertices.Length];for(int i=0;i<vertices.Length;i++){var v=Vector3.Scale(vertices[i],size);tex[i]=Mathf.Abs(normals[i].y)>.5f?new Vector2(v.x,v.z)*.3f:Mathf.Abs(normals[i].z)>.5f?new Vector2(v.x*.3f,v.y*.3f):new Vector2(v.z*.3f,v.y*.3f);}mesh.uv=tex;AssetDatabase.CreateAsset(mesh,path);}go.GetComponent<MeshFilter>().sharedMesh=mesh;
            }
            return go;
        }
        static void Wall(Transform parent,Vector3 a,Vector3 b,float height=5.5f)
        {
            var d=b-a;var wall=Box(parent,"Weathered_Stone_Boundary",(a+b)*.5f+Vector3.up*(height*.5f),new Vector3(d.magnitude+.1f,height,1.25f),stone);wall.transform.rotation=Quaternion.Euler(0,-Mathf.Atan2(d.z,d.x)*Mathf.Rad2Deg,0);
            var coping=Box(parent,"Stone_Coping",(a+b)*.5f+Vector3.up*height,new Vector3(d.magnitude+.15f,.35f,1.45f),cap,false);coping.transform.rotation=wall.transform.rotation;
        }
        static void Floor(Transform parent,string name,IList<Vector3> points)
        {
            var g=new Geometry();var center=points.Aggregate(Vector3.zero,(a,b)=>a+b)/points.Count;
            for(int i=0;i<points.Count;i++){var a=points[i];var b=points[(i+1)%points.Count];if(Vector3.Cross(a-center,b-center).y<0)g.Tri(center,b,a);else g.Tri(center,a,b);}g.Save(name,parent,ground,true);
        }
        static GameObject Prop(Transform parent,string set,string name,Vector3 position,float width,float yaw,bool solid=true)
        {
            SeasonCraftedArt.Init();
            string model=name.Contains("Stump")?"ExposedRoots":name.Contains("Tree")?"AncientSnag":"CoastalDriftwood";
            return SeasonCraftedArt.Model(model,parent,position,width/4f,yaw,solid);
        }
        static void Forest(Transform parent,int index,float x,float positive,float negative)
        {
            var random=new System.Random(8401+index*73);Func<float> rand=()=> (float)random.NextDouble();var wood=new Geometry();var leaves=new[]{new Geometry(),new Geometry(),new Geometry()};
            int count=7+index;float[] hues={.78f,1,1.2f};
            for(int i=0;i<count;i++)
            {
                float px=x+7+(i/2f%5)*5.5f;float side=i%2==0?1:-1;float z=side*(side>0?positive:negative)-side*(2+rand()*2);
                if((index==4&&side>0||index==6&&side<0)&&Mathf.Abs(px-(x+19.2f))<6)continue;
                var p=new Vector3(px,.26f,z);float height=6+rand()*4;var lean=new Vector3((rand()-.5f)*2,0,(rand()-.5f)*2);var top=p+Vector3.up*height+lean;wood.Branch(p,top,.45f+rand()*.2f,.12f);
                for(int branch=0;branch<5;branch++)
                {
                    float a=branch*Mathf.PI*.4f+rand();var end=top+new Vector3(Mathf.Cos(a)*2.5f,rand()*1.5f-1,Mathf.Sin(a)*2.5f);wood.Branch(Vector3.Lerp(p,top,.5f+rand()*.2f),end,.18f,.035f);
                    int density=index==8?24:28+index*3;
                    for(int j=0;j<density;j++){var offset=new Vector3((rand()-.5f)*4,(rand()-.5f)*2,(rand()-.5f)*3);leaves[(j+branch)%3].Leaf(end+offset,Quaternion.Euler(rand()*80-40,rand()*360,rand()*60-30),.33f+rand()*.24f);}
                }
                var collider=new GameObject("Maple_Trunk_Blocker");collider.transform.SetParent(parent,false);collider.transform.position=p+Vector3.up*2;collider.layer=8;var capsule=collider.AddComponent<CapsuleCollider>();capsule.height=4;capsule.radius=.65f;
            }
            wood.Save("Room"+index+"_MapleBranches",parent,bark);
            for(int i=0;i<3;i++){Color color=LeafColors[index]*hues[i];color.a=1;leaves[i].Save("Room"+index+"_MapleLeaves"+i,parent,Mat("Maple_Room"+(index+1)+"_"+i,color));}
            var groundLeaves=new Geometry();int scatter=100+index*65;
            for(int i=0;i<scatter;i++)
            {
                float px=x+5+rand()*28;float z=(rand()>.5f?1:-1)*(5+rand()*(Mathf.Min(positive,negative)-7));
                groundLeaves.Leaf(new Vector3(px,.28f+rand()*.02f,z),Quaternion.Euler(0,rand()*360,0),.14f+rand()*.22f);
            }
            groundLeaves.Save("Room"+index+"_FallenMapleCarpet",parent,Mat("FallenLeaves_Room"+(index+1),Color.Lerp(LeafColors[index],new Color(.42f,.20f,.04f),.25f)));
        }
        static void Label(Transform parent,string text,Vector3 point,Color color)
        {var go=new GameObject(text+"_Sign");go.transform.SetParent(parent,false);go.transform.position=point;go.transform.rotation=Quaternion.Euler(65,0,0);var label=go.AddComponent<TextMesh>();label.text=text;label.anchor=TextAnchor.MiddleCenter;label.characterSize=.35f;label.fontSize=64;label.color=color;}
        static VineGate SideRoom(ForestRoom room,Transform parent,int index,float x,float width)
        {
            bool shop=index==4;float sign=shop?1:-1;float cx=x+19.2f,z=sign*(width+13);var p=new Vector3(cx,.25f,z);string key=shop?"Shop_Left":"Synthesis_Right";
            Floor(parent,"Room"+index+"_"+key+"_Floor",new[]{new Vector3(cx-9,.25f,z+10),new Vector3(cx+9,.25f,z+10),new Vector3(cx+9,.25f,z-10),new Vector3(cx-9,.25f,z-10)});
            Floor(parent,"Room"+index+"_SidePassage",new[]{new Vector3(cx-4,.25f,sign*(width-1)),new Vector3(cx+4,.25f,sign*(width-1)),new Vector3(cx+4,.25f,sign*(width+5)),new Vector3(cx-4,.25f,sign*(width+5))});
            Wall(parent,new Vector3(cx-9,0,z-10),new Vector3(cx-9,0,z+10),4);Wall(parent,new Vector3(cx+9,0,z-10),new Vector3(cx+9,0,z+10),4);
            Wall(parent,new Vector3(cx-9,0,z+sign*10),new Vector3(cx+9,0,z+sign*10),4);
            Wall(parent,new Vector3(cx-9,0,z-sign*10),new Vector3(cx-4,0,z-sign*10),4);Wall(parent,new Vector3(cx+4,0,z-sign*10),new Vector3(cx+9,0,z-sign*10),4);
            Wall(parent,new Vector3(cx-4,0,sign*width),new Vector3(cx-4,0,z-sign*10),4);Wall(parent,new Vector3(cx+4,0,sign*width),new Vector3(cx+4,0,z-sign*10),4);
            var gate=Object.Instantiate(room.forwardVines.gameObject,room.transform);gate.name=key+"_Entrance";gate.transform.position=new Vector3(cx,-.12f,sign*width);gate.transform.rotation=Quaternion.Euler(0,90,0)*room.forwardVines.transform.rotation;var vines=gate.GetComponent<VineGate>();vines.startOpen=false;
            if(shop)
            {
                var cloth=Mat("Harvest_Market_Canvas",new Color(.58f,.22f,.065f));var gold=Mat("Harvest_Market_Trim",new Color(.85f,.59f,.19f));
                foreach(float dx in new[]{-4f,4f})foreach(float dz in new[]{-2.5f,2.5f})Box(parent,"Market_Timber_Post",p+new Vector3(dx,2.5f,dz),new Vector3(.24f,5,.24f),bark);
                var roof=Box(parent,"Copper_Canvas_Canopy",p+Vector3.up*5,new Vector3(9,.22f,6.5f),cloth,false);roof.transform.rotation=Quaternion.Euler(0,0,5);
                Box(parent,"Merchant_Counter",p+new Vector3(0,1,-2),new Vector3(7,1.6f,1.3f),bark);
                for(int i=0;i<5;i++){Box(parent,"Supply_Crate",p+new Vector3(-6+i*2.8f,.6f,4),Vector3.one*1.2f,i%2==0?bark:gold);var orb=GameObject.CreatePrimitive(PrimitiveType.Sphere);orb.name="Market_Wares";orb.transform.SetParent(parent);orb.transform.position=p+new Vector3(-2+i,2,-2);orb.transform.localScale=Vector3.one*.5f;orb.GetComponent<Renderer>().sharedMaterial=gold;Object.DestroyImmediate(orb.GetComponent<Collider>());}
                Label(parent,"SHOP",p+new Vector3(0,.15f,-6),new Color(1,.78f,.3f));
            }
            else
            {
                Box(parent,"Synthesis_Stone_Table",p+Vector3.up*1.15f,new Vector3(5,2,3),stone);
                var glow=Mat("Synthesis_Amber_Crystal",new Color(1,.52f,.09f));glow.EnableKeyword("_EMISSION");glow.SetColor("_EmissionColor",new Color(1,.25f,.025f)*1.5f);
                for(int i=0;i<6;i++){float a=i*Mathf.PI/3;var c=Box(parent,"Amber_Crystal",p+new Vector3(Mathf.Cos(a)*4,1.1f,Mathf.Sin(a)*4),new Vector3(.55f,2,.55f),glow);c.transform.rotation=Quaternion.Euler(12,i*60,20);}
                var orb=GameObject.CreatePrimitive(PrimitiveType.Sphere);orb.name="Synthesis_Core";orb.transform.SetParent(parent);orb.transform.position=p+Vector3.up*3.1f;orb.transform.localScale=Vector3.one*1.5f;orb.GetComponent<Renderer>().sharedMaterial=glow;Object.DestroyImmediate(orb.GetComponent<Collider>());
                Label(parent,"SYNTHESIS",p+new Vector3(0,.15f,6),new Color(1,.66f,.22f));
            }
            return vines;
        }
        public static string Build(int index)
        {
            string result=BuildLayout(index);
            AutumnCraftedBuilder.Apply(index);
            return result;
        }
        static string BuildLayout(int index)
        {
            if(EditorApplication.isPlaying||index<0||index>8)throw new InvalidOperationException();Init();
            string path=Folder+"/Autumn_Room"+(index+1).ToString("00")+".unity";var previous=SceneManager.GetActiveScene();
            var template=EditorSceneManager.OpenScene("Assets/Scenes/SeasonRooms/Spring/Spring_Room01.unity",OpenSceneMode.Additive);EditorSceneManager.SaveScene(template,path,true);EditorSceneManager.CloseScene(template,true);
            var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);SceneManager.SetActiveScene(scene);
            try
            {
                var room=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ForestRoom>(true)).Single();room.name="Autumn_"+(index+1).ToString("00")+"_"+Titles[index];
                var keep=new HashSet<GameObject>{room.encounter,room.forwardVines.gameObject,room.rearVines.gameObject,room.exit.gameObject};
                foreach(Transform child in room.transform.Cast<Transform>().ToArray())if(!keep.Contains(child.gameObject))Object.DestroyImmediate(child.gameObject);
                float x=index*44.4f;room.index=index;room.entryX=x;room.exitX=x+38.4f;room.forwardVines.transform.position=new Vector3(room.exitX,-.12f,0);room.rearVines.transform.position=new Vector3(x,-.12f,0);room.rearVines.startOpen=index>0;room.rearVines.hideWhenInitiallyOpen=false;room.exit.position=new Vector3(room.exitX+3,1,0);room.recoveryStations=new Transform[0];room.sideVines=new VineGate[0];
                var spawn=room.GetComponent<RoomMonsterSpawner>();spawn.spawnCenter=new Vector3(x+21,.4f,0);spawn.spawnArea=new Vector2(18,10);
                var shell=new GameObject("Autumn_ClippedStone_Layout");shell.transform.SetParent(room.transform,false);var decor=new GameObject("Autumn_UniqueLandmarks");decor.transform.SetParent(room.transform,false);var forest=new GameObject("Autumn_MapleForest_"+(index+1));forest.transform.SetParent(room.transform,false);
                float plus=Width[index],minus=Width[index]-(index%3-1)*3;
                var outline=new[]{new Vector3(x,.25f,5),new Vector3(x+8,.25f,plus),new Vector3(x+30,.25f,plus),new Vector3(x+38.4f,.25f,5),new Vector3(x+38.4f,.25f,-5),new Vector3(x+30,.25f,-minus),new Vector3(x+8,.25f,-minus),new Vector3(x,.25f,-5)};
                Floor(shell.transform,"Room"+index+"_ClippedFloor",outline);
                for(int i=0;i<outline.Length;i++)
                {
                    if(i==3||i==7)continue;var a=outline[i];a.y=0;var b=outline[(i+1)%outline.Length];b.y=0;
                    if((index==4&&i==1)||(index==6&&i==5)){Wall(shell.transform,a,new Vector3(i==1?x+15.2f:x+23.2f,0,a.z));Wall(shell.transform,new Vector3(i==1?x+23.2f:x+15.2f,0,a.z),b);}else Wall(shell.transform,a,b);
                }
                Floor(shell.transform,"Room"+index+"_ForwardPassage",new[]{new Vector3(x+38.4f,.25f,5),new Vector3(x+44.4f,.25f,5),new Vector3(x+44.4f,.25f,-5),new Vector3(x+38.4f,.25f,-5)});
                Wall(shell.transform,new Vector3(x+38.4f,0,5),new Vector3(x+44.4f,0,5));Wall(shell.transform,new Vector3(x+38.4f,0,-5),new Vector3(x+44.4f,0,-5));
                // Asymmetric landmarks: center lane remains broad enough for deer and player dodges.
                int landmarks=3+index/2;
                for(int j=0;j<landmarks;j++)
                {
                    string[] names={"FallenLog","HollowStump","DeadBranch","AncientTree","BrokenStump"};float px=x+9+(j%4)*6.2f;float sign=(j+index)%2==0?1:-1;float z=sign*(7+(j%2)*2.5f);
                    if((index==4&&sign>0||index==6&&sign<0)&&Mathf.Abs(px-(x+19.2f))<5)continue;
                    Prop(decor.transform,"BossRoom",names[(j+index)%names.Length],new Vector3(px,.26f,z),3.2f+(j%3),23+index*41+j*57);
                }
                if(index==3||index==5||index==8)for(int j=0;j<3;j++)Prop(decor.transform,"FirstRoom",index==5?"RootTerrace":"RuinsWall",new Vector3(x+10+j*9,.26f,(j%2==0?1:-1)*(Width[index]-5)),5,30+j*55);
                if(index==8)Prop(decor.transform,"BossRoom","AncientTree",new Vector3(x+24,.26f,15),10,170);
                Forest(forest.transform,index,x,plus,minus);
                if(index==4||index==6)room.sideVines=new[]{SideRoom(room,shell.transform,index,x,index==4?plus:minus)};
                var so=new SerializedObject(room);so.FindProperty("streamedPrefabPaths").arraySize=0;var groups=so.FindProperty("streamedGeometry");groups.arraySize=3;groups.GetArrayElementAtIndex(0).objectReferenceValue=shell;groups.GetArrayElementAtIndex(1).objectReferenceValue=decor;groups.GetArrayElementAtIndex(2).objectReferenceValue=forest;so.ApplyModifiedPropertiesWithoutUndo();
                var doors=room.GetComponentsInChildren<VineGate>(true);foreach(var d in doors)d.gameObject.SetActive(false);
                var nav=room.navigation;nav.RemoveData();nav.navMeshData=null;nav.BuildNavMesh();string navPath=Folder+"/Autumn_Room"+(index+1).ToString("00")+"_NavMesh.asset";var existing=AssetDatabase.LoadAssetAtPath<UnityEngine.AI.NavMeshData>(navPath);if(existing){var generated=nav.navMeshData;EditorUtility.CopySerialized(generated,existing);nav.navMeshData=existing;Object.DestroyImmediate(generated);}else AssetDatabase.CreateAsset(nav.navMeshData,navPath);
                foreach(var d in doors){d.gameObject.SetActive(true);PrefabUtility.RecordPrefabInstancePropertyModifications(d);}EditorSceneManager.SaveScene(scene,path);AssetDatabase.SaveAssets();return path+" / "+Titles[index];
            }
            finally{SceneManager.SetActiveScene(previous);EditorSceneManager.CloseScene(scene,true);}
        }
        public static string Connect()
        {
            var previous=SceneManager.GetActiveScene();const string path="Assets/Scenes/Stage3_Fall.unity";var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.isLoaded;if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            try
            {
                var stream=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ForestRoomStreaming>(true)).Single();stream.roomScenePaths=Enumerable.Range(1,9).Select(i=>Folder+"/Autumn_Room"+i.ToString("00")+".unity").ToArray();var so=new SerializedObject(stream);so.FindProperty("geometryWarmupPaths").arraySize=0;so.ApplyModifiedPropertiesWithoutUndo();
                var builds=EditorBuildSettings.scenes.ToList();foreach(string p in stream.roomScenePaths)if(!builds.Any(b=>b.path==p))builds.Add(new EditorBuildSettingsScene(p,true));EditorBuildSettings.scenes=builds.ToArray();EditorSceneManager.SaveScene(scene);return "Connected 9 rooms, shop left of 5, synthesis right of 7";
            }
            finally{SceneManager.SetActiveScene(previous);if(opened)EditorSceneManager.CloseScene(scene,true);}
        }
    }
}

