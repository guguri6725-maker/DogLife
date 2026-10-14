#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace DogLife
{
    // Isolated season shells exercise the real persistent soundscape and music controllers.
    public sealed class MusicHandoffValidation:MonoBehaviour
    {
        const BindingFlags F=BindingFlags.NonPublic|BindingFlags.Instance;
        readonly List<string> report=new List<string>();GameAudio audio;AudioPreferences preferences;
        AudioSource Music=>(AudioSource)typeof(GameAudio).GetField("musicSource",F).GetValue(audio);
        AudioSource Blend=>(AudioSource)typeof(GameAudio).GetField("musicBlendSource",F).GetValue(audio);
        AudioSource[] Beds()=>audio.GetComponents<AudioSource>().Where(s=>s.clip&&(s.clip.name=="Spring_Ambience"||s.clip.name.StartsWith("Summer_Amb_")||s.clip.name=="Autumn_Amb_Forest_Loop"||s.clip.name=="Winter_Amb_Wind_Loop")).ToArray();
        void Check(bool ok,string name){report.Add((ok?"PASS ":"FAIL ")+name);System.IO.Directory.CreateDirectory("Builds/AudioValidation");System.IO.File.WriteAllLines("Builds/AudioValidation/music-handoff-checks.txt",report);}
        IEnumerator Start()
        {
            yield return null;audio=FindFirstObjectByType<GameAudio>();preferences=GameAudio.Current;GameAudio.Preview(AudioPreferences.Defaults);
            GameAudio.PlayMusic(null);yield return new WaitForSecondsRealtime(1.8f);
            string[] scenes={"Stage1_Spring","Stage3_Summer","Stage3_Fall","Stage4_Winter"},seasons={"Spring","Summer","Autumn","Winter"};
            for(int i=0;i<4;i++)
            {
                var scene=SceneManager.CreateScene(scenes[i]);SceneManager.SetActiveScene(scene);yield return new WaitForSecondsRealtime(1.8f);
                var beds=Beds();Check(beds.Any(s=>s.isPlaying&&s.volume>0),seasons[i]+" exploration background starts");
                float bedTime=beds.First(s=>s.isPlaying).time;Time.timeScale=0;yield return new WaitForSecondsRealtime(.2f);
                Check(beds.Any(s=>s.isPlaying&&s.time>bedTime),seasons[i]+" exploration background continues through UI pause");Time.timeScale=1;
                var clip=Resources.Load<AudioClip>("Audio/Music/"+seasons[i]+"_Boss_BGM");GameAudio.PlayMusic(clip,this);yield return new WaitForSecondsRealtime(1);
                Check(Music.isPlaying&&Music.clip==clip&&beds.All(s=>!s.isPlaying),seasons[i]+" boss stops all old background beds");
                float time=Music.time;GameAudio.PlayMusic(clip,this);yield return new WaitForSecondsRealtime(.15f);
                Check(Music.time>=time,seasons[i]+" same track phase handoff does not restart");
                Time.timeScale=0;time=Music.time;yield return new WaitForSecondsRealtime(.25f);Check(Music.isPlaying&&Music.time>time,seasons[i]+" boss BGM continues under modal UI");Time.timeScale=1;
                Music.time=clip.length-1.1f;yield return new WaitForSecondsRealtime(.15f);
                Check(Blend.isPlaying,"Loop overlap prepared for "+seasons[i]);
                var next=Resources.Load<AudioClip>("Audio/Music/"+seasons[(i+1)%4]+"_Boss_BGM");GameAudio.PlayMusic(next,this);yield return null;
                Check(Music.isPlaying&&Music.clip==next&&!Blend.isPlaying&&!audio.GetComponents<AudioSource>().Any(s=>s.clip==clip&&s.isPlaying),seasons[i]+" replacement stops both previous loop voices");
                GameAudio.StopMusic(this);yield return new WaitForSecondsRealtime(1.8f);
                Check(!Music.isPlaying&&!Blend.isPlaying&&beds.Any(s=>s.isPlaying),seasons[i]+" encounter end restores background without overlap");
                yield return SceneManager.UnloadSceneAsync(scene);
            }
            var title=SceneManager.GetSceneByName("Title");SceneManager.SetActiveScene(title);FindFirstObjectByType<TitleScreen>().ResumeMenuMusic();yield return new WaitForSecondsRealtime(1);
            var settings=GameSettingsWindow.Instance;settings.Open();yield return new WaitForSecondsRealtime(.2f);
            Check(GameSettingsWindow.IsOpen&&Music.isPlaying,"Actual settings window keeps BGM playing");settings.Close();yield return null;
            Check(Music.isPlaying,"Closing settings does not restart or stop music");
            GameAudio.Preview(preferences);Check(true,"Complete; no run saves changed");
        }
        void OnDestroy(){Time.timeScale=1;GameAudio.Preview(preferences);}
    }
}
#endif
