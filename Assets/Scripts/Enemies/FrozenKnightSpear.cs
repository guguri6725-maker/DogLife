using System;
using UnityEngine;
namespace DogLife
{
    // 발악의 창은 발사 때 방향을 고정하고 가장 먼저 맞은 결정/플레이어/벽을 처리합니다.
    public sealed class FrozenKnightSpear : MonoBehaviour
    {
        [SerializeField,Min(.01f)] float radius=.25f,tipReach=1.5f;
        bool launched;
        FrozenKnightVfx effect;
        void Awake(){effect=GetComponent<FrozenKnightVfx>();}
        FrozenKnightBoss owner;FrozenKnightSwordCore core;float speed,damage,remaining;
        public void Launch(FrozenKnightBoss source,FrozenKnightSwordCore crystal,float velocity,float amount,float distance)
        {owner=source;core=crystal;speed=velocity;damage=amount;remaining=distance;launched=true;transform.localScale=Vector3.one;if(effect){effect.Show(FrozenKnightVfx.Shape.Line,transform.position,transform.forward,3,.4f,FrozenKnightVfx.Theme.BladeFlight);effect.Activate();}}
        // 예고 중에는 창만 커지고 충돌 판정은 발사 순간부터 시작합니다.
        public void Prepare(FrozenKnightBoss source,float progress)
        {
            if(!owner&&effect)effect.Show(FrozenKnightVfx.Shape.Ring,transform.position,transform.forward,.6f,0,FrozenKnightVfx.Theme.BladeCharge);
            owner=source;transform.localScale=Vector3.one*Mathf.SmoothStep(.05f,1,progress);
            if(effect){effect.MovePose(transform.position,transform.forward);effect.SetWarningProgress(progress);}
        }
        void Update()
        {
            if(!owner||!owner.isActiveAndEnabled){Destroy(gameObject);return;}
            if(!launched)return;
            float step=Mathf.Min(speed*Time.deltaTime,remaining);
            var contacts=Physics.SphereCastAll(transform.position,radius,transform.forward,step+tipReach,~0,QueryTriggerInteraction.Collide);
            Array.Sort(contacts,(a,b)=>a.distance.CompareTo(b.distance));
            foreach(var contact in contacts)
            {
                // 검이 보스 계층에 있어도 소유자 무시 검사보다 먼저 별도 표적을 처리합니다.
                if(core&&contact.collider==core.HitArea){core.ReceiveSpear();Destroy(gameObject);return;}
                if(contact.collider.isTrigger)continue; // 검 전용 트리거 외의 범위 표시/센서는 통과합니다.
                if(contact.collider.transform.IsChildOf(owner.transform)||contact.collider.transform.IsChildOf(transform))continue;
                var hp=contact.collider.GetComponentInParent<CombatHealth>();
                if(hp&&hp.player){owner.DealHit(hp,damage,4,0,transform.position);Destroy(gameObject);return;}
                if(!contact.collider.isTrigger){Destroy(gameObject);return;}
            }
            transform.position+=transform.forward*step;remaining-=step;
            if(effect)effect.MovePose(transform.position,transform.forward);
            if(remaining<=0)Destroy(gameObject);
        }
    }
}
