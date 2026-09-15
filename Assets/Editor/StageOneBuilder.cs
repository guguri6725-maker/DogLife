using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace DogLife.Editor
{
    public static class StageOneBuilder
    {
        static GameObject Block(string name, Vector3 position, Vector3 scale, Material material, Transform parent = null, bool solid = true)
        { return PrototypeBuilder.Shape(name, PrimitiveType.Cube, position, scale, material, parent, solid); }
        static void Sign(string text, Vector3 position, Color color)
        {
            var go = new GameObject(text); go.transform.position = position;
            var label = go.AddComponent<TextMesh>(); label.text = text; label.fontSize = 60; label.characterSize = .08f; label.anchor = TextAnchor.MiddleCenter; label.color = color;
        }
        static void Enemy(Transform parent, string name, float x, int type, Material bodyMat, Material crownMat, Material projectileMat)
        {
            bool boss = type == 3;
            var go = new GameObject(name); go.transform.SetParent(parent); go.transform.position = new Vector3(x, boss ? 1.7f : 1.05f, 0);
            var collider = go.AddComponent<CapsuleCollider>(); collider.height = boss ? 3.4f : 2; collider.radius = boss ? .8f : .4f;
            go.AddComponent<Rigidbody>(); var hp = go.AddComponent<CombatHealth>(); hp.maxHealth = boss ? 100 : 20;
            var visual = new GameObject("VisualRoot_REPLACE_MODEL").transform; visual.SetParent(go.transform, false);
            PrototypeBuilder.Shape("Body", PrimitiveType.Capsule, Vector3.zero, boss ? new Vector3(1.6f, 1.5f, 1.3f) : new Vector3(.75f, .8f, .7f), bodyMat, visual, false);
            PrototypeBuilder.Shape(type == 1 ? "Mushroom cap" : "Crown", PrimitiveType.Sphere, new Vector3(0, boss ? 1.5f : .9f, 0), boss ? new Vector3(2.5f, 1, 1.8f) : new Vector3(1.2f, .5f, .9f), crownMat, visual, false);
            var brain = go.AddComponent<StageEnemy>(); brain.boss = boss; brain.ranged = type == 2; brain.speed = boss ? 1.2f : type == 1 ? 1.6f : 2.1f; brain.damage = boss ? 10 : 5; brain.visualRoot = visual; brain.projectileMaterial = projectileMat;
            PrefabUtility.SaveAsPrefabAsset(go, PrototypeBuilder.Root + "/Prefabs/" + (boss ? "ForestGuardian" : type == 0 ? "SaplingEnt" : type == 1 ? "Mushroom" : "ForestSpirit") + ".prefab");
        }
        public static void Build()
        {
            PrototypeBuilder.Folders();
            var oldScene = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive); SceneManager.SetActiveScene(scene);
            var root = new GameObject("Stage 1 - Forest");
            var player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrototypeBuilder.Root + "/Prefabs/Player.prefab"));
            player.transform.position = new Vector3(0, 1.1f, 0);
            var hud = PrototypeBuilder.Environment(player, 166);
            Camera.main.backgroundColor = new Color(.07f, .15f, .17f);
            var floor = PrototypeBuilder.MakeMaterial("Forest soil", new Color(.21f, .23f, .15f));
            var grass = PrototypeBuilder.MakeMaterial("Forest grass", new Color(.25f, .47f, .26f));
            var bark = PrototypeBuilder.MakeMaterial("Forest bark", new Color(.24f, .16f, .11f));
            var leaf = PrototypeBuilder.MakeMaterial("Forest leaves", new Color(.13f, .31f, .23f));
            var far = PrototypeBuilder.MakeMaterial("Forest distant", new Color(.12f, .23f, .24f));
            var gateMat = PrototypeBuilder.MakeMaterial("Encounter gate", new Color(.95f, .48f, .13f));
            var purple = PrototypeBuilder.MakeMaterial("Spirit purple", new Color(.55f, .35f, .68f));
            var red = PrototypeBuilder.MakeMaterial("Mushroom red", new Color(.7f, .22f, .18f));
            var exitMat = PrototypeBuilder.MakeMaterial("Exit green", new Color(.4f, 1, .5f));
            var projectile = AssetDatabase.LoadAssetAtPath<Material>(PrototypeBuilder.Root + "/Materials/Projectile.mat");
            Block("Continuous side-view ground", new Vector3(80, -.6f, 0), new Vector3(190, 1.2f, 5), floor, root.transform);
            Block("Grass surface", new Vector3(80, -.04f, 0), new Vector3(190, .08f, 5), grass, root.transform, false);
            Block("West boundary", new Vector3(-9, 5, 0), new Vector3(1, 12, 5), bark, root.transform);
            Block("East boundary", new Vector3(176, 5, 0), new Vector3(1, 12, 5), bark, root.transform);
            for (int i = 0; i < 46; i++)
            {
                float x = -12 + i * 4.3f; float height = 5 + (i % 4);
                Block("Background trunk", new Vector3(x, height / 2, 4 + i % 3), new Vector3(.6f, height, .7f), bark, root.transform, false);
                PrototypeBuilder.Shape("Background canopy", PrimitiveType.Sphere, new Vector3(x, height, 5 + i % 3), new Vector3(5, 3.5f, 3), leaf, root.transform, false);
                Block("Distant tree", new Vector3(x + 2, 5, 10), new Vector3(1, 14, 1), far, root.transform, false);
            }
            var director = root.AddComponent<StageOneDirector>(); director.hud = hud; director.encounters = new GameObject[7]; director.gates = new GameObject[7];
            for (int room = 0; room < 7; room++)
            {
                float start = room * 23;
                var encounter = new GameObject(room == 6 ? "Forest_BossStage" : "Forest_Stage_" + (room + 1)); encounter.transform.SetParent(root.transform); director.encounters[room] = encounter;
                var gate = Block("Gate " + (room + 1), new Vector3(start + 22, 5, 0), new Vector3(.45f, 12, 4), gateMat, root.transform); director.gates[room] = gate;
                Sign(room == 6 ? "FOREST GUARDIAN" : "FOREST / 0" + (room + 1), new Vector3(start + 9, 5.5f, 2), new Color(.8f, .92f, .76f));
                if (room < 6)
                {
                    int count = room < 2 ? 2 : 3;
                    for (int j = 0; j < count; j++)
                    {
                        int type = (room + j) % 3;
                        Enemy(encounter.transform, type == 0 ? "Sapling Ent" : type == 1 ? "Mushroom" : "Forest Spirit", start + 10 + j * 4, type, type == 2 ? purple : bark, type == 1 ? red : leaf, projectile);
                    }
                    // Optional jump routes above a continuous, walkable combat lane.
                    Block("Upper route " + room, new Vector3(start + 7, 2.5f, 0), new Vector3(3.5f, .4f, 3), grass, root.transform);
                    Block("Upper route landing " + room, new Vector3(start + 12, 3.2f, 0), new Vector3(3, .4f, 3), grass, root.transform);
                }
                else Enemy(encounter.transform, "Forest Guardian", start + 15, 3, bark, leaf, projectile);
            }
            director.recoveryStations = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? 69 : 138;
                director.recoveryStations[i] = PrototypeBuilder.Shape("Recovery station", PrimitiveType.Sphere, new Vector3(x, 1.3f, 0), Vector3.one * .65f, exitMat, root.transform, false).transform;
                Sign("REST / HP", new Vector3(x, 3.5f, 1), Color.green);
            }
            var exit = new GameObject("Stage 1 exit"); exit.transform.SetParent(root.transform); exit.transform.position = new Vector3(170, 1, 0); director.exit = exit.transform;
            Block("Exit pillar left", new Vector3(168.5f, 2, 1), new Vector3(.4f, 4, .4f), exitMat, root.transform, false);
            Block("Exit pillar right", new Vector3(171.5f, 2, 1), new Vector3(.4f, 4, .4f), exitMat, root.transform, false);
            Block("Exit arch", new Vector3(170, 4, 1), new Vector3(3.4f, .4f, .4f), exitMat, root.transform, false);
            Sign("EXIT", new Vector3(170, 4.8f, 1), Color.green);
            Sign("A/D MOVE   SPACE JUMP   SHIFT DASH", new Vector3(0, 3.5f, 1), Color.white);
            string path = PrototypeBuilder.Root + "/Scenes/Stage01_Forest.unity";
            EditorSceneManager.SaveScene(scene, path);
            if (!oldScene.isDirty) EditorSceneManager.CloseScene(oldScene, true);
            var settings = new System.Collections.Generic.List<EditorBuildSettingsScene>();
            settings.Add(new EditorBuildSettingsScene(path, true));
            foreach (var existing in EditorBuildSettings.scenes) if (existing.path != path) settings.Add(existing);
            if (!settings.Exists(s => s.path.EndsWith("MovementWeapons.unity"))) settings.Add(new EditorBuildSettingsScene(PrototypeBuilder.Root + "/Scenes/MovementWeapons.unity", true));
            EditorBuildSettings.scenes = settings.ToArray(); AssetDatabase.SaveAssets();
        }
    }
}
