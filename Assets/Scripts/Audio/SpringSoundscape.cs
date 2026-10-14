using UnityEngine;
using UnityEngine.SceneManagement;

namespace DogLife
{
    // One stereo bed per game, independent of additive room streaming and voice stealing.
    public sealed class SpringSoundscape : MonoBehaviour
    {
        AudioSource source;
        bool wanted;
        float fade;
        public static bool IsSpringScene(Scene scene)=>scene.name=="Stage1_Spring"||scene.path.Contains("/SeasonRooms/Spring/");
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
            wanted=SceneManager.GetSceneByName("Stage1_Spring").isLoaded;
            if(wanted&&!source.clip)source.clip=Resources.Load<AudioClip>("Audio/Spring/Spring_Ambience");
            if(wanted&&source.clip&&!source.isPlaying&&!GameAudio.SuppressSeasonBackground)source.Play();
        }
        void Update()
        {
            if(GameAudio.SuppressSeasonBackground)
            {source.Stop();source.volume=0;fade=0;if(!wanted)source.clip=null;return;}
            if(wanted&&source.clip&&!source.isPlaying)source.Play();
            fade=Mathf.MoveTowards(fade,wanted?1:0,Time.unscaledDeltaTime/1.25f);
            var p=GameAudio.Current;
            source.volume=.65f*fade*Mathf.Pow(p.master,1.5f)*Mathf.Pow(p.effects,1.5f);
            if(!wanted&&fade<=0){source.Stop();source.clip=null;}
        }
        void OnDestroy()
        {SceneManager.sceneLoaded-=Loaded;SceneManager.sceneUnloaded-=Unloaded;SceneManager.activeSceneChanged-=Changed;}
    }
}
