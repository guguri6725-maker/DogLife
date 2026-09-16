using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace DogLife.EditorTools
{
    public static class ForestGroundingPass
    {
        const string Report = "ArtSource/ForestGrounding";
        static MeshCollider[] floors;
        static float Ground(float x, float z)
        {
            float height = float.NegativeInfinity;
            foreach (var floor in floors)
                if (floor.Raycast(new Ray(new Vector3(x, 30, z), Vector3.down), out var hit, 60)) height = Mathf.Max(height, hit.point.y);
            return height;
        }
        static float Bottom(Transform root)
        {
            float bottom=float.PositiveInfinity;
            foreach(var f in root.GetComponentsInChildren<MeshFilter>())
            {
                if(!f.sharedMesh)continue;
                var vertices=f.sharedMesh.vertices;var used=new HashSet<int>(f.sharedMesh.triangles);
                foreach(int i in used)bottom=Mathf.Min(bottom,f.transform.TransformPoint(vertices[i]).y);
            }
            return bottom;
        }
        public static string Apply()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop play mode first.");
            Directory.CreateDirectory(Report+"/Before");
            var common=SceneManager.GetSceneByPath("Assets/Scenes/3D.unity");
            EditorSceneManager.SaveScene(common,Report+"/Before/3D.unity",true);
            var rooms=new List<ForestRoom>();
            for(int i=1;i<=7;i++)
            {
                string path=$"Assets/Scenes/ForestRooms/Forest_Room_{i:00}.unity";
                var scene=SceneManager.GetSceneByPath(path);
                if(!scene.isLoaded)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
                EditorSceneManager.SaveScene(scene,$"{Report}/Before/Forest_Room_{i:00}.unity",true);
                rooms.Add(scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ForestRoom>()).Single());
            }
            floors=rooms.SelectMany(r=>r.GetComponentsInChildren<MeshCollider>()).Where(c=>c.name.StartsWith("ForestFloor")).ToArray();
            Physics.SyncTransforms();
            var changes=new List<object>();
            foreach(var room in rooms)
            {
                foreach(Transform t in room.transform.Cast<Transform>().ToArray())
                {
                    if(t.name.StartsWith("Canopy_")||t.name.StartsWith("Understory_")||t.name.StartsWith("Platforms_"))continue;
                    var f=t.GetComponent<MeshFilter>();
                    if(!f||!AssetDatabase.GetAssetPath(f.sharedMesh).Contains("3DStage1/Models"))continue;
                    float ground=Ground(t.position.x,t.position.z);if(float.IsInfinity(ground))continue;
                    float bottom=Bottom(t);float gap=bottom-ground;
                    // 접지면을 5cm 묻어 불규칙한 밑면의 빛샘을 없앱니다. 이미 묻힌 물체는 올리지 않습니다.
                    float delta=Mathf.Min(0,-.05f-gap);
                    if(delta>-.005f)continue;
                    t.position+=Vector3.up*delta;
                    if(t.name.StartsWith("Support_"))
                    {
                        var platform=room.transform.Find(t.name.Substring("Support_".Length));
                        if(platform)
                        {
                            platform.position+=Vector3.up*delta;
                            if(PrefabUtility.IsPartOfPrefabInstance(platform))PrefabUtility.RecordPrefabInstancePropertyModifications(platform);
                        }
                    }
                    if(PrefabUtility.IsPartOfPrefabInstance(t))PrefabUtility.RecordPrefabInstancePropertyModifications(t);
                    changes.Add(new{room=room.index+1,name=t.name,beforeGap=gap,down=-delta,afterGap=Bottom(t)-ground});
                }
                var old=room.transform.Find("ForestSideLimits");if(old)UnityEngine.Object.DestroyImmediate(old.gameObject);
                var limits=new GameObject("ForestSideLimits");limits.transform.SetParent(room.transform,false);
                var floor=floors.First(c=>c.gameObject.scene==room.gameObject.scene);
                float edge=room.index==6?15:12;
                foreach(int side in new[]{-1,1})
                {
                    var go=new GameObject(side<0?"SouthForestLimit":"NorthForestLimit");go.layer=8;go.transform.SetParent(limits.transform,false);
                    go.transform.position=new Vector3(floor.bounds.center.x,10,side*(edge+1));
                    var box=go.AddComponent<BoxCollider>();box.size=new Vector3(floor.bounds.size.x+1,24,2);
                    var obstacle=go.AddComponent<NavMeshObstacle>();obstacle.shape=NavMeshObstacleShape.Box;obstacle.size=box.size;obstacle.carving=true;
                }
                EditorSceneManager.MarkSceneDirty(room.gameObject.scene);
                EditorSceneManager.SaveScene(room.gameObject.scene);
            }
            Physics.SyncTransforms();
            // 바뀐 구조물 높이를 반영해 모든 방이 열린 상태에서 길찾기 데이터를 갱신합니다.
            var surface=GameObject.Find("ForestNavigation").GetComponent<NavMeshSurface>();
            surface.collectObjects=CollectObjects.All;surface.useGeometry=NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
            const string navPath="Assets/Scenes/ForestRooms/ForestGroundedNavigation.asset";
            var previous=AssetDatabase.LoadAssetAtPath<NavMeshData>(navPath);
            if(previous)AssetDatabase.DeleteAsset(navPath);
            AssetDatabase.CreateAsset(surface.navMeshData,navPath);
            EditorUtility.SetDirty(surface);EditorSceneManager.MarkSceneDirty(common);EditorSceneManager.SaveScene(common);
            AssetDatabase.SaveAssets();
            File.WriteAllText(Report+"/GroundingChanges.json",Newtonsoft.Json.JsonConvert.SerializeObject(changes,Newtonsoft.Json.Formatting.Indented));
            foreach(var room in rooms)EditorSceneManager.CloseScene(room.gameObject.scene,true);
            SceneManager.SetActiveScene(common);
            return $"Adjusted {changes.Count} objects; 14 side barriers; navigation rebuilt.";
        }
    }
}
