using System.Collections.Generic;
using UnityEngine;
namespace DogLife
{
    // Own transient cues so interruption/scene unload cannot leave attack loops behind.
    public sealed class SummerAudioEmitter : MonoBehaviour
    {
        [Range(0,1)] public float volume=.8f;
        readonly List<GameAudio.Handle> cues=new List<GameAudio.Handle>();
        GameAudio.Handle preparation,sustain;
        public static SummerAudioEmitter Get(Component owner)
        {var e=owner.GetComponent<SummerAudioEmitter>();return e?e:owner.gameObject.AddComponent<SummerAudioEmitter>();}
        public void Cue(string key,float gain=1)
        {
            if(cues.Count>=64)cues.RemoveAt(0);
            cues.Add(GameAudio.Play(key,transform.position,volume*gain,follow:transform,minimumInterval:.04f));
        }
        public void Prepare(string key,float gain=.8f)
        {StopPreparation();preparation=GameAudio.Play(key,transform.position,volume*gain,follow:transform);}
        public void StopPreparation(){preparation.Stop();}
        public void Sustain(string key,float gain=.4f)
        {StopSustain();sustain=GameAudio.Play(key,transform.position,volume*gain,follow:transform,loop:true);}
        public void StopSustain(){sustain.Stop();}
        public void StopAll(){StopPreparation();StopSustain();foreach(var h in cues)h.Stop();cues.Clear();}
        void OnDisable(){StopAll();}
    }
}
