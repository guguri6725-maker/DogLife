using UnityEngine;
namespace DogLife
{
    // HP가 소진됐을 때 다음 페이즈가 있는 대상만 실제 사망을 보류합니다.
    public abstract class CombatPhaseTransition : MonoBehaviour
    {
        public abstract bool TryBeginNextPhase();
    }
}
