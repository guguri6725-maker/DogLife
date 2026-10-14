using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DogLife
{
    [System.Serializable]
    public struct AudioPreferences
    {
        public float master, music, effects;
        public static AudioPreferences Defaults => new AudioPreferences { master=.8f, music=.65f, effects=.8f };
    }

    // All game effects, including environment and UI, share the effects volume.
    public sealed class GameAudio : MonoBehaviour
    {
        public struct Handle { internal int index, serial; public void Stop(){if(instance)instance.Stop(this);} }
        sealed class Voice
        {
            public AudioSource source; public Transform follow; public bool tracking, active, ui, paused;
            public float gain; public int serial;
        }
        const string Key="DogLife.AudioSettings";
        static GameAudio instance;
        readonly Dictionary<string,AudioClip> clips=new Dictionary<string,AudioClip>();
        readonly Dictionary<string,float> cooldowns=new Dictionary<string,float>();
        readonly Dictionary<string,int> variations=new Dictionary<string,int>();
        readonly Voice[] voices=new Voice[24];
        AudioSource musicSource,musicBlendSource;
        AudioClip musicWanted;Object musicOwner;
        float musicEnvelope,musicBlend;
        bool blendingMusic;
        const float MusicLevel=.55f,MusicFadeIn=.8f,MusicFadeOut=1.5f,MusicLoopOverlap=1.25f;
        int serial;
#if UNITY_EDITOR
        public static event System.Action<string> EffectStarted;
#endif
        public static AudioPreferences Current {get;private set;}
        public static AudioPreferences Saved {get;private set;}
        public static bool SuppressSeasonBackground => (instance&&(instance.musicWanted!=null||instance.musicEnvelope>0))||(StoryBook.IsOpen&&!StoryBook.Instance.GalleryOpen)||PlayerDeathSequence.IsActive;
        public static int ActiveVoices {get {int n=0;if(instance)foreach(var v in instance.voices)if(v.active)n++;return n;}}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset(){instance=null;Current=Saved=AudioPreferences.Defaults;
#if UNITY_EDITOR
            EffectStarted=null;
#endif
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            if(instance)return;
            var config=AudioSettings.GetConfiguration();
            if(config.dspBufferSize>512){config.dspBufferSize=512;AudioSettings.Reset(config);}
            new GameObject("Game Audio").AddComponent<GameAudio>();
        }
        void Awake()
        {
            if(instance){Destroy(gameObject);return;}instance=this;DontDestroyOnLoad(gameObject);
            var p=AudioPreferences.Defaults;
            if(PlayerPrefs.HasKey(Key)){try{p=JsonUtility.FromJson<AudioPreferences>(PlayerPrefs.GetString(Key));}catch{}}
            Preview(p);Saved=Current;
            foreach(var clip in Resources.LoadAll<AudioClip>("Audio/Player"))clips[clip.name]=clip;
            foreach(var clip in Resources.LoadAll<AudioClip>("Audio/UI"))clips[clip.name]=clip;
            for(int i=0;i<voices.Length;i++)
            {
                var go=new GameObject("Effect "+i);go.transform.SetParent(transform);
                var s=go.AddComponent<AudioSource>();s.playOnAwake=false;s.dopplerLevel=0;s.minDistance=3;s.maxDistance=35;s.rolloffMode=AudioRolloffMode.Linear;
                voices[i]=new Voice{source=s};
            }
            musicSource=gameObject.AddComponent<AudioSource>();musicSource.playOnAwake=false;musicSource.loop=false;musicSource.priority=64;
            musicBlendSource=gameObject.AddComponent<AudioSource>();musicBlendSource.playOnAwake=false;musicBlendSource.loop=false;musicBlendSource.priority=64;
            SceneManager.activeSceneChanged+=SceneChanged;
            gameObject.AddComponent<SpringSoundscape>();
            gameObject.AddComponent<WinterSoundscape>();
            gameObject.AddComponent<SummerSoundscape>();
            gameObject.AddComponent<FallSoundscape>();
        }
        void SceneChanged(Scene a,Scene b){foreach(var v in voices){v.source.Stop();v.active=false;}cooldowns.Clear();SetMusic(null,null);}
        void OnDestroy(){if(instance==this){SceneManager.activeSceneChanged-=SceneChanged;instance=null;}}
        static float Gain(float value)=>Mathf.Pow(Mathf.Clamp01(value),1.5f);
        public static void Preview(AudioPreferences p)
        {p.master=Mathf.Clamp01(p.master);p.music=Mathf.Clamp01(p.music);p.effects=Mathf.Clamp01(p.effects);Current=p;if(instance)instance.ApplyVolumes();}
        public static void Commit(){Saved=Current;PlayerPrefs.SetString(Key,JsonUtility.ToJson(Saved));PlayerPrefs.Save();}
        public static void Revert()=>Preview(Saved);
        void ApplyVolumes()
        {
            foreach(var v in voices)if(v!=null)v.source.volume=v.gain*Gain(Current.master)*Gain(Current.effects);
            float music=MusicLevel*musicEnvelope*Gain(Current.master)*Gain(Current.music);
            if(musicSource)musicSource.volume=music*(blendingMusic?Mathf.Cos(musicBlend*Mathf.PI*.5f):1);
            if(musicBlendSource)musicBlendSource.volume=music*(blendingMusic?Mathf.Sin(musicBlend*Mathf.PI*.5f):0);
        }
        public static void PlayMusic(AudioClip clip,Object owner=null)
        {if(!instance)Initialize();instance.SetMusic(clip,owner);}
        public static void StopMusic(Object owner)
        {if(instance&&instance.musicOwner==owner)instance.SetMusic(null,null);}
        void SetMusic(AudioClip clip,Object owner)
        {
            if(clip==musicWanted){if(clip)musicOwner=owner;return;}
            musicWanted=clip;musicOwner=owner;
            if(!clip)return;
            musicSource.Stop();musicBlendSource.Stop();blendingMusic=false;musicBlend=0;musicEnvelope=0;
            musicSource.clip=clip;musicBlendSource.clip=clip;ApplyVolumes();musicSource.Play();
        }
        void TickMusic()
        {
            // Music keeps progressing while gameplay is paused by any menu.
            float dt=Time.unscaledDeltaTime;
            musicEnvelope=Mathf.MoveTowards(musicEnvelope,musicWanted?1:0,dt/(musicWanted?MusicFadeIn:MusicFadeOut));
            if(!musicWanted&&musicEnvelope<=0)
            {musicSource.Stop();musicBlendSource.Stop();musicSource.clip=musicBlendSource.clip=null;blendingMusic=false;ApplyVolumes();return;}
            var clip=musicSource.clip;
            float overlap=clip?Mathf.Min(MusicLoopOverlap,clip.length*.1f):MusicLoopOverlap;
            if(musicWanted&&!blendingMusic&&clip&&musicSource.time>=clip.length-overlap)
            {musicBlendSource.clip=clip;musicBlendSource.time=0;musicBlendSource.Play();musicBlend=0;blendingMusic=true;}
            if(blendingMusic)
            {
                musicBlend=Mathf.Min(1,musicBlend+dt/Mathf.Max(.01f,overlap));
                if(musicBlend>=1){musicSource.Stop();var old=musicSource;musicSource=musicBlendSource;musicBlendSource=old;blendingMusic=false;musicBlend=0;}
            }
            ApplyVolumes();
        }
        public static Handle Play(string key,Vector3 position,float gain=1,float pitch=1,bool ui=false,Transform follow=null,bool loop=false,float minimumInterval=.025f,float spatialRange=35)
        {
            if(!Application.isPlaying)return default;
            if(!instance)Initialize();
            return instance.StartVoice(key,position,gain,pitch,ui,follow,loop,minimumInterval,spatialRange);
        }
        Handle StartVoice(string key,Vector3 position,float gain,float pitch,bool ui,Transform follow,bool loop,float interval,float spatialRange)
        {
            // A frozen animation/event must not enqueue world effects behind an open menu.
            if(!ui&&Time.timeScale<=0)return default;
            if(string.IsNullOrEmpty(key))return default;
            if(!clips.TryGetValue(key,out var clip))
            {
                clip=Resources.Load<AudioClip>("Audio/Spring/"+key);
                if(!clip)clip=Resources.Load<AudioClip>("Audio/Winter/"+key);
                if(!clip)clip=Resources.Load<AudioClip>("Audio/Summer/"+key);
                if(!clip)clip=Resources.Load<AudioClip>("Audio/Fall/"+key);
                if(!clip)return default;
                clips[key]=clip;
            }
            if(cooldowns.TryGetValue(key,out float next)&&Time.unscaledTime<next)return default;
            int chosen=-1;int oldest=int.MaxValue;
            for(int i=0;i<voices.Length;i++){var v=voices[i];if(!v.active){chosen=i;break;}if(!v.source.loop&&v.serial<oldest){chosen=i;oldest=v.serial;}}
            if(chosen<0)return default;
            cooldowns[key]=Time.unscaledTime+interval;
            var voice=voices[chosen];var s=voice.source;s.Stop();voice.active=true;voice.paused=false;voice.ui=ui;voice.gain=gain;voice.follow=follow;voice.tracking=follow;voice.serial=++serial;
            s.transform.position=position;s.clip=clip;s.pitch=Mathf.Clamp(pitch,.65f,1.5f);s.loop=loop;s.spatialBlend=ui?0:1;s.priority=ui?80:128;
            s.maxDistance=Mathf.Max(.2f,spatialRange);s.minDistance=Mathf.Min(3,s.maxDistance*.2f);
            s.volume=gain*Gain(Current.master)*Gain(Current.effects);s.Play();
#if UNITY_EDITOR
            EffectStarted?.Invoke(key);
#endif
            return new Handle{index=chosen,serial=voice.serial};
        }
        void Stop(Handle h){if(h.serial==0||h.index<0||h.index>=voices.Length)return;var v=voices[h.index];if(v.serial!=h.serial)return;v.source.Stop();v.active=false;}
        public static void EnterEconomyMenu()
        {
            if(!instance)return;
            foreach(var v in instance.voices)
            {
                if(!v.active)continue;
                // Short combat sounds are obsolete once the menu opens. Ambient loops may resume.
                if(!v.ui&&!v.source.loop || v.source.clip&&v.source.clip.name=="Synthesis_Loop")
                {v.source.Stop();v.active=false;v.paused=false;}
            }
        }
        void Update()
        {
            TickMusic();
            foreach(var v in voices)
            {
                if(!v.active)continue;
                if(v.tracking&&(!v.follow||!v.follow.gameObject.activeInHierarchy)){v.source.Stop();v.active=false;continue;}
                if(v.follow)v.source.transform.position=v.follow.position;
                bool pause=!v.ui&&Time.timeScale==0;
                if(pause&&!v.paused){v.source.Pause();v.paused=true;}
                else if(!pause&&v.paused){v.source.UnPause();v.paused=false;}
                if(!v.paused&&!v.source.isPlaying)v.active=false;
            }
        }
        public static void Variant(string prefix,Vector3 position,float volume)
        {
            if(!instance)Initialize();instance.variations.TryGetValue(prefix,out int last);
            int choice=last==0?Random.Range(1,4):((last-1+Random.Range(1,3))%3)+1;instance.variations[prefix]=choice;
            Play(prefix+choice,position,volume,Random.Range(.96f,1.04f));
        }
        public static float WeaponPitch(InventoryWeapon item)=>item&&item.boneOrigin==InventoryWeapon.BoneOrigin.Humanoid?.92f:1.05f;
        public static void WeaponAttack(InventoryWeapon item,int step,Vector3 position)
        {
            if(!item)return;string category=item.weaponCategory.ToString();string suffix="Attack";
            if(step==2&&(category=="Sword"||category=="Dagger"))suffix="Final";
            if(step==2&&category=="Spear")suffix="Thrust";
            Play(category+suffix,position,category=="Spear"&&step==2?.5f:.85f,WeaponPitch(item)*Random.Range(.98f,1.02f));
        }
        public static void WeaponHit(InventoryWeapon item,Vector3 position)
        {if(item)Play(item.weaponCategory+"Hit",position,.8f,WeaponPitch(item)*Random.Range(.97f,1.03f),minimumInterval:.06f);}
    }
}
