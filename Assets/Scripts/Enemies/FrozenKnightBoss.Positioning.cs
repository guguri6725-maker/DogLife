using System.Collections;
using UnityEngine;
using UnityEngine.AI;
namespace DogLife
{
    public sealed partial class FrozenKnightBoss
    {
        [Header("원거리 거리 유지 / 중앙 복귀")]
        [SerializeField,Min(.1f)] float rangedMinimumDistance=7,rangedPreferredDistance=10,rangedMaximumDistance=14;
        [SerializeField,Min(.1f)] float retreatDashDistance=2.5f,retreatDashDuration=.3f,retreatCooldown=3,rangedHoldTime=3;
        [SerializeField,Min(.1f)] float centerMoveSpeed=6,positionArrivalDistance=.6f,positionRepathTime=.35f,centerSearchRadius=3;
        [SerializeField,Min(1)] float standingTurnSpeed=65;
        float nextRetreat,rangedHoldUntil;
        IEnumerator FaceBeforeCast()
        {
            State=CombatState.Reposition;StopMovement();
            while(player&&target&&!target.Dead&&Vector3.Angle(transform.forward,Flat(player.transform.position-transform.position))>3)
            {Aim(false);PlayLocomotion(false);yield return null;}
            PlayLocomotion(false);
        }
        public bool RetreatDashing {get;private set;}
        static float GroundDistance(Vector3 a,Vector3 b)=>Vector3.ProjectOnPlane(a-b,Vector3.up).magnitude;
        bool RangedPattern(Pattern p)=>p==Pattern.MountedIceBlades||p==Pattern.RemoteSword||p==Pattern.IcePillars||p==Pattern.SummonMinions||p==Pattern.ExpandingBlade;

        void TravelPose(bool gallop=false)
        {
            // 이동 방향으로 몸을 돌려 걷고, 시전 직전에 다시 플레이어를 봅니다.
            if(Nav&&agent.desiredVelocity.sqrMagnitude>.01f)
                transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(Flat(agent.desiredVelocity)),turnSpeed*(gallop?2:1.25f)*Time.deltaTime);
            Forward=transform.forward;
            if(gallop&&PhaseNumber==2){Play(knightAnimator,"MountedIdle");Play(horseAnimator,"Gallop");}
            else PlayLocomotion(true);
        }
        IEnumerator MakeRangedSpace()
        {
            if(Time.time<nextRetreat||GroundDistance(transform.position,player.transform.position)>=rangedMinimumDistance)yield break;
            nextRetreat=Time.time+retreatCooldown;
            if(!Nav)yield break;
            State=CombatState.Reposition;Disarm();effects.Hide();StopMovement();Aim(true);
            // 시선은 플레이어 쪽으로 고정하고 걷기 대신 짧게 뒤로 미끄러집니다.
            Vector3 backward=-Forward;
            RetreatDashing=true;
            Play(knightAnimator,PhaseNumber==1?"FootBackDash":"MountedBackDash",retreatDashDuration);
            if(PhaseNumber==2)Play(horseAnimator,"BackDash",retreatDashDuration);
            try
            {
            for(float t=0;t<retreatDashDuration;)
            {
                if(!Nav||!target||target.Dead)break;
                if(CombatFeedback.Paused(this)){yield return null;continue;}
                float dt=Mathf.Min(Time.deltaTime,retreatDashDuration-t);
                float step=retreatDashDistance/retreatDashDuration*dt;
                if(MoveStraight(backward,step)<step*.25f)break;
                t+=dt;yield return null;
            }
            }
            finally{RetreatDashing=false;StopMovement();}
            Aim(true);PlayLocomotion(false);
        }
        // 중앙에 도착하기 전에는 경고/피해/소환을 시작하지 않습니다. 막힌 경로는 주기적으로 재탐색합니다.
        IEnumerator MoveToArenaCenter(bool transition)
        {
            State=transition?CombatState.Transition:CombatState.Reposition;Disarm();effects.Hide();
            float oldStop=agent.stoppingDistance,repath=0;Vector3 destination=Arena;
            try
            {
                agent.stoppingDistance=positionArrivalDistance*.5f;
                while(GroundDistance(transform.position,destination)>positionArrivalDistance)
                {
                    if(!target||target.Dead)yield break;
                    if(Nav&&!CombatFeedback.Paused(this))
                    {
                        if(Time.time>=repath)
                        {
                            repath=Time.time+positionRepathTime;NavMeshPath path;
                            if(CenterPath(out destination,out path))
                            {agent.speed=centerMoveSpeed;agent.isStopped=false;agent.SetPath(path);}
                            else{destination=Arena;StopMovement();}
                        }
                        TravelPose();
                    }
                    yield return null;
                }
            }
            finally{if(agent)agent.stoppingDistance=oldStop;StopMovement();}
            Aim(true);PlayLocomotion(false);
        }
        // 중앙에 장애물이 있으면 가까운 순서로 주변의 도달 가능한 지면을 찾습니다.
        bool CenterPath(out Vector3 destination,out NavMeshPath path)
        {
            destination=Arena;path=new NavMeshPath();
            for(int ring=0;ring<=3;ring++)for(int i=0;i<(ring==0?1:12);i++)
            {
                Vector3 point=Arena+Quaternion.Euler(0,i*30,0)*Vector3.forward*(centerSearchRadius*ring/3);
                NavMeshHit hit;
                if(NavMesh.SamplePosition(point,out hit,positionArrivalDistance,agent.areaMask)&&agent.CalculatePath(hit.position,path)&&path.status==NavMeshPathStatus.PathComplete)
                {destination=hit.position;return true;}
            }
            return false;
        }
        bool MaintainRangedSpacing(float distance)
        {
            if(Time.time>=rangedHoldUntil)return false;
            if(distance<rangedMinimumDistance&&Time.time>=nextRetreat)
            {busy=true;StartCoroutine(IdleRetreat());return true;}
            if(distance>rangedMaximumDistance&&Nav)
            {
                agent.isStopped=false;agent.speed=PhaseNumber==1?walkSpeed:rideMoveSpeed;
                if(Time.time>=nextPath){nextPath=Time.time+navUpdateInterval;agent.SetDestination(player.transform.position);}
                TravelPose();
            }
            else{StopMovement();PlayLocomotion(false);}
            return true;
        }
        IEnumerator IdleRetreat(){yield return MakeRangedSpace();State=CombatState.Idle;busy=false;}
    }
}

