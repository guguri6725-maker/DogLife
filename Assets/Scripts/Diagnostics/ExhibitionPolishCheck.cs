#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace DogLife
{
    // Explicit editor-only observation. Uses assisted phase triggers, never reports human balance results.
    public sealed class ExhibitionPolishCheck : MonoBehaviour
    {
        public string Status {get;private set;}
        public bool Completed {get;private set;}
        public int startSeason;
        readonly List<string> log=new List<string>();
        const string Folder="outputs/exhibition-polish";
        bool background;
        void Awake(){DontDestroyOnLoad(gameObject);background=Application.runInBackground;Application.runInBackground=true;Directory.CreateDirectory(Folder);}
        void Record(string message){log.Add(message);File.WriteAllLines(Folder+"/checks.txt",log);}
        void Check(string message,bool condition){Record((condition?"PASS ":"FAIL ")+message);}
        IEnumerator Wait(Func<bool> condition,float duration=60){float until=Time.realtimeSinceStartup+duration;while(!condition()&&Time.realtimeSinceStartup<until)yield return null;}
        IEnumerator Shot(string name){yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Folder+"/"+name+".png");yield return null;}
        void Update()
        {
            var s=ForestRoomStreaming.Instance;if(!s||!s.director||!s.director.hud)return;
            var h=s.director.hud;if(h.health){h.health.invulnerable=true;h.health.AddInvulnerability(1);}
            if(h.motor){h.motor.readInput=false;h.motor.MoveInput=Vector2.zero;}if(h.weapons)h.weapons.readInput=false;
            if(Camera.main){var c=Camera.main.GetComponent<PlayerCamera>();if(c)c.readInput=false;}
        }
        void Aim(Transform target)
        {
            var s=ForestRoomStreaming.Instance;var c=Camera.main.GetComponent<PlayerCamera>();var delta=target.position-s.director.hud.motor.transform.position;
            c.yaw=Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg;c.pitch=10;
        }
        IEnumerator ValidateHUD(string name,int phase)
        {
            yield return new WaitForSecondsRealtime(.2f);
            var hud=FindFirstObjectByType<BossEncounterHUD>();
            Check(name+" HUD exists",hud);if(!hud)yield break;
            Check(name+" HUD visible",hud.group.alpha>.9f);
            Check(name+" phase",phase==0?hud.phaseLabel.text.Contains("ENCOUNTER"):hud.phaseLabel.text.Contains(phase==2?"PHASE II":"PHASE I"));
            Check(name+" readable title",!string.IsNullOrEmpty(hud.bossName.text));
            yield return Shot(name);
        }
        IEnumerator Start()
        {
            for(int season=startSeason;season<4;season++)
            {
                Status="Loading season "+season;Time.timeScale=1;
                yield return SceneManager.LoadSceneAsync(SeasonRunSave.Scenes[season]);
                yield return Wait(()=>ForestRoomStreaming.Instance&&ForestRoomStreaming.Instance.Ready);
                var s=ForestRoomStreaming.Instance;Check(Status,s&&s.Ready);if(!s||!s.Ready)yield break;
                var map=FindFirstObjectByType<SeasonMapWindow>();
                Check("Map hint bottom-left "+season,map&&map.hint.rectTransform.anchorMin==Vector2.zero);
                Check("Boss jump "+season,s.RequestBossTestJump());
                yield return Wait(()=>!s.Busy&&s.CurrentRoom==s.roomScenePaths.Length-1);
                yield return Wait(()=>!BossCinematicUI.Hidden,40);yield return new WaitForSeconds(3);
                var target=s.Current.Enemies.FirstOrDefault(e=>e);if(!target){Record("FAIL no boss");continue;}
                if(season==1)yield return Wait(()=>target.GetComponent<KrakenPhaseOne>().State==KrakenPhaseOne.EncounterState.Combat,60);
                if(season==3)yield return Wait(()=>target.GetComponent<FrozenKnightBoss>().CanDealDamage&&!target.invulnerable&&!BossCinematicUI.Hidden,60);
                Aim(target.transform);yield return null;
                yield return ValidateHUD("season"+season+"-boss",season==2?0:1);
                var hud=FindFirstObjectByType<BossEncounterHUD>();
                GameSettingsWindow.Instance.Open();yield return null;yield return null;
                Check("HUD hidden in settings "+season,hud&&hud.group.alpha==0);
                GameSettingsWindow.Instance.Close();yield return new WaitForSeconds(.1f);
                if(season==0)
                {
                    Status="Spring phase transition";target.Damage(target.maxHealth);
                    yield return new WaitForSecondsRealtime(.4f);Check("Spring cutscene hides HUD",BossCinematicUI.Hidden&&hud.group.alpha==0);
                    var transition=target.GetComponent<SpringBoarPhaseTransition>();
                    yield return Wait(()=>transition.CurrentPhase==2&&!transition.Transitioning&&!BossCinematicUI.Hidden,45);
                    Aim(target.transform);yield return ValidateHUD("spring-phase2",2);
                }
                if(season==1)
                {
                    Status="Summer tentacles and underwater transition";
                    var one=target.GetComponent<KrakenPhaseOne>();
                    yield return Wait(()=>one.State==KrakenPhaseOne.EncounterState.Combat);
                    float timeout=Time.realtimeSinceStartup+50;
                    while(one.Kills<one.RequiredKills&&Time.realtimeSinceStartup<timeout){foreach(var t in one.Slots.ToArray())if(t.Alive&&!t.Health.invulnerable)t.Health.Damage(t.Health.maxHealth);yield return new WaitForSeconds(.5f);}
                    Check("Summer counted all kills",one.Kills==one.RequiredKills);
                    yield return Wait(()=>one.State==KrakenPhaseOne.EncounterState.Underwater&&!BossCinematicUI.Hidden,75);
                    var two=target.GetComponent<KrakenPhaseTwo>();
                    var c=Camera.main.GetComponent<PlayerCamera>();var delta=two.Center-s.director.hud.motor.transform.position;c.yaw=Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg;c.pitch=5;
                    yield return ValidateHUD("summer-phase2",2);
                    two.AutomaticAttacks=false;yield return Wait(()=>two.State==KrakenPhaseTwo.Phase.Idle,25);
                    yield return Shot("summer-idle-after");
                    var pincer=(IEnumerator)typeof(KrakenPhaseTwo).GetMethod("Pincer",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(two,null);
                    two.StartCoroutine(pincer);yield return new WaitForSeconds(.9f);yield return Shot("summer-pincer-after");
                }
                if(season==2)
                {
                    Status="Autumn pumpkin appearance";var reaper=target.GetComponent<HarvestReaperBoss>();
                    typeof(HarvestReaperBoss).GetMethod("Begin",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(reaper,new object[]{HarvestReaperBoss.Pattern.Pumpkins});
                    yield return Wait(()=>reaper.Patches.Count>0,15);yield return new WaitForSeconds(.3f);
                    Check("Detailed pumpkin instances",FindObjectsByType<HarvestPumpkinVisual>(FindObjectsSortMode.None).Length>0);
                    yield return Shot("autumn-pumpkins-after");
                }
                if(season==3)
                {
                    Status="Winter phase transition";target.Damage(target.maxHealth);
                    var knight=target.GetComponent<FrozenKnightBoss>();
                    yield return Wait(()=>knight.PhaseNumber==2&&!BossCinematicUI.Hidden&&knight.State!=FrozenKnightBoss.CombatState.Transition,90);
                    Aim(target.transform);yield return ValidateHUD("winter-phase2",2);
                }
            }
            Status="Complete";Completed=true;Record("COMPLETE — assisted editor checks, not a full playthrough.");
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Title.unity");
        }
        void OnDestroy(){Application.runInBackground=background;Time.timeScale=1;}
    }
}
#endif
