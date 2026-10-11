#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DogLife
{
    public sealed class StoryBookValidation:MonoBehaviour
    {
        readonly List<string> report=new List<string>();
        readonly Dictionary<string,int> backup=new Dictionary<string,int>();
        const string Folder="Builds/StoryValidation";
        StoryBook book;
        float scale;
        CursorLockMode cursor;
        bool visible;
        void Check(string name,bool valid){report.Add((valid?"PASS ":"FAIL ")+name);}
        void Save()=>File.WriteAllLines(Folder+"/checks.txt",report);
        IEnumerator Start()
        {
            Directory.CreateDirectory(Folder);book=StoryBook.Instance;scale=Time.timeScale;cursor=Cursor.lockState;visible=Cursor.visible;
            foreach(var c in book.Library.chapters){string key="DogLife.Memories.v1."+c.id;backup[key]=PlayerPrefs.HasKey(key)?PlayerPrefs.GetInt(key):-1;PlayerPrefs.DeleteKey(key);}
            try{yield return Run();}
            finally{Restore();Save();}
        }
        void Restore()
        {
            if(book&&StoryBook.IsOpen){book.Finish();if(StoryBook.IsOpen)book.Finish();}
            foreach(var item in backup){if(item.Value<0)PlayerPrefs.DeleteKey(item.Key);else PlayerPrefs.SetInt(item.Key,item.Value);}
            PlayerPrefs.Save();Time.timeScale=scale;Cursor.lockState=cursor;Cursor.visible=visible;backup.Clear();
        }
        void OnDestroy(){Restore();}
        IEnumerator Run()
        {
            yield return new WaitForSecondsRealtime(1);
            Check("All eight chapters have imported illustrations and Korean font",book.Library.font&&book.Library.chapters.Length==8&&book.Library.chapters.All(c=>c.pages.Length>0&&c.pages.All(p=>p.artwork&&!string.IsNullOrEmpty(p.text))));
            Check("Karma boundaries -34 / +34",book.Library.Ending(-100,100)=="life"&&book.Library.Ending(-34,100)=="life"&&book.Library.Ending(-33,100)=="farewell"&&book.Library.Ending(33,100)=="farewell"&&book.Library.Ending(34,100)=="revenge"&&book.Library.Ending(100,100)=="revenge");
            Check("Karma scales with configured maximum",book.Library.Ending(-68,200)=="life"&&book.Library.Ending(68,200)=="revenge");
            var source=new GameObject("Karma carry source");var inventory=source.AddComponent<PlayerInventory>();var sourceKarma=source.AddComponent<PlayerKarma>();sourceKarma.SetValue(-57);
            var wallet=source.AddComponent<RunEconomy>();wallet.inventory=inventory;yield return null;
            wallet.CarryTo(SceneManager.GetActiveScene().path);
            var destination=new GameObject("Karma carry destination");var destinationInventory=destination.AddComponent<PlayerInventory>();var destinationKarma=destination.AddComponent<PlayerKarma>();
            var nextWallet=destination.AddComponent<RunEconomy>();nextWallet.inventory=destinationInventory;yield return null;
            Check("Season carry restores accumulated karma with inventory",Mathf.Approximately(destinationKarma.Value,-57));
            Object.Destroy(source);Object.Destroy(destination);yield return null;
            var fresh=new GameObject("Fresh karma run");fresh.AddComponent<PlayerInventory>();var freshKarma=fresh.AddComponent<PlayerKarma>();var freshWallet=fresh.AddComponent<RunEconomy>();freshWallet.inventory=fresh.GetComponent<PlayerInventory>();yield return null;
            Check("Fresh run does not inherit consumed karma carry",Mathf.Approximately(freshKarma.Value,0));Object.Destroy(fresh);yield return null;
            Check("Locked chapter cannot be replayed or unlocked by gallery",!book.Play("winter")&&!StoryProgress.Unlocked("winter"));
            var title=FindFirstObjectByType<TitleScreen>();
            var memories=FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b=>b.name=="Memories Button");
            memories.onClick.Invoke();yield return null;
            Check("Title button opens gallery with gameplay blocked",book.GalleryOpen&&Time.timeScale==0&&SkillWindow.BlocksGameplayInput);
            Check("Locked chapters hide original illustration",book.GetComponentsInChildren<RawImage>(true).Single(i=>i.name=="Memory illustration").texture==null);
            yield return Capture("gallery-locked");
            book.Finish();yield return null;
            Check("Gallery close restores time and cursor",Mathf.Approximately(Time.timeScale,scale)&&Cursor.lockState==cursor&&Cursor.visible==visible);
            int continuations=0;book.BeginNewGame(()=>continuations++);yield return new WaitForSecondsRealtime(1.1f);
            Check("First new game shows prologue before continuation",StoryBook.IsOpen&&book.CurrentChapter=="prologue"&&continuations==0&&StoryProgress.Unlocked("prologue"));
            GameSettingsWindow.Instance.Open();Check("Settings cannot overlap story",!GameSettingsWindow.IsOpen);
            yield return Capture("prologue");
            var atmosphere=book.GetComponentInChildren<StoryAtmosphere>();
            Check("Narration gradient and atmosphere render actual geometry",atmosphere&&atmosphere.GetComponent<CanvasRenderer>()&&atmosphere.canvasRenderer.GetMesh()&&atmosphere.canvasRenderer.GetMesh().vertexCount>8);
            book.NextPage();yield return new WaitForSecondsRealtime(1);
            Check("Manual next changes story page",book.CurrentPage==1);
            book.PreviousPage();Check("Previous page works",book.CurrentPage==0);
            book.Finish();yield return null;book.Finish();
            Check("Skipping prologue invokes continuation exactly once",continuations==1&&!StoryBook.IsOpen);
            book.BeginNewGame(()=>continuations++);Check("Every new game starts at prologue page zero",continuations==1&&StoryBook.IsOpen&&book.CurrentChapter=="prologue"&&book.CurrentPage==0);
            book.Finish();yield return null;
            Check("Repeated prologue continues exactly once",continuations==2&&!StoryBook.IsOpen);

            // Exercise the real director's final-room hook, without killing any live project enemies.
            var scene=SceneManager.CreateScene("Stage1_Spring");
            var go=new GameObject("Story test director");SceneManager.MoveGameObjectToScene(go,scene);
            var director=go.AddComponent<StageOneDirector>();director.enabled=false;
            var hud=go.AddComponent<PrototypeHUD>();hud.enabled=false;director.hud=hud;
            var hp=go.AddComponent<CombatHealth>();hp.player=true;hud.health=hp;
            var stream=go.AddComponent<ForestRoomStreaming>();stream.enabled=false;stream.director=director;stream.roomScenePaths=new[]{"a","b"};director.streaming=stream;
            director.RecordStreamedClear(1);Check("Non-final room does not unlock season",!StoryProgress.Unlocked("spring"));
            director.RecordStreamedClear(2);Check("Final room saves season before presentation",StoryProgress.Unlocked("spring")&&StoryBook.AwaitingClear);
            yield return new WaitForSecondsRealtime(3.2f);
            Check("Final boss clear presents correct season",StoryBook.IsOpen&&book.CurrentChapter=="spring");
            yield return Capture("spring-memory");
            book.Finish();yield return null;yield return null;
            Check("Completion guard released after reading",!StoryBook.AwaitingClear);
            director.RecordStreamedClear(2);yield return null;Check("Repeated clear notification does not reopen story",!StoryBook.IsOpen&&!StoryBook.AwaitingClear);
            Object.Destroy(go);yield return null;yield return SceneManager.UnloadSceneAsync(scene);
            var winterScene=SceneManager.CreateScene("Stage4_Winter");
            var winterGo=new GameObject("Winter story test director");SceneManager.MoveGameObjectToScene(winterGo,winterScene);
            var winterDirector=winterGo.AddComponent<StageOneDirector>();winterDirector.enabled=false;
            var winterHud=winterGo.AddComponent<PrototypeHUD>();winterHud.enabled=false;winterDirector.hud=winterHud;
            var winterHealth=winterGo.AddComponent<CombatHealth>();winterHealth.player=true;winterHud.health=winterHealth;
            var winterKarma=winterGo.AddComponent<PlayerKarma>();winterKarma.SetValue(80);
            book.QueueClear(winterDirector);yield return new WaitForSecondsRealtime(3);
            Check("Winter clear waits for chapter before ending",book.CurrentChapter=="winter"&&StoryBook.IsOpen&&!StoryProgress.Unlocked("revenge"));
            book.Finish();yield return null;yield return null;
            Check("Winter routes to karma ending and unlocks only that ending",StoryBook.IsOpen&&book.CurrentChapter=="revenge"&&StoryProgress.Unlocked("revenge")&&!StoryProgress.Unlocked("life")&&!StoryProgress.Unlocked("farewell"));
            book.Finish();yield return null;yield return null;
            Check("Ending completion releases stage continuation",!StoryBook.AwaitingClear&&!StoryBook.IsOpen);
            Object.Destroy(winterGo);yield return null;yield return SceneManager.UnloadSceneAsync(winterScene);
            foreach(var c in book.Library.chapters)StoryProgress.Unlock(c.id);
            book.OpenGallery();yield return null;yield return Capture("gallery-collected");
            book.Play("summer");yield return new WaitForSecondsRealtime(1);
            Check("Gallery replay pauses and shows unlocked story",book.CurrentChapter=="summer"&&Time.timeScale==0);
            yield return Capture("summer-memory");book.Finish();
            Check("Replay returns to gallery instead of loading gameplay",book.GalleryOpen);book.Finish();yield return null;
            book.Play("winter");yield return new WaitForSecondsRealtime(1);yield return Capture("winter-memory");book.Finish();yield return null;
            foreach(string id in new[]{"life","farewell","revenge"}){book.Play(id);yield return new WaitForSecondsRealtime(1);book.NextPage();yield return new WaitForSecondsRealtime(1);yield return Capture("ending-"+id);book.Finish();yield return null;}
            Check("All unlocked memories survive repeated replay and close",book.Library.chapters.All(c=>StoryProgress.Unlocked(c.id)));
            report.Add("Original player memory unlocks restored after validation.");
        }
        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            // Render the actual overlay hierarchy into a known-size target for reproducible UI review.
            var camera=Camera.main;var canvas=book.GetComponentInChildren<Canvas>();var mode=canvas.renderMode;var oldCamera=canvas.worldCamera;
            var rt=new RenderTexture(1600,900,24);var oldTarget=camera.targetTexture;int mask=camera.cullingMask;camera.cullingMask=~0;
            canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
            camera.targetTexture=rt;Canvas.ForceUpdateCanvases();camera.Render();
            var oldActive=RenderTexture.active;RenderTexture.active=rt;
            var texture=new Texture2D(1600,900,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1600,900),0,0);texture.Apply();
            File.WriteAllBytes(Folder+"/"+name+".png",texture.EncodeToPNG());
            RenderTexture.active=oldActive;camera.targetTexture=oldTarget;camera.cullingMask=mask;canvas.renderMode=mode;canvas.worldCamera=oldCamera;Object.Destroy(texture);rt.Release();Object.Destroy(rt);
            Canvas.ForceUpdateCanvases();
        }
    }
}
#endif
