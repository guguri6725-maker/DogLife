using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace DogLife.Editor
{
    // 바다 스테이지의 지형, 전투방과 피아누스 보스를 구성합니다.
    public static class StageTwoBuilder
    {
        public const string ScenePath = "Assets/DogLife/Scenes/Stage02_Sea.unity";
        const string Root = PrototypeBuilder.Root;
        static GameObject Shape(string n, PrimitiveType type, Vector3 pos, Vector3 size, Material mat, Transform parent = null, bool solid = false)
        { return PrototypeBuilder.Shape(n, type, pos, size, mat, parent, solid); }
        static GameObject Block(string n, Vector3 p, Vector3 s, Material m, Transform parent, bool solid = false)
        { return Shape(n, PrimitiveType.Cube, p, s, m, parent, solid); }
        static void Sign(string text, Vector3 pos, Color color)
        {
            var go = new GameObject(text); go.transform.position = pos;
            var label = go.AddComponent<TextMesh>(); label.text = text; label.fontSize = 55; label.characterSize = .075f; label.anchor = TextAnchor.MiddleCenter; label.color = color;
        }
        static GameObject Creature(string name, int kind, Material bodyMat, Material detailMat, Material projectile)
        {
            var go = new GameObject(name);
            var col = go.AddComponent<CapsuleCollider>(); col.height = 2; col.radius = .42f;
            go.AddComponent<Rigidbody>();
            var visual = new GameObject("VisualRoot_REPLACE_MODEL").transform; visual.SetParent(go.transform, false);
            Shape("Body", PrimitiveType.Sphere, Vector3.zero, new Vector3(1.05f, .85f, .7f), bodyMat, visual);
            if (kind == 0)
            {
                for (int i = -1; i <= 1; i += 2)
                {
                    Shape("Crab claw", PrimitiveType.Sphere, new Vector3(i * .65f, .15f, 0), new Vector3(.5f, .35f, .4f), detailMat, visual);
                    Block("Crab leg", new Vector3(i * .45f, -.5f, 0), new Vector3(.7f, .15f, .3f), bodyMat, visual);
                }
            }
            else if (kind == 2)
            {
                Shape("Plant mouth", PrimitiveType.Cylinder, new Vector3(0, .65f, 0), new Vector3(.85f, .25f, .85f), detailMat, visual);
                Block("Plant stem", new Vector3(0, -.45f, 0), new Vector3(.2f, .7f, .2f), detailMat, visual);
            }
            else
            {
                Shape("Head", PrimitiveType.Sphere, new Vector3(.15f, .6f, 0), Vector3.one * .65f, detailMat, visual);
                Block("Tail or fin", new Vector3(-.65f, 0, 0), new Vector3(.5f, .5f, .18f), detailMat, visual);
            }
            var hp = go.AddComponent<CombatHealth>(); hp.maxHealth = kind == 4 ? 1 : kind == 5 ? 12 : 30;
            var brain = go.AddComponent<StageEnemy>(); brain.ranged = kind == 2 || kind == 3 || kind == 5;
            brain.speed = kind == 2 ? 0 : kind == 1 ? 2.8f : 2; brain.damage = kind == 4 ? 10 : kind == 2 ? 5 : 7;
            brain.visualRoot = visual; brain.projectileMaterial = projectile;
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, Root + "/Prefabs/Sea_" + name + ".prefab"); Object.DestroyImmediate(go); return prefab;
        }
        // Stage 2 씬 전체를 생성하고 Stage 1의 다음 씬으로 연결합니다.
        public static void Build()
        {
            PrototypeBuilder.Folders();
            var previous = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(previous.path)) EditorSceneManager.SaveScene(previous, Root + "/Scenes/OriginalSetup.unity");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive); SceneManager.SetActiveScene(scene);
            var root = new GameObject("Stage 2 - Sea");
            var player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/Player.prefab")); player.transform.position = new Vector3(0, 1.1f, 0);
            var hud = PrototypeBuilder.Environment(player, 166); hud.completionTitle = "STAGE 2 CLEAR";
            hud.weapons.aimCamera.backgroundColor = new Color(.025f, .12f, .21f);
            var sand = PrototypeBuilder.MakeMaterial("Sea sand", new Color(.55f, .53f, .38f));
            var stone = PrototypeBuilder.MakeMaterial("Sea stone", new Color(.18f, .36f, .42f));
            var coral = PrototypeBuilder.MakeMaterial("Sea coral", new Color(.8f, .32f, .35f));
            var kelp = PrototypeBuilder.MakeMaterial("Sea kelp", new Color(.1f, .42f, .33f));
            var distant = PrototypeBuilder.MakeMaterial("Sea distant", new Color(.035f, .2f, .28f));
            var cyan = PrototypeBuilder.MakeMaterial("Sea cyan", new Color(.15f, .75f, .8f));
            var purple = PrototypeBuilder.MakeMaterial("Sea purple", new Color(.5f, .3f, .65f));
            var gateMat = PrototypeBuilder.MakeMaterial("Sea gate", new Color(.92f, .66f, .17f));
            var exitMat = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/Exit green.mat");
            var projectile = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/Projectile.mat");
            var crab = Creature("Crab", 0, coral, sand, projectile);
            var walker = Creature("SeaWalker", 1, cyan, stone, projectile);
            var plant = Creature("Nepenthes", 2, purple, kelp, projectile);
            var frog = Creature("ToxicFrog", 3, kelp, purple, projectile);
            var bone = Creature("BoneFish", 4, sand, stone, projectile);
            var mini = Creature("MiniPianus", 5, purple, cyan, projectile);
            GameObject[] types = { crab, walker, plant, frog };
            Block("Sea floor", new Vector3(80, -.6f, 0), new Vector3(190, 1.2f, 5), sand, root.transform, true);
            Block("West boundary", new Vector3(-9, 5, 0), new Vector3(1, 12, 5), stone, root.transform, true);
            Block("East boundary", new Vector3(176, 5, 0), new Vector3(1, 12, 5), stone, root.transform, true);
            for (int i = 0; i < 44; i++)
            {
                float x = -10 + i * 4.3f;
                Shape("Distant rock", PrimitiveType.Sphere, new Vector3(x, 1.5f, 8), new Vector3(6, 4 + i % 4, 2), distant, root.transform);
                Block("Kelp", new Vector3(x, 2, 4), new Vector3(.15f, 4 + i % 3, .25f), kelp, root.transform);
                for (int j = 0; j < 3; j++)
                {
                    Shape("Coral branch", PrimitiveType.Capsule, new Vector3(x + j * .35f, .5f + j * .2f, 3.5f), new Vector3(.22f, .5f + j * .2f, .22f), coral, root.transform);
                    Shape("Background bubble", PrimitiveType.Sphere, new Vector3(x + 1 + j * .3f, 3 + j * 1.2f, 6), Vector3.one * (.15f + j * .06f), cyan, root.transform);
                }
            }
            var director = root.AddComponent<StageOneDirector>(); director.hud = hud; director.stageLabel = "SEA 02"; director.bossLabel = "PIANUS";
            director.encounters = new GameObject[7]; director.gates = new GameObject[7];
            for (int room = 0; room < 7; room++)
            {
                float start = room * 23;
                var encounter = new GameObject(room == 6 ? "Pianus arena" : "Sea_Stage" + (room + 1)); encounter.transform.SetParent(root.transform); director.encounters[room] = encounter;
                director.gates[room] = Block("Sea gate " + room, new Vector3(start + 22, 5, 0), new Vector3(.45f, 12, 4), gateMat, root.transform, true);
                Sign(room == 6 ? "PIANUS" : "SEA / 0" + (room + 1), new Vector3(start + 10, 5.5f, 2), Color.cyan);
                if (room < 6)
                {
                    for (int j = 0; j < (room < 2 ? 2 : 3); j++)
                    {
                        var enemy = (GameObject)PrefabUtility.InstantiatePrefab(types[(room + j) % types.Length], encounter.transform);
                        enemy.transform.position = new Vector3(start + 10 + j * 4, 1.05f, 0);
                    }
                    Block("Sea ledge " + room, new Vector3(start + 7, 2.5f, 0), new Vector3(3, .4f, 3), stone, root.transform, true);
                    Block("Sea ledge upper " + room, new Vector3(start + 12, 3.2f, 0), new Vector3(3.5f, .4f, 3), stone, root.transform, true);
                }
                else
                {
                    var boss = new GameObject("Pianus"); boss.transform.SetParent(encounter.transform); boss.transform.position = new Vector3(157, 1.9f, 0);
                    var collider = boss.AddComponent<BoxCollider>(); collider.size = new Vector3(3.2f, 3.4f, 1.5f);
                    var visual = new GameObject("VisualRoot_REPLACE_MODEL").transform; visual.SetParent(boss.transform, false);
                    Shape("Temporary fish body", PrimitiveType.Sphere, Vector3.zero, new Vector3(3.2f, 3.2f, 1.6f), purple, visual);
                    Shape("Fish mouth", PrimitiveType.Cylinder, new Vector3(-1.5f, -.3f, 0), new Vector3(.6f, .5f, .6f), cyan, visual).transform.localRotation = Quaternion.Euler(0, 0, 90);
                    Shape("Fin", PrimitiveType.Cube, new Vector3(.5f, 1.6f, 0), new Vector3(1, .8f, .2f), cyan, visual);
                    var hp = boss.AddComponent<CombatHealth>(); hp.maxHealth = 300;
                    var brain = boss.AddComponent<SeaBoss>(); brain.boneFishPrefab = bone; brain.miniPianusPrefab = mini; brain.hazardMaterial = cyan;
                    PrefabUtility.SaveAsPrefabAsset(boss, Root + "/Prefabs/Sea_Pianus.prefab");
                }
            }
            director.recoveryStations = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? 69 : 138;
                director.recoveryStations[i] = Shape("Sea recovery", PrimitiveType.Sphere, new Vector3(x, 1.3f, 0), Vector3.one * .6f, exitMat, root.transform).transform;
                Sign("REST / HP", new Vector3(x, 3.5f, 1), Color.green);
            }
            var exit = new GameObject("Stage 2 exit"); exit.transform.position = new Vector3(170, 1, 0); exit.transform.SetParent(root.transform); director.exit = exit.transform;
            Block("Exit left", new Vector3(168.5f, 2, 1), new Vector3(.4f, 4, .4f), exitMat, root.transform);
            Block("Exit right", new Vector3(171.5f, 2, 1), new Vector3(.4f, 4, .4f), exitMat, root.transform);
            Block("Exit top", new Vector3(170, 4, 1), new Vector3(3.4f, .4f, .4f), exitMat, root.transform);
            Sign("EXIT", new Vector3(170, 4.8f, 1), Color.green);
            Sign("SEA 02 / CLEAR EACH ENCOUNTER", new Vector3(0, 3.5f, 1), Color.white);
            if (previous.path == ScenePath && !previous.isDirty) EditorSceneManager.CloseScene(previous, true);
            EditorSceneManager.SaveScene(scene, ScenePath);
            // Update the existing forest scene in place to preserve its serialized content.
            var forest = SceneManager.GetSceneByPath(Root + "/Scenes/Stage01_Forest.unity");
            if (!forest.isLoaded) forest = EditorSceneManager.OpenScene(Root + "/Scenes/Stage01_Forest.unity", OpenSceneMode.Additive);
            foreach (var go in forest.GetRootGameObjects()) foreach (var forestHud in go.GetComponentsInChildren<PrototypeHUD>(true)) { forestHud.nextScene = ScenePath; EditorUtility.SetDirty(forestHud); }
            EditorSceneManager.SaveScene(forest); EditorSceneManager.CloseScene(forest, true);
            SceneManager.SetActiveScene(scene);
            var settings = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!settings.Exists(s => s.path == ScenePath)) settings.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = settings.ToArray(); AssetDatabase.SaveAssets();
        }
    }
}
