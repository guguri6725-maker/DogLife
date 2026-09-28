using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DogLife.EditorTools
{
    // Jagged coastal rocks remain the visible facade. Solid stone behind them closes
    // bottom gaps and seams without changing the animated doorway or room sequence.
    public static class SummerGateSealing
    {
        static void Stone(Transform parent,string name,Vector3 center,Vector3 size,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;
            go.transform.SetParent(parent,false);go.transform.position=center;go.transform.localScale=size;
            go.GetComponent<Renderer>().sharedMaterial=material;go.layer=8;go.isStatic=true;
        }
        public static string Apply()
        {
            var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/SummerRebuilt/CoastalStone.mat");
            if(!material)throw new System.InvalidOperationException("Missing coastal stone material");
            int count=0;
            foreach(var path in System.IO.Directory.GetFiles("Assets/Scenes/SeasonRooms/Summer","*.unity"))
            {
                var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
                try
                {
                    var room=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ForestRoom>(true)).Single();
                    var old=room.transform.Find("Coastal_Gate_StoneBacking");
                    if(old)Object.DestroyImmediate(old.gameObject);
                    var group=new GameObject("Coastal_Gate_StoneBacking");group.transform.SetParent(room.transform,false);
                    foreach(var gate in new[]{room.rearVines,room.forwardVines})
                    {
                        if(!gate)continue;
                        Stone(group.transform,gate.name+"_ContinuousThreshold",new Vector3(gate.transform.position.x,-.67f,0),new Vector3(2,1,6.6f),material);
                        // Gate banks extend to the side cliffs. 3.2m is the half width
                        // of the existing closed-door blocker; overlap the frame slightly.
                        float outer=22.5f;
                        foreach(var r in room.GetComponentsInChildren<Renderer>(true))
                            if(r.name.StartsWith("SeaCliffWall")&&Mathf.Abs(r.bounds.center.x-gate.transform.position.x)<2)
                                outer=Mathf.Max(outer,Mathf.Abs(r.bounds.center.z)+r.bounds.extents.z);
                        foreach(int sign in new[]{-1,1})
                        {
                            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=gate.name+"_StoneJamb_"+sign;
                            go.transform.SetParent(group.transform,false);
                            go.transform.position=new Vector3(gate.transform.position.x,4.25f,sign*(outer+3.15f)*.5f);
                            go.transform.localScale=new Vector3(1.35f,12.5f,outer-3.15f);
                            go.GetComponent<Renderer>().sharedMaterial=material;go.layer=8;go.isStatic=true;
                            count++;
                        }
                    }
                    // The original passage bed is only 7m wide, whereas its cliff
                    // colliders begin at +/-7.2m. Close that walkable-looking void.
                    float passageEnd=room.exitX+6;
                    Stone(group.transform,"Passage_ContinuousFoundation",new Vector3((room.exitX+passageEnd)*.5f,-.67f,0),new Vector3(8,1,6.6f),material);
                    foreach(int sign in new[]{-1,1})
                        Stone(group.transform,"Passage_SealedShoulder_"+sign,new Vector3((room.exitX+passageEnd)*.5f,4.25f,sign*6.075f),new Vector3(8,12.5f,5.85f),material);
                    EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                }
                finally { EditorSceneManager.CloseScene(scene,true); }
            }
            return count+" stone gate backing panels saved";
        }
    }
}
