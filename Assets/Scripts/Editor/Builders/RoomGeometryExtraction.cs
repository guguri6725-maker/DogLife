using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace DogLife.EditorTools
{
    public static class RoomGeometryExtraction
    {
        public static string ExtractSpring()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Edit mode required.");
            var stage=Object.FindFirstObjectByType<ForestRoomStreaming>();
            if(!stage)throw new InvalidOperationException("Open Spring stage.");
            const string folder="Assets/Resources/StreamedSpringGeometry";
            System.IO.Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            int total=0;
            foreach(var path in stage.roomScenePaths.Skip(1))
            {
                var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
                try
                {
                    var room=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ForestRoom>(true)).Single();
                    var so=new SerializedObject(room);
                    var paths=so.FindProperty("streamedPrefabPaths");
                    if(paths.arraySize>0)continue;
                    var geometry=so.FindProperty("streamedGeometry");
                    var candidates=new List<GameObject>();
                    for(int i=0;i<geometry.arraySize;i++)
                    {
                        var go=geometry.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
                        if(!go||!go.GetComponentInChildren<MeshFilter>(true))continue;
                        // 문과 게임용 스크립트는 씬에 유지합니다.
                        if(go.GetComponentsInChildren<Component>(true).Any(c=>!(c is Transform||c is MeshFilter||c is MeshRenderer||c is MeshCollider||c is LODGroup)))continue;
                        var subtree=new HashSet<Object>(go.GetComponentsInChildren<Component>(true).Cast<Object>());
                        foreach(var t in go.GetComponentsInChildren<Transform>(true))subtree.Add(t.gameObject);
                        bool externalReference=false;
                        foreach(var component in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Component>(true)))
                        {
                            if(!component||component==room||subtree.Contains(component))continue;
                            var iterator=new SerializedObject(component).GetIterator();
                            while(iterator.Next(true))
                                if(iterator.propertyType==SerializedPropertyType.ObjectReference && subtree.Contains(iterator.objectReferenceValue))
                                {
                                    // 부모 Transform의 자식 목록은 추출 시 자동 갱신됩니다.
                                    if(component is Transform && iterator.propertyPath.StartsWith("m_Children"))continue;
                                    externalReference=true;break;
                                }
                            if(externalReference)break;
                        }
                        if(!externalReference)candidates.Add(go);
                    }
                    var resources=new List<string>();
                    foreach(var go in candidates)
                    {
                        string resource="StreamedSpringGeometry/Room"+room.index+"_"+go.name;
                        string asset="Assets/Resources/"+resource+".prefab";
                        if(AssetDatabase.LoadAssetAtPath<GameObject>(asset))throw new InvalidOperationException("Asset already exists: "+asset);
                        go.SetActive(false);
                        var saved=PrefabUtility.SaveAsPrefabAsset(go,asset);
                        if(!saved)throw new InvalidOperationException("Could not save "+asset);
                        // 부모 기준 위치/회전/스케일을 그대로 유지합니다.
                        if(saved.transform.localPosition!=go.transform.localPosition||saved.transform.localScale!=go.transform.localScale)
                            throw new InvalidOperationException("Transform changed: "+asset);
                        resources.Add(resource);
                    }
                    var remaining=new List<GameObject>();
                    for(int i=0;i<geometry.arraySize;i++)
                    {
                        var go=geometry.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
                        if(go&&!candidates.Contains(go))remaining.Add(go);
                    }
                    geometry.arraySize=remaining.Count;
                    for(int i=0;i<remaining.Count;i++)geometry.GetArrayElementAtIndex(i).objectReferenceValue=remaining[i];
                    paths.arraySize=resources.Count;
                    for(int i=0;i<resources.Count;i++)paths.GetArrayElementAtIndex(i).stringValue=resources[i];
                    so.ApplyModifiedPropertiesWithoutUndo();
                    foreach(var go in candidates)Object.DestroyImmediate(go);
                    EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                    total+=resources.Count;
                }
                finally {EditorSceneManager.CloseScene(scene,true);}
            }
            AssetDatabase.SaveAssets();
            return "Extracted "+total+" geometry groups.";
        }
    }
}
