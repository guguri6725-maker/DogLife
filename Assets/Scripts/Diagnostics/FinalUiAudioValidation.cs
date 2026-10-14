#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
namespace DogLife
{
    public sealed class FinalUiAudioValidation:MonoBehaviour
    {
        const BindingFlags F=BindingFlags.NonPublic|BindingFlags.Instance;
        readonly List<string> report=new List<string>(),heard=new List<string>();readonly Dictionary<string,float> times=new Dictionary<string,float>();
        GameAudio audio;AudioPreferences preferences;StoryBook book;GameObject player,directorObject;BossMemoryPickup pickup;
        int unlock;bool backedUp;float deathStart;
        AudioSource Music=>(AudioSource)typeof(GameAudio).GetField("musicSource",F).GetValue(audio);
        AudioSource Effect(string key)=>audio.GetComponentsInChildren<AudioSource>().FirstOrDefault(s=>s.clip&&s.clip.name==key&&s.isPlaying);
        void Heard(string key){heard.Add(key);times[key]=Time.unscaledTime;}
        void Check(bool valid,string name){report.Add((valid?"PASS ":"FAIL ")+name);System.IO.Directory.CreateDirectory("Builds/AudioValidation");System.IO.File.WriteAllLines("Builds/AudioValidation/final-ui-checks.txt",report);}
        IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(1);
            audio=FindFirstObjectByType<GameAudio>();book=StoryBook.Instance;preferences=GameAudio.Current;GameAudio.Preview(AudioPreferences.Defaults);GameAudio.EffectStarted+=Heard;
            unlock=PlayerPrefs.HasKey("DogLife.Memories.v1.prologue")?PlayerPrefs.GetInt("DogLife.Memories.v1.prologue"):-1;backedUp=true;PlayerPrefs.SetInt("DogLife.Memories.v1.prologue",1);
            try{yield return Run();}finally{Restore();}
        }
        IEnumerator Run()
        {
            Check(Music.isPlaying&&Music.clip.name=="Title_BGM","Title starts music");
            Check(Music.clip.channels==2&&Music.clip.loadType==AudioClipLoadType.Streaming,"Title uses stereo streaming");
            Time.timeScale=0;yield return null;yield return null;Check(Music.isPlaying,"Title music continues under settings pause");Time.timeScale=1;
            var first=Music;Music.time=Music.clip.length-1.1f;yield return new WaitForSecondsRealtime(1.5f);Check(Music!=first&&Music.isPlaying,"Title music crossfades loop boundary");
            player=Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab"));
            foreach(var b in player.GetComponents<MonoBehaviour>())b.enabled=false;
            var hp=player.GetComponent<CombatHealth>();hp.enabled=true;hp.Restore();player.GetComponent<Rigidbody>().isKinematic=true;
            directorObject=new GameObject("Audio memory test director");var director=directorObject.AddComponent<StageOneDirector>();director.enabled=false;
            var hud=directorObject.AddComponent<PrototypeHUD>();hud.enabled=false;hud.motor=player.GetComponent<PlayerMotor>();hud.health=hp;director.hud=hud;
            pickup=BossMemoryPickup.Create(director,book.Library.Find("spring"));yield return null;
            var loop=Effect("Memory_Object_Loop");Check(loop&&loop.loop&&loop.spatialBlend==1&&loop.maxDistance==12,"Memory crystal uses local spatial loop");
            Check(!pickup.TryInteract(),"Memory cannot collect before spawn settles");
            var settings=AudioPreferences.Defaults;settings.effects=0;GameAudio.Preview(settings);yield return null;
            Check(loop.volume==0&&Music.volume>0,"Effects slider mutes crystal independently of music");GameAudio.Preview(AudioPreferences.Defaults);
            Time.timeScale=0;yield return null;yield return null;Check(!loop.isPlaying,"Crystal loop pauses with gameplay");Time.timeScale=1;yield return null;yield return null;Check(loop.isPlaying,"Crystal loop resumes");
            yield return new WaitForSeconds(.85f);Check(pickup.TryInteract()&&!Effect("Memory_Object_Loop"),"F interaction stops crystal loop");Destroy(pickup.gameObject);pickup=null;
            book.Play("prologue");yield return new WaitForSecondsRealtime(1);Check(Music.isPlaying&&Music.clip.name=="Story_Prologue","Story replaces title BGM");
            heard.Clear();book.NextPage();Check(!heard.Contains("Memory_Page_Turn"),"Revealing text has no page sound");yield return null;book.NextPage();Check(heard.Count(k=>k=="Memory_Page_Turn")==1,"Actual next page sounds once while paused");
            yield return new WaitForSecondsRealtime(.15f);book.PreviousPage();Check(heard.Count(k=>k=="Memory_Page_Turn")==2,"Previous page has page sound");
            book.Finish();yield return new WaitForSecondsRealtime(2);Check(!StoryBook.IsOpen&&Music.isPlaying&&Music.clip.name=="Title_BGM","Closing story restores title BGM");
            book.OpenGallery();yield return null;Check(book.GalleryOpen&&Music.isPlaying&&Music.clip.name=="Title_BGM","Gallery retains title BGM");book.Finish();yield return null;
            GameAudio.StopMusic(FindFirstObjectByType<TitleScreen>());heard.Clear();times.Clear();deathStart=Time.unscaledTime;
            hp.FatalDamage();PlayerDeathSequence.Begin(hp);yield return new WaitForSecondsRealtime(1.8f);
            Check(!heard.Contains("Player_Death")&&!heard.Contains("Death_Result_Show"),"No premature death impact or result sound");
            yield return new WaitForSecondsRealtime(2.6f);var sequence=player.GetComponent<PlayerDeathSequence>();
            Check(heard.Count(k=>k=="Player_Death")==1&&times["Player_Death"]-deathStart>=2.4f,"One death sound at collapse window");
            Check(sequence.MenuVisible&&heard.Count(k=>k=="Death_Result_Show")==1&&times["Death_Result_Show"]>times["Player_Death"],"Result sound follows animation and appears once");
            typeof(PlayerDeathSequence).GetMethod("ShowMenu",F).Invoke(sequence,null);Check(heard.Count(k=>k=="Death_Result_Show")==1,"Result redraw cannot duplicate sound");
            Destroy(player);player=null;yield return null;Check(!Effect("Death_Result_Show")&&!Effect("Player_Death"),"Player unload stops death sounds");
            Time.timeScale=1;FindFirstObjectByType<TitleScreen>().ResumeMenuMusic();Check(true,"Complete; original unlock and audio settings restored");
        }
        void Restore()
        {
            GameAudio.EffectStarted-=Heard;
            if(book&&StoryBook.IsOpen)typeof(StoryBook).GetMethod("Release",F).Invoke(book,new object[]{false});
            if(backedUp){if(unlock<0)PlayerPrefs.DeleteKey("DogLife.Memories.v1.prologue");else PlayerPrefs.SetInt("DogLife.Memories.v1.prologue",unlock);PlayerPrefs.Save();backedUp=false;}
            if(pickup)Destroy(pickup.gameObject);if(player)Destroy(player);if(directorObject)Destroy(directorObject);
            GameAudio.Preview(preferences);Time.timeScale=1;
        }
        void OnDestroy(){Restore();}
    }
}
#endif
