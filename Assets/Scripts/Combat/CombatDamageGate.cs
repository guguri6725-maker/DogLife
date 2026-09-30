using UnityEngine;
namespace DogLife
{
    // 방어를 가진 몬스터만 선택적으로 사용하는 피해 차단 지점.
    public abstract class CombatDamageGate : MonoBehaviour
    {
        public abstract bool BlocksDamage(float amount);
        // 타격 횟수로 부서지는 구조물만 피해량을 1회분으로 바꿉니다.
        public virtual float FilterDamage(float amount)=>amount;
    }
}
