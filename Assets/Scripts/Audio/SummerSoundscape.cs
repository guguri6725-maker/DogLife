using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace DogLife
{
    // A single ambience bed survives additive rooms. Waterfall is the nearest visible water feature.
    public sealed class SummerSoundscape : MonoBehaviour
    {
        [Range(0,1)] public float coastVolume=.3f,underwaterVolume=.28f,waterfallVolume=.22f;
        AudioSource coast,underwater,waterfall;bool paused,geometryDirty=true;float fade,depthBlend,flowBlend,nextScan;
        readonly List<Renderer> flows=new List<Renderer>();KrakenWaterVolume flood;Camera view;
        AudioSource Source(bool spatial){var s=gameObject.AddComponent<AudioSource>();s.playOnAwake=false;s.loop=true;s.volume=0;s.priority=200;s.spatialBlend=spatial?1:0;s.dopplerLevel=0;return s;}
        void Awake()
        {
            coast=Source(false);underwater=Source(false);
            var go=new GameObject("Nearest summer waterfall audio");go.transform.SetParent(transform,false);waterfall=go.AddComponent<AudioSource>();
            waterfall.playOnAwake=false;waterfall.loop=true;waterfall.volume=0;waterfall.spatialBlend=1;waterfall.dopplerLevel=0;waterfall.minDistance=2;waterfall.maxDistance=14;waterfall.rolloffMode=AudioRolloffMode.Linear;waterfall.priority=210;
            SceneManager.sceneLoaded+=Loaded;SceneManager.sceneUnloaded+=Unloaded;
        }
        void Loaded(Scene s,LoadSceneMode m){nextScan=0;geometryDirty=true;}
        void Unloaded(Scene s){nextScan=0;geometryDirty=true;flows.RemoveAll(r=>!r);}
        void Scan()
        {
            flows.Clear();flood=FindFirstObjectByType<KrakenWaterVolume>();view=Camera.main;
            foreach(var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            // 통일된 보스 수면 셰이더에서도 수직 폭포의 환경음을 유지합니다.
            {foreach(var m in r.sharedMaterials)if(m&&m.shader&&(m.shader.name=="DogLife/Original Coastal Waterfall"||(m.HasProperty("_EnvironmentMode")&&Mathf.Approximately(m.GetFloat("_EnvironmentMode"),2)))){flows.Add(r);break;}}
        }
        void Ensure(AudioSource s,string key){if(!s.clip)s.clip=Resources.Load<AudioClip>("Audio/Summer/"+key);if(s.clip&&!s.isPlaying&&(s!=waterfall||!paused)&&(!GameAudio.SuppressSeasonBackground||s==waterfall))s.Play();}
        void Update()
        {
            bool wanted=SceneManager.GetSceneByName("Stage3_Summer").isLoaded;
            bool pause=Time.timeScale<=0;
            if(pause!=paused){if(pause)waterfall.Pause();else waterfall.UnPause();paused=pause;}
            bool suppress=GameAudio.SuppressSeasonBackground;
            if(suppress){coast.Stop();underwater.Stop();}
            if(wanted)
            {
                if(Time.unscaledTime>=nextScan){nextScan=Time.unscaledTime+2;if(geometryDirty){Scan();geometryDirty=false;}else{if(!flood)flood=FindFirstObjectByType<KrakenWaterVolume>();if(!view)view=Camera.main;}}
                Ensure(coast,"Summer_Amb_Coast_Loop");Ensure(underwater,"Summer_Amb_Underwater_Loop");Ensure(waterfall,"Summer_Waterfall_Loop");
            }
            fade=Mathf.MoveTowards(fade,wanted?1:0,Time.unscaledDeltaTime/1.5f);
            bool below=wanted&&flood&&flood.IsRaised&&view&&view.transform.position.y<flood.Height;
            depthBlend=Mathf.MoveTowards(depthBlend,below?1:0,Time.unscaledDeltaTime);
            float nearest=14;Renderer chosen=null;
            if(wanted&&view)foreach(var r in flows)if(r&&r.enabled&&!r.forceRenderingOff&&r.gameObject.activeInHierarchy){float d=Vector3.Distance(view.transform.position,r.bounds.ClosestPoint(view.transform.position));if(d<nearest){nearest=d;chosen=r;}}
            float visibility=chosen?1:0;
            if(chosen){waterfall.transform.position=chosen.bounds.center;if(flood&&chosen.GetComponentInParent<ForestRoom>()==flood.GetComponentInParent<ForestRoom>())visibility*=flood.FlowVisibility;}
            flowBlend=Mathf.MoveTowards(flowBlend,visibility,Time.unscaledDeltaTime);
            var p=GameAudio.Current;float gain=fade*Mathf.Pow(p.master,1.5f)*Mathf.Pow(p.effects,1.5f);
            coast.volume=coastVolume*gain*(1-depthBlend)*(suppress?0:1);underwater.volume=underwaterVolume*gain*depthBlend*(suppress?0:1);waterfall.volume=waterfallVolume*gain*flowBlend*(1-depthBlend);
            if(!wanted&&fade<=0){foreach(var s in new[]{coast,underwater,waterfall}){s.Stop();s.clip=null;}flows.Clear();flood=null;}
        }
        void OnDestroy(){SceneManager.sceneLoaded-=Loaded;SceneManager.sceneUnloaded-=Unloaded;}
    }
}
