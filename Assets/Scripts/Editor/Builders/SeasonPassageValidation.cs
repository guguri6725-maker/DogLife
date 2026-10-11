using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace DogLife.EditorTools
{
    public static class SeasonPassageValidation
    {
        public static string Verify(int season)
        {
            var original=SceneManager.GetActiveScene();var main=EditorSceneManager.OpenScene(SeasonPassageBuilder.Scenes[season],OpenSceneMode.Additive);
            var issues=new List<string>();int sight=0,walk=0,nav=0;
            try
            {
                SceneManager.SetActiveScene(main);
                var stream=main.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ForestRoomStreaming>(true)).Single();
                var group=main.GetRootGameObjects().Single(g=>g.name=="Season Connecting Passages");
                foreach(Transform passage in group.transform)
                {
                    var walls=passage.GetComponentsInChildren<Collider>();
                    for(float a=-4;a<=4;a+=.5f)for(float b=-4;b<=4;b+=.5f)foreach(float h in new[]{2f,5f,7f})
                    {
                        var start=passage.position+new Vector3(0,h,a);var end=passage.position+new Vector3(18,h,b);var ray=new Ray(start,(end-start).normalized);RaycastHit hit;
                        if(!walls.Any(w=>w.Raycast(ray,out hit,Vector3.Distance(start,end))))issues.Add(passage.name+": visible unloaded exit");sight++;
                    }
                }
                for(int i=0;i<stream.roomScenePaths.Length;i++)
                {
                    var scene=EditorSceneManager.OpenScene(stream.roomScenePaths[i],OpenSceneMode.Additive);
                    try
                    {
                        var room=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ForestRoom>(true)).Single();
                        var so=new SerializedObject(room);var paths=so.FindProperty("streamedPrefabPaths");
                        for(int n=0;n<paths.arraySize;n++){var asset=Resources.Load<GameObject>(paths.GetArrayElementAtIndex(n).stringValue);var item=Object.Instantiate(asset,room.transform,false);item.SetActive(true);}
                        var geometry=so.FindProperty("streamedGeometry");for(int n=0;n<geometry.arraySize;n++){var item=geometry.GetArrayElementAtIndex(n).objectReferenceValue as GameObject;if(item)item.SetActive(true);}
                        room.GetComponent<RoomPassageLayout>().HideOldPassage();
                        if(room.navigation){room.navigation.RemoveData();room.navigation.AddData();}
                        Physics.SyncTransforms();
                        var spawn=room.GetComponent<RoomMonsterSpawner>();NavMeshHit nh;
                        if(spawn&&NavMesh.SamplePosition(spawn.spawnCenter,out nh,8,NavMesh.AllAreas)&&nh.position.x>room.entryX&&nh.position.x<room.exitX)nav++;
                        else issues.Add(scene.name+": moved navigation missing at spawn");
                        if(i<group.transform.childCount)
                        {
                            var passage=group.transform.GetChild(i);var points=new[]{new Vector3(.8f,0,0),new Vector3(3,0,3.15f),new Vector3(7.3f,0,3.15f),new Vector3(10.7f,0,-3.15f),new Vector3(15,0,-3.15f),new Vector3(17.2f,0,0)};
                            for(int n=0;n<points.Length-1;n++)for(int sample=0;sample<=16;sample++)
                            {
                                var pos=passage.position+Vector3.Lerp(points[n],points[n+1],sample/16f);
                                var overlaps=Physics.OverlapCapsule(pos+Vector3.up*.43f,pos+Vector3.up*1.65f,.38f,1<<8,QueryTriggerInteraction.Ignore);
                                var blocked=overlaps.FirstOrDefault(c=>!c.GetComponentInParent<VineGate>());
                                if(blocked)issues.Add(scene.name+": route obstruction "+blocked.name+" at "+pos);
                                RaycastHit floor;if(!Physics.Raycast(pos+Vector3.up*.4f,Vector3.down,out floor,1,1<<8,QueryTriggerInteraction.Ignore))issues.Add(scene.name+": missing floor "+pos);
                                walk++;
                            }
                            if(i==0)Capture(passage,season);
                        }
                    }
                    finally{EditorSceneManager.CloseScene(scene,true);}
                }
            }
            finally{SceneManager.SetActiveScene(original);EditorSceneManager.CloseScene(main,true);}
            string report="Season "+season+": "+sight+" sight lines; "+walk+" walk samples; "+nav+" room navigation checks.\n"+(issues.Count==0?"PASS":string.Join("\n",issues.Distinct().Take(35)));
            File.WriteAllText("Builds/PassageValidation/Season"+season+"_report.txt",report);return report;
        }
        static void Capture(Transform passage,int season)
        {
            var go=new GameObject("Passage inspection camera");var camera=go.AddComponent<Camera>();camera.enabled=false;camera.fieldOfView=78;
            var rt=new RenderTexture(1280,720,24);var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);var old=RenderTexture.active;
            try
            {
                for(int i=0;i<2;i++)
                {
                    camera.transform.position=passage.position+(i==0?new Vector3(1,2.5f,0):new Vector3(7.6f,2.5f,3.1f));
                    var target=passage.position+(i==0?new Vector3(6,2.5f,2):new Vector3(11,2.5f,-2.8f));camera.transform.LookAt(target);
                    camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();File.WriteAllBytes("Builds/PassageValidation/Season"+season+"_"+i+".png",texture.EncodeToPNG());
                }
            }
            finally{RenderTexture.active=old;camera.targetTexture=null;rt.Release();Object.DestroyImmediate(texture);Object.DestroyImmediate(rt);Object.DestroyImmediate(go);}
        }
    }
}
