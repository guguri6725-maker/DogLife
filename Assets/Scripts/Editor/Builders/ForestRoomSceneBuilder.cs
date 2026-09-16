using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace DogLife.EditorTools
{
    public static class ForestRoomSceneBuilder
    {
        const string Folder = "Assets/Scenes/ForestRooms";
        static readonly float[] Cuts = { -12.01f, 22, 45, 68, 91, 114, 137, 180.01f };

        [MenuItem("DogLife/Forest/Convert active 3D scene to streamed rooms")]
        public static void Build()
        {
            var common = SceneManager.GetActiveScene();
            if (common.path != "Assets/Scenes/3D.unity" || EditorApplication.isPlaying)
                throw new InvalidOperationException("Open 3D in Edit mode first.");
            var director = UnityEngine.Object.FindFirstObjectByType<StageOneDirector>();
            if (!director || director.streaming) throw new InvalidOperationException("Missing director or already converted.");
            Directory.CreateDirectory("ArtSource/ForestStreaming");
            EditorSceneManager.SaveScene(common, "ArtSource/ForestStreaming/BeforeStreaming.unity", true);
            Directory.CreateDirectory(Folder + "/Meshes");
            Directory.CreateDirectory(Folder + "/Materials");
            AssetDatabase.Refresh();
            var wall = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Materials/MossyDungeonStone.mat");
            if (!wall) { wall = new Material(Shader.Find("Standard")) { name = "MossyDungeonStone" }; AssetDatabase.CreateAsset(wall, Folder + "/Materials/MossyDungeonStone.mat"); }
            wall.color = new Color(.24f, .29f, .19f); wall.SetFloat("_Glossiness", .08f);
            var stoneShader = Shader.Find("DogLife/Forest Streaming Stone");
            if (stoneShader) wall.shader = stoneShader;
            var trim = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Materials/DungeonStoneTrim.mat");
            if (!trim) { trim = new Material(wall) { name = "DungeonStoneTrim", color = new Color(.36f, .4f, .27f) }; AssetDatabase.CreateAsset(trim, Folder + "/Materials/DungeonStoneTrim.mat"); }
            if (stoneShader) trim.shader = stoneShader;
            var forest = GameObject.Find("Stage1_3D_Forest");
            var floor = forest.transform.Find("ForestFloor").gameObject;
            var scenes = new Scene[7]; var roots = new GameObject[7]; var rooms = new ForestRoom[7];
            var paths = new string[7];
            for (int i = 0; i < 7; i++)
            {
                paths[i] = Folder + $"/Forest_Room_{i + 1:00}.unity";
                scenes[i] = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                roots[i] = new GameObject($"Forest_Room_{i + 1:00}");
                SceneManager.MoveGameObjectToScene(roots[i], scenes[i]);
                rooms[i] = roots[i].AddComponent<ForestRoom>(); rooms[i].index = i;
                rooms[i].entryX = i == 0 ? -12 : director.gates[i - 1].transform.position.x;
                rooms[i].exitX = director.gates[i].transform.position.x;
                var mesh = ClipFloor(floor, Cuts[i], Cuts[i + 1]);
                mesh.name = $"ForestFloor_Room_{i + 1:00}";
                if (AssetDatabase.LoadAssetAtPath<Mesh>(Folder + "/Meshes/" + mesh.name + ".asset")) AssetDatabase.DeleteAsset(Folder + "/Meshes/" + mesh.name + ".asset");
                AssetDatabase.CreateAsset(mesh, Folder + "/Meshes/" + mesh.name + ".asset");
                var ground = new GameObject(mesh.name); ground.layer = floor.layer; ground.transform.SetParent(roots[i].transform);
                ground.AddComponent<MeshFilter>().sharedMesh = mesh;
                ground.AddComponent<MeshRenderer>().sharedMaterials = floor.GetComponent<MeshRenderer>().sharedMaterials;
                ground.AddComponent<MeshCollider>().sharedMesh = mesh;
                ground.isStatic = true;
                EditorSceneManager.SaveScene(scenes[i], paths[i]);
            }
            SceneManager.SetActiveScene(common);
            // 원래 월드 좌표를 유지하고 장식을 중복 없이 한 방에 할당합니다.
            foreach (Transform group in forest.transform.Cast<Transform>().ToArray())
            {
                if (group.gameObject == floor) continue;
                foreach (var item in (group.childCount > 0 ? group.Cast<Transform>().ToArray() : new[] { group }))
                {
                    var renderers = item.GetComponentsInChildren<Renderer>();
                    float x = item.position.x;
                    if (renderers.Length > 0) { var b = renderers[0].bounds; foreach (var r in renderers) b.Encapsulate(r.bounds); x = b.center.x; }
                    int index = RoomIndex(x);
                    Move(item.gameObject, scenes[index], roots[index].transform);
                }
                if (group && group.parent == forest.transform) UnityEngine.Object.DestroyImmediate(group.gameObject);
            }
            UnityEngine.Object.DestroyImmediate(floor);
            forest.name = "ForestNavigation"; // 기존에 구운 길찾기 데이터만 공통 씬에 유지합니다.
            for (int i = 0; i < 7; i++)
            {
                var room = rooms[i];
                Move(director.encounters[i], scenes[i], roots[i].transform);
                room.encounter = director.encounters[i]; room.encounter.SetActive(false);
                Move(director.gates[i], scenes[i], roots[i].transform);
                room.forwardGate = director.gates[i]; room.forwardGate.SetActive(true);
                foreach (var renderer in room.forwardGate.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
                var door = Block("ClosedForwardDoor", roots[i].transform, new Vector3(room.exitX, 3.5f, 0), new Vector3(1.5f, 9, 6), wall);
                door.transform.SetParent(room.forwardGate.transform, true);
                EntranceFrame(roots[i].transform, room.exitX, wall, trim);
                room.rearSeal = Block("RearSeal", roots[i].transform, new Vector3(room.entryX, 12, 0), new Vector3(1.5f, 28, 64), wall);
                room.rearSeal.SetActive(i == 0);
                room.recoveryStations = director.recoveryStations.Where(t => RoomIndex(t.position.x) == i).ToArray();
                foreach (var station in room.recoveryStations) Move(station.gameObject, scenes[i], roots[i].transform);
                if (i == 6) { Move(director.exit.gameObject, scenes[i], roots[i].transform); room.exit = director.exit; }
                EditorSceneManager.SaveScene(scenes[i], paths[i]);
            }
            var streaming = director.gameObject.AddComponent<ForestRoomStreaming>();
            streaming.director = director; streaming.roomScenePaths = paths; director.streaming = streaming;
            director.encounters = Array.Empty<GameObject>(); director.gates = Array.Empty<GameObject>();
            director.recoveryStations = Array.Empty<Transform>(); director.exit = null;
            var build = EditorBuildSettings.scenes.ToList();
            foreach (string path in paths) if (!build.Any(s => s.path == path)) build.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = build.ToArray();
            AssetDatabase.SaveAssets();
            SceneManager.SetActiveScene(common);
            EditorSceneManager.SaveScene(common);
            for (int i = 0; i < 7; i++) EditorSceneManager.CloseScene(scenes[i], true);
            // 편집 화면에서도 처음 두 방을 보여 줍니다. 런타임은 이미 열린 방을 재사용합니다.
            EditorSceneManager.OpenScene(paths[0], OpenSceneMode.Additive);
            EditorSceneManager.OpenScene(paths[1], OpenSceneMode.Additive);
            SceneManager.SetActiveScene(common);
        }

        static int RoomIndex(float x) { for (int i = 0; i < 6; i++) if (x < Cuts[i + 1]) return i; return 6; }
        static void Move(GameObject go, Scene scene, Transform parent)
        {
            go.transform.SetParent(null, true); SceneManager.MoveGameObjectToScene(go, scene); go.transform.SetParent(parent, true);
        }
        static GameObject Block(string name, Transform parent, Vector3 position, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.layer = 8;
            go.transform.SetParent(parent); go.transform.position = position; go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
            var obstacle = go.AddComponent<NavMeshObstacle>(); obstacle.shape = NavMeshObstacleShape.Box; obstacle.size = Vector3.one; obstacle.center = Vector3.zero; obstacle.carving = true;
            return go;
        }
        static void EntranceFrame(Transform parent, float x, Material wall, Material trim)
        {
            Block("Boundary_Left", parent, new Vector3(x, 12, -17.5f), new Vector3(1.5f, 28, 29), wall);
            Block("Boundary_Right", parent, new Vector3(x, 12, 17.5f), new Vector3(1.5f, 28, 29), wall);
            Block("Boundary_Lintel", parent, new Vector3(x, 17, 0), new Vector3(1.5f, 18, 6), wall);
            for (int side = -1; side <= 1; side += 2)
                Block("Portal_Pillar", parent, new Vector3(x, 3.5f, side * 3.5f), new Vector3(2.4f, 9, 1), trim);
            Block("Portal_Crown", parent, new Vector3(x, 8, 0), new Vector3(2.4f, 1.2f, 8), trim);
        }

        struct Vertex
        {
            public Vector3 p, n;
            public static Vertex Lerp(Vertex a, Vertex b, float t) => new Vertex { p = Vector3.Lerp(a.p,b.p,t), n = Vector3.Lerp(a.n,b.n,t).normalized };
        }
        static List<Vertex> Clip(List<Vertex> input, float x, bool greater)
        {
            var output = new List<Vertex>();
            for (int i = 0; i < input.Count; i++)
            {
                var a=input[i];var b=input[(i+1)%input.Count];bool ia=greater?a.p.x>=x:a.p.x<=x;bool ib=greater?b.p.x>=x:b.p.x<=x;
                if(ia)output.Add(a);
                if(ia!=ib)output.Add(Vertex.Lerp(a,b,(x-a.p.x)/(b.p.x-a.p.x)));
            }
            return output;
        }
        static Mesh ClipFloor(GameObject floor, float min, float max)
        {
            var source=floor.GetComponent<MeshFilter>().sharedMesh;var sv=source.vertices;var sn=source.normals;
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var submeshes=new List<int[]>();
            for(int s=0;s<source.subMeshCount;s++)
            {
                var indices=new List<int>();var tris=source.GetTriangles(s);
                for(int t=0;t<tris.Length;t+=3)
                {
                    var polygon=new List<Vertex>();
                    for(int k=0;k<3;k++){int j=tris[t+k];polygon.Add(new Vertex{p=floor.transform.TransformPoint(sv[j]),n=floor.transform.TransformDirection(sn[j])});}
                    polygon=Clip(Clip(polygon,min,true),max,false);
                    for(int k=1;k+1<polygon.Count;k++)foreach(var v in new[]{polygon[0],polygon[k],polygon[k+1]}){indices.Add(vertices.Count);vertices.Add(v.p);normals.Add(v.n);}
                }
                submeshes.Add(indices.ToArray());
            }
            var mesh=new Mesh();mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.subMeshCount=submeshes.Count;
            for(int s=0;s<submeshes.Count;s++)mesh.SetTriangles(submeshes[s],s);mesh.RecalculateBounds();return mesh;
        }
    }
}
