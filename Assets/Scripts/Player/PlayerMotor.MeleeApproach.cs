using UnityEngine;

namespace DogLife
{
    public sealed partial class PlayerMotor
    {
        // 최대 거리만 보조합니다. 합성 무기도 접근 거리가 추가로 늘어나지 않습니다.
        [Header("근접 접근 보정")]
        public bool meleeApproachEnabled=true;
        [Min(0),Tooltip("모든 근접 무기의 기존 공격 범위 밖에서 추가로 탐색하는 거리 X. 이동 거리도 이 값을 넘지 않습니다.")]
        public float meleeApproachExtraRange=1.6f;
        [Range(0,45)] public float approachAngle=25;
        [Range(.05f,.2f)] public float approachDuration=.13f;
        Collider approachCollider;
        CombatHealth approachHealth;
        Vector3 approachStart,approachDirection;
        float approachDistance,approachElapsed,approachTime,approachReach;
        bool approachRush;
        readonly Collider[] approachCandidates=new Collider[96];

        public void CancelMeleeApproach(){approachCollider=null;approachHealth=null;approachDistance=0;approachElapsed=0;}
        bool MayApproach(Vector3 direction)
        {
            return meleeApproachEnabled&&body&&!body.isKinematic&&capsule&&Grounded&&!IsDashing&&!IsKnockedBack&&
                jumpBuffer<=Time.time&&body.linearVelocity.y<=.5f&&!(waterMovement&&waterMovement.Submerged)&&
                !SkillWindow.BlocksGameplayInput&&Vector3.Dot(MoveDirection,direction)>=-.05f&&
                (!readInput||Cursor.lockState==CursorLockMode.Locked);
        }
        public void BeginMeleeApproach(InventoryWeapon item,int step,float reach,Vector3 forward,float preparation)
        {
            CancelMeleeApproach();
            if(!item||step<0||step>=2)return;
            forward=Vector3.ProjectOnPlane(forward,Vector3.up).normalized;
            if(forward.sqrMagnitude<.5f||!MayApproach(forward))return;
            float limit=Mathf.Max(0,meleeApproachExtraRange);
            switch(item.weaponCategory)
            {
                case InventoryWeapon.WeaponCategory.Sword:
                case InventoryWeapon.WeaponCategory.Dagger:
                case InventoryWeapon.WeaponCategory.Axe:
                case InventoryWeapon.WeaponCategory.Spear:break;
                default:return;
            }
            if(limit<=0||preparation<.04f)return;
            float scale=BoneWeaponVfx.SizeFor(item);
            // 도끼는 세로 호의 실제 전방 길이, 나머지는 기본 궤적 끝을 기준으로 합니다.
            approachReach=item.weaponCategory==InventoryWeapon.WeaponCategory.Axe?reach*scale*.65f*(step==1?1.2f:1)+.2f:reach*scale;
            float width=item.weaponCategory==InventoryWeapon.WeaponCategory.Dagger?.08f:.2f*scale;
            approachReach+=width;
            var origin=capsule.bounds.center;
            // 낮은 토끼나 큰 보스도 동일한 수평 거리로 탐색합니다.
            var bounds=capsule.bounds;
            var low=new Vector3(origin.x,bounds.min.y,origin.z);var high=new Vector3(origin.x,bounds.max.y,origin.z);
            int count=Physics.OverlapCapsuleNonAlloc(low,high,approachReach+limit,approachCandidates,~0,QueryTriggerInteraction.Collide);
            var candidates=count==approachCandidates.Length?Physics.OverlapCapsule(low,high,approachReach+limit,~0,QueryTriggerInteraction.Collide):approachCandidates;
            if(candidates!=approachCandidates)count=candidates.Length;
            float best=float.PositiveInfinity,cone=Mathf.Cos(approachAngle*Mathf.Deg2Rad);
            for(int i=0;i<count;i++)
            {
                var candidate=candidates[i];
                if(!MonsterHurtbox.Accepts(candidate)||candidate.transform.IsChildOf(transform))continue;
                var health=candidate.GetComponentInParent<CombatHealth>();
                if(!health||health.player||health.Dead||health.invulnerable||!health.gameObject.activeInHierarchy)continue;
                // 구조물은 접근 대상에서 제외하되 연습 표적과 보스의 몸체는 허용합니다.
                if(!health.practiceTarget&&!health.GetComponent<MonsterHurtbox>()&&!candidate.GetComponent<MonsterHurtbox>())continue;
                var point=candidate.ClosestPoint(origin);
                if(candidate.bounds.max.y<bounds.min.y-.15f||candidate.bounds.min.y>bounds.max.y+.15f)continue;
                var delta=Vector3.ProjectOnPlane(point-origin,Vector3.up);float distance=delta.magnitude;
                if(distance<.01f)continue;
                float alignment=Vector3.Dot(delta/distance,forward);
                if(alignment<cone||distance>approachReach+limit)continue;
                // 검색 순서나 옆쪽의 가까운 적 대신 조준선에 가까운 몸체를 우선합니다.
                float score=(1-alignment)*4+distance*.03f;
                if(score>=best)continue;
                float needed=distance<=approachReach?0:ApproachTravel(distance,approachReach,limit);
                bool hidden=false;
                foreach(var hit in Physics.RaycastAll(origin,(point-origin).normalized,(point-origin).magnitude,~0,QueryTriggerInteraction.Ignore))
                    if(!hit.collider.transform.IsChildOf(transform)&&hit.collider.GetComponentInParent<CombatHealth>()!=health){hidden=true;break;}
                if(hidden)continue;
                if(needed>0&&(!ApproachPathClear(delta/distance,needed)||!ApproachSupported(delta/distance,needed)))continue;
                best=score;approachCollider=candidate;approachHealth=health;
                approachDirection=delta/distance;approachDistance=needed;
            }
            // 최종 선택한 대상이 이미 사거리 안이면 접근하지 않습니다.
            if(!approachCollider||approachDistance<=0){CancelMeleeApproach();return;}
            approachStart=body.position;approachElapsed=0;
            // 추가 거리 X는 1·2타 공통이며, 1타만 빠른 돌진 곡선을 사용합니다.
            approachRush=step==0;
            approachTime=Mathf.Min(approachDuration*(approachRush?.85f:1),preparation*.85f);
        }
        static float ApproachTravel(float distance,float range,float extra)
        {
            // 정확히 사거리+X까지 발동하며 경계에서도 실제 이동량은 X를 넘지 않습니다.
            float gap=distance-range;
            return extra>0&&distance>range&&distance<=range+extra+.00001f?Mathf.Min(gap+.04f,extra):0;
        }
        static float ApproachProgress(float progress,bool rush)
        {
            float t=Mathf.Clamp01(progress);
            if(!rush)return Mathf.SmoothStep(0,1,t);
            // 초반 20% 동안 가속하고 남은 구간에서 감속합니다. 출발·종료 속도는 모두 0입니다.
            return t<.2f?t*t/.2f:1-(1-t)*(1-t)/.8f;
        }
        bool ApproachPathClear(Vector3 direction,float distance)
        {
            var bounds=capsule.bounds;float radius=Mathf.Min(bounds.extents.x,bounds.extents.z);
            float half=Mathf.Max(0,bounds.extents.y-radius);
            var top=bounds.center+Vector3.up*half;var bottom=bounds.center-Vector3.up*half;
            foreach(var hit in Physics.CapsuleCastAll(top,bottom,radius,direction,distance+.05f,~0,QueryTriggerInteraction.Collide))
            {
                var c=hit.collider;
                if(!c||c.attachedRigidbody==body||c.transform.IsChildOf(transform))continue;
                if(c.isTrigger&&!c.GetComponent<MonsterHurtbox>())continue;
                if(hit.normal.y>GroundNormalY&&IsGroundCollider(c))continue;
                // 적 몸체는 레이어에서 충돌을 무시하더라도 보정 이동으로 통과하지 않습니다.
                return false;
            }
            return true;
        }
        bool ApproachSupported(Vector3 direction,float distance)
        {
            var bounds=capsule.bounds;var side=Vector3.Cross(Vector3.up,direction)*Mathf.Min(bounds.extents.x,bounds.extents.z)*.6f;
            int samples=Mathf.Max(1,Mathf.CeilToInt(distance/.1f));
            for(int i=0;i<=samples;i++)for(int j=-1;j<=1;j++)
            {
                var point=new Vector3(bounds.center.x,bounds.min.y+.2f,bounds.center.z)+direction*(distance*i/samples)+side*j;
                bool supported=false;
                foreach(var hit in Physics.RaycastAll(point,Vector3.down,.45f,groundMask.value|1,QueryTriggerInteraction.Ignore))
                    if(hit.normal.y>GroundNormalY&&IsGroundCollider(hit.collider)){supported=true;break;}
                if(!supported)return false;
            }
            return true;
        }
        Vector3 ResolveMeleeApproach(Vector3 horizontal)
        {
            if(!approachCollider||approachDistance<=0)return horizontal;
            if(!approachHealth||approachHealth.Dead||!approachCollider.enabled||!approachHealth.gameObject.activeInHierarchy||
                !MayApproach(approachDirection)||!attackCombo||!attackCombo.IsAttacking)
            {CancelMeleeApproach();return horizontal;}
            if(CombatFeedback.Paused(this))return Vector3.zero;
            var point=approachCollider.ClosestPoint(capsule.bounds.center);
            var toTarget=Vector3.ProjectOnPlane(point-capsule.bounds.center,Vector3.up);
            if(toTarget.magnitude<=approachReach||Vector3.Dot(toTarget.normalized,approachDirection)<Mathf.Cos(approachAngle*Mathf.Deg2Rad))
            {CancelMeleeApproach();return horizontal;}
            float elapsed=Mathf.Min(approachTime,approachElapsed+Time.fixedDeltaTime);
            float progressed=Vector3.Dot(body.position-approachStart,approachDirection);
            float travel=Mathf.Max(0,approachDistance*ApproachProgress(elapsed/approachTime,approachRush)-progressed);
            travel=Mathf.Min(travel,Mathf.Max(0,toTarget.magnitude-approachReach+.04f));
            var combined=horizontal-approachDirection*Vector3.Dot(horizontal,approachDirection)+approachDirection*(travel/Time.fixedDeltaTime);
            var displacement=combined*Time.fixedDeltaTime;
            if(displacement.sqrMagnitude>.000001f&&(!ApproachPathClear(displacement.normalized,displacement.magnitude)||!ApproachSupported(displacement.normalized,displacement.magnitude)))
            {CancelMeleeApproach();return Vector3.zero;}
            approachElapsed=elapsed;
            if(elapsed>=approachTime)CancelMeleeApproach();
            return combined;
        }
    }
}
