using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace DogLife.EditorTools
{
    public static class SpringLodBuilder
    {
        const string Work = "Library/SpringLODWorking";
        const string Output = "Assets/MapDesigne/Spring/LOD";
        [Serializable] public class Part { public int[] triangles; }
        [Serializable] public class Data
        {
            public Vector3[] vertices, normals;
            public Vector2[] uv;
            public Part[] parts;
        }
        static string Key(Mesh mesh)
        {
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh, out string guid, out long id);
            return guid + "_" + id;
        }
        static bool Eligible(MeshFilter f)
        {
            var m = f.sharedMesh;
            if (!m || f.GetComponentInParent<LODGroup>() || f.transform.Find("SpringLOD")) return false;
            string p = AssetDatabase.GetAssetPath(m);
            return p.StartsWith("Assets/MapDesigne/Spring/Models/") && !p.EndsWith("_Structure.fbx") && m.vertexCount > 15000;
        }
        static IEnumerable<string> Scenes() => Directory.GetFiles("Assets/Scenes/SeasonRooms/Spring", "*.unity");
        public static string Export()
        {
            if (Directory.Exists(Output) && Directory.GetFiles(Output,"*_0.asset").Length > 0)
                throw new InvalidOperationException("LOD sources already exported. Reuse the existing export.");
            Directory.CreateDirectory(Work); Directory.CreateDirectory(Output); AssetDatabase.Refresh();
            var seen = new HashSet<string>(); int count = 0;
            foreach (string path in Scenes())
            {
                var scene = EditorSceneManager.OpenPreviewScene(path);
                try
                {
                    foreach (var f in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MeshFilter>(true)).Where(Eligible))
                    {
                        var mesh = f.sharedMesh; string key = Key(mesh); if (!seen.Add(key)) continue;
                        var vertices = mesh.vertices; var normals = mesh.normals; var uv = mesh.uv;
                        var cells = new Dictionary<Vector2Int, List<int>[]>();
                        for (int s = 0; s < mesh.subMeshCount; s++)
                        {
                            var indices = mesh.GetTriangles(s);
                            for (int t = 0; t < indices.Length; t += 3)
                            {
                                var center = (vertices[indices[t]] + vertices[indices[t+1]] + vertices[indices[t+2]]) / 3;
                                var cell = new Vector2Int(Mathf.FloorToInt(center.x / 12), Mathf.FloorToInt(center.z / 12));
                                if (!cells.TryGetValue(cell, out var lists))
                                { lists = Enumerable.Range(0, mesh.subMeshCount).Select(_ => new List<int>()).ToArray(); cells.Add(cell, lists); }
                                lists[s].AddRange(new[]{indices[t],indices[t+1],indices[t+2]});
                            }
                        }
                        foreach (var cell in cells)
                        {
                            var remap = new Dictionary<int,int>(); var originals = new List<int>();
                            var parts = cell.Value.Select(list => new Part { triangles = list.Select(index => {
                                if (!remap.TryGetValue(index, out int mapped)) { mapped = originals.Count; remap.Add(index,mapped); originals.Add(index); } return mapped;
                            }).ToArray() }).ToArray();
                            var data = new Data { vertices = originals.Select(i=>vertices[i]).ToArray(), normals = originals.Select(i=>normals[i]).ToArray(), uv = originals.Select(i=>uv.Length>i?uv[i]:Vector2.zero).ToArray(), parts = parts };
                            string name = key + "_" + cell.Key.x + "_" + cell.Key.y;
                            var chunk = MakeMesh(data, name);
                            var tangents = mesh.tangents; if (tangents.Length == vertices.Length) chunk.tangents = originals.Select(i=>tangents[i]).ToArray();
                            var uv2 = mesh.uv2; if (uv2.Length == vertices.Length) chunk.uv2 = originals.Select(i=>uv2[i]).ToArray();
                            var colors = mesh.colors; if(colors.Length==vertices.Length)chunk.colors=originals.Select(i=>colors[i]).ToArray();
                            AssetDatabase.CreateAsset(chunk, Output + "/" + name + "_0.asset");
                            File.WriteAllText(Work + "/" + name + ".json", JsonUtility.ToJson(data)); count++;
                        }
                    }
                }
                finally { EditorSceneManager.ClosePreviewScene(scene); }
            }
            AssetDatabase.SaveAssets(); return count + " chunks exported";
        }
        static Mesh MakeMesh(Data d, string name)
        {
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32, vertices = d.vertices, normals = d.normals, uv = d.uv, subMeshCount = d.parts.Length };
            for(int i=0;i<d.parts.Length;i++)mesh.SetTriangles(d.parts[i].triangles,i);
            mesh.RecalculateBounds(); mesh.RecalculateTangents(); return mesh;
        }
        static Data Compact(Data data)
        {
            var map=new Dictionary<(Vector3,Vector3,Vector2),int>();
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();
            foreach(var part in data.parts)
                for(int t=0;t<part.triangles.Length;t++)
                {
                    int i=part.triangles[t];var key=(data.vertices[i],data.normals[i],data.uv[i]);
                    if(!map.TryGetValue(key,out int index))
                    {index=vertices.Count;map.Add(key,index);vertices.Add(key.Item1);normals.Add(key.Item2);uv.Add(key.Item3);}
                    part.triangles[t]=index;
                }
            data.vertices=vertices.ToArray();data.normals=normals.ToArray();data.uv=uv.ToArray();return data;
        }
        public static string ImportAndApply()
        {
            foreach(string source in Directory.GetFiles(Work,"*.json").Where(p=>!p.Contains("_lod")))
                for(int level=1;level<=2;level++)
                    if(!File.Exists(Path.ChangeExtension(source,null)+"_lod"+level+".json"))
                        throw new InvalidOperationException("LOD generation is not complete: "+source);
            foreach(string file in Directory.GetFiles(Work,"*_lod*.json"))
            {
                string name=Path.GetFileNameWithoutExtension(file).Replace("_lod","_");
                if(!File.Exists(Output+"/"+name.Substring(0,name.Length-1)+"0.asset"))continue;
                var data=Compact(JsonUtility.FromJson<Data>(File.ReadAllText(file)));
                // Blender의 오른손 좌표계에서 계산한 법선은 Unity 삼각형 감김 기준과 반대입니다.
                for(int n=0;n<data.normals.Length;n++)data.normals[n]=-data.normals[n];
                var mesh=MakeMesh(data,name);
                string assetPath=Output+"/"+name+".asset";
                var existing=AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
                if(existing){EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);EditorUtility.SetDirty(existing);}
                else AssetDatabase.CreateAsset(mesh,assetPath);
            }
            int groups=0;
            foreach(string path in Scenes())
            {
                var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
                bool opened=!scene.IsValid()||!scene.isLoaded;
                if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
                try
                {
                    foreach(var f in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MeshFilter>(true)).Where(Eligible).ToArray())
                    {
                        var original=f.GetComponent<MeshRenderer>(); if(!original||!original.enabled)continue;
                        var root=new GameObject("SpringLOD"); root.transform.SetParent(f.transform,false);
                        foreach(string file in Directory.GetFiles(Output,Key(f.sharedMesh)+"_*_0.asset"))
                        {
                            string stem=file.Substring(0,file.Length-8).Replace('\\','/');
                            var groupObject=new GameObject(Path.GetFileName(stem));groupObject.transform.SetParent(root.transform,false);
                            var group=groupObject.AddComponent<LODGroup>();var lods=new LOD[3];
                            for(int level=0;level<3;level++)
                            {
                                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(stem+"_"+level+".asset");
                                if(!mesh)throw new InvalidOperationException("Missing LOD: "+stem);
                                var go=new GameObject("LOD"+level);go.layer=f.gameObject.layer;go.transform.SetParent(groupObject.transform,false);
                                go.AddComponent<MeshFilter>().sharedMesh=mesh;
                                var r=go.AddComponent<MeshRenderer>();EditorUtility.CopySerialized(original,r);
                                r.enabled=true;r.lightmapIndex=-1;
                                lods[level]=new LOD(level==0?.4f:level==1?.16f:0f,new Renderer[]{r});
                            }
                            group.SetLODs(lods);group.RecalculateBounds();groups++;
                        }
                        original.enabled=false;PrefabUtility.RecordPrefabInstancePropertyModifications(original);
                    }
                    EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                }
                finally {if(opened)EditorSceneManager.CloseScene(scene,true);}
            }
            AssetDatabase.SaveAssets();
            return groups+" LOD groups installed";
        }
    }
}
