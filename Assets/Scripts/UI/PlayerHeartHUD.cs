using UnityEngine;

namespace DogLife
{
    public sealed class PlayerHeartHUD : MonoBehaviour
    {
        [SerializeField] private CombatHealth health;
        [SerializeField] private HeartIcon[] hearts;
        [SerializeField] private Color filled=new Color(1,.32f,.5f,1);
        [SerializeField] private Color empty=new Color(.25f,.19f,.25f,1);
        void LateUpdate()
        {
            if(!health)return;
            for(int i=0;i<hearts.Length;i++)if(hearts[i])hearts[i].color=i<health.Current?filled:empty;
        }
    }
}
