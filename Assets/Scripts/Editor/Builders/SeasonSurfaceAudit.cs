using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace DogLife.EditorTools
{
    public static class SeasonSurfaceAudit
    {
        static readonly MethodInfo Intersect=typeof(HandleUtility).GetMethod("IntersectRayMesh",BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public,null,new[]{typeof(Ray),typeof(Mesh),typeof(Matrix4x4),typeof(RaycastHit).MakeByRefType()},null);
        public static void Geometry(ForestRoom room)
        {
            var so=new SerializedObject(room);var paths=so.FindProperty("streamedPrefabPaths");
            for(int n=0;n<paths.arraySize;n++){var asset=Resources.Load<GameObject>(paths.GetArrayElementAtIndex(n).stringValue);var item=Object.Instantiate(asset,room.transform,false);item.SetActive(true);item.hideFlags=HideFlags.DontSave;}
            var geo=so.FindProperty("streamedGeometry");for(int n=0;n<geo.arraySize;n++){var g=geo.GetArrayElementAtIndex(n).objectReferenceValue as GameObject;if(g)g.SetActive(true);}
            var layout=room.GetComponent<RoomPassageLayout>();if(layout)layout.HideOldPassage();
            if(room.navigation){room.navigation.RemoveData();room.navigation.AddData();}Physics.SyncTransforms();
        }
        public static string Audit(int season,int index,bool capture=false)
        {
            Directory.CreateDirectory("Builds/SurfaceAudit");var original=SceneManager.GetActiveScene();
            var main=EditorSceneManager.OpenScene(SeasonPassageBuilder.Scenes[season],OpenSceneMode.Additive);Scene roomScene=default;
            var messages=new List<string>();int sampled=0,physical=0,visible=0,wallGaps=0;
            try
            {
                SceneManager.SetActiveScene(main);
                var stream=main.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ForestRoomStreaming>(true)).Single();
                roomScene=EditorSceneManager.OpenScene(stream.roomScenePaths[index],OpenSceneMode.Additive);
                var room=roomScene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ForestRoom>(true)).Single();Geometry(room);
                var meshes=main.GetRootGameObjects().Concat(roomScene.GetRootGameObjects()).SelectMany(g=>g.GetComponentsInChildren<MeshFilter>()).Where(f=>f.sharedMesh&&f.GetComponent<Renderer>()&&f.GetComponent<Renderer>().enabled).ToArray();
                float y=season==1?-.17f:.25f;
                var samples=new HashSet<Vector2>();
                // Entire navigable room and side spaces, not only the center walking line.
                for(float x=room.entryX-2;x<room.exitX+2;x+=1)for(float z=-45;z<=45;z+=1)
                {
                    var p=new Vector3(x,y,z);NavMeshHit h;
                    if(NavMesh.SamplePosition(p,out h,.65f,NavMesh.AllAreas)&&Mathf.Abs(h.position.y-y)<1)samples.Add(new Vector2(x,z));
                }
                // Door shoulders and both sides of the new corridor seam.
                foreach(float x0 in new[]{room.entryX,room.exitX})
                    for(float dx=-3;dx<=3;dx+=.5f)for(float z=-6;z<=6;z+=.5f)samples.Add(new Vector2(x0+dx,z));
                foreach(var sample in samples)
                {
                    var p=new Vector3(sample.x,y,sample.y);
                    if(Physics.CheckSphere(p+Vector3.up*.8f,.15f,1<<8,QueryTriggerInteraction.Ignore))continue;
                    sampled++;var ray=new Ray(p+Vector3.up*2.2f,Vector3.down);
                    var hits=Physics.RaycastAll(ray,3f,1<<8,QueryTriggerInteraction.Ignore);
                    if(!hits.Any(h=>h.normal.y>.5f)){physical++;messages.Add("FLOOR collision "+p);}
                    if(!VisibleHit(meshes,ray,3f,true)){visible++;messages.Add("FLOOR visible "+p);}
                }
                // Closed outer silhouette, sampled along entry/exit walls and longitudinal sides.
                // Skip the animated central door opening; its mesh is posed separately in play mode.
                foreach(float h in new[]{.6f,1.6f,3.2f})
                {
                    for(float z=-17;z<=17;z+=1)
                    {
                        if(Mathf.Abs(z)<6)continue;
                        foreach(int dir in new[]{-1,1})
                        {
                            float x=dir<0?room.entryX+5:room.exitX-5;
                            NavMeshHit inside;if(!NavMesh.SamplePosition(new Vector3(x,y,z),out inside,.65f,NavMesh.AllAreas))continue;
                            var ray=new Ray(new Vector3(x,y+h,z),Vector3.right*dir);
                            if(!VisibleHit(meshes,ray,12,false)){wallGaps++;messages.Add("WALL end "+ray.origin+" dir="+dir);}
                        }
                    }
                    for(float x=room.entryX+5;x<room.exitX-5;x+=1)
                        foreach(int sign in new[]{-1,1})
                        {
                            NavMeshHit inside;if(!NavMesh.SamplePosition(new Vector3(x,y,0),out inside,.65f,NavMesh.AllAreas))continue;
                            var ray=new Ray(new Vector3(x,y+h,0),Vector3.forward*sign);
                            if(!VisibleHit(meshes,ray,52,false)){wallGaps++;messages.Add("WALL side "+ray.origin+" dir="+sign);}
                        }
                }
                string prefix="S"+season+"_R"+index;
                if(capture)
                {
                    Capture(prefix+"_entry",new Vector3(room.entryX+5,5,4),new Vector3(room.entryX,.2f,4));
                    Capture(prefix+"_exit",new Vector3(room.exitX-5,5,-4),new Vector3(room.exitX,.2f,-4));
                    Capture(prefix+"_overview",new Vector3((room.entryX+room.exitX)*.5f,32,-22),new Vector3((room.entryX+room.exitX)*.5f,0,0));
                }
                string report=roomScene.name+": samples="+sampled+", missing collision="+physical+", missing visible floor="+visible+", wall rays="+wallGaps+"\n"+string.Join("\n",messages);
                File.WriteAllText("Builds/SurfaceAudit/"+prefix+".txt",report);
                return report.Split('\n')[0]+"\n"+string.Join("\n",messages.Take(12));
            }
            finally{SceneManager.SetActiveScene(original);if(roomScene.IsValid()&&roomScene.isLoaded)EditorSceneManager.CloseScene(roomScene,true);EditorSceneManager.CloseScene(main,true);}
        }
        public static bool VisibleHit(MeshFilter[] meshes,Ray ray,float distance,bool floor)
        {
            foreach(var f in meshes)
            {
                var r=f.GetComponent<Renderer>();float bd;if(!r.bounds.IntersectRay(ray,out bd)||bd>distance)continue;
                object[] args={ray,f.sharedMesh,f.transform.localToWorldMatrix,default(RaycastHit)};
                if((bool)Intersect.Invoke(null,args)){var hit=(RaycastHit)args[3];if(hit.distance<=distance&&(!floor||Mathf.Abs(hit.normal.normalized.y)>.5f))return true;}
            }
            return false;
        }
        public static void Capture(string name,Vector3 position,Vector3 target)
        {
            var go=new GameObject("Surface inspection camera");var camera=go.AddComponent<Camera>();camera.enabled=false;camera.fieldOfView=70;camera.transform.position=position;camera.transform.LookAt(target);
            var rt=new RenderTexture(1000,700,24);var texture=new Texture2D(1000,700,TextureFormat.RGB24,false);var old=RenderTexture.active;
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,1000,700),0,0);texture.Apply();File.WriteAllBytes("Builds/SurfaceAudit/"+name+".png",texture.EncodeToPNG());}
            finally{RenderTexture.active=old;camera.targetTexture=null;rt.Release();Object.DestroyImmediate(texture);Object.DestroyImmediate(rt);Object.DestroyImmediate(go);}
        }
    }
}
