using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Editor-only authoring tool. The resulting scene contains map geometry, not gameplay scripts.
public static class SummerCoastMapBuilder
{
    const string Root = "Assets/Environment/SummerCoast";
    const string ScenePath = "Assets/Scenes/3D_Stage2.unity";
    static readonly string[] Names = { "SandTile", "ShoreTile", "StoneWall", "LargeRocks", "RockCluster", "PalmTree", "Crates", "Shipwreck" };
    static readonly Dictionary<string, GameObject> Models = new Dictionary<string, GameObject>();
    static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
    static int serial;
    static Transform Group(string name, Transform parent)
    { var go = new GameObject(name); go.transform.SetParent(parent, false); return go.transform; }
    static Material Material(string name, Color color)
    {
        string path = Root + "/Materials/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!mat) { mat = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(mat, path); }
        mat.color = color; mat.enableInstancing = true; mat.SetFloat("_Glossiness", .2f); return mat;
    }
    static Texture2D Texture(string name, string suffix)
    { return AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/Summer_" + name + "_" + suffix + ".png"); }
    public static void PrepareAssets()
    {
        foreach (string name in Names)
        {
            foreach (string suffix in new[] { "BaseColor", "Normal", "MetallicSmoothness", "AO" })
            {
                string path = Root + "/Textures/Summer_" + name + "_" + suffix + ".png";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (!importer) continue;
                importer.textureType = suffix == "Normal" ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.sRGBTexture = suffix == "BaseColor"; importer.maxTextureSize = 2048; importer.SaveAndReimport();
            }
            var mat = Material("Summer_" + name + "_Standard", Color.white);
            mat.mainTexture = Texture(name, "BaseColor"); mat.SetTexture("_BumpMap", Texture(name, "Normal")); mat.EnableKeyword("_NORMALMAP");
            mat.SetTexture("_MetallicGlossMap", Texture(name, "MetallicSmoothness")); mat.EnableKeyword("_METALLICGLOSSMAP");
            mat.SetTexture("_OcclusionMap", Texture(name, "AO")); mat.SetFloat("_GlossMapScale", .45f); EditorUtility.SetDirty(mat);
        }
        AssetDatabase.SaveAssets();
    }
    static void Load()
    {
        Models.Clear(); Materials.Clear(); serial = 0;
        foreach (string name in Names)
        {
            Models[name] = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Models/Summer_" + name + ".fbx");
            Materials[name] = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/Summer_" + name + "_Standard.mat");
            if (!Models[name] || !Materials[name]) throw new System.InvalidOperationException("Missing summer asset: " + name);
        }
    }
    static GameObject Place(string type, Transform parent, Vector3 position, float scale = 1, float yaw = 0, bool solid = true)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(Models[type], parent);
        go.name = type + "_" + (++serial).ToString("000"); go.transform.localPosition = position;
        go.transform.localRotation = Quaternion.Euler(0, yaw, 0); go.transform.localScale = Vector3.one * scale;
        foreach (var r in go.GetComponentsInChildren<Renderer>()) { r.sharedMaterial = Materials[type]; r.gameObject.isStatic = true; }
        if (solid)
        {
            var rs = go.GetComponentsInChildren<MeshFilter>(); var bounds = rs[0].sharedMesh.bounds;
            var collider = go.AddComponent<BoxCollider>(); collider.center = bounds.center; collider.size = bounds.size;
            // Palm leaves should not obstruct the playable space.
            if (type == "PalmTree") { collider.center = new Vector3(0, 2, 0); collider.size = new Vector3(.8f, 4, .8f); }
            go.layer = 8;
        }
        return go;
    }
    static void Box(string name, Transform parent, Vector3 center, Vector3 size)
    { var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = center; go.layer = 8; go.AddComponent<BoxCollider>().size = size; }
    static float Center(float x) { return Mathf.Sin(x * .042f) * 2.4f; }
    static void Marker(string name, Transform parent, Vector3 position)
    { var t = Group(name, parent); t.localPosition = position; }

    public static void Build()
    {
        Load();
        var active = SceneManager.GetActiveScene();
        if (active.path != ScenePath)
        {
            var target = SceneManager.GetSceneByPath(ScenePath);
            if (!target.isLoaded) target = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(target);
        }
        var scene = SceneManager.GetActiveScene();
        foreach (var go in scene.GetRootGameObjects()) if (go.name == "Stage2_3D_SummerCoast") throw new System.InvalidOperationException("Map already exists; do not overwrite authored edits.");
        var map = new GameObject("Stage2_3D_SummerCoast").transform;
        var floor = Group("CoastFloor", map); var shore = Group("Shoreline", map); var palms = Group("Canopy", map);
        var rocks = Group("Rocks", map); var ruins = Group("Ruins", map); var props = Group("Landmarks", map);
        var platforms = Group("Platforms", map); var boss = Group("BossArena", map); var boundaries = Group("Boundaries", map); var markers = Group("LayoutMarkers", map);
        // Match 3D.unity: six 23 m sections and a larger final arena near X=155.
        for (int ix = 0; ix < 32; ix++)
        {
            float x = -9 + ix * 6;
            for (int iz = -2; iz <= 2; iz++)
            {
                var tile = Place("SandTile", floor, new Vector3(x, -.3f, iz * 6), 6.5f, ((ix + iz + 8) % 4) * 90, false);
                tile.transform.localScale = new Vector3(6.5f, 3.2f, 6.5f);
                foreach (var r in tile.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            Place("ShoreTile", shore, new Vector3(x, -1.6f, -18), 6.15f, 0, false);
            Place("ShoreTile", shore, new Vector3(x, -1.6f, 18), 6.15f, 180, false);
        }
        Box("WalkableGround", boundaries, new Vector3(84, -.6f, 0), new Vector3(192, 1.2f, 30));
        var sandBed = GameObject.CreatePrimitive(PrimitiveType.Cube); sandBed.name = "Continuous sand underlay"; sandBed.transform.SetParent(floor, false);
        sandBed.transform.localPosition = new Vector3(84, -.67f, 0); sandBed.transform.localScale = new Vector3(194, 1, 31);
        Object.DestroyImmediate(sandBed.GetComponent<Collider>());
        var sandMat = Material("Summer_Sand_Underlay", new Color(.8f, .74f, .56f)); sandMat.mainTexture = Texture("SandTile", "BaseColor"); sandMat.mainTextureScale = new Vector2(32, 5); sandBed.GetComponent<Renderer>().sharedMaterial = sandMat;
        Box("South sea boundary", boundaries, new Vector3(84, 3, -15.4f), new Vector3(194, 7, .8f));
        Box("North cliff boundary", boundaries, new Vector3(84, 3, 15.4f), new Vector3(194, 7, .8f));
        Box("West boundary", boundaries, new Vector3(-12.5f, 3, 0), new Vector3(1, 7, 32));
        Box("East boundary", boundaries, new Vector3(180.5f, 3, 0), new Vector3(1, 7, 32));
        // Ocean extends beneath the shoreline and beyond the playable collision boundary.
        var sea = GameObject.CreatePrimitive(PrimitiveType.Plane); sea.name = "Summer sea"; sea.transform.SetParent(shore, false);
        sea.transform.localPosition = new Vector3(84, -1.1f, 0); sea.transform.localScale = new Vector3(27, 1, 15); Object.DestroyImmediate(sea.GetComponent<Collider>());
        var water = Material("Summer_Sea", new Color(.055f, .55f, .65f)); water.SetFloat("_Glossiness", .7f); sea.GetComponent<Renderer>().sharedMaterial = water;
        for (int i = 0; i < 32; i++)
        {
            float x = -10 + i * 6;
            Place("LargeRocks", rocks, new Vector3(x, -.25f, 19 + Mathf.Sin(i * 1.7f)), 1.2f + (i % 3) * .16f, i * 73, false);
            if (i % 2 == 0) Place("RockCluster", rocks, new Vector3(x + 1, -.25f, -15.5f), 1.2f, i * 51, false);
            if (i % 3 != 1)
            {
                Place("PalmTree", palms, new Vector3(x, -.03f, 11.8f), .85f + (i % 3) * .12f, i * 41);
                if (i % 2 == 0) Place("PalmTree", palms, new Vector3(x + 2, -.03f, -12.3f), .7f, i * 79);
            }
        }
        string[] zones = { "01_Beach_Entrance", "02_Palm_Oasis", "03_Rock_Pass", "04_Sunken_Ruins", "05_Cargo_Cove", "06_Shipwreck_Coast", "07_Tidal_Sanctuary" };
        for (int room = 0; room < 7; room++)
        {
            float x = room < 6 ? 11 + room * 23 : 155;
            float z = Center(x);
            Marker(zones[room], map, new Vector3(x, 0, z));
            Marker("EncounterSpace_" + (room + 1), markers, new Vector3(x, .05f, z));
            if (room < 6)
            {
                float gateX = 22 + room * 23, gateZ = Center(gateX);
                Marker("Doorway_" + (room + 1), markers, new Vector3(gateX, 0, gateZ));
                // Leave a 7 m opening; wall meshes face across the route instead of sealing it.
                for (int side = -1; side <= 1; side += 2)
                    for (int j = 0; j < 2; j++) Place("StoneWall", ruins, new Vector3(gateX, -.08f, gateZ + side * (6 + j * 4)), 1, 90);
                Place("RockCluster", rocks, new Vector3(x - 3, -.05f, z + 6.5f), 1.3f, room * 52);
                Place("Crates", props, new Vector3(x + 4, 0, z - 7), .9f, room * 31);
                Place("LargeRocks", platforms, new Vector3(x + 5, -.25f, z + 9), .8f, room * 24);
            }
        }
        // Distinct landmarks for navigation and optional side pockets.
        Place("StoneWall", ruins, new Vector3(-5, 0, -6), 1.3f, 90);
        Place("StoneWall", ruins, new Vector3(-5, 0, 6), 1.3f, 90);
        Place("Crates", props, new Vector3(-2, 0, -9), 1.1f, -15);
        Place("PalmTree", palms, new Vector3(30, 0, -8), 1.1f, 90);
        Place("PalmTree", palms, new Vector3(36, 0, -9), 1.15f, 140);
        Place("LargeRocks", rocks, new Vector3(53, 0, -7), 1.4f, 50);
        Place("LargeRocks", rocks, new Vector3(60, 0, 8), 1.4f, 225);
        for (int j = 0; j < 3; j++) Place("StoneWall", ruins, new Vector3(74 + j * 4, 0, 10), 1.1f, 0);
        Place("StoneWall", ruins, new Vector3(85, 0, 7), 1.1f, 90);
        for (int j = 0; j < 4; j++) Place("Crates", props, new Vector3(98 + (j % 2) * 3, 0, -7 - (j / 2) * 3), 1.15f, j * 15);
        Place("Shipwreck", props, new Vector3(125, -.25f, 8), .82f, 70);
        Place("Crates", props, new Vector3(129, 0, -7), 1.1f, 20);
        Place("RockCluster", rocks, new Vector3(120, 0, -10), 1.7f, 30);
        // Broad, empty boss footprint; ship and ruin walls frame its perimeter.
        Place("Shipwreck", boss, new Vector3(156, -.4f, 13), .8f, -70, false);
        for (int i = 0; i < 5; i++) Place("StoneWall", boss, new Vector3(145 + i * 5, 0, 11), 1.1f, 0);
        Place("LargeRocks", boss, new Vector3(144, -.1f, -10), 1.4f, 40);
        Place("LargeRocks", boss, new Vector3(166, -.1f, -10), 1.4f, -40);
        Place("StoneWall", boss, new Vector3(173, 0, -6), 1.3f, 90);
        Place("StoneWall", boss, new Vector3(173, 0, 8), 1.3f, 90);
        Marker("PlayerStart", markers, new Vector3(0, 1.2f, 0)); Marker("StageExit", markers, new Vector3(174, .1f, 0));
        Marker("RecoverySpace_1", markers, new Vector3(68, .1f, -8)); Marker("RecoverySpace_2", markers, new Vector3(137, .1f, -8));
        Camera camera = null; Light sun = null;
        foreach (var go in scene.GetRootGameObjects()) { if (go.GetComponent<Camera>()) camera = go.GetComponent<Camera>(); if (go.GetComponent<Light>()) sun = go.GetComponent<Light>(); }
        if (!camera) camera = new GameObject("Main Camera").AddComponent<Camera>();
        camera.tag = "MainCamera"; camera.transform.position = new Vector3(18, 33, -36); camera.transform.LookAt(new Vector3(25, 0, 0));
        camera.orthographic = true; camera.orthographicSize = 24; camera.farClipPlane = 400; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.32f, .72f, .8f);
        if (!sun) sun = new GameObject("Directional Light").AddComponent<Light>();
        sun.type = LightType.Directional; sun.transform.rotation = Quaternion.Euler(48, -32, 0); sun.color = new Color(1, .94f, .8f); sun.intensity = .85f; sun.shadows = LightShadows.Soft;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight; RenderSettings.ambientSkyColor = new Color(.64f, .77f, .85f); RenderSettings.ambientEquatorColor = new Color(.5f, .6f, .62f); RenderSettings.ambientGroundColor = new Color(.37f, .32f, .24f); RenderSettings.fog = false;
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        if (active.IsValid() && active != scene && !active.isDirty) EditorSceneManager.CloseScene(active, true);
        if (SceneView.lastActiveSceneView) SceneView.lastActiveSceneView.LookAt(new Vector3(80, 0, 0), Quaternion.Euler(55, 0, 0), 100);
        Selection.activeGameObject = map.gameObject;
    }
}
