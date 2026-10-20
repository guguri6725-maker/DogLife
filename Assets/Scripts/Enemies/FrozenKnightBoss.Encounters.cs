using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
namespace DogLife
{
    public sealed partial class FrozenKnightBoss
    {
        [Header("신규 패턴 프리팹 — Inspector 연결")]
        [SerializeField] FrozenKnightConstruct pillarPrefab,pickupPrefab,corePrefab;
        [SerializeField] FrozenKnightSpear spearPrefab;
        [SerializeField] FrozenKnightSwordCore swordCore;
        [SerializeField] GameObject winterMinionA,winterMinionB,winterMinionC;
        [Header("전장 / 기둥")]
        [SerializeField] Transform arenaCenter;
        [SerializeField] Transform spearHand;
        [SerializeField,Min(.1f)] float pillarSeparation=3,pillarClearance=1.35f,crystalGatherMinimum=7,spearReleaseTime=.2f,spearReleaseDuration=.55f,spearHandOffset=.7f;
        [SerializeField,Min(1)] int pillarCount=3,pillarHits=3;
        [SerializeField,Range(1,3)] int maxMinions=3;
        [SerializeField,Min(.1f)] float arenaRadius=18,pillarWarning=2,pillarLifetime=35,pillarCooldown=16;
        [Header("삼각형 얼음벽 — 세 면의 중심 반경 / 고정 방향")]
        [SerializeField,Min(1)] float pillarTriangleRadius=7.2f;
        [SerializeField] float pillarTriangleYaw;
        [Header("확장 원형 검기 — 점프 또는 앉기")]
        [SerializeField,Min(.1f)] float waveWarning=1.5f,waveSpeed=8,waveWidth=.6f,waveHeight=1.5f,waveDamage=1,waveCooldown=12;
        [SerializeField,Min(0)] float jumpClearance=.65f;
        [Header("70% / 30% 한기")]
        [SerializeField,Range(0,1)] float coldHighHP=.7f,coldLowHP=.3f;
        [SerializeField,Min(1)] int coldCrystalCount=8;
        [SerializeField,Min(.1f)] float coldWarning=4,coldDuration=15,coldTick=1,coldDamage=1,crystalSpeed=.65f,crystalImmunity=3;
        [Header("기마 분신 / 소환 / 원격검")]
        [SerializeField,Min(.1f)] float cloneOffset=3,cloneCooldown=14,minionWarning=2,minionCooldown=20,remoteSwordWarning=1.2f,remoteSwordRange=4,remoteSwordCooldown=14;
        [Header("최후의 결정 / 얼음창")]
        [SerializeField,Min(1)] int finaleSpears=4,coreSpearHits=3;
        [SerializeField,Range(30,160)] float finalePositionAngle=110;
        [SerializeField,Range(.3f,.9f)] float finalePositionRadius=.75f;
        [SerializeField,Min(.1f)] float finalePositionMinDistance=4,finaleRetryDelay=.5f;
        [SerializeField,Min(1)] float finaleRepositionSpeed=17,finaleRepositionAcceleration=60;
        bool finalePositionChanged;
        [Header("최후 발악 — 잠식하는 눈보라")]
        [SerializeField,Min(1)] float finaleBlizzardSeconds=40,finaleBlizzardHeight=7;
        [SerializeField,Range(.3f,2)] float finaleBlizzardDensity=1;
        [SerializeField,Min(.1f)] float finaleBlizzardFadeSeconds=2;
        FrozenKnightVfx finaleBlizzard;
        [SerializeField,Min(.1f)] float coreRadius=3,finalePrepare=3,spearWarning=2.2f,spearSpeed=7,spearDamage=2,spearGap=1.5f,finaleExplosionWarning=2;
        [Header("신규 패턴 동작 / 후딜레이")]
        [SerializeField,Min(.1f)] float encounterRecovery=2,remoteSwingDuration=.6f,remoteReturnDuration=1,finaleMoveTimeout=6;
        readonly List<GameObject> encounterObjects=new List<GameObject>();
        readonly List<CombatHealth> summonedMinions=new List<CombatHealth>();
        bool coldHighUsed,coldLowUsed,finaleUsed,remoteSwordActive;
        [SerializeField,Min(.1f)] float remoteSwordHeight=.8f,spearMarkerLength=3;
        [Header("원본 애니메이션 내 시전 시점 — 1배속")]
        [SerializeField,Range(.1f,.9f)] float wallCastMoment=.65f,iceSwordCastMoment=.65f,pushCastMoment=.55f,spinCastMoment=.55f,remoteFirstMoment=.38f,remoteSecondMoment=.72f;
        Vector3 initialArenaCenter;
        public bool BlizzardActive=>CurrentPattern==Pattern.ColdWave&&State==CombatState.Active;
        public bool CloneChargeVisible=>CurrentPattern==Pattern.CloneCharge&&(State==CombatState.Warning||State==CombatState.Attack||State==CombatState.Active);
        public float CloneSeparation=>cloneOffset;
        Vector3 Arena=>initialArenaCenter;
        GameObject Keep(GameObject value){encounterObjects.Add(value);return value;}
        Vector3 GroundPoint(Vector3 desired)
        {
            if(NavMesh.SamplePosition(desired,out var hit,navSampleRadius,agent.areaMask))return hit.position;
            if(NavMesh.SamplePosition(Arena,out var center,navSampleRadius,agent.areaMask)&&NavMesh.Raycast(center.position,desired,out var edge,agent.areaMask))
                return Vector3.MoveTowards(edge.position,center.position,2);
            return desired;
        }
        Vector3 Perimeter(int index,int count,float radius)
        {return GroundPoint(Arena+Quaternion.Euler(0,index*360f/Mathf.Max(1,count),0)*Vector3.forward*radius);}
        bool ThresholdAttack()
        {
            if(PhaseNumber!=1)return false;
            float fraction=health.Current/Mathf.Max(1,health.maxHealth);
            if(!coldHighUsed&&fraction<=coldHighHP){coldHighUsed=true;Begin(Pattern.ColdWave);return true;}
            if(!Phase1SpecialUsed&&fraction<=swordFury.triggerHP){Begin(Pattern.SwordFury);return true;}
            if(!coldLowUsed&&fraction<=coldLowHP){coldLowUsed=true;Begin(Pattern.ColdWave);return true;}
            return false;
        }
        void AddCandidate(Pattern pattern){if(previous!=(int)pattern&&Time.time>=cooldowns[(int)pattern])candidates.Add(pattern);}
        void ClearEncounterObjects(bool preserveWalls=false)
        {
            if(finaleBlizzard){finaleBlizzard.ReleaseFinaleBlizzard();finaleBlizzard=null;}
            if(target)target.FrostProtectionPaused=false;
            // 페이즈 전환에는 벽을 남기고, 최후 발악/사망/방 해제에서는 모두 정리합니다.
            for(int i=encounterObjects.Count-1;i>=0;i--)
            {
                var value=encounterObjects[i];
                if(preserveWalls&&value&&value.activeInHierarchy&&value.TryGetComponent<FrozenKnightConstruct>(out var wall)&&wall.IsPillar)continue;
                if(value)Destroy(value);encounterObjects.RemoveAt(i);
            }
            summonedMinions.Clear();remoteSwordActive=false;
        }
        IEnumerator IcePillars()
        {
            BossSound("Frost_Charge");
            // 세 벽을 같은 기준으로 계산하여 개별 위치 보정으로 삼각형이 벌어지지 않습니다.
            var points=new Vector3[3];var rotations=new Quaternion[3];
            for(int i=0;i<3;i++)TriangleWallPose(i,out points[i],out rotations[i]);
            var box=pillarPrefab.GetComponent<BoxCollider>();
            effects.ShowWallEnclosure(points,rotations,Vector3.Scale(box.size,pillarPrefab.transform.localScale));
            float clipTime=StartNativeCast(PhaseNumber==1?"IceWallFull":"IceWallMounted");
            yield return Stage(CombatState.Warning,clipTime*wallCastMoment,null,effects);
            effects.Activate();
            for(int i=0;i<3;i++)
            {
                // 남아 있는 면은 유지하고, 부서진 면만 같은 위치에 다시 세웁니다.
                bool occupied=false;
                foreach(var old in encounterObjects)
                    if(old&&old.activeInHierarchy&&old.TryGetComponent<FrozenKnightConstruct>(out var existing)&&existing.IsPillar&&
                        (old.transform.position-points[i]).sqrMagnitude<.25f){occupied=true;break;}
                if(occupied)continue;
                var c=Instantiate(pillarPrefab,points[i],rotations[i]);Keep(c.gameObject);
                c.Initialize(this,FrozenKnightConstruct.Kind.Pillar,player,pillarHits,pillarLifetime);
            }
            Physics.SyncTransforms();
            yield return Stage(CombatState.Active,Mathf.Max(.7f,clipTime*(1-wallCastMoment)));yield return Recover(encounterRecovery);
        }
        void TriangleWallPose(int index,out Vector3 point,out Quaternion rotation)
        {
            rotation=Quaternion.Euler(0,pillarTriangleYaw+index*120,0);
            point=Arena+rotation*Vector3.forward*pillarTriangleRadius;
            // NavMesh의 가로 보정은 쓰지 않아, 기존 벽이 길을 막아도 다음 시전 위치가 바뀌지 않습니다.
            point.y=Arena.y;
        }
        IEnumerator ExpandingBlade()
        {
            Vector3 origin=transform.position;
            effects.Show(FrozenKnightVfx.Shape.Ring,origin,Forward,arenaRadius,0,FrozenKnightVfx.Theme.Crack);
            float clipTime=StartNativeCast("Spin3Sword");
            yield return Stage(CombatState.Warning,clipTime*spinCastMoment,null,effects);effects.Hide();
            BossSound("BladeRing");State=CombatState.Active;bool hit=false;float activeTime=0;
            for(float radius=0;radius<arenaRadius;)
            {
                float previousRadius=radius;if(!CombatFeedback.Paused(this)){radius+=waveSpeed*Time.deltaTime;activeTime+=Time.deltaTime;}
                effects.Show(FrozenKnightVfx.Shape.Ring,origin+Vector3.up*waveHeight,Forward,radius,Mathf.Max(0,radius-waveWidth),FrozenKnightVfx.Theme.BladeWave);effects.Activate();
                float distance=Vector3.ProjectOnPlane(player.transform.position-origin,Vector3.up).magnitude;
                // 확장 고리가 지나가는 프레임을 넓혀 검사하되 점프 높이/앉기는 기존 PlayerMotor에서 읽습니다.
                bool dodge=player.Crouching||(!player.Grounded&&player.transform.position.y-origin.y>jumpClearance);
                if(!hit&&!dodge&&distance>=previousRadius-waveWidth&&distance<=radius+.5f)hit=DealHit(target,waveDamage,0,0,origin);
                yield return null;
            }
            effects.Hide();if(activeTime<clipTime*(1-spinCastMoment))yield return Stage(CombatState.Recovery,clipTime*(1-spinCastMoment)-activeTime);
            yield return Recover(encounterRecovery);
        }
        IEnumerator ColdWave()
        {
            BossSound("Frost_Charge");target.FrostProtectionPaused=true;
            var crystals=new List<GameObject>();float gather=Mathf.Max(coldWarning,crystalGatherMinimum);
            for(int i=0;i<coldCrystalCount;i++)
            {
                var c=Instantiate(pickupPrefab,Perimeter(i,coldCrystalCount,arenaRadius*.85f)+Vector3.up,Quaternion.identity);Keep(c.gameObject);crystals.Add(c.gameObject);
                c.Initialize(this,FrozenKnightConstruct.Kind.Pickup,player,1,gather+coldDuration,crystalSpeed,crystalImmunity);
            }
            effects.Show(FrozenKnightVfx.Shape.Ring,Arena,Forward,arenaRadius*2,0,FrozenKnightVfx.Theme.ColdField);
            yield return Stage(CombatState.Warning,gather,"FrostChannel",effects);
            target.FrostProtectionPaused=false;
            StormSound(true);effects.Activate();Play(knightAnimator,"Roar",1);State=CombatState.Active;float next=0;
            for(float t=0;t<coldDuration;t+=Time.deltaTime)
            {
                if(t>=next){next=t+coldTick;DealHit(target,coldDamage,0,0,Arena);}yield return null;
            }
            StormSound(false);foreach(var c in crystals)if(c)Destroy(c);effects.Hide();yield return Recover(encounterRecovery);
        }
        IEnumerator CloneRush()
        {
            BossSound("Clone_Appear");
            // 좌우 분신을 포함한 전체 폭을 경고/판정에 동일하게 사용합니다.
            yield return Charge(charge,charge.attack.damage,charge.attack.width+cloneOffset*2,charge.attack.warning,false);
            yield return Recover(charge.attack.recovery);
        }
        IEnumerator SummonWinterMinions()
        {
            summonedMinions.RemoveAll(h=>!h||h.Dead||!h.gameObject.activeInHierarchy);
            effects.Show(FrozenKnightVfx.Shape.Ring,transform.position,Forward,3,0,FrozenKnightVfx.Theme.Summon);
            float clipTime=StartNativeCast("IceWallMounted");
            yield return Stage(CombatState.Warning,clipTime*wallCastMoment,null,effects);effects.Activate();
            BossSound("Summon");var choices=new[]{winterMinionA,winterMinionB,winterMinionC};int missing=Mathf.Max(0,Mathf.Clamp(maxMinions,1,3)-summonedMinions.Count);
            for(int i=0;i<missing;i++)
            {
                var prefab=choices[Random.Range(0,choices.Length)];if(!prefab)continue;
                var obj=Instantiate(prefab,Perimeter(i,Mathf.Max(3,missing),arenaRadius*.6f),Quaternion.identity);Keep(obj);
                CinematicCue(FrozenKnightVfx.CinematicMoment.Summon,obj.transform,2.2f,3.2f);
                var hp=obj.GetComponent<CombatHealth>();if(hp)summonedMinions.Add(hp);
            }
            yield return Stage(CombatState.Active,Mathf.Max(.4f,clipTime*(1-wallCastMoment)));yield return Recover(encounterRecovery);
        }
        IEnumerator RemoteSword()
        {
            float clipTime=StartNativeCast("IceSwordMounted"),elapsed=0;
            remoteSwordActive=true;Vector3 rest=plantedSwordPosition;Quaternion restRotation=plantedSwordRotation;
            // 손잡이 보정이 있는 모델도 실제 칼끝 축으로 정렬해 정면의 반원을 휘두릅니다.
            Vector3 bladeAxis=swordRoot.InverseTransformPoint(effects.SwordTipPosition);
            Quaternion bladeForward=Quaternion.FromToRotation(bladeAxis.normalized,Vector3.forward);
            float reach=Mathf.Max(remoteSwordRange,Vector3.Distance(swordRoot.position,effects.SwordTipPosition));
            for(int i=0;i<2;i++)
            {
                // 점프 높이는 조준에 사용하지 않아 지면 위의 검을 뛰어넘을 수 있습니다.
                Vector3 groundTarget=player.transform.position;groundTarget.y=Arena.y;
                Vector3 center=GroundPoint(groundTarget);Vector3 aim=Flat(player.transform.position-transform.position);
                Quaternion facing=Quaternion.LookRotation(aim);Vector3 pivot=center-aim*2;Vector3 from=swordRoot.position,to=pivot+Vector3.up*remoteSwordHeight;Quaternion original=swordRoot.rotation;
                effects.Show(FrozenKnightVfx.Shape.Sector,pivot,aim,reach,180,FrozenKnightVfx.Theme.Slash);
                State=CombatState.Warning;
                float warning=Mathf.Max(minimumAttackTime,clipTime*(i==0?remoteFirstMoment:remoteSecondMoment)-elapsed);
                for(float t=0;t<warning;t+=Time.deltaTime)
                {
                    float u=t/warning;swordRoot.SetPositionAndRotation(Vector3.Lerp(from,to,Mathf.SmoothStep(0,1,u)),Quaternion.Slerp(original,facing*Quaternion.Euler(0,i==0?-90:90,0)*bladeForward,u));effects.SetWarningProgress(u,i==0);yield return null;
                }
                elapsed+=warning+remoteSwingDuration;
                BossSound("Sword_Swing",swordRoot);State=CombatState.Active;effects.BeginSlash(i);effects.Activate();swordArea.Arm(this,combo.first.damage,combo.first.knockback,0);
                for(float t=0;t<remoteSwingDuration;t+=Time.deltaTime)
                {float u=Mathf.SmoothStep(0,1,t/remoteSwingDuration);float angle=Mathf.Lerp(i==0?-90:90,i==0?90:-90,u);swordRoot.SetPositionAndRotation(to,facing*Quaternion.Euler(0,angle,0)*bladeForward);yield return null;}
                swordArea.Disarm();effects.Hide();
            }
            if(elapsed<clipTime)yield return Stage(CombatState.Recovery,clipTime-elapsed);
            State=CombatState.Recovery;Vector3 start=swordRoot.position;Quaternion rotation=swordRoot.rotation;
            // 회수는 천천히 출발해 곡선으로 돌아오고 지면에 꽂히기 전에 감속합니다.
            for(float t=0;t<remoteReturnDuration;t+=Time.deltaTime){float u=Mathf.SmoothStep(0,1,t/remoteReturnDuration);swordRoot.SetPositionAndRotation(Vector3.Lerp(start,rest,u)+Vector3.up*Mathf.Sin(u*Mathf.PI)*.6f,Quaternion.Slerp(rotation,restRotation,u));yield return null;}
            swordRoot.SetPositionAndRotation(rest,restRotation);remoteSwordActive=false;yield return Recover(1.5f);
        }
        Vector3 SpearMarkerOrigin(Vector3 aim)
        {
            // 창 앞이 아니라 플레이어 발밑에서 끝나는 짧은 방향 표시입니다.
            Vector3 point=player.transform.position;point.y=Arena.y;
            return GroundPoint(point)-Flat(aim)*spearMarkerLength;
        }
        float StartNativeCast(string state)
        {
            // 선딜에 맞춰 배속하지 않고 원본 길이 안에서 피해/생성 시점을 나눕니다.
            lastKnight=null;Play(knightAnimator,state);
            foreach(var clip in knightAnimator.runtimeAnimatorController.animationClips)if(clip.name==state)return clip.length;
            Debug.LogError("Missing native cast clip: "+state,this);return 1;
        }
        void HoldHorseForSpear()
        {
            StopMovement();
            // Walk의 다리 동작이 시전 중 남지 않게 네 발을 디딘 Idle 자세를 고정합니다.
            Play(horseAnimator,"Idle");horseAnimator.Play("Idle",0,0);horseAnimator.Update(0);horseAnimator.speed=0;
        }
        IEnumerator RepositionForFinalSpear(FrozenKnightSwordCore core,int shot)
        {
            finalePositionChanged=false;State=CombatState.Reposition;Disarm();effects.Hide();
            if(!Nav)yield break;
            Vector3 start=transform.position;float angle=Quaternion.LookRotation(Flat(start-Arena)).eulerAngles.y;
            float oldStop=agent.stoppingDistance,oldAcceleration=agent.acceleration;
            try
            {
                agent.stoppingDistance=positionArrivalDistance*.5f;agent.acceleration=finaleRepositionAcceleration;
                for(int attempt=0;attempt<12;attempt++)
                {
                    // 검을 가운데 두고 플레이어가 반대편으로 이동할 수 있게 외곽의 다른 각도를 선택합니다.
                    float turn=finalePositionAngle+attempt*25;
                    Vector3 desired=Arena+Quaternion.Euler(0,angle+turn,0)*Vector3.forward*(arenaRadius*finalePositionRadius);
                    NavMeshHit hit;var path=new NavMeshPath();
                    if(!NavMesh.SamplePosition(desired,out hit,navSampleRadius,agent.areaMask)||GroundDistance(start,hit.position)<finalePositionMinDistance)continue;
                    if(!agent.CalculatePath(hit.position,path)||path.status!=NavMeshPathStatus.PathComplete)continue;
                    agent.speed=finaleRepositionSpeed;agent.isStopped=false;agent.SetPath(path);
                    for(float elapsed=0;elapsed<finaleMoveTimeout;)
                    {
                        if(core.Broken||!target||target.Dead)yield break;
                        if(CombatFeedback.Paused(this)){agent.isStopped=true;yield return null;continue;}
                        agent.isStopped=false;elapsed+=Time.deltaTime;
                        if(GroundDistance(transform.position,hit.position)<=positionArrivalDistance)break;
                        TravelPose(true);yield return null;
                    }
                    StopMovement();
                    if(GroundDistance(start,transform.position)>=finalePositionMinDistance){finalePositionChanged=true;yield break;}
                }
            }
            finally{if(agent){agent.stoppingDistance=oldStop;agent.acceleration=oldAcceleration;}StopMovement();Play(horseAnimator,"Idle");}
        }
        IEnumerator FinalStand()
        {
            ClearEncounterObjects();ClearHazards();Disarm();StopMovement();health.invulnerable=true;State=CombatState.Warning;
            var core=swordCore;core.Begin(coreSpearHits);HoldHorseForSpear();
            finaleBlizzard=effects.CreateFinaleBlizzard(this,core,GroundPoint(Arena),arenaRadius,finaleBlizzardSeconds,finaleBlizzardHeight,finaleBlizzardDensity,finaleBlizzardFadeSeconds);
            StormSound(true);
            Vector3 swordCenter=GroundPoint(core.Center);
            effects.Show(FrozenKnightVfx.Shape.Ring,swordCenter,Forward,coreRadius,0,FrozenKnightVfx.Theme.SwordConjure);
            yield return Stage(CombatState.Warning,finalePrepare,"MountedCommand",effects);effects.Hide();
            for(int i=0;i<finaleSpears;i++)
            {
                if(!core||core.Broken)break;
                // 매 투척 전에 다른 외곽 위치로 이동합니다. 이동 중에는 조준 표시와 공격을 끕니다.
                do
                {
                    yield return RepositionForFinalSpear(core,i);
                    if(!finalePositionChanged&&!core.Broken)yield return Stage(CombatState.Reposition,finaleRetryDelay);
                }while(!finalePositionChanged&&!core.Broken&&target&&!target.Dead);
                if(core.Broken||!target||target.Dead)break;
                HoldHorseForSpear();
                Aim(true);
                Vector3 launch=(spearHand?spearHand.position:transform.position+Vector3.up*coreRadius)+Forward*spearHandOffset;
                Vector3 aim=(player.transform.position+Vector3.up-launch).normalized;
                effects.Show(FrozenKnightVfx.Shape.Line,SpearMarkerOrigin(aim),Flat(aim),spearMarkerLength,1,FrozenKnightVfx.Theme.SpearAim);
                var spear=Instantiate(spearPrefab,launch,Quaternion.LookRotation(aim));Keep(spear.gameObject);spear.Prepare(this,0);
                State=CombatState.Warning;Play(knightAnimator,"SpearThrow",spearWarning);
                for(float t=0;t<spearWarning;t+=Time.deltaTime)
                {
                    Aim(false);
                    launch=(spearHand?spearHand.position:transform.position+Vector3.up*coreRadius)+Forward*spearHandOffset;
                    aim=(player.transform.position+Vector3.up-launch).normalized;
                    if(spear){spear.transform.SetPositionAndRotation(launch,Quaternion.LookRotation(aim));spear.Prepare(this,t/spearWarning);}
                    effects.MovePose(SpearMarkerOrigin(aim),Flat(aim));effects.SetWarningProgress(t/spearWarning);yield return null;
                }
                State=CombatState.Attack;Play(knightAnimator,"SpearRelease",spearReleaseDuration);
                for(float t=0;t<spearReleaseTime;t+=Time.deltaTime)
                {
                    launch=(spearHand?spearHand.position:transform.position+Vector3.up*coreRadius)+Forward*spearHandOffset;
                    aim=(player.transform.position+Vector3.up-launch).normalized;
                    spear.transform.SetPositionAndRotation(launch,Quaternion.LookRotation(aim));effects.MovePose(SpearMarkerOrigin(aim),Flat(aim));yield return null;
                }
                effects.Activate();
                // 플레이어는 바닥에 꽂힌 검을 사이에 두고 창을 유도합니다.
                aim=(player.transform.position+Vector3.up-launch).normalized;
                spear.transform.rotation=Quaternion.LookRotation(aim);spear.Launch(this,core,spearSpeed,spearDamage,arenaRadius*3);
                yield return Stage(CombatState.Active,spearReleaseDuration-spearReleaseTime);Play(knightAnimator,"MountedIdle");
                yield return Stage(CombatState.Active,spearGap+arenaRadius*2/spearSpeed);effects.Hide();
            }
            bool broken=!core||core.Broken;
            if(broken)
            {
                // 검을 부수면 무기와 빙판을 해제하고 마지막 한 타를 기다립니다.
                ClearEncounterObjects();ClearHazards();ClearCavalryEffects();Disarm();effects.Hide();StopMovement();
                State=CombatState.Groggy;Play(knightAnimator,"MountedGroggy");Play(horseAnimator,"Groggy");health.ReleaseForFinalHit();
                while(!health.Dead)yield return null;
                yield break;
            }
            if(!broken)
            {
                effects.Show(FrozenKnightVfx.Shape.Ring,Arena,Forward,arenaRadius*2,0,FrozenKnightVfx.Theme.ColdField);
                State=CombatState.Warning;Play(knightAnimator,"MountedCommand",finaleExplosionWarning);
                for(float t=0;t<finaleExplosionWarning;t+=Time.deltaTime)
                {
                    if(core&&core.Broken)break;
                    float progress=t/Mathf.Max(.01f,finaleExplosionWarning);
                    effects.SetWarningProgress(progress);
                    if(finaleBlizzard)finaleBlizzard.FillFinaleBlizzard(progress);
                    yield return null;
                }
                // 마지막 경고 중에도 검 파괴 성공을 인정합니다.
                if(core&&core.Broken)
                {
                    ClearEncounterObjects();ClearHazards();ClearCavalryEffects();Disarm();effects.Hide();StopMovement();
                    State=CombatState.Groggy;Play(knightAnimator,"MountedGroggy");Play(horseAnimator,"Groggy");health.ReleaseForFinalHit();
                    while(!health.Dead)yield return null;
                    yield break;
                }
                effects.Show(FrozenKnightVfx.Shape.Ring,Arena,Forward,arenaRadius*2,0,FrozenKnightVfx.Theme.Burst);effects.Activate();
                BossSound("Final_Explosion");if(core&&!core.Broken)target.FatalDamage();
            }
            yield return Stage(CombatState.Recovery,.5f);ClearEncounterObjects();effects.Hide();health.CompleteDeferredDeath();
        }
    }
}
