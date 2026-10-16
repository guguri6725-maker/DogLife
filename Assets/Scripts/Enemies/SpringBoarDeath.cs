using System.Collections;
using UnityEngine;
using UnityEngine.AI;
namespace DogLife
{
    [DefaultExecutionOrder(100)]
    public sealed class SpringBoarDeath:MonoBehaviour
    {
        [SerializeField,Min(.5f)] float deathDuration=5.2f;
        [SerializeField,Min(0)] float groundGap=.02f;
        SpringBoarCinematic cinematic;
        SkinnedMeshRenderer skin;Mesh contactMesh;
        readonly System.Collections.Generic.List<Vector3> contactVertices=new System.Collections.Generic.List<Vector3>();
        public float Duration=>deathDuration;
        [SerializeField,Min(.1f)] float poseBlend=.5f;
        [SerializeField] LayerMask sceneryMask=1<<8;
        [SerializeField,Min(0)] float landingShake=.16f;
        Transform[] bones;Vector3[] positions;Quaternion[] rotations;
        Transform hips;Animator actor;string state;float started;bool charging,landed;
        int side;
        float previousHipY;
        public float Progress {get;private set;}
        public Vector3 Contact=>hips?hips.position:transform.position;
        sealed class Support {public Transform upper,lower,foot;public Vector3 point;public bool planted;public float release;}
        readonly Support[] supports={new Support(),new Support()};
        public bool Dying {get;private set;}
        public bool Corpse {get;private set;}
        public bool Begin(Animator animator)
        {
            if(Dying||Corpse)return false;
            Dying=true;
            actor=animator;
            skin=actor?actor.GetComponentInChildren<SkinnedMeshRenderer>():null;
            if(skin)contactMesh=new Mesh();
            cinematic=GetComponent<SpringBoarCinematic>();
            var two=GetComponent<SpringBoarPhaseTwo>();charging=two&&two.Charging;if(two)two.enabled=false;
            var one=GetComponent<SpringBoarBoss>();if(one)one.enabled=false;
            var agent=GetComponent<NavMeshAgent>();if(agent)agent.enabled=false;
            foreach(var c in GetComponentsInChildren<Collider>())c.enabled=false;
            side=Clearance(transform.right)>=Clearance(-transform.right)?-1:1;
            state=side<0?"P2DeathLeft":"P2DeathRight";
            if(actor)
            {
                bones=actor.GetComponentsInChildren<Transform>();positions=new Vector3[bones.Length];rotations=new Quaternion[bones.Length];
                for(int i=0;i<bones.Length;i++){positions[i]=bones[i].localPosition;rotations[i]=bones[i].localRotation;if(bones[i].name=="Hips")hips=bones[i];}
                previousHipY=hips?hips.position.y:transform.position.y;
                for(int i=0;i<2;i++)
                {
                    string prefix=i==0?"LFront":"RFront";var support=supports[i];
                    foreach(var bone in bones){if(bone.name==prefix+"Upper")support.upper=bone;if(bone.name==prefix+"Lower")support.lower=bone;if(bone.name==prefix+"Foot")support.foot=bone;}
                    support.release=(i==0)==(side<0)?.23f:.36f;
                    if(support.foot&&Physics.Raycast(support.foot.position+Vector3.up*.3f,Vector3.down,out var ground,.9f,sceneryMask,QueryTriggerInteraction.Ignore)){support.point=support.foot.position;support.planted=!charging;}
                }
            }
            foreach(var fx in GetComponentsInChildren<SpringBoarVfx>(true)){fx.death=this;fx.gameObject.SetActive(true);}
            GetComponent<CombatHealth>().invulnerable=true;
            StartCoroutine(Fall(animator));return true;
        }
        IEnumerator Fall(Animator animator)
        {
            // CombatHealth must set phaseTransitionPending before the final death is completed.
            yield return null;
            started=Time.time;
            GameAudio.Play("Boar_Death",transform.position,.9f,follow:transform);
            // Let a transformation camera scheduled for destruction finish releasing first.
            if(cinematic)cinematic.BeginDeath();
            var feedback=GetComponent<CombatFeedback>();if(feedback)feedback.CancelHitStop();
            if(animator){animator.speed=1;animator.SetBool("Moving",false);animator.SetFloat("PhaseSpeed",1/deathDuration);animator.Play(state,0,0);}
            yield return new WaitForSeconds(deathDuration);
            if(animator){animator.Play(state,0,1);animator.Update(0);animator.speed=0;}
            Ground(1);
            // A zero-speed Animator still rewrites its pose every frame, undoing ground correction.
            if(animator)animator.enabled=false;
            Progress=1;foreach(var fx in GetComponentsInChildren<SpringBoarMetamorphosisVfx>(true))fx.Clear();
            Corpse=true;Dying=false;
            if(cinematic)cinematic.End();
            var health=GetComponent<CombatHealth>();health.CompleteDeferredDeath(true);
            // A corpse has no update work, including practice-target auto-respawn.
            health.enabled=false;
        }
        float Clearance(Vector3 direction)
        {
            float distance=5;
            foreach(var hit in Physics.SphereCastAll(transform.position+Vector3.up*2.6f,2,direction,5,sceneryMask,QueryTriggerInteraction.Ignore))
                if(!hit.collider.transform.IsChildOf(transform)&&!hit.collider.GetComponentInParent<CombatHealth>())distance=Mathf.Min(distance,hit.distance);
            return distance;
        }
        void LateUpdate()
        {
            if(!Dying||!actor||started<=0)return;
            float elapsed=Time.time-started;Progress=Mathf.Clamp01(elapsed/deathDuration);
            float blend=Mathf.SmoothStep(0,1,elapsed/poseBlend);
            for(int i=1;i<bones.Length&&blend<1;i++){bones[i].localPosition=Vector3.Lerp(positions[i],bones[i].localPosition,blend);bones[i].localRotation=Quaternion.Slerp(rotations[i],bones[i].localRotation,blend);}
            foreach(var support in supports)if(support.planted&&Progress<support.release)
            {
                float weight=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(support.release-.08f,support.release,Progress));
                Solve(support,Vector3.Lerp(support.foot.position,support.point,weight));
            }
            Ground(Mathf.SmoothStep(0,1,Mathf.InverseLerp(.1f,.4f,Progress)));
            if(cinematic)cinematic.SetProgress(Progress);
            foreach(var fx in GetComponentsInChildren<SpringBoarMetamorphosisVfx>(true))fx.DeathOpacity=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.65f,1,Progress));
            if(!landed&&Progress>=.66f){landed=true;GameAudio.Play("Boar_Ground_Slam",Contact,.5f,.85f);var camera=Camera.main;var follow=camera?camera.GetComponent<PlayerCamera>():null;if(follow)follow.AddImpactShake(landingShake*Mathf.Clamp01(1-Vector3.Distance(camera.transform.position,transform.position)/35),.25f);}
        }
        void Ground(float weight)
        {
            if(!skin||!hips||!contactMesh||weight<=0)return;
            float floor=transform.position.y;float nearest=float.MaxValue;
            foreach(var hit in Physics.RaycastAll(transform.position+Vector3.up*5,Vector3.down,15,sceneryMask,QueryTriggerInteraction.Ignore))
                if(!hit.collider.transform.IsChildOf(transform)&&!hit.collider.GetComponentInParent<CombatHealth>()&&hit.normal.y>.5f&&hit.distance<nearest){nearest=hit.distance;floor=hit.point.y;}
            skin.BakeMesh(contactMesh);contactMesh.GetVertices(contactVertices);float lowest=float.MaxValue;
            // Imported FBX bake vertices already include scale. Applying lossyScale again produces a false height.
            foreach(var v in contactVertices)lowest=Mathf.Min(lowest,(skin.transform.position+skin.transform.rotation*v).y);
            if(lowest<float.MaxValue)hips.position+=Vector3.up*Mathf.Min(0,floor+groundGap-lowest)*weight;
            var p=hips.position;p.y=Mathf.Min(p.y,previousHipY);hips.position=p;previousHipY=p.y;
        }
        void OnDisable(){if(cinematic&&Dying)cinematic.End();}
        void OnDestroy(){if(contactMesh)Destroy(contactMesh);}
        // 실제 사망의 접지 보정을 미리보기에도 공유하되 전투 종료와 카메라는 실행하지 않습니다.
        public void SamplePreview(Animator animator, float progress)
        {
            Dying = true; Progress = Mathf.Clamp01(progress);
            actor = animator;
            if (!skin) skin = actor.GetComponentInChildren<SkinnedMeshRenderer>();
            if (!hips) foreach (var bone in actor.GetComponentsInChildren<Transform>()) if (bone.name == "Hips") hips = bone;
            if (!contactMesh) contactMesh = new Mesh();
            previousHipY = hips ? hips.position.y : transform.position.y;
            Ground(Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.1f, .4f, progress)));
        }
        public void ResetPreview() { Dying = false; Progress = 0; }
        static void Solve(Support s,Vector3 target)
        {
            if(!s.upper||!s.lower||!s.foot)return;
            var a=s.upper.position;var b=s.lower.position;var c=s.foot.position;
            float ab=Vector3.Distance(a,b),bc=Vector3.Distance(b,c);var direction=(target-a).normalized;
            float d=Mathf.Clamp(Vector3.Distance(a,target),Mathf.Abs(ab-bc)+.001f,ab+bc-.001f);
            var bend=Vector3.ProjectOnPlane(b-a,direction).normalized;if(bend.sqrMagnitude<.01f)return;
            float x=(ab*ab+d*d-bc*bc)/(2*d);var knee=a+direction*x+bend*Mathf.Sqrt(Mathf.Max(0,ab*ab-x*x));
            s.upper.rotation=Quaternion.FromToRotation(b-a,knee-a)*s.upper.rotation;
            s.lower.rotation=Quaternion.FromToRotation(s.foot.position-s.lower.position,target-s.lower.position)*s.lower.rotation;
        }
    }
}
