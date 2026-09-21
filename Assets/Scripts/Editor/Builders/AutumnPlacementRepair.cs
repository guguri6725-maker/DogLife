using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
namespace DogLife.EditorTools
{
    public static class AutumnPlacementRepair
    {
        static readonly float[] Width = {13,16,12,18,15,19,17,20,22};
        public static Bounds BoundsOf(Transform t)
        {
            var rr=t.GetComponentsInChildren<Renderer>(true);
            if(rr.Length==0) throw new Exception("Missing renderers: "+t.name);
            var b=rr[0].bounds; foreach(var r in rr.Skip(1)) b.Encapsulate(r.bounds);
            return b;
        }
        static bool Inside(Bounds b,float x,int index)
        {
            float p=Width[index],m=p-(index%3-1)*3;
            Vector2[] poly={new Vector2(x,5),new Vector2(x+8,p),new Vector2(x+30,p),new Vector2(x+38.4f,5),new Vector2(x+38.4f,-5),new Vector2(x+30,-m),new Vector2(x+8,-m),new Vector2(x,-5)};
            foreach(float xx in new[]{b.min.x,b.max.x})
            foreach(float zz in new[]{b.min.z,b.max.z})
            for(int i=0;i<8;i++)
            {
                var a=poly[i];var d=poly[(i+1)%8]-a;var v=new Vector2(xx,zz)-a;
                if(d.x*v.y-d.y*v.x > -1.15f*d.magnitude) return false;
            }
            // Keep the combat lane and the entire approach to each side door clear.
            if(b.min.z<2.8f && b.max.z>-2.8f) return false;
            if((index==4 && b.max.z>0 || index==6 && b.min.z<0) &&
               b.min.x<x+24 && b.max.x>x+14.4f) return false;
            return true;
        }
        static bool Overlap(Bounds a,Bounds b)
        {return a.min.x<b.max.x+.4f&&a.max.x>b.min.x-.4f&&a.min.z<b.max.z+.4f&&a.max.z>b.min.z-.4f;}
        public static string Arrange(ForestRoom room,int index)
        {
            var forest=room.transform.Find("Autumn_MapleForest_"+(index+1));
            var decor=room.transform.Find("Autumn_UniqueLandmarks");
            foreach(Transform t in decor.Cast<Transform>().Where(t=>t.name.Contains("OfferingShrine")).ToArray())Object.DestroyImmediate(t.gameObject);
            var items=forest.Cast<Transform>().Where(t=>t.GetComponent<LODGroup>()).Concat(decor.Cast<Transform>()).ToArray();
            var occupied=new List<Bounds>();int removed=0,moved=0;
            foreach(var t in items)
            {
                // The third room is a narrow pass: retain tall trees with narrower crowns.
                if(index==2 && t.GetComponent<LODGroup>())
                {
                    var size=t.localScale;size.x=Mathf.Min(size.x,.42f);size.z=Mathf.Min(size.z,.42f);t.localScale=size;
                }
                var original=t.position;var scale=t.localScale;bool found=false;
                // Place large trees first. Preserve size where possible; do not force excess trees into gaps.
                for(int attempt=0;attempt<(t.GetComponent<LODGroup>()?3:1)&&!found;attempt++)
                {
                    t.localScale=scale*(1-attempt*.1f);
                    t.position=original;var relative=BoundsOf(t);relative.center-=original;
                    float best=float.MaxValue;Vector3 target=original;
                    for(float xx=room.entryX+5;xx<room.entryX+34;xx+=.6f)
                    for(float zz=-Width[index]-3;zz<=Width[index];zz+=.6f)
                    {
                        var pos=new Vector3(xx,original.y,zz);var b=relative;b.center+=pos;
                        if(!Inside(b,room.entryX,index)||occupied.Any(o=>Overlap(b,o)))continue;
                        float score=(pos-original).sqrMagnitude;
                        if(Mathf.Sign(zz)!=Mathf.Sign(original.z))score+=100;
                        if(score<best){best=score;target=pos;found=true;}
                    }
                    if(found){t.position=target;occupied.Add(BoundsOf(t));moved++;}
                }
                if(!found){Object.DestroyImmediate(t.gameObject);removed++;}
            }
            return "placed="+moved+", excess removed="+removed;
        }
        public static string Run(int index,bool repair=true)
        {
            var previous=SceneManager.GetActiveScene();
            string path="Assets/Scenes/SeasonRooms/AutumnJourney9/Autumn_Room"+(index+1).ToString("00")+".unity";
            var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);SceneManager.SetActiveScene(scene);
            try
            {
                var room=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ForestRoom>(true)).Single();
                var groups=new[]{room.transform.Find("Autumn_ClippedStone_Layout"),room.transform.Find("Autumn_UniqueLandmarks"),room.transform.Find("Autumn_MapleForest_"+(index+1))};
                foreach(var g in groups)g.gameObject.SetActive(true);
                string report=repair?Arrange(room,index):"audit";
                var items=groups[2].Cast<Transform>().Where(t=>t.GetComponent<LODGroup>()).Concat(groups[1].Cast<Transform>()).ToArray();
                int wall=0,pairs=0;
                for(int i=0;i<items.Length;i++)
                {
                    var b=BoundsOf(items[i]);if(!Inside(b,room.entryX,index))wall++;
                    for(int j=0;j<i;j++)if(Overlap(b,BoundsOf(items[j])))pairs++;
                }
                if(wall+ pairs>0)throw new Exception("Placement invalid "+wall+"/"+pairs);
                if(repair)
                {
                    var doors=room.GetComponentsInChildren<VineGate>(true);var states=doors.Select(d=>d.gameObject.activeSelf).ToArray();
                    foreach(var d in doors)d.gameObject.SetActive(false);
                    var nav=room.navigation;var existing=nav.navMeshData;nav.RemoveData();nav.navMeshData=null;nav.BuildNavMesh();
                    var generated=nav.navMeshData;EditorUtility.CopySerialized(generated,existing);EditorUtility.SetDirty(existing);nav.navMeshData=existing;Object.DestroyImmediate(generated);
                    for(int i=0;i<doors.Length;i++)doors[i].gameObject.SetActive(states[i]);
                    EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
                }
                return "Room "+(index+1)+": "+report+", wall/corridor violations="+wall+", prop/tree overlaps="+pairs;
            }
            finally {SceneManager.SetActiveScene(previous);EditorSceneManager.CloseScene(scene,true);}
        }
    }
}
