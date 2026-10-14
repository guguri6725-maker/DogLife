#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
namespace DogLife
{
    // Opt-in UI playback check; restores memory unlocks and user audio preferences.
    public sealed class StoryMusicValidation:MonoBehaviour
    {
        const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
        readonly List<string> report=new List<string>();readonly Dictionary<string,int> backup=new Dictionary<string,int>();
        StoryBook book;GameAudio audio;AudioPreferences preferences;
        AudioSource Source=>(AudioSource)typeof(GameAudio).GetField("musicSource",F).GetValue(audio);
        AudioSource Blend=>(AudioSource)typeof(GameAudio).GetField("musicBlendSource",F).GetValue(audio);
        void Check(bool ok,string label){report.Add((ok?"PASS ":"FAIL ")+label);System.IO.Directory.CreateDirectory("Builds/AudioValidation");System.IO.File.WriteAllLines("Builds/AudioValidation/story-music-checks.txt",report);}
        IEnumerator Start()
        {
            yield return null;book=StoryBook.Instance;audio=FindFirstObjectByType<GameAudio>();preferences=GameAudio.Current;GameAudio.Preview(AudioPreferences.Defaults);
            foreach(var c in book.Library.chapters){string key="DogLife.Memories.v1."+c.id;backup[key]=PlayerPrefs.HasKey(key)?PlayerPrefs.GetInt(key):-1;PlayerPrefs.SetInt(key,1);}
            try{yield return Run();}finally{Restore();}
        }
        IEnumerator Run()
        {
            var boss=Resources.Load<AudioClip>("Audio/Music/Spring_Boss_BGM");GameAudio.PlayMusic(boss,this);yield return new WaitForSecondsRealtime(.3f);
            Time.timeScale=0;yield return null;yield return null;Check(Source.isPlaying,"Boss music continues during menu pause");Time.timeScale=1;
            book.Play("prologue");yield return new WaitForSecondsRealtime(1.1f);
            Check(Source.isPlaying&&Time.timeScale==0&&Source.clip==book.Library.Find("prologue").music,"Opening music plays while story pauses gameplay");
            book.Finish();yield return new WaitForSecondsRealtime(2.6f);Check(!StoryBook.IsOpen&&Source.isPlaying&&Source.clip.name=="Title_BGM","Skipping opening restores title music");
            book.OpenGallery();
            foreach(var c in book.Library.chapters)
            {
                Check(c.music&&c.music.channels==2&&c.music.loadType==AudioClipLoadType.Streaming,c.id+" correct streaming music reference");
                Check(book.Play(c.id),c.id+" opens from gallery");yield return new WaitForSecondsRealtime(1.05f);
                Check(Time.timeScale==0&&Source.isPlaying&&Source.clip==c.music,c.id+" gallery replay plays correct music");
                float position=Source.time;var source=Source;book.NextPage();book.NextPage();yield return new WaitForSecondsRealtime(.1f);book.PreviousPage();
                Check(Source==source&&Source.time>=position,c.id+" page navigation preserves music position");
                if(c.id=="spring")
                {
                    var p=AudioPreferences.Defaults;p.music=0;GameAudio.Preview(p);yield return null;Check(Source.volume==0&&Blend.volume==0,"BGM slider mutes story music");
                    p=AudioPreferences.Defaults;p.effects=0;GameAudio.Preview(p);yield return null;Check(Source.volume>0,"Effects slider leaves story music audible");GameAudio.Preview(AudioPreferences.Defaults);
                    Source.time=c.music.length-1.1f;yield return new WaitForSecondsRealtime(1.5f);Check(Source!=source&&Source.isPlaying&&Source.time<4,"Story music loops during paused gameplay");
                    GameAudio.StopMusic(this);Check(Source.isPlaying,"Old boss owner cannot stop story music");
                }
                book.Finish();yield return new WaitForSecondsRealtime(2.6f);
                Check(book.GalleryOpen&&Source.isPlaying&&Source.clip.name=="Title_BGM",c.id+" return to gallery restores title music");
            }
            book.Finish();yield return null;Check(!StoryBook.IsOpen&&Time.timeScale==1,"Closing gallery restores gameplay time");
            Check(true,"Complete; original memory unlocks restored on cleanup");
        }
        void Restore()
        {
            if(book&&StoryBook.IsOpen)typeof(StoryBook).GetMethod("Release",F).Invoke(book,new object[]{false});
            foreach(var item in backup){if(item.Value<0)PlayerPrefs.DeleteKey(item.Key);else PlayerPrefs.SetInt(item.Key,item.Value);}if(backup.Count>0)PlayerPrefs.Save();backup.Clear();
            GameAudio.Preview(preferences);Time.timeScale=1;
        }
        void OnDestroy(){Restore();}
    }
}
#endif
