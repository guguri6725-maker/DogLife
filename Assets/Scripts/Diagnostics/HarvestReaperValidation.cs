#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;
namespace DogLife.EditorTools
{
 public sealed class HarvestReaperValidation:MonoBehaviour
 {
  const BindingFlags F=BindingFlags.NonPublic|BindingFlags.Instance;
  HarvestReaperBoss boss;PlayerMotor player;CombatHealth hp,bh;readonly List<string> log=new List<string>();
  object Get(string n)=>typeof(HarvestReaperBoss).GetField(n,F).GetValue(boss);
  void Set(string n,object v)=>typeof(HarvestReaperBoss).GetField(n,F).SetValue(boss,v);
  void Begin(HarvestReaperBoss.Pattern p){Vector3 d=player.transform.position-boss.transform.position;d.y=0;if(d.sqrMagnitude>.001f)boss.transform.rotation=Quaternion.LookRotation(d);typeof(HarvestReaperBoss).GetMethod("Begin",F).Invoke(boss,new object[]{p});}
  void Enter(HarvestReaperBoss.Phase p)=>typeof(HarvestReaperBoss).GetMethod("Enter",F).Invoke(boss,new object[]{p});
  void Check(bool ok,string s){log.Add((ok?"PASS ":"FAIL ")+s);if(!ok)Debug.LogError("Reaper validation: "+s);}
  void Place(Vector3 p){typeof(PlayerMotor).GetField("knockbackUntil",F).SetValue(player,0f);player.transform.position=p;player.GetComponent<Rigidbody>().position=p;player.GetComponent<Rigidbody>().linearVelocity=Vector3.zero;hp.Restore();Physics.SyncTransforms();}
  IEnumerator Start()
  {
   Application.runInBackground=true;System.IO.Directory.CreateDirectory("Builds/VfxValidation");player=FindFirstObjectByType<PlayerMotor>();hp=player.GetComponent<CombatHealth>();hp.Restore();
   boss=Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Monster/Autumn/HarvestReaper/Boss.prefab")).GetComponent<HarvestReaperBoss>();bh=boss.GetComponent<CombatHealth>();Place(new Vector3(0,0,6));yield return null;Set("nextAttack",Time.time+1000);
   var cam=Camera.main;cam.transform.position=new Vector3(22,27,27);cam.transform.LookAt(Vector3.up*2);cam.orthographicSize=25;
   Vector3 origin=boss.transform.position;Begin(HarvestReaperBoss.Pattern.Sweep);Vector3 forward=boss.Forward;Check(boss.Contains(new Vector3(0,1,10))&&!boss.Contains(new Vector3(0,1,-5)),"Sweep front 180 degrees; rear safe");
   yield return new WaitForSeconds(.5f);Check(hp.Current==1000,"Warning cannot damage");Place(new Vector3(4,0,5));Check(Vector3.Dot(forward,boss.Forward)>.999f,"Warning locks attack direction");Enter(HarvestReaperBoss.Phase.Active);yield return new WaitForSeconds(.7f);Check(hp.Current==999,"Sweep damages exactly once");yield return new WaitForSeconds(.4f);Check(hp.Current==999,"No duplicate active hit");
   Place(new Vector3(0,0,8));Begin(HarvestReaperBoss.Pattern.Cleave);Check(boss.Contains(new Vector3(0,1,20))&&!boss.Contains(new Vector3(3,1,10))&&!boss.Contains(new Vector3(0,1,-2)),"Cleave long narrow rectangle and rear exclusion");
   Enter(HarvestReaperBoss.Phase.Active);yield return new WaitForSeconds(.55f);Check(hp.Current==999,"Cleave deals damage");
   Place(new Vector3(0,0,6));Set("closeClock",6f);Check((int)typeof(HarvestReaperBoss).GetMethod("Choose",F).Invoke(boss,new object[]{6f})==2,"Lingering close selects shockwave");Begin(HarvestReaperBoss.Pattern.Shockwave);Enter(HarvestReaperBoss.Phase.Active);yield return new WaitForSeconds(.38f);Check(hp.Current==999&&player.IsKnockedBack,"Shockwave damage and knockback");
   hp.invulnerable=true;Place(new Vector3(0,0,7));Begin(HarvestReaperBoss.Pattern.Pumpkins);Enter(HarvestReaperBoss.Phase.Active);Check(boss.Patches.Count==5,"Five pumpkins spawned");Check(Vector3.Distance(boss.Patches[0].point,player.transform.position)<1,"First pumpkin targets player");bool separated=true;for(int i=0;i<boss.Patches.Count;i++)for(int j=0;j<i;j++)if(Vector3.Distance(boss.Patches[i].point,boss.Patches[j].point)<boss.Pumpkin.minSpacing-.01f)separated=false;Check(separated,"Pumpkin spacing respected");
   var first=boss.Patches[0];Begin(HarvestReaperBoss.Pattern.Sweep);Check(boss.Patches.Count==5,"Existing pumpkins survive next attack");
   yield return new WaitForSeconds(2.6f);Check(first.exploded&&boss.Patches.Count>0,"Explosion becomes persistent pool");ScreenCapture.CaptureScreenshot("Builds/VfxValidation/Reaper_Pumpkins.png");yield return null;
   boss.Pumpkin.poolDuration=.2f;yield return new WaitForSeconds(.2f);Check(boss.Patches.Count==0,"Pools expire independently");
   boss.Special.gatherWarning=.15f;boss.Special.gatherDuration=.15f;Begin(HarvestReaperBoss.Pattern.SafeWedge);
   Check(boss.Gathering&&!boss.Contains(Vector3.forward*3)&&boss.Contains(Vector3.forward*10),"Gather stage: whole inner circle safe; outer ring dangerous including future wedge");
   while(boss.Gathering)yield return null;
   Check(boss.OuterStormActive,"Outer storm persists through inner warning");boss.Special.warning=.15f;boss.Special.explosionDuration=.1f;boss.Special.delay=.1f;
   float angle=boss.SafeYaw;Vector3 dir=Quaternion.Euler(0,angle,0)*Vector3.forward;Check(!boss.Contains(dir*3+Vector3.up)&&boss.Contains(-dir*3+Vector3.up)&&boss.Contains(dir*10+Vector3.up),"Only inner 45-degree wedge safe; rest of inner circle and all outer ring dangerous");
   int explosions=0;bool wasActive=false,gapCleared=true;float deadline=Time.time+5;while(boss.State!=HarvestReaperBoss.Phase.Recovery&&Time.time<deadline){bool active=boss.State==HarvestReaperBoss.Phase.Active;if(active&&!wasActive)explosions++;wasActive=active;if(boss.State==HarvestReaperBoss.Phase.Gap){yield return new WaitForEndOfFrame();var mesh=boss.GetComponentInChildren<HarvestReaperVfx>().GetComponent<MeshFilter>().sharedMesh;if(mesh.vertexCount==0||!boss.OuterStormActive)gapCleared=false;}yield return null;}
   Check(explosions==8,"Eight separate wedge explosions");Check(gapCleared,"Outer storm remains visible during inner explosion gaps");Check(Mathf.Abs(Mathf.Abs(boss.SafeYaw-angle)-315)<.1f,"Safe zone jumps seven 45-degree steps across eight orientations");
   bool[] consumed=(bool[])Get("consumed");for(int i=0;i<3;i++)consumed[i]=false;bh.Damage(bh.maxHealth*.8f);
   Check((int)typeof(HarvestReaperBoss).GetMethod("PendingSpecial",F).Invoke(boss,null)==0,"Large HP drop queues 70 percent first");
   consumed[0]=true;Check((int)typeof(HarvestReaperBoss).GetMethod("PendingSpecial",F).Invoke(boss,null)==1,"50 percent storm remains pending");consumed[1]=true;Check((int)typeof(HarvestReaperBoss).GetMethod("PendingSpecial",F).Invoke(boss,null)==2,"30 percent wedge remains pending");consumed[2]=true;Check((int)typeof(HarvestReaperBoss).GetMethod("PendingSpecial",F).Invoke(boss,null)==-1,"HP thresholds cannot repeat");
   Place(new Vector3(0,0,7));hp.invulnerable=false;Begin(HarvestReaperBoss.Pattern.Storm);Enter(HarvestReaperBoss.Phase.Active);yield return new WaitForSeconds(2.3f);Check(hp.Current<=998&&hp.Current>=997,"Storm uses timed repeated damage");ScreenCapture.CaptureScreenshot("Builds/VfxValidation/Reaper_Storm.png");yield return null;
   Check(Vector3.Distance(origin,boss.transform.position)<.001f,"Standing boss never translates");Begin(HarvestReaperBoss.Pattern.Pumpkins);Enter(HarvestReaperBoss.Phase.Active);boss.enabled=false;yield return null;Check(boss.Patches.Count==0,"Disable clears all hazards");
   System.IO.File.WriteAllLines("Builds/VfxValidation/HarvestReaperValidation.txt",log);Debug.Log("Reaper validation complete: "+string.Join("; ",log));
  }
 }
}
#endif
