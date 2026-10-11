#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace DogLife
{
    public sealed class KrakenCaptureRegression:MonoBehaviour
    {
        const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
        readonly List<string> report=new List<string>();KrakenPhaseOne boss;PlayerMotor player;CombatHealth hp;
        public bool Finished{get;private set;}
        void Check(bool ok,string label){report.Add((ok?"PASS ":"FAIL ")+label);System.IO.Directory.CreateDirectory("Builds/KrakenValidation");System.IO.File.WriteAllLines("Builds/KrakenValidation/capture-regression.txt",report);Debug.Log(report.Last());}
        object Field(string key)=>typeof(KrakenPhaseOne).GetField(key,F).GetValue(boss);
        void ScheduleOff(){foreach(var key in new[]{"nextSlam","nextSweep","nextCapture"})typeof(KrakenPhaseOne).GetField(key,F).SetValue(boss,Time.time+10000);}
        void Snapshot(string name,Vector3 focus,Vector3 offset)
        {
            var c=Camera.main;var pos=c.transform.position;var rot=c.transform.rotation;c.transform.position=focus+offset;c.transform.LookAt(focus);
            var rt=RenderTexture.GetTemporary(1280,720,24);var prev=c.targetTexture;var active=RenderTexture.active;c.targetTexture=rt;c.Render();RenderTexture.active=rt;var tex=new Texture2D(1280,720,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1280,720),0,0);tex.Apply();System.IO.File.WriteAllBytes("Builds/KrakenValidation/"+name+".png",tex.EncodeToPNG());Destroy(tex);c.targetTexture=prev;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);c.transform.SetPositionAndRotation(pos,rot);
        }
        IEnumerator Start()
        {
            var stream=FindFirstObjectByType<ForestRoomStreaming>();while(!stream.Ready)yield return null;stream.StopAllCoroutines();stream.enabled=false;
            foreach(var e in FindObjectsByType<CombatHealth>(FindObjectsSortMode.None))if(!e.player)e.gameObject.SetActive(false);
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/SeasonRooms/Summer/Summer_Room04.unity",LoadSceneMode.Additive);
            var room=FindObjectsByType<ForestRoom>(FindObjectsSortMode.None).First(r=>r.gameObject.scene.name=="Summer_Room04");yield return room.PrepareGeometry();room.BeginEncounter(false);yield return null;yield return null;
            boss=FindFirstObjectByType<KrakenPhaseOne>();player=boss.Player;hp=player.GetComponent<CombatHealth>();hp.maxHealth=1000;hp.Restore();var rb=player.GetComponent<Rigidbody>();player.readInput=false;rb.position=player.transform.position=new Vector3(room.entryX+7,1,0);rb.linearVelocity=Vector3.zero;room.BeginEncounter();
            bool safeEntry=true;float end=Time.time+30;while(boss.State!=KrakenPhaseOne.EncounterState.Combat&&Time.time<end){foreach(var s in boss.Slots)if(s.State==KrakenPhaseTentacle.Pose.Slam||s.AttackCollider.enabled)safeEntry=false;yield return null;}ScheduleOff();Check(safeEntry,"No attacks or damage colliders during entry cinematic");
            var slam=(KrakenPhaseOne.SlamSettings)Field("slam");var sweep=(KrakenPhaseOne.SweepSettings)Field("sweep");var capture=(KrakenPhaseOne.CaptureSettings)Field("capture");
            Check(Mathf.Approximately(slam.length,18)&&Mathf.Approximately(slam.width,6.75f)&&Mathf.Approximately(sweep.radius,13.5f)&&Mathf.Approximately(capture.radius,3.9f),"Slam reach 18, width 6.75; other pattern ranges unchanged");
            Check(Mathf.Approximately(slam.strike,.35f/1.5f)&&Mathf.Approximately(slam.cooldown,4)&&Mathf.Approximately(sweep.duration,1.2f)&&Mathf.Approximately(capture.cooldown,13f/1.5f),"Attack timings are baseline divided by 1.5");
            var first=boss.Slots[0];var others=boss.Slots.Skip(1).Select(s=>s.Health.Current).ToArray();
            Check(Mathf.Approximately(first.transform.localScale.x,first.transform.localScale.y)&&Mathf.Approximately(first.transform.localScale.z,first.transform.localScale.y),"Uniform rig scale prevents flattened bending");
            typeof(KrakenPhaseOne).GetMethod("ConfigureAttackBox",F).Invoke(boss,new object[]{first,slam.width,slam.hitHeight,slam.length});
            Check(Vector3.Distance(Vector3.Scale(first.AttackCollider.size,first.transform.lossyScale),new Vector3(6.75f,slam.hitHeight,18))<.001f,"Nonuniform scaling preserves exact damage box dimensions");
            var warning=KrakenPhaseWarning.Create(boss.transform,boss.WarningMaterial);var floor=boss.Arena.min.y+.68f;
            warning.Show(KrakenPhaseWarning.Shape.Rectangle,new Vector3(boss.Arena.center.x-8,floor,-5),Quaternion.Euler(0,90,0),18,6.75f,.65f);
            yield return new WaitForEndOfFrame();Snapshot("warning-rectangle-upgraded",new Vector3(boss.Arena.center.x,floor,-5),new Vector3(-8,12,-12));
            warning.Show(KrakenPhaseWarning.Shape.Sector,new Vector3(boss.Arena.center.x,floor,0),Quaternion.identity,9,0,.65f,200);
            yield return new WaitForEndOfFrame();Snapshot("warning-sector-upgraded",new Vector3(boss.Arena.center.x,floor,0),new Vector3(-8,14,-12));Destroy(warning.gameObject);
            foreach(var slot in boss.Slots){slot.StopAttack();slot.SetPose(KrakenPhaseTentacle.Pose.Idle,0);}((HashSet<KrakenPhaseTentacle>)Field("slamming")).Clear();ScheduleOff();
            rb.position=player.transform.position=first.transform.position+Vector3.right*10+Vector3.up;hp.invulnerable=true;
            first.StartCoroutine((IEnumerator)typeof(KrakenPhaseOne).GetMethod("Slam",F).Invoke(boss,new object[]{first}));
            end=Time.time+6;while(first.State!=KrakenPhaseTentacle.Pose.Ground&&Time.time<end)yield return null;
            yield return new WaitForEndOfFrame();float reach=Vector3.Dot(first.TipPosition-first.transform.position,first.transform.forward);
            Check(reach>slam.length*.9f&&reach<slam.length+1,"Visible tentacle tip reaches the far end of the warning: "+reach.ToString("F2"));
            yield return new WaitForSeconds(.2f);Snapshot("slam-contact-matched",first.transform.position+first.transform.forward*8,new Vector3(-5,9,-11));
            end=Time.time+5;while(first.State!=KrakenPhaseTentacle.Pose.Idle&&Time.time<end)yield return null;ScheduleOff();
            first.StartCoroutine((IEnumerator)typeof(KrakenPhaseOne).GetMethod("Emerge",F).Invoke(boss,new object[]{first}));
            foreach(var key in new[]{"nextSlam","nextSweep","nextCapture"})typeof(KrakenPhaseOne).GetField(key,F).SetValue(boss,Time.time-1);
            bool safeRise=true;end=Time.time+5;while(first.State==KrakenPhaseTentacle.Pose.Emerging&&Time.time<end){foreach(var s in boss.Slots)if(s.State==KrakenPhaseTentacle.Pose.Slam||s.AttackCollider.enabled)safeRise=false;yield return null;}
            Check(safeRise&&(float)Field("attackAllowedAt")>Time.time+1,"Re-emergence blocks scheduler and grants over one second of grace");ScheduleOff();
            var central=(KrakenPhaseTentacle)Field("sweepTentacle");bool motorWasEnabled=player.enabled;bool wasKinematic=rb.isKinematic;player.enabled=false;rb.isKinematic=true;
            for(int pass=0;pass<2;pass++){
                hp.invulnerable=false;float before=hp.Current;bool visible=false,shot=false;
                rb.position=player.transform.position=new Vector3(boss.Arena.center.x,boss.Arena.min.y+(pass==0?1:3.5f),8);
                boss.StartCoroutine((IEnumerator)typeof(KrakenPhaseOne).GetMethod("Sweep",F).Invoke(boss,null));end=Time.time+9;
                while((bool)Field("special")&&Time.time<end){
                    rb.position=player.transform.position=new Vector3(boss.Arena.center.x,boss.Arena.min.y+(pass==0?1:3.5f),8);
                    if(central.State==KrakenPhaseTentacle.Pose.Sweep){visible|=central.TipPosition.y>boss.Arena.min.y+.55f;if(!shot&&central.PoseProgress>.4f){yield return new WaitForEndOfFrame();Snapshot("central-sweep-visible",new Vector3(boss.Arena.center.x,1,0),new Vector3(-10,11,-14));shot=true;}}
                    yield return null;
                }
                Check(visible&&shot,"Central sweep stays above water throughout visible arc, pass "+pass);
                Check(pass==0?hp.Current==before-sweep.damage:hp.Current==before,pass==0?"Ground player receives exactly one sweep hit":"Player above sweep height avoids damage");ScheduleOff();
            }
            rb.isKinematic=wasKinematic;player.enabled=motorWasEnabled;hp.invulnerable=true;
            var hit=first.GetComponentsInChildren<CapsuleCollider>().First(c=>c.enabled&&!c.isTrigger);
            Check(hit.bounds.size.magnitude<5,"Tentacle hurtbox is body-sized despite 100x imported bone scale");
            var weapon=ScriptableObject.CreateInstance<InventoryWeapon>();weapon.damage=10;
            var line=new GameObject("Regression weapon stroke").AddComponent<LineRenderer>();line.useWorldSpace=true;line.positionCount=2;line.widthMultiplier=.1f;
            System.Action strike=()=>{Physics.SyncTransforms();var p=hit.bounds.center;line.SetPosition(0,p-Vector3.right*.05f);line.SetPosition(1,p+Vector3.right*.05f);player.GetComponent<PlayerWeapons>().ApplyVisibleMelee(weapon,p,line,false,1);};
            strike();Check(first.Health.Current==90&&boss.Slots.Skip(1).Select(s=>s.Health.Current).SequenceEqual(others),"Real weapon overlap damages exactly one tentacle");
            weapon.damage=1000;strike();yield return null;
            Check(boss.Kills==1&&boss.State==KrakenPhaseOne.EncounterState.Combat&&boss.Slots.Skip(1).All(s=>!s.Health.Dead),"Real weapon kill counts once and leaves other four alive");
            Destroy(line.gameObject);Destroy(weapon);
            for(int avoid=0;avoid<2;avoid++){
                ScheduleOff();hp.Restore();hp.invulnerable=avoid==1;float beforeCapture=hp.Current;bool everCaptured=false;
                rb.position=player.transform.position=new Vector3(boss.Arena.center.x,1,0);rb.linearVelocity=Vector3.zero;yield return new WaitForFixedUpdate();
                boss.StartCoroutine((IEnumerator)typeof(KrakenPhaseOne).GetMethod("Capture",F).Invoke(boss,null));
                yield return new WaitForSeconds(capture.warning*.5f);
                if(avoid==0){rb.position=player.transform.position=new Vector3(boss.Arena.center.x+capture.radius+4,1,0);rb.linearVelocity=Vector3.zero;}
                end=Time.time+7;while((bool)Field("special")&&Time.time<end){everCaptured|=(bool)Field("captured");yield return null;}
                Check(!everCaptured&&hp.Current==beforeCapture,avoid==0?"Leaving locked capture warning avoids damage and binding":"Protected player cannot be captured");
            }
            ScheduleOff();rb.position=player.transform.position=new Vector3(boss.Arena.center.x,1,0);rb.linearVelocity=Vector3.zero;yield return new WaitForFixedUpdate();hp.invulnerable=false;
            boss.StartCoroutine((IEnumerator)typeof(KrakenPhaseOne).GetMethod("Capture",F).Invoke(boss,null));
            end=Time.time+4;while(!(bool)Field("captured")&&Time.time<end)yield return null;Check((bool)Field("captured"),"Actual capture connects at locked warning position");
            yield return new WaitForSeconds(.8f);yield return new WaitForEndOfFrame();var t=(KrakenPhaseTentacle)Field("captureTentacle");Snapshot("capture-coil-front",player.transform.position+Vector3.up,new Vector3(4,2,6));Snapshot("capture-coil-side",player.transform.position+Vector3.up,new Vector3(-6,1,0));
            Check(Vector3.Distance(player.transform.position,t.GripPoint.position)<.5f&&!player.enabled&&rb.isKinematic,"Player held inside moving coil anchor with movement locked");
            end=Time.time+4;while((bool)Field("captured")&&Time.time<end)yield return null;Check(player.enabled&&!rb.isKinematic&&player.IsKnockedBack,"Coil releases player into throw with controls/body restored");
            yield return new WaitForSeconds(2);ScheduleOff();hp.invulnerable=true;foreach(var slot in boss.Slots)slot.Health.Damage(10000);
            var waterDuring=boss.GetComponent<KrakenWaterVolume>();bool gradual=false,floorGradual=false,surfaceGradual=false;float previous=1;
            end=Time.time+25;while(boss.State!=KrakenPhaseOne.EncounterState.Underwater&&Time.time<end){
                var newSurface=boss.GetComponentsInChildren<Renderer>().FirstOrDefault(r=>r.name=="Full room flood surface");
                if(newSurface&&newSurface.enabled){float reveal=newSurface.sharedMaterial.GetFloat("_Reveal");if(reveal>.05f&&reveal<.95f)surfaceGradual=true;}
                foreach(var r in room.GetComponentsInChildren<Renderer>())if(r.sharedMaterial&&r.sharedMaterial.shader.name=="DogLife/Coastal Floor Water"){
                    var block=new MaterialPropertyBlock();r.GetPropertyBlock(block);float opacity=block.GetFloat("_FloodFloorVisibility");
                    if(opacity>.1f&&opacity<.9f&&r.enabled){if(!floorGradual)Snapshot("floor-water-crossfade",new Vector3(boss.Arena.center.x,2,0),new Vector3(-8,8,-7));floorGradual=true;}
                }
                foreach(var r in room.GetComponentsInChildren<Renderer>())if(r.sharedMaterial&&r.sharedMaterial.shader.name=="DogLife/Original Coastal Waterfall"){
                    var block=new MaterialPropertyBlock();r.GetPropertyBlock(block);float v=block.GetFloat("_FloodVisibility");
                    if(v>.05f&&v<.95f&&!r.forceRenderingOff){gradual=true;if(previous>.6f&&v<.6f)Snapshot("flood-progressive-flow-fade",new Vector3(boss.Arena.center.x,5,0),new Vector3(-8,2,-7));previous=v;}
                }yield return null;}
            Check(gradual,"Decoration water fades while flood rises, before final renderer shutdown");
            Check(floorGradual&&surfaceGradual,"Old floor ripples fade while new surface and waves gradually appear");
            var water=boss.GetComponent<KrakenWaterVolume>();var flowing=room.GetComponentsInChildren<Renderer>(true).Where(r=>r.sharedMaterials.Any(m=>m&&m.shader&&(m.shader.name=="DogLife/Original Coastal Waterfall"||m.shader.name=="DogLife/Coastal Landing Foam"))).ToArray();
            yield return new WaitForSeconds(.5f);Check(water.StoppedDecorationCount>0&&flowing.All(r=>r.forceRenderingOff),"Phase two disables waterfall curtains and landing foam despite gate updates");
            Check(room.GetComponentsInChildren<Renderer>().Any(r=>r.name=="Full room flood surface"&&r.enabled&&!r.forceRenderingOff),"Main rising water remains visible");
            Snapshot("phase-two-decoration-water-off",new Vector3(boss.Arena.center.x,5,0),new Vector3(-8,2,-7));
            boss.CancelEncounter();Check(flowing.All(r=>!r.forceRenderingOff),"Encounter cleanup restores decoration water visibility");Finished=true;
        }
    }
}
#endif
