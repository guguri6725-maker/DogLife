#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
namespace DogLife
{
    // Explicit opt-in fixture; tests the room music lifecycle without loading a saved run.
    public sealed class BossMusicValidation:MonoBehaviour
    {
        const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
        readonly List<string> report=new List<string>();GameAudio audio;
        AudioSource Source=>(AudioSource)typeof(GameAudio).GetField("musicSource",F).GetValue(audio);
        AudioSource Blend=>(AudioSource)typeof(GameAudio).GetField("musicBlendSource",F).GetValue(audio);
        void Check(bool ok,string label){report.Add((ok?"PASS ":"FAIL ")+label);System.IO.Directory.CreateDirectory("Builds/AudioValidation");System.IO.File.WriteAllLines("Builds/AudioValidation/boss-music-checks.txt",report);}
        IEnumerator Start()
        {
            audio=FindFirstObjectByType<GameAudio>();var preferences=GameAudio.Current;
            GameAudio.PlayMusic(null);yield return new WaitForSecondsRealtime(1.8f);
            foreach(var season in new[]{"Spring","Summer","Autumn","Winter"})
            {
                var clip=Resources.Load<AudioClip>("Audio/Music/"+season+"_Boss_BGM");
                Check(clip&&clip.channels==2&&clip.loadType==AudioClipLoadType.Streaming,season+" stereo streaming import");
                var go=new GameObject("Music validation boss");var hp=go.AddComponent<CombatHealth>();hp.practiceTarget=true;hp.maxHealth=100;hp.Restore();
                var music=go.AddComponent<BossBattleMusic>();music.Prepare(hp,season+"_Boss_BGM");yield return new WaitForSeconds(.2f);
                Check(!Source.isPlaying,season+" preload does not start battle music");
                music.Begin();yield return new WaitForSeconds(1.1f);
                Check(Source.clip==clip&&Source.isPlaying&&Source.volume>0,season+" battle begins with fade in");
                float position=Source.time;music.Begin();hp.Damage(1);yield return new WaitForSeconds(.15f);
                Check(Source.isPlaying&&Source.time>=position,season+" repeated begin and nonfatal damage do not restart music");
                var other=new GameObject("Preloaded unrelated room").AddComponent<BossBattleMusic>();other.Stop();Destroy(other.gameObject);
                Check(Source.isPlaying,season+" unrelated room cannot stop music");
                if(season=="Spring")
                {
                    Time.timeScale=0;yield return null;yield return null;Check(Source.isPlaying,"Menus keep BGM playing");Time.timeScale=1;yield return null;yield return null;Check(Source.isPlaying,"Resume continues BGM");
                    var p=GameAudio.Current;p.music=0;GameAudio.Preview(p);yield return null;Check(Source.volume==0&&Blend.volume==0,"BGM slider mutes both music voices");
                    p=preferences;p.effects=0;GameAudio.Preview(p);yield return null;Check(Source.volume>0,"Effects slider does not mute BGM");
                    p=preferences;p.master=0;GameAudio.Preview(p);yield return null;Check(Source.volume==0&&Blend.volume==0,"Master slider mutes music");GameAudio.Preview(preferences);
                }
                var before=Source;Source.time=clip.length-1.1f;yield return new WaitForSeconds(1.5f);
                Check(Source!=before&&Source.isPlaying&&Source.time<4,season+" loop crossfades into beginning");
                hp.Damage(10000);yield return new WaitForSeconds(1.8f);
                Check(!Source.isPlaying&&!Blend.isPlaying,season+" final death fades out music");
                Destroy(go);yield return null;
            }
            var owner=new GameObject("Room unload check").AddComponent<BossBattleMusic>();var health=owner.gameObject.AddComponent<CombatHealth>();health.Restore();owner.Prepare(health,"Winter_Boss_BGM");owner.Begin();yield return new WaitForSeconds(.5f);Destroy(owner.gameObject);yield return new WaitForSeconds(1.8f);
            Check(!Source.isPlaying&&!Blend.isPlaying,"Destroying encounter stops music");
            GameAudio.Preview(preferences);Check(true,"Complete; no run save modified");
        }
        void OnDestroy(){Time.timeScale=1;}
    }
}
#endif
