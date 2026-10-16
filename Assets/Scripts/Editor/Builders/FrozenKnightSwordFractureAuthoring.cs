#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace DogLife.EditorTools
{
    public static class FrozenKnightSwordFractureAuthoring
    {
        // 손잡이와 중심 날은 남기고 양쪽 날이 순서대로 떨어져 나가는 실제 손상 메시입니다.
        public static string Build()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Monster/Winter/FrozenKnight/FrozenKnight.prefab");
            var sword=prefab.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Sword").GetComponentInChildren<MeshFilter>().sharedMesh;
            var vertices=sword.vertices;var bounds=sword.bounds;
            for(int stage=1;stage<=2;stage++)
            {
                var mesh=Object.Instantiate(sword);mesh.name="SwordChipped"+stage;
                for(int sub=0;sub<mesh.subMeshCount;sub++)
                {
                    var source=sword.GetTriangles(sub);var triangles=new List<int>();
                    for(int i=0;i<source.Length;i+=3)
                    {
                        var p=(vertices[source[i]]+vertices[source[i+1]]+vertices[source[i+2]])/3;
                        bool blade=p.y<bounds.min.y+bounds.size.y*.73f;
                        float edge=bounds.extents.x*(.35f+.14f*Mathf.Sin(p.y*47));
                        bool removed=blade&&(p.x>bounds.center.x+edge||(stage==2&&p.x<bounds.center.x-edge));
                        if(!removed){triangles.Add(source[i]);triangles.Add(source[i+1]);triangles.Add(source[i+2]);}
                    }
                    mesh.SetTriangles(triangles,sub);
                }
                mesh.RecalculateBounds();string path="Assets/Models/Monster/Winter/FrozenKnight/"+mesh.name+".asset";
                var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(old){EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);}else AssetDatabase.CreateAsset(mesh,path);
            }
            AssetDatabase.SaveAssets();return "Two chipped sword meshes authored";
        }
    }
}
#endif
