using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DogLife.EditorTools
{
    [InitializeOnLoad]
    public static class StreamedRoomGeometryBuilder
    {
        static StreamedRoomGeometryBuilder() { EditorSceneManager.sceneSaving+=OnSaving; }
        // 미리보기 후 저장해도 로딩 최적화 상태로 저장합니다.
        static void OnSaving(Scene scene,string path)
        {
            if(Application.isPlaying)return;
            foreach(var room in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ForestRoom>(true)))
            {
                var serialized=new SerializedObject(room);
                var list=serialized.FindProperty("streamedGeometry");
                for(int i=0;i<list.arraySize;i++)
                {
                    var item=list.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
                    if(item)item.SetActive(false);
                }
            }
        }

        public static string PrepareSpringRooms()
        {
            var streaming=Object.FindFirstObjectByType<ForestRoomStreaming>();
            if(!streaming||Application.isPlaying)throw new System.InvalidOperationException("Open the spring stage in Edit mode.");
            int count=0;
            foreach(var path in streaming.roomScenePaths)
            {
                var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.isLoaded;
                if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
                try
                {
                    var room=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ForestRoom>(true)).Single();
                    var serialized=new SerializedObject(room);var list=serialized.FindProperty("streamedGeometry");
                    var objects=new List<GameObject>();
                    for(int i=0;i<list.arraySize;i++)
                    {
                        var item=list.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
                        if(item)objects.Add(item);
                    }
                    foreach(Transform child in room.transform)
                    {
                        if(child.gameObject==room.encounter||!child.gameObject.activeSelf||objects.Contains(child.gameObject))continue;
                        if(child.GetComponentInChildren<Renderer>(true)||child.GetComponentInChildren<Collider>(true))objects.Add(child.gameObject);
                    }
                    list.arraySize=objects.Count;
                    for(int i=0;i<objects.Count;i++){list.GetArrayElementAtIndex(i).objectReferenceValue=objects[i];objects[i].SetActive(false);}
                    serialized.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(room);
                    EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);count+=objects.Count;
                }
                finally {if(opened)EditorSceneManager.CloseScene(scene,true);}
            }
            return "Prepared "+count+" geometry groups in "+streaming.roomScenePaths.Length+" rooms.";
        }
    }
}
