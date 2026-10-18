#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace DogLife
{
    // Explicitly attached in Play mode; never starts as part of a normal game.
    public sealed class KrakenVisibilityRegression : MonoBehaviour
    {
        const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
        public string Status="Starting";
        public bool Finished;
        public bool SpecialOnly;
        public bool VortexOnly;
        readonly List<string> report=new List<string>();
        void Log(string text){report.Add(text);Directory.CreateDirectory("Builds/KrakenVisibility");File.WriteAllLines("Builds/KrakenVisibility/report.txt",report);Status=text;}
        IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);
            yield return SceneManager.LoadSceneAsync(SeasonRunSave.Scenes[1]);
            ForestRoomStreaming stream=null;
            while(!(stream=FindFirstObjectByType<ForestRoomStreaming>())||!stream.Ready)yield return null;
            yield return stream.StartCoroutine((IEnumerator)typeof(ForestRoomStreaming).GetMethod("JumpToSummerBoss",F).Invoke(stream,new object[]{stream.roomScenePaths.Length-1}));
            yield return null;yield return null;
            var first=FindFirstObjectByType<KrakenPhaseOne>();var boss=first.GetComponent<KrakenPhaseTwo>();boss.AutomaticAttacks=false;
            var player=first.Player;player.GetComponent<CombatHealth>().invulnerable=true;player.readInput=false;
            float until=Time.time+40;
            while(first.State!=KrakenPhaseOne.EncounterState.Combat&&Time.time<until)yield return null;
            Log("Phase one ready: "+first.State);
            foreach(var t in first.Slots){t.Health.invulnerable=false;t.Health.Damage(100000);}
            until=Time.time+60;
            while(first.State!=KrakenPhaseOne.EncounterState.Underwater&&Time.time<until)yield return null;
            Log("Underwater: "+first.State+" / "+boss.State+" kills="+first.Kills);
            if(first.State!=KrakenPhaseOne.EncounterState.Underwater){Finished=true;yield break;}
            player.GetComponent<CombatHealth>().invulnerable=true;player.enabled=false;player.GetComponent<Rigidbody>().isKinematic=true;
            var cam=Camera.main;var pc=cam.GetComponent<PlayerCamera>();if(pc)pc.enabled=false;
            var brain=cam.GetComponent<Unity.Cinemachine.CinemachineBrain>();if(brain)brain.enabled=false;
            var skin=boss.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(s=>s.name=="SummerBoss_Body_Skin");
            Log("Body enabled="+skin.enabled+" offscreen="+skin.updateWhenOffscreen+" bounds="+skin.bounds);
            if(VortexOnly)
            {
                cam.transform.position=boss.Center+new Vector3(19,5,-13);cam.transform.LookAt(boss.Center);
                player.transform.position=boss.Center+Vector3.right*8;
                boss.StartCoroutine((IEnumerator)typeof(KrakenPhaseTwo).GetMethod("Vortex",F).Invoke(boss,null));
                int earlyOrbs=0,burstMax=0;bool captured=false;
                for(float t=0;t<12;t+=Time.deltaTime)
                {
                    var fx=boss.GetComponentsInChildren<KrakenDeepVfx>();
                    earlyOrbs=Mathf.Max(earlyOrbs,fx.Count(f=>f.Shape==KrakenDeepVfx.Form.Orb));
                    burstMax=Mathf.Max(burstMax,fx.Count(f=>f.Shape==KrakenDeepVfx.Form.PressureBurst));
                    var burst=fx.FirstOrDefault(f=>f.Shape==KrakenDeepVfx.Form.PressureBurst);
                    if(!captured&&burst&&burst.Progress>.3f){ScreenCapture.CaptureScreenshot("Builds/KrakenVisibility/pressure-burst.png");captured=true;}
                    yield return null;
                }
                Log("Vortex early spheres="+earlyOrbs+" max outward pulses="+burstMax+" captured="+captured);
                Finished=true;yield break;
            }
            if(SpecialOnly)
            {
                cam.transform.position=boss.Center+new Vector3(15,5,-8);cam.transform.LookAt(boss.Center);
                player.transform.position=boss.Center+Vector3.right*8;
                var hp=boss.GetComponent<CombatHealth>();hp.invulnerable=false;
                for(int pass=0;pass<2;pass++)
                {
                    hp.Damage(hp.maxHealth*(pass==0?.35f:.4f));boss.AutomaticAttacks=true;
                    int grounded=0;bool intro=false;float deadline=Time.time+40;
                    while(boss.HeartExecutions<pass+1&&Time.time<deadline)yield return null;
                    boss.AutomaticAttacks=false;
                    while(boss.State==KrakenPhaseTwo.Phase.Heart&&Time.time<deadline)
                    {
                        intro|=boss.SpecialIntro&&boss.HeartSlamsCompleted==0;
                        grounded=Mathf.Max(grounded,boss.Arms.Count(a=>a.Tip.y<boss.Arena.min.y+.8f));
                        if(boss.SpecialIntro)ScreenCapture.CaptureScreenshot("Builds/KrakenVisibility/special-intro.png");
                        yield return null;
                    }
                    Log("Threshold "+(pass==0?"70":"30")+" executions="+boss.HeartExecutions+" slams="+boss.HeartSlamsCompleted+" intro="+intro+" grounded="+grounded+" ending="+boss.State);
                    while(boss.State==KrakenPhaseTwo.Phase.Groggy&&Time.time<deadline)yield return null;
                }
                boss.AutomaticAttacks=true;yield return new WaitForSeconds(2);boss.AutomaticAttacks=false;
                Log("No third special: "+(boss.HeartExecutions==2));
                Finished=true;yield break;
            }
            foreach(var method in new[]{"Idle","Pincer","Cannon","Vortex","Sweep"})
            {
                if(method!="Idle")boss.StartCoroutine((IEnumerator)typeof(KrakenPhaseTwo).GetMethod(method,F).Invoke(boss,null));
                for(int i=0;i<8;i++)
                {
                    float angle=i*45*Mathf.Deg2Rad;var target=skin.bounds.center;
                    cam.transform.position=target+new Vector3(Mathf.Cos(angle)*12,(i%3-1)*3,Mathf.Sin(angle)*12);cam.transform.LookAt(target);
                    player.transform.position=cam.transform.position-cam.transform.forward*2;
                    yield return new WaitForSeconds(.3f);
                    typeof(PlayerCamera).GetMethod("UpdateMonsterVisibility",F).Invoke(pc,null);
                    Log(method+" angle="+(i*45)+" visible="+skin.isVisible+" enabled="+skin.enabled+" active="+skin.gameObject.activeInHierarchy+" bounds="+skin.bounds);
                    if(i==0||i==2||i==4||i==6){ScreenCapture.CaptureScreenshot("Builds/KrakenVisibility/"+method+"_"+i+".png");yield return null;}
                }
                yield return new WaitForSeconds(8);
            }
            var contacts=boss.GetComponentsInChildren<Collider>().Where(c=>c.enabled&&MonsterHurtbox.Accepts(c)).ToArray();
            int hidden=0;
            foreach(var contact in contacts)
            {
                cam.transform.position=contact.bounds.center;cam.transform.LookAt(boss.Center+Vector3.up*.01f);Physics.SyncTransforms();
                typeof(PlayerCamera).GetMethod("UpdateMonsterVisibility",F).Invoke(pc,null);
                if(skin.forceRenderingOff)hidden++;
            }
            var boundary=contacts.FirstOrDefault(c=>c.name=="Flood arena boundary 0")??contacts.First();
            cam.transform.position=boundary.bounds.center;cam.transform.LookAt(boss.Center);Physics.SyncTransforms();
            typeof(PlayerCamera).GetMethod("UpdateMonsterVisibility",F).Invoke(pc,null);
            ScreenCapture.CaptureScreenshot("Builds/KrakenVisibility/camera-hide-fixed.png");yield return null;
            Log("Camera contact checks="+contacts.Length+" hidden="+hidden);
            Log("Completed visibility sweep");Finished=true;
        }
    }
}
#endif
