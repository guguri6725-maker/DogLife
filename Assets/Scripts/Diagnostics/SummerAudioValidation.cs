#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEditor;
namespace DogLife
{
    // Explicit opt-in runner. Drives real state machines without changing prefab balance.
    public sealed class SummerAudioValidation:MonoBehaviour
    {
        const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
        readonly List<string> heard=new List<string>(),report=new List<string>();
        PlayerMotor player;CombatHealth hp;Rigidbody rb;KrakenPhaseOne first;KrakenPhaseTwo second;
        public bool Finished{get;private set;}
        void Heard(string key){heard.Add(key);}
        void Check(bool ok,string label){report.Add((ok?"PASS ":"FAIL ")+label);System.IO.Directory.CreateDirectory("Builds/AudioValidation");System.IO.File.WriteAllLines("Builds/AudioValidation/summer-runtime-checks.txt",report);}
        static object Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,F).Invoke(o,args);
        static void Set(object o,string name,object value)=>o.GetType().GetField(name,F).SetValue(o,value);
        bool Playing(string key)=>FindFirstObjectByType<GameAudio>().GetComponentsInChildren<AudioSource>().Any(s=>s.clip&&s.clip.name==key&&s.isPlaying);
        void Place(Vector3 p){rb.isKinematic=false;rb.linearVelocity=Vector3.zero;rb.position=player.transform.position=p;player.MoveInput=Vector2.zero;Physics.SyncTransforms();}
        void ScheduleOff(){foreach(var key in new[]{"nextSlam","nextSweep","nextCapture"})Set(first,key,Time.time+10000);}
        IEnumerator Start()
        {
            GameAudio.EffectStarted+=Heard;
            var stream=FindFirstObjectByType<ForestRoomStreaming>();while(stream&&!stream.Ready)yield return null;
            if(stream){stream.StopAllCoroutines();stream.enabled=false;}
            player=FindFirstObjectByType<PlayerMotor>();hp=player.GetComponent<CombatHealth>();rb=player.GetComponent<Rigidbody>();player.readInput=false;hp.maxHealth=1000;hp.Restore();hp.invulnerable=true;
            foreach(var h in FindObjectsByType<CombatHealth>(FindObjectsSortMode.None))if(!h.player)h.gameObject.SetActive(false);
            var clips=Resources.LoadAll<AudioClip>("Audio/Summer");Check(clips.Length==40,"All 40 recordings import");
            Check(clips.Where(c=>!c.name.StartsWith("Summer_Amb_")).All(c=>c.channels==1&&c.loadType==AudioClipLoadType.DecompressOnLoad),"38 effects use mono decompressed playback");
            Check(clips.Where(c=>c.name.StartsWith("Summer_Amb_")).All(c=>c.channels==2&&c.loadType==AudioClipLoadType.Streaming),"Two long ambience beds use stereo streaming");
            foreach(var clip in clips.Where(c=>!c.name.StartsWith("Summer_Amb_"))){GameAudio.Play(clip.name,player.transform.position,minimumInterval:0).Stop();}
            Check(clips.Where(c=>!c.name.StartsWith("Summer_Amb_")).All(c=>heard.Contains(c.name)),"Every effect is reachable through GameAudio");
            yield return new WaitForSeconds(.2f);heard.Clear();
            foreach(var name in new[]{"CoralMermaid","BlueJellyfish","BoneTortoise","HermitCrab"})
            {
                var go=Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/monster/"+name+".prefab"),player.transform.position+Vector3.right*7,Quaternion.identity);
                var e=go.GetComponent<SummerMonsterEnemy>();e.enabled=false;go.GetComponent<NavMeshAgent>().enabled=false;yield return null;Call(e,"Start");
                string attack=e.Kind==SummerMonsterEnemy.Species.Mermaid?"Mermaid_Emerge_Attack":e.Kind==SummerMonsterEnemy.Species.Jellyfish?"Jellyfish_Discharge":e.Kind==SummerMonsterEnemy.Species.Tortoise?"Tortoise_Bite":"Crab_Bubble_Cast";
                if(e.Kind==SummerMonsterEnemy.Species.Mermaid){Call(e,"Enter",SummerMonsterEnemy.Phase.Dive);Call(e,"Enter",SummerMonsterEnemy.Phase.Burrow);Check(Playing("Mermaid_Underwater_Loop"),"Mermaid dive loop starts");}
                Call(e,"Enter",SummerMonsterEnemy.Phase.Warning);Call(e,"Enter",SummerMonsterEnemy.Phase.Attack);Set(e,"elapsed",.01f);Call(e,"FixedUpdate");Check(!heard.Contains(attack),name+" does not sound impact before active window");
                Set(e,"elapsed",(float)e.GetType().GetField("attackTime",F).GetValue(e)*.31f);Call(e,"FixedUpdate");Call(e,"FixedUpdate");Check(heard.Count(k=>k==attack)==1,name+" emits impact once at active window");
                if(e.Kind==SummerMonsterEnemy.Species.Mermaid)Check(!Playing("Mermaid_Underwater_Loop"),"Mermaid loop ends on emergence");
                Call(e,"Enter",SummerMonsterEnemy.Phase.Recovery);
                if(e.Kind==SummerMonsterEnemy.Species.Tortoise){Call(e,"Enter",SummerMonsterEnemy.Phase.GuardIn);e.BlocksDamage(1);Call(e,"Enter",SummerMonsterEnemy.Phase.GuardOut);Check(heard.Contains("Tortoise_Shell_Enter")&&heard.Contains("Tortoise_Shell_Block")&&heard.Contains("Tortoise_Shell_Exit"),"Tortoise guard enter/block/exit cues");}
                go.SetActive(false);Destroy(go);yield return null;
            }
            foreach(var b in FindObjectsByType<SummerHomingBubble>(FindObjectsSortMode.None))Destroy(b.gameObject);
            var debuff=player.GetComponent<BubbleDebuff>();if(!debuff)debuff=player.gameObject.AddComponent<BubbleDebuff>();
            debuff.Apply(.3f,.3f,.2f,null);yield return new WaitForSeconds(.1f);debuff.Apply(.3f,.3f,.2f,null);yield return new WaitForSeconds(.12f);Check(!heard.Contains("Crab_Debuff_End"),"Re-hit refresh does not emit an early debuff end");yield return new WaitForSeconds(.15f);Check(heard.Count(k=>k=="Crab_Debuff_End")==1,"Debuff expiry sounds once");
            var voice=SummerAudioEmitter.Get(player);voice.Sustain("Mermaid_Underwater_Loop");Time.timeScale=0;yield return null;yield return null;
            Check(!Playing("Mermaid_Underwater_Loop"),"Paused menu pauses world loop");int count=heard.Count;GameAudio.Play("Tortoise_Bite",player.transform.position);Check(heard.Count==count,"Paused menu rejects new combat cues");Time.timeScale=1;yield return null;yield return null;Check(Playing("Mermaid_Underwater_Loop"),"World loop resumes after pause");voice.StopAll();Check(!Playing("Mermaid_Underwater_Loop"),"Emitter cancellation stops loop");
            var preferences=GameAudio.Current;var muted=preferences;muted.effects=0;GameAudio.Preview(muted);yield return null;
            Check(FindFirstObjectByType<GameAudio>().GetComponentsInChildren<AudioSource>().Where(s=>s.clip&&s.clip.name.StartsWith("Summer_")).All(s=>s.volume==0),"Effects slider mutes summer ambience");GameAudio.Preview(preferences);
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/SeasonRooms/Summer/Summer_Room04.unity",LoadSceneMode.Additive);
            var room=FindObjectsByType<ForestRoom>(FindObjectsSortMode.None).First(r=>r.gameObject.scene.name=="Summer_Room04");yield return room.PrepareGeometry();room.BeginEncounter(false);yield return null;yield return null;
            first=FindFirstObjectByType<KrakenPhaseOne>();second=first.GetComponent<KrakenPhaseTwo>();second.AutomaticAttacks=false;Place(new Vector3(room.entryX+7,1,0));room.BeginEncounter();
            float deadline=Time.time+40;while(first.State!=KrakenPhaseOne.EncounterState.Combat&&Time.time<deadline)yield return null;
            Check(first.State==KrakenPhaseOne.EncounterState.Combat,"Boss entry completes");ScheduleOff();
            // Entry can schedule its first automatic slam before this coroutine resumes.
            foreach(var slot in first.Slots){slot.StopAttack();slot.SetPose(KrakenPhaseTentacle.Pose.Idle,0);}
            ((HashSet<KrakenPhaseTentacle>)first.GetType().GetField("slamming",F).GetValue(first)).Clear();
            yield return new WaitForSeconds(.1f);
            heard.Clear();var t=first.Slots[0];Place(t.transform.position+Vector3.back*4+Vector3.up);
            yield return t.StartCoroutine((IEnumerator)Call(first,"Slam",t));Check(heard.Count(k=>k=="Kraken_Tentacle_Slam")==1&&heard.Contains("Kraken_Tentacle_Windup"),"Slam windup then one contact cue: "+string.Join(",",heard));ScheduleOff();
            hp.invulnerable=false;Place(new Vector3(first.Arena.center.x,1,0));heard.Clear();yield return first.StartCoroutine((IEnumerator)Call(first,"Capture"));
            Check(heard.Contains("Kraken_Grab_Warning")&&heard.Contains("Kraken_Grab_Coil")&&heard.Count(k=>k=="Kraken_Throw")==1,"Capture warning, actual grip and throw cues");ScheduleOff();hp.invulnerable=true;
            heard.Clear();yield return first.StartCoroutine((IEnumerator)Call(first,"Sweep"));Check(heard.Contains("Kraken_Tentacle_Sweep"),"Central sweep sounds during sweep");ScheduleOff();
            heard.Clear();foreach(var slot in first.Slots)slot.Health.Damage(10000);deadline=Time.time+45;
            while(first.State!=KrakenPhaseOne.EncounterState.Underwater&&Time.time<deadline)yield return null;
            Check(first.Kills==5&&first.State==KrakenPhaseOne.EncounterState.Underwater,"Five kills transition to phase two");
            foreach(var key in new[]{"Kraken_Tentacle_Defeat","Kraken_Flood_Start","Kraken_Flood_Loop","Kraken_Submerge","Kraken_Sand_Rise","Kraken_Body_Emerge","Kraken_Roar"})Check(heard.Contains(key),"Transition cue "+key);
            Check(!Playing("Kraken_Flood_Loop"),"Flood loop stops after filling");Check(first.GetComponent<KrakenWaterVolume>().FlowVisibility==0,"Decoration flow audio follows fully hidden water");
            hp.invulnerable=true;Place(second.Center+Vector3.right*8);player.enabled=false;rb.isKinematic=true;
            foreach(var pattern in new[]{"Pincer","Cannon","Vortex","Sweep","Heart"})
            {
                heard.Clear();yield return second.StartCoroutine((IEnumerator)Call(second,pattern));
                string key=pattern=="Pincer"||pattern=="Sweep"?"Kraken_Underwater_Strike":"Kraken_Pressure_Burst";
                Check(heard.Contains(key),"Phase two "+pattern+" impact cue");if(pattern=="Heart")Check(heard.Count(k=>k==key)==3&&heard.Contains("Kraken_Groggy"),"Three heart blasts and groggy cue");
            }
            heard.Clear();second.TryBeginNextPhase();yield return new WaitForSeconds(.1f);Check(heard.Count(k=>k=="Kraken_Death")==1,"Boss death cue fires once");
            first.CancelEncounter();Check(!Playing("Kraken_Flood_Loop"),"Boss cancellation leaves no water loop");
            Finished=true;GameAudio.EffectStarted-=Heard;Debug.Log("Summer audio verification complete: "+report.Count+" checks, "+report.Count(x=>x.StartsWith("FAIL"))+" failures");
        }
        void OnDestroy(){GameAudio.EffectStarted-=Heard;Time.timeScale=1;}
    }
}
#endif
