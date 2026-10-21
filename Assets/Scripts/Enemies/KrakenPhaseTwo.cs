using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DogLife
{
    [DefaultExecutionOrder(100)]
    public sealed partial class KrakenPhaseTwo:CombatPhaseTransition
    {
        [Serializable] public class Pattern
        {
            [Min(.1f)] public float warning=1.6f,attack=.8f,recovery=1.7f,cooldown=8;
            [Min(0)] public float damage=1,knockback=4;
        }
        [Header("Existing body rig and natural, skinned tentacle")]
        [SerializeField] Transform bodyVisual;
        [SerializeField] GameObject armModel;
        [SerializeField] Mesh slimArmMesh;
        [SerializeField] Material armMaterial;
        [SerializeField] Material waterMaterial;
        [SerializeField] float bodyWidth=16.8f,bodyHeight=8.68f,bodyFloatHeight=.65f,armLength=30,armRadius=.68f;
        [SerializeField,Min(.1f)] float armSocketRadius=1.75f;
        // 음수 높이는 촉수 뿌리를 해저 아래에 숨깁니다.
        [SerializeField] float armSocketHeight=1.6f;
        [Header("Resting arms: clear view of the face")]
        [SerializeField] float idleArmRadius=10,idleArmHeight=3.2f;
        [SerializeField,Range(30,170)] float idleSideAngle=70;
        [SerializeField,Range(0,2)] float restingArmSeparation=.65f;
        [Header("Player movement barrier (world units)")]
        [SerializeField,Min(.1f)] float bodyBlockRadius=3.7f;
        CapsuleCollider bodyBarrier;
        Vector3 barrierLocalCenter;
        public Collider BodyBarrier=>bodyBarrier;
        [Header("Seabed reveal, after flooding has completed")]
        [SerializeField] float sandLead=2,emergeTime=5.5f,unfurlTime=2.5f,combatGrace=2,burialDepth=8;
        [SerializeField] float sandRadius=7,sandHeight=5,turnSpeed=60,idleSway=.12f;
        [Header("Pincer: locked horizontal layer; swim up or down")]
        [SerializeField] Pattern pincer=new Pattern{recovery=.65f};
        [SerializeField] float pincerReach=22,pincerHalfWidth=5,pincerHeight=2,pincerDepth=3,pincerContactWidth=1.5f,exposure=.65f;
        [Header("Pressure cannon: a locked three-dimensional line")]
        [SerializeField] Pattern cannon=new Pattern{warning=1.8f,attack=1,recovery=2,cooldown=9,knockback=6};
        [SerializeField] float beamRadius=1.1f,beamSpeed=22,beamDistance=45,beamLength=5,mouthHeight=5.18f,mouthForward=3.5f;
        [Header("Whirlpool: escapable current, pause, radial burst")]
        [SerializeField] Pattern vortex=new Pattern{warning=2,attack=3,recovery=2.2f,cooldown=15,knockback=8};
        [SerializeField] float suctionRadius=22,suctionSpeed=1.6f,burstRadius=13,pauseBeforeBurst=.9f;
        [SerializeField,Min(0)] float vortexArmRotationSpeed=34;
        [SerializeField,Min(.1f)] float pressureBurstDuration=.85f;
        [Header("Horizontal sweep: alternate height has its own warning")]
        [SerializeField] Pattern sweep=new Pattern{warning=1.65f,attack=1.4f,recovery=1.8f,cooldown=11,knockback=5};
        [SerializeField] float sweepRadius=22,sweepAngle=200,sweepHeight=1.8f,sweepWidth=1.5f,secondSweepHealth=.7f;
        [Header("HP special trigger and damage; once per encounter")]
        [SerializeField,Range(.05f,.95f)] float heartTrigger=.7f,heartSecondTrigger=.3f;
        [SerializeField] float heartDamage=1,heartKnockback=5,groggyTime=5;
        [Header("HP special: alternating tentacle slams")]
        [SerializeField,Min(1)] int heartSlamCount=12,heartSecondSlamCount=18;
        [SerializeField,Min(.1f)] float heartIntroDuration=1.2f;
        [SerializeField,Min(0)] float heartIntroLift=1.2f;
        [SerializeField,Min(.5f)] float heartOverheadClearance=2;
        [SerializeField,Min(.1f)] float heartSlamInterval=.6f,heartSlamWarning=.7f,heartSlamDuration=.22f,heartGroundHold=1.1f,heartArmReturn=.35f;
        [SerializeField,Min(.1f)] float heartSlamReach=24,heartSlamWidth=2.8f,heartRaiseHeight=8;
        public int HeartSlamsCompleted {get;private set;}
        [SerializeField] float deathTime=4;
        public enum Phase{Dormant,Reveal,Idle,Pincer,Cannon,Vortex,Sweep,Heart,Groggy,Dead}
        public Phase State{get;private set;}
        public int HeartExecutions{get;private set;}
        public int CompletedPatterns{get;private set;}
        public IReadOnlyList<KrakenDeepArm> Arms=>arms;
        public bool AutomaticAttacks=true;
        SummerAudioEmitter Sound=>SummerAudioEmitter.Get(this);
        readonly List<KrakenDeepArm> arms=new List<KrakenDeepArm>();
        readonly List<KrakenDeepVfx> effects=new List<KrakenDeepVfx>();
        readonly List<Collider> hurtboxes=new List<Collider>();
        readonly Collider[] overlaps=new Collider[96];
        readonly float[] ready=new float[4];
        Transform pool;Animator animator;CombatHealth health,playerHealth;PlayerMotor player;PlayerWaterMovement swim;
        KrakenPhaseOne first;Bounds arena;Vector3 bodyHome,center,facing=Vector3.right;Quaternion bodyRest;
        BoxCollider attackBox;bool running;int last=-1;
        public Bounds Arena=>arena;
        public Vector3 Center=>center;
        public IEnumerator Reveal(KrakenPhaseOne owner,KrakenSequenceCamera camera,float waterHeight)
        {
            first=owner;player=owner.Player;playerHealth=player.GetComponent<CombatHealth>();swim=player.GetComponent<PlayerWaterMovement>();
            health=GetComponent<CombatHealth>();var flood=owner.GetComponent<KrakenWaterVolume>();arena=flood?flood.UnderwaterBounds:owner.Arena;arena.SetMinMax(arena.min,new Vector3(arena.max.x,waterHeight-1,arena.max.z));
            State=Phase.Reveal;health.invulnerable=true;
            if(!bodyVisual)bodyVisual=transform.Find("RiggedBody");
            var legacy=bodyVisual.GetComponent<KrakenRigMotion>();if(legacy)legacy.enabled=false;
            animator=bodyVisual.GetComponent<Animator>();animator.enabled=true;animator.Play("Idle",0,0);animator.Update(0);
            bodyRest=bodyVisual.localRotation;
            foreach(var r in bodyVisual.GetComponentsInChildren<Renderer>(true))
            {
                r.enabled=true;r.forceRenderingOff=false;
                // The scaled body turns and deforms beyond its imported, fixed culling bounds.
                // Recalculate skinned bounds even after the old bounds have left the camera.
                if(r is SkinnedMeshRenderer bodySkin)bodySkin.updateWhenOffscreen=true;
            }
            // Measure the rest mesh in world units, rather than the inflated animation culling bounds.
            var skin=bodyVisual.GetComponentInChildren<SkinnedMeshRenderer>();var b=skin.sharedMesh.bounds;Bounds bounds=new Bounds(skin.transform.TransformPoint(b.center),Vector3.zero);
            for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)bounds.Encapsulate(skin.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3(x,y,z))));
            float fit=Mathf.Min(bodyWidth/Mathf.Max(.01f,bounds.size.x),bodyHeight/Mathf.Max(.01f,bounds.size.y));bodyVisual.localScale*=fit;
            Vector3 bottom=bodyVisual.position+(new Vector3(bounds.center.x,bounds.min.y,bounds.center.z)-bodyVisual.position)*fit;
            bodyVisual.position+=new Vector3(arena.center.x,arena.min.y+bodyFloatHeight,arena.center.z)-bottom;
            bodyHome=bodyVisual.position;center=new Vector3(arena.center.x,arena.min.y+mouthHeight,arena.center.z);
            pool=new GameObject("Kraken underwater combat").transform;pool.SetParent(transform,false);pool.localScale=new Vector3(1/transform.lossyScale.x,1/transform.lossyScale.y,1/transform.lossyScale.z);pool.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            for(int i=0;i<4;i++){
                var a=new GameObject("Phase two arm "+(i+1)).AddComponent<KrakenDeepArm>();a.transform.SetParent(pool,false);
                // Leave enough arc length to reach the announced outer sweep at either height.
                float reach=Mathf.Max(armLength,Mathf.Sqrt(Mathf.Pow(sweepRadius+armSocketRadius,2)+Mathf.Pow(arena.size.y*.72f-armSocketHeight,2))+.5f);
                a.Build(armModel,slimArmMesh,armMaterial,reach,armRadius);
                float angle=i==0?idleSideAngle:i==1?-idleSideAngle:i==2?140:-140;
                Vector3 socket=new Vector3(center.x,arena.min.y+armSocketHeight,center.z)+Quaternion.AngleAxis(angle,Vector3.up)*facing*armSocketRadius;
                a.Attach(bodyVisual,socket);arms.Add(a);
            }
            // Every hurtbox resolves to the same existing root health. No tentacle-local health is created.
            for(int i=0;i<3;i++){
                var h=new GameObject("Body hurtbox "+i);h.transform.SetParent(bodyVisual,false);h.transform.position=new Vector3(center.x,arena.min.y+1.5f+i*1.6f,center.z);
                var c=h.AddComponent<SphereCollider>();c.isTrigger=true;c.radius=(i==1?2.7f:2.2f)/h.transform.lossyScale.x;c.enabled=false;h.AddComponent<MonsterHurtbox>();hurtboxes.Add(c);
            }
            var attack=new GameObject("Phase two DamageArea");attack.transform.SetParent(pool,false);attackBox=attack.AddComponent<BoxCollider>();attackBox.isTrigger=true;attackBox.enabled=false;
            BuildBodyBarrier();
            bodyVisual.position=bodyHome-Vector3.up*burialDepth;SetIdleArms(0);
            var sand=Effect(KrakenDeepVfx.Form.Sand,new Vector3(center.x,arena.min.y+.1f,center.z));sand.Size=new Vector3(sandRadius,sandHeight,0);sand.Intensity=0;
            camera.BeginReveal(center,waterHeight);Sound.Cue("Kraken_Sand_Rise",.75f);
            Play("Roar",emergeTime+unfurlTime);
            float duration=sandLead+emergeTime+unfurlTime;bool emergedSound=false,roaredSound=false;
            for(float t=0;t<duration;t+=Time.deltaTime){
                if(!emergedSound&&t>=sandLead){emergedSound=true;Sound.Cue("Kraken_Body_Emerge");}
                if(!roaredSound&&t>=sandLead+emergeTime*.6f){roaredSound=true;Sound.Cue("Kraken_Roar",.8f);}
                float rise=Mathf.SmoothStep(0,1,Mathf.InverseLerp(sandLead,sandLead+emergeTime,t));
                bodyVisual.position=bodyHome-Vector3.up*burialDepth*(1-rise);
                sand.Intensity=Mathf.SmoothStep(0,1,t/sandLead)*(1-Mathf.InverseLerp(sandLead+emergeTime*.5f,duration-1,t));
                SetIdleArms(rise);camera.SetRevealProgress(t/duration,bodyVisual.position+Vector3.up*2);yield return null;
            }
            bodyVisual.position=bodyHome;ClearEffects();SetIdleArms(1);health.RefreshVisuals();
            foreach(var h in hurtboxes)h.enabled=true;foreach(var a in arms)a.SetExposed(true);
            bodyBarrier.enabled=true;
            var bar=GetComponent<SeasonEnemyHealthBar>();if(bar){bar.SetWorldAnchor(hurtboxes[hurtboxes.Count-1].transform);bar.enabled=true;}
            State=Phase.Idle;Play("Idle",1);camera.End();
        }
        public void BeginCombat(){if(State!=Phase.Idle)return;health.invulnerable=false;running=true;StartCoroutine(CombatLoop());}
        void BuildBodyBarrier()
        {
            // The unit-scale pool keeps collider dimensions independent of the imported model scale.
            var go=new GameObject("Kraken body movement barrier");go.transform.SetParent(pool,false);
            go.layer=gameObject.layer;
            bodyBarrier=go.AddComponent<CapsuleCollider>();bodyBarrier.isTrigger=false;
            bodyBarrier.direction=1;bodyBarrier.radius=bodyBlockRadius;
            bodyBarrier.height=Mathf.Max(bodyHeight,bodyBlockRadius*2);
            go.transform.position=new Vector3(center.x,arena.min.y+bodyFloatHeight+bodyBarrier.height*.5f,center.z);
            barrierLocalCenter=bodyVisual.InverseTransformPoint(go.transform.position);
            bodyBarrier.enabled=false;
        }
        void LateUpdate()
        {
            if(bodyBarrier&&bodyVisual)bodyBarrier.transform.position=bodyVisual.TransformPoint(barrierLocalCenter);
        }
        IEnumerator CombatLoop()
        {
            yield return new WaitForSeconds(combatGrace);
            while(running&&!health.Dead){
                // 근접 누적 무력화로 생긴 빈틈에는 다음 패턴을 시작하지 않습니다.
                if(CombatFeedback.Paused(this)){yield return null;continue;}
                if(!AutomaticAttacks){yield return null;continue;}
                if(HeartExecutions<2&&health.Current<=health.maxHealth*(HeartExecutions==0?heartTrigger:heartSecondTrigger)){yield return Heart();continue;}
                int choice=-1;var available=new List<int>();
                for(int i=0;i<4;i++)if(i!=last&&Time.time>=ready[i])available.Add(i);
                if(available.Count>0)choice=available[UnityEngine.Random.Range(0,available.Count)];
                if(choice<0){yield return null;continue;}
                last=choice;yield return RunPattern(choice);CompletedPatterns++;
            }
        }
        IEnumerator RunPattern(int index)
        {
            if(index==0)yield return Pincer();else if(index==1)yield return Cannon();else if(index==2)yield return Vortex();else yield return Sweep();
            ready[index]=Time.time+Settings(index).cooldown;State=Phase.Idle;Play("Idle",1);ClearEffects();
        }
        Pattern Settings(int i)=>i==0?pincer:i==1?cannon:i==2?vortex:sweep;
        Vector3 FlatAim(){var d=Vector3.ProjectOnPlane(player.transform.position-center,Vector3.up);return d.sqrMagnitude>.01f?d.normalized:facing;}
        void Update()
        {
            for(int i=0;i<arms.Count;i++)arms[i].SetReadability(State==Phase.Pincer&&i<2||State==Phase.Sweep&&i==2||State==Phase.Heart ? 1f : .15f);
            if(State==Phase.Idle||State==Phase.Groggy){
                if(State==Phase.Idle)facing=Vector3.RotateTowards(facing,FlatAim(),turnSpeed*Mathf.Deg2Rad*Time.deltaTime,0);
                bodyVisual.position=bodyHome+Vector3.up*Mathf.Sin(Time.time*.7f)*idleSway;SetIdleArms(1);
            }
            if(bodyVisual&&State!=Phase.Dormant&&State!=Phase.Reveal&&State!=Phase.Dead){
                var target=Quaternion.LookRotation(facing)*bodyRest;bodyVisual.rotation=Quaternion.RotateTowards(bodyVisual.rotation,target,turnSpeed*Time.deltaTime);
            }
        }
        void SetIdleArms(float emergence)
        {
            for(int i=0;i<arms.Count;i++){
                IdleTargets(i,out var start,out var end,out var curve);Vector3 buried=Vector3.up*burialDepth*(1-emergence);
                arms[i].SetShape(start-buried,end-buried,curve,true);
            }
        }
        void IdleTargets(int i,out Vector3 start,out Vector3 end,out Vector3 curve)
        {
            float angle=i==0?idleSideAngle:i==1?-idleSideAngle:i==2?140:-140;Vector3 dir=Quaternion.AngleAxis(angle,Vector3.up)*facing;
            start=arms[i].SocketPosition;
            end=new Vector3(center.x,arena.min.y+idleArmHeight+(i<2?-.35f:.55f)*restingArmSeparation+Mathf.Sin(Time.time*.7f+i)*.2f,center.z)+dir*(idleArmRadius+restingArmSeparation);
            curve=RestingArmCurve(dir,facing,i);
        }
        Vector3 RestingArmCurve(Vector3 dir,Vector3 forward,int index)
        {
            // Route the slack toward the sides and rear, leaving the face clear.
            float side=index%2==0?1:-1;
            return Vector3.Cross(Vector3.up,forward)*side*2-forward*1.3f+Vector3.up*.25f;
        }
        void Play(string state,float duration){if(!animator)return;animator.SetFloat("PhaseSpeed",1/Mathf.Max(.1f,duration));animator.CrossFadeInFixedTime(state,.25f);}
        KrakenDeepVfx Effect(KrakenDeepVfx.Form shape,Vector3 p){var f=KrakenDeepVfx.Create(pool,waterMaterial?waterMaterial:first.WarningMaterial,shape);f.transform.position=p;effects.Add(f);return f;}
        void Remove(KrakenDeepVfx f){if(!f)return;effects.Remove(f);Destroy(f.gameObject);}
        void ClearEffects(){foreach(var f in effects)if(f)Destroy(f.gameObject);effects.Clear();if(attackBox)attackBox.enabled=false;if(pool)foreach(var w in pool.GetComponentsInChildren<KrakenPhaseWarning>())Destroy(w.gameObject);}
        bool Hit(float damage,Vector3 direction,float knockback)
        {
            if(!running||!playerHealth||playerHealth.Dead)return false;float before=playerHealth.Current;playerHealth.Damage(damage);
            if(playerHealth.Current<before&&!playerHealth.Dead){player.ApplyKnockback(direction,knockback,0,.4f);return true;}return false;
        }
        bool BoxHit(Vector3 p,Vector3 size,Quaternion rot,float damage,float force)
        {
            attackBox.transform.SetPositionAndRotation(p,rot);attackBox.size=size;attackBox.enabled=true;Physics.SyncTransforms();
            int n=Physics.OverlapBoxNonAlloc(p,size*.5f,overlaps,rot,~0,QueryTriggerInteraction.Collide);
            var hits=n==overlaps.Length?Physics.OverlapBox(p,size*.5f,rot,~0,QueryTriggerInteraction.Collide):overlaps;if(hits!=overlaps)n=hits.Length;
            for(int i=0;i<n;i++)if(hits[i].GetComponentInParent<PlayerMotor>()==player)return Hit(damage,player.transform.position-center,force);
            return false;
        }
        bool BeamHit(Vector3 from,Vector3 to,float radius,float damage,float force)
        {
            int n=Physics.OverlapCapsuleNonAlloc(from,to,radius,overlaps,~0,QueryTriggerInteraction.Collide);
            var hits=n==overlaps.Length?Physics.OverlapCapsule(from,to,radius,~0,QueryTriggerInteraction.Collide):overlaps;if(hits!=overlaps)n=hits.Length;
            for(int i=0;i<n;i++)if(hits[i].GetComponentInParent<PlayerMotor>()==player)return Hit(damage,to-from,force);return false;
        }
        IEnumerator Pincer()
        {
            State=Phase.Pincer;Sound.Prepare("Kraken_Underwater_Windup");facing=FlatAim();Play("Command",pincer.warning);var side=Vector3.Cross(Vector3.up,facing);
            float reach=Mathf.Clamp(Vector3.Dot(player.transform.position-center,facing),4,pincerReach);
            float y=Mathf.Clamp(player.transform.position.y+.6f,arena.min.y+1,arena.max.y-1);
            Vector3 target=new Vector3(center.x,y,center.z)+facing*reach;
            var warning=Effect(KrakenDeepVfx.Form.Band,target);warning.transform.rotation=Quaternion.LookRotation(facing);warning.Size=new Vector3(pincerHalfWidth*2+pincerContactWidth,pincerHeight,pincerDepth);
            Vector3[] fromRoots={arms[0].SamplePoints()[0],arms[1].SamplePoints()[0]},fromTips={arms[0].Tip,arms[1].Tip};
            for(float t=0;t<pincer.warning;t+=Time.deltaTime){
                float q=t/pincer.warning;warning.Progress=q;
                for(int i=0;i<2;i++){float s=i==0?1:-1;Vector3 start=center+side*s*3.5f;
                    float blend=Mathf.SmoothStep(0,1,q);arms[i].SetShape(Vector3.Lerp(fromRoots[i],start,blend),Vector3.Lerp(fromTips[i],target+side*s*pincerHalfWidth,blend),Vector3.up*Mathf.Sin(q*Mathf.PI)*.4f+side*s*2);}
                yield return null;
            }
            warning.Progress=1;warning.Impact=true;Sound.StopPreparation();Sound.Cue("Kraken_Underwater_Strike");Play("Wave",pincer.attack);bool hit=false;
            for(float t=0;t<pincer.attack;t+=Time.deltaTime){float q=Mathf.SmoothStep(0,1,t/pincer.attack);
                for(int i=0;i<2;i++){float s=i==0?1:-1;arms[i].SetShape(center+side*s*3.5f,target+side*s*Mathf.Lerp(pincerHalfWidth,.3f,q),side*s*2);}
                // The moving damage boxes follow the tips, not the entire warning all at once.
                if(!hit)for(int i=0;i<2;i++){float s=i==0?1:-1;hit|=BoxHit(target+side*s*Mathf.Lerp(pincerHalfWidth,.3f,q),new Vector3(pincerContactWidth,pincerHeight,pincerDepth),warning.transform.rotation,pincer.damage,pincer.knockback);}
                yield return null;
            }
            attackBox.enabled=false;Remove(warning);yield return new WaitForSeconds(exposure);yield return Recover(pincer.recovery);
        }
        IEnumerator Cannon()
        {
            State=Phase.Cannon;
            // Turn before locking the firing line, so the blast leaves the face rather than a shoulder.
            for(float t=0;t<180/Mathf.Max(1,turnSpeed)+.1f;t+=Time.deltaTime){facing=FlatAim();if(Quaternion.Angle(bodyVisual.rotation,Quaternion.LookRotation(facing)*bodyRest)<5)break;yield return null;}
            facing=FlatAim();Vector3 mouth=new Vector3(center.x,arena.min.y+mouthHeight,center.z)+facing*mouthForward;
            Vector3 direction=(player.transform.position+Vector3.up*.6f-mouth).normalized;Quaternion rotation=Quaternion.LookRotation(direction);
            float travel=beamDistance;if(Physics.Raycast(mouth,direction,out var wall,travel,1<<8,QueryTriggerInteraction.Ignore))travel=wall.distance;
            var line=Effect(KrakenDeepVfx.Form.Beam,mouth);line.transform.rotation=rotation;line.Size=new Vector3(beamRadius*2,0,travel);
            var orb=Effect(KrakenDeepVfx.Form.Orb,mouth);Sound.Prepare("Kraken_Pressure_Charge");Play("Roar",cannon.warning);
            for(float t=0;t<cannon.warning;t+=Time.deltaTime){float q=t/cannon.warning;line.Progress=q;orb.Size=Vector3.one*Mathf.Lerp(.2f,beamRadius,q);bodyVisual.position=bodyHome-facing*.3f*q;yield return null;}
            Remove(orb);line.Impact=true;Sound.StopPreparation();Sound.Cue("Kraken_Pressure_Burst");Play("Wave",cannon.attack);bool hit=false;
            for(float distance=0;distance<travel+beamLength;distance+=beamSpeed*Time.deltaTime){
                float front=Mathf.Min(travel,distance),back=Mathf.Max(0,distance-beamLength);if(back>=travel)break;
                line.transform.position=mouth+direction*back;line.Size=new Vector3(beamRadius*2,0,front-back);line.Progress=1;
                if(!hit&&front>back)hit=BeamHit(mouth+direction*back,mouth+direction*front,beamRadius,cannon.damage,cannon.knockback);
                bodyVisual.position=Vector3.Lerp(bodyHome-facing*.8f,bodyHome,Mathf.Clamp01(distance/travel));yield return null;
            }
            attackBox.enabled=false;Remove(line);yield return Recover(cannon.recovery);
        }
        IEnumerator Vortex()
        {
            State=Phase.Vortex;Sound.Prepare("Kraken_Pressure_Charge");Play("Command",vortex.warning);var water=Effect(KrakenDeepVfx.Form.Vortex,center);water.Size=new Vector3(suctionRadius,5,0);
            for(float t=0;t<vortex.warning;t+=Time.deltaTime){water.Progress=t/vortex.warning;water.Intensity=t/vortex.warning;SetVortexArms(VortexArmAngle(t));yield return null;}
            water.Impact=true;water.Progress=1;water.Intensity=1;Sound.StopPreparation();Sound.Sustain("Kraken_Flood_Loop",.4f);
            for(float t=0;t<vortex.attack;t+=Time.deltaTime){
                Vector3 d=center-(player.transform.position+Vector3.up*.6f);
                if(swim)swim.SetCurrent(d.magnitude<suctionRadius?d.normalized*suctionSpeed:Vector3.zero);
                SetVortexArms(VortexArmAngle(vortex.warning+t));
                yield return null;
            }
            if(swim)swim.SetCurrent(Vector3.zero);Remove(water);Sound.StopSustain();Sound.Prepare("Kraken_Pressure_Charge");
            // The brief intake pause has no full-sized sphere: only one outward pulse is shown.
            yield return new WaitForSeconds(pauseBeforeBurst);
            Sound.StopPreparation();Sound.Cue("Kraken_Pressure_Burst");Play("Wave",pressureBurstDuration);
            var burst=Effect(KrakenDeepVfx.Form.PressureBurst,center);burst.Size=Vector3.one*burstRadius;burst.Impact=true;
            bool hit=false;
            for(float t=0;t<pressureBurstDuration+Time.deltaTime;t+=Time.deltaTime)
            {
                float q=Mathf.Clamp01(t/pressureBurstDuration);burst.Progress=q;burst.Intensity=1-q*q;
                float front=burstRadius*KrakenDeepVfx.PressureFront(q);
                if(!hit&&Vector3.Distance(player.GetComponent<CapsuleCollider>().ClosestPoint(center),center)<=front)
                    hit=Hit(vortex.damage,player.transform.position-center,vortex.knockback);
                yield return null;
            }
            Remove(burst);yield return Recover(vortex.recovery);
        }
        float VortexArmAngle(float elapsed)
        {
            // Start from the idle pose with zero angular velocity, then gain momentum.
            float ramp=Mathf.Min(.6f,vortex.attack*.3f);
            float t=Mathf.Clamp(elapsed,0,vortex.warning+vortex.attack);
            return vortexArmRotationSpeed*(t<ramp?t*t/(2*Mathf.Max(.001f,ramp)):t-ramp*.5f);
        }
        void SetVortexArms(float angle)
        {
            var rotation=Quaternion.AngleAxis(angle,Vector3.up);
            for(int i=0;i<arms.Count;i++)
            {
                IdleTargets(i,out var start,out var end,out var curve);
                arms[i].SetShape(start,center+rotation*(end-center),rotation*curve,true);
            }
        }
        IEnumerator Sweep()
        {
            State=Phase.Sweep;facing=FlatAim();bool upper=player.transform.position.y>arena.center.y;
            int count=health.Current<=health.maxHealth*secondSweepHealth?2:1;
            for(int pass=0;pass<count;pass++){
                float y=Mathf.Lerp(arena.min.y,arena.max.y,upper?.72f:.28f);Vector3 origin=new Vector3(center.x,y,center.z);
                float yaw=Quaternion.LookRotation(facing).eulerAngles.y;
                var marker=Effect(KrakenDeepVfx.Form.Sector,origin);marker.transform.rotation=Quaternion.Euler(0,yaw,0);
                marker.Size=new Vector3(sweepRadius,sweepHeight,sweepAngle);
                Sound.Prepare("Kraken_Underwater_Windup");Play("Command",sweep.warning);
                Vector3 fromRoot=arms[2].SamplePoints()[0],fromTip=arms[2].Tip;
                for(float t=0;t<sweep.warning;t+=Time.deltaTime){float q=t/sweep.warning;marker.Progress=q;Vector3 d=Quaternion.Euler(0,yaw-sweepAngle*.5f,0)*Vector3.forward;float blend=Mathf.SmoothStep(0,1,q);arms[2].SetShape(Vector3.Lerp(fromRoot,origin,blend),Vector3.Lerp(fromTip,origin+d*sweepRadius,blend),Vector3.up*Mathf.Sin(q*Mathf.PI)*2+Vector3.Cross(Vector3.up,d));yield return null;}
                Sound.StopPreparation();Sound.Cue("Kraken_Underwater_Strike");bool hit=false;float previous=-sweepAngle*.5f;Play("Wave",sweep.attack);
                for(float t=0;t<sweep.attack+Time.deltaTime;t+=Time.deltaTime){float q=Mathf.Clamp01(t/sweep.attack),angle=Mathf.Lerp(-sweepAngle*.5f,sweepAngle*.5f,Mathf.SmoothStep(0,1,q));
                    Vector3 d=Quaternion.Euler(0,yaw+angle,0)*Vector3.forward;arms[2].SetShape(origin,origin+d*sweepRadius,Vector3.Cross(Vector3.up,d));
                    int steps=Mathf.Max(1,Mathf.CeilToInt(Mathf.Abs(angle-previous)/3));
                    for(int j=1;j<=steps&&!hit;j++){var r=Quaternion.Euler(0,yaw+Mathf.Lerp(previous,angle,j/(float)steps),0);hit=BoxHit(origin+r*Vector3.forward*sweepRadius*.5f,new Vector3(sweepWidth,sweepHeight,sweepRadius),r,sweep.damage,sweep.knockback);}
                    marker.Impact=true;marker.Progress=Mathf.SmoothStep(0,1,q);previous=angle;yield return null;
                }
                attackBox.enabled=false;Remove(marker);upper=!upper;
                if(pass+1<count)yield return Recover(sweep.recovery*.5f);
            }
            yield return Recover(sweep.recovery);
        }
        IEnumerator Heart(){yield return AlternatingSlams(HeartExecutions==0?heartSlamCount:heartSecondSlamCount);}
        IEnumerator Recover(float duration)
        {
            var starts=new Vector3[arms.Count];for(int i=0;i<arms.Count;i++)starts[i]=arms[i].Tip;
            for(float t=0;t<duration;t+=Time.deltaTime){float q=Mathf.SmoothStep(0,1,t/duration);bodyVisual.position=Vector3.Lerp(bodyVisual.position,bodyHome,q);
                for(int i=0;i<arms.Count;i++){IdleTargets(i,out var start,out var end,out var curve);arms[i].SetShape(start,Vector3.Lerp(starts[i],end,q),curve,true);}
                yield return null;
            }
        }
        public override bool TryBeginNextPhase()
        {
            if(bodyBarrier)bodyBarrier.enabled=false;
            if(State==Phase.Dormant)return false;StopAllCoroutines();running=false;ClearEffects();if(swim)swim.SetCurrent(Vector3.zero);State=Phase.Dead;
            foreach(var h in hurtboxes)if(h)h.enabled=false;foreach(var a in arms)a.SetExposed(false);StartCoroutine(Death());return true;
        }
        IEnumerator Death()
        {
            BossCinematicUI.Begin(this);
            Sound.StopAll();Sound.Cue("Kraken_Death");
            Play("Groggy",deathTime);Vector3 from=bodyVisual.position;var sand=Effect(KrakenDeepVfx.Form.Sand,new Vector3(center.x,arena.min.y,center.z));sand.Size=new Vector3(sandRadius,sandHeight*.5f,0);
            for(float t=0;t<deathTime;t+=Time.deltaTime){float q=Mathf.SmoothStep(0,1,t/deathTime);bodyVisual.position=from-Vector3.up*q*Mathf.Max(.65f,Mathf.Abs(bodyFloatHeight));SetIdleArms(1-q*.5f);sand.Intensity=1-q;yield return null;}
            ClearEffects();if(animator)animator.speed=0;BossCinematicUI.End(this);health.CompleteDeferredDeath(true);if(first)first.FinishUnderwaterEncounter();
        }
        public void Cancel()
        {
            BossCinematicUI.End(this);
            var audio=GetComponent<SummerAudioEmitter>();if(audio)audio.StopAll();
            if(bodyBarrier)bodyBarrier.enabled=false;
            running=false;SpecialIntro=false;StopAllCoroutines();if(swim)swim.SetCurrent(Vector3.zero);ClearEffects();foreach(var a in arms)if(a)a.SetExposed(false);foreach(var h in hurtboxes)if(h)h.enabled=false;State=Phase.Dead;
            if(pool)foreach(var warning in pool.GetComponentsInChildren<KrakenPhaseWarning>())Destroy(warning.gameObject);
        }
        void OnDisable(){Cancel();}
    }
}
