#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.AI;

namespace DogLife
{
    // Explicitly attached in Play mode only. Never auto-starts in shipped games.
    public sealed class CampaignPlaytest : MonoBehaviour
    {
        public string Status { get; private set; }
        public bool Completed { get; private set; }
        public int StartSeason;
        public int StartRoom;
        string[] carriedWeapons;string carriedEquipped;int carriedGold;float carriedKarma;
        readonly List<string> report=new List<string>();
        readonly HashSet<string> errors=new HashSet<string>();
        readonly Dictionary<string,int> memories=new Dictionary<string,int>();
        string checkpoint;bool hadCheckpoint;bool saved;bool background;
        const string Folder="Builds/CampaignPlaytest";
        void Awake(){DontDestroyOnLoad(gameObject);}
        void Record(string text){report.Add(text);File.WriteAllLines(Folder+"/results.txt",report);}
        void Check(string text,bool ok){Record((ok?"PASS ":"FAIL ")+text);}
        void Log(string text,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)if(errors.Add(text))Record("ERROR "+text+"\n"+stack);}
        IEnumerator Start()
        {
            Directory.CreateDirectory(Folder);background=Application.runInBackground;Application.runInBackground=true;
            hadCheckpoint=PlayerPrefs.HasKey(SeasonRunSave.Key);checkpoint=PlayerPrefs.GetString(SeasonRunSave.Key,"");
            foreach(var chapter in Resources.Load<StoryLibrary>("Story/StoryLibrary").chapters){string key="DogLife.Memories.v1."+chapter.id;memories[key]=PlayerPrefs.HasKey(key)?PlayerPrefs.GetInt(key):-1;}
            saved=true;Application.logMessageReceived+=Log;
            yield return Run();
            Completed=true;Status="Complete";Record("COMPLETE; errors="+errors.Count);Restore();
        }
        void Restore()
        {
            Application.logMessageReceived-=Log;Time.timeScale=1;Application.runInBackground=background;
            if(!saved)return;saved=false;
            if(hadCheckpoint)PlayerPrefs.SetString(SeasonRunSave.Key,checkpoint);else PlayerPrefs.DeleteKey(SeasonRunSave.Key);
            foreach(var entry in memories){if(entry.Value<0)PlayerPrefs.DeleteKey(entry.Key);else PlayerPrefs.SetInt(entry.Key,entry.Value);}PlayerPrefs.Save();
        }
        void OnDestroy(){Restore();}
        IEnumerator Wait(Func<bool> predicate,float seconds)
        {float end=Time.realtimeSinceStartup+seconds;while(!predicate()&&Time.realtimeSinceStartup<end)yield return null;}
        IEnumerator Shot(string name){yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Folder+"/"+name+".png");yield return null;}
        void Place(PlayerMotor motor,Vector3 position)
        {var body=motor.GetComponent<Rigidbody>();body.position=position;motor.transform.position=position;if(!body.isKinematic)body.linearVelocity=Vector3.zero;Physics.SyncTransforms();}
        void Configure(ForestRoomStreaming stream)
        {
            var hud=stream.director.hud;hud.health.invulnerable=true;hud.motor.readInput=false;hud.weapons.readInput=false;
            if(Camera.main){var camera=Camera.main.GetComponent<PlayerCamera>();if(camera)camera.readInput=false;}
        }
        void Bot(ForestRoomStreaming stream,CombatHealth target,bool attack)
        {
            var motor=stream.director.hud.motor;var delta=target.transform.position-motor.transform.position;delta.y=0;
            var camera=Camera.main?Camera.main.GetComponent<PlayerCamera>():null;
            if(camera){camera.yaw=Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg;camera.pitch=14;}
            var direction=delta.normalized;var path=new NavMeshPath();
            NavMeshHit near;
            if(NavMesh.SamplePosition(target.transform.position,out near,6,NavMesh.AllAreas)&&NavMesh.CalculatePath(motor.transform.position,near.position,NavMesh.AllAreas,path)&&path.corners.Length>1)
                direction=Vector3.ProjectOnPlane(path.corners[1]-motor.transform.position,Vector3.up).normalized;
            var forward=Vector3.ProjectOnPlane(Camera.main.transform.forward,Vector3.up).normalized;var right=Vector3.Cross(Vector3.up,forward);
            float distance=target.GetComponentsInChildren<Collider>().Where(c=>c.enabled&&!c.isTrigger).Select(c=>Vector3.Distance(c.ClosestPoint(motor.transform.position),motor.transform.position)).DefaultIfEmpty(delta.magnitude).Min();
            motor.MoveInput=distance>1.1f?new Vector2(Vector3.Dot(direction,right),Vector3.Dot(direction,forward)):Vector2.zero;
            if(attack&&distance<3.5f){var combat=motor.GetComponent<BasicWeaponCombat>();combat.PressAttack();combat.ReleaseAttack();}
        }
        void Scan(string label)
        {
            foreach(var renderer in FindObjectsByType<Renderer>(FindObjectsSortMode.None))foreach(var mat in renderer.sharedMaterials)
            {
                if(!mat){Record("FAIL "+label+" missing material: "+renderer.name);continue;}
                string path=UnityEditor.AssetDatabase.GetAssetPath(mat);
                if(path.EndsWith(".fbx")&&!mat.mainTexture&&mat.HasProperty("_Color")&&mat.color==Color.white)
                    Record("VISUAL "+label+" white untextured imported material: "+path+" | "+mat.name);
            }
            var triangles=NavMesh.CalculateTriangulation();int unsupported=0,sampled=0;
            foreach(var point in triangles.vertices.Where((p,i)=>i%3==0))
            {
                if(point.y>2||point.y< -1)continue;
                sampled++;
                if(!Physics.RaycastAll(point+Vector3.up*1.5f,Vector3.down,3,~0,QueryTriggerInteraction.Ignore).Any(h=>h.normal.y>.45f&&!h.collider.GetComponentInParent<CombatHealth>()))unsupported++;
            }
            Record("FLOOR "+label+" unsupported navigation samples="+unsupported+"/"+sampled);
        }
        IEnumerator Run()
        {
            Status="Title and new game";yield return SceneManager.LoadSceneAsync("Assets/Scenes/Title.unity");yield return null;
            var title=FindFirstObjectByType<TitleScreen>();Check("Title present",title);yield return Shot("title");
            title.StartGame();yield return Wait(()=>StoryBook.IsOpen,10);Check("START opens prologue",StoryBook.IsOpen&&StoryBook.Instance.CurrentChapter=="prologue");yield return Shot("prologue");
            yield return new WaitForSecondsRealtime(3);yield return Shot("prologue-visible");
            StoryBook.Instance.Finish();yield return null;if(StoryBook.IsOpen)StoryBook.Instance.Finish();
            if(StartSeason>0){yield return Wait(()=>ForestRoomStreaming.Instance&&ForestRoomStreaming.Instance.Ready,120);yield return SceneManager.LoadSceneAsync(SeasonRunSave.Scenes[StartSeason]);}
            for(int season=StartSeason;season<4;season++)
            {
                string expectedScene=SeasonRunSave.Scenes[season];
                yield return Wait(()=>ForestRoomStreaming.Instance&&ForestRoomStreaming.Instance.gameObject.scene.path==expectedScene&&ForestRoomStreaming.Instance.Ready,120);
                var stream=ForestRoomStreaming.Instance;
                if(!stream||!stream.Ready){Record("FAIL season load "+season);yield break;}
                Configure(stream);var hud=stream.director.hud;string scene=stream.gameObject.scene.name;
                if(carriedWeapons!=null){var inventory=hud.motor.GetComponent<PlayerInventory>();Check(scene+" preserves inventory",carriedWeapons.SequenceEqual(inventory.OwnedWeapons.Select(w=>w.weaponId)));Check(scene+" preserves equipped weapon",inventory.EquippedWeapon&&inventory.EquippedWeapon.weaponId==carriedEquipped);Check(scene+" preserves gold",RunEconomy.Instance.Gold==carriedGold);Check(scene+" preserves karma",Mathf.Approximately(hud.motor.GetComponent<PlayerKarma>().Value,carriedKarma));Check(scene+" saves next season checkpoint",PlayerPrefs.GetString(SeasonRunSave.Key,"").Contains(scene));}
                Check(scene+" correct season progression",stream.gameObject.scene.path==SeasonRunSave.Scenes[season]);
                Check(scene+" starts with 5 HP",Mathf.Approximately(hud.health.Current,5));
                if(season==0){Check("New game dash cooldown 1",Mathf.Approximately(hud.motor.dashCooldown,1));var inv=hud.motor.GetComponent<PlayerInventory>();Check("New game has only basic animal sword",inv.OwnedWeapons.Count==1&&inv.EquippedWeapon&&inv.EquippedWeapon.weaponId=="dog_sword_basic");}
                int firstRoom=season==StartSeason?StartRoom:0;
                if(firstRoom>0){var jump=(IEnumerator)typeof(ForestRoomStreaming).GetMethod("JumpToSummerBoss",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(stream,new object[]{firstRoom});yield return stream.StartCoroutine(jump);}
                for(int roomIndex=firstRoom;roomIndex<stream.roomScenePaths.Length;roomIndex++)
                {
                    Status=scene+" room "+(roomIndex+1)+"/"+stream.roomScenePaths.Length;
                    var room=stream.Current;Configure(stream);Scan(Status);yield return Shot(scene+"_room"+(roomIndex+1));
                    Check(Status+" has enemies",room.Enemies!=null&&room.Enemies.Any(h=>h&&!h.Dead));
                    if(room.GetComponentInChildren<KrakenPhaseOne>(true))Check("Kraken room counts only boss root",room.Enemies.Length==1);
                    float started=Time.realtimeSinceStartup;float duration=roomIndex==stream.roomScenePaths.Length-1?45:10;
                    float damageBefore=room.Enemies.Where(h=>h).Sum(h=>h.Current);int presses=0;
                    while(Time.realtimeSinceStartup-started<duration&&room&&room.Enemies.Any(h=>h&&!h.Dead))
                    {
                        var target=room.Enemies.Where(h=>h&&!h.Dead).OrderBy(h=>(h.transform.position-hud.motor.transform.position).sqrMagnitude).FirstOrDefault();
                        if(target){Bot(stream,target,true);presses++;}yield return null;
                    }
                    hud.motor.MoveInput=Vector2.zero;float after=room.Enemies.Where(h=>h).Sum(h=>h.Current);
                    Record("COMBAT "+Status+" observed "+duration+"s; HP "+damageBefore+" -> "+after+"; input frames="+presses);
                    // Assist is logged separately: this checks death/phase/progression, not combat balance.
                    Record("ASSIST "+Status+" clearing survivors through real Damage/phase handlers");
                    float limit=Time.realtimeSinceStartup+240;float nextDamage=0;
                    while(room&&room.Enemies.Any(h=>h&&!h.Dead)&&Time.realtimeSinceStartup<limit)
                    {
                        Configure(stream);
                        var core=FindFirstObjectByType<FrozenKnightSwordCore>();
                        var knight=FindFirstObjectByType<FrozenKnightBoss>();
                        if(core&&knight&&knight.CurrentPattern==FrozenKnightBoss.Pattern.FinalStand&&core.HitArea.enabled&&!core.Broken)
                        {
                            // Assist the authored spear/core success path; never bypass boss death.
                            core.ReceiveSpear();
                            Record("ASSIST winter sword core spear hit "+core.Hits);
                        }
                        if(Time.realtimeSinceStartup>=nextDamage){
                            foreach(var h in FindObjectsByType<CombatHealth>(FindObjectsSortMode.None).Where(h=>!h.player&&!h.Dead&&h.gameObject.scene==room.gameObject.scene).ToArray())h.Damage(h.maxHealth*.24f);
                            nextDamage=Time.realtimeSinceStartup+5;
                        }
                        yield return null;
                    }
                    if(room.Enemies.Any(h=>h&&!h.Dead)){Record("FAIL "+Status+" phase/death timeout");yield break;}
                    yield return Wait(()=>!room||!room.forwardVines||room.forwardVines.IsOpen,12);Check(Status+" exit opens",room&&(!room.forwardVines||room.forwardVines.IsOpen));
                    foreach(var station in FindObjectsByType<EconomyStation>(FindObjectsSortMode.None).Where(x=>x.gameObject.scene==room.gameObject.scene))
                    {
                        if(!station.gameObject.activeInHierarchy)continue;
                        Place(hud.motor,station.transform.position+Vector3.up*.5f);yield return null;
                        EconomyWindow.Instance.Open(station);yield return null;Check(Status+" "+station.kind+" UI opens",EconomyWindow.IsOpen);yield return Shot(scene+"_"+station.kind);EconomyWindow.Instance.Close();yield return null;
                    }
                    if(roomIndex<stream.roomScenePaths.Length-1)
                    {
                        var passage=FindObjectsByType<SeasonPassage>(FindObjectsSortMode.None).FirstOrDefault(p=>p.previousRoomIndex==roomIndex);
                        if(passage){
                            var rear=passage.rearCameraLimitX;Place(hud.motor,new Vector3(rear+1,1,0));
                            if(Camera.main){var c=Camera.main.GetComponent<PlayerCamera>();if(c)c.yaw=90;}
                            passage.Request(true);yield return Wait(()=>!stream.Current,15);
                            Check(Status+" corridor releases previous room",!stream.Current);
                            if(!stream.Current){yield return Wait(()=>stream.IsRoomPrepared(roomIndex+1),90);Check(Status+" corridor prepares next room",stream.IsRoomPrepared(roomIndex+1));}
                        }
                        if(!stream.IsRoomPrepared(roomIndex+1)){
                            Record("ASSIST transition through existing test loader");
                            var routine=(IEnumerator)typeof(ForestRoomStreaming).GetMethod("JumpToSummerBoss",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(stream,new object[]{roomIndex+1});yield return stream.StartCoroutine(routine);
                        }else{
                            var next=FindObjectsByType<ForestRoom>(FindObjectsSortMode.None).First(r=>r.index==roomIndex+1);
                            Place(hud.motor,new Vector3(next.entryX+5,2,0));yield return Wait(()=>stream.CurrentRoom==roomIndex+1&&!stream.Busy,20);
                        }
                        Check(Status+" advanced to next room",stream.CurrentRoom==roomIndex+1);
                    }
                }
                Status=scene+" memory and next season";yield return Wait(()=>FindFirstObjectByType<BossMemoryPickup>(),30);
                var pickup=FindFirstObjectByType<BossMemoryPickup>();Check(scene+" boss drops memory",pickup);
                if(!pickup)yield break;Place(hud.motor,pickup.transform.position);yield return new WaitForSecondsRealtime(1.2f);Check(scene+" F interaction accepted",pickup.TryInteract());
                yield return Wait(()=>StoryBook.IsOpen,15);Check(scene+" season story shown",StoryBook.IsOpen);yield return Shot(scene+"_memory");
                var carry=hud.motor.GetComponent<PlayerInventory>();carriedWeapons=carry.OwnedWeapons.Select(w=>w.weaponId).ToArray();carriedEquipped=carry.EquippedWeapon?carry.EquippedWeapon.weaponId:null;carriedGold=RunEconomy.Instance.Gold;carriedKarma=hud.motor.GetComponent<PlayerKarma>().Value;
                if(StoryBook.IsOpen)StoryBook.Instance.Finish();yield return null;
                if(season==3){yield return Wait(()=>StoryBook.IsOpen&&new[]{"life","farewell","revenge"}.Contains(StoryBook.Instance.CurrentChapter),15);Check("Winter presents ending",StoryBook.IsOpen&&new[]{"life","farewell","revenge"}.Contains(StoryBook.Instance.CurrentChapter));yield return new WaitForSecondsRealtime(3);yield return Shot("ending");if(StoryBook.IsOpen)StoryBook.Instance.Finish();yield return Wait(()=>SceneManager.GetActiveScene().name=="Title",40);Check("Ending returns to Title",SceneManager.GetActiveScene().name=="Title");}
            }
        }
    }
}
#endif
