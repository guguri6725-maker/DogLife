#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
namespace DogLife
{
    public sealed class SpringAudioValidation:MonoBehaviour
    {
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        readonly List<string> heard=new List<string>(),report=new List<string>();
        readonly List<GameObject> temporary=new List<GameObject>();
        void Heard(string key)=>heard.Add(key);
        void Check(bool value,string label)=>report.Add((value?"PASS ":"FAIL ")+label);
        static object Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,Flags).Invoke(o,args);
        static void Set(object o,string name,object value)=>o.GetType().GetField(name,Flags).SetValue(o,value);
        static void Property(object o,string name,object value)=>o.GetType().GetProperty(name).SetValue(o,value);
        GameObject Spawn(string name)
        {
            var go=Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/monster/"+name+".prefab"),new Vector3(1000,0,1000),Quaternion.identity);
            temporary.Add(go);var agent=go.GetComponent<UnityEngine.AI.NavMeshAgent>();if(agent)agent.enabled=false;return go;
        }
        bool Playing(string key)=>FindFirstObjectByType<GameAudio>().GetComponentsInChildren<AudioSource>().Any(s=>s.clip&&s.clip.name==key&&s.isPlaying);
        IEnumerator Start()
        {
            while(ForestRoomStreaming.Instance&&!ForestRoomStreaming.Instance.Ready)yield return null;
            var player=FindFirstObjectByType<PlayerMotor>();var hp=player.GetComponent<CombatHealth>();bool invulnerable=hp.invulnerable;hp.invulnerable=true;
            var settings=GameAudio.Current;var originalScene=SceneManager.GetActiveScene();Scene testScene=default;
            GameAudio.EffectStarted+=Heard;
            try
            {
                var clips=Resources.LoadAll<AudioClip>("Audio/Spring");
                Check(clips.Length==22,"All 22 edited clips resolve");
                Check(clips.Where(c=>c.name!="Spring_Ambience").All(c=>c.channels==1&&c.loadType==AudioClipLoadType.DecompressOnLoad),"Effects preloaded mono; no streaming startup delay");
                var ambience=clips.Single(c=>c.name=="Spring_Ambience");Check(ambience.channels==2&&ambience.loadType==AudioClipLoadType.Streaming,"Ambience uses stereo streaming");
                foreach(var clip in clips.Where(c=>c.name!="Spring_Ambience")){var handle=GameAudio.Play(clip.name,player.transform.position);handle.Stop();}
                Check(clips.Where(c=>c.name!="Spring_Ambience").All(c=>heard.Contains(c.name)),"Every effect is reachable through GameAudio");
                yield return new WaitForSecondsRealtime(.12f);heard.Clear();
                var spirit=Spawn("BlossomSpirit").GetComponent<BlossomSpiritEnemy>();spirit.enabled=false;
                Call(spirit,"Charge",true);Call(spirit,"Charge",true);Check(heard.Count(k=>k=="Blossom_Charge")==1,"Repeated charge updates start only one sound");Call(spirit,"Charge",false);Check(!Playing("Blossom_Charge"),"Cancelling spirit charge stops its sound");
                Set(spirit,"aimedPoint",spirit.transform.position+Vector3.forward*8+Vector3.up);Call(spirit,"Shoot");
                Check(heard.Contains("Blossom_Launch"),"Spirit release emits launch immediately");
                var shot=FindObjectsByType<CombatProjectile>(FindObjectsSortMode.None).FirstOrDefault(p=>p.owner==spirit.transform);
                Check(shot&&shot.impactSound=="Blossom_Burst","Spirit projectile carries collision sound");
                if(shot)
                {
                    var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);temporary.Add(wall);wall.transform.position=shot.transform.position+shot.direction*1.5f;wall.transform.localScale=Vector3.one*.4f;
                    shot.speed=200;Physics.SyncTransforms();Call(shot,"FixedUpdate");Check(heard.Count(k=>k=="Blossom_Burst")==1,"Collision emits exactly one burst");
                }
                var deer=Spawn("FloralDeer").GetComponent<FloralDeerEnemy>();deer.enabled=false;
                Call(deer,"BeginWindup",Vector3.forward);Check(heard.Contains("Deer_Charge_Ready"),"Deer preparation starts on windup");Call(deer,"Cancel");Check(!Playing("Deer_Charge_Ready"),"Deer cancellation stops preparation");
                var rabbit=Spawn("SakuraRabbit").GetComponent<SakuraRabbitEnemy>();rabbit.enabled=false;
                Set(rabbit,"target",player.transform);Set(rabbit,"targetHealth",hp);Set(rabbit,"biteVisualActive",true);Set(rabbit,"phaseStarted",Time.time-.24f);
                Call(rabbit,"LateUpdate");Call(rabbit,"LateUpdate");Check(heard.Count(k=>k=="Rabbit_Bite")==1,"Rabbit chew threshold fires once despite repeated visual updates");
                var boss=Spawn("SpringBoarBoss");var one=boss.GetComponent<SpringBoarBoss>();one.enabled=false;
                Set(one,"player",player);Set(one,"targetHealth",hp);Set(one,"targetCollider",player.GetComponent<Collider>());Set(one,"engaged",true);
                foreach(var pattern in new[]{SpringBoarBoss.Pattern.Tusk,SpringBoarBoss.Pattern.Slam,SpringBoarBoss.Pattern.Spikes})
                {
                    Property(one,"AttackPattern",pattern);Call(one,"Enter",SpringBoarBoss.Phase.Attack);Set(one,"elapsed",.4f);heard.Clear();Call(one,"FixedUpdate");Call(one,"FixedUpdate");
                    string key=pattern==SpringBoarBoss.Pattern.Tusk?"Boar_Tusk_Uppercut":pattern==SpringBoarBoss.Pattern.Slam?"Boar_Ground_Slam":"Boar_Bone_Spike";
                    Check(heard.Count(k=>k==key)==1,"Phase one "+pattern+" emits once even when player is out of range");
                }
                Property(one,"AttackPattern",SpringBoarBoss.Pattern.Charge);Call(one,"Enter",SpringBoarBoss.Phase.Attack);Call(one,"Enter",SpringBoarBoss.Phase.Stunned);
                Check(heard.Contains("Boar_Wall_Impact")&&!Playing("Boar_Charge"),"Wall impact replaces charge sound");
                var two=boss.GetComponent<SpringBoarPhaseTwo>();two.enabled=false;
                Set(two,"health",boss.GetComponent<CombatHealth>());Set(two,"targetHealth",hp);Set(two,"player",player);Set(two,"target",player.GetComponent<Collider>());Set(two,"nextSpike",Time.time+1000);Property(two,"Active",true);
                heard.Clear();var strike=new SpringBoarPhaseTwo.Strike();
                for(int i=0;i<4;i++)Call(two,"Add",strike,SpringBoarPhaseTwo.Shape.Sector,SpringBoarPhaseTwo.Effect.Roots,boss.transform.position,Vector3.forward,.1f,123,false);
                Check(heard.Count(k=>k=="Boar_Root_Warning")==1,"Four root branches share one warning sound");foreach(var h in two.Hazards)h.age=.15f;Call(two,"Update");Call(two,"Update");
                Check(heard.Count(k=>k=="Boar_Root_Eruption")==1,"Four root branches share one eruption without requiring a hit");Call(two,"Cleanup");
                // Simulate main Spring scene lifecycle without replacing or saving user scenes.
                testScene=SceneManager.CreateScene("Stage1_Spring");var bed=FindFirstObjectByType<SpringSoundscape>();Call(bed,"Check");yield return new WaitForSecondsRealtime(.3f);
                Check(Playing("Spring_Ambience"),"Spring scene starts ambience");Call(bed,"Check");Check(FindFirstObjectByType<GameAudio>().GetComponentsInChildren<AudioSource>().Count(s=>s.clip&&s.clip.name=="Spring_Ambience")==1,"Repeated scene checks do not duplicate ambience");
                var gateGo=new GameObject("Spring audio test gate");temporary.Add(gateGo);SceneManager.MoveGameObjectToScene(gateGo,testScene);var gate=gateGo.AddComponent<VineGate>();gate.blockers=new Collider[0];gate.navigationBlockers=new UnityEngine.AI.NavMeshObstacle[0];yield return null;
                heard.Clear();gate.Open();gate.Open();Check(heard.Count(k=>k=="Spring_Gate_Open")==1,"Gate opens with one sound and no restart on duplicate command");yield return new WaitForSecondsRealtime(.05f);gate.Close();Check(heard.Contains("Spring_Gate_Close")&&!Playing("Spring_Gate_Open"),"Reversing gate replaces opening sound");
                Time.timeScale=0;yield return null;yield return null;Check(!Playing("Spring_Ambience")&&!Playing("Spring_Gate_Close"),"Opening a paused menu pauses ambient and world voices");
                Time.timeScale=1;yield return null;yield return null;Check(Playing("Spring_Ambience"),"Closing menu resumes ambience");
                GameAudio.Preview(new AudioPreferences{master=1,music=1,effects=0});yield return null;Check(FindFirstObjectByType<GameAudio>().GetComponentsInChildren<AudioSource>().Where(s=>s.isPlaying).All(s=>s.volume==0),"Effects volume mutes ambience and active Spring effects");GameAudio.Preview(settings);
                yield return SceneManager.UnloadSceneAsync(testScene);testScene=default;yield return new WaitForSecondsRealtime(1.4f);Check(!Playing("Spring_Ambience"),"Leaving Spring fades and stops ambience");
            }
            finally
            {
                Time.timeScale=1;GameAudio.Preview(settings);GameAudio.EffectStarted-=Heard;hp.invulnerable=invulnerable;
                foreach(var go in temporary)if(go)Destroy(go);if(testScene.IsValid()&&testScene.isLoaded)SceneManager.UnloadSceneAsync(testScene);
                System.IO.File.WriteAllLines("Builds/AudioValidation/spring-runtime-checks.txt",report);Debug.Log(string.Join("\n",report));Destroy(gameObject);
            }
        }
    }
}
#endif
