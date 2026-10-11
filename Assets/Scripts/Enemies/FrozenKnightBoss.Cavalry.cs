using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
namespace DogLife
{
    // 기존 보스의 HP/AI/판정을 공유하는 군마 협공, 빙판, 기마 얼음검 패턴입니다.
    public sealed partial class FrozenKnightBoss
    {
        [Serializable] public class CavalryFurySettings
        {
            [Min(.1f)] public float safeRadius=7,outerRadius=45,gatherWarning=2.5f,outerDamage=1,outerTick=1;
            [Min(1)] public int charges=6;
            [Min(.1f)] public float entryRadius=14,chargeSpeed=17,chargeWidth=2.6f,bodyLength=4,damage=1,knockback=7;
            [Min(1)] public float chargeSpeedMultiplier=1.2f;
            [Min(.1f)] public float warning=1,delay=.35f,repositionSpeed=13,repositionTimeout=6;
            [Range(15,120)] public float directionStep=90;
            [Min(.1f)] public float safeRouteMargin=.6f;
            [Range(12,48)] public int routeSamples=24;
        }
        [Serializable] public class IceFieldSettings
        {
            [Min(.1f)] public float radius=12,steeringAcceleration=14;
        }
        [Serializable] public class MountedBladesSettings
        {
            [Min(1)] public int count=3;
            [Min(.1f)] public float damage=1,speed=14,maxDistance=35,width=1.1f,length=3,height=1.1f;
            [Min(.1f)] public float warning=.85f,shotDelay=.2f,recovery=1.5f,cooldown=8;
            [Min(0)] public float slowDuration=3;
            [Range(.1f,1)] public float movementMultiplier=.6f;
            [Min(1)] public float homingTurnSpeed=210;
            [UnityEngine.Serialization.FormerlySerializedAs("iceStructureDuration"),Min(.1f)] public float plantedSwordDuration=4.5f;
        }
        [Header("얼음검의 저주 — 보스 주변 안전지대 / 군마 협공")]
        [SerializeField] CavalryFurySettings cavalryFury=new CavalryFurySettings();
        [SerializeField] FrozenKnightVfx horseWarningVfx;
        [Header("탑승 후 바닥에 남은 검 / 빙판")]
        [SerializeField] IceFieldSettings plantedField=new IceFieldSettings();
        [SerializeField] WinterIcePatch swordIcePatch;
        [SerializeField] FrozenKnightVfx swordFieldVfx;
        [Header("기마 얼음검 발사 — 대시에는 슬로우 미적용")]
        [SerializeField] MountedBladesSettings mountedBlades=new MountedBladesSettings();
        [SerializeField] Transform iceBladeSpawnRight,iceBladeSpawnLeft;
        bool outerFuryActive,leaveSwordPlanted,iceFieldActive;
        float nextOuterFuryTick;
        Vector3 iceFieldPosition;
        bool horseRepositionReached;
        public bool OuterFuryActive=>outerFuryActive;
        public float FurySafeRadius=>cavalryFury.safeRadius;
        public bool SwordLeftInGround=>leaveSwordPlanted&&swordPlanted;
        public bool IceFieldActive=>iceFieldActive;

        void TickCavalryField()
        {
            if(outerFuryActive&&CanDealDamage&&!CombatFeedback.Paused(this)&&Time.time>=nextOuterFuryTick)
            {
                nextOuterFuryTick=Time.time+Mathf.Max(.05f,cavalryFury.outerTick);
                hits.Clear();HitRing(cavalryFury.safeRadius,cavalryFury.outerRadius,cavalryFury.outerDamage);
            }
            if(iceFieldActive&&swordIcePatch)swordIcePatch.transform.position=iceFieldPosition;
        }
        IEnumerator CavalryFury()
        {
            yield return PlantSword(swordFury.prepareTime);
            effects.Show(FrozenKnightVfx.Shape.Ring,transform.position,Forward,cavalryFury.outerRadius,cavalryFury.safeRadius);
            yield return Stage(CombatState.Warning,cavalryFury.gatherWarning,null,effects);
            StormSound(true);outerFuryActive=true;nextOuterFuryTick=Time.time;effects.Activate();horseWaiting=false;horseRoot.gameObject.SetActive(true);
            Vector3 center=transform.position;float baseAngle=transform.eulerAngles.y,currentChargeSpeed=cavalryFury.chargeSpeed;
            for(int i=0;i<Mathf.Max(1,cavalryFury.charges);i++)
            {
                // 반대편 왕복 후 옆 방향으로 옮깁니다. 경고 시작 이후 경로는 바뀌지 않습니다.
                float angle=baseAngle+(i/2)*cavalryFury.directionStep+(i%2)*180;
                Vector3 side=Quaternion.AngleAxis(angle,Vector3.up)*Vector3.forward;
                Vector3 entry=HorseBoundary(center,center+side*cavalryFury.entryRadius);
                yield return MoveUnriddenHorse(entry,cavalryFury.repositionSpeed);
                if(!horseRepositionReached)continue;
                Vector3 aim=player?player.transform.position:center;
                aim=center+Vector3.ClampMagnitude(Vector3.ProjectOnPlane(aim-center,Vector3.up),cavalryFury.safeRadius*.8f);
                Vector3 dir=Flat(aim-horseRoot.position);
                Vector3 end=HorseBoundary(horseRoot.position,aim+dir*cavalryFury.entryRadius);
                float distance=Vector3.ProjectOnPlane(end-horseRoot.position,Vector3.up).magnitude;
                float clearance=cavalryFury.safeRadius+cavalryFury.chargeWidth*.5f+cavalryFury.safeRouteMargin;
                if(distance<cavalryFury.safeRadius||Vector3.ProjectOnPlane(end-center,Vector3.up).magnitude<clearance)continue;
                // 끝까지 통과할 수 없는 돌진은 건너뛰어 군마가 안전구역 안에서 멈추지 않게 합니다.
                if(Physics.SphereCast(horseRoot.position+Vector3.up*chargeObstacleHeight,chargeObstacleRadius,dir,out _,distance,obstacleMask,QueryTriggerInteraction.Ignore))continue;
                horseRoot.rotation=Quaternion.LookRotation(dir);
                horseWarningVfx.Show(FrozenKnightVfx.Shape.Line,horseRoot.position,dir,distance,cavalryFury.chargeWidth,FrozenKnightVfx.Theme.HorseCharge);
                Play(horseAnimator,"Rear",cavalryFury.warning);yield return Stage(CombatState.Warning,cavalryFury.warning,null,horseWarningVfx);
                var box=(BoxCollider)chargeArea.Shape;var scale=chargeArea.transform.lossyScale;
                box.size=new Vector3(cavalryFury.chargeWidth/Mathf.Abs(scale.x),hitHeight/Mathf.Abs(scale.y),cavalryFury.bodyLength/Mathf.Abs(scale.z));
                box.center=new Vector3(0,hitHeight*.5f/scale.y,0);
                chargeArea.transform.SetPositionAndRotation(horseRoot.position,horseRoot.rotation);
                chargeArea.Arm(this,cavalryFury.damage,cavalryFury.knockback,0);
                horseWarningVfx.Activate();Play(horseAnimator,"Gallop");State=CombatState.Active;BossSound("Charge_Start",horseRoot);BossSound("Charge_Pass",horseRoot,.65f);
                for(float travelled=0;travelled<distance;)
                {
                    if(CombatFeedback.Paused(this)){yield return null;continue;}
                    float step=Mathf.Min(currentChargeSpeed*Time.deltaTime,distance-travelled);
                    if(Physics.SphereCast(horseRoot.position+Vector3.up*chargeObstacleHeight,chargeObstacleRadius,dir,out var hit,step+chargeSkin,obstacleMask,QueryTriggerInteraction.Ignore))step=Mathf.Min(step,Mathf.Max(0,hit.distance-chargeSkin));
                    if(step<=.001f)break;
                    horseRoot.position+=dir*step;travelled+=step;chargeArea.transform.SetPositionAndRotation(horseRoot.position,horseRoot.rotation);
                    yield return null;
                }
                // 실제로 수행한 돌진마다 누적 가속하며 인위적인 최대 속도는 두지 않습니다.
                currentChargeSpeed*=cavalryFury.chargeSpeedMultiplier;
                chargeArea.Disarm();horseWarningVfx.Hide();Play(horseAnimator,"Idle");yield return Stage(CombatState.Recovery,cavalryFury.delay);
            }
            StormSound(false);outerFuryActive=false;effects.Hide();horseWarningVfx.Hide();chargeArea.Disarm();
            yield return MoveUnriddenHorse(horseWaitingPosition,cavalryFury.repositionSpeed);
            horseWaitingPosition=horseRoot.position;horseWaitingRotation=horseRoot.rotation;horseWaiting=true;Play(horseAnimator,"Idle");
            yield return Recover(swordFury.recovery);
        }
        Vector3 HorseBoundary(Vector3 start,Vector3 desired)
        {
            var filter=new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask};
            if(Nav&&NavMesh.Raycast(start,desired,out var edge,filter))desired=Vector3.MoveTowards(edge.position,start,chargeObstacleRadius+chargeSkin);
            if(NavMesh.SamplePosition(desired,out var point,navSampleRadius,filter))return point.position;
            return start;
        }
        IEnumerator MoveUnriddenHorse(Vector3 destination,float speed)
        {
            State=CombatState.Reposition;chargeArea.Disarm();horseWarningVfx.Hide();
            horseRepositionReached=false;
            var route=OuterHorseRoute(horseRoot.position,destination);if(route==null)yield break;
            Play(horseAnimator,"Gallop");float elapsed=0;
            foreach(var corner in route)
            {
                while(Vector3.Distance(horseRoot.position,corner)>.05f&&elapsed<cavalryFury.repositionTimeout)
                {
                    Vector3 delta=corner-horseRoot.position;
                    horseRoot.rotation=Quaternion.RotateTowards(horseRoot.rotation,Quaternion.LookRotation(Flat(delta)),turnSpeed*3*Time.deltaTime);
                    horseRoot.position=Vector3.MoveTowards(horseRoot.position,corner,speed*Time.deltaTime);elapsed+=Time.deltaTime;yield return null;
                }
            }
            horseRepositionReached=Vector3.Distance(horseRoot.position,destination)<.12f;
            Play(horseAnimator,"Idle");
        }
        Vector3[] OuterHorseRoute(Vector3 start,Vector3 destination)
        {
            // 원 바깥의 NavMesh 가시성 그래프를 사용합니다. 모든 선분이 안전구역을 피하는지 검사합니다.
            float clearance=cavalryFury.safeRadius+cavalryFury.chargeWidth*.5f+cavalryFury.safeRouteMargin;
            return ClearHorseRoute(start,destination,transform.position,clearance,cavalryFury.entryRadius);
        }
        // 군마의 접근 경로도 같은 우회 검사를 사용해 꽂힌 검을 가로지르지 않습니다.
        Vector3[] ClearHorseRoute(Vector3 start,Vector3 destination,Vector3 center,float clearance,float outerRadius)
        {
            var nodes=new System.Collections.Generic.List<Vector3>{start,destination};
            var filter=new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask};
            for(int ring=0;ring<2;ring++)for(int i=0;i<cavalryFury.routeSamples;i++)
            {
                float r=ring==0?clearance+.45f:Mathf.Max(clearance+1,outerRadius);
                Vector3 sample=center+Quaternion.AngleAxis(360f*i/cavalryFury.routeSamples,Vector3.up)*Vector3.forward*r;
                if(NavMesh.SamplePosition(sample,out var point,.7f,filter)&&Vector3.ProjectOnPlane(point.position-center,Vector3.up).magnitude>=clearance)nodes.Add(point.position);
            }
            int n=nodes.Count;var costs=new float[n];var previous=new int[n];var visited=new bool[n];
            for(int i=0;i<n;i++){costs[i]=float.PositiveInfinity;previous[i]=-1;}costs[0]=0;
            for(int iteration=0;iteration<n;iteration++)
            {
                int current=-1;for(int i=0;i<n;i++)if(!visited[i]&&(current<0||costs[i]<costs[current]))current=i;
                if(current<0||float.IsPositiveInfinity(costs[current]))return null;if(current==1)break;visited[current]=true;
                for(int j=0;j<n;j++)
                {
                    if(visited[j]||j==current)continue;
                    Vector3 a=Vector3.ProjectOnPlane(nodes[current]-center,Vector3.up),delta=Vector3.ProjectOnPlane(nodes[j]-nodes[current],Vector3.up);
                    float u=delta.sqrMagnitude>.0001f?Mathf.Clamp01(-Vector3.Dot(a,delta)/delta.sqrMagnitude):0;
                    if((a+delta*u).magnitude<clearance-.01f)continue;
                    if(NavMesh.Raycast(nodes[current],nodes[j],out _,filter))continue;
                    if(Physics.SphereCast(nodes[current]+Vector3.up*chargeObstacleHeight,chargeObstacleRadius,delta.normalized,out _,delta.magnitude,obstacleMask,QueryTriggerInteraction.Ignore))continue;
                    float cost=costs[current]+Vector3.Distance(nodes[current],nodes[j]);if(cost>=costs[j])continue;costs[j]=cost;previous[j]=current;
                }
            }
            if(previous[1]<0)return null;var route=new System.Collections.Generic.List<Vector3>();
            for(int i=1;i>0;i=previous[i])route.Add(nodes[i]);route.Reverse();return route.ToArray();
        }
        void BeginSwordField()
        {
            if(!swordIcePatch||!swordFieldVfx||!swordPlanted)return;
            iceFieldPosition=new Vector3(plantedSwordPosition.x,transform.position.y,plantedSwordPosition.z);
            swordIcePatch.transform.position=iceFieldPosition;
            // 검 위치에서 방의 모든 모서리까지 덮되 미끄러지는 가속도는 그대로 사용합니다.
            float radius=Mathf.Max(plantedField.radius,arenaRadius*2+Vector3.ProjectOnPlane(Arena-iceFieldPosition,Vector3.up).magnitude);
            if(room)foreach(var renderer in room.GetComponentsInChildren<MeshRenderer>())
            {
                if(renderer.GetComponentInParent<CombatHealth>())continue;
                var b=renderer.bounds;
                for(int i=0;i<4;i++){var corner=new Vector3((i&1)==0?b.min.x:b.max.x,iceFieldPosition.y,(i&2)==0?b.min.z:b.max.z);radius=Mathf.Max(radius,Vector3.Distance(iceFieldPosition,corner));}
            }
            swordIcePatch.Initialize(health,radius,float.PositiveInfinity,plantedField.steeringAcceleration);
            swordFieldVfx.Hide();iceFieldActive=true;
        }
        IEnumerator MountedIceBlades()
        {
            Play(horseAnimator,"Idle");
            for(int i=0;i<Mathf.Max(1,mountedBlades.count);i++)
            {
                Aim(true);
                // 손 대신 Hierarchy에 배치한 등 뒤 위쪽의 좌우 발사점을 번갈아 사용합니다.
                Transform spawnPoint=i%2==0?iceBladeSpawnRight:iceBladeSpawnLeft;
                if(!spawnPoint){Debug.LogError("Frozen Knight ice blade spawn point is missing",this);yield break;}
                Vector3 spawn=spawnPoint.position;
                var collider=player.GetComponent<Collider>();Vector3 targetPoint=collider?collider.bounds.center:player.transform.position+Vector3.up;
                Vector3 direction=(targetPoint-spawn).normalized;
                // 준비 중에는 조준하고, 발사 순간의 플레이어 위치로 방향을 고정합니다.
                var blade=Instantiate(iceBladePrefab,spawn,Quaternion.LookRotation(direction));hazards.Add(blade);blade.Prepare(this);
                blade.Track(player.transform,mountedBlades.homingTurnSpeed,mountedBlades.plantedSwordDuration);
                // 생성 고리는 검에 남기고, 바닥 조준 표시는 실제 표적 아래에 따로 놓습니다.
                effects.Show(FrozenKnightVfx.Shape.Ring,IceBladeWarningPoint(),Forward,1.4f,.9f);
                // 연속 발사도 매번 새로 지정한 시전 동작을 처음부터 재생합니다.
                float clipTime=StartNativeCast("IceSwordMounted");
                var warning=Stage(CombatState.Warning,clipTime*iceSwordCastMoment,null,effects);
                while(warning.MoveNext())
                {
                    effects.MoveOrigin(IceBladeWarningPoint());
                    yield return warning.Current;
                }
                // 발사 직전까지 조준을 맞추고 이후에는 표식을 고정합니다.
                effects.MoveOrigin(IceBladeWarningPoint());
                if(blade)blade.Launch(this,mountedBlades.width,mountedBlades.length,mountedBlades.height,mountedBlades.damage,0,0,mountedBlades.speed,mountedBlades.maxDistance,0,0,mountedBlades.movementMultiplier,mountedBlades.slowDuration);
                effects.Activate();yield return Stage(CombatState.Active,Mathf.Max(mountedBlades.shotDelay,clipTime*(1-iceSwordCastMoment)));effects.Hide();
            }
            yield return Recover(mountedBlades.recovery);
        }
        Vector3 IceBladeWarningPoint()
        {
            var body=player.GetComponent<Collider>();
            Vector3 point=body?body.bounds.center:player.transform.position;
            point.y=Arena.y;
            return GroundPoint(point);
        }
        void ClearCavalryEffects()
        {
            StormSound(false);outerFuryActive=false;iceFieldActive=false;leaveSwordPlanted=false;
            if(horseWarningVfx)horseWarningVfx.Hide();if(swordFieldVfx)swordFieldVfx.Hide();if(swordIcePatch)swordIcePatch.Clear();
        }
    }
}
