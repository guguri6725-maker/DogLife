using UnityEngine;
namespace DogLife
{
    public sealed class HarvestReaperAudio : MonoBehaviour
    {
        [Range(0,1)] public float attackVolume=.82f, stormVolume=.32f, curseVolume=.2f;
        HarvestReaperBoss boss;CombatHealth health;SummerAudioEmitter voice;
        GameAudio.Handle curse;bool stormPlaying,cursePlaying;
        readonly System.Collections.Generic.List<GameAudio.Handle> patchCues=new System.Collections.Generic.List<GameAudio.Handle>();
        void Awake(){boss=GetComponent<HarvestReaperBoss>();health=GetComponent<CombatHealth>();voice=SummerAudioEmitter.Get(this);}
        void OnEnable(){health.Died+=Stop;}
        void OnDisable(){health.Died-=Stop;Stop();}
        public void Stop(){voice.StopAll();curse.Stop();foreach(var h in patchCues)h.Stop();patchCues.Clear();stormPlaying=cursePlaying=false;}
        public void Enter(HarvestReaperBoss.Phase state)
        {
            voice.volume=attackVolume;voice.StopPreparation();
            var pattern=boss.CurrentPattern;
            if(state==HarvestReaperBoss.Phase.Warning)
            {
                string key=pattern==HarvestReaperBoss.Pattern.Sweep?"Reaper_Scythe_Ready":pattern==HarvestReaperBoss.Pattern.Cleave||pattern==HarvestReaperBoss.Pattern.Shockwave?"Reaper_Scythe_Raise":pattern==HarvestReaperBoss.Pattern.SafeWedge&&!boss.Gathering?"Reaper_Sector_Warning":"Reaper_Special_Charge";
                voice.Prepare(key);
            }
            if(state==HarvestReaperBoss.Phase.Active)
            {
                if(pattern==HarvestReaperBoss.Pattern.Pumpkins)voice.Cue("Reaper_Pumpkin_Summon");
                if(pattern==HarvestReaperBoss.Pattern.Storm||pattern==HarvestReaperBoss.Pattern.SafeWedge&&boss.Gathering)
                {voice.Cue("Reaper_Ground_Plant",.7f);voice.Cue("Reaper_LeafStorm_Start");voice.Sustain("Reaper_LeafStorm_Loop",stormVolume);stormPlaying=true;}
            }
            if(state==HarvestReaperBoss.Phase.Recovery&&stormPlaying)
            {voice.StopSustain();voice.Cue("Reaper_LeafStorm_End",.65f);stormPlaying=false;}
        }
        public void Impact()
        {
            var p=boss.CurrentPattern;
            if(p==HarvestReaperBoss.Pattern.Shockwave)voice.Cue("Reaper_Ground_Plant",.65f);
            voice.Cue(p==HarvestReaperBoss.Pattern.Sweep?"Reaper_Scythe_Sweep":p==HarvestReaperBoss.Pattern.Cleave?"Reaper_Scythe_Cleave":p==HarvestReaperBoss.Pattern.Shockwave?"Reaper_Shockwave":"Reaper_Sector_Explosion");
        }
        void PatchCue(string key,Vector3 point,float volume)
        {if(patchCues.Count>=64)patchCues.RemoveAt(0);patchCues.Add(GameAudio.Play(key,point,volume,minimumInterval:.06f));}
        public void PumpkinWarning(Vector3 point){PatchCue("Reaper_Pumpkin_Warning",point,.5f);}
        public void PumpkinExplosion(Vector3 point){PatchCue("Reaper_Pumpkin_Explosion",point,attackVolume);}
        void LateUpdate()
        {
            if(!boss.enabled||health.Dead)return;
            bool active=false;foreach(var p in boss.Patches)if(p.exploded){active=true;break;}
            // One bed for all independent pools; each pool still keeps its own damage/lifetime.
            if(active&&!cursePlaying)curse=GameAudio.Play("Reaper_Curse_Field_Loop",transform.position,curseVolume,follow:transform,loop:true);
            if(!active&&cursePlaying){curse.Stop();voice.Cue("Reaper_Curse_Field_End",.45f);}
            cursePlaying=active;
        }
    }
}
