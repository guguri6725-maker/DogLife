using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace DogLife
{
    // 이동/HP/피격은 기존 컴포넌트를 사용하고, 공격 순서와 두 페이즈만 관리합니다.
    [RequireComponent(typeof(CombatHealth),typeof(NavMeshAgent),typeof(Rigidbody))]
    public sealed partial class FrozenKnightBoss : CombatPhaseTransition
    {
        public enum Pattern { Combo, Crack, IceBlade, FrostBurst, SwordFury, Charge, RideSlash, Stomp, MultiCharge, MountedFury, MountedIceBlades, IcePillars, ExpandingBlade, ColdWave, CloneCharge, SummonMinions, RemoteSword, FinalStand }
        public enum CombatState { Idle, Warning, Attack, Active, Recovery, Transition, Reposition, Groggy, Dead }
        [Serializable] public class Attack
        {
            [Min(0)] public float damage=1,range=4.5f,width=2,warning=1.1f,duration=.5f,recovery=1.4f,cooldown=6,knockback=4,lift=0;
        }
        [Serializable] public class ComboSettings
        {
            public Attack first=new Attack{warning=.9f,duration=.45f};
            public Attack second=new Attack{warning=.35f,duration=.45f};
            public Attack final=new Attack{damage=2,warning=.85f,duration=.7f,recovery=2.8f,knockback=8};
            [Min(.1f)] public float speed=1;
            [Min(0)] public float cooldown=6;
        }
        [Serializable] public class CrackSettings
        {
            public Attack sword=new Attack{damage=2,warning=1.5f,duration=.55f,recovery=2,cooldown=8};
            [Min(0)] public float damage=1,length=14,width=1.8f,delay=.18f,activeTime=.4f;
        }
        [Serializable] public class ProjectileSettings
        {
            public Attack attack=new Attack{warning=1.25f,duration=.5f,recovery=1.2f,cooldown=6};
            [Min(.1f)] public float speed=13,size=2.2f,maxDistance=25,height=1.1f,depth=.8f;
        }
        [Serializable] public class RingSettings
        {
            [Range(0,1)] public float triggerHP=.5f;
            [Min(1)] public int count=3;
            [Min(0)] public float damage=1,firstRadius=3,width=2.6f,distanceBetween=3,warning=1.15f,delay=.25f,activeTime=.22f,recovery=2.8f,prepareTime=1.4f;
            [Min(.1f)] public float comboSpeedMultiplier=1.25f,projectileSpeedMultiplier=1.3f,recoveryMultiplier=.8f;
        }
        [Serializable] public class RideSettings
        {
            public Attack attack=new Attack{damage=2,range=20,width=2.8f,warning=1.4f,duration=.35f,recovery=2,cooldown=8,knockback=9};
            [Min(.1f)] public float speed=15,deceleration=.55f;
        }
        [Serializable] public class RideSlashSettings
        {
            public Attack attack=new Attack{damage=2,range=4.5f,warning=1.2f,duration=.55f,recovery=2.2f,cooldown=8,knockback=6};
            [Min(.1f)] public float speed=8,distance=15,sideOffset=2.5f,hitDistance=5;
        }
        [Serializable] public class MountedSpecialSettings
        {
            [Range(0,1)] public float triggerHP=.5f;
            [Min(1)] public int count=4;
            [Min(.1f)] public float damage=1,speed=17,width=3,warning=1.2f,distance=24,delay=.55f;
            [Min(.1f)] public float trailWidth=2.8f,trailDuration=12,trailDamage=1,trailTick=1,trailSpacing=1.2f;
            [Min(.1f)] public float finalDamage=2,finalWidth=4.5f,finalWarning=2.1f,groggy=5;
            [Min(.1f)] public float edgeDistance=9,repositionTimeout=5;
        }
        [Header("연결 — Hierarchy에서 지정")]
        [SerializeField] Animator knightAnimator,horseAnimator;
        [SerializeField] Transform riderRoot,horseRoot,swordRoot,mountSeat,specialEntry,horseArrivalPoint;
        [SerializeField] FrozenKnightDamageArea swordArea,chargeArea,crackArea,radialArea;
        [SerializeField] FrozenKnightHazard iceBladePrefab,iceTrailPrefab;
        [SerializeField] FrozenKnightVfx effects;
        [Header("페이즈별 HP / 탑승 연출")]
        [SerializeField,Min(1)] float phase1MaxHP=350,phase2MaxHP=450;
        [SerializeField,Min(.1f)] float kneelDuration=1.2f,summonDuration=1.7f,mountDuration=1.2f,transitionRecovery=1;
        [Header("군마 접근 / 정지 / 탑승")]
        [SerializeField,Min(.1f)] float horseArrivalDistance=12,horseArrivalSpeed=7,horseBrakeDistance=2.5f,horseStopDuration=.65f,mountSideDistance=1.3f,mountArcHeight=.8f;
        [SerializeField,Min(1)] int horseEntryCandidates=12;
        [SerializeField] bool showWaitingHorse=true;
        [Header("이동 / AI")]
        [SerializeField,Min(.1f)] float walkSpeed=2.1f,rideMoveSpeed=5,turnSpeed=120,engageRange=40,entryInset=3,initialDelay=2,globalCooldown=1.1f;
        [Header("군마 보행 재생 속도")]
        [SerializeField,Min(.1f)] float horseWalkReferenceSpeed=3.2f,horseWalkMinimumRate=.25f,horseWalkMaximumRate=2.2f;
        [Header("기마 패턴 예고 / 선택")]
        [SerializeField,Min(.1f)] float mountedFuryIntroduction=2.4f,chargeEmblemRadius=3;
        [SerializeField,Min(.1f)] float closeRange=5,closeDwell=4,navSampleRadius=3,navUpdateInterval=.25f,attackApproachDistance=5.5f;
        [Header("판정 공통")]
        [SerializeField,Min(.01f)] float hitHeight=3,knockbackDuration=.45f,attackLeadTime=.12f,minimumAttackTime=.05f;
        [SerializeField,Range(1,360)] float meleeSectorAngle=150;
        [SerializeField,Min(.1f)] float swordReferenceRange=4.5f;
        [SerializeField] bool useFullPlayerDamage;
        [SerializeField] LayerMask obstacleMask=1<<8;
        [SerializeField,Min(.01f)] float chargeObstacleRadius=.65f,chargeObstacleHeight=1.5f,chargeSkin=.08f;
        [Header("1페이즈")]
        [SerializeField] ComboSettings combo=new ComboSettings();
        [SerializeField] CrackSettings crack=new CrackSettings();
        [SerializeField] ProjectileSettings iceBlade=new ProjectileSettings();
        [SerializeField] Attack frostBurst=new Attack{range=6,warning=1.5f,duration=.4f,recovery=2,cooldown=12,knockback=11,lift=2};
        [SerializeField] RingSettings swordFury=new RingSettings();
        [Header("검 꽂기 / 손 놓기 / 회수")]
        [SerializeField,Range(0,1)] float swordPlantLockTime=.56f,swordRegripTime=.4f;
        [SerializeField,Min(.1f)] float swordRetrieveDuration=1.4f;
        [Header("원본 비탑승 공격 — 클립 재생 위치(0~1) 기준 판정")]
        [SerializeField] Vector2 firstHitWindow=new Vector2(.20f,.40f),secondHitWindow=new Vector2(.62f,.83f),finalHitWindow=new Vector2(.16f,.36f),slamHitWindow=new Vector2(.29f,.43f);
        [SerializeField,Range(0,1)] float slamImpactTime=.40f;
        [Header("2페이즈")]
        [SerializeField] RideSettings charge=new RideSettings();
        [SerializeField] RideSlashSettings rideSlash=new RideSlashSettings();
        [SerializeField] Attack stomp=new Attack{damage=2,range=6,warning=1.4f,duration=.4f,recovery=2,cooldown=10,knockback=10,lift=3};
        [SerializeField] MountedSpecialSettings mountedFury=new MountedSpecialSettings();
        readonly HashSet<CombatHealth> hits=new HashSet<CombatHealth>();
        readonly List<FrozenKnightHazard> hazards=new List<FrozenKnightHazard>();
        readonly float[] cooldowns=new float[18];
        readonly List<Pattern> candidates=new List<Pattern>();
        readonly Collider[] ringContacts=new Collider[64];
        CombatHealth health,target;
        PlayerMotor player;
        NavMeshAgent agent;
        ForestRoom room;
        Vector3 horseScale,swordScale;
        Vector3 swordRestPosition,plantedSwordPosition;
        Transform swordRestParent;
        Quaternion swordRestRotation,plantedSwordRotation;
        bool plantingSword,swordPlanted,retrievingSword;
        Vector3 horseWaitingPosition;
        Quaternion horseWaitingRotation;
        Quaternion ridingModelRotation;
        [SerializeField,Min(1)] float mountedSeatRotationSharpness=14;
        bool horseWaiting;
        float nextAttack,nextPath,closeTime;
        bool busy,engaged,pendingSpecial;
        int previous=-1;
        public int PhaseNumber { get; private set; }=1;
        public bool Phase1SpecialUsed { get; private set; }
        public bool Phase2SpecialUsed { get; private set; }
        public CombatState State { get; private set; }
        public Pattern CurrentPattern { get; private set; }
        public Vector3 Forward { get; private set; }=Vector3.forward;
        public bool CanDealDamage=>isActiveAndEnabled&&health&&!health.Dead&&State!=CombatState.Transition&&State!=CombatState.Dead&&target&&!target.Dead;
        public bool Empowered=>PhaseNumber==1&&Phase1SpecialUsed;
        public bool SwordActive=>(swordArea&&swordArea.Armed)||(CurrentPattern==Pattern.Combo&&radialArea&&radialArea.SectorArmed);
        bool Nav=>agent&&agent.enabled&&agent.isOnNavMesh;
        void Awake()
        {
            health=GetComponent<CombatHealth>();agent=GetComponent<NavMeshAgent>();
            health.maxHealth=phase1MaxHP;health.Restore();
            var body=GetComponent<Rigidbody>();body.isKinematic=true;body.constraints=RigidbodyConstraints.FreezeRotation;
            agent.updateRotation=false;agent.updateUpAxis=false;
            horseScale=horseRoot.localScale;swordScale=swordRoot.localScale;
            // 모델의 기존 정면 축을 보존하면서 안장의 기울기를 따라갑니다.
            ridingModelRotation=riderRoot.localRotation;
            swordRestParent=swordRoot.parent;swordRestPosition=swordRoot.localPosition;swordRestRotation=swordRoot.localRotation;
            horseRoot.gameObject.SetActive(false);knightAnimator.applyRootMotion=false;horseAnimator.applyRootMotion=false;
            health.Died+=Died;nextAttack=Time.time+initialDelay;
        }
        void Start()
        {
            player=FindFirstObjectByType<PlayerMotor>();if(player){target=player.GetComponent<CombatHealth>();var body=GetComponent<Collider>();if(body)foreach(var c in player.GetComponents<Collider>())Physics.IgnoreCollision(body,c);}
            if(!room)room=GetComponentInParent<ForestRoom>();Play(knightAnimator,"Idle");
            initialArenaCenter=arenaCenter?arenaCenter.position:room?new Vector3((room.entryX+room.exitX)*.5f,transform.position.y,room.transform.position.z):transform.position;
            // 씬에서 보스를 바닥 위에 배치해도 전장 연출은 실제 이동 바닥에 놓습니다.
            if(NavMesh.SamplePosition(initialArenaCenter,out var arenaFloor,Mathf.Max(navSampleRadius,10),agent.areaMask))initialArenaCenter=arenaFloor.position;
            if(showWaitingHorse)
            {
                // 1페이즈부터 떨어진 곳에 군마를 배치해 실제로 기다리다가 접근하게 합니다.
                horseWaitingPosition=HorseArrivalRoute(initialArenaCenter)[0];horseWaitingRotation=Quaternion.LookRotation(Flat(initialArenaCenter-horseWaitingPosition));
                horseRoot.SetPositionAndRotation(horseWaitingPosition,horseWaitingRotation);horseRoot.gameObject.SetActive(true);horseWaiting=true;Play(horseAnimator,"Idle");
            }
            PrepareIntroduction();
        }
        public void BindRoom(ForestRoom value){room=value;}
        void LateUpdate()
        {
            UpdateIntroductionSword();
            // 탑승 위치 보정 후 검의 월드 위치를 고정해야 검이 군마를 따라 움직이지 않습니다.
            if(PhaseNumber==2&&mountSeat&&riderRoot)
            {
                riderRoot.position=mountSeat.position;
                riderRoot.rotation=Quaternion.Slerp(riderRoot.rotation,mountSeat.rotation*ridingModelRotation,
                    1-Mathf.Exp(-mountedSeatRotationSharpness*Time.deltaTime));
            }
            else if(horseWaiting)horseRoot.SetPositionAndRotation(horseWaitingPosition,horseWaitingRotation);
            if(iceFieldActive&&swordIcePatch)swordIcePatch.transform.position=iceFieldPosition;
            // 손을 놓아도 검은 월드 위치를 유지하고, 회수 동작에서 다시 손잡이를 잡습니다.
            if(knightAnimator&&swordRoot)
            {
                var pose=knightAnimator.GetCurrentAnimatorStateInfo(0);
                // 같은 Plant에서 페이즈 전환을 시작해도 이전 클립의 끝 자세를 고정하지 않습니다.
                if(plantingSword&&!swordPlanted&&!knightAnimator.IsInTransition(0)&&pose.IsName("Plant")&&pose.normalizedTime>=swordPlantLockTime)
                {
                    BossSound("Sword_Slam",swordRoot);plantingSword=false;swordPlanted=true;plantedSwordPosition=swordRoot.position;plantedSwordRotation=swordRoot.rotation;
                    // 피격 반동은 LateUpdate에서 상체 뼈를 움직이므로 꽂힌 검을 손 뼈에서 분리합니다.
                    swordRoot.SetParent(transform,true);
                }
                if(retrievingSword&&pose.IsName("PlantRetrieve")&&pose.normalizedTime>=swordRegripTime)RestoreSwordGrip();
                if(swordPlanted&&!remoteSwordActive)swordRoot.SetPositionAndRotation(plantedSwordPosition,plantedSwordRotation);
            }
        }
        void Update()
        {
            TickBossAudio();
            ResetIntroductionSword();
            if(!health||health.Dead||!target||target.Dead)return;
            if(room&&!room.CombatStarted)return;
            if(awaitingIntroduction){StartCoroutine(Introduction());return;}
            float distance=GroundDistance(player.transform.position,transform.position);
            if(!engaged){if(!room&&distance>engageRange)return;engaged=true;nextAttack=Time.time+initialDelay;}
            if(State==CombatState.Transition)return;
            TickCavalryField();
            // 실제 폭발 범위 안에서 머문 시간을 누적합니다. 다른 공격이 끝나도 대기 시간을 지우지 않습니다.
            float proximity=PhaseNumber==1?frostBurst.range:stomp.range;
            closeTime=Vector3.ProjectOnPlane(player.transform.position-transform.position,Vector3.up).magnitude<=proximity?closeTime+Time.deltaTime:0;
            if(busy||CombatFeedback.Paused(this))return;
            if(ThresholdAttack())return;
            Aim(false);
            if(MaintainRangedSpacing(distance)){if(busy)return;}
            else if(distance>attackApproachDistance&&Nav)
            {
                agent.isStopped=false;agent.speed=PhaseNumber==1?walkSpeed:rideMoveSpeed;
                if(Time.time>=nextPath){nextPath=Time.time+navUpdateInterval;agent.SetDestination(player.transform.position);}
                PlayLocomotion(true);
            }
            else{StopMovement();PlayLocomotion(false);}
            if(Time.time>=nextAttack){var choice=Choose(distance);if(choice.HasValue)Begin(choice.Value);}
        }
        Pattern? Choose(float distance)
        {
            // 근접 체류 조건을 만족하면 무작위 선택 때문에 폭발이 계속 밀리지 않게 우선 실행합니다.
            if(PhaseNumber==1&&closeTime>=closeDwell&&Time.time>=cooldowns[(int)Pattern.FrostBurst]&&previous!=(int)Pattern.FrostBurst)return Pattern.FrostBurst;
            // 발구르기는 보이는 원형 범위 안의 체류로 발동하며 다른 무작위 패턴보다 우선합니다.
            if(PhaseNumber==2&&closeTime>=closeDwell&&Time.time>=cooldowns[(int)Pattern.Stomp]&&previous!=(int)Pattern.Stomp)return Pattern.Stomp;
            candidates.Clear();int start=PhaseNumber==1?0:5;
            for(int i=start;i<start+4;i++)
            {
                if(Time.time<cooldowns[i]||i==previous)continue;
                if(PhaseNumber==1&&i<2&&distance>attackApproachDistance)continue;
                if(i==(int)Pattern.IceBlade)continue; // 기존 Enum 번호는 유지하되 검기 패턴은 제거합니다.
                if(i==(int)Pattern.RideSlash)continue;
                if(i==(int)Pattern.MultiCharge)continue; // 제거한 연속 질주의 Enum 번호는 저장 호환용으로만 유지합니다.
                if((i==3||i==7)&&closeTime<closeDwell)continue;
                candidates.Add((Pattern)i);
            }
            if(PhaseNumber==2&&Time.time>=cooldowns[(int)Pattern.MountedIceBlades]&&previous!=(int)Pattern.MountedIceBlades)candidates.Add(Pattern.MountedIceBlades);
            AddCandidate(Pattern.IcePillars);
            if(PhaseNumber==1)AddCandidate(Pattern.ExpandingBlade);
            else{AddCandidate(Pattern.CloneCharge);AddCandidate(Pattern.RemoteSword);summonedMinions.RemoveAll(h=>!h||h.Dead||!h.gameObject.activeInHierarchy);if(summonedMinions.Count<maxMinions)AddCandidate(Pattern.SummonMinions);}
            // 직전 패턴밖에 준비되지 않았다면 잠깐 기다려 같은 패턴의 연속 사용을 피합니다.
            return candidates.Count==0?(Pattern?)null:candidates[UnityEngine.Random.Range(0,candidates.Count)];
        }
        public bool Begin(Pattern pattern)
        {
            if(pattern==Pattern.IceBlade||pattern==Pattern.RideSlash||pattern==Pattern.MultiCharge||pattern==Pattern.MountedFury||pattern==Pattern.FinalStand)return false;
            if(busy||!CanDealDamage)return false;
            if(PhaseNumber==1&&((int)pattern>=5&&(int)pattern<=10||(int)pattern>=14))return false;
            if(PhaseNumber==2&&((int)pattern<5||pattern==Pattern.ExpandingBlade||pattern==Pattern.ColdWave))return false;
            if(pattern==Pattern.SwordFury){if(Phase1SpecialUsed)return false;Phase1SpecialUsed=true;}
            if(pattern==Pattern.MountedFury){if(Phase2SpecialUsed)return false;Phase2SpecialUsed=true;}
            CurrentPattern=pattern;busy=true;StopMovement();
            StartCoroutine(Run(pattern));return true;
        }
        IEnumerator Run(Pattern pattern)
        {
            // 시전 전에 발을 디디며 방향을 맞추고, 준비 중에는 공격을 켜지 않습니다.
            yield return FaceBeforeCast();
            // 체력 기믹은 중앙, 원거리 기술은 확보한 거리에서 준비합니다.
            if(pattern==Pattern.ColdWave||pattern==Pattern.SwordFury)yield return MoveToArenaCenter(false);
            else if(RangedPattern(pattern))yield return MakeRangedSpace();
            if(!target||target.Dead)yield break;
            Aim(true);
            switch(pattern)
            {
                case Pattern.Combo:yield return Combo();break;
                case Pattern.Crack:yield return Crack();break;
                case Pattern.FrostBurst:yield return Burst(frostBurst,false);break;
                case Pattern.SwordFury:yield return SwordFury();break;
                case Pattern.Charge:yield return Charge(charge,charge.attack.damage,charge.attack.width,charge.attack.warning,false);yield return Recover(charge.attack.recovery);break;
                case Pattern.MountedIceBlades:yield return MountedIceBlades();break;
                case Pattern.Stomp:yield return Burst(stomp,true);break;
                case Pattern.MountedFury:yield return MountedFury();break;
                case Pattern.IcePillars:yield return IcePillars();break;
                case Pattern.ExpandingBlade:yield return ExpandingBlade();break;
                case Pattern.ColdWave:yield return ColdWave();break;
                case Pattern.CloneCharge:yield return CloneRush();break;
                case Pattern.SummonMinions:yield return SummonWinterMinions();break;
                case Pattern.RemoteSword:yield return RemoteSword();break;
            }
            Disarm();effects.Hide();previous=(int)pattern;cooldowns[(int)pattern]=Time.time+Cooldown(pattern);
            rangedHoldUntil=RangedPattern(pattern)?Time.time+rangedHoldTime:0;
            nextAttack=Time.time+globalCooldown;if(pattern==Pattern.FrostBurst||pattern==Pattern.Stomp)closeTime=0;State=CombatState.Idle;busy=false;PlayLocomotion(false);
        }
        float Cooldown(Pattern p)
        {
            if(p==Pattern.MountedIceBlades)return mountedBlades.cooldown;
            if(p==Pattern.IcePillars)return pillarCooldown;
            if(p==Pattern.ExpandingBlade)return waveCooldown;
            if(p==Pattern.CloneCharge)return cloneCooldown;
            if(p==Pattern.SummonMinions)return minionCooldown;
            if(p==Pattern.RemoteSword)return remoteSwordCooldown;
            switch(p){case Pattern.Combo:return combo.cooldown;case Pattern.Crack:return crack.sword.cooldown;case Pattern.IceBlade:return iceBlade.attack.cooldown;case Pattern.FrostBurst:return frostBurst.cooldown;case Pattern.Charge:return charge.attack.cooldown;case Pattern.RideSlash:return rideSlash.attack.cooldown;case Pattern.Stomp:return stomp.cooldown;default:return globalCooldown;}
        }
        IEnumerator Stage(CombatState state,float duration,string animation=null,FrozenKnightVfx progressVfx=null,float progressFrom=0,float progressTo=1)
        {
            State=state;if(animation!=null)Play(knightAnimator,animation,duration);
            for(float elapsed=0;elapsed<Mathf.Max(minimumAttackTime,duration);)
            {
                if(progressVfx)progressVfx.SetWarningProgress(Mathf.Lerp(progressFrom,progressTo,elapsed/Mathf.Max(minimumAttackTime,duration)));
                if(!CombatFeedback.Paused(this))elapsed+=Time.deltaTime;yield return null;
            }
            if(progressVfx)progressVfx.SetWarningProgress(progressTo);
        }
        IEnumerator Recover(float duration,bool groggy=false)
        {
            Disarm();effects.Hide();Play(horseAnimator,groggy?"Groggy":"Idle");
            if(swordPlanted&&!leaveSwordPlanted)
            {
                retrievingSword=true;
                yield return Stage(CombatState.Recovery,swordRetrieveDuration,"PlantRetrieve");
                RestoreSwordGrip();
            }
            // 후딜레이 길이와 대기 애니메이션 배속을 분리합니다.
            Play(knightAnimator,groggy?"MountedGroggy":PhaseNumber==2?"MountedIdle":"Idle");
            yield return Stage(groggy?CombatState.Groggy:CombatState.Recovery,duration*(Empowered?swordFury.recoveryMultiplier:1));
        }
        IEnumerator Combo()
        {
            // 원본 1타2타를 한 번 재생하므로 두 타격 사이에 클립을 재시작하거나 정지하지 않습니다.
            yield return OriginalFootMotion("1타2타",new[]{combo.first,combo.second},new[]{firstHitWindow,secondHitWindow},false);
            yield return Recover(combo.second.recovery);
        }
        IEnumerator Crack()
        {
            // 관절 동작/속도는 원본을 유지하고 루트 방향만 정면으로 보정한 클립입니다.
            yield return OriginalFootMotion("내려찍기_정면",new[]{crack.sword},new[]{slamHitWindow},true,"SlamForward");
            yield return Recover(crack.sword.recovery);
        }
        IEnumerator OriginalFootMotion(string state,Attack[] attacks,Vector2[] windows,bool fissure,string clipName=null)
        {
            // 원본 클립/State의 1배속을 사용하고 판정만 Animator의 실제 재생 위치에 맞춥니다.
            float length=0;foreach(var clip in knightAnimator.runtimeAnimatorController.animationClips)if(clip.name==(clipName??state)){length=clip.length;break;}
            if(length<=0||!knightAnimator.HasState(0,Animator.StringToHash(state))){Debug.LogError("Missing Frozen Knight original attack: "+state,this);yield break;}
            Play(knightAnimator,state);State=CombatState.Warning;
            int armed=-1,shown=-1;bool crackStarted=false;float crackEnd=0;
            while(true)
            {
                var info=knightAnimator.IsInTransition(0)?knightAnimator.GetNextAnimatorStateInfo(0):knightAnimator.GetCurrentAnimatorStateInfo(0);
                if(!info.IsName(state)){yield return null;continue;}
                float t=info.normalizedTime;int hit=-1,next=-1;
                for(int i=0;i<windows.Length;i++)
                {
                    float from=Mathf.Clamp01(windows[i].x),to=Mathf.Clamp(windows[i].y,from,1);
                    if(t>=from&&t<to)hit=i;
                    if(next<0&&t<to)next=i;
                }
                if(next>=0&&next!=shown)
                {
                    shown=next;var attack=attacks[next];swordRoot.localScale=swordScale;
                    effects.Show(fissure?FrozenKnightVfx.Shape.Line:FrozenKnightVfx.Shape.Sector,transform.position,Forward,fissure?crack.length:attack.range,fissure?crack.width:meleeSectorAngle);
                }
                if(fissure&&!crackStarted)effects.SetWarningProgress(t/Mathf.Max(.001f,slamImpactTime+crack.delay/length));
                else if(!fissure&&next>=0&&hit<0)
                {
                    float start=next==0?0:windows[next-1].y;
                    effects.SetWarningProgress(Mathf.InverseLerp(start,windows[next].x,t),next==0);
                }
                if(hit!=armed)
                {
                    swordArea.Disarm();radialArea.Disarm();armed=hit;
                    if(hit>=0)
                    {
                        var attack=attacks[hit];hits.Clear();BossSound(fissure?"Sword_Slam":"Sword_Swing",swordRoot);
                        if(fissure)swordArea.Arm(this,attack.damage,attack.knockback,attack.lift,hits);
                        else{radialArea.transform.SetPositionAndRotation(transform.position,Quaternion.LookRotation(Forward));radialArea.ArmSector(this,attack.damage,attack.knockback,attack.lift,attack.range,meleeSectorAngle,hitHeight,hits);effects.BeginSlash(hit);effects.Activate();}
                    }
                    else if(!fissure&&next<0)effects.Hide();
                }
                if(fissure&&!crackStarted&&t>=slamImpactTime+crack.delay/length)
                {
                    BossSound("Fissure");crackStarted=true;crackEnd=t+crack.activeTime/length;SetBox(crackArea,crack.width,crack.length,true);
                    crackArea.Arm(this,crack.damage,crack.sword.knockback,crack.sword.lift);effects.Activate();
                }
                if(crackStarted&&t>=crackEnd){crackArea.Disarm();effects.Hide();}
                State=hit>=0||crackArea.Armed?CombatState.Active:next>=0?CombatState.Warning:CombatState.Recovery;
                if(t>=1)break;
                yield return null;
            }
            swordArea.Disarm();radialArea.Disarm();crackArea.Disarm();effects.Hide();
        }
        IEnumerator PlantSword(float duration,CombatState plantState=CombatState.Warning,FrozenKnightVfx progressVfx=null)
        {
            RestoreSwordGrip();swordRoot.localScale=swordScale;plantingSword=true;
            yield return Stage(plantState,duration,"Plant",progressVfx,0,.9f);
            // CrossFade가 차지한 시간까지 기다려 검이 바닥에 닿기 전에 판정이 나오지 않게 합니다.
            if(!knightAnimator||!knightAnimator.isActiveAndEnabled)yield break;
            for(float t=0;t<duration;t+=Time.deltaTime)
            {
                var pose=knightAnimator.GetCurrentAnimatorStateInfo(0);
                if(pose.IsName("Plant")&&pose.normalizedTime>=1&&!knightAnimator.IsInTransition(0))break;
                yield return null;
            }
        }
        IEnumerator Burst(Attack a,bool mounted)
        {
            BossSound("Frost_Charge");
            Disarm();
            effects.Show(FrozenKnightVfx.Shape.Ring,GroundPoint(transform.position),Forward,a.range,0);
            if(mounted)Play(horseAnimator,"Rear",a.warning);
            float clipTime=0;
            if(mounted)yield return Stage(CombatState.Warning,a.warning,"MountedStompReady",effects,0,.9f);
            else
            {
                clipTime=StartNativeCast("PushUpper");
                // 예고 시간을 실제로 보장하며, 밀쳐올리는 순간과 얼음 폭발을 함께 맞춥니다.
                float warning=Mathf.Max(a.warning,clipTime*pushCastMoment);
                knightAnimator.speed=clipTime*pushCastMoment/(warning+attackLeadTime);
                yield return Stage(CombatState.Warning,warning,null,effects,0,.9f);
            }
            yield return Stage(CombatState.Attack,attackLeadTime,null,effects,.9f,1);
            if(!mounted)knightAnimator.speed=1;
            if(mounted)Play(horseAnimator,"Stomp",a.duration);
            radialArea.transform.SetPositionAndRotation(transform.position,Quaternion.LookRotation(Forward));
            radialArea.ArmSector(this,a.damage,a.knockback,a.lift,a.range,360,hitHeight);effects.Activate();BossSound("Frost_Burst");
            // 비탑승 폭발은 밀어내기 상체 동작을 유지하고 하체는 서 있는 자세를 보존합니다.
            yield return Stage(CombatState.Active,a.duration,mounted?"MountedStompImpact":null);radialArea.Disarm();
            if(!mounted&&clipTime*(1-pushCastMoment)>a.duration)yield return Stage(CombatState.Recovery,clipTime*(1-pushCastMoment)-a.duration);
            yield return Recover(a.recovery);
        }
        IEnumerator SwordFury(){yield return CavalryFury();}
        IEnumerator Charge(RideSettings settings,float amount,float width,float warning,bool leaveTrail)
        {
            Aim(true);effects.Show(FrozenKnightVfx.Shape.Line,transform.position,Forward,settings.attack.range,width);
            Play(horseAnimator,"Rear",warning);yield return Stage(CombatState.Warning,warning,"MountedStompReady",effects,0,.9f);
            yield return Stage(CombatState.Attack,attackLeadTime,null,effects,.9f,1);
            SetBox(chargeArea,width,settings.attack.width,false);hits.Clear();chargeArea.Arm(this,amount,settings.attack.knockback,settings.attack.lift,hits);
            effects.Activate();Play(horseAnimator,"Gallop");Play(knightAnimator,"MountedCharge");State=CombatState.Active;BossSound("Charge_Start",horseRoot);BossSound("Charge_Pass",horseRoot,.65f);
            float travelled=0,trailTravel=0;Vector3 trailStart=transform.position;FrozenKnightHazard trail=null;bool trailStarted=false;
            while(travelled<settings.attack.range)
            {
                if(CombatFeedback.Paused(this)){yield return null;continue;}
                if(leaveTrail&&trailStarted&&!trail){trailStart=transform.position;trailTravel=0;trailStarted=false;}
                float step=Mathf.Min(settings.speed*Time.deltaTime,settings.attack.range-travelled);
                // 플레이어 전용 얼음벽은 돌진 경로에도 영향을 주지 않습니다.
                float moved=MoveStraight(Forward,step);
                travelled+=moved;trailTravel+=moved;
                if(leaveTrail&&trailTravel>=mountedFury.trailSpacing)
                {
                    if(!trail)trail=SpawnTrail(trailStart,transform.position);else trail.ExtendTrail(trailStart,transform.position);
                    trailTravel=0;trailStarted=true;
                }
                if(moved<step*.5f)break;yield return null;
            }
            if(leaveTrail&&travelled>chargeSkin){if(!trail)SpawnTrail(trailStart,transform.position);else trail.ExtendTrail(trailStart,transform.position);}
            chargeArea.Disarm();effects.Hide();State=CombatState.Recovery;
            for(float t=0;t<settings.deceleration;t+=Time.deltaTime){MoveStraight(Forward,Mathf.Lerp(settings.speed,0,t/settings.deceleration)*Time.deltaTime);yield return null;}
            StopMovement();Play(horseAnimator,"Idle");
        }
        IEnumerator MountedFury()
        {
            // HP 특수 패턴 시작은 이동 전에 독립된 문장 연출로 알립니다. 이때 피해 판정은 없습니다.
            yield return ChargeIntroduction();
            State=CombatState.Reposition;effects.Hide();
            Vector3 desired=specialEntry?specialEntry.position:transform.position-Flat(player.transform.position-transform.position)*mountedFury.edgeDistance;
            if(Nav&&NavMesh.SamplePosition(desired,out var point,navSampleRadius,new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask}))
            {
                agent.isStopped=false;agent.speed=rideMoveSpeed;agent.SetDestination(point.position);PlayLocomotion(true);
                for(float t=0;t<mountedFury.repositionTimeout&&(agent.pathPending||agent.remainingDistance>agent.stoppingDistance+.2f);t+=Time.deltaTime)
                {if(agent.velocity.sqrMagnitude>.1f)transform.rotation=Quaternion.LookRotation(Flat(agent.velocity));yield return null;}
            }
            StopMovement();
            var ride=new RideSettings{speed=mountedFury.speed,deceleration=charge.deceleration,attack=new Attack{range=mountedFury.distance,width=mountedFury.width,knockback=charge.attack.knockback,lift=charge.attack.lift}};
            for(int i=0;i<Mathf.Max(1,mountedFury.count);i++)
            {
                bool final=i==Mathf.Max(1,mountedFury.count)-1;
                effects.SetChargeSequence(i,mountedFury.count);
                yield return Charge(ride,final?mountedFury.finalDamage:mountedFury.damage,final?mountedFury.finalWidth:mountedFury.width,final?mountedFury.finalWarning:mountedFury.warning,true);
                if(!final)yield return Stage(CombatState.Recovery,mountedFury.delay,"MountedIdle");
            }
            yield return Recover(mountedFury.groggy,true);ClearHazards();
        }
        IEnumerator ChargeIntroduction()
        {
            Disarm();StopMovement();float duration=mountedFuryIntroduction;
            effects.SetChargeSequence(0,mountedFury.count);
            effects.Show(FrozenKnightVfx.Shape.Ring,transform.position,Forward,chargeEmblemRadius,0,FrozenKnightVfx.Theme.RoyalReady);
            Play(horseAnimator,"Rear",duration);
            yield return Stage(CombatState.Warning,duration,"MountedReady");effects.Hide();
        }
        FrozenKnightHazard SpawnTrail(Vector3 from,Vector3 to)
        {
            Vector3 delta=to-from;float distance=delta.magnitude;if(distance<chargeSkin)return null;
            var h=Instantiate(iceTrailPrefab,(from+to)*.5f,Quaternion.LookRotation(Flat(delta)));hazards.Add(h);
            h.Launch(this,mountedFury.trailWidth,distance,hitHeight,mountedFury.trailDamage,0,0,0,0,mountedFury.trailDuration,mountedFury.trailTick);
            return h;
        }
        public override bool TryBeginNextPhase()
        {
            if(PhaseNumber==2&&finaleUsed&&mountedDeath&&!deathCinematicStarted)
            {
                deathCinematicStarted=true;StopAllCoroutines();ClearEncounterObjects();ClearCavalryEffects();ClearHazards();Disarm();StopMovement();
                busy=true;health.invulnerable=true;State=CombatState.Transition;if(effects)effects.Hide();
                StartCoroutine(DeathCinematic());return true;
            }
            if(PhaseNumber==2&&!finaleUsed)
            {
                finaleUsed=true;StopAllCoroutines();ClearCavalryEffects();Disarm();StopMovement();busy=true;health.invulnerable=true;
                CurrentPattern=Pattern.FinalStand;StartCoroutine(FinalStand());return true;
            }
            if(PhaseNumber!=1||State==CombatState.Transition)return false;
            StopAllCoroutines();ClearEncounterObjects(true);ClearCavalryEffects();RestoreSwordGrip();Disarm();ClearHazards();StopMovement();health.invulnerable=true;busy=true;pendingSpecial=false;State=CombatState.Transition;
            StartCoroutine(Transition());return true;
        }
        IEnumerator Transition()
        {
            yield return MoveToArenaCenter(true);
            if(!target||target.Dead)yield break;
            if(cinematicStage)cinematicStage.Begin(player,transform,true);
            yield return PlantSword(swordFury.prepareTime,CombatState.Transition);leaveSwordPlanted=true;
            // 검을 꽂는 동작 직후 빙판 확산을 보여준 뒤 군마 탑승으로 연결합니다.
            Play(knightAnimator,"Idle");
            if(cinematicStage)cinematicStage.ShowIceField(swordRoot,transform);
            // 전투에 쓰이는 빙판은 유지하고 전환 전용 파동/섬광은 생략합니다.
            BeginSwordField();
            if(swordIcePatch)swordIcePatch.SetGrowthDuration(iceSpreadDuration);
            yield return new WaitForSeconds(iceSpreadDuration+iceSpreadHold);
            if(cinematicStage)cinematicStage.ShowMount(transform);BossSound("Summon",horseRoot,.65f);
            effects.Show(FrozenKnightVfx.Shape.Ring,transform.position,Forward,frostBurst.range,0,FrozenKnightVfx.Theme.Summon);effects.Activate();
            // 검을 꽂은 뒤 쓰러지지 않고 서서 군마를 기다립니다.
            Play(knightAnimator,"Idle");effects.Hide();
            if(!horseRoot.gameObject.activeSelf){horseRoot.position=HorseArrivalRoute(transform.position)[0];horseRoot.gameObject.SetActive(true);}
            Vector3 destination;Vector3[] route;
            while(!TryBoardingRoute(out destination,out route))yield return new WaitForSeconds(.5f);
            Vector3 standing=riderRoot.position,boarding=destination-transform.right*boardingStandDistance;
            Quaternion riderRotation=riderRoot.rotation;
            Play(knightAnimator,"Walk");float walkDuration=Vector3.Distance(standing,boarding)/Mathf.Max(.1f,walkSpeed);
            for(float t=0;t<walkDuration;t+=Time.deltaTime)
            {
                riderRoot.position=Vector3.Lerp(standing,boarding,t/walkDuration);
                riderRoot.rotation=Quaternion.RotateTowards(riderRoot.rotation,Quaternion.LookRotation(Flat(boarding-standing))*Quaternion.Euler(0,180,0),turnSpeed*Time.deltaTime);
                yield return null;
            }
            riderRoot.position=boarding;Play(knightAnimator,"Idle");
            for(float t=0;t<horseStopDuration;t+=Time.deltaTime){riderRoot.rotation=Quaternion.RotateTowards(riderRoot.rotation,riderRotation,turnSpeed*Time.deltaTime);yield return null;}
            riderRoot.rotation=riderRotation;horseWaiting=false;
            horseRoot.localScale=horseScale;Play(horseAnimator,"Gallop");
            for(int i=0;i<route.Length;i++)
            {
                while(Vector3.Distance(horseRoot.position,route[i])>.025f)
                {
                    Vector3 direction=route[i]-horseRoot.position;
                    float remaining=direction.magnitude;for(int j=i;j<route.Length-1;j++)remaining+=Vector3.Distance(route[j],route[j+1]);
                    float speed=horseArrivalSpeed*Mathf.Clamp(remaining/horseBrakeDistance,.15f,1);
                    horseAnimator.speed=Mathf.Clamp(speed/horseArrivalSpeed,.35f,1);
                    horseRoot.rotation=Quaternion.RotateTowards(horseRoot.rotation,Quaternion.LookRotation(Flat(direction)),turnSpeed*2*Time.deltaTime);
                    horseRoot.position=Vector3.MoveTowards(horseRoot.position,route[i],speed*Time.deltaTime);yield return null;
                }
            }
            horseRoot.position=destination;Play(horseAnimator,"Idle");
            Quaternion stopRotation=horseRoot.rotation;
            for(float t=0;t<horseStopDuration;t+=Time.deltaTime){horseRoot.rotation=Quaternion.Slerp(stopRotation,transform.rotation,Mathf.SmoothStep(0,1,t/horseStopDuration));yield return null;}
            horseRoot.rotation=transform.rotation;
            // 왼발을 걸고 몸을 올린 뒤 오른다리를 넘기는 동작과 이동 곡선을 맞춥니다.
            float boardingDuration=cinematicMount?cinematicMount.length:mountDuration;
            Play(knightAnimator,cinematicMount?"CinematicMount":"Mount",boardingDuration);Vector3 start=riderRoot.position;
            for(float t=0;t<boardingDuration;t+=Time.deltaTime)
            {
                // 손을 먼저 짚고 몸을 올린 뒤 다리를 넘깁니다. 클립의 접촉 구간과 같은 곡선입니다.
                float progress=t/boardingDuration;
                float approach=Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.22f,progress));
                float cross=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.42f,.80f,progress));
                float rise=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.28f,.84f,progress));
                Vector3 seat=mountSeat.position;
                Vector3 position=Vector3.Lerp(start,seat,.26f*approach+.74f*cross);
                position.y=Mathf.Lerp(start.y,seat.y,rise)+Mathf.Sin(rise*Mathf.PI)*mountArcHeight;
                riderRoot.position=position;yield return null;
            }
            riderRoot.position=mountSeat.position;
            // 탑승 완료 지점을 새 이동 기준으로 삼되 군마/기사/꽂힌 검은 월드 위치를 보존합니다.
            Vector3 riderPosition=riderRoot.position,horsePosition=horseRoot.position;Quaternion horseRotation=horseRoot.rotation;
            if(Nav)agent.Warp(destination);else transform.position=destination;
            horseRoot.SetPositionAndRotation(horsePosition,horseRotation);riderRoot.position=riderPosition;
            swordRoot.SetPositionAndRotation(plantedSwordPosition,plantedSwordRotation);
            PhaseNumber=2;previous=-1;Array.Clear(cooldowns,0,cooldowns.Length);closeTime=0;
            health.maxHealth=phase2MaxHP;health.Restore();
            yield return Stage(CombatState.Transition,transitionRecovery,"MountedIdle");
            health.invulnerable=false;effects.Hide();Play(horseAnimator,"Idle");State=CombatState.Idle;nextAttack=Time.time+globalCooldown;busy=false;
            if(cinematicStage)cinematicStage.End();
        }
        Vector3[] HorseArrivalRoute(Vector3 destination,bool fromWaiting=false)
        {
            var filter=new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask};
            if(fromWaiting&&horseWaiting)
            {
                if(!Nav)return new[]{horseRoot.position,destination};
                var arrival=new NavMeshPath();
                if(NavMesh.CalculatePath(horseRoot.position,destination,filter,arrival)&&arrival.status==NavMeshPathStatus.PathComplete&&arrival.corners.Length>1)return arrival.corners;
            }
            for(int i=0;i<Mathf.Max(1,horseEntryCandidates);i++)
            {
                Vector3 candidate=i==0&&horseArrivalPoint?horseArrivalPoint.position:destination+Quaternion.AngleAxis(180+i*360f/horseEntryCandidates,Vector3.up)*transform.forward*horseArrivalDistance;
                if(Nav)
                {
                    var path=new NavMeshPath();
                    if(NavMesh.SamplePosition(candidate,out var point,navSampleRadius,filter)&&Vector3.Distance(point.position,destination)>horseBrakeDistance&&NavMesh.CalculatePath(point.position,destination,filter,path)&&path.status==NavMeshPathStatus.PathComplete&&path.corners.Length>1)return path.corners;
                }
                else if(!Physics.SphereCast(destination+Vector3.up*chargeObstacleHeight,chargeObstacleRadius,Flat(candidate-destination),out _,Vector3.Distance(candidate,destination),obstacleMask,QueryTriggerInteraction.Ignore))return new[]{candidate,destination};
            }
            // 좁은 방에서는 현재 연결된 NavMesh 경계 안에서 가장 긴 안전한 접근 구간을 사용합니다.
            Vector3 best=destination;
            for(int i=0;i<horseEntryCandidates;i++)
            {
                Vector3 end=destination+Quaternion.AngleAxis(i*360f/horseEntryCandidates,Vector3.up)*transform.forward*horseArrivalDistance;
                if(Nav&&NavMesh.Raycast(destination,end,out var edge,filter))end=Vector3.MoveTowards(edge.position,destination,chargeObstacleRadius);
                if((end-destination).sqrMagnitude>(best-destination).sqrMagnitude)best=end;
            }
            return new[]{best,destination};
        }
        void SetSwordRange(float range){swordRoot.localScale=swordScale*(range/Mathf.Max(.1f,swordReferenceRange));}
        void SetBox(FrozenKnightDamageArea area,float width,float length,bool front)
        {
            area.transform.SetPositionAndRotation(transform.position,Quaternion.LookRotation(Forward));
            // 보스 확대와 무관하게 경고에 전달한 월드 크기와 실제 Box 판정을 일치시킵니다.
            var box=(BoxCollider)area.Shape;var scale=area.transform.lossyScale;
            box.size=new Vector3(width/Mathf.Abs(scale.x),hitHeight/Mathf.Abs(scale.y),length/Mathf.Abs(scale.z));box.center=new Vector3(0,hitHeight*.5f/scale.y,front?length*.5f/scale.z:0);
        }
        void SetSphere(FrozenKnightDamageArea area,float radius)
        {area.transform.position=transform.position;var sphere=(SphereCollider)area.Shape;sphere.center=Vector3.zero;sphere.radius=radius;}
        void HitRing(float inner,float outer,float damage)
        {
            // 바닥 원형 표시의 끝에서도 플레이어 몸체가 닿으면 맞도록 수평 범위로 검사합니다.
            int count=Physics.OverlapBoxNonAlloc(transform.position+Vector3.up*hitHeight*.5f,new Vector3(outer,hitHeight*.5f,outer),ringContacts,Quaternion.identity,~0,QueryTriggerInteraction.Ignore);
            var contacts=count==ringContacts.Length?Physics.OverlapBox(transform.position+Vector3.up*hitHeight*.5f,new Vector3(outer,hitHeight*.5f,outer),Quaternion.identity,~0,QueryTriggerInteraction.Ignore):ringContacts;
            if(contacts!=ringContacts)count=contacts.Length;
            for(int i=0;i<count;i++)
            {
                var c=contacts[i];var hp=c.GetComponentInParent<CombatHealth>();if(!hp||!hp.player||hits.Contains(hp))continue;
                float distance=Vector3.ProjectOnPlane(c.bounds.center-transform.position,Vector3.up).magnitude;
                float bodyRadius=Mathf.Max(c.bounds.extents.x,c.bounds.extents.z);
                if(distance+bodyRadius<inner||distance-bodyRadius>outer||c.bounds.max.y<transform.position.y||c.bounds.min.y>transform.position.y+hitHeight)continue;
                if(DealHit(hp,damage,0,0,transform.position))hits.Add(hp);
            }
        }
        public bool DealHit(CombatHealth hp,float damage,float push,float lift,Vector3 origin)
        {
            if(!CanDealDamage)return false;Vector3 direction=Flat(hp.transform.position-origin);
            if(!CombatFeedback.DealDamage(hp,damage,hp.transform.position,direction,1,transform,new Color(.4f,.85f,1),useFullPlayerDamage))return false;
            var motor=hp.GetComponent<PlayerMotor>();if(motor&&(push>0||lift>0))motor.ApplyKnockback(direction,push,lift,knockbackDuration);return true;
        }
        float MoveStraight(Vector3 direction,float distance)
        {
            if(distance<=0)return 0;float allowed=distance;
            if(Physics.SphereCast(transform.position+Vector3.up*chargeObstacleHeight,chargeObstacleRadius,direction,out var wall,distance+chargeSkin,obstacleMask,QueryTriggerInteraction.Ignore))allowed=Mathf.Max(0,wall.distance-chargeSkin);
            if(Nav&&NavMesh.Raycast(transform.position,transform.position+direction*allowed,out var edge,new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask}))allowed=Mathf.Min(allowed,Mathf.Max(0,Vector3.Distance(transform.position,edge.position)-chargeSkin));
            if(Nav)agent.Move(direction*allowed);else transform.position+=direction*allowed;return allowed;
        }
        void Aim(bool instant)
        {if(!player)return;var direction=Flat(player.transform.position-transform.position);float rate=(State==CombatState.Idle||State==CombatState.Reposition)&&(!Nav||agent.velocity.sqrMagnitude<.1f)?standingTurnSpeed:turnSpeed;transform.rotation=instant?Quaternion.LookRotation(direction):Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(direction),rate*Time.deltaTime);Forward=transform.forward;}
        static Vector3 Flat(Vector3 v){v.y=0;return v.sqrMagnitude>.001f?v.normalized:Vector3.forward;}
        void StopMovement(){if(Nav){agent.isStopped=true;agent.ResetPath();agent.velocity=Vector3.zero;}}
        string lastKnight,lastHorse;
        [SerializeField,Min(.01f)] float attackBlendTime=.14f,recoveryBlendTime=.28f,originalAttackBlendTime=.25f;
        void PlayLocomotion(bool moving)
        {
            // 방향을 바꿔도 기존 대기/보행 모션을 유지합니다.
            if(PhaseNumber!=2)
            {
                Play(knightAnimator,moving?(introductionRunning&&!introductionSwordDrawn?"UnarmedWalk":"Walk"):"Idle");
                return;
            }
            // 일반 추적에는 접지가 있는 보행을 사용하고, 돌진의 Gallop과 구분합니다.
            float speed=Nav?agent.velocity.magnitude:0;bool walking=moving&&speed>.08f;
            Play(horseAnimator,walking?"Walk":"Idle");
            Play(knightAnimator,walking?"MountedWalk":"MountedIdle");
            // 말과 기사의 보행 클립 길이/배속을 같이 맞춰 보폭마다 반동이 어긋나지 않습니다.
            if(walking)knightAnimator.speed=horseAnimator.speed=Mathf.Clamp(speed/horseWalkReferenceSpeed,horseWalkMinimumRate,horseWalkMaximumRate);
        }
        void Play(Animator animator,string state,float duration=0)
        {
            if(!animator||!animator.gameObject.activeInHierarchy)return;
            string last=animator==knightAnimator?lastKnight:lastHorse;
            // 같은 대기 상태로 돌아가도 공격/후딜레이용 재생 배속은 원래대로 되돌립니다.
            if(duration<=0&&last==state){animator.speed=1;return;}
            if(animator==knightAnimator)lastKnight=state;else lastHorse=state;AnimationSound(animator,state);
            float length=1;foreach(var clip in animator.runtimeAnimatorController.animationClips)if(clip.name==state){length=clip.length;break;}
            animator.speed=duration>0?length/Mathf.Max(minimumAttackTime,duration):1;
            // 준비/공격은 짧게, 대기 복귀는 길게 섞되 짧은 공격 대부분을 블렌드로 소비하지 않습니다.
            float blend=state=="Idle"||state=="MountedIdle"?recoveryBlendTime:attackBlendTime;
            if(state=="1타2타"||state=="3타"||state=="내려찍기_정면")blend=originalAttackBlendTime;
            if(duration>0)blend=Mathf.Min(blend,duration*.25f);
            float offset=0;
            if(animator==knightAnimator&&(state=="MountedIdle"||state=="MountedWalk"||state=="MountedCharge"))
            {
                string horseState=state=="MountedWalk"?"Walk":state=="MountedCharge"?"Gallop":"Idle";
                var gait=horseAnimator.GetCurrentAnimatorStateInfo(0);
                if(!horseAnimator.IsInTransition(0)&&gait.IsName(horseState))offset=Mathf.Repeat(gait.normalizedTime,1)*length;
            }
            animator.CrossFadeInFixedTime(state,blend,0,offset);
        }
        void Disarm(){if(swordArea)swordArea.Disarm();if(chargeArea)chargeArea.Disarm();if(crackArea)crackArea.Disarm();if(radialArea)radialArea.Disarm();}
        void ClearHazards(){foreach(var h in hazards)if(h)Destroy(h.gameObject);hazards.Clear();}
        void RestoreSwordGrip()
        {
            // 비활성화 처리 중에는 부모를 바꿀 수 없으므로 다음 활성화 때 복구합니다.
            if(!gameObject.activeInHierarchy)return;
            // 중단/사망/페이즈 전환에서도 검이 바닥에 남아 다음 공격을 방해하지 않게 복구합니다.
            if((plantingSword||swordPlanted||retrievingSword)&&swordRoot)
            {swordRoot.SetParent(swordRestParent,false);swordRoot.localPosition=swordRestPosition;swordRoot.localRotation=swordRestRotation;swordRoot.localScale=swordScale;}
            plantingSword=swordPlanted=retrievingSword=false;
        }
        void OnEnable(){RestoreSwordGrip();}
        void Died(){StormSound(false);State=CombatState.Dead;StopAllCoroutines();ClearEncounterObjects();ClearCavalryEffects();RestoreSwordGrip();Disarm();ClearHazards();StopMovement();if(effects)effects.Hide();}
        void OnDisable(){StormSound(false);StopAllCoroutines();if(cinematicStage)cinematicStage.End();ClearEncounterObjects();ClearCavalryEffects();RestoreSwordGrip();Disarm();ClearHazards();StopMovement();if(effects)effects.Hide();busy=false;}
        void OnDestroy(){if(health)health.Died-=Died;}
    }
}
