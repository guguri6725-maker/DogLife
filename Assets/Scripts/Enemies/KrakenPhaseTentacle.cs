using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace DogLife
{
    [RequireComponent(typeof(CombatHealth))]
    public sealed class KrakenPhaseTentacle : CombatPhaseTransition
    {
        public enum Pose { Hidden, Emerging, Idle, Ready, Slam, Ground, Capture, Sweep, Dying, Throw, Recover, SweepReady }
        [SerializeField,Min(0)] float sweepSurfaceLift=.55f;
        [SerializeField] Transform visual;
        [SerializeField] AnimationClip idleClip;
        [SerializeField] Mesh longReachMesh;
        SkinnedMeshRenderer skin;Mesh originalMesh;
        [SerializeField] float submergedDepth=9, idleRate=1, rippleRadius=2;
        [Header("Motion phrasing: anticipation, travelling force, contact, settle")]
        [SerializeField,Range(0,.35f)] float jointLag=.18f;
        [SerializeField,Range(0,40)] float anticipationLean=24,arrivalCurl=32;
        [SerializeField,Range(0,8)] float settleDegrees=2.2f;
        [SerializeField,Range(.1f,.5f)] float withdrawalPause=.22f;
        readonly Quaternion[] outgoingPose=new Quaternion[18];
        float outgoingHeight,poseStarted;
        float restChainLength;
        readonly Vector3[] sampledDirections=new Vector3[17];
        public Vector3 TipPosition=>bones.Count>0?bones[bones.Count-1].position:transform.position;
        [SerializeField] Vector3 gripOffset=new Vector3(0,.2f,-.55f),releaseOffset=Vector3.zero;
        [Header("Tip coils around the torso; rotations only, no bone stretching")]
        [SerializeField,Range(220,350)] float coilAngle=260;
        [SerializeField] float coilRise=.28f,gripTorsoHeight=-.3f;
        public Pose State{get;private set;}=Pose.Hidden;
        public float PoseProgress{get;private set;}
        public CombatHealth Health{get;private set;}
        public KrakenPhaseOne Owner{get;private set;}
        public Transform GripPoint{get;private set;}
        public Transform ReleasePoint{get;private set;}
        public bool CombatSlot{get;private set;}
        public int LifeId{get;private set;}
        public bool Alive=>CombatSlot&&!reported&&State!=Pose.Hidden&&State!=Pose.Dying;
        public bool Available=>Alive&&State==Pose.Idle&&Time.time>=ReadyAt;
        public float ReadyAt;
        public KrakenPhaseWarning Warning{get;private set;}
        public BoxCollider AttackCollider{get;private set;}
        public void SetReach(float length)
        {
            float scale=Mathf.Max(.1f,length/Mathf.Max(1,restChainLength+.5f));transform.localScale=Vector3.one*scale;
            // The long rest mesh compensates radial size. Reach increases without a 2.5x thicker wall of flesh.
            if(skin)skin.sharedMesh=longReachMesh?longReachMesh:originalMesh;
            float radial=longReachMesh ? .4f : 1;
            for(int i=0;i<HitColliders.Count;i++)HitColliders[i].radius=localHitRadii[i]*radial;
        }
        public readonly List<CapsuleCollider> HitColliders=new List<CapsuleCollider>();
        readonly List<float> localHitRadii=new List<float>();
        readonly List<Transform> bones=new List<Transform>();
        readonly List<Quaternion> rest=new List<Quaternion>();
        bool reported;float height;Vector3 visualRest;
        public void Setup(KrakenPhaseOne owner,bool combat)
        {
            Owner=owner;CombatSlot=combat;Health=GetComponent<CombatHealth>();Health.maxHealth=owner.TentacleHP;Health.Restore();Health.invulnerable=true;
            if(!visual)visual=transform.GetChild(0);
            skin=visual.GetComponentInChildren<SkinnedMeshRenderer>();if(skin)originalMesh=skin.sharedMesh;
            if(!combat&&skin&&longReachMesh)skin.sharedMesh=longReachMesh;
            visualRest=visual.localPosition;
            foreach(var a in visual.GetComponentsInChildren<Animator>(true))a.enabled=false;
            var all=visual.GetComponentsInChildren<Transform>(true);
            for(int i=0;i<18;i++)foreach(var t in all)if(t.name=="Tendril"+i){bones.Add(t);rest.Add(t.localRotation);break;}
            for(int i=1;i<bones.Count;i++)restChainLength+=Vector3.Distance(bones[i-1].position,bones[i].position);
            var grip=new GameObject("GripPoint").transform;grip.SetParent(bones[Mathf.Min(13,bones.Count-1)],false);grip.localPosition=gripOffset;GripPoint=grip;
            ReleasePoint=new GameObject("ReleasePoint").transform;ReleasePoint.SetParent(GripPoint,false);ReleasePoint.localPosition=releaseOffset;
            if(combat)for(int i=0;i<bones.Count-1;i+=2)
            {
                // Imported FBX bones carry a 100x unit conversion. Sizes below are metres at setup scale,
                // not bone-local units; otherwise every tentacle's hurtbox overlaps the entire room.
                var scale=bones[i].lossyScale;float radialScale=Mathf.Max(.0001f,Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.z))),heightScale=Mathf.Max(.0001f,Mathf.Abs(scale.y));
                var hit=bones[i].gameObject.AddComponent<CapsuleCollider>();hit.direction=1;hit.radius=Mathf.Lerp(.85f,.3f,i/17f)/radialScale;hit.height=1.15f/heightScale;hit.center=Vector3.up*(.4f/heightScale);hit.enabled=false;HitColliders.Add(hit);localHitRadii.Add(hit.radius);
                if(owner.Player)foreach(var pc in owner.Player.GetComponents<Collider>())Physics.IgnoreCollision(hit,pc,true);
            }
            var attack=new GameObject("AttackCollider");attack.transform.SetParent(transform,false);AttackCollider=attack.AddComponent<BoxCollider>();AttackCollider.isTrigger=true;AttackCollider.enabled=false;
            Warning=KrakenPhaseWarning.Create(transform,owner.WarningMaterial);
            Health.RefreshVisuals();SetPose(Pose.Hidden,0);
            transform.localScale=Vector3.one;
        }
        public void SetPose(Pose pose,float progress)
        {
            if(State!=pose){for(int i=0;i<bones.Count;i++)outgoingPose[i]=bones[i].localRotation;outgoingHeight=visual?visual.localPosition.y-visualRest.y:0;poseStarted=Time.time;}
            State=pose;PoseProgress=Mathf.Clamp01(progress);
            bool exposed=Alive&&(pose==Pose.Idle||pose==Pose.Ready||pose==Pose.Slam||pose==Pose.Ground||pose==Pose.Recover);
            foreach(var c in HitColliders)c.enabled=exposed;
            if(Health)Health.invulnerable=!exposed;
            var bar=GetComponent<SeasonEnemyHealthBar>();if(bar)bar.enabled=exposed;
        }
        public void ResetSlot(Vector3 position)
        {
            StopAllCoroutines();transform.position=position;LifeId++;reported=false;Health.Restore();ReadyAt=Time.time+Owner.EntryGrace;
        }
        public override bool TryBeginNextPhase()
        {
            if(!CombatSlot||reported)return true;
            reported=true;StopAllCoroutines();AttackCollider.enabled=false;Warning.Hide();SetPose(Pose.Dying,0);Owner.TentacleKilled(this);return true;
        }
        public IEnumerator Animate(Pose pose,float duration)
        {
            float t=0;while(t<duration){SetPose(pose,t/Mathf.Max(.01f,duration));t+=Time.deltaTime;yield return null;}SetPose(pose,1);yield return new WaitForEndOfFrame();
        }
        void LateUpdate()
        {
            if(!Owner||bones.Count==0||CombatFeedback.Paused(this))return;
            // One writer owns the final pose. The authored idle is sampled before bounded additives.
            if(idleClip)idleClip.SampleAnimation(visual.gameObject,Mathf.Repeat(Time.time*idleRate,idleClip.length));
            else for(int i=0;i<bones.Count;i++)bones[i].localRotation=rest[i];
            float q=PoseProgress;
            float rise=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.08f,.88f,q));
            float sink=Mathf.SmoothStep(0,1,Mathf.InverseLerp(withdrawalPause,1,q));
            height=State==Pose.Hidden?-submergedDepth:State==Pose.Emerging?-submergedDepth*(1-rise):State==Pose.Dying?Mathf.Lerp(outgoingHeight,-submergedDepth,sink):State==Pose.Sweep||State==Pose.SweepReady?sweepSurfaceLift/Mathf.Max(.01f,transform.lossyScale.y):0;
            visual.localPosition=visualRest+Vector3.up*height;visual.localScale=Vector3.one;
            if(State==Pose.Capture||State==Pose.Throw)PoseCoil(q);
            else if(State==Pose.Ready||State==Pose.Slam||State==Pose.Ground||State==Pose.Sweep||State==Pose.SweepReady||State==Pose.Recover)PoseStrike(q);
            else if(State==Pose.Emerging)PoseArrival(q);
            else if(State==Pose.Dying){
                for(int i=0;i<bones.Count;i++){
                    float u=i/(float)(bones.Count-1),release=Mathf.SmoothStep(0,1,Mathf.InverseLerp(u*.12f,.65f,q));
                    Quaternion limp=rest[i]*Quaternion.Euler(Mathf.Sin(u*Mathf.PI)*2.5f,0,Mathf.Sin(u*5)*1.2f);
                    bones[i].localRotation=Quaternion.Slerp(outgoingPose[i],limp,release);
                }
            }
        }
        void PoseArrival(float q)
        {
            for(int i=0;i<bones.Count-1;i++)sampledDirections[i]=(bones[i+1].position-bones[i].position).normalized;
            for(int i=0;i<bones.Count;i++)bones[i].localRotation=rest[i];
            for(int i=0;i<bones.Count-1;i++){
                float u=i/(float)(bones.Count-2),open=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.12f+u*jointLag,.88f,q));
                Vector3 folded=Quaternion.AngleAxis(arrivalCurl*Mathf.Sin(u*Mathf.PI*1.35f),transform.right)*Vector3.up;
                float settle=Mathf.Sin((q-.75f)*24-u*2)*Mathf.Clamp01((q-.75f)*4)*(1-q)*settleDegrees;
                Vector3 desired=Quaternion.AngleAxis(settle,transform.right)*Vector3.Slerp(folded,sampledDirections[i],open);
                Vector3 current=bones[i+1].position-bones[i].position;
                if(current.sqrMagnitude>.00001f)bones[i].rotation=Quaternion.FromToRotation(current,desired)*bones[i].rotation;
            }
        }
        void PoseStrike(float q)
        {
            // Solve the authored chain with rotations only. No animated scale or stretched joints.
            for(int i=0;i<bones.Count-1;i++)sampledDirections[i]=(bones[i+1].position-bones[i].position).normalized;
            for(int i=0;i<bones.Count;i++)bones[i].localRotation=rest[i];
            float down=State==Pose.Ready?0:State==Pose.Slam?Mathf.SmoothStep(0,1,q):State==Pose.SweepReady?Mathf.SmoothStep(0,1,Mathf.Clamp01(q/.75f)):1;
            float prepare=State==Pose.Ready?Mathf.SmoothStep(0,1,Mathf.InverseLerp(.08f,.82f,q)):1;
            for(int i=0;i<bones.Count-1;i++){
                float u=i/(float)(bones.Count-2);
                Vector3 ready=Quaternion.AngleAxis(-anticipationLean*(.3f+.7f*Mathf.Sin(u*Mathf.PI))*prepare,transform.right)*Vector3.up;
                Vector3 ground=(transform.forward+Vector3.up*(i==0?.28f:Mathf.Cos(u*Mathf.PI)*.035f)).normalized;
                float travelling=State==Pose.Slam?Mathf.SmoothStep(0,1,Mathf.InverseLerp(.08f+u*jointLag,1,q)):down;
                Vector3 direction=Vector3.Slerp(ready,ground,travelling);
                if(State==Pose.Ready)direction=Vector3.Slerp(sampledDirections[i],ready,prepare);
                if(State==Pose.Ground){float recoil=Mathf.Sin(q*24-u*1.5f)*Mathf.Sin(Mathf.Clamp01(q*8)*Mathf.PI*.5f)*Mathf.Exp(-q*10)*Mathf.Sin(u*Mathf.PI)*settleDegrees;direction=Quaternion.AngleAxis(-recoil,transform.right)*ground;}
                if(State==Pose.Recover){float recover=Mathf.SmoothStep(0,1,Mathf.InverseLerp(u*jointLag,1,q));direction=Vector3.Slerp(ground,sampledDirections[i],recover);}
                if(State==Pose.Sweep){float drag=Mathf.Sin(q*Mathf.PI)*Mathf.Sin(u*Mathf.PI)*5;direction=Quaternion.AngleAxis(-drag,Vector3.up)*direction;}
                Vector3 current=bones[i+1].position-bones[i].position;
                if(current.sqrMagnitude>.00001f)bones[i].rotation=Quaternion.FromToRotation(current,direction)*bones[i].rotation;
            }
        }
        void PoseCoil(float progress)
        {
            if(bones.Count<18)return;
            // Eliminate sampled idle twist while wrapping: one continuous, length-preserving chain.
            for(int i=0;i<bones.Count;i++)bones[i].localRotation=rest[i];
            float close=State==Pose.Throw?1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.65f,1,progress)):progress;
            float bodyBlend=State==Pose.Throw?1:Mathf.SmoothStep(0,1,progress);
            // Close first, then lift the captive. Throw starts from that held pose without snapping.
            float heldLift=6;
            float lift=State==Pose.Throw?heldLift+Mathf.Sin(progress*Mathf.PI)*18:heldLift*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.55f,1,progress));
            if(State==Pose.Capture&&progress>=1)lift+=Mathf.Sin((Time.time-poseStarted)*2)*.45f;
            var forward=transform.forward;var right=transform.right;
            for(int i=0;i<8;i++)
            {
                float lean=(i<4?i*5:15+(i-3)*14)-lift;
                Vector3 direction=Quaternion.AngleAxis(lean,right)*Vector3.up;
                Vector3 current=bones[i+1].position-bones[i].position;
                if(current.sqrMagnitude>.00001f)bones[i].rotation=Quaternion.FromToRotation(current,Vector3.Slerp(current.normalized,direction,bodyBlend))*bones[i].rotation;
            }
            float length=0;for(int i=8;i<17;i++)length+=Vector3.Distance(bones[i].position,bones[i+1].position);
            float step=coilAngle*Mathf.Deg2Rad/9f,radius=(length/9f)/(2*Mathf.Sin(step*.5f));
            Vector3 center=bones[8].position+right*radius+Vector3.up*(coilRise*.5f);
            for(int i=8;i<17;i++)
            {
                float angle=(i-8+.5f)*step;
                Vector3 tangent=(forward*Mathf.Cos(angle)+right*Mathf.Sin(angle)+Vector3.up*(coilRise/Mathf.Max(.1f,length))).normalized;
                float delayed=Mathf.SmoothStep(0,1,Mathf.InverseLerp((i-8)*.018f,1,close));
                Vector3 current=bones[i+1].position-bones[i].position;
                Vector3 desired=Vector3.Slerp(current.normalized,tangent,delayed);
                if(current.sqrMagnitude>.00001f)bones[i].rotation=Quaternion.FromToRotation(current,desired)*bones[i].rotation;
            }
            // Root position sits below the coil centre so the loop surrounds the chest, not the feet.
            GripPoint.position=center-Vector3.up*gripTorsoHeight;GripPoint.rotation=Quaternion.LookRotation(forward,Vector3.up);
        }
        public void StopAttack(){var sound=GetComponent<SummerAudioEmitter>();if(sound)sound.StopAll();StopAllCoroutines();if(AttackCollider)AttackCollider.enabled=false;if(Warning)Warning.Hide();}
        void OnDisable(){StopAttack();foreach(var c in HitColliders)if(c)c.enabled=false;}
    }
}
