#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;
namespace DogLife.EditorTools
{
 public sealed class KrakenValidation:MonoBehaviour
 {
  const BindingFlags F=BindingFlags.NonPublic|BindingFlags.Instance;
  KrakenBoss b;PlayerMotor p;CombatHealth hp,bh;readonly List<string> results=new List<string>();
  void Check(bool ok,string message){results.Add((ok?"PASS ":"FAIL ")+message);if(!ok)Debug.LogError("Kraken: "+message);}
  void Place(Vector3 position){p.transform.position=position;p.GetComponent<Rigidbody>().position=position;p.GetComponent<Rigidbody>().linearVelocity=Vector3.zero;hp.Restore();Physics.SyncTransforms();}
  void Shorten(string key){var a=(KrakenBoss.Attack)typeof(KrakenBoss).GetField(key,F).GetValue(b);a.warning=.2f;a.duration=.2f;a.groundDuration=.4f;a.recovery=.1f;}
  IEnumerator WaitIdle(float limit=15){float end=Time.time+limit;while(b.State!=KrakenBoss.Phase.Idle&&Time.time<end)yield return null;Check(b.State==KrakenBoss.Phase.Idle,"Pattern returns to idle");typeof(KrakenBoss).GetField("nextAttack",F).SetValue(b,Time.time+1000);}
  IEnumerator Start()
  {
   p=FindFirstObjectByType<PlayerMotor>();hp=p.GetComponent<CombatHealth>();hp.player=true;hp.maxHealth=1000;hp.Restore();
   b=Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/monster/KrakenBoss.prefab")).GetComponent<KrakenBoss>();bh=b.GetComponent<CombatHealth>();Place(new Vector3(0,0,7));yield return null;typeof(KrakenBoss).GetField("nextAttack",F).SetValue(b,Time.time+1000);
   foreach(string key in new[]{"slam","cross","capture","wave"})Shorten(key);
   Vector3 origin=b.transform.position;b.Begin(KrakenBoss.Pattern.Slam);yield return new WaitForSeconds(.1f);Check(hp.Current==1000,"Slam warning harmless");Place(new Vector3(8,0,7));yield return new WaitForSeconds(.4f);Check(hp.Current==1000,"Slam saved direction dodge");
   var t=b.Tentacles[0];var hit=t.transform.Find("HitColliderLine").GetComponent<Collider>();Check(hit.enabled&&hit.GetComponentInParent<CombatHealth>()==bh,"Exposed tentacle resolves to only boss HP");float before=bh.Current;hit.GetComponentInParent<CombatHealth>().Damage(10);Check(bh.Current==before-10,"Tentacle damage forwarded to body");yield return WaitIdle();Check(!hit.enabled,"Retracted tentacle no longer targetable");
   Place(new Vector3(0,0,7));b.Begin(KrakenBoss.Pattern.Slam);yield return new WaitForSeconds(.5f);Check(hp.Current==999,"Slam one damage despite overlap and trigger");yield return WaitIdle();
   Place(new Vector3(0,0,7));b.Begin(KrakenBoss.Pattern.Cross);Vector3 saved=b.Tentacles[0].Origin;Check(b.Tentacles.Count>=4,"Cross creates four tentacles");Place(new Vector3(6,0,13));yield return new WaitForSeconds(.4f);Check(Vector3.Distance(saved,b.Tentacles[0].Origin)<.001f,"Cross warnings do not track moving player");yield return WaitIdle();
   var cfg=(KrakenBoss.CrossSettings)typeof(KrakenBoss).GetField("surrounding",F).GetValue(b);cfg.simultaneous=false;cfg.delay=.35f;Place(new Vector3(0,0,7));hp.invulnerable=true;b.Begin(KrakenBoss.Pattern.Cross);yield return new WaitForSeconds(.45f);Check(b.Tentacles[0].State!=b.Tentacles[3].State,"Sequential tentacles stagger");yield return WaitIdle();hp.invulnerable=false;
   var bind=(KrakenBoss.CaptureSettings)typeof(KrakenBoss).GetField("binding",F).GetValue(b);bind.bindDuration=.5f;bind.throwAnimation=.3f;
   Place(new Vector3(0,0,7));b.Begin(KrakenBoss.Pattern.Capture);Vector3 target=b.Tentacles[0].Origin;Place(new Vector3(8,0,12));yield return new WaitForSeconds(.4f);Check(hp.Current==1000&&Vector3.Distance(target,b.Tentacles[0].Origin)<.001f,"Capture fixed point can be dodged");yield return WaitIdle();
   Place(new Vector3(0,0,7));b.Begin(KrakenBoss.Pattern.Capture);yield return new WaitForSeconds(.4f);Check(hp.Current==999&&p.IsKnockedBack,"Capture applies damage and bind");yield return new WaitForSeconds(.5f);Check(p.GetComponent<Rigidbody>().linearVelocity.y>1,"Capture releases with upward throw");yield return WaitIdle();
   b.Tide.speed=18;b.Tide.travelDistance=15;b.Tide.safeGapWidth=4;Place(new Vector3(0,0,8));b.Begin(KrakenBoss.Pattern.Wave);yield return new WaitForSeconds(1);Check(hp.Current==1000,"Wave gap is safe");yield return WaitIdle();
   Place(new Vector3(6,0,8));b.Begin(KrakenBoss.Pattern.Wave);yield return new WaitForSeconds(1);Check(hp.Current==999,"Wave solid side damages once");yield return WaitIdle();
   var fury=(KrakenBoss.FurySettings)typeof(KrakenBoss).GetField("fury",F).GetValue(b);fury.roarDuration=.2f;fury.warning=.2f;fury.finalWarning=.3f;fury.delayBetweenWaves=.1f;fury.groggyDuration=.8f;fury.tentacleDelay=.1f;
   Place(new Vector3(0,0,7));hp.invulnerable=true;b.Begin(KrakenBoss.Pattern.Slam);bh.Damage(bh.maxHealth*.6f);yield return new WaitForSeconds(.1f);Check(b.CurrentPattern==KrakenBoss.Pattern.Slam&&!b.SpecialUsed,"HP trigger waits for current attack to finish");
   float end=Time.time+20;bool groggy=false,final=false;while(Time.time<end){if(b.CurrentPattern==KrakenBoss.Pattern.Fury){if(b.FuryRound==fury.waveCount)final=true;if(b.State==KrakenBoss.Phase.Groggy){groggy=true;break;}}yield return null;}
   Check(b.SpecialUsed&&final&&groggy,"50 percent sequence completes final strike and groggy");yield return WaitIdle();yield return new WaitForSeconds(.15f);Check(b.CurrentPattern==KrakenBoss.Pattern.Fury&&b.State==KrakenBoss.Phase.Idle,"Special does not repeat");Check(Vector3.Distance(origin,b.transform.position)<.001f,"Body remains stationary");
   b.Begin(KrakenBoss.Pattern.Capture);b.enabled=false;yield return null;bool clean=true;foreach(var x in b.Tentacles)if(x.Busy)clean=false;Check(clean,"Disable clears tentacles and hazards");
   System.IO.File.WriteAllLines("Builds/VfxValidation/KrakenValidation.txt",results);Debug.Log("Kraken validation complete: "+string.Join("; ",results));
  }
 }
}
#endif
