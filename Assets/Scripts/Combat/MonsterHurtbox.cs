using UnityEngine;

namespace DogLife
{
    // 몸체 트리거만 피격 대상으로 표시합니다. 공격 범위/센서 트리거와 구분합니다.
    [DisallowMultipleComponent]
    public sealed class MonsterHurtbox : MonoBehaviour
    {
        public static bool Accepts(Collider collider)
        {
            return collider && (!collider.isTrigger || collider.GetComponent<MonsterHurtbox>());
        }
    }
}
