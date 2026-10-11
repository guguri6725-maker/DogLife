#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace DogLife
{
    public sealed class SpringBoarPhaseValidation : MonoBehaviour
    {
        readonly List<string> results=new List<string>();
        GameObject boss;
        Camera camera;
        RenderTexture target;
        Camera previousMain;
        void Check(bool ok,string text) { results.Add((ok?"PASS ":"FAIL ")+text); }
        IEnumerator Start()
        {
            System.IO.Directory.CreateDirectory("Builds/SpringBoarPhaseValidation");
            boss=Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/monster/SpringBoarBoss.prefab"),new Vector3(1000,0,1000),Quaternion.identity);
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Death validation ground";floor.layer=8;
            floor.transform.position=boss.transform.position+Vector3.down*.85f;floor.transform.localScale=new Vector3(40,1,40);
            var brain=boss.GetComponent<SpringBoarBoss>();
            Check(brain.enabled,"Phase one uses existing boss AI");
            brain.enabled=false;
            boss.GetComponent<UnityEngine.AI.NavMeshAgent>().enabled=false;
            var health=boss.GetComponent<CombatHealth>();
            var transition=boss.GetComponent<SpringBoarPhaseTransition>();
            var serialized=new SerializedObject(transition);
            var flesh=(GameObject)serialized.FindProperty("fleshBody").objectReferenceValue;
            var skeleton=(GameObject)serialized.FindProperty("skeletonBody").objectReferenceValue;
            int deaths=0; health.Died+=()=>deaths++;
            Check(flesh.activeSelf&&!skeleton.activeSelf,"Starts with flesh only");
            var visual=flesh.GetComponent<Animator>();
            foreach(string state in new[]{"Idle","Walk","Scrape","Charge","TuskReady","Tusk","SlamReady","Slam","Roar","Stunned","Recovery","Metamorphosis"})
                Check(visual.HasState(0,Animator.StringToHash(state)),"Animation "+state);
            camera=new GameObject("PhaseValidationCamera").AddComponent<Camera>();
            camera.transform.position=boss.transform.position+new Vector3(15,10,22);
            camera.transform.LookAt(boss.transform.position+Vector3.up*4);
            camera.fieldOfView=35;camera.backgroundColor=new Color(.12f,.15f,.18f);
            previousMain=Camera.main;
            if(previousMain)previousMain.tag="Untagged";
            camera.tag="MainCamera";
            camera.clearFlags=CameraClearFlags.SolidColor;camera.enabled=true;
            target=new RenderTexture(1024,1024,24);camera.targetTexture=target;
            yield return null;
            Capture("01-flesh");
            health.Damage(health.maxHealth+1);
            Check(transition.Transitioning&&transition.CurrentPhase==2,"Lethal hit starts second phase");
            Check(!health.Dead&&deaths==0,"First depletion does not signal death");
            Check(!brain.enabled&&!boss.GetComponentInChildren<SpringBoarVfx>(true).gameObject.activeSelf,"Attack AI and warnings stop");
            float hp=health.Current;health.Damage(10000);
            Check(health.Current==hp&&deaths==0,"Transition is invulnerable");
            float began=Time.unscaledTime;float originalScale=Time.timeScale;
            yield return Until(transition,.17f);Capture("02-collapse");
            Check(camera.GetComponent<Unity.Cinemachine.CinemachineBrain>()&&boss.GetComponent<SpringBoarCinematic>().Active,"Cinemachine drives transition camera");
            Check(visual.GetCurrentAnimatorStateInfo(0).IsName("Metamorphosis"),"Dedicated transition animation plays");
            yield return Until(transition,.33f);Capture("03-charge");
            yield return Until(transition,.56f);Capture("04-roar");
            Check(visual.speed<.5f,"Reveal animation is slowed below half speed");
            var strips=boss.transform.Find("SpringBoar peeling fragments");
            var stripsRenderer=strips?strips.GetComponent<MeshRenderer>():null;
            Check(stripsRenderer&&Vector3.Distance(stripsRenderer.bounds.center,boss.transform.position)<20,"Peeling fragments stay close to the body");
            yield return Until(transition,.70f);Capture("05-peeling");
            yield return Until(transition,.85f);Capture("06-landing");
            Check(transition.HealthReveal>0&&transition.HealthReveal<1,"Health bar refills during landing");
            float deadline=Time.time+8;
            while(transition.Transitioning&&Time.time<deadline) yield return null;
            Capture("07-skeleton");
            Check(Time.unscaledTime-began>7,"Transformation has a longer cinematic duration");
            Check(Time.timeScale==originalScale&&visual.speed==1,"Time scale and animation speed restored");
            Check(!boss.GetComponent<SpringBoarCinematic>().Active,"Cinematic camera released after transition");
            Check(!transition.Transitioning&&skeleton.activeSelf&&!flesh.activeSelf,"Skeleton completes transition");
            Check(health.Current==health.maxHealth&&!health.invulnerable,"Second health bar restored");
            Check(!brain.enabled&&boss.GetComponent<SpringBoarPhaseTwo>().Active,"Phase one stops and phase two activates");
            Check(!transition.TryBeginNextPhase(),"Transition cannot repeat");
            Check(deaths==0,"No early room-clear death event");
            health.practiceTarget=true; // Avoid changing the user's run economy during this diagnostic.
            health.Damage(health.maxHealth+1);
            Check(boss.activeSelf&&boss.GetComponent<SpringBoarDeath>().Dying&&deaths==0,"Final hit starts death animation before clear event");
            StartCoroutine(TrackCollapse(skeleton));
            yield return new WaitForSeconds(1.4f);Capture("08-death-fall");
            Check(boss.GetComponent<SpringBoarCinematic>().Active,"Death uses Cinemachine framing");
            yield return new WaitForSeconds(boss.GetComponent<SpringBoarDeath>().Duration-1.4f+.4f);Capture("09-corpse");
            Check(!boss.GetComponent<SpringBoarCinematic>().Active,"Death releases cinematic camera");
            var corpseSkin=skeleton.GetComponentInChildren<SkinnedMeshRenderer>();var baked=new Mesh();corpseSkin.BakeMesh(baked);
            float minY=float.MaxValue;foreach(var v in baked.vertices)minY=Mathf.Min(minY,(corpseSkin.transform.position+corpseSkin.transform.rotation*v).y);
            Check(Mathf.Abs(minY-(-.35f+.02f))<.015f,"Corpse rests on actual floor with 2cm clearance");Destroy(baked);
            Check(health.Dead&&deaths==1,"Second depletion is final death exactly once");
            Check(boss.activeSelf&&boss.GetComponent<SpringBoarDeath>().Corpse,"Corpse remains active after death");
            Check(skeleton.GetComponent<Animator>().speed==0,"Corpse holds final animation pose");
            bool collidersOff=true;foreach(var collider in boss.GetComponentsInChildren<Collider>())collidersOff&=!collider.enabled;
            Check(collidersOff&&!boss.GetComponent<SpringBoarPhaseTwo>().Active,"Corpse has no combat or collision");
            yield return new WaitForSeconds(2.2f);Check(boss.activeSelf&&health.Dead&&deaths==1,"Corpse persists without respawn or repeated death event");
            System.IO.File.WriteAllLines("Builds/SpringBoarPhaseValidation/results.txt",results);
            Debug.Log("SpringBoarPhaseValidation: "+string.Join("; ",results));
            if(previousMain)previousMain.tag="MainCamera";
            Destroy(boss);Destroy(floor);Destroy(camera.gameObject);target.Release();Destroy(target);Destroy(gameObject);
        }
        void Capture(string name)
        {
            camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;
            var texture=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
            texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);texture.Apply();
            System.IO.File.WriteAllBytes("Builds/SpringBoarPhaseValidation/"+name+".png",texture.EncodeToPNG());
            RenderTexture.active=previous;Destroy(texture);
        }
        IEnumerator TrackCollapse(GameObject skeleton)
        {
            Transform hips=null;foreach(var bone in skeleton.GetComponentsInChildren<Transform>())if(bone.name=="Hips")hips=bone;
            float start=hips.position.y,previous=start,maxRise=0;var origin=boss.transform.position;
            while(boss.GetComponent<SpringBoarDeath>().Dying)
            {
                yield return new WaitForEndOfFrame();
                maxRise=Mathf.Max(maxRise,hips.position.y-previous);previous=hips.position.y;
            }
            Check(maxRise<.025f,"Pelvis never rises during collapse (max rise "+maxRise.ToString("F3")+"m)");
            Check(start-previous>1.2f,"Body visibly drops instead of folding in midair (drop "+(start-previous).ToString("F2")+"m)");
            Check(Vector3.Distance(origin,boss.transform.position)<.01f,"Death stays at original world position");
        }
        IEnumerator Until(SpringBoarPhaseTransition transition,float progress)
        {
            float deadline=Time.realtimeSinceStartup+20;
            while(transition.Transitioning&&transition.Progress<progress&&Time.realtimeSinceStartup<deadline)yield return null;
        }
    }
}
#endif
