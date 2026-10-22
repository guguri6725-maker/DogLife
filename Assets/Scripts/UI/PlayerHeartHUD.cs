using UnityEngine;
using UnityEngine.UI;

namespace DogLife
{
    public sealed class PlayerHeartHUD : MonoBehaviour
    {
        [SerializeField] private CombatHealth health;
        [SerializeField] private HeartIcon[] hearts;
        [SerializeField] private Text healthCount;
        [SerializeField] private Color filled=new Color(1,.32f,.5f,1);
        [SerializeField] private Color empty=new Color(.25f,.19f,.25f,1);
        // 배경과 장식은 프리팹 Hierarchy에서 제작하고 체력에 따른 표시만 갱신합니다.
        void LateUpdate()
        {
            if(!health)return;
            if(healthCount)healthCount.text=Mathf.CeilToInt(health.Current)+" / "+Mathf.CeilToInt(health.maxHealth);
            for(int i=0;i<hearts.Length;i++)if(hearts[i])hearts[i].color=i<health.Current?filled:empty;
        }
    }
}
