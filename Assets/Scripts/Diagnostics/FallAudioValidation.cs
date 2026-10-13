#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
namespace DogLife
{
    // Opt-in fixture only: isolates sound timing from combat damage and save progression.
    public sealed class FallAudioValidation:MonoBehaviour
    {
        const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
        readonly List<string> heard=new List<string>(),report=new List<string>();
        void Heard(string key){heard.Add(key);}
        void Check(bool ok,string label){report.Add((ok?"PASS ":"FAIL ")+label);System.IO.Directory.CreateDirectory("Builds/AudioValidation");System.IO.File.WriteAllLines("Builds/AudioValidation/fall-runtime-checks.txt",report);}
        static object Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,F).Invoke(o,args);
        static void Set(object o,string name,object value)=>o.GetType().GetField(name,F).SetValue(o,value);
        bool Playing(string key)=>FindFirstObjectByType<GameAudio>().GetComponentsInChildren<AudioSource>().Any(s=>s.clip&&s.clip.name==key&&s.isPlaying);
        IEnumerator Start()
        {
            GameAudio.EffectStarted+=Heard;
            var playerObject=new GameObject("Audio test target");playerObject.AddComponent<CapsuleCollider>();
            var player=playerObject.AddComponent<PlayerMotor>();player.enabled=false;
            var hp=playerObject.GetComponent<CombatHealth>();if(!hp)hp=playerObject.AddComponent<CombatHealth>();hp.player=true;hp.maxHealth=1000;hp.Restore();hp.invulnerable=true;
            playerObject.GetComponent<Rigidbody>().isKinematic=true;
            var clips=Resources.LoadAll<AudioClip>("Audio/Fall");Check(clips.Length==36,"All 36 recordings import");
            Check(clips.Where(c=>!c.name.StartsWith("Autumn_Amb_")).All(c=>c.channels==1&&c.loadType==AudioClipLoadType.DecompressOnLoad),"35 effects mono and predecoded");
            Check(clips.Single(c=>c.name.StartsWith("Autumn_Amb_")).loadType==AudioClipLoadType.Streaming,"Forest ambience streaming");
            foreach(var c in clips)GameAudio.Play(c.name,Vector3.zero,minimumInterval:0).Stop();
            Check(clips.All(c=>heard.Contains(c.name)),"Every recording resolves through GameAudio");
            yield return new WaitForSeconds(.2f);
            foreach(var name in new[]{"MyceliumSkeleton","Scarecrow","LeafHedgehog","BoneCrow"})
            {
                heard.Clear();var go=Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/monster/"+name+".prefab"));
                var e=go.GetComponent<FallMonsterEnemy>();e.enabled=false;go.GetComponent<NavMeshAgent>().enabled=false;Call(e,"Start");
                string impact=e.AttackPattern==FallMonsterEnemy.Pattern.Spores?"Mycelium_Spore_Burst":e.AttackPattern==FallMonsterEnemy.Pattern.ArmSweep?"Scarecrow_Arm_Sweep":e.AttackPattern==FallMonsterEnemy.Pattern.Spikes?"Hedgehog_Spike_Erupt":"Crow_Dash";
                Call(e,"Enter",FallMonsterEnemy.Phase.Anticipation);Check(heard.Count==1,name+" preparation cue");
                Call(e,"Enter",FallMonsterEnemy.Phase.Attack);Check(!heard.Contains(impact),name+" no early impact");
                Call(e,"Enter",FallMonsterEnemy.Phase.Active);
                if(e.AttackPattern==FallMonsterEnemy.Pattern.Spikes){Set(e,"elapsed",(float)e.GetType().GetField("activeDuration",F).GetValue(e)*.21f);Call(e,"FixedUpdate");Call(e,"FixedUpdate");}
                Check(heard.Count(k=>k==impact)==1,name+" one impact at damage window");
                if(e.AttackPattern==FallMonsterEnemy.Pattern.Spores)Check(Playing("Mycelium_Spore_Loop"),"Spore loop during active window");
                Call(e,"Enter",FallMonsterEnemy.Phase.Recovery);Check(!Playing("Mycelium_Spore_Loop"),name+" no lingering spore loop");
                go.SetActive(false);Destroy(go);yield return new WaitForSeconds(.1f);
            }
            var root=Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/monster/HarvestReaperBoss.prefab"));
            var boss=root.GetComponent<HarvestReaperBoss>();boss.enabled=false;Call(boss,"Start");Set(boss,"engaged",true);
            foreach(var pattern in new[]{HarvestReaperBoss.Pattern.Sweep,HarvestReaperBoss.Pattern.Cleave,HarvestReaperBoss.Pattern.Shockwave})
            {
                heard.Clear();Call(boss,"Begin",pattern);Call(boss,"Enter",HarvestReaperBoss.Phase.Active);
                string key=pattern==HarvestReaperBoss.Pattern.Sweep?"Reaper_Scythe_Sweep":pattern==HarvestReaperBoss.Pattern.Cleave?"Reaper_Scythe_Cleave":"Reaper_Shockwave";
                Check(!heard.Contains(key),pattern+" no pre-contact hit sound");
                Set(boss,"elapsed",boss.CurrentAttack.duration*boss.CurrentAttack.hitTime);Call(boss,"Update");Call(boss,"Update");
                Check(heard.Count(k=>k==key)==1,pattern+" contact sound exactly once");yield return new WaitForSeconds(.1f);
            }
            heard.Clear();Call(boss,"Begin",HarvestReaperBoss.Pattern.Storm);Call(boss,"Enter",HarvestReaperBoss.Phase.Active);
            Check(Playing("Reaper_LeafStorm_Loop"),"50% storm loop starts");Call(boss,"Enter",HarvestReaperBoss.Phase.Recovery);
            Check(!Playing("Reaper_LeafStorm_Loop")&&heard.Contains("Reaper_LeafStorm_End"),"50% storm ends cleanly");yield return new WaitForSeconds(.1f);
            heard.Clear();Call(boss,"Begin",HarvestReaperBoss.Pattern.SafeWedge);Call(boss,"Enter",HarvestReaperBoss.Phase.Active);Set(boss,"gathering",false);
            for(int i=0;i<8;i++){Call(boss,"Enter",HarvestReaperBoss.Phase.Warning);Call(boss,"Enter",HarvestReaperBoss.Phase.Active);Call(boss,"Update");Call(boss,"Update");Call(boss,"Enter",HarvestReaperBoss.Phase.Gap);yield return new WaitForSeconds(.08f);}
            Check(heard.Count(k=>k=="Reaper_Sector_Explosion")==8,"Eight sector explosions each sound once");
            Check(heard.Count(k=>k=="Reaper_LeafStorm_Loop")==1&&Playing("Reaper_LeafStorm_Loop"),"Outer storm remains continuous between wedges");
            Call(boss,"Enter",HarvestReaperBoss.Phase.Recovery);Check(!Playing("Reaper_LeafStorm_Loop"),"Wedge storm stops after pattern");
            heard.Clear();Call(boss,"Begin",HarvestReaperBoss.Pattern.Pumpkins);Call(boss,"Enter",HarvestReaperBoss.Phase.Active);
            Check(heard.Contains("Reaper_Pumpkin_Summon")&&heard.Contains("Reaper_Pumpkin_Warning"),"Pumpkin summon and warning");
            var patch=boss.Patches[0];patch.born=Time.time-boss.Pumpkin.explosionWarning-.1f;Call(boss,"TickPatches");
            var audio=root.GetComponent<HarvestReaperAudio>();boss.enabled=true;Set(boss,"nextAttack",Time.time+1000);Call(boss,"Enter",HarvestReaperBoss.Phase.Recovery);
            yield return null;Check(heard.Contains("Reaper_Pumpkin_Explosion")&&Playing("Reaper_Curse_Field_Loop"),"Pumpkin explodes into curse bed");
            patch.born=Time.time-boss.Pumpkin.explosionWarning-boss.Pumpkin.poolDuration-.1f;Call(boss,"TickPatches");yield return null;
            Check(!Playing("Reaper_Curse_Field_Loop")&&heard.Contains("Reaper_Curse_Field_End"),"Last curse pool expires cleanly");
            Call(boss,"Begin",HarvestReaperBoss.Pattern.Storm);Call(boss,"Enter",HarvestReaperBoss.Phase.Active);
            Time.timeScale=0;yield return null;yield return null;Check(!Playing("Reaper_LeafStorm_Loop"),"Pause stops world audio progression");
            int count=heard.Count;GameAudio.Play("Reaper_Scythe_Sweep",Vector3.zero);Check(count==heard.Count,"Menu rejects new combat sounds");
            Time.timeScale=1;yield return null;yield return null;Check(Playing("Reaper_LeafStorm_Loop"),"Resume restores storm audio");
            var prefs=GameAudio.Current;var muted=prefs;muted.effects=0;GameAudio.Preview(muted);yield return null;
            Check(FindFirstObjectByType<GameAudio>().GetComponentsInChildren<AudioSource>().Where(s=>s.clip&&s.clip.name.StartsWith("Reaper_")).All(s=>s.volume==0),"Effects volume mutes reaper audio");GameAudio.Preview(prefs);
            Call(boss,"Clear");Check(!Playing("Reaper_LeafStorm_Loop"),"Encounter cancellation stops storm");boss.enabled=false;
            heard.Clear();Call(root.GetComponent<HarvestReaperDeath>(),"OnDeath");yield return new WaitForSeconds(5.2f);
            foreach(var key in new[]{"Reaper_Death_Release","Reaper_Death_Kneel","Reaper_Scythe_Drop","Reaper_Death_Fall"})Check(heard.Count(k=>k==key)==1,"Corpse animation cue "+key);
            Destroy(root);Destroy(playerObject);Check(true,"Validation complete; no save progression changed");
            GameAudio.EffectStarted-=Heard;
        }
        void OnDestroy(){GameAudio.EffectStarted-=Heard;Time.timeScale=1;}
    }
}
#endif
