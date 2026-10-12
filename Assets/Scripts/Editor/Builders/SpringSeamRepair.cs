#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace DogLife.EditorTools
{
    public static class SpringSeamRepair
    {
        public static string Apply()
        {
            if(Application.isPlaying)throw new System.InvalidOperationException("Edit mode required");
            var main=SceneManager.GetSceneByPath(SeasonPassageBuilder.Scenes[0]);
            bool opened=!main.isLoaded;
            if(opened)main=EditorSceneManager.OpenScene(SeasonPassageBuilder.Scenes[0],OpenSceneMode.Additive);
            int fixedRooms=0;
            try
            {
                var stream=main.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ForestRoomStreaming>(true)).Single();
                var floor=AssetDatabase.LoadAssetAtPath<Material>("Assets/MapDesigne/Spring/Materials/Journey_Ground.mat");
                foreach(var path in stream.roomScenePaths)
                {
                    var scene=SceneManager.GetSceneByPath(path);bool loaded=scene.isLoaded;
                    if(!loaded)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
                    try
                    {
                        var room=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ForestRoom>(true)).Single();
                        var reinforcement=room.transform.Find("Surface Seam Reinforcement");
                        if(!reinforcement)continue;
                        foreach(var t in reinforcement.GetComponentsInChildren<Transform>(true))
                        {
                            if(t.name=="Doorway Side Return")
                            {
                                var p=t.position;p.z=Mathf.Sign(p.z)*6f;t.position=p;
                            }
                            if(t.name=="Doorway Foundation"||t.name.StartsWith("Ground Edge Foundation"))
                                t.GetComponent<Renderer>().sharedMaterial=floor;
                        }
                        // Side rooms and the boss arena exceed the normal room's 42m width.
                        // Measure the actual streamed shell instead of assuming a fixed footprint.
                        var originals=room.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t,t=>t.gameObject.activeSelf);
                        var renderers=room.GetComponentsInChildren<Renderer>(true).ToDictionary(r=>r,r=>r.enabled);
                        var colliders=room.GetComponentsInChildren<Collider>(true).ToDictionary(c=>c,c=>c.enabled);
                        SeasonSurfaceAudit.Geometry(room);
                        var footprint=new Bounds(new Vector3((room.entryX+room.exitX)*.5f,0,0),new Vector3(room.exitX-room.entryX+8,1,42));
                        foreach(var shell in room.GetComponentsInChildren<Renderer>(true).Where(r=>r.name.Contains("_Shell")))footprint.Encapsulate(shell.bounds);
                        foreach(var t in room.GetComponentsInChildren<Transform>(true).Where(t=>!originals.ContainsKey(t)&&t.parent&&originals.ContainsKey(t.parent)).ToArray())Object.DestroyImmediate(t.gameObject);
                        foreach(var pair in originals)pair.Key.gameObject.SetActive(pair.Value);
                        foreach(var pair in renderers)pair.Key.enabled=pair.Value;
                        foreach(var pair in colliders)pair.Key.enabled=pair.Value;
                        var old=reinforcement.Find("Continuous Edge Underlay");if(old)Object.DestroyImmediate(old.gameObject);
                        var world=new Vector3(footprint.center.x,-.10f,footprint.center.z);
                        SeasonPassageBuilder.Block(reinforcement,"Continuous Edge Underlay",reinforcement.InverseTransformPoint(world),new Vector3(footprint.size.x+.6f,.5f,footprint.size.z+.6f),floor);
                        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);fixedRooms++;
                    }
                    finally{if(!loaded)EditorSceneManager.CloseScene(scene,true);}
                }
            }
            finally{if(opened)EditorSceneManager.CloseScene(main,true);}
            return "Spring rooms repaired: "+fixedRooms;
        }
    }
}
#endif
