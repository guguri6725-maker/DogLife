#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace DogLife.EditorTools
{
    public static class SeasonMaterialReview
    {
        public static string Run(int season,bool repair)
        {
            var original=SceneManager.GetActiveScene();
            var main=EditorSceneManager.OpenScene(SeasonPassageBuilder.Scenes[season],OpenSceneMode.Additive);
            var report=new List<string>();
            try
            {
                var stream=main.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ForestRoomStreaming>(true)).Single();
                foreach(var path in stream.roomScenePaths)
                {
                    var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
                    try
                    {
                        var room=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ForestRoom>(true)).Single();
                        int changed=0;
                        // Corridor inner face is z=4.30. A 3.70m return centered at 6.15
                        // produces the exact same plane, causing unstable depth ordering.
                        foreach(var t in room.GetComponentsInChildren<Transform>(true))
                            if(t.name=="Doorway Side Return"&&Mathf.Abs(Mathf.Abs(t.position.z)-6.15f)<.001f)
                            {changed++;if(repair){var p=t.position;p.z=Mathf.Sign(p.z)*6;t.position=p;}}
                        if(repair&&changed>0){EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);}
                        SeasonSurfaceAudit.Geometry(room);
                        int missing=0,uv=0,count=0;
                        foreach(var r in room.GetComponentsInChildren<MeshRenderer>())
                        {
                            count++;
                            if(r.sharedMaterials.Any(m=>!m||!m.shader||!m.shader.isSupported))
                            {missing++;report.Add("MATERIAL "+scene.name+" / "+r.name);}
                            var f=r.GetComponent<MeshFilter>();
                            if(f&&f.sharedMesh&&r.sharedMaterials.Any(m=>m&&m.HasProperty("_MainTex")&&m.GetTexture("_MainTex")&&!(m.shader.name=="DogLife/Coastal Floor Water"&&m.GetFloat("_SurfaceMode")>.5f))&&!f.sharedMesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord0))
                            {uv++;report.Add("UV "+scene.name+" / "+r.name);}
                        }
                        report.Add(scene.name+": meshes="+count+", invalid materials="+missing+", missing UV="+uv+", coplanar returns="+changed+(repair?" corrected":""));
                    }
                    finally{EditorSceneManager.CloseScene(scene,true);}
                }
                Directory.CreateDirectory("Builds/SurfaceAudit");File.WriteAllLines("Builds/SurfaceAudit/Materials_"+season+".txt",report);
                return string.Join("\n",report);
            }
            finally{SceneManager.SetActiveScene(original);EditorSceneManager.CloseScene(main,true);}
        }
    }
}
#endif
