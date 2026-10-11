using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
namespace DogLife
{
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(CombatHealth))]
    public sealed class KrakenPhaseOne:MonoBehaviour
    {
        public enum EncounterState{Waiting,Entry,Combat,Transition,Underwater,Stopped}
        [Serializable] public class SlamSettings
        {
            public float range=18,length=8,width=4.5f,warning=1.7f,strike=.35f,exposure=2,recovery=1.1f,cooldown=6,interval=1.7f,damage=1,knockback=4,lift=1,hitHeight=2.2f;
            [Min(1)] public int maxSimultaneous=1;
        }
        [Serializable] public class CaptureSettings
        {
            [Min(.05f)] public float throwWindup=.4f;
            public float radius=2.6f,warning=1.8f,emergence=.75f,connect=.4f,hold=1.6f,throwDuration=.7f,recovery=1.5f,cooldown=13,damage=1,throwDamage=1,throwSpeed=12,throwLift=6,hitHeight=3;
        }
        [Serializable] public class SweepSettings
        {
            public float radius=9,angle=200,warning=2.2f,duration=1.8f,recovery=1.8f,cooldown=18,damage=1,knockback=5,height=1.05f,width=1.4f;
        }
        [Header("Assets")][SerializeField] KrakenPhaseTentacle tentaclePrefab;
        [SerializeField] Material warningMaterial;
        [Header("Five independent slots")][SerializeField,Min(1)] int tentacleCount=5,killsToTransition=5;
        [SerializeField] float tentacleHP=100,respawnDelay=2,emergeDuration=2.2f,retractDuration=1.4f;
        [Header("Arena / safe placement")][SerializeField] float arenaHalfWidth=15,wallMargin=3,ringRadius=8,minSpacing=5,playerSpacing=4,oldPositionSpacing=4,placementRetry=.7f;
        [SerializeField,Min(1)] int placementAttempts=48;
        [SerializeField] float warningSurfaceOffset=.68f;
        [SerializeField,Min(.1f)] float poseSpeedMultiplier=1.5f;
        [SerializeField,Min(.2f)] float emergenceGrace=1.4f;
        [SerializeField,Min(.1f)] float captureVisualScale=.85f;
        [SerializeField,Range(.1f,1)] float captureContactProgress=.72f;
        [SerializeField,Min(.02f)] float captureActiveTime=.22f;
        float attackAllowedAt;
        public float PoseSpeedMultiplier=>poseSpeedMultiplier;
        [Header("Entry")][SerializeField] float entryStagger=.4f,entryGrace=2,entryInset=3;
        [Header("Patterns")][SerializeField] SlamSettings slam=new SlamSettings();
        [SerializeField] CaptureSettings capture=new CaptureSettings();
        [SerializeField] SweepSettings sweep=new SweepSettings();
        [Header("Flood")][SerializeField] float floodDelay=1.2f,floodDuration=9,floodHeight=11;
        [Header("Optional sound / presentation hooks; no audio generated")][SerializeField] UnityEvent onEntry,onSlam,onCapture,onSweep,onFlood,onUnderwater;
        public EncounterState State{get;private set;}
        public int Kills{get;private set;}
        public int RequiredKills=>Mathf.Max(1,killsToTransition);
        public IReadOnlyList<KrakenPhaseTentacle> Slots=>slots;
        public PlayerMotor Player{get;private set;}
        public float TentacleHP=>tentacleHP;
        public float EntryGrace=>entryGrace;
        public Material WarningMaterial=>warningMaterial;
        public Bounds Arena{get;private set;}
        readonly List<KrakenPhaseTentacle> slots=new List<KrakenPhaseTentacle>();
        readonly Dictionary<KrakenPhaseTentacle,int> countedLives=new Dictionary<KrakenPhaseTentacle,int>();
        readonly Collider[] overlaps=new Collider[64];
        ForestRoom room;CombatHealth playerHealth;Transform pool;KrakenPhaseTentacle captureTentacle,sweepTentacle;KrakenSequenceCamera sequence;KrakenWaterVolume water;
        readonly HashSet<KrakenPhaseTentacle> slamming=new HashSet<KrakenPhaseTentacle>();
        int activeSlams=>slamming.Count;
        float nextSlam,nextCapture,nextSweep;bool special,locked,captured,cleaned;
        bool oldRead,oldWeapons,oldCombat,oldInvulnerable,oldMotorEnabled,oldKinematic,oldGravity;CursorLockMode oldCursor;bool oldCursorVisible;
        PlayerWeapons weapons;BasicWeaponCombat combat;Rigidbody playerBody;Vector3 captureFrom;float floor;
        void Awake()
        {
            var old=GetComponent<KrakenBoss>();if(old)old.enabled=false;
            foreach(var v in GetComponentsInChildren<KrakenVfx>(true))v.enabled=false;
            foreach(var r in GetComponentsInChildren<Renderer>(true))r.enabled=false;
            foreach(var c in GetComponentsInChildren<Collider>(true))c.enabled=false;
            foreach(var a in GetComponentsInChildren<Animator>(true))a.enabled=false;
            var nav=GetComponent<NavMeshAgent>();if(nav)nav.enabled=false;
            GetComponent<CombatHealth>().invulnerable=true;
        }
        void Start()
        {
            room=GetComponentInParent<ForestRoom>();Player=FindFirstObjectByType<PlayerMotor>();if(!Player||!room||!tentaclePrefab){Debug.LogError("Kraken phase one requires room, player and natural tentacle prefab",this);enabled=false;return;}
            playerHealth=Player.GetComponent<CombatHealth>();weapons=Player.GetComponent<PlayerWeapons>();combat=Player.GetComponent<BasicWeaponCombat>();playerBody=Player.GetComponent<Rigidbody>();playerHealth.Died+=CancelEncounter;
            floor=transform.position.y;
            Arena=new Bounds(new Vector3((room.entryX+room.exitX)*.5f,floor+floodHeight*.5f,0),new Vector3(room.exitX-room.entryX-wallMargin*2,floodHeight,arenaHalfWidth*2-wallMargin*2));
            var root=new GameObject("Kraken phase one pool");pool=root.transform;pool.SetParent(transform,false);pool.rotation=Quaternion.identity;pool.localScale=new Vector3(1/transform.lossyScale.x,1/transform.lossyScale.y,1/transform.lossyScale.z);
            for(int i=0;i<tentacleCount;i++)slots.Add(Make("Combat tentacle "+(i+1),true));
            captureTentacle=Make("Capture tentacle (no HP)",false);sweepTentacle=Make("Central sweep (no HP)",false);
            sequence=GetComponent<KrakenSequenceCamera>();if(!sequence)sequence=gameObject.AddComponent<KrakenSequenceCamera>();water=GetComponent<KrakenWaterVolume>();if(!water)water=gameObject.AddComponent<KrakenWaterVolume>();water.Setup(Player,Arena,warningMaterial);
            BuildBoundary();State=EncounterState.Waiting;
        }
        KrakenPhaseTentacle Make(string title,bool counted){var t=Instantiate(tentaclePrefab,pool);t.name=title;t.Setup(this,counted);if(counted)t.SetReach(slam.length);else t.transform.localScale=Vector3.one*captureVisualScale;return t;}
        void BuildBoundary()
        {
            // Vertical barriers extend above the final waterline, independently of gate decoration.
            for(int i=0;i<4;i++){var go=new GameObject("Flood arena boundary "+i);go.layer=8;go.transform.SetParent(pool,false);var c=go.AddComponent<BoxCollider>();Vector3 p=Arena.center;bool x=i<2;float sign=i%2==0?-1:1;if(x)p.x+=sign*(Arena.extents.x+.6f);else p.z+=sign*(Arena.extents.z+.6f);go.transform.position=p;c.size=x?new Vector3(.5f,floodHeight+8,Arena.size.z+2):new Vector3(Arena.size.x+2,floodHeight+8,.5f);c.enabled=false;boundaries.Add(c);}
        }
        readonly List<Collider> boundaries=new List<Collider>();
        void Update()
        {
            if(cleaned||!Player||playerHealth.Dead)return;
            if(State==EncounterState.Waiting&&room.CombatStarted&&Player.transform.position.x>room.entryX+entryInset){StartCoroutine(Entry());return;}
            if(State!=EncounterState.Combat||special)return;
            if(Time.time<attackAllowedAt)return;
            foreach(var slot in slots)if(slot.State==KrakenPhaseTentacle.Pose.Emerging)return;
            if(activeSlams==0&&Time.time>=nextSweep){StartCoroutine(Sweep());return;}
            if(activeSlams==0&&Time.time>=nextCapture){StartCoroutine(Capture());return;}
            if(Time.time<nextSlam||activeSlams>=Mathf.Max(1,slam.maxSimultaneous))return;
            KrakenPhaseTentacle nearest=null;float distance=slam.range*slam.range;
            foreach(var t in slots)if(t.Available){float d=(t.transform.position-Player.transform.position).sqrMagnitude;if(d<distance){distance=d;nearest=t;}}
            if(nearest){nextSlam=Time.time+slam.interval;nearest.StartCoroutine(Slam(nearest));}
        }
        IEnumerator Entry()
        {
            State=EncounterState.Entry;foreach(var c in boundaries)c.enabled=true;if(room.rearSeal)room.rearSeal.SetActive(true);if(room.rearVines)room.rearVines.Close();if(room.forwardVines)room.forwardVines.Close();
            LockPlayer(true);sequence.Begin(new Vector3(Arena.center.x,floor,0),false);onEntry?.Invoke();
            for(int i=0;i<slots.Count;i++)
            {
                Vector3 preferred=new Vector3(Arena.center.x,floor,0)+Quaternion.Euler(0,i*360f/slots.Count,0)*Vector3.forward*ringRadius;
                Vector3 p;while(!TryPosition(preferred,slots[i],out p)){yield return new WaitForSeconds(placementRetry);preferred=RandomPosition();}
                slots[i].ResetSlot(p);slots[i].StartCoroutine(Emerge(slots[i]));yield return new WaitForSeconds(entryStagger);
            }
            yield return new WaitForSeconds(emergeDuration);sequence.End();UnlockPlayer();yield return new WaitForSeconds(entryGrace);
            State=EncounterState.Combat;nextSlam=Time.time;nextCapture=Time.time+capture.cooldown*.65f;nextSweep=Time.time+sweep.cooldown;
        }
        IEnumerator Emerge(KrakenPhaseTentacle t)
        {
            t.Warning.Show(KrakenPhaseWarning.Shape.Ripple,t.transform.position+Vector3.up*warningSurfaceOffset,Quaternion.identity,2,0,0);
            yield return t.Animate(KrakenPhaseTentacle.Pose.Emerging,emergeDuration);t.Warning.Hide();t.SetPose(KrakenPhaseTentacle.Pose.Idle,0);
            t.ReadyAt=Mathf.Max(t.ReadyAt,Time.time+emergenceGrace);attackAllowedAt=Mathf.Max(attackAllowedAt,Time.time+emergenceGrace);
        }
        Vector3 RandomPosition()=>new Vector3(UnityEngine.Random.Range(Arena.min.x+wallMargin,Arena.max.x-wallMargin),floor,UnityEngine.Random.Range(Arena.min.z+wallMargin,Arena.max.z-wallMargin));
        bool TryPosition(Vector3 preferred,KrakenPhaseTentacle self,out Vector3 result)
        {
            for(int i=0;i<placementAttempts;i++)
            {
                Vector3 p=i==0?preferred:RandomPosition();
                if(p.x<Arena.min.x+1||p.x>Arena.max.x-1||p.z<Arena.min.z+1||p.z>Arena.max.z-1)continue;
                if(Vector3.ProjectOnPlane(p-Player.transform.position,Vector3.up).magnitude<playerSpacing)continue;
                if(self.State==KrakenPhaseTentacle.Pose.Dying&&Vector3.Distance(p,self.transform.position)<oldPositionSpacing)continue;
                bool overlap=false;foreach(var t in slots)if(t!=self&&t.State!=KrakenPhaseTentacle.Pose.Hidden&&Vector3.Distance(t.transform.position,p)<minSpacing){overlap=true;break;}if(overlap)continue;
                if(!NavMesh.SamplePosition(p,out var nav,1.5f,NavMesh.AllAreas))continue;p=nav.position;
                if(Physics.CheckSphere(p+Vector3.up*2.5f,1.7f,1<<8,QueryTriggerInteraction.Ignore))continue;
                result=p;return true;
            }
            result=Vector3.zero;return false;
        }
        IEnumerator Slam(KrakenPhaseTentacle t)
        {
            slamming.Add(t);t.ReadyAt=Time.time+slam.cooldown;
            t.SetReach(slam.length);
            float width=slam.width;
            Vector3 forward=Vector3.ProjectOnPlane(Player.transform.position-t.transform.position,Vector3.up).normalized;
            t.transform.rotation=Quaternion.LookRotation(forward);
            float timer=0;while(timer<slam.warning){t.SetPose(KrakenPhaseTentacle.Pose.Ready,timer/slam.warning);t.Warning.Show(KrakenPhaseWarning.Shape.Rectangle,t.transform.position+Vector3.up*warningSurfaceOffset,t.transform.rotation,slam.length,width,timer/slam.warning);timer+=Time.deltaTime;yield return null;}
            t.Warning.Show(KrakenPhaseWarning.Shape.Rectangle,t.transform.position+Vector3.up*warningSurfaceOffset,t.transform.rotation,slam.length,width,1);
            yield return t.Animate(KrakenPhaseTentacle.Pose.Slam,slam.strike);
            if(!t.Alive||State!=EncounterState.Combat){slamming.Remove(t);yield break;}
            ConfigureAttackBox(t,width,slam.hitHeight,slam.length);t.AttackCollider.enabled=true;
            Physics.SyncTransforms();if(BoxContainsPlayer(t.AttackCollider))Hit(slam.damage,forward,slam.knockback,slam.lift,.45f);t.AttackCollider.enabled=false;onSlam?.Invoke();
            t.Warning.Show(KrakenPhaseWarning.Shape.SlamImpact,t.transform.position+Vector3.up*warningSurfaceOffset,t.transform.rotation,slam.length,width,0);
            yield return t.Animate(KrakenPhaseTentacle.Pose.Ground,slam.exposure);t.Warning.Hide();yield return t.Animate(KrakenPhaseTentacle.Pose.Recover,slam.recovery);t.SetPose(KrakenPhaseTentacle.Pose.Idle,0);slamming.Remove(t);
        }
        bool BoxContainsPlayer(BoxCollider box)
        {
            int n=Physics.OverlapBoxNonAlloc(box.transform.TransformPoint(box.center),Vector3.Scale(box.size,box.transform.lossyScale)*.5f,overlaps,box.transform.rotation,~0,QueryTriggerInteraction.Ignore);
            for(int i=0;i<n;i++)if(overlaps[i].GetComponentInParent<PlayerMotor>()==Player)return true;return false;
        }
        void ConfigureAttackBox(KrakenPhaseTentacle t,float width,float height,float length)
        {
            var s=t.transform.lossyScale;var inverse=new Vector3(1/s.x,1/s.y,1/s.z);
            t.AttackCollider.center=Vector3.Scale(new Vector3(0,height*.5f,length*.5f),inverse);
            t.AttackCollider.size=Vector3.Scale(new Vector3(width,height,length),inverse);
        }
        bool CircleContainsPlayer(Vector3 p,float radius,float height)
        {
            var c=Player.GetComponent<CapsuleCollider>();return c.bounds.min.y<p.y+height&&c.bounds.max.y>p.y&&Vector3.ProjectOnPlane(c.ClosestPoint(p+Vector3.up*.6f)-p,Vector3.up).sqrMagnitude<=radius*radius;
        }
        bool Hit(float amount,Vector3 direction,float speed,float lift,float duration)
        {
            if(State!=EncounterState.Combat||playerHealth.Dead)return false;float before=playerHealth.Current;playerHealth.Damage(amount);bool landed=playerHealth.Current<before;if(landed&&!playerHealth.Dead)Player.ApplyKnockback(direction,speed,lift,duration);return landed;
        }
        IEnumerator Capture()
        {
            special=true;nextCapture=Time.time+capture.cooldown;var t=captureTentacle;Vector3 p=Player.transform.position;p.y=floor;t.transform.position=p;t.transform.rotation=Quaternion.identity;
            float clock=0;while(clock<capture.warning){t.Warning.Show(KrakenPhaseWarning.Shape.Circle,p+Vector3.up*warningSurfaceOffset,Quaternion.identity,capture.radius,0,clock/capture.warning);clock+=Time.deltaTime;yield return null;}t.Warning.Hide();
            // Contact starts as the visible tip breaks out, not after a second hidden delay.
            bool caught=false;float contactStart=capture.emergence*captureContactProgress;
            float contactEnd=Mathf.Max(capture.emergence,contactStart+captureActiveTime);
            clock=0;
            while(true){
                t.SetPose(KrakenPhaseTentacle.Pose.Emerging,Mathf.Clamp01(clock/Mathf.Max(.01f,capture.emergence)));
                t.Warning.Show(KrakenPhaseWarning.Shape.Circle,p+Vector3.up*warningSurfaceOffset,Quaternion.identity,capture.radius,0,1);
                // Let the pose update before checking contact, including on slower frames.
                yield return new WaitForEndOfFrame();Physics.SyncTransforms();
                if(clock>=contactStart&&CircleContainsPlayer(p,capture.radius,capture.hitHeight)&&Hit(capture.damage,Vector3.zero,0,0,.05f)){caught=true;break;}
                if(clock>=contactEnd)break;
                clock=Mathf.Min(contactEnd,clock+Time.deltaTime);
            }
            t.Warning.Show(KrakenPhaseWarning.Shape.Impact,p+Vector3.up*warningSurfaceOffset,Quaternion.identity,capture.radius,0,0);
            if(caught)
            {
                if(playerHealth.Dead)yield break;onCapture?.Invoke();LockPlayer(false);captured=true;captureFrom=playerBody.position;oldKinematic=playerBody.isKinematic;oldGravity=playerBody.useGravity;playerBody.linearVelocity=Vector3.zero;playerBody.isKinematic=true;Player.enabled=false;
                clock=0;while(clock<capture.connect+capture.hold){t.SetPose(KrakenPhaseTentacle.Pose.Capture,Mathf.Clamp01(clock/capture.connect));Vector3 target=Vector3.Lerp(captureFrom,t.GripPoint.position,Mathf.SmoothStep(0,1,clock/capture.connect));MoveCaptured(target);clock+=Time.deltaTime;yield return null;}
                Vector3 direction=Vector3.ProjectOnPlane(new Vector3(Arena.center.x,floor,0)-t.transform.position,Vector3.up).normalized;if(direction.sqrMagnitude<.1f)direction=Vector3.right;
                clock=0;bool released=false;
                while(clock<capture.throwWindup)
                {
                    float q=clock/Mathf.Max(.05f,capture.throwWindup);t.SetPose(KrakenPhaseTentacle.Pose.Throw,q);
                    if(!released&&q<.65f)MoveCaptured(t.GripPoint.position);
                    else if(!released){released=true;MoveCaptured(t.ReleasePoint.position);ReleaseCapture();UnlockPlayer();if(!playerHealth.Dead){playerHealth.Damage(capture.throwDamage);Player.ApplyKnockback(direction,capture.throwSpeed,capture.throwLift,capture.throwDuration);}}
                    clock+=Time.deltaTime;yield return null;
                }
                if(!released){ReleaseCapture();UnlockPlayer();if(!playerHealth.Dead)Player.ApplyKnockback(direction,capture.throwSpeed,capture.throwLift,capture.throwDuration);}
            }
            yield return t.Animate(KrakenPhaseTentacle.Pose.Dying,retractDuration/poseSpeedMultiplier);t.SetPose(KrakenPhaseTentacle.Pose.Hidden,0);yield return new WaitForSeconds(capture.recovery);special=false;nextSlam=Time.time+entryGrace*.5f/poseSpeedMultiplier;
        }
        void MoveCaptured(Vector3 target)
        {
            target.x=Mathf.Clamp(target.x,Arena.min.x+1,Arena.max.x-1);target.z=Mathf.Clamp(target.z,Arena.min.z+1,Arena.max.z-1);target.y=Mathf.Max(floor+.2f,target.y);
            Vector3 delta=target-playerBody.position;if(delta.magnitude>.01f&&playerBody.SweepTest(delta.normalized,out var hit,delta.magnitude+.08f,QueryTriggerInteraction.Ignore))target=playerBody.position+delta.normalized*Mathf.Max(0,hit.distance-.08f);playerBody.MovePosition(target);
        }
        IEnumerator Sweep()
        {
            special=true;nextSweep=Time.time+sweep.cooldown;var t=sweepTentacle;t.transform.position=new Vector3(Arena.center.x,floor,0);t.SetReach(sweep.radius);
            t.Warning.Show(KrakenPhaseWarning.Shape.Ripple,t.transform.position+Vector3.up*warningSurfaceOffset,Quaternion.identity,2.4f,0,0);
            yield return t.Animate(KrakenPhaseTentacle.Pose.Emerging,emergeDuration/poseSpeedMultiplier);
            float yaw=Quaternion.LookRotation(Vector3.ProjectOnPlane(Player.transform.position-t.transform.position,Vector3.up)).eulerAngles.y;
            float timer=0;while(timer<sweep.warning){t.SetPose(KrakenPhaseTentacle.Pose.SweepReady,timer/sweep.warning);t.transform.rotation=Quaternion.Euler(0,yaw-sweep.angle/2,0);t.Warning.Show(KrakenPhaseWarning.Shape.Sector,t.transform.position+Vector3.up*warningSurfaceOffset,Quaternion.Euler(0,yaw,0),sweep.radius,0,timer/sweep.warning,sweep.angle);timer+=Time.deltaTime;yield return null;}
            t.Warning.Hide();onSweep?.Invoke();bool struck=false;timer=0;float previousAngle=yaw-sweep.angle/2;
            while(true)
            {
                float q=Mathf.Clamp01(timer/Mathf.Max(.01f,sweep.duration));t.SetPose(KrakenPhaseTentacle.Pose.Sweep,q);t.transform.rotation=Quaternion.Euler(0,yaw-sweep.angle/2+sweep.angle*Mathf.SmoothStep(0,1,q),0);
                t.Warning.Show(KrakenPhaseWarning.Shape.SweepTrail,t.transform.position+Vector3.up*warningSurfaceOffset,Quaternion.Euler(0,yaw,0),sweep.radius,sweep.width,Mathf.SmoothStep(0,1,q),sweep.angle);
                ConfigureAttackBox(t,sweep.width,sweep.height,sweep.radius);t.AttackCollider.enabled=true;Physics.SyncTransforms();
                float currentAngle=yaw-sweep.angle/2+sweep.angle*Mathf.SmoothStep(0,1,q);
                float stepAngle=Mathf.Atan2(sweep.width*.5f,sweep.radius)*Mathf.Rad2Deg;
                int steps=Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(currentAngle-previousAngle)/Mathf.Max(.5f,stepAngle)),1,32);
                // Test the travelled arc as well as the rendered tip, even on slower frames.
                for(int sample=1;sample<=steps&&!struck;sample++)
                {
                    var rotation=Quaternion.Euler(0,Mathf.Lerp(previousAngle,currentAngle,sample/(float)steps),0);var direction=rotation*Vector3.forward;
                    int count=Physics.OverlapBoxNonAlloc(t.transform.position+direction*sweep.radius*.5f+Vector3.up*sweep.height*.5f,new Vector3(sweep.width,sweep.height,sweep.radius)*.5f,overlaps,rotation,~0,QueryTriggerInteraction.Ignore);
                    for(int i=0;i<count&&!struck;i++)if(overlaps[i].GetComponentInParent<PlayerMotor>()==Player)struck=Hit(sweep.damage,direction,sweep.knockback,0,.45f);
                }
                previousAngle=currentAngle;
                if(q>=1){yield return new WaitForEndOfFrame();break;}
                timer+=Time.deltaTime;yield return null;
            }
            t.AttackCollider.enabled=false;t.Warning.Hide();yield return t.Animate(KrakenPhaseTentacle.Pose.Dying,retractDuration/poseSpeedMultiplier);t.SetPose(KrakenPhaseTentacle.Pose.Hidden,0);yield return new WaitForSeconds(sweep.recovery);special=false;nextSlam=Time.time+entryGrace*.5f/poseSpeedMultiplier;
        }
        public void TentacleKilled(KrakenPhaseTentacle t)
        {
            if(State!=EncounterState.Combat||!t||t.Owner!=this||!t.CombatSlot||!slots.Contains(t)||t.Health.Current>0)return;
            if(countedLives.TryGetValue(t,out var life)&&life==t.LifeId)return;
            countedLives[t]=t.LifeId;Kills=Mathf.Min(RequiredKills,Kills+1);
            // Killing one slot must not reset the other four attacks or their cooldowns.
            slamming.Remove(t);
            if(Kills>=RequiredKills){State=EncounterState.Transition;StopAllCoroutines();foreach(var slot in slots)slot.StopAttack();captureTentacle.StopAttack();sweepTentacle.StopAttack();ReleaseCapture();UnlockPlayer();StartCoroutine(Flood());}
            else StartCoroutine(Replace(t));
        }
        IEnumerator Replace(KrakenPhaseTentacle t)
        {
            yield return t.Animate(KrakenPhaseTentacle.Pose.Dying,retractDuration);yield return new WaitForSeconds(respawnDelay);
            Vector3 position;while(!TryPosition(RandomPosition(),t,out position)){yield return new WaitForSeconds(placementRetry);}
            t.ResetSlot(position);yield return Emerge(t);
        }
        IEnumerator Flood()
        {
            special=true;foreach(var t in slots)t.StartCoroutine(t.Animate(KrakenPhaseTentacle.Pose.Dying,retractDuration));captureTentacle.StartCoroutine(captureTentacle.Animate(KrakenPhaseTentacle.Pose.Dying,retractDuration));sweepTentacle.StartCoroutine(sweepTentacle.Animate(KrakenPhaseTentacle.Pose.Dying,retractDuration));
            yield return new WaitForSeconds(retractDuration+floodDelay);LockPlayer(true);water.SetCinematicLift(true);sequence.Begin(new Vector3(Arena.center.x,floor,0),true);onFlood?.Invoke();float clock=0;
            while(clock<floodDuration){float progress=Mathf.Clamp01(clock/Mathf.Max(.01f,floodDuration));water.SetHeight(Mathf.Lerp(floor-.2f,floor+floodHeight,Mathf.SmoothStep(0,1,progress)));sequence.SetFloodProgress(progress,water.Height,Player.transform);clock+=Time.deltaTime;yield return null;}
            water.SetHeight(floor+floodHeight);water.SetCinematicLift(false);water.EnterUnderwater();
            var phaseTwo=GetComponent<KrakenPhaseTwo>();
            if(phaseTwo)yield return phaseTwo.Reveal(this,sequence,water.Height);else sequence.End();
            UnlockPlayer();State=EncounterState.Underwater;onUnderwater?.Invoke();if(phaseTwo)phaseTwo.BeginCombat();
        }
        void LockPlayer(bool invincible)
        {
            if(locked)return;locked=true;oldRead=Player.readInput;oldMotorEnabled=Player.enabled;oldWeapons=weapons&&weapons.enabled;oldCombat=combat&&combat.enabled;oldInvulnerable=playerHealth.invulnerable;oldCursor=Cursor.lockState;oldCursorVisible=Cursor.visible;
            Player.StopForAttack();Player.readInput=false;if(combat){combat.Cancel();combat.enabled=false;}if(weapons)weapons.enabled=false;if(invincible)playerHealth.invulnerable=true;
        }
        void UnlockPlayer()
        {
            if(!locked)return;locked=false;if(playerHealth)playerHealth.invulnerable=oldInvulnerable;
            if(!playerHealth||playerHealth.Dead)return;
            Player.readInput=oldRead;Player.enabled=oldMotorEnabled;if(weapons)weapons.enabled=oldWeapons;if(combat)combat.enabled=oldCombat;Cursor.lockState=oldCursor;Cursor.visible=oldCursorVisible;
        }
        void ReleaseCapture(){if(!captured)return;captured=false;if(playerBody){playerBody.useGravity=oldGravity;if(!playerHealth.Dead){playerBody.isKinematic=oldKinematic;if(!oldKinematic)playerBody.linearVelocity=Vector3.zero;}}if(Player&&!playerHealth.Dead)Player.enabled=oldMotorEnabled;}
        public void CancelEncounter()
        {
            if(cleaned)return;cleaned=true;var phaseTwo=GetComponent<KrakenPhaseTwo>();if(phaseTwo)phaseTwo.Cancel();StopAllCoroutines();foreach(var t in slots)if(t)t.StopAttack();if(captureTentacle)captureTentacle.StopAttack();if(sweepTentacle)sweepTentacle.StopAttack();ReleaseCapture();if(sequence)sequence.End();UnlockPlayer();if(water)water.Clear();foreach(var c in boundaries)if(c)c.enabled=false;State=EncounterState.Stopped;
        }
        public void FinishUnderwaterEncounter(){foreach(var c in boundaries)if(c)c.enabled=false;var swim=Player?Player.GetComponent<PlayerWaterMovement>():null;if(swim)swim.ReleaseArenaLimits();}
        void OnDisable(){CancelEncounter();}
        GUIStyle progressStyle;
        void OnGUI()
        {
            if(State!=EncounterState.Combat||Time.timeScale<=0)return;
            if(progressStyle==null)progressStyle=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold};
            float scale=Mathf.Clamp(Screen.height/1080f,.75f,1.5f);progressStyle.fontSize=Mathf.RoundToInt(19*scale);
            var old=GUI.color;var rect=new Rect(Screen.width*.5f-135*scale,30*scale,270*scale,38*scale);
            GUI.color=new Color(.025f,.09f,.12f,.82f);GUI.DrawTexture(rect,Texture2D.whiteTexture);
            GUI.color=new Color(.6f,.8f,.8f,.9f);GUI.DrawTexture(new Rect(rect.x,rect.yMax-2*scale,rect.width,2*scale),Texture2D.whiteTexture);
            GUI.color=new Color(.87f,.94f,.92f);GUI.Label(rect,"촉수 처치  "+Kills+" / "+RequiredKills,progressStyle);GUI.color=old;
        }
        void OnDestroy(){if(playerHealth)playerHealth.Died-=CancelEncounter;}
    }
}
