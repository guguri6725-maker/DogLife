using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace DogLife.EditorTools
{
    public static class SeasonSurfaceRepair
    {
        static readonly string[] Names={"Spring","Summer","Fall","Winter"};
        public static string Apply(int season)
        {
            var original=SceneManager.GetActiveScene();var main=EditorSceneManager.OpenScene(SeasonPassageBuilder.Scenes[season],OpenSceneMode.Additive);
            var report=new List<string>();Directory.CreateDirectory("Builds/SurfaceAudit/Backup");
            try
            {
                var stream=main.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ForestRoomStreaming>(true)).Single();
                var stone=AssetDatabase.LoadAssetAtPath<Material>(SeasonPassageBuilder.Folder+"/"+Names[season]+"_Stone.mat");
                var fallbackFloor=AssetDatabase.LoadAssetAtPath<Material>(SeasonPassageBuilder.Folder+"/"+Names[season]+"_Floor.mat");
                for(int i=0;i<stream.roomScenePaths.Length;i++)
                {
                    var path=stream.roomScenePaths[i];var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
                    try
                    {
                        if(scene.isDirty)throw new InvalidOperationException("Unsaved room "+path);
                        var backup="Builds/SurfaceAudit/Backup/"+Path.GetFileName(path);if(!File.Exists(backup))File.Copy(path,backup);
                        var room=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ForestRoom>(true)).Single();
                        var old=room.transform.Find("Surface Seam Reinforcement");if(old)throw new InvalidOperationException("Repair already applied: "+path);
                        // Preview source meshes for accurate material selection and confirmation, then restore before saving.
                        var originalObjects=new HashSet<GameObject>(room.GetComponentsInChildren<Transform>(true).Select(t=>t.gameObject));
                        var active=originalObjects.ToDictionary(g=>g,g=>g.activeSelf);
                        var renderers=room.GetComponentsInChildren<Renderer>(true).ToDictionary(r=>r,r=>r.enabled);
                        var colliders=room.GetComponentsInChildren<Collider>(true).ToDictionary(c=>c,c=>c.enabled);
                        SeasonSurfaceAudit.Geometry(room);
                        var meshes=main.GetRootGameObjects().Concat(scene.GetRootGameObjects()).SelectMany(g=>g.GetComponentsInChildren<MeshFilter>()).Where(f=>f.sharedMesh&&f.GetComponent<Renderer>()&&f.GetComponent<Renderer>().enabled).ToArray();
                        var ground=room.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled&&r.bounds.size.y<1&&r.bounds.size.x>10&&r.bounds.size.z>10&&r.bounds.center.y<1&&r.sharedMaterial&&r.sharedMaterial.renderQueue<2500).OrderByDescending(r=>r.bounds.size.x*r.bounds.size.z).FirstOrDefault();
                        var floor=season==0?AssetDatabase.LoadAssetAtPath<Material>("Assets/MapDesigne/Spring/Materials/Journey_Ground.mat"):(ground?ground.sharedMaterial:fallbackFloor);
                        float y=season==1?-.17f:.25f;
                        var points=new List<Vector3>();string audit="Builds/SurfaceAudit/S"+season+"_R"+i+".txt";
                        foreach(var line in File.ReadAllLines(audit).Where(l=>l.StartsWith("FLOOR")))
                        {
                            var m=Regex.Match(line,@"\(([-\d.]+), ([-\d.]+), ([-\d.]+)\)");if(!m.Success)continue;
                            var p=new Vector3(float.Parse(m.Groups[1].Value,CultureInfo.InvariantCulture),y,float.Parse(m.Groups[3].Value,CultureInfo.InvariantCulture));
                            if(points.Any(v=>(v-p).sqrMagnitude<.01f))continue;
                            var ray=new Ray(p+Vector3.up*2.2f,Vector3.down);
                            bool collision=Physics.RaycastAll(ray,3,1<<8,QueryTriggerInteraction.Ignore).Any(h=>h.normal.y>.5f);
                            if(!collision||!SeasonSurfaceAudit.VisibleHit(meshes,ray,3,true))points.Add(p);
                        }
                        foreach(var t in room.GetComponentsInChildren<Transform>(true).Where(t=>!originalObjects.Contains(t.gameObject)&&originalObjects.Contains(t.parent?t.parent.gameObject:null)).ToArray())Object.DestroyImmediate(t.gameObject);
                        foreach(var pair in active)pair.Key.SetActive(pair.Value);foreach(var pair in renderers)pair.Key.enabled=pair.Value;foreach(var pair in colliders)pair.Key.enabled=pair.Value;
                        var group=new GameObject("Surface Seam Reinforcement");group.transform.SetParent(room.transform,false);group.transform.position=Vector3.zero;
                        // Full-width aprons overlap the irregular doorway facade and the corridor sidewalls.
                        foreach(float xx in new[]{room.entryX,room.exitX})
                            SeasonPassageBuilder.Block(group.transform,"Doorway Foundation",new Vector3(xx,y-.30f,0),new Vector3(8.4f,.54f,16),floor);
                        // Fill only confirmed gaps; group nearby samples into a small number of continuous slabs.
                        int patches=0;
                        while(points.Count>0)
                        {
                            var cluster=new List<Vector3>{points[0]};points.RemoveAt(0);
                            for(int n=0;n<cluster.Count;n++)for(int k=points.Count-1;k>=0;k--)
                                if(Mathf.Abs(cluster[n].x-points[k].x)<=1.1f&&Mathf.Abs(cluster[n].z-points[k].z)<=1.1f){cluster.Add(points[k]);points.RemoveAt(k);}
                            float minX=cluster.Min(p=>p.x)-.7f,maxX=cluster.Max(p=>p.x)+.7f,minZ=cluster.Min(p=>p.z)-.7f,maxZ=cluster.Max(p=>p.z)+.7f;
                            SeasonPassageBuilder.Block(group.transform,"Ground Edge Foundation "+patches,new Vector3((minX+maxX)*.5f,y-.30f,(minZ+maxZ)*.5f),new Vector3(maxX-minX,.54f,maxZ-minZ),floor);patches++;
                        }
                        // Return walls close the reachable pocket beside a wide gate and a narrower corridor.
                        foreach(float xx in new[]{room.entryX-1.6f,room.exitX+1.6f})foreach(int sign in new[]{-1,1})
                            SeasonPassageBuilder.Block(group.transform,"Doorway Side Return",new Vector3(xx,y+4.2f,sign*6f),new Vector3(1.1f,9,3.7f),stone);
                        if(i==0)SeasonPassageBuilder.Block(group.transform,"Rear Entrance Backing",new Vector3(room.entryX-3.5f,y+4.2f,0),new Vector3(.8f,9,16),stone);
                        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                        report.Add(scene.name+": doorway aprons/returns, "+patches+" ground edge patches");
                    }
                    finally{EditorSceneManager.CloseScene(scene,true);}
                }
                AssetDatabase.SaveAssets();File.WriteAllText("Builds/SurfaceAudit/Repair_"+season+".txt",string.Join("\n",report));return string.Join("\n",report);
            }
            finally{SceneManager.SetActiveScene(original);EditorSceneManager.CloseScene(main,true);}
        }
    }
}
