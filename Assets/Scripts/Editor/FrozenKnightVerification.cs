#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
namespace DogLife.EditorTools
{
    // 배치된 보스와 플레이어로만 실행하는 수동 검증 도구입니다. 씬/프리팹 설정은 저장하지 않습니다.
    public static class FrozenKnightVerification
    {
        static IEnumerator routine;
        static readonly List<string> results=new List<string>();
        static FrozenKnightBoss boss;
        static CombatHealth hp,playerHealth;
        static Transform player;
        static float deadline;
        public static string Status {get;private set;}="Not started";
        static readonly BindingFlags Fields=BindingFlags.Instance|BindingFlags.NonPublic;
        static void Set(object target,string field,object value)=>target.GetType().GetField(field,Fields).SetValue(target,value);
        static T Get<T>(object target,string field)=>(T)target.GetType().GetField(field,Fields).GetValue(target);
        static void Check(bool condition,string name){results.Add((condition?"PASS ":"FAIL ")+name);if(!condition)Debug.LogError("Frozen Knight verification: "+name);}
        public static string Start()
        {
            if(!EditorApplication.isPlaying)return "Requires play mode with an existing FrozenKnight and Player";
            boss=UnityEngine.Object.FindFirstObjectByType<FrozenKnightBoss>();var motor=UnityEngine.Object.FindFirstObjectByType<PlayerMotor>();
            if(!boss||!motor)return "Missing scene objects";
            hp=boss.GetComponent<CombatHealth>();player=motor.transform;playerHealth=motor.GetComponent<CombatHealth>();
            results.Clear();playerHealth.maxHealth=100;playerHealth.Restore();playerHealth.invulnerable=true;
            Set(boss,"engaged",true);Set(boss,"nextAttack",float.PositiveInfinity);Set(boss,"globalCooldown",float.PositiveInfinity);
            Time.timeScale=1;Status="Running";deadline=Time.realtimeSinceStartup+240;routine=Run();EditorApplication.update-=Tick;EditorApplication.update+=Tick;return Status;
        }
        static void Tick()
        {
            try
            {
                if(!EditorApplication.isPlaying||Time.realtimeSinceStartup>deadline)throw new Exception("Verification stopped or timed out");
                if(routine.MoveNext())return;
                Status=results.Any(r=>r.StartsWith("FAIL"))?"FAILED":"PASSED";Finish();
            }
            catch(Exception e){results.Add("FAIL "+e);Status="FAILED";Finish();}
        }
        static void Finish(){EditorApplication.update-=Tick;Directory.CreateDirectory("Temp/FrozenKnight");File.WriteAllLines("Temp/FrozenKnight/verification.txt",results);}
        static IEnumerator Run()
        {
            while(boss.State!=FrozenKnightBoss.CombatState.Idle)yield return null;
            Check(boss.GetComponent<NavMeshAgent>().isOnNavMesh,"Boss uses the existing baked NavMesh");
            var sword=boss.GetComponentsInChildren<Transform>().First(t=>t.name=="Sword");var hand=sword.parent.parent;
            Check(Vector3.Distance(hand.position,sword.TransformPoint(new Vector3(0,.85f,0)))<.01f,"Sword handle stays in the right hand");
            var area=Get<FrozenKnightDamageArea>(boss,"radialArea");var collider=player.GetComponent<Collider>();
            playerHealth.invulnerable=false;Set(playerHealth,"invulnerableUntil",0f);float before=playerHealth.Current;
            area.Arm(boss,1,0,0);area.Touch(collider);Set(playerHealth,"invulnerableUntil",0f);for(int i=0;i<8;i++)area.Touch(collider);
            Check(Mathf.Approximately(playerHealth.Current,before-1),"One hit per attack despite repeated player contacts");area.Disarm();
            Set(playerHealth,"invulnerableUntil",0f);before=playerHealth.Current;area.Arm(boss,1,0,0,null,1);area.Touch(collider);Set(playerHealth,"invulnerableUntil",0f);area.Touch(collider);
            Check(Mathf.Approximately(playerHealth.Current,before-1),"Damage tick cannot repeat immediately");float until=Time.time+1.1f;while(Time.time<until)yield return null;area.Touch(collider);
            Check(Mathf.Approximately(playerHealth.Current,before-2),"Damage tick can repeat after its interval");area.Disarm();playerHealth.invulnerable=true;
            for(int p=0;p<5;p++)
            {
                if(p==(int)FrozenKnightBoss.Pattern.IceBlade){Check(!boss.Begin(FrozenKnightBoss.Pattern.IceBlade),"Removed ice blade cannot be started");continue;}
                var pattern=(FrozenKnightBoss.Pattern)p;Check(boss.Begin(pattern),"Start phase 1 "+pattern);bool warning=false,active=false;bool shot=false;
                while(boss.State!=FrozenKnightBoss.CombatState.Idle)
                {warning|=boss.State==FrozenKnightBoss.CombatState.Warning;active|=boss.State==FrozenKnightBoss.CombatState.Active;if(active&&!shot){Snapshot(pattern.ToString());shot=true;}yield return null;}
                Check(warning&&active,"Warning then active and recovery: "+pattern);Check(!boss.GetComponentsInChildren<FrozenKnightDamageArea>().Any(a=>a.Armed),"All hitboxes disarmed after "+pattern);
            }
            Check(boss.Phase1SpecialUsed&&!boss.Begin(FrozenKnightBoss.Pattern.SwordFury),"Phase 1 special only once");
            int deaths=0;hp.Died+=()=>deaths++;hp.Damage(99999);Check(!hp.Dead&&hp.invulnerable&&deaths==0,"Phase 1 lethal damage defers death and enables invulnerability");hp.Damage(99999);
            var horse=Get<Transform>(boss,"horseRoot");var rider=Get<Transform>(boss,"riderRoot");bool approached=false,mountedAfterStop=true;float farthest=0;
            while(boss.State==FrozenKnightBoss.CombatState.Transition)
            {
                if(horse.gameObject.activeSelf){float distance=Vector3.Distance(horse.position,boss.transform.position);farthest=Mathf.Max(farthest,distance);if(distance>3)approached=true;if(rider.position.y>boss.transform.position.y+.5f&&distance>.15f)mountedAfterStop=false;}
                yield return null;
            }
            Check(approached&&farthest>3&&mountedAfterStop,"Horse runs in from a distance before rider mounts");
            Check(boss.PhaseNumber==2&&Mathf.Approximately(hp.Current,450)&&!hp.invulnerable&&deaths==0,"Separate phase 2 HP restored after mounting");Snapshot("MountedIdle");
            Check(boss.SwordLeftInGround&&boss.IceFieldActive,"Mounted phase leaves sword and slippery field on ground");
            Vector3 planted=Get<Transform>(boss,"swordRoot").position;
            for(int p=5;p<=10;p++)
            {
                if(p==(int)FrozenKnightBoss.Pattern.RideSlash){Check(!boss.Begin(FrozenKnightBoss.Pattern.RideSlash),"Removed mounted side slash cannot be started");continue;}
                var pattern=(FrozenKnightBoss.Pattern)p;Check(boss.Begin(pattern),"Start phase 2 "+pattern);bool warning=false,active=false,shot=false;Vector3 direction=Vector3.zero;
                while(boss.State!=FrozenKnightBoss.CombatState.Idle)
                {
                    warning|=boss.State==FrozenKnightBoss.CombatState.Warning;
                    if(boss.State==FrozenKnightBoss.CombatState.Active){active=true;if(!shot){direction=boss.Forward;Snapshot(pattern.ToString());shot=true;}if(pattern==FrozenKnightBoss.Pattern.Charge)Check(Vector3.Angle(direction,boss.Forward)<.01f,"Charge direction stays fixed");}
                    yield return null;
                }
                Check(warning&&active,"Warning then active and recovery: "+pattern);
                Check(Vector3.Distance(planted,Get<Transform>(boss,"swordRoot").position)<.01f,"Planted sword stays fixed during mounted "+pattern);
            }
            Check(boss.Phase2SpecialUsed&&!boss.Begin(FrozenKnightBoss.Pattern.MountedFury),"Phase 2 special only once");
            hp.Damage(99999);Check(hp.Dead&&deaths==1&&!boss.gameObject.activeSelf,"Phase 2 death fires once and deactivates boss");
            Check(!boss.IceFieldActive&&!boss.OuterFuryActive,"Boss death clears slippery field and outer danger");
        }
        static void Snapshot(string name)
        {
            var camera=GameObject.Find("BossReviewCamera")?.GetComponent<Camera>()??Camera.main;if(!camera)return;
            Vector3 originalPosition=camera.transform.position;Quaternion originalRotation=camera.transform.rotation;
            // 임시 렌더 텍스처만 사용하며 프로젝트 에셋이나 카메라 설정을 저장하지 않습니다.
            camera.transform.position=boss.transform.position+boss.transform.TransformDirection(new Vector3(5,4,8));camera.transform.LookAt(boss.transform.position+Vector3.up*2);
            var rt=new RenderTexture(1100,700,24);var old=camera.targetTexture;var previous=RenderTexture.active;
            camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var image=new Texture2D(1100,700,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1100,700),0,0);image.Apply();
            Directory.CreateDirectory("Temp/FrozenKnight");File.WriteAllBytes("Temp/FrozenKnight/"+name+".png",image.EncodeToPNG());camera.targetTexture=old;RenderTexture.active=previous;camera.transform.SetPositionAndRotation(originalPosition,originalRotation);UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(rt);
        }
        public static IEnumerator VerifySlowDash(PlayerMotor motor)
        {
            // Editor 콜백 대신 실제 FixedUpdate 뒤에서 속도를 측정합니다.
            var body=motor.GetComponent<Rigidbody>();motor.readInput=false;motor.MoveInput=Vector2.up;
            Set(motor,"nextDash",0f);Set(motor,"movementSlowUntil",0f);motor.ApplyMovementSlow(.6f,1.2f);
            yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();
            var lines=new List<string>{"slow="+Vector3.ProjectOnPlane(body.linearVelocity,Vector3.up).magnitude+" expected="+motor.moveSpeed*.6f};
            lines.Add("request="+motor.RequestDash());
            yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();
            lines.Add("dash="+Vector3.ProjectOnPlane(body.linearVelocity,Vector3.up).magnitude+" expected="+motor.dashSpeed+" active="+motor.IsDashing);
            yield return new WaitForSeconds(1.4f);yield return new WaitForFixedUpdate();
            lines.Add("expired="+Vector3.ProjectOnPlane(body.linearVelocity,Vector3.up).magnitude+" expected="+motor.moveSpeed);
            motor.MoveInput=Vector2.zero;File.WriteAllLines("Temp/FrozenKnight/SlowDashPhysics.txt",lines);
        }
    }
}
#endif
