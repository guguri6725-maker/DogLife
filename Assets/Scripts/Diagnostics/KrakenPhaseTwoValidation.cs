#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace DogLife
{
    [DefaultExecutionOrder(250)]
    public sealed class KrakenPhaseTwoValidation:MonoBehaviour
    {
        const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
        readonly List<string> report=new List<string>();
        KrakenPhaseOne first;KrakenPhaseTwo boss;PlayerMotor player;CombatHealth hp;Rigidbody rb;
        float maximumSocketError,maximumLengthError;
        readonly Dictionary<KrakenDeepArm,float[]> armLengths=new Dictionary<KrakenDeepArm,float[]>();
        void LateUpdate()
        {
            if(!boss||Finished)return;
            foreach(var a in boss.Arms){
                maximumSocketError=Mathf.Max(maximumSocketError,Vector3.Distance(a.RootPosition,a.SocketPosition));
                var points=a.SamplePoints();
                if(!armLengths.TryGetValue(a,out var lengths)){lengths=new float[points.Length-1];for(int i=0;i<lengths.Length;i++)lengths[i]=Vector3.Distance(points[i],points[i+1]);armLengths[a]=lengths;}
                else for(int i=0;i<lengths.Length;i++)maximumLengthError=Mathf.Max(maximumLengthError,Mathf.Abs(Vector3.Distance(points[i],points[i+1])-lengths[i]));
            }
        }
        IEnumerator CaptureImpact(string name)
        {
            float until=Time.time+12;
            while(Time.time<until){
                var fx=boss.GetComponentsInChildren<KrakenDeepVfx>().FirstOrDefault(x=>x.Impact);
                if(fx){yield return new WaitForSeconds(.12f);yield return new WaitForEndOfFrame();Snapshot("deep-"+name.ToLower()+"-impact");yield break;}
                yield return null;
            }
        }
        public bool Finished{get;private set;}
        void Check(bool ok,string label){report.Add((ok?"PASS ":"FAIL ")+label);System.IO.Directory.CreateDirectory("Builds/KrakenValidation");System.IO.File.WriteAllLines("Builds/KrakenValidation/phase-two.txt",report);Debug.Log(report.Last());}
        object Field(string name)=>typeof(KrakenPhaseTwo).GetField(name,F).GetValue(boss);
        IEnumerator Call(string name)=> (IEnumerator)typeof(KrakenPhaseTwo).GetMethod(name,F).Invoke(boss,null);
        void Move(Vector3 p){rb.position=player.transform.position=p;Physics.SyncTransforms();}
        void Snapshot(string name,bool cinematic=false)
        {
            var c=Camera.main;var pos=c.transform.position;var rot=c.transform.rotation;
            if(!cinematic){Vector3 aim=(Vector3)Field("facing"),side=Vector3.Cross(Vector3.up,aim);c.transform.position=boss.Center+aim*15+side*7+Vector3.up*2;c.transform.LookAt(boss.Center+aim*3);}
            var rt=RenderTexture.GetTemporary(1280,720,24);var previous=c.targetTexture;var active=RenderTexture.active;c.targetTexture=rt;c.Render();RenderTexture.active=rt;
            var tex=new Texture2D(1280,720,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1280,720),0,0);tex.Apply();System.IO.File.WriteAllBytes("Builds/KrakenValidation/"+name+".png",tex.EncodeToPNG());Destroy(tex);c.targetTexture=previous;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);c.transform.SetPositionAndRotation(pos,rot);
        }
        IEnumerator Start()
        {
            System.IO.Directory.CreateDirectory("Builds/KrakenValidation");
            var stream=FindFirstObjectByType<ForestRoomStreaming>();while(!stream.Ready)yield return null;stream.StopAllCoroutines();stream.enabled=false;
            foreach(var h in FindObjectsByType<CombatHealth>(FindObjectsSortMode.None))if(!h.player)h.gameObject.SetActive(false);
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/SeasonRooms/Summer/Summer_Room04.unity",LoadSceneMode.Additive);
            var room=FindObjectsByType<ForestRoom>(FindObjectsSortMode.None).First(r=>r.gameObject.scene.name=="Summer_Room04");yield return room.PrepareGeometry();room.BeginEncounter(false);yield return null;yield return null;
            first=FindFirstObjectByType<KrakenPhaseOne>();boss=first.GetComponent<KrakenPhaseTwo>();boss.AutomaticAttacks=false;
            player=first.Player;hp=player.GetComponent<CombatHealth>();hp.maxHealth=1000;hp.Restore();rb=player.GetComponent<Rigidbody>();player.readInput=false;Move(new Vector3(room.entryX+7,1,0));room.BeginEncounter();
            float deadline=Time.time+35;while(first.State!=KrakenPhaseOne.EncounterState.Combat&&Time.time<deadline)yield return null;
            foreach(var key in new[]{"nextSlam","nextSweep","nextCapture"})typeof(KrakenPhaseOne).GetField(key,F).SetValue(first,Time.time+10000);
            foreach(var slot in first.Slots)slot.Health.Damage(10000);
            Check(first.Kills==5,"Five individual tentacles required before flood");
            deadline=Time.time+45;bool reveal=false,sand=false,protectedIntro=true;int shots=0;
            while(first.State!=KrakenPhaseOne.EncounterState.Underwater&&Time.time<deadline){
                if(boss.State==KrakenPhaseTwo.Phase.Reveal){
                    reveal=true;sand|=boss.GetComponentsInChildren<KrakenDeepVfx>().Any(x=>x.Shape==KrakenDeepVfx.Form.Sand);protectedIntro&=hp.invulnerable&&!player.readInput;
                    float visible=boss.GetComponentsInChildren<SkinnedMeshRenderer>().Where(x=>x.enabled).Count();
                    if(shots<3&&visible>0){yield return new WaitForSeconds(shots==0?1:3);yield return new WaitForEndOfFrame();Snapshot("deep-reveal-"+shots,true);shots++;}
                }
                yield return null;
            }
            Check(reveal&&sand&&protectedIntro,"Flood completes before protected sand and body reveal");
            Check(first.State==KrakenPhaseOne.EncounterState.Underwater&&boss.State==KrakenPhaseTwo.Phase.Idle,"Reveal returns camera and starts underwater phase");
            Check(!hp.invulnerable&&player.enabled&&player.GetComponent<PlayerWaterMovement>().Submerged,"Player control state and underwater mode restored");
            Check(boss.Arms.Count==4&&boss.Arms.All(a=>a.GetComponentInParent<CombatHealth>()==boss.GetComponent<CombatHealth>()&&a.GetComponent<CombatHealth>()==null),"All phase two arms share root health without local health");
            yield return new WaitForEndOfFrame();Snapshot("deep-idle");
            float endpointError=boss.Arms.Max(a=>Vector3.Distance(a.Tip,(Vector3)typeof(KrakenDeepArm).GetField("target",F).GetValue(a)));
            Check(endpointError<.2f,"Rotation-only arm solver preserves reach without stretching: "+endpointError.ToString("F3"));
            player.enabled=false;rb.isKinematic=true;
            var center=boss.Center;Move(center+Vector3.right*8);hp.invulnerable=true;
            var weapon=ScriptableObject.CreateInstance<InventoryWeapon>();weapon.damage=10;var line=new GameObject("Phase two test weapon").AddComponent<LineRenderer>();line.useWorldSpace=true;line.positionCount=2;
            var c=boss.Arms[0].GetComponentsInChildren<SphereCollider>().First(x=>x.enabled);var p=c.bounds.center;line.SetPosition(0,p-Vector3.right*.2f);line.SetPosition(1,p+Vector3.right*.2f);float before=boss.GetComponent<CombatHealth>().Current;
            player.GetComponent<PlayerWeapons>().ApplyVisibleMelee(weapon,p,line,false,1);
            Check(boss.GetComponent<CombatHealth>().Current==before-10,"Real melee on an exposed tentacle damages body HP exactly once");Destroy(line.gameObject);Destroy(weapon);
            var barrier=boss.BodyBarrier;Check(barrier&&barrier.enabled&&!barrier.isTrigger,"Body has an enabled solid movement collider");
            var playerCollider=player.GetComponent<CapsuleCollider>();bool blocked=true;
            rb.isKinematic=false;rb.useGravity=false;
            foreach(var direction in new[]{Vector3.right,Vector3.left,Vector3.forward,Vector3.back,Vector3.down,Vector3.up}){
                Vector3 midpoint=barrier.bounds.center;
                Vector3 offset=playerCollider.bounds.center-player.transform.position;
                float distance=Mathf.Abs(direction.y)>.5f?barrier.bounds.extents.y+playerCollider.bounds.extents.y+1:barrier.bounds.extents.x+playerCollider.bounds.extents.x+1;
                Move(midpoint+direction*distance-offset);
                for(int frame=0;frame<35;frame++){rb.linearVelocity=-direction*8;yield return new WaitForFixedUpdate();}
                Physics.SyncTransforms();
                bool overlap=Physics.ComputePenetration(playerCollider,playerCollider.transform.position,playerCollider.transform.rotation,barrier,barrier.transform.position,barrier.transform.rotation,out var separation,out float depth);
                blocked&=(!overlap||depth<.04f)&&Vector3.Dot(playerCollider.bounds.center-barrier.bounds.center,direction)>0;
            }
            Check(blocked,"Dynamic player cannot enter body from four sides, above or below");
            rb.linearVelocity=Vector3.zero;rb.isKinematic=true;
            foreach(var method in new[]{"Pincer","Cannon","Vortex","Sweep"}){
                hp.Restore();hp.invulnerable=false;Move(new Vector3(center.x+8,first.Arena.min.y+2,center.z));float healthBefore=hp.Current;
                var task=boss.StartCoroutine(Call(method));
                StartCoroutine(CaptureImpact(method));
                if(method=="Cannon"){float ready=Time.time+6;while(!boss.GetComponentsInChildren<KrakenDeepVfx>().Any(x=>x.Shape==KrakenDeepVfx.Form.Beam)&&Time.time<ready)yield return null;}
                yield return new WaitForSeconds(.8f);yield return new WaitForEndOfFrame();Snapshot("deep-"+method.ToLower()+"-warning");
                if(method=="Pincer")Move(new Vector3(center.x+8,first.Arena.max.y-2,center.z));
                if(method=="Cannon")Move(new Vector3(center.x+8,first.Arena.min.y+2,center.z+6));
                if(method=="Vortex")Move(new Vector3(first.Arena.max.x-1,first.Arena.max.y-2,center.z));
                if(method=="Sweep")Move(new Vector3(center.x+8,first.Arena.max.y-2,center.z));
                yield return task;
                Check(hp.Current==healthBefore,method+" warning can be escaped in the announced safe direction");
                typeof(KrakenPhaseTwo).GetMethod("ClearEffects",F).Invoke(boss,null);
            }
            // Actual collider hit inside/outside the same announced height band.
            hp.Restore();hp.invulnerable=false;Move(center);var box=typeof(KrakenPhaseTwo).GetMethod("BoxHit",F);
            bool inside=(bool)box.Invoke(boss,new object[]{center,new Vector3(6,2,6),Quaternion.identity,1f,2f});
            yield return new WaitForSeconds(1);Move(center+Vector3.up*3);
            bool outside=(bool)box.Invoke(boss,new object[]{center,new Vector3(6,2,6),Quaternion.identity,1f,2f});
            Check(inside&&!outside,"Damage volume hits its layer and excludes the upper safe layer");
            hp.invulnerable=true;boss.GetComponent<CombatHealth>().Damage(1400);boss.AutomaticAttacks=true;
            deadline=Time.time+25;int layers=0;float lastY=-100;
            while(Time.time<deadline){
                var band=boss.GetComponentsInChildren<KrakenDeepVfx>().FirstOrDefault(x=>x.Shape==KrakenDeepVfx.Form.Band);
                if(boss.State==KrakenPhaseTwo.Phase.Heart&&band&&Mathf.Abs(band.transform.position.y-lastY)>.2f){lastY=band.transform.position.y;layers++;yield return new WaitForSeconds(.8f);yield return new WaitForEndOfFrame();Snapshot("deep-heart-layer-"+layers);}
                if(boss.State==KrakenPhaseTwo.Phase.Groggy)break;yield return null;
            }
            Check(layers==3&&boss.HeartExecutions==1&&boss.State==KrakenPhaseTwo.Phase.Groggy,"HP threshold runs lower, upper, middle once and grants groggy opening");
            boss.AutomaticAttacks=false;yield return new WaitForSeconds(6);Check(boss.HeartExecutions==1,"Special does not retrigger below threshold");
            boss.GetComponent<CombatHealth>().Damage(10000);yield return new WaitForSeconds(5);
            Check(boss.GetComponent<CombatHealth>().Dead&&boss.gameObject.activeSelf&&player.GetComponent<PlayerWaterMovement>().Submerged,"Boss death completes without removing underwater environment");
            Check(!boss.BodyBarrier.enabled,"Body movement barrier releases on boss death");
            Vector3 oldPosition=player.transform.position;Move(new Vector3(boss.Arena.max.x+3,oldPosition.y,oldPosition.z));player.GetComponent<PlayerWaterMovement>().ResolveVelocity(Vector3.right);
            Check(!player.GetComponent<PlayerWaterMovement>().Submerged,"Defeated boss releases arena limits and swimming ends outside room");Move(oldPosition);
            Check(maximumSocketError<.01f,"Arm roots remain attached throughout reveal, attacks and death: "+maximumSocketError.ToString("F5"));
            Check(maximumLengthError<.01f,"Animated arms retain every bone length: "+maximumLengthError.ToString("F5"));
            rb.isKinematic=false;player.enabled=true;player.readInput=true;hp.invulnerable=false;hp.maxHealth=6;hp.Restore();Finished=true;
        }
    }
}
#endif
