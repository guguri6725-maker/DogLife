#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
namespace DogLife
{
    // Explicit editor-only bot run. Records failures instead of forcing kills or reporting estimated clears.
    [DefaultExecutionOrder(-1000)]
    public sealed class WeaponBalancePlaytest:MonoBehaviour
    {
        public string Status {get;private set;}
        public bool Completed {get;private set;}
        public int Season,Room,StopAfterRooms=1;
        public bool Endurance;
        public bool AppendResults;
        public float Elapsed;
        const string Folder="Builds/WeaponBalancePlaytest";
        readonly List<string> rows=new List<string>();
        string checkpoint;bool hadCheckpoint,background,saved;
        readonly Dictionary<string,int> memories=new Dictionary<string,int>();
        float lastDash,nextInput;Vector3 lastPosition;float stuckSince;
        void Awake(){DontDestroyOnLoad(gameObject);}
        void FixedUpdate(){if(!Completed&&Endurance){var m=FindFirstObjectByType<PlayerMotor>();if(m){var w=m.GetComponent<PlayerWaterMovement>();if(w&&w.Submerged&&!SkillWindow.BlocksGameplayInput)m.readInput=true;}}}
        void Update(){if(!Completed){var m=FindFirstObjectByType<PlayerMotor>();if(m)m.readInput=false;}}
        void Save(){File.WriteAllLines(Folder+(Endurance?"/endurance.csv":"/rooms.csv"),rows,new System.Text.UTF8Encoding(true));}
        IEnumerator Wait(Func<bool> f,float seconds){float end=Time.realtimeSinceStartup+seconds;while(!f()&&Time.realtimeSinceStartup<end)yield return null;}
        IEnumerator Start()
        {
            Directory.CreateDirectory(Folder);background=Application.runInBackground;Application.runInBackground=true;
            hadCheckpoint=PlayerPrefs.HasKey(SeasonRunSave.Key);checkpoint=PlayerPrefs.GetString(SeasonRunSave.Key,"");
            var library=Resources.Load<StoryLibrary>("Story/StoryLibrary");
            if(library)foreach(var chapter in library.chapters){var key="DogLife.Memories.v1."+chapter.id;memories[key]=PlayerPrefs.HasKey(key)?PlayerPrefs.GetInt(key):-1;}
            saved=true;if(Endurance&&File.Exists(Folder+"/endurance.csv"))rows.AddRange(File.ReadAllLines(Folder+"/endurance.csv"));
            else if(AppendResults&&File.Exists(Folder+"/rooms.csv"))rows.AddRange(File.ReadAllLines(Folder+"/rooms.csv"));
            else rows.Add("Season,Room,Type,Result,CombatSeconds,SetupSeconds,InitialEnemyHP,RemainingEnemyHP,PlayerHP,Method");Save();
            int measured=0;
            for(int season=Season;season<4;season++)
            {
                int roomCount=Endurance?new[]{5,9,9,11}[season]:0;
                for(int index=season==Season?Room:0;roomCount==0||index<roomCount;index++)
                {
                    if(Endurance&&File.ReadAllLines(Folder+"/rooms.csv").Any(line=>line.StartsWith(season+","+(index+1)+",Normal,CLEAR,")))continue;
                    Status="Loading season "+season+" room "+(index+1);
                    Time.timeScale=1;float loading=Time.realtimeSinceStartup;
                    yield return SceneManager.LoadSceneAsync(SeasonRunSave.Scenes[season]);
                    yield return Wait(()=>ForestRoomStreaming.Instance&&ForestRoomStreaming.Instance.Ready,120);
                    var stream=ForestRoomStreaming.Instance;if(!stream||!stream.Ready){Status="Load failed";Restore();yield break;}
                    roomCount=stream.roomScenePaths.Length;
                    if(index>0){var method=typeof(ForestRoomStreaming).GetMethod("JumpToSummerBoss",BindingFlags.NonPublic|BindingFlags.Instance);yield return stream.StartCoroutine((IEnumerator)method.Invoke(stream,new object[]{index}));}
                    yield return Wait(()=>stream.Current&&stream.Current.Enemies!=null,30);
                    var room=stream.Current;if(!room||room.Enemies==null){Status="Room missing";Restore();yield break;}
                    var motor=stream.director.hud.motor;var player=stream.director.hud.health;
                    var inv=motor.GetComponent<PlayerInventory>();var sword=UnityEditor.AssetDatabase.LoadAssetAtPath<InventoryWeapon>("Assets/Data/Weapons/Dog/Synthesized/Sword.asset");
                    inv.RestoreRunInventory(new[]{sword},0);player.RestoreCheckpoint(Endurance?1000:5,Endurance?1000:5);motor.dashCooldown=1;
                    motor.readInput=false;motor.GetComponent<PlayerWeapons>().readInput=false;
                    var camera=Camera.main?Camera.main.GetComponent<PlayerCamera>():null;if(camera)camera.readInput=false;
                    float setup=Time.realtimeSinceStartup-loading;float initial=room.Enemies.Where(h=>h).Sum(h=>h.Current);
                    float begin=Time.realtimeSinceStartup;lastDash=Time.time;stuckSince=Time.time;lastPosition=motor.transform.position;
                    bool boss=index==roomCount-1;float limit=boss?900:240;
                    while(room&&room.Enemies.Any(h=>h&&!h.Dead)&&!player.Dead&&Time.realtimeSinceStartup-begin<limit)
                    {
                        if(Endurance&&player.maxHealth<1000&&!player.RecoveryBlocked)
                            player.RestoreCheckpoint(1000-(player.maxHealth-player.Current),1000);
                        Elapsed=Time.realtimeSinceStartup-begin;Status="Season "+season+" room "+(index+1)+" "+Elapsed.ToString("F1")+"s HP "+player.Current;
                        Bot(motor,room);yield return null;
                    }
                    motor.MoveInput=Vector2.zero;
                    float remaining=room.Enemies.Where(h=>h&&!h.Dead).Sum(h=>h.Current);
                    string result=player.Dead?"DEATH":remaining<=0?"CLEAR":"TIMEOUT";
                    rows.Add(string.Join(",",new[]{season.ToString(),(index+1).ToString(),boss?"Boss":"Normal",result,(Time.realtimeSinceStartup-begin).ToString("F2",System.Globalization.CultureInfo.InvariantCulture),setup.ToString("F2",System.Globalization.CultureInfo.InvariantCulture),initial.ToString("F2",System.Globalization.CultureInfo.InvariantCulture),remaining.ToString("F2",System.Globalization.CultureInfo.InvariantCulture),player.Current.ToString(),Endurance?"1000 HP endurance control; normal damage and knockback; not real clear time":"Normal damage; 5 HP; synthesized sword; automated movement"}));Save();
                    yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Folder+"/"+(Endurance?"endurance_":"")+"season"+season+"_room"+(index+1)+".png");yield return null;
                    measured++;if(StopAfterRooms>0&&measured>=StopAfterRooms){Completed=true;Status="Complete";Restore();yield break;}
                }
            }
            Completed=true;Status="Complete";Restore();
        }
        void Bot(PlayerMotor motor,ForestRoom room)
        {
            if(Time.timeScale<=0||SkillWindow.BlocksGameplayInput)return;
            motor.readInput=false;
            var candidates=room.Enemies.Where(h=>h&&!h.Dead&&!h.invulnerable).ToList();
            var phase=room.GetComponentInChildren<KrakenPhaseOne>(true);
            if(phase&&phase.State==KrakenPhaseOne.EncounterState.Combat)candidates=phase.Slots.Where(t=>t&&t.Health&&!t.Health.Dead).Select(t=>t.Health).ToList();
            var target=candidates.OrderBy(h=>Vector3.Distance(h.transform.position,motor.transform.position)).FirstOrDefault();
            var activeCore=FindFirstObjectByType<FrozenKnightSwordCore>();
            bool solvingCore=activeCore&&activeCore.HitArea.enabled&&!activeCore.Broken;
            if(!target&&solvingCore){var owner=FindFirstObjectByType<FrozenKnightBoss>();if(owner)target=owner.GetComponent<CombatHealth>();}
            if(!target){motor.MoveInput=Vector2.zero;return;}
            Vector3 pos=motor.transform.position,aim=target.transform.position;
            var cols=target.GetComponentsInChildren<Collider>().Where(c=>c.enabled&&MonsterHurtbox.Accepts(c)).ToArray();
            if(cols.Length>0)aim=cols.Select(c=>c.ClosestPoint(pos+Vector3.up*.8f)).OrderBy(v=>(v-pos).sqrMagnitude).First();
            Vector3 delta=Vector3.ProjectOnPlane(aim-pos,Vector3.up);float distance=delta.magnitude;
            var camera=Camera.main?Camera.main.GetComponent<PlayerCamera>():null;
            if(camera){camera.readInput=false;camera.yaw=Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg;camera.pitch=12;}
            motor.GetComponent<PlayerWeapons>().AimAt(aim);
            Vector3 direction=delta.normalized;
            var core=FindFirstObjectByType<FrozenKnightSwordCore>();
            var knight=target.GetComponent<FrozenKnightBoss>();
            if(core&&core.HitArea.enabled&&!core.Broken){var owner=FindFirstObjectByType<FrozenKnightBoss>();if(owner){aim=core.Center+Vector3.ProjectOnPlane(core.Center-owner.transform.position,Vector3.up).normalized*3;delta=Vector3.ProjectOnPlane(aim-pos,Vector3.up);distance=delta.magnitude;direction=delta.normalized;}}
            var swim=motor.GetComponent<PlayerWaterMovement>();
            if(!swim||!swim.Submerged){NavMeshHit near;var path=new NavMeshPath();if(distance>2&&NavMesh.SamplePosition(aim,out near,8,NavMesh.AllAreas)&&NavMesh.CalculatePath(pos,near.position,NavMesh.AllAreas,path)&&path.corners.Length>1)direction=Vector3.ProjectOnPlane(path.corners[1]-pos,Vector3.up).normalized;}
            else swim.AscendHeld=aim.y>pos.y+1;
            Vector3 f=motor.movementCamera?Vector3.ProjectOnPlane(motor.movementCamera.forward,Vector3.up).normalized:Vector3.forward;
            Vector3 right=Vector3.Cross(Vector3.up,f);
            if(solvingCore&&distance<.5f)direction=Vector3.zero;
            else if(!solvingCore&&distance<1.5f)direction=Vector3.Cross(Vector3.up,delta.normalized)*.6f;
            motor.MoveInput=new Vector2(Vector3.Dot(direction,right),Vector3.Dot(direction,f));
            if(!solvingCore&&distance<3&&Time.time-lastDash>2.4f){motor.MoveInput=new Vector2(1,0);if(motor.RequestDash())lastDash=Time.time;}
            if(!solvingCore&&distance<2.4f&&Time.time>=nextInput){var combat=motor.GetComponent<BasicWeaponCombat>();combat.PressAttack();combat.ReleaseAttack();nextInput=Time.time+.05f;}
            if((pos-lastPosition).sqrMagnitude>.25f){lastPosition=pos;stuckSince=Time.time;}else if(Time.time-stuckSince>3){motor.RequestJump();stuckSince=Time.time;}
        }
        void Restore()
        {
            Application.runInBackground=background;
            if(!saved)return;saved=false;
            if(hadCheckpoint)PlayerPrefs.SetString(SeasonRunSave.Key,checkpoint);else PlayerPrefs.DeleteKey(SeasonRunSave.Key);
            foreach(var p in memories){if(p.Value<0)PlayerPrefs.DeleteKey(p.Key);else PlayerPrefs.SetInt(p.Key,p.Value);}PlayerPrefs.Save();
        }
        void OnDestroy(){Restore();}
    }
}
#endif
