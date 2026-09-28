#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
namespace DogLife.EditorTools
{
    public sealed class WinterMonsterValidationRunner:MonoBehaviour
    {
        const BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Instance;
        readonly List<string> checks=new List<string>();PlayerMotor player;CombatHealth hp;WinterMonsterEnemy enemy;
        static void Set(object o,string n,object v){o.GetType().GetField(n,Flags).SetValue(o,v);}
        static void Enter(WinterMonsterEnemy e,WinterMonsterEnemy.Phase p){typeof(WinterMonsterEnemy).GetMethod("Enter",Flags).Invoke(e,new object[]{p});}
        void Check(bool value,string text){checks.Add((value?"PASS ":"FAIL ")+text);if(!value)Debug.LogError("Winter validation: "+text);}
        void Place(Vector3 p){Set(player,"knockbackUntil",0f);player.MoveInput=Vector2.zero;var rb=player.GetComponent<Rigidbody>();rb.position=p;rb.linearVelocity=Vector3.zero;player.transform.position=p;hp.Restore();Physics.SyncTransforms();}
        WinterMonsterEnemy Spawn(string name)
        {
            if(enemy)Destroy(enemy.gameObject);var go=Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/monster/"+name+".prefab"));go.name=name;enemy=go.GetComponent<WinterMonsterEnemy>();Set(enemy,"nextAttack",Time.time+100);Check(go.transform.localScale==Vector3.one*(name=="SnowSpirit"?1:2),name+" has expected XYZ scale");return enemy;
        }
        void Shot(string name){ScreenCapture.CaptureScreenshot("Builds/VfxValidation/Winter_"+name+".png");}
        IEnumerator Start()
        {
            System.IO.Directory.CreateDirectory("Builds/VfxValidation");Application.runInBackground=true;
            player=FindFirstObjectByType<PlayerMotor>();hp=player.GetComponent<CombatHealth>();hp.Restore();
            Camera.main.transform.position=new Vector3(13,12,18);Camera.main.transform.LookAt(new Vector3(0,2,5));Camera.main.orthographicSize=9f;
            var golem=Spawn("IceGolem");Place(new Vector3(0,0,14));yield return null;golem.GetComponent<NavMeshAgent>().enabled=false;
            Enter(golem,WinterMonsterEnemy.Phase.Warning);yield return new WaitForSeconds(.8f);Check(hp.Current==1000,"Golem warning is harmless");Shot("GolemWarning");
            Enter(golem,WinterMonsterEnemy.Phase.Attack);yield return new WaitForSeconds(.4f);Check(hp.Current==1000,"Golem distant target not hit before wave reaches it");
            yield return new WaitForSeconds(.38f);Shot("GolemSpikes");Check(hp.Current==999&&golem.Range==18,"Faster golem wave reaches extended range");Check(player.IsKnockedBack&&player.GetComponent<Rigidbody>().linearVelocity.y<.1f,"Golem applies tiny grounded knockback");
            Check(!golem.Contains(new Vector3(0,1,-2),.3f)&&!golem.Contains(new Vector3(4,1,1),.3f),"Golem fan excludes rear and side");
            yield return new WaitForSeconds(.4f);Check(hp.Current==999,"Golem does not repeat damage");
            var wolf=Spawn("SnowWolf");Place(new Vector3(0,0,3));yield return null;wolf.GetComponent<NavMeshAgent>().enabled=false;
            Enter(wolf,WinterMonsterEnemy.Phase.Warning);yield return new WaitForSeconds(.84f);Vector3 firstTarget=wolf.TargetPoint;Check(wolf.State==WinterMonsterEnemy.Phase.Attack,"Wolf starts first leap");Shot("WolfLeap");
            yield return new WaitForSeconds(.46f);Check(hp.Current==999,"Wolf first landing damages once");Check(player.GetComponent<Rigidbody>().linearVelocity.y>0,"Wolf first hit gives small lift");Place(new Vector3(2,0,3));
            yield return new WaitForSeconds(.18f);Check(wolf.State==WinterMonsterEnemy.Phase.Reaim&&Vector3.Distance(wolf.TargetPoint,firstTarget)>1,"Wolf reaims toward new player position after landing");Shot("WolfReaim");
            Vector3 grounded=wolf.transform.position;float deadline=Time.time+1.5f;while(hp.Current==1000&&Time.time<deadline)yield return null;
            Check(hp.Current==999&&player.IsKnockedBack,"Wolf claw deals damage and small knockback");
            Check(Vector3.Distance(wolf.transform.position,grounded)<.03f&&Mathf.Abs(wolf.transform.Find("Visual").localPosition.y)<.02f,"Wolf second attack stays grounded without another leap");Shot("WolfClaw");
            deadline=Time.time+1;while(wolf.State==WinterMonsterEnemy.Phase.SecondAttack&&Time.time<deadline)yield return null;
            Check(wolf.State==WinterMonsterEnemy.Phase.Recovery,"Wolf enters recovery after claw");yield return new WaitForSeconds(.8f);Check(wolf.State==WinterMonsterEnemy.Phase.Recovery,"Wolf leaves a clear counterattack window");
            var mammoth=Spawn("FrostMammoth");Place(new Vector3(.25f,0,2));yield return null;mammoth.GetComponent<NavMeshAgent>().enabled=false;
            Enter(mammoth,WinterMonsterEnemy.Phase.Warning);yield return new WaitForSeconds(.65f);Shot("MammothReady");Check(hp.Current==1000,"Mammoth frost preparation is harmless");
            Enter(mammoth,WinterMonsterEnemy.Phase.Attack);yield return new WaitForSeconds(.45f);Shot("MammothSweep");Check(hp.Current==999&&player.IsKnockedBack&&player.GetComponent<Rigidbody>().linearVelocity.y>4,"Mammoth launches player upward");Check(mammoth.transform.position.z>.4f,"Mammoth makes short forward step");
            yield return new WaitForSeconds(.5f);Check(player.transform.position.z>4,"Mammoth launches player far away");Place(new Vector3(0,0,-2));Enter(mammoth,WinterMonsterEnemy.Phase.Attack);yield return new WaitForSeconds(.6f);Check(hp.Current==1000,"Mammoth attack does not hit behind");
            var spirit=Spawn("SnowSpirit");Place(new Vector3(0,0,3));yield return null;spirit.GetComponent<NavMeshAgent>().enabled=false;
            Enter(spirit,WinterMonsterEnemy.Phase.Warning);yield return new WaitForSeconds(.5f);Shot("SpiritWarning");Enter(spirit,WinterMonsterEnemy.Phase.Attack);yield return new WaitForSeconds(1.1f);
            var patch=FindFirstObjectByType<WinterIcePatch>();Check(patch&&hp.Current==1000&&Mathf.Approximately(patch.Radius,5.4f),"Spirit creates doubled-radius ice without direct damage");Shot("SpiritIce");
            Vector3 oldVelocity=Vector3.right*4;Vector3 onIce=WinterIcePatch.ApplySurface(patch.transform.position,oldVelocity,Vector3.zero,.02f);
            Check(onIce.x>3.8f,"Ice preserves momentum during braking");Check(WinterIcePatch.ApplySurface(patch.transform.position+Vector3.right*8,oldVelocity,Vector3.zero,.02f)==Vector3.zero,"Leaving ice immediately restores normal steering");
            Check(WinterIcePatch.ApplySurface(patch.transform.position+Vector3.up*2,oldVelocity,Vector3.zero,.02f)==Vector3.zero,"Airborne player unaffected by ice");
            Set(spirit,"nextAttack",Time.time+100);yield return new WaitForSeconds(5.6f);Check(!FindFirstObjectByType<WinterIcePatch>(),"Ice expires without permanent movement debuff");
            System.IO.File.WriteAllLines("Builds/VfxValidation/WinterValidation.txt",checks);Debug.Log("Winter validation complete: "+string.Join("; ",checks));
        }
    }
}
#endif
