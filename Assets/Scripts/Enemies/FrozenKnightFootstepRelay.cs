using UnityEngine;
namespace DogLife
{
    // Animator가 있는 자식에서 실제 발 접지 프레임의 이벤트를 받습니다.
    public sealed class FrozenKnightFootstepRelay : MonoBehaviour
    {
        FrozenKnightBoss boss;
        Transform left,right;
        void Awake()
        {
            boss=GetComponentInParent<FrozenKnightBoss>();
            foreach(var bone in GetComponentsInChildren<Transform>(true))
            {
                if(bone.name=="LeftFoot")left=bone;
                if(bone.name=="RightFoot")right=bone;
            }
        }
        public void KnightFootContact(AnimationEvent contact)
        {
            if(!boss||contact.animatorClipInfo.weight<.5f)return;
            var state=contact.animatorStateInfo;
            if(!state.IsName("Walk")&&!state.IsName("UnarmedWalk"))return;
            boss.KnightFootstep(contact.intParameter==0?left:right);
        }
    }
}
