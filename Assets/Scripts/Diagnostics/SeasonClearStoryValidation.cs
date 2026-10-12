#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace DogLife
{
    public sealed class SeasonClearStoryValidation : MonoBehaviour
    {
        readonly Dictionary<string,int> saved=new Dictionary<string,int>();
        readonly List<string> results=new List<string>();
        StoryBook book;
        void Check(string label,bool value){results.Add((value?"PASS ":"FAIL ")+label);}
        IEnumerator Start()
        {
            book=StoryBook.Instance;
            foreach(var c in book.Library.chapters){string k="DogLife.Memories.v1."+c.id;saved[k]=PlayerPrefs.HasKey(k)?PlayerPrefs.GetInt(k):-1;}
            try{yield return Run();}
            finally{Restore();Directory.CreateDirectory("Builds/StoryValidation");File.WriteAllLines("Builds/StoryValidation/season-clears.txt",results);}
        }
        void Restore(){foreach(var p in saved){if(p.Value<0)PlayerPrefs.DeleteKey(p.Key);else PlayerPrefs.SetInt(p.Key,p.Value);}PlayerPrefs.Save();saved.Clear();}
        void OnDestroy(){Restore();}
        IEnumerator Run()
        {
            foreach(string id in new[]{"spring","summer","autumn","winter"})
            for(int run=0;run<2;run++)
            {
                var chapter=book.Library.Find(id);
                if(run==0)PlayerPrefs.DeleteKey("DogLife.Memories.v1."+id);
                var scene=SceneManager.GetSceneByName(chapter.sceneName);
                bool temporary=!scene.IsValid()||!scene.isLoaded;
                if(temporary)scene=SceneManager.CreateScene(chapter.sceneName);
                var go=new GameObject("Season story verification");SceneManager.MoveGameObjectToScene(go,scene);
                var d=go.AddComponent<StageOneDirector>();d.enabled=false;
                var hud=go.AddComponent<PrototypeHUD>();hud.enabled=false;d.hud=hud;
                hud.motor=FindFirstObjectByType<PlayerMotor>();
                hud.health=go.AddComponent<CombatHealth>();hud.health.player=true;
                var stream=go.AddComponent<ForestRoomStreaming>();stream.enabled=false;stream.roomScenePaths=new[]{"first","boss"};d.streaming=stream;
                d.RecordStreamedClear(1);Check(id+" nonfinal run "+run,!StoryBook.IsOpen&&!StoryBook.AwaitingClear);
                d.RecordStreamedClear(2);
                float deadline=Time.realtimeSinceStartup+8;
                BossMemoryPickup pickup=null;
                while(!pickup&&Time.realtimeSinceStartup<deadline){pickup=FindFirstObjectByType<BossMemoryPickup>();yield return null;}
                Check(id+" waits for interaction "+run,pickup&&!StoryBook.IsOpen&&StoryBook.AwaitingClear);
                if(pickup){yield return new WaitForSecondsRealtime(1.1f);pickup.transform.position=hud.motor.transform.position;Check(id+" interaction accepted "+run,pickup.TryInteract());Check(id+" duplicate interaction blocked "+run,!pickup.TryInteract());}
                deadline=Time.realtimeSinceStartup+8;
                while(!StoryBook.IsOpen&&Time.realtimeSinceStartup<deadline)yield return null;
                Check(id+" clear run "+run,StoryBook.IsOpen&&book.CurrentChapter==id&&StoryProgress.Unlocked(id));
                book.Finish();yield return null;yield return null;
                if(id=="winter"){Check("winter ending run "+run,StoryBook.IsOpen&&book.CurrentChapter=="farewell");book.Finish();yield return null;yield return null;}
                Check(id+" continuation released "+run,!StoryBook.AwaitingClear&&!StoryBook.IsOpen);
                d.RecordStreamedClear(2);yield return null;
                Check(id+" duplicate ignored "+run,!StoryBook.AwaitingClear&&!StoryBook.IsOpen);
                Destroy(go);yield return null;if(temporary)yield return SceneManager.UnloadSceneAsync(scene);
            }
            Debug.Log("Season clear story validation complete: "+string.Join("\n",results));
        }
    }
}
#endif
