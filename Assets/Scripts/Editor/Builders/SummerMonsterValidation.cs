using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.AI.Navigation;
namespace DogLife.EditorTools
{
    // 임시 검사 장면과 실제 플레이 검증. 게임 빌드에는 포함되지 않습니다.
    public static class SummerMonsterValidation
    {
        public static void CreateArena()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Validation floor";floor.layer=8;floor.transform.position=Vector3.down*.25f;floor.transform.localScale=new Vector3(30,.5f,30);
            var mat=new Material(Shader.Find("Standard"));mat.color=new Color(.22f,.28f,.30f);floor.GetComponent<Renderer>().sharedMaterial=mat;
            var nav=floor.AddComponent<NavMeshSurface>();nav.layerMask=1<<8;nav.BuildNavMesh();
            var camera=new GameObject("Main Camera").AddComponent<Camera>();camera.tag="MainCamera";camera.transform.position=new Vector3(7,8,11);camera.transform.LookAt(new Vector3(0,1,0));camera.orthographic=true;camera.orthographicSize=5;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.08f,.13f,.18f);
            var light=new GameObject("Sun").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1;light.transform.rotation=Quaternion.Euler(45,-30,0);RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.4f,.4f,.4f);
            var player=new GameObject("Validation player");player.transform.position=new Vector3(0,0,5);var capsule=player.AddComponent<CapsuleCollider>();capsule.center=Vector3.up;capsule.height=2;capsule.radius=.4f;
            var rb=player.AddComponent<Rigidbody>();rb.constraints=RigidbodyConstraints.FreezeRotation;var motor=player.AddComponent<PlayerMotor>();motor.readInput=false;var health=player.AddComponent<CombatHealth>();health.player=true;health.maxHealth=1000;
        }
        public static void Run(){new GameObject("Summer validation runner").AddComponent<SummerMonsterValidationRunner>();}
    }
    public sealed class SummerMonsterValidationRunner:MonoBehaviour
    {
        readonly List<string> checks=new List<string>();PlayerMotor player;CombatHealth hp;SummerMonsterEnemy enemy;
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        static void Set(object o,string n,object v){o.GetType().GetField(n,Flags).SetValue(o,v);}
        static void Enter(SummerMonsterEnemy e,SummerMonsterEnemy.Phase p){typeof(SummerMonsterEnemy).GetMethod("Enter",Flags).Invoke(e,new object[]{p});}
        void Check(bool condition,string message){checks.Add((condition?"PASS ":"FAIL ")+message);if(!condition)Debug.LogError("Summer validation: "+message);}
        void Place(Vector3 p){Set(player,"knockbackUntil",0f);player.MoveInput=Vector2.zero;player.GetComponent<Rigidbody>().linearVelocity=Vector3.zero;player.GetComponent<Rigidbody>().position=p;player.transform.position=p;Physics.SyncTransforms();hp.Restore();}
        SummerMonsterEnemy Spawn(string name)
        {
            if(enemy)Destroy(enemy.gameObject);
            var go=Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/monster/"+name+".prefab"));go.name=name;
            enemy=go.GetComponent<SummerMonsterEnemy>();return enemy;
        }
        void Shot(string name){ScreenCapture.CaptureScreenshot("Builds/VfxValidation/"+name+".png");}
        IEnumerator Start()
        {
            System.IO.Directory.CreateDirectory("Builds/VfxValidation");player=FindFirstObjectByType<PlayerMotor>();hp=player.GetComponent<CombatHealth>();hp.Restore();
            var jelly=Spawn("BlueJellyfish");yield return null;jelly.GetComponent<NavMeshAgent>().enabled=false;
            Place(new Vector3(0,0,2));Enter(jelly,SummerMonsterEnemy.Phase.Warning);float before=hp.Current;
            yield return new WaitForSeconds(.5f);Check(hp.Current==before&&jelly.State==SummerMonsterEnemy.Phase.Warning,"Jelly warning does not damage player");Shot("JellyWarning");
            yield return new WaitForSeconds(.65f);Shot("JellyAttack");Check(hp.Current==before-1,"Jelly centered area applies exactly one damage");
            Check(player.IsKnockedBack&&!player.RequestDash(),"Jelly stun blocks movement actions");
            Vector3 stoppedAt=player.transform.position;player.MoveInput=Vector2.right;yield return new WaitForSeconds(.12f);
            Check(Vector3.ProjectOnPlane(player.transform.position-stoppedAt,Vector3.up).magnitude<.03f,"Jelly stun holds horizontal position despite movement input");player.MoveInput=Vector2.zero;
            yield return new WaitForSeconds(.65f);Check(hp.Current==before-1,"Jelly area does not damage repeatedly");
            Check(!player.IsKnockedBack,"Jelly stun expires");
            Place(new Vector3(0,0,5));Enter(jelly,SummerMonsterEnemy.Phase.Attack);before=hp.Current;yield return new WaitForSeconds(.7f);Check(hp.Current==before,"Outside jelly radius remains safe");
            var turtle=Spawn("BoneTortoise");Place(new Vector3(0,0,6));yield return null;turtle.GetComponent<NavMeshAgent>().enabled=false;var th=turtle.GetComponent<CombatHealth>();before=th.Current;th.Damage(10);
            var jaw=Array.Find(turtle.GetComponentsInChildren<Transform>(),bone=>bone.name=="Jaw");Check(jaw&&Vector3.Dot(jaw.position-turtle.transform.position,turtle.AttackForward)>.25f,"Tortoise jaw is on the forward attack side");
            Check(turtle.Defending&&th.Current==before,"Tortoise detects attack and blocks damage");yield return new WaitForSeconds(.6f);Shot("TortoiseGuard");th.Damage(10);Check(th.Current==before,"Tortoise guard blocks subsequent hit");
            yield return new WaitForSeconds(1.5f);Check(!turtle.Defending&&turtle.DefenseRemaining>3,"Tortoise exits guard and starts cooldown");th.Damage(10);Check(th.Current==before-10,"Tortoise takes damage during defense cooldown");
            Set(turtle,"nextDefense",0f);Enter(turtle,SummerMonsterEnemy.Phase.Warning);th.Damage(10);Check(!turtle.Defending,"Tortoise cannot guard during bite preparation");
            Place(new Vector3(.3f,0,1.6f));Enter(turtle,SummerMonsterEnemy.Phase.Attack);before=hp.Current;yield return new WaitForSeconds(.25f);Shot("TortoiseBite");
            Check(player.IsKnockedBack&&Vector3.Dot(player.GetComponent<Rigidbody>().linearVelocity,new Vector3(.3f,0,1.6f))>0,"Tortoise bite pushes away from its contact side");
            yield return new WaitForSeconds(.4f);Check(hp.Current==before-1,"Tortoise bite damage window");
            Place(new Vector3(0,0,1.6f));hp.invulnerable=true;Enter(turtle,SummerMonsterEnemy.Phase.Attack);yield return new WaitForSeconds(.3f);
            Check(!player.IsKnockedBack&&hp.Current==hp.maxHealth,"Invulnerable hit does not apply knockback");hp.invulnerable=false;
            var mermaid=Spawn("CoralMermaid");Place(new Vector3(0,0,1.4f));yield return null;Enter(mermaid,SummerMonsterEnemy.Phase.Dive);before=hp.Current;
            yield return new WaitForSeconds(.3f);Check(!mermaid.GetComponent<Collider>().enabled,"Mermaid disables body collision while diving");Shot("MermaidDive");
            Check(Quaternion.Angle(mermaid.transform.Find("Visual").rotation,Quaternion.LookRotation(mermaid.AttackForward))>15,"Mermaid visibly pitches into the dive");
            float deadline=Time.time+5;while(mermaid.State!=SummerMonsterEnemy.Phase.Warning&&Time.time<deadline)yield return null;
            yield return new WaitForSeconds(.3f);Check(mermaid.State==SummerMonsterEnemy.Phase.Warning&&hp.Current==before,"Mermaid approaches then warns without damage");Shot("MermaidWarning");
            yield return new WaitForSeconds(.55f);Place(new Vector3(0,0,6));yield return new WaitForSeconds(.25f);Shot("MermaidEmerge");yield return new WaitForSeconds(.7f);Check(hp.Current==1000,"Escaping mermaid warning avoids attack");Check(mermaid.GetComponent<Collider>().enabled,"Mermaid restores body collision after surfacing");
            mermaid.GetComponent<NavMeshAgent>().enabled=false;Place(mermaid.transform.position+Vector3.forward*1.4f);Vector3 launchStart=player.transform.position;
            Enter(mermaid,SummerMonsterEnemy.Phase.Attack);yield return new WaitForSeconds(.4f);
            Check(player.IsKnockedBack&&player.GetComponent<Rigidbody>().linearVelocity.y>3,"Mermaid launches player upward");
            yield return new WaitForSeconds(.35f);Check(Vector3.ProjectOnPlane(player.transform.position-launchStart,Vector3.up).magnitude>2,"Mermaid launches player far away");
            var crab=Spawn("HermitCrab");Place(new Vector3(0,0,6));yield return null;crab.GetComponent<NavMeshAgent>().enabled=false;Enter(crab,SummerMonsterEnemy.Phase.Attack);
            yield return new WaitForSeconds(.28f);var bubbles=FindObjectsByType<SummerHomingBubble>(FindObjectsSortMode.None);Check(bubbles.Length==3,"Crab launches exactly three bubbles");Check(bubbles.Length==3&&Vector3.Distance(bubbles[0].transform.position,bubbles[1].transform.position)>.25f,"Bubbles start separated");Shot("CrabBubbles");
            if(bubbles.Length==3){Place(bubbles[0].transform.position-Vector3.up*.8f);Vector3 bubbleStart=player.transform.position;yield return new WaitForSeconds(.08f);Check(FindObjectsByType<SummerHomingBubble>(FindObjectsSortMode.None).Length==2,"Collision removes only the contacted bubble");Check(player.IsKnockedBack&&Vector3.ProjectOnPlane(player.transform.position-bubbleStart,Vector3.up).magnitude<.2f&&hp.Current==hp.maxHealth,"Bubble gives tiny push without health damage");}
            Place(new Vector3(10,0,10));var debuff=player.GetComponent<BubbleDebuff>();Check(debuff&&debuff.Active,"Bubble applies debuff");
            var fx=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/SummerMonsters/Materials/SummerWater.mat");if(!debuff)debuff=player.gameObject.AddComponent<BubbleDebuff>();debuff.Apply(.3f,.3f,1,fx);yield return new WaitForSeconds(.6f);debuff.Apply(.3f,.3f,1,fx);yield return new WaitForSeconds(.6f);Check(debuff.Active&&Mathf.Approximately(debuff.MoveMultiplier,.7f)&&Mathf.Approximately(debuff.RollMultiplier,.7f),"Reapplying slow refreshes time without stacking");yield return new WaitForSeconds(.5f);Check(!debuff.Active&&debuff.MoveMultiplier==1&&debuff.RollMultiplier==1,"Debuff restores movement and roll multipliers");
            Set(crab,"nextAttack",Time.time+100);Enter(crab,SummerMonsterEnemy.Phase.Idle);Set(crab,"nextAttack",Time.time+100);yield return new WaitForSeconds(5.5f);Check(FindObjectsByType<SummerHomingBubble>(FindObjectsSortMode.None).Length==0,"Escaped bubbles expire");
            System.IO.File.WriteAllLines("Builds/VfxValidation/SummerValidation.txt",checks);Debug.Log("Summer validation complete: "+string.Join("; ",checks));
        }
    }
}
