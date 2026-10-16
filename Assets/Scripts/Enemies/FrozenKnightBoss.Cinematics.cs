using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace DogLife
{
    public sealed partial class FrozenKnightBoss
    {
        [Header("왕좌 / 입장 연출 — 원본 길이로 재생")]
        [SerializeField] AnimationClip throneIdle,throneRise,throneDraw1,throneDraw2,cinematicMount;
        [SerializeField,Range(0,1)] float drawGripTime=.68f;
        [SerializeField] Transform sheathedSwordSocket;
        [SerializeField,Min(.1f)] float introductionWalkSpeed=2.1f,introductionMoveTimeout=18;
        FrozenKnightCinematicStage cinematicStage;
        [Header("최후의 일격 / 사망 연출")]
        [SerializeField] AnimationClip mountedDeath;
        [SerializeField] FrozenIceBurst deathIceEffect;
        [SerializeField,Min(0)] float deathPoseHold=1.2f;
        [SerializeField,Range(0,1)] float deathIceMoment=.65f;
        [Header("검에서 퍼지는 빙판 연출")]
        [SerializeField,Min(.1f)] float iceSpreadDuration=3.2f;
        [SerializeField,Min(0)] float iceSpreadHold=.7f;
        bool deathCinematicStarted;
        public bool DeathCinematicPlaying=>deathCinematicStarted&&health&&!health.Dead;
        IEnumerator DeathCinematic()
        {
            // Damage 호출이 사망 보류 상태를 기록한 다음 프레임부터 연출합니다.
            yield return null;
            if(cinematicStage)cinematicStage.BeginDeath(player,transform);
            Play(horseAnimator,"Idle");horseAnimator.Update(0);horseAnimator.speed=0;
            StormSound(false);Play(knightAnimator,"MountedDeath");
            float duration=mountedDeath.length;bool emitted=false,deathSoundPlayed=false;
            for(float t=0;t<duration;t+=Time.deltaTime)
            {
                if(!deathSoundPlayed&&t>=duration*.25f){deathSoundPlayed=true;BossSound("Death_Fall",riderRoot);}
                if(!emitted&&t>=duration*deathIceMoment)
                {
                    emitted=true;
                    if(deathIceEffect){var fx=Instantiate(deathIceEffect,riderRoot.position+Vector3.up,Quaternion.identity);fx.Play(FrozenIceBurst.Mode.Chip,new Vector3(1.5f,2,1.5f),null);}
                }
                yield return null;
            }
            knightAnimator.speed=0;
            yield return new WaitForSeconds(deathPoseHold);
            if(cinematicStage)cinematicStage.End();
            // 이 시점에만 사망 이벤트를 보내 문 열림/방 클리어를 허용합니다.
            // 시체는 남기되 공격/이동/충돌은 종료합니다.
            foreach(var collider in GetComponentsInChildren<Collider>())collider.enabled=false;
            if(agent)agent.enabled=false;
            health.CompleteDeferredDeath(true);
        }
        bool awaitingIntroduction,introductionRunning,introductionSwordDrawn;
        [Header("군마 탑승 대기 위치 / 검 회피")]
        [SerializeField,Min(1)] float horseBoardingDistance=6,horseSwordClearance=3.5f,boardingStandDistance=2.4f;
        bool TryBoardingRoute(out Vector3 destination,out Vector3[] route)
        {
            destination=transform.position;route=null;
            var bounds=new Bounds(plantedSwordPosition,Vector3.zero);
            foreach(var r in swordRoot.GetComponentsInChildren<Renderer>())bounds.Encapsulate(r.bounds);
            Vector3 center=bounds.center;center.y=transform.position.y;
            // 긴 군마가 회전할 때도 머리/꼬리가 닿지 않도록 실제 메시의 수평 반경을 포함합니다.
            float horseRadius=horseSwordClearance;
            foreach(var r in horseRoot.GetComponentsInChildren<Renderer>())
                horseRadius=Mathf.Max(horseRadius,new Vector2(r.bounds.extents.x,r.bounds.extents.z).magnitude+GroundDistance(r.bounds.center,horseRoot.position)+.5f);
            float clearance=new Vector2(bounds.extents.x,bounds.extents.z).magnitude+horseRadius;
            Vector3 routeStart=horseRoot.position;bool exitFirst=GroundDistance(routeStart,center)<clearance;
            if(exitFirst)
            {
                // 이전 패턴이 검 가까이에서 끝났다면 먼저 바깥으로만 빠져나온 뒤 우회합니다.
                NavMeshHit exit;
                Vector3 outward=center+Flat(routeStart-center)*(clearance+1);
                if(!NavMesh.SamplePosition(outward,out exit,.7f,agent.areaMask)||GroundDistance(exit.position,center)<clearance||NavMesh.Raycast(routeStart,exit.position,out _,agent.areaMask))return false;
                routeStart=exit.position;
            }
            for(int i=0;i<horseEntryCandidates;i++)
            {
                Vector3 side=Quaternion.AngleAxis(i*360f/horseEntryCandidates,Vector3.up)*transform.right;
                Vector3 candidate=center+side*Mathf.Max(horseBoardingDistance,clearance+1);
                NavMeshHit point;
                if(!NavMesh.SamplePosition(candidate,out point,navSampleRadius,agent.areaMask)||GroundDistance(point.position,center)<clearance)continue;
                var path=ClearHorseRoute(routeStart,point.position,center,clearance,horseArrivalDistance);
                if(path==null)continue;
                if(exitFirst){var extended=new Vector3[path.Length+1];extended[0]=routeStart;path.CopyTo(extended,1);path=extended;}
                destination=point.position;route=path;return true;
            }
            return false;
        }
        public bool IntroductionRunning=>introductionRunning;
        public void BindCinematicStage(FrozenKnightCinematicStage stage){cinematicStage=stage;}
        void PrepareIntroduction()
        {
            if(!cinematicStage||!cinematicStage.Valid||!throneIdle||!throneRise||!throneDraw1||!throneDraw2)return;
            awaitingIntroduction=true;State=CombatState.Transition;health.invulnerable=true;StopMovement();agent.enabled=false;
            transform.SetPositionAndRotation(cinematicStage.seatedOrigin.position,cinematicStage.seatedOrigin.rotation);
            if(cinematicStage.horseWaitingOrigin)
            {
                // 왕좌 옆의 씬 기준점을 사용해 보스가 걸어 나가도 군마는 제자리에 둡니다.
                horseWaitingPosition=cinematicStage.horseWaitingOrigin.position;
                horseWaitingRotation=cinematicStage.horseWaitingOrigin.rotation;
                horseRoot.SetPositionAndRotation(horseWaitingPosition,horseWaitingRotation);horseWaiting=true;
            }
            // 발도 전에는 검을 손에서 분리해 등 뒤에 유지합니다.
            Play(knightAnimator,"ThroneIdle");Forward=transform.forward;
        }
        IEnumerator Introduction()
        {
            awaitingIntroduction=false;introductionRunning=true;busy=true;State=CombatState.Transition;Disarm();
            float previousStop=agent.stoppingDistance,previousSpeed=agent.speed;
            cinematicStage.Begin(player,transform,false);
            try
            {
                yield return new WaitForSeconds(cinematicStage.seatedHold);
                yield return Stage(CombatState.Transition,throneRise.length,"ThroneRise");
                // 왕좌 앞 접지 지점으로 나온 뒤 NavMesh 이동으로 돌려줍니다.
                Play(knightAnimator,"UnarmedWalk");Vector3 start=transform.position;
                float distance=Vector3.Distance(start,cinematicStage.standingOrigin.position),duration=distance/Mathf.Max(.1f,introductionWalkSpeed);
                for(float t=0;t<duration;t+=Time.deltaTime){transform.position=Vector3.Lerp(start,cinematicStage.standingOrigin.position,t/duration);yield return null;}
                transform.position=cinematicStage.standingOrigin.position;
                NavMeshHit floor;
                if(NavMesh.SamplePosition(transform.position,out floor,navSampleRadius,agent.areaMask))transform.position=floor.position;
                agent.enabled=true;float deadline=Time.time+introductionMoveTimeout;
                // 전투용 정지 거리(4.5m) 대신 연출의 도착 거리로 중앙까지 걷습니다.
                agent.stoppingDistance=positionArrivalDistance*.5f;
                if(Nav&&agent.SetDestination(Arena))
                {
                    agent.isStopped=false;agent.speed=introductionWalkSpeed;
                    while(Time.time<deadline)
                    {
                        if(!agent.pathPending&&(!agent.hasPath||agent.remainingDistance<=positionArrivalDistance))break;
                        // 경로 계산/감속 중 속도가 잠깐 떨어져도 보행 클립을 처음부터 다시 틀지 않습니다.
                        TravelPose();
                        yield return null;
                    }
                }
                // 검을 뽑기 전에는 무장 대기 자세가 섞이지 않도록 빈손 자세로 정지합니다.
                StopMovement();Aim(true);Play(knightAnimator,"ThroneStand");yield return new WaitForSeconds(.4f);
                // 중앙에 도착해 완전히 멈춘 다음 등 뒤 검을 뽑습니다.
                cinematicStage.ShowDraw();
                yield return Stage(CombatState.Transition,throneDraw1.length,"ThroneDraw1");
                yield return Stage(CombatState.Transition,throneDraw2.length,"ThroneDraw2");
                introductionSwordDrawn=true;
                Play(knightAnimator,"Idle");yield return new WaitForSeconds(.4f);
            }
            finally
            {
                StopMovement();agent.stoppingDistance=previousStop;agent.speed=previousSpeed;
                introductionRunning=false;cinematicStage.End();health.invulnerable=false;State=CombatState.Idle;busy=false;
                engaged=true;nextAttack=Time.time+initialDelay;
            }
        }
        void UpdateIntroductionSword()
        {
            if((!awaitingIntroduction&&!introductionRunning)||introductionSwordDrawn||MonsterAnimationPreview.IsPreviewing(this))return;
            var state=knightAnimator.GetCurrentAnimatorStateInfo(0);
            // 블렌드 중에는 새 발도 상태의 시간을 사용해 손에 붙은 검이 다시 등으로 튀지 않습니다.
            if(knightAnimator.IsInTransition(0))
            {
                var next=knightAnimator.GetNextAnimatorStateInfo(0);
                if(next.IsName("ThroneDraw1")||next.IsName("ThroneDraw2"))state=next;
            }
            string clip=state.IsName("ThroneDraw2")?"ThroneDraw2":state.IsName("ThroneDraw1")?"ThroneDraw1":"ThroneIdle";
            SampleIntroductionSword(clip,state.normalizedTime,swordRestPosition,swordRestRotation);
        }
        // 인게임과 미리보기가 동일한 손잡이 기준과 발도 접촉 시점을 사용합니다.
        public void SampleIntroductionSword(string clip,float progress,Vector3 restPosition,Quaternion restRotation)
        {
            if(!swordRoot||!knightAnimator)return;
            swordRoot.localPosition=restPosition;swordRoot.localRotation=restRotation;
            if(clip!="ThroneIdle"&&clip!="ThroneRise"&&clip!="ThroneStand"&&clip!="ThroneDraw1")return;
            if(clip=="ThroneDraw1"&&progress>=drawGripTime)return;
            if(!sheathedSwordSocket)return;
            // 등뼈 아래의 고정점으로 상체의 기울기와 회전을 함께 따릅니다.
            Vector3 backPosition=sheathedSwordSocket.TransformPoint(restPosition);
            Quaternion backRotation=sheathedSwordSocket.rotation*restRotation;
            float blend=clip=="ThroneDraw1"?Mathf.SmoothStep(0,1,Mathf.InverseLerp(drawGripTime-.10f,drawGripTime,progress)):0;
            swordRoot.SetPositionAndRotation(Vector3.Lerp(backPosition,swordRoot.position,blend),Quaternion.Slerp(backRotation,swordRoot.rotation,blend));
        }
        void ResetIntroductionSword()
        {
            if(!awaitingIntroduction&&!introductionRunning)return;
            swordRoot.localPosition=swordRestPosition;swordRoot.localRotation=swordRestRotation;
        }
    }
}
