using UnityEngine;
using UnityEngine.SceneManagement;

namespace DogLife
{
    // One stereo bed per game, independent of additive room streaming and voice stealing.
    public sealed class FallSoundscape : MonoBehaviour
    {
        AudioSource source;
        bool wanted,paused;
        float fade,nextAccent;
        GameAudio.Handle accent;
        public static bool IsFallScene(Scene scene)=>scene.name=="Stage3_Fall"||scene.path.Contains("/SeasonRooms/AutumnJourney9/");
        void Awake()
        {
            source=gameObject.AddComponent<AudioSource>();source.playOnAwake=false;
            source.loop=true;source.spatialBlend=0;source.priority=200;source.volume=0;
            SceneManager.sceneLoaded+=Loaded;SceneManager.sceneUnloaded+=Unloaded;
            SceneManager.activeSceneChanged+=Changed;Check();
        }
        void Loaded(Scene scene,LoadSceneMode mode)=>Check();
        void Unloaded(Scene scene)=>Check();
        void Changed(Scene before,Scene after)=>Check();
        void Check()
        {
            // Main scene owns the soundscape, so preloaded rooms cannot start it early.
            wanted=SceneManager.GetSceneByName("Stage3_Fall").isLoaded;
            if(wanted&&!source.clip)source.clip=Resources.Load<AudioClip>("Audio/Fall/Autumn_Amb_Forest_Loop");
            if(wanted&&source.clip&&!source.isPlaying&&!paused)source.Play();
        }
        void Update()
        {
            bool pause=Time.timeScale==0;
            if(pause&&!paused){source.Pause();paused=true;}
            else if(!pause&&paused){source.UnPause();paused=false;}
            fade=Mathf.MoveTowards(fade,wanted?1:0,Time.unscaledDeltaTime/1.25f);
            var p=GameAudio.Current;
            source.volume=.65f*fade*Mathf.Pow(p.master,1.5f)*Mathf.Pow(p.effects,1.5f);
            if(wanted&&!pause&&Time.time>=nextAccent){nextAccent=Time.time+Random.Range(16f,28f);var camera=Camera.main;if(camera)accent=GameAudio.Play(Random.value<.7f?"Autumn_Leaf_Gust":"Autumn_Wood_Creak",camera.transform.position+camera.transform.right*Random.Range(-5f,5f),.18f);}
            if(!wanted){accent.Stop();nextAccent=Time.time+12;}
            if(!wanted&&fade<=0){source.Stop();source.clip=null;paused=false;}
        }
        void OnDestroy()
        {SceneManager.sceneLoaded-=Loaded;SceneManager.sceneUnloaded-=Unloaded;SceneManager.activeSceneChanged-=Changed;}
    }
}
