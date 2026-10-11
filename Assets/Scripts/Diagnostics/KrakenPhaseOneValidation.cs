#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace DogLife
{
    // Explicitly started in play mode by the editor; never attached to shipping scenes.
    public sealed class KrakenPhaseOneValidation:MonoBehaviour
    {
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        readonly List<string> report=new List<string>();KrakenPhaseOne boss;PlayerMotor player;CombatHealth hp;Rigidbody rb;
        Vector3 center;public bool Finished{get;private set;}
        void Check(bool value,string label){report.Add((value?"PASS ":"FAIL ")+label);Debug.Log(report[report.Count-1]);Save();}
        void Save(){System.IO.File.WriteAllLines("C:/unity/DogLife/ArtSource/Kraken/NaturalRig/phase-one-validation.txt",report);}
        object Field(string name)=>typeof(KrakenPhaseOne).GetField(name,Flags).GetValue(boss);
        void ScheduleOff(){foreach(var name in new[]{"nextSlam","nextCapture","nextSweep"})typeof(KrakenPhaseOne).GetField(name,Flags).SetValue(boss,Time.time+10000);}
        IEnumerator Pattern(string name)=>(IEnumerator)typeof(KrakenPhaseOne).GetMethod(name,Flags).Invoke(boss,null);
        void Place(Vector3 point){rb.isKinematic=false;rb.useGravity=true;rb.position=point;player.transform.position=point;rb.linearVelocity=Vector3.zero;player.MoveInput=Vector2.zero;player.readInput=false;hp.invulnerable=false;hp.Restore();Physics.SyncTransforms();}
        IEnumerator Start()
        {
            var streaming=FindFirstObjectByType<ForestRoomStreaming>();while(!streaming.Ready)yield return null;streaming.StopAllCoroutines();streaming.enabled=false;
            foreach(var enemy in FindObjectsByType<CombatHealth>(FindObjectsSortMode.None))if(!enemy.player)enemy.gameObject.SetActive(false);
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/SeasonRooms/Summer/Summer_Room04.unity",LoadSceneMode.Additive);
            ForestRoom room=null;foreach(var r in FindObjectsByType<ForestRoom>(FindObjectsInactive.Include,FindObjectsSortMode.None))if(r.gameObject.scene.name=="Summer_Room04")room=r;
            room.BeginEncounter(false);yield return null;yield return null;boss=FindFirstObjectByType<KrakenPhaseOne>();player=boss.Player;hp=player.GetComponent<CombatHealth>();rb=player.GetComponent<Rigidbody>();hp.maxHealth=1000;hp.Restore();center=new Vector3(boss.Arena.center.x,.9f,0);
            Check(boss.State==KrakenPhaseOne.EncounterState.Waiting&&boss.Slots.Count==5,"Outside room: waiting and exactly five slots");
            Place(new Vector3(room.entryX+6,.9f,0));room.BeginEncounter();float deadline=Time.time+25;while(boss.State!=KrakenPhaseOne.EncounterState.Combat&&Time.time<deadline)yield return null;
            Check(boss.State==KrakenPhaseOne.EncounterState.Combat,"Entry cinematic completes and combat begins");ScheduleOff();
            var striker=boss.Slots[0];Place(striker.transform.position+Vector3.back*4+Vector3.up);yield return new WaitForFixedUpdate();float slamBefore=hp.Current;
            striker.StartCoroutine((IEnumerator)typeof(KrakenPhaseOne).GetMethod("Slam",Flags).Invoke(boss,new object[]{striker}));yield return new WaitForSeconds(1);
            Check(striker.Warning.Mode==KrakenPhaseWarning.Shape.Rectangle&&striker.Warning.Progress>0&&striker.Warning.Progress<1,"Slam fills entire rectangular warning during windup");yield return new WaitForSeconds(1.5f);Check(hp.Current==slamBefore-1,"Slam hits player inside displayed rectangle exactly once");yield return new WaitForSeconds(3);ScheduleOff();
            Place(striker.transform.position+Vector3.back*4+Vector3.up);yield return new WaitForFixedUpdate();slamBefore=hp.Current;striker.StartCoroutine((IEnumerator)typeof(KrakenPhaseOne).GetMethod("Slam",Flags).Invoke(boss,new object[]{striker}));yield return new WaitForSeconds(.7f);Place(striker.transform.position+Vector3.right*7+Vector3.up);yield return new WaitForSeconds(5);Check(hp.Current==slamBefore,"Leaving locked slam rectangle avoids impact");ScheduleOff();
            Place(center+new Vector3(-7,0,0));float before=hp.Current;boss.StartCoroutine(Pattern("Capture"));yield return new WaitForSeconds(.6f);Place(center+new Vector3(7,0,0));yield return new WaitForSeconds(6);
            Check(!(bool)Field("captured")&&hp.Current==before,"Capture warning position locked; moving out avoids damage");ScheduleOff();
            Place(center);yield return new WaitForFixedUpdate();boss.StartCoroutine(Pattern("Capture"));deadline=Time.time+4;while(!(bool)Field("captured")&&Time.time<deadline)yield return null;Check((bool)Field("captured")&&!player.enabled,"Capture connects and locks movement");deadline=Time.time+4;while((bool)Field("captured")&&Time.time<deadline)yield return null;Check(!(bool)Field("captured")&&player.enabled&&!rb.isKinematic&&rb.linearVelocity.y>0,"Capture throws and restores dynamic body");yield return new WaitForSeconds(3);ScheduleOff();
            Place(center+Vector3.forward*6);yield return new WaitForFixedUpdate();boss.StartCoroutine(Pattern("Sweep"));yield return new WaitForSeconds(4.65f);before=hp.Current;player.RequestJump();yield return new WaitForSeconds(1.7f);Check(hp.Current==before,"Real PlayerMotor jump avoids moving low sweep");player.ReleaseJump();yield return new WaitForSeconds(3.5f);ScheduleOff();
            Place(center+Vector3.forward*6);before=hp.Current;boss.StartCoroutine(Pattern("Sweep"));yield return new WaitForSeconds(6.3f);Check(hp.Current<before&&hp.Current>=before-1,"Grounded sweep hits exactly once");yield return new WaitForSeconds(3.5f);ScheduleOff();
            var first=boss.Slots[0];Vector3 old=first.transform.position;first.Health.Damage(10000);first.Health.Damage(10000);Check(boss.Kills==1,"Repeated lethal damage counts one kill");yield return new WaitForSeconds(7);Check(boss.Slots.Count==5&&first.Health.Current>0&&Vector3.Distance(old,first.transform.position)>3,"Dead slot respawns elsewhere without exceeding five");ScheduleOff();
            Place(center);yield return new WaitForFixedUpdate();boss.StartCoroutine(Pattern("Capture"));deadline=Time.time+4;while(!(bool)Field("captured")&&Time.time<deadline)yield return null;
            for(int i=1;i<5;i++)boss.Slots[i].Health.Damage(10000);Check(boss.Kills==5&&boss.State==KrakenPhaseOne.EncounterState.Transition,"Fifth kill immediately enters transition");Check(!(bool)Field("captured")&&player.enabled&&!rb.isKinematic,"Phase transition safely releases an active capture");
            yield return new WaitForSeconds(1);bool damageOff=true;foreach(var t in boss.GetComponentsInChildren<KrakenPhaseTentacle>())damageOff&=!t.AttackCollider.enabled;Check(damageOff,"All attack colliders disabled during transition");
            float initialY=player.transform.position.y;yield return new WaitForSeconds(4);var water=boss.GetComponent<KrakenWaterVolume>();float intermediate=water.Height;yield return new WaitForSeconds(10);
            var swim=player.GetComponent<PlayerWaterMovement>();Check(boss.State==KrakenPhaseOne.EncounterState.Underwater&&water.Height>intermediate&&swim.Submerged,"Continuous water rise reaches underwater test state");Check(player.transform.position.y>initialY&&player.enabled&&!hp.invulnerable,"Player floats after water contact, cinematic input/invulnerability restored");
            Check(Camera.main.transform.position.y<water.Height,"Gameplay camera returns below water surface");
            player.readInput=true;Cursor.lockState=CursorLockMode.Locked;Check(player.RequestDash(),"Underwater dash remains available");player.readInput=false;yield return new WaitForSeconds(.4f);
            // ResolveVelocity is tested with explicit held input so Editor focus cannot release Space.
            player.readInput=true;rb.linearVelocity=Vector3.zero;swim.AscendHeld=true;var ascending=swim.ResolveVelocity(Vector3.zero);Check(ascending.y>0&&ascending.y<1,"Held ascent accelerates gradually from rest");swim.AscendHeld=false;player.readInput=false;
            rb.position=new Vector3(center.x,5,0);player.transform.position=rb.position;rb.linearVelocity=Vector3.zero;var look=player.movementCamera.rotation;player.movementCamera.rotation=Quaternion.Euler(60,0,0);player.MoveInput=Vector2.up;yield return new WaitForSeconds(1);Check(rb.linearVelocity.y<-.1f,"Looking down and moving forward descends");player.MoveInput=Vector2.zero;player.movementCamera.rotation=look;yield return new WaitForSeconds(1);Check(rb.linearVelocity.y<0&&rb.linearVelocity.y>-.7f,"Released input settles into gentle sinking");
            rb.position=new Vector3(boss.Arena.max.x-.05f,5,0);rb.linearVelocity=Vector3.zero;var bounded=swim.ResolveVelocity(Vector3.right*100);Check(rb.position.x+bounded.x*Time.fixedDeltaTime<=boss.Arena.max.x+.001f,"Swim motion cannot cross arena boundary");
            boss.CancelEncounter();Check(!swim.Submerged&&rb.useGravity&&!hp.invulnerable&&boss.State==KrakenPhaseOne.EncounterState.Stopped,"Cancellation restores gravity, input ownership and underwater state");
            hp.FatalDamage();yield return null;Check(!swim.Submerged&&!hp.invulnerable&&!player.enabled,"Player death retains death lock and clears water state");
            Finished=true;Save();Debug.Log("Kraken phase-one validation finished");
        }
    }
}
#endif
