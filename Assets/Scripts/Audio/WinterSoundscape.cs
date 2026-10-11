using UnityEngine;
using UnityEngine.SceneManagement;
namespace DogLife
{
    public sealed class WinterSoundscape:MonoBehaviour
    {
        AudioSource wind;float fade,nextCreak;bool paused;
        public static bool IsWinter(Scene scene)=>scene.name=="Stage4_Winter"||scene.path.Contains("/SeasonRooms/Winter/");
        void Awake(){wind=gameObject.AddComponent<AudioSource>();wind.playOnAwake=false;wind.loop=true;wind.spatialBlend=0;wind.volume=0;wind.priority=200;nextCreak=Time.time+18;}
        void Update()
        {
            bool wanted=SceneManager.GetSceneByName("Stage4_Winter").isLoaded;
            if(wanted&&!wind.clip)wind.clip=Resources.Load<AudioClip>("Audio/Winter/Winter_Amb_Wind_Loop");
            bool pause=Time.timeScale==0;
            if(pause&&!paused)wind.Pause();else if(!pause&&paused)wind.UnPause();paused=pause;
            if(wanted&&!paused&&wind.clip&&!wind.isPlaying)wind.Play();
            fade=Mathf.MoveTowards(fade,wanted?1:0,Time.unscaledDeltaTime/1.5f);
            var p=GameAudio.Current;wind.volume=.28f*fade*Mathf.Pow(p.master,1.5f)*Mathf.Pow(p.effects,1.5f);
            if(wanted&&!pause&&Time.time>=nextCreak){nextCreak=Time.time+Random.Range(22,38);var listener=FindFirstObjectByType<AudioListener>();if(listener)GameAudio.Play("Winter_Amb_IceCreak",listener.transform.position+Random.insideUnitSphere*8,.18f);}
            if(!wanted&&fade<=0){wind.Stop();wind.clip=null;nextCreak=Time.time+18;}
        }
    }
}
