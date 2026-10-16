#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
namespace DogLife.EditorTools
{
    public sealed class SpringBoarValidationRunner:MonoBehaviour
    {
        const BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Instance;
        SpringBoarBoss boss;PlayerMotor player;CombatHealth hp,bossHealth;
        readonly List<string> checks=new List<string>();
        void Check(bool ok,string message){checks.Add((ok?"PASS ":"FAIL ")+message);if(!ok)Debug.LogError("Boar QA: "+message);}
        void Set(object o,string name,object value){o.GetType().GetField(name,Flags).SetValue(o,value);}
        void Begin(SpringBoarBoss.Pattern pattern){typeof(SpringBoarBoss).GetMethod("Begin",Flags).Invoke(boss,new object[]{pattern});}
        void Enter(SpringBoarBoss.Phase phase){typeof(SpringBoarBoss).GetMethod("Enter",Flags).Invoke(boss,new object[]{phase});}
        void Place(Vector3 point){Set(player,"knockbackUntil",0f);player.transform.position=point;var rb=player.GetComponent<Rigidbody>();rb.position=point;rb.linearVelocity=Vector3.zero;hp.Restore();Physics.SyncTransforms();}
        void Shot(string name){ScreenCapture.CaptureScreenshot("Builds/VfxValidation/Boar_"+name+".png");}
        IEnumerator Start()
        {
            Application.runInBackground=true;System.IO.Directory.CreateDirectory("Builds/VfxValidation");player=FindFirstObjectByType<PlayerMotor>();hp=player.GetComponent<CombatHealth>();
            Camera.main.transform.position=new Vector3(15,15,19);Camera.main.transform.LookAt(new Vector3(0,3,3));Camera.main.orthographicSize=12;
            boss=Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Monster/Spring/SpringBoar/Boss.prefab")).GetComponent<SpringBoarBoss>();bossHealth=boss.GetComponent<CombatHealth>();
            Place(new Vector3(0,0,6));yield return null;Set(boss,"nextAttack",Time.time+1000);
            Check(boss.transform.localScale==Vector3.one*10,"XYZ scale is 10");
            bool highHasSpikes=false;var choose=typeof(SpringBoarBoss).GetMethod("Choose",Flags);for(int i=0;i<100;i++)if((SpringBoarBoss.Pattern)choose.Invoke(boss,new object[]{10f,0f})==SpringBoarBoss.Pattern.Spikes)highHasSpikes=true;
            Check(!highHasSpikes,"Spikes unavailable above 50 percent HP");Set(boss,"flankClock",3f);Check((SpringBoarBoss.Pattern)choose.Invoke(boss,new object[]{6f,130f})==SpringBoarBoss.Pattern.Slam,"Lingering beside or behind selects slam");Set(boss,"flankClock",0f);
            Begin(SpringBoarBoss.Pattern.Tusk);yield return new WaitForSeconds(.6f);Check(hp.Current==1000,"Tusk warning is harmless");Shot("TuskWarning");
            Enter(SpringBoarBoss.Phase.Attack);yield return new WaitForSeconds(.4f);Shot("Tusk");Check(hp.Current==998,"Tusk heavy hit removes two HP once");Check(player.IsKnockedBack&&player.GetComponent<Rigidbody>().linearVelocity.y>1,"Tusk launches player backward and upward");
            yield return new WaitForSeconds(.5f);Place(new Vector3(0,0,-6));Enter(SpringBoarBoss.Phase.Attack);yield return new WaitForSeconds(.6f);Check(hp.Current==1000,"Tusk excludes rear target");
            Place(new Vector3(6,0,0));Begin(SpringBoarBoss.Pattern.Slam);yield return new WaitForSeconds(.8f);Check(hp.Current==1000,"Circular slam warning is harmless");Shot("SlamReady");yield return null;Enter(SpringBoarBoss.Phase.Attack);yield return new WaitForSeconds(.65f);Shot("Slam");Check(hp.Current==999,"Expanding slam hits side target once");yield return new WaitForSeconds(.4f);Check(hp.Current==999,"Slam does not repeat damage");
            bossHealth.Damage(1000);Check(boss.Enraged,"Half HP enables second phase");bool lowHasSpikes=false;for(int i=0;i<100;i++)if((SpringBoarBoss.Pattern)choose.Invoke(boss,new object[]{10f,0f})==SpringBoarBoss.Pattern.Spikes)lowHasSpikes=true;Check(lowHasSpikes,"Spikes join random pool below half HP");
            Place(new Vector3(0,0,7));Begin(SpringBoarBoss.Pattern.Spikes);Check(boss.SpikeCount>=3&&boss.SpikeCount<=5,"Three to five non-overlapping spike locations");Vector3 first=boss.SpikePoint(0);yield return new WaitForSeconds(.7f);Check(hp.Current==1000&&boss.State==SpringBoarBoss.Phase.Warning,"Spikes give more than one second warning");Shot("SpikeWarning");yield return null;Place(first);yield return new WaitForSeconds(.7f);Shot("Spikes");Check(hp.Current==999,"Bone spike contact damages once");yield return new WaitForSeconds(2);Check(boss.SpikeCount==0,"Spikes expire after attack");
            boss.GetComponent<NavMeshAgent>().Warp(Vector3.zero);Place(new Vector3(0,0,10));Begin(SpringBoarBoss.Pattern.Charge);Vector3 locked=boss.Forward;Place(new Vector3(8,0,8));yield return new WaitForSeconds(.8f);Check(Vector3.Dot(locked,boss.Forward)>.999f,"Charge direction is locked during warning");Shot("Scrape");
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Boss QA wall";wall.layer=8;wall.transform.position=new Vector3(0,5,12);wall.transform.localScale=new Vector3(28,10,1);Physics.SyncTransforms();
            Enter(SpringBoarBoss.Phase.Attack);yield return new WaitForSeconds(.55f);Check(boss.State==SpringBoarBoss.Phase.Stunned,"Wall collision stops charge and stuns");Vector3 stopped=boss.transform.position;Shot("Stunned");yield return new WaitForSeconds(1.6f);Check(boss.State==SpringBoarBoss.Phase.Stunned&&Vector3.Distance(stopped,boss.transform.position)<.02f,"Wall stun leaves at least two seconds to attack");yield return new WaitForSeconds(1);Check(boss.State==SpringBoarBoss.Phase.Move,"Boss resumes movement after stun");
            Begin(SpringBoarBoss.Pattern.Spikes);boss.enabled=false;Check(boss.SpikeCount==0,"Disabling boss clears hazards");Destroy(wall);
            System.IO.File.WriteAllLines("Builds/VfxValidation/SpringBoarValidation.txt",checks);Debug.Log("Boar QA complete: "+string.Join("; ",checks));
        }
    }
}
#endif
