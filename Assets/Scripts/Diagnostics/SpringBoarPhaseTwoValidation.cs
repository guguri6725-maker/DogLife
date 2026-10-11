#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;
namespace DogLife
{
    public sealed class SpringBoarPhaseTwoValidation:MonoBehaviour
    {
        readonly List<string> results=new List<string>();
        const BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Instance;
        SpringBoarPhaseTwo two;GameObject boss;Camera camera;RenderTexture texture;
        void Check(bool value,string label){results.Add((value?"PASS ":"FAIL ")+label);}
        void Set(string name,object value){typeof(SpringBoarPhaseTwo).GetField(name,Flags).SetValue(two,value);}
        IEnumerator Pattern(string name)=> (IEnumerator)typeof(SpringBoarPhaseTwo).GetMethod(name,Flags).Invoke(two,null);
        IEnumerator Start()
        {
            while(ForestRoomStreaming.Instance&&!ForestRoomStreaming.Instance.Ready)yield return null;
            var player=FindFirstObjectByType<PlayerMotor>();var hp=player.GetComponent<CombatHealth>();var rb=player.GetComponent<Rigidbody>();
            var pos=player.transform.position;bool input=player.readInput,inv=hp.invulnerable,enabled=player.enabled,kinematic=rb.isKinematic;
            var main=Camera.main;bool follow=main&&main.GetComponent<PlayerCamera>()&&main.GetComponent<PlayerCamera>().enabled;
            GameObject floor=null,wall=null;
            try
            {
                hp.Restore();hp.invulnerable=true;player.readInput=false;player.enabled=true;rb.isKinematic=true;
                floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(1000,-.55f,1000);floor.transform.localScale=new Vector3(90,1,90);floor.layer=8;
                boss=Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/monster/SpringBoarBoss.prefab"),new Vector3(1000,0,1000),Quaternion.identity);
                boss.GetComponent<SpringBoarBoss>().enabled=false;boss.GetComponent<UnityEngine.AI.NavMeshAgent>().enabled=false;
                var transition=boss.GetComponent<SpringBoarPhaseTransition>();var so=new SerializedObject(transition);var skeleton=(GameObject)so.FindProperty("skeletonBody").objectReferenceValue;
                ((GameObject)so.FindProperty("fleshBody").objectReferenceValue).SetActive(false);skeleton.SetActive(true);
                var animator=(Animator)so.FindProperty("skeletonAnimator").objectReferenceValue;
                two=boss.GetComponent<SpringBoarPhaseTwo>();player.transform.position=boss.transform.position+Vector3.forward*7;
                two.Activate(animator);two.StopAllCoroutines();
                foreach(var state in new[]{"P2SweepReady","P2Sweep","P2Brake","P2BloomReady","P2Bloom","P2Storm"})Check(animator.HasState(0,Animator.StringToHash(state)),"Animation "+state);
                camera=new GameObject("PhaseTwoValidationCamera").AddComponent<Camera>();camera.transform.position=boss.transform.position+new Vector3(22,22,27);camera.transform.LookAt(boss.transform.position+Vector3.up*2);camera.fieldOfView=53;
                texture=new RenderTexture(1280,720,24);camera.targetTexture=texture;if(main)main.tag="Untagged";camera.tag="MainCamera";
                System.IO.Directory.CreateDirectory("Builds/SpringBoarPhaseTwoValidation");
                var sample=new SpringBoarPhaseTwo.Hazard{shape=SpringBoarPhaseTwo.Shape.Sector,origin=Vector3.zero,forward=Vector3.forward,range=12,angle=42};
                Check(sample.Contains(Vector3.forward*8,0)&&!sample.Contains(Quaternion.Euler(0,45,0)*Vector3.forward*8,0),"Root danger sector and diagonal safe corridor match");
                Set("nextSpike",Time.time);yield return null;yield return null;
                var spike=two.Hazards.Find(h=>h.ambient);Check(spike!=null,"Ambient spike starts independently");
                var locked=spike==null?Vector3.zero:spike.origin;player.transform.position+=Vector3.right*4;yield return null;
                Check(spike!=null&&spike.origin==locked,"Spike warning stops tracking after placement");Capture("01-spike-warning");
                yield return new WaitForSeconds(1.05f);Capture("02-spike-erupt");
                two.Hazards.Clear();Set("nextSpike",Time.time+1000);
                player.transform.position=boss.transform.position+Vector3.forward*7;
                var routine=two.StartCoroutine(Pattern("Tusks"));yield return new WaitForSeconds(.65f);Capture("03-sweep-warning");yield return new WaitForSeconds(.8f);Capture("04-sweep");yield return routine;
                Check(two.Hazards.Count==0,"Two tusk strikes clean up all hit areas");
                routine=two.StartCoroutine(Pattern("Bloom"));yield return new WaitForSeconds(.65f);Check(two.Hazards.Count==4,"Bloom creates four branches");Capture("05-bloom-warning");yield return new WaitForSeconds(1.25f);Capture("06-bloom");yield return routine;
                wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.layer=8;wall.transform.position=boss.transform.position+Vector3.forward*15+Vector3.up*5;wall.transform.localScale=new Vector3(30,10,1);Physics.SyncTransforms();player.transform.position=boss.transform.position+Vector3.forward*13;
                routine=two.StartCoroutine(Pattern("DoubleCharge"));float deadline=Time.time+8;while(!two.Stunned&&Time.time<deadline)yield return null;
                Check(two.Stunned&&two.VulnerableWindow&&!two.Charging,"Charge wall hit interrupts combo and opens punish window");Check(two.Hazards.Count==0,"Wall stun clears ambient hazards");Capture("07-wall-stun");yield return routine;wall.SetActive(false);
                boss.transform.position=new Vector3(1000,0,1000);player.transform.position=boss.transform.position+Vector3.forward*7;
                typeof(SpringBoarPhaseTwo).GetProperty("Special").SetValue(two,true);two.Hazards.Clear();Set("nextSpike",Time.time);
                routine=two.StartCoroutine(Pattern("Storm"));yield return new WaitForSeconds(2.1f);Check(!two.Hazards.Exists(h=>h.ambient),"Special suppresses ambient spikes");Capture("08-storm-warning");yield return new WaitForSeconds(.8f);Capture("09-storm-roots");
                deadline=Time.time+15;while(!two.VulnerableWindow&&Time.time<deadline)yield return null;
                Check(two.VulnerableWindow&&two.Hazards.Count==0,"Special finishes with safe groggy window");yield return routine;
                typeof(SpringBoarPhaseTwo).GetProperty("Special").SetValue(two,false);
                player.transform.position=boss.transform.position+Vector3.forward*8.5f;
                var counts=new int[3];var used=(int[])typeof(SpringBoarPhaseTwo).GetField("used",Flags).GetValue(two);
                for(int i=0;i<30;i++){int choice=(int)typeof(SpringBoarPhaseTwo).GetMethod("Choose",Flags).Invoke(two,null);if(choice>=0){counts[choice]++;used[choice]++;Set("previous",choice);}}
                Check(counts[0]>=8&&counts[1]>=8&&counts[2]>=8,"Eligible patterns fairly distributed: "+string.Join(",",counts));
                boss.GetComponent<CombatHealth>().Damage(1100);two.StartCoroutine(Pattern("Combat"));
                deadline=Time.time+5;while(!two.Special&&Time.time<deadline)yield return null;
                Check(two.Special,"HP below 50 percent triggers special automatically");
                var held=two.Hazards.Count>0?two.Hazards[0]:null;float age=held!=null?held.age:0;
                Time.timeScale=0;yield return new WaitForSecondsRealtime(.15f);Check(held==null||held.age==age,"Pause freezes hazard warning timers");Time.timeScale=1;
                deadline=Time.time+18;while(two.Special&&Time.time<deadline)yield return null;
                Check(!two.Special&&(bool)typeof(SpringBoarPhaseTwo).GetField("specialUsed",Flags).GetValue(two),"HP special completes and is consumed once");
                player.transform.position=boss.transform.position+Vector3.right*30;
                yield return new WaitForSeconds(2.2f);Check(two.Hazards.Exists(h=>h.ambient),"Ambient spikes resume after special grace period");
                Check(!two.Special,"Low HP does not repeat the special");
                two.gameObject.SetActive(false);Check(!two.Active&&two.Hazards.Count==0,"Disable clears attacks and scheduler");
            }
            finally
            {
                Time.timeScale=1;
                if(boss)Destroy(boss);if(floor)Destroy(floor);if(wall)Destroy(wall);if(camera)Destroy(camera.gameObject);if(texture){texture.Release();Destroy(texture);}
                if(main)main.tag="MainCamera";player.transform.position=pos;player.readInput=input;player.enabled=enabled;hp.invulnerable=inv;rb.isKinematic=kinematic;
                System.IO.File.WriteAllLines("Builds/SpringBoarPhaseTwoValidation/results.txt",results);Debug.Log(string.Join("; ",results));Destroy(gameObject);
            }
        }
        void Capture(string name)
        {camera.Render();var previous=RenderTexture.active;RenderTexture.active=texture;var image=new Texture2D(texture.width,texture.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);image.Apply();System.IO.File.WriteAllBytes("Builds/SpringBoarPhaseTwoValidation/"+name+".png",image.EncodeToPNG());RenderTexture.active=previous;Destroy(image);}
    }
}
#endif
